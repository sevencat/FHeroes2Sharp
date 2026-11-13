using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Fonts;

/// <summary>
/// Read only source of glyph sprites plus the metrics text layout needs. It is the seam between
/// the UI, which measures and draws text, and whatever produces the glyph pixels.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BitmapFont"/> is the original Heroes II implementation: one sprite per character,
/// single byte, taken from <c>FONT.ICN</c>/<c>SMALFONT.ICN</c>. <see cref="TrueTypeGlyphSource"/>
/// is the second provider for text the bitmap tables cannot express (CJK, anything above
/// <c>U+00FF</c>): the glyph is rasterized on demand by <c>Sevencat.GlyphRaster</c>, its coverage is
/// quantized to palette indices, and the advance comes from the face instead of the sprite offset.
/// Both live behind this interface so the layout code (<see cref="GlyphText"/>) is written once.
/// </para>
/// <para>
/// Characters are <see cref="char"/> (UTF-16 code units); supplementary plane characters arrive as
/// surrogate pairs and are not a single glyph in either provider.
/// </para>
/// </remarks>
public interface IGlyphSource
{
    /// <summary>Distance between two baselines, in logical pixels.</summary>
    int LineHeight { get; }

    /// <summary>Horizontal advance of a space, in logical pixels.</summary>
    int SpaceWidth { get; }

    /// <summary>Returns <see langword="true"/> when the character can be drawn.</summary>
    bool IsAvailable(char character);

    /// <summary>Horizontal advance of one character, in logical pixels.</summary>
    int GetAdvance(char character);

    /// <summary>
    /// Gets the glyph to draw. When the source has no glyph for <paramref name="character"/> the
    /// method returns <see langword="false"/> and yields the provider's substitute (the <c>?</c>
    /// glyph for <see cref="BitmapFont"/>).
    /// </summary>
    bool TryGetGlyph(char character, out Sprite glyph);
}
