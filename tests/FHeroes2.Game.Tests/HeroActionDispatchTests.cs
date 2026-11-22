using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the dispatcher that turns a tile into one of the <c>ActionTo*</c> handlers, <c>Heroes::Action()</c>
/// (<c>heroes_action.cpp:3747</c>): how it reads the object type off the tile, what it reports back, and what it
/// refuses to serve.
/// </summary>
/// <remarks>
/// 每一支 <c>ActionTo*</c> 本身归它自己的测试管；这里问的是「走进去以后出来的是哪一支、世界动了没有、结果装在
/// 哪一格」，以及「瓦片上的动作位在进门处被折掉了没有」。
/// </remarks>
public sealed class HeroActionDispatchTests
{
    private const int MapSize = 36;

    /// <summary>The world always creates its heroes; this is one whose id resolves back through the world.</summary>
    private const int HeroId = 3;

    private static int At(int column, int row) => column + (row * MapSize);

    [Fact]
    public void TheWellUnderTheHeroIsSeenThroughHim()
    {
        int well = At(10, 10);

        HeroActions actions = Actions(
            out World world,
            out Hero hero,
            (well, Object(Mp2ObjectIcnType.ObjnGras, 29, Mp2ObjectType.ActionMagicWell, 41)));

        // 英雄踩在井上：瓦片的主对象类型已经变成 ActionHero，井只剩在 ObjectTypeUnderHero 里。分发器要按参考的
        // getMainObjectType( tileIndex != heroPosIndex ) 透过英雄看，否则「喝井水」永远触发不了。
        Place(world, hero, well);
        hero.SetSpellPoints(0);

        HeroActionResult result = actions.Action(well, world);

        Assert.Equal(HeroActionKind.MagicWell, result.Kind);
        Assert.Equal(Mp2ObjectType.MagicWell, result.ObjectType);
        Assert.True(result.Changed);
        Assert.True(result.TryGetPayload(out bool refilled));
        Assert.True(refilled);
        Assert.Equal(hero.GetMaxSpellPoints(), hero.SpellPoints);
        Assert.True(hero.Modes.HasFlag(HeroModes.Action));
    }

    [Fact]
    public void TheSignInFrontIsReadWithoutItsActionBit()
    {
        int sign = At(10, 10);

        HeroActions actions = Actions(
            out World world,
            out Hero hero,
            (sign, Object(Mp2ObjectIcnType.ObjnXtra, 1, Mp2ObjectType.ActionSign, 51)));

        // 站在旁边而不是踩上去：这一支读的是瓦片缓存的那一层，而那一层带着动作位。
        Place(world, hero, At(9, 10));

        HeroActionResult result = actions.Action(sign, world);

        // 动作位折掉了：报给调用方的 ObjectType 与对象族里其他值同一种写法，case 走的也是不带位的那一支。
        Assert.Equal(Mp2ObjectType.Sign, result.ObjectType);
        Assert.Equal(HeroActionKind.Sign, result.Kind);

        // 告示牌什么也不改；这张地图没配文案，所以连牌子也没有。
        Assert.False(result.Changed);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void TheWindmillPaysOnceAndThenRemembers()
    {
        int windmill = At(10, 10);

        HeroActions actions = Actions(
            out World world,
            out Hero hero,
            (windmill, Object(Mp2ObjectIcnType.ObjnGras, 21, Mp2ObjectType.ActionWindmill, 61)));

        Place(world, hero, windmill);

        // 风车的收益写在元数据里：第 0 格是资源，第 1 格是数量。
        Tile tile = world.GetTile(windmill);
        tile.Metadata[0] = (uint)Resource.Gold;
        tile.Metadata[1] = 5;

        HeroActionResult first = actions.Action(windmill, world);

        Assert.Equal(HeroActionKind.ObjectResource, first.Kind);
        Assert.True(first.Changed);
        Assert.True(first.TryGetPayload(out Funds funds));
        Assert.True(funds.GetValidItemsCount() > 0);

        // 第二次：名册记的是折过的 Windmill，所以查得回来——记成 ActionWindmill 就永远查不到，这正是 ADR-133
        // 要堵的那个洞。
        HeroActionResult second = actions.Action(windmill, world);

        Assert.Equal(HeroActionKind.ObjectResource, second.Kind);
        Assert.False(second.Changed);
    }

    [Fact]
    public void APatchOfGrassIsNoAction()
    {
        HeroActions actions = Actions(out World world, out Hero hero);

        HeroActionResult result = actions.Action(At(20, 20), world);

        Assert.Equal(HeroActionKind.None, result.Kind);
        Assert.Equal(Mp2ObjectType.None, result.ObjectType);
        Assert.False(result.Changed);
        Assert.False(hero.Modes.HasFlag(HeroModes.Action));
    }

    [Fact]
    public void TheArenaLeavesTheSkillToTheCaller()
    {
        int arena = At(10, 10);

        HeroActions actions = Actions(
            out World world,
            out Hero hero,
            (arena, Object(Mp2ObjectIcnType.ObjnGras, 103, Mp2ObjectType.ActionArena, 81)));

        Place(world, hero, At(9, 10));

        int attack = hero.GetAttack();

        HeroActionResult result = actions.Action(arena, world);

        // 参考在函数里弹四选一；端口报个到就交回控制权，技能一点没动。
        Assert.Equal(HeroActionKind.Arena, result.Kind);
        Assert.False(result.Changed);
        Assert.Null(result.Payload);
        Assert.Equal(attack, hero.GetAttack());

        // 玩家选完，调用方再拿选择去结算——这就是两步走的那一步。
        Assert.True(actions.ActionToArena(arena, PrimarySkill.Attack));
        Assert.Equal(attack + 1, hero.GetAttack());
    }

    [Fact]
    public void TheWhirlpoolMovesTheHeroAndSaysSo()
    {
        int entrance = At(10, 10);
        int exit = At(20, 20);

        HeroActions actions = Actions(
            out World world,
            out Hero hero,
            (entrance, Whirlpool(91)),
            (exit, Whirlpool(92)));

        Place(world, hero, entrance);
        hero.Army.JoinTroop(MonsterType.Peasant, 4);

        HeroActionResult result = actions.Action(entrance, world);

        Assert.Equal(HeroActionKind.Whirlpools, result.Kind);
        Assert.True(result.Changed);
        Assert.True(result.TryGetPayload(out TeleportOutcome outcome));
        Assert.Equal(TeleportOutcome.Moved, outcome);
        Assert.Equal(exit, hero.MapIndex);
        Assert.True(hero.Modes.HasFlag(HeroModes.Action));
    }

    /// <summary>
    /// A world holding <paramref name="tiles"/> and a blue hero who owns nothing but <see cref="HeroActions"/> — the
    /// same scaffolding as <c>HeroTeleportTests</c>.
    /// </summary>
    /// <remarks>
    /// The hero is one the world created rather than a new one: a tile finds the hero standing on it through
    /// <c>World.GetHero( id )</c>, so an id of its own would make the tile look up a stranger.
    /// </remarks>
    private static HeroActions Actions(
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

    /// <summary>A whirlpool tile on water.</summary>
    private static Mp2TileInfo Whirlpool(int uid) =>
        Object(
            Mp2ObjectIcnType.ObjnWatr,
            218,
            Mp2ObjectType.ActionWhirlpool,
            (uint)uid,
            MapGrounds.WaterStartImageIndex);

    private static Mp2TileInfo Object(
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
