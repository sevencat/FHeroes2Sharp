using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="MapCatalog"/>, the port of <c>Maps::getAllMapFileInfos</c> and its
/// <c>getValidMaps</c> helper.
/// </summary>
public sealed class MapCatalogTests
{
    [Fact]
    public void FindDirectories_ProbesTheMapsFolderNextToTheDataDirectory()
    {
        string root = CreateRoot();

        try
        {
            string maps = Path.Combine(root, "Maps");
            string data = Path.Combine(root, "Data");
            Directory.CreateDirectory(maps);
            Directory.CreateDirectory(data);

            // 'Maps' lives next to the data directory, which is recognized by its base archive.
            File.WriteAllBytes(Path.Combine(data, AssetLocator.BaseArchiveName), []);

            IReadOnlyList<string> directories = MapCatalog.FindDirectories([data]);

            Assert.Contains(directories, entry => string.Equals(entry, maps, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindFiles_IgnoresEverythingButMaps()
    {
        string root = CreateRoot();

        try
        {
            WriteMap(root, "First.mp2", name: "First");
            File.WriteAllText(Path.Combine(root, "Notes.txt"), "no map");

            IReadOnlyList<string> files = MapCatalog.FindFiles([root]);

            Assert.Single(files);
            Assert.EndsWith("First.mp2", files[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadAll_SortsByScenarioNameAndSkipsBrokenFiles()
    {
        string root = CreateRoot();

        try
        {
            WriteMap(root, "z-file.mp2", name: "Alpha");
            WriteMap(root, "a-file.MX2", name: "Beta");
            File.WriteAllBytes(Path.Combine(root, "Broken.mp2"), new byte[16]);

            MapScanResult scan = MapCatalog.Scan([root]);

            Assert.Equal(["Alpha", "Beta"], scan.Maps.Select(map => map.Name));
            Assert.Equal(MapVersion.PriceOfLoyalty, scan.Maps[1].Version);
            Assert.Single(scan.Rejected);
            Assert.Equal(MapCatalog.ReadAll([root]).Count, scan.Maps.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadAll_RejectsMapsThatNeedMoreHumanPlayers()
    {
        string root = CreateRoot();

        try
        {
            // Blue and green have to be played by a human, the computer cannot take over.
            WriteMap(root, "Duo.mp2", name: "Duo", header =>
            {
                header[14] = 1;
                header[15] = 1;
                header[20] = 0;
                header[21] = 0;
            });

            Assert.Empty(MapCatalog.ReadAll([root]));
            Assert.Single(MapCatalog.ReadAll([root], humanPlayerCount: 2));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadAll_RestrictsTheHumanColorsWhenTheMapHasExactlyThatManyHumanOnlyColors()
    {
        string root = CreateRoot();

        try
        {
            // Blue is human only, red may be played by both sides. Asking for one human player narrows
            // the human colors down to blue, so red is no longer offered to a human.
            WriteMap(root, "Solo.mp2", name: "Solo", header =>
            {
                header[14] = 1;
                header[16] = 1;
                header[20] = 0;
                header[22] = 1;
            });

            Mp2FileInfo info = Assert.Single(MapCatalog.ReadAll([root]));

            Assert.Equal(PlayerColor.Blue, info.HumanPlayers);
            Assert.Equal(PlayerColor.Blue, info.HumanOnlyColors);
            Assert.Equal(PlayerColor.None, info.SharedColors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_ReadsTheMapsOfTheRealGameData()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        MapScanResult scan = MapCatalog.Scan([dataDirectory]);

        Assert.True(scan.Maps.Count >= 20, $"Only {scan.Maps.Count} maps were found.");

        Mp2FileInfo? beltway = scan.Maps.FirstOrDefault(map => map.Name == "Beltway");
        Assert.True(beltway is not null, "The installation does not contain the 'Beltway' map.");
        Assert.Equal(72, beltway!.Width);
        Assert.Equal(72, beltway.Height);
        Assert.Equal(GameDifficulty.Normal, beltway.Difficulty);
        Assert.Equal(3, beltway.PlayerCount);
        Assert.Equal(MapVictoryCondition.DefeatEveryone, beltway.VictoryCondition);
    }

    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "fh2-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteMap(string directory, string fileName, string name, Action<byte[]>? configureHeader = null)
    {
        File.WriteAllBytes(
            Path.Combine(directory, fileName),
            TemporaryMap.CreateContent(configureHeader, name));
    }
}
