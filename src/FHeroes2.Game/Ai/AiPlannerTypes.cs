using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Ai;

/// <summary>
/// What the strategic AI wants a hero to do next, <c>AI::PriorityTaskType</c> (<c>ai_planner.h:65-75</c>).
/// </summary>
/// <remarks>
/// The values are the reference's and only ever travel inside a running turn: a priority task is never saved, so
/// the numbering is free to follow the header.
/// </remarks>
public enum PriorityTaskType
{
    /// <summary>Besiege or chase the selected enemy castle or hero.</summary>
    Attack = 0,

    /// <summary>Defend and garrison a friendly castle.</summary>
    Defend = 1,

    /// <summary>Bring troops to a friendly castle or hero.</summary>
    Reinforce = 2,
}

/// <summary>
/// The strategic evaluation of one map region, <c>AI::RegionStats</c> (<c>ai_planner.h:78-87</c>).
/// </summary>
/// <remarks>
/// The reference notes this structure is not kept up to date while AI heroes act, so it is a snapshot taken by
/// <c>evaluateRegionSafety()</c> and read during the same turn. <see cref="HighestThreat"/> starts at <c>-1</c>, which
/// means "not evaluated yet" rather than "no threat".
/// </remarks>
public sealed class RegionStats
{
    /// <summary>Whether the region has been evaluated this turn.</summary>
    public bool Evaluated { get; set; }

    /// <summary>Strength of the strongest enemy stack seen in the region, <c>-1</c> before evaluation.</summary>
    public double HighestThreat { get; set; } = -1;

    /// <summary>Number of friendly heroes inside the region.</summary>
    public int FriendlyHeroes { get; set; }

    /// <summary>Number of friendly castles inside the region.</summary>
    public int FriendlyCastles { get; set; }

    /// <summary>Number of enemy castles inside the region.</summary>
    public int EnemyCastles { get; set; }

    /// <summary>How safe the region is, higher is better.</summary>
    public int SafetyFactor { get; set; }

    /// <summary>Level of the spells the AI may cast here, <c>2</c> until the region is evaluated.</summary>
    public int SpellLevel { get; set; } = 2;
}

/// <summary>
/// A castle with the numbers the AI ranks it by, <c>AI::AICastle</c> (<c>ai_planner.h:89-104</c>).
/// </summary>
public sealed class AiCastle
{
    /// <summary>Ranks a castle for the AI.</summary>
    /// <param name="castle">The castle; must not be <see langword="null"/>.</param>
    /// <param name="underThreat">Whether an enemy army can reach the castle this turn.</param>
    /// <param name="safetyFactor">How safe the castle's region is.</param>
    /// <param name="buildingValue">How much the AI wants to build here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    public AiCastle(CastleTown castle, bool underThreat, int safetyFactor, int buildingValue)
    {
        ArgumentNullException.ThrowIfNull(castle);

        Castle = castle;
        UnderThreat = underThreat;
        SafetyFactor = safetyFactor;
        BuildingValue = buildingValue;
    }

    /// <summary>The castle itself.</summary>
    public CastleTown Castle { get; }

    /// <summary>Whether an enemy army can reach the castle this turn.</summary>
    public bool UnderThreat { get; }

    /// <summary>How safe the castle's region is.</summary>
    public int SafetyFactor { get; }

    /// <summary>How much the AI wants to build here.</summary>
    public int BuildingValue { get; }
}

/// <summary>
/// One line of the AI's budget, <c>AI::BudgetEntry</c> (<c>ai_planner.h:106-123</c>).
/// </summary>
/// <remarks>
/// The budget is a fixed array of seven entries, one per resource, and every entry remembers how much of that resource
/// is still missing for the plans of the current turn. <see cref="Reset"/> is called when a new turn starts.
/// </remarks>
public sealed class BudgetEntry
{
    /// <summary>Starts an entry for <paramref name="resource"/>.</summary>
    /// <param name="resource">One of the <see cref="Resource"/> constants, <see cref="Resource.Unknown"/> when unknown.</param>
    public BudgetEntry(int resource) => Resource = resource;

    /// <summary>The resource this entry tracks, one of the <see cref="Resource"/> constants.</summary>
    public int Resource { get; }

    /// <summary>How much of the resource is still missing for the plans of this turn.</summary>
    public int Missing { get; set; }

    /// <summary>Whether the plans of this turn need the resource urgently.</summary>
    public bool Priority { get; set; }

    /// <summary>Whether the spend repeats every turn (income, not a one-off).</summary>
    public bool RecurringCost { get; set; }

    /// <summary>Clears the three mutable numbers, keeping the resource, <c>BudgetEntry::reset()</c>.</summary>
    public void Reset()
    {
        Missing = 0;
        Priority = false;
        RecurringCost = false;
    }
}

/// <summary>
/// An enemy army the strategic AI keeps an eye on, <c>AI::EnemyArmy</c> (<c>ai_planner.h:125-140</c>).
/// </summary>
public sealed class EnemyArmy
{
    /// <summary>An entry with no army in it.</summary>
    public EnemyArmy()
    {
    }

    /// <summary>An entry for one known enemy hero.</summary>
    /// <param name="index">Tile index the army was seen at.</param>
    /// <param name="hero">The enemy hero, or <see langword="null"/> when the army is a neutral one.</param>
    /// <param name="strength">Strength of the army.</param>
    /// <param name="movePoints">Move points the army has left this turn.</param>
    public EnemyArmy(int index, Hero? hero, double strength, uint movePoints)
    {
        Index = index;
        Hero = hero;
        Strength = strength;
        MovePoints = movePoints;
    }

    /// <summary>Tile index the army was seen at, <c>-1</c> when unknown.</summary>
    public int Index { get; } = -1;

    /// <summary>The enemy hero, or <see langword="null"/> for a neutral army.</summary>
    public Hero? Hero { get; }

    /// <summary>Strength of the army.</summary>
    public double Strength { get; }

    /// <summary>Move points the army has left this turn.</summary>
    public uint MovePoints { get; }
}

/// <summary>
/// A task the heroes should focus on, <c>AI::PriorityTask</c> (<c>ai_planner.h:142-158</c>).
/// </summary>
/// <remarks>
/// The reference keeps the secondary tile ids in a <c>std::set</c>; the port uses a <see cref="HashSet{T}"/> because
/// nothing iterates it in order - it is only asked whether a tile is in it.
/// </remarks>
public sealed class PriorityTask
{
    /// <summary>An attack task without a target yet.</summary>
    public PriorityTask()
    {
    }

    /// <summary>A task of the given type.</summary>
    /// <param name="type">What the heroes should do.</param>
    public PriorityTask(PriorityTaskType type) => Type = type;

    /// <summary>A task of the given type with one secondary tile.</summary>
    /// <param name="type">What the heroes should do.</param>
    /// <param name="secondaryTaskTileIndex">First tile the task points at.</param>
    public PriorityTask(PriorityTaskType type, int secondaryTaskTileIndex)
    {
        Type = type;
        SecondaryTaskTileIndexes.Add(secondaryTaskTileIndex);
    }

    /// <summary>What the heroes should do.</summary>
    public PriorityTaskType Type { get; } = PriorityTaskType.Attack;

    /// <summary>Tiles the task points at, <c>secondaryTaskTileId</c> of the reference.</summary>
    public HashSet<int> SecondaryTaskTileIndexes { get; } = [];
}
