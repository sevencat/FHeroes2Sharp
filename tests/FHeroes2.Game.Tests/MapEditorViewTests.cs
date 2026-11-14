using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapEditorView"/>: the tile under the pointer (including the stale-hover trap of
/// a scroll), the tile borders of the grid and the outlined tile. The map is synthetic, thirty two
/// pixel tiles, so a tile boundary is always a multiple of thirty two.
/// </summary>
public sealed class MapEditorViewTests
{
    private const int TileSize = 32;

    [Fact]
    public void HoverTile_FollowsTheCursor()
    {
        MapEditorView editor = CreateEditor();

        editor.SetCursor(new Point(40, 40));

        Assert.Equal(new Point(1, 1), editor.HoverTile);
        Assert.Equal(MapGround.Grass, editor.HoverGround);
    }

    [Fact]
    public void HoverTile_FollowsTheMapAfterAScroll()
    {
        MapEditorView editor = CreateEditor();

        editor.SetCursor(new Point(40, 40));
        Assert.Equal(new Point(1, 1), editor.HoverTile);

        // The pointer stands still while the terrain moves, so the tile under it changes.
        editor.Map.ScrollTo(TileSize, 0);

        Assert.Equal(new Point(2, 1), editor.HoverTile);
    }

    [Fact]
    public void HoverTile_IsNullOutsideTheViewport()
    {
        MapEditorView editor = CreateEditor();

        editor.SetCursor(new Point(-1, 10));
        Assert.Null(editor.HoverTile);

        editor.SetCursor(new Point(10, 96));
        Assert.Null(editor.HoverTile);
    }

    [Fact]
    public void HoverTile_IsNullWhereTheMapHasNoTiles()
    {
        // A two by two map leaves the rest of the ninety six pixel viewport empty.
        MapEditorView editor = CreateEditor(mapTiles: 2);

        editor.SetCursor(new Point(80, 80));

        Assert.Null(editor.HoverTile);
        Assert.Null(editor.Hover);
        Assert.Equal(MapGround.Unknown, editor.HoverGround);
    }

    [Fact]
    public void HoverAreaOutlinesTheTileInViewportPixels()
    {
        MapEditorView editor = CreateEditor();
        editor.Map.ScrollTo(5, 7);

        editor.SetCursor(new Point(40, 40));

        Assert.Equal(new Point(1, 1), editor.HoverTile);
        Assert.Equal(new Rect(TileSize - 5, TileSize - 7, TileSize, TileSize), editor.HoverArea);
    }

    [Fact]
    public void HoverArea_IsNullWithoutACursor()
    {
        MapEditorView editor = CreateEditor();

        editor.SetCursor(null);

        Assert.Null(editor.HoverArea);
    }

    [Fact]
    public void TheGridCoversTheVisibleBorders()
    {
        MapEditorView editor = CreateEditor();

        // Ninety six pixels hold three tiles, so the borders are at 0, 32 and 64; 96 is the right
        // edge of the viewport and is not painted.
        Assert.Equal([0, 32, 64], editor.GridColumns());
        Assert.Equal([0, 32, 64], editor.GridRows());
    }

    [Fact]
    public void TheGridFollowsTheScroll()
    {
        MapEditorView editor = CreateEditor();
        editor.Map.ScrollTo(16, 16);

        Assert.Equal([16, 48, 80], editor.GridColumns());
        Assert.Equal([16, 48, 80], editor.GridRows());
    }

    [Fact]
    public void TheGridClosesAtTheEdgeOfTheMap()
    {
        // A two by two map in a 128 pixel viewport: the border at 64 is the right edge of the map and
        // is painted, which is what closes the grid instead of leaving it open.
        MapEditorView editor = CreateEditor(mapTiles: 2, viewport: 128);

        Assert.Equal([0, 32, 64], editor.GridColumns());
        Assert.Equal([0, 32, 64], editor.GridRows());
    }

    [Fact]
    public void TogglingTheGridRemovesTheBorders()
    {
        MapEditorView editor = CreateEditor();

        Assert.True(editor.GridEnabled);
        Assert.False(editor.ToggleGrid());
        Assert.False(editor.GridEnabled);
        Assert.Empty(editor.GridColumns());
        Assert.Empty(editor.GridRows());

        Assert.True(editor.ToggleGrid());
        Assert.Equal([0, 32, 64], editor.GridColumns());
    }

    [Fact]
    public void TheGroundComesFromTheTerrainImage()
    {
        MapEditorView water = CreateEditor(terrainImageIndex: 0);
        water.SetCursor(new Point(40, 40));

        Assert.Equal(MapGround.Water, water.HoverGround);

        MapEditorView grass = CreateEditor(terrainImageIndex: 30);
        grass.SetCursor(new Point(40, 40));

        Assert.Equal(MapGround.Grass, grass.HoverGround);
    }

    private static MapEditorView CreateEditor(
        int mapTiles = 8,
        int viewport = 96,
        ushort terrainImageIndex = 30)
    {
        Mp2Map map = SyntheticMap.Create(mapTiles, mapTiles, _ => Tile(terrainImageIndex));
        TerrainTileSet tiles = SyntheticTerrain.Create(4, TileSize);

        return new MapEditorView(new AdventureMapView(map, tiles, new Size(viewport, viewport)));
    }

    private static Mp2TileInfo Tile(ushort terrainImageIndex) =>
        new()
        {
            TerrainImageIndex = terrainImageIndex,
            ObjectName1 = 0,
            BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            TerrainFlags = 0,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };
}
