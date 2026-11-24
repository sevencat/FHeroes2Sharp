using System.Runtime.InteropServices;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Present;
using SDL3;

namespace FHeroes2.App.Graphics;

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
    private readonly uint[] _conversionBuffer;
    private readonly WindowIcon? _windowIcon;

    private IntPtr _renderer;
    private IntPtr _frameTexture;
    private IntPtr _logicalTarget;

    private uint[] _lookup;
    private Palette? _paletteInUse;
    private PresentationViewport _viewport;
    private bool _disposed;

    /// <summary>Creates the window, its renderer and the streaming texture.</summary>
    /// <param name="logicalSize">Size of the frame buffer every scene draws into.</param>
    /// <param name="windowScale">Magnification of the frame buffer in the window.</param>
    /// <param name="fullscreen">Whether the window starts full screen.</param>
    /// <param name="windowPosition">
    /// Where to put the window, or <see langword="null"/> to let the window manager decide. It is applied while the
    /// window is still hidden, so it never shows up at the wrong place first (the <c>Display::setWindowPos</c> of
    /// <c>game_init.cpp:96</c>).
    /// </param>
    public SdlPresenter(Size logicalSize, int windowScale, bool fullscreen, Point? windowPosition = null)
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

        try
        {
            CreateRendererResources();
        }
        catch
        {
            Dispose();
            throw;
        }

        _conversionBuffer = new uint[logicalSize.Area];
        _paletteInUse = new Palette();
        _lookup = IndexedFrameConverter.BuildLookup(_paletteInUse);

        // The window is still hidden here, so the icon is already in place when it appears.
        _windowIcon = WindowIcon.TryLoad();
        _windowIcon?.Apply(_window);

        if (fullscreen && !SDL.SetWindowFullscreen(_window, true))
        {
            GameLog.Warn($"Fullscreen could not be enabled: {SDL.GetError()}");
        }

        if (windowPosition is { } position && !SDL.SetWindowPosition(_window, position.X, position.Y))
        {
            GameLog.Warn($"The window could not be moved to [ {position.X}, {position.Y} ]: {SDL.GetError()}");
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

    /// <summary>
    /// Where the window is on the desktop, the <c>Display::getWindowPos()</c> of <c>game_mainmenu.cpp:239</c>.
    /// </summary>
    /// <remarks>
    /// <c>(-1, -1)</c> when the position cannot be read, which is the value the configuration file starts with.
    /// </remarks>
    public Point WindowPosition =>
        SDL.GetWindowPosition(_window, out int x, out int y) ? new Point(x, y) : new Point(-1, -1);

    /// <summary>Uploads the dirty region of the frame into the streaming texture.</summary>
    public void Upload(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Size != LogicalSize)
        {
            throw new ArgumentException($"The frame is {frame.Size} but the presenter expects {LogicalSize}.", nameof(frame));
        }

        EnsureLookup(frame);

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

    /// <summary>
    /// Keeps the index to RGBA lookup in sync with the palette the frame is drawn with, the port of the
    /// palette refresh inside <c>Display::changePalette()</c> (<c>src/engine/screen.cpp:1622-1630</c>).
    /// </summary>
    /// <remarks>
    /// 参考换调色板就让引擎刷 GPU 的调色板表；端口在上传时把索引落成 RGBA，所以这里记住查找表是用哪份
    /// 调色板算出来的，画面换了调色板就重算。<see cref="ScreenBuffer.ChangePalette"/> 已经把整屏标脏，
    /// 于是换完调色板的那一帧会把全部像素重新上传。
    /// </remarks>
    private void EnsureLookup(ScreenBuffer frame)
    {
        if (ReferenceEquals(frame.Palette, _paletteInUse))
        {
            return;
        }

        _paletteInUse = frame.Palette;
        _lookup = IndexedFrameConverter.BuildLookup(_paletteInUse);
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

    /// <summary>
    /// Creates the renderer and the two textures it owns, or re-creates them after a reset.
    /// </summary>
    /// <exception cref="InvalidOperationException">SDL refused a surface.</exception>
    private void CreateRendererResources()
    {
        _renderer = SDL.CreateRenderer(_window, (string?)null);
        if (_renderer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateRenderer failed: {SDL.GetError()}");
        }

        _frameTexture = SDL.CreateTexture(
            _renderer,
            SDL.PixelFormat.ABGR8888,
            SDL.TextureAccess.Streaming,
            LogicalSize.Width,
            LogicalSize.Height);

        _logicalTarget = SDL.CreateTexture(
            _renderer,
            SDL.PixelFormat.ABGR8888,
            SDL.TextureAccess.Target,
            LogicalSize.Width,
            LogicalSize.Height);

        if (_frameTexture == IntPtr.Zero || _logicalTarget == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateTexture failed: {SDL.GetError()}");
        }

        SDL.SetTextureScaleMode(_frameTexture, SDL.ScaleMode.Nearest);
        SDL.SetTextureBlendMode(_logicalTarget, SDL.BlendMode.None);
    }

    /// <summary>Releases the renderer and both textures, leaving the window in place.</summary>
    private void ReleaseRendererResources()
    {
        if (_logicalTarget != IntPtr.Zero)
        {
            SDL.DestroyTexture(_logicalTarget);
            _logicalTarget = IntPtr.Zero;
        }

        if (_frameTexture != IntPtr.Zero)
        {
            SDL.DestroyTexture(_frameTexture);
            _frameTexture = IntPtr.Zero;
        }

        if (_renderer != IntPtr.Zero)
        {
            SDL.DestroyRenderer(_renderer);
            _renderer = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Switches between borderless full screen and the windowed mode, <c>engine().setFullScreen()</c>.
    /// </summary>
    /// <param name="enable">Whether the window has to become full screen.</param>
    /// <remarks>A refused switch keeps the current mode and is only logged, as in the reference.</remarks>
    public void SetFullscreen(bool enable)
    {
        if (IsFullscreen == enable)
        {
            return;
        }

        if (!SDL.SetWindowFullscreen(_window, enable))
        {
            GameLog.Warn($"Fullscreen could not be changed: {SDL.GetError()}");
            return;
        }

        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);
        GameLog.Info(enable ? "Switched to fullscreen." : "Switched to windowed mode.");
    }

    /// <summary>
    /// Resizes the window to a resolution, <c>Display::setResolution()</c>.
    /// </summary>
    /// <param name="resolution">The resolution to switch to; its frame buffer must be <see cref="LogicalSize"/>.</param>
    /// <exception cref="ArgumentException">The resolution changes the logic size, which this presenter cannot do.</exception>
    /// <remarks>
    /// The port keeps a fixed 640x480 frame buffer and scales it into the window, so only the window half of the
    /// resolution is applied; a refused resize keeps the current size and is only logged.
    /// </remarks>
    public void SetResolution(ResolutionInfo resolution)
    {
        if (resolution.GameSize != LogicalSize)
        {
            throw new ArgumentException(
                $"The presenter draws in {LogicalSize} and cannot switch to the logic size {resolution.GameSize}.",
                nameof(resolution));
        }

        if (resolution.ScreenSize == OutputSize)
        {
            return;
        }

        if (!SDL.SetWindowSize(_window, resolution.ScreenWidth, resolution.ScreenHeight))
        {
            GameLog.Warn($"The window could not be resized: {SDL.GetError()}");
            return;
        }

        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);
    }

    /// <summary>
    /// Rebuilds the renderer and its textures after a scaling change, <c>Display::resetRenderer()</c>.
    /// </summary>
    public void ResetRenderer()
    {
        ReleaseRendererResources();
        CreateRendererResources();
        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);
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

        ReleaseRendererResources();

        if (_window != IntPtr.Zero)
        {
            SDL.DestroyWindow(_window);
        }

        // SDL keeps referencing the icon surfaces while the window exists, so they are released
        // only after the window is gone.
        _windowIcon?.Dispose();
    }
}
