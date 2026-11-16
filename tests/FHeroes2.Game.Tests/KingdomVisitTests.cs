using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the kingdom's visit book and its day/week ticks: <c>visit_object</c>, the three lifetimes
/// (<c>visit.h</c>, <c>visit.cpp</c>) and <c>kingdom.cpp:197-290</c>.
/// </summary>
public sealed class KingdomVisitTests
{
    private static PlayerKingdom KingdomInPlay(PlayerColor color = PlayerColor.Blue, bool isAiControlled = false)
    {
        PlayerKingdom kingdom = new();
        kingdom.Init(color, GameDifficulty.Normal, isAiControlled);
        return kingdom;
    }

    /// <summary>
    /// A kingdom that is <b>not</b> lost: one hero is enough (<c>Kingdom::isLoss()</c> wants both rosters empty).
    /// </summary>
    /// <returns>The kingdom, with a single hero in it.</returns>
    private static PlayerKingdom KingdomNotLost()
    {
        PlayerKingdom kingdom = KingdomInPlay();
        kingdom.AddHero(new Hero(1, MapRace.Knight));
        return kingdom;
    }

    [Fact]
    public void TheNewestVisitComesFirstAndIsNeverTwice()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        kingdom.SetVisited(10, Mp2ObjectType.Obelisk);
        kingdom.SetVisited(20, Mp2ObjectType.Windmill);

        Assert.Equal(2, kingdom.VisitedObjects.Count);
        Assert.Equal(new IndexObject(20, Mp2ObjectType.Windmill), kingdom.VisitedObjects[0]);

        // Visiting the same tile for the same object again moves it to the front instead of duplicating it.
        kingdom.SetVisited(10, Mp2ObjectType.Obelisk);

        Assert.Equal(2, kingdom.VisitedObjects.Count);
        Assert.Equal(new IndexObject(10, Mp2ObjectType.Obelisk), kingdom.VisitedObjects[0]);
    }

    [Fact]
    public void EveryVisitIsAboutATileAndAnObject()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        kingdom.SetVisited(33, Mp2ObjectType.Windmill);

        Assert.True(kingdom.IsVisited(33, Mp2ObjectType.Windmill));
        Assert.False(kingdom.IsVisited(33, Mp2ObjectType.WaterWheel));
        Assert.False(kingdom.IsVisited(34, Mp2ObjectType.Windmill));

        Assert.True(kingdom.IsVisited(Mp2ObjectType.Windmill));
        Assert.False(kingdom.IsVisited(Mp2ObjectType.Obelisk));
    }

    [Fact]
    public void AnEmptyObjectIsNotAVisit()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        kingdom.SetVisited(7, Mp2ObjectType.None);

        Assert.Empty(kingdom.VisitedObjects);
        Assert.False(kingdom.IsVisited(7, Mp2ObjectType.None));
    }

    [Fact]
    public void CountingTheObelisksIsWhatThePuzzleAsksFor()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        kingdom.SetVisited(1, Mp2ObjectType.Obelisk);
        kingdom.SetVisited(2, Mp2ObjectType.Obelisk);
        kingdom.SetVisited(3, Mp2ObjectType.Obelisk);
        kingdom.SetVisited(4, Mp2ObjectType.Windmill);

        Assert.Equal(3, kingdom.CountVisitedObjects(Mp2ObjectType.Obelisk));
        Assert.Equal(1, kingdom.CountVisitedObjects(Mp2ObjectType.Windmill));
        Assert.Equal(0, kingdom.CountVisitedObjects(Mp2ObjectType.MagicWell));
    }

    [Fact]
    public void TheDayForgetsTheDailyObjectsAndNothingElse()
    {
        PlayerKingdom kingdom = KingdomInPlay();
        kingdom.SetVisited(1, Mp2ObjectType.MagicWell);
        kingdom.SetVisited(2, Mp2ObjectType.Windmill);
        kingdom.SetVisited(3, Mp2ObjectType.Obelisk);

        kingdom.ActionNewDay(2);

        // The magic well is visitable again tomorrow; the windmill waits for the week; the obelisk is spent.
        Assert.False(kingdom.IsVisited(1, Mp2ObjectType.MagicWell));
        Assert.True(kingdom.IsVisited(2, Mp2ObjectType.Windmill));
        Assert.True(kingdom.IsVisited(3, Mp2ObjectType.Obelisk));
    }

    [Fact]
    public void TheWeekForgetsTheWeeklyObjectsAndNothingElse()
    {
        PlayerKingdom kingdom = KingdomInPlay();
        kingdom.SetVisited(1, Mp2ObjectType.MagicWell);
        kingdom.SetVisited(2, Mp2ObjectType.Windmill);
        kingdom.SetVisited(3, Mp2ObjectType.Obelisk);

        kingdom.ActionNewWeek(2);

        Assert.True(kingdom.IsVisited(1, Mp2ObjectType.MagicWell));
        Assert.False(kingdom.IsVisited(2, Mp2ObjectType.Windmill));
        Assert.True(kingdom.IsVisited(3, Mp2ObjectType.Obelisk));
    }

    [Fact]
    public void ADayTicksOnlyTheKingdomInPlay()
    {
        // The reference clears the day-life visits even for a kingdom that is out of the game (kingdom.cpp:198),
        // but it does not touch the lost-town countdown or the spell book of one.
        PlayerKingdom dead = new();
        dead.SetVisited(1, Mp2ObjectType.MagicWell);

        dead.ActionNewDay(2);

        Assert.Empty(dead.VisitedObjects);
        Assert.Equal(PlayerKingdom.GameOverLostDays + 1, dead.LostTownDays);
    }

    [Fact]
    public void TheLostTownCountdownRunsOutAfterSevenDays()
    {
        PlayerKingdom kingdom = KingdomNotLost();
        Assert.Equal(PlayerKingdom.GameOverLostDays + 1, kingdom.LostTownDays);

        // The first day is not counted: the town of the map is not settled yet on day one.
        kingdom.ActionNewDay(1);
        Assert.Equal(PlayerKingdom.GameOverLostDays + 1, kingdom.LostTownDays);

        // One decrement per day from day two: GameOverLostDays + 1 of them take the counter to zero.
        for (int i = 0; i < PlayerKingdom.GameOverLostDays + 1; ++i)
        {
            kingdom.ActionNewDay((uint)(i + 2));
        }

        Assert.Equal(0, kingdom.LostTownDays);
    }

    [Fact]
    public void RunningOutOfTimeHandsOverToTheLossFlow()
    {
        PlayerKingdom kingdom = KingdomNotLost();
        int calls = 0;

        for (uint day = 1; day <= PlayerKingdom.GameOverLostDays + 2; ++day)
        {
            kingdom.ActionNewDay(day, () => ++calls);
        }

        // The reference calls LossPostActions once, when the countdown hits zero, and returns.
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TheDayClearsTheIdentifyHeroModeAndTheCrystalBall()
    {
        PlayerKingdom kingdom = KingdomNotLost();
        kingdom.SetModes(KingdomModes.IdentifyHero | KingdomModes.KingdomOverviewCastleSelection);
        kingdom.AddMonsterUnderVision(77);
        Assert.True(kingdom.IsMonsterUnderVisionSpell(77));

        kingdom.ActionNewDay(2);

        // Only the identify bit lives for a day; the overview selection belongs to the interface.
        Assert.Equal(KingdomModes.KingdomOverviewCastleSelection, kingdom.Modes);
        Assert.False(kingdom.IsMonsterUnderVisionSpell(77));
    }

    [Fact]
    public void TheDebugGiftIsPaidFromTheSecondWeekToAHumanOnly()
    {
        PlayerKingdom human = KingdomInPlay(isAiControlled: false);
        Funds before = human.Treasury;

        // Week one is skipped, and a release build has no gift at all.
        human.ActionNewWeek(1, isDebugMode: true);
        Assert.Equal(before, human.Treasury);

        human.ActionNewWeek(2, isDebugMode: false);
        Assert.Equal(before, human.Treasury);

        human.ActionNewWeek(2, isDebugMode: true);
        Assert.Equal(before.Gold + 5000, human.Treasury.Gold);
        Assert.Equal(before.Wood + 20, human.Treasury.Wood);

        PlayerKingdom computer = KingdomInPlay(isAiControlled: true);
        Funds computerBefore = computer.Treasury;
        computer.ActionNewWeek(2, isDebugMode: true);
        Assert.Equal(computerBefore, computer.Treasury);
    }

    [Fact]
    public void TheTravellerTentsAreAForeverUnlockNotAVisit()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        Assert.False(kingdom.IsTravellerTentVisited((int)PlayerColor.Red));

        kingdom.MarkTravellerTentVisited((int)PlayerColor.Red);

        Assert.True(kingdom.IsTravellerTentVisited((int)PlayerColor.Red));
        Assert.False(kingdom.IsTravellerTentVisited((int)PlayerColor.Blue));

        // Neither the day nor the week takes it back.
        kingdom.ActionNewDay(2);
        kingdom.ActionNewWeek(2);

        Assert.True(kingdom.IsTravellerTentVisited((int)PlayerColor.Red));
    }

    [Fact]
    public void ClearingTheKingdomForgetsTheVisitsTheModesAndTheTents()
    {
        PlayerKingdom kingdom = KingdomInPlay();
        kingdom.SetVisited(5, Mp2ObjectType.Obelisk);
        kingdom.SetModes(KingdomModes.IdentifyHero);
        kingdom.MarkTravellerTentVisited((int)PlayerColor.Red);
        kingdom.AddMonsterUnderVision(9);

        kingdom.Clear();

        Assert.Empty(kingdom.VisitedObjects);
        Assert.Equal(KingdomModes.None, kingdom.Modes);
        Assert.False(kingdom.IsTravellerTentVisited((int)PlayerColor.Red));
        Assert.False(kingdom.IsMonsterUnderVisionSpell(9));
        Assert.False(kingdom.IsPlay);
    }

    [Fact]
    public void TheFirstObeliskRevealsOneTileOfFortyEight()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        Assert.True(kingdom.ActionObelisk(100, Puzzle.NumOfPuzzleTiles));

        Assert.Equal(1, kingdom.GetPuzzleMaps().Count);
        Assert.True(kingdom.IsVisited(100, Mp2ObjectType.Obelisk));
    }

    [Fact]
    public void ComingBackToTheSameObeliskRevealsNothing()
    {
        PlayerKingdom kingdom = KingdomInPlay();
        kingdom.ActionObelisk(100, Puzzle.NumOfPuzzleTiles);

        Assert.False(kingdom.ActionObelisk(100, Puzzle.NumOfPuzzleTiles));

        Assert.Equal(1, kingdom.GetPuzzleMaps().Count);
        Assert.Equal(1, kingdom.CountVisitedObjects(Mp2ObjectType.Obelisk));
    }

    [Fact]
    public void EveryObeliskOfTheMapRevealsTheWholePicture()
    {
        PlayerKingdom kingdom = KingdomInPlay();

        for (int obelisk = 0; obelisk < Puzzle.NumOfPuzzleTiles; ++obelisk)
        {
            Assert.True(kingdom.ActionObelisk(obelisk, Puzzle.NumOfPuzzleTiles));
        }

        Assert.Equal(Puzzle.NumOfPuzzleTiles, kingdom.GetPuzzleMaps().Count);
    }

    [Fact]
    public void TheSevenSlotsAllTick()
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue | PlayerColor.Red, GameDifficulty.Normal, PlayerColor.Red);

        PlayerKingdom blue = kingdoms.GetKingdom(PlayerColor.Blue);
        PlayerKingdom red = kingdoms.GetKingdom(PlayerColor.Red);
        blue.SetVisited(1, Mp2ObjectType.MagicWell);
        red.SetVisited(2, Mp2ObjectType.MagicWell);

        kingdoms.NewDay(2);

        Assert.Empty(blue.VisitedObjects);
        Assert.Empty(red.VisitedObjects);
        Assert.True(blue.IsControlHuman);
        Assert.False(red.IsControlHuman);
    }
}
