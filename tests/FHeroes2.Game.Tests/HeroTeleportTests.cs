using FHeroes2.Assets.Maps;

using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two objects that move the hero without walking him there: the stone liths
/// (<c>ActionToTeleports()</c>, <c>heroes_action.cpp:1940</c>) and the whirlpool (<c>ActionToWhirlpools()</c>,
/// <c>heroes_action.cpp:1981</c>) together with the drowning it may cost (<c>WhirlpoolTroopLoseEffect()</c>,
/// <c>heroes_action.cpp:300</c>).
/// </summary>
/// <remarks>
/// The endpoints themselves are <see cref="TeleportTests"/>' business; here the question is what the hero does once
/// he stands on one end — where he comes out, what he uncovers, and what his army pays for the ride.
/// </remarks>
public sealed class HeroTeleportTests
{
    private const int MapSize = 36;

    /// <summary>Sprite of a stone liths entrance; both ends of a pair share it (<c>world.cpp:725</c>).</summary>
    private const byte LithsSprite = 116;

    /// <summary>A liths of another sprite, the one no other tile pairs with.</summary>
    private const byte LonelyLithsSprite = 119;

    private const byte WhirlpoolSprite = 218;

    /// <summary>The world always creates its heroes; this is one whose id resolves back through the world.</summary>
    private const int HeroId = 3;

    private static int At(int column, int row) => column + (row * MapSize);

    [Fact]
    public void TheLithsPutTheHeroOnTheOtherEnd()
    {
        int entrance = At(10, 10);
        int exit = At(20, 20);

        HeroActions actions = TeleportActions(
            out World world,
            out Hero hero,
            (entrance, Liths(LithsSprite, 11)),
            (exit, Liths(LithsSprite, 12)));

        Place(world, hero, entrance);

        // A route towards some other tile: teleporting drops it, or the next walk would follow it from the new
        // position (heroes_action.cpp:1972).
        hero.Path.SetPath([new RouteStep(At(11, 10), entrance, Direction.Right, 100)]);
        Assert.False(hero.Path.IsEmpty);

        Assert.Equal(TeleportOutcome.Moved, actions.ActionToTeleports(entrance, world));

        Assert.Equal(exit, hero.MapIndex);
        Assert.True(hero.Path.IsEmpty);
        Assert.Equal(Mp2ObjectType.ActionHero, world.GetTile(exit).MainObjectType);

        // The entrance is a plain liths again: nobody stands on it any more, and the object underneath came back.
        Assert.Equal(Mp2ObjectType.ActionStoneLiths, world.GetTile(entrance).MainObjectType);
        Assert.Null(world.GetTile(entrance).GetHero(world));
    }

    [Fact]
    public void TheLithsUncoverTheFogAtTheOtherEnd()
    {
        int entrance = At(10, 10);
        int exit = At(20, 20);

        HeroActions actions = TeleportActions(
            out World world,
            out Hero hero,
            (entrance, Liths(LithsSprite, 11)),
            (exit, Liths(LithsSprite, 12)));

        Place(world, hero, entrance);

        actions.ActionToTeleports(entrance, world);

        // The three steps of heroes_action.cpp:1962-1972: Scout first, then the move, and the fog is the hero's.
        Assert.False(world.Fog!.IsFogged(20, 20, PlayerColor.Blue));
        Assert.Equal(Direction.Unknown, world.Fog.GetDirection(20, 20));
        Assert.Equal(Directions.All, world.Fog.GetDirection(0, 0));

        // The entrance is not uncovered on the way out: the reference scouts the destination only.
        Assert.True(world.Fog.IsFogged(10, 10, PlayerColor.Blue));
    }

    [Fact]
    public void ALonelyLithsMovesNobody()
    {
        int entrance = At(10, 10);
        int lonely = At(25, 25);

        HeroActions actions = TeleportActions(
            out World world,
            out Hero hero,
            (entrance, Liths(LithsSprite, 11)),
            (lonely, Liths(LonelyLithsSprite, 13)));

        Place(world, hero, entrance);

        Assert.Equal(TeleportOutcome.NoDestination, actions.ActionToTeleports(entrance, world));

        Assert.Equal(entrance, hero.MapIndex);
        Assert.Equal(Mp2ObjectType.ActionHero, world.GetTile(entrance).MainObjectType);
    }

    [Fact]
    public void TheWhirlpoolDrownsHalfTheWeakestStack()
    {
        int entrance = At(10, 10);
        int exit = At(20, 20);

        HeroActions actions = TeleportActions(
            out World world,
            out Hero hero,
            (entrance, Whirlpool(21)),
            (exit, Whirlpool(22)));

        Place(world, hero, entrance);

        // Peasants are by far the weakest stack, and their hit points are one apiece, so half of them drowns:
        // 10 becomes 5 (heroes_action.cpp:300-340).
        hero.Army.JoinTroop(MonsterType.Peasant, 10);
        hero.Army.JoinTroop(MonsterType.Swordsman, 2);

        Assert.Equal(TeleportOutcome.Moved, actions.ActionToWhirlpools(entrance, world, Rolling(1)));

        Assert.Equal(exit, hero.MapIndex);
        Assert.Equal(5u, hero.Army.GetTroop(0).Count);
        Assert.Equal(2u, hero.Army.GetTroop(1).Count);
    }

    [Fact]
    public void DrowningRoundsUpToWholeCreatures()
    {
        // A stack whose hit points are not a whole multiple of one creature is still alive creature by creature
        // (monster.cpp:840): a remainder counts as one more.
        uint single = (uint)MonsterStatistics.GetBattleStats(MonsterType.Swordsman).HitPoints;

        Assert.Equal(1u, Troop.GetCountFromHitPoints(MonsterType.Swordsman, 1));
        Assert.Equal(3u, Troop.GetCountFromHitPoints(MonsterType.Swordsman, single * 3));
        Assert.Equal(4u, Troop.GetCountFromHitPoints(MonsterType.Swordsman, (single * 3) + 1));
        Assert.Equal(0u, Troop.GetCountFromHitPoints(MonsterType.Swordsman, 0));
    }

    [Fact]
    public void TheWhirlpoolLeavesTheOnlyDefenderAlone()
    {
        int entrance = At(10, 10);
        int exit = At(20, 20);

        HeroActions actions = TeleportActions(
            out World world,
            out Hero hero,
            (entrance, Whirlpool(21)),
            (exit, Whirlpool(22)));

        Place(world, hero, entrance);
        hero.Army.JoinTroop(MonsterType.Peasant, 1);

        Assert.Equal(TeleportOutcome.Moved, actions.ActionToWhirlpools(entrance, world, Rolling(1)));

        // One slot with one creature in it is the stack the whirlpool never takes; he still went through.
        Assert.Equal(exit, hero.MapIndex);
        Assert.Equal(1u, hero.Army.GetTroop(0).Count);
    }

    [Fact]
    public void AMissedRollDrownsNobodyButStillMoves()
    {
        int entrance = At(10, 10);
        int exit = At(20, 20);

        HeroActions actions = TeleportActions(
            out World world,
            out Hero hero,
            (entrance, Whirlpool(21)),
            (exit, Whirlpool(22)));

        Place(world, hero, entrance);
        hero.Army.JoinTroop(MonsterType.Peasant, 10);

        Assert.Equal(TeleportOutcome.Moved, actions.ActionToWhirlpools(entrance, world, Rolling(2)));

        Assert.Equal(exit, hero.MapIndex);
        Assert.Equal(10u, hero.Army.GetTroop(0).Count);
    }

    [Fact]
    public void AWhirlpoolWithNoOtherEndTakesNobody()
    {
        int entrance = At(10, 10);

        HeroActions actions = TeleportActions(out World world, out Hero hero, (entrance, Whirlpool(31)));

        Place(world, hero, entrance);
        hero.Army.JoinTroop(MonsterType.Peasant, 10);

        Assert.Equal(TeleportOutcome.NoDestination, actions.ActionToWhirlpools(entrance, world, Rolling(1)));

        Assert.Equal(entrance, hero.MapIndex);
        Assert.Equal(10u, hero.Army.GetTroop(0).Count);
    }

    /// <summary>
    /// A world holding <paramref name="tiles"/> and a blue hero who owns nothing but <see cref="HeroActions"/> — the
    /// scaffolding of <c>HeroFogTests</c>: the actions need a kingdom, the fog needs a colour.
    /// </summary>
    /// <remarks>
    /// The hero is one the world created rather than a new one: a tile finds the hero standing on it through
    /// <c>World.GetHero( id )</c> (<see cref="Tile.GetMainObjectType"/>), so an id of its own would make the tile look up
    /// a stranger and the endpoints would vanish — a collision worth remembering.
    /// </remarks>
    private static HeroActions TeleportActions(
        out World world,
        out Hero hero,
        params (int Index, Mp2TileInfo Tile)[] tiles)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        foreach ((int index, Mp2TileInfo tile) in tiles)
        {
            map.SetTileAt(index, tile);
        }

        world = new World();
        world.LoadMap(map);

        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        hero = world.GetHero(HeroId)!;
        kingdoms.GetKingdom(PlayerColor.Blue).AddHero(hero);

        return new HeroActions(hero, kingdoms);
    }

    /// <summary>Puts <paramref name="hero"/> on a tile, the way <c>Heroes::SetMapIndex()</c> and a tile agree on.</summary>
    private static void Place(World world, Hero hero, int tileIndex)
    {
        world.GetTile(tileIndex).SetHero(world, hero);
        hero.SetMapIndex(tileIndex);
    }

    /// <summary>
    /// A generator whose first one-in-three draw is <paramref name="wanted"/>: the whirlpool roll is the only thing it
    /// is asked for, so the seed fixes which branch the action takes. Same idea as the <c>generator</c> argument of
    /// <c>Army::Reset()</c>.
    /// </summary>
    private static Pcg32 Rolling(uint wanted)
    {
        for (ulong seed = 0; seed < 10_000; ++seed)
        {
            if (Rand.GetWithGen(1, 3, new Pcg32(seed)) == wanted)
            {
                return new Pcg32(seed);
            }
        }

        throw new InvalidOperationException($"No seed rolls {wanted} on the first draw.");
    }

    /// <summary>
    /// A stone liths tile of the given sprite. Both ends of a pair share the sprite; the UID keeps the two objects
    /// apart, and it stays inside a byte because that is all an MP2 tile stores.
    /// </summary>
    private static Mp2TileInfo Liths(byte sprite, int uid) =>
        ObjectTile(Mp2ObjectIcnType.ObjnMul2, sprite, Mp2ObjectType.ActionStoneLiths, (uint)uid);

    /// <summary>A whirlpool tile on water.</summary>
    private static Mp2TileInfo Whirlpool(int uid) =>
        ObjectTile(
            Mp2ObjectIcnType.ObjnWatr,
            WhirlpoolSprite,
            Mp2ObjectType.ActionWhirlpool,
            (uint)uid,
            MapGrounds.WaterStartImageIndex);

    private static Mp2TileInfo ObjectTile(
        Mp2ObjectIcnType icnType,
        byte icnIndex,
        Mp2ObjectType objectType,
        uint uid,
        ushort terrain = MapGrounds.GrassStartImageIndex) => new()
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
}
