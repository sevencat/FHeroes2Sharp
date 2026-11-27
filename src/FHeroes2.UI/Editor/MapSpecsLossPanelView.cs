using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;

namespace FHeroes2.UI.Editor;

/// <summary>
/// Everything <c>LossConditionUI::render()</c> (<c>editor_map_specs_window.cpp:1668-1743</c>) reads besides the panel
/// rectangle.
/// </summary>
/// <param name="Condition">The condition the panel currently shows.</param>
/// <param name="MapWidth">Width of the map, needed by the town and hero titles.</param>
/// <param name="Town">The town to lose; only <see cref="MapLossCondition.LoseTown"/> reads it (<c>:1675</c>).</param>
/// <param name="Hero">The hero to lose; only <see cref="MapLossCondition.LoseHero"/> reads it (<c>:1719</c>).</param>
/// <remarks>
/// 损失条件没有勾选项，也没有联盟块；<see cref="MapLossCondition.OutOfTime"/> 的天数同样由调用方持有的
/// <c>ValueSelectionElement</c> 带着，所以视图里只剩目标与地图宽。
/// </remarks>
public readonly record struct MapSpecsLossPanelView(
    MapLossCondition Condition, int MapWidth, MapSpecsConditionTownRowData? Town = null,
    MapSpecsConditionHeroRowData? Hero = null);
