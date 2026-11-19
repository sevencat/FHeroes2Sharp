using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="BuildingInfo"/>: the four rewrites of the constructor, the price it asks about, and the text it
/// answers with — <c>buildinginfo.cpp:214-315</c> and <c>:564-610</c>.
/// </summary>
public sealed class BuildingInfoTests
{
    /// <summary>Enough of everything for any single building in the game, several times over.</summary>
    private static Funds Rich => new(999, 999, 999, 999, 999, 999, 1_000_000);

    [Fact]
    public void AnUnbuiltDwellingAnswersWithTheDwellingAndItsCreature()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.DwellingMonster1);

        BuildingInfo info = new(castle, BuildingType.DwellingMonster2, Rich);

        Assert.Equal(BuildingType.DwellingMonster2, info.Building);
        Assert.Equal(BuildingType.DwellingMonster2, info.Type);
        Assert.Equal(BuildingStatus.AllowBuild, info.Status);
        Assert.Equal("The Archery Range produces archers.", info.Description);
        Assert.Equal("Archery Range", info.Name);
    }

    [Fact]
    public void ABuiltDwellingAnswersWithItsUpgrade()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.DwellingMonster2);

        BuildingInfo info = new(castle, BuildingType.DwellingMonster2, Rich);

        // The screen still asks about the base dwelling; the answer is the upgrade the town can buy next.
        Assert.Equal(BuildingType.DwellingMonster2, info.Building);
        Assert.Equal(BuildingType.DwellingUpgrade2, info.Type);
        Assert.Equal(CastleBuildings.GetCost(BuildingType.DwellingUpgrade2, MapRace.Knight), info.Cost);
    }

    [Fact]
    public void ThePositionMovesTheBuildingWithoutResizingIt()
    {
        CastleTown castle = new(0, MapRace.Knight);
        BuildingInfo info = new(castle, BuildingType.Tent, Rich);

        Assert.Equal(new Rect(0, 0, 135, 70), info.Area);

        info.SetPosition(12, 34);

        Assert.Equal(new Rect(12, 34, 135, 70), info.Area);

        info.SetPosition(new Point(5, 6));

        Assert.Equal(new Rect(5, 6, 135, 70), info.Area);
    }

    [Fact]
    public void ATownWithoutACastleIsToldSoInsteadOfThePrice()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent);

        BuildingInfo info = new(castle, BuildingType.Well, Rich);

        Assert.Equal(BuildingStatus.NeedCastle, info.Status);

        // Only "disabled" and "no sea access" swap the text for the condition: every other status still describes
        // what the player would get, and the status bar is what says why it is not for sale yet.
        Assert.DoesNotContain("%{count}", info.Description, StringComparison.Ordinal);
        Assert.Contains(CastleTown.GrownWell.ToString(), info.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void APoorTreasuryLeavesTheStatusToSaySoButKeepsTheText()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle);

        Funds poor = new(0, 0, 0, 0, 0, 0, 0);
        BuildingInfo info = new(castle, BuildingType.Well, poor);

        Assert.Equal(BuildingStatus.LackResources, info.Status);
        Assert.DoesNotContain("%{count}", info.Description, StringComparison.Ordinal);
        Assert.Contains(CastleTown.GrownWell.ToString(), info.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ANecromancerTavernAsksAboutTheShrineInPriceOfLoyalty()
    {
        CastleTown castle = new(0, MapRace.Necromancer, MapVersion.PriceOfLoyalty);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Tavern);

        BuildingInfo info = new(castle, BuildingType.Tavern, Rich);

        Assert.Equal(BuildingType.Shrine, info.Type);
        Assert.Equal("Shrine", info.Name);
    }

    [Fact]
    public void ANecromancerTavernIsNothingWithoutTheShrine()
    {
        CastleTown castle = new(0, MapRace.Necromancer, MapVersion.SuccessionWars);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Tavern);

        BuildingInfo info = new(castle, BuildingType.Tavern, Rich);

        Assert.Equal(BuildingType.Nothing, info.Type);
    }

    [Fact]
    public void TheTextWithANumberIsFilledIn()
    {
        CastleTown castle = new(0, MapRace.Knight);

        string moat = BuildingInfo.GetBuildingDescription(MapRace.Knight, BuildingType.Moat);
        string well = BuildingInfo.GetBuildingDescription(MapRace.Knight, BuildingType.Well);

        Assert.DoesNotContain("%{count}", moat);
        Assert.DoesNotContain("%{count}", well);
        Assert.Contains(GameStatic.BattleMoatReduceDefense.ToString(), moat);
        Assert.Contains(CastleTown.GrownWell.ToString(), well);
    }

    [Fact]
    public void ARandomTownNamesTheLevelOfADwelling()
    {
        string description = BuildingInfo.GetBuildingDescription(MapRace.Random, BuildingType.DwellingMonster2);

        // No race has been picked yet, so not even the building's name is known.
        Assert.EndsWith("produces level 2 creatures.", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ADwellingDescriptionUsesTheReferencesPlural()
    {
        // "Magi", not "Mages": the description takes the reference's plural, lowercased because it follows "produces".
        string description = BuildingInfo.GetBuildingDescription(MapRace.Wizard, BuildingType.DwellingMonster5);

        Assert.EndsWith("produces magi.", description, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatusLineOfADwellingOffersItsCreatureInThePlural()
    {
        // StringReplaceWithLowercase: the sentence starts with a capital, the creature stays lower case.
        Assert.Equal("Recruit peasants", BuildingInfo.GetStatusLine(MapRace.Knight, BuildingType.DwellingMonster1));
        Assert.Equal("Recruit magi", BuildingInfo.GetStatusLine(MapRace.Wizard, BuildingType.DwellingMonster5));
    }

    [Fact]
    public void TheStatusLineOfAnUpgradedDwellingNamesTheUpgradedCreature()
    {
        string line = BuildingInfo.GetStatusLine(MapRace.Knight, BuildingType.DwellingUpgrade6);

        Assert.Equal("Recruit " + MonsterNames.GetMulti(MonsterType.Crusader).ToLowerInvariant(), line);
    }

    [Fact]
    public void TheStatusLineOfAnythingElseIsItsName()
    {
        Assert.Equal(CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Well),
            BuildingInfo.GetStatusLine(MapRace.Knight, BuildingType.Well));
        Assert.Equal(CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Tavern),
            BuildingInfo.GetStatusLine(MapRace.Knight, BuildingType.Tavern));
    }

    [Fact]
    public void TheStatusLineOfARandomTownNamesTheLevel()
    {
        // A random town's dwelling has no creature yet, so the line falls back to the level.
        Assert.Equal("Recruit level 3 creatures",
            BuildingInfo.GetStatusLine(MapRace.Random, BuildingType.DwellingMonster3));
    }
}
