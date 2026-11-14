using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Builds an in memory <see cref="Mp2Map"/> so the adventure map tests do not need a map file. Only
/// the members the renderers read are filled in.
/// </summary>
internal static class SyntheticMap
{
    /// <summary>Creates a map of <paramref name="width"/> by <paramref name="height"/> empty tiles.</summary>
    /// <param name="width">Width in tiles.</param>
    /// <param name="height">Height in tiles.</param>
    /// <param name="tileFactory">Builds tile <c>i</c> in row major order; empty tiles by default.</param>
    /// <param name="addons">
    /// Add-ons in file order. Index 0 of <see cref="Mp2Map.Addons"/> is the "no more add-ons"
    /// sentinel, so the first element of this array is add-on 1 and <c>NextAddonIndex</c> of a tile
    /// points at it with 1.
    /// </param>
    public static Mp2Map Create(
        int width,
        int height,
        Func<int, Mp2TileInfo>? tileFactory = null,
        Mp2AddonInfo[]? addons = null)
    {
        Mp2TileInfo[] tiles = new Mp2TileInfo[width * height];
        for (int index = 0; index < tiles.Length; ++index)
        {
            tiles[index] = tileFactory is null ? default : tileFactory(index);
        }

        // Heroes are derived from the tiles the same way Mp2Map does it, so a test only has to put a
        // hero tile into the factory.
        List<Mp2MapHero> heroes = [];
        for (int index = 0; index < tiles.Length; ++index)
        {
            if (Mp2MapHeroes.TryRead(tiles[index], index % width, index / width, out Mp2MapHero hero))
            {
                heroes.Add(hero);
            }
        }

        return new Mp2Map
        {
            Info = new Mp2FileInfo
            {
                FilePath = "synthetic.mp2",
                Name = "Synthetic",
                Description = string.Empty,
                Width = width,
                Height = height,
                Difficulty = GameDifficulty.Normal,
            },
            Tiles = tiles,
            Addons = addons is null ? [default] : [default, .. addons],
            Castles = [],
            CaptureObjects = [],
            Heroes = heroes,
            InfoBlocks = [],
        };
    }
}
