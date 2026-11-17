namespace FHeroes2.Game.Battle;

/// <summary>
/// How one side of the battle ended, <c>RESULT_*</c> in <c>battle.h:35-41</c>.
/// </summary>
/// <remarks>
/// The values are the reference's, including the gap between <see cref="Surrender"/> and <see cref="Wins"/>: the
/// low four bits are the "this side lost" reasons, and winning is a single high bit rather than a fourth reason, so
/// a winning side never carries a loss bit.
/// </remarks>
[Flags]
public enum BattleOutcome : uint
{
    /// <summary>Nothing has been decided yet, or this side took no part.</summary>
    None = 0,

    /// <summary><c>RESULT_LOSS</c>: wiped out.</summary>
    Loss = 0x01,

    /// <summary><c>RESULT_RETREAT</c>: the hero fled.</summary>
    Retreat = 0x02,

    /// <summary><c>RESULT_SURRENDER</c>: the hero paid the enemy off.</summary>
    Surrender = 0x04,

    /// <summary><c>RESULT_WINS</c>: the other side is gone.</summary>
    Wins = 0x80,
}

/// <summary>
/// What a battle reports back, <c>Battle::Result</c> (<c>battle.h:43-66</c>).
/// </summary>
/// <remarks>
/// The reference keeps the two outcomes as raw <c>uint</c> flags and reduces them to a single value through
/// <c>getBattleResult()</c>, which prefers surrender over retreat over loss over winning. That precedence is the
/// only place the combinations are resolved, so it is kept in <see cref="GetOutcome"/> rather than spread over the
/// callers.
/// </remarks>
public struct BattleResult
{
    /// <summary>The attacker's flags, <c>attacker</c>.</summary>
    public BattleOutcome Attacker;

    /// <summary>The defender's flags, <c>defender</c>.</summary>
    public BattleOutcome Defender;

    /// <summary>Experience the attacker's hero earns, <c>attackerExperience</c>.</summary>
    public uint AttackerExperience;

    /// <summary>Experience the defender's hero earns, <c>defenderExperience</c>.</summary>
    public uint DefenderExperience;

    /// <summary>
    /// How many creatures died on both sides combined, <c>numOfDeadUnitsForNecromancy</c> — the necromancy skill's
    /// input, so it counts the enemy's dead as well as the hero's own.
    /// </summary>
    public uint NumOfDeadUnitsForNecromancy;

    /// <summary>
    /// <c>isAttackerWin()</c>.
    /// </summary>
    /// <returns><see langword="true"/> if the attacker holds <see cref="BattleOutcome.Wins"/>.</returns>
    public readonly bool IsAttackerWin => (Attacker & BattleOutcome.Wins) != 0;

    /// <summary>
    /// <c>isDefenderWin()</c>.
    /// </summary>
    /// <returns><see langword="true"/> if the defender holds <see cref="BattleOutcome.Wins"/>.</returns>
    public readonly bool IsDefenderWin => (Defender & BattleOutcome.Wins) != 0;

    /// <summary>
    /// <c>getAttackerResult()</c>.
    /// </summary>
    /// <returns>The single outcome the attacker's flags reduce to.</returns>
    public readonly BattleOutcome GetAttackerOutcome() => GetOutcome(Attacker);

    /// <summary>
    /// <c>getDefenderResult()</c>.
    /// </summary>
    /// <returns>The single outcome the defender's flags reduce to.</returns>
    public readonly BattleOutcome GetDefenderOutcome() => GetOutcome(Defender);

    /// <summary>
    /// <c>getBattleResult()</c> (<c>battle_main.cpp:163-179</c>): the flags reduced to one outcome.
    /// </summary>
    /// <remarks>
    /// The order is the reference's and it matters: a hero who surrenders after retreating is reported as having
    /// surrendered, and any loss reason wins over the win bit.
    /// </remarks>
    /// <param name="outcome">The side's flags.</param>
    /// <returns>The one outcome that describes the side.</returns>
    public static BattleOutcome GetOutcome(BattleOutcome outcome)
    {
        if ((outcome & BattleOutcome.Surrender) != 0)
        {
            return BattleOutcome.Surrender;
        }

        if ((outcome & BattleOutcome.Retreat) != 0)
        {
            return BattleOutcome.Retreat;
        }

        if ((outcome & BattleOutcome.Loss) != 0)
        {
            return BattleOutcome.Loss;
        }

        if ((outcome & BattleOutcome.Wins) != 0)
        {
            return BattleOutcome.Wins;
        }

        return BattleOutcome.None;
    }
}
