using System.Runtime.InteropServices;
using System.Text;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Text;
using SDL3;

namespace FHeroes2.App.Fonts;

/// <summary>
/// SDL_ttf backed <see cref="ITextRenderer"/>. FreeType rasterizes the glyphs into blended surfaces
/// which are uploaded as point sampled textures and drawn 1:1 into the 640x480 logical frame, so
/// the text keeps the game's pixel grid instead of being scaled by the window factor.
/// </summary>
/// <remarks>
/// Every text run turns into its own texture. The textures are cached per (text, size, color) and
/// released as soon as a whole frame went by without drawing them, which keeps the changing FPS
/// counter from growing the cache without bound.
/// </remarks>
public sealed class SdlTextRenderer : ITextRenderer
{
    private const int MinimumPixelSize = 6;
    private const int MaximumPixelSize = 96;

    /// <summary>
    /// Coverage values at or above this threshold become fully opaque; everything below becomes
    /// transparent. Only used when <c>FHEROES2_TEXT_CRISP=1</c> is set: the quantization turns the
    /// antialiased glyphs into hard edges, which is exactly right for a font with embedded bitmap
    /// strikes (SimSun at 12 px), but destroys thin strokes of outlines-only fonts.
    /// </summary>
    private const byte AlphaThreshold = 128;

    /// <summary>
    /// Full hinting snaps stems onto the pixel grid, which is what a 640x480 pixel art UI needs.
    /// Subpixel hinting is deliberately avoided: it produces colored fringes.
    /// </summary>
    private const TTF.HintingFlags Hinting = TTF.HintingFlags.Normal;

    private readonly IntPtr _renderer;
    private readonly Dictionary<int, IntPtr> _fonts = new();
    private readonly Dictionary<TextKey, GlyphRun> _runs = new();
    private readonly bool _crispText = IsCrispTextRequested();

    private long _frame;
    private bool _disposed;

    public SdlTextRenderer(IntPtr renderer, string fontPath)
    {
        if (renderer == IntPtr.Zero)
        {
            throw new ArgumentException("A valid SDL renderer is required.", nameof(renderer));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);

        _renderer = renderer;
        FontPath = Path.GetFullPath(fontPath);

        // Fail here rather than in the middle of a frame when the font file cannot be parsed.
        IntPtr probe = TTF.OpenFont(FontPath, MinimumPixelSize);
        if (probe == IntPtr.Zero)
        {
            throw new InvalidOperationException($"TTF_OpenFont('{FontPath}') failed: {SDL.GetError()}");
        }

        TTF.CloseFont(probe);
    }

    public string FontPath { get; }

    public string Name => $"SDL_ttf ({Path.GetFileName(FontPath)})";

    public TextMetrics Measure(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TextMetrics.Empty;
        }

        IntPtr font = GetFont(ClampPixelSize(style.PixelSize));
        byte[] utf8 = Encoding.UTF8.GetBytes(text);

        if (!TTF.GetStringSize(font, utf8, out int width, out int height))
        {
            return TextMetrics.Empty;
        }

        return new TextMetrics(new Size(width, height), TTF.GetFontHeight(font), text.Length);
    }

    /// <summary>
    /// Starts a new frame; textures that were not used during the previous frame are released.
    /// </summary>
    public void BeginFrame()
    {
        ++_frame;

        if (_runs.Count == 0)
        {
            return;
        }

        List<TextKey>? stale = null;
        foreach (KeyValuePair<TextKey, GlyphRun> pair in _runs)
        {
            if (pair.Value.LastFrame + 1 < _frame)
            {
                (stale ??= []).Add(pair.Key);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (TextKey key in stale)
        {
            SDL.DestroyTexture(_runs[key].Texture);
            _runs.Remove(key);
        }
    }

    /// <summary>Draws a single line of text at a logical (game pixel) position.</summary>
    public void Draw(string text, in Point position, in TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int pixelSize = ClampPixelSize(style.PixelSize);

        if (style.Shadow)
        {
            DrawRun(text, pixelSize, new Point(position.X + 1, position.Y + 1), RgbaColor.Black);
        }

        DrawRun(text, pixelSize, position, style.EffectiveColor);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (GlyphRun run in _runs.Values)
        {
            SDL.DestroyTexture(run.Texture);
        }

        _runs.Clear();

        foreach (IntPtr font in _fonts.Values)
        {
            TTF.CloseFont(font);
        }

        _fonts.Clear();
    }

    private void DrawRun(string text, int pixelSize, Point position, RgbaColor color)
    {
        GlyphRun run = GetRun(text, pixelSize, color);

        SDL.FRect destination = new()
        {
            X = position.X,
            Y = position.Y,
            W = run.Width,
            H = run.Height,
        };

        if (!SDL.RenderTexture(_renderer, run.Texture, IntPtr.Zero, in destination))
        {
            throw new InvalidOperationException($"SDL_RenderTexture failed: {SDL.GetError()}");
        }
    }

    private GlyphRun GetRun(string text, int pixelSize, RgbaColor color)
    {
        TextKey key = new(text, pixelSize, color.ToPackedAbgr());
        if (_runs.TryGetValue(key, out GlyphRun? cached))
        {
            cached.LastFrame = _frame;
            return cached;
        }

        IntPtr font = GetFont(pixelSize);
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        SDL.Color foreground = new() { R = color.R, G = color.G, B = color.B, A = color.A };

        IntPtr surface = TTF.RenderTextBlended(font, utf8, foreground);
        if (surface == IntPtr.Zero)
        {
            throw new InvalidOperationException($"TTF_RenderText_Blended failed: {SDL.GetError()}");
        }

        try
        {
            SDL.Surface info = Marshal.PtrToStructure<SDL.Surface>(surface);
            IntPtr texture = CreateTexture(surface);

            GlyphRun run = new(texture, info.Width, info.Height, _frame);
            _runs[key] = run;
            return run;
        }
        finally
        {
            SDL.DestroySurface(surface);
        }
    }

    /// <summary>Uploads a blended text surface, optionally quantizing the coverage into hard edges.</summary>
    private IntPtr CreateTexture(IntPtr surface)
    {
        if (!_crispText)
        {
            return UploadSurface(surface);
        }

        IntPtr crisp = SDL.ConvertSurface(surface, SDL.PixelFormat.ABGR8888);
        if (crisp == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_ConvertSurface failed: {SDL.GetError()}");
        }

        try
        {
            QuantizeAlpha(crisp);
            return UploadSurface(crisp);
        }
        finally
        {
            SDL.DestroySurface(crisp);
        }
    }

    private IntPtr UploadSurface(IntPtr surface)
    {
        IntPtr texture = SDL.CreateTextureFromSurface(_renderer, surface);
        if (texture == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateTextureFromSurface failed: {SDL.GetError()}");
        }

        SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Nearest);
        SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);

        return texture;
    }

    /// <summary>Replaces the 8 bit coverage of an ABGR8888 surface with a 1 bit mask.</summary>
    private static void QuantizeAlpha(IntPtr surface)
    {
        SDL.Surface info = Marshal.PtrToStructure<SDL.Surface>(surface);
        int rowBytes = info.Width * 4;
        byte[] row = new byte[rowBytes];

        for (int y = 0; y < info.Height; ++y)
        {
            IntPtr target = IntPtr.Add(info.Pixels, y * info.Pitch);
            Marshal.Copy(target, row, 0, rowBytes);

            // ABGR8888 keeps the alpha in the fourth byte of every pixel.
            for (int offset = 3; offset < rowBytes; offset += 4)
            {
                row[offset] = row[offset] >= AlphaThreshold ? (byte)255 : (byte)0;
            }

            Marshal.Copy(row, 0, target, rowBytes);
        }
    }

    private IntPtr GetFont(int pixelSize)
    {
        if (_fonts.TryGetValue(pixelSize, out IntPtr cached))
        {
            return cached;
        }

        IntPtr font = TTF.OpenFont(FontPath, pixelSize);
        if (font == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"TTF_OpenFont('{FontPath}', {pixelSize}) failed: {SDL.GetError()}");
        }

        TTF.SetFontHinting(font, Hinting);
        TTF.SetFontKerning(font, true);
        _fonts[pixelSize] = font;

        return font;
    }

    private static int ClampPixelSize(int pixelSize) => Math.Clamp(pixelSize, MinimumPixelSize, MaximumPixelSize);

    /// <summary>Reads <c>FHEROES2_TEXT_CRISP</c>; when set the glyph coverage is quantized.</summary>
    private static bool IsCrispTextRequested()
    {
        string? value = Environment.GetEnvironmentVariable("FHEROES2_TEXT_CRISP");
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct TextKey(string Text, int PixelSize, uint PackedColor);

    private sealed class GlyphRun(IntPtr texture, int width, int height, long lastFrame)
    {
        public IntPtr Texture { get; } = texture;

        public int Width { get; } = width;

        public int Height { get; } = height;

        public long LastFrame { get; set; } = lastFrame;
    }
}
