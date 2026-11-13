using FHeroes2.Core.Imaging;
using GlyphRaster;

namespace FHeroes2.Assets.Fonts;

/// <summary>
/// A TrueType backed <see cref="IGlyphSource"/>: the second provider next to
/// <see cref="BitmapFont"/>, for text the 96 glyph ICN tables cannot express (CJK, anything above
/// <c>U+00FF</c>, and any string a translation catalogue hands over as UTF-8).
/// </summary>
/// <remarks>
/// <para>
/// Glyphs are rasterized by <c>Sevencat.GlyphRaster</c>, a pure managed TrueType rasterizer with an
/// embedded CJK font (WenQuanYi Micro Hei) that needs neither SDL nor a font installed on the
/// machine. Each coverage mask is converted once into a <see cref="Sprite"/> whose pixels are
/// palette indices taken from a <see cref="GlyphCoverageRamp"/> and whose transform layer marks
/// uncovered pixels as "skip", so the glyph plugs into the same
/// <see cref="ImageOps.BlitSprite(PixelBuffer, Sprite)"/> path as the original font.
/// </para>
/// <para>
/// <b>Coordinate convention.</b> The rasterizer reports <c>OffsetY</c> relative to the baseline
/// while <see cref="BitmapFont"/> anchors a glyph at the top left corner of the text cell (the
/// <c>y</c> passed to <c>DrawText</c>). The sprite offset therefore has <see cref="Ascent"/> added
/// to it, which makes both providers accept the same origin - this is the one place where the two
/// coordinate systems meet.
/// </para>
/// <para>
/// Sprites are cached per character, so repeated frames only pay for the layout. The instance is
/// not thread safe, exactly like <see cref="BitmapFont"/>; the UI draws text from one thread.
/// </para>
/// </remarks>
public sealed class TrueTypeGlyphSource : IGlyphSource
{
    private const char SpaceCharacter = ' ';
    private const char LineSeparator = '\n';
    private const char FallbackCharacter = '?';

    private readonly TextFont _font;
    private readonly float _emSize;
    private readonly Dictionary<char, Sprite> _glyphs = [];

    /// <param name="font">Rasterizer instance; share one per process, parsing a TTF is not cheap.</param>
    /// <param name="emSize">Em height in pixels, not the ink height of a glyph.</param>
    /// <param name="ramp">Coverage to palette mapping; defaults to <see cref="GlyphCoverageRamp.Heroes2Text"/>.</param>
    public TrueTypeGlyphSource(TextFont font, float emSize, GlyphCoverageRamp? ramp = null)
    {
        ArgumentNullException.ThrowIfNull(font);

        if (!(emSize > 0f) || float.IsInfinity(emSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(emSize), emSize, "The em size must be a positive pixel count.");
        }

        _font = font;
        _emSize = emSize;
        Ramp = ramp ?? GlyphCoverageRamp.Heroes2Text;

        // GetAscent(), GetLineHeight() and the mask offsets are floats; the sprite grid is integral,
        // so the ascent is rounded once and every glyph is shifted by the same value.
        Ascent = Math.Max(0, (int)MathF.Round(font.GetAscent(emSize)));
        LineHeight = Math.Max(1, (int)MathF.Round(font.GetLineHeight(emSize)));
        SpaceWidth = Math.Max(0, font.GetGlyphMask(SpaceCharacter, emSize).Advance);
    }

    /// <summary>
    /// Creates a source whose <see cref="LineHeight"/> equals <paramref name="lineHeight"/>, so
    /// TrueType text sits on the same baseline grid as a <see cref="BitmapFont"/> (17 pixels for
    /// the normal size, 11 for the small one).
    /// </summary>
    /// <exception cref="ArgumentException">The font reports a non positive line height.</exception>
    public static TrueTypeGlyphSource ForLineHeight(TextFont font, int lineHeight, GlyphCoverageRamp? ramp = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineHeight);

        // Every metric of TextFont is linear in the em size, so one unit gives the scale factor.
        float unitLineHeight = font.GetLineHeight(1f);
        if (!(unitLineHeight > 0f))
        {
            throw new ArgumentException("The font reports a non positive line height.", nameof(font));
        }

        return new TrueTypeGlyphSource(font, lineHeight / unitLineHeight, ramp);
    }

    /// <summary>Font family of the rasterized face, for diagnostics.</summary>
    public string FamilyName => _font.FamilyName;

    /// <summary>Em height in pixels the glyphs are rasterized at.</summary>
    public float EmSize => _emSize;

    /// <summary>Baseline offset from the top of the text cell.</summary>
    public int Ascent { get; }

    /// <summary>Coverage to palette mapping used for every glyph.</summary>
    public GlyphCoverageRamp Ramp { get; }

    /// <inheritdoc/>
    public int LineHeight { get; }

    /// <inheritdoc/>
    public int SpaceWidth { get; }

    /// <inheritdoc/>
    public bool IsAvailable(char character) =>
        character != LineSeparator && !char.IsControl(character) && _font.HasGlyph(character);

    /// <summary>
    /// Horizontal advance of one character in pixels, taken from the font instead of the sprite
    /// bounding box: an outline glyph only touches a fraction of its advance width. A line
    /// separator advances nothing.
    /// </summary>
    public int GetAdvance(char character)
    {
        if (character == LineSeparator)
        {
            return 0;
        }

        return _font.GetGlyphMask(character, _emSize).Advance;
    }

    /// <inheritdoc/>
    int IGlyphSource.GetAdvance(char character) => GetAdvance(character);

    /// <summary>
    /// Returns the glyph for <paramref name="character"/>, rasterizing and caching it on first use.
    /// Characters the face has no glyph for return the <c>?</c> substitute.
    /// </summary>
    public Sprite GetSprite(char character) =>
        _glyphs.TryGetValue(character, out Sprite? cached)
            ? cached
            : _glyphs[character] = Rasterize(character);

    /// <inheritdoc/>
    public bool TryGetGlyph(char character, out Sprite glyph)
    {
        bool available = IsAvailable(character);
        glyph = GetSprite(available ? character : FallbackCharacter);
        return available;
    }

    private Sprite Rasterize(char character)
    {
        GlyphMask mask = _font.GetGlyphMask(character, _emSize);
        if (mask.IsEmpty)
        {
            // Space and other blank glyphs still advance the pen, they just have nothing to draw.
            return new Sprite(0, 0);
        }

        Sprite sprite = new(mask.Width, mask.Height, mask.OffsetX, mask.OffsetY + Ascent);
        byte[] transform = sprite.Transform!;
        byte[] pixels = sprite.Image.Pixels;

        for (int index = 0; index < transform.Length; ++index)
        {
            if (Ramp.TryMap(mask.Coverage[index], out byte paletteIndex))
            {
                pixels[index] = paletteIndex;
            }
            else
            {
                transform[index] = 1;
            }
        }

        return sprite;
    }
}
