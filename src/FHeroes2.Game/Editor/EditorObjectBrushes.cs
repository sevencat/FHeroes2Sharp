using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The kinds of object one instrument of the editor panel offers, the
/// <c>LandscapeObjectBrush</c>, <c>AdventureObjectBrush</c> and <c>KingdomObjectBrush</c> enumerations of the reference
/// (<c>editor/editor_interface_panel.h:205-235</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把「物件」这件工具分成四把刷子，每把刷子下面再分几类：地形物件五类、冒险物件七类、王国物件两类、怪物一类。
/// 每一类都正好对应物件目录（<see cref="ObjectGroup"/>）里的一个分组，所以这里的唯一职责是「哪一类是哪个分组」，
/// 以及它们在面板上的先后——那个顺序对应资产里图标的排布，不能改。
/// </para>
/// <para>
/// <b>这一层不做筛选</b>：分组里所有条目都能选，包括随机城堡、随机资源这类「占位」物件——参考的编辑器也允许，因为
/// 地图作者正要靠它们表达「这里随便放一个」。
/// </para>
/// </remarks>
public static class EditorObjectBrushes
{
    /// <summary>The five landscape brushes, <c>LANDSCAPE_COUNT</c> of <c>:213</c>.</summary>
    public static readonly IReadOnlyList<ObjectGroup> Landscape =
    [
        ObjectGroup.LandscapeMountains,
        ObjectGroup.LandscapeRocks,
        ObjectGroup.LandscapeTrees,
        ObjectGroup.LandscapeWater,
        ObjectGroup.LandscapeMiscellaneous,
    ];

    /// <summary>The seven adventure brushes, <c>ADVENTURE_COUNT</c> of <c>:226</c>.</summary>
    public static readonly IReadOnlyList<ObjectGroup> Adventure =
    [
        ObjectGroup.AdventureArtifacts,
        ObjectGroup.AdventureDwellings,
        ObjectGroup.AdventureMines,
        ObjectGroup.AdventurePowerUps,
        ObjectGroup.AdventureTreasures,
        ObjectGroup.AdventureWater,
        ObjectGroup.AdventureMiscellaneous,
    ];

    /// <summary>The two kingdom brushes, <c>KINGDOM_OBJECTS_COUNT</c> of <c>:234</c>.</summary>
    public static readonly IReadOnlyList<ObjectGroup> Kingdom =
    [
        ObjectGroup.KingdomHeroes,
        ObjectGroup.KingdomTowns,
    ];

    /// <summary>The single monster brush, which has no sub kinds to choose from.</summary>
    public static readonly IReadOnlyList<ObjectGroup> Monsters = [ObjectGroup.Monsters];

    /// <summary>The brushes an instrument offers, empty for the instruments that place no catalogue object.</summary>
    /// <param name="tool">The instrument.</param>
    /// <returns>The groups, in the order the reference lists them.</returns>
    public static IReadOnlyList<ObjectGroup> For(EditorTool tool) => tool switch
    {
        EditorTool.LandscapeObjects => Landscape,
        EditorTool.AdventureObjects => Adventure,
        EditorTool.KingdomObjects => Kingdom,
        EditorTool.Monsters => Monsters,
        _ => [],
    };

    /// <summary>Whether an instrument places a catalogue object.</summary>
    /// <param name="tool">The instrument.</param>
    /// <returns><see langword="true"/> for the four object instruments, <c>isObjectMode()</c> of <c>:93-97</c>.</returns>
    public static bool IsObjectMode(EditorTool tool) =>
        tool is EditorTool.LandscapeObjects or EditorTool.AdventureObjects
            or EditorTool.KingdomObjects or EditorTool.Monsters;

    /// <summary>
    /// The brush an instrument starts on, the <c>-1</c> the reference gives the landscape, adventure and kingdom
    /// brushes so that nothing is selected until the author picks something.
    /// </summary>
    /// <param name="tool">The instrument.</param>
    /// <returns><c>0</c> for the monster brush, which has only one entry, and <c>-1</c> for the rest.</returns>
    /// <remarks>
    /// 参考的怪物刷子没有「类别」这一维（<c>_selectedMonsterType</c> 是物件编号本身），所以它一开始就有一个可选项；
    /// 另外三类刷子在作者点之前是空的（<c>-1</c>）。
    /// </remarks>
    public static int InitialBrush(EditorTool tool) => tool == EditorTool.Monsters ? 0 : -1;
}
