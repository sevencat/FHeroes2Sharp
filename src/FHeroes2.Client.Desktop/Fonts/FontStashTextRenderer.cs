using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Text;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TextStyle = FHeroes2.Rendering.Text.TextStyle;
using Point = FHeroes2.Core.Geometry.Point;

namespace FHeroes2.Client.Desktop.Fonts;

/// <summary>
/// FontStashSharp backed <see cref="ITextRenderer"/>. Text is rasterized into a dynamic glyph
/// atlas, so any TrueType font (Latin and CJK) can be used without a content pipeline.
/// </summary>
public sealed class FontStashTextRenderer : ITextRenderer
{
    private const int AtlasSize = 2048;

    private readonly FontSystem _fontSystem;
    private readonly Dictionary<int, SpriteFontBase> _fonts = new();

    private bool _disposed;

    public FontStashTextRenderer(string fontPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);

        byte[] fontData = File.ReadAllBytes(fontPath);
        _fontSystem = new FontSystem(new FontSystemSettings
        {
            TextureWidth = AtlasSize,
            TextureHeight = AtlasSize,
        });

        _fontSystem.AddFont(fontData);
        FontPath = Path.GetFullPath(fontPath);
    }

    public string FontPath { get; }

    public string Name => $"FontStashSharp ({Path.GetFileName(FontPath)})";

    public SpriteFontBase GetFont(int pixelSize)
    {
        int size = Math.Clamp(pixelSize, 6, 96);
        if (!_fonts.TryGetValue(size, out SpriteFontBase? font))
        {
            font = _fontSystem.GetFont(size);
            _fonts[size] = font;
        }

        return font;
    }

    public TextMetrics Measure(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TextMetrics.Empty;
        }

        SpriteFontBase font = GetFont(style.PixelSize);
        Vector2 measured = font.MeasureString(text);

        return new TextMetrics(
            new Size((int)MathF.Ceiling(measured.X), (int)MathF.Ceiling(measured.Y)),
            (int)font.LineHeight,
            text.Length);
    }

    /// <summary>Draws a single line of text at a logical (game pixel) position.</summary>
    public void Draw(SpriteBatch batch, string text, Point position, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        SpriteFontBase font = GetFont(style.PixelSize);
        Vector2 origin = new(position.X, position.Y);

        if (style.Shadow)
        {
            font.DrawText(batch, text, origin + new Vector2(1.0f, 1.0f), Color.Black);
        }

        font.DrawText(batch, text, origin, ToXnaColor(style.EffectiveColor));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fonts.Clear();
        _fontSystem.Dispose();
    }

    private static Color ToXnaColor(RgbaColor color) => new(color.R, color.G, color.B, color.A);
}
