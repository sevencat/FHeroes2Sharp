using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using GlyphRaster;

namespace FHeroes2.App.Fonts;

/// <summary>
/// The fonts the UI draws with, loaded once at startup: the original bitmap tables
/// (<c>FONT.ICN</c> / <c>SMALFONT.ICN</c>) plus a TrueType fallback for the characters they cannot
/// express.
/// </summary>
/// <remarks>
/// <para>
/// Everything the game shows is drawn into the indexed frame buffer through these sources, so the
/// palette - not a GPU shader - decides the final colour. <see cref="Yellow"/> is the
/// <c>YELLOW_FONT</c> conversion of <c>FONT.ICN</c>; its CJK fallback inherits that colour through
/// <see cref="CompositeGlyphSource.ForBitmapFont"/>.
/// </para>
/// <para>
/// Loading is best effort: without game data (or with a truncated archive) <see cref="TryLoad"/>
/// returns <see langword="null"/> and the client keeps its diagnostic overlay only.
/// </para>
/// </remarks>
public sealed class GameFonts
{
    /// <summary>Character used to find out whether the fallback face covers CJK at all.</summary>
    private const char ProbeCharacter = '中';

    private GameFonts(IGlyphSource small, IGlyphSource normal, IGlyphSource yellow, string description)
    {
        Small = small;
        Normal = normal;
        Yellow = yellow;
        SupportsCjk = normal.IsAvailable(ProbeCharacter);

        // The overlay has one line for the fonts, so the CJK state belongs in the description.
        Description = SupportsCjk ? $"{description} (CJK)" : $"{description} (no CJK)";
    }

    /// <summary><c>SMALFONT.ICN</c>: 8 pixel letters on an 11 pixel line.</summary>
    public IGlyphSource Small { get; }

    /// <summary><c>FONT.ICN</c> in its original grey, with CJK filled in if a fallback loaded.</summary>
    public IGlyphSource Normal { get; }

    /// <summary>
    /// <c>FONT.ICN</c> converted with <see cref="PaletteId.YellowFont"/>, the colour of the yellow
    /// menu text of the original game.
    /// </summary>
    public IGlyphSource Yellow { get; }

    /// <summary>Whether the fallback can draw Chinese/Japanese/Korean characters.</summary>
    public bool SupportsCjk { get; }

    /// <summary>Human readable list of what was loaded, used by the diagnostics overlay.</summary>
    public string Description { get; }

    /// <summary>
    /// Loads the bitmap fonts from <paramref name="bundle"/> and combines them with
    /// <paramref name="face"/> (the embedded WenQuanYi face by default).
    /// </summary>
    /// <returns><see langword="null"/> when the archives do not hold the font tables.</returns>
    public static GameFonts? TryLoad(AssetBundle? bundle, TextFont? face = null)
    {
        if (bundle is null)
        {
            return null;
        }

        try
        {
            BitmapFont normal = BitmapFont.Load(bundle, BitmapFontSize.Normal);
            BitmapFont small = BitmapFont.Load(bundle, BitmapFontSize.Small);

            // One conversion of FONT.ICN serves every yellow line of the UI.
            BitmapFont yellow = normal.WithPalette(PaletteId.YellowFont);

            TextFont? cjkFace = face ?? TryGetBuiltInFace();

            GameFonts fonts = new(
                Wrap(small, cjkFace),
                Wrap(normal, cjkFace),
                Wrap(yellow, cjkFace),
                Describe(normal, small, cjkFace));

            GameLog.Info($"Game fonts ready: {fonts.Description}.");

            return fonts;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or BinaryFormatException or ArgumentException or NotSupportedException)
        {
            GameLog.Warn($"The bitmap fonts could not be loaded: {exception.Message}");
            return null;
        }
    }

    private static IGlyphSource Wrap(BitmapFont font, TextFont? face) =>
        face is null ? font : CompositeGlyphSource.ForBitmapFont(font, face);

    private static TextFont? TryGetBuiltInFace()
    {
        try
        {
            return TextFont.BuiltIn;
        }
        catch (Exception exception)
        {
            // A broken embedded face must not take the whole client down: the bitmap fonts still work.
            GameLog.Warn($"The embedded CJK face could not be loaded: {exception.Message}");
            return null;
        }
    }

    private static string Describe(BitmapFont normal, BitmapFont small, TextFont? face)
    {
        string tables = $"{normal.SourceName} + {small.SourceName}";
        return face is null ? tables : $"{tables} + {face.FamilyName}";
    }
}
