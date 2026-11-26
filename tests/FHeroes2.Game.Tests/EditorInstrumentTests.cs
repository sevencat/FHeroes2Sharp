using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The nine instruments of the editor panel and the two level object page under them
/// (<c>editor/editor_interface_panel.h:171-235</c>).
/// </summary>
public sealed class EditorInstrumentTests
{
    [Fact]
    public void TheInstrumentOrderIsTheOrderOfTheArtwork()
    {
        // 顺序对应 EDITBTNS 里的图标，重排会让面板上的按钮与它选中的仪器错位。
        Assert.Equal(0, (int)EditorTool.Terrain);
        Assert.Equal(1, (int)EditorTool.LandscapeObjects);
        Assert.Equal(2, (int)EditorTool.Detail);
        Assert.Equal(3, (int)EditorTool.AdventureObjects);
        Assert.Equal(4, (int)EditorTool.KingdomObjects);
        Assert.Equal(5, (int)EditorTool.Monsters);
        Assert.Equal(6, (int)EditorTool.Stream);
        Assert.Equal(7, (int)EditorTool.Road);
        Assert.Equal(8, (int)EditorTool.Erase);
    }

    [Fact]
    public void EveryBrushFamilyMatchesTheReferenceGroups()
    {
        Assert.Equal(5, EditorObjectBrushes.Landscape.Count);
        Assert.Equal(7, EditorObjectBrushes.Adventure.Count);
        Assert.Equal(2, EditorObjectBrushes.Kingdom.Count);
        Assert.Single(EditorObjectBrushes.Monsters);

        Assert.Equal(ObjectGroup.LandscapeMountains, EditorObjectBrushes.Landscape[0]);
        Assert.Equal(ObjectGroup.LandscapeMiscellaneous, EditorObjectBrushes.Landscape[^1]);
        Assert.Equal(ObjectGroup.AdventureArtifacts, EditorObjectBrushes.Adventure[0]);
        Assert.Equal(ObjectGroup.AdventureMiscellaneous, EditorObjectBrushes.Adventure[^1]);
        Assert.Equal(ObjectGroup.KingdomHeroes, EditorObjectBrushes.Kingdom[0]);
        Assert.Equal(ObjectGroup.KingdomTowns, EditorObjectBrushes.Kingdom[^1]);
    }

    [Fact]
    public void OnlyTheFourObjectInstrumentsOfferBrushes()
    {
        Assert.True(EditorObjectBrushes.IsObjectMode(EditorTool.LandscapeObjects));
        Assert.True(EditorObjectBrushes.IsObjectMode(EditorTool.AdventureObjects));
        Assert.True(EditorObjectBrushes.IsObjectMode(EditorTool.KingdomObjects));
        Assert.True(EditorObjectBrushes.IsObjectMode(EditorTool.Monsters));

        Assert.False(EditorObjectBrushes.IsObjectMode(EditorTool.Terrain));
        Assert.False(EditorObjectBrushes.IsObjectMode(EditorTool.Detail));
        Assert.False(EditorObjectBrushes.IsObjectMode(EditorTool.Stream));
        Assert.False(EditorObjectBrushes.IsObjectMode(EditorTool.Road));
        Assert.False(EditorObjectBrushes.IsObjectMode(EditorTool.Erase));

        Assert.Empty(EditorObjectBrushes.For(EditorTool.Terrain));
        Assert.Empty(EditorObjectBrushes.For(EditorTool.Road));
    }

    [Fact]
    public void TheObjectsOfAGroupAreTheObjectsOfTheCatalogue()
    {
        // 每一个能搭出模板的目录条目都该出现在页面上，且顺序不变。
        IReadOnlyList<Mp2ObjectPreset> trees = MapObjectCatalogPresets.For(ObjectGroup.LandscapeTrees);

        Assert.NotEmpty(trees);
        Assert.All(trees, preset => Assert.False(preset.Template.ObjectName1 == 0 && preset.Template.ObjectName2 == 0));

        // 缓存：同一个分组问两次必须是同一个表，否则每次画面板都要重搭几百个模板。
        Assert.Same(trees, MapObjectCatalogPresets.For(ObjectGroup.LandscapeTrees));
    }

    [Fact]
    public void TheCatalogueTemplateCarriesTheObjectType()
    {
        IReadOnlyList<Mp2ObjectPreset> mountains = MapObjectCatalogPresets.For(ObjectGroup.LandscapeMountains);

        Assert.NotEmpty(mountains);
        Assert.Equal(Mp2ObjectType.Mountains, mountains[0].Type);
    }

    [Fact]
    public void APickedKindEmptiesThePageBelowIt()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.AdventureObjects);

        Assert.Equal(7, session.BrushKinds.Count);
        Assert.Equal(-1, session.BrushKind);
        Assert.Empty(session.BrushObjects);
        Assert.Null(session.SelectedBrushObject);

        Assert.True(session.SelectBrushKind(2));

        // 选类别会清掉下一层：旧条目属于别的组，留着就会「页面显示矿、落子却是宝物」。
        Assert.True(session.BrushKind >= 0);
        Assert.Equal(-1, session.BrushObjectIndex);
    }

    [Fact]
    public void PickingAnObjectMakesItTheBrush()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.LandscapeObjects);
        Assert.True(session.SelectBrushKind(0));

        IReadOnlyList<Mp2ObjectPreset> page = session.BrushObjects;

        Assert.NotEmpty(page);
        Assert.True(session.SelectBrushObject(0));
        Assert.Same(page[0], session.SelectedBrushObject);
        Assert.Same(page[0], session.ObjectBrush);
    }

    [Fact]
    public void AnOutOfRangePickChangesNothing()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.LandscapeObjects);

        Assert.False(session.SelectBrushKind(-1));
        Assert.False(session.SelectBrushKind(99));
        Assert.False(session.SelectBrushObject(0));

        session.SelectBrushKind(0);

        Assert.False(session.SelectBrushObject(-1));
        Assert.False(session.SelectBrushObject(99_999));
    }

    [Fact]
    public void EachBrushKeepsItsOwnPick()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.LandscapeObjects);
        session.SelectBrushKind(2);
        session.SelectBrushObject(1);

        session.SetTool(EditorTool.AdventureObjects);

        // 从地形物件跳到冒险物件，后者还是空的，前者的选择还在。
        Assert.Equal(-1, session.BrushKind);
        Assert.Empty(session.BrushObjects);

        session.SetTool(EditorTool.LandscapeObjects);

        Assert.Equal(2, session.BrushKind);
        Assert.Equal(1, session.BrushObjectIndex);
    }

    [Fact]
    public void PlacementUsesThePickedObjectOfAnyFamily()
    {
        MapEditSession session = CreateSession();

        session.SetTool(EditorTool.LandscapeObjects);
        session.SelectBrushKind(2);
        session.SelectBrushObject(0);

        Mp2ObjectPreset chosen = session.SelectedBrushObject!;

        Assert.True(session.PlaceObject(new Point(1, 1)));
        Assert.Equal(chosen.Type, session.Map.Tile(1, 1).ObjectType);
    }

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

