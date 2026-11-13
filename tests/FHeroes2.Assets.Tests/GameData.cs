using FHeroes2.Assets;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Helper that locates the real game data. The tests are skipped (they return early) when the
/// data is unavailable, so a fresh clone can still run the full suite. See docs/MISC.md.
/// </summary>
internal static class GameData
{
    public const string DataDirectoryEnvironmentVariable = "FHEROES2_TEST_DATA";

    public static string? ResolveDataDirectory()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && AssetLocator.IsGameDataDirectory(fromEnvironment))
        {
            return fromEnvironment;
        }

        return AssetLocator.Resolve();
    }

    public static string? FindArchive(string archiveName)
    {
        string? dataDirectory = ResolveDataDirectory();
        return dataDirectory is null ? null : AssetLocator.FindArchive(dataDirectory, archiveName);
    }
}
