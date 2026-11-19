using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <see cref="ImageOps.BlitSpriteClipped"/>: drawing a slice of a sprite at a destination point, clipped to a
/// region — the <c>fheroes2::Blit( in, inPos, inSize, out, outPos )</c> shape that
/// <c>castle_mageguild.cpp</c> needs for the bottom bar and the guild picture.
/// </summary>
public sealed class ImageOpsBlitClippedTests
{
    [Fact]
    public void BlitClipped_PutsTheSourceAreaCornerOnTheDestination()
    {
        // Column x carries the colour x + 1, so a copied pixel says which column it came from.
        Sprite sprite = CreateColumnSprite(8, 4);
        PixelBuffer target = new(10, 4);

        ImageOps.BlitSpriteClipped(
            target,
            new Point(2, 1),
            sprite,
            new Rect(1, 0, 4, 2),
            target.Bounds);

        // Source column 1 lands on x = 2, not on 2 + 1.
        Assert.Equal((byte)2, target[2, 1]);
        Assert.Equal((byte)5, target[5, 1]);
        Assert.Equal((byte)2, target[2, 2]);

        // The area is two rows tall, and the untouched columns stay empty.
        Assert.Equal((byte)0, target[2, 3]);
        Assert.Equal((byte)0, target[1, 1]);
        Assert.Equal((byte)0, target[6, 1]);
    }

    [Fact]
    public void BlitClipped_DropsThePartOutsideTheClip()
    {
        Sprite sprite = CreateColumnSprite(8, 4);
        PixelBuffer target = new(10, 4);

        ImageOps.BlitSpriteClipped(
            target,
            new Point(6, 0),
            sprite,
            sprite.Image.Bounds,
            new Rect(0, 0, 8, 4));

        // Source columns 0 and 1 fall on 6 and 7; columns 2.. land past the clip.
        Assert.Equal((byte)1, target[6, 0]);
        Assert.Equal((byte)2, target[7, 0]);
        Assert.Equal((byte)0, target[8, 0]);
        Assert.Equal((byte)0, target[9, 0]);
    }

    [Fact]
    public void BlitClipped_DoesNothingWhenTheAreaMissesTheClip()
    {
        Sprite sprite = CreateColumnSprite(8, 4);
        PixelBuffer target = new(10, 4);

        ImageOps.BlitSpriteClipped(
            target,
            new Point(20, 0),
            sprite,
            sprite.Image.Bounds,
            target.Bounds);

        Assert.All(target.AsSpan().ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void BlitClipped_AppliesTheTransformLayer()
    {
        Sprite sprite = CreateColumnSprite(4, 1);

        // Transform 1 keeps the destination: the shadow rows of the archive must not overwrite anything.
        Array.Fill(sprite.Transform!, (byte)1);
        sprite.Transform![1] = 0;

        PixelBuffer target = new(4, 1);
        target.Fill(9);

        ImageOps.BlitSpriteClipped(target, Point.Zero, sprite, sprite.Image.Bounds, target.Bounds);

        Assert.Equal((byte)9, target[0, 0]);
        Assert.Equal((byte)2, target[1, 0]);
        Assert.Equal((byte)9, target[2, 0]);
    }

    private static Sprite CreateColumnSprite(int width, int height)
    {
        PixelBuffer image = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            Span<byte> row = image.Row(y);

            for (int x = 0; x < width; ++x)
            {
                row[x] = (byte)(x + 1);
            }
        }

        return new Sprite(image, 0, 0, new byte[width * height]);
    }
}
