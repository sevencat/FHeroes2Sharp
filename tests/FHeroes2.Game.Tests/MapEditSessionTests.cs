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

    private const MapGround GrassGround = MapGround.Grass;
    private const MapGround SnowGround = MapGround.Snow;

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

        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex));

        // The plain part of a ground block starts at +38 and holds eight variants; the first images of
        // the block are transitions to another ground, so a painted tile must not be one of those.
        Assert.InRange(tile.TerrainImageIndex, Snow + 38, Snow + 45);
    }

    [Fact]
    public void PaintingRewritesTheShapeAndDropsTheBeachBits()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Grass);

        // Bits 0 and 1 are the terrain shape, bits 2 to 4 are the beach transition flags.
        session.Map.SetTile(1, 1, Tile(Grass) with { TerrainFlags = 0b0001_0011 });

        session.Paint(new Point(1, 1));

        // Grass all around: no edge has to be drawn, and the beach flag describes water that is no
        // longer there.
        Assert.Equal(0, session.Map.Tile(1, 1).TerrainFlags);
    }

    [Fact]
    public void PaintingTheSameGroundTwiceChangesOnlyOnce()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Grass);

        Assert.True(session.Paint(new Point(0, 0)));

        int depth = session.UndoDepth;

        // A drag goes through the same tile again: the second time the tile already holds what the
        // brush would write, so nothing is added to the stroke.
        Assert.False(session.Paint(new Point(0, 0)));
        Assert.Equal(depth, session.UndoDepth);
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
    public void TheBrushStartsSmallAndTheSizeCanBePicked()
    {
        MapEditSession session = CreateSession();

        Assert.Equal(BrushSize.Small, session.BrushSize);
        Assert.True(session.SetBrushSize(BrushSize.Large));
        Assert.Equal(BrushSize.Large, session.BrushSize);
        Assert.False(session.SetBrushSize(BrushSize.Large));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetBrushSize((BrushSize)4));
        Assert.Equal(BrushSize.Large, session.BrushSize);
    }

    [Fact]
    public void TheMediumBrushPaintsTwoByTwoTiles()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);
        session.SetBrushSize(BrushSize.Medium);

        Assert.True(session.Paint(new Point(1, 1)));

        // The reference anchors the 2x2 square at the tile under the pointer, top left corner first.
        Assert.Equal(SnowGround, GroundAt(session, 1, 1));
        Assert.Equal(SnowGround, GroundAt(session, 2, 1));
        Assert.Equal(SnowGround, GroundAt(session, 1, 2));
        Assert.Equal(SnowGround, GroundAt(session, 2, 2));

        // The stroke is one undo step whatever number of tiles it covers.
        Assert.Equal(1, session.UndoDepth);
        Assert.True(session.Undo());
        Assert.Equal(GrassGround, GroundAt(session, 1, 1));
        Assert.Equal(GrassGround, GroundAt(session, 2, 2));
    }

    [Fact]
    public void TheLargeBrushIsCentredOnTheTileAndClippedByTheMap()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);
        session.SetBrushSize(BrushSize.Large);

        // The map is 4x4, so a 4x4 square centred at (2,2) hangs over the right and the bottom edge:
        // the tiles outside must be dropped instead of throwing.
        Assert.True(session.Paint(new Point(2, 2)));

        Assert.Equal(SnowGround, GroundAt(session, 1, 1));
        Assert.Equal(SnowGround, GroundAt(session, 3, 3));
        Assert.Equal(GrassGround, GroundAt(session, 0, 0));
        Assert.Equal(GrassGround, GroundAt(session, 3, 0));
    }

    [Fact]
    public void TheAreaBrushFillsTheRectangleOfTheDrag()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);
        session.SetBrushSize(BrushSize.Area);

        session.BeginStroke();
        session.Paint(new Point(2, 2));
        session.Paint(new Point(0, 1));
        session.EndStroke();

        // The rectangle between the tile the stroke started on and the one the pointer is over, no
        // matter which of the two is the top left corner.
        Assert.Equal(SnowGround, GroundAt(session, 0, 1));
        Assert.Equal(SnowGround, GroundAt(session, 2, 2));
        Assert.Equal(SnowGround, GroundAt(session, 1, 1));
        Assert.Equal(GrassGround, GroundAt(session, 0, 0));
        Assert.Equal(GrassGround, GroundAt(session, 3, 0));

        Assert.Equal(1, session.UndoDepth);
    }

    [Fact]
    public void TheEraserClearsEveryTileOfTheBrushArea()
    {
        MapEditSession session = CreateSession();

        // 用一个真物件而不是手搭的字节：橡皮按目录分组认物件，认不出的一格按参考的做法跳过。
        Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart);

        session.SelectObject(preset);

        for (int y = 1; y <= 2; ++y)
        {
            for (int x = 1; x <= 2; ++x)
            {
                Assert.True(session.PlaceObject(new Point(x, y)));
            }
        }

        session.SetTool(EditorTool.Erase);
        session.SetBrushSize(BrushSize.Medium);
        Assert.True(session.EraseObject(new Point(1, 1)));

        for (int y = 1; y <= 2; ++y)
        {
            for (int x = 1; x <= 2; ++x)
            {
                Mp2TileInfo tile = session.Map.Tile(x, y);

                Assert.Equal(Mp2ObjectType.None, tile.ObjectType);
                Assert.False(tile.HasGroundObject);
                Assert.False(tile.HasTopObject);
            }
        }

        Assert.Equal(Grass, session.Map.Tile(1, 1).TerrainImageIndex);
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
        Assert.Equal(MapGround.Swamp, MapGrounds.FromTerrainImageIndex(session.Map.Tile(1, 1).TerrainImageIndex));

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

        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(session.Map.Tile(2, 2).TerrainImageIndex));
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

        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(painted.TerrainImageIndex));
        Assert.Equal(Mp2ObjectType.TreasureChest, painted.ObjectType);
        Assert.Equal(7, painted.ObjectName1);
    }

    [Fact]
    public void ASaveClearsTheDirtyFlagAndAStrokeSetsItAgain()
    {
        string filePath = CreateBlankMapFile();

        try
        {
            MapEditSession session = CreateSessionFrom(filePath);

            // The map is grass, so the brush has to be something else or the stroke would be a no-op.
            session.SetBrush(MapGround.Snow);

            Assert.False(session.IsDirty);

            session.Paint(new Point(1, 1));
            Assert.True(session.IsDirty);

            Assert.True(session.Save(out string? error), error);
            Assert.False(session.IsDirty);

            session.Paint(new Point(2, 2));
            Assert.True(session.IsDirty);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void UndoingBackToTheSavePointClearsTheDirtyFlag()
    {
        string filePath = CreateBlankMapFile();

        try
        {
            MapEditSession session = CreateSessionFrom(filePath);
            session.SetBrush(MapGround.Snow);

            session.Paint(new Point(0, 0));
            session.Paint(new Point(1, 0));
            Assert.True(session.Save(out string? error), error);

            session.Paint(new Point(2, 0));
            Assert.True(session.IsDirty);

            Assert.True(session.Undo());
            Assert.False(session.IsDirty);

            // Undoing further than the save point removes a saved change, so the file is stale again.
            Assert.True(session.Undo());
            Assert.True(session.IsDirty);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AFailedSaveKeepsTheMapDirty()
    {
        string filePath = CreateBlankMapFile();

        try
        {
            MapEditSession session = CreateSessionFrom(filePath);
            session.SetBrush(MapGround.Snow);
            session.Paint(new Point(0, 0));

            // A file cannot be a directory: the map file itself is used as the parent of the target, which
            // no writer can create and which therefore exercises the failure path for real.
            string unwritable = Path.Combine(filePath, "map.mp2");

            Assert.False(session.SaveAs(unwritable, out string? error));
            Assert.NotNull(error);
            Assert.True(session.IsDirty);
            Assert.Equal(filePath, session.SavePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AMapWithoutAFileCannotBeSaved()
    {
        MapEditSession session = CreateSession();

        Assert.False(session.Save(out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ANewMapIsWrittenWholeAndBecomesTheSaveTarget()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"fh2-editor-new-{Guid.NewGuid():N}.mp2");

        try
        {
            MapEditSession session = CreateSessionOn(
                Mp2Map.CreateBlank(36, MapGrounds.StartImageIndex(MapGround.Grass)));

            Assert.Equal(string.Empty, session.SavePath);
            Assert.False(session.IsDirty);

            Assert.True(session.SaveAs(filePath, out string? error), error);
            Assert.Equal(filePath, session.SavePath);
            Assert.False(session.IsDirty);

            Mp2Map saved = Mp2Map.Read(filePath);

            Assert.Equal(36, saved.Width);
            Assert.Equal(Mp2Map.NewMapName, saved.Info.Name);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AnObjectIsPlacedOnTheTileAndGetsItsOwnAddon()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.AddonCount > 0);

        Assert.True(session.SelectObject(preset));
        Assert.Equal(EditorTool.LandscapeObjects, session.Tool);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        Mp2TileInfo tile = session.Map.Tile(1, 1);

        Assert.Equal(preset.Type, tile.ObjectType);
        Assert.Equal(preset.Template.ObjectName1, tile.ObjectName1);
        Assert.NotEqual(0, tile.NextAddonIndex);
        Assert.NotEqual(0u, tile.Level1ObjectUid);
        Assert.Equal(preset.AddonCount, session.Map.EnumerateAddonChain(tile).Count());
        Assert.True(session.IsDirty);
        Assert.Equal(1, session.UndoDepth);
    }

    [Fact]
    public void PlacingTheSameObjectOnTheSameTileChangesNothingTheSecondTime()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.AddonCount > 0);

        session.SelectObject(preset);
        session.PlaceObject(new Point(1, 1));

        uint uid = session.Map.Tile(1, 1).Level1ObjectUid;
        int addons = session.Map.Addons.Count;
        int depth = session.UndoDepth;

        // A drag passes the same tile more than once: it must not spend a UID or an add-on per pass.
        Assert.False(session.PlaceObject(new Point(1, 1)));
        Assert.Equal(uid, session.Map.Tile(1, 1).Level1ObjectUid);
        Assert.Equal(addons, session.Map.Addons.Count);
        Assert.Equal(depth, session.UndoDepth);
    }

    [Fact]
    public void ErasingClearsTheObjectAndKeepsTheGround()
    {
        MapEditSession session = CreateSession();
        session.SetBrush(MapGround.Snow);
        session.Paint(new Point(1, 1));

        session.SelectObject(Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart));
        session.PlaceObject(new Point(1, 1));

        session.SetTool(EditorTool.Erase);
        Assert.True(session.EraseObject(new Point(1, 1)));

        Mp2TileInfo tile = session.Map.Tile(1, 1);

        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex));
        Assert.Equal(Mp2ObjectType.None, tile.ObjectType);
        Assert.Equal(0, tile.NextAddonIndex);
        Assert.Equal(0u, tile.Level1ObjectUid);
        Assert.Equal(0u, tile.Level2ObjectUid);
        Assert.Equal(Mp2TileInfo.EmptyImageIndex, tile.BottomIcnImageIndex);
        Assert.Equal(Mp2TileInfo.EmptyImageIndex, tile.TopIcnImageIndex);
        Assert.Equal(0, tile.Quantity1);
        Assert.False(tile.HasGroundObject);
        Assert.False(tile.HasTopObject);
    }

    [Fact]
    public void ErasingAnEmptyTileIsNotAStroke()
    {
        MapEditSession session = CreateSession();

        Assert.False(session.EraseObject(new Point(1, 1)));
        Assert.False(session.IsDirty);
        Assert.Equal(0, session.UndoDepth);
        Assert.False(session.EraseObject(null));
        Assert.False(session.EraseObject(new Point(9, 9)));
    }

    [Fact]
    public void UndoBringsThePlacedObjectBack()
    {
        MapEditSession session = CreateSession();
        session.SelectObject(Mp2ObjectPresets.All.First(candidate => candidate.AddonCount > 0));

        session.PlaceObject(new Point(1, 1));
        Assert.True(session.Undo());

        Assert.Equal(Mp2ObjectType.None, session.Map.Tile(1, 1).ObjectType);
        Assert.Equal(0, session.Map.Tile(1, 1).NextAddonIndex);
        Assert.False(session.IsDirty);

        Assert.True(session.Redo());
        Assert.NotEqual(Mp2ObjectType.None, session.Map.Tile(1, 1).ObjectType);
    }

    [Fact]
    public void TheToolDecidesWhatAClickDoes()
    {
        MapEditSession session = CreateSession();

        session.SetBrush(MapGround.Snow);
        Assert.True(session.Apply(new Point(0, 0)));
        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(session.Map.Tile(0, 0).TerrainImageIndex));

        Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart);
        session.SelectObject(preset);
        Assert.True(session.Apply(new Point(0, 0)));
        Assert.Equal(preset.Type, session.Map.Tile(0, 0).ObjectType);
        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(session.Map.Tile(0, 0).TerrainImageIndex));

        session.SetTool(EditorTool.Erase);
        Assert.True(session.Apply(new Point(0, 0)));
        Assert.Equal(Mp2ObjectType.None, session.Map.Tile(0, 0).ObjectType);
        Assert.Equal(MapGround.Snow, MapGrounds.FromTerrainImageIndex(session.Map.Tile(0, 0).TerrainImageIndex));
    }

    [Fact]
    public void PickingAGroundOrAnObjectSelectsItsTool()
    {
        MapEditSession session = CreateSession();

        Assert.False(session.SetTool(EditorTool.Terrain));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetTool((EditorTool)9));

        session.CycleObject(1);
        Assert.Equal(EditorTool.LandscapeObjects, session.Tool);
        Assert.Same(Mp2ObjectPresets.All[1], session.ObjectBrush);

        // Cycling wraps around in both directions.
        Assert.Same(Mp2ObjectPresets.All[0], session.CycleObject(Mp2ObjectPresets.Count - 1));
        Assert.Same(Mp2ObjectPresets.All[^1], session.CycleObject(-1));

        Assert.True(session.SetBrush(MapGround.Lava));
        Assert.Equal(EditorTool.Terrain, session.Tool);
    }

    [Fact]
    public void TheObjectOnATileCarriesADataWord()
    {
        MapEditSession session = CreateSession();
        session.SelectObject(Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart));
        session.PlaceObject(new Point(1, 1));

        int before = session.ObjectData(new Point(1, 1));
        int depth = session.UndoDepth;

        Assert.True(session.SetObjectData(new Point(1, 1), 1234));
        Assert.Equal(1234, session.ObjectData(new Point(1, 1)));
        Assert.Equal(depth + 1, session.UndoDepth);

        // The same value again is not a stroke: a drag passes a tile more than once.
        Assert.False(session.SetObjectData(new Point(1, 1), 1234));
        Assert.Equal(depth + 1, session.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal(before, session.ObjectData(new Point(1, 1)));
    }

    [Fact]
    public void TheDataWordLeavesTheLayerBitsOfTheTileAlone()
    {
        MapEditSession session = CreateSession();
        session.SelectObject(Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart));
        session.PlaceObject(new Point(1, 1));

        // The three low bits of quantity1 are outside the word: two of them are the layer the piece is
        // drawn in, so writing the word must not touch them.
        byte layers = (byte)(session.Map.Tile(1, 1).Quantity1 & 0x07);

        Assert.True(session.SetObjectData(new Point(1, 1), MapEditSession.MaxObjectData));

        Mp2TileInfo tile = session.Map.Tile(1, 1);

        Assert.Equal(layers, (byte)(tile.Quantity1 & 0x07));
        Assert.Equal((uint)MapEditSession.MaxObjectData, tile.Metadata);
        Assert.Equal(0xFF, tile.Quantity2);
    }

    [Fact]
    public void AnEmptyTileTakesNoDataWord()
    {
        MapEditSession session = CreateSession();

        Assert.Equal(0, session.ObjectData(new Point(2, 2)));
        Assert.False(session.SetObjectData(new Point(2, 2), 7));
        Assert.False(session.IsDirty);
        Assert.False(session.SetObjectData(null, 7));
        Assert.False(session.SetObjectData(new Point(99, 99), 7));

        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetObjectData(new Point(2, 2), -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.SetObjectData(new Point(2, 2), MapEditSession.MaxObjectData + 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ObjectData(new Point(99, 99)));
    }

    [Fact]
    public void TheEditorKnowsWhichPresetATileShows()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart);

        session.SelectObject(preset);
        session.PlaceObject(new Point(1, 1));

        Assert.Same(preset, Mp2ObjectPresets.Match(session.Map.Tile(1, 1)));

        // The data word is not part of the identity of an object, so the name survives a change.
        session.SetObjectData(new Point(1, 1), 99);
        Assert.Same(preset, Mp2ObjectPresets.Match(session.Map.Tile(1, 1)));

        // A tile of the bare map shows no preset.
        Assert.Null(Mp2ObjectPresets.Match(session.Map.Tile(0, 0)));
    }

    [Fact]
    public void APlacedAddonMakesTheSaveWriteEverySection()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"fh2-editor-object-{Guid.NewGuid():N}.mp2");

        try
        {
            MapEditSession session = CreateSessionOn(
                Mp2Map.CreateBlank(36, MapGrounds.StartImageIndex(MapGround.Grass)));

            Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.AddonCount > 0);
            session.SelectObject(preset);
            Assert.True(session.PlaceObject(new Point(2, 3)));
            Assert.True(session.SaveAs(filePath, out string? error), error);

            Mp2Map saved = Mp2Map.Read(filePath);

            Assert.Equal(preset.Type, saved.Tile(2, 3).ObjectType);
            Assert.Equal(preset.AddonCount, saved.EnumerateAddonChain(saved.Tile(2, 3)).Count());

            // The second save patches again: the add-ons are in the file now.
            session.Paint(new Point(0, 0));
            Assert.True(session.Save(out string? secondError), secondError);
            Assert.Equal(filePath, session.SavePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>Creates a session around an already built map, the way the host does.</summary>
    private static MapEditSession CreateSessionOn(Mp2Map map)
    {
        TerrainTileSet tiles = SyntheticTerrain.Create(4, 32);

        return new MapEditSession(new MapEditorView(new AdventureMapView(map, tiles, new Size(96, 96))));
    }

    /// <summary>Creates a session whose map pretends to have been read from <paramref name="filePath"/>.</summary>
    private static MapEditSession CreateSessionFrom(string filePath)
    {
        Mp2Map template = SyntheticMap.Create(4, 4, _ => Tile(Grass));
        Mp2Map map = template with { Info = template.Info with { FilePath = filePath } };

        TerrainTileSet tiles = SyntheticTerrain.Create(4, 32);

        return new MapEditSession(new MapEditorView(new AdventureMapView(map, tiles, new Size(96, 96))));
    }

    /// <summary>
    /// A file image with room for a 4x4 tile area and the add-on count: enough for the writer to patch,
    /// and deliberately not a readable map, because the session only needs something to patch.
    /// </summary>
    private static string CreateBlankMapFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"fh2-edit-{Guid.NewGuid():N}.mp2");
        int length = Mp2FileInfo.HeaderSize + (4 * 4 * Mp2TileInfo.StructureSize) + Mp2Map.AddonCountSize;

        File.WriteAllBytes(filePath, new byte[length]);
        return filePath;
    }

    private static MapEditSession CreateSession(ushort terrainImageIndex = Grass)
    {
        Mp2Map map = SyntheticMap.Create(4, 4, _ => Tile(terrainImageIndex));
        TerrainTileSet tiles = SyntheticTerrain.Create(4, 32);

        return new MapEditSession(new MapEditorView(new AdventureMapView(map, tiles, new Size(96, 96))));
    }

    /// <summary>Reads the ground of one tile back, the way the panel names it.</summary>
    private static MapGround GroundAt(MapEditSession session, int x, int y) =>
        MapGrounds.FromTerrainImageIndex(session.Map.Tile(x, y).TerrainImageIndex);

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

