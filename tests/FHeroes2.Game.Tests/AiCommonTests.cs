using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;
using GameHero = FHeroes2.Game.Heroes.Hero;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using GameTroop = FHeroes2.Game.Army.Troop;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Behaviour tests for the nine functions of <c>ai_common.cpp</c> that live in <see cref="AiCommon"/>.
/// </summary>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AiCommonTests : IDisposable
{
    private readonly World _previousWorld = World.Current;

    public void Dispose()
    {
        World.Current = _previousWorld;
        Settings.Reset();
    }

    [Fact]
    public void OnlyTheHardDifficultiesLetTheAiSplitAndShare()
    {
        Assert.False(Difficulty.AllowAiToSplitWeakStacks(GameDifficulty.Easy));
        Assert.False(Difficulty.AllowAiToSplitWeakStacks(GameDifficulty.Normal));
        Assert.True(Difficulty.AllowAiToSplitWeakStacks(GameDifficulty.Hard));
        Assert.True(Difficulty.AllowAiToSplitWeakStacks(GameDifficulty.Expert));
        Assert.True(Difficulty.AllowAiToSplitWeakStacks(GameDifficulty.Impossible));

        Assert.False(Difficulty.IsObjectVisitInfoSharingAllowedForAi(GameDifficulty.Easy));
        Assert.False(Difficulty.IsObjectVisitInfoSharingAllowedForAi(GameDifficulty.Normal));
        Assert.True(Difficulty.IsObjectVisitInfoSharingAllowedForAi(GameDifficulty.Hard));
        Assert.True(Difficulty.IsObjectVisitInfoSharingAllowedForAi(GameDifficulty.Expert));
        Assert.True(Difficulty.IsObjectVisitInfoSharingAllowedForAi(GameDifficulty.Impossible));
    }

    [Fact]
    public void CountingTheFogAroundATileUsesTheSquaredRadiusOfTheReference()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        Assert.Equal(9, world.Fog!.CountFoggedAround(4, 4, 1, PlayerColor.Blue));
        Assert.Equal(21, world.Fog.CountFoggedAround(4, 4, 2, PlayerColor.Blue));

        // Radius zero clears the tile itself.
        world.Fog.Reveal(4, 4, 0, PlayerColor.Blue);

        Assert.Equal(8, world.Fog.CountFoggedAround(4, 4, 1, PlayerColor.Blue));

        // A radius of zero reveals nothing, and a tile outside the map has no neighbours.
        Assert.Equal(0, world.Fog.CountFoggedAround(4, 4, 0, PlayerColor.Blue));
        Assert.Equal(0, world.Fog.CountFoggedAround(-1, 4, 1, PlayerColor.Blue));
        Assert.Equal(0, world.Fog.CountFoggedAround(4, 99, 1, PlayerColor.Blue));
    }

    [Fact]
    public void TheMarketplacePlanSellsResourcesWithoutSpendingThem()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        AddMarketplace(kingdom);

        kingdom.Treasury = new Funds(ore: 20, wood: 20, mercury: 5, sulfur: 5, crystal: 5, gems: 5, gold: 7500);

        Funds? transaction = AiCommon.CalculateMarketplaceTransaction(
            kingdom, new Funds(Resource.Gold, 9000));

        Assert.NotNull(transaction);

        // Planning is side effect free: the treasury still holds everything it had.
        Assert.Equal(7500, kingdom.Treasury.Gold);
        Assert.Equal(20, kingdom.Treasury.Wood);

        // The plan buys 1500 gold: 17 wood * 25 + 17 ore * 25 + 13 rare * 50. The common resources are
        // sold while they are worth more than a rare one, i.e. down to the three units that no longer
        // beat the 250 gold a rare stack of five brings in.
        Assert.Equal(-1500, transaction.Value.Gold);
        Assert.Equal(17, transaction.Value.Wood);
        Assert.Equal(17, transaction.Value.Ore);
        Assert.Equal(13, (int)(transaction.Value.Mercury + transaction.Value.Sulfur +
                               transaction.Value.Crystal + transaction.Value.Gems));

        Assert.True(AiCommon.TradeAtMarketplace(kingdom, new Funds(Resource.Gold, 9000)));

        Assert.Equal(9000, kingdom.Treasury.Gold);
        Assert.Equal(3, kingdom.Treasury.Wood);
        Assert.Equal(3, kingdom.Treasury.Ore);
        Assert.Equal(7, (int)(kingdom.Treasury.Mercury + kingdom.Treasury.Sulfur +
                              kingdom.Treasury.Crystal + kingdom.Treasury.Gems));
    }

    [Fact]
    public void WithoutAMarketplaceNothingCanBeTraded()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        Funds before = kingdom.Treasury;

        Funds target = new(Resource.Gold, (uint)(before.Gold + 1));

        Assert.Null(AiCommon.CalculateMarketplaceTransaction(kingdom, target));
        Assert.False(AiCommon.TradeAtMarketplace(kingdom, target));
        Assert.Equal(before, kingdom.Treasury);
    }

    [Fact]
    public void BuildIfPossibleTradesForTheMissingWood()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        AddMarketplace(kingdom);

        CastleTown castle = NewCastle(5, PlayerColor.Blue);
        Funds cost = PaymentConditions.BuyBuilding(castle.Race, BuildingType.Castle);

        Assert.True(cost.Gold > 0);
        Assert.True(cost.Wood > 0);
        Assert.True(cost.Ore > 0);

        // Enough gold for the building and for buying the missing wood and ore at the only marketplace.
        int woodPrice = (int)ResourceTrading.GetTradeCost(1, Resource.Gold, Resource.Wood);
        int orePrice = (int)ResourceTrading.GetTradeCost(1, Resource.Gold, Resource.Ore);
        kingdom.Treasury = new Funds(
            ore: 0, wood: 0, mercury: 0, sulfur: 0, crystal: 0, gems: 0,
            gold: cost.Gold + (cost.Wood * woodPrice) + (cost.Ore * orePrice));

        Assert.True(AiCommon.BuildIfPossible(castle, BuildingType.Castle));
        Assert.True(castle.IsBuild(BuildingType.Castle));

        // The trade and the building spend the treasury to the last gold piece.
        Assert.Equal(0, kingdom.Treasury.Gold);
        Assert.Equal(0, kingdom.Treasury.Wood);
        Assert.Equal(0, kingdom.Treasury.Ore);
    }

    [Fact]
    public void BuildIfPossibleRefusesWhenTheMissingResourcesCannotBeBought()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        CastleTown castle = NewCastle(5, PlayerColor.Blue);
        Funds cost = PaymentConditions.BuyBuilding(castle.Race, BuildingType.Castle);

        // Every marketplace is absent, so the missing wood cannot be bought.
        kingdom.Treasury = new Funds(
            ore: 0, wood: 0, mercury: 0, sulfur: 0, crystal: 0, gems: 0, gold: cost.Gold);

        Assert.False(AiCommon.BuildIfPossible(castle, BuildingType.Castle));
        Assert.False(castle.IsBuild(BuildingType.Castle));
        Assert.Equal(cost.Gold, kingdom.Treasury.Gold);
    }

    [Fact]
    public void BuildIfEnoughFundsRespectsTheMultiplier()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        CastleTown castle = NewCastle(5, PlayerColor.Blue);
        Funds cost = PaymentConditions.BuyBuilding(castle.Race, BuildingType.Castle);

        // One extra copy of the price is all the kingdom has: a multiplier of three cannot be met.
        kingdom.Treasury = cost * 2u;

        Assert.False(AiCommon.BuildIfEnoughFunds(castle, BuildingType.Castle, 3));
        Assert.False(castle.IsBuild(BuildingType.Castle));

        // A multiplier of two is affordable: the extra copy of the price stays in the treasury.
        Assert.True(AiCommon.BuildIfEnoughFunds(castle, BuildingType.Castle, 2));
        Assert.True(castle.IsBuild(BuildingType.Castle));
        Assert.Equal(cost, kingdom.Treasury);
    }

    [Fact]
    public void BuildIfEnoughFundsRejectsTheOutOfRangeMultipliers()
    {
        Assert.False(AiCommon.BuildIfEnoughFunds(null!, default, 0));
        Assert.False(AiCommon.BuildIfEnoughFunds(null!, default, 100));
    }

    [Fact]
    public void OptimizeTroopsOrderPutsTheArchersInTheFrontSlot()
    {
        Settings.Get().GameDifficulty = GameDifficulty.Normal;

        GameArmy army = new();
        army.GetTroop(0).Set(new GameTroop(MonsterType.Champion, 5));
        army.GetTroop(1).Set(new GameTroop(MonsterType.Archer, 3));
        army.GetTroop(2).Set(new GameTroop(MonsterType.Peasant, 20));

        AiCommon.OptimizeTroopsOrder(army);

        // One shooter and two melee stacks: the shooter takes slot 0, then the melee stacks fill the
        // slots from the back in ascending speed order.
        Assert.Equal(MonsterType.Archer, army.GetTroop(0).Monster);
        Assert.Equal(MonsterType.Champion, army.GetTroop(1).Monster);
        Assert.Equal(MonsterType.Peasant, army.GetTroop(2).Monster);
        Assert.Equal(3u, army.OccupiedSlotCount);
    }

    [Fact]
    public void OptimizeTroopsOrderSplitsOnlyOnTheHardDifficulties()
    {
        GameArmy BuildArmy()
        {
            GameArmy army = new();
            army.GetTroop(0).Set(new GameTroop(MonsterType.Archer, 1));
            army.GetTroop(1).Set(new GameTroop(MonsterType.Peasant, 10));
            return army;
        }

        Settings.Get().GameDifficulty = GameDifficulty.Normal;
        GameArmy normal = BuildArmy();
        AiCommon.OptimizeTroopsOrder(normal);
        Assert.Equal(2u, normal.OccupiedSlotCount);

        Settings.Get().GameDifficulty = GameDifficulty.Hard;
        GameArmy hard = BuildArmy();
        AiCommon.OptimizeTroopsOrder(hard);

        // The weakest per head stack (peasants) is split into the three free slots, one creature each.
        Assert.Equal(5u, hard.OccupiedSlotCount);

        uint total = 0;
        for (int slot = 0; slot < GameArmy.MaximumTroopCount; ++slot)
        {
            total += hard.GetTroop(slot).Count;
        }

        Assert.Equal(11u, total);
    }

    [Fact]
    public void TransferSlowestTroopsToGarrisonKeepsTheFastestStack()
    {
        GameHero hero = new(GameHero.LordKilburn, MapRace.Knight);
        hero.Army.GetTroop(0).Set(new GameTroop(MonsterType.Peasant, 10));
        hero.Army.GetTroop(1).Set(new GameTroop(MonsterType.Champion, 5));
        hero.Army.GetTroop(2).Set(new GameTroop(MonsterType.Archer, 3));

        CastleTown castle = NewCastle(5, PlayerColor.Blue);
        AiCommon.TransferSlowestTroopsToGarrison(hero, castle);

        // Only the fastest stack stays with the hero; the two slowest ones join the garrison. The
        // reference empties a slot instead of compacting the army, so the champion keeps its slot.
        Assert.Equal(1u, hero.Army.OccupiedSlotCount);
        Assert.Equal(1u, CountTroops(hero.Army, MonsterType.Champion));
        Assert.Equal(0u, CountTroops(hero.Army, MonsterType.Peasant));
        Assert.Equal(0u, CountTroops(hero.Army, MonsterType.Archer));
        Assert.Equal(2u, castle.Army.OccupiedSlotCount);
    }

    [Fact]
    public void TransferSlowestTroopsToGarrisonWithoutACastleDoesNothing()
    {
        GameHero hero = new(GameHero.LordKilburn, MapRace.Knight);
        hero.Army.GetTroop(0).Set(new GameTroop(MonsterType.Peasant, 10));
        hero.Army.GetTroop(1).Set(new GameTroop(MonsterType.Champion, 5));

        AiCommon.TransferSlowestTroopsToGarrison(hero, null);

        // Without a garrison there is nowhere to move the troops to, so the army is untouched.
        Assert.Equal(2u, hero.Army.OccupiedSlotCount);
        Assert.Equal(1u, CountTroops(hero.Army, MonsterType.Peasant));
        Assert.Equal(1u, CountTroops(hero.Army, MonsterType.Champion));
    }

    [Fact]
    public void TheAlliesLearnWhatAHeroVisited()
    {
        World world = NewWorld(
            PlayerColor.Blue | PlayerColor.Red,
            PlayerColor.Blue | PlayerColor.Red,
            GameDifficulty.Hard);

        world.GetTile(5).SetMainObjectType(Mp2ObjectType.Sign);

        Players.Get(PlayerColor.Blue)!.Friends = PlayerColor.Blue | PlayerColor.Red;
        Players.Get(PlayerColor.Red)!.Friends = PlayerColor.Blue | PlayerColor.Red;

        AiCommon.ShareObjectVisitInfoWithAllies(world.GetKingdom(PlayerColor.Blue), 5);

        Assert.True(world.GetKingdom(PlayerColor.Red).IsVisited(5, Mp2ObjectType.Sign));
    }

    [Fact]
    public void AHeroDoesNotShareWithAHumanAlly()
    {
        World world = NewWorld(
            PlayerColor.Blue | PlayerColor.Red,
            PlayerColor.Blue | PlayerColor.Red,
            GameDifficulty.Hard);

        world.GetTile(5).SetMainObjectType(Mp2ObjectType.Sign);

        Players.Get(PlayerColor.Blue)!.Friends = PlayerColor.Blue | PlayerColor.Red;
        Players.Get(PlayerColor.Red)!.Friends = PlayerColor.Blue | PlayerColor.Red;
        Players.Get(PlayerColor.Red)!.Control = PlayerControl.Human;

        AiCommon.ShareObjectVisitInfoWithAllies(world.GetKingdom(PlayerColor.Blue), 5);

        Assert.False(world.GetKingdom(PlayerColor.Red).IsVisited(5, Mp2ObjectType.Sign));
    }

    [Fact]
    public void SharingIsOffOnTheEasyDifficulties()
    {
        World world = NewWorld(
            PlayerColor.Blue | PlayerColor.Red,
            PlayerColor.Blue | PlayerColor.Red,
            GameDifficulty.Normal);

        world.GetTile(5).SetMainObjectType(Mp2ObjectType.Sign);

        Players.Get(PlayerColor.Blue)!.Friends = PlayerColor.Blue | PlayerColor.Red;

        AiCommon.ShareObjectVisitInfoWithAllies(world.GetKingdom(PlayerColor.Blue), 5);

        Assert.False(world.GetKingdom(PlayerColor.Red).IsVisited(5, Mp2ObjectType.Sign));
    }

    [Fact]
    public void TheUltimateArtifactNeedsACompletedPuzzleAndAFreeBag()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        // An uninitialized map is water under the hood; the artifact needs dry, walkable land.
        world.GetTile(27).SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        world.UltimateArtifact.Set(27, ArtifactType.UltimateCrown, MapVersion.SuccessionWars);

        GameHero hero = new(GameHero.LordKilburn, MapRace.Knight);
        hero.SetColor(PlayerColor.Blue);

        // The puzzle is not finished yet: the position is still unknown.
        Assert.False(AiCommon.IsUltimateArtifactAvailableToHero(world.UltimateArtifact, hero));

        world.GetKingdom(PlayerColor.Blue).GetPuzzleMaps().Update(Puzzle.NumOfPuzzleTiles, Puzzle.NumOfPuzzleTiles);

        Assert.True(AiCommon.IsUltimateArtifactAvailableToHero(world.UltimateArtifact, hero));

        world.UltimateArtifact.MarkAsFound();

        Assert.False(AiCommon.IsUltimateArtifactAvailableToHero(world.UltimateArtifact, hero));
    }

    [Fact]
    public void OnlyActionObjectsWithSomethingToGiveAreValuable()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        const int tile = 27;

        // The plain value and the action value of the same object are both understood.
        Assert.True(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionMine, tile));
        Assert.True(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.Mine, tile));

        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.None, tile));
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.Event, tile));
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionEvent, tile));

        // Objects a hero cannot gain anything from.
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionBoat, tile));
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionSign, tile));
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionStoneLiths, tile));
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionWhirlpool, tile));

        // A pyramid is worth a visit only while it still holds something.
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionPyramid, tile));
    }

    [Fact]
    public void ObjectsThatPayOnlyOnceStopBeingValuableAfterTheVisit()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        const int tile = 27;

        Assert.True(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionMagellansMaps, tile));
        kingdom.SetVisited(tile, Mp2ObjectType.MagellansMaps);
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionMagellansMaps, tile));

        Assert.True(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionObelisk, tile));
        kingdom.SetVisited(tile, Mp2ObjectType.Obelisk);
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionObelisk, tile));

        // An observation tower that uncovers nothing is skipped.
        Assert.True(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionObservationTower, tile));
        world.ClearFog(tile, MapFog.ObservationTowerRevealDistance, PlayerColor.Blue);
        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionObservationTower, tile));
    }

    [Fact]
    public void AnEnemyHeroIsWorthFightingButAnAllyIsNot()
    {
        World world = NewWorld(PlayerColor.Blue | PlayerColor.Red, PlayerColor.Blue | PlayerColor.Red,
            GameDifficulty.Normal);

        world.Heroes.Init(MapVersion.SuccessionWars);

        GameHero hero = world.Heroes.Get(GameHero.Roland)!;
        hero.SetColor(PlayerColor.Red);
        world.GetTile(27).SetHero(world, hero);

        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        Assert.True(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionHero, 27));

        Players.Get(PlayerColor.Blue)!.Friends = PlayerColor.Blue | PlayerColor.Red;

        Assert.False(AiCommon.IsValuableAdventureMapObject(kingdom, Mp2ObjectType.ActionHero, 27));
    }

    /// <summary>Starts a fresh game world on a square of empty tiles.</summary>
    /// <param name="colors">The colours in play.</param>
    /// <param name="computerColors">The colours the computer plays.</param>
    /// <param name="difficulty">The difficulty written to the settings and the kingdoms.</param>
    /// <returns>The world, also installed as <see cref="World.Current"/>.</returns>
    private static World NewWorld(PlayerColor colors, PlayerColor computerColors, GameDifficulty difficulty)
    {
        Settings.Reset();
        Settings.Get().GameDifficulty = difficulty;
        Settings.Get().Players.Init(colors);

        World world = new();
        world.GenerateUninitializedMap(8);
        world.Kingdoms.Init(colors, difficulty, computerColors);
        World.Current = world;

        return world;
    }

    /// <summary>Counts the stacks of one monster in an army.</summary>
    /// <param name="army">The army to scan.</param>
    /// <param name="monster">The monster to look for.</param>
    /// <returns>The number of stacks, empty slots excluded.</returns>
    private static uint CountTroops(GameArmy army, MonsterType monster)
    {
        uint count = 0;

        for (int slot = 0; slot < GameArmy.MaximumTroopCount; ++slot)
        {
            if (army.GetTroop(slot).Monster == monster)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Adds a town with one marketplace to a kingdom.</summary>
    /// <param name="kingdom">The kingdom that owns it.</param>
    private static void AddMarketplace(GameKingdom kingdom)
    {
        CastleTown market = NewCastle(21, kingdom.Color);
        market.AddBuildings(BuildingType.Marketplace);
        kingdom.AddCastle(market);
    }

    /// <summary>Builds a tent of the given colour; it is not registered in any table.</summary>
    /// <param name="tileIndex">Index of the entrance tile.</param>
    /// <param name="color">The owner.</param>
    /// <returns>The town.</returns>
    private static CastleTown NewCastle(int tileIndex, PlayerColor color)
    {
        CastleTown castle = new(tileIndex, MapRace.Knight);
        castle.ChangeColor(color);
        return castle;
    }
}
