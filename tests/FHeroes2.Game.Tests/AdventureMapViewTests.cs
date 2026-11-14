using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="AdventureMapView"/>: the scroll clamping, the tile lookup of a viewport point
/// and the ground layer drawing. The map is synthetic and its tiles are 2x2 pixels, which keeps the
/// expectations readable.
/// </summary>
public sealed class AdventureMapViewTests
{
    private const int TileSize = 2;

    [Fact]
    public void ScrollTo_IsClampedToTheMap()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));

        Assert.Equal(new Point(4, 2), view.MaximumScroll);

        view.ScrollTo(100, 100);
        Assert.Equal(new Point(4, 2), view.Scroll);

        view.ScrollTo(-10, -10);
        Assert.Equal(new Point(0, 0), view.Scroll);
    }

    [Fact]
    public void ScrollBy_AccumulatesAndStopsAtTheEdges()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));

        view.ScrollBy(TileSize, TileSize);
        Assert.Equal(new Point(2, 2), view.Scroll);

        view.ScrollBy(TileSize, TileSize);
        Assert.Equal(new Point(4, 2), view.Scroll);
    }

    [Fact]
    public void MapPixelSize_UsesTheTileSize()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));

        Assert.Equal(new Size(4 * TileSize, 3 * TileSize), view.MapPixelSize);
        Assert.True(view.IsScrollingPossible);
    }

    [Fact]
    public void View_ThatFitsTheMapDoesNotScroll()
    {
        AdventureMapView view = CreateView(2, 2, new Size(8, 8));

        Assert.False(view.IsScrollingPossible);
        Assert.Equal(new Point(0, 0), view.MaximumScroll);

        view.ScrollBy(10, 10);
        Assert.Equal(new Point(0, 0), view.Scroll);
    }

    [Fact]
    public void TileAt_UsesTheScrollOffset()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));
        view.ScrollTo(2, 2);

        Assert.Equal(new Point(1, 1), view.TileAt(new Point(0, 0)));
        Assert.Equal(new Point(2, 1), view.TileAt(new Point(2, 0)));
    }

    [Fact]
    public void CenterOn_PutsTheTileIntoTheMiddle()
    {
        AdventureMapView view = CreateView(8, 8, new Size(4, 4));

        view.CenterOn(4, 4);

        // The tile top left corner is (8; 8) and the viewport is 4x4 pixels, so the scroll offset is 6.
        Assert.Equal(new Point(6, 6), view.Scroll);
    }

    [Fact]
    public void VisibleTileArea_CoversThePartiallyVisibleTiles()
    {
        AdventureMapView view = CreateView(8, 8, new Size(4, 4));
        view.ScrollTo(1, 1);

        // Columns 0…2 and rows 0…2 are touched; neither edge is aligned to the tile grid.
        Assert.Equal(new Rect(0, 0, 3, 3), view.VisibleTileArea);
    }

    [Fact]
    public void VisibleTileArea_StopsAtTheMapEdge()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));
        view.ScrollTo(4, 2);

        Assert.Equal(new Rect(2, 1, 2, 2), view.VisibleTileArea);
    }

    [Fact]
    public void Draw_PaintsTheVisibleTerrain()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));
        PixelBuffer target = new(4, 4);

        view.Draw(target);

        // Tile 0 of the map has image 1, tile 1 has image 2, and so on; every image is a flat fill.
        Assert.Equal(1, target.Row(0)[0]);
        Assert.Equal(2, target.Row(0)[2]);
        Assert.Equal(2, target.Row(0)[3]);
        Assert.Equal(5, target.Row(2)[0]);
        Assert.Equal(6, target.Row(2)[2]);
    }

    [Fact]
    public void Draw_HonoursTheScrollOffset()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4));
        view.ScrollTo(2, 2);

        PixelBuffer target = new(4, 4);
        view.Draw(target);

        // The top left pixel of the viewport is the second column of the second row, i.e. tile 5.
        Assert.Equal(6, target.Row(0)[0]);
        Assert.Equal(7, target.Row(0)[2]);
    }

    [Fact]
    public void Draw_SkipsTilesWithoutAnImage()
    {
        AdventureMapView view = CreateView(4, 3, new Size(4, 4), unknownTiles: true);
        PixelBuffer target = new(4, 4);
        ImageOps.FillRect(target, new Rect(0, 0, 4, 4), 200);

        view.Draw(target);

        Assert.Equal(200, target.Row(0)[0]);
    }

    private static AdventureMapView CreateView(int width, int height, Size viewport, bool unknownTiles = false)
    {
        Mp2Map map = SyntheticMap.Create(
            width,
            height,
            index => default(Mp2TileInfo) with
            {
                // Out of range image indices exercise the "nothing to draw" path.
                TerrainImageIndex = unknownTiles ? (ushort)999 : (ushort)index,
                TerrainFlags = 0,
            });

        TerrainTileSet tiles = TerrainTileSet.Create(CreateTerrainTil(width * height));
        return new AdventureMapView(map, tiles, viewport);
    }

    /// <summary>One flat 2x2 image per map tile; image <c>i</c> is filled with <c>i + 1</c>.</summary>
    private static TilFile CreateTerrainTil(int count)
    {
        List<byte> data = [];
        data.Add((byte)count);
        data.Add((byte)(count >> 8));
        data.Add(TileSize);
        data.Add(0);
        data.Add(TileSize);
        data.Add(0);

        for (int index = 0; index < count; ++index)
        {
            byte value = (byte)(index + 1);
            for (int pixel = 0; pixel < TileSize * TileSize; ++pixel)
            {
                data.Add(value);
            }
        }

        return TilFile.Parse([.. data]);
    }
}
