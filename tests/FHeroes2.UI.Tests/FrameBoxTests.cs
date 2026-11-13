using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

public sealed class FrameBoxTests
{
    private const byte Background = 3;
    private const byte LeftTop = 10;
    private const byte RightTop = 20;
    private const byte LeftBottom = 30;
    private const byte RightBottom = 40;

    // Sizes of BUYBUILD.ICN as read from heroes2.agg: right column 0/1/2, left column 4/5/6.
    private static readonly Size[] PieceSizes =
    [
        new(144, 99), new(145, 45), new(144, 81), new(251, 20),
        new(160, 99), new(161, 45), new(161, 81),
    ];

    [Fact]
    public void Constants_MatchTheReferenceEngine()
    {
        // fheroes2::boxAreaWidthPx / borderWidthPx, offsetFromBorder, activeAreaHeight and
        // buttonHeight of dialog_box.cpp.
        Assert.Equal(244, FrameBox.AreaWidth);
        Assert.Equal(16, FrameBox.BorderWidth);
        Assert.Equal(6, FrameBox.CornerOffset);
        Assert.Equal(288, FrameBox.WindowWidth);
        Assert.Equal(35, FrameBox.ActiveAreaHeight);
        Assert.Equal(40, FrameBox.ButtonAreaHeight);
        Assert.Equal(10, FrameBox.MiddleSourceOffsetY);
        Assert.Equal(70, FrameBox.MinimumAreaHeight);
    }

    [Fact]
    public void Chrome_TakesTheSixColumnsOutOfTheTable()
    {
        FrameBoxChrome? chrome = FrameBoxChrome.TryCreate(CreatePieces());

        Assert.NotNull(chrome);
        Assert.Equal(161, chrome.LeftWidth);
        Assert.Equal(145, chrome.RightWidth);
        Assert.Equal(99, chrome.TopHeight);
        Assert.Equal(81, chrome.BottomHeight);

        // A table without a left column cannot build a frame.
        Assert.Null(FrameBoxChrome.TryCreate(null));
        Assert.Null(FrameBoxChrome.TryCreate(new[] { CreatePiece(PieceSizes[0], RightTop) }));
    }

    [Fact]
    public void Create_MatchesTheLayoutOfTheReference()
    {
        // A text of 50 pixels is shorter than two middle slices, so the reference still draws the
        // smallest box: origin (159,141) and a 244x70 area at (198,205).
        FrameBox box = FrameBox.Create(CreateChrome(), 50);

        Assert.Equal(new Point(159, 141), box.Origin);
        Assert.Equal(new Rect(198, 205, 244, 70), box.Area);
        Assert.Equal(0, box.MiddleFragmentCount);
        Assert.Equal(0, box.MiddleFragmentHeight);

        // The middle slices are one pixel wider than the top and the bottom of their column, so a
        // box without a slice is 305 pixels wide while a box with one is 306.
        Assert.Equal(new Rect(159, 141, 305, 180), box.FrameArea);
    }

    [Theory]
    [InlineData(70, 0, 0, 70)]
    [InlineData(71, 1, 1, 71)]
    [InlineData(88, 1, 18, 88)]
    [InlineData(104, 1, 34, 104)]
    [InlineData(105, 2, 35, 105)]
    [InlineData(140, 3, 70, 140)]
    public void Create_RepeatsTheMiddleSlice(int height, int slices, int middleHeight, int areaHeight)
    {
        FrameBox box = FrameBox.Create(CreateChrome(), height, startY: 0);

        Assert.Equal(slices, box.MiddleFragmentCount);
        Assert.Equal(middleHeight, box.MiddleFragmentHeight);
        Assert.Equal(areaHeight, box.Area.Height);
    }

    [Fact]
    public void Create_ReservesTheButtonRow()
    {
        // showButtons adds buttonHeight (40) before the slices are counted.
        FrameBox box = FrameBox.Create(CreateChrome(), 50, showButtons: true, startY: 0);

        Assert.Equal(20, box.MiddleFragmentHeight);
        Assert.Equal(1, box.MiddleFragmentCount);
        Assert.Equal(90, box.Area.Height);
    }

    [Fact]
    public void Create_KeepsTheMinimumAreaForShortContent()
    {
        Assert.Equal(70, FrameBox.Create(CreateChrome(), 0).Area.Height);
        Assert.Equal(70, FrameBox.Create(CreateChrome(), -20).Area.Height);
    }

    [Fact]
    public void Create_CentersTheMiddleOnTheScreenUnlessStartYIsGiven()
    {
        // originY = (480 - middleFragmentHeight) / 2 - topHeight
        Assert.Equal(106, FrameBox.Create(CreateChrome(), 140).Origin.Y);
        Assert.Equal(170, FrameBox.Create(CreateChrome(), 140).Area.Y);

        FrameBox placed = FrameBox.Create(CreateChrome(), 140, startY: 20);

        Assert.Equal(20, placed.Origin.Y);
        Assert.Equal(84, placed.Area.Y);
    }

    [Fact]
    public void Draw_PlacesEveryPiece()
    {
        PixelBuffer target = new(640, 480);
        target.Fill(Background);

        FrameBox box = FrameBox.Create(CreateChrome(), 140, startY: 0);
        box.Draw(target);

        int seam = box.Origin.X + 161;

        // Top row: the left column is right aligned to the seam, the right one starts on it.
        Assert.Equal(LeftTop, target[seam - 1, 0]);
        Assert.Equal(RightTop, target[seam, 0]);

        // The slices start below the 99 pixel tall top and copy from row 10 of the source, so the
        // first row of a slice is source row 10 and its last one source row 44. The left column is
        // the coloured one here, the right column is a flat sprite.
        Assert.Equal(MiddleRowColor(10), target[seam - 1, 99]);
        Assert.Equal(MiddleRowColor(44), target[seam - 1, 99 + 34]);
        Assert.Equal(MiddleRowColor(10), target[seam - 1, 99 + 35]);

        // The bottom row sits after the two slices.
        Assert.Equal(LeftBottom, target[seam - 1, 169]);
        Assert.Equal(RightBottom, target[seam, 169]);
    }

    [Fact]
    public void Draw_RespectsTheSpriteTransformLayer()
    {
        // The ICN transform layer expresses "keep the destination" as id 1; a frame piece may use
        // it, so the blit must not paint those pixels.
        PixelBuffer image = new(161, 45);
        image.Fill(MiddleRowColor(10));

        byte[] transforms = new byte[161 * 45];
        transforms[FrameBox.MiddleSourceOffsetY * 161] = 1;

        Sprite leftMiddle = new(image, 0, 0, transforms);
        FrameBoxChrome chrome = CreateChrome(leftMiddle);

        PixelBuffer target = new(640, 480);
        target.Fill(Background);

        FrameBox box = FrameBox.Create(chrome, 140, startY: 0);
        box.Draw(target);

        int seam = box.Origin.X + 161;

        Assert.Equal(Background, target[seam - 161, 99]);
        Assert.Equal(MiddleRowColor(10), target[seam - 160, 99]);
    }

    private static byte MiddleRowColor(int row) => (byte)(100 + row);

    private static FrameBoxChrome CreateChrome() => CreateChrome(CreateMiddleSprite());

    private static FrameBoxChrome CreateChrome(Sprite leftMiddle)
    {
        Sprite[] pieces = CreatePieces();
        pieces[FrameBoxChrome.LeftFrame + 1] = leftMiddle;

        return FrameBoxChrome.TryCreate(pieces)!;
    }

    private static Sprite[] CreatePieces()
    {
        Sprite[] pieces = new Sprite[PieceSizes.Length];
        for (int index = 0; index < pieces.Length; ++index)
        {
            pieces[index] = CreatePiece(PieceSizes[index], (byte)(index + 1));
        }

        pieces[FrameBoxChrome.LeftFrame] = CreatePiece(PieceSizes[FrameBoxChrome.LeftFrame], LeftTop);
        pieces[FrameBoxChrome.RightFrame] = CreatePiece(PieceSizes[FrameBoxChrome.RightFrame], RightTop);
        pieces[FrameBoxChrome.LeftFrame + 2] = CreatePiece(PieceSizes[FrameBoxChrome.LeftFrame + 2], LeftBottom);
        pieces[FrameBoxChrome.RightFrame + 2] = CreatePiece(PieceSizes[FrameBoxChrome.RightFrame + 2], RightBottom);
        pieces[FrameBoxChrome.LeftFrame + 1] = CreateMiddleSprite();

        return pieces;
    }

    /// <summary>Middle sprite whose every row carries its own colour, so a slice can be traced back.</summary>
    private static Sprite CreateMiddleSprite()
    {
        PixelBuffer image = new(161, 45);
        for (int y = 0; y < image.Height; ++y)
        {
            image.Row(y).Fill(MiddleRowColor(y));
        }

        return new Sprite(image, 0, 0);
    }

    private static Sprite CreatePiece(Size size, byte color)
    {
        PixelBuffer image = new(size.Width, size.Height);
        image.Fill(color);

        return new Sprite(image, 0, 0);
    }
}
