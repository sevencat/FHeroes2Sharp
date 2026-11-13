namespace FHeroes2.Assets;

/// <summary>
/// Locates the directory that holds the original game data (<c>Data/heroes2.agg</c>, …).
///
/// Resolution order:
/// <list type="number">
///   <item><description>explicit <c>--data</c> command line value,</description></item>
///   <item><description><c>FHEROES2_DATA</c> environment variable,</description></item>
///   <item><description>the executable directory (and its <c>Data</c> sub directory),</description></item>
///   <item><description>a small list of well known local installs used during development.</description></item>
/// </list>
/// Nothing is ever downloaded; the player has to own the original game files.
/// </summary>
public static class AssetLocator
{
    public const string DataDirectoryEnvironmentVariable = "FHEROES2_DATA";

    public const string BaseArchiveName = "Heroes2.agg";

    public const string ExpansionArchiveName = "Heroes2x.agg";

    private static readonly string[] DevelopmentDataDirectories =
    [
        @"F:\game\fh2",
        @"C:\Program Files (x86)\GOG Galaxy\Games\Heroes of Might and Magic 2",
        @"C:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic II",
    ];

    /// <summary>All directories that will be probed, in priority order.</summary>
    public static IEnumerable<string> CandidateDataDirectories(string? explicitDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
        {
            yield return explicitDirectory;
        }

        string? fromEnvironment = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            yield return fromEnvironment;
        }

        string baseDirectory = AppContext.BaseDirectory;
        yield return baseDirectory;
        yield return System.IO.Path.Combine(baseDirectory, "Data");

        foreach (string candidate in DevelopmentDataDirectories)
        {
            yield return candidate;
        }
    }

    /// <summary>Returns the first candidate directory that contains the base archive, or <see langword="null"/>.</summary>
    public static string? Resolve(string? explicitDirectory = null)
    {
        foreach (string candidate in CandidateDataDirectories(explicitDirectory))
        {
            if (IsGameDataDirectory(candidate))
            {
                return System.IO.Path.GetFullPath(candidate);
            }

            // The installer variants place the archives either in the root or in a "Data" sub folder.
            string withData = System.IO.Path.Combine(candidate, "Data");
            if (IsGameDataDirectory(withData))
            {
                return System.IO.Path.GetFullPath(withData);
            }
        }

        return null;
    }

    /// <summary>Checks whether <paramref name="directory"/> contains the base game archive.</summary>
    public static bool IsGameDataDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return File.Exists(System.IO.Path.Combine(directory, BaseArchiveName));
    }

    /// <summary>Finds an archive inside <paramref name="dataDirectory"/> regardless of the concrete layout.</summary>
    public static string? FindArchive(string dataDirectory, string archiveName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveName);

        string direct = System.IO.Path.Combine(dataDirectory, archiveName);
        if (File.Exists(direct))
        {
            return direct;
        }

        string nested = System.IO.Path.Combine(dataDirectory, "Data", archiveName);
        if (File.Exists(nested))
        {
            return nested;
        }

        string? parent = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(dataDirectory));
        if (parent is not null)
        {
            string sibling = System.IO.Path.Combine(parent, archiveName);
            if (File.Exists(sibling))
            {
                return sibling;
            }
        }

        return null;
    }
}
