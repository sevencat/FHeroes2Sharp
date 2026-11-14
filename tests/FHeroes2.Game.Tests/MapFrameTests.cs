using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapFrame"/>: the layout the reference gives a 640x480 screen
/// (<c>Interface::EditorInterface::reset</c> and <c>fheroes2::borderWidthPx</c>) and the flat bays the
/// frame draws when <c>ADVBORD.ICN</c> is not available.
/// </summary>
public sealed class MapFrameTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TheMapSitsBetweenTheBorderAndTheRadarColumn()
    {
        MapFrame frame = new(Screen, null);

        Assert.Equal(new Rect(16, 16, 448, 448), frame.Viewport);
    }

    [Fact]
    public void TheRadarSitsInTheTopRightCorner()
    {
        MapFrame frame = new(Screen, null);

        Assert.Equal(new Rect(480, 16, 144, 144), frame.Radar);
    }

    [Fact]
    public void ThePanelFillsTheColumnBelowTheRadar()
    {
        MapFrame frame = new(Screen, null);

        // From the bottom of the radar to the frame edge and not to the border: the system buttons of
        // the reference sit on the last rows of the display.
        Assert.Equal(new Rect(480, 160, 144, 320), frame.Panel);
        Assert.Equal(Screen.Height, frame.Panel.Bottom);
    }

    [Fact]
    public void TheBottomBorderKeepsAStripForTheStatusLine()
    {
        MapFrame frame = new(Screen, null);

        Assert.Equal(new Rect(16, 464, 608, 16), frame.BottomBorder);
    }

    [Fact]
    public void ASmallerFrameKeepsTheSameInsets()
    {
        MapFrame frame = new(new Size(320, 240), null);

        Assert.Equal(new Rect(16, 16, 128, 208), frame.Viewport);
        Assert.Equal(new Rect(160, 16, 144, 144), frame.Radar);
        Assert.Equal(new Rect(160, 160, 144, 80), frame.Panel);
        Assert.Equal(new Rect(16, 224, 288, 16), frame.BottomBorder);
    }

    [Fact]
    public void TheFlatFallbackFillsTheFrameAndEveryBay()
    {
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(0xFF);

        MapFrame frame = new(Screen, null);
        frame.Draw(pixels, 7);

        Assert.False(frame.HasChrome);
        Assert.Equal(7, pixels[0, 0]);
        Assert.Equal(7, pixels[Screen.Width - 1, Screen.Height - 1]);
        Assert.Equal(7, pixels[frame.Viewport.X + 1, frame.Viewport.Y + 1]);
        Assert.Equal(7, pixels[frame.Radar.X + 1, frame.Radar.Y + 1]);
        Assert.Equal(7, pixels[frame.Panel.X + 1, frame.Panel.Y + 1]);
    }

    [Fact]
    public void ThePanelPaintingIsANoOpWithoutTheArtwork()
    {
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        MapFrame frame = new(Screen, null);
        frame.DrawPanel(pixels);

        Assert.Equal(3, pixels[frame.Panel.X + 1, frame.Panel.Y + 1]);
        Assert.Equal(3, pixels[frame.Panel.X + 1, frame.Panel.Bottom - 1]);
    }
}
