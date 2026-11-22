using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The zoom tables and the per-level visible rectangles of the View World window,
/// <c>view_world.cpp:72-187</c> and <c>ViewWorld::ZoomROIs</c> of <c>view_world.cpp:508-587</c>.
/// </summary>
public sealed class ViewWorldZoomTests
{
    private static readonly Rect Window = new(0, 0, 640, 480);

    [Theory]
    [InlineData(ZoomLevel.ZoomLevel0, 4, -1560, -920, 5120, 3840)]
    [InlineData(ZoomLevel.ZoomLevel1, 6, -706, -280, 3413, 2560)]
    [InlineData(ZoomLevel.ZoomLevel2, 12, 147, 360, 1706, 1280)]
    [InlineData(ZoomLevel.ZoomLevel3, 32, 680, 760, 640, 480)]
    public void ComputeRoi_TruncatesLikeTheReference(ZoomLevel level, int tileSize, int x, int y, int width, int height)
    {
        Assert.Equal(tileSize, ViewWorldZoomLevels.TileSize(level));

        Rect roi = ViewWorldZoomLevels.ComputeRoi(new Point(1000, 1000), level, Window);

        Assert.Equal(new Rect(x, y, width, height), roi);
    }

    [Fact]
    public void ImageSets_FollowTheZoomLevel()
    {
        Assert.Equal(IcnType.Misc4, ViewWorldZoomLevels.TerrainIcn(ZoomLevel.ZoomLevel0));
        Assert.Equal(IcnType.Misc6, ViewWorldZoomLevels.TerrainIcn(ZoomLevel.ZoomLevel1));
        Assert.Equal(IcnType.Misc12, ViewWorldZoomLevels.TerrainIcn(ZoomLevel.ZoomLevel2));
        Assert.Equal(IcnType.Misc12, ViewWorldZoomLevels.TerrainIcn(ZoomLevel.ZoomLevel3));

        Assert.Equal(IcnType.Letter4, ViewWorldZoomLevels.LetterIcn(ZoomLevel.ZoomLevel0));
        Assert.Equal(IcnType.Letter12, ViewWorldZoomLevels.LetterIcn(ZoomLevel.ZoomLevel3));

        Assert.Equal(IcnType.Vwflag4, ViewWorldZoomLevels.FlagIcn(ZoomLevel.ZoomLevel0));
        Assert.Equal(IcnType.Vwflag6, ViewWorldZoomLevels.FlagIcn(ZoomLevel.ZoomLevel1));
        Assert.Equal(IcnType.Vwflag12, ViewWorldZoomLevels.FlagIcn(ZoomLevel.ZoomLevel2));
    }

    [Fact]
    public void Constructor_ClampsTheCentreIntoTheMap()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel3, new Point(0, 0), Window, ViewWorldZoomLevels.TotalZoomLevels, 144, 144);

        // At 1:1 the window is 640x480 of 4608x4608 map pixels, so the centre sits half a window in.
        Assert.Equal(new Point(320, 240), view.Center);
        Assert.Equal(new Rect(0, 0, 640, 480), view.RoiInPixels);
    }

    [Fact]
    public void Constructor_PinsTheCentreOnAMapSmallerThanTheWindow()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel3, new Point(1000, 1000), Window, ViewWorldZoomLevels.TotalZoomLevels, 4, 4);

        // ( 128 - 1 ) / 2, not 64: the reference shaves half a pixel off even-sized maps.
        Assert.Equal(new Point(63, 63), view.Center);
        Assert.Equal(new Rect(-257, -177, 640, 480), view.RoiInPixels);
    }

    [Fact]
    public void RoiInTiles_RoundsToTheNearestTile()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel3, new Point(1000, 1000), Window, ViewWorldZoomLevels.TotalZoomLevels, 144, 144);

        Assert.Equal(new Point(680, 760), new Point(view.RoiInPixels.X, view.RoiInPixels.Y));
        Assert.Equal(new Rect(21, 24, 20, 15), view.RoiInTiles);
    }

    [Fact]
    public void ZoomIn_ChangesTheLevelAndTheRectangle()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel0, new Point(2000, 2000), Window, ViewWorldZoomLevels.RegularZoomLevels, 144, 144);

        Assert.True(view.ZoomIn());

        Assert.Equal(ZoomLevel.ZoomLevel1, view.ZoomLevel);
        Assert.Equal(3413, view.RoiInPixels.Width);
        Assert.Equal(2560, view.RoiInPixels.Height);

        Assert.True(view.ZoomIn());
        Assert.Equal(ZoomLevel.ZoomLevel2, view.ZoomLevel);

        // Without cycling the last level stays where it is.
        Assert.False(view.ZoomIn());
        Assert.Equal(ZoomLevel.ZoomLevel2, view.ZoomLevel);

        Assert.True(view.ZoomIn(true));
        Assert.Equal(ZoomLevel.ZoomLevel0, view.ZoomLevel);
    }

    [Fact]
    public void ZoomOut_StopsAtTheFurthestLevel()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel2, new Point(2000, 2000), Window, ViewWorldZoomLevels.RegularZoomLevels, 144, 144);

        Assert.True(view.ZoomOut());
        Assert.Equal(ZoomLevel.ZoomLevel1, view.ZoomLevel);

        Assert.True(view.ZoomOut());
        Assert.Equal(ZoomLevel.ZoomLevel0, view.ZoomLevel);

        Assert.False(view.ZoomOut());
        Assert.Equal(ZoomLevel.ZoomLevel0, view.ZoomLevel);

        Assert.True(view.ZoomOut(true));
        Assert.Equal(ZoomLevel.ZoomLevel2, view.ZoomLevel);
    }

    [Fact]
    public void ZoomOut_ReclampsTheCentreWithTheNewRectangle()
    {
        // At 1:1 the map is bigger than the window, so the centre may sit in the corner.
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel3, new Point(0, 0), Window, ViewWorldZoomLevels.TotalZoomLevels, 144, 144);

        Assert.Equal(new Point(320, 240), view.Center);

        // One level out the window is 1706x1280, so the corner is no longer reachable.
        view.ZoomOut();

        Assert.Equal(ZoomLevel.ZoomLevel2, view.ZoomLevel);
        Assert.Equal(new Point(853, 640), view.Center);
        Assert.Equal(new Rect(0, 0, 1706, 1280), view.RoiInPixels);
    }

    [Fact]
    public void Zoom_KeepsAPinnedCentreOnAMapSmallerThanEveryWindow()
    {
        // A map of 5x5 tiles is smaller than the window at 1:1, so the centre is pinned to ( 160 - 1 ) / 2.
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel0, new Point(0, 0), Window, ViewWorldZoomLevels.RegularZoomLevels, 5, 5);

        Assert.Equal(new Point(79, 79), view.Center);

        view.ZoomIn();
        Assert.Equal(new Point(79, 79), view.Center);

        view.ZoomOut();
        Assert.Equal(new Point(79, 79), view.Center);
    }

    [Fact]
    public void ChangeCenter_ClampsAndReportsTheMove()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel3, new Point(1000, 1000), Window, ViewWorldZoomLevels.TotalZoomLevels, 144, 144);

        Assert.False(view.ChangeCenter(new Point(1000, 1000)));

        // Half a window of 640x480 keeps 320 pixels on the left and 240 on the top.
        Assert.True(view.ChangeCenter(new Point(10000, 10000)));
        Assert.Equal(new Point(4288, 4368), view.Center);
        Assert.Equal(new Rect(3968, 4128, 640, 480), view.RoiInPixels);

        Assert.True(view.ChangeCenter(new Point(-500, -500)));
        Assert.Equal(new Point(320, 240), view.Center);
        Assert.Equal(new Rect(0, 0, 640, 480), view.RoiInPixels);
    }

    [Theory]
    [InlineData(PlayerColor.Blue, 0u)]
    [InlineData(PlayerColor.Green, 1u)]
    [InlineData(PlayerColor.Red, 2u)]
    [InlineData(PlayerColor.Yellow, 3u)]
    [InlineData(PlayerColor.Orange, 4u)]
    [InlineData(PlayerColor.Purple, 5u)]
    [InlineData(PlayerColor.None, 6u)]
    [InlineData(PlayerColor.Unused, 6u)]
    public void ColorToOffsetIcn_MapsTheSevenThumbnails(PlayerColor color, uint expected)
    {
        Assert.Equal(expected, ViewWorldZoomLevels.ColorToOffsetIcn(color));
    }

    [Fact]
    public void ColorToOffsetIcn_RejectsCombinations()
    {
        Assert.Equal(ViewWorldZoomLevels.UnknownIndex, ViewWorldZoomLevels.ColorToOffsetIcn(PlayerColor.Blue | PlayerColor.Green));

        // 0x40 是玩家颜色区里没有定义的一位（参考的 UNUSED 是 0x80，端口已补）。
        Assert.Equal(ViewWorldZoomLevels.UnknownIndex, ViewWorldZoomLevels.ColorToOffsetIcn((PlayerColor)0x40));
    }

    [Theory]
    [InlineData(Resource.Wood, 0u)]
    [InlineData(Resource.Mercury, 1u)]
    [InlineData(Resource.Ore, 2u)]
    [InlineData(Resource.Sulfur, 3u)]
    [InlineData(Resource.Crystal, 4u)]
    [InlineData(Resource.Gems, 5u)]
    [InlineData(Resource.Gold, 6u)]
    public void ResourceToOffsetIcn_MapsTheSevenLetters(int resource, uint expected)
    {
        Assert.Equal(expected, ViewWorldZoomLevels.ResourceToOffsetIcn(resource));
    }

    [Fact]
    public void ResourceToOffsetIcn_RejectsCombinationsAndZero()
    {
        Assert.Equal(ViewWorldZoomLevels.UnknownIndex, ViewWorldZoomLevels.ResourceToOffsetIcn(Resource.Wood | Resource.Ore));
        Assert.Equal(ViewWorldZoomLevels.UnknownIndex, ViewWorldZoomLevels.ResourceToOffsetIcn(0));
    }

    [Fact]
    public void ZoomTables_RejectUnknownLevels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewWorldZoomLevels.TileSize((ZoomLevel)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewWorldZoomLevels.TerrainIcn((ZoomLevel)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewWorldZoomLevels.Next(ZoomLevel.ZoomLevel0, false, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewWorldZoomLevels.Previous(ZoomLevel.ZoomLevel0, false, 5));
    }

    [Fact]
    public void Constructor_RejectsBadArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewWorldZoom(ZoomLevel.ZoomLevel0, new Point(0, 0), Window, 2, 144, 144));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewWorldZoom(ZoomLevel.ZoomLevel0, new Point(0, 0), Window, 4, 0, 144));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewWorldZoom(ZoomLevel.ZoomLevel0, new Point(0, 0), Window, 4, 144, 0));
    }

    [Fact]
    public void FourLevelView_CanReachTheOneToOneScale()
    {
        ViewWorldZoom view = new(ZoomLevel.ZoomLevel0, new Point(2000, 2000), Window, ViewWorldZoomLevels.TotalZoomLevels, 144, 144);

        Assert.True(view.ZoomIn());
        Assert.True(view.ZoomIn());
        Assert.True(view.ZoomIn());

        Assert.Equal(ZoomLevel.ZoomLevel3, view.ZoomLevel);
        Assert.False(view.ZoomIn());
        Assert.True(view.ZoomIn(true));
        Assert.Equal(ZoomLevel.ZoomLevel0, view.ZoomLevel);
    }
}
