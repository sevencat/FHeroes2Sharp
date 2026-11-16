using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;
using KingdomState = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the daily income: the three tables of <c>profit.cpp</c> (<see cref="ProfitConditions"/>) and the kingdom's
/// sum over them (<c>kingdom.cpp:564-646</c>).
/// </summary>
public sealed class KingdomIncomeTests
{
    [Fact]
    public void EveryMinePaysWhatTheReferenceSaysItPays()
    {
        // profit.cpp:101-114. Note the one trap: mercury comes before ore in the reference's `Cost`, so the ore
        // entry `Cost{ 0, 0, 0, 2, 0, 0, 0 }` is two ore and not two mercury.
        Assert.Equal(new Funds { Wood = 2 }, ProfitConditions.FromMine(Resource.Wood));
        Assert.Equal(new Funds { Ore = 2 }, ProfitConditions.FromMine(Resource.Ore));
        Assert.Equal(new Funds { Mercury = 1 }, ProfitConditions.FromMine(Resource.Mercury));
        Assert.Equal(new Funds { Sulfur = 1 }, ProfitConditions.FromMine(Resource.Sulfur));
        Assert.Equal(new Funds { Crystal = 1 }, ProfitConditions.FromMine(Resource.Crystal));
        Assert.Equal(new Funds { Gems = 1 }, ProfitConditions.FromMine(Resource.Gems));
        Assert.Equal(new Funds { Gold = 1000 }, ProfitConditions.FromMine(Resource.Gold));
    }

    [Fact]
    public void AnUnknownMinePaysNothing()
    {
        // The reference asserts here (profit.cpp:116); the port answers zero instead of aborting.
        Assert.Equal(default, ProfitConditions.FromMine(Resource.Unknown));
        Assert.Equal(default, ProfitConditions.FromMine(0x4000));
    }

    [Fact]
    public void OnlyFourBuildingsPayAndOneOfThemOnlyForTheWarlock()
    {
        Assert.Equal(new Funds { Gold = 1000 }, ProfitConditions.FromBuilding(BuildingType.Castle, MapRace.None));
        Assert.Equal(new Funds { Gold = 250 }, ProfitConditions.FromBuilding(BuildingType.Tent, MapRace.None));
        Assert.Equal(new Funds { Gold = 250 }, ProfitConditions.FromBuilding(BuildingType.Statue, MapRace.None));

        Assert.Equal(new Funds { Gold = 500 }, ProfitConditions.FromBuilding(BuildingType.Spec, MapRace.Warlock));

        // The other five races' special buildings pay nothing, and neither does any other building.
        Assert.Equal(default, ProfitConditions.FromBuilding(BuildingType.Spec, MapRace.Knight));
        Assert.Equal(default, ProfitConditions.FromBuilding(BuildingType.Spec, MapRace.None));
        Assert.Equal(default, ProfitConditions.FromBuilding(BuildingType.Marketplace, MapRace.Warlock));
        Assert.Equal(default, ProfitConditions.FromBuilding(BuildingType.Nothing, MapRace.Warlock));
    }

    [Fact]
    public void TheGoldenGoosePaysTenThousand()
    {
        // profit.cpp:52-96 over artifact_info.cpp's table: the goose is the only income artifact of the game.
        Assert.Equal(new Funds { Gold = 10000 }, ProfitConditions.FromArtifact(ArtifactType.GoldenGoose));
    }

    [Fact]
    public void TheTaxLienTakesTwoHundredAndFiftyGoldAway()
    {
        // artifact_info.cpp:734: the one curse that names a resource. A negative Funds is the answer, not an
        // exception, because the kingdom's sum has to be able to go down.
        Assert.Equal(-250, ProfitConditions.FromArtifact(ArtifactType.TaxLien).Gold);
    }

    [Fact]
    public void TheEndlessBagsFeedOneResourceEach()
    {
        // artifact_info.cpp:738-744 and its three gold cousins: seven bags, one resource each.
        Assert.Equal(new Funds { Wood = 1 }, ProfitConditions.FromArtifact(ArtifactType.EndlessCordWood));
        Assert.Equal(new Funds { Ore = 1 }, ProfitConditions.FromArtifact(ArtifactType.EndlessCartOre));
        Assert.Equal(new Funds { Mercury = 1 }, ProfitConditions.FromArtifact(ArtifactType.EndlessVialMercury));
        Assert.Equal(new Funds { Sulfur = 1 }, ProfitConditions.FromArtifact(ArtifactType.EndlessPouchSulfur));
        Assert.Equal(new Funds { Crystal = 1 }, ProfitConditions.FromArtifact(ArtifactType.EndlessPouchCrystal));
        Assert.Equal(new Funds { Gems = 1 }, ProfitConditions.FromArtifact(ArtifactType.EndlessPouchGems));

        Assert.Equal(new Funds { Gold = 500 }, ProfitConditions.FromArtifact(ArtifactType.EndlessPurseGold));
        Assert.Equal(new Funds { Gold = 750 }, ProfitConditions.FromArtifact(ArtifactType.EndlessBagGold));
        Assert.Equal(new Funds { Gold = 1000 }, ProfitConditions.FromArtifact(ArtifactType.EndlessSackGold));
    }

    [Fact]
    public void AnArtifactWithoutResourceBonusesPaysNothing()
    {
        // The ultimate sword grants attack, which is somebody else's business.
        Assert.Equal(default, ProfitConditions.FromArtifact(ArtifactType.UltimateSword));
        Assert.Equal(default, ProfitConditions.FromArtifact(ArtifactType.Unknown));
        Assert.Equal(default, ProfitConditions.FromArtifact(ArtifactType.ArtifactCount));
    }

    [Fact]
    public void AKingdomWithoutTownsOrMinesEarnsNothing()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        Assert.Equal(default, kingdom.GetIncome(_ => 0));
        Assert.Equal(default, kingdom.GetIncome(_ => 0, IncomeType.None));
    }

    [Fact]
    public void ACountedMineIsMultiplyingTheTable()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        Funds income = kingdom.GetIncome(resource => resource switch
        {
            Resource.Gold => 3,
            Resource.Wood => 2,
            _ => 0,
        });

        Assert.Equal(3000, income.Gold);
        Assert.Equal(4, income.Wood);

        // The casters, the sulfur and the rest are untouched.
        Assert.Equal(0, income.Ore);
        Assert.Equal(0, income.Mercury);
    }

    [Fact]
    public void TheCallbackIsAskedForEveryResourceOfTheReference()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        List<int> asked = [];

        kingdom.GetIncome(resource =>
        {
            asked.Add(resource);
            return 0;
        });

        // kingdom.cpp:570-571, in that order: the answer is a sum, so only the set is asserted, not the order.
        Assert.Equal(7, asked.Count);
        Assert.Contains(Resource.Wood, asked);
        Assert.Contains(Resource.Ore, asked);
        Assert.Contains(Resource.Mercury, asked);
        Assert.Contains(Resource.Sulfur, asked);
        Assert.Contains(Resource.Crystal, asked);
        Assert.Contains(Resource.Gems, asked);
        Assert.Contains(Resource.Gold, asked);
        Assert.DoesNotContain(Resource.Unknown, asked);
    }

    [Fact]
    public void ATownWithoutItsCastlePaysTwoHundredAndFifty()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        kingdom.AddCastle(new CastleTown(5, MapRace.Knight));

        Assert.Equal(new Funds { Gold = 250 }, kingdom.GetIncome(_ => 0));
    }

    [Fact]
    public void TheCastleAndTheStatueAddUp()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        CastleTown town = new(5, MapRace.Knight);
        town.AddBuildings(BuildingType.Castle | BuildingType.Statue);

        kingdom.AddCastle(town);

        // 1000 for the fort plus 250 for the statue.
        Assert.Equal(new Funds { Gold = 1250 }, kingdom.GetIncome(_ => 0));
    }

    [Fact]
    public void TheWarlocksDungeonPaysOnlyForTheWarlock()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        CastleTown warlock = new(5, MapRace.Warlock);
        warlock.AddBuildings(BuildingType.Castle | BuildingType.Spec);

        CastleTown knight = new(20, MapRace.Knight);
        knight.AddBuildings(BuildingType.Castle | BuildingType.Spec);

        kingdom.AddCastle(warlock);
        kingdom.AddCastle(knight);

        // Two castles are 2000; only the warlock's dungeon adds 500 on top.
        Assert.Equal(new Funds { Gold = 2500 }, kingdom.GetIncome(_ => 0));
    }

    [Fact]
    public void TheTypeMaskPicksTheSources()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        CastleTown town = new(5, MapRace.Knight);
        town.AddBuildings(BuildingType.Castle);
        kingdom.AddCastle(town);

        // Castles only: the mine callback is never asked, so the gold mine below is not counted.
        Funds castlesOnly = kingdom.GetIncome(_ => 5, IncomeType.Castles);
        Assert.Equal(1000, castlesOnly.Gold);
        Assert.Equal(0, castlesOnly.Wood);

        // Captured only: five gold mines and nothing else.
        Funds minesOnly = kingdom.GetIncome(resource => resource == Resource.Gold ? 5u : 0u, IncomeType.Captured);
        Assert.Equal(5000, minesOnly.Gold);
    }
}
