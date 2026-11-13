using System.Runtime.InteropServices;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Present;
using SDL3;

namespace FHeroes2.Client.Desktop.Graphics;

/// <summary>
/// SDL3 implementation of <see cref="IScreenPresenter"/>: owns the window, the renderer, the
/// streaming frame texture and the 640x480 logical render target.
/// </summary>
/// <remarks>
/// The indexed <see cref="ScreenBuffer"/> is converted into packed ABGR pixels
/// (<see cref="IndexedFrameConverter"/>) and uploaded as the dirty rectangle only. Overlays are
/// drawn into a 640x480 target texture so text keeps the logical pixel grid; the target is then
/// blitted to the window with nearest sampling and integer scaling plus letterboxing, which keeps
/// the pixel art crisp.
/// </remarks>
internal sealed class SdlPresenter : IScreenPresenter
{
    private readonly IntPtr _window;
    private readonly IntPtr _renderer;
    private readonly IntPtr _frameTexture;
    private readonly IntPtr _logicalTarget;
    private readonly uint[] _conversionBuffer;

    private uint[] _lookup;
    private PresentationViewport _viewport;
    private bool _disposed;

    public SdlPresenter(Size logicalSize, int windowScale, bool fullscreen)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalSize.Width, nameof(logicalSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalSize.Height, nameof(logicalSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowScale, nameof(logicalSize));

        LogicalSize = logicalSize;
        Size windowSize = logicalSize * windowScale;

        _window = SDL.CreateWindow(
            BuildInfo.DisplayName,
            windowSize.Width,
            windowSize.Height,
            SDL.WindowFlags.Resizable | SDL.WindowFlags.Hidden);

        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");
        }

        _renderer = SDL.CreateRenderer(_window, (string?)null);
        if (_renderer == IntPtr.Zero)
        {
            SDL.DestroyWindow(_window);
            throw new InvalidOperationException($"SDL_CreateRenderer failed: {SDL.GetError()}");
        }

        _frameTexture = SDL.CreateTexture(
            _renderer,
            SDL.PixelFormat.ABGR8888,
            SDL.TextureAccess.Streaming,
            logicalSize.Width,
            logicalSize.Height);

        _logicalTarget = SDL.CreateTexture(
            _renderer,
            SDL.PixelFormat.ABGR8888,
            SDL.TextureAccess.Target,
            logicalSize.Width,
            logicalSize.Height);

        if (_frameTexture == IntPtr.Zero || _logicalTarget == IntPtr.Zero)
        {
            Dispose();
            throw new InvalidOperationException($"SDL_CreateTexture failed: {SDL.GetError()}");
        }

        SDL.SetTextureScaleMode(_frameTexture, SDL.ScaleMode.Nearest);
        SDL.SetTextureBlendMode(_logicalTarget, SDL.BlendMode.None);

        _conversionBuffer = new uint[logicalSize.Area];
        _lookup = IndexedFrameConverter.BuildLookup(new Palette());

        if (fullscreen && !SDL.SetWindowFullscreen(_window, true))
        {
            GameLog.Warn($"Fullscreen could not be enabled: {SDL.GetError()}");
        }

        SDL.ShowWindow(_window);
        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);
    }

    /// <summary>Native window handle, needed by the event loop.</summary>
    public IntPtr Window => _window;

    /// <summary>Native renderer handle; only the text backend should need it.</summary>
    public IntPtr Renderer => _renderer;

    public Size LogicalSize { get; }

    public Size OutputSize
    {
        get
        {
            if (SDL.GetRenderOutputSize(_renderer, out int width, out int height) && width > 0 && height > 0)
            {
                return new Size(width, height);
            }

            return LogicalSize;
        }
    }

    public PresentationViewport Viewport => _viewport;

    public bool IsFullscreen => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Fullscreen) != 0;

    /// <summary>Precomputes the packed pixel lookup table used when uploading indexed pixels.</summary>
    public void SetPalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _lookup = IndexedFrameConverter.BuildLookup(palette);
    }

    /// <summary>Uploads the dirty region of the frame into the streaming texture.</summary>
    public void Upload(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Size != LogicalSize)
        {
            throw new ArgumentException($"The frame is {frame.Size} but the presenter expects {LogicalSize}.", nameof(frame));
        }

        Rect dirty = frame.TakeDirtyRegion();
        if (dirty.IsEmpty)
        {
            return;
        }

        Rect converted = IndexedFrameConverter.ConvertRegion(frame, dirty, _lookup, _conversionBuffer);
        if (converted.IsEmpty)
        {
            return;
        }

        SDL.Rect region = new() { X = converted.X, Y = converted.Y, W = converted.Width, H = converted.Height };
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(_conversionBuffer.AsSpan(0, converted.Area));

        if (!SDL.UpdateTexture(_frameTexture, in region, bytes, converted.Width * IndexedFrameConverter.BytesPerPixel))
        {
            throw new InvalidOperationException($"SDL_UpdateTexture failed: {SDL.GetError()}");
        }
    }

    /// <summary>Binds the 640x480 logical render target and clears it.</summary>
    public void BeginLogicalFrame()
    {
        if (!SDL.SetRenderTarget(_renderer, _logicalTarget))
        {
            throw new InvalidOperationException($"SDL_SetRenderTarget failed: {SDL.GetError()}");
        }

        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL.RenderClear(_renderer);
    }

    /// <summary>Draws the uploaded frame into the logical render target with 1:1 pixel mapping.</summary>
    public void DrawFrameTexture()
    {
        if (!SDL.RenderTexture(_renderer, _frameTexture, IntPtr.Zero, IntPtr.Zero))
        {
            throw new InvalidOperationException($"SDL_RenderTexture failed: {SDL.GetError()}");
        }
    }

    /// <summary>Blits the logical render target to the window and presents the frame.</summary>
    public void EndFrame()
    {
        if (!SDL.SetRenderTarget(_renderer, IntPtr.Zero))
        {
            throw new InvalidOperationException($"SDL_SetRenderTarget failed: {SDL.GetError()}");
        }

        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);

        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL.RenderClear(_renderer);

        if (!_viewport.Destination.IsEmpty)
        {
            SDL.FRect destination = new()
            {
                X = _viewport.Destination.X,
                Y = _viewport.Destination.Y,
                W = _viewport.Destination.Width,
                H = _viewport.Destination.Height,
            };

            if (!SDL.RenderTexture(_renderer, _logicalTarget, IntPtr.Zero, in destination))
            {
                throw new InvalidOperationException($"SDL_RenderTexture failed: {SDL.GetError()}");
            }
        }

        SDL.RenderPresent(_renderer);
    }

    /// <summary>Convenience path: upload, composite and present in one call.</summary>
    public void Present(ScreenBuffer frame)
    {
        Upload(frame);
        BeginLogicalFrame();
        DrawFrameTexture();
        EndFrame();
    }

    /// <summary>Switches between borderless fullscreen and the windowed mode.</summary>
    public void ToggleFullscreen()
    {
        bool fullscreen = !IsFullscreen;
        if (!SDL.SetWindowFullscreen(_window, fullscreen))
        {
            GameLog.Warn($"Fullscreen could not be changed: {SDL.GetError()}");
            return;
        }

        GameLog.Info(fullscreen ? "Switched to fullscreen." : "Switched to windowed mode.");
    }

    /// <summary>
    /// Reads the composed logical frame (indexed frame plus overlays) back from the render target
    /// and writes it as a PNG. Must run while the logical target is still bound, that is between
    /// <see cref="DrawFrameTexture"/> and <see cref="EndFrame"/>.
    /// </summary>
    public void SaveLogicalFrame(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        IntPtr raw = SDL.RenderReadPixels(_renderer, (SDL.Rect?)null);
        if (raw == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_RenderReadPixels failed: {SDL.GetError()}");
        }

        IntPtr converted = SDL.ConvertSurface(raw, SDL.PixelFormat.ABGR8888);
        SDL.DestroySurface(raw);

        if (converted == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_ConvertSurface failed: {SDL.GetError()}");
        }

        try
        {
            SDL.Surface surface = Marshal.PtrToStructure<SDL.Surface>(converted);
            if (surface.Width != LogicalSize.Width || surface.Height != LogicalSize.Height)
            {
                throw new InvalidOperationException(
                    $"The render target returned {surface.Width}x{surface.Height} instead of {LogicalSize}.");
            }

            // ABGR8888 is stored as R, G, B, A in memory, which is exactly the PNG byte order.
            int rowBytes = surface.Width * 4;
            byte[] rgba = new byte[rowBytes * surface.Height];
            for (int y = 0; y < surface.Height; ++y)
            {
                Marshal.Copy(IntPtr.Add(surface.Pixels, y * surface.Pitch), rgba, y * rowBytes, rowBytes);
            }

            using FileStream stream = File.Create(path);
            PngWriter.WriteRgba(stream, new PngImage(surface.Width, surface.Height, rgba));
        }
        finally
        {
            SDL.DestroySurface(converted);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_logicalTarget != IntPtr.Zero)
        {
            SDL.DestroyTexture(_logicalTarget);
        }

        if (_frameTexture != IntPtr.Zero)
        {
            SDL.DestroyTexture(_frameTexture);
        }

        if (_renderer != IntPtr.Zero)
        {
            SDL.DestroyRenderer(_renderer);
        }

        if (_window != IntPtr.Zero)
        {
            SDL.DestroyWindow(_window);
        }
    }
}
