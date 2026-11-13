using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Present;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FHeroes2.Client.Desktop.Graphics;

/// <summary>
/// MonoGame implementation of <see cref="IScreenPresenter"/>.
///
/// The indexed <see cref="ScreenBuffer"/> is uploaded into a 640x480 texture, composited into a
/// 640x480 render target (so overlays are drawn in logical pixel space) and finally blitted to the
/// back buffer with point sampling and letterboxing. Scaling is integer whenever the window is at
/// least 640x480, which keeps the pixel art crisp.
/// </summary>
public sealed class MonoGamePresenter : IScreenPresenter
{
    private readonly GraphicsDevice _device;
    private readonly RenderTarget2D _logicalTarget;
    private readonly Texture2D _frameTexture;
    private readonly SpriteBatch _spriteBatch;
    private readonly Color[] _paletteLookup = new Color[Palette.ColorCount];
    private readonly Color[] _conversionBuffer;

    private PresentationViewport _viewport;
    private bool _disposed;

    public MonoGamePresenter(GraphicsDevice device, Size logicalSize)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalSize.Width, nameof(logicalSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalSize.Height, nameof(logicalSize));

        _device = device;
        LogicalSize = logicalSize;

        _logicalTarget = new RenderTarget2D(
            device,
            logicalSize.Width,
            logicalSize.Height,
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.DiscardContents);

        _frameTexture = new Texture2D(device, logicalSize.Width, logicalSize.Height, false, SurfaceFormat.Color);
        _spriteBatch = new SpriteBatch(device);
        _conversionBuffer = new Color[logicalSize.Area];

        SetPalette(new Palette());
        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);
    }

    public Size LogicalSize { get; }

    public Size OutputSize => new(
        _device.PresentationParameters.BackBufferWidth,
        _device.PresentationParameters.BackBufferHeight);

    public PresentationViewport Viewport => _viewport;

    /// <summary>Precomputes the RGBA lookup table used when uploading indexed pixels.</summary>
    public void SetPalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        for (int i = 0; i < Palette.ColorCount; ++i)
        {
            RgbaColor color = palette[i];
            _paletteLookup[i] = new Color(color.R, color.G, color.B, color.A);
        }
    }

    /// <summary>Uploads the dirty region of the frame into the GPU texture.</summary>
    public void Upload(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        Rect dirty = frame.TakeDirtyRegion();
        if (dirty.IsEmpty)
        {
            return;
        }

        PixelBuffer pixels = frame.Pixels;
        Color[] lookup = _paletteLookup;
        Color[] destination = _conversionBuffer;

        int index = 0;
        for (int y = dirty.Y; y < dirty.Bottom; ++y)
        {
            ReadOnlySpan<byte> row = pixels.Row(y);
            for (int x = dirty.X; x < dirty.Right; ++x)
            {
                destination[index++] = lookup[row[x]];
            }
        }

        _frameTexture.SetData(0, new Rectangle(dirty.X, dirty.Y, dirty.Width, dirty.Height), destination, 0, index);
    }

    /// <summary>Binds the logical render target and clears it.</summary>
    public void BeginLogicalFrame()
    {
        _device.SetRenderTarget(_logicalTarget);
        _device.Clear(Color.Black);
    }

    /// <summary>Draws the uploaded frame into the logical render target (1:1 pixel mapping).</summary>
    public void DrawFrameTexture()
    {
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        _spriteBatch.Draw(
            _frameTexture,
            new Rectangle(0, 0, LogicalSize.Width, LogicalSize.Height),
            Color.White);

        _spriteBatch.End();
    }

    /// <summary>Presents the logical render target on the back buffer and recomputes the viewport.</summary>
    public void EndFrame()
    {
        _device.SetRenderTarget(null);
        _device.Clear(Color.Black);

        _viewport = PresentationViewport.Compute(LogicalSize, OutputSize);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        _spriteBatch.Draw(_logicalTarget, _viewport.Destination.ToXnaRectangle(), Color.White);

        _spriteBatch.End();
    }

    /// <summary>Convenience path used by tests and tools: upload, composite and present in one call.</summary>
    public void Present(ScreenBuffer frame)
    {
        Upload(frame);
        BeginLogicalFrame();
        DrawFrameTexture();
        EndFrame();
    }

    /// <summary>
    /// Saves the composed logical frame (frame + overlays) as a PNG. Used by the smoke tests and
    /// will back the golden-image tests of M1.
    /// </summary>
    public void SaveLogicalFrame(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(path);
        _logicalTarget.SaveAsPng(stream, LogicalSize.Width, LogicalSize.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _spriteBatch.Dispose();
        _frameTexture.Dispose();
        _logicalTarget.Dispose();
    }
}
