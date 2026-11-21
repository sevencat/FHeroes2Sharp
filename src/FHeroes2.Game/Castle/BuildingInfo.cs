using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Castle;

/// <summary>
/// One building as the town screen sees it: what the building really is after the upgrade and necromancer rules,
/// what the owner may do with it, and the rectangle it occupies — <c>castle/buildinginfo.cpp:214-315</c> and
/// <c>:564-610</c>.
/// </summary>
/// <remarks>
/// <para>
/// The constructor is a chain of four rewrites and the order matters. A dwelling is first resolved to the building
/// whose creature actually lives there (<see cref="Castle.GetActualDwelling"/>), then a built building is replaced by
/// what it can be upgraded into (<see cref="Castle.GetUpgradeBuilding"/>), then the necromancer's tavern becomes the
/// shrine or nothing at all, and only then is the status asked for. A town that has built its tavern asks about the
/// shrine; a town that has not asks about the tavern and is told it is disabled.
/// </para>
/// <para>
/// The reference sizes the captain's rectangle from the sprite it will draw
/// (<c>Assets::getImage( ICN::getCaptainIcnId( race ), built ? 1 : 0 )</c>). The port has no ICN here, so the
/// rectangle keeps the default <c>135x70</c> of the reference until the town screen can ask the renderer.
/// </para>
/// </remarks>
public sealed class BuildingInfo
{
    private readonly Castle _castle;
    private readonly BuildingType _building;
    private readonly BuildingStatus _status;
    private readonly string _description;

    private BuildingType _buildingType;
    private Rect _area;

    /// <summary>
    /// Answers the town screen's questions about one building, <c>BuildingInfo::BuildingInfo()</c>
    /// (<c>buildinginfo.cpp:214-245</c>).
    /// </summary>
    /// <param name="castle">The town the building belongs to.</param>
    /// <param name="building">The building as the screen names it — often a base dwelling, never an upgrade.</param>
    /// <param name="treasury">The owner's resources, needed to answer <see cref="Status"/>.</param>
    public BuildingInfo(Castle castle, BuildingType building, in Funds treasury)
    {
        ArgumentNullException.ThrowIfNull(castle);

        _castle = castle;
        _building = building;
        _buildingType = building;
        _area = new Rect(0, 0, 135, 70);

        // The reference resolves the dwelling first and then overwrites the answer with the upgrade question below;
        // the first call is a no-op in the end, but it is kept because the two rewrites disagree on purpose for an
        // upgraded dwelling: GetActualDwelling answers the built upgrade, GetUpgradeBuilding the next one up.
        if (CastleBuildings.IsDwelling(_buildingType))
        {
            _buildingType = castle.GetActualDwelling(building);
        }

        _buildingType = castle.IsBuild(building) ? castle.GetUpgradeBuilding(building) : building;

        if (_buildingType == BuildingType.Tavern && castle.Race == MapRace.Necromancer)
        {
            _buildingType = castle.Version is MapVersion.PriceOfLoyalty or MapVersion.Resurrection
                ? BuildingType.Shrine
                : BuildingType.Nothing;
        }

        _status = castle.CheckBuyBuilding(_buildingType, treasury);

        _description = _status is BuildingStatus.BuildDisabled or BuildingStatus.ShipyardNotAllowed
            ? GetConditionDescription()
            : GetBuildingDescription(castle.Race, _buildingType);
    }

    /// <summary>The town the building belongs to.</summary>
    public Castle Castle => _castle;

    /// <summary>The building the screen asked about, <c>BuildingInfo::building</c>.</summary>
    public BuildingType Building => _building;

    /// <summary>
    /// The building after the rewrites — what will actually be built or upgraded, <c>BuildingInfo::_buildingType</c>.
    /// </summary>
    public BuildingType Type => _buildingType;

    /// <summary>What the owner may do with <see cref="Type"/> right now.</summary>
    public BuildingStatus Status => _status;

    /// <summary>The price of <see cref="Type"/> for this town's race.</summary>
    public Funds Cost => CastleBuildings.GetCost(_buildingType, _castle.Race);

    /// <summary>The name the screen and the status bar print for <see cref="Type"/>.</summary>
    public string Name => CastleBuildingInfo.GetName(_castle.Race, _buildingType);

    /// <summary>The text the town screen shows for <see cref="Type"/>.</summary>
    public string Description => _description;

    /// <summary>The rectangle the building occupies in the town view.</summary>
    public Rect Area => _area;

    /// <summary>
    /// Moves the building to a spot of the town view, <c>BuildingInfo::SetPos()</c> (<c>buildinginfo.cpp:247-251</c>).
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    public void SetPosition(int x, int y) => _area = new Rect(x, y, _area.Width, _area.Height);

    /// <summary>
    /// Moves the building to a spot of the town view, <c>BuildingInfo::SetPos()</c> (<c>buildinginfo.cpp:247-251</c>).
    /// </summary>
    /// <param name="position">The top-left corner.</param>
    public void SetPosition(Point position) => SetPosition(position.X, position.Y);

    /// <summary>
    /// The description of a building or of its creature, <c>BuildingInfo::getBuildingDescription()</c>
    /// (<c>buildinginfo.cpp:275-315</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dwelling is described by what it produces, in the creature's plural
    /// ("The Archers' Tower produces archers."), because its own table entry says nothing about the creature. A
    /// random town's dwelling names the level instead of a creature, since no race has been picked yet.
    /// </para>
    /// <para>
    /// Everything else is the race's table text with <c>%{count}</c> filled in, and the four texts with a number are
    /// the growth of the two wells, the moat's defense penalty, and the gold of the keep, the statue and the special
    /// building. The reference lowercases the creature name only when it is not the first word of the sentence via
    /// <c>StringReplaceWithLowercase</c>; the template puts it after "produces", so the port lowercases it directly.
    /// </para>
    /// </remarks>
    /// <param name="race">The town's race, <see cref="MapRace.Random"/> for a random town.</param>
    /// <param name="buildingId">The building to describe.</param>
    /// <returns>The description, with its placeholders replaced.</returns>
    public static string GetBuildingDescription(MapRace race, BuildingType buildingId)
    {
        if (CastleBuildings.IsDwelling(buildingId))
        {
            string description = "The %{building} produces %{monster}.";
            description = description.Replace(
                "%{building}",
                CastleBuildingInfo.GetName(race, buildingId),
                StringComparison.Ordinal);

            string monster = race == MapRace.Random
                ? MonsterNames.GetRandomRaceMonstersName(buildingId)
                : MonsterNames.GetMulti(DwellingMonsters.FromDwelling(race, buildingId)).ToLowerInvariant();

            return description.Replace("%{monster}", monster, StringComparison.Ordinal);
        }

        string table = CastleBuildingInfo.GetDescription(race, buildingId);

        return buildingId switch
        {
            BuildingType.Well => table.Replace("%{count}", Castle.GrownWell.ToString(), StringComparison.Ordinal),
            BuildingType.Well2 => table.Replace("%{count}", Castle.GrownWel2.ToString(), StringComparison.Ordinal),
            BuildingType.Moat => table.Replace(
                "%{count}",
                GameStatic.BattleMoatReduceDefense.ToString(),
                StringComparison.Ordinal),
            BuildingType.Castle or BuildingType.Statue or BuildingType.Spec => table.Replace(
                "%{count}",
                ProfitConditions.FromBuilding(buildingId, race).Gold.ToString(),
                StringComparison.Ordinal),
            _ => table,
        };
    }

    /// <summary>
    /// The line the town screen shows at the bottom while the pointer is over a building,
    /// <c>buildingStatusMessage()</c> (<c>castle_dialog.cpp:118-131</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dwelling offers its creature, and the words are <c>"Recruit %{name}"</c> with the creature in the plural and
    /// <b>lower case</b> — the reference replaces the placeholder through
    /// <c>StringReplaceWithLowercase</c>, so the sentence starts "Recruit peasants" rather than "Recruit Peasants".
    /// Everything else shows its own name.
    /// </para>
    /// <para>
    /// The test is the two dwelling masks of the reference, which is what <see cref="CastleBuildings.IsDwelling"/>
    /// already is; a random town, whose dwelling has no creature yet, names the level instead.
    /// </para>
    /// </remarks>
    /// <param name="race">The town's race, <see cref="MapRace.Random"/> for a random town.</param>
    /// <param name="buildingId">The building under the pointer.</param>
    /// <returns>The status line.</returns>
    public static string GetStatusLine(MapRace race, BuildingType buildingId)
    {
        if (!CastleBuildings.IsDwelling(buildingId))
        {
            return CastleBuildingInfo.GetName(race, buildingId);
        }

        string monster = race == MapRace.Random
            ? MonsterNames.GetRandomRaceMonstersName(buildingId)
            : MonsterNames.GetMulti(DwellingMonsters.FromDwelling(race, buildingId)).ToLowerInvariant();

        return "Recruit " + monster;
    }

    /// <summary>
    /// Why the building cannot be built, <c>BuildingInfo::GetConditionDescription()</c>
    /// (<c>buildinginfo.cpp:564-610</c>).
    /// </summary>
    /// <remarks>
    /// Only the two statuses that have no text of their own in the reference's main switch come from the shared
    /// <c>GetBuildConditionDescription()</c>; the rest name the building so the player knows which one is meant.
    /// </remarks>
    /// <returns>The text, empty for a status with no answer.</returns>
    public string GetConditionDescription() => _status switch
    {
        BuildingStatus.NotToday => "Cannot build. You have already built here today.",
        BuildingStatus.NeedCastle => "For this action it is necessary to build a castle first.",
        BuildingStatus.ShipyardNotAllowed => ShipyardTooFarFromWater(),
        BuildingStatus.BuildDisabled => "This building has been disabled.",
        BuildingStatus.LackResources => "Cannot afford the " + Name + ".",
        BuildingStatus.AlreadyBuilt => "The " + Name + " is already built.",
        BuildingStatus.RequiresBuild => "Cannot build the " + Name + ".",
        BuildingStatus.AllowBuild => "Build " + Name + ".",
        _ => string.Empty,
    };

    private string ShipyardTooFarFromWater()
    {
        Debug.Assert(_buildingType == BuildingType.Shipyard, "only the shipyard has this status");

        return "Cannot build " + CastleBuildingInfo.GetName(_castle.Race, BuildingType.Shipyard) +
               ". The castle is too far away from an ocean.";
    }
}
