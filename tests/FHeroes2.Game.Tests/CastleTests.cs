using System.Numerics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The <see cref="Castle"/> skeleton: what a town starts with, what it may buy, and what it then counts as.
/// </summary>
public sealed class CastleTests
{
    /// <summary>Enough of everything for any single building in the game, several times over.</summary>
    private static Funds Rich => new(999, 999, 999, 999, 999, 999, 1_000_000);

    [Fact]
    public void ANewTownOwnsNothingAndMayBuildToday()
    {
        CastleTown castle = new(42, MapRace.Knight);

        Assert.Equal(42, castle.TileIndex);
        Assert.Equal(MapRace.Knight, castle.Race);
        Assert.Equal(PlayerColor.None, castle.Color);
        Assert.Equal(BuildingType.Nothing, castle.ConstructedBuildings);
        Assert.False(castle.IsCastle);
        Assert.True(castle.CanBuildToday);
        Assert.Equal(0, castle.CountBuildings());
        Assert.Equal(0, castle.GetLevelMageGuild());
        Assert.Equal(0u, castle.Army.GetCount());
    }

    [Fact]
    public void TheSecondHutIsRolledPerDifficulty()
    {
        GameDifficulty[] ladder =
        [
            GameDifficulty.Impossible,
            GameDifficulty.Expert,
            GameDifficulty.Hard,
            GameDifficulty.Normal,
            GameDifficulty.Easy,
        ];

        int[] townsWithTwoHuts = new int[ladder.Length];

        for (int difficulty = 0; difficulty < ladder.Length; ++difficulty)
        {
            for (ulong seed = 1; seed <= 200; ++seed)
            {
                CastleTown castle = new(0, MapRace.Knight);
                castle.SetDefaultBuildings(ladder[difficulty], new Pcg32(seed));

                Assert.True(castle.IsBuild(BuildingType.DwellingMonster1));

                if (castle.IsBuild(BuildingType.DwellingMonster2))
                {
                    ++townsWithTwoHuts[difficulty];
                }
            }
        }

        // Impossible is a hard zero; the rest are monotone in the difficulty, on the same 200 rolls.
        Assert.Equal(0, townsWithTwoHuts[0]);
        Assert.True(townsWithTwoHuts[1] <= townsWithTwoHuts[2], "expert must not beat hard");
        Assert.True(townsWithTwoHuts[2] <= townsWithTwoHuts[3], "hard must not beat normal");
        Assert.True(townsWithTwoHuts[3] <= townsWithTwoHuts[4], "normal must not beat easy");
        Assert.InRange(townsWithTwoHuts[4], townsWithTwoHuts[1] + 1, 200);
    }

    [Fact]
    public void OnlyTheCastleIsForSaleBeforeItIsBuilt()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent);

        Funds treasury = Rich;

        Assert.Equal(BuildingStatus.AllowBuild, castle.CheckBuyBuilding(BuildingType.Castle, treasury));
        Assert.Equal(BuildingStatus.NeedCastle, castle.CheckBuyBuilding(BuildingType.Well, treasury));
        Assert.Equal(BuildingStatus.NeedCastle, castle.CheckBuyBuilding(BuildingType.MageGuild1, treasury));
        Assert.True(castle.AllowBuyBuilding(BuildingType.Castle, treasury));
        Assert.False(castle.AllowBuyBuilding(BuildingType.Well, treasury));
    }

    [Fact]
    public void TheTentIsGoneOnceTheCastleStands()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent);

        Funds treasury = Rich;

        Assert.True(castle.BuyBuilding(BuildingType.Castle, ref treasury));
        Assert.False(castle.IsBuild(BuildingType.Tent));
        Assert.True(castle.IsCastle);

        // The permit was spent, and "not today" is asked before "you already have a castle".
        Assert.Equal(BuildingStatus.NotToday, castle.CheckBuyBuilding(BuildingType.Tent, treasury));

        castle.ActionNewDay();

        Assert.Equal(BuildingStatus.BuildDisabled, castle.CheckBuyBuilding(BuildingType.Tent, treasury));
    }

    [Fact]
    public void OneBuildingPerTownPerDay()
    {
        Funds treasury = Rich;
        CastleTown castle = Town(MapRace.Knight);

        Assert.Equal(BuildingStatus.AllowBuild, castle.CheckBuyBuilding(BuildingType.Well, treasury));
        Assert.True(castle.BuyBuilding(BuildingType.Well, ref treasury));
        Assert.False(castle.CanBuildToday);
        Assert.Equal(BuildingStatus.NotToday, castle.CheckBuyBuilding(BuildingType.Statue, treasury));

        castle.ActionNewDay();

        Assert.True(castle.CanBuildToday);
        Assert.Equal(BuildingStatus.AllowBuild, castle.CheckBuyBuilding(BuildingType.Statue, treasury));
    }

    [Fact]
    public void TheMageGuildIsRaisedOneLevelAtATime()
    {
        Funds treasury = Rich;
        CastleTown castle = Town(MapRace.Knight);

        // Not today comes before "this upgrade does not exist here", but after "one level at a time": the second
        // level needs the first, so a town without a guild is told the second one is disabled, not that it is
        // for sale.
        Assert.Equal(BuildingStatus.BuildDisabled, castle.CheckBuyBuilding(BuildingType.MageGuild2, treasury));
        Assert.Equal(BuildingStatus.BuildDisabled, castle.CheckBuyBuilding(BuildingType.MageGuild3, treasury));

        Build(castle, BuildingType.MageGuild1);

        Assert.Equal(BuildingStatus.AllowBuild, castle.CheckBuyBuilding(BuildingType.MageGuild2, treasury));
        Assert.Equal(BuildingStatus.BuildDisabled, castle.CheckBuyBuilding(BuildingType.MageGuild3, treasury));

        Build(castle, BuildingType.MageGuild2);

        Assert.Equal(2, castle.GetLevelMageGuild());

        Build(castle, BuildingType.MageGuild3);
        Build(castle, BuildingType.MageGuild4);
        Build(castle, BuildingType.MageGuild5);

        Assert.Equal(5, castle.GetLevelMageGuild());
        Assert.Equal(BuildingStatus.AlreadyBuilt, castle.CheckBuyBuilding(BuildingType.MageGuild5, treasury));
    }

    [Fact]
    public void TheShipyardNeedsWater()
    {
        Funds treasury = Rich;
        CastleTown castle = Town(MapRace.Knight);
        castle.HasSeaAccess = false;

        Assert.Equal(BuildingStatus.ShipyardNotAllowed, castle.CheckBuyBuilding(BuildingType.Shipyard, treasury));

        castle.HasSeaAccess = true;

        Assert.Equal(BuildingStatus.AllowBuild, castle.CheckBuyBuilding(BuildingType.Shipyard, treasury));
    }

    [Fact]
    public void TheNecromancerTradesTheTavernForTheShrine()
    {
        Funds treasury = Rich;

        CastleTown necromancer = Town(MapRace.Necromancer, MapVersion.PriceOfLoyalty);

        Assert.Equal(BuildingStatus.BuildDisabled, necromancer.CheckBuyBuilding(BuildingType.Tavern, treasury));
        Assert.Equal(BuildingStatus.AllowBuild, necromancer.CheckBuyBuilding(BuildingType.Shrine, treasury));

        CastleTown oldNecromancer = Town(MapRace.Necromancer, MapVersion.SuccessionWars);

        Assert.Equal(BuildingStatus.BuildDisabled, oldNecromancer.CheckBuyBuilding(BuildingType.Tavern, treasury));
        Assert.Equal(BuildingStatus.BuildDisabled, oldNecromancer.CheckBuyBuilding(BuildingType.Shrine, treasury));

        CastleTown knight = Town(MapRace.Knight);

        Assert.Equal(BuildingStatus.AllowBuild, knight.CheckBuyBuilding(BuildingType.Tavern, treasury));
        Assert.Equal(BuildingStatus.BuildDisabled, knight.CheckBuyBuilding(BuildingType.Shrine, treasury));
    }

    [Fact]
    public void OnlyTheWarlockHasABlackDragonLair()
    {
        Funds treasury = Rich;

        CastleTown knight = Town(MapRace.Knight);

        Assert.Equal(BuildingStatus.UnknownUpgrade, knight.CheckBuyBuilding(BuildingType.DwellingUpgrade7, treasury));

        CastleTown warlock = Town(MapRace.Warlock);

        Assert.Equal(BuildingStatus.RequiresBuild, warlock.CheckBuyBuilding(BuildingType.DwellingUpgrade7, treasury));

        Build(warlock, BuildingType.DwellingMonster6);

        Assert.Equal(
            BuildingStatus.AllowBuild,
            warlock.CheckBuyBuilding(BuildingType.DwellingUpgrade6, treasury));

        Build(warlock, BuildingType.DwellingUpgrade6);

        Assert.Equal(
            BuildingStatus.AllowBuild,
            warlock.CheckBuyBuilding(BuildingType.DwellingUpgrade7, treasury));
    }

    [Fact]
    public void ADwellingOpensFull()
    {
        CastleTown castle = Town(MapRace.Knight);

        Build(castle, BuildingType.DwellingMonster1);

        Assert.Equal(12u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));

        Build(castle, BuildingType.DwellingMonster2);

        Assert.Equal(8u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(0u, castle.GetMonstersInDwelling(BuildingType.MageGuild1));
    }

    [Fact]
    public void AnUpgradedDwellingIsTheSameShelf()
    {
        CastleTown castle = Town(MapRace.Knight);

        Build(castle, BuildingType.DwellingMonster2);
        Build(castle, BuildingType.DwellingUpgrade2);

        Assert.Equal(BuildingType.DwellingUpgrade2, castle.GetActualDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster2),
            castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2));
        Assert.Equal(8u, castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2));
        Assert.Equal(BuildingType.Nothing, castle.GetActualDwelling(BuildingType.Well));
    }

    [Fact]
    public void TheBlackDragonLairIsWhereTheDragonUpgradeChainEnds()
    {
        CastleTown warlock = Town(MapRace.Warlock);

        Build(warlock, BuildingType.DwellingMonster6);

        Assert.Equal(BuildingType.DwellingMonster6, warlock.GetActualDwelling(BuildingType.DwellingMonster6));
        Assert.Equal(BuildingType.DwellingUpgrade6, warlock.GetUpgradeBuilding(BuildingType.DwellingMonster6));

        Build(warlock, BuildingType.DwellingUpgrade6);

        Assert.Equal(BuildingType.DwellingUpgrade6, warlock.GetActualDwelling(BuildingType.DwellingMonster6));
        Assert.Equal(BuildingType.DwellingUpgrade7, warlock.GetUpgradeBuilding(BuildingType.DwellingMonster6));

        Build(warlock, BuildingType.DwellingUpgrade7);

        Assert.Equal(BuildingType.DwellingUpgrade7, warlock.GetActualDwelling(BuildingType.DwellingMonster6));
        Assert.Equal(
            warlock.GetMonstersInDwelling(BuildingType.DwellingMonster6),
            warlock.GetMonstersInDwelling(BuildingType.DwellingUpgrade7));

        // The other towns have nothing to do with the lair, however it is asked about.
        CastleTown knight = Town(MapRace.Knight);

        Build(knight, BuildingType.DwellingMonster6);

        Assert.Equal(BuildingType.DwellingMonster6, knight.GetActualDwelling(BuildingType.DwellingMonster6));
        Assert.Equal(BuildingType.DwellingUpgrade6, knight.GetUpgradeBuilding(BuildingType.DwellingMonster6));
    }

    [Fact]
    public void TheCountedBuildingsIgnoreTheUpgradesAndTheTent()
    {
        CastleTown castle = Town(MapRace.Knight);

        Assert.Equal(1, castle.CountBuildings());

        Build(castle, BuildingType.MageGuild1);

        Assert.Equal(2, castle.CountBuildings());

        Build(castle, BuildingType.MageGuild2);

        Assert.Equal(2, castle.CountBuildings());

        Build(castle, BuildingType.DwellingMonster1);

        Assert.Equal(3, castle.CountBuildings());

        Build(castle, BuildingType.DwellingMonster2);

        Assert.Equal(4, castle.CountBuildings());

        // The upgrades have three quarters of the town as prerequisites, so the bit is set directly: what is under
        // test is the count, not the shop.
        castle.AddBuildings(BuildingType.DwellingUpgrade2);

        Assert.Equal(4, castle.CountBuildings());

        Build(castle, BuildingType.Well);

        Assert.Equal(5, castle.CountBuildings());
    }

    [Fact]
    public void TheTownValueWeighsTheCastleTheDragonsAndTheGuild()
    {
        CastleTown tent = new(0, MapRace.Knight);
        tent.AddBuildings(BuildingType.Tent | BuildingType.DwellingMonster1);

        // A tent and its hut are worth nothing: the count starts at the castle.
        Assert.Equal(1, tent.CountBuildings());
        Assert.Equal(1, tent.GetBuildingValue());

        CastleTown castle = Town(MapRace.Knight);

        Assert.Equal(6, castle.GetBuildingValue());

        // A town with every building it can own: 20 counted, the castle 5, the sixth dwelling 6, five dwelling
        // upgrades 5 and the guild's levels 1+2+3+4 on top of the level the count already holds.
        Funds treasury = Rich;
        CastleTown knight = Town(MapRace.Knight);
        BuildEverything(knight, ref treasury);

        Assert.Equal(20, knight.CountBuildings());
        Assert.Equal(46, knight.GetBuildingValue());

        treasury = Rich;
        CastleTown necromancer = Town(MapRace.Necromancer);
        BuildEverything(necromancer, ref treasury);

        // The necromancer has no sixth dwelling upgrade, so one point less.
        Assert.Equal(20, necromancer.CountBuildings());
        Assert.Equal(45, necromancer.GetBuildingValue());

        treasury = Rich;
        CastleTown warlock = Town(MapRace.Warlock);
        BuildEverything(warlock, ref treasury);

        // The warlock's only upgrades are the fourth dwelling's, the sixth's and the lair: two of the five the
        // count sweeps, plus the lair's two.
        Assert.Equal(20, warlock.CountBuildings());
        Assert.Equal(45, warlock.GetBuildingValue());
    }

    [Fact]
    public void TheExactBuildingPictureWantsTheWholeGuildBelowIt()
    {
        CastleTown castle = Town(MapRace.Knight);

        Build(castle, BuildingType.MageGuild1);
        Build(castle, BuildingType.MageGuild2);

        Assert.False(castle.IsExactBuildingBuilt(BuildingType.MageGuild1));
        Assert.True(castle.IsExactBuildingBuilt(BuildingType.MageGuild2));
        Assert.False(castle.IsExactBuildingBuilt(BuildingType.MageGuild3));
        Assert.False(castle.IsExactBuildingBuilt(BuildingType.Well));

        Build(castle, BuildingType.MageGuild3);

        Assert.True(castle.IsExactBuildingBuilt(BuildingType.MageGuild3));

        // The buildings that lead to the archers' dwelling fill half the town, so the bits are set directly: what is
        // under test is the picture, not the shop.
        castle.AddBuildings(BuildingType.DwellingMonster2);

        Assert.True(castle.IsExactBuildingBuilt(BuildingType.DwellingMonster2));

        castle.AddBuildings(BuildingType.DwellingUpgrade2);

        Assert.False(castle.IsExactBuildingBuilt(BuildingType.DwellingMonster2));
        Assert.True(castle.IsExactBuildingBuilt(BuildingType.DwellingUpgrade2));
        Assert.Throws<ArgumentException>(() => castle.IsExactBuildingBuilt(BuildingType.MageGuild));
    }

    [Fact]
    public void EveryTownCanBuildEveryBuildingButTheOnesItHasNoUseFor()
    {
        foreach (MapRace race in new[]
                 {
                     MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard,
                     MapRace.Necromancer,
                 })
        {
            foreach (MapVersion version in new[] { MapVersion.SuccessionWars, MapVersion.PriceOfLoyalty })
            {
                CastleTown castle = new(0, race, version) { HasSeaAccess = true };
                Funds treasury = Rich;
                BuildEverything(castle, ref treasury);

                // The old necromancer town has neither a tavern nor a shrine, so it counts one building fewer.
                int counted = race == MapRace.Necromancer && version == MapVersion.SuccessionWars ? 19 : 20;

                Assert.Equal(counted, castle.CountBuildings());
                Assert.Equal(5, castle.GetLevelMageGuild());

                // Whatever is left is what the town can never own: the tent goes when the castle comes, the
                // shrine is the necromancer's alone and only from Price of Loyalty, and his town has no tavern.
                List<BuildingType> unreachable = [];

                foreach (BuildingType building in SingleBuildings())
                {
                    if (!castle.IsBuild(building))
                    {
                        unreachable.Add(building);
                    }
                }

                List<BuildingType> expected = [BuildingType.Tent];

                // The upgrades this race has no dwelling for. The upgrade table is the authority here, and
                // CheckBuyBuilding's race switch has to agree with it.
                HashSet<BuildingType> ownableUpgrades = OwnableUpgrades(race);

                foreach (BuildingType upgrade in SingleBuildings().Where(
                             building => (building & BuildingType.DwellingUpgrades) != 0 &&
                                         !ownableUpgrades.Contains(building)))
                {
                    expected.Add(upgrade);
                }

                if (race == MapRace.Necromancer)
                {
                    expected.Add(BuildingType.Tavern);

                    if (version == MapVersion.SuccessionWars)
                    {
                        expected.Add(BuildingType.Shrine);
                    }
                }
                else
                {
                    expected.Add(BuildingType.Shrine);
                }

                Assert.Equal(expected.Order().ToList(), unreachable.Order().ToList());
            }
        }
    }

    [Fact]
    public void TheTownAlwaysReportsItsBestAnswer()
    {
        Funds rich = Rich;
        Funds empty = new();

        CastleTown tent = new(0, MapRace.Knight);
        tent.AddBuildings(BuildingType.Tent);

        Assert.Equal(BuildingStatus.NeedCastle, CastleTown.GetAllBuildingStatus(tent, rich));

        CastleTown castle = Town(MapRace.Knight);

        Assert.Equal(BuildingStatus.AllowBuild, CastleTown.GetAllBuildingStatus(castle, rich));
        Assert.Equal(BuildingStatus.LackResources, CastleTown.GetAllBuildingStatus(castle, empty));

        castle.ActionNewDay();

        Funds treasury = Rich;
        Assert.True(castle.BuyBuilding(BuildingType.Well, ref treasury));
        Assert.Equal(BuildingStatus.NotToday, CastleTown.GetAllBuildingStatus(castle, rich));

        BuildEverything(castle, ref rich);

        Assert.Equal(BuildingStatus.UnknownCondition, CastleTown.GetAllBuildingStatus(castle, rich));
    }

    /// <summary>
    /// A town that already has its castle and its building permit, so that a test can start from the interesting
    /// part.
    /// </summary>
    /// <param name="race">The race of the town.</param>
    /// <param name="version">The map version.</param>
    /// <returns>The town, with the castle built.</returns>
    private static CastleTown Town(MapRace race, MapVersion version = MapVersion.PriceOfLoyalty)
    {
        CastleTown castle = new(0, race, version) { HasSeaAccess = true };
        Funds treasury = Rich;

        Assert.True(castle.BuyBuilding(BuildingType.Castle, ref treasury));

        castle.ActionNewDay();

        return castle;
    }

    /// <summary>
    /// Buys one building and everything it needs, then hands the building permit back, so that a test can write the
    /// shopping list it cares about and ignore the half-town that leads to it.
    /// </summary>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building to buy.</param>
    private static void Build(CastleTown castle, BuildingType building)
    {
        if (castle.IsBuild(building))
        {
            return;
        }

        BuildingType requirement = CastleBuildings.GetRequirement(castle.Race, building);

        foreach (BuildingType prerequisite in SingleBuildings())
        {
            if ((requirement & prerequisite) != 0)
            {
                Build(castle, prerequisite);
            }
        }

        Funds treasury = Rich;

        castle.ActionNewDay();

        Assert.True(castle.BuyBuilding(building, ref treasury), $"{building} should be for sale");

        castle.ActionNewDay();
    }

    /// <summary>
    /// Buys everything the town will ever allow, one building per day, until nothing more can be built.
    /// </summary>
    /// <param name="castle">The town.</param>
    /// <param name="treasury">The resources to spend; the buying stops when they are gone.</param>
    private static void BuildEverything(CastleTown castle, ref Funds treasury)
    {
        bool builtSomething = true;

        while (builtSomething)
        {
            builtSomething = false;

            foreach (BuildingType building in SingleBuildings())
            {
                castle.ActionNewDay();

                if (castle.CheckBuyBuilding(building, treasury) != BuildingStatus.AllowBuild)
                {
                    continue;
                }

                if (castle.BuyBuilding(building, ref treasury))
                {
                    builtSomething = true;
                }
            }
        }
    }

    /// <summary>
    /// The dwelling upgrades a race can own, followed along the upgrade table from each of its dwellings.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <returns>The upgrade bits that race's towns have somewhere to put.</returns>
    private static HashSet<BuildingType> OwnableUpgrades(MapRace race)
    {
        HashSet<BuildingType> upgrades = [];

        foreach (BuildingType dwelling in SingleBuildings().Where(CastleBuildings.IsDwelling))
        {
            BuildingType current = dwelling;
            BuildingType upgrade = CastleBuildings.GetUpgradeForBuilding(race, current);

            while (upgrade != current && upgrades.Add(upgrade))
            {
                current = upgrade;
                upgrade = CastleBuildings.GetUpgradeForBuilding(race, current);
            }
        }

        return upgrades;
    }

    /// <summary>The buildings, without the three masks <see cref="BuildingType"/> throws in for convenience.</summary>
    /// <returns>Every building whose value has one bit.</returns>
    private static IEnumerable<BuildingType> SingleBuildings() =>
        Enum.GetValues<BuildingType>().Where(building => BitOperations.PopCount((uint)building) == 1);
}
