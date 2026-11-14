using FHeroes2.Assets.Til;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Builds a <c>GROUND32.TIL</c> in memory so the adventure map tests can draw real terrain without
/// the game archives. Tile <c>i</c> is a flat square filled with <c>i + 1</c>, which makes every pixel
/// of a test frame tell which tile it came from.
/// </summary>
internal static class SyntheticTerrain
{
    /// <summary>Creates <paramref name="count"/> flat tiles of <paramref name="tileSize"/> pixels.</summary>
    public static TerrainTileSet Create(int count, int tileSize)
    {
        List<byte> data = [];
        data.Add((byte)count);
        data.Add((byte)(count >> 8));
        data.Add((byte)tileSize);
        data.Add(0);
        data.Add((byte)tileSize);
        data.Add(0);

        for (int index = 0; index < count; ++index)
        {
            byte value = (byte)(index + 1);
            for (int pixel = 0; pixel < tileSize * tileSize; ++pixel)
            {
                data.Add(value);
            }
        }

        return TerrainTileSet.Create(TilFile.Parse([.. data]));
    }
}
