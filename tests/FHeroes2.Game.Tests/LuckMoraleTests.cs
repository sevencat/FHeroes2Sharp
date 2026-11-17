using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using Xunit;
using ArmyStack = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>luck.cpp</c>, <c>morale.cpp</c> and the four spiritual numbers of a hero and his army:
/// <c>Army::GetLuckModificator</c> / <c>Army::GetMoraleModificator</c> (<c>army.cpp:1206</c>, <c>:1226</c>)
/// and <c>Heroes::GetLuck</c> / <c>Heroes::GetMorale</c> (<c>heroes.cpp:1033</c>, <c>:1069</c>).
/// </summary>
public class LuckMoraleTests
{
    [Theory]
    [InlineData(-10, Luck.Cursed)]
    [InlineData(-3, Luck.Cursed)]
    [InlineData(-2, Luck.Awful)]
    [InlineData(-1, Luck.Bad)]
    [InlineData(0, Luck.Normal)]
    [InlineData(1, Luck.Good)]
    [InlineData(2, Luck.Great)]
    [InlineData(3, Luck.Irish)]
    [InlineData(10, Luck.Irish)]
    public void TheLuckIsClampedIntoItsScale(int luck, int expected)
    {
        Assert.Equal(expected, Luck.Normalize(luck));
    }

    [Theory]
    [InlineData(-10, Morale.Treason)]
    [InlineData(-3, Morale.Treason)]
    [InlineData(-2, Morale.Awful)]
    [InlineData(-1, Morale.Poor)]
    [InlineData(0, Morale.Normal)]
    [InlineData(1, Morale.Good)]
    [InlineData(2, Morale.Great)]
    [InlineData(3, Morale.Blood)]
    [InlineData(10, Morale.Blood)]
    public void TheMoraleIsClampedIntoItsScale(int morale, int expected)
    {
        Assert.Equal(expected, Morale.Normalize(morale));
    }

    private static ArmyStack ArmyOf(params (MonsterType Monster, uint Count)[] troops)
    {
        var army = new ArmyStack();

        foreach ((MonsterType monster, uint count) in troops)
        {
            army.JoinTroop(monster, count);
        }

        return army;
    }

    [Fact]
    public void AnArmyWithoutHeroesHasNoLuckModifier()
    {
        Assert.Equal(Luck.Normal, new ArmyStack().GetLuck());
        Assert.Equal(Luck.Normal, ArmyOf((MonsterType.Archer, 3)).GetLuck());
        Assert.Equal(Luck.Normal, ArmyOf((MonsterType.Archer, 3), (MonsterType.Boar, 1)).GetLuck());
    }

    [Fact]
    public void AnEmptyArmyHasNormalMorale()
    {
        Assert.Equal(Morale.Normal, new ArmyStack().GetMorale());
    }

    [Fact]
    public void OneRaceOfDifferentMonstersGivesPlusOne()
    {
        // Archer and swordsman are both knights, and the army is not five copies of one stack.
        Assert.Equal(Morale.Good, ArmyOf((MonsterType.Archer, 3), (MonsterType.Swordsman, 2)).GetMorale());
    }

    [Fact]
    public void FiveCopiesOfOneMonsterDoNotGetTheRaceBonus()
    {
        // The reference asks AllTroopsAreTheSame(), not "one race": an army of one kind of monster is too
        // uniform for the "all <race> troops" bonus.
        Assert.Equal(Morale.Normal, ArmyOf((MonsterType.Archer, 3), (MonsterType.Archer, 2)).GetMorale());
        Assert.Equal(Morale.Normal, ArmyOf((MonsterType.Archer, 3)).GetMorale());
    }

    [Fact]
    public void TwoRacesAreNeutral()
    {
        Assert.Equal(Morale.Normal, ArmyOf((MonsterType.Archer, 3), (MonsterType.Boar, 2)).GetMorale());
    }

    [Fact]
    public void EveryRacePastTheSecondCostsAPoint()
    {
        Assert.Equal(Morale.Poor, ArmyOf((MonsterType.Archer, 3), (MonsterType.Boar, 2), (MonsterType.Centaur, 2)).GetMorale());

        Assert.Equal(
            Morale.Awful,
            ArmyOf((MonsterType.Archer, 3), (MonsterType.Boar, 2), (MonsterType.Centaur, 2), (MonsterType.Goblin, 2)).GetMorale());
    }

    [Fact]
    public void UndeadInTheRanksCostOneMore()
    {
        // Skeleton and zombie are both necromancer, but undead armies never get the single-race bonus.
        Assert.Equal(Morale.Poor, ArmyOf((MonsterType.Skeleton, 3), (MonsterType.Zombie, 2)).GetMorale());

        // A living and an undead race: two races are neutral, the undead cost the point.
        Assert.Equal(Morale.Poor, ArmyOf((MonsterType.Archer, 3), (MonsterType.Skeleton, 2)).GetMorale());
    }

    [Fact]
    public void TheNeutralRaceCountsAsARace()
    {
        // MapRace.None goes into the set just like the six player races: an elemental next to a wizard
        // stack is two races, so the single-race bonus is gone.
        Assert.Equal(Morale.Normal, ArmyOf((MonsterType.AirElement, 2), (MonsterType.Boar, 2)).GetMorale());

        // The ghost shares that neutral race with the elemental, and being undead costs the point.
        Assert.Equal(Morale.Poor, ArmyOf((MonsterType.Ghost, 2), (MonsterType.AirElement, 2)).GetMorale());
    }

    [Fact]
    public void AllTroopsAreTheSameIsAlsoTrueOfAnEmptyArmy()
    {
        Assert.True(new ArmyStack().AllTroopsAreTheSame());
        Assert.True(ArmyOf((MonsterType.Archer, 3), (MonsterType.Archer, 2)).AllTroopsAreTheSame());
        Assert.False(ArmyOf((MonsterType.Archer, 3), (MonsterType.Swordsman, 2)).AllTroopsAreTheSame());
    }

    [Fact]
    public void AllTroopsAreUndeadNeedsAtLeastOneStackAndNoLivingOne()
    {
        Assert.False(new ArmyStack().AllTroopsAreUndead());
        Assert.True(ArmyOf((MonsterType.Skeleton, 3), (MonsterType.Zombie, 2)).AllTroopsAreUndead());
        Assert.False(ArmyOf((MonsterType.Skeleton, 3), (MonsterType.Archer, 2)).AllTroopsAreUndead());
    }

    [Fact]
    public void AHeroWithoutSkillsHasNoLuckAndNoMoraleBonus()
    {
        var hero = new Hero(Hero.Celia, MapRace.Necromancer);

        Assert.Equal(Luck.Normal, hero.GetLuck());
        Assert.Equal(Morale.Normal, hero.GetMorale());
    }

    [Theory]
    [InlineData(SkillLevel.Basic, 1)]
    [InlineData(SkillLevel.Advanced, 2)]
    [InlineData(SkillLevel.Expert, 3)]
    public void LeadershipRaisesTheMoraleOnePointPerLevel(SkillLevel level, int expected)
    {
        var hero = new Hero(Hero.Celia, MapRace.Necromancer);
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Leadership, level));

        Assert.Equal(expected, hero.GetMorale());
    }

    [Theory]
    [InlineData(SkillLevel.Basic, 1)]
    [InlineData(SkillLevel.Advanced, 2)]
    [InlineData(SkillLevel.Expert, 3)]
    public void TheLuckSkillRaisesTheLuckOnePointPerLevel(SkillLevel level, int expected)
    {
        var hero = new Hero(Hero.Celia, MapRace.Necromancer);
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Luck, level));

        Assert.Equal(expected, hero.GetLuck());
    }

    [Fact]
    public void TheArmOfTheMartyrCostsMoraleLikeUndeadInTheRanks()
    {
        var hero = new Hero(Hero.Celia, MapRace.Necromancer);
        var army = new ArmyStack { Commander = hero };
        army.JoinTroop(MonsterType.Archer, 3);
        army.JoinTroop(MonsterType.Boar, 2);

        // Two living races: neutral. The curse is the artifact's, not the undead's.
        Assert.Equal(Morale.Normal, army.GetMorale());

        hero.BagArtifacts.Push(ArtifactType.ArmMartyr);

        Assert.Equal(Morale.Poor, army.GetMorale());
    }

    [Fact]
    public void ACastleCommanderPassesTheTownsMoraleToItsGarrison()
    {
        CastleTown castle = new(0, MapRace.Knight);

        // An empty garrison has nothing to disagree about, so the tavern's point decides alone.
        castle.AddBuildings(BuildingType.Tavern);

        Assert.Equal(Morale.Good, castle.Army.GetMorale());
    }
}
