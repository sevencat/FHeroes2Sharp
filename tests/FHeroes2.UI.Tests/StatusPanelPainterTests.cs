using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The stone panel under the adventure screen buttons, <c>Interface::StatusPanel</c>
/// (<c>src/fheroes2/gui/interface_status.cpp:218-436</c>).
/// </summary>
public sealed class StatusPanelPainterTests
{
    /// <summary>Where the panel of the reference layout starts: under the buttons, at y = 392.</summary>
    private static readonly Point Origin = new(480, 392);

    /// <summary>The panel bay of a 640x480 screen, <c>MapFrame.Panel</c>.</summary>
    private static readonly Rect PanelBay = new(480, 160, StatusPanelPainter.Width, 320);

    /// <summary>Where the eight buttons of the panel bay are, <c>ButtonsPanel.Area</c>.</summary>
    private static readonly Rect ButtonsArea = new(480, 320, StatusPanelPainter.Width, 72);

    [Fact]
    public void DrawBackground_StretchesTheFrameWithItsTopAndBottomBands()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);

        panel.DrawBackground(target, Origin, 88);

        // Top 11 pixels are the top of the artwork, the bottom 61 are glued to the end of the panel and therefore cover
        // the lower part of the single middle band ( interface_status.cpp:411-431, count = ( 88 - 26 ) / 46 = 1 ).
        for (int y = 0; y < StatusPanelPainter.BackgroundHeadHeight; ++y)
        {
            Assert.Equal((byte)(y + 1), target.Row(Origin.Y + y)[Origin.X]);
        }

        int bottomHeight = 72 - StatusPanelPainter.BackgroundHeadHeight;
        int bottomTop = 88 - bottomHeight;

        // The middle band is only visible where the bottom band does not cover it: rows 11 to 26.
        for (int y = StatusPanelPainter.BackgroundHeadHeight; y < bottomTop; ++y)
        {
            Assert.Equal((byte)(y + 1), target.Row(Origin.Y + y)[Origin.X]);
        }

        for (int y = 0; y < bottomHeight; ++y)
        {
            Assert.Equal((byte)(11 + y + 1), target.Row(Origin.Y + bottomTop + y)[Origin.X]);
        }
    }

    [Fact]
    public void DrawBackground_RejectsAPanelTooShortForTheBands()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);

        Assert.Throws<ArgumentOutOfRangeException>(() => panel.DrawBackground(target, Origin, 25));
    }

    [Theory]
    [InlineData(1, 1, 1, 0)]
    [InlineData(1, 2, 1, 2)]
    [InlineData(1, 3, 1, 3)]
    [InlineData(1, 4, 1, 4)]
    [InlineData(1, 5, 1, 1)]
    [InlineData(1, 2, 2, 0)]
    public void DrawDate_ShowsTheMoonPhaseOfTheFirstDayOfTheWeek(int month, int week, int day, int expectedFrame)
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);

        panel.DrawDate(
            target,
            Origin,
            new StatusPanelDate(month, week, day),
            new FakeGlyphSource(200),
            new FakeGlyphSource(201));

        // Every moon frame is a solid block of its own value, so the phase can be read back from the panel.
        Assert.Equal((byte)(10 + expectedFrame), target.Row(Origin.Y + 1)[Origin.X]);
    }

    [Fact]
    public void DrawDate_WritesBothLinesCentredInThePanel()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);

        panel.DrawDate(target, Origin, new StatusPanelDate(1, 2, 3), new FakeGlyphSource(200), new FakeGlyphSource(201));

        // The small font starts at y + 32, the normal one at y + 48. A centred line that starts at column c and is w wide
        // leaves c columns of background on the left and 144 - c - w on the right, so the outer columns of both lines
        // are mirror images of each other: their sum plus one is the width of the panel.
        (int first, int last) = FirstAndLastColumn(target, 32, 200);
        Assert.Equal(144, first + last + 1);

        (first, last) = FirstAndLastColumn(target, 48, 201);
        Assert.Equal(144, first + last + 1);

        // «Month: 1 Week: 2» is sixteen 6 pixel cells wide, so it starts 24 pixels into the panel.
        Assert.Equal(24, FirstAndLastColumn(target, 32, 200).First);
    }

    /// <summary>The leftmost and the rightmost column of the panel that hold <paramref name="color"/> on a row.</summary>
    private static (int First, int Last) FirstAndLastColumn(PixelBuffer target, int offsetY, byte color)
    {
        int first = -1;
        int last = -1;

        for (int x = 0; x < StatusPanelPainter.Width; ++x)
        {
            if (target.Row(Origin.Y + offsetY)[Origin.X + x] != color)
            {
                continue;
            }

            first = first < 0 ? x : first;
            last = x;
        }

        return (first, last);
    }

    [Fact]
    public void DrawKingdom_PutsTheCountsUnderTheirIcons()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);

        var funds = new Funds();
        funds.Gold = 12500;
        funds.Wood = 7;

        panel.DrawKingdom(
            target, Origin, new StatusPanelKingdom(2, 5, funds), new FakeGlyphSource(202));

        // The icon strip is 3 pixels below the top, the first count row 27 below that and the second 30 below that.
        Assert.Equal((byte)30, target.Row(Origin.Y + 3)[Origin.X + 6]);

        int firstRow = Origin.Y + 3 + StatusPanelPainter.KingdomRowStep;
        int secondRow = firstRow + StatusPanelPainter.ResourceRowStep;

        Assert.Equal((byte)202, target.Row(firstRow)[Origin.X + 26]);
        Assert.Equal((byte)202, target.Row(secondRow)[Origin.X + 15]);
    }

    [Fact]
    public void AreaFor_LeavesExactlyOneFrameUnderTheButtons()
    {
        // The reference layout: the buttons end at 392 and the frame border is 16, so the stone gets 480 - 392 - 16.
        Rect area = StatusPanelPainter.AreaFor(PanelBay, ButtonsArea, 480, 16);

        Assert.Equal(ButtonsArea.X, area.X);
        Assert.Equal(ButtonsArea.Bottom, area.Y);
        Assert.Equal(StatusPanelPainter.Width, area.Width);
        Assert.Equal(StatusPanelPainter.SegmentHeight, area.Height);
    }

    [Fact]
    public void Draw_ShowsTheDateInAOneFramePanel()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);
        Rect area = StatusPanelPainter.AreaFor(PanelBay, ButtonsArea, 480, 16);

        panel.Draw(
            target,
            area,
            StatusPanelState.Day,
            new StatusPanelDate(1, 2, 1),
            CreateKingdom(),
            new FakeGlyphSource(200),
            new FakeGlyphSource(201));

        // The stone is the stretched STONBACK, the moon goes one pixel below its top and the date text below that.
        Assert.Equal((byte)1, target.Row(area.Y)[area.X]);
        Assert.Equal((byte)12, target.Row(area.Y + 1)[area.X]);
        Assert.Equal((byte)200, target.Row(area.Y + 32)[area.X + 24]);
        Assert.Equal((byte)201, target.Row(area.Y + 48)[area.X + 54]);

        // The panel is one state tall, so the kingdom strip stays off the screen.
        Assert.NotEqual((byte)30, target.Row(area.Y + 3)[area.X + 6]);
    }

    [Fact]
    public void Draw_ShowsTheKingdomInAOneFramePanel()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);
        Rect area = StatusPanelPainter.AreaFor(PanelBay, ButtonsArea, 480, 16);

        panel.Draw(
            target,
            area,
            StatusPanelState.Funds,
            new StatusPanelDate(1, 2, 1),
            CreateKingdom(),
            new FakeGlyphSource(200),
            new FakeGlyphSource(201));

        // The icon strip sits three pixels below the top, the moon does not, and the last row is the untouched artwork.
        // Column 3 holds no number in either state, so the row of the date line is stone and not text.
        Assert.Equal((byte)30, target.Row(area.Y + 3)[area.X + 6]);
        Assert.Equal((byte)71, target.Row(area.Y + 70)[area.X]);
        Assert.Equal((byte)33, target.Row(area.Y + 32)[area.X + 3]);
    }

    [Fact]
    public void Draw_LeavesTheStoneBareWhenTheGameHasNotStarted()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);
        Rect area = StatusPanelPainter.AreaFor(PanelBay, ButtonsArea, 480, 16);

        panel.Draw(
            target,
            area,
            StatusPanelState.Unknown,
            new StatusPanelDate(1, 1, 1),
            CreateKingdom(),
            new FakeGlyphSource(200),
            new FakeGlyphSource(201));

        for (int y = 0; y < area.Height; ++y)
        {
            Assert.Equal((byte)(y + 1), target.Row(area.Y + y)[area.X]);
        }
    }

    [Fact]
    public void Draw_PutsTheKingdomUnderTheDateInATwoFramePanel()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);
        Rect area = new(Origin.X, Origin.Y, StatusPanelPainter.Width, (StatusPanelPainter.SegmentHeight * 2) + 15);

        panel.Draw(
            target,
            area,
            StatusPanelState.Funds,
            new StatusPanelDate(1, 2, 1),
            CreateKingdom(),
            new FakeGlyphSource(200),
            new FakeGlyphSource(201));

        // The date owns the first frame and the kingdom starts 5 pixels into the second, so its strip is at 72 + 5 + 3.
        Assert.Equal((byte)12, target.Row(area.Y + 1)[area.X]);
        Assert.Equal((byte)30, target.Row(area.Y + StatusPanelPainter.SegmentHeight + 8)[area.X + 6]);
    }

    [Fact]
    public void Draw_AbbreviatesTheResourcesButNotTheGold()
    {
        StatusPanelPainter panel = CreatePanel();
        PixelBuffer target = new(640, 720);
        Rect area = StatusPanelPainter.AreaFor(PanelBay, ButtonsArea, 480, 16);

        var funds = new Funds();
        funds.Gold = 12500;
        funds.Wood = 12500;
        funds.Ore = 999;

        panel.Draw(
            target,
            area,
            StatusPanelState.Funds,
            new StatusPanelDate(1, 2, 1),
            new StatusPanelKingdom(0, 0, funds),
            new FakeGlyphSource(202),
            new FakeGlyphSource(203));

        // 12500 wood is «12K» — three 6 pixel cells — while 12500 gold is written out in full; a five digit pile is 30
        // pixels wide and is the reason the resource columns are 22 pixels apart: the number is centred, not left aligned.
        int goldRow = Origin.Y + 3 + StatusPanelPainter.KingdomRowStep;

        Assert.Equal(30, GlyphWidth(target, area, goldRow, 95, 144));

        int resourceRow = goldRow + StatusPanelPainter.ResourceRowStep;

        Assert.Equal(18, GlyphWidth(target, area, resourceRow, 0, 32));
    }

    /// <summary>The width the glyphs of <paramref name="row"/> cover between two columns of the panel.</summary>
    private static int GlyphWidth(PixelBuffer target, Rect area, int row, int from, int to)
    {
        int first = -1;
        int last = -1;

        for (int x = from; x < to; ++x)
        {
            if (target.Row(row)[area.X + x] != 202)
            {
                continue;
            }

            first = first < 0 ? x : first;
            last = x;
        }

        return last - first + 1;
    }

    /// <summary>A kingdom of two castles, five towns and a pile of every resource below four digits.</summary>
    private static StatusPanelKingdom CreateKingdom()
    {
        var funds = new Funds();
        funds.Gold = 12500;
        funds.Wood = 7;

        return new StatusPanelKingdom(2, 5, funds);
    }

    /// <summary>
    /// A panel whose background is a value ramp, the moon five solid blocks and the resource strip a solid 30x17 block,
    /// so every position can be checked by reading a single pixel.
    /// </summary>
    private static StatusPanelPainter CreatePanel()
    {
        Sprite background = new(144, 72);

        for (int y = 0; y < background.Height; ++y)
        {
            background.Image.Row(y).Fill((byte)(y + 1));
        }

        List<Sprite> sunMoon = new(5);

        for (int frame = 0; frame < 5; ++frame)
        {
            Sprite moon = new(144, 56);
            moon.Image.Fill((byte)(10 + frame));
            sunMoon.Add(moon);
        }

        Sprite smallResources = new(30, 17);
        smallResources.Image.Fill(30);

        return new StatusPanelPainter(background, sunMoon, smallResources);
    }

    /// <summary>Solid 6x15 blocks, the metrics of <c>FONT.ICN</c>.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
        {
            PixelBuffer image = new(6, LineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.SpaceCharacter ? SpaceWidth : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
