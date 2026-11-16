using FHeroes2.Assets.H2D;
using FHeroes2.Game.H2D;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests for the H2D gateway: the sample list that has to match the archive, the three ways opening fails, and the
/// <see cref="H2DResources.Current"/> convenience that stands in for the reference's global reader.
/// </summary>
public sealed class H2DResourcesTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    public void Dispose()
    {
        H2DResources.Current = null;

        foreach (string path in _temporaryFiles)
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SampleFileNames_HasTheSixtyEightEntriesOfTheCurrentArchive()
    {
        Assert.Equal(68, H2DResources.SampleFileNames.Count);
        Assert.All(H2DResources.SampleFileNames, name => Assert.EndsWith(".image", name, StringComparison.Ordinal));
    }

    [Fact]
    public void Open_AcceptsAnArchiveThatMatchesTheSample()
    {
        string path = WriteArchive(H2DResources.SampleFileNames);

        using H2DResources resources = H2DResources.Open(path);

        Assert.Equal(68, resources.FileNames.Count);
        Assert.True(resources.Contains("black_cat.image"));
        Assert.Equal(new byte[] { 7 }, resources.TryReadFile("black_cat.image"));
    }

    [Fact]
    public void Open_RejectsAnArchiveWithAnExtraEntry()
    {
        List<string> names = [.. H2DResources.SampleFileNames, "extra.image"];
        string path = WriteArchive(names);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => H2DResources.Open(path));

        Assert.Contains("does not match the sample", exception.Message, StringComparison.Ordinal);
        Assert.Contains("resurrection.h2d", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_RejectsAnArchiveThatMissesAnEntry()
    {
        List<string> names = [.. H2DResources.SampleFileNames];
        names.Remove("monocle.image");

        Assert.Throws<InvalidOperationException>(() => H2DResources.Open(WriteArchive(names)));
    }

    [Fact]
    public void Open_RejectsAMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}", H2DResources.FileName);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => H2DResources.Open(path));

        Assert.Contains(H2DResources.FileName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadImage_WithoutALoadedArchive()
    {
        Assert.Null(H2DResources.Current);
        Assert.False(H2DResources.TryReadCurrentImage("monocle.image", out _));
    }

    [Fact]
    public void ReadImage_UsesTheLoadedArchive()
    {
        using H2DResources resources = H2DResources.Open(WriteArchive(H2DResources.SampleFileNames));
        H2DResources.Current = resources;

        // The bodies written by the test are not sprites, so the decode fails, but the lookup itself must succeed.
        Assert.True(resources.Contains("monocle.image"));
        Assert.False(H2DResources.TryReadCurrentImage("monocle.image", out _));
        Assert.False(H2DResources.TryReadCurrentImage("not-there.image", out _));
    }

    [Fact]
    public void GetDefaultPath_PutsTheArchiveUnderFilesData()
    {
        string path = H2DResources.GetDefaultPath("data");

        Assert.Equal(Path.Combine("data", "files", "data", H2DResources.FileName), path);
    }

    /// <summary>Writes an archive whose entries hold one byte each, so only the record table matters.</summary>
    private string WriteArchive(IEnumerable<string> names)
    {
        H2DWriter writer = new();
        foreach (string name in names)
        {
            writer.Add(name, [7]);
        }

        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.h2d");
        Assert.True(writer.Write(path));
        _temporaryFiles.Add(path);
        return path;
    }
}
