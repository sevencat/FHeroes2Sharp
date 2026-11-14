using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapEditSession"/>: what a stroke writes, how a drag collapses into one undo step
/// and how undo and redo replay it. The map is synthetic and its tiles are grass with a shape flag, so
/// a painted tile is checked by its terrain image and by the bits that must survive the paint.
/// </summary>
public sealed class MapEditSessionTests
{
    private const ushort Grass = MapGrounds.GrassStartImageIndex;
    private const ushort Snow = MapGrounds.SnowStartImageIndex;

    [Fact]
    public void TheBrushStartsOnGrassAndCanBePicked()
    {
        MapEditSession session = CreateSession();

        Assert.Equal(MapGround.Grass, session.Brush);
        Assert.True(session.SetBrush(MapGround.Snow));
        Assert.Equal(MapGround.Snow, session.Brush);

        // Picking the ground that is already loaded changes nothing, so no redraw is needed.
        Assert.False(session.SetBrush(MapGround.Snow));
    }

    [Fact]
    public void AnUnknownGroundCannotBePainted()
    {
        MapEditSession session = CreateSession();

        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetBrush(MapGround.Unknown));
        Assert.Equal(MapGround.Grass, session.Brush);
    }

    [Fact]
    public void PaintingChangesTheTerrainImageAndGround()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);

        Assert.True(session.Paint(new Point(1, 2)));

        Mp2TileInfo tile = session.Map.Tile(1, 2);

        Assert.Equal(Snow, tile.TerrainImageIndex);
        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex));
    }

    [Fact]
    public void PaintingKeepsTheShapeAndDropsTheBeachBits()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);

        // Bits 0 and 1 are the terrain shape, bits 2 to 4 are the beach transition flags.
        session.Map.SetTile(1, 1, Tile(Grass) with { TerrainFlags = 0b0001_0011 });

        session.Paint(new Point(1, 1));

        Assert.Equal(0b0000_0011, session.Map.Tile(1, 1).TerrainFlags);
    }

    [Fact]
    public void PaintingTheGroundThatIsAlreadyThereDoesNothing()
    {
        MapEditSession session = CreateSession();

        session.SetBrush(MapGround.Grass);

        Assert.False(session.Paint(new Point(0, 0)));
        Assert.False(session.IsDirty);
        Assert.Equal(0, session.UndoDepth);
    }

    [Fact]
    public void PaintingOutsideTheMapIsIgnored()
    {
        MapEditSession session = CreateSession();

        Assert.False(session.Paint(null));
        Assert.False(session.Paint(new Point(-1, 0)));
        Assert.False(session.Paint(new Point(0, -1)));
        Assert.False(session.Paint(new Point(4, 3)));
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void OneStrokeIsOneUndoStepWhateverTheNumberOfTiles()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);

        session.BeginStroke();
        session.Paint(new Point(0, 0));
        session.Paint(new Point(1, 0));
        session.Paint(new Point(2, 0));
        session.EndStroke();

        Assert.Equal(1, session.UndoDepth);
        Assert.True(session.IsDirty);

        Assert.True(session.Undo());

        Assert.Equal(Grass, session.Map.Tile(0, 0).TerrainImageIndex);
        Assert.Equal(Grass, session.Map.Tile(1, 0).TerrainImageIndex);
        Assert.Equal(Grass, session.Map.Tile(2, 0).TerrainImageIndex);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void CrossingTheSameTileTwiceUndoesInOneStep()
    {
        MapEditSession session = CreateSession();

        // A drag that goes back and forth also goes through the same tile again, and the middle value
        // of that tile (snow, then again snow) must not become a step of its own.
        session.SetBrush(MapGround.Snow);
        session.BeginStroke();
        session.Paint(new Point(1, 1));
        session.SetBrush(MapGround.Swamp);
        session.Paint(new Point(1, 1));
        session.EndStroke();

        session.SetBrush(MapGround.Grass);
        session.BeginStroke();
        session.Paint(new Point(1, 1));
        session.EndStroke();

        Assert.Equal(2, session.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal(MapGrounds.StartImageIndex(MapGround.Swamp), session.Map.Tile(1, 1).TerrainImageIndex);

        Assert.True(session.Undo());
        Assert.Equal(Grass, session.Map.Tile(1, 1).TerrainImageIndex);
    }

    [Fact]
    public void RedoAppliesTheStrokeAgain()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);
        session.Paint(new Point(2, 2));

        Assert.True(session.Undo());
        Assert.Equal(1, session.RedoDepth);

        Assert.True(session.Redo());

        Assert.Equal(Snow, session.Map.Tile(2, 2).TerrainImageIndex);
        Assert.Equal(0, session.RedoDepth);
        Assert.Equal(1, session.UndoDepth);
    }

    [Fact]
    public void AnEmptyHistoryRefusesToUndoOrRedo()
    {
        MapEditSession session = CreateSession();

        Assert.False(session.Undo());
        Assert.False(session.Redo());
    }

    [Fact]
    public void ANewStrokeDropsTheRedoBranch()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);
        session.Paint(new Point(0, 0));

        Assert.True(session.Undo());
        Assert.Equal(1, session.RedoDepth);

        session.SetBrush(MapGround.Dirt);
        session.Paint(new Point(0, 1));

        Assert.Equal(0, session.RedoDepth);
        Assert.False(session.Redo());
    }

    [Fact]
    public void PaintingWithoutAnExplicitStrokeIsAStrokeOfItsOwn()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);

        session.Paint(new Point(0, 0));
        session.Paint(new Point(1, 0));

        Assert.Equal(2, session.UndoDepth);
        Assert.False(session.IsPainting);
    }

    [Fact]
    public void UndoAlsoClosesAStrokeThatIsStillOpen()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);

        session.BeginStroke();
        session.Paint(new Point(0, 0));
        Assert.True(session.IsPainting);

        Assert.True(session.Undo());

        Assert.False(session.IsPainting);
        Assert.Equal(Grass, session.Map.Tile(0, 0).TerrainImageIndex);
    }

    [Fact]
    public void TheEyedropperTakesTheGroundUnderTheTile()
    {
        MapEditSession session = CreateSession(terrainImageIndex: MapGrounds.StartImageIndex(MapGround.Lava));

        Assert.True(session.SampleBrush(new Point(0, 0)));
        Assert.Equal(MapGround.Lava, session.Brush);

        Assert.False(session.SampleBrush(null));
        Assert.False(session.SampleBrush(new Point(9, 9)));
    }

    [Fact]
    public void PaintingLeavesTheObjectsOfTheTileAlone()
    {
        MapEditSession session = CreateSession();
        Mp2TileInfo decorated = Tile(Grass) with { ObjectType = Mp2ObjectType.TreasureChest, ObjectName1 = 7 };
        session.Map.SetTile(0, 0, decorated);

        session.SetBrush(MapGround.Snow);
        session.Paint(new Point(0, 0));

        Mp2TileInfo painted = session.Map.Tile(0, 0);

        Assert.Equal(Snow, painted.TerrainImageIndex);
        Assert.Equal(Mp2ObjectType.TreasureChest, painted.ObjectType);
        Assert.Equal(7, painted.ObjectName1);
    }

    private static MapEditSession CreateSession(ushort terrainImageIndex = Grass)
    {
        Mp2Map map = SyntheticMap.Create(4, 4, _ => Tile(terrainImageIndex));
        TerrainTileSet tiles = SyntheticTerrain.Create(4, 32);

        return new MapEditSession(new MapEditorView(new AdventureMapView(map, tiles, new Size(96, 96))));
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
