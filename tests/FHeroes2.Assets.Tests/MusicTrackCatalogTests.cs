using FHeroes2.Assets.Audio;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="MusicTrackCatalog"/>, the port of <c>MUS::getFileName</c> and of the music file
/// lookup of <c>audio_manager.cpp</c>.
/// </summary>
public sealed class MusicTrackCatalogTests
{
    [Theory]
    [InlineData(MusicTrack.MainMenu, MusicNamingScheme.Dos, "homm2_41.ogg")]
    [InlineData(MusicTrack.MainMenu, MusicNamingScheme.Windows, "Track42.ogg")]
    [InlineData(MusicTrack.MainMenu, MusicNamingScheme.Mapped, "42 Main Menu.ogg")]
    [InlineData(MusicTrack.Battle1, MusicNamingScheme.Dos, "homm2_01.ogg")]
    [InlineData(MusicTrack.KnightCastle, MusicNamingScheme.Mapped, "08 Knight Castle.ogg")]
    [InlineData(MusicTrack.Unknown, MusicNamingScheme.Windows, "Track44.ogg")]
    public void GetFileName_FollowsTheReferenceNamingSchemes(MusicTrack track, MusicNamingScheme scheme, string expected)
    {
        Assert.Equal(expected, MusicTrackCatalog.GetFileName(track, scheme, Path.GetExtension(expected)));
    }

    [Fact]
    public void EnumerateCandidateFileNames_PrefersDosThenWindowsThenMapped()
    {
        string[] names = [.. MusicTrackCatalog.EnumerateCandidateFileNames(MusicTrack.Battle1)];

        Assert.Equal(MusicTrackCatalog.Extensions.Length * 3, names.Length);
        Assert.Equal("homm2_01.ogg", names[0]);
        Assert.Equal("homm2_01.mp3", names[1]);
        Assert.Equal("homm2_01.flac", names[2]);
        Assert.Equal("Track02.ogg", names[3]);
        Assert.Equal("02 Battle 1.ogg", names[6]);
    }

    [Fact]
    public void GetDescription_ReturnsTheReferenceName()
    {
        Assert.Equal("Main Menu", MusicTrackCatalog.GetDescription(MusicTrack.MainMenu));
        Assert.Equal("Battle 1", MusicTrackCatalog.GetDescription(MusicTrack.Battle1));
    }

    [Theory]
    [InlineData(MusicTrack.Unused)]
    [InlineData((MusicTrack)99)]
    public void GetFileName_RejectsTracksWithoutAnExternalFile(MusicTrack track)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MusicTrackCatalog.GetFileName(track, MusicNamingScheme.Dos, ".ogg"));
    }

    [Fact]
    public void FindFile_LooksIntoTheMusicFolderNextToTheDataDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "fh2-music-" + Guid.NewGuid().ToString("N"));
        string music = Path.Combine(root, "MUSIC");
        Directory.CreateDirectory(music);

        try
        {
            string expected = Path.Combine(music, "homm2_41.ogg");
            File.WriteAllBytes(expected, [0]);

            // The data directory itself does not exist; the soundtrack sits next to it.
            string? found = MusicTrackCatalog.FindFile([Path.Combine(root, "Data")], MusicTrack.MainMenu);

            Assert.NotNull(found);

            // The same file may be reached through 'music' or 'MUSIC' depending on the probe order.
            Assert.Equal(Path.GetFullPath(expected), found, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindFile_ReturnsNullWithoutASoundtrack()
    {
        string root = Path.Combine(Path.GetTempPath(), "fh2-music-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Assert.Null(MusicTrackCatalog.FindFile([root], MusicTrack.MainMenu));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindFile_FindsTheMainMenuThemeOfTheLocalInstallation()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        string? path = MusicTrackCatalog.FindFile([dataDirectory], MusicTrack.MainMenu);
        if (path is null)
        {
            // A data folder without the separate soundtrack is a valid installation as well.
            return;
        }

        Assert.True(File.Exists(path));
        Assert.Contains("homm2_41", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
    }
}
