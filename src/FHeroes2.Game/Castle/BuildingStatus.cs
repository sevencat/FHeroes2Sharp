namespace FHeroes2.Game.Castle;

/// <summary>
/// Whether a building can be bought right now, and when it cannot, why,
/// <c>enum class BuildingStatus</c> (<c>castle.h:111-123</c>).
/// </summary>
/// <remarks>
/// <para>
/// The castle dialog shows the answer as a tooltip ("not today", "requires a castle", "not enough
/// resources"); the AI reads it as a first-match-wins priority in <c>Castle::GetAllBuildingStatus</c>. Both uses
/// are why the reasons are an enumeration rather than a boolean.
/// </para>
/// <para>
/// The order of the enumerators is the reference's and is <b>not</b> a priority: the priorities are decided in
/// <see cref="Castle.GetAllBuildingStatus"/>, which asks three questions in turn.
/// </para>
/// </remarks>
public enum BuildingStatus
{
    /// <summary>
    /// Nothing is available, <c>UNKNOWN_COND</c> — the fallback of
    /// <see cref="Castle.GetAllBuildingStatus"/> when every remaining building is disabled, needs a prerequisite
    /// that is itself unbuildable, or is impossible for the town (the warlock's seventh dwelling).
    /// </summary>
    UnknownCondition = 0,

    /// <summary>It can be built now, <c>ALLOW_BUILD</c>.</summary>
    AllowBuild,

    /// <summary>
    /// Something else was built today, <c>NOT_TODAY</c>. One building per town per day.
    /// </summary>
    NotToday,

    /// <summary>Already built, <c>ALREADY_BUILT</c>.</summary>
    AlreadyBuilt,

    /// <summary>
    /// The town is still a tent, <c>NEED_CASTLE</c> — nothing but the castle itself is for sale.
    /// </summary>
    NeedCastle,

    /// <summary>
    /// Ruled out by something other than a prerequisite, <c>BUILD_DISABLE</c>: the town has the castle and this is
    /// the tent, the map bans it, the necromancer has no tavern but does have a shrine, or the next level of the
    /// mage guild is not built yet.
    /// </summary>
    BuildDisabled,

    /// <summary>
    /// The shipyard, in a town that cannot reach water, <c>SHIPYARD_NOT_ALLOWED</c>.
    /// </summary>
    ShipyardNotAllowed,

    /// <summary>
    /// This town has no such upgrade, <c>UNKNOWN_UPGRADE</c> — the barbarian has no archer upgrade, the warlock is
    /// the only one with the black dragon lair.
    /// </summary>
    UnknownUpgrade,

    /// <summary>
    /// A building in <see cref="BuildingType"/>'s prerequisite mask is missing, <c>REQUIRES_BUILD</c>.
    /// </summary>
    RequiresBuild,

    /// <summary>
    /// Everything else is satisfied but the treasury is short, <c>LACK_RESOURCES</c>.
    /// </summary>
    LackResources,
}
