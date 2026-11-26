using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The decoration passes of <c>generateMap()</c>: the trees on a region's edge and the scenes in its empty corners
/// (<c>map_random_generator_helper.cpp:914-934</c>, <c>:1178-1235</c>).
/// </summary>
public sealed class RandomMapDecorationPhaseTests
{
    private const int Size = 36;

    [Fact]
    public void EveryTreeBlockFollowsTheReference()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5], RandomMapDecorations.ObstacleIndicesFor(MapGround.Grass));
        Assert.Equal([6, 7, 8, 9, 10, 11], RandomMapDecorations.ObstacleIndicesFor(MapGround.Lava));
        Assert.Equal([12, 13, 14, 15, 16, 17], RandomMapDecorations.ObstacleIndicesFor(MapGround.Wasteland));
        Assert.Equal([18, 19, 20, 21, 22, 23], RandomMapDecorations.ObstacleIndicesFor(MapGround.Swamp));
        Assert.Equal([18, 19, 20, 21, 22, 23], RandomMapDecorations.ObstacleIndicesFor(MapGround.Dirt));
        Assert.Equal([24, 25, 26, 27, 28, 29], RandomMapDecorations.ObstacleIndicesFor(MapGround.Desert));
        Assert.Equal([24, 25, 26, 27, 28, 29], RandomMapDecorations.ObstacleIndicesFor(MapGround.Beach));
        Assert.Equal([30, 31, 32, 33, 34, 35], RandomMapDecorations.ObstacleIndicesFor(MapGround.Snow));
        Assert.Empty(RandomMapDecorations.ObstacleIndicesFor(MapGround.Water));
    }

    [Fact]
    public void EveryGroundHasTheSceneCountOfTheReference()
    {
        Assert.Equal(7, RandomMapDecorations.Common.Count);
        Assert.Equal(2, RandomMapDecorations.For(MapGround.Grass).Count);
        Assert.Equal(3, RandomMapDecorations.For(MapGround.Snow).Count);
        Assert.Equal(2, RandomMapDecorations.For(MapGround.Swamp).Count);
        Assert.Equal(5, RandomMapDecorations.For(MapGround.Lava).Count);
        Assert.Single(RandomMapDecorations.For(MapGround.Dirt));        Assert.Equal(3, RandomMapDecorations.For(MapGround.Wasteland).Count);
        Assert.Equal(2, RandomMapDecorations.For(MapGround.Beach).Count);

        // 沙漠与水面在参考里没有条目，只拿 Common 那一套。
        Assert.Empty(RandomMapDecorations.For(MapGround.Desert));
        Assert.Empty(RandomMapDecorations.For(MapGround.Water));
    }

    /// <summary>
    /// Every index of the tables resolves inside the catalogue once the terrain variant is applied — a table entry that
    /// names an object the catalogue does not have would silently turn into an empty scene.
    /// </summary>
    [Theory]
    [InlineData(MapGround.Grass)]
    [InlineData(MapGround.Snow)]
    [InlineData(MapGround.Swamp)]
    [InlineData(MapGround.Lava)]
    [InlineData(MapGround.Dirt)]
    [InlineData(MapGround.Wasteland)]
    [InlineData(MapGround.Beach)]
    [InlineData(MapGround.Desert)]
    public void EveryTableEntryExistsInTheCatalogue(MapGround ground)
    {
        List<RandomMapDecorationSet> scenes = [.. RandomMapDecorations.Common, .. RandomMapDecorations.For(ground)];

        foreach (RandomMapDecorationSet scene in scenes)
        {
            foreach (RandomMapObjectPlacement placement in scene.Obstacles.Concat(scene.Optional))
            {
                int index = RandomMapObjectRules.SelectTerrainVariant(
                    placement.Group, placement.ObjectIndex, ground);

                IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(placement.Group);

                Assert.True(
                    index >= 0 && index < objects.Count,
                    $"{placement.Group} has no entry {index} (named {placement.ObjectIndex}, {ground}).");
            }
        }
    }

    [Fact]
    public void ABorderTileOfALandRegionGetsATree()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        int border = state.IndexAt(new Point(10, 10));

        Assert.True(RandomMapDecorationPhase.PlaceBorderObstacle(
            map, state, border, MapGround.Grass, new Pcg32(seed: 7)));

        Assert.NotEqual(0U, map.LastObjectUid);
        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(border).Type);

        // 树落在锚点上：这一格的物体类型不再为空。
        Assert.NotEqual(Mp2ObjectType.None, map.Tile(10, 10).ObjectType);
    }

    [Fact]
    public void WaterHasNoBorderTrees()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.False(RandomMapDecorationPhase.PlaceBorderObstacle(
            map, state, state.IndexAt(new Point(10, 10)), MapGround.Water, new Pcg32(seed: 7)));

        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void ScenesFillTheCornersAroundTakenTiles()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        // 中间放一小块障碍：只有贴着它、还能整组放下景的空地才长装饰，这块障碍本身要被让出来。
        // （一条一格宽的路不行——参考的每一组景都占三到四格，紧贴单格缝隙时整组都放不下。）
        for (int y = 10; y < 13; ++y)
        {
            for (int x = 10; x < 13; ++x)
            {
                state.GetNodeToUpdate(new Point(x, y)).Type = RandomMapNodeType.Obstacle;
            }
        }

        RandomMapRegion region = RegionWithNodes(state, RandomMapRegionType.Neutral, MapGround.Grass);
        List<RandomMapDecorationSet> scenes = [.. RandomMapDecorations.Common, .. RandomMapDecorations.For(MapGround.Grass)];

        int placed = RandomMapDecorationPhase.PlaceDecorations(
            map, state, region, scenes, new Pcg32(seed: 99));

        Assert.True(placed > 0);
        Assert.NotEqual(0U, map.LastObjectUid);

        for (int y = 10; y < 13; ++y)
        {
            for (int x = 10; x < 13; ++x)
            {
                // 网格上仍是障碍：装饰物的树冠可以从上方盖过去（参考也这样），但地面层不许占。
                Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(new Point(x, y)).Type);
            }
        }
    }

    [Fact]
    public void ARegionWithNothingTakenGetsNoDecoration()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        // 全是 OPEN：每个空格子的八邻都是 OPEN，过滤器把它们全刷掉。
        int placed = RandomMapDecorationPhase.PlaceDecorations(
            map,
            state,
            RegionWithNodes(state, RandomMapRegionType.Neutral, MapGround.Grass),
            [.. RandomMapDecorations.Common],
            new Pcg32(seed: 5));

        Assert.Equal(0, placed);
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void AnEmptySceneTablePlacesNothing()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        state.GetNodeToUpdate(new Point(10, 10)).Type = RandomMapNodeType.Path;

        Assert.Equal(
            0,
            RandomMapDecorationPhase.PlaceDecorations(
                map, state, RegionWithNodes(state, RandomMapRegionType.Neutral), [], new Pcg32(seed: 5)));

        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void TheSameSeedDecoratesTheSameWay()
    {
        (int placed, uint uid) first = Decorate(seed: 4242);
        (int placed, uint uid) second = Decorate(seed: 4242);
        (int placed, uint uid) other = Decorate(seed: 2424);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    private static (int Placed, uint Uid) Decorate(uint seed)
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        for (int y = 10; y < 13; ++y)
        {
            for (int x = 10; x < 13; ++x)
            {
                state.GetNodeToUpdate(new Point(x, y)).Type = RandomMapNodeType.Obstacle;
            }
        }

        int placed = RandomMapDecorationPhase.PlaceDecorations(
            map,
            state,
            RegionWithNodes(state, RandomMapRegionType.Neutral, MapGround.Grass),
            [.. RandomMapDecorations.Common, .. RandomMapDecorations.For(MapGround.Grass)],
            new Pcg32(seed));

        return (placed, map.LastObjectUid);
    }

    /// <summary>A blank 36x36 map of plain grass.</summary>
    private static Mp2Map Map() => Mp2Map.CreateBlank(Size, 0);

    /// <summary>A 36x36 grid whose inner tiles all belong to region 1.</summary>
    private static RandomMapState Grid()
    {
        RandomMapState state = new(Size, Size);

        for (int y = 2; y < Size - 2; ++y)
        {
            for (int x = 2; x < Size - 2; ++x)
            {
                state.GetNodeToUpdate(new Point(x, y)).Region = 1;
            }
        }

        return state;
    }

    /// <summary>A region whose node list covers the whole grid.</summary>
    private static RandomMapRegion RegionWithNodes(
        RandomMapState state, RandomMapRegionType type, MapGround ground = MapGround.Grass)
    {
        Point centre = new(10, 10);
        RandomMapRegion region = new(1, state.GetNode(centre), 0, ground, 900, 20000, type);

        for (int y = 2; y < Size - 2; ++y)
        {
            for (int x = 2; x < Size - 2; ++x)
            {
                RandomMapNode node = state.GetNode(new Point(x, y));

                if (node != state.GetNode(centre))
                {
                    region.Nodes.Add(node);
                }
            }
        }

        return region;
    }
}
