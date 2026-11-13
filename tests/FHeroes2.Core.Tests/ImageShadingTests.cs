using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Covers the index based effects the dialogs are painted with. The pixel exact comparison with the
/// C++ engine lives in <c>tools/scripts/CompareEffectsWithReference.ps1</c>; the tests below pin the
/// contract of the C# API on its own, so a regression is caught without a compiler for the reference.
/// </summary>
public sealed class ImageShadingTests
{
    private const byte Background = 128;

    [Fact]
    public void ApplyTransform_ConvertsOnlyTheRequestedArea()
    {
        PixelBuffer target = Filled(8, 6);

        ImageOps.ApplyTransform(target, 2, 1, 3, 2, 5);

        byte expected = PaletteTransformTable.Apply(5, Background);

        for (int y = 0; y < target.Height; ++y)
        {
            for (int x = 0; x < target.Width; ++x)
            {
                bool inside = x is >= 2 and < 5 && y is >= 1 and < 3;
                Assert.Equal(inside ? expected : Background, target[x, y]);
            }
        }
    }

    [Fact]
    public void ApplyTransform_ClipsTheRectangleToTheImage()
    {
        PixelBuffer target = Filled(4, 4);

        // Three of the five columns and three of the four rows are outside of the image.
        ImageOps.ApplyTransform(target, -2, -1, 5, 4, 5);

        byte expected = PaletteTransformTable.Apply(5, Background);

        Assert.Equal(expected, target[0, 0]);
        Assert.Equal(expected, target[2, 2]);
        Assert.Equal(Background, target[3, 2]);
        Assert.Equal(Background, target[2, 3]);
    }

    [Fact]
    public void ApplyTransform_RejectsAnUnknownTableId()
    {
        PixelBuffer target = Filled(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ImageOps.ApplyTransform(target, 0, 0, 2, 2, PaletteTransformTable.TransformCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageOps.ApplyTransform(target, 0, 0, 2, 2, -1));
    }

    [Fact]
    public void AddGradientShadowForArea_DarkensTheLeftAndBottomEdges()
    {
        PixelBuffer target = Filled(96, 64);

        ImageOps.AddGradientShadowForArea(target, new Point(20, 10), 40, 30, 8);

        // Top row of the left ramp and the rows just above the bottom edge of the area.
        Assert.Equal(PaletteTransformTable.Apply(5, Background), target[19, 18]);
        Assert.Equal(PaletteTransformTable.Apply(2, Background), target[16, 40]);
        Assert.Equal(PaletteTransformTable.Apply(5, Background), target[30, 47]);

        // The shadow is one-sided: the rest of the surface keeps its colour.
        Assert.Equal(Background, target[0, 0]);
        Assert.Equal(Background, target[95, 63]);
        Assert.Equal(Background, target[60, 20]);
    }

    [Fact]
    public void AddGradientShadow_DoesNothingWithoutATransformLayer()
    {
        Sprite source = new(Filled(6, 6));
        PixelBuffer target = Filled(20, 20);

        ImageOps.AddGradientShadow(source, target, new Point(5, 5), new Point(-3, 3));

        Assert.All(target.AsSpan().ToArray(), pixel => Assert.Equal(Background, pixel));
    }

    [Fact]
    public void AddGradientShadow_GetsStrongerTowardsTheArtwork()
    {
        // An opaque silhouette: every pixel of the source is visible.
        Sprite source = new(Filled(6, 6), 0, 0, new byte[36]);
        PixelBuffer target = Filled(20, 20);

        ImageOps.AddGradientShadow(source, target, new Point(5, 5), new Point(-3, 3));

        // Walk the shadow ramp to the left of the source; the closer pixel uses the stronger table.
        Assert.Equal(PaletteTransformTable.Apply(4, Background), target[3, 9]);
        Assert.Equal(PaletteTransformTable.Apply(5, Background), target[2, 8]);
        Assert.Equal(Background, target[0, 0]);
    }

    [Fact]
    public void CreateDitheringTransition_MirrorsTheBandAndSkipsTheMiddleColumns()
    {
        // Every source column holds 100 plus its index, so the destination reveals which column was
        // used: 100 is the left edge of the band, 107 the right one.
        PixelBuffer target = new(8, 8);
        Sprite source = new(Gradient(8, 8, 100));

        ImageOps.CreateDitheringTransition(source, Point.Zero, target, Point.Zero, new Size(8, 8), vertical: true, reverse: false);

        // Rows whose index matches the point of the pattern go to the near half of the band, the
        // others to the far half; each half only ever writes its own column.
        Assert.Equal(100, target[0, 0]);
        Assert.Equal(0, target[0, 1]);
        Assert.Equal(0, target[7, 0]);
        Assert.Equal(107, target[7, 1]);

        // The second column pair uses a period of four rows instead of eight:
        // row 2 matches, so it is the one that goes to the near half.
        Assert.Equal(0, target[1, 0]);
        Assert.Equal(101, target[1, 2]);
        Assert.Equal(106, target[6, 0]);
        Assert.Equal(0, target[6, 2]);
    }

    [Fact]
    public void CreateDitheringTransition_LeavesTransparentSourcePixelsAlone()
    {
        PixelBuffer target = Filled(8, 8);
        byte[] transform = new byte[64];
        Array.Fill(transform, (byte)1);
        Sprite source = new(Gradient(8, 8), 0, 0, transform);

        ImageOps.CreateDitheringTransition(source, Point.Zero, target, Point.Zero, new Size(8, 8), vertical: true, reverse: false);

        Assert.All(target.AsSpan().ToArray(), pixel => Assert.Equal(Background, pixel));
    }

    [Fact]
    public void CreateDitheringTransition_ClipsTheBandToBothImages()
    {
        PixelBuffer target = Filled(10, 10);
        Sprite source = new(Gradient(6, 6));

        // Only the top left 2x2 of the band fits, the rest is clipped away on both sides.
        ImageOps.CreateDitheringTransition(source, new Point(1, 1), target, new Point(8, 8), new Size(6, 6), vertical: false, reverse: false);

        Assert.Equal(1, target[8, 9]);
        Assert.Equal(2, target[9, 8]);
        Assert.Equal(Background, target[8, 8]);
        Assert.Equal(Background, target[7, 8]);
    }

    private static PixelBuffer Filled(int width, int height)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill(Background);

        return buffer;
    }

    /// <summary>Image whose value is the column index plus <paramref name="offset"/>.</summary>
    private static PixelBuffer Gradient(int width, int height, int offset = 0)
    {
        PixelBuffer buffer = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                buffer[x, y] = (byte)(x + offset);
            }
        }

        return buffer;
    }
}