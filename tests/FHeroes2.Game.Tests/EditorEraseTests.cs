using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The eleven erasure types of the editor panel and the eraser that honours them
/// (<c>editor/editor_interface_panel.h:249-292</c>, <c>editor_interface_panel.cpp:268-320</c>).
/// </summary>
public sealed class EditorEraseTests
{
    [Fact]
    public void TheErasureTypesAreTheBitsOfTheReference()
    {
        // 位值是参考的，不能重排：面板把勾选状态当成位掩码用。
        Assert.Equal(0u, (uint)EditorEraseType.None);
        Assert.Equal(1u << 0, (uint)EditorEraseType.Mountains);
        Assert.Equal(1u << 1, (uint)EditorEraseType.Rocks);
        Assert.Equal(1u << 2, (uint)EditorEraseType.Trees);
        Assert.Equal(1u << 3, (uint)EditorEraseType.Landscape);
        Assert.Equal(1u << 4, (uint)EditorEraseType.AdventureNonPickable);
        Assert.Equal(1u << 5, (uint)EditorEraseType.Towns);
        Assert.Equal(1u << 6, (uint)EditorEraseType.AdventurePickable);
        Assert.Equal(1u << 7, (uint)EditorEraseType.Monsters);
        Assert.Equal(1u << 8, (uint)EditorEraseType.Heroes);
        Assert.Equal(1u << 9, (uint)EditorEraseType.Streams);
        Assert.Equal(1u << 10, (uint)EditorEraseType.Roads);

        // 十一位全开，再多一位就不是类别了。
        Assert.Equal((1u << 11) - 1, (uint)EditorEraseType.AllObjects);
    }

    [Fact]
    public void TheErasePageIsInTheOrderOfTheArtwork()
    {
        Assert.Equal(11, EditorEraseTypes.Buttons.Count);
        Assert.Equal(EditorEraseType.Mountains, EditorEraseTypes.Buttons[0]);
        Assert.Equal(EditorEraseType.AdventureNonPickable, EditorEraseTypes.Buttons[4]);
        Assert.Equal(EditorEraseType.AdventurePickable, EditorEraseTypes.Buttons[6]);
        Assert.Equal(EditorEraseType.Roads, EditorEraseTypes.Buttons[9]);
        Assert.Equal(EditorEraseType.Streams, EditorEraseTypes.Buttons[^1]);

        Assert.All(EditorEraseTypes.Buttons, button => Assert.True(EditorEraseTypes.IsButton(button)));
        Assert.False(EditorEraseTypes.IsButton(EditorEraseType.None));
        Assert.False(EditorEraseTypes.IsButton(EditorEraseType.Trees | EditorEraseType.Rocks));
    }

    [Fact]
    public void EachErasureTypeCoversTheGroupsOfTheReference()
    {
        Assert.Empty(EditorEraseTypes.Groups(EditorEraseType.None));

        Assert.Equal(
            [ObjectGroup.LandscapeMountains],
            EditorEraseTypes.Groups(EditorEraseType.Mountains).Order());

        // 一位不止一个分组：地形杂物与水面地形共用一个按钮，五种冒险物件共用另一个。
        Assert.Equal(
            [ObjectGroup.LandscapeWater, ObjectGroup.LandscapeMiscellaneous],
            EditorEraseTypes.Groups(EditorEraseType.Landscape).Order());

        Assert.Equal(
            [
                ObjectGroup.AdventureDwellings,
                ObjectGroup.AdventureMines,
                ObjectGroup.AdventurePowerUps,
                ObjectGroup.AdventureWater,
                ObjectGroup.AdventureMiscellaneous,
            ],
            EditorEraseTypes.Groups(EditorEraseType.AdventureNonPickable).Order());

        Assert.Equal(
            [ObjectGroup.AdventureArtifacts, ObjectGroup.AdventureTreasures, ObjectGroup.AdventureWater],
            EditorEraseTypes.Groups(EditorEraseType.AdventurePickable).Order());

        Assert.Equal(
            [ObjectGroup.KingdomTowns],
            EditorEraseTypes.Groups(EditorEraseType.Towns).Order());

        Assert.Equal(
            [ObjectGroup.KingdomHeroes],
            EditorEraseTypes.Groups(EditorEraseType.Heroes).Order());

        Assert.Equal([ObjectGroup.Monsters], EditorEraseTypes.Groups(EditorEraseType.Monsters).Order());
        Assert.Equal([ObjectGroup.Roads], EditorEraseTypes.Groups(EditorEraseType.Roads).Order());
        Assert.Equal([ObjectGroup.Streams], EditorEraseTypes.Groups(EditorEraseType.Streams).Order());

        // 全部打开：十七个分组，重叠的冒险水面只算一次。
        Assert.Equal(17, EditorEraseTypes.Groups(EditorEraseType.AllObjects).Count);
    }

    [Fact]
    public void TheNamesAreTheOnesOfTheReference()
    {
        Assert.Equal("Mountains", EditorEraseTypes.Name(EditorEraseType.Mountains));
        Assert.Equal("Castles", EditorEraseTypes.Name(EditorEraseType.Towns));
        Assert.Equal("Streams", EditorEraseTypes.Name(EditorEraseType.Streams));
        Assert.Equal("Unknown object type", EditorEraseTypes.Name(EditorEraseType.None));
        Assert.Equal("Unknown object type", EditorEraseTypes.Name(EditorEraseType.Roads | EditorEraseType.Trees));
    }

    [Fact]
    public void TheEraserStartsWithEveryTypeChecked()
    {
        MapEditSession session = CreateSession();

        Assert.Equal(EditorEraseType.AllObjects, session.EraseTypes);
        Assert.Equal(EditorEraseType.AllObjects, EditorEraseType.Mountains | EditorEraseType.Rocks
            | EditorEraseType.Trees | EditorEraseType.Landscape | EditorEraseType.AdventureNonPickable
            | EditorEraseType.Towns | EditorEraseType.AdventurePickable | EditorEraseType.Monsters
            | EditorEraseType.Heroes | EditorEraseType.Streams | EditorEraseType.Roads);
    }

    [Fact]
    public void TogglingATypeFlipsJustThatBit()
    {
        MapEditSession session = CreateSession();

        Assert.True(session.ToggleEraseType(EditorEraseType.Roads));
        Assert.Equal(EditorEraseType.AllObjects & ~EditorEraseType.Roads, session.EraseTypes);
        Assert.False(session.EraseTypes.HasFlag(EditorEraseType.Roads));

        Assert.True(session.ToggleEraseType(EditorEraseType.Roads));
        Assert.Equal(EditorEraseType.AllObjects, session.EraseTypes);

        // 一个格子只翻一位：整组位掩码与 NONE 都不是「某一类」。
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ToggleEraseType(EditorEraseType.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.ToggleEraseType(EditorEraseType.Trees | EditorEraseType.Rocks));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.SetEraseTypes((EditorEraseType)(1u << 11)));
    }

    [Fact]
    public void AnEraserWithNothingCheckedDeletesNothing()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset preset = PlaceablePreset(ObjectGroup.LandscapeTrees);

        session.SelectObject(preset);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        session.SetTool(EditorTool.Erase);
        Assert.True(session.SetEraseTypes(EditorEraseType.None));
        Assert.False(session.EraseObject(new Point(1, 1)));
        Assert.True(session.Map.Tile(1, 1).HasGroundObject);
    }

    [Fact]
    public void OnlyTheCheckedTypesAreErased()
    {
        MapEditSession session = CreateSession();
        Mp2ObjectPreset tree = PlaceablePreset(ObjectGroup.LandscapeTrees);

        session.SelectObject(tree);
        Assert.True(session.PlaceObject(new Point(1, 1)));

        session.SetTool(EditorTool.Erase);

        // 只勾路：树留着，橡皮这一步什么也没改（因此不进撤销历史）。
        Assert.True(session.SetEraseTypes(EditorEraseType.Roads));
        int depth = session.UndoDepth;

        Assert.False(session.EraseObject(new Point(1, 1)));
        Assert.True(session.Map.Tile(1, 1).HasGroundObject);
        Assert.Equal(depth, session.UndoDepth);

        // 勾上树：同一格这次被清掉。
        Assert.True(session.SetEraseTypes(EditorEraseType.Trees));
        Assert.True(session.EraseObject(new Point(1, 1)));
        Assert.False(session.Map.Tile(1, 1).HasGroundObject);
    }

    [Fact]
    public void TheEraserLeavesThePartOfAnotherTypeAlone()
    {
        MapEditSession session = CreateSession();

        Mp2ObjectPreset ground = PlaceablePreset(ObjectGroup.LandscapeTrees);

        // 参考的编辑器模板只落地面层，顶层部件都在附加链里，所以顶层这一半从目录里直接取一个真实的面片。
        ObjectPartInfo top = MapObjectCatalog
            .GetObjectsByGroup(ObjectGroup.LandscapeMountains)
            .First(objectInfo => objectInfo.TopLevelParts.Count > 0)
            .TopLevelParts[0];

        // 一格两个部件、两种类别：只清掉勾中的那一半。
        session.Map.SetTile(
            1,
            1,
            session.Map.Tile(1, 1) with
            {
                ObjectName1 = ground.Template.ObjectName1,
                BottomIcnImageIndex = ground.Template.BottomIcnImageIndex,
                Quantity1 = ground.Template.Quantity1,
                ObjectName2 = (byte)((int)top.IcnType << 2),
                TopIcnImageIndex = (byte)top.IcnIndex,
                ObjectType = ground.Type,
                NextAddonIndex = 0,
                Level1ObjectUid = 5,
                Level2ObjectUid = 6,
            });

        Assert.True(session.SetEraseTypes(EditorEraseType.Trees));
        Assert.True(session.EraseObject(new Point(1, 1)));

        Mp2TileInfo after = session.Map.Tile(1, 1);

        Assert.False(after.HasGroundObject);
        Assert.True(after.HasTopObject);
        Assert.Equal(0u, after.Level1ObjectUid);
        Assert.Equal(6u, after.Level2ObjectUid);
        Assert.Equal(0, after.Quantity1);

        // 物件类型按剩下的顶层部件重算，而不是留着一个已经被删掉的地面物件。
        Assert.Equal(top.ObjectType, after.ObjectType);
    }

    [Fact]
    public void AnObjectTheCatalogueDoesNotKnowIsLeftAlone()
    {
        MapEditSession session = CreateSession();

        // 目录里查不到的部件（这里是一格没有精灵的树）没有分组，参考也只删它认得的物件。
        session.Map.SetTile(
            1,
            1,
            session.Map.Tile(1, 1) with
            {
                ObjectName1 = (byte)((int)Mp2ObjectIcnType.TreVil << 2),
                BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
                ObjectType = Mp2ObjectType.Trees,
            });

        Assert.False(session.EraseObject(new Point(1, 1)));
        Assert.True(session.Map.Tile(1, 1).HasGroundObject);
    }

    private static Mp2ObjectPreset PlaceablePreset(ObjectGroup group) =>
        MapObjectCatalogPresets
            .For(group)
            .First(preset => preset.HasGroundPart && MapObjectCatalog.GetGroundGroup(preset.Template) == group);

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
