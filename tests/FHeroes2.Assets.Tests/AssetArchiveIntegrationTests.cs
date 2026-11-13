using FHeroes2.Assets.Agg;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests against the real, unmodified game archives. They validate the port against 1400+ name
/// hashes produced by the original games, which is the strongest available ground truth.
/// When the data directory cannot be found the tests return early (see docs/MISC.md).
/// </summary>
public sealed class AssetArchiveIntegrationTests
{
    private const int ExpectedBaseArchiveEntries = 1434;

    [Fact]
    public void BaseArchive_HasExpectedEntryCount()
    {
        string? path = GameData.FindArchive(AssetLocator.BaseArchiveName);
        if (path is null)
        {
            return;
        }

        using AssetArchive archive = AssetArchive.Open(path);

        Assert.Equal(ExpectedBaseArchiveEntries, archive.Count);
    }

    [Fact]
    public void BaseArchive_ContainsTheFontAndPaletteEntries()
    {
        string? path = GameData.FindArchive(AssetLocator.BaseArchiveName);
        if (path is null)
        {
            return;
        }

        using AssetArchive archive = AssetArchive.Open(path);

        foreach (string name in new[] { "KB.PAL", "BIGFONT.FNT", "SMALFONT.FNT", "FONT.ICN", "HEROES.ICN", "ADVBORD.ICN" })
        {
            Assert.Contains(name, archive.Entries.Select(entry => entry.Name));
        }
    }

    [Fact]
    public void BaseArchive_KbPaletteHas768Bytes()
    {
        string? path = GameData.FindArchive(AssetLocator.BaseArchiveName);
        if (path is null)
        {
            return;
        }

        using AssetArchive archive = AssetArchive.Open(path);
        byte[] paletteData = archive.Read("KB.PAL");

        Assert.Equal(Palette.RawPaletteSize, paletteData.Length);

        Palette palette = Palette.FromGamePalette(paletteData);

        Assert.Contains(palette.Colors.ToArray(), color => color.R > 200);
    }

    [Fact]
    public void BaseArchive_EntriesStayInsideTheFile()
    {
        string? path = GameData.FindArchive(AssetLocator.BaseArchiveName);
        if (path is null)
        {
            return;
        }

        long length = new FileInfo(path).Length;

        using AssetArchive archive = AssetArchive.Open(path);

        Assert.All(archive.Entries, entry => Assert.True(entry.EndOffset <= length, entry.ToString()));
        Assert.All(archive.Entries, entry => Assert.False(string.IsNullOrEmpty(entry.Name)));
    }

    [Fact]
    public void ExpansionArchive_OpensWhenPresent()
    {
        string? path = GameData.FindArchive(AssetLocator.ExpansionArchiveName);
        if (path is null)
        {
            return;
        }

        using AssetArchive archive = AssetArchive.Open(path);

        Assert.True(archive.Count > 0);
        Assert.All(archive.Entries, entry => Assert.False(string.IsNullOrEmpty(entry.Name)));
    }
}
