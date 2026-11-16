using FHeroes2.Assets.H2D;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for the H2D container: the record table, the zlib bodies and the writer that has to produce archives the
/// reader accepts. All inputs are synthetic so that malformed records can be tested explicitely.
/// </summary>
public sealed class H2DArchiveTests
{
    [Fact]
    public void Open_ReadsEveryRecordInNameOrder()
    {
        byte[] data = H2DFileBuilder.Build(
            ("b.image", "second", H2DFileBuilder.Compress([2])),
            ("a.image", "first", H2DFileBuilder.Compress([1])));

        using H2DArchive archive = H2DArchive.Open(WriteTempArchive(data));

        Assert.Equal(2, archive.Count);
        Assert.Equal(["a.image", "b.image"], archive.FileNames);
        Assert.Equal("first", archive.Entries["a.image"].Info);
    }

    [Fact]
    public void TryRead_DecompressesTheBody()
    {
        byte[] body = [1, 2, 3, 4, 5, 6, 7, 8];
        using H2DArchive archive = H2DArchive.Open(
            WriteTempArchive(H2DFileBuilder.Build(("numbers.image", "", H2DFileBuilder.Compress(body)))));

        Assert.Equal(body, archive.TryRead("numbers.image"));
        Assert.Equal(body, archive.Read("numbers.image"));
    }

    [Fact]
    public void TryRead_ReturnsNullForAMissingName()
    {
        using H2DArchive archive = H2DArchive.Open(
            WriteTempArchive(H2DFileBuilder.Build(("a.image", "", H2DFileBuilder.Compress([1])))));

        Assert.False(archive.Contains("b.image"));
        Assert.Null(archive.TryRead("b.image"));
        Assert.Throws<KeyNotFoundException>(() => archive.Read("b.image"));
    }

    [Fact]
    public void TryRead_ReturnsNullWhenTheBodyIsNotZlib()
    {
        // The reference returns an empty vector when zlib refuses the data, so a broken body must not throw.
        using H2DArchive archive = H2DArchive.Open(
            WriteTempArchive(H2DFileBuilder.Build(("broken.image", "", [0x78, 0x9C, 0xFF, 0xFF]))));

        Assert.Null(archive.TryRead("broken.image"));
    }

    [Fact]
    public void Open_RejectsAWrongMagic()
    {
        byte[] data = H2DFileBuilder.Build(("a.image", "", H2DFileBuilder.Compress([1])));
        data[0] = (byte)'h';

        Assert.Throws<BinaryFormatException>(() => H2DArchive.Open(WriteTempArchive(data)));
    }

    [Fact]
    public void Open_RejectsAnUnsupportedVersion()
    {
        byte[] data = H2DFileBuilder.Build(("a.image", "", H2DFileBuilder.Compress([1])));
        data[3] = 2;

        Assert.Throws<BinaryFormatException>(() => H2DArchive.Open(WriteTempArchive(data)));
    }

    [Fact]
    public void Open_RejectsAFileShorterThanTheHeader()
    {
        Assert.Throws<BinaryFormatException>(() => H2DArchive.Open(WriteTempArchive(new byte[12])));
    }

    [Fact]
    public void Open_RejectsAnArchiveWithoutEntries()
    {
        byte[] file = new byte[24];
        file[0] = (byte)'H';
        file[1] = (byte)'2';
        file[2] = (byte)'D';
        file[3] = H2DArchive.Version;

        Assert.Throws<BinaryFormatException>(() => H2DArchive.Open(WriteTempArchive(file)));
    }

    [Fact]
    public void Open_RejectsACountThatCannotFit()
    {
        byte[] data = H2DFileBuilder.Build(("a.image", "", H2DFileBuilder.Compress([1])));
        data[4] = 0xFF;
        data[5] = 0xFF;

        Assert.Throws<BinaryFormatException>(() => H2DArchive.Open(WriteTempArchive(data)));
    }

    [Fact]
    public void Open_SkipsEntriesThatPointOutsideTheFile()
    {
        byte[] body = H2DFileBuilder.Compress([1, 2, 3, 4]);
        byte[] data = H2DFileBuilder.Build(("a.image", "", body));

        // The first record's offset field starts right after the magic and the count.
        data[8] = 0xFF;
        data[9] = 0xFF;

        using H2DArchive archive = H2DArchive.Open(WriteTempArchive(data));

        Assert.Equal(0, archive.Count);
    }

    [Fact]
    public void Open_KeepsTheFirstOfDuplicateNames()
    {
        byte[] data = H2DFileBuilder.Build(
            ("a.image", "first", H2DFileBuilder.Compress([1])),
            ("a.image", "second", H2DFileBuilder.Compress([2])));

        using H2DArchive archive = H2DArchive.Open(WriteTempArchive(data));

        Assert.Equal(1, archive.Count);
        Assert.Equal("first", archive.Entries["a.image"].Info);
    }

    [Fact]
    public void Writer_RoundTripsThroughTheFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.h2d");
        try
        {
            H2DWriter writer = new();
            Assert.True(writer.Add("b.image", [4, 5, 6], "info"));
            Assert.True(writer.Add("a.image", [1, 2, 3]));
            Assert.True(writer.Write(path));

            using H2DArchive archive = H2DArchive.Open(path);

            Assert.Equal(["a.image", "b.image"], archive.FileNames);
            Assert.Equal([1, 2, 3], archive.Read("a.image"));
            Assert.Equal([4, 5, 6], archive.Read("b.image"));
            Assert.Equal("info", archive.Entries["b.image"].Info);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Writer_RejectsAnEmptyNameOrBody()
    {
        H2DWriter writer = new();

        Assert.False(writer.Add(string.Empty, [1]));
        Assert.False(writer.Add("a.image", []));
        Assert.Empty(writer.FileNames);
    }

    [Fact]
    public void Writer_ReplacesAnEntryWithTheSameName()
    {
        H2DWriter writer = new();
        writer.Add("a.image", [1]);

        Assert.True(writer.Add("a.image", [2]));
        Assert.Single(writer.FileNames);

        using H2DArchive archive = H2DArchive.Open(WriteTempArchive(writer.Serialize()!));
        Assert.Equal([2], archive.Read("a.image"));
    }

    [Fact]
    public void Writer_RemovesAnEntry()
    {
        H2DWriter writer = new();
        writer.Add("a.image", [1]);

        Assert.True(writer.Remove("a.image"));
        Assert.False(writer.Remove("a.image"));
        Assert.Null(writer.Serialize());
    }

    [Fact]
    public void Writer_WritesNothingWhenEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.h2d");

        Assert.False(new H2DWriter().Write(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Writer_StoresTheBodiesInNameOrder()
    {
        H2DWriter writer = new();
        writer.Add("c.image", [3, 3, 3]);
        writer.Add("a.image", [1]);
        writer.Add("b.image", [2, 2]);

        using H2DArchive archive = H2DArchive.Open(WriteTempArchive(writer.Serialize()!));

        uint previous = 0;
        foreach ((string _, H2DEntry entry) in archive.Entries)
        {
            Assert.True(entry.Offset > previous);
            previous = entry.Offset;
        }
    }

    [Fact]
    public void Writer_CopiesAnArchive()
    {
        using H2DArchive source = H2DArchive.Open(WriteTempArchive(H2DFileBuilder.Build(
            ("a.image", "info", H2DFileBuilder.Compress([1, 2])),
            ("b.image", "", H2DFileBuilder.Compress([3, 4])))));

        H2DWriter writer = new();
        Assert.True(writer.Add(source));

        using H2DArchive copy = H2DArchive.Open(WriteTempArchive(writer.Serialize()!));

        Assert.Equal(["a.image", "b.image"], copy.FileNames);
        Assert.Equal([1, 2], copy.Read("a.image"));
        Assert.Equal([3, 4], copy.Read("b.image"));
        Assert.Equal("info", copy.Entries["a.image"].Info);
    }

    [Fact]
    public void Writer_KeepsTheBodyCompressible()
    {
        // A thousand identical pixels compress to far less than the body, so the archive is written compressed.
        byte[] body = new byte[1024];

        H2DWriter writer = new();
        writer.Add("flat.image", body);

        byte[] archive = writer.Serialize()!;
        Assert.True(archive.Length < body.Length);

        using H2DArchive reopened = H2DArchive.Open(WriteTempArchive(archive));
        Assert.Equal(body, reopened.Read("flat.image"));
    }

    private static string WriteTempArchive(byte[] content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.h2d");
        File.WriteAllBytes(path, content);
        return path;
    }
}
