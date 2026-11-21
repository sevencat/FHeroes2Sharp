using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// Language metadata, the free functions of <c>ui_language.cpp</c> (<c>ui_language.cpp:162-360</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用「两字母缩写 + 英文全名」两张表互相映射，英文的缩写是空串（它是默认语言，永远兜底到它）。端口照抄，
/// 区别只在返回类型：参考返回 <c>const char *</c>，端口返回 <see cref="string"/>。
/// </para>
/// <para>
/// 语言名在参考里包了 <c>_( )</c>，是会随界面语言变的可翻译串；端口按 ADR-137 统一在 Game 层留英文原文
/// （msgid），翻译交给 App 层的 <c>GameText</c>。
/// </para>
/// <para>
/// 未移植的两个：<c>getSupportedLanguages()</c>（<c>ui_language.cpp:114-160</c>）靠 <c>setGameLanguage()</c>
/// 的返回值判断某语言的翻译是否存在，端口的翻译表在 App 层（<c>GameText.LoadAll</c>），Game 层拿不到，语言选择
/// 已由 <c>LanguageSelectionDialog</c> 用 App 给出的清单实现；<c>updateAlphabet()</c>（<c>ui_language.cpp:291-311</c>）
/// 要调 <c>Assets::updateLanguageDependentResources</c> 按代码页重建字体，属未移植的 <c>ui_font.cpp</c>。
/// </para>
/// </remarks>
public static class GameLanguage
{
    /// <summary>Lower-case abbreviations and full names, the reference's <c>languageName</c> map.</summary>
    /// <remarks>参考：<c>ui_language.cpp:50-72</c>。键必须小写且不可翻译，查表前参考会先 <c>StringLower</c>。</remarks>
    private static readonly Dictionary<string, SupportedLanguage> LanguageNames = new(StringComparer.Ordinal)
    {
        ["pl"] = SupportedLanguage.Polish,
        ["polish"] = SupportedLanguage.Polish,
        ["de"] = SupportedLanguage.German,
        ["german"] = SupportedLanguage.German,
        ["fr"] = SupportedLanguage.French,
        ["french"] = SupportedLanguage.French,
        ["ru"] = SupportedLanguage.Russian,
        ["russian"] = SupportedLanguage.Russian,
        ["it"] = SupportedLanguage.Italian,
        ["italian"] = SupportedLanguage.Italian,
        ["cs"] = SupportedLanguage.Czech,
        ["czech"] = SupportedLanguage.Czech,
        ["nb"] = SupportedLanguage.Norwegian,
        ["norwegian"] = SupportedLanguage.Norwegian,
        ["be"] = SupportedLanguage.Belarusian,
        ["belarusian"] = SupportedLanguage.Belarusian,
        ["uk"] = SupportedLanguage.Ukrainian,
        ["ukrainian"] = SupportedLanguage.Ukrainian,
        ["bg"] = SupportedLanguage.Bulgarian,
        ["bulgarian"] = SupportedLanguage.Bulgarian,
        ["es"] = SupportedLanguage.Spanish,
        ["spanish"] = SupportedLanguage.Spanish,
        ["pt"] = SupportedLanguage.Portuguese,
        ["portuguese"] = SupportedLanguage.Portuguese,
        ["sv"] = SupportedLanguage.Swedish,
        ["swedish"] = SupportedLanguage.Swedish,
        ["tr"] = SupportedLanguage.Turkish,
        ["turkish"] = SupportedLanguage.Turkish,
        ["ro"] = SupportedLanguage.Romanian,
        ["romanian"] = SupportedLanguage.Romanian,
        ["nl"] = SupportedLanguage.Dutch,
        ["dutch"] = SupportedLanguage.Dutch,
        ["hu"] = SupportedLanguage.Hungarian,
        ["hungarian"] = SupportedLanguage.Hungarian,
        ["da"] = SupportedLanguage.Danish,
        ["danish"] = SupportedLanguage.Danish,
        ["sk"] = SupportedLanguage.Slovak,
        ["slovak"] = SupportedLanguage.Slovak,
        ["vi"] = SupportedLanguage.Vietnamese,
        ["vietnamese"] = SupportedLanguage.Vietnamese,
        ["el"] = SupportedLanguage.Greek,
        ["greek"] = SupportedLanguage.Greek,
        ["eo"] = SupportedLanguage.Esperanto,
        ["esperanto"] = SupportedLanguage.Esperanto,
    };

    /// <summary>The language of the interface, <c>getCurrentLanguage()</c> (<c>ui_language.cpp:313</c>).</summary>
    /// <remarks>
    /// 参考从 <c>Settings::getGameLanguage()</c> 里取缩写再查表；端口上 <see cref="Settings.GameLanguage"/> 存的是
    /// App 的 culture 名（<c>"en"</c>、<c>"zh_CN"</c>），认不出的按参考的规则兜底到英文。
    /// </remarks>
    public static SupportedLanguage Current => FromAbbreviation(Settings.Get().GameLanguage);

    /// <summary>The English name of a language, <c>getLanguageName()</c> (<c>ui_language.cpp:162-216</c>).</summary>
    /// <param name="language">The language.</param>
    /// <returns>Its name as an untranslated message id.</returns>
    public static string GetName(SupportedLanguage language) => language switch
    {
        SupportedLanguage.English => "English",
        SupportedLanguage.French => "French",
        SupportedLanguage.Polish => "Polish",
        SupportedLanguage.German => "German",
        SupportedLanguage.Russian => "Russian",
        SupportedLanguage.Italian => "Italian",
        SupportedLanguage.Czech => "Czech",
        SupportedLanguage.Norwegian => "Norwegian",
        SupportedLanguage.Belarusian => "Belarusian",
        SupportedLanguage.Bulgarian => "Bulgarian",
        SupportedLanguage.Ukrainian => "Ukrainian",
        SupportedLanguage.Romanian => "Romanian",
        SupportedLanguage.Spanish => "Spanish",
        SupportedLanguage.Swedish => "Swedish",
        SupportedLanguage.Portuguese => "Portuguese",
        SupportedLanguage.Turkish => "Turkish",
        SupportedLanguage.Dutch => "Dutch",
        SupportedLanguage.Hungarian => "Hungarian",
        SupportedLanguage.Danish => "Danish",
        SupportedLanguage.Slovak => "Slovak",
        SupportedLanguage.Vietnamese => "Vietnamese",
        SupportedLanguage.Greek => "Greek",
        SupportedLanguage.Esperanto => "Esperanto",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    /// <summary>
    /// The two letter abbreviation used as translation file name, <c>getLanguageAbbreviation()</c>
    /// (<c>ui_language.cpp:218-272</c>).
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The abbreviation; an empty string for English, which is the default language.</returns>
    public static string GetAbbreviation(SupportedLanguage language) => language switch
    {
        SupportedLanguage.English => string.Empty,
        SupportedLanguage.French => "fr",
        SupportedLanguage.Polish => "pl",
        SupportedLanguage.German => "de",
        SupportedLanguage.Russian => "ru",
        SupportedLanguage.Italian => "it",
        SupportedLanguage.Czech => "cs",
        SupportedLanguage.Norwegian => "nb",
        SupportedLanguage.Belarusian => "be",
        SupportedLanguage.Bulgarian => "bg",
        SupportedLanguage.Ukrainian => "uk",
        SupportedLanguage.Romanian => "ro",
        SupportedLanguage.Spanish => "es",
        SupportedLanguage.Swedish => "sv",
        SupportedLanguage.Portuguese => "pt",
        SupportedLanguage.Turkish => "tr",
        SupportedLanguage.Dutch => "nl",
        SupportedLanguage.Hungarian => "hu",
        SupportedLanguage.Danish => "da",
        SupportedLanguage.Slovak => "sk",
        SupportedLanguage.Vietnamese => "vi",
        SupportedLanguage.Greek => "el",
        SupportedLanguage.Esperanto => "eo",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    /// <summary>
    /// The language an abbreviation or a full name stands for, <c>getLanguageFromAbbreviation()</c>
    /// (<c>ui_language.cpp:274-289</c>).
    /// </summary>
    /// <param name="abbreviation">An abbreviation or a full name, in any case.</param>
    /// <returns>The language; English for an empty or unknown name, exactly as in the reference.</returns>
    public static SupportedLanguage FromAbbreviation(string? abbreviation)
    {
        if (string.IsNullOrEmpty(abbreviation))
        {
            return SupportedLanguage.English;
        }

        return LanguageNames.TryGetValue(abbreviation.ToLowerInvariant(), out SupportedLanguage language)
            ? language
            : SupportedLanguage.English;
    }

    /// <summary>
    /// The character set a language needs, <c>getCodePage()</c> (<c>ui_language.cpp:318-360</c>).
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The code page its glyphs come from.</returns>
    public static CodePage GetCodePage(SupportedLanguage language) => language switch
    {
        SupportedLanguage.English => CodePage.Ascii,
        SupportedLanguage.Czech or SupportedLanguage.Hungarian or SupportedLanguage.Polish or SupportedLanguage.Slovak
            => CodePage.Cp1250,
        SupportedLanguage.Belarusian or SupportedLanguage.Bulgarian or SupportedLanguage.Russian or SupportedLanguage.Ukrainian
            => CodePage.Cp1251,
        SupportedLanguage.Danish
            or SupportedLanguage.Dutch
            or SupportedLanguage.French
            or SupportedLanguage.German
            or SupportedLanguage.Italian
            or SupportedLanguage.Norwegian
            or SupportedLanguage.Portuguese
            or SupportedLanguage.Spanish
            or SupportedLanguage.Swedish
            => CodePage.Cp1252,
        SupportedLanguage.Greek => CodePage.Cp1253,
        SupportedLanguage.Turkish => CodePage.Cp1254,
        SupportedLanguage.Vietnamese => CodePage.Cp1258,
        SupportedLanguage.Romanian => CodePage.Iso8859_16,
        SupportedLanguage.Esperanto => CodePage.Iso8859_3,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };
}
