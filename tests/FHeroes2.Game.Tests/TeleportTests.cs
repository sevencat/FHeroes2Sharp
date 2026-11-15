using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the teleport and whirlpool endpoints of <c>world.cpp</c>: <c>GetTeleportEndPoints</c>
/// (<c>:704</c>), <c>NextTeleport</c> (<c>:737</c>), <c>GetWhirlpoolEndPoints</c> (<c>:748</c>) and
/// <c>NextWhirlpool</c> (<c>:790</c>), together with the caches <c>PostLoad</c> fills for them.
/// </summary>
public sealed class TeleportTests
{
    private const int MapSize = 36;

    private static int At(int x, int y) => x + (y * MapSize);

    private static Mp2Map BlankMap() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

    private static Mp2TileInfo ObjectTile(Mp2ObjectIcnType icnType, byte icnIndex, Mp2ObjectType objectType, uint uid = 1, ushort terrain = MapGrounds.GrassStartImageIndex) => new()
    {
        TerrainImageIndex = terrain,
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
    public void TeleportEndPointsPairTheSameSpriteOnly()
    {
        int first = At(10, 10);
        int second = At(20, 20);
        int lonely = At(30, 30);

        World world = LoadWorld(
            (first, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 11)),
            (second, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 12)),
            (lonely, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 119, Mp2ObjectType.ActionStoneLiths, 13)));

        Assert.Equal([second], world.GetTeleportEndPoints(first));
        Assert.Equal([first], world.GetTeleportEndPoints(second));

        // A liths of another sprite has no partner, and NextTeleport hands the entrance back (world.cpp:742).
        Assert.Empty(world.GetTeleportEndPoints(lonely));
        Assert.Equal(lonely, world.NextTeleport(lonely));
    }

    [Fact]
    public void TeleportEndPointsIgnoreWaterAndLandPairing()
    {
        int land = At(10, 10);
        int water = At(20, 20);

        // The same sprite on both sides: only the water flag separates them, and the reference refuses to
        // pair across it (world.cpp:725).
        World world = LoadWorld(
            (land, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 21)),
            (water, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 22, terrain: 0)));

        Assert.True(world.GetTile(water).IsWater);

        Assert.Empty(world.GetTeleportEndPoints(land));
        Assert.Empty(world.GetTeleportEndPoints(water));
    }

    [Fact]
    public void NextTeleportPicksOneOfTheEndPoints()
    {
        int entrance = At(5, 5);
        int first = At(15, 15);
        int second = At(25, 25);

        World world = LoadWorld(
            (entrance, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 31)),
            (first, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 32)),
            (second, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 33)));

        HashSet<int> destinations = [];

        for (int i = 0; i < 200; ++i)
        {
            destinations.Add(world.NextTeleport(entrance));
        }

        Assert.Equal(new[] { first, second }, destinations.Order());
    }

    [Fact]
    public void AWeakEntranceStillWorksThroughAHeroButABlockedDestinationDoesNot()
    {
        // The entrance is looked at with getMainObjectType( false ) (world.cpp:710) and the candidates with
        // the cached type (world.cpp:725): a hero standing on the entrance may still use it, a hero on the
        // destination takes it out of the pair.
        int entrance = At(10, 10);
        int blocked = At(20, 20);

        World world = LoadWorld(
            (entrance, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 41)),
            (blocked, ObjectTile(Mp2ObjectIcnType.ObjnMul2, 116, Mp2ObjectType.ActionStoneLiths, 42)));

        Hero heroOnEntrance = world.GetHero(3)!;
        world.GetTile(entrance).SetHero(world, heroOnEntrance);

        Assert.Equal(Mp2ObjectType.ActionHero, world.GetTile(entrance).MainObjectType);
        Assert.Equal([blocked], world.GetTeleportEndPoints(entrance));

        Hero heroOnDestination = world.GetHero(4)!;
        world.GetTile(blocked).SetHero(world, heroOnDestination);

        Assert.Empty(world.GetTeleportEndPoints(entrance));
    }

    [Fact]
    public void TeleportEndPointsOfANonTeleportTileAreEmpty()
    {
        int plain = At(7, 7);

        World world = LoadWorld((plain, ObjectTile(Mp2ObjectIcnType.ObjnRsrc, 1, Mp2ObjectType.ActionResource, 51)));

        Assert.Empty(world.GetTeleportEndPoints(plain));
        Assert.Equal(plain, world.NextTeleport(plain));
        Assert.Empty(world.GetWhirlpoolEndPoints(plain));
        Assert.Equal(plain, world.NextWhirlpool(plain));
    }

    [Fact]
    public void WhirlpoolEndPointsSkipTheOtherPartsOfTheSameWhirlpool()
    {
        // One whirlpool is several tiles sharing a UID, and a hero must not be thrown back onto his own
        // whirlpool (world.cpp:778).
        int firstPart = At(10, 10);
        int secondPart = At(11, 10);
        int otherWhirlpool = At(20, 20);

        World world = LoadWorld(
            (firstPart, ObjectTile(Mp2ObjectIcnType.ObjnWatr, 218, Mp2ObjectType.ActionWhirlpool, 61, terrain: 0)),
            (secondPart, ObjectTile(Mp2ObjectIcnType.ObjnWatr, 218, Mp2ObjectType.ActionWhirlpool, 61, terrain: 0)),
            (otherWhirlpool, ObjectTile(Mp2ObjectIcnType.ObjnWatr, 218, Mp2ObjectType.ActionWhirlpool, 62, terrain: 0)));

        Assert.Equal([otherWhirlpool], world.GetWhirlpoolEndPoints(firstPart));
        Assert.Equal([otherWhirlpool], world.GetWhirlpoolEndPoints(secondPart));
        Assert.Equal([firstPart, secondPart], world.GetWhirlpoolEndPoints(otherWhirlpool));

        HashSet<int> destinations = [];

        for (int i = 0; i < 200; ++i)
        {
            destinations.Add(world.NextWhirlpool(otherWhirlpool));
        }

        Assert.Equal(new[] { firstPart, secondPart }, destinations.Order());
    }
}
