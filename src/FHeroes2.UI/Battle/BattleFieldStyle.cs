using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The archives the static layers of a battlefield come from, <c>Battle::Interface</c>'s
/// <c>_battleGroundIcn</c> and <c>_borderObjectsIcn</c> plus the castle background of a siege
/// (<c>battle_interface.cpp:1399-1451</c>, <c>:2497-2535</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在 <c>Interface</c> 的构造函数里按脚下地形挑一次图，之后整个战斗都用这一份。挑图同时看两件事：
/// 地形本身，以及身边九格里有没有树木（<c>Maps::hasNearbyObject( tileIndex, MP2::OBJ_TREES )</c>）——
/// 草地、土地与雪地各有一版「近处有树」的战场背景，边框杂物也跟着换。这三张表没有任何状态，所以做成纯函数，测试不必先造战场。
/// </para>
/// <para>
/// <see cref="IcnType.Unknown"/> 表示参考的 <c>default: break;</c>：地形不认识时什么都不画，战场底色留空。
/// </para>
/// </remarks>
/// <param name="Ground">The battlefield background, <c>CBKG*.ICN</c> frame 0.</param>
/// <param name="Border">The objects near the left and right borders, <c>FRNG*.ICN</c> frame 0.</param>
/// <param name="CastleBackground">The ground of a siege, <c>CASTBKG*.ICN</c>; only used with a castle.</param>
public readonly record struct BattleFieldStyle(IcnType Ground, IcnType Border, IcnType CastleBackground)
{
    /// <summary>The style of a battle that has no battlefield of its own.</summary>
    public static BattleFieldStyle Empty => new(IcnType.Unknown, IcnType.Unknown, IcnType.Unknown);

    /// <summary>
    /// Picks the background and the border objects of <paramref name="ground"/>,
    /// <c>Battle::Interface::Interface()</c>'s ground switch (<c>battle_interface.cpp:1406-1451</c>).
    /// </summary>
    /// <param name="ground">The ground of the tile the battle happens on.</param>
    /// <param name="hasNearbyTrees">
    /// Whether one of the eight tiles around it holds trees, <c>Maps::hasNearbyObject( ..., OBJ_TREES )</c>.
    /// Only grass, dirt and snow have a second version for it.
    /// </param>
    /// <returns>The three archives; <see cref="Empty"/> for a ground the reference does not handle.</returns>
    public static BattleFieldStyle ForGround(MapGround ground, bool hasNearbyTrees) => ground switch
    {
        MapGround.Desert => new(IcnType.Cbkgdsrt, IcnType.Frng0004, IcnType.Unknown),
        MapGround.Snow => hasNearbyTrees
            ? new(IcnType.Cbkgsntr, IcnType.Frng0006, IcnType.Unknown)
            : new(IcnType.Cbkgsnmt, IcnType.Frng0007, IcnType.Unknown),
        MapGround.Swamp => new(IcnType.Cbkgswmp, IcnType.Frng0008, IcnType.Unknown),
        MapGround.Wasteland => new(IcnType.Cbkgcrck, IcnType.Frng0003, IcnType.Unknown),
        MapGround.Beach => new(IcnType.Cbkgbeac, IcnType.Frng0002, IcnType.Unknown),
        MapGround.Lava => new(IcnType.Cbkglava, IcnType.Frng0005, IcnType.Unknown),
        MapGround.Dirt => hasNearbyTrees
            ? new(IcnType.Cbkgditr, IcnType.Frng0010, IcnType.Unknown)
            : new(IcnType.Cbkgdimt, IcnType.Frng0009, IcnType.Unknown),
        MapGround.Grass => hasNearbyTrees
            ? new(IcnType.Cbkggrtr, IcnType.Frng0011, IcnType.Unknown)
            : new(IcnType.Cbkggrmt, IcnType.Frng0012, IcnType.Unknown),
        MapGround.Water => new(IcnType.Cbkgwatr, IcnType.Frng0013, IcnType.Unknown),
        _ => Empty,
    };

    /// <summary>
    /// The castle background of a siege, the race switch of <c>_redrawBattleGround()</c>
    /// (<c>battle_interface.cpp:2502-2525</c>).
    /// </summary>
    /// <param name="race">The race of the defending town.</param>
    /// <returns>The archive, or <see cref="IcnType.Unknown"/> for a race the reference does not handle.</returns>
    public static IcnType CastleBackgroundFor(MapRace race) => race switch
    {
        MapRace.Barbarian => IcnType.Castbkgb,
        MapRace.Knight => IcnType.Castbkgk,
        MapRace.Necromancer => IcnType.Castbkgn,
        MapRace.Sorceress => IcnType.Castbkgs,
        MapRace.Warlock => IcnType.Castbkgw,
        MapRace.Wizard => IcnType.Castbkgz,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The wall, tower and drawbridge sprites of a siege, <c>ICN::getCastleIcnId( race )</c>
    /// (<c>icn.cpp:928-948</c>, used by <c>RedrawCastle()</c> at <c>battle_interface.cpp:2645</c>).
    /// </summary>
    /// <remarks>
    /// 这是战场专用的 <c>CASTLE*</c> 图，不是城镇建筑用的 <c>CSTL*</c>（那一套是
    /// <c>ICN::getBuildingIcnId()</c> 的）。两者名字只差几个字母，帧的内容完全不同。
    /// </remarks>
    /// <param name="race">The race of the defending town.</param>
    /// <returns>The archive, or <see cref="IcnType.Unknown"/> for a race the reference does not handle.</returns>
    public static IcnType CastleFor(MapRace race) => race switch
    {
        MapRace.Barbarian => IcnType.Castleb,
        MapRace.Knight => IcnType.Castlek,
        MapRace.Necromancer => IcnType.Castlen,
        MapRace.Sorceress => IcnType.Castles,
        MapRace.Warlock => IcnType.Castlew,
        MapRace.Wizard => IcnType.Castlez,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The style of a battle that happens inside the town of <paramref name="castle"/>: the same ground
    /// archives plus the castle background of its race.
    /// </summary>
    /// <param name="ground">The ground of the tile the battle happens on.</param>
    /// <param name="hasNearbyTrees">Whether the tile has trees next to it.</param>
    /// <param name="castleRace">The race of the defending town.</param>
    /// <returns>The style with <see cref="CastleBackground"/> filled in.</returns>
    public static BattleFieldStyle ForCastle(MapGround ground, bool hasNearbyTrees, MapRace castleRace)
    {
        BattleFieldStyle style = ForGround(ground, hasNearbyTrees);

        return style with { CastleBackground = CastleBackgroundFor(castleRace) };
    }
}
