using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The tile set of the map editor: <see cref="MapTileSet"/> builds the same <see cref="Tile"/> objects a
/// loaded <see cref="World"/> has and runs the reference passability over them
/// (<c>maps_tiles.cpp:583-800</c>).
/// </summary>
/// <remarks>
/// 用例只挑已经由 <c>TileTests</c> 钉死的三条规则来验证装配本身对不对：空地全通、挡路物件只放行中排与下排、物件压在地图
/// 底边时整格不可进。算法本身不在这里重测。
/// </remarks>
public sealed class MapTileSetTests
{
    private const int MapSize = 3;

    [Fact]
    public void Create_EmptyMap_IsPassableInEveryDirection()
    {
        MapTileSet tiles = MapTileSet.Create(SyntheticMap.Create(MapSize, MapSize, _ => SyntheticMap.BlankTile()));

        for (int index = 0; index < MapSize * MapSize; ++index)
        {
            Assert.Equal(Directions.All, tiles.GetTile(index).PassableDirections);
        }
    }

    [Fact]
    public void Create_BlockingObject_ClosesTheMiddleRowDown()
    {
        MapTileSet tiles = MapTileSet.Create(
            SyntheticMap.Create(MapSize, MapSize, index => index == 4 ? BlockedTile() : SyntheticMap.BlankTile()));

        Assert.Equal(Directions.CenterRow | Directions.BottomRow, tiles.GetTile(4).PassableDirections);

        // 挡路只发生在有物件的那一格，中间行往下封死，别的地块照旧全通。
        Assert.Equal(Directions.All, tiles.GetTile(0).PassableDirections);
    }

    [Fact]
    public void Create_ObjectOnTheBottomEdge_IsImpassable()
    {
        // 物件贴着地图底边时英雄根本没地方站：参考把整格标成不可进（maps_tiles.cpp:668-675）。
        MapTileSet tiles = MapTileSet.Create(
            SyntheticMap.Create(MapSize, MapSize, index => index == 7 ? BlockedTile() : SyntheticMap.BlankTile()));

        Assert.Equal(Direction.Unknown, tiles.GetTile(7).PassableDirections);
    }

    [Fact]
    public void Refresh_RecomputesAfterAnEdit()
    {
        Mp2Map map = SyntheticMap.Create(MapSize, MapSize, _ => SyntheticMap.BlankTile());
        MapTileSet tiles = MapTileSet.Create(map);

        Assert.Equal(Directions.All, tiles.GetTile(4).PassableDirections);

        // 编辑器改一格就换出一张新地图（Mp2Map 是 record），再让地块集重算——画上一座山之后走的就是这条路。
        Mp2Map edited = ReplaceTile(map, 4, BlockedTile());
        tiles.Refresh(edited, 4);
        Assert.Equal(Directions.CenterRow | Directions.BottomRow, tiles.GetTile(4).PassableDirections);

        // 擦掉之后要能回到全通，而不是留下上一次的标记。
        tiles.Refresh(ReplaceTile(map, 4, SyntheticMap.BlankTile()), 4);
        Assert.Equal(Directions.All, tiles.GetTile(4).PassableDirections);
    }

    [Fact]
    public void GetHero_AnswersNoneBecauseEditorTilesCarryNoHeroes()
    {
        MapTileSet tiles = MapTileSet.Create(SyntheticMap.Create(MapSize, MapSize, _ => SyntheticMap.BlankTile()));

        Assert.Null(tiles.GetHero(0));
        Assert.Null(tiles.GetMapEvent(0));
        Assert.Equal(MapSize, tiles.Geometry?.Width);
        Assert.Equal(MapSize, tiles.Geometry?.Height);
    }

    /// <summary>A copy of the map with one tile replaced, what the editor produces for an edit.</summary>
    private static Mp2Map ReplaceTile(Mp2Map map, int index, Mp2TileInfo tile)
    {
        Mp2TileInfo[] tiles = [.. map.Tiles];
        tiles[index] = tile;

        return map with { Tiles = tiles };
    }

    /// <summary>A mountain of the grass mountain archive: an opaque, non action object.</summary>
    private static Mp2TileInfo BlockedTile() =>
        SyntheticMap.BlankTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MtnGras << 2),
            BottomIcnImageIndex = 14,
            Quantity1 = (byte)ObjectLayerType.Object,
            Level1ObjectUid = 1,
            ObjectType = Mp2ObjectType.Mountains,
        };
}
