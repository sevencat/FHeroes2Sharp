using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <see cref="ImageOps.AlphaBlit(Sprite, PixelBuffer, Point, byte, Palette)"/>,
/// <c>fheroes2::AlphaBlit()</c> from <c>src/engine/image.cpp:961-1085</c>.
/// </summary>
public sealed class AlphaBlitTests
{
    private const byte Black = 1;
    private const byte White = 2;
    private const byte MidGrey = 40;
    private const byte Red = 41;
    private const byte Green = 42;
    private const byte Olive = 43;

    /// <summary>
    /// Palette with the entries a 50% blend of black and white (or red and green) lands on.
    /// </summary>
    /// <remarks>
    /// Every value is a multiple of four so that the six bit reading of <see cref="ImageOps.AlphaBlit"/>
    /// (<c>raw &gt;&gt; 2</c>) is exact: white is <c>252</c> = <c>63 &lt;&lt; 2</c>, and the blended greys and olives
    /// are <c>124</c> = <c>31 &lt;&lt; 2</c>.
    /// </remarks>
    private static Palette CreatePalette()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];

        SetColor(raw, White, 252, 252, 252);
        SetColor(raw, MidGrey, 124, 124, 124);
        SetColor(raw, Red, 252, 0, 0);
        SetColor(raw, Green, 0, 252, 0);
        SetColor(raw, Olive, 124, 124, 0);

        return new Palette(raw);
    }

    private static void SetColor(byte[] raw, byte index, byte red, byte green, byte blue)
    {
        raw[index * 3] = red;
        raw[(index * 3) + 1] = green;
        raw[(index * 3) + 2] = blue;
    }

    private static Sprite CreateSprite(byte color, int width = 4, int height = 4)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image);
    }

    [Fact]
    public void HalfOpacity_BlendsTowardsTheSourceColour()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White);
        PixelBuffer target = new(8, 8);
        target.Fill(Black);

        ImageOps.AlphaBlit(source, target, new Point(2, 3), 128, palette);

        // Six bit blend: ( 63 * 128 + 0 * 127 ) / 255 = 31, which is exactly the mid grey entry.
        Assert.Equal(MidGrey, target[2, 3]);
        Assert.Equal(MidGrey, target[5, 6]);

        // Nothing outside the sprite was touched.
        Assert.Equal(Black, target[0, 0]);
        Assert.Equal(Black, target[6, 7]);
    }

    [Fact]
    public void HalfOpacity_MixesTheChannelsIndependently()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(Green);
        PixelBuffer target = new(4, 4);
        target.Fill(Red);

        ImageOps.AlphaBlit(source, target, new Point(1, 1), 128, palette);

        // Half of 63 on red and green, nothing on blue: 31, 31, 0 - the olive entry.
        Assert.Equal(Olive, target[1, 1]);
        Assert.Equal(Red, target[0, 0]);
    }

    [Fact]
    public void BlendFraction_RoundsDownLikeTheReference()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White);
        PixelBuffer target = new(4, 4);
        target.Fill(Black);

        // 63 * 64 / 255 = 15, the reference divides with integer truncation.
        ImageOps.AlphaBlit(source, target, Point.Zero, 64, palette);

        byte expected = PALColorId.Get(15, 15, 15, palette);

        Assert.Equal(expected, target[0, 0]);
        Assert.NotEqual(White, target[0, 0]);
    }

    [Fact]
    public void FullOpacity_IsAPlainBlit()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White);
        PixelBuffer target = new(8, 8);
        target.Fill(Black);

        ImageOps.AlphaBlit(source, target, new Point(4, 4), byte.MaxValue, palette);

        Assert.Equal(White, target[4, 4]);
        Assert.Equal(White, target[7, 7]);
        Assert.Equal(Black, target[3, 3]);
    }

    [Fact]
    public void ZeroOpacity_DrawsNothing()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White);
        PixelBuffer target = new(8, 8);
        target.Fill(Black);

        ImageOps.AlphaBlit(source, target, new Point(4, 4), 0, palette);

        Assert.Equal(Black, target[4, 4]);
        Assert.Equal(Black, target[7, 7]);
    }

    [Fact]
    public void TransformLayer_SkipsSeeThroughPixels()
    {
        Palette palette = CreatePalette();

        // 2x1 sprite: the left pixel is see through (transform 1), the right one is opaque.
        PixelBuffer image = new(2, 1);
        image.Fill(White);
        Sprite source = new(image, 0, 0, [1, 0]);

        PixelBuffer target = new(4, 4);
        target.Fill(Black);

        ImageOps.AlphaBlit(source, target, new Point(1, 1), 128, palette);

        Assert.Equal(Black, target[1, 1]);
        Assert.Equal(MidGrey, target[2, 1]);
    }

    [Fact]
    public void Clipping_KeepsThePixelsInsideTheFrame()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White, 6, 6);
        PixelBuffer target = new(4, 4);
        target.Fill(Black);

        // The sprite starts outside the frame on both axes; only its bottom right corner shows up.
        ImageOps.AlphaBlit(source, target, new Point(-2, -2), 128, palette);

        Assert.Equal(MidGrey, target[0, 0]);
        Assert.Equal(MidGrey, target[3, 3]);
    }

    [Fact]
    public void FullyOutsideTheFrame_DrawsNothing()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White);
        PixelBuffer target = new(4, 4);
        target.Fill(Black);

        ImageOps.AlphaBlit(source, target, new Point(10, 10), 128, palette);

        Assert.Equal(Black, target[0, 0]);
        Assert.Equal(Black, target[3, 3]);
    }

    [Fact]
    public void SpriteOffsetOverload_UsesTheOffset()
    {
        Palette palette = CreatePalette();
        Sprite source = new(CreateSprite(White).Image, 3, 2);
        PixelBuffer target = new(8, 8);
        target.Fill(Black);

        ImageOps.AlphaBlit(source, target, byte.MaxValue, palette);

        Assert.Equal(White, target[3, 2]);
        Assert.Equal(Black, target[0, 0]);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Palette palette = CreatePalette();
        Sprite source = CreateSprite(White);
        PixelBuffer target = new(4, 4);

        Assert.Throws<ArgumentNullException>(() => ImageOps.AlphaBlit(null!, target, Point.Zero, 128, palette));
        Assert.Throws<ArgumentNullException>(() => ImageOps.AlphaBlit(source, null!, Point.Zero, 128, palette));
        Assert.Throws<ArgumentNullException>(() => ImageOps.AlphaBlit(source, target, Point.Zero, 128, null!));
    }
}
