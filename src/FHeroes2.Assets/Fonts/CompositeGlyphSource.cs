using FHeroes2.Core.Imaging;
using GlyphRaster;

namespace FHeroes2.Assets.Fonts;

/// <summary>
/// The source the UI actually draws with: a primary source that owns the layout (normally a
/// <see cref="BitmapFont"/>) plus a fallback that fills in the characters the primary cannot
/// express (normally a <see cref="TrueTypeGlyphSource"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Metrics come from the primary source only.</b> That is what keeps a mixed string on the
/// original 17 pixel line: the TrueType face is solved to that line height by
/// <see cref="TrueTypeGlyphSource.ForLineHeight"/> and never dictates the layout.
/// </para>
/// <para>
/// A character is drawn by the fallback only when the primary does not claim it, so Latin text
/// stays pixel identical to the original game and CJK text appears where the 96 glyph tables would
/// have printed <c>?</c>. Characters neither provider can express yield the <i>primary</i>
/// substitute, which is the bitmap <c>?</c>.
/// </para>
/// </remarks>
public sealed class CompositeGlyphSource : IGlyphSource
{
    /// <param name="primary">Source that decides metrics, advances and the substitute glyph.</param>
    /// <param name="fallback">Source used for characters <paramref name="primary"/> cannot draw.</param>
    public CompositeGlyphSource(IGlyphSource primary, IGlyphSource fallback)
    {
        Primary = primary ?? throw new ArgumentNullException(nameof(primary));
        Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    /// <summary>Source that owns the layout and draws every character it can express.</summary>
    public IGlyphSource Primary { get; }

    /// <summary>Source used for the remaining characters.</summary>
    public IGlyphSource Fallback { get; }

    /// <inheritdoc/>
    public int LineHeight => Primary.LineHeight;

    /// <inheritdoc/>
    public int SpaceWidth => Primary.SpaceWidth;

    /// <summary>
    /// Combines a bitmap font with a TrueType face solved to the same line height. The coverage
    /// ramp is derived from <see cref="BitmapFont.Palette"/>, so the rasterized characters inherit
    /// the colour of the ICN table (ADR-020).
    /// </summary>
    /// <param name="primary">The original font, e.g. <c>FONT.ICN</c> converted to yellow.</param>
    /// <param name="face">Rasterizer instance; share one per process.</param>
    /// <param name="ramp">
    /// Explicit coverage ramp, for a caller that wants a different contrast. Defaults to
    /// <see cref="GlyphCoverageRamp.Heroes2Text"/> converted with <paramref name="primary"/>'s palette.
    /// </param>
    public static CompositeGlyphSource ForBitmapFont(
        BitmapFont primary, TextFont face, GlyphCoverageRamp? ramp = null)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(face);

        GlyphCoverageRamp coverage = ramp ?? GlyphCoverageRamp.Heroes2Text.WithPalette(primary.Palette);

        return new CompositeGlyphSource(
            primary, TrueTypeGlyphSource.ForLineHeight(face, primary.LineHeight, coverage));
    }

    /// <inheritdoc/>
    public bool IsAvailable(char character) =>
        Primary.IsAvailable(character) || Fallback.IsAvailable(character);

    /// <summary>
    /// Horizontal advance of one character, taken from whichever provider draws it. A character
    /// neither provider has keeps the advance of the primary substitute, so measuring a string
    /// never disagrees with drawing it.
    /// </summary>
    public int GetAdvance(char character)
    {
        if (Primary.IsAvailable(character))
        {
            return Primary.GetAdvance(character);
        }

        return Fallback.IsAvailable(character)
            ? Fallback.GetAdvance(character)
            : Primary.GetAdvance(character);
    }

    /// <summary>
    /// Returns the glyph of whichever provider can draw <paramref name="character"/>. When neither
    /// can, the method returns <see langword="false"/> and yields the primary substitute glyph.
    /// </summary>
    public bool TryGetGlyph(char character, out Sprite glyph)
    {
        if (Primary.IsAvailable(character))
        {
            return Primary.TryGetGlyph(character, out glyph);
        }

        if (Fallback.TryGetGlyph(character, out glyph))
        {
            return true;
        }

        // Neither provider has the character: report the bitmap '?' so a fallback chain still ends
        // with the glyph the original game would have printed.
        return Primary.TryGetGlyph(character, out glyph);
    }
}
