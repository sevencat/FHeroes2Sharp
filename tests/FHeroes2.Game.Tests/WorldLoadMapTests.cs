using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>World::LoadMapMP2()</c> (<c>src/fheroes2/world/world_loadmap.cpp:141</c>) as far as the port
/// can run it: the tile loop, the object type repairs and the final passability pass.
/// </summary>
public sealed class WorldLoadMapTests
{
    /// <summary>
    /// Width of the synthetic maps. <see cref="Mp2Map.CreateBlank(int, ushort, string)"/> only accepts the
    /// four sizes of the original map editor, so the tests use the smallest one.
    /// </summary>
    private const int MapSize = 36;

    /// <summary>Index of a tile of the synthetic map, <c>x + y * width</c>.</summary>
    /// <param name="x">Column.</param>
    /// <param name="y">Row.</param>
    /// <returns>The tile index.</returns>
    private static int At(int x, int y) => x + (y * MapSize);

    /// <summary>
    /// An empty grass tile: no ground object, no top object, no add-ons.
    /// </summary>
    /// <param name="terrainImageIndex">Terrain image; at or above the grass index means land.</param>
    /// <returns>The tile structure.</returns>
    private static Mp2TileInfo EmptyTile(ushort terrainImageIndex = MapGrounds.GrassStartImageIndex) => new()
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

    /// <summary>
    /// A tile holding one ground object, the way the map file stores it.
    /// </summary>
    /// <param name="icnType">Object set of the sprite.</param>
    /// <param name="icnIndex">Sprite inside the set.</param>
    /// <param name="objectType">Object type the file claims.</param>
    /// <param name="uid">Object UID.</param>
    /// <param name="layer">Layer the part is drawn on.</param>
    /// <returns>The tile structure.</returns>
    private static Mp2TileInfo GroundObjectTile(
        Mp2ObjectIcnType icnType, byte icnIndex, Mp2ObjectType objectType = Mp2ObjectType.None, uint uid = 1,
        ObjectLayerType layer = ObjectLayerType.Object) => EmptyTile() with
        {
            ObjectName1 = (byte)((int)icnType << 2),
            BottomIcnImageIndex = icnIndex,
            Quantity1 = (byte)layer,
            ObjectType = objectType,
            Level1ObjectUid = uid,
        };

    private static Mp2Map BlankMap() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

    [Fact]
    public void LoadMapCopiesTheTilesAndTheirTerrain()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), EmptyTile(MapGrounds.WaterStartImageIndex));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(MapSize, world.Width);
        Assert.Equal(MapSize, world.Height);
        Assert.Equal(MapSize * MapSize, world.Size);

        for (int i = 0; i < world.Size; ++i)
        {
            Assert.Equal(i, world.Tiles[i].Index);
        }

        Assert.True(world.GetTile(10, 10).IsWater);
        Assert.False(world.GetTile(9, 10).IsWater);
    }

    [Fact]
    public void LoadMapBuildsTheGroundAndTopParts()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), EmptyTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            BottomIcnImageIndex = 5,
            Quantity1 = (byte)ObjectLayerType.Object,
            Level1ObjectUid = 7,
            ObjectName2 = (byte)((int)Mp2ObjectIcnType.ObjnRsrc << 2),
            TopIcnImageIndex = 3,
            Level2ObjectUid = 8,
            ObjectType = Mp2ObjectType.ActionResource,
        });

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);

        // The sorting pass promotes the ground part to the main part.
        Assert.Equal(Mp2ObjectIcnType.ObjnCrck, tile.MainObjectPart.IcnType);
        Assert.Equal(5, tile.MainObjectPart.IcnIndex);
        Assert.Equal(7u, tile.MainObjectPart.Uid);
        Assert.Equal(ObjectLayerType.Object, tile.MainObjectPart.LayerType);
        Assert.Empty(tile.GroundObjectParts);

        ObjectPart top = Assert.Single(tile.TopObjectParts);
        Assert.Equal(Mp2ObjectIcnType.ObjnRsrc, top.IcnType);
        Assert.Equal(8u, top.Uid);

        Assert.Equal(Mp2ObjectType.ActionResource, tile.MainObjectType);
    }

    [Fact]
    public void LoadMapBuildsTheAddonChain()
    {
        Mp2Map map = BlankMap();

        int firstAddon = map.AppendAddon(new Mp2AddonInfo
        {
            NextAddonIndex = 0,
            ObjectNameN1 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            BottomIcnImageIndex = 10,
            QuantityN = (byte)ObjectLayerType.Object,
            ObjectNameN2 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            TopIcnImageIndex = 11,
            Level1ObjectUid = 5,
            Level2ObjectUid = 5,
        });

        int secondAddon = map.AppendAddon(new Mp2AddonInfo
        {
            NextAddonIndex = 0,
            ObjectNameN1 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            BottomIcnImageIndex = 12,
            QuantityN = (byte)ObjectLayerType.Background,
            ObjectNameN2 = 0,
            TopIcnImageIndex = 0,
            Level1ObjectUid = 6,
            Level2ObjectUid = 0,
        });

        // The chain is built backwards: the tile points at the first add-on, which points at the second.
        map.SetTileAt(firstAddon - 1, EmptyTile() with { NextAddonIndex = (ushort)firstAddon });
        map.SetAddonAt(firstAddon, map.Addons[firstAddon] with { NextAddonIndex = (ushort)secondAddon });

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(firstAddon - 1);

        // The tile had no object of its own, so the sorting pass promotes one of the add-on parts: the
        // object layer one, which is the lowest layer and therefore the last of the sorted list.
        Assert.Equal(10, tile.MainObjectPart.IcnIndex);
        Assert.Equal(ObjectLayerType.Object, tile.MainObjectPart.LayerType);
        Assert.Equal(5u, tile.MainObjectPart.Uid);

        ObjectPart background = Assert.Single(tile.GroundObjectParts);
        Assert.Equal(12, background.IcnIndex);
        Assert.Equal(ObjectLayerType.Background, background.LayerType);
        Assert.Equal(6u, background.Uid);

        // Only the first add-on has a top part.
        ObjectPart top = Assert.Single(tile.TopObjectParts);
        Assert.Equal(11, top.IcnIndex);
    }

    [Fact]
    public void LoadMapFixesTheReefsThatTheOriginalEditorMarkedAsRocks()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.XLoc2, 113, Mp2ObjectType.Rock));

        var world = new World();
        world.LoadMap(map);

        // X_LOC2 sprite 113 is a reef; the reference repairs the type before the passability is computed.
        Assert.Equal(Mp2ObjectType.Reefs, world.GetTile(10, 10).MainObjectType);
        Assert.Equal(Direction.Unknown, world.GetTile(10, 10).PassableDirections);
    }

    [Fact]
    public void LoadMapFixesTheTypeOfARandomMonsterPlaceholder()
    {
        Mp2Map map = BlankMap();

        // The original editor writes the wrong type for the placeholder; the reference trusts the sprite.
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.Mons32, 67, Mp2ObjectType.Trees));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Mp2ObjectType.ActionRandomMonsterWeak, world.GetTile(10, 10).MainObjectType);
    }

    [Fact]
    public void LoadMapMovesTheOasisTopSpritesToTheTopLayer()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnDsrt, 105, Mp2ObjectType.Oasis, 4));

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);

        // The part belongs to the top level, so it leaves the ground parts and the main part stays empty.
        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        ObjectPart top = Assert.Single(tile.TopObjectParts);
        Assert.Equal(Mp2ObjectIcnType.ObjnDsrt, top.IcnType);
        Assert.Equal(105, top.IcnIndex);
        Assert.Equal(4u, top.Uid);
    }

    [Fact]
    public void LoadMapFixesTheSkeletonOnDesert()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnDsrt, 83, Mp2ObjectType.ActionSkeleton));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Mp2ObjectType.Skeleton, world.GetTile(10, 10).MainObjectType);
    }

    [Fact]
    public void LoadMapClearsTheCoastTypeOfAWaterTile()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(
            At(10, 10), EmptyTile(MapGrounds.WaterStartImageIndex) with { ObjectType = Mp2ObjectType.Coast });

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Mp2ObjectType.None, world.GetTile(10, 10).MainObjectType);
    }

    [Fact]
    public void LoadMapComputesThePassabilityOfEveryTile()
    {
        Mp2Map map = BlankMap();

        // One object spanning two tiles: the upper tile becomes impassable.
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 5));
        map.SetTileAt(At(10, 11), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 5));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Direction.Unknown, world.GetTile(10, 10).PassableDirections);
        Assert.Equal(Directions.All, world.GetTile(9, 10).PassableDirections);
    }

    [Fact]
    public void LoadMapTakesTheUidCounterFromTheFile()
    {
        Mp2Map map = BlankMap() with { LastObjectUid = 3 };

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(3u, world.LastObjectUid);
        Assert.Equal(4u, world.GetNewObjectUid());
        Assert.Equal(4u, world.LastObjectUid);
    }

    [Fact]
    public void LoadMapRaisesTheUidCounterToTheHighestObjectUid()
    {
        // Some official maps store a counter lower than the UIDs of their own objects.
        Mp2Map map = BlankMap() with { LastObjectUid = 3 };
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 42));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(42u, world.LastObjectUid);
        Assert.Equal(43u, world.GetNewObjectUid());
    }

    [Fact]
    public void LoadMapDropsWhateverWasLoadedBefore()
    {
        var world = new World();

        Mp2Map first = BlankMap();
        first.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 9));

        world.LoadMap(first);
        Assert.NotEqual(Mp2ObjectIcnType.Unknown, world.GetTile(10, 10).MainObjectPart.IcnType);

        world.LoadMap(BlankMap());

        Assert.Equal(MapSize, world.Width);
        Assert.Equal(MapSize * MapSize, world.Size);
        Assert.Equal(0u, world.LastObjectUid);
        Assert.Equal(Mp2ObjectIcnType.Unknown, world.GetTile(10, 10).MainObjectPart.IcnType);
        Assert.All(world.Tiles, tile => Assert.Equal(Directions.All, tile.PassableDirections));
    }

    [Fact]
    public void LoadMapRejectsAMapWhoseDimensionsDoNotMatchItsTiles()
    {
        Mp2Map map = BlankMap() with { Info = BlankMap().Info with { Width = MapSize + 1 } };

        var world = new World();

        Assert.Throws<ArgumentException>(() => world.LoadMap(map));
    }

    [Fact]
    public void TryLoadMapRefusesAFileThatIsNotAMap()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-not-a-map-{Guid.NewGuid():N}.mp2");
        File.WriteAllBytes(path, new byte[1024]);

        try
        {
            var world = new World();

            Assert.False(world.TryLoadMap(path));
            Assert.False(world.TryLoadMap(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mp2")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryLoadMapBuildsTheWorldFromARealMap()
    {
        string? dataDirectory = GameData.TryResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        string? mapFile = MapCatalog.FindFiles(MapCatalog.FindDirectories([dataDirectory]))
            .FirstOrDefault(path => Path.GetFileName(path).Equals("Beltway.mp2", StringComparison.OrdinalIgnoreCase));

        if (mapFile is null)
        {
            return;
        }

        var world = new World();

        Assert.True(world.TryLoadMap(mapFile));
        Assert.Equal(72, world.Width);
        Assert.Equal(72 * 72, world.Size);
        Assert.True(world.LastObjectUid > 0);

        for (int i = 0; i < world.Size; ++i)
        {
            Assert.Equal(i, world.Tiles[i].Index);
        }

        // The passability pass may only take bits away: a tile can never become more walkable than its own
        // sprites allow. This is the property the whole neighbour chain exists to preserve.
        Assert.All(world.Tiles, tile =>
            Assert.Equal(Direction.Unknown, tile.PassableDirections & ~tile.GetTileIndependentPassability()));

        // A real map has both: water and mountains that cannot be entered, and open grass that can.
        Assert.Contains(world.Tiles, tile => tile.PassableDirections == Direction.Unknown);
        Assert.Contains(world.Tiles, tile => tile.PassableDirections == Directions.All);
    }
}
