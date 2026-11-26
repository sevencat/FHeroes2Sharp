using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The writer that puts a catalogue object into an MP2 map, the port of <c>putObjectOnMap()</c> and
/// <c>placeObjectOnTile()</c> (<c>map_random_generator_helper.cpp:761</c>, <c>maps_tiles_helper.cpp:195</c>).
/// </summary>
public sealed class RandomMapObjectWriterTests
{
    private const int Size = 36;

    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-object-writer-{Guid.NewGuid():N}.mp2");

    [Fact]
    public void ATreeGoesIntoTheGroundLane()
    {
        Mp2Map map = Map();

        Assert.True(RandomMapObjectWriter.TryWrite(
            map, new Point(5, 5), Object(Mp2ObjectType.Trees, (Mp2ObjectIcnType.ObjnArti, 7, 0, 0))));

        Mp2TileInfo tile = map.Tile(5, 5);

        Assert.Equal(Mp2ObjectType.Trees, tile.ObjectType);
        Assert.Equal(Mp2ObjectIcnType.ObjnArti, tile.GroundIcnType);
        Assert.Equal(7, tile.BottomIcnImageIndex);
        Assert.Equal((int)ObjectLayerType.Object, tile.GroundLayer);
        Assert.False(tile.HasTopObject);
        Assert.NotEqual(0U, tile.Level1ObjectUid);
        Assert.Equal(0, tile.NextAddonIndex);
    }

    [Fact]
    public void EveryPartOfAnObjectSharesOneUid()
    {
        Mp2Map map = Map();

        Assert.True(RandomMapObjectWriter.TryWrite(
            map,
            new Point(5, 5),
            Object(
                Mp2ObjectType.Trees,
                (Mp2ObjectIcnType.ObjnArti, 7, 0, 0),
                (Mp2ObjectIcnType.ObjnArti, 8, 1, 0))));

        uint uid = map.Tile(5, 5).Level1ObjectUid;

        Assert.NotEqual(0U, uid);
        Assert.Equal(uid, map.Tile(6, 5).Level1ObjectUid);
        Assert.Equal(uid, map.LastObjectUid);
    }

    [Fact]
    public void ATopPartGoesIntoTheTopLane()
    {
        Mp2Map map = Map();
        ObjectInfo info = Object(Mp2ObjectType.Trees, (Mp2ObjectIcnType.ObjnArti, 7, 0, 0));

        info.AddTopPart(Mp2ObjectIcnType.ObjnArti, 9, new Point(0, 0), Mp2ObjectType.Trees);

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(5, 5), info));

        Mp2TileInfo tile = map.Tile(5, 5);

        Assert.True(tile.HasTopObject);
        Assert.Equal(Mp2ObjectIcnType.ObjnArti, tile.TopIcnType);
        Assert.Equal(9, tile.TopIcnImageIndex);
        Assert.Equal(tile.Level1ObjectUid, tile.Level2ObjectUid);
    }

    [Fact]
    public void ASecondPartOfTheSameTileBecomesAnAddon()
    {
        Mp2Map map = Map();
        ObjectInfo info = Object(Mp2ObjectType.Trees, (Mp2ObjectIcnType.ObjnArti, 4, 0, 0));

        // 影子先列、物件后列：层号小的（Object 0）才是瓦片本体的那一个。
        info.GroundLevelParts.Insert(
            0,
            new LayeredObjectPartInfo(
                Mp2ObjectIcnType.ObjnArti, 3, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Shadow));

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(5, 5), info));

        Mp2TileInfo tile = map.Tile(5, 5);

        Assert.Equal(4, tile.BottomIcnImageIndex);
        Assert.NotEqual(0, tile.NextAddonIndex);

        Mp2AddonInfo addon = map.Addons[tile.NextAddonIndex];

        Assert.Equal(3, addon.BottomIcnImageIndex);
        Assert.Equal((int)ObjectLayerType.Shadow, addon.GroundLayer);
        Assert.Equal(tile.Level1ObjectUid, addon.Level1ObjectUid);
    }

    [Fact]
    public void TheMetadataWordSurvives()
    {
        Mp2Map map = Map();
        ObjectInfo info = Object(Mp2ObjectType.Mine, (Mp2ObjectIcnType.ObjnArti, 0, 0, 0));

        // 13 位：矿的资源类型就存在这里，8191 是能存下的最大一个值。
        info.Metadata[0] = 8191;

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(5, 5), info));

        Assert.Equal(8191U, map.Tile(5, 5).Metadata);
        Assert.Equal((int)ObjectLayerType.Object, map.Tile(5, 5).GroundLayer);
    }

    [Fact]
    public void AnActionObjectNamesTheTile()
    {
        Mp2Map map = Map();

        Assert.True(RandomMapObjectWriter.TryWrite(
            map,
            new Point(5, 5),
            Object(Mp2ObjectType.ActionTreasureChest, (Mp2ObjectIcnType.ObjnArti, 1, 0, 0))));

        Assert.Equal(Mp2ObjectType.ActionTreasureChest, map.Tile(5, 5).ObjectType);
    }

    [Fact]
    public void AnObjectThatLeavesTheMapIsRejected()
    {
        Mp2Map map = Map();

        Assert.False(RandomMapObjectWriter.TryWrite(
            map,
            new Point(Size - 1, 5),
            Object(
                Mp2ObjectType.Trees,
                (Mp2ObjectIcnType.ObjnArti, 7, 0, 0),
                (Mp2ObjectIcnType.ObjnArti, 8, 1, 0))));

        Assert.Equal(0U, map.LastObjectUid);
        Assert.True(map.Tile(Size - 1, 5).IsEmpty);
        Assert.True(map.Tile(Size - 1, 6).IsEmpty);
    }

    [Fact]
    public void TheSameActionObjectIsNotPlacedTwice()
    {
        Mp2Map map = Map();
        ObjectInfo chest = Object(Mp2ObjectType.ActionTreasureChest, (Mp2ObjectIcnType.ObjnArti, 7, 0, 0));

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(5, 5), chest));

        Mp2TileInfo before = map.Tile(5, 5);

        Assert.False(RandomMapObjectWriter.TryWrite(map, new Point(5, 5), chest));
        Assert.Equal(before, map.Tile(5, 5));
    }
    [Fact]
    public void ARealCatalogueObjectGoesThroughTheFile()
    {
        Mp2Map map = Map();

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(10, 10), ObjectGroup.LandscapeTrees, 3));

        Mp2TileInfo written = map.Tile(10, 10);

        Assert.Equal(Mp2ObjectType.Trees, written.ObjectType);

        try
        {
            // 从零生成的图没有源文件可以补丁，只能用 WriteAll（Save 会去读源文件）。
            Mp2MapWriter.WriteAll(_filePath, map);

            Mp2Map loaded = Mp2Map.Read(_filePath);

            Assert.Equal(written.ObjectType, loaded.Tile(10, 10).ObjectType);
            Assert.Equal(written.GroundIcnType, loaded.Tile(10, 10).GroundIcnType);
            Assert.Equal(written.BottomIcnImageIndex, loaded.Tile(10, 10).BottomIcnImageIndex);
            Assert.Equal(written.Level1ObjectUid, loaded.Tile(10, 10).Level1ObjectUid);
            Assert.Equal(written.NextAddonIndex, loaded.Tile(10, 10).NextAddonIndex);
            Assert.Equal(map.LastObjectUid, loaded.LastObjectUid);
        }
        finally
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }

    [Fact]
    public void AMonsterWritesNoCount()
    {
        Mp2Map map = Map();
        ObjectInfo monster = Object(Mp2ObjectType.Monster, (Mp2ObjectIcnType.Mons32, 5, 0, 0));

        // 目录对怪物存的是怪物编号（这里是 5），它绝不能落到瓦片的「数量」字段上。
        monster.Metadata[0] = 5;

        Assert.True(RandomMapObjectWriter.TryWrite(map, new Point(5, 5), monster));

        Assert.Equal(0U, map.Tile(5, 5).Metadata);
        Assert.Equal(5, map.Tile(5, 5).BottomIcnImageIndex);
    }

    [Fact]
    public void ADecorationOnTheTileIsKeptWhenAnObjectLandsOnIt()
    {
        Mp2Map map = Map();
        Mp2TileInfo before = map.Tile(5, 5);

        // 一片没有类型的装饰面片（宝物精灵的边缘就是这样），物件落到它上面时必须并进去而不是覆盖。
        map.SetTile(5, 5, before with
        {
            ObjectName1 = (byte)((byte)Mp2ObjectIcnType.ObjnArti << 2),
            BottomIcnImageIndex = 3,
        });

        Assert.True(RandomMapObjectWriter.TryWrite(
            map, new Point(5, 5), Object(Mp2ObjectType.Trees, (Mp2ObjectIcnType.ObjnArti, 7, 0, 0))));

        Mp2TileInfo after = map.Tile(5, 5);

        Assert.Equal(Mp2ObjectType.Trees, after.ObjectType);
        Assert.NotEqual(0, after.NextAddonIndex);

        // 两片都在：一片在瓦片上，另一片在附加链里。
        Mp2AddonInfo addon = map.Addons[after.NextAddonIndex];
        int[] pieces = [(int)after.BottomIcnImageIndex, (int)addon.BottomIcnImageIndex];

        Assert.Contains(3, pieces);
        Assert.Contains(7, pieces);
    }

    /// <summary>A blank 36x36 map of plain grass.</summary>
    private static Mp2Map Map() => Mp2Map.CreateBlank(Size, 0);

    /// <summary>An object of <paramref name="type"/> whose ground parts are the given pieces.</summary>
    private static ObjectInfo Object(
        Mp2ObjectType type, params (Mp2ObjectIcnType Icn, uint Index, int X, int Y)[] parts)
    {
        ObjectInfo info = new(type);

        foreach ((Mp2ObjectIcnType icn, uint index, int x, int y) in parts)
        {
            info.AddGroundPart(icn, index, new Point(x, y), type, ObjectLayerType.Object);
        }

        return info;
    }


}
