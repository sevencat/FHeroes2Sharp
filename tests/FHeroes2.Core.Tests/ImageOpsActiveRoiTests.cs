using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <see cref="ImageOps.GetActiveRoi"/>, the port of <c>fheroes2::GetActiveROI</c>
/// (<c>engine/image.cpp:2574-2620</c>).
/// </summary>
/// <remarks>
/// The function reads the transform layer, not the image layer: a two layer sprite marks every pixel with the
/// operation that put it there, and only some of those operations count as drawn. The tests pin that reading and the
/// single layer answer, because the mage guild picture is centred on this box.
/// </remarks>
public sealed class ImageOpsActiveRoiTests
{
    private const byte Ink = 7;

    [Fact]
    public void GetActiveRoi_ReturnsTheBoundingBoxOfTheDrawnPixels()
    {
        PixelBuffer image = new(10, 8);
        byte[] transform = new byte[image.Width * image.Height];
        Array.Fill(transform, (byte)1);

        // A four by two block at (4, 3), one stray pixel further right and one lower down.
        for (int y = 0; y < 2; ++y)
        {
            for (int x = 0; x < 4; ++x)
            {
                image[4 + x, 3 + y] = Ink;
                transform[((3 + y) * 10) + 4 + x] = 0;
            }
        }

        transform[(4 * 10) + 9] = 0;
        transform[(7 * 10) + 5] = 0;

        Sprite sprite = new(image, 0, 0, transform);

        Assert.Equal(new Rect(4, 3, 6, 5), ImageOps.GetActiveRoi(sprite));
    }

    [Fact]
    public void GetActiveRoi_CountsACopiedPixelEvenWhenItsColourIsZero()
    {
        PixelBuffer image = new(4, 4);
        byte[] transform = new byte[image.Width * image.Height];
        Array.Fill(transform, (byte)1);

        // Only the transform layer is read: transform 0 means "copy", so the pixel counts even though the image
        // layer holds palette index 0 there.
        transform[0] = 0;

        Sprite sprite = new(image, 0, 0, transform);

        Assert.Equal(new Rect(0, 0, 1, 1), ImageOps.GetActiveRoi(sprite));
    }

    [Fact]
    public void GetActiveRoi_IsEmptyForASingleLayerSprite()
    {
        PixelBuffer image = new(8, 8);
        image.Fill(Ink);

        Sprite sprite = new(image);

        // A sprite without a transform layer cannot say which pixels are transparent, so the reference answers
        // with nothing and lets the caller fall back to the full bounds.
        Assert.Equal(Rect.Empty, ImageOps.GetActiveRoi(sprite));
    }

    [Fact]
    public void GetActiveRoi_IsEmptyWhenEveryPixelIsSkipped()
    {
        PixelBuffer image = new(5, 5);
        byte[] transform = new byte[image.Width * image.Height];
        Array.Fill(transform, (byte)3);

        Sprite sprite = new(image, 0, 0, transform);

        Assert.Equal(Rect.Empty, ImageOps.GetActiveRoi(sprite));
    }

    [Fact]
    public void GetActiveRoi_TakesTheThresholdFromTheCaller()
    {
        PixelBuffer image = new(6, 6);
        byte[] transform = new byte[image.Width * image.Height];
        Array.Fill(transform, (byte)1);
        transform[0] = 4;

        Sprite sprite = new(image, 0, 0, transform);

        // Transform 4 is below the default threshold of 6 and counts once the caller lowers it.
        Assert.Equal(Rect.Empty, ImageOps.GetActiveRoi(sprite));
        Assert.Equal(new Rect(0, 0, 1, 1), ImageOps.GetActiveRoi(sprite, minTransformValue: 4));
    }
}
