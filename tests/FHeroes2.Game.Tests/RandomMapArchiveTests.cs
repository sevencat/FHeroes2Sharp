using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Saving a generated map where the game looks for maps, so that it shows up in the scenario list and can be started
/// (<c>Maps::getValidMaps()</c> over the <c>maps</c> folder).
/// </summary>
public sealed class RandomMapArchiveTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(), $"fh2-random-archive-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void TheFileIsNamedAfterTheSeed()
    {
        Assert.Equal("random_map_424242.mp2", RandomMapArchive.FileNameFor(424242));
        Assert.Equal("random_map_0.mp2", RandomMapArchive.FileNameFor(0));
    }

    [Fact]
    public void TheFolderIsCreatedNextToTheDataDirectory()
    {
        Directory.CreateDirectory(_dataDirectory);

        string? directory = RandomMapArchive.ResolveDirectory(_dataDirectory);

        Assert.NotNull(directory);
        Assert.Equal(Path.Combine(_dataDirectory, RandomMapArchive.DirectoryName), directory);
        Assert.True(Directory.Exists(directory));

        // 第二次调用不会因为目录已存在而失败。
        Assert.Equal(directory, RandomMapArchive.ResolveDirectory(_dataDirectory));
    }

    [Fact]
    public void AGeneratedMapIsSavedAndCanBeReadBack()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 424242 }, out uint seed);
        string directory = MapsFolder();

        string filePath = RandomMapArchive.Save(map, directory, seed);

        Assert.True(File.Exists(filePath));
        Assert.Equal(RandomMapArchive.FileNameFor(424242), Path.GetFileName(filePath));

        Mp2Map loaded = Mp2Map.Read(filePath);

        Assert.Equal(map.Width, loaded.Width);
        Assert.Equal(map.Castles.Count, loaded.Castles.Count);
        Assert.Equal(map.Info.Name, loaded.Info.Name);
    }

    [Fact]
    public void TheSavedMapIsVisibleInTheScenarioList()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 424242, PlayerCount = 2 }, out uint seed);
        string directory = MapsFolder();

        RandomMapArchive.Save(map, directory, seed);

        IReadOnlyList<Mp2FileInfo> listed = MapCatalog.ReadAll([_dataDirectory], humanPlayerCount: 1);

        Mp2FileInfo info = Assert.Single(listed);

        Assert.Equal(map.Info.Name, info.Name);
        Assert.Equal(map.Width, info.Width);
        Assert.Equal(2, PlayerColors.Count(info.KingdomColors));
        Assert.Equal(Path.Combine(directory, RandomMapArchive.FileNameFor(seed)), info.FilePath);
    }

    [Fact]
    public void TheSavedMapCanBeStarted()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 20261126, PlayerCount = 2 }, out uint seed);
        string directory = MapsFolder();

        string filePath = RandomMapArchive.Save(map, directory, seed);

        // 「开局」就是读回文件再喂给 World：城镇、英雄、信息块都得从文件里重建出来。
        Mp2Map loaded = Mp2Map.Read(filePath);
        World world = new();
        world.LoadMap(loaded);

        Assert.Equal(loaded.Width, world.Width);

        for (int index = 0; index < 2; ++index)
        {
            Kingdom.Kingdom kingdom = world.GetKingdom(PlayerColors.Ordered[index]);

            Assert.NotEmpty(kingdom.Castles);
            Assert.NotEmpty(kingdom.Heroes);
        }
    }

    [Fact]
    public void TheSameSeedOverwritesItsOwnFile()
    {
        string directory = MapsFolder();

        string first = RandomMapArchive.Save(Generate(new RandomMapConfig { Seed = 7 }, out uint firstSeed), directory, firstSeed);
        string second = RandomMapArchive.Save(Generate(new RandomMapConfig { Seed = 7 }, out uint secondSeed), directory, secondSeed);

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(directory, "*.mp2"));
    }

    [Fact]
    public void DifferentSeedsGetDifferentFiles()
    {
        string directory = MapsFolder();

        RandomMapArchive.Save(Generate(new RandomMapConfig { Seed = 7 }, out uint first), directory, first);
        RandomMapArchive.Save(Generate(new RandomMapConfig { Seed = 8 }, out uint second), directory, second);

        Assert.Equal(2, Directory.GetFiles(directory, "*.mp2").Length);
    }

    [Fact]
    public void TheDescriptionNamesTheSizeTheCastlesAndTheFile()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 7 }, out _);

        string text = RandomMapArchive.Describe(map, @"C:\maps\random_map_7.mp2");

        Assert.Contains("72x72", text, StringComparison.Ordinal);
        Assert.Contains($"{map.Castles.Count} castle(s)", text, StringComparison.Ordinal);
        Assert.Contains("random_map_7.mp2", text, StringComparison.Ordinal);
    }

    /// <summary>Generates a map and reports the seed it used, the way the host does.</summary>
    private static Mp2Map Generate(RandomMapConfig config, out uint seed) =>
        RandomMapGenerator.Generate(config, 72, 72, out _, out seed)
        ?? throw new InvalidOperationException("The map was not generated.");

    /// <summary>Creates the data directory and returns the folder the maps go into.</summary>
    private string MapsFolder()
    {
        Directory.CreateDirectory(_dataDirectory);

        return RandomMapArchive.ResolveDirectory(_dataDirectory)
            ?? throw new InvalidOperationException("The maps folder should have been created.");
    }
}



