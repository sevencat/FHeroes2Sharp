namespace FHeroes2.Game.Editor;

/// <summary>
/// What the left button of the editor does, the <c>Instrument</c> enumeration of the reference's editor panel
/// (<c>editor/editor_interface_panel.h:171-186</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>顺序就是资产里图标的顺序，不能重排</b>：面板上九个按钮的图标按这个顺序排列，
/// <see cref="EditorPanel"/> 的画法与命中判定都按序号走。参考在那里写着「IMPORTANT. This enumeration corresponds with
/// the order of instruments in original assets. Do not change this order.」
/// </para>
/// <para>
/// 三个「放置物件」的仪器（<see cref="LandscapeObjects"/>、<see cref="AdventureObjects"/>、<see cref="KingdomObjects"/>）
/// 与 <see cref="Monsters"/> 共用一个落子路径，只是可选的分组不同，见 <see cref="EditorObjectBrushes"/>。
/// </para>
/// </remarks>
public enum EditorTool : byte
{
    /// <summary>The ground brush: a click replaces the terrain of the tile, <c>TERRAIN</c>.</summary>
    Terrain = 0,

    /// <summary>Places mountains, rocks, trees and the other scenery, <c>LANDSCAPE_OBJECTS</c>.</summary>
    LandscapeObjects = 1,

    /// <summary>Edits, moves or copies an object already on the map, <c>DETAIL</c>.</summary>
    Detail = 2,

    /// <summary>Places artifacts, mines, treasures and the like, <c>ADVENTURE_OBJECTS</c>.</summary>
    AdventureObjects = 3,

    /// <summary>Places heroes and towns, <c>KINGDOM_OBJECTS</c>.</summary>
    KingdomObjects = 4,

    /// <summary>Places a guard, <c>MONSTERS</c>.</summary>
    Monsters = 5,

    /// <summary>Draws a stream, <c>STREAM</c>.</summary>
    Stream = 6,

    /// <summary>Draws a road, <c>ROAD</c>.</summary>
    Road = 7,

    /// <summary>Deletes objects and ground, <c>ERASE</c>.</summary>
    Erase = 8,
}
