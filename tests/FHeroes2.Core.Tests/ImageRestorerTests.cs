using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <see cref="ImageRestorer"/>, the remember-and-put-back helper of <c>src/engine/image.cpp:665-773</c>.
/// </summary>
public sealed class ImageRestorerTests
{
    [Fact]
    public void Restore_PutsBackTheSavedPixels()
    {
        PixelBuffer image = CreateCoordinateBuffer(8, 8);

        using (new ImageRestorer(image, 2, 3, 4, 2))
        {
            image.Fill(99);

            Assert.Equal((byte)99, image[3, 3]);
        }

        Assert.Equal((byte)((3 * 8) + 3 + 1), image[3, 3]);
        Assert.Equal((byte)((3 * 8) + 5 + 1), image[5, 3]);

        // Outside the saved rectangle - x is 2..5, y is 3..4 - the overwrite stays.
        Assert.Equal((byte)99, image[1, 3]);
        Assert.Equal((byte)99, image[6, 3]);
        Assert.Equal((byte)99, image[3, 2]);
        Assert.Equal((byte)99, image[3, 5]);
    }

    [Fact]
    public void Restore_ClipsARectangleThatStartsOutsideTheImage()
    {
        PixelBuffer image = CreateCoordinateBuffer(8, 8);

        // Two columns and one row are outside, so only 2x2 pixels are remembered.
        using (var restorer = new ImageRestorer(image, -2, -1, 4, 3))
        {
            Assert.Equal(0, restorer.X);
            Assert.Equal(0, restorer.Y);
            Assert.Equal(2, restorer.Width);
            Assert.Equal(2, restorer.Height);

            image.Fill(99);
        }

        Assert.Equal((byte)1, image[0, 0]);
        Assert.Equal((byte)((1 * 8) + 1 + 1), image[1, 1]);
        Assert.Equal((byte)99, image[2, 2]);
    }

    [Fact]
    public void Restore_DoesNothingWhenTheRectangleIsFullyOutside()
    {
        PixelBuffer image = CreateCoordinateBuffer(8, 8);

        using (var restorer = new ImageRestorer(image, 20, 20, 4, 4))
        {
            Assert.Equal(new Rect(0, 0, 0, 0), restorer.Rect);

            image.Fill(99);
        }

        Assert.Equal((byte)99, image[0, 0]);
    }

    [Fact]
    public void Update_MovesTheSavedRectangle()
    {
        PixelBuffer image = CreateCoordinateBuffer(8, 8);

        using (var restorer = new ImageRestorer(image, 0, 0, 2, 2))
        {
            restorer.Update(4, 4, 2, 2);

            Assert.Equal(new Rect(4, 4, 2, 2), restorer.Rect);

            image.Fill(99);
        }

        // Only the second rectangle comes back: the first one was forgotten by the update.
        Assert.Equal((byte)99, image[0, 0]);
        Assert.Equal((byte)((4 * 8) + 4 + 1), image[4, 4]);
    }

    [Fact]
    public void Restore_CanRunTwice()
    {
        PixelBuffer image = CreateCoordinateBuffer(4, 4);

        ImageRestorer restorer = new(image, 0, 0, 4, 4);

        image.Fill(99);
        restorer.Restore();
        image.Fill(99);
        restorer.Restore();

        Assert.Equal((byte)1, image[0, 0]);
    }

    [Fact]
    public void Update_ResumesRestoringAfterAnExplicitRestore()
    {
        PixelBuffer image = CreateCoordinateBuffer(4, 4);

        using (var restorer = new ImageRestorer(image, 0, 0, 2, 2))
        {
            image.Fill(99);
            restorer.Restore();

            // Distinctive pixels for the rectangle the update remembers.
            image[2, 2] = 42;
            image[3, 3] = 43;

            restorer.Update(2, 2, 2, 2);
            image.Fill(50);
        }

        Assert.Equal((byte)42, image[2, 2]);
        Assert.Equal((byte)43, image[3, 3]);

        // The first rectangle was restored before the update, so it is not remembered any more.
        Assert.Equal((byte)50, image[0, 0]);
    }

    private static PixelBuffer CreateCoordinateBuffer(int width, int height)
    {
        PixelBuffer buffer = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                buffer[x, y] = (byte)((y * width) + x + 1);
            }
        }

        return buffer;
    }
}
