using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2MapWriter"/>. A save of an opened map is deliberately incremental - it
/// rewrites the tile area of the file it read and leaves every other section alone - so most of these
/// tests are about what a save must <em>not</em> touch. The other shape, <see cref="Mp2MapWriter.WriteAll"/>,
/// is what a map created by the editor uses, and there the tests read the file back instead.
/// "The original editor would reopen it" is not checkable (the reference implementation has no MP2
/// writer any more), but byte equality and a successful read are, and they imply it.
/// </summary>
public sealed class Mp2MapWriterTests
{
    [Fact]
    public void Save_ReproducesTheFileWhenNothingChanged()
    {
        Mp2MapFileBuilder builder = new(36)
        {
            ObeliskCount = 3,
            LastObjectUid = 4321,
        };

        builder.Tiles[0] = default(Mp2TileInfo) with { TerrainImageIndex = 0x0123, TerrainFlags = 0x21 };
        builder.Addons.Add(default(Mp2AddonInfo) with
        {
            ObjectNameN1 = 40,
            BottomIcnImageIndex = 3,
            QuantityN = 0b1_0111,
            Level1ObjectUid = 12,
            Level2ObjectUid = 13,
        });

        builder.Castles.Add((4, 6, 0x20));
        builder.CaptureObjects.Add((9, 2, 0x81));
        builder.InfoBlocks.Add([0xDE, 0xAD]);

        using TemporaryMapFile source = TemporaryMapFile.Create(builder);
        byte[] original = File.ReadAllBytes(source.FilePath);

        Mp2MapWriter.Save(source.FilePath, Mp2Map.Read(source.FilePath));

        Assert.Equal(original, File.ReadAllBytes(source.FilePath));
    }

    [Fact]
    public void Save_ChangesOnlyTheEditedTile()
    {
        using TemporaryMapFile source = TemporaryMapFile.Create(new Mp2MapFileBuilder(36));
        byte[] original = File.ReadAllBytes(source.FilePath);

        Mp2Map map = Mp2Map.Read(source.FilePath);
        int index = map.TileIndex(5, 7);
        Mp2TileInfo[] tiles = [.. map.Tiles];
        tiles[index] = tiles[index] with { TerrainImageIndex = 4321, TerrainFlags = 0x03 };

        string target = Path.Combine(Path.GetDirectoryName(source.FilePath)!, "Edited.mp2");
        Mp2MapWriter.Save(target, map with { Tiles = tiles });

        byte[] saved = File.ReadAllBytes(target);
        Assert.Equal(original.Length, saved.Length);

        long tileStart = Mp2FileInfo.HeaderSize + ((long)index * Mp2TileInfo.StructureSize);
        long tileEnd = tileStart + Mp2TileInfo.StructureSize;

        for (int offset = 0; offset < original.Length; ++offset)
        {
            if (offset < tileStart || offset >= tileEnd)
            {
                Assert.True(
                    original[offset] == saved[offset],
                    $"Byte {offset} is outside the edited tile but changed from {original[offset]} to {saved[offset]}.");
            }
        }

        Mp2Map reread = Mp2Map.Read(target);
        Assert.Equal(4321, reread.Tiles[index].TerrainImageIndex);
        Assert.Equal(0x03, reread.Tiles[index].TerrainFlags);
    }

    [Fact]
    public void Save_KeepsTheSectionsAfterTheTiles()
    {
        Mp2MapFileBuilder builder = new(36)
        {
            ObeliskCount = 7,
            LastObjectUid = 999,
        };

        builder.Castles.Add((1, 2, 0x40));
        builder.InfoBlocks.Add([1, 2, 3, 4, 5]);

        using TemporaryMapFile source = TemporaryMapFile.Create(builder);
        byte[] original = File.ReadAllBytes(source.FilePath);

        Mp2Map map = Mp2Map.Read(source.FilePath);
        Mp2TileInfo[] tiles = [.. map.Tiles];
        tiles[0] = tiles[0] with { TerrainImageIndex = 2048 };

        string target = Path.Combine(Path.GetDirectoryName(source.FilePath)!, "Tail.mp2");
        Mp2MapWriter.Save(target, map with { Tiles = tiles });

        Mp2Map reread = Mp2Map.Read(target);
        Assert.Equal(2048, reread.Tiles[0].TerrainImageIndex);
        Assert.Equal(7, reread.ObeliskCount);
        Assert.Equal(999u, reread.LastObjectUid);
        Assert.Equal((1, 2, 0x40), (reread.Castles[0].X, reread.Castles[0].Y, reread.Castles[0].RawType));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, reread.InfoBlocks[0].Data);

        // The tail must also survive literally, not only through the reader: the save is a patch of the
        // tile area, so everything from the first add-on onwards is the same bytes as before.
        byte[] saved = File.ReadAllBytes(target);
        int tilesEnd = Mp2FileInfo.HeaderSize + (map.TileCount * Mp2TileInfo.StructureSize);

        Assert.Equal(original[tilesEnd..], saved[tilesEnd..]);
    }

    [Fact]
    public void Save_RejectsAMapWithMissingTiles()
    {
        using TemporaryMapFile source = TemporaryMapFile.Create(new Mp2MapFileBuilder(36));

        Mp2Map map = Mp2Map.Read(source.FilePath);
        Mp2Map broken = map with { Tiles = [.. map.Tiles.Take(10)] };
        byte[] image = File.ReadAllBytes(source.FilePath);

        Assert.Throws<BinaryFormatException>(() => Mp2MapWriter.WriteTiles(image, broken));
    }

    [Fact]
    public void Save_RejectsATruncatedSource()
    {
        using TemporaryMapFile source = TemporaryMapFile.Create(new Mp2MapFileBuilder(36));

        Mp2Map map = Mp2Map.Read(source.FilePath);
        byte[] image = File.ReadAllBytes(source.FilePath);
        File.WriteAllBytes(source.FilePath, image[..(Mp2FileInfo.HeaderSize + 100)]);

        Assert.Throws<BinaryFormatException>(() => Mp2MapWriter.Save(source.FilePath, map));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        using TemporaryMapFile source = TemporaryMapFile.Create(new Mp2MapFileBuilder(36));
        string directory = Path.GetDirectoryName(source.FilePath)!;

        Mp2MapWriter.Save(source.FilePath, Mp2Map.Read(source.FilePath));

        Assert.Single(Directory.GetFiles(directory, "*.mp2"));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void WriteAll_ProducesAFileTheReaderAccepts()
    {
        string filePath = NewMapPath();

        try
        {
            Mp2MapWriter.WriteAll(filePath, Mp2Map.CreateBlank(36, 30));

            Mp2Map map = Mp2Map.Read(filePath);

            Assert.Equal(36, map.Width);
            Assert.Equal(36, map.Height);
            Assert.Equal(Mp2Map.NewMapName, map.Info.Name);
            Assert.Equal(36 * 36, map.Tiles.Count);
            Assert.Empty(map.Castles);
            Assert.Empty(map.CaptureObjects);
            Assert.Single(map.Addons);
            Assert.Equal(0, map.ObeliskCount);
            Assert.Empty(map.InfoBlocks);
            Assert.Equal(0u, map.LastObjectUid);

            Assert.All(map.Tiles, tile =>
            {
                Assert.Equal(30, tile.TerrainImageIndex);
                Assert.Equal(Mp2ObjectType.None, tile.ObjectType);
            });

            // Header, tiles, the add-on count and its placeholder entry, both object tables, the
            // obelisk count, the block list and the object id counter: a whole file, not a patch.
            Assert.Equal(
                Mp2FileInfo.HeaderSize
                + (36 * 36 * Mp2TileInfo.StructureSize)
                + Mp2Map.AddonCountSize
                + Mp2AddonInfo.StructureSize
                + (Mp2Map.CastleCount * Mp2Map.CastlePositionSize)
                + (Mp2Map.CaptureObjectCount * Mp2Map.CaptureObjectPositionSize)
                + 1
                + 4
                + Mp2Map.ObjectUidSize,
                new FileInfo(filePath).Length);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteAll_KeepsTheObjectsAndBlocksItIsGiven()
    {
        string filePath = NewMapPath();

        try
        {
            Mp2Map blank = Mp2Map.CreateBlank(36, 30);
            Mp2Map map = blank with
            {
                Castles = [new Mp2CastlePosition(5, 6, 0x40, MapRace.Random)],
                CaptureObjects = [new Mp2CaptureObject(7, 8, 0x00, Mp2ObjectType.Sawmill)],
                InfoBlocks = [new Mp2InfoBlock(0, [0xAA, 0xBB]), new Mp2InfoBlock(2, [0xCC])],
                ObeliskCount = 4,
                LastObjectUid = 777,
            };

            Mp2MapWriter.WriteAll(filePath, map);

            Mp2Map reread = Mp2Map.Read(filePath);

            Assert.Equal(
                (5, 6, 0x40),
                (reread.Castles[0].X, reread.Castles[0].Y, reread.Castles[0].RawType));
            Assert.Equal(
                (7, 8, 0x00),
                (reread.CaptureObjects[0].X, reread.CaptureObjects[0].Y, reread.CaptureObjects[0].RawType));
            Assert.Equal(4, reread.ObeliskCount);
            Assert.Equal(777u, reread.LastObjectUid);

            // The gap at index 1 goes back as an empty block, so block 2 keeps the index the map uses.
            Assert.Equal([0, 2], reread.InfoBlocks.Select(block => block.Index));
            Assert.Equal(new byte[] { 0xAA, 0xBB }, reread.InfoBlocks[0].Data);
            Assert.Equal(new byte[] { 0xCC }, reread.InfoBlocks[1].Data);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteAll_IsStableAcrossAReadAndWrite()
    {
        string first = NewMapPath();
        string second = NewMapPath();

        try
        {
            Mp2MapWriter.WriteAll(first, Mp2Map.CreateBlank(36, 30));
            Mp2MapWriter.WriteAll(second, Mp2Map.Read(first));

            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    [Fact]
    public void WriteAll_RejectsAMapWithoutAStorableSize()
    {
        Mp2Map blank = Mp2Map.CreateBlank(36, 30);
        Mp2Map broken = blank with { Info = blank.Info with { Width = 37 } };

        Assert.Throws<BinaryFormatException>(() => Mp2MapWriter.Serialize(broken));
    }

    [Fact]
    public void CreateBlank_RejectsASizeTheFormatDoesNotKnow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Mp2Map.CreateBlank(50, 30));
    }

    [Fact]
    public void CreateBlank_AsksForTheImageOfEveryTile()
    {
        List<Point> asked = [];

        Mp2Map map = Mp2Map.CreateBlank(36, (x, y) =>
        {
            asked.Add(new Point(x, y));
            return (ushort)((x * 100) + y);
        });

        // One call per tile, in row-major order, and the returned image ends up on that tile.
        Assert.Equal(36 * 36, asked.Count);
        Assert.Equal(new Point(0, 0), asked[0]);
        Assert.Equal(new Point(1, 0), asked[1]);
        Assert.Equal((ushort)((3 * 100) + 4), map.Tile(3, 4).TerrainImageIndex);
    }

    private static string NewMapPath() =>
        Path.Combine(Path.GetTempPath(), $"fh2-new-{Guid.NewGuid():N}.mp2");
}
