using FHeroes2.Assets;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Locates the real game data for the tests that need the archives. They return early when the data
/// is unavailable, so a fresh clone can still run the full suite. See docs/MISC.md.
/// </summary>
internal static class GameData
{
    public static AssetBundle? TryOpen()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("FHEROES2_TEST_DATA");
        string? directory = fromEnvironment is not null && AssetLocator.IsGameDataDirectory(fromEnvironment)
            ? fromEnvironment
            : AssetLocator.Resolve();

        return directory is null ? null : AssetBundle.Open(directory);
    }
}
