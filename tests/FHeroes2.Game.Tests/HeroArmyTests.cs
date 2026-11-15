using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the army and the movement points of a hero: <c>heroes.h:290</c>, <c>heroes.cpp:972</c>
/// (<c>Heroes::GetMaxMovePoints</c>), <c>heroes.cpp:1782</c> (<c>_updateMovementPoints</c>) and
/// <c>heroes_move.cpp:468</c> (<c>_applyMovementPenalty</c>).
/// </summary>
/// <remarks>
/// <para>
/// The daily allowance is one of the few pieces of the reference that is a pure function of the army and the
/// secondary skills, so it is pinned here without any world at all — except for the lighthouse bonus, which
/// is the one term that asks the map.
/// </para>
/// <para>
/// Not covered, because the systems are missing: the <c>LAND_MOBILITY</c>/<c>SEA_MOBILITY</c> artifact
/// bonuses and the 400-point Stables bonus.
/// </para>
/// </remarks>
public sealed class HeroArmyTests
{
    private const int MapSize = 5;

    private static Hero CreateHero() => new(Hero.LordKilburn, MapRace.Knight);

    [Fact]
    public void AHeroStartsWithAnEmptyArmyAndNoPoints()
    {
        Hero hero = CreateHero();

        Assert.False(hero.Army.IsValid);
        Assert.Equal(0u, hero.MovePoints);
        Assert.Equal(0u, hero.GetMaxMovePoints());
    }

    [Fact]
    public void TheArmyAndTheSkillsBelongToTheHero()
    {
        Hero hero = CreateHero();

        Assert.Same(hero.Army, hero.Army);
        Assert.Same(hero.SecondarySkills, hero.SecondarySkills);
        Assert.NotSame(CreateHero().Army, hero.Army);
    }

    [Theory]
    [InlineData(MonsterType.Peasant, 1000u)] // VerySlow
    [InlineData(MonsterType.Halfling, 1100u)] // Slow
    [InlineData(MonsterType.Goblin, 1200u)] // Average
    [InlineData(MonsterType.Paladin, 1300u)] // Fast
    [InlineData(MonsterType.Cavalry, 1400u)] // VeryFast
    [InlineData(MonsterType.Champion, 1500u)] // UltraFast
    public void TheLandBasePointsFollowTheSlowestStack(MonsterType monster, uint expected)
    {
        Hero hero = CreateHero();
        hero.Army.JoinTroop(monster, 1);

        Assert.Equal(expected, hero.GetMaxMovePoints());
    }

    [Fact]
    public void TheSlowestStackOfTheArmyDecides()
    {
        Hero hero = CreateHero();
        hero.Army.JoinTroop(MonsterType.Champion, 1);
        hero.Army.JoinTroop(MonsterType.Peasant, 1);

        Assert.Equal(1000u, hero.GetMaxMovePoints());
    }

    [Theory]
    [InlineData(SkillLevel.Basic, 1100u)]
    [InlineData(SkillLevel.Advanced, 1200u)]
    [InlineData(SkillLevel.Expert, 1300u)]
    public void LogisticsScalesTheLandPoints(SkillLevel level, uint expected)
    {
        Hero hero = CreateHero();
        hero.Army.JoinTroop(MonsterType.Peasant, 1);
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Logistics, level));

        Assert.Equal(expected, hero.GetMaxMovePoints());
    }

    [Fact]
    public void LogisticsOnAnEmptyArmyKeepsItAtZero()
    {
        Hero hero = CreateHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Logistics, SkillLevel.Expert));

        Assert.Equal(0u, hero.GetMaxMovePoints());
    }

    [Fact]
    public void TheWaterBaseIsAFlatFifteenHundred()
    {
        Hero hero = CreateHero();
        hero.Army.JoinTroop(MonsterType.Peasant, 1);

        // The ship does not care what the hero loaded aboard.
        Assert.Equal(1500u, hero.GetMaxMovePoints(onWater: true));
    }

    [Theory]
    [InlineData(SkillLevel.Basic, 2000u)]
    [InlineData(SkillLevel.Advanced, 2500u)]
    [InlineData(SkillLevel.Expert, 3000u)]
    public void NavigationScalesTheWaterPoints(SkillLevel level, uint expected)
    {
        Hero hero = CreateHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Navigation, level));

        Assert.Equal(expected, hero.GetMaxMovePoints(onWater: true));
    }

    [Fact]
    public void NavigationUsesTheExactThirds()
    {
        Hero hero = CreateHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Navigation, SkillLevel.Advanced));

        // 66% of 1500 is 990 by percentage, but _updateMovementPoints() multiplies by 5/3 instead: 2500.
        Assert.Equal(2500u, hero.GetMaxMovePoints(onWater: true));
    }

    [Fact]
    public void LogisticsDoesNotApplyOnWater()
    {
        Hero hero = CreateHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Logistics, SkillLevel.Expert));

        Assert.Equal(1500u, hero.GetMaxMovePoints(onWater: true));
    }

    [Fact]
    public void CapturedLighthousesAddFiveHundredEach()
    {
        World world = CreateLandWorld();
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Lighthouse);
        world.GetTile(8).SetMainObjectType(Mp2ObjectType.Lighthouse);
        world.CaptureObject(6, PlayerColor.Blue);
        world.CaptureObject(8, PlayerColor.Blue);

        Hero hero = CreateHero();
        hero.SetColor(PlayerColor.Blue);

        Assert.Equal(2500u, hero.GetMaxMovePoints(onWater: true, world));
    }

    [Fact]
    public void LighthousesOfAnotherColorDoNotCount()
    {
        World world = CreateLandWorld();
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Lighthouse);
        world.CaptureObject(6, PlayerColor.Green);

        Hero hero = CreateHero();
        hero.SetColor(PlayerColor.Blue);

        Assert.Equal(1500u, hero.GetMaxMovePoints(onWater: true, world));
    }

    [Fact]
    public void TheLighthouseBonusIsOnlyAddedOnWater()
    {
        World world = CreateLandWorld();
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Lighthouse);
        world.CaptureObject(6, PlayerColor.Blue);

        Hero hero = CreateHero();
        hero.SetColor(PlayerColor.Blue);
        hero.Army.JoinTroop(MonsterType.Peasant, 1);

        Assert.Equal(1000u, hero.GetMaxMovePoints(false, world));
    }

    [Fact]
    public void ThePointsCanBeSetAndIncreased()
    {
        Hero hero = CreateHero();

        hero.SetMovePoints(750);
        Assert.Equal(750u, hero.MovePoints);

        hero.IncreaseMovePoints(250);
        Assert.Equal(1000u, hero.MovePoints);
    }

    [Fact]
    public void IncreasingBeyondTheDailyMaximumIsAllowed()
    {
        Hero hero = CreateHero();
        hero.Army.JoinTroop(MonsterType.Peasant, 1);
        hero.SetMovePoints(hero.GetMaxMovePoints());

        // The Stables bonus pushes a hero past his maximum; the reference does not clamp either.
        hero.IncreaseMovePoints(400);

        Assert.Equal(1400u, hero.MovePoints);
    }

    [Fact]
    public void APenaltyIsSubtractedWhileItIsAffordable()
    {
        Hero hero = CreateHero();
        hero.SetMovePoints(100);

        hero.ApplyMovementPenalty(70);

        Assert.Equal(30u, hero.MovePoints);
    }

    [Fact]
    public void AnUnaffordablePenaltyZeroesThePoints()
    {
        Hero hero = CreateHero();
        hero.SetMovePoints(70);

        // "The last step": the pathfinder granted a step the hero cannot fully pay for, and the reference
        // zeroes the points instead of going negative.
        hero.ApplyMovementPenalty(200);

        Assert.Equal(0u, hero.MovePoints);
    }

    private static World CreateLandWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int i = 0; i < world.Size; ++i)
        {
            Tile tile = world.Tiles[i];
            tile.Index = i;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        world.Heroes.Init(MapVersion.SuccessionWars);

        return world;
    }
}
