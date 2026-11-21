using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <c>fheroes2::SubpixelResize()</c> (<c>src/engine/image.cpp:2986-3236</c>).
/// </summary>
public sealed class SubpixelResizeTests
{
    /// <summary>Equal sizes fall back to a plain copy of the image layer, the transform layer is left alone.</summary>
    [Fact]
    public void SubpixelResize_CopiesPixelsWhenTheSizesMatch()
    {
        PixelBuffer source = BuildGradient(4, 3);
        Sprite target = BuildSprite(4, 3, transform: 7);

        ImageOps.SubpixelResize(new Sprite(source), target, new Palette());

        Assert.Equal(source.Pixels, target.Image.Pixels);

        for (int i = 0; i < target.Transform!.Length; ++i)
        {
            Assert.Equal(7, target.Transform[i]);
        }
    }

    /// <summary>
    /// Corner aligned upscale: the first and the last source pixel land on the first and the last target
    /// pixel, the one in between is the average of its neighbours.
    /// </summary>
    [Fact]
    public void SubpixelResize_AlignsTheCornersAndAveragesTheMiddle()
    {
        Palette palette = BuildGreyPalette();
        PixelBuffer source = new(2, 1);
        source[0, 0] = 100; // 60
        source[1, 0] = 102; // 20

        PixelBuffer target = new(3, 1);

        ImageOps.SubpixelResize(source, target, palette);

        Assert.Equal(100, target[0, 0]);
        Assert.Equal(101, target[1, 0]); // (60 + 20) / 2 = 40, exactly the grey of entry 101
        Assert.Equal(102, target[2, 0]);
    }

    /// <summary>A 2x2 to 3x3 upscale exercises the four tap path and both edge paths.</summary>
    [Fact]
    public void SubpixelResize_InterpolatesTheInteriorAndCopiesTheEdges()
    {
        Palette palette = BuildGreyPalette();
        PixelBuffer source = new(2, 2);
        source[0, 0] = 100; // 60
        source[1, 0] = 102; // 20
        source[0, 1] = 102; // 20
        source[1, 1] = 102; // 20

        PixelBuffer target = new(3, 3);

        ImageOps.SubpixelResize(source, target, palette);

        Assert.Equal(new byte[] { 100, 101, 102 }, Row(target, 0));
        Assert.Equal(new byte[] { 101, 104, 102 }, Row(target, 1)); // the centre averages to 30
        Assert.Equal(new byte[] { 102, 102, 102 }, Row(target, 2));
    }

    /// <summary>
    /// Transparent source pixels are left out of the average; when less than half of the weight is opaque
    /// the target pixel becomes black and is marked as skipped.
    /// </summary>
    [Fact]
    public void SubpixelResize_CompressesTransparencyIntoBlackPixels()
    {
        Palette palette = BuildGreyPalette();

        PixelBuffer sourceImage = new(2, 1);
        sourceImage[0, 0] = 100;
        sourceImage[1, 0] = 100;
        Sprite source = new(sourceImage, 0, 0, new byte[] { 0, 1 });

        Sprite target = BuildSprite(3, 1, transform: 7);

        ImageOps.SubpixelResize(source, target, palette);

        Assert.Equal(100, target.Image[0, 0]);
        Assert.Equal(100, target.Image[1, 0]); // half of the weight is opaque, so it still counts
        Assert.Equal(10, target.Image[2, 0]); // nothing opaque is left, the reference paints palette black
        Assert.Equal(new byte[] { 0, 0, 1 }, target.Transform);
    }

    /// <summary>A single layer target gets the black pixel but no transform layer to mark it.</summary>
    [Fact]
    public void SubpixelResize_WritesBlackWhenTheTargetHasNoTransformLayer()
    {
        Palette palette = BuildGreyPalette();

        PixelBuffer sourceImage = new(2, 1);
        sourceImage[0, 0] = 100;
        sourceImage[1, 0] = 100;
        Sprite source = new(sourceImage, 0, 0, new byte[] { 1, 1 });

        PixelBuffer target = new(3, 1);

        ImageOps.SubpixelResize(source, target, palette);

        Assert.Equal(new byte[] { 10, 10, 10 }, Row(target, 0));
    }

    /// <summary>A region that does not fit into the image is rejected as a whole, like the engine <c>Validate()</c>.</summary>
    [Fact]
    public void SubpixelResize_DoesNothingWhenTheRegionIsOutside()
    {
        Palette palette = BuildGreyPalette();
        Sprite source = BuildSprite(2, 2, transform: 7);
        Sprite target = BuildSprite(4, 4, transform: 7);

        ImageOps.SubpixelResize(source, new Rect(1, 1, 2, 2), target, new Rect(0, 0, 4, 4), palette);

        Assert.Equal(new byte[16], target.Image.Pixels);
    }

    /// <summary>Rows of the gradient buffer: <c>10 * y + x</c>.</summary>
    private static PixelBuffer BuildGradient(int width, int height)
    {
        PixelBuffer buffer = new(width, height);
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                buffer[x, y] = (byte)((10 * y) + x);
            }
        }

        return buffer;
    }

    private static Sprite BuildSprite(int width, int height, byte transform)
    {
        byte[] layer = new byte[width * height];
        Array.Fill(layer, transform);

        return new Sprite(new PixelBuffer(width, height), 0, 0, layer);
    }

    private static byte[] Row(PixelBuffer image, int y)
    {
        byte[] row = new byte[image.Width];
        for (int x = 0; x < image.Width; ++x)
        {
            row[x] = image[x, y];
        }

        return row;
    }

    /// <summary>
    /// Greys the resampler can land on exactly: entry 100 is <c>(60, 60, 60)</c>, 101 <c>(40, 40, 40)</c>,
    /// 102 <c>(20, 20, 20)</c> and 104 <c>(30, 30, 30)</c> in the six bit space the palette lookup uses.
    /// Every other entry stays black, which is entry 10 as far as <see cref="PALColorId"/> is concerned.
    /// </summary>
    private static Palette BuildGreyPalette()
    {
        byte[] raw = new byte[PaletteTable.Size * 3];
        raw[100 * 3] = raw[(100 * 3) + 1] = raw[(100 * 3) + 2] = 240;
        raw[101 * 3] = raw[(101 * 3) + 1] = raw[(101 * 3) + 2] = 160;
        raw[102 * 3] = raw[(102 * 3) + 1] = raw[(102 * 3) + 2] = 80;
        raw[104 * 3] = raw[(104 * 3) + 1] = raw[(104 * 3) + 2] = 120;

        return new Palette(raw);
    }
}
