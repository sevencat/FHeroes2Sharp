using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the ported part of <see cref="MapObjectCatalog"/>: every object table, the ICN index and the
/// two tile offset queries. The expected numbers come from the reference
/// (<c>src/fheroes2/maps/map_object_info.cpp</c>) and are pinned so a regeneration of the tables
/// cannot silently drop or reorder objects.
/// </summary>
public sealed class MapObjectCatalogTests
{
    [Fact]
    public void RoadTableHasAllLayouts()
    {
        IReadOnlyList<ObjectInfo> roads = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Roads);

        Assert.Equal(513, roads.Count);
        Assert.All(roads, road => Assert.False(road.IsEmpty));
    }

    [Fact]
    public void RoadsAreTerrainPartsWithoutType()
    {
        IReadOnlyList<ObjectInfo> roads = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Roads);

        Assert.All(roads, road =>
        {
            Assert.Empty(road.TopLevelParts);
            Assert.All(road.GroundLevelParts, part =>
            {
                Assert.Equal(Mp2ObjectIcnType.Road, part.IcnType);
                Assert.Equal(ObjectLayerType.Terrain, part.LayerType);
                Assert.Equal(Mp2ObjectType.None, part.ObjectType);
            });
        });
    }

    [Fact]
    public void FirstRoadLayoutMatchesTheReference()
    {
        ObjectInfo road = MapObjectCatalog.GetObjectInfo(ObjectGroup.Roads, 0);

        Assert.Equal(6, road.GroundLevelParts.Count);
        Assert.Equal(2u, road.GroundLevelParts[0].IcnIndex);
        Assert.Equal(new Point(0, 0), road.GroundLevelParts[0].TileOffset);
        Assert.Equal(8u, road.GroundLevelParts[3].IcnIndex);
        Assert.Equal(new Point(-1, 1), road.GroundLevelParts[3].TileOffset);
    }

    [Fact]
    public void SecondVariantOfALayoutIsACopyNotTheSameInstance()
    {
        IReadOnlyList<ObjectInfo> roads = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Roads);

        Assert.NotSame(roads[0], roads[256]);
        Assert.Equal(6, roads[256].GroundLevelParts.Count);
        Assert.Equal(28u, roads[256].GroundLevelParts[0].IcnIndex);
    }

    [Fact]
    public void LayoutsWithLeftAndBottomLeftReuseTheOnesWithout()
    {
        IReadOnlyList<ObjectInfo> roads = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Roads);

        // The end of populateRoads copies 128-191 over 192-255 for both variants.
        AssertLayoutEquals(roads[128], roads[192]);
        AssertLayoutEquals(roads[256 + 128], roads[256 + 192]);
    }

    [Fact]
    public void CastleEntranceRoadIsTheLastObject()
    {
        ObjectInfo entrance = MapObjectCatalog.GetObjectInfo(ObjectGroup.Roads, 512);

        LayeredObjectPartInfo part = Assert.Single(entrance.GroundLevelParts);
        Assert.Equal(31u, part.IcnIndex);
        Assert.Equal(new Point(0, 0), part.TileOffset);
    }

    [Fact]
    public void StreamsHaveThirteenSprites()
    {
        IReadOnlyList<ObjectInfo> streams = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Streams);

        Assert.Equal(13, streams.Count);

        for (int i = 0; i < streams.Count; ++i)
        {
            LayeredObjectPartInfo part = Assert.Single(streams[i].GroundLevelParts);
            Assert.Equal(Mp2ObjectIcnType.Stream, part.IcnType);
            Assert.Equal((uint)i, part.IcnIndex);
            Assert.Equal(ObjectLayerType.Terrain, part.LayerType);
        }
    }

    [Fact]
    public void TreeTableHasSixtyNineObjects()
    {
        Assert.Equal(69, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeTrees).Count);
    }

    [Fact]
    public void SingleTreeOnGrassHasShadowGroundPartAndCrownAbove()
    {
        // Six forest appearances times six objects, then the three grass objects; the single tree is
        // the last of them.
        ObjectInfo tree = MapObjectCatalog.GetObjectInfo(ObjectGroup.LandscapeTrees, 38);

        Assert.Equal(Mp2ObjectType.Trees, tree.ObjectType);
        Assert.Equal(2, tree.GroundLevelParts.Count);
        Assert.Equal(93u, tree.GroundLevelParts[0].IcnIndex);
        Assert.Equal(ObjectLayerType.Object, tree.GroundLevelParts[0].LayerType);
        Assert.Equal(92u, tree.GroundLevelParts[1].IcnIndex);
        Assert.Equal(ObjectLayerType.Shadow, tree.GroundLevelParts[1].LayerType);

        ObjectPartInfo crown = Assert.Single(tree.TopLevelParts);
        Assert.Equal(91u, crown.IcnIndex);
        Assert.Equal(new Point(0, -1), crown.TileOffset);
    }

    [Fact]
    public void RoadsOccupyNoTileButUseThem()
    {
        ObjectInfo road = MapObjectCatalog.GetObjectInfo(ObjectGroup.Roads, 0);

        // A road is on the terrain layer: it never blocks movement but it does belong to the object.
        Assert.Empty(MapObjectCatalog.GetGroundLevelOccupiedTileOffset(road));
        Assert.Equal(road.GroundLevelParts.Count, MapObjectCatalog.GetGroundLevelUsedTileOffset(road).Count);
    }

    [Fact]
    public void OccupiedTilesSkipTheShadowLayer()
    {
        ObjectInfo tree = MapObjectCatalog.GetObjectInfo(ObjectGroup.LandscapeTrees, 38);

        Assert.Equal(new[] { new Point(0, 0) }, MapObjectCatalog.GetGroundLevelOccupiedTileOffset(tree));
        Assert.Equal(new[] { new Point(0, 0) }, MapObjectCatalog.GetGroundLevelUsedTileOffset(tree));
    }

    [Fact]
    public void PartIndexFindsTheFirstRegisteredPart()
    {
        ObjectPartInfo? road = MapObjectCatalog.GetObjectPartByIcn(Mp2ObjectIcnType.Road, 2);

        Assert.NotNull(road);
        Assert.Equal(Mp2ObjectType.None, road.ObjectType);

        Assert.Equal(Mp2ObjectType.Trees, MapObjectCatalog.GetObjectTypeByIcn(Mp2ObjectIcnType.TreDeci, 5));
        Assert.Null(MapObjectCatalog.GetObjectPartByIcn(Mp2ObjectIcnType.TreDeci, 99));
        Assert.Equal(Mp2ObjectType.None, MapObjectCatalog.GetObjectTypeByIcn(Mp2ObjectIcnType.TreDeci, 99));
    }

    [Fact]
    public void EveryPortedGroupHasTheReferenceNumberOfObjects()
    {
        // Pinned from a run of the port and cross-checked against the reference: the generated tables
        // hold one 'objects.Add' per 'objects.emplace_back' of the reference, so a regeneration that
        // drops or duplicates an object fails here.
        Assert.Equal(513, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Roads).Count);
        Assert.Equal(13, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Streams).Count);
        Assert.Equal(63, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeMountains).Count);
        Assert.Equal(40, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeRocks).Count);
        Assert.Equal(69, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeTrees).Count);
        Assert.Equal(12, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeWater).Count);
        Assert.Equal(147, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeMiscellaneous).Count);
        Assert.Equal(8, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeTownBasements).Count);
        Assert.Equal(14, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeFlags).Count);
        Assert.Equal(25, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureDwellings).Count);
        Assert.Equal(56, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureMines).Count);
        Assert.Equal(23, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventurePowerUps).Count);
        Assert.Equal(13, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureTreasures).Count);
        Assert.Equal(96, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureArtifacts).Count);
        Assert.Equal(13, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureWater).Count);
        Assert.Equal(71, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureMiscellaneous).Count);
        Assert.Equal(42, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.KingdomHeroes).Count);
        Assert.Equal(14, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.KingdomTowns).Count);
        Assert.Equal(71, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Monsters).Count);
        Assert.Equal(7, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.MapExtras).Count);
    }

    [Fact]
    public void EveryPortedObjectHasGroundLevelParts()
    {
        foreach (ObjectGroup group in Enum.GetValues<ObjectGroup>())
        {
            if (group == ObjectGroup.GroupCount) { continue; }

            Assert.All(MapObjectCatalog.GetObjectsByGroup(group), info => Assert.False(info.IsEmpty));
        }
    }

    [Fact]
    public void EveryGroupHasObjects()
    {
        // The reference fills every table, so an empty one means a populate call is missing. NONE is
        // not a table and GROUP_COUNT is the count itself.
        foreach (ObjectGroup group in Enum.GetValues<ObjectGroup>())
        {
            if (group == ObjectGroup.None || group == ObjectGroup.GroupCount) { continue; }

            Assert.NotEmpty(MapObjectCatalog.GetObjectsByGroup(group));
        }
    }

    /// <summary>
    /// Every object the reference builds from an <c>MP2::OBJ_*</c> action constant must be stored with
    /// the action bit. The two exceptions are the castle extras, which the reference builds from
    /// <c>MP2::OBJ_NON_ACTION_CASTLE</c> on purpose (<c>map_object_info.cpp:3779</c> for the basements):
    /// they are pieces of a castle, never a whole object of their own.
    /// </summary>
    [Fact]
    public void ObjectsWithAnActionCounterpartAreStoredWithTheActionType()
    {
        foreach (ObjectGroup group in Enum.GetValues<ObjectGroup>())
        {
            if (group is ObjectGroup.GroupCount or ObjectGroup.LandscapeFlags or ObjectGroup.LandscapeTownBasements)
            {
                continue;
            }

            Assert.All(MapObjectCatalog.GetObjectsByGroup(group), info => Assert.True(
                !Mp2ObjectTypes.CanBeActionObject(info.ObjectType) || Mp2ObjectTypes.IsActionObject(info.ObjectType),
                $"{group}: {info.ObjectType} has an action counterpart but is stored without the action bit."));
        }
    }

    [Fact]
    public void CastleBasementsAndFlagsUseTheNonActionCastleType()
    {
        // The reference names MP2::OBJ_NON_ACTION_CASTLE here: a basement or a flag is a piece of a
        // castle that is placed next to it, not an interactable object.
        Assert.All(MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeTownBasements), info => Assert.Equal(Mp2ObjectType.Castle, info.ObjectType));
        Assert.All(MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeFlags), info => Assert.Equal(Mp2ObjectType.Castle, info.ObjectType));
    }

    [Fact]
    public void MineVariantsAreIndependentCopiesWithTheirOwnResource()
    {
        // populateAdventureMines pushes the same object five times, each time with the resource part
        // swapped for the next one: the copies must not share the mutated object.
        IReadOnlyList<ObjectInfo> mines = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureMines);

        Assert.Equal((uint)Mp2ResourceType.Ore, mines[0].Metadata[0]);
        Assert.Equal((uint)Mp2ResourceType.Sulfur, mines[1].Metadata[0]);
        Assert.Equal((uint)Mp2ResourceType.Crystal, mines[2].Metadata[0]);
        Assert.Equal((uint)Mp2ResourceType.Gems, mines[3].Metadata[0]);
        Assert.Equal((uint)Mp2ResourceType.Gold, mines[4].Metadata[0]);

        Assert.NotSame(mines[0], mines[1]);
        Assert.Equal(mines[0].GroundLevelParts.Count, mines[1].GroundLevelParts.Count);
        Assert.Equal(0u, mines[0].GroundLevelParts[^1].IcnIndex);
        Assert.Equal(1u, mines[1].GroundLevelParts[^1].IcnIndex);
    }

    [Fact]
    public void FlagsComeInSevenColoursAndTwoSides()
    {
        IReadOnlyList<ObjectInfo> flags = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.LandscapeFlags);

        for (int color = 0; color < 7; ++color)
        {
            ObjectInfo left = flags[color * 2];
            ObjectInfo right = flags[color * 2 + 1];

            Assert.Equal((uint)color, left.Metadata[0]);
            Assert.Equal((uint)color, right.Metadata[0]);
            Assert.Equal(new Point(0, 0), left.GroundLevelParts[0].TileOffset);
            Assert.Equal(new Point(0, 0), right.GroundLevelParts[0].TileOffset);
            Assert.Equal((uint)(color * 2), left.GroundLevelParts[0].IcnIndex);
            Assert.Equal((uint)(color * 2 + 1), right.GroundLevelParts[0].IcnIndex);
        }
    }

    [Fact]
    public void ArtifactsFollowTheArtifactIdOrderAndSkipTheMagicBookSlot()
    {
        // populateAdventureArtifacts walks the artifact ids from ARCANE_NECKLACE to ARTIFACT_COUNT, adds
        // the Magic Book in between with a temporary sprite, then the four random ones and the ultimate.
        IReadOnlyList<ObjectInfo> artifacts = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureArtifacts);

        ObjectInfo first = artifacts[0];
        Assert.Equal((uint)ArtifactType.ArcaneNecklace, first.Metadata[0]);
        Assert.Equal((uint)ArtifactType.ArcaneNecklace * 2U - 1U, first.GroundLevelParts[0].IcnIndex);
        Assert.Equal(first.GroundLevelParts[0].IcnIndex - 1U, first.GroundLevelParts[^1].IcnIndex);

        // The Magic Book: an id without an image, so the reference gives it frame 207.
        int magicBook = (int)ArtifactType.MagicBook - (int)ArtifactType.ArcaneNecklace;
        Assert.Equal((uint)ArtifactType.MagicBook, artifacts[magicBook].Metadata[0]);
        Assert.Equal(207U, artifacts[magicBook].GroundLevelParts[0].IcnIndex);

        // The last artifact with an id, and the last one of the Price of Loyalty block.
        ObjectInfo last = artifacts[^6];
        Assert.Equal((uint)ArtifactType.SpadeNecromancy, last.Metadata[0]);
        Assert.Equal((uint)ArtifactType.SpadeNecromancy * 2U - 1U, last.GroundLevelParts[0].IcnIndex);

        // The four random ones carry no artifact id, and the ultimate one has no shadow of its own.
        for (int i = 2; i <= 5; ++i)
        {
            Assert.Equal((uint)ArtifactType.Unknown, artifacts[^i].Metadata[0]);
        }

        Assert.Equal(164U, artifacts[^1].GroundLevelParts[0].IcnIndex);
        Assert.Equal(165U, artifacts[^1].GroundLevelParts[^1].IcnIndex);
    }

    [Fact]
    public void MonstersUseTheMons32FrameOfTheirId()
    {
        // Monster::PEASANT is 1 while ICN::MONS32 starts at PEASANT, hence frame = id - 1.
        IReadOnlyList<ObjectInfo> monsters = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Monsters);

        Assert.Equal((uint)MonsterType.Peasant, monsters[0].Metadata[0]);
        Assert.Equal(0U, monsters[0].GroundLevelParts[0].IcnIndex);
        Assert.Equal(Mp2ObjectIcnType.Mons32, monsters[0].GroundLevelParts[0].IcnType);

        int lastCreature = (int)MonsterType.WaterElement - (int)MonsterType.Peasant;
        Assert.Equal((uint)MonsterType.WaterElement, monsters[lastCreature].Metadata[0]);
        Assert.Equal((uint)MonsterType.WaterElement - 1U, monsters[lastCreature].GroundLevelParts[0].IcnIndex);

        // The five editor placeholders follow the creatures.
        for (int i = 1; i <= 5; ++i)
        {
            Assert.Equal((uint)((int)MonsterType.RandomMonster + i - 1), monsters[lastCreature + i].Metadata[0]);
        }

        Assert.Equal((uint)MonsterType.MonsterCount - (uint)MonsterType.Peasant, (uint)monsters.Count);
    }

    [Fact]
    public void GroupCountIsNotAGroup()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapObjectCatalog.GetObjectsByGroup(ObjectGroup.GroupCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapObjectCatalog.GetObjectInfo(ObjectGroup.Roads, 513));
    }

    /// <summary>Compares two objects part by part, the way the copy loop of the reference would.</summary>
    /// <param name="expected">Object to compare against.</param>
    /// <param name="actual">Object that came out of a copy.</param>
    private static void AssertLayoutEquals(ObjectInfo expected, ObjectInfo actual)
    {
        Assert.Equal(expected.ObjectType, actual.ObjectType);
        Assert.Equal(expected.GroundLevelParts.Count, actual.GroundLevelParts.Count);

        for (int i = 0; i < expected.GroundLevelParts.Count; ++i)
        {
            LayeredObjectPartInfo left = expected.GroundLevelParts[i];
            LayeredObjectPartInfo right = actual.GroundLevelParts[i];

            Assert.Equal(left.IcnType, right.IcnType);
            Assert.Equal(left.IcnIndex, right.IcnIndex);
            Assert.Equal(left.TileOffset, right.TileOffset);
            Assert.Equal(left.LayerType, right.LayerType);
        }
    }
}
