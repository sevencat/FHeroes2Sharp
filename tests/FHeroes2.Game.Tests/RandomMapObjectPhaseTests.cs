using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The glue of the object phase: one object, a whole prefab with its rolled prizes, and the guard
/// (<c>placeSimpleObject()</c>, <c>placeValidTreasures()</c>, <c>placeMonster()</c> of
/// <c>map_random_generator_helper.cpp</c>).
/// </summary>
public sealed class RandomMapObjectPhaseTests
{
    private const int Size = 36;

    [Fact]
    public void AWholePrefabLandsOnTheMap()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapRegion region = Region(state, MapGround.Grass, treasure: 20000);

        int value = RandomMapObjectPhase.PlaceTreasureGroup(
            map,
            state,
            region,
            RandomMapObjectSets.Prefabs[0],
            new Point(10, 10),
            RandomMapMonsterStrength.Normal,
            new Pcg32(seed: 2026));

        // 三丛树挡在外圈。
        Assert.Equal(Mp2ObjectType.Trees, map.Tile(10, 9).ObjectType);
        Assert.Equal(Mp2ObjectType.Trees, map.Tile(13, 9).ObjectType);
        Assert.Equal(Mp2ObjectType.Trees, map.Tile(13, 11).ObjectType);

        // 四个奖品落在树围起来的中间四格，锚点上站一只守卫。
        Assert.False(map.Tile(11, 9).IsEmpty);
        Assert.False(map.Tile(12, 9).IsEmpty);
        Assert.False(map.Tile(11, 10).IsEmpty);
        Assert.False(map.Tile(12, 10).IsEmpty);

        // 目录里的怪物存的是带动作位的类型（ActionMonster 一族），ICN 一定是 MONS32。
        Assert.Equal(Mp2ObjectIcnType.Mons32, map.Tile(10, 10).GroundIcnType);
        Assert.True(Mp2ObjectTypes.IsActionObject(map.Tile(10, 10).ObjectType));

        Assert.True(value >= 4 * RandomMapObjectValues.MinimumTreasureValue);
    }

    [Fact]
    public void TheBudgetShrinksByWhatWasPlaced()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapRegion region = Region(state, MapGround.Grass, treasure: 20000);
        int before = region.TreasureLimit;

        int value = RandomMapObjectPhase.PlaceTreasureGroup(
            map,
            state,
            region,
            RandomMapObjectSets.Prefabs[0],
            new Point(10, 10),
            RandomMapMonsterStrength.Normal,
            new Pcg32(seed: 7));

        Assert.Equal(before - value, region.TreasureLimit);
    }

    [Fact]
    public void TheObstaclesAreMarkedAndTheAnchorTakesTheGuard()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        RandomMapObjectPhase.PlaceTreasureGroup(
            map,
            state,
            Region(state, MapGround.Grass, treasure: 20000),
            RandomMapObjectSets.Prefabs[0],
            new Point(10, 10),
            RandomMapMonsterStrength.Normal,
            new Pcg32(seed: 2026));

        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(new Point(10, 9)).Type);
        Assert.Equal(RandomMapNodeType.Action, state.GetNode(new Point(10, 10)).Type);
    }

    [Fact]
    public void ACheapGroupGetsNoGuard()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapObjectSet onePrize = new(
            Obstacles: [],
            Valuables:
            [
                new RandomMapObjectPlacement(
                    new Point(1, 0), ObjectGroup.AdventureTreasures, RandomMapObjectSets.TreasureIndex),
            ],
            EntranceCheck: []);

        int value = RandomMapObjectPhase.PlaceTreasureGroup(
            map,
            state,
            Region(state, MapGround.Grass, treasure: 700),
            onePrize,
            new Point(10, 10),
            RandomMapMonsterStrength.Normal,
            new Pcg32(seed: 11));

        // 一件宝物撑不起 2000 的保护价值：锚点留空，奖品自己占一格。
        Assert.True(value < 2000);
        Assert.True(map.Tile(10, 10).IsEmpty);
        Assert.False(map.Tile(11, 10).IsEmpty);
    }

    [Fact]
    public void TheTerrainVariantFollowsTheGround()
    {
        Mp2Map grass = Map();
        Mp2Map snow = Map();
        RandomMapState state = Grid();
        RandomMapObjectPlacement tree = new(new Point(1, 0), ObjectGroup.LandscapeTrees, 3);

        Assert.True(RandomMapObjectPhase.PlaceSimpleObject(
            grass, state, new Point(10, 10), tree, MapGround.Grass));
        Assert.True(RandomMapObjectPhase.PlaceSimpleObject(
            snow, state, new Point(20, 10), tree, MapGround.Snow));

        // 同一个「树 3」在两块地上是不同的图。
        Assert.NotEqual(grass.Tile(11, 10).GroundIcnType, snow.Tile(21, 10).GroundIcnType);
    }

    [Fact]
    public void AnObjectOffTheMapIsRejected()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.False(RandomMapObjectPhase.PlaceSimpleObject(
            map,
            state,
            new Point(Size - 1, 10),
            new RandomMapObjectPlacement(new Point(1, 0), ObjectGroup.LandscapeTrees, 3),
            MapGround.Grass));

        Assert.True(map.Tile(Size - 1, 10).IsEmpty);
        Assert.Equal(RandomMapNodeType.Open, state.GetNode(new Point(Size - 1, 10)).Type);
    }

    [Fact]
    public void AGuardStandsOnAnActionTile()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(RandomMapObjectPhase.PlaceMonster(
            map, state, new Point(5, 5), new RandomMapMonsterSelection(MonsterType.RandomMonsterLevel4, [])));

        Assert.Equal(Mp2ObjectIcnType.Mons32, map.Tile(5, 5).GroundIcnType);
        Assert.True(Mp2ObjectTypes.IsActionObject(map.Tile(5, 5).ObjectType));
        Assert.Equal(RandomMapNodeType.Action, state.GetNode(new Point(5, 5)).Type);

        // 空选择什么也不放。
        Assert.False(RandomMapObjectPhase.PlaceMonster(
            map, state, new Point(6, 5), RandomMapMonsterSelection.None));
        Assert.True(map.Tile(6, 5).IsEmpty);
    }

    [Fact]
    public void ATemporaryRoadIsWrittenOntoTheTile()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        int index = state.IndexAt(new Point(10, 10));

        Assert.True(RandomMapObjectPhase.ForceTempRoadOnTile(map, state, index));

        Assert.Equal(Mp2ObjectIcnType.Road, map.Tile(10, 10).GroundIcnType);
        Assert.Equal(RandomMapNodeType.Path, state.GetNode(index).Type);
    }

    [Fact]
    public void AnObstacleGetsNoRoad()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        int index = state.IndexAt(new Point(10, 10));

        state.GetNodeToUpdate(index).Type = RandomMapNodeType.Obstacle;

        Assert.False(RandomMapObjectPhase.ForceTempRoadOnTile(map, state, index));

        Assert.True(map.Tile(10, 10).IsEmpty);
        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(index).Type);
    }

    [Fact]
    public void TheGroundIsCleanedOfEmbeddedObjectsBeforeTheRoadGoesDown()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        Mp2TileInfo before = map.Tile(10, 10);

        // 一张自带装饰的草地（reference: getRandomTerrainImageIndex( ground, true )）。
        ushort decorated = DecoratedGrassImage();
        MapGround ground = MapGrounds.FromTerrainImageIndex(decorated);

        map.SetTile(10, 10, before with { TerrainImageIndex = decorated });

        Assert.True(RandomMapObjectPhase.ForceTempRoadOnTile(map, state, state.IndexAt(new Point(10, 10))));

        Mp2TileInfo after = map.Tile(10, 10);

        Assert.False(MapGrounds.ContainsEmbeddedObjects(after.TerrainImageIndex));
        Assert.Equal(ground, MapGrounds.FromTerrainImageIndex(after.TerrainImageIndex));
        Assert.Equal(Mp2ObjectIcnType.Road, after.GroundIcnType);
    }

    [Fact]
    public void ARoadThatIsAlreadyThereIsNotWrittenTwice()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        int index = state.IndexAt(new Point(10, 10));

        Assert.True(RandomMapObjectPhase.ForceTempRoadOnTile(map, state, index));

        Mp2TileInfo once = map.Tile(10, 10);

        Assert.True(RandomMapObjectPhase.ForceTempRoadOnTile(map, state, index));
        Assert.Equal(once, map.Tile(10, 10));
    }

    /// <summary>The first grass image that carries embedded decorations.</summary>
    private static ushort DecoratedGrassImage()
    {
        for (ushort image = 0; image < 1000; ++image)
        {
            if (MapGrounds.FromTerrainImageIndex(image) == MapGround.Grass
                && MapGrounds.ContainsEmbeddedObjects(image))
            {
                return image;
            }
        }

        throw new InvalidOperationException("The ground table has no decorated grass image.");
    }

    [Fact]
    public void PlanningLeavesTheGridAlone()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapRegion region = RegionWithNodes(state, treasure: 20000);

        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;

        string[] before = Snapshot(state);

        List<(int Tile, RandomMapObjectSet Set)> plan =
            RandomMapObjectPhase.PlanObjectPlacement(state, region, RandomMapObjectSets.Prefabs, new Pcg32(seed: 2026));

        Assert.NotEmpty(plan);
        Assert.Equal(before, Snapshot(state));
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void ARegionWithoutARoadPlansNothing()
    {
        RandomMapState state = Grid();

        Assert.Empty(RandomMapObjectPhase.PlanObjectPlacement(
            state, RegionWithNodes(state, treasure: 20000), RandomMapObjectSets.Prefabs, new Pcg32(seed: 2026)));
    }

    [Fact]
    public void ARegionWithoutBudgetPlansNothing()
    {
        RandomMapState state = Grid();

        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;

        Assert.Empty(RandomMapObjectPhase.PlanObjectPlacement(
            state, RegionWithNodes(state, treasure: 0), RandomMapObjectSets.Prefabs, new Pcg32(seed: 2026)));
    }

    [Fact]
    public void GroupsLandTogetherWithTheirRoads()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapRegion region = RegionWithNodes(state, treasure: 20000);

        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;

        int placed = RandomMapObjectPhase.PlaceObjectSet(
            map,
            state,
            region,
            RandomMapObjectSets.Prefabs,
            RandomMapMonsterStrength.Normal,
            expectedCount: 2,
            new Pcg32(seed: 99));

        Assert.InRange(placed, 1, 2);
        Assert.True(region.TreasureLimit < 20000);

        int roads = 0;

        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                if (map.Tile(x, y).GroundIcnType == Mp2ObjectIcnType.Road)
                {
                    ++roads;
                }
            }
        }

        Assert.True(roads > 0, "no road was laid");
    }

    [Fact]
    public void NothingIsPlacedWhenTheRegionWantsNothing()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;

        Assert.Equal(
            0,
            RandomMapObjectPhase.PlaceObjectSet(
                map,
                state,
                RegionWithNodes(state, treasure: 20000),
                RandomMapObjectSets.Prefabs,
                RandomMapMonsterStrength.Normal,
                expectedCount: 0,
                new Pcg32(seed: 99)));

        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void AMineLandsWithItsGuardAndItsRoad()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapEconomy economy = new();
        int mine = state.IndexAt(new Point(20, 20));

        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;

        Assert.Equal(
            mine,
            RandomMapObjectPhase.PlaceMine(
                map, state, economy, [mine], Mp2ResourceType.Gold, RandomMapMonsterStrength.Normal));

        Assert.NotEqual(0U, map.Tile(20, 20).Level1ObjectUid);
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Gold));

        // 守卫挡在矿的正下方。
        Assert.Equal(Mp2ObjectIcnType.Mons32, map.Tile(20, 21).GroundIcnType);

        // 到最近那条路之间铺上了路。
        int roads = 0;

        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                if (map.Tile(x, y).GroundIcnType == Mp2ObjectIcnType.Road)
                {
                    ++roads;
                }
            }
        }

        Assert.True(roads > 0, "no road was laid to the mine");
    }

    [Fact]
    public void AMineWithNoRoadToReachItIsSkipped()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapEconomy economy = new();

        // 区域里一条路也没有：placeActionObject 修不出路，矿就放不下。
        Assert.Equal(
            -1,
            RandomMapObjectPhase.PlaceMine(
                map, state, economy, [state.IndexAt(new Point(20, 20))], Mp2ResourceType.Gold,
                RandomMapMonsterStrength.Normal));

        Assert.True(map.Tile(20, 20).IsEmpty);
        Assert.Equal(0U, economy.MineCount(Mp2ResourceType.Gold));
    }

    /// <summary>The type and region of every tile, to prove that planning changed nothing.</summary>
    private static string[] Snapshot(RandomMapState state)
    {
        string[] snapshot = new string[state.Count];

        for (int index = 0; index < state.Count; ++index)
        {
            RandomMapNode node = state.GetNode(index);

            snapshot[index] = $"{node.Region}:{node.Type}";
        }

        return snapshot;
    }

    /// <summary>A region whose node list covers the whole area of <see cref="Grid"/>.</summary>
    private static RandomMapRegion RegionWithNodes(RandomMapState state, int treasure)
    {
        Point centre = new(10, 10);
        RandomMapRegion region = new(1, state.GetNode(centre), 0, MapGround.Grass, 900, treasure, RandomMapRegionType.Neutral);

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

    /// <summary>A region anchored at (10, 10) with the given ground and treasure budget.</summary>
    private static RandomMapRegion Region(RandomMapState state, MapGround ground, int treasure) =>
        new(1, state.GetNode(new Point(10, 10)), 0, ground, 100, treasure, RandomMapRegionType.Neutral);
}

