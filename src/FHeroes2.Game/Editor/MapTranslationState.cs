namespace FHeroes2.Game.Editor;

/// <summary>
/// The texts of a map in one language: <c>TranslationBaseMapMetadata</c> (<c>map_format_info.h:321-326</c>).
/// </summary>
/// <remarks>
/// 参考的 <c>TranslationFormat</c>（<c>:328-343</c>）还带每日事件、传闻、城镇 / 英雄的自定义名、斯芬克斯的谜题与答案、
/// 路牌和地图事件的文本。端口编辑器尚未建模这些字段（它们随 <c>.fh2m</c> 一起进来），所以这一层先只有地图名、描述和
/// 创作者注记三件套；<see cref="CreatorNotes"/> 参考里有、端口的地图头部装不下，先留着给 <c>.fh2m</c>。
/// </remarks>
public sealed class MapTranslationText
{
    /// <summary>Scenario name (<c>name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Scenario description (<c>description</c>).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Contact details of the map maker (<c>creatorNotes</c>); editor only, never played.</summary>
    public string CreatorNotes { get; set; } = string.Empty;

    /// <summary>Copies the three texts, so a stored translation cannot be edited through the live one.</summary>
    /// <returns>A new instance holding the same strings.</returns>
    public MapTranslationText Clone() => new()
    {
        Name = Name,
        Description = Description,
        CreatorNotes = CreatorNotes,
    };
}

/// <summary>
/// The languages a map carries: its main language and the translations made for it
/// (<c>BaseMapFormat::mainLanguage</c> / <c>translations</c>, <c>map_format_info.h:375-386</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把「当前语言的文本」摊在 <c>MapFormat</c> 的 <c>name</c> / <c>description</c> 字段上，<c>translations</c> 只存别的
/// 语言；本移植照搬这个分工：<see cref="Current"/> 是主语言的文本（地图规格窗改的就是它），<see cref="Translations"/>
/// 存其余语言。换主语言走的正是 <c>map_format_helper.cpp:1904</c> 的 <c>changeLanguage</c>：
/// </para>
/// <list type="number">
/// <item>把 <see cref="Current"/> 存进 <see cref="MainLanguage"/> 的槽（旧的同语言槽被覆盖，照 <c>:1912</c> 的 <c>operator[]</c>）；</item>
/// <item>
/// 目标语言有存文本就读回来（<c>loadTranslation</c>，<c>:1991-1997</c>；参考把空文本也当作有意留空，所以即便读回的是空串
/// 也要装进去），没有就保持现在的文本；
/// </item>
/// <item>把目标语言从 <see cref="Translations"/> 里删掉——它已经是主语言了，<see cref="MainLanguage"/> 换成它。</item>
/// </list>
/// <para>
/// <see cref="Languages"/> 的顺序照参考的 <c>std::map</c>：主语言在最前，其余按 <see cref="SupportedLanguage"/> 的枚举
/// 值升序——语言窗的列表和确定后的写回顺序都看它。
/// </para>
/// </remarks>
public sealed class MapTranslationState
{
    /// <summary>The language a map starts with, <c>BaseMapFormat::mainLanguage</c> (<c>map_format_info.h:375</c>).</summary>
    public const SupportedLanguage DefaultLanguage = SupportedLanguage.English;

    private readonly Dictionary<SupportedLanguage, MapTranslationText> _translations = [];

    /// <summary>Starts a map that is written in <see cref="DefaultLanguage"/> and has no translations.</summary>
    public MapTranslationState()
        : this(DefaultLanguage, new MapTranslationText())
    {
    }

    /// <summary>Starts a map whose main language already has <paramref name="current"/> texts.</summary>
    /// <param name="mainLanguage">Language the texts of <paramref name="current"/> are written in.</param>
    /// <param name="current">The texts shown and edited right now; kept, not copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="current"/> is null.</exception>
    public MapTranslationState(SupportedLanguage mainLanguage, MapTranslationText current)
    {
        ArgumentNullException.ThrowIfNull(current);

        MainLanguage = mainLanguage;
        Current = current;
    }

    /// <summary>The language <see cref="Current"/> is written in (<c>mainLanguage</c>).</summary>
    public SupportedLanguage MainLanguage { get; private set; }

    /// <summary>The texts of the main language, the ones the map windows show and edit.</summary>
    public MapTranslationText Current { get; private set; }

    /// <summary>How many languages the map carries: the main one plus every translation.</summary>
    public int Count => 1 + _translations.Count;

    /// <summary>Every language of the map, main first and the rest in enum order (the list of the language window).</summary>
    public IReadOnlyList<SupportedLanguage> Languages =>
    [
        MainLanguage,
        .. _translations.Keys.Order(),
    ];

    /// <summary>The translations, keyed by language; the main language is never in here.</summary>
    public IReadOnlyDictionary<SupportedLanguage, MapTranslationText> Translations => _translations;

    /// <summary>Whether the map carries <paramref name="language"/>, as its main language or as a translation.</summary>
    /// <param name="language">Language to look for.</param>
    /// <returns><see langword="true"/> for any language in <see cref="Languages"/>.</returns>
    public bool Has(SupportedLanguage language) =>
        language == MainLanguage || _translations.ContainsKey(language);

    /// <summary>The texts stored for <paramref name="language"/>, or <see langword="null"/> when it has none.</summary>
    /// <param name="language">Language to look for; the main language has no slot.</param>
    /// <returns>The stored texts, not a copy; <see langword="null"/> for the main language.</returns>
    public MapTranslationText? Find(SupportedLanguage language) =>
        _translations.GetValueOrDefault(language);

    /// <summary>
    /// Registers <paramref name="language"/> with empty texts, the ADD button of the language window
    /// (<c>editor_language_window.cpp:267-270</c>): the list grows at once, the texts stay empty until the map is saved in
    /// that language.
    /// </summary>
    /// <param name="language">Language to add.</param>
    /// <returns><see langword="false"/> when the map already carries it.</returns>
    public bool Add(SupportedLanguage language)
    {
        if (Has(language))
        {
            return false;
        }

        _translations[language] = new MapTranslationText();

        return true;
    }

    /// <summary>Makes <paramref name="language"/> the main language, keeping the texts of the old one as a translation.</summary>
    /// <remarks>照 <c>map_format_helper.cpp:1904-1958</c>；主语言传进来等于什么都不做（<c>:1906</c>）。</remarks>
    /// <param name="language">Language to switch to.</param>
    /// <returns><see langword="false"/> when it already is the main language.</returns>
    public bool ChangeLanguage(SupportedLanguage language)
    {
        if (language == MainLanguage)
        {
            return false;
        }

        _translations[MainLanguage] = Current.Clone();

        if (_translations.TryGetValue(language, out MapTranslationText? stored))
        {
            Current = stored;
        }

        _translations.Remove(language);
        MainLanguage = language;

        return true;
    }

    /// <summary>Drops the translation of <paramref name="language"/>, <c>removeTranslation</c> (<c>:2080-2084</c>).</summary>
    /// <param name="language">Language to drop.</param>
    /// <returns><see langword="false"/> for the main language, which cannot be removed, or when there was none.</returns>
    public bool RemoveTranslation(SupportedLanguage language)
    {
        if (language == MainLanguage)
        {
            return false;
        }

        return _translations.Remove(language);
    }
}
