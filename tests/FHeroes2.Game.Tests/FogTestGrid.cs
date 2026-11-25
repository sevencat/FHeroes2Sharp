using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Builds a <see cref="MapFog"/> over a fresh grid of undiscovered tiles, the state a map is in right after it
/// is loaded: <see cref="MapFog"/> owns no fog of its own, so a test that wants one needs the tiles that store it.
/// </summary>
internal static class FogTestGrid
{
    /// <summary>A fog view of a <paramref name="width"/> × <paramref name="height"/> map, every tile undiscovered.</summary>
    /// <param name="width">Width in tiles.</param>
    /// <param name="height">Height in tiles.</param>
    /// <returns>The fog view over the new tiles.</returns>
    public static MapFog Create(int width, int height)
    {
        List<Tile> tiles = new(width * height);

        for (int index = 0; index < width * height; ++index)
        {
            tiles.Add(new Tile());
        }

        return new MapFog(tiles, width, height);
    }
}
