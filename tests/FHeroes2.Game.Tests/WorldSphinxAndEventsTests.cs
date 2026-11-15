using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the information blocks that are not a hero: the sphinx (<c>world_loadmap.cpp:642-650</c>), the
/// daily events and the rumors, which are blocks no object of the map points at
/// (<c>world_loadmap.cpp:656-684</c>).
/// </summary>
public sealed class WorldSphinxAndEventsTests
{
    /// <summary>Side of the smallest maps the tests can build.</summary>
    private const int MapSize = 36;

    /// <summary>Tile the object of every test stands on; 1 is grass, far from the edges.</summary>
    private const int ObjectTileIndex = 37;

    /// <summary>Id of the single information block of every test, as stored in the quantity word.</summary>
    private const int BlockId = 1;

    [Fact]
    public void ASphinxBlockGivesItsRiddleAndItsReward()
    {
        World world = new();
        world.LoadMap(MapWithSphinx(SphinxBlock("What walks on four legs?", ["a table", "a chair"])));

        MapSphinx sphinx = world.GetMapSphinx(ObjectTileIndex)!;

        Assert.Equal("What walks on four legs?", sphinx.Riddle);
        Assert.Equal(["a table", "a chair"], sphinx.Answers);
        Assert.Equal(100, sphinx.Resources.Gold);
        Assert.Equal(2, sphinx.Resources.Wood);
        Assert.Equal((ArtifactType)5, sphinx.Artifact);
        Assert.True(sphinx.IsValid);
        Assert.Equal(ObjectTileIndex, sphinx.TileIndex);
    }

    [Fact]
    public void AnAnswerIsComparedOnItsFirstFourLetters()
    {
        World world = new();
        world.LoadMap(MapWithSphinx(SphinxBlock("Who is it?", ["abracadabra"])));

        MapSphinx sphinx = world.GetMapSphinx(ObjectTileIndex)!;

        // The original game compares four characters, because an answer is cut to 13 bytes in the file and
        // the keyboard of 1994 could not do better (maps_objects.cpp:273).
        Assert.True(sphinx.IsCorrectAnswer("ABRA"));
        Assert.True(sphinx.IsCorrectAnswer("abra cadabra"));
        // Everything after the fourth character is ignored, so a longer word with the same beginning counts.
        Assert.True(sphinx.IsCorrectAnswer("abram"));
        Assert.False(sphinx.IsCorrectAnswer("ab"));
    }

    [Fact]
    public void ASphinxWithoutARiddleIsNotRead()
    {
        World world = new();
        world.LoadMap(MapWithSphinx(SphinxBlock(string.Empty, ["nothing"])));

        // validate() drops a sphinx that asks nothing, so the tile stays without an object
        // (maps_objects.h:117-132).
        Assert.Null(world.GetMapSphinx(ObjectTileIndex));
        Assert.Equal(0, world.MapObjects.Count);
    }

    [Fact]
    public void ADailyEventCarriesItsDayAndItsColors()
    {
        World world = new();
        world.LoadMap(MapWithWorldBlock(DailyEventBlock("A gift from the gods.")));

        EventDate eventDate = Assert.Single(world.DailyEvents);

        Assert.Equal("A gift from the gods.", eventDate.Message);
        Assert.Equal(100, eventDate.Resources.Gold);
        Assert.Equal(10u, eventDate.FirstOccurrenceDay);
        Assert.Equal(5u, eventDate.RepeatPeriodInDays);
        Assert.True(eventDate.IsApplicableForAIPlayers);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, eventDate.Colors);

        Assert.True(eventDate.IsAllowed(PlayerColor.Blue, 10));
        Assert.True(eventDate.IsAllowed(PlayerColor.Red, 15));
        Assert.False(eventDate.IsAllowed(PlayerColor.Green, 10));
        Assert.False(eventDate.IsAllowed(PlayerColor.Blue, 9));
        Assert.False(eventDate.IsAllowed(PlayerColor.Blue, 16));
        Assert.False(eventDate.IsDeprecated(15));
    }

    [Fact]
    public void AnEventWithoutAPeriodFiresOnce()
    {
        World world = new();
        world.LoadMap(MapWithWorldBlock(DailyEventBlock("Once.", repeat: 0)));

        EventDate eventDate = Assert.Single(world.DailyEvents);

        Assert.True(eventDate.IsAllowed(PlayerColor.Blue, 10));
        Assert.False(eventDate.IsAllowed(PlayerColor.Blue, 11));
        Assert.True(eventDate.IsDeprecated(11));
        Assert.False(eventDate.IsDeprecated(10));
    }

    [Fact]
    public void ARumorBlockGivesItsText()
    {
        World world = new();
        world.LoadMap(MapWithWorldBlock(RumorBlock("The dragons sleep under the mountain.")));

        Assert.Equal(["The dragons sleep under the mountain."], world.Rumors);
        Assert.Empty(world.DailyEvents);
    }

    [Fact]
    public void ACastleBlockIsNotTakenForADailyEvent()
    {
        World world = new();

        // A castle block is at least 70 bytes and starts with a zero byte, but a castle tile carries it, so
        // the block never reaches the world branch (world_loadmap.cpp:473-483).
        world.LoadMap(MapWithCastleBlock(CastleBlock()));

        Assert.Empty(world.DailyEvents);
        Assert.Empty(world.Rumors);
    }

    /// <summary>
    /// A map with one sphinx tile and the given information block.
    /// </summary>
    /// <param name="block">Payload of the block.</param>
    private static Mp2Map MapWithSphinx(byte[] block)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(ObjectTileIndex, ObjectTile(ObjectTileType(Mp2ObjectType.Sphinx), 79));

        return map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>
    /// A map with one castle tile and the given information block.
    /// </summary>
    /// <param name="block">Payload of the block.</param>
    private static Mp2Map MapWithCastleBlock(byte[] block)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(ObjectTileIndex, ObjectTile(ObjectTileType(Mp2ObjectType.Castle), 24));

        return map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>A map without objects that carries one block of its own.</summary>
    /// <param name="block">Payload of the block.</param>
    private static Mp2Map MapWithWorldBlock(byte[] block)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        return map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>Object type of a tile, kept in a method so the helper below reads better.</summary>
    /// <param name="type">The type.</param>
    private static Mp2ObjectType ObjectTileType(Mp2ObjectType type) => type;

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
    /// A sphinx block: marker, seven resource amounts, an artifact, the number of answers, eight answers of
    /// 13 bytes and the riddle.
    /// </summary>
    /// <param name="riddle">The question.</param>
    /// <param name="answers">The correct answers, at most eight.</param>
    /// <param name="gold">Amount of gold, 100 by default.</param>
    /// <param name="wood">Amount of wood, 2 by default.</param>
    /// <param name="artifactId">Artifact id as the file counts it, from 0.</param>
    private static byte[] SphinxBlock(string riddle, string[] answers, int gold = 100, int wood = 2, int artifactId = 4)
    {
        byte[] text = Encoding.ASCII.GetBytes(riddle);
        byte[] data = new byte[137 + text.Length];

        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), wood);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(25), gold);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(29), (ushort)artifactId);

        data[31] = (byte)Math.Min(answers.Length, 8);

        for (int slot = 0; slot < data[31]; ++slot)
        {
            Encoding.ASCII.GetBytes(answers[slot]).CopyTo(data, 32 + (slot * 13));
        }

        text.CopyTo(data, 136);

        return data;
    }

    /// <summary>
    /// A daily event block: marker, seven resource amounts, the flags, the days, six color flags and the
    /// message; byte 42 is the constant that tells it from a rumor.
    /// </summary>
    /// <param name="message">The message of the event.</param>
    /// <param name="repeat">Repeat period in days; 0 for a single occurrence.</param>
    private static byte[] DailyEventBlock(string message, ushort repeat = 5)
    {
        byte[] text = Encoding.ASCII.GetBytes(message);
        byte[] data = new byte[50 + text.Length];

        data[0] = 0x00;

        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 7);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(25), 100);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(31), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(33), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(35), repeat);

        data[42] = 0x01;
        data[43] = 1; // blue
        data[45] = 1; // red

        text.CopyTo(data, 49);

        return data;
    }

    /// <summary>A rumor block: marker, seven unused bytes and the text.</summary>
    /// <param name="rumor">The text.</param>
    private static byte[] RumorBlock(string rumor)
    {
        byte[] text = Encoding.ASCII.GetBytes(rumor);

        return [0x00, 0, 0, 0, 0, 0, 0, 0, .. text, 0];
    }

    /// <summary>
    /// A castle block: a 70 byte payload whose byte 42 is not the daily event constant.
    /// </summary>
    private static byte[] CastleBlock()
    {
        byte[] data = new byte[70];

        data[0] = 0x00;
        data[42] = 0x02;

        return data;
    }
}
