using FHeroes2.Assets.Agg;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

public sealed class AssetArchiveTests
{
    [Fact]
    public void Open_ReadsSyntheticArchive()
    {
        byte[] archiveBytes = BuildArchive(("KB.PAL", [1, 2, 3]), ("HEROES.ICN", [9, 8, 7, 6, 5]));
        string path = WriteTempArchive(archiveBytes);

        try
        {
            using AssetArchive archive = AssetArchive.Open(path);

            Assert.Equal(2, archive.Count);
            Assert.True(archive.Contains("KB.PAL"));
            Assert.Equal([1, 2, 3], archive.Read("KB.PAL"));
            Assert.Equal([9, 8, 7, 6, 5], archive.Read("HEROES.ICN"));
            Assert.Null(archive.TryRead("MISSING.ICN"));
            Assert.Equal(["KB.PAL", "HEROES.ICN"], archive.Entries.Select(entry => entry.Name));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_CorruptedHash_Throws()
    {
        byte[] archiveBytes = BuildArchive(("KB.PAL", [1, 2, 3]));

        // Flip the first byte of the stored 32 bit name hash.
        archiveBytes[2] ^= 0xFF;

        string path = WriteTempArchive(archiveBytes);

        try
        {
            Assert.Throws<BinaryFormatException>(() => AssetArchive.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_TruncatedFile_Throws()
    {
        string path = WriteTempArchive([0x10, 0x00]);

        try
        {
            Assert.Throws<BinaryFormatException>(() => AssetArchive.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MissingAsset_ThrowsKeyNotFoundException()
    {
        byte[] archiveBytes = BuildArchive(("KB.PAL", [1]));
        string path = WriteTempArchive(archiveBytes);

        try
        {
            using AssetArchive archive = AssetArchive.Open(path);

            Assert.Throws<KeyNotFoundException>(() => archive.Read("MISSING.ICN"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetEntry_ReturnsOffsetsInsideFile()
    {
        byte[] archiveBytes = BuildArchive(("A.ICN", [1, 2]), ("B.ICN", [3, 4, 5]));
        string path = WriteTempArchive(archiveBytes);

        try
        {
            using AssetArchive archive = AssetArchive.Open(path);

            Assert.True(archive.TryGetEntry("B.ICN", out AssetEntry entry));
            Assert.Equal(3u, entry.Size);
            Assert.True(entry.EndOffset <= (uint)archiveBytes.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Builds a minimal but valid AGG container using the documented layout.</summary>
    private static byte[] BuildArchive(params (string Name, byte[] Data)[] entries)
    {
        using MemoryStream stream = new();

        using (LittleEndianWriter writer = new(stream))
        {
            writer.WriteUInt16((ushort)entries.Length);

            uint offset = (uint)(2 + (entries.Length * 12));
            foreach ((string name, byte[] data) in entries)
            {
                writer.WriteUInt32(AggFileNameHash.Compute(name));
                writer.WriteUInt32(offset);
                writer.WriteUInt32((uint)data.Length);
                offset += (uint)data.Length;
            }

            foreach ((string _, byte[] data) in entries)
            {
                writer.WriteBytes(data);
            }

            foreach ((string name, byte[] _) in entries)
            {
                writer.WriteFixedAscii(name, AggFileNameHash.MaxFileNameLength);
            }
        }

        return stream.ToArray();
    }

    private static string WriteTempArchive(byte[] content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.agg");
        File.WriteAllBytes(path, content);
        return path;
    }
}
