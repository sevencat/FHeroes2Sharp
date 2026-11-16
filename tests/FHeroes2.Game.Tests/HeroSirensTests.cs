using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the sirens, <c>ActionToSirens</c> (<c>heroes_action.cpp:3366</c>) and the army side of it,
/// <c>Army::ActionToSirens</c> (<c>army.cpp:1542</c>).
/// </summary>
public sealed class HeroSirensTests
{
    private const int SirensTile = 0;

    private static HeroActions SirensActions(out Hero hero, out World world, params (MonsterType Monster, uint Count)[] stacks)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        PlayerKingdom kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        hero = new Hero(1, MapRace.Knight);
        kingdom.AddHero(hero);

        foreach ((MonsterType monster, uint count) in stacks)
        {
            Assert.True(hero.Army.JoinTroop(monster, count));
        }

        world = new World();
        world.GenerateUninitializedMap(2);

        return new HeroActions(hero, kingdoms);
    }

    [Fact]
    public void TheSirensDrownThirtyPercentOfAStack()
    {
        HeroActions actions = SirensActions(out Hero hero, out World world, (MonsterType.Archer, 10));
        uint before = hero.Experience;

        SirensReward reward = actions.ActionToSirens(SirensTile, world);

        Assert.Equal(7u, hero.Army[0].Count);
        Assert.Equal(30u, reward.Experience);
        Assert.False(reward.AlreadyVisited);
        Assert.False(reward.IsEmpty);
        Assert.Equal(before + 30, hero.Experience);
    }

    [Fact]
    public void ASmallStackStillLosesOneCreature()
    {
        // Three peasants: 30% floors to zero, and the reference drowns one anyway.
        HeroActions actions = SirensActions(out Hero hero, out World world, (MonsterType.Peasant, 3));

        SirensReward reward = actions.ActionToSirens(SirensTile, world);

        Assert.Equal(2u, hero.Army[0].Count);
        Assert.Equal(1u, reward.Experience);
    }

    [Fact]
    public void AStackOfOneIsLeftAlone()
    {
        HeroActions actions = SirensActions(out Hero hero, out World world, (MonsterType.Archer, 1));

        SirensReward reward = actions.ActionToSirens(SirensTile, world);

        Assert.Equal(1u, hero.Army[0].Count);
        Assert.Equal(0u, reward.Experience);
        Assert.False(reward.AlreadyVisited);
        Assert.True(reward.IsEmpty);
    }

    [Fact]
    public void TheExperienceIsPaidInHitPointsOfTheDrowned()
    {
        // 10 archers lose 3 (10 HP each) and 5 peasants lose 1 (1 HP): 31 in total, not a gold value.
        HeroActions actions = SirensActions(
            out Hero hero,
            out World world,
            (MonsterType.Archer, 10),
            (MonsterType.Peasant, 5));

        SirensReward reward = actions.ActionToSirens(SirensTile, world);

        Assert.Equal(7u, hero.Army[0].Count);
        Assert.Equal(4u, hero.Army[1].Count);
        Assert.Equal(31u, reward.Experience);
    }

    [Fact]
    public void AnEmptyArmyLosesNobody()
    {
        HeroActions actions = SirensActions(out Hero hero, out World world);

        SirensReward reward = actions.ActionToSirens(SirensTile, world);

        Assert.True(reward.IsEmpty);
        Assert.False(reward.AlreadyVisited);
        Assert.Equal(0u, hero.Experience);
    }

    [Fact]
    public void ASmallArmyIsStillRecordedAsAVisit()
    {
        HeroActions actions = SirensActions(out Hero hero, out World world, (MonsterType.Archer, 1));

        actions.ActionToSirens(SirensTile, world);

        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Sirens));
    }

    [Fact]
    public void TheSecondVisitLeavesTheArmyAlone()
    {
        HeroActions actions = SirensActions(out Hero hero, out World world, (MonsterType.Archer, 10));

        SirensReward first = actions.ActionToSirens(SirensTile, world);
        uint afterFirst = hero.Experience;

        SirensReward second = actions.ActionToSirens(SirensTile, world);

        Assert.False(first.AlreadyVisited);
        Assert.True(second.AlreadyVisited);
        Assert.Equal(0u, second.Experience);
        Assert.Equal(7u, hero.Army[0].Count);
        Assert.Equal(afterFirst, hero.Experience);
    }
}
