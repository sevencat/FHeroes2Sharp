namespace FHeroes2.Core.Imaging;

/// <summary>
/// The recolouring palettes of the original engine, the equivalent of <c>PAL::PaletteType</c>
/// (<c>src/engine/pal.h</c>). The numeric values match the C++ enum.
/// </summary>
/// <remarks>
/// A table converts one palette index into another, so it changes the colours of an indexed
/// image while keeping its pixels. <see cref="PaletteTable"/> holds the data,
/// <see cref="ImageOps.ApplyPalette(PixelBuffer, System.ReadOnlySpan{byte})"/> applies it.
/// <c>PAL::PaletteType::CUSTOM</c> is left out: it marks a palette passed by the caller, which is
/// simply a table of your own.
/// </remarks>
public enum PaletteId
{
    /// <summary>Identity: every colour maps to itself. <c>PAL::GetPalette(STANDARD)</c>.</summary>
    Standard = 0,

    /// <summary>
    /// Yellow text: the grey text ramp <c>10..36</c> becomes the yellow ramp <c>114..130</c>, which
    /// is how <c>YELLOW_FONT</c> / <c>YELLOW_SMALLFONT</c> are built from <c>FONT.ICN</c>.
    /// </summary>
    YellowFont = 1,

    /// <summary>
    /// White text, <c>PAL::PaletteType::WHITE_FONT</c>. It keeps the grey ramp of <c>FONT.ICN</c>
    /// unchanged and exists so that the white and yellow faces share one code path.
    /// </summary>
    WhiteFont = 2,

    /// <summary>
    /// Gray text: the grey text ramp becomes the darker one, used by <c>GRAY_FONT</c> /
    /// <c>GRAY_SMALL_FONT</c> and by inactive list entries.
    /// </summary>
    GrayFont = 3,

    /// <summary>Red tint, e.g. the Blood Lust flash and the red route arrow.</summary>
    Red = 4,

    /// <summary>Stone grey: the petrified unit colour and grayed out (inactive) icons.</summary>
    Gray = 5,

    /// <summary>Brown tint, <c>PAL::PaletteType::BROWN</c>.</summary>
    Brown = 6,

    /// <summary>Tan tint, used when the puzzle map is revealed.</summary>
    Tan = 7,

    /// <summary>
    /// Maps the cycling color ranges onto their static duplicates, so an image can be drawn
    /// without the water / lava animation.
    /// </summary>
    NoCycle = 8,

    /// <summary>Horizontal mirror of a monster sprite: it swaps the light and shadow sides.</summary>
    MirrorImage = 9,

    /// <summary>Darkened sprite, which is how disabled buttons and scrollbars are drawn.</summary>
    Darkening = 10,

    /// <summary>Converts Good interface artwork into the Evil interface.</summary>
    GoodToEvilInterface = 11,

    /// <summary>Converts Good style buttons into Evil style buttons.</summary>
    GoodToEvilButton = 12,

    /// <summary>Purple tint, used for the random object and random monster variants.</summary>
    Purple = 13,
}
