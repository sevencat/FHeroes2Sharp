using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The block cache of the View World window, <c>CacheForMapWithResources</c> of <c>view_world.cpp:189-268</c>.
/// </summary>
public sealed class ViewWorldMapCacheTests
{
    private const int MapSize = 36;

    [Fact]
    public void AWidthThatIsNotABlockIsRejected()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new ViewWorldMapCache(ViewWorldMode.ViewAll, 35, MapSize, ViewWorldZoomLevels.RegularZoomLevels, NoDraw));

        Assert.Equal("mapWidthInTiles", error.ParamName);
    }

    [Fact]
    public void AHeightThatIsNotABlockIsRejected()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new ViewWorldMapCache(ViewWorldMode.ViewAll, MapSize, 20, ViewWorldZoomLevels.RegularZoomLevels, NoDraw));

        Assert.Equal("mapHeightInTiles", error.ParamName);
    }

    [Fact]
    public void ALevelCountOtherThanThreeOrFourIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ViewWorldMapCache(ViewWorldMode.ViewAll, MapSize, MapSize, 2, NoDraw));

    [Fact]
    public void ANonPositiveSizeIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ViewWorldMapCache(ViewWorldMode.ViewAll, 0, MapSize, ViewWorldZoomLevels.RegularZoomLevels, NoDraw));

    [Fact]
    public void EveryLevelIsCachedInItsOwnTileSize()
    {
        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, MapSize, 18, ViewWorldZoomLevels.RegularZoomLevels, NoDraw);

        Assert.Equal(ViewWorldZoomLevels.RegularZoomLevels, cache.ZoomLevelCount);
        Assert.Equal(4, ViewWorldZoomLevels.TileSize(ZoomLevel.ZoomLevel0));
        Assert.Equal(new Size(144, 72), cache.GetImage(ZoomLevel.ZoomLevel0).Size);
        Assert.Equal(new Size(216, 108), cache.GetImage(ZoomLevel.ZoomLevel1).Size);
        Assert.Equal(new Size(432, 216), cache.GetImage(ZoomLevel.ZoomLevel2).Size);
    }

    [Fact]
    public void TheDebugLevelIsCachedOneToOne()
    {
        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, 18, 18, ViewWorldZoomLevels.TotalZoomLevels, NoDraw);

        Assert.Equal(new Size(576, 576), cache.GetImage(ZoomLevel.ZoomLevel3).Size);
    }

    [Fact]
    public void TheBlockDrawerIsCalledOncePerBlockWithTheBlockCentre()
    {
        List<Point> centres = [];

        _ = new ViewWorldMapCache(ViewWorldMode.ViewAll, MapSize, MapSize, ViewWorldZoomLevels.RegularZoomLevels,
            (_, centre, _) => centres.Add(centre));

        Assert.Equal(
            [new Point(288, 288), new Point(864, 288), new Point(288, 864), new Point(864, 864)],
            centres);
    }

    [Fact]
    public void EveryBlockIsScaledIntoEveryLevel()
    {
        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, 18, 18, ViewWorldZoomLevels.RegularZoomLevels,
            (block, _, _) => block.Fill(7));

        foreach (PixelBuffer image in cache.Images)
        {
            Assert.All(image.Pixels, pixel => Assert.Equal(7, pixel));
        }
    }

    [Fact]
    public void ABlockDoesNotLeakIntoTheNextOne()
    {
        int painted = 0;

        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, MapSize, MapSize, ViewWorldZoomLevels.RegularZoomLevels,
            (block, _, _) =>
            {
                // 一点，只在第一块上：临时图是共用的，没清干净就会跟着后面的块出现。
                if (painted++ == 0)
                {
                    block[0, 0] = 9;
                }
            });

        Assert.Equal(9, cache.GetImage(ZoomLevel.ZoomLevel0)[0, 0]);
        Assert.Equal(0, cache.GetImage(ZoomLevel.ZoomLevel0)[18 * 4, 0]);
        Assert.Equal(0, cache.GetImage(ZoomLevel.ZoomLevel2)[18 * 12, 0]);
    }

    [Fact]
    public void TheLayersFollowTheMode()
    {
        Assert.Equal(new ViewWorldDrawLayers(Fog: false, Towns: false, Heroes: false, Routes: false),
            new ViewWorldMapCache(ViewWorldMode.ViewAll, 18, 18, ViewWorldZoomLevels.RegularZoomLevels, NoDraw).Layers);

        Assert.Equal(new ViewWorldDrawLayers(Fog: true, Towns: true, Heroes: false, Routes: false),
            new ViewWorldMapCache(ViewWorldMode.ViewTowns, 18, 18, ViewWorldZoomLevels.RegularZoomLevels, NoDraw).Layers);

        Assert.Equal(new ViewWorldDrawLayers(Fog: true, Towns: false, Heroes: false, Routes: false),
            new ViewWorldMapCache(ViewWorldMode.OnlyVisible, 18, 18, ViewWorldZoomLevels.RegularZoomLevels, NoDraw).Layers);
    }

    [Fact]
    public void ALevelThatWasNotCachedIsRejected()
    {
        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, 18, 18, ViewWorldZoomLevels.RegularZoomLevels, NoDraw);

        Assert.Throws<ArgumentOutOfRangeException>(() => cache.GetImage(ZoomLevel.ZoomLevel3));
    }

    private static void NoDraw(PixelBuffer block, Point centreInPixels, ViewWorldDrawLayers layers)
    {
    }
}
