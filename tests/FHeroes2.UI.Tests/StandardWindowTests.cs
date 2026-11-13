using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

public sealed class StandardWindowTests
{
    private const byte Canvas = 3;
    private const byte Vertical = 20;
    private const byte Horizontal = 30;
    private const byte Interior = 40;

    // Sizes of the window artwork as read from heroes2.agg.
    private static readonly Size VerticalSize = new(319, 456);
    private static readonly Size HorizontalSize = new(476, 215);
    private static readonly Size BackgroundSize = new(640, 480);

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
        StandardWindow window = StandardWindow.Create(CreateChrome(), new Rect(170, 140, 300, 200));

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
        StandardWindow window = StandardWindow.CreateCentered(CreateChrome(), new Size(300, 200));

        Assert.Equal(new Rect(170, 140, 300, 200), window.ActiveArea);

        // An odd difference leaves the extra pixel on the right and the bottom side.
        StandardWindow odd = StandardWindow.CreateCentered(CreateChrome(), new Size(301, 201));
        Assert.Equal(new Rect(169, 139, 301, 201), odd.ActiveArea);
    }

    [Fact]
    public void Chrome_TakesTheFirstFrameOfEachArchive()
    {
        StandardWindowChrome? chrome = StandardWindowChrome.TryCreate(
            CreateSprites(HorizontalSize, Horizontal),
            CreateSprites(VerticalSize, Vertical),
            CreateSprites(BackgroundSize, Interior));

        Assert.NotNull(chrome);
        Assert.Equal(HorizontalSize, new Size(chrome.Horizontal.Width, chrome.Horizontal.Height));
        Assert.Equal(VerticalSize, new Size(chrome.Vertical.Width, chrome.Vertical.Height));
        Assert.Equal(BackgroundSize, new Size(chrome.Background.Width, chrome.Background.Height));

        // An empty archive cannot provide a piece, so the caller has to draw without a frame.
        Assert.Null(StandardWindowChrome.TryCreate(null, CreateSprites(VerticalSize, Vertical), CreateSprites(BackgroundSize, Interior)));
        Assert.Null(StandardWindowChrome.TryCreate(CreateSprites(HorizontalSize, Horizontal), null, CreateSprites(BackgroundSize, Interior)));
        Assert.Null(StandardWindowChrome.TryCreate(CreateSprites(HorizontalSize, Horizontal), CreateSprites(VerticalSize, Vertical), []));
    }

    [Fact]
    public void Draw_PaintsCornersBordersAndTheTiledInterior()
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(CreateChrome(), new Rect(170, 140, 300, 200));
        Rect frame = window.WindowArea;

        window.Draw(target);

        // The four corners and both vertical borders come from WINLOSE.ICN ...
        Assert.Equal(Vertical, target[frame.X + 5, frame.Y + 5]);
        Assert.Equal(Vertical, target[frame.Right - 5, frame.Y + 5]);
        Assert.Equal(Vertical, target[frame.X + 5, frame.Bottom - 5]);
        Assert.Equal(Vertical, target[frame.Right - 5, frame.Bottom - 5]);
        Assert.Equal(Vertical, target[frame.X + 5, frame.Y + (frame.Height / 2)]);
        Assert.Equal(Vertical, target[frame.Right - 5, frame.Y + (frame.Height / 2)]);

        // ... the top and the bottom border from SURDRBKG.ICN ...
        Assert.Equal(Horizontal, target[frame.X + (frame.Width / 2), frame.Y + 5]);
        Assert.Equal(Horizontal, target[frame.X + (frame.Width / 2), frame.Bottom - 20]);

        // ... and the interior is the STONEBAK.ICN tile, offset by the background margin. The first
        // pixels of the tile belong to the dithered seam of the corner, so the check sits deeper.
        Assert.Equal(Interior, target[frame.X + StandardWindow.BorderEdgeOffset + 5, frame.Y + StandardWindow.BorderEdgeOffset + 5]);
        Assert.Equal(Interior, target[frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2)]);
        Assert.Equal(Canvas, target[frame.X - 1, frame.Y - 1]);
    }

    [Fact]
    public void Draw_MixesBorderAndInteriorInTheCornerSeam()
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(CreateChrome(), new Rect(170, 140, 300, 200));
        Rect frame = window.WindowArea;

        window.Draw(target);

        // A dithered transition, not a hard edge: the 10 pixel band below the top left corner holds
        // pixels of the border as well as of the interior.
        int startX = frame.X + StandardWindow.BackgroundOffset;
        int startY = frame.Y + StandardWindow.BackgroundOffset;
        int borderPixels = 0;
        int interiorPixels = 0;

        for (int y = startY; y < startY + StandardWindow.TransitionSize; ++y)
        {
            for (int x = startX; x < frame.X + StandardWindow.BorderEdgeOffset; ++x)
            {
                borderPixels += target[x, y] == Vertical ? 1 : 0;
                interiorPixels += target[x, y] == Interior ? 1 : 0;
            }
        }

        Assert.True(borderPixels > 0, "the seam should keep pixels of the border");
        Assert.True(interiorPixels > 0, "the seam should keep pixels of the interior");
    }

    [Fact]
    public void Draw_WithoutBackground_LeavesTheInteriorUntouched()
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(CreateChrome(), new Rect(170, 140, 300, 200), renderBackground: false);
        Rect frame = window.WindowArea;

        window.Draw(target);

        Assert.False(window.HasBackground);

        // The corner pieces grow by the background margin, so the frame is still solid ...
        Assert.Equal(Vertical, target[frame.X + 5, frame.Y + 5]);
        Assert.Equal(Vertical, target[frame.X + 30, frame.Y + 5]);
        Assert.Equal(Vertical, target[frame.X + 5, frame.Y + 30]);

        // ... but nothing is tiled inside it.
        Assert.Equal(Canvas, target[frame.X + 30, frame.Y + 30]);
        Assert.Equal(Canvas, target[frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2)]);
    }

    [Fact]
    public void Draw_ClipsAWindowThatStartsOutsideTheTarget()
    {
        PixelBuffer target = CreateCanvas();
        StandardWindow window = StandardWindow.Create(CreateChrome(), new Rect(0, 0, 700, 460));

        window.Draw(target);

        // The frame starts at (-16,-16), so the left border is cut off but the interior is still
        // tiled into the visible part of the target.
        Assert.Equal(Vertical, target[5, 5]);
        Assert.Equal(Interior, target[350, 200]);
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        StandardWindow window = StandardWindow.Create(CreateChrome(), new Rect(170, 140, 300, 200));

        Assert.Throws<ArgumentNullException>(() => window.Draw(null!));
    }

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(Canvas);

        return canvas;
    }

    private static StandardWindowChrome CreateChrome() => StandardWindowChrome.TryCreate(
        CreateSprites(HorizontalSize, Horizontal),
        CreateSprites(VerticalSize, Vertical),
        CreateSprites(BackgroundSize, Interior))!;

    private static Sprite[] CreateSprites(Size size, byte color)
    {
        PixelBuffer image = new(size.Width, size.Height);
        image.Fill(color);

        return [new Sprite(image, 0, 0)];
    }
}
