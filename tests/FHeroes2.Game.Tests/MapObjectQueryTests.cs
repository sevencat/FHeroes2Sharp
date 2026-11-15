using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the object queries of <c>maps.cpp</c> — <c>Maps::doesObjectExistOnMap()</c> (<c>:601</c>),
/// <c>Maps::GetObjectPositions()</c> (<c>:613</c>) over <c>MapsIndexesObject()</c> (<c>:65</c>) and
/// <c>Maps::getObjectParts()</c> (<c>:618</c>) — the part lookup
/// <c>getObjectPartByActionType()</c> (<c>maps_tiles_helper.cpp:487</c>) and the three caches that
/// <c>World::PostLoad()</c> fills (<c>world.cpp:1200-1230</c>).
/// </summary>
public sealed class MapObjectQueryTests
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

    private static Mp2Map BlankMap() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

    /// <summary>
    /// A tile holding one ground object, the way the map file stores it.
    /// </summary>
    /// <param name="icnType">Object set of the sprite.</param>
    /// <param name="icnIndex">Sprite inside the set.</param>
    /// <param name="objectType">Object type the file claims.</param>
    /// <param name="uid">Object UID.</param>
    /// <returns>The tile structure.</returns>
    private static Mp2TileInfo ObjectTile(Mp2ObjectIcnType icnType, byte icnIndex, Mp2ObjectType objectType, uint uid = 1) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)icnType << 2),
        BottomIcnImageIndex = icnIndex,
        Quantity1 = (byte)ObjectLayerType.Object,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = objectType,
        NextAddonIndex = 0,
        Level1ObjectUid = uid,
        Level2ObjectUid = 0,
    };

    /// <summary>Loaded world of a blank map with the given tiles in place.</summary>
    /// <param name="tiles">Tile index and tile content.</param>
    /// <returns>The world.</returns>
    private static World LoadWorld(params (int Index, Mp2TileInfo Tile)[] tiles)
    {
        Mp2Map map = BlankMap();

        foreach ((int index, Mp2TileInfo tile) in tiles)
        {
            map.SetTileAt(index, tile);
        }

        var world = new World();
        world.LoadMap(map);

        return world;
    }

    [Fact]
    public void ABlankMapHasNoObjectsAndNoCaches()
    {
        World world = LoadWorld();

        Assert.Empty(world.AllTeleports);
        Assert.Empty(world.AllWhirlpools);
        Assert.Empty(world.AllEyeOfMagi);
        Assert.Empty(world.GetObjectPositions(Mp2ObjectType.ActionStoneLiths));
        Assert.False(world.DoesObjectExistOnMap(Mp2ObjectType.ActionStoneLiths));
    }

    [Fact]
    public void PostLoadGroupsStoneLithsByTheirSprite()
    {
        // Anything else would not be a pair of teleports: two liths only lead to each other when they use
        // the same sprite. 116 and 119 are two of the liths of OBJNMUL2.ICN.
        World world = LoadWorld(
            (At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 11)),
            (At(20, 20), ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 12)),
            (At(30, 30), ObjectTile(Mp2ObjectIcnType.ObjnMul2, 119, Mp2ObjectType.ActionStoneLiths, 13)));

        Assert.Equal([At(10, 10), At(20, 20), At(30, 30)], world.GetObjectPositions(Mp2ObjectType.ActionStoneLiths));

        Assert.Equal(2, world.AllTeleports.Count);
        Assert.Equal([At(10, 10), At(20, 20)], world.AllTeleports[116]);
        Assert.Equal([At(30, 30)], world.AllTeleports[119]);
    }

    [Fact]
    public void PostLoadCachesWhirlpoolsAndTheEyeOfMagi()
    {
        World world = LoadWorld(
            (At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnWatr, 218, Mp2ObjectType.ActionWhirlpool, 21)),
            (At(12, 12), ObjectTile(Mp2ObjectIcnType.ObjnWatr, 218, Mp2ObjectType.ActionWhirlpool, 22)),
            (At(20, 20), ObjectTile(Mp2ObjectIcnType.XLoc3, 50, Mp2ObjectType.ActionEyeOfMagi, 23)));

        Assert.Equal([At(10, 10), At(12, 12)], world.AllWhirlpools[218]);
        Assert.Equal([At(20, 20)], world.AllEyeOfMagi);
    }

    [Fact]
    public void PostLoadIgnoresATileThatOnlyClaimsToBeStoneLiths()
    {
        // A broken map can store the type without the part. The reference asserts and carries on; the
        // port skips the tile instead of writing a null part into the cache.
        World world = LoadWorld(
            (At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnRsrc, 1, Mp2ObjectType.ActionStoneLiths, 31)));

        Assert.Empty(world.AllTeleports);
    }

    [Fact]
    public void GetObjectPartByActionTypeNeedsTheActionBit()
    {
        World world = LoadWorld(
            (At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 41)));

        Tile tile = world.GetTile(10, 10);

        // The same sprite, asked for by the twin without the action bit: the reference rejects such a
        // query outright, because the caller wants a part an action can run on.
        Assert.Null(tile.GetObjectPartByActionType(Mp2ObjectType.StoneLiths));

        ObjectPart? part = tile.GetObjectPartByActionType(Mp2ObjectType.ActionStoneLiths);

        Assert.NotNull(part);
        Assert.Equal(Mp2ObjectIcnType.ObjnMul2, part.Value.IcnType);
        Assert.Equal(116, part.Value.IcnIndex);
        Assert.Equal(41u, part.Value.Uid);

        Assert.Null(tile.GetObjectPartByActionType(Mp2ObjectType.ActionWhirlpool));
    }

    [Fact]
    public void GetObjectPartsFindsAPartUnderAnotherObject()
    {
        // A whirlpool can carry a boat, and such a tile is marked as a boat: searching by type would miss
        // the whirlpool, so the part is what the cache is built from.
        World world = LoadWorld();
        Tile tile = world.GetTile(0);

        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 51, Mp2ObjectIcnType.ObjnWatr, 218));

        var parts = world.GetObjectParts(Mp2ObjectType.ActionWhirlpool);

        Assert.Single(parts);
        Assert.Equal(0, parts[0].Index);
        Assert.Equal(51u, parts[0].Part.Uid);

        // The tile itself does not report a whirlpool, which is exactly why the parts are searched.
        Assert.NotEqual(Mp2ObjectType.ActionWhirlpool, tile.GetMainObjectType(world, false));
    }

    [Fact]
    public void GetObjectPositionsLooksThroughAHeroByDefault()
    {
        World world = LoadWorld((At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnRsrc, 1, Mp2ObjectType.ActionResource, 61)));

        Tile tile = world.GetTile(10, 10);
        Assert.Equal(Mp2ObjectType.ActionResource, tile.MainObjectType);

        Hero hero = world.GetHero(6)!;
        tile.SetHero(world, hero);
        Assert.Equal(Mp2ObjectType.ActionHero, tile.MainObjectType);

        // ignoreHeroes == true is the reference default and puts getMainObjectType( false ) into the
        // comparison: the hero is transparent, the resource is still found under it.
        Assert.Equal([At(10, 10)], world.GetObjectPositions(Mp2ObjectType.ActionResource));
        Assert.Empty(world.GetObjectPositions(Mp2ObjectType.ActionResource, ignoreHeroes: false));

        // The other way round the hero is the object of the tile.
        Assert.Equal([At(10, 10)], world.GetObjectPositions(Mp2ObjectType.ActionHero, ignoreHeroes: false));
        Assert.Empty(world.GetObjectPositions(Mp2ObjectType.ActionHero));
    }

    [Fact]
    public void DoesObjectExistOnMapLooksThroughAHero()
    {
        World world = LoadWorld((At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnRsrc, 1, Mp2ObjectType.ActionResource, 71)));

        Hero hero = world.GetHero(7)!;
        world.GetTile(10, 10).SetHero(world, hero);

        Assert.True(world.DoesObjectExistOnMap(Mp2ObjectType.ActionResource));
        Assert.False(world.DoesObjectExistOnMap(Mp2ObjectType.ActionWhirlpool));
    }

    [Fact]
    public void ResettingTheWorldDropsTheCaches()
    {
        World world = LoadWorld(
            (At(10, 10), ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 81)),
            (At(20, 20), ObjectTile(Mp2ObjectIcnType.XLoc3, 50, Mp2ObjectType.ActionEyeOfMagi, 82)));

        Assert.NotEmpty(world.AllTeleports);
        Assert.NotEmpty(world.AllEyeOfMagi);

        // The caches hold tile indexes, so they must not outlive the tiles.
        world.Reset();

        Assert.Empty(world.AllTeleports);
        Assert.Empty(world.AllWhirlpools);
        Assert.Empty(world.AllEyeOfMagi);
    }
}
