using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The runtime button artwork of <c>src/fheroes2/gui/ui_button.cpp</c>: the stretching of the empty
/// button sprite, the shine decoration painted on top of it and the transparent background of the
/// pressed state.
/// </summary>
public sealed class ButtonArtworkTests
{
    [Fact]
    public void Resize_ReturnsAFreshCopy_WhenTheSizeDoesNotChange()
    {
        Sprite original = CreateCoordinateSprite(4, 3);

        Sprite result = ButtonArtwork.Resize(original, new Size(4, 3));

        Assert.NotSame(original, result);
        Assert.Equal(original.Image.Pixels, result.Image.Pixels);

        // Decorating the result must never touch the template it came from.
        result.Image[0, 0] = 250;
        Assert.NotEqual((byte)250, original.Image[0, 0]);
    }

    [Fact]
    public void Resize_RepeatsTheMiddleThird_WhenOnlyTheWidthGrows()
    {
        Sprite original = CreateCoordinateSprite(9, 5);

        Sprite result = ButtonArtwork.Resize(original, new Size(12, 5));

        // The reference keeps the first third, repeats the middle one and keeps the last third.
        int[] sourceColumns = [0, 1, 2, 3, 4, 5, 3, 4, 5, 6, 7, 8];

        Assert.Equal(12, result.Width);
        AssertAllPixelsAreSource(result, original, (x, y) => (sourceColumns[x], y));
    }

    [Fact]
    public void Resize_RepeatsTheMiddleFifth_WhenOnlyTheHeightGrows()
    {
        Sprite original = CreateCoordinateSprite(5, 10);

        Sprite result = ButtonArtwork.Resize(original, new Size(5, 20));

        int[] sourceRows = [0, 1, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 8, 9];

        Assert.Equal(20, result.Height);
        AssertAllPixelsAreSource(result, original, (x, y) => (x, sourceRows[y]));
    }

    [Fact]
    public void Resize_KeepsTheFourCorners_WhenTheButtonShrinks()
    {
        Sprite original = CreateCoordinateSprite(9, 10);

        Sprite result = ButtonArtwork.Resize(original, new Size(6, 8));

        // Top left quarter, then the bottom left, top right and bottom right corners.
        AssertAllPixelsAreSource(result, original, (x, y) => (x < 3, y < 4) switch
        {
            (true, true) => (x, y),
            (false, true) => (x + 3, y),
            (true, false) => (x, y + 2),
            _ => (x + 3, y + 2),
        });

        // Every pixel of a shrunk button is covered, so nothing stays transparent.
        Assert.All(result.Transform!, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Resize_SqueezesTheEndsAndRepeatsTheMiddle_WhenTheButtonGrowsTallerAndThinner()
    {
        Sprite original = CreateCoordinateSprite(9, 10);

        Sprite result = ButtonArtwork.Resize(original, new Size(6, 14));

        // The left third is kept, the right part comes from the last three columns and the two
        // middle rows repeat.
        int[] sourceRows = [0, 1, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 8, 9];

        Assert.Equal(6, result.Width);
        Assert.Equal(14, result.Height);
        AssertAllPixelsAreSource(result, original, (x, y) => (x < 3 ? x : x + 3, sourceRows[y]));
    }

    [Fact]
    public void Resize_FillsTheCentreWithTheCentralPixel_WhenTheButtonGrowsInBothDirections()
    {
        Sprite original = CreateCoordinateSprite(9, 10);

        Sprite result = ButtonArtwork.Resize(original, new Size(20, 30));

        Assert.Equal(20, result.Width);
        Assert.Equal(30, result.Height);

        // Corners are kept as they are.
        Assert.Equal(original.Image[0, 0], result.Image[0, 0]);
        Assert.Equal(original.Image[8, 0], result.Image[19, 0]);
        Assert.Equal(original.Image[0, 9], result.Image[0, 29]);
        Assert.Equal(original.Image[8, 9], result.Image[19, 29]);

        // The hole in the middle is a flat plate: it takes the colour of the central pixel.
        byte centre = original.Image[4, 4];
        Assert.Equal(centre, result.Image[10, 15]);
        Assert.Equal(centre, result.Image[5, 5]);
        Assert.Equal((byte)0, result.Transform![(15 * 20) + 10]);
    }

    [Fact]
    public void AddShine_PaintsTheGoodInterfaceHighlights()
    {
        Sprite button = CreateHiddenSprite(96, 25);

        ButtonArtwork.AddShine(button, DialogButtonTemplate.Good);

        Assert.Equal((byte)10, button.Image[11, 4]);

        // The longer shine cross the basic one, so these pixels end up with the first colour.
        Assert.Equal((byte)10, button.Image[10, 5]);
        Assert.Equal((byte)39, button.Image[12, 5]);
        Assert.Equal((byte)39, button.Image[8, 7]);
        Assert.Equal((byte)39, button.Image[15, 4]);

        // Right side, mirroring the left one.
        Assert.Equal((byte)10, button.Image[87, 4]);
        Assert.Equal((byte)10, button.Image[86, 5]);
        Assert.Equal((byte)10, button.Image[85, 6]);

        // The wider shine starts on the pixel the basic one had set.
        Assert.Equal((byte)10, button.Image[88, 5]);

        // The longer shines are only drawn on a wide button.
        Assert.Equal((byte)39, button.Image[6, 11]);
        Assert.Equal((byte)39, button.Image[9, 8]);
        Assert.Equal((byte)10, button.Image[7, 10]);
        Assert.Equal((byte)10, button.Image[86, 2]);

        // Bottom left shine; the centre one needs more than 96x25 pixels.
        Assert.Equal((byte)39, button.Image[11, 19]);
        Assert.Equal((byte)10, button.Image[12, 18]);
        Assert.Equal((byte)39, button.Image[14, 16]);
        Assert.Equal((byte)0, button.Image[30, 20]);

        // Written pixels become visible, untouched ones stay transparent.
        Assert.Equal((byte)0, button.Transform![(4 * 96) + 11]);
        Assert.Equal((byte)1, button.Transform[0]);
    }

    [Fact]
    public void AddShine_UsesTheEvilInterfaceColours()
    {
        Sprite button = new(96, 25);

        ButtonArtwork.AddShine(button, DialogButtonTemplate.Evil);

        Assert.Equal((byte)10, button.Image[11, 4]);
        Assert.Equal((byte)10, button.Image[10, 5]);
        Assert.Equal((byte)16, button.Image[8, 7]);
        Assert.Equal((byte)15, button.Image[88, 3]);
        Assert.Equal((byte)15, button.Image[85, 3]);
        Assert.Equal((byte)16, button.Image[6, 11]);
    }

    [Fact]
    public void AddShine_SkipsTheLongShines_OnANarrowButton()
    {
        Sprite button = new(40, 25);

        ButtonArtwork.AddShine(button, DialogButtonTemplate.Good);

        // The basic left and right shines are always drawn.
        Assert.Equal((byte)10, button.Image[11, 4]);
        Assert.Equal((byte)10, button.Image[31, 4]);

        // Everything guarded by "width > 50" is not.
        Assert.Equal((byte)0, button.Image[6, 11]);
        Assert.Equal((byte)0, button.Image[30, 2]);
        Assert.Equal((byte)0, button.Image[11, 19]);
    }

    [Fact]
    public void AddShine_LeavesTheOtherTemplatesAlone()
    {
        Sprite button = CreateHiddenSprite(96, 25);

        ButtonArtwork.AddShine(button, DialogButtonTemplate.GuildWell);

        Assert.All(button.Image.Pixels, value => Assert.Equal((byte)0, value));
        Assert.All(button.Transform!, value => Assert.Equal((byte)1, value));
    }

    [Fact]
    public void MakeTransparentBackground_FillsThePressedOnlyPixelsFromTheCentreOfTheBackground()
    {
        Sprite released = CreateUniformSprite(4, 4, 1, 0);
        Sprite pressed = CreateUniformSprite(4, 4, 2, 0);
        pressed.Transform![0] = 1;
        pressed.Transform[1] = 1;

        Sprite background = CreateCoordinateSprite(8, 8);

        ButtonArtwork.MakeTransparentBackground(released, pressed, background);

        // The pressed sprite is centred on the background, so (0, 0) reads (2, 2).
        Assert.Equal(background.Image[2, 2], pressed.Image[0, 0]);
        Assert.Equal(background.Image[3, 2], pressed.Image[1, 0]);
        Assert.Equal((byte)0, pressed.Transform[0]);

        // A pixel that both states cover keeps the pressed colour.
        Assert.Equal((byte)2, pressed.Image[2, 0]);
        Assert.Equal((byte)0, pressed.Transform[2]);
    }

    [Fact]
    public void MakeTransparentBackground_RequiresATransformLayer()
    {
        Sprite released = new(new PixelBuffer(4, 4));
        Sprite pressed = CreateUniformSprite(4, 4, 2, 1);

        Assert.Throws<InvalidOperationException>(() =>
            ButtonArtwork.MakeTransparentBackground(released, pressed, CreateCoordinateSprite(8, 8)));
    }

    private static Sprite CreateHiddenSprite(int width, int height) =>
        new(new PixelBuffer(width, height), 0, 0, Enumerable.Repeat((byte)1, width * height).ToArray());

    private static Sprite CreateUniformSprite(int width, int height, byte color, byte transformValue)
    {
        Sprite sprite = new(new PixelBuffer(width, height), 0, 0,
            Enumerable.Repeat(transformValue, width * height).ToArray());
        sprite.Image.Fill(color);
        return sprite;
    }

    private static Sprite CreateCoordinateSprite(int width, int height)
    {
        Sprite sprite = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                sprite.Image[x, y] = (byte)((y * width) + x);
            }
        }

        return sprite;
    }

    private static void AssertAllPixelsAreSource(Sprite result, Sprite original, Func<int, int, (int X, int Y)> source)
    {
        for (int y = 0; y < result.Height; ++y)
        {
            for (int x = 0; x < result.Width; ++x)
            {
                (int sourceX, int sourceY) = source(x, y);
                Assert.Equal(original.Image[sourceX, sourceY], result.Image[x, y]);
            }
        }
    }
}
