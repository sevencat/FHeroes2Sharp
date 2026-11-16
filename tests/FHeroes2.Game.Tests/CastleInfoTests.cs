using System.Text;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using ArmyStack = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// What a town looks like and what it is worth: the building tables of
/// <see cref="CastleBuildingInfo"/>, the mage guild's spell storage and its teaching, the captain, and the three
/// modificators.
/// </summary>
public sealed class CastleInfoTests
{
    /// <summary>Enough of everything for any single building in the game, several times over.</summary>
    private static Funds Rich => new(999, 999, 999, 999, 999, 999, 1_000_000);

    [Fact]
    public void TheSharedBuildingsAreNamedTheSameInEveryTown()
    {
        Assert.Equal("Tavern", CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Tavern));
        Assert.Equal("Mage Guild, Level 3", CastleBuildingInfo.GetName(MapRace.Necromancer, BuildingType.MageGuild3));
        Assert.Equal("Captain's Quarters", CastleBuildingInfo.GetName(MapRace.Wizard, BuildingType.Captain));
        Assert.Equal(string.Empty, CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Nothing));
    }

    [Fact]
    public void EachRaceNamesItsOwnBuildings()
    {
        Assert.Equal("Thatched Hut", CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.DwellingMonster1));
        Assert.Equal("Fortifications", CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Spec));
        Assert.Equal("Farm", CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Well2));

        Assert.Equal("Skull Pile", CastleBuildingInfo.GetName(MapRace.Necromancer, BuildingType.Well2));
        Assert.Equal("Storm", CastleBuildingInfo.GetName(MapRace.Necromancer, BuildingType.Spec));

        Assert.Equal("Library", CastleBuildingInfo.GetName(MapRace.Wizard, BuildingType.Spec));
    }

    [Fact]
    public void TheDescriptionOfALandmarkKeepsItsPlaceholder()
    {
        string description = CastleBuildingInfo.GetDescription(MapRace.Knight, BuildingType.Well2);

        Assert.Contains("%{count}", description, StringComparison.Ordinal);
        Assert.Equal(string.Empty, CastleBuildingInfo.GetDescription(MapRace.Knight, BuildingType.Nothing));
    }

    [Fact]
    public void TheAreaOfABuildingIsPerRaceAndEmptyForNothing()
    {
        IReadOnlyList<Rect> knightHut = CastleBuildingInfo.GetArea(MapRace.Knight, BuildingType.DwellingMonster1);
        IReadOnlyList<Rect> necromancerHut = CastleBuildingInfo.GetArea(MapRace.Necromancer, BuildingType.DwellingMonster1);

        Assert.Single(knightHut);
        Assert.Single(necromancerHut);
        Assert.NotEqual(knightHut[0], necromancerHut[0]);
        Assert.Empty(CastleBuildingInfo.GetArea(MapRace.Knight, BuildingType.Nothing));
    }

    [Fact]
    public void SomeBuildingsAreDrawnAsMoreThanOneSprite()
    {
        // The necromancer's marketplace is a stall and its awning, the castle a wall and a gate.
        Assert.Equal(2, CastleBuildingInfo.GetArea(MapRace.Necromancer, BuildingType.Marketplace).Count);
        Assert.Equal(2, CastleBuildingInfo.GetArea(MapRace.Knight, BuildingType.Statue).Count);
    }

    [Fact]
    public void TheSpriteIndexIsTheOffsetsIntoTheTownsOwnIconSet()
    {
        Assert.Equal(0, CastleBuildingInfo.GetSpriteIndex(BuildingType.MageGuild1));
        Assert.Equal(0, CastleBuildingInfo.GetSpriteIndex(BuildingType.MageGuild5));
        Assert.Equal(2, CastleBuildingInfo.GetSpriteIndex(BuildingType.Tavern));
        Assert.Equal(2, CastleBuildingInfo.GetSpriteIndex(BuildingType.Shrine));
        Assert.Equal(19, CastleBuildingInfo.GetSpriteIndex(BuildingType.DwellingMonster1));
        Assert.Equal(30, CastleBuildingInfo.GetSpriteIndex(BuildingType.DwellingUpgrade7));

        // The tent has no sprite of its own: the town draws the unfinished castle instead.
        Assert.Equal(0, CastleBuildingInfo.GetSpriteIndex(BuildingType.Tent));
    }

    [Fact]
    public void TheRequirementStringIsTheNamesOfThePrerequisites()
    {
        string requirements = CastleBuildingInfo.GetRequirementString(MapRace.Knight, BuildingType.DwellingMonster2);

        Assert.Equal("Thatched Hut", requirements);
        Assert.Equal(string.Empty, CastleBuildingInfo.GetRequirementString(MapRace.Knight, BuildingType.DwellingMonster1));
    }

    [Fact]
    public void TheNecromancerShrineIsDrawnOnlyAfterSuccessionWars()
    {
        IReadOnlyList<BuildingType> successionWars = CastleBuildingInfo.GetDrawingPriorities(MapRace.Necromancer, MapVersion.SuccessionWars);
        IReadOnlyList<BuildingType> priceOfLoyalty = CastleBuildingInfo.GetDrawingPriorities(MapRace.Necromancer, MapVersion.PriceOfLoyalty);

        Assert.DoesNotContain(BuildingType.Shrine, successionWars);

        int shrine = priceOfLoyalty.ToList().IndexOf(BuildingType.Shrine);
        int special = priceOfLoyalty.ToList().IndexOf(BuildingType.Spec);

        Assert.True(shrine >= 0, "the shrine is part of a necromancer town from Price of Loyalty on");
        Assert.Equal(special + 1, shrine);
        Assert.Equal(successionWars.Count + 1, priceOfLoyalty.Count);

        // No other town has the shrine at all.
        foreach (MapRace race in new[] { MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard })
        {
            Assert.DoesNotContain(
                BuildingType.Shrine,
                CastleBuildingInfo.GetDrawingPriorities(race, MapVersion.PriceOfLoyalty));
        }
    }

    [Fact]
    public void TheTavernIsWorthOneMoraleInEveryTown()
    {
        CastleTown castle = new(0, MapRace.Knight);
        StringBuilder explanation = new();

        Assert.Equal(0, castle.GetMoraleModificator(explanation));
        Assert.Equal(0, explanation.Length);

        castle.AddBuildings(BuildingType.Tavern);

        Assert.Equal(1, castle.GetMoraleModificator(explanation));
        Assert.Equal("Tavern +1\n", explanation.ToString());
    }

    [Fact]
    public void TheColiseumIsWorthTwoMoreMorale()
    {
        CastleTown barbarian = new(0, MapRace.Barbarian);
        barbarian.AddBuildings(BuildingType.Tavern | BuildingType.Spec);

        CastleTown knight = new(0, MapRace.Knight);
        knight.AddBuildings(BuildingType.Tavern | BuildingType.Spec);

        Assert.Equal(3, barbarian.GetMoraleModificator());
        Assert.Equal(1, knight.GetMoraleModificator());
    }

    [Fact]
    public void TheShrineOfTheUndeadIsWorthTwoSpellPower()
    {
        CastleTown necromancer = new(0, MapRace.Necromancer);
        necromancer.AddBuildings(BuildingType.Spec);

        CastleTown knight = new(0, MapRace.Knight);
        knight.AddBuildings(BuildingType.Spec);

        StringBuilder explanation = new();

        Assert.Equal(2, necromancer.GetPowerModificator(explanation));
        Assert.Equal("Storm +2", explanation.ToString());
        Assert.Equal(0, knight.GetPowerModificator());
    }

    [Fact]
    public void TheRainbowIsWorthTwoLuck()
    {
        CastleTown sorceress = new(0, MapRace.Sorceress);
        sorceress.AddBuildings(BuildingType.Spec);

        Assert.Equal(2, sorceress.GetLuckModificator());

        sorceress.AddBuildings(BuildingType.Tavern);

        Assert.Equal(2, sorceress.GetLuckModificator());
        Assert.Equal(0, new CastleTown(0, MapRace.Knight).GetLuckModificator());
    }

    [Fact]
    public void TheGuildGrowsWithItsLevelAndTheLibraryAddsOneSlotPerLevel()
    {
        Assert.Equal(3, MageGuild.GetMaxSpellsCount(1, false));
        Assert.Equal(3, MageGuild.GetMaxSpellsCount(2, false));
        Assert.Equal(2, MageGuild.GetMaxSpellsCount(3, false));
        Assert.Equal(2, MageGuild.GetMaxSpellsCount(4, false));
        Assert.Equal(1, MageGuild.GetMaxSpellsCount(5, false));
        Assert.Equal(4, MageGuild.GetMaxSpellsCount(1, true));

        MageGuild guild = new();
        guild.Initialize(MapRace.Knight, false, new Pcg32(1));

        Assert.Equal(3, guild.GetSpells(1, false).Count);
        Assert.Equal(11, guild.GetSpells(5, false).Count);
        Assert.Equal(0, guild.GetSpells(0, false).Count);

        // The library slot only exists when the town rolled with one; asking for it without is empty.
        Assert.Equal(3, guild.GetSpells(1, true).Count);

        MageGuild wizardTown = new();
        wizardTown.Initialize(MapRace.Wizard, true, new Pcg32(1));

        Assert.Equal(4, wizardTown.GetSpells(1, true).Count);
        Assert.Equal(3, wizardTown.GetSpells(1, false).Count);
        Assert.Equal(16, wizardTown.GetSpells(5, true).Count);
    }

    [Fact]
    public void TheCaptainOfAKnightIsAOneOfEachCommander()
    {
        CastleTown castle = new(7, MapRace.Knight);
        Captain captain = castle.Captain;

        Assert.False(captain.IsValid);
        Assert.Equal(PlayerColor.None, captain.Color);
        Assert.Equal(MapRace.Knight, captain.Race);
        Assert.Equal(1, captain.GetAttack());
        Assert.Equal(1, captain.GetDefense());
        Assert.Equal(1, captain.GetPower());
        Assert.Equal(1, captain.GetKnowledge());
        Assert.Equal(10u, captain.SpellPoints);

        castle.AddBuildings(BuildingType.Captain);

        Assert.True(captain.IsValid);
    }

    [Fact]
    public void ACaptainLearnsOnlyTheFirstTwoLevelsAndOnlyWhatTheGuildTeaches()
    {
        CastleTown castle = new(7, MapRace.Wizard);
        castle.AddBuildings(BuildingType.Captain | BuildingType.MageGuild1);
        castle.InitializeMageGuild(new Pcg32(3));

        Captain captain = castle.Captain;
        int before = captain.MagicBook.Count;

        castle.TrainGuestHeroAndCaptainInMageGuild();

        Assert.True(captain.MagicBook.Count > before, "a first level guild has spells a captain can learn");

        foreach (Spell spell in captain.MagicBook)
        {
            Assert.True(spell.Level() <= 2, "a captain may only learn the first two levels");
        }
    }

    [Fact]
    public void ATownWithoutAGuildTeachesNobody()
    {
        CastleTown castle = new(7, MapRace.Wizard);
        castle.AddBuildings(BuildingType.Captain);

        Captain captain = castle.Captain;
        int before = captain.MagicBook.Count;

        castle.TrainGuestHeroAndCaptainInMageGuild();

        Assert.Equal(before, captain.MagicBook.Count);
    }

    [Fact]
    public void TheAvailableArmyIsTheGarrisonPlusWhatTheMoneyBuys()
    {
        CastleTown castle = new(0, MapRace.Knight);

        // A town without walls cannot buy anything but the walls, so the castle comes first.
        castle.AddBuildings(BuildingType.Castle);

        // Buying the dwelling is what fills its stock with a week's growth; AddBuildings() only raises the bit.
        Funds treasury = Rich;
        Assert.True(castle.BuyBuilding(BuildingType.DwellingMonster1, ref treasury));

        ArmyStack garrison = castle.Army;
        Assert.Equal(0u, garrison.GetCount());

        ArmyStack rich = castle.GetAvailableArmy(Rich);
        ArmyStack poor = castle.GetAvailableArmy(new Funds());

        Assert.True(rich.GetCount() > 0, "the dwelling's stock is for sale");
        Assert.True(poor.GetCount() == 0, "nothing is free");

        // Estimating the army does not spend the garrison.
        Assert.Equal(0u, garrison.GetCount());
        Assert.Equal(0.0, castle.GetArmyRecruitmentValue(new Funds()));
        Assert.True(castle.GetArmyRecruitmentValue(Rich) > 0.0);
    }

    [Fact]
    public void AnEmptyTownWithoutAGuildIsWorthNothingToAVisitor()
    {
        CastleTown castle = new(0, MapRace.Knight);
        Hero hero = new(1, MapRace.Knight);

        Assert.Equal(0.0, castle.GetVisitValue(hero, Rich));
        Assert.Equal(0.0, castle.GetGarrisonStrength());
    }

    [Fact]
    public void ACastleIsWorthMoreThanItsGarrison()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.DwellingMonster1 | BuildingType.Castle);
        castle.Army.JoinTroop(MonsterType.Peasant, 10);

        double bare = castle.Army.GetStrength();
        double defended = castle.GetGarrisonStrength();

        Assert.True(defended > bare, "the walls multiply what stands behind them");

        castle.AddBuildings(BuildingType.Moat);

        Assert.True(castle.GetGarrisonStrength() > defended, "the moat adds to the walls");
    }
}
