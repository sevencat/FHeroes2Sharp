using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests for <see cref="Sprite"/> and <see cref="ImageOps.BlitSprite"/>. They pin the transform
/// layer semantics of <c>fheroes2::Blit</c>: 0 copies, 1 skips and everything else converts the
/// destination through <see cref="PaletteTransformTable"/>.
/// </summary>
public sealed class SpriteTests
{
    [Fact]
    public void Constructor_AllocatesAPixelAndTransformLayer()
    {
        Sprite sprite = new(3, 2, -4, 7);

        Assert.Equal(3, sprite.Width);
        Assert.Equal(2, sprite.Height);
        Assert.Equal(-4, sprite.OffsetX);
        Assert.Equal(7, sprite.OffsetY);
        Assert.Equal(6, sprite.Transform!.Length);
        Assert.True(sprite.HasTransformLayer);
    }

    [Fact]
    public void Constructor_RejectsMismatchedTransformLayer()
    {
        PixelBuffer image = new(2, 2);

        Assert.Throws<ArgumentException>(() => new Sprite(image, 0, 0, new byte[3]));
    }

    [Fact]
    public void Reset_MakesTheSpriteFullyTransparent()
    {
        Sprite sprite = new(2, 1);
        sprite.Image[0, 0] = 9;
        sprite.Transform![0] = 4;

        sprite.Reset();

        Assert.Equal(0, sprite.Image[0, 0]);
        Assert.Equal(new byte[] { 1, 1 }, sprite.Transform);
    }

    [Fact]
    public void WithoutTransformLayer_KeepsTheImageAndDropsTheLayer()
    {
        Sprite sprite = new(2, 1);

        Sprite single = sprite.WithoutTransformLayer();

        Assert.False(single.HasTransformLayer);
        Assert.Same(sprite.Image, single.Image);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        Sprite sprite = new(2, 1);
        sprite.Image[0, 0] = 5;
        sprite.Transform![1] = 3;

        Sprite clone = sprite.Clone();
        clone.Image[0, 0] = 6;
        clone.Transform![1] = 0;

        Assert.Equal(5, sprite.Image[0, 0]);
        Assert.Equal(3, sprite.Transform[1]);
    }

    [Fact]
    public void BlitSprite_CopiesOpaquePixelsAndSkipsTransparentOnes()
    {
        PixelBuffer destination = new(2, 1);
        destination.Fill(42);

        Sprite sprite = new(2, 1);
        sprite.Image[0, 0] = 7;
        sprite.Image[1, 0] = 8;
        sprite.Transform![0] = 0;
        sprite.Transform[1] = 1;

        ImageOps.BlitSprite(destination, new Point(0, 0), sprite);

        Assert.Equal(7, destination[0, 0]);
        Assert.Equal(42, destination[1, 0]);
    }

    [Fact]
    public void BlitSprite_ConvertsTheDestinationWithTheTransformTable()
    {
        PixelBuffer destination = new(1, 1);
        destination[0, 0] = 100;

        Sprite sprite = new(1, 1);
        sprite.Image[0, 0] = 255;
        sprite.Transform![0] = 2;

        ImageOps.BlitSprite(destination, new Point(0, 0), sprite);

        Assert.Equal(PaletteTransformTable.Apply(2, 100), destination[0, 0]);
        Assert.NotEqual(100, destination[0, 0]);
    }

    [Fact]
    public void BlitSprite_WithoutTransformLayer_CopiesEverything()
    {
        PixelBuffer destination = new(1, 1);

        Sprite sprite = new(new PixelBuffer(1, 1, new byte[] { 21 }));

        ImageOps.BlitSprite(destination, new Point(0, 0), sprite);

        Assert.Equal(21, destination[0, 0]);
    }

    [Fact]
    public void BlitSprite_UsesTheSpriteOffsets()
    {
        PixelBuffer destination = new(4, 4);

        Sprite sprite = new(1, 1, 2, 3);
        sprite.Image[0, 0] = 17;

        ImageOps.BlitSprite(destination, sprite);

        Assert.Equal(17, destination[2, 3]);
        Assert.Equal(0, destination[0, 0]);
    }

    [Fact]
    public void BlitSprite_ClipsOnAllSides()
    {
        PixelBuffer destination = new(2, 2);

        Sprite sprite = new(2, 2);
        for (int y = 0; y < 2; ++y)
        {
            for (int x = 0; x < 2; ++x)
            {
                sprite.Image[x, y] = (byte)(1 + (y * 2) + x);
            }
        }

        // The top left sprite pixel lies outside the destination, the bottom right one is copied.
        ImageOps.BlitSprite(destination, new Point(-1, -1), sprite);

        Assert.Equal(4, destination[0, 0]);
        Assert.Equal(0, destination[1, 0]);
        Assert.Equal(0, destination[0, 1]);
        Assert.Equal(0, destination[1, 1]);
    }
}
