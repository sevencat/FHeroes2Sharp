using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="FrameBorder"/>, the floating window frame of <c>dialog_frameborder.cpp:34-77</c>.
/// </summary>
public sealed class FrameBorderTests
{
    [Fact]
    public void SetPosition_GrowsTheContentAreaByTheBorderOnEverySide()
    {
        PixelBuffer target = new(320, 240);
        FrameBorder border = new(target);

        border.SetPosition(30, 40, 100, 60);

        Assert.True(border.IsValid);
        Assert.Equal(new Rect(30, 40, 132, 92), border.Rect);
        Assert.Equal(new Rect(46, 56, 100, 60), border.Area);
        Assert.Equal(new Rect(30, 40, 132, 16), border.Top);
        Assert.Equal(FrameBorder.DefaultBorderWidth, border.BorderWidth);
        Assert.Equal(FrameBorder.DefaultBorderWidth, border.BorderHeight);
    }

    [Fact]
    public void SetPosition_WithoutAContentSize_KeepsTheFrameSize()
    {
        PixelBuffer target = new(320, 240);
        FrameBorder border = new(target);

        border.SetPosition(30, 40, 100, 60);
        border.SetPosition(80, 90, 0, 0);

        // Only the origin moves: the remembered rectangle keeps its size, contents included.
        Assert.Equal(new Rect(80, 90, 132, 92), border.Rect);
        Assert.Equal(new Rect(96, 106, 100, 60), border.Area);
    }

    [Fact]
    public void SetPosition_UsesTheRequestedBorderWidth()
    {
        PixelBuffer target = new(320, 240);
        FrameBorder border = new(target, borderWidth: 8);

        border.SetPosition(10, 20, 50, 40);

        Assert.Equal(new Rect(10, 20, 66, 56), border.Rect);
        Assert.Equal(new Rect(18, 28, 50, 40), border.Area);
        Assert.Equal(8, border.Top.Height);
    }

    [Fact]
    public void Dispose_RestoresThePixelsTheFrameCovered()
    {
        PixelBuffer target = new(320, 240);
        target.Fill(7);

        using (FrameBorder border = new(target))
        {
            border.SetPosition(10, 10, 20, 20);
            target.Fill(9);
        }

        // The saved rectangle is the contents grown by the border: 20 + 2 * 16 on both axes.
        Assert.Equal((byte)7, target[10, 10]);
        Assert.Equal((byte)7, target[61, 61]);
        Assert.Equal((byte)9, target[62, 62]);
    }

    [Fact]
    public void RenderOther_StretchesTheSourceOverTheRectangle()
    {
        PixelBuffer target = new(16, 16);
        PixelBuffer source = new(2, 2);
        source[0, 0] = 1;
        source[1, 0] = 2;
        source[0, 1] = 3;
        source[1, 1] = 4;

        FrameBorder.RenderOther(target, source, new Rect(4, 4, 4, 4));

        Assert.Equal((byte)1, target[4, 4]);
        Assert.Equal((byte)2, target[7, 4]);
        Assert.Equal((byte)3, target[4, 7]);
        Assert.Equal((byte)4, target[7, 7]);
        Assert.Equal((byte)0, target[3, 3]);
    }

    [Fact]
    public void RenderRegular_StartsTheStretchAfterTheShadowStrip()
    {
        PixelBuffer target = new(32, 32);
        Sprite artwork = CreateColumnArtwork(40, 40);

        FrameBorder.RenderRegular(target, artwork, new Rect(4, 4, 24, 24));

        // The frame is stretched from x = ShadowWidth on, so the first drawn column is that of the artwork.
        Assert.Equal((byte)FrameBorder.ShadowWidth, target[4, 4]);
        Assert.Equal((byte)39, target[27, 27]);
    }

    [Fact]
    public void RenderRegular_DoesNothingForAnEmptyRectangle()
    {
        PixelBuffer target = new(8, 8);
        Sprite artwork = CreateColumnArtwork(40, 40);

        FrameBorder.RenderRegular(target, artwork, new Rect(0, 0, 0, 0));

        Assert.All(target.Pixels, value => Assert.Equal((byte)0, value));
    }

    private static Sprite CreateColumnArtwork(int width, int height)
    {
        PixelBuffer image = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                image[x, y] = (byte)x;
            }
        }

        return new Sprite(image, 0, 0, new byte[width * height]);
    }
}
