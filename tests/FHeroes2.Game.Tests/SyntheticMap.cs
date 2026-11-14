using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Builds an in memory <see cref="Mp2Map"/> so the adventure map tests do not need a map file. Only
/// the members the terrain renderer reads are filled in.
/// </summary>
internal static class SyntheticMap
{
    /// <summary>Creates a map of <paramref name="width"/> by <paramref name="height"/> empty tiles.</summary>
    public static Mp2Map Create(int width, int height, Func<int, Mp2TileInfo>? tileFactory = null)
    {
        Mp2TileInfo[] tiles = new Mp2TileInfo[width * height];
        for (int index = 0; index < tiles.Length; ++index)
        {
            tiles[index] = tileFactory is null ? default : tileFactory(index);
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
            Addons = [default],
            Castles = [],
            CaptureObjects = [],
            InfoBlocks = [],
        };
    }
}
