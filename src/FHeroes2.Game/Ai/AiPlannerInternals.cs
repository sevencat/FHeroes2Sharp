namespace FHeroes2.Game.Ai;

/// <summary>
/// The numbers <c>ai_planner_internals.h</c> shares between the three planner translation units
/// (<c>ai_planner_internals.h:27-30</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference puts these in the header because <c>ai_planner_hero.cpp</c>, <c>ai_planner_castle.cpp</c> and
/// <c>ai_planner_kingdom.cpp</c> each include it; the port keeps them together for the same reason - the AI-side
/// hero decisions compare an army's strength against its enemy's, and the four thresholds have to agree.
/// </para>
/// <para>
/// <b>Not ported from the same header:</b> <c>AIWorldPathfinderStateRestorer</c>
/// (<c>ai_planner_internals.h:32-56</c>). It saves and restores two settings of <c>AIWorldPathfinder</c>, which does
/// not exist yet (ADR-091 ports the player side only), so the restorer has nothing to restore until that class
/// lands - it belongs to the AI pathfinder batch.
/// </para>
/// </remarks>
public static class AiPlannerInternals
{
    /// <summary>The AI accepts a fight it is not sure to win only above this ratio, <c>ARMY_ADVANTAGE_DESPERATE</c>.</summary>
    public const double ArmyAdvantageDesperate = 0.8;

    /// <summary>A small advantage, <c>ARMY_ADVANTAGE_SMALL</c>.</summary>
    public const double ArmyAdvantageSmall = 1.3;

    /// <summary>A medium advantage, <c>ARMY_ADVANTAGE_MEDIUM</c>.</summary>
    public const double ArmyAdvantageMedium = 1.5;

    /// <summary>A large advantage, <c>ARMY_ADVANTAGE_LARGE</c>.</summary>
    public const double ArmyAdvantageLarge = 1.8;
}
