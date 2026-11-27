using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests of <c>fheroes2::StandardWindow::applyTextBackgroundShading()</c> (<c>gui/ui_window.cpp:337-364</c>)
/// as ported to <see cref="ImageOps.ApplyTextBackgroundShading"/>.
/// </summary>
public sealed class ImageOpsTextBackgroundShadingTests
{
    private const byte Base = 10;

    /// <summary>A rectangle wide enough for all nine bands (0…8) plus a centre.</summary>
    private static readonly Rect Roi = new(5, 5, 60, 40);

    [Fact]
    public void TheCentreIsDarkenedByTheTwoInnerPasses()
    {
        PixelBuffer target = Uniform(80, 60);

        ImageOps.ApplyTextBackgroundShading(target, Roi);

        // 参考先按 3 号表、再按 5 号表各转一次。
        Assert.Equal(Inner(Base), target[35, 25]);
    }

    [Fact]
    public void TheBorderIsNineBandsOfAlternatingStrength()
    {
        PixelBuffer target = Uniform(80, 60);

        ImageOps.ApplyTextBackgroundShading(target, Roi);

        // 从外到内：9、4、3、2（3 像素宽的一圈）、3、4、5。
        Assert.Equal(Transform(9, Base), target[Roi.X, Roi.Y]);
        Assert.Equal(Transform(4, Base), target[Roi.X + 1, Roi.Y + 1]);
        Assert.Equal(Transform(3, Inner(Base)), target[Roi.X + 2, Roi.Y + 2]);

        for (int offset = 3; offset <= 5; ++offset)
        {
            Assert.Equal(Transform(2, Inner(Base)), target[Roi.X + offset, Roi.Y + offset]);
        }

        Assert.Equal(Transform(3, Inner(Base)), target[Roi.X + 6, Roi.Y + 6]);
        Assert.Equal(Transform(4, Inner(Base)), target[Roi.X + 7, Roi.Y + 7]);
        Assert.Equal(Transform(5, Inner(Base)), target[Roi.X + 8, Roi.Y + 8]);
    }

    [Fact]
    public void TheBandsFollowAllFourEdges()
    {
        PixelBuffer target = Uniform(80, 60);

        ImageOps.ApplyTextBackgroundShading(target, Roi);

        // 上带整宽：右上角的上带与左上角同表。
        Assert.Equal(Transform(9, Base), target[Roi.Right - 1, Roi.Y]);

        // 下带：左下角也是 1 像素宽的外圈。
        Assert.Equal(Transform(9, Base), target[Roi.X, Roi.Bottom - 1]);

        // 左右两条竖带各自存在，且不重复处理角。
        Assert.Equal(Transform(9, Base), target[Roi.Right - 1, 32]);
        Assert.Equal(Transform(9, Base), target[Roi.X, 32]);
    }

    [Fact]
    public void NothingOutsideTheRectangleChanges()
    {
        PixelBuffer target = Uniform(80, 60);

        ImageOps.ApplyTextBackgroundShading(target, Roi);

        Assert.Equal(Base, target[Roi.X - 1, Roi.Y]);
        Assert.Equal(Base, target[Roi.X, Roi.Y - 1]);
        Assert.Equal(Base, target[Roi.Right, Roi.Y + 10]);
        Assert.Equal(Base, target[Roi.X + 10, Roi.Bottom]);
        Assert.Equal(Base, target[0, 0]);
        Assert.Equal(Base, target[79, 59]);
    }

    [Fact]
    public void ARectangleLargerThanTheBufferIsClipped()
    {
        PixelBuffer target = Uniform(20, 20);

        // 越界部分由 ApplyTransform 裁掉：(0,0) 落在偏移 4，属于 3 像素宽的那一圈。
        ImageOps.ApplyTextBackgroundShading(target, new Rect(-4, -4, 40, 40));

        Assert.Equal(Transform(2, Inner(Base)), target[0, 0]);
    }

    [Fact]
    public void ATooSmallRectangleChangesNothing()
    {
        PixelBuffer target = Uniform(20, 20);

        // 宽高算出来是负数或 0 的边带什么也不做。
        ImageOps.ApplyTextBackgroundShading(target, new Rect(5, 5, 2, 2));

        for (int y = 0; y < target.Height; ++y)
        {
            for (int x = 0; x < target.Width; ++x)
            {
                Assert.Equal(Base, target[x, y]);
            }
        }
    }

    private static byte Inner(byte value) => Transform(5, Transform(3, value));

    private static byte Transform(int transformId, byte value) => PaletteTransformTable.Apply(transformId, value);

    private static PixelBuffer Uniform(int width, int height)
    {
        PixelBuffer buffer = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            buffer.Row(y).Fill(Base);
        }

        return buffer;
    }
}
