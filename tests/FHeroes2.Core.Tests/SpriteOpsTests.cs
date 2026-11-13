using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// The sprite level primitives of <see cref="ImageOps"/> that write both layers: <c>Copy</c>,
/// <c>Fill</c>, <c>SetPixel</c> and <c>DrawLine</c> of <c>src/engine/image.cpp</c>.
/// </summary>
public sealed class SpriteOpsTests
{
    [Fact]
    public void Copy_CopiesColourAndTransform_WhenBothImagesHaveATransformLayer()
    {
        Sprite source = CreateSprite(4, 4, 5);
        source.Image[1, 1] = 9;

        Sprite destination = CreateSprite(6, 6, 1);

        ImageOps.Copy(source, new Rect(0, 0, 2, 2), destination, new Point(2, 3));

        Assert.Equal((byte)9, destination.Image[3, 4]);
        Assert.Equal((byte)5, destination.Transform![(4 * 6) + 3]);

        // Everything outside the copied rectangle keeps its old transform value.
        Assert.Equal((byte)1, destination.Transform[0]);
        Assert.Equal((byte)1, destination.Transform[(5 * 6) + 5]);
    }

    [Fact]
    public void Copy_ZeroesTheTransform_WhenTheSourceIsSingleLayer()
    {
        Sprite source = new(new PixelBuffer(4, 4));
        source.Image[0, 0] = 42;

        Sprite destination = CreateSprite(4, 4, 1);

        ImageOps.Copy(source, new Rect(0, 0, 4, 4), destination, new Point(0, 0));

        Assert.Equal((byte)42, destination.Image[0, 0]);
        Assert.All(destination.Transform!, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Copy_LeavesTheTransform_AloneWhenTheDestinationIsSingleLayer()
    {
        Sprite source = CreateSprite(4, 4, 5);
        source.Image[2, 2] = 17;

        Sprite destination = new(new PixelBuffer(4, 4));

        ImageOps.Copy(source, source.Image.Bounds, destination, new Point(0, 0));

        Assert.Equal((byte)17, destination.Image[2, 2]);
        Assert.Null(destination.Transform);
    }

    [Fact]
    public void Copy_ClipsTheAreaToBothImages()
    {
        Sprite source = CreateSprite(4, 4, 0);
        source.Image[3, 3] = 8;

        Sprite destination = CreateSprite(4, 4, 1);

        // The third column and row of the requested area fall outside the destination and are dropped.
        ImageOps.Copy(source, new Rect(2, 2, 4, 4), destination, new Point(2, 2));

        Assert.Equal((byte)8, destination.Image[3, 3]);
        Assert.Equal((byte)1, destination.Transform![(3 * 4) + 0]);
    }

    private static Sprite CreateSprite(int width, int height, byte transformValue) =>
        new(new PixelBuffer(width, height), 0, 0, Enumerable.Repeat(transformValue, width * height).ToArray());

    [Fact]
    public void FillSprite_WritesColourAndClearsTheTransform_OnlyInsideTheArea()
    {
        Sprite sprite = CreateSprite(4, 4, 1);

        ImageOps.FillSprite(sprite, new Rect(1, 1, 2, 2), 33);

        Assert.Equal((byte)33, sprite.Image[1, 1]);
        Assert.Equal((byte)33, sprite.Image[2, 2]);
        Assert.Equal((byte)0, sprite.Transform![(1 * 4) + 1]);
        Assert.Equal((byte)1, sprite.Transform[0]);
        Assert.Equal((byte)0, sprite.Image[0, 0]);
    }

    [Fact]
    public void SetPixel_IgnoresCoordinatesOutsideTheSprite()
    {
        Sprite sprite = CreateSprite(2, 2, 1);

        ImageOps.SetPixel(sprite, -1, 0, 4);
        ImageOps.SetPixel(sprite, 0, -1, 4);
        ImageOps.SetPixel(sprite, 2, 0, 4);
        ImageOps.SetPixel(sprite, 0, 2, 4);

        Assert.All(sprite.Image.Pixels, value => Assert.Equal((byte)0, value));

        ImageOps.SetPixel(sprite, 1, 1, 4);

        Assert.Equal((byte)4, sprite.Image[1, 1]);
        Assert.Equal((byte)0, sprite.Transform![(1 * 2) + 1]);
    }

    [Fact]
    public void DrawLine_DrawsBothEndsAndThePixelsBetweenThem()
    {
        Sprite sprite = CreateSprite(8, 8, 1);

        ImageOps.DrawLine(sprite, new Point(0, 0), new Point(7, 7), 6);

        for (int i = 0; i < 8; ++i)
        {
            Assert.Equal((byte)6, sprite.Image[i, i]);
            Assert.Equal((byte)0, sprite.Transform![(i * 8) + i]);
        }

        Assert.Equal((byte)0, sprite.Image[0, 7]);
    }

    [Fact]
    public void DrawLine_MatchesTheBresenhamPatternOfTheReference()
    {
        Sprite sprite = CreateSprite(8, 8, 1);

        // A shallow line: one y step for every two x steps, like fheroes2::DrawLine().
        ImageOps.DrawLine(sprite, new Point(0, 0), new Point(6, 3), 2);

        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 3 }, Enumerable.Range(0, 7).Select(x => RowOf(sprite, x)).ToArray());
    }

    private static int RowOf(Sprite sprite, int x)
    {
        for (int y = 0; y < sprite.Height; ++y)
        {
            if (sprite.Image[x, y] == 2)
            {
                return y;
            }
        }

        return -1;
    }
}
