using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The detail instrument of the editor panel: its three modes and the move and copy they make
/// (<c>editor/editor_interface_panel.h:269-278</c>, <c>editor_interface.cpp:1675-1780</c>).
/// </summary>
public sealed class EditorDetailTests
{
    [Fact]
    public void TheModesAreTheOnesOfTheReference()
    {
        // 数字是参考的：面板的三格按数字排，右手边的图标与它选中的模式不能各自重排。
        Assert.Equal(0, (int)EditorDetailMode.Editing);
        Assert.Equal(1, (int)EditorDetailMode.Moving);
        Assert.Equal(2, (int)EditorDetailMode.Copying);

        Assert.Equal(
            [EditorDetailMode.Editing, EditorDetailMode.Moving, EditorDetailMode.Copying],
            EditorDetailModes.All);

        Assert.Equal(0, EditorDetailModes.IndexOf(EditorDetailMode.Editing));
        Assert.Equal(2, EditorDetailModes.IndexOf(EditorDetailMode.Copying));
        Assert.Equal(-1, EditorDetailModes.IndexOf((EditorDetailMode)3));
    }

    [Fact]
    public void TheNamesAreTheOnesOfTheReference()
    {
        Assert.Equal("Edit", EditorDetailModes.Name(EditorDetailMode.Editing));
        Assert.Equal("Move", EditorDetailModes.Name(EditorDetailMode.Moving));
        Assert.Equal("Copy", EditorDetailModes.Name(EditorDetailMode.Copying));
        Assert.Equal("Unknown detail mode", EditorDetailModes.Name((EditorDetailMode)3));

        Assert.Equal("Edit objects on the Adventure Map.", EditorDetailModes.Description(EditorDetailMode.Editing));
        Assert.Equal("Move objects on the Adventure Map.", EditorDetailModes.Description(EditorDetailMode.Moving));
        Assert.Equal("Copy objects on the Adventure Map.", EditorDetailModes.Description(EditorDetailMode.Copying));
        Assert.Equal(string.Empty, EditorDetailModes.Description((EditorDetailMode)3));
    }

    [Fact]
    public void TheInstrumentStartsWithTheEditingMode()
    {
        MapEditSession session = CreateSession();

        Assert.Equal(EditorDetailMode.Editing, session.DetailMode);
        Assert.Null(session.DetailSource);

        Assert.True(session.SetDetailMode(EditorDetailMode.Copying));
        Assert.Equal(EditorDetailMode.Copying, session.DetailMode);

        // 选中同一个模式不算改动，面板不必重画。
        Assert.False(session.SetDetailMode(EditorDetailMode.Copying));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetDetailMode((EditorDetailMode)3));
    }

    [Fact]
    public void TheDetailInstrumentPaintsNothing()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.Detail);
        Assert.False(session.Apply(new Point(1, 1)));
        Assert.Equal(MapGrounds.GrassStartImageIndex, session.Map.Tile(1, 1).TerrainImageIndex);
        Assert.Equal(0, session.UndoDepth);
    }

    [Fact]
    public void AStripOfWaterOrGroundIsNotAHeldObject()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.Detail);
        session.SetDetailMode(EditorDetailMode.Moving);

        // 空地上按下没有东西可搬，松开也不该改编地图。
        Assert.False(session.GrabObject(new Point(1, 1)));
        Assert.Null(session.DetailSource);
        Assert.False(session.ReleaseObject(new Point(2, 2)));
        Assert.Equal(0, session.UndoDepth);

        // 地图外同样不算。
        Assert.False(session.GrabObject(new Point(-1, 0)));
        Assert.False(session.GrabObject(null));
    }

    [Fact]
    public void SwitchingToolsDropsTheHeldObject()
    {
        MapEditSession session = CreateSession();

        session.SelectObject(PlaceablePreset(ObjectGroup.LandscapeTrees));
        Assert.True(session.PlaceObject(new Point(1, 1)));

        session.SetTool(EditorTool.Detail);
        Assert.True(session.GrabObject(new Point(1, 1)));
        Assert.Equal(new Point(1, 1), session.DetailSource);

        // 按着的时候换了工具：那次按下作废，松开落不了子。
        Assert.True(session.SetTool(EditorTool.Terrain));
        Assert.Null(session.DetailSource);
        Assert.False(session.ReleaseObject(new Point(2, 2)));
        Assert.True(session.Map.Tile(1, 1).HasGroundObject);

        // 换模式也一样。
        session.SetTool(EditorTool.Detail);
        Assert.True(session.GrabObject(new Point(1, 1)));
        Assert.True(session.SetDetailMode(EditorDetailMode.Copying));
        Assert.Null(session.DetailSource);
    }

    [Fact]
    public void AMoveCarriesTheObjectAndItsAddOns()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = PlaceablePresetWithAddOns();

        session.SelectObject(preset);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        Mp2TileInfo before = session.Map.Tile(1, 1);
        int addons = session.Map.Addons.Count;
        int chain = session.Map.EnumerateAddonChain(before).Count();

        Assert.True(chain > 0);

        session.SetTool(EditorTool.Detail);
        session.SetDetailMode(EditorDetailMode.Moving);
        Assert.True(session.GrabObject(new Point(1, 1)));
        Assert.True(session.ReleaseObject(new Point(2, 2)));

        Mp2TileInfo moved = session.Map.Tile(2, 2);

        // 两个部件的类型与图号、层、数量、物件类型都跟着走，UID 也还是原来那个：搬的是同一件东西。
        Assert.Equal(before.ObjectName1, moved.ObjectName1);
        Assert.Equal(before.BottomIcnImageIndex, moved.BottomIcnImageIndex);
        Assert.Equal(before.Quantity1, moved.Quantity1);
        Assert.Equal(before.Quantity2, moved.Quantity2);
        Assert.Equal(before.ObjectName2, moved.ObjectName2);
        Assert.Equal(before.TopIcnImageIndex, moved.TopIcnImageIndex);
        Assert.Equal(before.ObjectType, moved.ObjectType);
        Assert.Equal(before.Level1ObjectUid, moved.Level1ObjectUid);
        Assert.Equal(before.Level2ObjectUid, moved.Level2ObjectUid);

        // 附加链不复制：目标格直接指向原来那条链，整条链一张面片都没多。
        Assert.Equal(before.NextAddonIndex, moved.NextAddonIndex);
        Assert.Equal(chain, session.Map.EnumerateAddonChain(moved).Count());
        Assert.Equal(addons, session.Map.Addons.Count);

        // 地形不动。
        Assert.Equal(before.TerrainImageIndex, moved.TerrainImageIndex);
        Assert.Equal(before.TerrainFlags, moved.TerrainFlags);

        // 源格清成空件，地形留着。
        Mp2TileInfo source = session.Map.Tile(1, 1);

        Assert.False(source.HasGroundObject);
        Assert.False(source.HasTopObject);
        Assert.Equal(Mp2ObjectType.None, source.ObjectType);
        Assert.Equal(0u, source.Level1ObjectUid);
        Assert.Equal(0u, source.Level2ObjectUid);
        Assert.Equal(0, source.NextAddonIndex);
        Assert.Equal(Mp2TileInfo.EmptyImageIndex, source.BottomIcnImageIndex);
        Assert.Equal(Mp2TileInfo.EmptyImageIndex, source.TopIcnImageIndex);
        Assert.Equal(before.TerrainImageIndex, source.TerrainImageIndex);
    }

    [Fact]
    public void AMoveIsOneStrokeOfHistory()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = PlaceablePreset(ObjectGroup.LandscapeTrees);

        session.SelectObject(preset);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        int depth = session.UndoDepth;

        session.SetTool(EditorTool.Detail);
        session.SetDetailMode(EditorDetailMode.Moving);
        Assert.True(session.GrabObject(new Point(1, 1)));
        Assert.True(session.ReleaseObject(new Point(2, 2)));

        // 两张格子（源格与目标格）同属一笔编辑，一次撤销就把两格都还原。
        Assert.Equal(depth + 1, session.UndoDepth);
        Assert.True(session.Undo());

        Assert.True(session.Map.Tile(1, 1).HasGroundObject);
        Assert.False(session.Map.Tile(2, 2).HasGroundObject);
        Assert.Equal(depth, session.UndoDepth);
    }

    [Fact]
    public void ACopyKeepsTheSourceAndGivesTheObjectNewUids()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = PlaceablePresetWithAddOns();

        session.SelectObject(preset);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        Mp2TileInfo source = session.Map.Tile(1, 1);
        int addons = session.Map.Addons.Count;
        int chain = session.Map.EnumerateAddonChain(source).Count();

        session.SetTool(EditorTool.Detail);
        session.SetDetailMode(EditorDetailMode.Copying);
        Assert.True(session.GrabObject(new Point(1, 1)));
        Assert.True(session.ReleaseObject(new Point(2, 2)));

        Mp2TileInfo copied = session.Map.Tile(2, 2);

        Assert.Equal(source.ObjectName1, copied.ObjectName1);
        Assert.Equal(source.ObjectType, copied.ObjectType);

        // 复制出来的是一件新东西：UID 不同，附加链整条复制一份。
        Assert.NotEqual(source.Level1ObjectUid, copied.Level1ObjectUid);
        Assert.NotEqual(source.NextAddonIndex, copied.NextAddonIndex);
        Assert.Equal(chain, session.Map.EnumerateAddonChain(copied).Count());
        Assert.Equal(addons + chain, session.Map.Addons.Count);

        // 源格原样留着，连它那条链的首地址都没变。
        Mp2TileInfo after = session.Map.Tile(1, 1);

        Assert.True(after.HasGroundObject);
        Assert.Equal(source.Level1ObjectUid, after.Level1ObjectUid);
        Assert.Equal(source.NextAddonIndex, after.NextAddonIndex);
    }

    [Fact]
    public void TheSameObjectIsNotMovedOrCopiedOntoItself()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = PlaceablePreset(ObjectGroup.LandscapeTrees);

        session.SelectObject(preset);
        Assert.True(session.PlaceObject(new Point(1, 1)));
        Assert.True(session.PlaceObject(new Point(2, 2)));

        int depth = session.UndoDepth;

        session.SetTool(EditorTool.Detail);
        session.SetDetailMode(EditorDetailMode.Moving);
        Assert.True(session.GrabObject(new Point(1, 1)));

        // 目标格已经画着同样的东西：参考提示 "The same object exists on the tile." 并放弃这一步。
        Assert.False(session.ReleaseObject(new Point(2, 2)));
        Assert.Equal(depth, session.UndoDepth);
        Assert.True(session.Map.Tile(1, 1).HasGroundObject);
        Assert.Null(session.DetailSource);

        // 拖回原格同样是空操作。
        session.SetDetailMode(EditorDetailMode.Copying);
        Assert.True(session.GrabObject(new Point(1, 1)));
        Assert.False(session.ReleaseObject(new Point(1, 1)));
        Assert.Equal(depth, session.UndoDepth);
    }

    [Fact]
    public void ADropOutsideTheMapChangesNothing()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = PlaceablePreset(ObjectGroup.LandscapeTrees);

        session.SelectObject(preset);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        int depth = session.UndoDepth;

        session.SetTool(EditorTool.Detail);
        session.SetDetailMode(EditorDetailMode.Moving);
        Assert.True(session.GrabObject(new Point(1, 1)));

        // 拖到地图外松开：物件还在原处，历史不动。
        Assert.False(session.ReleaseObject(new Point(9, 9)));
        Assert.False(session.ReleaseObject(null));
        Assert.True(session.Map.Tile(1, 1).HasGroundObject);
        Assert.Equal(depth, session.UndoDepth);
    }

    private static Mp2ObjectPreset PlaceablePreset(ObjectGroup group) =>
        MapObjectCatalogPresets
            .For(group)
            .First(preset => preset.HasGroundPart && MapObjectCatalog.GetGroundGroup(preset.Template) == group);

    /// <summary>
    /// A preset whose object is wider than one tile, so that placing it builds an add-on chain: the
    /// parts of the other cells live there.
    /// </summary>
    /// <returns>A preset with at least one add-on.</returns>
    private static Mp2ObjectPreset PlaceablePresetWithAddOns() =>
        MapObjectCatalogPresets
            .For(ObjectGroup.LandscapeMountains)
            .First(preset => preset.HasGroundPart && preset.Addons.Count > 0);

    private static MapEditSession CreateSession()
    {
        Mp2Map map = SyntheticMap.Create(4, 4, _ => new Mp2TileInfo
        {
            TerrainImageIndex = MapGrounds.GrassStartImageIndex,
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
        });

        TerrainTileSet tiles = SyntheticTerrain.Create(4, 32);

        return new MapEditSession(new MapEditorView(new AdventureMapView(map, tiles, new Size(96, 96))));
    }
}
