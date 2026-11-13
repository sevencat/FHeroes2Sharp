namespace FHeroes2.Assets.Fonts;

/// <summary>
/// Size of the original bitmap fonts, the equivalent of <c>fheroes2::FontSize</c> minus the
/// button-only variants. The numeric order matches the C++ enum for the shared values.
/// </summary>
public enum BitmapFontSize
{
    /// <summary>Eight pixel tall letters taken from <c>SMALFONT.ICN</c> (line height 11).</summary>
    Small,

    /// <summary>Thirteen pixel tall letters taken from <c>FONT.ICN</c> (line height 17).</summary>
    Normal,

    /// <summary>
    /// Twenty-six pixel tall letters (line height 33). The original engine builds this one at run
    /// time by scaling <c>FONT.ICN</c>, so there is no archive entry to load.
    /// </summary>
    Large,
}
