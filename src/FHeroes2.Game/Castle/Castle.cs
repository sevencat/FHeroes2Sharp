using System.Numerics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using ArmyTroops = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Castle;

/// <summary>
/// A town: the buildings it owns, the creatures waiting in its dwellings and the garrison — <c>Castle</c>
/// (<c>castle.h:125</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is the skeleton, not the whole object. What is here is the part that needs neither a world nor a kingdom:
/// the construction bitmask and its consequences (<see cref="IsBuild"/>, <see cref="IsCastle"/>,
/// <see cref="CountBuildings"/>, <see cref="GetBuildingValue"/>, the mage guild level), the six dwelling stocks,
/// the daily build permission, the buy-a-building rules (<see cref="CheckBuyBuilding"/>, <see cref="BuyBuilding"/>,
/// <see cref="GetAllBuildingStatus"/>), recruiting (<see cref="GetRecruitLimit"/>, <see cref="RecruitMonster"/>), and
/// the 70-byte map block that builds a town out of a file (<see cref="LoadFromMP2"/>).
/// </para>
/// <para>
/// Three of the reference's globals had to become an argument or a property, because the port has no
/// <c>Kingdom</c>, no loaded <c>World</c> and no global <c>Settings</c> yet:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>GetKingdom().AllowPayment()</c> and <c>OddFundsResource()</c> are the <c>treasury</c> argument of
/// <see cref="CheckBuyBuilding"/> and <see cref="BuyBuilding"/>. When <c>Kingdom</c> lands it will pass
/// <c>kingdom.GetFunds()</c> and the callers need not change.
/// </description></item>
/// <item><description>
/// <c>Settings::Get().getCurrentMapInfo().version</c> is <see cref="Version"/>, a constructor argument: the map
/// version never changes once the town exists.
/// </description></item>
/// <item><description>
/// <c>HasSeaAccess()</c> — the reference scans the tiles around the town for water — is
/// <see cref="HasSeaAccess"/>, a property. The scan needs a loaded map; until the castle placement that goes with
/// it exists, the map loader (or a test) answers the question.
/// </description></item>
/// </list>
/// <para>
/// Not here yet, roughly in the order they are worth doing: the weekly growth (<c>ActionNewWeek</c>) and its well
/// bonuses, which need the world's week type; taxes, which need a kingdom; hiring heroes (<c>AllowBuyHero</c>,
/// <c>getAvailableArmy</c>); the mage guild's spells and the captain, which <see cref="LoadFromMP2"/> notes as
/// missing; boats (<c>AllowBuyBoat</c>, <c>BuyBoat</c>) and the AI's visit value and power modifier.
/// </para>
/// </remarks>
public sealed class Castle
{
    /// <summary>
    /// The number of creature dwellings a town can have, <c>Castle::maxNumOfDwellings</c> (<c>castle.h:129</c>).
    /// </summary>
    public const int MaxNumOfDwellings = 6;

    /// <summary>
    /// The size of a town's block in an MP2 map file, <c>MP2::MP2_CASTLE_STRUCTURE_SIZE</c> (<c>mp2.h:36</c>).
    /// </summary>
    public const int Mp2BlockSize = 70;

    /// <summary>The creatures waiting in each dwelling, <c>_dwelling[6]</c>.</summary>
    private readonly uint[] _dwelling = new uint[MaxNumOfDwellings];

    private readonly ArmyTroops _army;

    /// <summary>The buildings the town owns, <c>_constructedBuildings</c>, as a bitmask.</summary>
    private BuildingType _constructedBuildings;

    /// <summary>
    /// Creates a town, <c>Castle::Castle()</c> plus the parts of <c>_postLoad()</c> that need no map
    /// (<c>castle.cpp:529</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A new town may build today. The reference reaches that state at the end of <c>_postLoad()</c> rather than in
    /// the constructor; a port town that is not loaded from a map would otherwise have to wait for
    /// <see cref="ActionNewDay"/> before it could do anything at all.
    /// </para>
    /// <para>
    /// The garrison is empty and takes the town's race, which is the state <c>_postLoad()</c> leaves it in after
    /// <c>_army.SetColor( GetColor() )</c>.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the town stands on, <c>Castle::GetIndex()</c>.</param>
    /// <param name="race">The race of the town and of its garrison.</param>
    /// <param name="version">The map version, which decides whether the necromancer has a tavern or a shrine.</param>
    public Castle(int tileIndex, MapRace race, MapVersion version = MapVersion.SuccessionWars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileIndex);

        TileIndex = tileIndex;
        Race = race;
        Version = version;
        _army = new ArmyTroops(race);
        CanBuildToday = true;
    }

    /// <summary>The tile the town stands on, <c>Castle::GetIndex()</c>.</summary>
    public int TileIndex { get; }

    /// <summary>
    /// The town's race, <c>_race</c>. Fixed once the town exists — a random castle gets its race from the map block
    /// that describes it, which is why only <see cref="LoadFromMP2"/> may write it after construction.
    /// </summary>
    public MapRace Race { get; private set; }

    /// <summary>
    /// The map version the town stands on, <c>Settings::Get().getCurrentMapInfo().version</c>.
    /// </summary>
    /// <remarks>
    /// Only the necromancer town cares: before <c>Price of Loyalty</c> it has neither a tavern nor a shrine, from
    /// <c>Price of Loyalty</c> on it has the shrine. See <see cref="CountBuildings"/> and
    /// <see cref="CheckBuyBuilding"/>.
    /// </remarks>
    public MapVersion Version { get; }

    /// <summary>The owner, <c>GetColor()</c>; <see cref="PlayerColor.None"/> for an unclaimed town.</summary>
    public PlayerColor Color { get; private set; } = PlayerColor.None;

    /// <summary>
    /// The town's name, <c>_name</c>.
    /// </summary>
    /// <remarks>
    /// The block of a map's town may carry a name; the rest get one of <see cref="DefaultNames"/> through
    /// <see cref="SetName"/>, which the map loader runs once every town is loaded. A town created any other way keeps
    /// whatever the caller sets here.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>The garrison, <c>_army</c>.</summary>
    public ArmyTroops Army => _army;

    /// <summary>The buildings the town owns, <c>_constructedBuildings</c>.</summary>
    public BuildingType ConstructedBuildings => _constructedBuildings;

    /// <summary>
    /// The buildings the map forbids, <c>_disabledBuildings</c> (<c>castle.cpp:407-410</c>).
    /// </summary>
    /// <remarks>
    /// Written once by the map loader and never changed during play, which is why it has a setter:
    /// <see cref="CheckBuyBuilding"/> only reads it.
    /// </remarks>
    public BuildingType DisabledBuildings { get; set; }

    /// <summary>Whether the town may build today, <c>Modes( ALLOW_TO_BUILD_TODAY )</c>.</summary>
    /// <remarks>Set by <see cref="ActionNewDay"/>, cleared by <see cref="BuyBuilding"/>: one building per day.</remarks>
    public bool CanBuildToday { get; private set; }

    /// <summary>
    /// Whether the town can put a boat on water, <c>Castle::HasSeaAccess()</c> (<c>castle.cpp:1861</c>).
    /// </summary>
    /// <remarks>
    /// The reference walks the tiles around the town looking for water a boat could be placed on, which needs the
    /// map. The property is the seam: whoever loads the map answers it once, and <see cref="CheckBuyBuilding"/>
    /// asks it about the shipyard.
    /// </remarks>
    public bool HasSeaAccess { get; set; }

    /// <summary>
    /// Whether the garrison came from the map instead of being grown here, <c>Modes( CUSTOM_ARMY )</c>.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="LoadFromMP2"/> when the block carries custom defenders. The only reader is
    /// <see cref="PostLoad"/>, which leaves a neutral town's custom garrison alone instead of reinforcing it.
    /// </remarks>
    public bool HasCustomArmy { get; private set; }

    /// <summary>Whether the town has a castle and not merely a tent, <c>Castle::isCastle()</c>.</summary>
    public bool IsCastle => IsBuild(BuildingType.Castle);

    /// <summary>Whether the town owns the building, <c>Castle::isBuild()</c> (<c>castle.h:174</c>).</summary>
    /// <param name="building">A building, or a mask of buildings.</param>
    /// <returns><see langword="true"/> when every bit of <paramref name="building"/> is built.</returns>
    public bool IsBuild(BuildingType building) => (_constructedBuildings & building) == building;

    /// <summary>Whether the map forbids the building, <c>_disabledBuildings &amp; build</c>.</summary>
    /// <param name="building">A building, or a mask of buildings.</param>
    /// <returns><see langword="true"/> when any bit of <paramref name="building"/> is banned.</returns>
    public bool IsBuildingDisabled(BuildingType building) => (DisabledBuildings & building) != 0;

    /// <summary>
    /// Whether the town's special building trains heroes in magic, <c>Castle::HaveLibraryCapability()</c>
    /// (<c>castle.h:223-226</c>). The wizard's library is the only one that does.
    /// </summary>
    public bool HasLibraryCapability => Race == MapRace.Wizard;

    /// <summary>
    /// Whether the library is built and therefore adds a spell to the mage guild,
    /// <c>Castle::isLibraryBuilt()</c> (<c>castle.h:228-231</c>).
    /// </summary>
    public bool IsLibraryBuilt => Race == MapRace.Wizard && IsBuild(BuildingType.Spec);

    /// <summary>
    /// Whether the fortifications are built, <c>Castle::isFortificationBuilt()</c> (<c>castle.h:238-241</c>).
    /// </summary>
    /// <remarks>
    /// The knight's special building. Every castle has a palisade; this is the masonry around it, and it is what
    /// the knight pays the special building's price for.
    /// </remarks>
    public bool IsFortificationBuilt => Race == MapRace.Knight && IsBuild(BuildingType.Spec);

    /// <summary>
    /// Whether the necromancer's shrine is built, <c>Castle::isNecromancyShrineBuild()</c>
    /// (<c>castle.h:191-194</c>).
    /// </summary>
    public bool IsNecromancyShrineBuild => Race == MapRace.Necromancer && IsBuild(BuildingType.Shrine);

    /// <summary>
    /// The level of the mage guild, <c>Castle::GetLevelMageGuild()</c> (<c>castle.cpp:955-969</c>).
    /// </summary>
    /// <returns>1 to 5, or 0 when the town has no mage guild.</returns>
    public int GetLevelMageGuild()
    {
        if (IsBuild(BuildingType.MageGuild5))
        {
            return 5;
        }

        if (IsBuild(BuildingType.MageGuild4))
        {
            return 4;
        }

        if (IsBuild(BuildingType.MageGuild3))
        {
            return 3;
        }

        if (IsBuild(BuildingType.MageGuild2))
        {
            return 2;
        }

        return IsBuild(BuildingType.MageGuild1) ? 1 : 0;
    }

    /// <summary>
    /// The buildings that count towards the town's value, <c>Castle::CountBuildings()</c>
    /// (<c>castle.cpp:564-581</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twenty-two bits at most: the tent, the shrine and every dwelling upgrade are worth nothing here, and the
    /// five mage guild levels are worth one between them. The necromancer's tavern counts through the shrine in
    /// the later map versions, and not at all in <c>Succession Wars</c>, where his town simply has one building
    /// fewer.
    /// </para>
    /// <para>
    /// The count is the AI's first impression of a town and the base of <see cref="GetBuildingValue"/>; it is the
    /// price of nothing.
    /// </para>
    /// </remarks>
    /// <returns>The number of buildings that count, up to 22.</returns>
    public int CountBuildings()
    {
        BuildingType counted = BuildingType.ThievesGuild | BuildingType.Shipyard | BuildingType.Well |
                               BuildingType.Statue | BuildingType.LeftTurret | BuildingType.RightTurret |
                               BuildingType.Marketplace | BuildingType.Well2 | BuildingType.Moat |
                               BuildingType.Spec | BuildingType.Captain | BuildingType.Castle |
                               BuildingType.MageGuild1 | BuildingType.DwellingMonsters;

        if (Race == MapRace.Necromancer)
        {
            counted |= Version is MapVersion.PriceOfLoyalty or MapVersion.Resurrection
                ? BuildingType.Shrine
                : BuildingType.Nothing;
        }
        else
        {
            counted |= BuildingType.Tavern;
        }

        return BitOperations.PopCount((uint)(_constructedBuildings & counted));
    }

    /// <summary>
    /// The town's worth to the AI, <c>Castle::getBuildingValue()</c> (<c>castle.cpp:613-641</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="CountBuildings"/> plus what a mere count misses: the castle is worth five, the sixth dwelling
    /// six, the warlock's black dragon lair two, each of the five dwelling upgrades one, and the mage guild levels
    /// a further one, two, three and four — the first level is already in the count.
    /// </remarks>
    /// <returns>The value, a small positive number.</returns>
    public int GetBuildingValue()
    {
        int value = CountBuildings();

        if (IsBuild(BuildingType.Castle))
        {
            value += 5;
        }

        if (IsBuild(BuildingType.DwellingMonster6))
        {
            value += 6;
        }

        if (Race == MapRace.Warlock && IsBuild(BuildingType.DwellingUpgrade7))
        {
            value += 2;
        }

        // The seventh upgrade's bit is the top of the uint32_t, so the sweep stops at the sixth.
        for (uint upgrade = (uint)BuildingType.DwellingUpgrade2;
             upgrade <= (uint)BuildingType.DwellingUpgrade6;
             upgrade <<= 1)
        {
            if (IsBuild((BuildingType)upgrade))
            {
                ++value;
            }
        }

        int increase = 1;
        for (uint guild = (uint)BuildingType.MageGuild2; guild <= (uint)BuildingType.MageGuild5; guild <<= 1)
        {
            if (IsBuild((BuildingType)guild))
            {
                value += increase;
            }

            ++increase;
        }

        return value;
    }

    /// <summary>
    /// The building a dwelling stands for once its upgrades are taken into account, <c>Castle::GetActualDwelling()</c>
    /// (<c>castle.cpp:1931-1958</c>).
    /// </summary>
    /// <remarks>
    /// Asking for the second dwelling when the archers' tower is built answers the tower, because that is the
    /// building that decides which creature is waiting inside. The sixth dwelling has two upgrades and the
    /// warlock's second one wins. An upgrade answers with itself whether or not it is built, as in the reference.
    /// </remarks>
    /// <param name="buildId">A dwelling or a dwelling upgrade.</param>
    /// <returns>The building whose monster the dwelling holds, or <see cref="BuildingType.Nothing"/>.</returns>
    public BuildingType GetActualDwelling(BuildingType buildId) => buildId switch
    {
        BuildingType.DwellingMonster1 or BuildingType.DwellingUpgrade2 or BuildingType.DwellingUpgrade3 or
            BuildingType.DwellingUpgrade4 or BuildingType.DwellingUpgrade5 or BuildingType.DwellingUpgrade7 => buildId,
        BuildingType.DwellingMonster2 => IsBuild(BuildingType.DwellingUpgrade2) ? BuildingType.DwellingUpgrade2 : buildId,
        BuildingType.DwellingMonster3 => IsBuild(BuildingType.DwellingUpgrade3) ? BuildingType.DwellingUpgrade3 : buildId,
        BuildingType.DwellingMonster4 => IsBuild(BuildingType.DwellingUpgrade4) ? BuildingType.DwellingUpgrade4 : buildId,
        BuildingType.DwellingMonster5 => IsBuild(BuildingType.DwellingUpgrade5) ? BuildingType.DwellingUpgrade5 : buildId,
        BuildingType.DwellingMonster6 => IsBuild(BuildingType.DwellingUpgrade7) ? BuildingType.DwellingUpgrade7
            : IsBuild(BuildingType.DwellingUpgrade6) ? BuildingType.DwellingUpgrade6
            : buildId,
        BuildingType.DwellingUpgrade6 => IsBuild(BuildingType.DwellingUpgrade7) ? BuildingType.DwellingUpgrade7 : buildId,
        _ => BuildingType.Nothing,
    };

    /// <summary>
    /// What a built dwelling can be upgraded into, <c>Castle::GetUpgradeBuilding()</c> (<c>castle.cpp:1960-1968</c>).
    /// </summary>
    /// <remarks>
    /// The warlock's sixth dwelling is the special case: once the red dragon's cave is built, the answer is the
    /// lair's own upgrade — the black dragon's — rather than the base dwelling's. A building with no upgrade
    /// answers with itself, the convention of <see cref="CastleBuildings.GetUpgradeForBuilding"/>.
    /// </remarks>
    /// <param name="buildingId">The building that is already built.</param>
    /// <returns>The upgrade, or <paramref name="buildingId"/> itself when there is none.</returns>
    public BuildingType GetUpgradeBuilding(BuildingType buildingId)
    {
        if (Race == MapRace.Warlock && buildingId == BuildingType.DwellingMonster6 &&
            IsBuild(BuildingType.DwellingUpgrade6))
        {
            return CastleBuildings.GetUpgradeForBuilding(Race, BuildingType.DwellingUpgrade6);
        }

        return CastleBuildings.GetUpgradeForBuilding(Race, buildingId);
    }

    /// <summary>
    /// The creatures waiting in a dwelling, <c>Castle::getMonstersInDwelling()</c> (<c>castle.cpp:1108-1135</c>).
    /// </summary>
    /// <remarks>
    /// An upgraded dwelling keeps the stock of the building it replaced — the archers' tower and the archers'
    /// dwelling share one shelf — which is why the sixth dwelling's stock answers for three building names.
    /// </remarks>
    /// <param name="buildingType">A dwelling or a dwelling upgrade.</param>
    /// <returns>The count, 0 for anything that is not a dwelling.</returns>
    public uint GetMonstersInDwelling(BuildingType buildingType)
    {
        int index = DwellingIndex(buildingType);

        return index < 0 ? 0u : _dwelling[index];
    }

    /// <summary>
    /// How many of a creature the owner could buy right now, <c>Castle::getRecruitLimit()</c>
    /// (<c>castle.cpp:1093-1106</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two caps, the smaller one wins: what the dwelling holds and what the money pays for. The budget is what the
    /// kingdom has minus what it has already spent on the other dwellings in the same visit — that subtraction is
    /// the caller's job (the reference's recruit dialog does it), because the reference reads live
    /// <c>Kingdom::GetFunds()</c> and a port method that took it would lie during a multi-dwelling purchase.
    /// </para>
    /// <para>
    /// The reference stores the quotient in a <see langword="uint"/> while
    /// <see cref="Funds.GetLowestQuotient"/> returns a signed number, so a negative budget wraps to a huge number
    /// and the answer becomes "everything in the dwelling". That is the reference's behaviour; the port keeps the
    /// cast explicit rather than "fixing" it, and the test file records it.
    /// </para>
    /// </remarks>
    /// <param name="monster">The creature to ask about.</param>
    /// <param name="budget">What may still be spent.</param>
    /// <returns>The number that could be bought, 0 for a creature of another race.</returns>
    public uint GetRecruitLimit(MonsterType monster, in Funds budget)
    {
        MonsterGeneralStats stats = MonsterStatistics.GetGeneralStats(monster);

        if (stats.Race != Race)
        {
            return 0;
        }

        uint available = GetMonstersInDwelling(DwellingMonsters.GetDwelling(monster));
        uint willRecruit = unchecked((uint)budget.GetLowestQuotient(stats.GetCost()));

        return available < willRecruit ? available : willRecruit;
    }

    /// <summary>
    /// Recruits a stack into the garrison, or into the visiting hero's army, <c>Castle::RecruitMonster()</c>
    /// (<c>castle.cpp:1025-1091</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order of the checks is the reference's and it decides who pays for what: the shelf is clamped, the price
    /// is computed from the stack <i>as asked for</i>, the money is checked, then the stack is placed, and only then
    /// is the owner charged and the shelf reduced. A recruit that cannot be placed costs nothing.
    /// </para>
    /// <para>
    /// The clamp is a guard, not a rule: the price and the stack that joins both use the count the caller asked
    /// for, while the shelf only ever loses what it had. Asking for more than the shelf holds therefore hands out
    /// the difference for free — the reference is written that way and its dialog never asks for more than
    /// <see cref="GetRecruitLimit"/> allows. The port copies the asymmetry instead of inventing a clamp the
    /// reference does not have; callers are expected to ask <see cref="GetRecruitLimit"/> first.
    /// </para>
    /// <para>
    /// The garrison comes first and the visiting hero second, which is <c>GetArmy()</c> then
    /// <c>world.GetHero( *this )</c> in the reference. There is no loaded world here, so the hero's army is the
    /// optional <paramref name="heroArmy"/>; a map that has a hero on the town passes <c>hero.Army</c>.
    /// </para>
    /// </remarks>
    /// <param name="troop">The stack to buy; its count is the number asked for.</param>
    /// <param name="treasury">The owner's resources, reduced by the price on success.</param>
    /// <param name="heroArmy">The army of the hero standing on the town, or <see langword="null"/> for none.</param>
    /// <returns><see langword="false"/> when the stack is invalid, the race is wrong, the money is short or both armies are full.</returns>
    public bool RecruitMonster(Troop troop, ref Funds treasury, ArmyTroops? heroArmy = null)
    {
        ArgumentNullException.ThrowIfNull(troop);

        if (!troop.IsValid)
        {
            return false;
        }

        int dwellingIndex = DwellingIndex(troop.GetDwelling());

        if (dwellingIndex < 0)
        {
            return false;
        }

        uint count = troop.Count;

        if (_dwelling[dwellingIndex] < count)
        {
            count = _dwelling[dwellingIndex];
        }

        Funds paymentCosts = troop.GetTotalCost();

        if (treasury < paymentCosts)
        {
            return false;
        }

        if (!_army.JoinTroop(troop) && (heroArmy is null || !heroArmy.JoinTroop(troop)))
        {
            // The reference shows "There is no room in the garrison for this army." when it was asked to.
            return false;
        }

        treasury -= paymentCosts;
        treasury.Trim();

        _dwelling[dwellingIndex] -= count;

        return true;
    }

    /// <summary>
    /// Whether exactly this level of a building is built and no further one,
    /// <c>Castle::_isExactBuildingBuilt()</c> (<c>castle.cpp:731-803</c>).
    /// </summary>
    /// <remarks>
    /// The castle dialog asks "is the mage guild exactly at level 3", not "at least at level 3", because that is
    /// the picture it draws. A single bit is required, as the reference asserts.
    /// </remarks>
    /// <param name="buildingToCheck">One single building bit.</param>
    /// <returns>
    /// <see langword="true"/> when the building and every level below it are built and no level above it is.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="buildingToCheck"/> is not a single bit.</exception>
    public bool IsExactBuildingBuilt(BuildingType buildingToCheck)
    {
        if (BitOperations.PopCount((uint)buildingToCheck) != 1)
        {
            throw new ArgumentException("A single building bit is expected.", nameof(buildingToCheck));
        }

        if ((_constructedBuildings & buildingToCheck) == 0)
        {
            return false;
        }

        bool CheckBuilding(BuildingType expectedLevels, BuildingType allPossibleLevels) =>
            (_constructedBuildings & allPossibleLevels) == expectedLevels;

        if ((buildingToCheck & BuildingType.MageGuild) != 0)
        {
            return buildingToCheck switch
            {
                BuildingType.MageGuild1 => CheckBuilding(BuildingType.MageGuild1, BuildingType.MageGuild),
                BuildingType.MageGuild2 => CheckBuilding(
                    BuildingType.MageGuild1 | BuildingType.MageGuild2, BuildingType.MageGuild),
                BuildingType.MageGuild3 => CheckBuilding(
                    BuildingType.MageGuild1 | BuildingType.MageGuild2 | BuildingType.MageGuild3,
                    BuildingType.MageGuild),
                BuildingType.MageGuild4 => CheckBuilding(
                    BuildingType.MageGuild1 | BuildingType.MageGuild2 | BuildingType.MageGuild3 |
                    BuildingType.MageGuild4,
                    BuildingType.MageGuild),
                BuildingType.MageGuild5 => CheckBuilding(
                    BuildingType.MageGuild1 | BuildingType.MageGuild2 | BuildingType.MageGuild3 |
                    BuildingType.MageGuild4 | BuildingType.MageGuild5,
                    BuildingType.MageGuild),
                _ => throw new ArgumentException("Not a mage guild level.", nameof(buildingToCheck)),
            };
        }

        if ((buildingToCheck & (BuildingType.DwellingMonsters | BuildingType.DwellingUpgrades)) != 0)
        {
            return buildingToCheck switch
            {
                // The first dwelling has no upgrade.
                BuildingType.DwellingMonster1 => true,
                BuildingType.DwellingMonster2 => CheckBuilding(
                    BuildingType.DwellingMonster2, BuildingType.DwellingMonster2 | BuildingType.DwellingUpgrade2),
                BuildingType.DwellingMonster3 => CheckBuilding(
                    BuildingType.DwellingMonster3, BuildingType.DwellingMonster3 | BuildingType.DwellingUpgrade3),
                BuildingType.DwellingMonster4 => CheckBuilding(
                    BuildingType.DwellingMonster4, BuildingType.DwellingMonster4 | BuildingType.DwellingUpgrade4),
                BuildingType.DwellingMonster5 => CheckBuilding(
                    BuildingType.DwellingMonster5, BuildingType.DwellingMonster5 | BuildingType.DwellingUpgrade5),

                // The black dragon's lair is the second upgrade of the sixth dwelling.
                BuildingType.DwellingMonster6 => CheckBuilding(
                    BuildingType.DwellingMonster6,
                    BuildingType.DwellingMonster6 | BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7),
                BuildingType.DwellingUpgrade2 => CheckBuilding(
                    BuildingType.DwellingMonster2 | BuildingType.DwellingUpgrade2,
                    BuildingType.DwellingMonster2 | BuildingType.DwellingUpgrade2),
                BuildingType.DwellingUpgrade3 => CheckBuilding(
                    BuildingType.DwellingMonster3 | BuildingType.DwellingUpgrade3,
                    BuildingType.DwellingMonster3 | BuildingType.DwellingUpgrade3),
                BuildingType.DwellingUpgrade4 => CheckBuilding(
                    BuildingType.DwellingMonster4 | BuildingType.DwellingUpgrade4,
                    BuildingType.DwellingMonster4 | BuildingType.DwellingUpgrade4),
                BuildingType.DwellingUpgrade5 => CheckBuilding(
                    BuildingType.DwellingMonster5 | BuildingType.DwellingUpgrade5,
                    BuildingType.DwellingMonster5 | BuildingType.DwellingUpgrade5),
                BuildingType.DwellingUpgrade6 => CheckBuilding(
                    BuildingType.DwellingMonster6 | BuildingType.DwellingUpgrade6,
                    BuildingType.DwellingMonster6 | BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7),
                BuildingType.DwellingUpgrade7 => CheckBuilding(
                    BuildingType.DwellingMonster6 | BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7,
                    BuildingType.DwellingMonster6 | BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7),
                _ => throw new ArgumentException("Not a dwelling.", nameof(buildingToCheck)),
            };
        }

        return true;
    }

    /// <summary>
    /// Whether the building can be bought and, when it cannot, why, <c>Castle::CheckBuyBuilding()</c>
    /// (<c>castle.cpp:1137-1234</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The questions come in the reference's order and the order is visible. An already built building answers
    /// <see cref="BuildingStatus.AlreadyBuilt"/> before the one-building-a-day rule is consulted; the mage guild's
    /// "one level at a time" rule comes before that rule too, while the race's "this upgrade does not exist here"
    /// rule comes after it. Only a caller that walks the whole list can tell, and the castle dialog does walk it.
    /// </para>
    /// <para>
    /// The three buildings special-cased here rather than in the tables are the shipyard (water), the shrine
    /// (necromancer, and only from <c>Price of Loyalty</c> on) and the tavern (never for the necromancer).
    /// </para>
    /// </remarks>
    /// <param name="build">The building to ask about.</param>
    /// <param name="treasury">The owner's resources, <c>Kingdom::AllowPayment()</c>.</param>
    /// <returns>The status, <see cref="BuildingStatus.AllowBuild"/> when the answer is yes.</returns>
    public BuildingStatus CheckBuyBuilding(BuildingType build, in Funds treasury)
    {
        if ((_constructedBuildings & build) != 0)
        {
            return BuildingStatus.AlreadyBuilt;
        }

        if ((DisabledBuildings & build) != 0)
        {
            return BuildingStatus.BuildDisabled;
        }

        switch (build)
        {
        case BuildingType.Shipyard when !HasSeaAccess:
            return BuildingStatus.ShipyardNotAllowed;

        case BuildingType.Shrine when Race != MapRace.Necromancer || Version == MapVersion.SuccessionWars:
            return BuildingStatus.BuildDisabled;

        case BuildingType.Tavern when Race == MapRace.Necromancer:
            return BuildingStatus.BuildDisabled;

        default:
            break;
        }

        if ((uint)build >= (uint)BuildingType.MageGuild2 && (uint)build <= (uint)BuildingType.MageGuild5)
        {
            // The mage guild bits are consecutive, so the level below is the bit below.
            BuildingType previousLevel = (BuildingType)((uint)build >> 1);

            if ((_constructedBuildings & previousLevel) == 0)
            {
                return BuildingStatus.BuildDisabled;
            }
        }

        if (!CanBuildToday)
        {
            return BuildingStatus.NotToday;
        }

        if (IsCastle)
        {
            if (build == BuildingType.Tent)
            {
                return BuildingStatus.BuildDisabled;
            }
        }
        else if (build != BuildingType.Castle)
        {
            return BuildingStatus.NeedCastle;
        }

        switch (build)
        {
        case BuildingType.DwellingUpgrade2 when Race is MapRace.Warlock or MapRace.Wizard:
        case BuildingType.DwellingUpgrade3 when Race is MapRace.Barbarian or MapRace.Warlock:
        case BuildingType.DwellingUpgrade4 when Race == MapRace.Wizard:
        case BuildingType.DwellingUpgrade5 when Race is MapRace.Sorceress or MapRace.Warlock:
        case BuildingType.DwellingUpgrade6 when Race is MapRace.Barbarian or MapRace.Sorceress or MapRace.Necromancer:
        case BuildingType.DwellingUpgrade7 when Race != MapRace.Warlock:
            return BuildingStatus.UnknownUpgrade;

        default:
            break;
        }

        BuildingType requirement = CastleBuildings.GetRequirement(Race, build);

        for (uint itr = 0x00000001; itr != 0; itr <<= 1)
        {
            if ((requirement & (BuildingType)itr) != 0 && (_constructedBuildings & (BuildingType)itr) == 0)
            {
                return BuildingStatus.RequiresBuild;
            }
        }

        if (treasury < CastleBuildings.GetCost(build, Race))
        {
            return BuildingStatus.LackResources;
        }

        return BuildingStatus.AllowBuild;
    }

    /// <summary>
    /// Whether the building can be bought, <c>Castle::AllowBuyBuilding()</c> (<c>castle.h:207</c>).
    /// </summary>
    /// <param name="build">The building to ask about.</param>
    /// <param name="treasury">The owner's resources.</param>
    /// <returns><see langword="true"/> when <see cref="CheckBuyBuilding"/> answers "yes".</returns>
    public bool AllowBuyBuilding(BuildingType build, in Funds treasury) =>
        CheckBuyBuilding(build, treasury) == BuildingStatus.AllowBuild;

    /// <summary>
    /// The best answer any remaining building can give, <c>Castle::GetAllBuildingStatus()</c>
    /// (<c>castle.cpp:1236-1258</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AI's question: "can this town build <i>something</i> today?" Three sweeps over the bits it does not own,
    /// in the reference's order of interest — yes, no money, needs a prerequisite — and
    /// <see cref="BuildingStatus.UnknownCondition"/> when the answer is none of those.
    /// </para>
    /// <para>
    /// A town that may not build today and a town that is still a tent answer before the sweeps start, so the
    /// sweep only ever runs for a castle with its building permit.
    /// </para>
    /// </remarks>
    /// <param name="castle">The town to ask about.</param>
    /// <param name="treasury">The owner's resources.</param>
    /// <returns>The most interesting status, never <see cref="BuildingStatus.AlreadyBuilt"/>.</returns>
    public static BuildingStatus GetAllBuildingStatus(Castle castle, in Funds treasury)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (!castle.CanBuildToday)
        {
            return BuildingStatus.NotToday;
        }

        if (!castle.IsCastle)
        {
            return BuildingStatus.NeedCastle;
        }

        Funds wallet = treasury;
        BuildingType rest = ~castle._constructedBuildings;

        BuildingStatus Sweep(BuildingStatus wanted)
        {
            for (uint itr = 0x00000001; itr != 0; itr <<= 1)
            {
                if ((rest & (BuildingType)itr) != 0 &&
                    castle.CheckBuyBuilding((BuildingType)itr, wallet) == wanted)
                {
                    return wanted;
                }
            }

            return BuildingStatus.UnknownCondition;
        }

        BuildingStatus status = Sweep(BuildingStatus.AllowBuild);

        if (status != BuildingStatus.UnknownCondition)
        {
            return status;
        }

        status = Sweep(BuildingStatus.LackResources);

        return status != BuildingStatus.UnknownCondition ? status : Sweep(BuildingStatus.RequiresBuild);
    }

    /// <summary>
    /// Buys a building, <c>Castle::BuyBuilding()</c> (<c>castle.cpp:1260-1324</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The price is taken out of <paramref name="treasury"/>, which is the kingdom's resource pool:
    /// <c>OddFundsResource()</c> is <c>operator-=</c> followed by <c>Trim()</c>, so a treasury that somehow goes
    /// short clamps to zero rather than going negative.
    /// </para>
    /// <para>
    /// What the building does when it appears: the castle removes the tent, a dwelling fills its stock with one
    /// week's growth of the creature it now houses, and everything else only raises a bit. The reference also
    /// repaints the map, clears the fog for the owner and trains the visiting hero and the captain in the mage
    /// guild; those three need the world, the fog and the hero book, so they are left to their own batches. The
    /// captain's quarters and the mage guild levels are where the omissions are.
    /// </para>
    /// <para>
    /// The building permit is spent whatever was built, which is why it is cleared at the end rather than by the
    /// caller.
    /// </para>
    /// </remarks>
    /// <param name="buildingType">The building to buy.</param>
    /// <param name="treasury">The owner's resources, reduced by the price on success.</param>
    /// <returns><see langword="false"/> when the building was not for sale.</returns>
    public bool BuyBuilding(BuildingType buildingType, ref Funds treasury)
    {
        if (CheckBuyBuilding(buildingType, treasury) != BuildingStatus.AllowBuild)
        {
            return false;
        }

        Funds cost = CastleBuildings.GetCost(buildingType, Race);
        treasury -= cost;
        treasury.Trim();

        _constructedBuildings |= buildingType;

        switch (buildingType)
        {
        case BuildingType.Castle:
            _constructedBuildings &= ~BuildingType.Tent;
            break;

        case BuildingType.DwellingMonster1:
            _dwelling[0] = GetGrowth(BuildingType.DwellingMonster1);
            break;

        case BuildingType.DwellingMonster2:
            _dwelling[1] = GetGrowth(BuildingType.DwellingMonster2);
            break;

        case BuildingType.DwellingMonster3:
            _dwelling[2] = GetGrowth(BuildingType.DwellingMonster3);
            break;

        case BuildingType.DwellingMonster4:
            _dwelling[3] = GetGrowth(BuildingType.DwellingMonster4);
            break;

        case BuildingType.DwellingMonster5:
            _dwelling[4] = GetGrowth(BuildingType.DwellingMonster5);
            break;

        case BuildingType.DwellingMonster6:
            _dwelling[5] = GetGrowth(BuildingType.DwellingMonster6);
            break;

        default:
            // The mage guild levels, the captain's quarters and the library train the visiting hero; the map
            // repaint belongs to the castle's sprite in Maps.
            break;
        }

        CanBuildToday = false;

        return true;
    }

    /// <summary>
    /// Starts a new day, <c>Castle::ActionNewDay()</c> (<c>castle.cpp:833-836</c>).
    /// </summary>
    /// <remarks>All it does is hand the building permit back.</remarks>
    public void ActionNewDay() => CanBuildToday = true;

    /// <summary>
    /// The extra growth the well gives every dwelling, <c>Castle::GetGrownWell()</c> (<c>castle.cpp:2193</c>).
    /// </summary>
    /// <remarks>
    /// <c>GameStatic::GetCastleGrownWell()</c> returns the literal 2 (<c>game_static.cpp:145-148</c>); the port
    /// keeps it as a constant until it has a settings layer.
    /// </remarks>
    public const uint GrownWell = 2;

    /// <summary>
    /// The extra growth the second well gives the first dwelling, <c>Castle::GetGrownWel2()</c>
    /// (<c>castle.cpp:2198</c>).
    /// </summary>
    /// <remarks>
    /// <c>BUILD_WEL2</c> is the race's landmark building, not a second well; 8 is the reference literal
    /// (<c>game_static.cpp:150-153</c>).
    /// </remarks>
    public const uint GrownWel2 = 8;

    /// <summary>
    /// What a monster's week adds to its dwelling, <c>Castle::GetGrownWeekOf()</c> (<c>castle.cpp:2203</c>).
    /// </summary>
    /// <remarks>A flat number of creatures, not a percentage (<c>game_static.cpp:155-158</c>).</remarks>
    public const uint GrownWeekOf = 5;

    /// <summary>
    /// What a monster's month adds to its dwelling, <c>Castle::GetGrownMonthOf()</c> (<c>castle.cpp:2208</c>).
    /// </summary>
    /// <remarks>
    /// A percentage of the stock, so 100 doubles it (<c>game_static.cpp:160-163</c>). It is applied to what the
    /// dwelling holds <i>after</i> the weekly growth.
    /// </remarks>
    public const uint GrownMonthOf = 100;

    /// <summary>
    /// The six buildable dwellings, in the reference's order (<c>castle.cpp:855-856</c>).
    /// </summary>
    private static readonly BuildingType[] BasicDwellings =
    [
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
    ];

    /// <summary>
    /// The six buildable dwellings plus the six upgrades, in the reference's order
    /// (<c>castle.cpp:845-847</c>).
    /// </summary>
    private static readonly BuildingType[] AllDwellings =
    [
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
        BuildingType.DwellingUpgrade2,
        BuildingType.DwellingUpgrade3,
        BuildingType.DwellingUpgrade4,
        BuildingType.DwellingUpgrade5,
        BuildingType.DwellingUpgrade6,
        BuildingType.DwellingUpgrade7,
    ];

    /// <summary>
    /// Starts a new week: the dwellings grow, a monster's week pays a flat bonus and the first day of a month
    /// settles the monthly share, <c>Castle::ActionNewWeek()</c> (<c>castle.cpp:838-940</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Five rules carry the whole method. The <b>first week is skipped</b> (<c>castle.cpp:840-843</c>): every
    /// dwelling already holds one week of growth when the map starts. A <b>plague month's first week freezes the
    /// growth</b> — and its first day halves every stock, including the dwellings that just skipped. A
    /// <b>monster's week</b> feeds only the dwelling whose <b>exact</b> building stands and whose creature is the
    /// week's monster, so an upgraded dwelling never takes the bonus. A <b>neutral town</b> grows at half speed
    /// and gets free garrison troops (<c>castle.cpp:877-916</c>). And the <b>monthly</b> bonus is a percentage of
    /// the stock <i>after</i> the weekly growth, which is why it is settled at the end.
    /// </para>
    /// <para>
    /// The reference reads the clock and the week out of the world; a town knows neither, so both come in as
    /// arguments. <paramref name="isBeginMonth"/> is <c>World::BeginMonth()</c> — the first day of the first week
    /// of a month — and it changes what the day means: on that day the month's rule applies instead of the
    /// week's, and the flat monster-week bonus is skipped (<c>castle.cpp:884</c>).
    /// </para>
    /// </remarks>
    /// <param name="weekCount">Weeks since the start of the game, <c>world.CountWeek()</c>.</param>
    /// <param name="week">The current week, <c>world.GetWeekType()</c>.</param>
    /// <param name="isBeginMonth">Whether today is the first day of a month, <c>world.BeginMonth()</c>.</param>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>, for the neutral garrison.</param>
    /// <param name="generator">
    /// The generator of the neutral garrison roll; <see langword="null"/> means the thread's own device, which is
    /// what the reference does.
    /// </param>
    public void ActionNewWeek(uint weekCount, in Week week, bool isBeginMonth, uint dayCount, Pcg32? generator = null)
    {
        if (weekCount < 2)
        {
            return;
        }

        bool isNeutral = Color == PlayerColor.None;
        bool isPlagueWeek = week.Type == WeekName.Plague;
        bool isMonsterWeek = week.Type == WeekName.Monsters;

        if (!isPlagueWeek)
        {
            foreach (BuildingType dwelling in BasicDwellings)
            {
                // castle.cpp:860-863: a dwelling that was never built has no shelf to feed, and the reference
                // checks the basic id, not the upgrade — a castle that only carries the upgrade bit grows
                // nothing here, the same way its pointer lookup answers nullptr.
                int shelf = GetDwellingShelf(dwelling);

                if (shelf < 0)
                {
                    continue;
                }

                uint growth = GetGrowth(GetActualDwelling(dwelling));

                if (IsBuild(BuildingType.Well))
                {
                    growth += GrownWell;
                }

                if (dwelling == BuildingType.DwellingMonster1 && IsBuild(BuildingType.Well2))
                {
                    growth += GrownWel2;
                }

                if (isNeutral)
                {
                    growth /= 2;
                }

                _dwelling[shelf] += growth;
            }

            if (isMonsterWeek && !isBeginMonth && TryFindWeekMonsterDwelling(week.Monster, out int weekShelf))
            {
                _dwelling[weekShelf] += GrownWeekOf;
            }

            if (isNeutral)
            {
                JoinRandomArmy(dayCount, generator);

                // castle.cpp:911-916: a neutral castle always gets a second wave, a town does so 40% of the time.
                if (IsCastle || Roll(1, 100, generator) <= 40)
                {
                    JoinRandomArmy(dayCount, generator);
                }
            }
        }

        if (!isBeginMonth)
        {
            return;
        }

        if (isPlagueWeek)
        {
            // castle.cpp:922-926: the plague halves everything, whether or not the dwelling grew this week.
            for (int index = 0; index < _dwelling.Length; ++index)
            {
                _dwelling[index] /= 2;
            }
        }
        else if (isMonsterWeek && TryFindWeekMonsterDwelling(week.Monster, out int monthShelf))
        {
            _dwelling[monthShelf] += _dwelling[monthShelf] * GrownMonthOf / 100;
        }
    }

    /// <summary>
    /// Gives the town buildings without charging for them, the write the map loader and the editor make
    /// (<c>castle.cpp:407-410</c>).
    /// </summary>
    /// <remarks>
    /// The loader reads the town's bits from the map file and writes them here; the dwelling stocks are filled
    /// separately, by the part of <c>_postLoad()</c> that has not been ported yet. Nothing in a game played from
    /// scratch calls this — <see cref="BuyBuilding"/> is the only way a town grows during play.
    /// </remarks>
    /// <param name="buildings">The buildings to add, or a mask of buildings.</param>
    public void AddBuildings(BuildingType buildings) => _constructedBuildings |= buildings;

    /// <summary>
    /// Gives the town the buildings it starts with, <c>Castle::_setDefaultBuildings()</c>
    /// (<c>castle.cpp:537-562</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first dwelling is always there. The second one is a difficulty roll: 75% of towns have it on easy, 50%
    /// on normal, 25% on hard, 10% on expert and none on impossible. What the other dwellings hold is not a roll —
    /// a town never starts with them.
    /// </para>
    /// <para>
    /// This is called while loading a town that has no custom buildings, right after the map's own bits are read
    /// (<c>castle.cpp:401-405</c>), and the roll comes from the reference's global generator. The port takes an
    /// optional <paramref name="generator"/> so that map loading and tests can be reproducible, the same seam
    /// <see cref="Army.Reset"/> uses.
    /// </para>
    /// </remarks>
    /// <param name="difficulty">The map's difficulty, which only changes the second dwelling's odds.</param>
    /// <param name="generator">The generator for the roll; the thread generator when absent.</param>
    public void SetDefaultBuildings(GameDifficulty difficulty, Pcg32? generator = null)
    {
        _constructedBuildings |= BuildingType.DwellingMonster1;

        uint secondDwellingChance = difficulty switch
        {
            GameDifficulty.Easy => 75,
            GameDifficulty.Normal => 50,
            GameDifficulty.Hard => 25,
            GameDifficulty.Expert => 10,
            _ => 0,
        };

        uint roll = generator is null ? Rand.Get(1u, 100u) : Rand.GetWithGen(1, 100, generator);

        if (secondDwellingChance >= roll)
        {
            _constructedBuildings |= BuildingType.DwellingMonster2;
        }
    }

    /// <summary>
    /// Changes the owner, <c>Castle::ChangeColor()</c>.
    /// </summary>
    /// <remarks>The garrison changes colour with the town so that the two never disagree.</remarks>
    /// <param name="color">The new owner.</param>
    public void ChangeColor(PlayerColor color)
    {
        Color = color;
        _army.Color = color;
    }

    /// <summary>
    /// The names a map's unnamed towns are given, <c>defaultCastleNames</c> (<c>castle.cpp:84-101</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seventy-two names, one per entry of a map file's town table, so a full map can always name every town. The
    /// reference keeps the table inside <c>Castle::setName()</c> and wraps each name in <c>gettext_noop</c>; the port
    /// exposes it because a test has no other way to see which names are legal, and because the translations the
    /// reference plugs in here do not exist yet.
    /// </para>
    /// <para>
    /// The order is the reference's, which matters: the picking shuffles this list and takes the first name that is
    /// not taken, so the table itself must not be sorted.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> DefaultNames { get; } = new[]
    {
        "Blackridge", "Pinehurst", "Woodhaven", "Hillstone", "Whiteshield",
        "Bloodreign", "Dragontooth", "Greywind", "Blackwind", "Portsmith",
        "Middle Gate", "Tundara", "Vulcania", "Sansobar", "Atlantium",
        "Baywatch", "Wildabar", "Fountainhead", "Vertigo", "Winterkill",
        "Nightshadow", "Sandcaster", "Lakeside", "Olympus", "Brindamoor",
        "Burlock", "Xabran", "Dragadune", "Alamar", "Kalindra",
        "Blackfang", "Basenji", "Algary", "Sorpigal", "New Dawn",
        "Erliquin", "Avone", "Big Oak", "Hampshire", "Chandler",
        "South Mill", "Weed Patch", "Roc Haven", "Avalon", "Antioch",
        "Brownston", "Weddington", "Whittingham", "Westfork", "Hilltop",
        "Yorksford", "Sherman", "Roscomon", "Elk's Head", "Cathcart",
        "Viper's Nest", "Pig's Eye", "Blacksford", "Burton", "Blackburn",
        "Lankershire", "Lombard", "Timberhill", "Fenton", "Troy",
        "Forder Oaks", "Meramec", "Quick Silver", "Westmoor", "Willow",
        "Sheltemburg", "Corackston",
    };

    /// <summary>
    /// Gives an unnamed town one of the default names, <c>Castle::setName()</c> (<c>castle.cpp:2169-2183</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is shuffled and the first name not in <paramref name="usedNames"/> is taken, so the result is
    /// random but never a duplicate. That is why <c>World.UpdateCastleNames()</c> has to pass every name already
    /// spoken for on the map, not only the ones it has handed out itself.
    /// </para>
    /// <para>
    /// The reference ends with <c>assert( 0 )</c> when all seventy-two names are taken — impossible, because a map
    /// cannot hold more than seventy-two towns. The port throws instead of asserting, which is the usual mapping of
    /// the reference's asserts.
    /// </para>
    /// </remarks>
    /// <param name="usedNames">Every name already in use on the map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="usedNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">All default names are taken.</exception>
    public void SetName(IReadOnlySet<string> usedNames)
    {
        ArgumentNullException.ThrowIfNull(usedNames);

        string[] remaining = DefaultNames.ToArray();

        Rand.Shuffle(remaining);

        foreach (string name in remaining)
        {
            if (!usedNames.Contains(name))
            {
                Name = name;

                return;
            }
        }

        throw new InvalidOperationException("Every default castle name is already taken.");
    }

    /// <summary>
    /// Reads a town out of its 70-byte block in an MP2 map, <c>Castle::LoadFromMP2()</c>
    /// (<c>castle.cpp:102-395</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The block is read strictly in order, and every non-custom section is skipped by a fixed width because the
    /// file format has no lengths:
    /// </para>
    /// <list type="bullet">
    /// <item>1 byte owner: 0…5 are the six player colours in MP2 order, anything else is "no owner".</item>
    /// <item>1 byte "custom buildings", then 2 bytes of common buildings, 2 bytes of dwellings and 1 byte of mage
    /// guild level; or, when that byte is zero, the same 5 bytes are skipped and the default buildings are
    /// rolled.</item>
    /// <item>1 byte "custom defenders", then five 1-byte monster ids and five 2-byte little-endian counts; or 15
    /// skipped bytes.</item>
    /// <item>1 byte captain, 1 byte "custom name", 13 bytes of name, 1 byte faction, 1 byte "castle built", 1 byte
    /// "castle forbidden", then 29 bytes the reference never reads.</item>
    /// </list>
    /// <para>
    /// Monster ids are stored one less than <see cref="MonsterType"/> because the format reserves 0 for "none",
    /// hence the <c>+ 1</c> that the reference also writes.
    /// </para>
    /// <para>
    /// <b>Three seams stand in for the world</b>. The faction byte's catch-all branch in the reference asks
    /// <c>Players::GetPlayerRace()</c> for the owner's kingdom race and rolls a random one when there is no owner;
    /// the port takes that answer as <paramref name="ownerRace"/> (and still rolls when it is absent, because a
    /// random castle with no owner is a random castle). The default buildings' roll needs the map difficulty, which
    /// is the caller's knowledge. And the "no sea access removes the shipyard" rule of the tail needs
    /// <see cref="HasSeaAccess"/>, which is answered by the map — <b>set it before calling this method</b>, or a
    /// shipyard the block names will be taken away again.
    /// </para>
    /// <para>
    /// <b>What is not here</b>: the mage guild's spell storage (<c>_mageGuild.initialize</c>) — the port has a guild
    /// level but no spell book for towns; and the captain's stats, which need the hero-base captain. Both are
    /// noted in <see cref="PostLoad"/>, which is where the reference repairs them.
    /// </para>
    /// </remarks>
    /// <param name="data">The 70-byte block, exactly.</param>
    /// <param name="difficulty">The map's difficulty, used only when the block has no custom buildings.</param>
    /// <param name="ownerRace">The race of the owner's kingdom, when there is an owner.</param>
    /// <param name="generator">Generator for the random race and the default buildings; the thread generator when absent.</param>
    /// <param name="dayCount">Days elapsed on the map, used for a neutral town's reinforcements.</param>
    /// <exception cref="ArgumentException"><paramref name="data"/> is not <see cref="Mp2BlockSize"/> bytes long.</exception>
    public void LoadFromMP2(
        ReadOnlySpan<byte> data,
        GameDifficulty difficulty,
        MapRace? ownerRace = null,
        Pcg32? generator = null,
        uint dayCount = 0)
    {
        if (data.Length != Mp2BlockSize)
        {
            throw new ArgumentException($"A town block is {Mp2BlockSize} bytes, not {data.Length}.", nameof(data));
        }

        int offset = 0;

        ChangeColor(data[offset++] switch
        {
            0 => PlayerColor.Blue,
            1 => PlayerColor.Green,
            2 => PlayerColor.Red,
            3 => PlayerColor.Yellow,
            4 => PlayerColor.Orange,
            5 => PlayerColor.Purple,
            _ => PlayerColor.None,
        });

        if (data[offset++] != 0)
        {
            ushort common = ReadUInt16(data, ref offset);

            if ((common & 0x0002) != 0)
            {
                _constructedBuildings |= BuildingType.ThievesGuild;
            }

            if ((common & 0x0004) != 0)
            {
                _constructedBuildings |= BuildingType.Tavern;
            }

            if ((common & 0x0008) != 0)
            {
                _constructedBuildings |= BuildingType.Shipyard;
            }

            if ((common & 0x0010) != 0)
            {
                _constructedBuildings |= BuildingType.Well;
            }

            if ((common & 0x0080) != 0)
            {
                _constructedBuildings |= BuildingType.Statue;
            }

            if ((common & 0x0100) != 0)
            {
                _constructedBuildings |= BuildingType.LeftTurret;
            }

            if ((common & 0x0200) != 0)
            {
                _constructedBuildings |= BuildingType.RightTurret;
            }

            if ((common & 0x0400) != 0)
            {
                _constructedBuildings |= BuildingType.Marketplace;
            }

            if ((common & 0x0800) != 0)
            {
                _constructedBuildings |= BuildingType.Well2;
            }

            if ((common & 0x1000) != 0)
            {
                _constructedBuildings |= BuildingType.Moat;
            }

            if ((common & 0x2000) != 0)
            {
                _constructedBuildings |= BuildingType.Spec;
            }

            // Existing dwellings. The format cannot say "upgraded" without also meaning "built": an upgrade bit
            // always brings its base dwelling along, which is why each upgrade line sets two bits.
            ushort dwellings = ReadUInt16(data, ref offset);

            if ((dwellings & 0x0008) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingMonster1;
            }

            if ((dwellings & 0x0010) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingMonster2;
            }

            if ((dwellings & 0x0020) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingMonster3;
            }

            if ((dwellings & 0x0040) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingMonster4;
            }

            if ((dwellings & 0x0080) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingMonster5;
            }

            if ((dwellings & 0x0100) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingMonster6;
            }

            if ((dwellings & 0x0200) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingUpgrade2 | BuildingType.DwellingMonster2;
            }

            if ((dwellings & 0x0400) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingUpgrade3 | BuildingType.DwellingMonster3;
            }

            if ((dwellings & 0x0800) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingUpgrade4 | BuildingType.DwellingMonster4;
            }

            if ((dwellings & 0x1000) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingUpgrade5 | BuildingType.DwellingMonster5;
            }

            if ((dwellings & 0x2000) != 0)
            {
                _constructedBuildings |= BuildingType.DwellingUpgrade6 | BuildingType.DwellingMonster6;
            }

            // The mage guild stores its level, not its bits: level 3 means the first three guilds are built.
            int guildLevel = data[offset++];

            if (guildLevel > 0)
            {
                _constructedBuildings |= BuildingType.MageGuild1;
            }

            if (guildLevel > 1)
            {
                _constructedBuildings |= BuildingType.MageGuild2;
            }

            if (guildLevel > 2)
            {
                _constructedBuildings |= BuildingType.MageGuild3;
            }

            if (guildLevel > 3)
            {
                _constructedBuildings |= BuildingType.MageGuild4;
            }

            if (guildLevel > 4)
            {
                _constructedBuildings |= BuildingType.MageGuild5;
            }
        }
        else
        {
            offset += 5;

            SetDefaultBuildings(difficulty, generator);
        }

        if (data[offset++] != 0)
        {
            Troop[] troops = new Troop[5];

            for (int slot = 0; slot < troops.Length; ++slot)
            {
                troops[slot] = new Troop((MonsterType)(data[offset + slot] + 1), 0);
            }

            offset += 5;

            for (int slot = 0; slot < troops.Length; ++slot)
            {
                troops[slot].Count = ReadUInt16(data, ref offset);
            }

            _army.Assign(troops);
            HasCustomArmy = true;
        }
        else
        {
            offset += 15;
        }

        if (data[offset++] != 0)
        {
            _constructedBuildings |= BuildingType.Captain;
        }

        if (data[offset++] != 0)
        {
            Name = MapTextCodec.Decode(data.Slice(offset, 13));
        }

        offset += 13;

        Race = data[offset++] switch
        {
            0 => MapRace.Knight,
            1 => MapRace.Barbarian,
            2 => MapRace.Sorceress,
            3 => MapRace.Warlock,
            4 => MapRace.Wizard,
            5 => MapRace.Necromancer,
            _ => Color != PlayerColor.None && ownerRace is { } race && race is not MapRace.None
                ? race
                : MapRaces.FromIndex((int)Roll(0, 5, generator)),
        };

        _constructedBuildings |= data[offset++] != 0 ? BuildingType.Castle : BuildingType.Tent;

        DisabledBuildings = BuildingType.Nothing;

        if (data[offset] != 0)
        {
            DisabledBuildings |= BuildingType.Castle;
        }

        PostLoad(dayCount, generator);
    }

    /// <summary>
    /// The state a loaded town is repaired into, <c>Castle::_postLoad()</c> (<c>castle.cpp:428-535</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six repairs, in the reference's order, and each one exists because the block cannot say what the game needs:
    /// </para>
    /// <list type="number">
    /// <item>A random castle may have been given its race after the map gave it buildings, so the upgrades the race
    /// does not have are dropped. This is what makes a random castle's block harmless whatever it stored.</item>
    /// <item>Every built dwelling is filled with one week's growth — of the <i>actual</i> dwelling, so an upgraded
    /// sixth dwelling stocks black dragons and not green ones.</item>
    /// <item>The garrison takes the town's colour.</item>
    /// <item>A neutral town without custom defenders is reinforced four times, which is the "a gray town grows a
    /// garrison by itself" rule.</item>
    /// <item>A shipyard without water is removed, and a necromancer's tavern becomes a shrine (or nothing at all
    /// before <c>Price of Loyalty</c>).</item>
    /// <item>The town may build today.</item>
    /// </list>
    /// <para>
    /// <b>Two steps are missing</b>, both because the systems are: the captain's defaults and spell points, which
    /// need <c>HeroBase</c>'s captain; and <c>_trainGuestHeroAndCaptainInMageGuild()</c>, which needs the mage
    /// guild's spell storage. They are called out here rather than silently skipped, because a loaded town with a
    /// captain would otherwise come out with the building bit and no captain behind it.
    /// </para>
    /// </remarks>
    /// <param name="dayCount">Days elapsed on the map, which is what the neutral reinforcements scale with.</param>
    /// <param name="generator">Generator for those reinforcements; the thread generator when absent.</param>
    private void PostLoad(uint dayCount, Pcg32? generator)
    {
        _constructedBuildings &= Race switch
        {
            MapRace.Knight => ~BuildingType.DwellingUpgrade7,
            MapRace.Barbarian => ~(BuildingType.DwellingUpgrade3 | BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7),
            MapRace.Sorceress => ~(BuildingType.DwellingUpgrade5 | BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7),
            MapRace.Warlock => ~(BuildingType.DwellingUpgrade2 | BuildingType.DwellingUpgrade3 | BuildingType.DwellingUpgrade5),
            MapRace.Wizard => ~(BuildingType.DwellingUpgrade2 | BuildingType.DwellingUpgrade4 | BuildingType.DwellingUpgrade7),
            MapRace.Necromancer => ~(BuildingType.DwellingUpgrade6 | BuildingType.DwellingUpgrade7),
            _ => BuildingType.Nothing,
        };

        if (IsBuild(BuildingType.DwellingMonster1))
        {
            _dwelling[0] = GetGrowth(BuildingType.DwellingMonster1);
        }

        FillShelf(1, BuildingType.DwellingUpgrade2, BuildingType.DwellingMonster2);
        FillShelf(2, BuildingType.DwellingUpgrade3, BuildingType.DwellingMonster3);
        FillShelf(3, BuildingType.DwellingUpgrade4, BuildingType.DwellingMonster4);
        FillShelf(4, BuildingType.DwellingUpgrade5, BuildingType.DwellingMonster5);
        FillShelf(5, BuildingType.DwellingUpgrade7, BuildingType.DwellingUpgrade6, BuildingType.DwellingMonster6);

        _army.Color = Color;

        if (Color == PlayerColor.None && !HasCustomArmy)
        {
            for (int reinforcement = 0; reinforcement < 4; ++reinforcement)
            {
                JoinRandomArmy(dayCount, generator);
            }
        }

        if (!HasSeaAccess)
        {
            _constructedBuildings &= ~BuildingType.Shipyard;
        }

        if (Race == MapRace.Necromancer
            && (_constructedBuildings & BuildingType.Tavern) != 0
            && (DisabledBuildings & BuildingType.Tavern) == 0)
        {
            _constructedBuildings &= ~BuildingType.Tavern;

            if (Version is MapVersion.PriceOfLoyalty or MapVersion.Resurrection)
            {
                _constructedBuildings |= BuildingType.Shrine;
            }
        }

        CanBuildToday = true;
    }

    /// <summary>
    /// Stocks one shelf from the first dwelling in the chain that the town actually has, <c>_postLoad()</c>'s
    /// ladder.
    /// </summary>
    /// <remarks>
    /// The first name that is built decides the creature, so a town with the base dwelling and its upgrade stocks
    /// the upgrade's creature — the shelf is the same, and the newest thing on it wins.
    /// </remarks>
    /// <param name="shelf">The index into <c>_dwelling</c>.</param>
    /// <param name="chain">The dwellings of that shelf, from the best to the base one.</param>
    private void FillShelf(int shelf, params BuildingType[] chain)
    {
        foreach (BuildingType dwelling in chain)
        {
            if (IsBuild(dwelling))
            {
                _dwelling[shelf] = GetGrowth(dwelling);

                return;
            }
        }
    }

    /// <summary>
    /// Adds one random reinforcement to a neutral town's garrison, <c>Castle::_joinRNDArmy()</c>
    /// (<c>castle.cpp:2218-2247</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The quality roll is <c>Rand(1, 15)</c> plus one per ten days elapsed, so the same town gets better troops as
    /// the game goes on; the thresholds turn that number into a dwelling from the second to the fifth, and every
    /// branch adds a different roll of creatures on top of a count that also grows with time.
    /// </para>
    /// <para>
    /// The reference joins with <c>emptySlotFirst = false</c>, which is what lets four reinforcements of the same
    /// kind stack into one slot instead of filling the garrison.
    /// </para>
    /// </remarks>
    /// <param name="dayCount">Days elapsed on the map.</param>
    /// <param name="generator">Generator for the rolls; the thread generator when absent.</param>
    private void JoinRandomArmy(uint dayCount, Pcg32? generator)
    {
        uint timeModifier = dayCount / 10;
        uint quality = Roll(1, 15, generator) + timeModifier;
        uint count = timeModifier / 2;
        BuildingType dwelling = BuildingType.DwellingMonster1;

        if (quality > 15)
        {
            dwelling = BuildingType.DwellingMonster5;
            count += 1;
        }
        else if (quality > 13)
        {
            dwelling = BuildingType.DwellingMonster4;
            count += Roll(1, 3, generator);
        }
        else if (quality > 10)
        {
            dwelling = BuildingType.DwellingMonster3;
            count += Roll(3, 5, generator);
        }
        else if (quality > 5)
        {
            dwelling = BuildingType.DwellingMonster2;
            count += Roll(5, 7, generator);
        }
        else
        {
            count += Roll(8, 15, generator);
        }

        _army.JoinTroop(DwellingMonsters.FromDwelling(Race, dwelling), count, false);
    }

    /// <summary>An inclusive roll, from the given generator when there is one and from the thread generator otherwise.</summary>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value.</param>
    /// <param name="generator">The generator, or <see langword="null"/> for the thread one.</param>
    /// <returns>A value in <c>[min, max]</c>.</returns>
    private static uint Roll(uint min, uint max, Pcg32? generator) =>
        generator is null ? Rand.Get(min, max) : Rand.GetWithGen(min, max, generator);

    /// <summary>A little-endian 16-bit field, the reference's <c>getLE16()</c>.</summary>
    /// <param name="data">The block being read.</param>
    /// <param name="offset">The read position, advanced by two.</param>
    /// <returns>The value.</returns>
    private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        ushort value = (ushort)(data[offset] | (data[offset + 1] << 8));
        offset += 2;

        return value;
    }

    /// <summary>
    /// Where a dwelling's stock lives, the body of <c>Castle::_getDwelling()</c> (<c>castle.cpp:805-831</c>).
    /// </summary>
    /// <remarks>
    /// An upgrade is the same shelf as the dwelling it replaces, and the sixth shelf answers for three names.
    /// </remarks>
    /// <param name="buildingType">A dwelling or a dwelling upgrade.</param>
    /// <returns>An index into <c>_dwelling</c>, or -1 when the building is not a dwelling.</returns>
    private static int DwellingIndex(BuildingType buildingType) => buildingType switch
    {
        BuildingType.DwellingMonster1 => 0,
        BuildingType.DwellingMonster2 or BuildingType.DwellingUpgrade2 => 1,
        BuildingType.DwellingMonster3 or BuildingType.DwellingUpgrade3 => 2,
        BuildingType.DwellingMonster4 or BuildingType.DwellingUpgrade4 => 3,
        BuildingType.DwellingMonster5 or BuildingType.DwellingUpgrade5 => 4,
        BuildingType.DwellingMonster6 or BuildingType.DwellingUpgrade6 or BuildingType.DwellingUpgrade7 => 5,
        _ => -1,
    };

    /// <summary>
    /// One week's growth of a dwelling's creature, <c>Monster( _race, dwelling ).GetGrown()</c>.
    /// </summary>
    /// <param name="dwelling">A dwelling of this town's race.</param>
    /// <returns>The growth, which is what the shelf holds when the dwelling is finished.</returns>
    private uint GetGrowth(BuildingType dwelling) =>
        MonsterStatistics.GetGeneralStats(DwellingMonsters.FromDwelling(Race, dwelling)).BaseGrowth;

    /// <summary>
    /// The shelf of a building's stock, <c>Castle::_getDwelling()</c> (<c>castle.cpp:813-836</c>).
    /// </summary>
    /// <remarks>
    /// The reference returns a pointer and answers <c>nullptr</c> when the building is not built; the port returns
    /// <c>-1</c>, which is the same "no shelf" and cannot be dereferenced by accident. Note that the lookup is for
    /// the building asked for, not for its upgrade: the weekly growth asks for the basic ids, so a castle that
    /// only carries an upgrade bit grows nothing.
    /// </remarks>
    /// <param name="building">A dwelling or a dwelling upgrade.</param>
    /// <returns>An index into <c>_dwelling</c>, or -1 when it is not built.</returns>
    private int GetDwellingShelf(BuildingType building) => IsBuild(building) ? DwellingIndex(building) : -1;

    /// <summary>
    /// The shelf of the dwelling whose creature is the monster of the week or month
    /// (<c>castle.cpp:886-908</c>, <c>castle.cpp:928-943</c>).
    /// </summary>
    /// <remarks>
    /// Only a dwelling whose <b>exact</b> building stands counts, and the two wells of the same creature share a
    /// shelf anyway, so the first match is the only match. The reference walks the twelve dwellings in order and
    /// breaks on the first hit; the order is kept in case a race ever maps two dwellings to one creature.
    /// </remarks>
    /// <param name="monster">The monster of the week, <c>Week.Monster</c>.</param>
    /// <param name="shelf">The shelf it feeds.</param>
    /// <returns><see langword="true"/> when this town has such a dwelling.</returns>
    private bool TryFindWeekMonsterDwelling(MonsterType monster, out int shelf)
    {
        shelf = -1;

        if (monster == MonsterType.Unknown)
        {
            return false;
        }

        foreach (BuildingType dwelling in AllDwellings)
        {
            if (!IsExactBuildingBuilt(dwelling) || DwellingMonsters.FromDwelling(Race, dwelling) != monster)
            {
                continue;
            }

            shelf = DwellingIndex(dwelling);

            return shelf >= 0;
        }

        return false;
    }
}
