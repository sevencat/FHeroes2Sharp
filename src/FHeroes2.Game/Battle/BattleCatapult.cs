using System.Diagnostics;

using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The attacker's catapult and the four to six shots it gets at a castle's walls — <c>battle_catapult.h</c> and
/// <c>battle_catapult.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// All of the catapult's numbers come from one skill and one artifact: ballistics decides how many shots there are, how
/// likely each is to bring the wall down and whether it can miss at all, and the ballista's extra-shot artifact only
/// adds shots. A hero with no ballistics fires a single shot that may miss; an expert fires two that never miss and
/// always do double damage.
/// </para>
/// <para>
/// Every roll is taken from the battle's generator, passed in rather than kept here, so that a replay of the same
/// battle draws the same numbers.
/// </para>
/// </remarks>
public sealed class BattleCatapult
{
    /// <summary>
    /// Builds the catapult of a hero, <c>Battle::Catapult::Catapult()</c> (<c>battle_catapult.cpp:39-67</c>).
    /// </summary>
    /// <param name="hero">The hero who commands the attack, whose ballistics and artifacts apply.</param>
    public BattleCatapult(IArmyCommander hero)
    {
        Shots = 1;
        DoubleDamageChance = 25;
        CanMiss = true;

        switch (hero.GetLevelSkill(SecondarySkill.Ballistics))
        {
        case SkillLevel.Basic:
            DoubleDamageChance = 50;
            CanMiss = false;
            break;

        case SkillLevel.Advanced:
            DoubleDamageChance = 50;
            Shots += 1;
            CanMiss = false;
            break;

        case SkillLevel.Expert:
            DoubleDamageChance = 100;
            Shots += 1;
            CanMiss = false;
            break;
        }

        Shots += (uint)hero.BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.ExtraCatapultShots);
    }

    /// <summary>How many shots the catapult has, <c>Catapult::GetShots()</c>.</summary>
    public uint Shots { get; private set; }

    /// <summary>The chance of a shot doing double damage, in percent.</summary>
    public uint DoubleDamageChance { get; private set; }

    /// <summary>Whether a shot can miss the wall it aims at.</summary>
    public bool CanMiss { get; private set; }

    /// <summary>
    /// The damage of one shot, <c>Catapult::GetDamage()</c> (<c>battle_catapult.cpp:69-77</c>).
    /// </summary>
    /// <remarks>
    /// One or two wall sections — a shot never does anything in between. The reference logs the double hit at trace
    /// level; the port has no logger, and the value is what the walls are destroyed by.
    /// </remarks>
    /// <param name="randomGenerator">The battle's generator.</param>
    /// <returns>1, or 2 for a lucky (or expert) shot.</returns>
    public int GetDamage(Pcg32 randomGenerator)
    {
        if (DoubleDamageChance == 100 || DoubleDamageChance >= Rand.GetWithGen(1U, 100U, randomGenerator))
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// Everything the catapult may aim at, <c>Catapult::getAllowedTargets()</c> (<c>battle_catapult.cpp:79-83</c>).
    /// </summary>
    /// <remarks>
    /// The four wall sections, the two turrets, the drawbridge and the keep. The two small towers beside the bridge are
    /// not targets: they are drawn only when the bridge goes down.
    /// </remarks>
    /// <returns>The targets, in the reference's order.</returns>
    public static CastleDefenseStructure[] GetAllowedTargets() =>
    [
        CastleDefenseStructure.Wall1,
        CastleDefenseStructure.Wall2,
        CastleDefenseStructure.Wall3,
        CastleDefenseStructure.Wall4,
        CastleDefenseStructure.Tower1,
        CastleDefenseStructure.Tower2,
        CastleDefenseStructure.Bridge,
        CastleDefenseStructure.CentralTower
    ];

    /// <summary>
    /// Where the shot is drawn from and to, <c>Catapult::GetTargetPosition()</c> (<c>battle_catapult.cpp:85-113</c>).
    /// </summary>
    /// <remarks>
    /// Two positions per target: the hit one (the wall itself) and the miss one (a little further along the
    /// trajectory). Not a target the catapult picks — the arena uses it to place the stone.
    /// </remarks>
    /// <param name="target">The structure the shot is aimed at.</param>
    /// <param name="hit">Whether the shot hits.</param>
    /// <returns>The screen position, or the origin for a structure with no such point.</returns>
    public static Point GetTargetPosition(CastleDefenseStructure target, bool hit) => target switch
    {
        CastleDefenseStructure.Wall1 => hit ? new Point(475, 45) : new Point(495, 105),
        CastleDefenseStructure.Wall2 => hit ? new Point(420, 115) : new Point(460, 175),
        CastleDefenseStructure.Wall3 => hit ? new Point(415, 280) : new Point(455, 280),
        CastleDefenseStructure.Wall4 => hit ? new Point(490, 390) : new Point(530, 390),
        CastleDefenseStructure.Tower1 => hit ? new Point(430, 40) : new Point(490, 120),
        CastleDefenseStructure.Tower2 => hit ? new Point(430, 300) : new Point(490, 340),
        CastleDefenseStructure.Bridge => hit ? new Point(400, 195) : new Point(450, 235),
        CastleDefenseStructure.CentralTower => hit ? new Point(580, 160) : new Point(610, 320),
        CastleDefenseStructure.TopBridgeTower => hit ? new Point(395, 152) : new Point(445, 202),
        CastleDefenseStructure.BottomBridgeTower => hit ? new Point(395, 240) : new Point(445, 290),
        _ => default
    };

    /// <summary>
    /// Picks the next target, <c>Catapult::GetTarget()</c> (<c>battle_catapult.cpp:115-172</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The priority is strict and is the whole of the catapult's tactics: the wall sections while any stands, then the
    /// turrets, then the drawbridge, then the keep. Only within one tier is the choice random, and the tier is picked
    /// afresh for every shot.
    /// </para>
    /// <para>
    /// The dictionary holds how much of each structure is left; a zero takes it out of the running. The reference
    /// asserts that every target it asks about is present, so a missing key is a caller error rather than "already
    /// destroyed".
    /// </para>
    /// </remarks>
    /// <param name="stateOfCatapultTargets">The remaining state of each structure.</param>
    /// <param name="randomGenerator">The battle's generator.</param>
    /// <returns>The structure to aim at, or <see cref="CastleDefenseStructure.None"/> for none.</returns>
    public static CastleDefenseStructure GetTarget(
        IReadOnlyDictionary<CastleDefenseStructure, int> stateOfCatapultTargets,
        Pcg32 randomGenerator)
    {
        bool CheckTargetState(CastleDefenseStructure target)
        {
            if (!stateOfCatapultTargets.TryGetValue(target, out int state))
            {
                Debug.Assert(false, "the arena has no state for this target");
                return false;
            }

            return state > 0;
        }

        List<CastleDefenseStructure> targets = new(4);

        if (CheckTargetState(CastleDefenseStructure.Wall1))
        {
            targets.Add(CastleDefenseStructure.Wall1);
        }

        if (CheckTargetState(CastleDefenseStructure.Wall2))
        {
            targets.Add(CastleDefenseStructure.Wall2);
        }

        if (CheckTargetState(CastleDefenseStructure.Wall3))
        {
            targets.Add(CastleDefenseStructure.Wall3);
        }

        if (CheckTargetState(CastleDefenseStructure.Wall4))
        {
            targets.Add(CastleDefenseStructure.Wall4);
        }

        if (targets.Count == 0)
        {
            if (CheckTargetState(CastleDefenseStructure.Tower1))
            {
                targets.Add(CastleDefenseStructure.Tower1);
            }

            if (CheckTargetState(CastleDefenseStructure.Tower2))
            {
                targets.Add(CastleDefenseStructure.Tower2);
            }
        }

        if (targets.Count == 0 && CheckTargetState(CastleDefenseStructure.Bridge))
        {
            targets.Add(CastleDefenseStructure.Bridge);
        }

        if (targets.Count == 0 && CheckTargetState(CastleDefenseStructure.CentralTower))
        {
            targets.Add(CastleDefenseStructure.CentralTower);
        }

        if (targets.Count > 0)
        {
            return Rand.GetWithGen(targets, randomGenerator);
        }

        return CastleDefenseStructure.None;
    }

    /// <summary>
    /// Whether the next shot connects at all, <c>Catapult::IsNextShotHit()</c> (<c>battle_catapult.cpp:174-178</c>).
    /// </summary>
    /// <remarks>
    /// A quarter of the shots miss, unless ballistics says otherwise: the reference rolls 1..20 and needs at least a 6.
    /// Note that the roll is only taken when the shot can miss, so a hero with ballistics does not consume the
    /// generator here — replays depend on it.
    /// </remarks>
    /// <param name="randomGenerator">The battle's generator.</param>
    /// <returns><see langword="true"/> if the shot hits the wall.</returns>
    public bool IsNextShotHit(Pcg32 randomGenerator) => !CanMiss || Rand.GetWithGen(1U, 20U, randomGenerator) >= 6;
}
