using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The road reorder pass: <c>updateAllRoads()</c>, <c>getRoadObjectIndex()</c> and <c>updateRoadOnTile()</c>
/// (<c>map_format_helper.cpp:858-882</c>, <c>:1848-1897</c>).
/// </summary>
public sealed class RandomMapRoadsTests
{
    private const int Size = 36;

    private const ushort GrassImage = MapGrounds.GrassStartImageIndex + 5;

    [Fact]
    public void AStraightRoadJoinsItsTwoNeighbours()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Lay(state, map, (5, 10), (6, 10), (7, 10));

        int updated = RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        Assert.True(updated > 0);

        // 中间那格左右各一条：Direction.Right（0x08）与 Direction.Left（0x80）。
        int mask = RandomMapRoads.ObjectIndexAt(map, state, Index(6, 10), new Pcg32(seed: 7))
            % RandomMapRoads.VariantStride;

        Assert.Equal((int)(Direction.Right | Direction.Left), mask);
    }

    [Fact]
    public void TheEndOfARoadOnlyJoinsTheWayIn()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Lay(state, map, (5, 10), (6, 10));

        RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        // 右端只有左边有路。
        Assert.Equal(
            (int)Direction.Left,
            RandomMapRoads.ObjectIndexAt(map, state, Index(6, 10), new Pcg32(seed: 7)) % RandomMapRoads.VariantStride);
    }

    [Fact]
    public void AnIsolatedRoadHasNoDirection()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Lay(state, map, (10, 10));

        RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        Assert.Equal(
            0,
            RandomMapRoads.ObjectIndexAt(map, state, Index(10, 10), new Pcg32(seed: 7)) % RandomMapRoads.VariantStride);
    }

    [Fact]
    public void ATileBelowATownGetsTheCastleEntranceRoad()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(10, 10), ObjectGroup.KingdomTowns, 0));
        Lay(state, map, (10, 11));

        RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        Assert.Equal(RandomMapRoads.CastleEntranceIndex, RandomMapRoads.ObjectIndexAt(map, state, Index(10, 11), new Pcg32(seed: 7)));
        Assert.Equal(Mp2ObjectIcnType.Road, map.Tile(10, 11).GroundIcnType);
    }

    [Fact]
    public void ATileWithNoRoadKeepsItsNeighboursOut()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Lay(state, map, (5, 10), (7, 10));

        RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        // 6 号格没有路，所以两端的格子各自孤立。
        Assert.False(state.Roads.ContainsKey(Index(6, 10)));
        Assert.Equal(
            0,
            RandomMapRoads.ObjectIndexAt(map, state, Index(5, 10), new Pcg32(seed: 7)) % RandomMapRoads.VariantStride);
        Assert.Equal(
            0,
            RandomMapRoads.ObjectIndexAt(map, state, Index(7, 10), new Pcg32(seed: 7)) % RandomMapRoads.VariantStride);
    }

    [Fact]
    public void ReorderingKeepsTheObjectIdOfTheRoad()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Lay(state, map, (5, 10), (6, 10));

        uint uid = state.Roads[Index(6, 10)].Uid;
        uint lastUid = map.LastObjectUid;

        RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        // 旧件按 UID 撤掉，新件用同一个 UID 写上：UID 计数器一次都没动，对象表里的 id 一直有效。
        Assert.Equal(uid, state.Roads[Index(6, 10)].Uid);
        Assert.Equal(lastUid, map.LastObjectUid);
        Assert.NotEqual(RandomMapObjectPhase.TemporaryRoadIndex, state.Roads[Index(6, 10)].ObjectIndex);
    }

    [Fact]
    public void ReorderingTwiceChangesNothing()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Lay(state, map, (5, 10), (6, 10), (7, 10));

        RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7));

        Mp2TileInfo before = map.Tile(6, 10);

        Assert.Equal(0, RandomMapRoads.UpdateAll(map, state, new Pcg32(seed: 7)));
        Assert.Equal(before, map.Tile(6, 10));
    }

    [Fact]
    public void RemovingAnObjectLeavesTheRestOfTheTileAlone()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(12, 12), ObjectGroup.AdventureTreasures, 9));
        uint treasureUid = map.Tile(12, 12).Level1ObjectUid;

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(12, 12), ObjectGroup.Roads, RandomMapObjectPhase.TemporaryRoadIndex));

        Assert.Equal(Mp2ObjectType.ActionTreasureChest, map.Tile(12, 12).ObjectType);

        RandomMapObjectWriter.Remove(map, treasureUid);

        // 宝物撤掉之后那一格只剩路面：动作位与宝物的类型都不见了。
        Assert.NotEqual(Mp2ObjectType.ActionTreasureChest, map.Tile(12, 12).ObjectType);
        Assert.Equal(0, CountPiecesWith(map, treasureUid, tilesOnly: true));
        Assert.Equal(Mp2ObjectType.None, map.Tile(12, 12).ObjectType);
        Assert.Equal(Mp2ObjectIcnType.Road, map.Tile(12, 12).GroundIcnType);
        Assert.NotEqual(0U, map.Tile(12, 12).Level1ObjectUid);
    }

    [Fact]
    public void AGeneratedMapHasEveryRoadJointMatchedToItsNeighbours()
    {
        Mp2Map map = RandomMapGenerator.Generate(
                new RandomMapConfig { Seed = 424242, PlayerCount = 2 }, 72, 72, out RandomMapTerrain? terrain)
            ?? throw new InvalidOperationException("The map was not generated.");

        RandomMapState state = terrain!.State;

        Assert.NotEmpty(state.Roads);

        int joints = 0;

        foreach ((int tileIndex, RandomMapRoadPlacement road) in state.Roads)
        {
            // 每一格的编号都等于「按它现在的邻居算出来的那个」，也就是重排确实跑过、而且跑对了。
            int expected = RandomMapRoads.ObjectIndexAt(map, state, tileIndex, new Pcg32(seed: 1));

            Assert.Equal(expected % RandomMapRoads.VariantStride, road.ObjectIndex % RandomMapRoads.VariantStride);

            int mask = road.ObjectIndex % RandomMapRoads.VariantStride;

            if (mask != 0 && (mask & (mask - 1)) != 0)
            {
                ++joints;
            }
        }

        Assert.True(joints > 0, "no road on the generated map joins two directions");
    }

    /// <summary>Lays a temporary road on every listed tile, the way the placement passes do.</summary>
    private static void Lay(RandomMapState state, Mp2Map map, params (int X, int Y)[] tiles)
    {
        foreach ((int x, int y) in tiles)
        {
            Assert.True(RandomMapObjectPhase.ForceTempRoadOnTile(map, state, Index(x, y)));
        }
    }

    private static int Index(int x, int y) => (y * Size) + x;

    /// <summary>How many pieces of an object the map holds, counting add-ons too unless <paramref name="tilesOnly"/>.</summary>
    private static int CountPiecesWith(Mp2Map map, uint uid, bool tilesOnly)
    {
        int count = 0;

        foreach (Mp2TileInfo tile in map.Tiles)
        {
            if (tile.Level1ObjectUid == uid && tile.HasGroundObject)
            {
                ++count;
            }

            if (tile.Level2ObjectUid == uid && tile.HasTopObject)
            {
                ++count;
            }

            if (tilesOnly)
            {
                continue;
            }

            foreach (int index in map.EnumerateAddonChain(tile))
            {
                Mp2AddonInfo addon = map.Addons[index];

                if (addon.Level1ObjectUid == uid || addon.Level2ObjectUid == uid)
                {
                    ++count;
                }
            }
        }

        return count;
    }

    /// <summary>A blank 36x36 map of plain grass.</summary>
    private static Mp2Map Map() => Mp2Map.CreateBlank(Size, GrassImage);

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
}
