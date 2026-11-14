using FHeroes2.Assets;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The terrain tiles of the adventure map, the port of <c>Assets::GetMaximumTILIndex()</c> and
/// <c>Assets::getTileImage()</c> for <c>TIL::GROUND32</c> (<c>game_assets.cpp</c>).
///
/// <para>
/// The reference keeps four pre flipped copies of every tile: the map stores a two bit terrain shape
/// per tile and the second shape bit mirrors horizontally while the first one mirrors vertically
/// (<c>Flip( originalTIL[i], ..., horizontalFlip, verticalFlip )</c>). Doing the same here keeps the
/// adventure map loop a plain copy instead of a per pixel mirror.
/// </para>
/// </summary>
public sealed class TerrainTileSet
{
    /// <summary>The TIL entry the adventure map draws its ground with, <c>TIL::GROUND32</c>.</summary>
    public const string GroundFileName = "GROUND32.TIL";

    /// <summary>Number of terrain shapes: original, vertical flip, horizontal flip, both.</summary>
    public const int ShapeCount = 4;

    private readonly PixelBuffer[][] _shapes;

    private TerrainTileSet(PixelBuffer[][] shapes, int tileWidth, int tileHeight)
    {
        _shapes = shapes;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
    }

    /// <summary>Width of one terrain tile in pixels.</summary>
    public int TileWidth { get; }

    /// <summary>Height of one terrain tile in pixels.</summary>
    public int TileHeight { get; }

    /// <summary>Number of terrain images, i.e. the valid range of the terrain image index.</summary>
    public int Count => _shapes[0].Length;

    /// <summary>Builds the four shape variants of a parsed TIL file.</summary>
    public static TerrainTileSet Create(TilFile til)
    {
        ArgumentNullException.ThrowIfNull(til);

        PixelBuffer[][] shapes = new PixelBuffer[ShapeCount][];
        shapes[0] = [.. til.Images];

        for (int shape = 1; shape < ShapeCount; ++shape)
        {
            bool horizontal = (shape & 2) != 0;
            bool vertical = (shape & 1) != 0;

            shapes[shape] = new PixelBuffer[shapes[0].Length];
            for (int index = 0; index < shapes[0].Length; ++index)
            {
                shapes[shape][index] = ImageOps.Flip(shapes[0][index], horizontal, vertical);
            }
        }

        return new TerrainTileSet(shapes, til.Width, til.Height);
    }

    /// <summary>
    /// Loads <c>GROUND32.TIL</c> from the game archives. The caller has to treat
    /// <see langword="null"/> as "the data is not installed".
    /// </summary>
    public static TerrainTileSet? TryLoad(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        byte[]? data = assets.TryRead(GroundFileName);
        if (data is null)
        {
            GameLog.Warn($"'{GroundFileName}' is missing; the adventure map cannot draw terrain.");
            return null;
        }

        try
        {
            TerrainTileSet tiles = Create(TilFile.Parse(data));
            GameLog.Info(
                $"Terrain ready: {tiles.Count} images of {tiles.TileWidth}x{tiles.TileHeight} " +
                $"from '{GroundFileName}' ({ShapeCount} shapes).");
            return tiles;
        }
        catch (BinaryFormatException exception)
        {
            GameLog.Error($"'{GroundFileName}' could not be parsed", exception);
            return null;
        }
    }

    /// <summary>
    /// The image of <paramref name="imageIndex"/> mirrored by <paramref name="shape"/>, which is the
    /// two bit terrain shape of a tile; <see langword="null"/> when the index is out of range.
    /// </summary>
    public PixelBuffer? GetTile(int imageIndex, int shape)
    {
        if (imageIndex < 0 || imageIndex >= Count)
        {
            return null;
        }

        return _shapes[shape & 0x03][imageIndex];
    }
}
