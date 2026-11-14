using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapScrollController"/>: the ten pixel threshold that turns a press into a drag,
/// the direction the terrain follows the pointer in, the edge scroll tick and the wheel step. The map
/// is synthetic, eight by eight tiles of thirty two pixels, in a ninety six pixel viewport, so the
/// maximum scroll is 160 pixels on both axes.
/// </summary>
public sealed class MapScrollControllerTests
{
    private const int TileSize = 32;
    private const int MapTiles = 8;
    private const int ViewportEdge = 96;

    [Fact]
    public void APressWithoutMovement_IsNotADrag()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(50, 50));
        mouse.SetLeftButton(true);
        mouse.SetCursor(new Point(58, 58));

        Assert.False(mouse.IsDragging);
        Assert.Equal(new Point(0, 0), view.Scroll);
    }

    [Fact]
    public void TheThresholdIsStrict()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(50, 50));
        mouse.SetLeftButton(true);
        mouse.SetCursor(new Point(60, 50));

        Assert.False(mouse.IsDragging);
        Assert.Equal(new Point(0, 0), view.Scroll);
    }

    [Fact]
    public void MovementBeyondTheThreshold_StartsTheDrag()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(50, 50));
        mouse.SetLeftButton(true);
        mouse.SetCursor(new Point(39, 50));

        Assert.True(mouse.IsDragging);

        // The drag starts from the press position, so the eleven pixels that crossed the threshold
        // are part of the first shift: the terrain follows the pointer to the left.
        Assert.Equal(new Point(11, 0), view.Scroll);
    }

    [Fact]
    public void ThePanButtonDragsLikeTheLeftOne()
    {
        // The editor paints with the left button, so it pans with the right one; both drive the same
        // state machine, and a release of either ends the gesture.
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(50, 50));
        mouse.SetPanButton(true);
        mouse.SetCursor(new Point(30, 50));

        Assert.True(mouse.IsDragging);
        Assert.Equal(new Point(20, 0), view.Scroll);

        mouse.SetPanButton(false);
        Assert.False(mouse.IsDragging);
    }

    [Fact]
    public void TheTerrainFollowsThePointer_UntilTheRelease()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(50, 50));
        mouse.SetLeftButton(true);
        mouse.SetCursor(new Point(30, 40));
        Assert.Equal(new Point(20, 10), view.Scroll);

        mouse.SetCursor(new Point(20, 40));
        Assert.Equal(new Point(30, 10), view.Scroll);

        mouse.SetLeftButton(false);
        mouse.SetCursor(new Point(90, 40));

        Assert.False(mouse.IsDragging);
        Assert.Equal(new Point(30, 10), view.Scroll);
    }

    [Fact]
    public void TheDragIsClampedToTheMap()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(90, 90));
        mouse.SetLeftButton(true);
        mouse.SetCursor(new Point(-200, -200));

        // A drag of 290 pixels is cut at the 160 pixels of the map: the controller never reports a
        // position outside of it.
        Assert.Equal(new Point(160, 160), view.Scroll);
    }

    [Fact]
    public void TheEdgeScrollsOncePerTick()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        // Start in the middle of the map: the left border has room to scroll towards zero.
        view.ScrollTo(100, 100);
        mouse.SetCursor(new Point(0, 50));

        Assert.True(mouse.Update(MapScrollController.EdgeScrollInterval));
        Assert.Equal(new Point(100 - MapScrollController.EdgeScrollStep, 100), view.Scroll);

        // Half a tick does nothing, the second half completes the next step.
        Assert.False(mouse.Update(MapScrollController.EdgeScrollInterval / 2));
        Assert.True(mouse.Update(MapScrollController.EdgeScrollInterval / 2));
        Assert.Equal(new Point(100 - (MapScrollController.EdgeScrollStep * 2), 100), view.Scroll);
    }

    [Fact]
    public void TheCornerScrollsBothAxes()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        view.ScrollTo(100, 100);
        mouse.SetCursor(new Point(0, 0));
        mouse.Update(MapScrollController.EdgeScrollInterval);

        Assert.Equal(
            new Point(100 - MapScrollController.EdgeScrollStep, 100 - MapScrollController.EdgeScrollStep),
            view.Scroll);

        mouse.SetCursor(new Point(ViewportEdge - 1, ViewportEdge - 1));
        mouse.Update(MapScrollController.EdgeScrollInterval);

        Assert.Equal(new Point(100, 100), view.Scroll);
    }

    [Fact]
    public void TheMiddleOfTheViewportDoesNotScroll()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(48, 48));

        Assert.False(mouse.Update(1.0));
        Assert.Equal(new Point(0, 0), view.Scroll);
    }

    [Fact]
    public void ADragDisablesTheEdgeScroll()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.SetCursor(new Point(50, 50));
        mouse.SetLeftButton(true);
        mouse.SetCursor(new Point(20, 50));
        mouse.SetCursor(new Point(0, 50));
        Point dragged = view.Scroll;

        mouse.Update(MapScrollController.EdgeScrollInterval * 2);

        Assert.Equal(dragged, view.Scroll);
    }

    [Fact]
    public void TheEdgeScrollStopsAtTheBorderOfTheMap()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        view.ScrollTo(160, 160);
        mouse.SetCursor(new Point(ViewportEdge - 1, ViewportEdge - 1));

        Assert.False(mouse.Update(MapScrollController.EdgeScrollInterval));
        Assert.Equal(new Point(160, 160), view.Scroll);
    }

    [Fact]
    public void TheWheelMovesOneTilePerNotch()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        Assert.True(mouse.ScrollByWheel(0, 1));
        Assert.Equal(new Point(0, TileSize), view.Scroll);

        Assert.True(mouse.ScrollByWheel(1, 0));
        Assert.Equal(new Point(TileSize, TileSize), view.Scroll);

        Assert.True(mouse.ScrollByWheel(-1, -1));
        Assert.Equal(new Point(0, 0), view.Scroll);

        Assert.False(mouse.ScrollByWheel(0, 0));
    }

    [Fact]
    public void TheWheelIsClampedToTheMap()
    {
        MapScrollController mouse = CreateController(out AdventureMapView view);

        mouse.ScrollByWheel(0, 99);
        Assert.Equal(new Point(0, 160), view.Scroll);

        mouse.ScrollByWheel(0, -99);
        Assert.Equal(new Point(0, 0), view.Scroll);
    }

    private static MapScrollController CreateController(out AdventureMapView view)
    {
        Mp2Map map = SyntheticMap.Create(MapTiles, MapTiles);
        TerrainTileSet tiles = SyntheticTerrain.Create(4, TileSize);
        view = new AdventureMapView(map, tiles, new Size(ViewportEdge, ViewportEdge));

        return new MapScrollController(view);
    }
}
