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
        AssetBundle? bundle = TryResolveDataDirectory() is { } directory ? AssetBundle.Open(directory) : null;

        return bundle;
    }

    /// <summary>
    /// The installation directory itself, for the tests that need a file the archives do not hold — the
    /// map files live next to them, not inside them.
    /// </summary>
    /// <returns>The directory, or <see langword="null"/> when the game data is not installed.</returns>
    public static string? TryResolveDataDirectory()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("FHEROES2_TEST_DATA");

        return fromEnvironment is not null && AssetLocator.IsGameDataDirectory(fromEnvironment)
            ? fromEnvironment
            : AssetLocator.Resolve();
    }
}
