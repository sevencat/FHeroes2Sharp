using FHeroes2.App.Fonts;
using FHeroes2.Core.Diagnostics;

namespace FHeroes2.App.Localization;

/// <summary>
/// The installed interface languages and which of them the preferences point at, <c>Settings::getGameLanguage()</c>.
/// </summary>
/// <remarks>
/// 这两步原本是 <c>MainMenuScene</c> 的私有方法；编辑器选项页的语言子页（<c>editor_options.cpp:283-296</c>）要的是
/// 同一份口径——能画出来的语言只有「字体有中日韩字形」的那些，选中的语言按文化名找、找不到退回第一个本地化的目录。
/// 搬到这里给两处共用，免得两边慢慢走岔。
/// </remarks>
internal static class LanguageCatalog
{
    /// <summary>
    /// Drops the languages the fonts cannot draw, <c>getSupportedLanguages()</c> minus the glyph check of the reference.
    /// </summary>
    /// <param name="languages">The catalogs found in the data directory, English first.</param>
    /// <param name="fonts">The fonts the interface is drawn with.</param>
    /// <returns>The languages that can be shown; English alone when the fonts have no CJK fallback.</returns>
    public static IReadOnlyList<GameText> Select(IReadOnlyList<GameText> languages, GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(fonts);

        // Without a CJK fallback Chinese tooltips would be drawn as '?' glyphs, so the menu stays
        // English instead of showing mojibake.
        if (fonts.SupportsCjk || languages.Count <= 1)
        {
            return languages;
        }

        GameLog.Warn("No CJK glyphs are available; the main menu stays in English.");

        return [languages[0]];
    }

    /// <summary>
    /// Finds the catalog of a culture, <c>Settings::getGameLanguage()</c> being its name.
    /// </summary>
    /// <param name="languages">The installed catalogs.</param>
    /// <param name="culture">Culture name from the preferences; ignored when empty.</param>
    /// <returns>
    /// The index of the catalog; the first localized one when the preference names none, zero when nothing is localized.
    /// </returns>
    public static int IndexOfCulture(IReadOnlyList<GameText> languages, string? culture)
    {
        ArgumentNullException.ThrowIfNull(languages);

        if (!string.IsNullOrWhiteSpace(culture))
        {
            for (int index = 0; index < languages.Count; ++index)
            {
                if (string.Equals(languages[index].Culture, culture, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            GameLog.Warn($"The requested language '{culture}' is not installed; using the default.");
        }

        // A localized catalog is what makes the CJK path visible, so it wins over English.
        for (int index = 0; index < languages.Count; ++index)
        {
            if (languages[index].IsLocalized)
            {
                return index;
            }
        }

        return 0;
    }
}
