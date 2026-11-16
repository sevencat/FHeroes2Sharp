using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>heroes.cpp</c>'s experience and level-up pair (<c>IncreaseExperience</c> / <c>_levelUp</c>) and the
/// two action branches that pay experience out: the gazebo and the tree of knowledge.
/// </summary>
public sealed class HeroExperienceTests
{
    private const uint MapSeed = 20261116;

    /// <summary>Blue kingdom, a knight on tile 0 and a two by two uninitialized map.</summary>
    private static HeroActions BlueActions(out PlayerKingdom kingdom, out World world, int heroId = 1)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        kingdom.Treasury.Gold = 0;

        Hero hero = new(heroId, MapRace.Knight);
        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);

        return new HeroActions(hero, kingdoms);
    }

    /// <summary>The same, plus a tree of knowledge on tile 0 asking <paramref name="price"/> gold.</summary>
    private static HeroActions TreeActions(out PlayerKingdom kingdom, out World world, uint gold, uint price)
    {
        HeroActions actions = BlueActions(out kingdom, out world);

        kingdom.AddFundsResource(new Funds(Resource.Gold, gold));

        Tile tile = world.GetTile(0);
        tile.SetMainObjectType(Mp2ObjectType.TreeOfKnowledge);
        tile.Metadata[0] = (uint)Resource.Gold;
        tile.Metadata[1] = price;

        return actions;
    }

    [Fact]
    public void TheLevelIsDerivedFromTheExperience()
    {
        HeroActions actions = BlueActions(out _, out _);
        Hero hero = actions.Hero;

        Assert.Equal(1, hero.Level);

        hero.SetExperience(HeroExperience.FromLevel(1) - 1);
        Assert.Equal(1, hero.Level);

        hero.SetExperience(HeroExperience.FromLevel(1));
        Assert.Equal(2, hero.Level);

        hero.SetExperience(HeroExperience.FromLevel(2) - 1);
        Assert.Equal(2, hero.Level);
    }

    [Fact]
    public void TheTreeGivesExactlyTheExperienceOfOneLevel()
    {
        HeroActions actions = TreeActions(out _, out World world, 0, 0);

        Assert.Equal(TreeKnowledgeOutcome.Learned, actions.ActionToTreeKnowledge(0, world));
        Assert.Equal(HeroExperience.FromLevel(1), actions.Hero.Experience);
        Assert.Equal(2, actions.Hero.Level);

        Assert.Equal(TreeKnowledgeOutcome.None, actions.ActionToTreeKnowledge(0, world));
        Assert.Equal(HeroExperience.FromLevel(1), actions.Hero.Experience);
    }

    [Fact]
    public void APaidTreeTakesThePaymentOnlyWhenTheTreasuryCanCoverIt()
    {
        HeroActions actions = TreeActions(out PlayerKingdom kingdom, out World world, 2500, 2000);

        Assert.Equal(TreeKnowledgeOutcome.Learned, actions.ActionToTreeKnowledge(0, world));
        Assert.Equal(500, kingdom.Treasury.Gold);
        Assert.Equal(HeroExperience.FromLevel(1), actions.Hero.Experience);
    }

    [Fact]
    public void APoorHeroLeavesTheTreePriceUntouched()
    {
        HeroActions actions = TreeActions(out PlayerKingdom kingdom, out World world, 100, 2000);

        Assert.Equal(TreeKnowledgeOutcome.CannotPay, actions.ActionToTreeKnowledge(0, world));
        Assert.Equal(100, kingdom.Treasury.Gold);
        Assert.Equal(0u, actions.Hero.Experience);

        // The kingdom book has the trip, the hero book does not: the next visit still asks for the money.
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.TreeOfKnowledge));
        Assert.False(actions.Hero.IsVisited(world.GetTile(0)));
    }

    [Fact]
    public void TheGazeboPaysOncePerTile()
    {
        HeroActions actions = BlueActions(out _, out World world);

        world.GetTile(0).SetMainObjectType(Mp2ObjectType.Gazebo);

        Assert.True(actions.ActionToExperienceObject(0, world));
        Assert.Equal(1000u, actions.Hero.Experience);

        Assert.False(actions.ActionToExperienceObject(0, world));
        Assert.Equal(1000u, actions.Hero.Experience);
    }

    [Fact]
    public void ALevelUpRaisesOnePrimaryAndAddsOneSecondarySkill()
    {
        HeroActions actions = BlueActions(out _, out _);
        Hero hero = actions.Hero;

        int before = hero.GetAttack() + hero.GetDefense() + hero.GetPower() + hero.GetKnowledge();

        hero.IncreaseExperience(HeroExperience.FromLevel(1), MapSeed);

        Assert.Equal(2, hero.Level);
        Assert.Equal(before + 1, hero.GetAttack() + hero.GetDefense() + hero.GetPower() + hero.GetKnowledge());
        Assert.Equal(1, hero.SecondarySkills.Count);
    }

    [Fact]
    public void JumpingSeveralLevelsDrawsEveryLevel()
    {
        HeroActions actions = BlueActions(out _, out _);
        Hero hero = actions.Hero;

        int before = hero.GetAttack() + hero.GetDefense() + hero.GetPower() + hero.GetKnowledge();

        // Level 3 needs FromLevel(2) experience: two level-ups, two primary points and two secondary skills.
        hero.IncreaseExperience(HeroExperience.FromLevel(2), MapSeed);

        Assert.Equal(3, hero.Level);
        Assert.Equal(before + 2, hero.GetAttack() + hero.GetDefense() + hero.GetPower() + hero.GetKnowledge());
        Assert.Equal(2, hero.SecondarySkills.Count);
    }

    [Fact]
    public void TheSameSeedAndStateDrawTheSameSkills()
    {
        HeroActions first = BlueActions(out _, out _);
        HeroActions second = BlueActions(out _, out _);

        first.Hero.IncreaseExperience(HeroExperience.FromLevel(2), MapSeed);
        second.Hero.IncreaseExperience(HeroExperience.FromLevel(2), MapSeed);

        Assert.Equal(first.Hero.GetAttack(), second.Hero.GetAttack());
        Assert.Equal(first.Hero.GetDefense(), second.Hero.GetDefense());
        Assert.Equal(first.Hero.GetPower(), second.Hero.GetPower());
        Assert.Equal(first.Hero.GetKnowledge(), second.Hero.GetKnowledge());

        foreach (SecondarySkillLevel entry in first.Hero.SecondarySkills.Skills)
        {
            Assert.Equal(entry.Level, second.Hero.SecondarySkills.GetLevel(entry.Skill));
        }
    }

    [Fact]
    public void TheChooserDecidesWhichCandidateIsTaken()
    {
        HeroActions actions = BlueActions(out _, out _);

        SecondarySkillLevel? offeredFirst = null;
        SecondarySkillLevel? offeredSecond = null;

        actions.Hero.IncreaseExperience(
            HeroExperience.FromLevel(1),
            MapSeed,
            (first, second) =>
            {
                offeredFirst = first;
                offeredSecond = second;

                return second.IsValid ? second : first;
            });

        Assert.True(offeredFirst!.Value.IsValid);
        Assert.True(offeredSecond!.Value.IsValid);
        Assert.Equal(offeredSecond!.Value.Skill, actions.Hero.SecondarySkills.Skills[0].Skill);
    }

    [Fact]
    public void TheMagicClassesAreTheFourSpellcasters()
    {
        Assert.True(SkillLevelUp.IsMagicalRace(MapRace.Sorceress));
        Assert.True(SkillLevelUp.IsMagicalRace(MapRace.Warlock));
        Assert.True(SkillLevelUp.IsMagicalRace(MapRace.Wizard));
        Assert.True(SkillLevelUp.IsMagicalRace(MapRace.Necromancer));

        Assert.False(SkillLevelUp.IsMagicalRace(MapRace.Knight));
        Assert.False(SkillLevelUp.IsMagicalRace(MapRace.Barbarian));
        Assert.False(SkillLevelUp.IsMagicalRace(MapRace.Multiple));
        Assert.False(SkillLevelUp.IsMagicalRace(MapRace.Random));
    }
}
