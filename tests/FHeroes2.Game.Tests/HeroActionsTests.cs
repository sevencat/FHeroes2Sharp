using FHeroes2.Assets.Maps;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the first slice of <c>heroes_action.cpp</c>: the magic well, the artesian spring, the good-morale
/// family (buoy/oasis/watering hole/temple) and the good-luck family (fountain/faerie ring/idol/mermaid).
/// </summary>
public sealed class HeroActionsTests
{
    private static HeroActions ActionsOf(Kingdoms kingdoms, PlayerKingdom kingdom, int heroId = 1)
    {
        Hero hero = new(heroId, MapRace.Knight);
        kingdom.AddHero(hero);

        return new HeroActions(hero, kingdoms);
    }

    private static HeroActions BlueActions(out PlayerKingdom kingdom)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        return ActionsOf(kingdoms, kingdom);
    }

    [Fact]
    public void TheMagicWellFillsThePoolOnce()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;
        hero.SetSpellPoints(0);
        uint max = hero.GetMaxSpellPoints();

        Assert.True(actions.ActionToMagicWell(100));
        Assert.Equal(max, hero.SpellPoints);

        // The record is in and the pool is full: neither branch lets the well pay twice.
        hero.SetSpellPoints(0);
        Assert.False(actions.ActionToMagicWell(100));
        Assert.Equal(0u, hero.SpellPoints);
    }

    [Fact]
    public void TheMagicWellRefusesAHeroWhosePoolIsAlreadyFull()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;
        hero.SetSpellPoints(hero.GetMaxSpellPoints());

        Assert.False(actions.ActionToMagicWell(100));

        // Nothing was recorded, so the well is still worth coming back to later that day.
        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.MagicWell));
    }

    [Fact]
    public void TheMagicWellIsWorthAnotherDrinkTomorrow()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;
        hero.SetSpellPoints(0);
        Assert.True(actions.ActionToMagicWell(100));

        hero.SetSpellPoints(0);
        hero.ActionNewDay(2);

        Assert.True(actions.ActionToMagicWell(100));
    }

    [Fact]
    public void TheSpringFillsToTwiceTheMaximumForTheWholeKingdom()
    {
        HeroActions first = BlueActions(out PlayerKingdom kingdom);
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);
        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        HeroActions acting = ActionsOf(kingdoms, kingdom, heroId: 1);
        acting.Hero.SetSpellPoints(0);

        Assert.True(acting.ActionToArtesianSpring(200));
        Assert.Equal(acting.Hero.GetMaxSpellPoints() * 2, acting.Hero.SpellPoints);

        // A second hero of the same kingdom is turned away: the record is global.
        HeroActions other = ActionsOf(kingdoms, kingdom, heroId: 2);
        other.Hero.SetSpellPoints(0);

        Assert.False(other.ActionToArtesianSpring(200));
        Assert.Equal(0u, other.Hero.SpellPoints);
    }

    [Fact]
    public void TheSpringRecordsTheVisitEvenWhenItCannotPay()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        Hero hero = actions.Hero;

        // Already at twice the maximum: no points, but the kingdom has still drunk this week.
        hero.SetSpellPoints(hero.GetMaxSpellPoints() * 2);

        Assert.False(actions.ActionToArtesianSpring(200));
        Assert.True(kingdom.IsVisited(200, Mp2ObjectType.ArtesianSpring));
    }

    [Fact]
    public void TheOasisGivesMoraleAndMovementPoints()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;
        hero.SetMovePoints(0);

        Assert.True(actions.ActionToGoodMoraleObject(Mp2ObjectType.Oasis, 300));

        // The morale comes from the visit record, not from a stored counter.
        Assert.Equal(1, hero.GetMorale());
        Assert.Equal(800u, hero.MovePoints);

        // Coming back gives no second helping.
        Assert.False(actions.ActionToGoodMoraleObject(Mp2ObjectType.Oasis, 300));
        Assert.Equal(800u, hero.MovePoints);
        Assert.Equal(1, hero.GetMorale());
    }

    [Fact]
    public void TheWateringHoleIsWorthLessMovementThanTheOasis()
    {
        HeroActions actions = BlueActions(out _);
        actions.Hero.SetMovePoints(0);

        Assert.True(actions.ActionToGoodMoraleObject(Mp2ObjectType.WateringHole, 310));

        Assert.Equal(400u, actions.Hero.MovePoints);
        Assert.Equal(1, actions.Hero.GetMorale());
    }

    [Fact]
    public void TheTempleIsWorthTwoMoraleAndNoMovement()
    {
        HeroActions actions = BlueActions(out _);
        actions.Hero.SetMovePoints(0);

        Assert.True(actions.ActionToGoodMoraleObject(Mp2ObjectType.Temple, 320));

        Assert.Equal(2, actions.Hero.GetMorale());
        Assert.Equal(0u, actions.Hero.MovePoints);
    }

    [Fact]
    public void ThreeTemplesAreStillOnlyTwoMorale()
    {
        HeroActions actions = BlueActions(out _);

        actions.ActionToGoodMoraleObject(Mp2ObjectType.Temple, 320);
        actions.ActionToGoodMoraleObject(Mp2ObjectType.Temple, 321);

        Assert.Equal(2, actions.Hero.GetMorale());
    }

    [Fact]
    public void TheFountainGivesLuckAndNothingElse()
    {
        HeroActions actions = BlueActions(out _);
        actions.Hero.SetMovePoints(0);

        Assert.True(actions.ActionToGoodLuckObject(Mp2ObjectType.Fountain, 400));

        Assert.Equal(1, actions.Hero.GetLuck());
        Assert.Equal(0, actions.Hero.GetMorale());
        Assert.Equal(0u, actions.Hero.MovePoints);

        Assert.False(actions.ActionToGoodLuckObject(Mp2ObjectType.Fountain, 400));
    }

    [Fact]
    public void TheLuckLastsOnlyUntilTheNextBattle()
    {
        HeroActions actions = BlueActions(out _);
        actions.ActionToGoodLuckObject(Mp2ObjectType.Idol, 410);
        Assert.Equal(1, actions.Hero.GetLuck());

        actions.Hero.ActionAfterBattle();

        // The record is gone, so the recomputed luck is back to normal without any "undo" call.
        Assert.Equal(0, actions.Hero.GetLuck());
        Assert.True(actions.ActionToGoodLuckObject(Mp2ObjectType.Idol, 410));
    }

    [Fact]
    public void AnObjectOfTheOtherFamilyDoesNothing()
    {
        HeroActions actions = BlueActions(out _);

        Assert.False(actions.ActionToGoodLuckObject(Mp2ObjectType.Oasis, 500));
        Assert.False(actions.ActionToGoodMoraleObject(Mp2ObjectType.Fountain, 500));

        Assert.Empty(actions.Hero.VisitedObjects);
        Assert.Equal(0, actions.Hero.GetLuck());
        Assert.Equal(0, actions.Hero.GetMorale());
    }

    [Fact]
    public void TheFamiliesAreTheOnesTheReferenceLists()
    {
        Assert.True(HeroActions.IsGoodMoraleObject(Mp2ObjectType.Buoy));
        Assert.True(HeroActions.IsGoodMoraleObject(Mp2ObjectType.Oasis));
        Assert.True(HeroActions.IsGoodMoraleObject(Mp2ObjectType.WateringHole));
        Assert.True(HeroActions.IsGoodMoraleObject(Mp2ObjectType.Temple));
        Assert.False(HeroActions.IsGoodMoraleObject(Mp2ObjectType.Fountain));

        Assert.True(HeroActions.IsGoodLuckObject(Mp2ObjectType.Fountain));
        Assert.True(HeroActions.IsGoodLuckObject(Mp2ObjectType.FaerieRing));
        Assert.True(HeroActions.IsGoodLuckObject(Mp2ObjectType.Idol));
        Assert.True(HeroActions.IsGoodLuckObject(Mp2ObjectType.Mermaid));
        Assert.False(HeroActions.IsGoodLuckObject(Mp2ObjectType.Temple));
    }

    [Fact]
    public void TheFreeArtifactGoesIntoTheBagAndLeavesTheTile()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        World world = ArtifactWorld(ArtifactType.UltimateSword, ArtifactCaptureCondition.NoConditions);

        Assert.Equal(ArtifactPickupOutcome.Picked, actions.ActionToArtifact(0, world));
        Assert.True(actions.Hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
        Assert.All(world.GetTile(0).Metadata, lane => Assert.Equal(0u, lane));
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.Artifact));

        Assert.Equal(ArtifactPickupOutcome.None, actions.ActionToArtifact(0, world));
        Assert.Single(actions.Hero.BagArtifacts.Slots, slot => slot == ArtifactType.UltimateSword);
    }

    [Fact]
    public void AFourteenSlotBagRefusesTheArtifactButKeepsItOnTheTile()
    {
        HeroActions actions = BlueActions(out _);

        for (int id = 2; id < 2 + ArtifactBag.MaxCapacity; ++id)
        {
            Assert.True(actions.Hero.BagArtifacts.Push((ArtifactType)id));
        }

        Assert.True(actions.Hero.BagArtifacts.IsFull);

        World world = ArtifactWorld(ArtifactType.UltimateSword, ArtifactCaptureCondition.NoConditions);

        Assert.Equal(ArtifactPickupOutcome.BagFull, actions.ActionToArtifact(0, world));
        Assert.Equal((uint)ArtifactType.UltimateSword, world.GetTile(0).Metadata[0]);
    }

    [Fact]
    public void TheSecondSpellBookIsRefused()
    {
        HeroActions actions = BlueActions(out _);
        Assert.True(actions.Hero.BagArtifacts.Push(ArtifactType.MagicBook));

        World world = ArtifactWorld(ArtifactType.MagicBook, ArtifactCaptureCondition.NoConditions);

        Assert.Equal(ArtifactPickupOutcome.AlreadyHasSpellBook, actions.ActionToArtifact(0, world));
        Assert.Equal((uint)ArtifactType.MagicBook, world.GetTile(0).Metadata[0]);
    }

    [Fact]
    public void AGuardedArtifactWaitsForThePayment()
    {
        HeroActions actions = BlueActions(out _);
        World world = ArtifactWorld(ArtifactType.UltimateSword, ArtifactCaptureCondition.Pay2000Gold);

        Assert.Equal(ArtifactPickupOutcome.NeedsPayment, actions.ActionToArtifact(0, world));
        Assert.Equal((uint)ArtifactType.UltimateSword, world.GetTile(0).Metadata[0]);
        Assert.False(actions.Hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
    }

    [Fact]
    public void TheSkillGuardIsNotClaimedToWork()
    {
        HeroActions actions = BlueActions(out _);
        World world = ArtifactWorld(ArtifactType.UltimateSword, ArtifactCaptureCondition.HaveWisdomSkill);

        Assert.Equal(ArtifactPickupOutcome.NotSupported, actions.ActionToArtifact(0, world));
        Assert.Equal((uint)ArtifactType.UltimateSword, world.GetTile(0).Metadata[0]);
    }

    private static World ArtifactWorld(ArtifactType artifact, ArtifactCaptureCondition condition)
    {
        var world = new World();
        world.GenerateUninitializedMap(2);

        Tile tile = world.GetTile(0);
        tile.SetMainObjectType(Mp2ObjectType.Artifact);
        tile.Metadata[0] = (uint)artifact;
        tile.Metadata[2] = (uint)condition;

        return world;
    }

    [Fact]
    public void ABarrierOpensOnlyWithTheMatchingTent()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        var world = new World();
        world.GenerateUninitializedMap(2);
        world.GetTile(0).SetMainObjectType(Mp2ObjectType.Barrier);
        world.GetTile(0).SetBarrierColorOnTile(world, 1);

        Assert.False(actions.ActionToBarrier(0, world));

        kingdom.MarkTravellerTentVisited(0);
        Assert.False(actions.ActionToBarrier(0, world));

        kingdom.MarkTravellerTentVisited(1);
        Assert.True(actions.ActionToBarrier(0, world));
    }

    [Fact]
    public void TheArenaTrainsOnePrimarySkillOncePerGame()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;
        int attack = hero.GetAttack();
        int knowledge = hero.GetKnowledge();

        Assert.True(actions.ActionToArena(800, PrimarySkill.Attack));
        Assert.Equal(attack + 1, hero.GetAttack());
        Assert.Equal(knowledge, hero.GetKnowledge());

        // The guards turn the hero away the second time, whatever skill is offered.
        Assert.False(actions.ActionToArena(800, PrimarySkill.Knowledge));
        Assert.Equal(attack + 1, hero.GetAttack());
        Assert.Equal(knowledge, hero.GetKnowledge());
    }

    [Fact]
    public void TheBlackCatLiftsMoraleAndCursesLuckOnce()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;

        Assert.True(actions.ActionToBlackCat(600));
        Assert.Equal(Morale.Blood, hero.GetMorale());
        Assert.Equal(Luck.Awful, hero.GetLuck());

        // A second visit finds the record and changes nothing (heroes_action.cpp:3706).
        Assert.False(actions.ActionToBlackCat(600));
        Assert.Equal(Morale.Blood, hero.GetMorale());
        Assert.Equal(Luck.Awful, hero.GetLuck());
    }

    [Fact]
    public void TheMillPaysTheTileAndEmptiesIt()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        World world = MillWorld(Mp2ObjectType.Windmill, Resource.Gold, 500);
        int before = kingdom.Treasury.Gold;

        Funds funds = actions.ActionToObjectResource(Mp2ObjectType.Windmill, 0, world);

        Assert.Equal(500, funds.Gold);
        Assert.Equal(before + 500, kingdom.Treasury.Gold);
        Assert.All(world.GetTile(0).Metadata, lane => Assert.Equal(0u, lane));

        // The tile is empty now, so the next visit pays nothing — the reference tells the keeper's apology
        // instead of refilling (the weekly refill lives in Maps::updateObjectInfoTile, not here).
        Funds second = actions.ActionToObjectResource(Mp2ObjectType.Windmill, 0, world);

        Assert.Equal(0u, second.GetValidItemsCount());
        Assert.Equal(before + 500, kingdom.Treasury.Gold);
    }

    [Fact]
    public void TheEmptyWaterWheelStillGetsRecordedInTheKingdom()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        World world = MillWorld(Mp2ObjectType.WaterWheel, Resource.Gold, 0);

        Funds funds = actions.ActionToObjectResource(Mp2ObjectType.WaterWheel, 0, world);

        Assert.Equal(0u, funds.GetValidItemsCount());

        // "No gold available" still counts as a visit: the reference writes the visit outside the if.
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.WaterWheel));
    }

    [Fact]
    public void TheResourceFamilyIsTheFourObjectsOfTheReference()
    {
        Assert.True(HeroActions.IsResourceObject(Mp2ObjectType.Windmill));
        Assert.True(HeroActions.IsResourceObject(Mp2ObjectType.WaterWheel));
        Assert.True(HeroActions.IsResourceObject(Mp2ObjectType.LeanTo));
        Assert.True(HeroActions.IsResourceObject(Mp2ObjectType.MagicGarden));
        Assert.False(HeroActions.IsResourceObject(Mp2ObjectType.MagicWell));

        // An object outside the family leaves the tile alone.
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        World world = MillWorld(Mp2ObjectType.MagicWell, Resource.Gold, 500);
        int before = kingdom.Treasury.Gold;

        Funds funds = actions.ActionToObjectResource(Mp2ObjectType.MagicWell, 0, world);

        Assert.Equal(0u, funds.GetValidItemsCount());
        Assert.Equal(before, kingdom.Treasury.Gold);
        Assert.Equal(500u, world.GetTile(0).Metadata[1]);
        Assert.False(kingdom.IsVisited(0, Mp2ObjectType.MagicWell));
    }

    [Fact]
    public void TheStablesGiveAFreshHorseOnceAWeek()
    {
        HeroActions actions = BlueActions(out _);
        Hero hero = actions.Hero;
        hero.SetMovePoints(0);

        Assert.True(actions.ActionToStables(700));
        Assert.Equal(400u, hero.MovePoints);

        Assert.False(actions.ActionToStables(700));
        Assert.Equal(400u, hero.MovePoints);

        // The stables are a weekly object, so a new week makes the horse fresh again.
        hero.ActionNewWeek();

        Assert.True(actions.ActionToStables(700));
        Assert.Equal(800u, hero.MovePoints);
    }

    [Fact]
    public void TheTentUnlocksOnlyItsOwnColor()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        var world = new World();
        world.GenerateUninitializedMap(2);
        world.GetTile(0).SetMainObjectType(Mp2ObjectType.TravellerTent);
        world.GetTile(0).SetBarrierColorOnTile(world, 3);

        Assert.Equal(3, actions.ActionToTravellersTent(0, world));
        Assert.True(kingdom.IsTravellerTentVisited(3));
        Assert.False(kingdom.IsTravellerTentVisited(2));

        // Tents never enter the visit book: the unlock is permanent.
        Assert.Empty(kingdom.VisitedObjects);
    }

    [Fact]
    public void TheHeroRevealsAPuzzlePieceOnlyTheFirstTime()
    {
        HeroActions actions = BlueActions(out PlayerKingdom kingdom);
        var world = new World();
        world.GenerateUninitializedMap(3);
        world.GetTile(4).SetMainObjectType(Mp2ObjectType.Obelisk);

        Assert.True(actions.ActionToObelisk(4, world));
        Assert.True(kingdom.GetPuzzleMaps().Count > 0);
        Assert.True(kingdom.IsVisited(4, Mp2ObjectType.Obelisk));

        Assert.False(actions.ActionToObelisk(4, world));
    }

    private static World MillWorld(Mp2ObjectType objectType, int resourceType, uint amount)
    {
        var world = new World();
        world.GenerateUninitializedMap(2);
        world.GetTile(0).SetMainObjectType(objectType);
        world.GetTile(0).SetResourceOnTile(resourceType, amount);

        return world;
    }
}
