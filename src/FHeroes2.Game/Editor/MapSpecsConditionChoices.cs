using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The victory and loss conditions the drop-down lists of the map specifications page offer, the reference's
/// <c>supportedVictoryConditions</c> / <c>supportedLossConditions</c> (<c>editor_map_specs_window.cpp:29-32</c>)
/// together with the filtering of <c>showWinLoseList()</c> (<c>:1904-1926</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的列表顺序就是下面两个数组的顺序，画、点、选都按它走；选项不够的时候按地图内容剪掉：
/// 没有「电脑控制的英雄」就没有「击败英雄」，没有城堡就没有「占领城堡」，可选颜色不足三种就没有「击败另一方」；
/// 失败条件同理（用的是「人类控制的英雄」）。
/// </para>
/// <para>
/// 与参考的偏差：城堡归属端口没解析（<see cref="MapSpecsState.Towns"/> 的文档写明了），所以「城堡」一律按
/// 「地图上有没有城堡」判断，不再区分是不是电脑的、是不是中立的——参考的 <c>getMapTowns()</c> 会按颜色筛。
/// </para>
/// </remarks>
public static class MapSpecsConditionChoices
{
    /// <summary>Player colours a map must offer before the alliance condition makes sense, <c>3</c>.</summary>
    public const int MinimumColoursForAlliance = 3;

    /// <summary>
    /// The victory conditions under the drop-down button, in the reference's order,
    /// <c>editor_map_specs_window.cpp:29-30</c>.
    /// </summary>
    public static IReadOnlyList<MapVictoryCondition> SupportedVictoryConditions { get; } =
    [
        MapVictoryCondition.DefeatEveryone,
        MapVictoryCondition.CaptureTown,
        MapVictoryCondition.KillHero,
        MapVictoryCondition.ObtainArtifact,
        MapVictoryCondition.DefeatOtherSide,
        MapVictoryCondition.CollectGold,
    ];

    /// <summary>
    /// The loss conditions under the drop-down button, in the reference's order,
    /// <c>editor_map_specs_window.cpp:31-32</c>.
    /// </summary>
    public static IReadOnlyList<MapLossCondition> SupportedLossConditions { get; } =
    [
        MapLossCondition.Everything,
        MapLossCondition.LoseTown,
        MapLossCondition.LoseHero,
        MapLossCondition.OutOfTime,
    ];

    /// <summary>
    /// The victory conditions a map can actually be won by,
    /// <c>showWinLoseList()</c>'s <c>victoryConditions</c> (<c>editor_map_specs_window.cpp:1904-1926</c>).
    /// </summary>
    /// <param name="state">The map being edited.</param>
    /// <returns>The supported conditions the map has the pieces for, in reference order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<MapVictoryCondition> Victory(MapSpecsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        bool hasComputerHero = state.VictoryHeroes.Count > 0;
        bool hasTown = state.Towns.Count > 0;
        bool hasAlliance = PlayerColors.Count(state.AvailablePlayers) >= MinimumColoursForAlliance;

        List<MapVictoryCondition> conditions = new(SupportedVictoryConditions.Count);

        foreach (MapVictoryCondition condition in SupportedVictoryConditions)
        {
            if ((condition == MapVictoryCondition.KillHero && !hasComputerHero)
                || (condition == MapVictoryCondition.CaptureTown && !hasTown)
                || (condition == MapVictoryCondition.DefeatOtherSide && !hasAlliance))
            {
                continue;
            }

            conditions.Add(condition);
        }

        return conditions;
    }

    /// <summary>
    /// The loss conditions a map can actually be lost by,
    /// <c>showWinLoseList()</c>'s <c>lossConditions</c> (<c>editor_map_specs_window.cpp:1904-1926</c>).
    /// </summary>
    /// <param name="state">The map being edited.</param>
    /// <returns>The supported conditions the map has the pieces for, in reference order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<MapLossCondition> Loss(MapSpecsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        bool hasHumanHero = state.LossHeroes.Count > 0;
        bool hasTown = state.Towns.Count > 0;

        List<MapLossCondition> conditions = new(SupportedLossConditions.Count);

        foreach (MapLossCondition condition in SupportedLossConditions)
        {
            if ((condition == MapLossCondition.LoseHero && !hasHumanHero)
                || (condition == MapLossCondition.LoseTown && !hasTown))
            {
                continue;
            }

            conditions.Add(condition);
        }

        return conditions;
    }
}
