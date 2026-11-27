using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The town a condition panel row stands for, the <c>TownInfo</c> of the reference
/// (<c>editor_map_specs_window.cpp:80-92</c>) reduced to the fields the row draws.
/// </summary>
/// <param name="Color">Owner of the town, which picks the colour of its flags.</param>
/// <param name="Race">Race of the town, which picks the castle body and the portrait in the town list.</param>
/// <param name="Name">Custom name of the town, empty when the author did not type one.</param>
/// <param name="HasFort">
/// Whether <c>BUILD_CASTLE</c> is among the built buildings. The reference recomputes this from the castle metadata
/// every time it draws the row (<c>:289-290</c>); the port keeps it on the data so the row stays a pure drawing function.
/// </param>
/// <param name="TileIndex">Tile the town stands on, used by the title.</param>
/// <remarks>
/// 参考的 <c>TownInfo</c> 还带 <c>castleMetadata</c> 指针（自定义名与已建建筑在那里）和 <c>language</c>，
/// 端口把前者的两个用处（自定义名、<c>BUILD_CASTLE</c>）提成值字段，后者没有本地化层可直接去掉。
/// </remarks>
public readonly record struct MapSpecsConditionTownRowData(
    PlayerColor Color, MapRace Race, string Name, bool HasFort, int TileIndex);
