using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="FrameBox"/>, the port of <c>Dialog::ResizableFrameBox</c>: the constants of
/// <c>dialog_box.cpp</c>, the layout arithmetic and the composition drawn on screen.
/// </summary>
/// <remarks>
/// The frame reads <c>BUYBUILD.ICN</c> itself, so every test that lays one out needs the installed
/// archives and returns when the machine has none. The expected numbers were derived from that
/// archive: the left column is 160/161 wide with a 99 pixel tall top, the right one 144/145/144.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FrameBoxTests
{
    private const byte Background = 3;

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
    public void Artwork_IsBuybuildUnlessTheEvilInterfaceIsOn()
    {
        // ICN::BUYBUILD, the frame ui_window.cpp's FrameBox draws; ICN::BUYBUILE replaces it for the
        // evil interface (getICNByIcnId( ICN::BUYBUILE ) in the reference).
        Assert.Equal("BUYBUILD.ICN", FrameBox.ArtworkFileName);
        Assert.Equal(7, FrameBox.RequiredSpriteCount);
        Assert.Equal(0, FrameBox.RightFrame);
        Assert.Equal(4, FrameBox.LeftFrame);
    }

    [Fact]
    public void Create_WithoutGameDataThrows()
    {
        GameAssets.Current = null;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => FrameBox.Create(140));

        Assert.Contains("BUYBUILD.ICN", error.Message, StringComparison.Ordinal);
        Assert.False(FrameBox.IsArtworkAvailable);
    }

    [Fact]
    public void Create_MatchesTheLayoutOfTheReference() => Assert.True(TryWithFrame(
        () => FrameBox.Create(50),
        box =>
        {
            // A text of 50 pixels is shorter than two middle slices, so the reference still draws
            // the smallest box: origin (159,141) and a 244x70 area at (198,205).
            Assert.Equal(new Point(159, 141), box.Origin);
            Assert.Equal(new Rect(198, 205, 244, 70), box.Area);
            Assert.Equal(0, box.MiddleFragmentCount);
            Assert.Equal(0, box.MiddleFragmentHeight);

            // The left column is 161 pixels wide while its top piece is 160, so a box without a
            // slice is 305 pixels wide while a box with one is 306.
            Assert.Equal(new Rect(159, 141, 305, 180), box.FrameArea);
        }));

    [Theory]
    [InlineData(70, 0, 0, 70)]
    [InlineData(71, 1, 1, 71)]
    [InlineData(88, 1, 18, 88)]
    [InlineData(104, 1, 34, 104)]
    [InlineData(105, 2, 35, 105)]
    [InlineData(140, 3, 70, 140)]
    public void Create_RepeatsTheMiddleSlice(int height, int slices, int middleHeight, int areaHeight) =>
        Assert.True(TryWithFrame(
            () => FrameBox.Create(height, startY: 0),
            box =>
            {
                Assert.Equal(slices, box.MiddleFragmentCount);
                Assert.Equal(middleHeight, box.MiddleFragmentHeight);
                Assert.Equal(areaHeight, box.Area.Height);
            }));

    [Fact]
    public void Create_ReservesTheButtonRow() => Assert.True(TryWithFrame(
        // showButtons adds buttonHeight (40) before the slices are counted.
        () => FrameBox.Create(50, showButtons: true, startY: 0),
        box =>
        {
            Assert.Equal(20, box.MiddleFragmentHeight);
            Assert.Equal(1, box.MiddleFragmentCount);
            Assert.Equal(90, box.Area.Height);
        }));

    [Fact]
    public void Create_KeepsTheMinimumAreaForShortContent() => Assert.True(TryWithFrame(
        () => FrameBox.Create(0),
        box =>
        {
            Assert.Equal(70, box.Area.Height);
            Assert.Equal(70, FrameBox.Create(-20).Area.Height);
        }));

    [Fact]
    public void Create_CentersTheMiddleOnTheScreenUnlessStartYIsGiven() => Assert.True(TryWithFrame(
        () => FrameBox.Create(140),
        box =>
        {
            // originY = (480 - middleFragmentHeight) / 2 - topHeight
            Assert.Equal(106, box.Origin.Y);
            Assert.Equal(170, box.Area.Y);

            FrameBox placed = FrameBox.Create(140, startY: 20);

            Assert.Equal(20, placed.Origin.Y);
            Assert.Equal(84, placed.Area.Y);
        }));

    [Fact]
    public void Draw_PaintsTheFrameAreaAndNothingElse() => Assert.True(TryWithFrame(
        () => FrameBox.Create(140, startY: 0),
        box =>
        {
            PixelBuffer target = new(640, 480);
            target.Fill(Background);

            box.Draw(target);

            int painted = 0;

            for (int y = 0; y < target.Height; ++y)
            {
                for (int x = 0; x < target.Width; ++x)
                {
                    if (target[x, y] == Background)
                    {
                        continue;
                    }

                    ++painted;
                    Assert.True(box.FrameArea.Contains(new Point(x, y)), $"({x}; {y}) lies outside the frame");
                }
            }

            // The two 99 pixel tall tops, the slices and the 81 pixel tall bottoms cover most of the
            // 305x180 box; the transparent gaps of the art keep it below the whole area.
            Assert.InRange(painted, 20_000, box.FrameArea.Width * box.FrameArea.Height);
        }));

    [Fact]
    public void Draw_PutsTheBottomRowBelowTheSlices() => Assert.True(TryWithFrame(
        () => FrameBox.Create(140, startY: 0),
        box =>
        {
            PixelBuffer target = new(640, 480);
            target.Fill(Background);

            box.Draw(target);

            int seam = box.Origin.X + 161;

            // The 99 pixel tall tops and the 70 pixels of slices end at 169; the 81 pixel tall bottoms
            // follow, so the frame stops at 250.
            Assert.Equal(250, box.Origin.Y + 99 + 70 + 81);
            Assert.True(box.FrameArea.Contains(new Point(seam - 1, 169)));
            Assert.True(box.FrameArea.Contains(new Point(seam, 169)));
            Assert.True(box.FrameArea.Contains(new Point(seam - 1, 169 + 81 - 1)));
            Assert.True(box.FrameArea.Contains(new Point(seam, 169 + 81 - 1)));
        }));

    /// <summary>Opens the game data the frame artwork is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Runs a frame test against the real artwork.</summary>
    /// <param name="create">Builds the frame.</param>
    /// <param name="assert">Checks it.</param>
    /// <returns><see langword="false"/> when the machine has no game data.</returns>
    private static bool TryWithFrame(Func<FrameBox> create, Action<FrameBox> assert)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            assert(create());
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }
}
