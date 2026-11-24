using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers where <c>fheroes2.cfg</c> lives, <c>System::GetConfigDirectory()</c> 加 <c>Settings::configFileName</c>。
/// </summary>
public sealed class ConfigurationFileTests
{
    [Fact]
    public void TheDefaultFileSitsInTheFheroes2FolderOfTheApplicationData()
    {
        string appData = Path.Combine(Path.GetTempPath(), "appdata");

        Assert.Equal(Path.Combine(appData, "fheroes2", "fheroes2.cfg"), ConfigurationFile.DefaultPath(appData));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutAnApplicationDataFolderThereIsNoDefaultFile(string? appData) =>
        Assert.Null(ConfigurationFile.DefaultPath(appData));

    [Fact]
    public void AnExplicitFileWinsOverTheDefault()
    {
        string appData = Path.Combine(Path.GetTempPath(), "appdata");
        string explicitFile = Path.Combine(Path.GetTempPath(), "custom.cfg");

        Assert.Equal(explicitFile, ConfigurationFile.Resolve(appData, explicitFile));
        Assert.Equal(Path.Combine(appData, "fheroes2", "fheroes2.cfg"), ConfigurationFile.Resolve(appData, null));
        Assert.Null(ConfigurationFile.Resolve(null, null));
    }

    [Fact]
    public void EnsureDirectoryCreatesTheMissingFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}");
        string file = Path.Combine(root, "fheroes2", "fheroes2.cfg");

        try
        {
            Assert.True(ConfigurationFile.EnsureDirectory(file));
            Assert.True(Directory.Exists(Path.GetDirectoryName(file)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
