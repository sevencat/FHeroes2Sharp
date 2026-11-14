using System.Globalization;

namespace FHeroes2.Assets.Audio;

/// <summary>Naming scheme of the external music files (see <c>MUS::ExternalMusicNamingScheme</c>).</summary>
public enum MusicNamingScheme
{
    /// <summary>DOS / "GOG" scheme: <c>homm2_01.ogg</c>, the data track is not numbered.</summary>
    Dos,

    /// <summary>Windows scheme: <c>Track02.ogg</c>.</summary>
    Windows,

    /// <summary>Mapped scheme: <c>02 Battle 1.ogg</c>.</summary>
    Mapped,
}

/// <summary>
/// Turns a <see cref="MusicTrack"/> into the file names of the external music files and finds them
/// next to the game data, mirroring <c>MUS::getFileName</c> and the music lookup of
/// <c>audio_manager.cpp</c>.
///
/// <para>
/// The original game ships its soundtrack as separate files in a <c>MUSIC</c> folder, so it never
/// lives inside the archives. Three naming schemes are supported, in the order the reference
/// implementation tries them: DOS (<c>homm2_NN</c>), Windows (<c>TrackNN</c>) and mapped
/// (<c>NN &lt;description&gt;</c>), each with the extensions <c>.ogg</c>, <c>.mp3</c> and
/// <c>.flac</c>. Nothing is decoded here; that is the job of the audio backend.
/// </para>
/// </summary>
public static class MusicTrackCatalog
{
    /// <summary>Extensions searched for an external music file, in priority order.</summary>
    public static readonly string[] Extensions = [".ogg", ".mp3", ".flac"];

    private static readonly string[] Descriptions =
    [
        string.Empty, // Unused
        string.Empty, // DataTrack
        "Battle 1",
        "Battle 2",
        "Battle 3",
        "Sorceress Castle",
        "Warlock Castle",
        "Necromancer Castle",
        "Knight Castle",
        "Barbarian Castle",
        "Wizard Castle",
        "Lava Theme",
        "Wasteland Theme",
        "Desert Theme",
        "Snow Theme",
        "Swamp Theme",
        "Ocean Theme",
        "Dirt Theme",
        "Grass Theme",
        "Lost Game",
        "New Week",
        "New Month",
        "Archibald Campaign",
        "Map Puzzle",
        "Roland Campaign",
        "25",
        "26",
        "27",
        "AI Turn",
        "Battle Won",
        "Battle Lost",
        "Dungeon",
        "Waterspring",
        "Arabian",
        "Hillfort",
        "Treehouse",
        "Demoncave",
        "Experience",
        "Skill",
        "Watchtower",
        "Xanadu",
        "Ultimate Artifact",
        "Main Menu",
        "Scenario Victory",
        "UNKNOWN",
    ];

    /// <summary>Human readable name of a track, e.g. <c>Main Menu</c>.</summary>
    public static string GetDescription(MusicTrack track) => Descriptions[Validate(track)];

    /// <summary>
    /// File name of <paramref name="track"/> for one naming scheme, e.g. <c>homm2_41.ogg</c> (DOS)
    /// or <c>Track42.ogg</c> (Windows) for the main menu theme.
    /// </summary>
    public static string GetFileName(MusicTrack track, MusicNamingScheme scheme, string extension)
    {
        ArgumentException.ThrowIfNullOrEmpty(extension);

        int id = Validate(track);

        return scheme switch
        {
            // The DOS release dropped the data track, so its files are numbered one lower.
            MusicNamingScheme.Dos => "homm2_" + Pad(id - 1) + extension,
            MusicNamingScheme.Windows => "Track" + Pad(id) + extension,
            MusicNamingScheme.Mapped => Pad(id) + " " + Descriptions[id] + extension,
            _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "Unknown naming scheme."),
        };
    }

    /// <summary>
    /// Every file name that could hold <paramref name="track"/>, most likely first: the DOS names of
    /// all extensions, then the Windows ones, then the mapped ones.
    /// </summary>
    public static IEnumerable<string> EnumerateCandidateFileNames(MusicTrack track)
    {
        _ = Validate(track);

        MusicNamingScheme[] schemes = [MusicNamingScheme.Dos, MusicNamingScheme.Windows, MusicNamingScheme.Mapped];

        foreach (MusicNamingScheme scheme in schemes)
        {
            foreach (string extension in Extensions)
            {
                yield return GetFileName(track, scheme, extension);
            }
        }
    }

    /// <summary>
    /// Searches the <c>MUSIC</c> folders around <paramref name="rootDirectories"/> (a root itself, a
    /// <c>MUSIC</c> sub folder, or the one next to a <c>Data</c> directory) for the first existing
    /// file of <paramref name="track"/>; returns its full path or <see langword="null"/> when the
    /// player does not have that part of the soundtrack.
    /// </summary>
    public static string? FindFile(IEnumerable<string> rootDirectories, MusicTrack track)
    {
        ArgumentNullException.ThrowIfNull(rootDirectories);
        _ = Validate(track);

        foreach (string root in rootDirectories)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            foreach (string directory in EnumerateMusicDirectories(root))
            {
                foreach (string name in EnumerateCandidateFileNames(track))
                {
                    string candidate = Path.Combine(directory, name);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateMusicDirectories(string root)
    {
        string[] names = ["music", "MUSIC"];

        yield return root;

        foreach (string name in names)
        {
            yield return Path.Combine(root, name);
        }

        string fullRoot;
        try
        {
            fullRoot = Path.GetFullPath(root);
        }
        catch (ArgumentException)
        {
            yield break;
        }

        // The data directory (the one holding Heroes2.agg) usually sits next to MUSIC.
        string? parent = Path.GetDirectoryName(fullRoot);
        if (parent is not null)
        {
            foreach (string name in names)
            {
                yield return Path.Combine(parent, name);
            }
        }

        // Keep the lookup working on case sensitive file systems: the original folder is 'MUSIC'.
        List<string> subdirectories;
        try
        {
            subdirectories = [.. Directory.EnumerateDirectories(fullRoot)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A root we cannot enumerate just does not provide music.
            yield break;
        }

        foreach (string sub in subdirectories)
        {
            if (string.Equals(Path.GetFileName(sub), "music", StringComparison.OrdinalIgnoreCase))
            {
                yield return sub;
            }
        }
    }

    private static int Validate(MusicTrack track)
    {
        int id = (int)track;
        if (id <= (int)MusicTrack.Unused || id > (int)MusicTrack.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(track), track, "The music track has no external file.");
        }

        return id;
    }

    private static string Pad(int value) => value.ToString("00", CultureInfo.InvariantCulture);
}

