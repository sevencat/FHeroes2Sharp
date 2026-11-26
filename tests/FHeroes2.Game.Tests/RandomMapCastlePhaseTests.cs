using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The castle pass of <c>generateMap()</c>: <c>placeCastle()</c> and the step 5 driver
/// (<c>map_random_generator_helper.cpp:824-895</c>, <c>map_random_generator.cpp:788-804</c>).
/// </summary>
public sealed class RandomMapCastlePhaseTests : IDisposable
{
    private const int Size = 36;

    private const ushort GrassImage = MapGrounds.GrassStartImageIndex + 5;

    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-random-castle-{Guid.NewGuid():N}.mp2");

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public void AStartingCastleTakesItsTableEntryAndItsBlock()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(PlaceCastle(map, state, RandomMapRegionType.Starting, colorIndex: 1));

        Mp2CastlePosition castle = Assert.Single(map.Castles);
        Assert.Equal(10, castle.X);
        Assert.Equal(10, castle.Y);
        Assert.True(castle.HasFort);

        // 主格指向城堡块；块里的主人、种族、城堡位与表一致。
        uint metadata = map.Tile(10, 10).Metadata;
        Assert.NotEqual(0U, metadata);

        Mp2InfoBlock block = Assert.Single(map.InfoBlocks, entry => entry.ObjectId == (int)metadata);
        Assert.Equal(RandomMapCastlePhase.CastleBlockSize, block.Data.Length);
        Assert.Equal(1, (int)block.Data[0]);
        Assert.Equal(MapRaces.ToIndex(castle.Race), (int)block.Data[37]);
        Assert.Equal(1, (int)block.Data[38]);
        Assert.Equal(Mp2ObjectType.ActionCastle, map.Tile(10, 10).ObjectType);
    }

    [Fact]
    public void TheHeroStandsBelowTheCastleWithItsOwnBlock()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(PlaceCastle(map, state, RandomMapRegionType.Starting, colorIndex: 1));

        Mp2TileInfo hero = map.Tile(10, 11);

        Assert.Equal(Mp2ObjectType.ActionHero, hero.ObjectType);
        Assert.Equal(RandomMapNodeType.Action, state.GetNode(new Point(10, 11)).Type);

        Mp2InfoBlock block = Assert.Single(map.InfoBlocks, entry => entry.ObjectId == (int)hero.Metadata);
        Assert.Equal(RandomMapCastlePhase.HeroBlockSize, block.Data.Length);
        Assert.All(block.Data, value => Assert.Equal(0, (int)value));

        // 七个精灵一组，最后一个是「随机英雄」，主人是 region 的颜色。
        Mp2MapHero sprite = Mp2MapHeroes.FromSpriteIndex(hero.BottomIcnImageIndex, 10, 11);
        Assert.Equal(PlayerColor.Green, sprite.Color);
    }

    [Fact]
    public void TheTwoFlagsStandBesideTheEntrance()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(PlaceCastle(map, state, RandomMapRegionType.Starting, colorIndex: 1));

        Assert.NotEqual(Mp2ObjectType.None, map.Tile(9, 10).ObjectType);
        Assert.NotEqual(Mp2ObjectType.None, map.Tile(11, 10).ObjectType);
        Assert.NotEqual(map.Tile(9, 10).BottomIcnImageIndex, map.Tile(11, 10).BottomIcnImageIndex);
    }

    [Fact]
    public void TheRoadOutOfTheCastleIsLaidDown()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(PlaceCastle(map, state, RandomMapRegionType.Starting, colorIndex: 1));

        Assert.Equal(Mp2ObjectIcnType.Road, map.Tile(10, 12).GroundIcnType);
        Assert.True(CarriesRoad(map, map.Tile(10, 11)));
        Assert.Equal(RandomMapNodeType.Path, state.GetNode(new Point(10, 12)).Type);
    }

    [Fact]
    public void ANeutralTownHasNoOwnerAndNoHero()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();

        Assert.True(PlaceCastle(
            map, state, RandomMapRegionType.Neutral, RandomMapTerrainGenerator.NeutralColorIndex, isCastle: false));

        Mp2CastlePosition castle = Assert.Single(map.Castles);
        Assert.False(castle.HasFort);

        Mp2InfoBlock block = Assert.Single(map.InfoBlocks);
        Assert.Equal(0xFF, (int)block.Data[0]);
        Assert.Equal(map.Tile(10, 10).Metadata, (uint)block.ObjectId);

        Assert.Equal(Mp2ObjectType.None, map.Tile(10, 11).ObjectType);
    }

    [Fact]
    public void AWaterTileTakesNoCastle()
    {
        Mp2Map map = Mp2Map.CreateBlank(Size, MapGrounds.WaterStartImageIndex);
        RandomMapState state = Grid();

        Assert.False(PlaceCastle(map, state, RandomMapRegionType.Starting, colorIndex: 1));
        Assert.Empty(map.Castles);
        Assert.Empty(map.InfoBlocks);
        Assert.Equal(0U, map.LastObjectUid);
    }

    [Fact]
    public void ASmallNeutralRegionGetsAPathInsteadOfACastle()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapRegion region = RegionWithNodes(
            state, RandomMapRegionType.Neutral, RandomMapTerrainGenerator.NeutralColorIndex);

        Assert.True(RandomMapCastlePhase.PlaceCastles(
            map, state, [WaterRegion(state), region], regionSizeLimit: 2000,
            RandomMapResourceDensity.Normal, new Pcg32(seed: 5)));

        Assert.Empty(map.Castles);
        Assert.Equal(RandomMapNodeType.Path, state.GetNode(region.CenterIndex).Type);
    }

    [Fact]
    public void ABigNeutralRegionGetsATown()
    {
        Mp2Map map = Map();
        RandomMapState state = Grid();
        RandomMapRegion region = RegionWithNodes(
            state, RandomMapRegionType.Neutral, RandomMapTerrainGenerator.NeutralColorIndex);

        Assert.True(RandomMapCastlePhase.PlaceCastles(
            map, state, [WaterRegion(state), region], regionSizeLimit: 10,
            RandomMapResourceDensity.Normal, new Pcg32(seed: 5)));

        Mp2CastlePosition castle = Assert.Single(map.Castles);
        Assert.False(castle.HasFort);
    }

    [Fact]
    public void AStartingRegionThatCannotTakeItsCastleFailsThePass()
    {
        Mp2Map map = Mp2Map.CreateBlank(Size, MapGrounds.WaterStartImageIndex);
        RandomMapState state = Grid();

        Assert.False(RandomMapCastlePhase.PlaceCastles(
            map, state, [WaterRegion(state), RegionWithNodes(state, RandomMapRegionType.Starting, colorIndex: 1)],
            regionSizeLimit: 2000, RandomMapResourceDensity.Normal, new Pcg32(seed: 5)));
    }

    [Fact]
    public void AGeneratedMapGivesEveryPlayerACastleAndAHero()
    {
        RandomMapConfig config = new() { Seed = 424242, PlayerCount = 2 };

        Mp2Map generated = RandomMapGenerator.Generate(config, 72, 72)
            ?? throw new InvalidOperationException("The map was not generated.");

        // 玩家各一座起点城堡；大的中立区还可能带一座无主的城镇，所以只要求不少于玩家人数。
        Assert.True(generated.Castles.Count >= config.PlayerCount);

        // 从零生成的图只能用 WriteAll 落盘，读回来之后城镇表与英雄表都由文件重建。
        Mp2MapWriter.WriteAll(_filePath, generated);
        Mp2Map loaded = Mp2Map.Read(_filePath);

        Assert.Equal(generated.Castles.Count, loaded.Castles.Count);

        // 英雄只有玩家城堡才有：每人一个，中立城镇没有。
        Assert.Equal(config.PlayerCount, loaded.Heroes.Count);

        foreach (Mp2CastlePosition castle in loaded.Castles)
        {
            Assert.NotEqual(Mp2ObjectType.None, loaded.Tile(castle.X, castle.Y).ObjectType);
        }
    }

    [Fact]
    public void TheGeneratedMapGivesEveryPlayerKingdomATownAndAHero()
    {
        RandomMapConfig config = new() { Seed = 20261126, PlayerCount = 2 };

        Mp2Map generated = RandomMapGenerator.Generate(config, 72, 72)
            ?? throw new InvalidOperationException("The map was not generated.");

        Mp2MapWriter.WriteAll(_filePath, generated);

        World world = new();
        world.LoadMap(Mp2Map.Read(_filePath));

        for (int index = 0; index < config.PlayerCount; ++index)
        {
            Kingdom.Kingdom kingdom = world.GetKingdom(PlayerColors.Ordered[index]);

            Assert.NotEmpty(kingdom.Castles);
            Assert.NotEmpty(kingdom.Heroes);
            Assert.Equal(PlayerColors.Ordered[index], kingdom.Castles[0].Color);
        }
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

    /// <summary>The water placeholder every region list starts with.</summary>
    private static RandomMapRegion WaterRegion(RandomMapState state) =>
        new(0, state.GetNode(0), RandomMapTerrainGenerator.NeutralColorIndex, MapGround.Water, 1, 0,
            RandomMapRegionType.Neutral);

    /// <summary>A region whose node list covers the whole grid.</summary>
    private static RandomMapRegion RegionWithNodes(
        RandomMapState state, RandomMapRegionType type, int colorIndex, MapGround ground = MapGround.Grass)
    {
        Point centre = new(18, 18);
        RandomMapRegion region = new(1, state.GetNode(centre), colorIndex, ground, 900, 20000, type);

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

    private static bool PlaceCastle(Mp2Map map, RandomMapState state, RandomMapRegionType type, int colorIndex,
        bool isCastle = true) =>
        RandomMapCastlePhase.PlaceCastle(
            map, state, RegionWithNodes(state, type, colorIndex), new Point(10, 10), isCastle, new Pcg32(seed: 2026));

    /// <summary>Whether a tile draws a road on any layer, the <c>Maps::doesContainRoad()</c> test.</summary>
    private static bool CarriesRoad(Mp2Map map, Mp2TileInfo tile)
    {
        if (tile.GroundIcnType == Mp2ObjectIcnType.Road || tile.TopIcnType == Mp2ObjectIcnType.Road)
        {
            return true;
        }

        return map.EnumerateAddonChain(tile).Any(index =>
            map.Addons[index].GroundIcnType == Mp2ObjectIcnType.Road
            || map.Addons[index].TopIcnType == Mp2ObjectIcnType.Road);
    }
}
