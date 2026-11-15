using System.Numerics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
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
/// the daily build permission, and the buy-a-building rules (<see cref="CheckBuyBuilding"/>,
/// <see cref="BuyBuilding"/>, <see cref="GetAllBuildingStatus"/>).
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
/// Not here yet, roughly in the order they are worth doing: recruitment (<c>RecruitMonster</c>,
/// <c>getRecruitLimit</c>, <c>getAvailableArmy</c>), which needs <c>Troop</c> arithmetic and the kingdom's money;
/// the weekly growth (<c>ActionNewWeek</c>) and the well bonuses, which need the world's week type; taxes and the
/// mage guild's spells; the <c>_postLoad()</c> tail that refills the dwellings of a loaded town; and the score
/// modifiers (<c>GetPowerModificator</c> and the AI's visit value).
/// </para>
/// </remarks>
public sealed class Castle
{
    /// <summary>
    /// The number of creature dwellings a town can have, <c>Castle::maxNumOfDwellings</c> (<c>castle.h:129</c>).
    /// </summary>
    public const int MaxNumOfDwellings = 6;

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

    /// <summary>The town's race, <c>_race</c>. Fixed for the life of the town.</summary>
    public MapRace Race { get; }

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
    /// The reference fills it from the race's name table in <c>_postLoad()</c> and offers <c>setName()</c> for
    /// renaming; the port stores what the caller gives it and leaves the table to the map loader.
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
}
