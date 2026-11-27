using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The few things a <see cref="Tile"/> asks the world around it, i.e. the part of <see cref="World"/> the
/// passability and the object type repairs need. <see cref="World"/> is the implementation a running game
/// uses; <see cref="MapTileSet"/> is a bare map without kingdoms, heroes or castles, which is what the map
/// editor has.
/// </summary>
/// <remarks>
/// <para>
/// 参考里这些函数直接摸全局 <c>world</c>（<c>maps_tiles.cpp:657-800</c> 与 <c>:1321-1400</c>），端口把用到的
/// 四样抽成接口：地图尺寸、取地块、取英雄、取地面事件。编辑器没有 <see cref="World"/>，但确实有前两样与「没有英雄」
/// 这一事实，所以通行性叠层也能跑同一套算法，不必另写一份。
/// </para>
/// <para>
/// <b>只有这四样。</b> 需要寻路器重置或国王表的方法（<see cref="Tile.SetHero"/>）不在其中，它们仍然要
/// <see cref="World"/>。
/// </para>
/// </remarks>
public interface ITileNeighbourhood
{
    /// <summary>The size of the map, <c>world.getSize()</c>; <see langword="null"/> while the map has none.</summary>
    MapGeometry? Geometry { get; }

    /// <summary>The tile at <paramref name="tileIndex"/>.</summary>
    /// <param name="tileIndex">Index of the tile, row major.</param>
    /// <returns>The tile.</returns>
    Tile GetTile(int tileIndex);

    /// <summary>The hero of the given id, <c>world.GetHeroes( id )</c>.</summary>
    /// <param name="heroId">Id of the hero.</param>
    /// <returns>The hero, or <see langword="null"/> when there is none.</returns>
    Hero? GetHero(int heroId);

    /// <summary>The ground event of a tile, <c>world.GetMapEvent()</c>.</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns>The event, or <see langword="null"/> when the tile carries none.</returns>
    MapEvent? GetMapEvent(int tileIndex);
}
