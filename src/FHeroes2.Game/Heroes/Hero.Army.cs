using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Monsters;
using ArmyStack = FHeroes2.Game.Army.Army;
using TroopStack = FHeroes2.Game.Army.Troop;
using WorldMap = FHeroes2.Game.World;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The army and the movement points of a hero: <c>heroes.h:290-470</c> (the army member and the five
/// movement-point methods) and <c>heroes.cpp:972-1031</c> plus <c>heroes.cpp:1782-1799</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two belong in one file because the amount of movement a hero gets per day is decided by his army:
/// <c>GetMaxMovePoints()</c> reads the speed of the slowest stack and then scales the result by the
/// Logistics (or Navigation) skill. Splitting them would mean a comment in one file pointing at the other
/// for the only interesting part.
/// </para>
/// <para>
/// <b>Port substitutions</b>, all of them because the systems they read are not ported:
/// </para>
/// <list type="bullet">
/// <item>
/// The artifact bonuses (<c>LAND_MOBILITY</c>, <c>SEA_MOBILITY</c>) are missing — there is no artifact bag,
/// so the term is zero. The reference adds <c>getTotalArtifactEffectValue()</c> in the same place, which is
/// where the port will add it back.
/// </item>
/// <item>
/// The Stables bonus (<c>isObjectTypeVisited( OBJ_STABLES )</c>, <c>GameStatic::getMovementPointBonus</c>)
/// is missing — the port does not track visited objects. It is worth 400 movement points for the day, so a
/// hero that visited the stables moves less than he should until the visit lists land.
/// </item>
/// <item>
/// The lighthouse bonus keeps its full form: <see cref="World.CountCapturedObject"/> exists, so
/// <c>500 × captured lighthouses</c> is computed from the map instead of being skipped. The
/// <see cref="World"/> is a parameter rather than the reference's global <c>world</c>.
/// </item>
/// <item>
/// The reference returns early from <c>GetMaxMovePoints()</c>'s land branch through
/// <c>assert( 0 )</c> for the speeds it does not list (standing, crawling, blazing, instant). The port
/// leaves the result at zero and does not assert: a debug build of the reference would abort, a release
/// build would return zero, and the release behaviour is the one a player sees.
/// </item>
/// </list>
/// </remarks>
public sealed partial class Hero
{
    private readonly ArmyStack _army = new();

    private readonly SecSkills _secondarySkills = new();

    private uint _movePoints;

    /// <summary>
    /// The army of the hero, <c>_army</c> / <c>GetArmy()</c> (<c>heroes.h:290</c>, <c>heroes.h:465</c>).
    /// </summary>
    /// <remarks>
    /// Never <see langword="null"/> and never <see langword="null"/>-able: the reference constructs the army
    /// with the hero and <c>Army::Reset()</c> only replaces its contents, so a hero without an army is a
    /// hero who has not been given one yet, not a hero with no army object.
    /// </remarks>
    public ArmyStack Army => _army;

    /// <summary>
    /// The secondary skills of the hero, <c>_secondarySkills</c> (<c>heroes.h:291</c>).
    /// </summary>
    /// <remarks>
    /// The reference fills them from the race in <c>HeroBase::LoadDefaults()</c>. The port keeps the object
    /// on the hero so that <see cref="GetMaxMovePoints"/> and the rest of the map logic have something to
    /// ask; the initial values are set where the reference sets them, which is not here yet.
    /// </remarks>
    public SecSkills SecondarySkills => _secondarySkills;

    /// <summary>
    /// Movement points left today, <c>GetMovePoints()</c> (<c>heroes.h</c>). A hero that has not been given
    /// a day yet has none, which is what <c>_movePoints = 0</c> means in the reference.
    /// </summary>
    public uint MovePoints => _movePoints;

    /// <summary>Sets the movement points, <c>SetMovePoints()</c>.</summary>
    /// <param name="movePoints">Points left.</param>
    public void SetMovePoints(uint movePoints) => _movePoints = movePoints;

    /// <summary>Spends the whole day's movement, <c>ResetMovePoints()</c> (<c>heroes.h</c>).</summary>
    /// <remarks>Digging for the ultimate artifact is the one action that costs a hero his whole day.</remarks>
    public void ResetMovePoints() => _movePoints = 0;

    /// <summary>
    /// Adds movement points, <c>IncreaseMovePoints()</c>. No clamping: the reference does not clamp either,
    /// which is how the Stables bonus can push a hero above his daily maximum.
    /// </summary>
    /// <param name="movePoints">Points to add.</param>
    public void IncreaseMovePoints(uint movePoints) => _movePoints += movePoints;

    /// <summary>
    /// The movement points a hero gets per day, <c>Heroes::GetMaxMovePoints()</c>
    /// (<c>heroes.cpp:972</c>).
    /// </summary>
    /// <remarks>
    /// On land the base comes from the slowest stack of the army — 1000 to 1500 points for the six speed
    /// levels with a stack, zero for an empty army — then the Logistics skill scales it. On water the base
    /// is a flat 1500 and the Navigation skill scales it, because the ship does not care what the hero
    /// loaded aboard.
    /// </remarks>
    /// <param name="onWater">Whether the hero is sailing.</param>
    /// <param name="world">
    /// The world, for the captured lighthouses of the water branch. <see langword="null"/> counts no
    /// lighthouse, which is what a hero outside a world would have.
    /// </param>
    /// <returns>The points for one day.</returns>
    public uint GetMaxMovePoints(bool onWater = false, WorldMap? world = null)
    {
        if (onWater)
        {
            // Initial mobility on water does not depend on the composition of the army.
            uint onWaterPoints = UpdateMovementPoints(1500, SecondarySkill.Navigation);

            // Artifact bonuses (SEA_MOBILITY) are not ported; see the class remarks.
            onWaterPoints += 500 * (world?.CountCapturedObject(Mp2ObjectType.Lighthouse, Color) ?? 0);

            return onWaterPoints;
        }

        uint result = 0;

        TroopStack? troop = _army.GetSlowestTroop();

        if (troop is not null)
        {
            result = troop.GetSpeed() switch
            {
                MonsterSpeed.VerySlow => 1000,
                MonsterSpeed.Slow => 1100,
                MonsterSpeed.Average => 1200,
                MonsterSpeed.Fast => 1300,
                MonsterSpeed.VeryFast => 1400,
                MonsterSpeed.UltraFast => 1500,
                _ => 0,
            };
        }

        result = UpdateMovementPoints(result, SecondarySkill.Logistics);

        // Artifact bonuses (LAND_MOBILITY) and the Stables bonus are not ported; see the class remarks.

        return result;
    }

    /// <summary>
    /// Scales movement points by a secondary skill, <c>Heroes::_updateMovementPoints()</c>
    /// (<c>heroes.cpp:1782</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two round numbers are special-cased rather than going through the percentage: 33% is exactly a
    /// third and 66% is exactly two thirds, so the reference multiplies by 4/3 and 5/3 instead of adding
    /// <c>value * points / 100</c>. That keeps the result a whole number of thirds of the base and matches
    /// what the original game shows; for a base of 1500 the two forms agree, for 1000 they do not (1333 vs
    /// 1330).
    /// </para>
    /// <para>
    /// The multiplication is done in <see cref="uint"/> on purpose: the reference multiplies
    /// <c>uint32_t</c> values too, and a base of 1500 never overflows.
    /// </para>
    /// </remarks>
    /// <param name="movePoints">Base points.</param>
    /// <param name="skill">Logistics or Navigation.</param>
    /// <returns>The scaled points.</returns>
    private uint UpdateMovementPoints(uint movePoints, SecondarySkill skill)
    {
        if (_secondarySkills.GetLevel(skill) == SkillLevel.None)
        {
            return movePoints;
        }

        uint skillValue = _secondarySkills.GetValue(skill);

        if (skillValue == 33)
        {
            return movePoints * 4 / 3;
        }

        if (skillValue == 66)
        {
            return movePoints * 5 / 3;
        }

        return movePoints + (skillValue * movePoints / 100);
    }

    /// <summary>
    /// How many days it takes to walk the route to a tile, <c>Heroes::getNumOfTravelDays()</c>
    /// (<c>heroes.cpp:1806-1840</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 一天一天地「刷卡」：当天剩下的移动力够走这一步就扣掉继续，不够就翻篇——新的一天从满值开始，前一天剩下的
    /// 点数不带过去，所以这个数是整数天，不是「总消耗 / 每日」的向上取整。
    /// </para>
    /// <para>
    /// 参考在这里 <c>assert( maxMovePoints >= stepPenalty )</c>：新的一天还走不动一步说明路线本身有问题（比如
    /// 惩罚超过上限）。端口照抄这个断言，越界时调试版会停下来。
    /// </para>
    /// </remarks>
    /// <param name="world">The world holding the pathfinder.</param>
    /// <param name="destinationIndex">The tile the route ends on.</param>
    /// <returns>The number of days, 0 when there is no route.</returns>
    public int GetNumOfTravelDays(World world, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(world);

        uint maxMovePoints = GetMaxMovePoints(IsShipMaster);
        List<RouteStep> routePath = world.GetPath(this, destinationIndex);

        if (routePath.Count == 0)
        {
            return 0;
        }

        uint movePoints = MovePoints;
        int days = 1;

        foreach (RouteStep step in routePath)
        {
            if (movePoints >= step.Penalty)
            {
                movePoints -= step.Penalty;
            }
            else
            {
                Debug.Assert(maxMovePoints >= step.Penalty, "A single step must fit into one day's movement points.");

                movePoints = maxMovePoints - step.Penalty;
                ++days;
            }
        }

        return days;
    }
}
