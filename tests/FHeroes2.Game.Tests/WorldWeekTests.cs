using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the week and the month of the world: <c>World::NewWeek()</c> (<c>world.cpp:462</c>),
/// <c>World::NewMonth()</c> (<c>:493</c>), <c>World::_monthOfMonstersAction()</c> (<c>:500</c>),
/// <c>Maps::updateObjectInfoTile()</c> (<c>maps_tiles_helper.cpp:1641</c>) and
/// <c>World::setUltimateArtifact()</c> (<c>world_loadmap.cpp:1627</c>).
/// </summary>
/// <remarks>
/// The maps are synthetic and 36×36, the smallest size MP2 accepts: the month of monsters scales its wave with the
/// number of tiles, so a smaller map would put the count below the reference's own minimum of one stack.
/// </remarks>
public sealed class WorldWeekTests
{
    /// <summary>Edge of the map, in tiles.</summary>
    private const int MapSize = 36;

    /// <summary>Column and row the paying objects sit on.</summary>
    private const int ObjectColumn = 10;

    /// <summary>Row the paying objects sit on.</summary>
    private const int ObjectRow = 10;

    /// <summary>The tile index those columns and rows make.</summary>
    private const int ObjectTileIndex = (ObjectRow * MapSize) + ObjectColumn;

    /// <summary>Where the digging placeholder sits in the artifact tests.</summary>
    private const int PlaceholderTileIndex = (18 * MapSize) + 18;

    /// <summary>The map's header, which the loader keeps for the version check of the artifact and the week.</summary>
    private static Mp2FileInfo Info() => new()
    {
        FilePath = "week.mp2",
        Name = "Week",
        Description = string.Empty,
        Width = MapSize,
        Height = MapSize,
        Difficulty = GameDifficulty.Normal,
        KingdomColors = PlayerColor.Blue,
        HumanPlayers = PlayerColor.Blue,
        ComputerPlayers = PlayerColor.None,
        VictoryCondition = MapVictoryCondition.DefeatEveryone,
        LossCondition = MapLossCondition.Everything,
        VictoryConditionParameters = [0, 0],
        LossConditionParameters = [0, 0],
    };

    /// <summary>Builds a map of grass with the given objects on it and loads it.</summary>
    /// <param name="tiles">Tile index and file record pairs.</param>
    /// <returns>The loaded world.</returns>
    private static World LoadWorld(params (int Index, Mp2TileInfo Tile)[] tiles)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        foreach ((int index, Mp2TileInfo tile) in tiles)
        {
            map.SetTileAt(index, tile);
        }

        World world = new();
        world.LoadMap(map with { Info = Info() });

        return world;
    }

    /// <summary>
    /// A windmill, <c>OBJ_WINDMILL</c> with the action bit: the week treats it as week life, the refill switch as a
    /// windmill.
    /// </summary>
    /// <returns>The tile record.</returns>
    private static Mp2TileInfo WindmillTile() => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnGra2 << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = 0,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.ActionWindmill,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>A water wheel, the same way round as the windmill.</summary>
    /// <returns>The tile record.</returns>
    private static Mp2TileInfo WaterWheelTile() => WindmillTile() with
    {
        ObjectType = Mp2ObjectType.ActionWaterWheel,
    };

    /// <summary>A magic garden, the same way round as the windmill.</summary>
    /// <returns>The tile record.</returns>
    private static Mp2TileInfo MagicGardenTile() => WindmillTile() with
    {
        ObjectType = Mp2ObjectType.ActionMagicGarden,
    };

    /// <summary>
    /// The editor's <c>OBJ_RANDOM_ULTIMATE_ARTIFACT</c> placeholder; <paramref name="radius"/> is the 13 bit data word
    /// the map stores, which the loader puts into the first metadata lane.
    /// </summary>
    /// <param name="radius">Radius around the placeholder the artifact may be buried in; 0 means anywhere.</param>
    /// <returns>The tile record.</returns>
    private static Mp2TileInfo PlaceholderTile(uint radius = 0) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnGra2 << 2),
        BottomIcnImageIndex = 0,

        // The data word lives in the two quantity bytes, three bits per layer: `((quantity2 << 8) + quantity1) >> 3`.
        Quantity1 = unchecked((byte)(radius << 3)),
        Quantity2 = (byte)(radius >> 5),
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.ActionRandomUltimateArtifact,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>A mine, which is an in-game action object and therefore a place the month of monsters likes.</summary>
    /// <returns>The tile record.</returns>
    private static Mp2TileInfo MineTile() => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnGra2 << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = 0,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.ActionMine,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    [Fact]
    public void AMapWithoutAPlaceholderStillGetsAnArtifact()
    {
        World world = LoadWorld();

        int position = world.UltimateArtifact.Position;

        Assert.True(position >= 0, "a map without a placeholder must still bury an artifact");
        Assert.False(world.UltimateArtifact.IsFound);

        // A ninth of the map's width along every edge is never used.
        Assert.InRange(position % MapSize, MapSize / 9, MapSize - 1 - (MapSize / 9));
        Assert.InRange(position / MapSize, MapSize / 9, MapSize - 1 - (MapSize / 9));
    }

    [Fact]
    public void ThePlaceholderIsRemovedWhenTheArtifactIsBuried()
    {
        World world = LoadWorld((PlaceholderTileIndex, PlaceholderTile()));

        Assert.NotEqual(
            Mp2ObjectType.ActionRandomUltimateArtifact,
            world.GetTile(PlaceholderTileIndex).GetMainObjectType(world));
        Assert.True(world.UltimateArtifact.Position >= 0);
    }

    [Fact]
    public void TheRadiusKeepsTheArtifactClose()
    {
        World world = LoadWorld((PlaceholderTileIndex, PlaceholderTile(radius: 2)));

        int position = world.UltimateArtifact.Position;
        int dx = Math.Abs((position % MapSize) - (PlaceholderTileIndex % MapSize));
        int dy = Math.Abs((position / MapSize) - (PlaceholderTileIndex / MapSize));

        Assert.InRange(dx, 0, 2);
        Assert.InRange(dy, 0, 2);
    }

    [Fact]
    public void TheWindmillPaysTwoOfANonWoodResource()
    {
        World world = LoadWorld((ObjectTileIndex, WindmillTile()));
        Tile tile = world.GetTile(ObjectTileIndex);

        for (int i = 0; i < 20; ++i)
        {
            tile.UpdateObjectInfoTile(world);

            Assert.NotEqual(Resource.Wood, (int)tile.Metadata[0]);
            Assert.NotEqual(Resource.Unknown, (int)tile.Metadata[0]);
            Assert.Equal(2u, tile.Metadata[1]);
        }
    }

    [Fact]
    public void TheWaterWheelPaysDoubleFromTheSecondWeekOn()
    {
        World world = LoadWorld((ObjectTileIndex, WaterWheelTile()));
        Tile tile = world.GetTile(ObjectTileIndex);

        // Day 0 is the loader's own settlement, not a week: the reference compares against 0, so it pays 500.
        tile.UpdateObjectInfoTile(world);

        Assert.Equal(Resource.Gold, (int)tile.Metadata[0]);
        Assert.Equal(500u, tile.Metadata[1]);

        world.NewDay();
        tile.UpdateObjectInfoTile(world);

        Assert.Equal(1000u, tile.Metadata[1]);
    }

    [Fact]
    public void TheMagicGardenRollsGemsOrGold()
    {
        World world = LoadWorld((ObjectTileIndex, MagicGardenTile()));
        Tile tile = world.GetTile(ObjectTileIndex);

        for (int i = 0; i < 20; ++i)
        {
            tile.UpdateObjectInfoTile(world);

            bool gems = tile.Metadata[0] == (uint)Resource.Gems && tile.Metadata[1] == 5;
            bool gold = tile.Metadata[0] == (uint)Resource.Gold && tile.Metadata[1] == 500;

            Assert.True(gems || gold, $"unexpected garden payload {tile.Metadata[0]}/{tile.Metadata[1]}");
        }
    }

    [Fact]
    public void TheNewWeekRefillsTheWeekLifeObjects()
    {
        World world = LoadWorld((ObjectTileIndex, WindmillTile()));
        Tile tile = world.GetTile(ObjectTileIndex);

        tile.Metadata[0] = 0;
        tile.Metadata[1] = 0;

        // The first week is skipped: the loader has just settled the objects. `BeginWeek()` is `day % 7 == 1`
        // (world.h:396-399) and the clock starts at day 0, so the second week opens on the eighth day.
        for (int i = 0; i < 8; ++i)
        {
            world.NewDay();
        }

        Assert.NotEqual(0u, tile.Metadata[0]);
        Assert.Equal(2u, tile.Metadata[1]);
    }

    [Fact]
    public void TheMonthOfMonstersPlacesAStackNearAnActionObject()
    {
        World world = LoadWorld((ObjectTileIndex, MineTile()));

        world.MonthOfMonstersAction(MonsterType.Peasant);

        int stacks = 0;
        int monsters = 0;

        for (int index = 0; index < MapSize * MapSize; ++index)
        {
            uint count = WanderingMonstersAt(world, index);

            if (count > 0)
            {
                ++stacks;
                monsters += (int)count;
            }
        }

        Assert.True(stacks > 0, "the month of monsters must place at least one stack");
        Assert.True(monsters > 0);
        Assert.True(stacks <= 4, $"the wave is capped at a few stacks on a map this small, got {stacks}");
    }

    [Fact]
    public void AnUnknownMonthMonsterPlacesNothing()
    {
        World world = LoadWorld((ObjectTileIndex, MineTile()));

        world.MonthOfMonstersAction(MonsterType.Unknown);

        for (int index = 0; index < MapSize * MapSize; ++index)
        {
            Assert.Equal(0u, WanderingMonstersAt(world, index));
        }
    }

    /// <summary>
    /// The size of the wandering stack on a tile, or zero when the tile holds no monster.
    /// </summary>
    /// <remarks>
    /// <c>getMonsterCountFromTile()</c> asserts when the tile is not a monster count holder
    /// (<c>maps_tiles_helper.cpp:856</c>), so the type has to be checked first; a dwelling or an abandoned mine would
    /// otherwise pass through the same lane.
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="index">The tile index.</param>
    /// <returns>The count, 0 when there is no wandering stack.</returns>
    private static uint WanderingMonstersAt(World world, int index)
    {
        Tile tile = world.GetTile(index);

        return tile.GetMainObjectType(world) == Mp2ObjectType.ActionMonster ? tile.GetMonsterCount() : 0u;
    }
}
