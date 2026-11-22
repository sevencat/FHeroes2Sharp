using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the map objects whose data sits in an information block: the sign and bottle branch and the
/// ground event branch of <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:625-640</c>), and the event
/// branch of <c>Maps::Tiles::updateObjectType()</c> (<c>maps_tiles.cpp:1599</c>).
/// </summary>
public sealed class WorldMapObjectTests
{
    /// <summary>
    /// Side of the smallest maps the tests can build — the file format only knows 36, 72, 108 and 144.
    /// </summary>
    private const int MapSize = 36;

    /// <summary>Tile the object of every test stands on.</summary>
    private const int ObjectTileIndex = 37;

    /// <summary>Id of the single information block of every test, as stored in the quantity word.</summary>
    private const int BlockId = 1;

    [Fact]
    public void ASignBlockGivesItsMessage()
    {
        World world = new();
        world.LoadMap(MapWithObjectTile(SignTile(), SignBlock("Treasure east of here.")));

        MapSign sign = world.GetMapSign(ObjectTileIndex)!;

        Assert.Equal("Treasure east of here.", sign.Message.Text);
        Assert.Equal(ObjectTileIndex, sign.TileIndex);
        Assert.Equal((uint)ObjectTileIndex, sign.Uid);
        Assert.Single(world.MapObjects.All);
    }

    [Fact]
    public void ASignWithoutAMessageKeepsAnEmptyOne()
    {
        World world = new();
        world.LoadMap(MapWithObjectTile(SignTile(), SignBlock(string.Empty)));

        // The reference fills the message with a random line; the port has no language table here yet.
        Assert.Equal(string.Empty, world.GetMapSign(ObjectTileIndex)!.Message.Text);
    }

    [Fact]
    public void ABlockThatIsNotASignIsSkipped()
    {
        World world = new();
        byte[] block = SignBlock("Not a sign.");

        block[0] = 0;

        world.LoadMap(MapWithObjectTile(SignTile(), block));

        Assert.Null(world.GetMapSign(ObjectTileIndex));
        Assert.Equal(0, world.MapObjects.Count);
    }

    [Fact]
    public void AnEventBlockGivesItsResourcesAndFlags()
    {
        World world = new();
        world.LoadMap(MapWithObjectTile(EventTile(), EventBlock("A gift.")));

        MapEvent mapEvent = world.GetMapEvent(ObjectTileIndex)!;

        Assert.Equal(100, mapEvent.Resources.Wood);
        Assert.Equal(2, mapEvent.Resources.Mercury);
        Assert.Equal(3, mapEvent.Resources.Ore);
        Assert.Equal(4, mapEvent.Resources.Sulfur);
        Assert.Equal(5, mapEvent.Resources.Crystal);
        Assert.Equal(6, mapEvent.Resources.Gems);
        Assert.Equal(1000, mapEvent.Resources.Gold);

        // The file counts artifacts from 0, the engine from 1.
        Assert.Equal((ArtifactType)6, mapEvent.Artifact);
        Assert.True(mapEvent.IsComputerPlayerAllowed);
        Assert.False(mapEvent.IsSingleTimeEvent);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Yellow, mapEvent.Colors);
        Assert.Equal("A gift.", mapEvent.Message);
    }

    [Fact]
    public void AnEventWithItsBlockKeepsItsTypeWhenRecomputed()
    {
        World world = new();
        world.LoadMap(MapWithObjectTile(EventTile(), EventBlock("A gift.")));

        Tile tile = world.GetTile(ObjectTileIndex);

        tile.UpdateObjectType(world);

        Assert.NotNull(world.GetMapEvent(ObjectTileIndex));
        Assert.Equal(Mp2ObjectType.ActionEvent, tile.MainObjectType);
    }

    [Fact]
    public void AnEventWhoseBlockIsMissingLosesItsTypeWhenRecomputed()
    {
        World world = new();

        // A tile that claims to be an event but has no information block. The loader keeps the type of the
        // file — the passability pass only recomputes tiles that have no type — so the test asks for the
        // recomputation the reference runs when an object leaves a tile (maps_tiles.cpp:1599-1611).
        world.LoadMap(MapWithObjectTile(EventTile(), block: null));

        Assert.Null(world.GetMapEvent(ObjectTileIndex));

        Tile tile = world.GetTile(ObjectTileIndex);

        tile.UpdateObjectType(world);

        Assert.Equal(Mp2ObjectType.None, tile.MainObjectType);
    }

    /// <summary>
    /// A map with one tile that carries an object with an information block.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="block">Payload of the block, or <see langword="null"/> for a map without blocks.</param>
    private static Mp2Map MapWithObjectTile(Mp2TileInfo tile, byte[]? block)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(ObjectTileIndex, tile);

        return block is null ? map : map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>
    /// A sign of the generic terrain layer, <c>OBJN_MUL2</c> sprite 114. The file stores the action value,
    /// which is what the reference calls <c>MP2::OBJ_SIGN</c>.
    /// </summary>
    private static Mp2TileInfo SignTile() => ObjectTile(Mp2ObjectType.ActionSign, 114);

    /// <summary>
    /// A ground event of the generic terrain layer, <c>OBJN_MUL2</c> sprite 163. The file stores the action
    /// value, which is what the reference calls <c>MP2::OBJ_EVENT</c>.
    /// </summary>
    private static Mp2TileInfo EventTile() => ObjectTile(Mp2ObjectType.ActionEvent, 163);

    /// <summary>
    /// A tile with one object of the object layer whose quantity word refers to the single block.
    /// </summary>
    /// <param name="type">Object type of the file.</param>
    /// <param name="spriteIndex">Sprite index of the object.</param>
    private static Mp2TileInfo ObjectTile(Mp2ObjectType type, int spriteIndex) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnMul2 << 2),
        BottomIcnImageIndex = (byte)spriteIndex,

        // Object layer in the low two bits, and the block id in the 13 bit quantity word.
        Quantity1 = (byte)((BlockId << 3) | 1),
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
    /// A sign block: marker, eight unused bytes, the message and its NUL terminator.
    /// </summary>
    /// <param name="message">Message of the sign.</param>
    private static byte[] SignBlock(string message)
    {
        byte[] text = Encoding.ASCII.GetBytes(message);
        byte[] data = new byte[10 + text.Length];

        data[0] = 0x01;

        text.CopyTo(data, 9);

        return data;
    }

    /// <summary>
    /// An event block: marker, seven resource amounts, an artifact, two flags, ten unused bytes, six color
    /// flags and the message.
    /// </summary>
    /// <param name="message">Message of the event.</param>
    private static byte[] EventBlock(string message)
    {
        byte[] text = Encoding.ASCII.GetBytes(message);
        byte[] data = new byte[50 + text.Length];

        data[0] = 0x01;

        // Wood, mercury, ore, sulfur, crystal, gems, gold, in the order of the file.
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 100);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(5), 2);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(9), 3);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(13), 4);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(17), 5);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(21), 6);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(25), 1000);

        // Artifact 5 of the file, which is artifact 6 of the engine.
        data[29] = 5;
        data[30] = 0;

        data[31] = 1; // also for computer players
        data[32] = 0; // not a single time event
        data[43] = 1; // blue
        data[46] = 1; // yellow

        text.CopyTo(data, 49);

        return data;
    }
}
