using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Editor;

/// <summary>
/// Everything <c>VictoryConditionUI::render()</c> (<c>editor_map_specs_window.cpp:1021-1217</c>) reads besides the
/// panel rectangle: the condition, its flags and the row it has to show.
/// </summary>
/// <param name="Condition">The condition the panel currently shows.</param>
/// <param name="MapWidth">Width of the map, needed by the town and hero titles.</param>
/// <param name="AllowNormalVictory">Value of <c>allowNormalVictory</c>, the tick of the "standard victory" box.</param>
/// <param name="AllowForAi">Value of <c>isVictoryConditionApplicableForAI</c>, the tick of the "also for AI" box.</param>
/// <param name="Town">
/// The town to capture; only <see cref="MapVictoryCondition.CaptureTown"/> reads it, and the reference asserts that the
/// list it comes from is not empty (<c>:1040</c>).
/// </param>
/// <param name="Hero">The hero to kill; only <see cref="MapVictoryCondition.KillHero"/> reads it (<c>:1088</c>).</param>
/// <param name="Artifact">The artifact to obtain; only <see cref="MapVictoryCondition.ObtainArtifact"/> reads it (<c>:1119</c>).</param>
/// <param name="AvailableColors">
/// The colours the map allows; only <see cref="MapVictoryCondition.DefeatOtherSide"/> reads it, as one checkbox per colour
/// (<c>:1145-1157</c>).
/// </param>
/// <param name="Alliances">
/// One colour set per alliance row, the <c>_alliances</c> of the reference (<c>:1157</c>); only
/// <see cref="MapVictoryCondition.DefeatOtherSide"/> reads it.
/// </param>
/// <remarks>
/// 参考把这些都存成 <c>VictoryConditionUI</c> 的成员，绘制时直接读；端口把它们收成一个只读视图，
/// 绘制函数因此不带状态，事件循环（下一个 issue）负责在每次重画前把视图拼出来。
/// 金与天数两个条件没有视图字段：数值由调用方持有的 <c>ValueSelectionElement</c> 自己带着。
/// </remarks>
public readonly record struct MapSpecsVictoryPanelView(
    MapVictoryCondition Condition,
    int MapWidth,
    bool AllowNormalVictory = false,
    bool AllowForAi = false,
    MapSpecsConditionTownRowData? Town = null,
    MapSpecsConditionHeroRowData? Hero = null,
    ArtifactType Artifact = ArtifactType.Unknown,
    PlayerColor AvailableColors = PlayerColor.None,
    IReadOnlyList<PlayerColor>? Alliances = null)
{
    /// <summary>
    /// Whether the town to capture belongs to nobody, the <c>_townToCapture.second == PlayerColor::NONE</c> test that
    /// decides whether the "also for AI" checkbox is offered (<c>:1066</c>).
    /// </summary>
    public bool IsNeutralTownTarget => Town is { } town && town.Color == PlayerColor.None;
}
