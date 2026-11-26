using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The object popup of the editor (<c>editor_object_popup_window.cpp:57-267</c>): what the author sees when the tile
/// is described, and the coordinates line under it.
/// </summary>
/// <remarks>
/// 参考用「这一格的主部件去物件表里认领」的办法说出物件是什么，端口照做：用例从同一份物件表里取部件的 ICN 与图片号
/// 摆到格上，再核对文字。数量与归属从元数据表取，缺表时就只剩名字。
/// </remarks>
public sealed class EditorObjectPopupTests
{
    private const uint ObjectUid = 77;

    private static readonly Point Position = new(12, 34);

    [Fact]
    public void ARoadSaysRoadAndAnEmptyTileSaysItsGround()
    {
        MapObjectMetadataStore metadata = new();

        Mp2TileInfo road = EmptyTile with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.Road << 2),
            BottomIcnImageIndex = 2,
            Level1ObjectUid = ObjectUid,
        };

        Assert.Equal("Road\n[12; 34]", EditorObjectPopup.Describe(road, Position, metadata));
        Assert.Equal("Grass\n[12; 34]", EditorObjectPopup.Describe(EmptyTile, Position, metadata));
    }

    [Fact]
    public void AResourcePileTellsHowMuchAndWhich()
    {
        ObjectInfo pile = FirstOf(ObjectGroup.AdventureTreasures);
        MapObjectMetadataStore metadata = new();

        Mp2TileInfo tile = TileOf(pile, Mp2ObjectType.Resource);

        // 没记数量时只报资源名，与参考一致（:96-105）。
        Assert.Equal(
            $"{ResourceText.Of((int)pile.Metadata[0])}\n[12; 34]",
            EditorObjectPopup.Describe(tile, Position, metadata));

        metadata.Resources[ObjectUid] = new ResourceMetadata { Count = 3 };

        Assert.Equal($"3 {ResourceText.Of((int)pile.Metadata[0])}\n[12; 34]", EditorObjectPopup.Describe(tile, Position, metadata));
    }

    [Fact]
    public void AMonsterStackPicksSingularOrPluralByItsCount()
    {
        ObjectInfo stack = FirstOf(ObjectGroup.Monsters);
        MapObjectMetadataStore metadata = new();

        Mp2TileInfo tile = TileOf(stack, Mp2ObjectType.Monster);
        MonsterType monster = (MonsterType)stack.Metadata[0];

        metadata.Monsters[ObjectUid] = new MonsterMetadata { Count = 1 };

        Assert.Equal($"1 {MonsterNames.GetSingular(monster)}\n[12; 34]", EditorObjectPopup.Describe(tile, Position, metadata));

        metadata.Monsters[ObjectUid] = new MonsterMetadata { Count = 4 };

        Assert.Equal($"4 {MonsterNames.GetMulti(monster)}\n[12; 34]", EditorObjectPopup.Describe(tile, Position, metadata));
    }

    [Fact]
    public void AMonsterStackWithoutRecordedCountStillSaysWhatItIs()
    {
        ObjectInfo stack = FirstOf(ObjectGroup.Monsters);
        Mp2TileInfo tile = TileOf(stack, Mp2ObjectType.Monster);
        MonsterType monster = (MonsterType)stack.Metadata[0];

        string expected = $"{Mp2ObjectName.Of(Mp2ObjectType.Monster)}\n{MonsterNames.GetMulti(monster)}\n[12; 34]";

        Assert.Equal(expected, EditorObjectPopup.Describe(tile, Position, new MapObjectMetadataStore()));
    }

    [Fact]
    public void AnArtifactTellsItsName()
    {
        ObjectInfo artifact = FirstOf(ObjectGroup.AdventureArtifacts);
        Mp2TileInfo tile = TileOf(artifact, Mp2ObjectType.Artifact);

        string text = EditorObjectPopup.Describe(tile, Position, new MapObjectMetadataStore());

        // 参考报「Artifact」再一行名字（:118）。
        Assert.Equal(2, text.Split('\n').Length - 1);
        Assert.StartsWith($"{Mp2ObjectName.Of(Mp2ObjectType.Artifact)}\n", text);
    }

    [Fact]
    public void AMineTellsItsKindAndItsOwner()
    {
        // 矿洞的矿种写在背景层的最后一个部件上（:159-160）。
        ObjectInfo mine = FirstOf(ObjectGroup.AdventureMines);
        MapObjectMetadataStore metadata = new();

        Mp2TileInfo tile = TileOf(mine, Mp2ObjectType.Mine, mainPartIsLast: true);
        string name = MineText.GetMineName((int)mine.Metadata[0]);

        Assert.Equal($"{name}\n[12; 34]", EditorObjectPopup.Describe(tile, Position, metadata));

        metadata.CapturableObjects[ObjectUid] = new CapturableObjectMetadata { OwnerColor = PlayerColor.Blue };

        Assert.Equal($"{name}\n({PlayerColor.GetName(PlayerColor.Blue)} player)\n[12; 34]",
            EditorObjectPopup.Describe(tile, Position, metadata));
    }

    [Fact]
    public void ACapturableBuildingKeepsItsNameAndAddsTheOwner()
    {
        ObjectInfo lab = FirstOf(ObjectGroup.AdventureMines, info => Mp2ObjectTypes.NonActionType(info.ObjectType) == Mp2ObjectType.AlchemistLab);
        MapObjectMetadataStore metadata = new();

        Mp2TileInfo tile = TileOf(lab, Mp2ObjectType.AlchemistLab);

        Assert.Equal($"{Mp2ObjectName.Of(Mp2ObjectType.AlchemistLab)}\n[12; 34]",
            EditorObjectPopup.Describe(tile, Position, metadata));

        metadata.CapturableObjects[ObjectUid] = new CapturableObjectMetadata { OwnerColor = PlayerColor.Red };

        Assert.Equal(
            $"{Mp2ObjectName.Of(Mp2ObjectType.AlchemistLab)}\n({PlayerColor.GetName(PlayerColor.Red)} player)\n[12; 34]",
            EditorObjectPopup.Describe(tile, Position, metadata));
    }

    [Fact]
    public void ABarrierAndATentTellTheirColour()
    {
        ObjectInfo barrier = FirstOf(ObjectGroup.AdventureMiscellaneous, info => Mp2ObjectTypes.NonActionType(info.ObjectType) == Mp2ObjectType.Barrier);
        ObjectInfo tent = FirstOf(ObjectGroup.AdventureMiscellaneous, info => Mp2ObjectTypes.NonActionType(info.ObjectType) == Mp2ObjectType.TravellerTent);
        MapObjectMetadataStore metadata = new();

        Mp2TileInfo barrierTile = TileOf(barrier, Mp2ObjectType.Barrier);

        Assert.Equal($"{BarrierColors.GetBarrierName((int)barrier.Metadata[0])} Barrier\n[12; 34]",
            EditorObjectPopup.Describe(barrierTile, Position, metadata));

        Mp2TileInfo tentTile = TileOf(tent, Mp2ObjectType.TravellerTent);

        Assert.Equal($"{BarrierColors.GetTentName((int)tent.Metadata[0])} Tent\n[12; 34]",
            EditorObjectPopup.Describe(tentTile, Position, metadata));
    }

    [Fact]
    public void AnyOtherObjectFallsBackToItsTypeName()
    {
        ObjectInfo tree = FirstOf(ObjectGroup.LandscapeTrees);
        Mp2TileInfo tile = TileOf(tree, tree.ObjectType);

        Assert.Equal($"{Mp2ObjectName.Of(tree.ObjectType)}\n[12; 34]",
            EditorObjectPopup.Describe(tile, Position, new MapObjectMetadataStore()));
    }

    [Fact]
    public void TheMapOverloadDescribesTheTileItIsGiven()
    {
        Mp2Map map = Mp2Map.CreateBlank(36, MapGrounds.GrassStartImageIndex);

        Assert.Equal("Grass\n[3; 4]", EditorObjectPopup.Describe(map, new Point(3, 4), new MapObjectMetadataStore()));
        Assert.Throws<ArgumentNullException>(() => EditorObjectPopup.Describe(map, new Point(3, 4), null!));
        Assert.Throws<ArgumentNullException>(() => EditorObjectPopup.Describe(null!, new Point(3, 4), new MapObjectMetadataStore()));
    }

    /// <summary>A blank grass tile; every case starts from it and swaps in the parts it needs.</summary>
    private static Mp2TileInfo EmptyTile => Mp2Map.CreateBlank(36, MapGrounds.GrassStartImageIndex).Tile(0, 0);

    /// <summary>Places the part of an object on a tile the way the editor's object brushes do.</summary>
    /// <param name="info">The object whose part to place.</param>
    /// <param name="type">The type to write on the tile, which the popup reads back.</param>
    /// <param name="mainPartIsLast">Whether the identifying part is the last ground part, as for mines.</param>
    private static Mp2TileInfo TileOf(ObjectInfo info, Mp2ObjectType type, bool mainPartIsLast = false)
    {
        LayeredObjectPartInfo part = mainPartIsLast ? info.GroundLevelParts[^1] : info.GroundLevelParts[0];

        return EmptyTile with
        {
            ObjectType = type,
            ObjectName1 = (byte)((int)part.IcnType << 2),
            BottomIcnImageIndex = (byte)part.IcnIndex,
            Level1ObjectUid = ObjectUid,
        };
    }

    /// <summary>The first object of a group, or the first one of a wanted type.</summary>
    private static ObjectInfo FirstOf(ObjectGroup group, Func<ObjectInfo, bool>? predicate = null)
    {
        foreach (ObjectInfo info in MapObjectCatalog.GetObjectsByGroup(group))
        {
            if (!info.IsEmpty && (predicate is null || predicate(info)))
            {
                return info;
            }
        }

        throw new InvalidOperationException($"The group {group} holds no such object.");
    }
}
