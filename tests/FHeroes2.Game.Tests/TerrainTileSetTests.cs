using FHeroes2.Assets.Til;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="TerrainTileSet"/>: the four terrain shape variants and the bounds handling of
/// the lookup, the two things the adventure map relies on.
/// </summary>
public sealed class TerrainTileSetTests
{
    [Fact]
    public void Create_ProducesTheFourMirroredShapes()
    {
        // A 2x2 tile with four distinct values, so every mirror is unambiguous.
        TilFile til = CreateTil(2, 2, [1, 2, 3, 4]);
        TerrainTileSet tiles = TerrainTileSet.Create(til);

        Assert.Equal(2, tiles.TileWidth);
        Assert.Equal(2, tiles.TileHeight);
        Assert.Equal(1, tiles.Count);

        Assert.Equal([1, 2, 3, 4], Read(tiles.GetTile(0, 0)));
        Assert.Equal([3, 4, 1, 2], Read(tiles.GetTile(0, 1))); // vertical flip
        Assert.Equal([2, 1, 4, 3], Read(tiles.GetTile(0, 2))); // horizontal flip
        Assert.Equal([4, 3, 2, 1], Read(tiles.GetTile(0, 3))); // both
    }

    [Fact]
    public void GetTile_MasksTheShapeToTwoBits()
    {
        TilFile til = CreateTil(2, 2, [1, 2, 3, 4]);
        TerrainTileSet tiles = TerrainTileSet.Create(til);

        Assert.Equal(Read(tiles.GetTile(0, 1)), Read(tiles.GetTile(0, 5)));
    }

    [Fact]
    public void GetTile_ReturnsNullOutsideTheImageRange()
    {
        TerrainTileSet tiles = TerrainTileSet.Create(CreateTil(1, 1, [7]));

        Assert.NotNull(tiles.GetTile(0, 0));
        Assert.Null(tiles.GetTile(-1, 0));
        Assert.Null(tiles.GetTile(1, 0));
    }

    [Fact]
    public void Create_KeepsEveryImageOfTheFile()
    {
        TerrainTileSet tiles = TerrainTileSet.Create(CreateTil(1, 1, [7], [8], [9]));

        Assert.Equal(3, tiles.Count);
        Assert.Equal([7], Read(tiles.GetTile(0, 0)));
        Assert.Equal([8], Read(tiles.GetTile(1, 0)));
        Assert.Equal([9], Read(tiles.GetTile(2, 0)));
    }

    private static byte[] Read(PixelBuffer? buffer)
    {
        Assert.NotNull(buffer);

        byte[] pixels = new byte[buffer.Width * buffer.Height];
        for (int y = 0; y < buffer.Height; ++y)
        {
            buffer.Row(y).CopyTo(pixels.AsSpan(y * buffer.Width));
        }

        return pixels;
    }

    private static TilFile CreateTil(int width, int height, params byte[][] images)
    {
        List<byte> data = [];
        data.Add((byte)images.Length);
        data.Add((byte)(images.Length >> 8));
        data.Add((byte)width);
        data.Add((byte)(width >> 8));
        data.Add((byte)height);
        data.Add((byte)(height >> 8));

        foreach (byte[] image in images)
        {
            Assert.Equal(width * height, image.Length);
            data.AddRange(image);
        }

        return TilFile.Parse([.. data]);
    }
}
