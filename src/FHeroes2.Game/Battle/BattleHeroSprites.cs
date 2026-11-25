using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The battle poses of a hero and of a captain: which archive holds them, which frames make up each pose
/// and where they stand, the reference's <c>matchHeroType</c> / <c>getHeroAnimation</c> and the
/// <c>OpponentSprite</c> offsets (<c>battle_interface.cpp:383-451</c>, <c>battle_interface.h:170-179</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把这张表和 <c>OpponentSprite</c> 的绘制混在一个类里；端口把「哪一族用哪个 ICN、每个姿势有哪些帧、
/// 施法点偏移多少」这三件纯查表的事留在这里，绘制见 <c>FHeroes2.UI.Battle.BattleOpponentSprite</c>。
/// </para>
/// <para>
/// 差异：参考对不认识的种族 <c>assert( 0 )</c>；端口不中断发布版，<see cref="GetIcn"/> 回
/// <see cref="IcnType.Unknown"/>（参考保持构造时的 <c>ICN::UNKNOWN</c>），帧表按骑士算（参考取
/// <c>Battle::KNIGHT</c>）。
/// </para>
/// </remarks>
public static class BattleHeroSprites
{
    /// <summary>Right hero's distance from the right edge, <c>RIGHT_HERO_X_OFFSET</c>.</summary>
    public const int RightHeroXOffset = 29;

    /// <summary>Left hero's distance from the left edge, <c>LEFT_HERO_X_OFFSET</c>.</summary>
    public const int LeftHeroXOffset = 30;

    /// <summary>Left hero's distance from the top, <c>LEFT_HERO_Y_OFFSET</c>.</summary>
    public const int LeftHeroYOffset = 183;

    /// <summary>Right hero's distance from the top, <c>RIGHT_HERO_Y_OFFSET</c>.</summary>
    public const int RightHeroYOffset = 148;

    /// <summary>Captain's extra horizontal shift, <c>CAPTAIN_X_OFFSET</c>.</summary>
    public const int CaptainXOffset = 6;

    /// <summary>Captain's extra vertical shift, <c>CAPTAIN_Y_OFFSET</c>.</summary>
    public const int CaptainYOffset = -13;

    /// <summary>The single frame of the standing pose, <c>{ 1 }</c>.</summary>
    private static readonly int[] StandingFrames = [1];

    /// <summary>The sighing pose of a hero, <c>{ 2, 3, 4, 5, 4, 5, 4, 3, 2 }</c>.</summary>
    private static readonly int[] SorrowFrames = [2, 3, 4, 5, 4, 5, 4, 3, 2];

    /// <summary>
    /// The frames of every pose of every hero type, <c>heroTypeAnim[7][9]</c>: the rows are the six hero
    /// races plus the captain, the columns are <see cref="BattleHeroAnimation.Joy"/> …
    /// <see cref="BattleHeroAnimation.Idle2"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="BattleHeroAnimation.Static"/> 与 <see cref="BattleHeroAnimation.Sorrow"/> 不在表里：
    /// 参考让它们提前返回常量，所以最后两列（表里的 15/16/17 之类）永远取不到。
    /// </remarks>
    private static readonly int[][][] HeroTypeAnim =
    [
        // JOY                CAST_MASS    CAST_MASS_RETURN  CAST_UP       CAST_UP_RETURN  CAST_DOWN  CAST_DOWN_RETURN  IDLE           IDLE2
        [[6, 7, 8, 9, 8, 9, 8, 7, 6], [10, 11], [10], [6, 12, 13], [12, 6], [2, 14], [2], [15, 16, 17], [18, 19]], // Knight
        [[6, 7, 8, 9, 9, 8, 7, 6], [6, 10, 11], [10, 6], [6, 12, 13], [12, 6], [6, 14], [6], [15, 16, 17], [18]], // Barbarian
        [[6, 7, 8, 7, 6], [6, 7, 9], [7, 6], [6, 10, 11], [10, 6], [6, 12], [6], [13, 14, 15], [16]], // Sorceress
        [[6, 7, 8, 9, 10, 9, 8, 7, 6], [6, 7, 11, 12], [11, 6], [6, 7, 13], [6], [6, 14], [6], [15, 16], [6]], // Warlock
        [[6, 7, 8, 9, 8, 7, 6], [6, 10, 11, 12, 13], [12, 11, 10, 6], [6, 14], [6], [6, 15], [6], [16, 17], [18]], // Wizard
        [[6, 7, 6, 7, 6, 7], [7, 8, 9, 10, 11], [10, 9, 7], [7, 12, 13, 14, 15], [7], [7, 12, 13, 14, 16], [7], [17], [18, 19]], // Necromancer
        [[1], [2, 3, 4], [3, 2], [5, 6], [5], [5, 7], [5], [8, 9], [10]], // Captain
    ];

    /// <summary>
    /// The archive that holds a hero's or captain's battle poses, <c>OpponentSprite::_heroIcnId</c>.
    /// </summary>
    /// <param name="race">The race of the commander, <c>HeroBase::GetRace()</c>.</param>
    /// <param name="isCaptain">Whether the commander is the town's captain rather than a hero.</param>
    /// <returns>The archive, or <see cref="IcnType.Unknown"/> for a race without sprites.</returns>
    public static IcnType GetIcn(MapRace race, bool isCaptain) => (isCaptain, race) switch
    {
        (true, MapRace.Knight) => IcnType.Cmbtcapk,
        (true, MapRace.Barbarian) => IcnType.Cmbtcapb,
        (true, MapRace.Sorceress) => IcnType.Cmbtcaps,
        (true, MapRace.Warlock) => IcnType.Cmbtcapw,
        (true, MapRace.Wizard) => IcnType.Cmbtcapz,
        (true, MapRace.Necromancer) => IcnType.Cmbtcapn,
        (false, MapRace.Knight) => IcnType.Cmbthrok,
        (false, MapRace.Barbarian) => IcnType.Cmbthrob,
        (false, MapRace.Sorceress) => IcnType.Cmbthros,
        (false, MapRace.Warlock) => IcnType.Cmbthrow,
        (false, MapRace.Wizard) => IcnType.Cmbthroz,
        (false, MapRace.Necromancer) => IcnType.Cmbthron,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The frames that make up one pose, <c>getHeroAnimation()</c>.
    /// </summary>
    /// <param name="race">The race of the commander.</param>
    /// <param name="isCaptain">Whether the commander is a captain.</param>
    /// <param name="animation">The pose.</param>
    /// <returns>The frames in the order they play; the array belongs to this class and must not be modified.</returns>
    public static int[] GetFrames(MapRace race, bool isCaptain, BattleHeroAnimation animation)
    {
        if (animation == BattleHeroAnimation.Static)
        {
            return StandingFrames;
        }

        // A captain never sighs: the reference answers the standing pose for SORROW as well.
        if (animation == BattleHeroAnimation.Sorrow)
        {
            return isCaptain ? StandingFrames : SorrowFrames;
        }

        return HeroTypeAnim[GetHeroType(race, isCaptain)][(int)animation];
    }

    /// <summary>
    /// The offset of the caster's hand from the sprite's centre, <c>OpponentSprite::GetCastPosition()</c>.
    /// </summary>
    /// <param name="race">The race of the commander.</param>
    /// <param name="isCaptain">Whether the commander is a captain.</param>
    /// <returns>The offset; <see cref="Point.Zero"/> for a race without sprites.</returns>
    public static Point GetCastOffset(MapRace race, bool isCaptain) => race switch
    {
        MapRace.Knight => new Point(isCaptain ? 0 : 13, isCaptain ? 3 : -7),
        MapRace.Barbarian => new Point(isCaptain ? 0 : 16, isCaptain ? 3 : -15),
        MapRace.Sorceress => new Point(isCaptain ? 0 : 11, isCaptain ? 3 : -8),
        MapRace.Warlock => new Point(isCaptain ? 2 : 9, isCaptain ? 5 : -11),
        MapRace.Wizard => new Point(isCaptain ? 5 : 1, isCaptain ? 8 : -9),
        MapRace.Necromancer => new Point(isCaptain ? 5 : 13, isCaptain ? 6 : -7),
        _ => Point.Zero,
    };

    /// <summary>
    /// Where a frame of a hero's pose lands on the battlefield, <c>OpponentSprite</c>'s constructor and
    /// <c>Redraw()</c>.
    /// </summary>
    /// <param name="offset">Top left of the interface, <c>_offset</c>.</param>
    /// <param name="screenWidth">Width of the screen the field is centred in, 640 in the reference.</param>
    /// <param name="frame">The frame to place; its own offset shifts it.</param>
    /// <param name="isFlipped">Whether the hero stands on the right and faces left.</param>
    /// <param name="isCaptain">Whether the commander is a captain, which shifts the pose by a few pixels.</param>
    /// <returns>The area the frame covers.</returns>
    public static Rect GetPosition(Point offset, int screenWidth, Sprite frame, bool isFlipped, bool isCaptain)
    {
        int x = offset.X;
        int y = offset.Y;

        if (isCaptain)
        {
            // 队长的立绘比英雄的靠内一点：左边往右缩、右边往左缩，都是 6 像素。
            x += isFlipped ? CaptainXOffset : -CaptainXOffset;
            y += CaptainYOffset;
        }

        x += isFlipped
            ? screenWidth - RightHeroXOffset - (frame.OffsetX + frame.Width)
            : LeftHeroXOffset + frame.OffsetX;
        y += isFlipped ? RightHeroYOffset + frame.OffsetY : LeftHeroYOffset + frame.OffsetY;

        return new Rect(x, y, frame.Width, frame.Height);
    }

    /// <summary>
    /// Where a spell cast by this hero leaves the hand, <c>OpponentSprite::GetCastPosition()</c>.
    /// </summary>
    /// <param name="area">The hero's area, <c>_area</c>.</param>
    /// <param name="isFlipped">Whether the hero stands on the right and faces left.</param>
    /// <param name="castOffset">The race's hand offset, <see cref="GetCastOffset"/>.</param>
    /// <returns>The point on the battlefield surface.</returns>
    public static Point GetCastPosition(Rect area, bool isFlipped, Point castOffset) => new(
        area.X + (isFlipped ? castOffset.X : area.Width - castOffset.X),
        area.Y + (area.Height / 2) + castOffset.Y);

    /// <summary>The row of <see cref="HeroTypeAnim"/>, the reference's <c>BattleHeroType</c>.</summary>
    /// <param name="race">The race of the commander.</param>
    /// <param name="isCaptain">Whether the commander is a captain.</param>
    /// <returns>0…5 for the six races, 6 for a captain.</returns>
    private static int GetHeroType(MapRace race, bool isCaptain)
    {
        if (isCaptain)
        {
            return 6;
        }

        return race switch
        {
            MapRace.Knight => 0,
            MapRace.Barbarian => 1,
            MapRace.Sorceress => 2,
            MapRace.Warlock => 3,
            MapRace.Wizard => 4,
            MapRace.Necromancer => 5,
            _ => 0,
        };
    }
}
