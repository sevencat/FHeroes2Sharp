using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="StandardWindow"/>: the constants of <c>ui_window.cpp</c>, the layout arithmetic
/// and the painting of the frame, its borders and the tiled interior.
/// </summary>
/// <remarks>
/// The window reads <c>SURDRBKG.ICN</c>, <c>WINLOSE.ICN</c> and <c>STONEBAK.ICN</c> itself when it is
/// first drawn, so the drawing tests need the installed archives and return when the machine has none.
/// Laying a window out works without game data, which keeps the arithmetic tests free of it.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class StandardWindowTests
{
    private const byte Canvas = 3;

    private const int ButtonsWidth = 59;

    private const int ButtonsHeight = 27;

    [Fact]
    public void Constants_MatchTheReferenceEngine()
    {
        // borderSize, borderEdgeOffset, transitionSize and backgroundOffset of ui_window.cpp.
        Assert.Equal(16, StandardWindow.BorderWidth);
        Assert.Equal(43, StandardWindow.BorderEdgeOffset);
        Assert.Equal(10, StandardWindow.TransitionSize);
        Assert.Equal(22, StandardWindow.BackgroundOffset);
    }

    [Fact]
    public void Create_WrapsTheActiveAreaInTheBorder()
    {
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));

        Assert.Equal(new Rect(170, 140, 300, 200), window.ActiveArea);
        Assert.Equal(new Rect(154, 124, 332, 232), window.WindowArea);

        // The shadow hangs off the left and the bottom side only, so the total area is 16 pixels
        // wider and 16 pixels taller than the frame.
        Assert.Equal(new Rect(138, 124, 348, 248), window.TotalArea);
        Assert.True(window.HasBackground);
    }

    [Fact]
    public void CreateCentered_CentersTheActiveAreaOnTheScreen()
    {
        StandardWindow window = StandardWindow.CreateCentered(new Size(300, 200));

        Assert.Equal(new Rect(170, 140, 300, 200), window.ActiveArea);

        // An odd difference leaves the extra pixel on the right and the bottom side.
        StandardWindow odd = StandardWindow.CreateCentered(new Size(301, 201));
        Assert.Equal(new Rect(169, 139, 301, 201), odd.ActiveArea);
    }

    [Fact]
    public void Draw_WithoutGameDataThrows()
    {
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));

        GameAssets.Current = null;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => window.Draw(CreateCanvas()));

        Assert.Contains("SURDRBKG.ICN", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Draw_PaintsCornersBordersAndTheTiledInterior() => Assert.True(TryWithArchives(() =>
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));
        Rect frame = window.WindowArea;

        window.Draw(target);

        // The four corners and both vertical borders come from WINLOSE.ICN ...
        Assert.NotEqual(Canvas, target[frame.X + 5, frame.Y + 5]);
        Assert.NotEqual(Canvas, target[frame.Right - 5, frame.Y + 5]);
        Assert.NotEqual(Canvas, target[frame.X + 5, frame.Bottom - 5]);
        Assert.NotEqual(Canvas, target[frame.Right - 5, frame.Bottom - 5]);
        Assert.NotEqual(Canvas, target[frame.X + 5, frame.Y + (frame.Height / 2)]);
        Assert.NotEqual(Canvas, target[frame.Right - 5, frame.Y + (frame.Height / 2)]);

        // ... the top and the bottom border from SURDRBKG.ICN ...
        Assert.NotEqual(Canvas, target[frame.X + (frame.Width / 2), frame.Y + 5]);
        Assert.NotEqual(Canvas, target[frame.X + (frame.Width / 2), frame.Bottom - 20]);

        // ... and the interior is the STONEBAK.ICN tile, offset by the background margin.
        Assert.NotEqual(Canvas, target[frame.X + StandardWindow.BorderEdgeOffset + 5,
            frame.Y + StandardWindow.BorderEdgeOffset + 5]);
        Assert.NotEqual(Canvas, target[frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2)]);

        // Nothing outside the frame is touched.
        Assert.Equal(Canvas, target[frame.X - 1, frame.Y - 1]);
    }));

    [Fact]
    public void Draw_MixesBorderAndInteriorInTheCornerSeam() => Assert.True(TryWithArchives(() =>
    {
        PixelBuffer withBackground = CreateCanvas();
        PixelBuffer withoutBackground = CreateCanvas();
        Rect activeArea = new(170, 140, 300, 200);

        StandardWindow.Create(activeArea).Draw(withBackground);
        StandardWindow.Create(activeArea, renderBackground: false).Draw(withoutBackground);

        // A dithered transition, not a hard edge: in the 10 pixel band under the top left corner some
        // pixels come from the tiled interior, which only the version with a background paints.
        Rect frame = StandardWindow.Create(activeArea).WindowArea;
        int startX = frame.X + StandardWindow.BackgroundOffset;
        int startY = frame.Y + StandardWindow.BackgroundOffset;
        int interiorPixels = 0;

        for (int y = startY; y < startY + StandardWindow.TransitionSize; ++y)
        {
            for (int x = startX; x < frame.X + StandardWindow.BorderEdgeOffset; ++x)
            {
                interiorPixels += withBackground[x, y] != withoutBackground[x, y] ? 1 : 0;
            }
        }

        Assert.True(interiorPixels > 0, "the seam should keep pixels of the interior");
    }));

    [Fact]
    public void Draw_WithoutBackground_LeavesTheInteriorUntouched() => Assert.True(TryWithArchives(() =>
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200), renderBackground: false);
        Rect frame = window.WindowArea;

        window.Draw(target);

        Assert.False(window.HasBackground);

        // The corner pieces grow by the background margin, so the frame is still solid ...
        Assert.NotEqual(Canvas, target[frame.X + 5, frame.Y + 5]);
        Assert.NotEqual(Canvas, target[frame.X + 30, frame.Y + 5]);
        Assert.NotEqual(Canvas, target[frame.X + 5, frame.Y + 30]);

        // ... but nothing is tiled inside it.
        Assert.Equal(Canvas, target[frame.X + 30, frame.Y + 30]);
        Assert.Equal(Canvas, target[frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2)]);
    }));

    [Fact]
    public void Draw_ClipsAWindowThatStartsOutsideTheTarget() => Assert.True(TryWithArchives(() =>
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(new Rect(0, 0, 700, 460));

        window.Draw(target);

        // The frame starts at (-16,-16), so the left border is cut off but the interior is still
        // tiled into the visible part of the target.
        Assert.NotEqual(Canvas, target[5, 5]);
        Assert.NotEqual(Canvas, target[350, 200]);
    }));

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));

        Assert.Throws<ArgumentNullException>(() => window.Draw(null!));
    }

    [Fact]
    public void ActiveAreaForSymmetricButtons_SizesTheDialogAroundFourButtons()
    {
        ButtonGroup buttons = CreateButtonGroup(4);

        // Four buttons make two columns: 60 pixels of horizontal padding, two button widths and one
        // 20 pixel gap. The cancel button reserves 46 pixels of height under them.
        Rect area = StandardWindow.ActiveAreaForSymmetricButtons(buttons, false, 0);

        Assert.Equal(new Rect(221, 167, 198, 146), area);
    }

    [Fact]
    public void ActiveAreaForSymmetricButtons_GivesAnOddCountOneColumn()
    {
        ButtonGroup buttons = CreateButtonGroup(5);

        // Five buttons are laid out in one column, but the padding and the room a cancel button would
        // need still follow the requested layout: 60 pixels wide, 46 pixels reserved under the group and
        // 26 pixels above and below, because isSingleColumn is false.
        Rect area = StandardWindow.ActiveAreaForSymmetricButtons(buttons, false, 0);

        Assert.Equal(new Rect(260, 116, 119, 247), area);
    }

    [Fact]
    public void ActiveAreaForSymmetricButtons_GivesMoreThanSixButtonsThreeColumns()
    {
        ButtonGroup buttons = CreateButtonGroup(9);

        Rect area = StandardWindow.ActiveAreaForSymmetricButtons(buttons, false, 0);

        Assert.Equal(new Rect(181, 143, 277, 193), area);
    }

    [Fact]
    public void ActiveAreaForSymmetricButtons_KeepsTwoButtonsOnOneRow()
    {
        ButtonGroup buttons = CreateButtonGroup(2);

        Rect area = StandardWindow.ActiveAreaForSymmetricButtons(buttons, false, 0);

        Assert.Equal(new Rect(212, 190, 215, 99), area);
    }

    [Fact]
    public void ActiveAreaForSymmetricButtons_AddsTheExtraHeight()
    {
        ButtonGroup buttons = CreateButtonGroup(4);

        Rect area = StandardWindow.ActiveAreaForSymmetricButtons(buttons, false, 30);

        Assert.Equal(146 + 30, area.Height);
        Assert.Equal(167 - 15, area.Y);
    }

    [Fact]
    public void ActiveAreaForSymmetricButtons_RejectsAnEmptyGroup()
    {
        Assert.Throws<ArgumentException>(
            () => StandardWindow.ActiveAreaForSymmetricButtons(new ButtonGroup(), false, 0));
    }

    [Fact]
    public void PlaceSymmetricButtons_LaysFourButtonsOutInTwoColumns()
    {
        ButtonGroup buttons = CreateButtonGroup(4);
        StandardWindow window = StandardWindow.CreateForSymmetricButtons(buttons);

        window.PlaceSymmetricButtons(buttons);

        // The first button starts 30 pixels in and 15 pixels down from the active area.
        Assert.Equal(new Point(251, 182), TopLeftOf(buttons[0]));
        Assert.Equal(new Point(330, 182), TopLeftOf(buttons[1]));
        Assert.Equal(new Point(251, 229), TopLeftOf(buttons[2]));
        Assert.Equal(new Point(330, 229), TopLeftOf(buttons[3]));
    }

    [Fact]
    public void PlaceSymmetricButtons_MovesTheGroupDownByTheOffset()
    {
        ButtonGroup buttons = CreateButtonGroup(4);
        StandardWindow window = StandardWindow.CreateForSymmetricButtons(buttons);

        window.PlaceSymmetricButtons(buttons, offsetY: 11);

        Assert.Equal(new Point(251, 193), TopLeftOf(buttons[0]));
        Assert.Equal(new Point(330, 240), TopLeftOf(buttons[3]));
    }

    [Fact]
    public void PlaceSymmetricButtons_CentresALastRowThatIsNotFull()
    {
        ButtonGroup buttons = CreateButtonGroup(8);
        StandardWindow window = StandardWindow.CreateForSymmetricButtons(buttons);

        window.PlaceSymmetricButtons(buttons);

        // Eight buttons make three columns and a last row of two. The pair is shifted by half a button
        // plus half a gap, which is 39 pixels, and the active area of eight buttons starts at (181,143).
        Assert.Equal(new Point(211, 158), TopLeftOf(buttons[0]));
        Assert.Equal(new Point(250, 252), TopLeftOf(buttons[6]));
        Assert.Equal(new Point(329, 252), TopLeftOf(buttons[7]));
    }

    [Fact]
    public void RenderPosition_AlignsToEveryPadding()
    {
        StandardWindow window = StandardWindow.Create(new Rect(100, 100, 200, 100));
        Size item = new(ButtonsWidth, ButtonsHeight);

        Assert.Equal(new Point(120, 107), window.RenderPosition(new Point(20, 7), item, WindowPadding.TopLeft));
        Assert.Equal(new Point(190, 107), window.RenderPosition(new Point(20, 7), item, WindowPadding.TopCenter));
        Assert.Equal(new Point(221, 107), window.RenderPosition(new Point(20, 7), item, WindowPadding.TopRight));
        Assert.Equal(new Point(120, 143), window.RenderPosition(new Point(20, 7), item, WindowPadding.CenterLeft));
        Assert.Equal(new Point(190, 143), window.RenderPosition(new Point(20, 7), item, WindowPadding.CenterCenter));
        Assert.Equal(new Point(221, 143), window.RenderPosition(new Point(20, 7), item, WindowPadding.CenterRight));
        Assert.Equal(new Point(120, 166), window.RenderPosition(new Point(20, 7), item, WindowPadding.BottomLeft));
        Assert.Equal(new Point(190, 166), window.RenderPosition(new Point(20, 7), item, WindowPadding.BottomCenter));
        Assert.Equal(new Point(221, 166), window.RenderPosition(new Point(20, 7), item, WindowPadding.BottomRight));
    }

    [Fact]
    public void RenderPosition_RejectsAnUnknownPadding()
    {
        StandardWindow window = StandardWindow.Create(new Rect(100, 100, 200, 100));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => window.RenderPosition(new Point(0, 0), new Size(10, 10), (WindowPadding)99));
    }

    private static ButtonGroup CreateButtonGroup(int count)
    {
        ButtonGroup group = new();

        for (int index = 0; index < count; ++index)
        {
            group.CreateButton(0, 0, CreateSprite(), CreateSprite(), DialogButton.Ok);
        }

        return group;
    }

    private static Sprite CreateSprite()
    {
        PixelBuffer image = new(ButtonsWidth, ButtonsHeight);
        image.Fill(7);

        return new Sprite(image, 0, 0);
    }

    private static Point TopLeftOf(ButtonBase button) => new(button.Area.X, button.Area.Y);

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(Canvas);

        return canvas;
    }

    [Fact]
    public void RenderButton_PlacesTheButtonByItsPadding()
    {
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));
        PixelBuffer target = CreateCanvas();
        ButtonSprite button = new(new Sprite(59, 27), new Sprite(59, 27));

        window.RenderButton(button, target, new Sprite(59, 27), new Sprite(59, 27), new Point(20, 7),
            WindowPadding.BottomRight);

        // Bottom right: 20 pixels off the right edge and 7 off the bottom one.
        Assert.Equal(new Rect(170 + 300 - 59 - 20, 140 + 200 - 27 - 7, 59, 27), button.Area);
    }

    [Fact]
    public void RenderOkayCancelButtons_PutsThemInTheBottomCorners()
    {
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));
        PixelBuffer target = CreateCanvas();
        ButtonSprite okay = new(new Sprite(59, 27), new Sprite(59, 27));
        ButtonSprite cancel = new(new Sprite(59, 27), new Sprite(59, 27));

        window.RenderOkayCancelButtons(okay, cancel, target, new Sprite(59, 27), new Sprite(59, 27),
            new Sprite(59, 27), new Sprite(59, 27));

        Assert.Equal(new Rect(190, 306, 59, 27), okay.Area);
        Assert.Equal(new Rect(391, 306, 59, 27), cancel.Area);
    }

    [Fact]
    public void RenderTextAdaptedButtonSprite_GrowsThePlateAroundTheLabel() => Assert.True(TryWithArchives(() =>
    {
        StandardWindow window = StandardWindow.Create(new Rect(170, 140, 300, 200));
        PixelBuffer target = CreateCanvas();
        ButtonSprite button = new(new Sprite(1, 1), new Sprite(1, 1));

        window.RenderTextAdaptedButtonSprite(button, target, "SET SPELLS", new Point(219, 78),
            WindowPadding.TopCenter);

        // The plate is as wide as the label plus its margins, and the button hangs where the padding says.
        Assert.True(button.Area.Width > button.Area.Height);
        Assert.Equal(170 + ((300 - button.Area.Width) / 2) + 219, button.Area.X);
        Assert.Equal(140 + 78, button.Area.Y);
    }));

    /// <summary>Opens the game data the window artwork is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Runs a drawing test against the real window artwork.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArchives(Action body)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            body();
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }
}
