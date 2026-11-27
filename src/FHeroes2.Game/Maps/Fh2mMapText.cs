using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Carries the language block of an <c>.fh2m</c> file to and from the editor's
/// <see cref="MapTranslationState"/> (<c>map_format_info.h:375-386</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把「当前语言的文本」摊在 <c>BaseMapFormat</c> 的 <c>name</c> / <c>description</c> / <c>creatorNotes</c> 三个字段上，
/// <c>translations</c> 只装别的语言。端口的 <see cref="MapTranslationState"/> 是同一个分工，所以两边是一一映射——
/// 不需要额外一张表。
/// </para>
/// <para>
/// 主语言的 <see cref="MapTranslationText"/> 与 <c>translations</c> 里的一格不同：它没有「自己这条记录」，文本就摊在
/// 头部字段上。<see cref="ToState"/> 按当前主语言造出这一格，<see cref="ApplyTo"/> 把它摊回字段。
/// </para>
/// </remarks>
public static class Fh2mMapText
{
    /// <summary>
    /// Builds the editor's language state out of the header, <c>map_format_helper.cpp:2030-2045</c> 的读入那一半。
    /// </summary>
    /// <param name="map">The header that was read from a file.</param>
    /// <returns>The state the language window edits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static MapTranslationState ToState(Fh2mBaseMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var state = new MapTranslationState(map.MainLanguage, new MapTranslationText
        {
            Name = map.Name,
            Description = map.Description,
            CreatorNotes = map.CreatorNotes,
        });

        foreach ((SupportedLanguage language, MapTranslationText text) in map.Translations)
        {
            // 主语言不该出现在 translations 里；真出现了就跳过，否则状态会有两份主语言。
            if (language == map.MainLanguage)
            {
                continue;
            }

            _ = state.Add(language);

            if (state.Find(language) is { } stored)
            {
                stored.Name = text.Name;
                stored.Description = text.Description;
                stored.CreatorNotes = text.CreatorNotes;
            }
        }

        return state;
    }

    /// <summary>
    /// Writes the state back into a copy of the header, the write half of <c>changeLanguage()</c>.
    /// </summary>
    /// <param name="map">The header read from the file; its other fields are kept.</param>
    /// <param name="state">The language state the editor ended up with.</param>
    /// <returns>A new header carrying the current language and the translations of <paramref name="state"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Fh2mBaseMap ApplyTo(Fh2mBaseMap map, MapTranslationState state)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);

        Dictionary<SupportedLanguage, MapTranslationText> translations = [];

        foreach ((SupportedLanguage language, MapTranslationText text) in state.Translations)
        {
            translations[language] = text.Clone();
        }

        return map with
        {
            MainLanguage = state.MainLanguage,
            Name = state.Current.Name,
            Description = state.Current.Description,
            CreatorNotes = state.Current.CreatorNotes,
            Translations = translations,
        };
    }

    /// <summary>
    /// Fills a fresh <c>.fh2m</c> header out of the map specifications of an editor session, so a map that was made as
    /// an <c>.mp2</c> can be saved with its translations.
    /// </summary>
    /// <remarks>
    /// 尾段留空：端口不写瓦片与元数据（见 ADR-380），所以由这里造出的文件只有头部可用，另存之后仍需用其它路径
    /// 补上尾段。这条路径的用途是「先把语言块保住」，不是「另存出一张完整的地图」。
    /// </remarks>
    /// <param name="state">The map specifications the editor is editing.</param>
    /// <returns>A header carrying the language block and the map size of <paramref name="state"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static Fh2mBaseMap FromSpecs(MapSpecsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Mp2FileInfo info = state.CreateInfo();

        return new Fh2mBaseMap
        {
            Version = Fh2mBaseMap.CurrentSupportedVersion,
            Difficulty = info.Difficulty,
            AvailablePlayerColors = info.KingdomColors,
            HumanPlayerColors = info.HumanPlayers,
            ComputerPlayerColors = info.ComputerPlayers,
            PlayerRaces = [.. info.PlayerRaces],
            VictoryConditionType = (int)info.VictoryCondition,
            IsVictoryConditionApplicableForAi = info.ComputerAlsoWins,
            AllowNormalVictory = info.AllowNormalVictory,
            VictoryConditionMetadata = [.. info.VictoryConditionParameters.Select(parameter => (uint)parameter)],
            LossConditionType = (int)info.LossCondition,
            LossConditionMetadata = [.. info.LossConditionParameters.Select(parameter => (uint)parameter)],
            Width = info.Width,
            MainLanguage = state.Translations.MainLanguage,
            Name = state.Translations.Current.Name,
            Description = state.Translations.Current.Description,
            CreatorNotes = state.Translations.Current.CreatorNotes,
            Translations = state.Translations.Translations.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Clone()),
            Payload = [],
        };
    }
}
