using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two mine passes of <c>generateMap()</c> and the roads that join the regions
/// (<c>map_random_generator.cpp:806-881</c>).
/// </summary>
public sealed class RandomMapMinePhaseTests
{
    private const int Size = 36;

    [Fact]
    public void OnlyAStartingRegionGetsPrimaryMines()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapEconomy economy = new();
        RandomMapRegion region = RegionWithNodes(state, RandomMapRegionType.Neutral);

        Assert.Empty(
            RandomMapMinePhase.PlacePrimaryMines(
                map, state, region, economy, RandomMapMonsterStrength.Normal, new Pcg32(seed: 1)));

        Assert.Equal(0U, economy.MineCount(Mp2ResourceType.Wood));
        Assert.Equal(0U, economy.MineCount(Mp2ResourceType.Ore));
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void AStartingRegionGetsItsWoodAndOreMines()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;
        RandomMapEconomy economy = new();

        SortedSet<int> primary = RandomMapMinePhase.PlacePrimaryMines(
            map,
            state,
            RegionWithNodes(state, RandomMapRegionType.Starting),
            economy,
            RandomMapMonsterStrength.Normal,
            new Pcg32(seed: 2026));

        // 木与铁各一座：两种资源的计数各自加一，说明第二轮没有把第一轮的资源又放一遍。
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Wood));
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Ore));
        Assert.Equal((uint)primary.Count, economy.MineCount(Mp2ResourceType.Wood) + economy.MineCount(Mp2ResourceType.Ore));

        foreach (int mineIndex in primary)
        {
            Assert.NotEqual(0U, state.GetNode(mineIndex).Region);
            Assert.Equal(RandomMapNodeType.Action, state.GetNode(mineIndex).Type);
        }
    }

    [Fact]
    public void AWideRegionGetsItsWholeMineQuota()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;
        RandomMapEconomy economy = new();

        int placed = RandomMapMinePhase.FillRegion(
            map,
            state,
            RegionWithNodes(state, RandomMapRegionType.Expansion),
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Normal),
            regionSizeLimit: 300,
            economy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        Assert.Equal(4, placed);
        Assert.Equal(4U, TotalMines(economy));

        // 资源轮流挑：木、铁、水晶、硫磺各一座。
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Wood));
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Ore));
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Crystal));
        Assert.Equal(1U, economy.MineCount(Mp2ResourceType.Sulfur));
    }

    [Fact]
    public void ASmallRegionGetsOneMineAndANeutralOneGetsTheFullQuota()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        state.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;
        RandomMapEconomy smallEconomy = new();

        int small = RandomMapMinePhase.FillRegion(
            map,
            state,
            RegionWithNodes(state, RandomMapRegionType.Expansion),
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Normal),
            regionSizeLimit: 200,
            smallEconomy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        Assert.Equal(1, small);
        Assert.Equal(1U, TotalMines(smallEconomy));

        Mp2Map neutralMap = Map();
        RandomMapState neutralState = Grid();
        neutralState.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;
        RandomMapEconomy neutralEconomy = new();

        int neutral = RandomMapMinePhase.FillRegion(
            neutralMap,
            neutralState,
            RegionWithNodes(neutralState, RandomMapRegionType.Neutral),
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Normal),
            regionSizeLimit: 200,
            neutralEconomy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        // 中立区是玩家要去抢的地方，再小也给满。
        Assert.Equal(4, neutral);
        Assert.Equal(4U, TotalMines(neutralEconomy));
    }

    [Fact]
    public void GoldMinesNeedAWideRegion()
    {
        Mp2Map narrowMap = Map();
        RandomMapState narrowState = Grid();
        narrowState.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;
        RandomMapEconomy narrowEconomy = new();

        RandomMapMinePhase.FillRegion(
            narrowMap,
            narrowState,
            RegionWithNodes(narrowState, RandomMapRegionType.Expansion),
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Abundant),
            regionSizeLimit: 300,
            narrowEconomy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        // 300 格装得下副矿，但还不到金矿的门槛。
        Assert.Equal(0U, narrowEconomy.MineCount(Mp2ResourceType.Gold));

        Mp2Map wideMap = Map();
        RandomMapState wideState = Grid();
        wideState.GetNodeToUpdate(new Point(3, 3)).Type = RandomMapNodeType.Path;
        RandomMapEconomy wideEconomy = new();

        RandomMapMinePhase.FillRegion(
            wideMap,
            wideState,
            RegionWithNodes(wideState, RandomMapRegionType.Expansion),
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Abundant),
            regionSizeLimit: 400,
            wideEconomy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        Assert.Equal(1U, wideEconomy.MineCount(Mp2ResourceType.Gold));
        Assert.Equal(5U, TotalMines(wideEconomy));
    }

    [Fact]
    public void AWaterRegionGetsNoMines()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapEconomy economy = new();

        int placed = RandomMapMinePhase.FillRegion(
            map,
            state,
            RegionWithNodes(state, RandomMapRegionType.Neutral, MapGround.Water),
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Abundant),
            regionSizeLimit: 400,
            economy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        Assert.Equal(0, placed);
        Assert.Equal(0U, TotalMines(economy));
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void ARegionWithNoOpenTilesGetsNoMines()
    {
        Mp2Map map = Map();
        RandomMapState state = new(Size, Size);
        RandomMapEconomy economy = new();

        // 网格上没有任何区域（Region 全是 0）：矿没有地方站。
        RandomMapRegion region = new(
            1, state.GetNode(new Point(10, 10)), 0, MapGround.Grass, 900, 20000, RandomMapRegionType.Neutral);

        int placed = RandomMapMinePhase.FillRegion(
            map,
            state,
            region,
            RandomMapRegionalObjects.For(RandomMapResourceDensity.Normal),
            regionSizeLimit: 300,
            economy,
            RandomMapMonsterStrength.Normal,
            new SortedSet<int>());

        Assert.Equal(0, placed);
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void ConnectionsAreTurnedIntoRoads()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        // 路与接口格在同一行：参考只认「从直方向到达」的路，斜着摆放的孤立路不算数。
        state.GetNodeToUpdate(new Point(3, 10)).Type = RandomMapNodeType.Path;

        int connection = state.IndexAt(new Point(10, 10));
        RandomMapNode node = state.GetNodeToUpdate(connection);
        node.Type = RandomMapNodeType.Connector;

        RandomMapRegion region = RegionWithNodes(state, RandomMapRegionType.Expansion);
        region.Connections[2] = connection;

        int connected = RandomMapMinePhase.ConnectRegion(map, state, region);

        Assert.Equal(1, connected);
        Assert.Equal(RandomMapNodeType.Path, state.GetNode(connection).Type);

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

        Assert.True(roads > 0, "no road was laid to the connection");
    }

    [Fact]
    public void AConnectionWithNoRoadToReachIsLeftOnTheGrid()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        int connection = state.IndexAt(new Point(10, 10));
        state.GetNodeToUpdate(connection).Type = RandomMapNodeType.Connector;

        RandomMapRegion region = RegionWithNodes(state, RandomMapRegionType.Expansion);
        region.Connections[2] = connection;

        Assert.Equal(0, RandomMapMinePhase.ConnectRegion(map, state, region));

        // 参考也不撤回这一格：它留在 PATH 上，等后落的矿或宝物从旁边经过时被接上。
        Assert.Equal(RandomMapNodeType.Path, state.GetNode(connection).Type);
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void TheThresholdsMatchTheReference()
    {
        Assert.Equal(250, RandomMapMinePhase.RegionSizeForSecondaryMines);
        Assert.Equal(350, RandomMapMinePhase.RegionSizeForGoldMine);
        Assert.Equal(4, RandomMapMinePhase.FirstPrimaryMineRing);
    }

    private static uint TotalMines(RandomMapEconomy economy)
    {
        uint total = 0;

        foreach (Mp2ResourceType resource in Enum.GetValues<Mp2ResourceType>())
        {
            total += economy.MineCount(resource);
        }

        return total;
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
        RandomMapRegion region = new(
            1, state.GetNode(centre), 0, ground, 900, 20000, type);

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
