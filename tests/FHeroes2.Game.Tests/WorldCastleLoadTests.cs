using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the town half of the map loader: the town table of the file
/// (<c>world_loadmap.cpp:296-355</c>), the two castle information blocks (<c>world_loadmap.cpp:596-641</c>), the
/// repaint of a random town (<c>maps.cpp:717-819</c>) and the naming of the towns the map leaves unnamed
/// (<c>world_loadmap.cpp:92-111</c>).
/// </summary>
public sealed class WorldCastleLoadTests
{
    private const int MapSize = 36;

    /// <summary>Column of the town of every test, far enough from the edges to have a whole footprint.</summary>
    private const int TownX = 18;

    /// <summary>Row of the town of every test.</summary>
    private const int TownY = 18;

    /// <summary>The entrance tile of the town of every test.</summary>
    private const int TownTileIndex = (TownY * MapSize) + TownX;

    /// <summary>The sprite index <c>OBJNTWRD</c> gives the gate, entry thirteen of the layout.</summary>
    private const int GateImageIndex = 13;

    /// <summary>
    /// Tile offsets of the sixteen <c>OBJNTOWN</c> sprites, relative to the gate, in the order
    /// <see cref="World.UpdateCastleSprite"/> walks them (<c>maps.cpp:781-782</c>).
    /// </summary>
    private static readonly (int X, int Y)[] TownTileOffsets =
    [
        (0, -3), (-2, -2), (-1, -2), (0, -2), (1, -2), (2, -2), (-2, -1), (-1, -1),
        (0, -1), (1, -1), (2, -1), (-2, 0), (-1, 0), (0, 0), (1, 0), (2, 0),
    ];

    /// <summary>
    /// Tile offsets of the sixteen <c>OBJNTWSH</c> shadows, relative to the gate, in the same order
    /// (<c>maps.cpp:783-784</c>). Six of them sit on a town tile; the town sprite wins there, as it does in
    /// the reference because that tile was already repainted when the shadow is reached.
    /// </summary>
    private static readonly (int X, int Y)[] ShadowTileOffsets =
    [
        (-4, -2), (-3, -2), (-2, -2), (-1, -2), (-5, -1), (-4, -1), (-3, -1), (-2, -1),
        (-1, -1), (-4, 0), (-3, 0), (-2, 0), (-1, 0), (-3, 1), (-2, 1), (-1, 1),
    ];

    [Fact]
    public void ATownTableEntryBecomesATown()
    {
        World world = new();
        world.LoadMap(MapWithTown(block: null));

        CastleTown castle = Assert.Single(world.Castles);

        Assert.Equal(TownTileIndex, castle.TileIndex);
        Assert.Equal(MapRace.Knight, castle.Race);
        Assert.Equal(PlayerColor.None, castle.Color);
        Assert.False(castle.IsCastle);

        // The entrance answers, and the wider lookup answers with the same town.
        Assert.Same(castle, world.GetCastleEntrance(TownTileIndex));
        Assert.Same(castle, world.GetCastle(TownTileIndex));
        Assert.Same(castle, world.GetCastle(TownX, TownY));
        Assert.True(world.IsValidCastleEntrance(TownTileIndex));

        // The town is capturable from the moment it exists, as a neutral one until a block says otherwise.
        Assert.True(world.HasCapturedObject(TownTileIndex));
        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(TownTileIndex));
    }

    [Fact]
    public void TheFootprintOfATownAnswersForTheTilesItCovers()
    {
        World world = new();
        world.LoadMap(MapWithTown(block: null));

        // The square of the sprite, including the tile at its top, is the town.
        Assert.NotNull(world.GetCastle(TownX, TownY - 3));
        Assert.NotNull(world.GetCastle(TownX - 2, TownY - 2));
        Assert.NotNull(world.GetCastle(TownX + 2, TownY - 2));
        Assert.NotNull(world.GetCastle(TownX - 2, TownY));
        Assert.NotNull(world.GetCastle(TownX + 2, TownY));

        // The tiles below the gate are not: that is where a visiting hero stands.
        Assert.Null(world.GetCastle(TownX, TownY + 1));
        Assert.Null(world.GetCastle(TownX, TownY + 2));

        // A tile of the footprint is not an entrance, so the narrower lookup rejects it.
        Assert.False(world.IsValidCastleEntrance((TownY - 3) * MapSize + TownX));
        Assert.Null(world.GetCastleEntrance((TownY - 3) * MapSize + TownX));
    }

    [Fact]
    public void ATownBlockFillsTheTownTheTableMade()
    {
        World world = new();

        world.LoadMap(MapWithTown(TownBlock(owner: 2, name: "Stonehelm", faction: 3, dwellings: 0x0008)));

        CastleTown castle = Assert.Single(world.Castles);

        Assert.Equal("Stonehelm", castle.Name);
        Assert.Equal(PlayerColor.Red, castle.Color);
        Assert.Equal(MapRace.Warlock, castle.Race);
        Assert.True(castle.IsCastle);
        Assert.True(castle.IsBuild(BuildingType.DwellingMonster1));

        // The garrison takes the town's colour and the first dwelling is stocked with a week of creatures.
        Assert.Equal(PlayerColor.Red, castle.Army.Color);
        Assert.True(castle.GetMonstersInDwelling(BuildingType.DwellingMonster1) > 0);

        // The capture table follows the owner.
        Assert.Equal(PlayerColor.Red, world.ColorCapturedObject(TownTileIndex));
    }

    [Fact]
    public void ARandomTownsTilesStopClaimingToBeRandom()
    {
        World world = new();

        world.LoadMap(MapWithTown(
            TownBlock(faction: 0),
            tileType: Mp2ObjectType.RandomTown));

        CastleTown castle = Assert.Single(world.Castles);
        Tile entrance = world.GetTile(TownTileIndex);

        Assert.Equal(TownTileIndex, castle.TileIndex);
        Assert.Equal(MapRace.Knight, castle.Race);

        // The entrance is a castle now, and its sprite is the town's, not the random town's: entry thirteen of
        // the knight town graphics, which is where the gate sits in the layout.
        Assert.Equal(Mp2ObjectType.Castle, entrance.MainObjectType);
        Assert.Equal(Mp2ObjectIcnType.ObjnTown, entrance.MainObjectPart.IcnType);
        Assert.Equal(GateImageIndex, entrance.MainObjectPart.IcnIndex);

        // So is the footprint: the sprite two rows above the gate is entry three of the same graphics, and the
        // shadow to the left of the town becomes a town shadow rather than a random town sprite.
        Tile top = world.GetTile((TownY - 2) * MapSize + TownX);
        Assert.Equal(Mp2ObjectIcnType.ObjnTown, top.MainObjectPart.IcnType);
        Assert.Equal(3, top.MainObjectPart.IcnIndex);

        Tile shadow = world.GetTile((TownY - 2) * MapSize + TownX - 4);
        Assert.Equal(Mp2ObjectIcnType.ObjnTwsh, shadow.MainObjectPart.IcnType);

        // And the random object type is gone from every tile.
        Assert.DoesNotContain(
            Enumerable.Range(0, MapSize * MapSize),
            index => world.GetTile(index).MainObjectType is Mp2ObjectType.RandomTown or Mp2ObjectType.RandomCastle);
    }

    [Fact]
    public void ATownOnTheCoastKeepsItsShipyard()
    {
        World world = new();

        world.LoadMap(MapWithTown(
            TownBlock(common: 0x0008, faction: 0),
            waterBelow: true));

        CastleTown castle = Assert.Single(world.Castles);

        Assert.True(castle.HasSeaAccess);
        Assert.True(castle.IsBuild(BuildingType.Shipyard));
    }

    [Fact]
    public void ATownWithoutWaterLosesItsShipyard()
    {
        World world = new();

        world.LoadMap(MapWithTown(TownBlock(common: 0x0008, faction: 0)));

        CastleTown castle = Assert.Single(world.Castles);

        Assert.False(castle.HasSeaAccess);
        Assert.False(castle.IsBuild(BuildingType.Shipyard));
    }

    [Fact]
    public void WaterOnlyToTheSideOfTheGateStillCounts()
    {
        World world = new();

        // The reference probes the tile below the town and then its two neighbours, so a lake that only touches
        // the left of the gate is enough (castle.cpp:1836-1875).
        Mp2Map map = MapWithTown(TownBlock(common: 0x0008, faction: 0));
        map.SetTileAt((TownY + 2) * MapSize + TownX - 1, WaterTile());

        world.LoadMap(map);

        CastleTown castle = Assert.Single(world.Castles);

        Assert.True(castle.HasSeaAccess);
        Assert.True(castle.IsBuild(BuildingType.Shipyard));
    }

    [Fact]
    public void TheDifficultyOfTheGameDecidesTheDefaultBuildingsOfATown()
    {
        // 参考的 Castle::_setDefaultBuildings() 问的是 Game::getDifficulty()（castle.cpp:542），也就是玩家挑的那一档，
        // 不是地图头里的那一档。这里让地图头说 Easy（二级兵营 75%）、会话难度说 Impossible（0%）：一座没有自定义
        // 建筑的城一定只剩第一兵营。地图头若被误用，八次装载里一次都抽不到第二兵营的概率只有 0.25^8。
        byte[] block = TownBlock(faction: 0);
        block[1] = 0; // No custom buildings, so the block rolls the defaults.

        for (int attempt = 0; attempt < 8; ++attempt)
        {
            Mp2Map map = MapWithTown(block);
            map = map with { Info = map.Info with { Difficulty = GameDifficulty.Easy } };

            World world = new();
            world.LoadMap(map, GameDifficulty.Impossible);

            CastleTown castle = Assert.Single(world.Castles);

            Assert.True(castle.IsBuild(BuildingType.DwellingMonster1));
            Assert.False(castle.IsBuild(BuildingType.DwellingMonster2));
        }

        // 没有玩家的选择时（测试与工具）退回地图头那一档，也就是老口径。
        Mp2Map headerOnly = MapWithTown(block);
        headerOnly = headerOnly with { Info = headerOnly.Info with { Difficulty = GameDifficulty.Impossible } };

        World headerWorld = new();
        headerWorld.LoadMap(headerOnly);

        Assert.False(Assert.Single(headerWorld.Castles).IsBuild(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void UnnamedTownsGetDifferentDefaultNames()
    {
        World world = new();
        world.LoadMap(MapWithTwoTowns(TownBlock(faction: 0), TownBlock(faction: 0)));

        Assert.Equal(2, world.Castles.Count);

        string first = world.Castles[0].Name;
        string second = world.Castles[1].Name;

        Assert.Contains(first, CastleTown.DefaultNames);
        Assert.Contains(second, CastleTown.DefaultNames);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void AMapNameIsKeptAndNotHandedOutAgain()
    {
        World world = new();

        // "Blackridge" is the first default name, so a second town that draws the same name would collide with it.
        world.LoadMap(MapWithTwoTowns(TownBlock(name: "Blackridge", faction: 0), TownBlock(faction: 0)));

        Assert.Equal("Blackridge", world.Castles[0].Name);
        Assert.NotEqual("Blackridge", world.Castles[1].Name);
    }

    [Fact]
    public void ATownOutsideTheMapIsSkipped()
    {
        World world = new();
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex)
            with { Castles = [new Mp2CastlePosition(MapSize + 5, 1, 0, MapRace.Knight)] };

        world.LoadMap(map);

        Assert.Empty(world.Castles);
    }

    [Fact]
    public void ABrokenTownBlockLeavesTheTownAsTheTableMadeIt()
    {
        World world = new();

        world.LoadMap(MapWithTown(new byte[CastleTown.Mp2BlockSize - 1]));

        CastleTown castle = Assert.Single(world.Castles);

        Assert.Equal(PlayerColor.None, castle.Color);
        Assert.Equal(MapRace.Knight, castle.Race);
    }

    [Fact]
    public void RemovingATownKeepsTheOtherTownsAnswering()
    {
        AllCastles castles = new(MapSize);

        CastleTown first = new((5 * MapSize) + 5, MapRace.Knight);
        CastleTown second = new(TownTileIndex, MapRace.Wizard);

        castles.Add(first);
        castles.Add(second);

        Assert.Same(second, castles.Get(TownTileIndex));

        castles.Remove(5, 5);

        Assert.Single(castles);
        Assert.Same(second, castles.Get(TownTileIndex));
        Assert.Null(castles.Get(5, 5));
    }

    /// <summary>
    /// A 36 by 36 grass map with one town on <see cref="TownTileIndex"/> and, when asked for, water below its gate.
    /// </summary>
    /// <param name="block">The town's information block, or <see langword="null"/> for a town without one.</param>
    /// <param name="tileType">The object type of the entrance tile: a castle or a random town.</param>
    /// <param name="waterBelow">Whether to put water on the tile below the town.</param>
    /// <param name="isCastle">Whether the town has its fort; a random town draws its sprites from either half.</param>
    /// <returns>The map.</returns>
    private static Mp2Map MapWithTown(
        byte[]? block,
        Mp2ObjectType tileType = Mp2ObjectType.Castle,
        bool waterBelow = false,
        bool isCastle = true)
    {
        bool isRandom = tileType is Mp2ObjectType.RandomTown or Mp2ObjectType.RandomCastle;

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        if (isRandom)
        {
            PaintRandomTown(map, isCastle);
        }

        // A real map describes every tile of the town, not only its gate: that is what the repaint of a random
        // town walks over, and a gate at sprite index zero would not be found by it (maps.cpp:788-818).
        map.SetTileAt(
            TownTileIndex,
            TownTile(tileType, imageIndex: isRandom ? GateImageIndex + (isCastle ? 0 : 16) : 0));

        if (waterBelow)
        {
            map.SetTileAt(((TownY + 2) * MapSize) + TownX, WaterTile());
        }

        map = map with { Castles = [new Mp2CastlePosition(TownX, TownY, 0, MapRace.Knight)] };

        return block is null ? map : map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>
    /// Draws the sixteen <c>OBJNTWRD</c> town sprites and the sixteen <c>OBJNTWSH</c> shadows of a random town on
    /// the map, all of them sharing the gate's object identifier, which is what a map file does.
    /// </summary>
    /// <remarks>
    /// Only the tiles of the gate's box claim to be a random town: that is the box the reference's
    /// <c>ReplaceRandomCastleObjectId()</c> walks (<c>maps.cpp:720-728</c>), so a map that marked the shadow left of
    /// the town as random would keep a random object type after loading — the reference does not fix those either.
    /// </remarks>
    /// <param name="map">The map to paint.</param>
    /// <param name="isCastle">Whether the town has its fort, i.e. whether to use the first or the second half.</param>
    private static void PaintRandomTown(Mp2Map map, bool isCastle)
    {
        int indexOffset = isCastle ? 0 : 16;
        HashSet<(int X, int Y)> painted = [];

        for (int index = 0; index < 16; ++index)
        {
            if (painted.Add(TownTileOffsets[index]))
            {
                SetRandomTownPart(map, TownTileOffsets[index], imageIndex: index + indexOffset);
            }

            // The shadow sprites continue after the thirty-two town ones, hence the plus thirty-two.
            if (painted.Add(ShadowTileOffsets[index]))
            {
                SetRandomTownPart(map, ShadowTileOffsets[index], imageIndex: index + indexOffset + 32);
            }
        }
    }

    /// <summary>Puts one <c>OBJNTWRD</c> part on the tile an offset points at.</summary>
    /// <param name="map">The map to paint.</param>
    /// <param name="offset">Offset of the tile, relative to the gate.</param>
    /// <param name="imageIndex">Sprite index inside <c>OBJNTWRD</c>.</param>
    private static void SetRandomTownPart(Mp2Map map, (int X, int Y) offset, int imageIndex)
    {
        bool inGateBox = offset.X is >= -2 and < 3 && offset.Y is >= -3 and < 2;

        map.SetTileAt(
            ((TownY + offset.Y) * MapSize) + TownX + offset.X,
            RandomTownPart(inGateBox ? Mp2ObjectType.RandomTown : Mp2ObjectType.None, imageIndex));
    }

    /// <summary>
    /// One tile of a random town's footprint: an <c>OBJNTWRD</c> ground object with the town's identifier and no
    /// metadata, so that only the gate refers to the information block.
    /// </summary>
    /// <param name="type">The object type of the tile.</param>
    /// <param name="imageIndex">Sprite index inside <c>OBJNTWRD</c>.</param>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo RandomTownPart(Mp2ObjectType type, int imageIndex) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTwrd << 2),
        BottomIcnImageIndex = (byte)imageIndex,
        Quantity1 = 1,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = type,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// A map with two towns, one at [5, 5] and one on <see cref="TownTileIndex"/>, each with its own block.
    /// </summary>
    /// <param name="first">The block of the town at [5, 5].</param>
    /// <param name="second">The block of the town on <see cref="TownTileIndex"/>.</param>
    /// <returns>The map.</returns>
    private static Mp2Map MapWithTwoTowns(byte[] first, byte[] second)
    {
        const int firstTileIndex = (5 * MapSize) + 5;

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(firstTileIndex, TownTile(Mp2ObjectType.Castle, blockId: 1));
        map.SetTileAt(TownTileIndex, TownTile(Mp2ObjectType.Castle, blockId: 2));

        return map with
        {
            Castles =
            [
                new Mp2CastlePosition(5, 5, 0, MapRace.Knight),
                new Mp2CastlePosition(TownX, TownY, 0, MapRace.Knight),
            ],
            InfoBlocks = [new Mp2InfoBlock(0, first), new Mp2InfoBlock(1, second)],
        };
    }

    /// <summary>A town tile of the map file: the object type of the entrance and the block it refers to.</summary>
    /// <param name="type">The object type of the tile.</param>
    /// <param name="blockId">The information block the tile refers to.</param>
    /// <param name="imageIndex">Sprite index inside <c>OBJNTWRD</c>.</param>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo TownTile(Mp2ObjectType type, int blockId = 1, int imageIndex = 0) =>
        RandomTownPart(type, imageIndex) with
        {
            // Object layer in the low two bits, and the block id in the 13 bit quantity word.
            Quantity1 = (byte)((blockId << 3) | 1),
        };

    /// <summary>A plain water tile, which is every terrain image below the first grass one.</summary>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo WaterTile() => new()
    {
        TerrainImageIndex = MapGrounds.WaterStartImageIndex,
        ObjectName1 = 0,
        BottomIcnImageIndex = 0,
        Quantity1 = 0,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.None,
        NextAddonIndex = 0,
        Level1ObjectUid = 0,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// Builds a 70 byte town block, <see cref="CastleTown.Mp2BlockSize"/> bytes of <c>castle.cpp:102-395</c>.
    /// </summary>
    /// <param name="owner">The owner byte: 0 to 5 are blue to purple, anything else is "nobody".</param>
    /// <param name="name">The town's name, or <see langword="null"/> for a town the map leaves unnamed.</param>
    /// <param name="faction">The race byte: 0 to 5 are the six races, anything else asks the owner or rolls.</param>
    /// <param name="hasCastle">Whether the town has its fort.</param>
    /// <param name="common">The common building bits.</param>
    /// <param name="dwellings">The dwelling bits.</param>
    /// <returns>The block.</returns>
    private static byte[] TownBlock(
        int owner = 255,
        string? name = null,
        int faction = 0,
        bool hasCastle = true,
        ushort common = 0,
        ushort dwellings = 0)
    {
        byte[] data = new byte[CastleTown.Mp2BlockSize];

        data[0] = (byte)owner;
        data[1] = 1; // Custom buildings, so the block says what it means.

        data[2] = (byte)common;
        data[3] = (byte)(common >> 8);
        data[4] = (byte)dwellings;
        data[5] = (byte)(dwellings >> 8);
        data[6] = 0; // No mage guild.

        if (name is not null)
        {
            data[24] = 1;

            byte[] encoded = MapTextCodec.Encode(name, 13);

            encoded.CopyTo(data.AsSpan(25));
        }

        data[38] = (byte)faction;
        data[39] = (byte)(hasCastle ? 1 : 0);

        return data;
    }
}
