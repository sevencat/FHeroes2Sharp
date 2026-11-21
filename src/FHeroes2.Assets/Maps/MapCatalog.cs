using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// Finds the map files of the original game and reads their information blocks, the port of
/// <c>Maps::getAllMapFileInfos</c> and its <c>getValidMaps</c> helper (<c>maps_fileinfo.cpp</c>).
///
/// <para>
/// Maps live outside the archives, in a <c>maps</c> folder next to the data directory. Only the
/// original <c>.mp2</c> format and the <c>.mx2</c> maps of the Price of Loyalty expansion are read;
/// the fheroes2 map format (<c>.fh2m</c>) is not supported yet.
/// </para>
/// </summary>
public static class MapCatalog
{
    /// <summary>Extensions of the supported map files, in the order they are probed.</summary>
    public static readonly string[] Extensions = [".mp2", ".mx2"];

    private const string MapDirectoryName = "maps";

    /// <summary>
    /// Directories that are probed for map files: every root, its <c>maps</c> sub folder and every
    /// child folder of a root that is called <c>maps</c> (the file system may be case sensitive).
    /// Duplicates are removed and the result keeps the order of <paramref name="rootDirectories"/>.
    /// </summary>
    public static IReadOnlyList<string> FindDirectories(IEnumerable<string> rootDirectories)
    {
        ArgumentNullException.ThrowIfNull(rootDirectories);

        List<string> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string root in rootDirectories)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string fullRoot;
            try
            {
                fullRoot = Path.GetFullPath(root);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            Probe(fullRoot, result, seen);

            // The data directory (the one holding Heroes2.agg) usually sits next to 'Maps'.
            string? parent = AssetLocator.IsGameDataDirectory(fullRoot) ? Path.GetDirectoryName(fullRoot) : null;
            if (parent is not null)
            {
                Probe(parent, result, seen);
            }
        }

        return result;
    }

    private static void Probe(string directory, List<string> directories, HashSet<string> seen)
    {
        Add(directories, seen, directory);

        string nested = Path.Combine(directory, MapDirectoryName);
        if (Directory.Exists(nested))
        {
            Add(directories, seen, nested);
        }

        string[] children;
        try
        {
            children = [.. Directory.EnumerateDirectories(directory)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (string child in children)
        {
            if (string.Equals(Path.GetFileName(child), MapDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                Add(directories, seen, child);
            }
        }
    }

    /// <summary>
    /// Looks for one file by name below <paramref name="rootDirectories"/>,
    /// <c>Maps::tryGetMatchingFile()</c>.
    /// </summary>
    /// <param name="rootDirectories">The directories to search.</param>
    /// <param name="fileName">The file name to look for, compared ignoring case.</param>
    /// <returns>The full path, or <see langword="null"/> when no directory has the file.</returns>
    /// <remarks>
    /// 不按扩展名过滤：战役地图叫 <c>.h2c</c>/<c>.hxc</c>，不在 <see cref="Extensions"/> 里，
    /// 但战役数据就是靠文件名找不到就判定「地图没装」。
    /// </remarks>
    public static string? FindFile(IEnumerable<string> rootDirectories, string fileName)
    {
        ArgumentNullException.ThrowIfNull(rootDirectories);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        foreach (string directory in FindDirectories(rootDirectories))
        {
            string[] candidates;
            try
            {
                candidates = [.. Directory.EnumerateFiles(directory)];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in candidates)
            {
                if (Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(file);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Every map file found below <paramref name="rootDirectories"/>, without reading it.
    /// </summary>
    public static IReadOnlyList<string> FindFiles(IEnumerable<string> rootDirectories)
    {
        List<string> files = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in FindDirectories(rootDirectories))
        {
            string[] candidates;
            try
            {
                candidates = [.. Directory.EnumerateFiles(directory)];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in candidates)
            {
                if (IsMapFile(file) && seen.Add(Path.GetFullPath(file)))
                {
                    files.Add(Path.GetFullPath(file));
                }
            }
        }

        files.Sort(static (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    /// <summary>Whether <paramref name="path"/> has the extension of a supported map file.</summary>
    public static bool IsMapFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string extension = Path.GetExtension(path);
        foreach (string candidate in Extensions)
        {
            if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads every map below <paramref name="rootDirectories"/> and keeps only the ones that
    /// <paramref name="humanPlayerCount"/> human players can start, sorted by scenario name.
    /// </summary>
    public static IReadOnlyList<Mp2FileInfo> ReadAll(IEnumerable<string> rootDirectories, int humanPlayerCount = 1) =>
        Scan(rootDirectories, humanPlayerCount).Maps;

    /// <summary>
    /// Same as <see cref="ReadAll"/> but also reports the skipped files: a file that cannot be parsed
    /// and a map that does not fit <paramref name="humanPlayerCount"/> players both end up in
    /// <see cref="MapScanResult.Rejected"/>, which mirrors the silent <c>continue</c> of the reference
    /// implementation but keeps the information available for the diagnostics.
    /// </summary>
    public static MapScanResult Scan(IEnumerable<string> rootDirectories, int humanPlayerCount = 1)
    {
        ArgumentNullException.ThrowIfNull(rootDirectories);
        ArgumentOutOfRangeException.ThrowIfLessThan(humanPlayerCount, 1);

        List<Mp2FileInfo> maps = [];
        List<string> rejected = [];

        foreach (string file in FindFiles(rootDirectories))
        {
            if (!Mp2FileInfo.TryRead(file, out Mp2FileInfo? info) || info is null)
            {
                GameLog.Debug($"Map '{file}' cannot be read and is skipped.");
                rejected.Add(file);
                continue;
            }

            int humanOnly = PlayerColors.Count(info.HumanOnlyColors);
            if (humanOnly > humanPlayerCount)
            {
                // The map needs more human-only players than requested.
                GameLog.Debug($"Map '{file}' requires {humanOnly} human players and is skipped.");
                rejected.Add(file);
                continue;
            }

            if (humanPlayerCount > humanOnly + PlayerColors.Count(info.SharedColors))
            {
                // The map cannot host this many human players.
                GameLog.Debug($"Map '{file}' allows fewer than {humanPlayerCount} human players and is skipped.");
                rejected.Add(file);
                continue;
            }

            if (humanOnly == humanPlayerCount)
            {
                // Exactly enough human-only colors: the player must not pick a shared one.
                info = info.RestrictHumanColorsTo(info.HumanOnlyColors);
            }

            maps.Add(info);
        }

        maps.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return new MapScanResult(maps, rejected);
    }

    private static void Add(List<string> directories, HashSet<string> seen, string directory)
    {
        string full = Path.GetFullPath(directory);
        if (seen.Add(full))
        {
            directories.Add(full);
        }
    }
}

/// <summary>Result of <see cref="MapCatalog.Scan"/>: the playable maps and the skipped files.</summary>
/// <param name="Maps">Maps that can be started, sorted by scenario name.</param>
/// <param name="Rejected">Map files that could not be read or do not fit the player count.</param>
public sealed record MapScanResult(IReadOnlyList<Mp2FileInfo> Maps, IReadOnlyList<string> Rejected);

