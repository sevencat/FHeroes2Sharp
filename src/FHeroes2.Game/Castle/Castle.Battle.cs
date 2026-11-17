using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The two hooks the battle calls on the town it is fought over, <c>castle.cpp:2249-2290</c>.
/// </summary>
/// <remarks>
/// <para>
/// Neither hook does much on its own: the reference's <c>ActionPreBattle</c> hands the work to the AI planner for
/// an AI town and to the visiting hero's army otherwise, and <c>ActionAfterBattle</c> only reacts to a lost defence.
/// The walls, the towers, the moat and the keep are set up by <c>Battle::Arena</c>, which the port does not have
/// yet, so nothing here touches them.
/// </para>
/// <para>
/// The visiting hero is an argument rather than a map lookup: the reference asks <c>world.GetHero( *this )</c>,
/// and the port's <see cref="Castle"/> has no way back to the <see cref="World"/>.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// What the town does before a battle, <c>Castle::ActionPreBattle()</c> (<c>castle.cpp:2249-2263</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a human defender the visiting hero folds the garrison into its own army, so the player fights with one
    /// stack of five instead of two armies. The merge follows the human rule of
    /// <see cref="Army.ArrangeForCastleDefense"/>: the garrison moves into the same slot numbers it occupied, and
    /// the player's own arrangement is left alone.
    /// </para>
    /// <para>
    /// An AI town is a gap: the reference calls <c>AI::Planner::CastlePreBattle</c>, which decides the defending
    /// army for the battle planner. The port has no battle planner, so the AI path returns unchanged.
    /// </para>
    /// </remarks>
    /// <param name="guestHero">The hero standing in the town, or <see langword="null"/> when there is none.</param>
    /// <param name="isControlAi">Whether the town is controlled by the AI, <c>isControlAI()</c>.</param>
    public void ActionPreBattle(Hero? guestHero, bool isControlAi)
    {
        if (isControlAi)
        {
            return;
        }

        if (guestHero is null)
        {
            return;
        }

        guestHero.Army.ArrangeForCastleDefense(_army, isControlAi: false);
    }

    /// <summary>
    /// What the town does after a battle, <c>Castle::ActionAfterBattle()</c> (<c>castle.cpp:2265-2290</c>).
    /// </summary>
    /// <remarks>
    /// Only a lost defence matters: the garrison is wiped and the town forgets that it was given a custom army, so
    /// that the next owner gets the race's default one. That is the whole of the reference's hook; the towers, the
    /// moat and the captain are the arena's own business. When the attacker lost, the town is untouched — the battle
    /// already removed the casualties.
    /// </remarks>
    /// <param name="attackerWins">Whether the attacker won the battle.</param>
    public void ActionAfterBattle(bool attackerWins)
    {
        if (!attackerWins)
        {
            return;
        }

        _army.Clean();
        HasCustomArmy = false;
    }
}
