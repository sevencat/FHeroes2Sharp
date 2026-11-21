using FHeroes2.Assets.Maps;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Behaviour tests for the hero half of <c>ai_planner_hero.cpp</c> that is ported: the two secondary skill
/// choices of <see cref="AiPlanner.PickSecondarySkill"/> and the price <see cref="Hero.SpellCasted"/> charges.
/// </summary>
/// <remarks>
/// The skill values of the reference only depend on the skill itself (Wisdom, Logistics) or on the hero's role and
/// army, so a hero of its own — <c>new Hero(1, MapRace.Knight)</c> — is enough. The two skills that read
/// <see cref="World.Current"/> (Pathfinding, Navigation) are left out on purpose.
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AiHeroPlannerTests
{
    private static Hero NewHero() => new(1, MapRace.Knight);

    private static SecondarySkillLevel None => new(SecondarySkill.Unknown, SkillLevel.None);

    [Fact]
    public void PickSecondarySkillReturnsTheOnlyChoiceWhenTheOtherOneIsEmpty()
    {
        SecondarySkillLevel only = new(SecondarySkill.Wisdom, SkillLevel.Basic);

        Assert.Equal(only, AiPlanner.PickSecondarySkill(NewHero(), only, None));
    }

    [Fact]
    public void PickSecondarySkillPrefersWisdomOverLogistics()
    {
        SecondarySkillLevel wisdom = new(SecondarySkill.Wisdom, SkillLevel.Basic);
        SecondarySkillLevel logistics = new(SecondarySkill.Logistics, SkillLevel.Basic);

        Assert.Equal(wisdom, AiPlanner.PickSecondarySkill(NewHero(), wisdom, logistics));
        Assert.Equal(wisdom, AiPlanner.PickSecondarySkill(NewHero(), logistics, wisdom));
    }

    [Fact]
    public void PickSecondarySkillKeepsTheFirstOfTwoEquallyGoodSkills()
    {
        SecondarySkillLevel wisdom = new(SecondarySkill.Wisdom, SkillLevel.Basic);
        SecondarySkillLevel sameWisdom = new(SecondarySkill.Wisdom, SkillLevel.Basic);

        Assert.Equal(wisdom, AiPlanner.PickSecondarySkill(NewHero(), wisdom, sameWisdom));
    }

    [Fact]
    public void PickSecondarySkillTakesTheBasicLevelWhenTwoWorthlessSkillsTie()
    {
        // Estates 对 Fighter/Champion 之外的英雄值 1000，而 Luck 对非 Courier/Scout 值 500：先比分数，再看等级。
        SecondarySkillLevel estatesBasic = new(SecondarySkill.Estates, SkillLevel.Basic);
        SecondarySkillLevel estatesAdvanced = new(SecondarySkill.Estates, SkillLevel.Advanced);

        Assert.Equal(estatesBasic, AiPlanner.PickSecondarySkill(NewHero(), estatesBasic, estatesAdvanced));
    }

    [Fact]
    public void SpellCastedChargesTheSpellPointsOfTheSpell()
    {
        Hero hero = NewHero();

        hero.SetSpellPoints(100);
        hero.SetMovePoints(2000);

        hero.SpellCasted(new Spell(SpellType.ViewAll));

        // 全图视野只花法术点，不花移动力；两笔账都不许倒着走。
        Assert.True(hero.SpellPoints < 100, "The spell points of the spell are paid.");
        Assert.True(hero.MovePoints <= 2000, "A spell never gives movement points back.");
    }

    [Fact]
    public void SpellCastedNeverTakesMoreSpellPointsThanTheHeroHas()
    {
        Hero hero = NewHero();

        hero.SetSpellPoints(0);
        hero.SpellCasted(new Spell(SpellType.ViewAll));

        Assert.Equal(0U, hero.SpellPoints);
    }
}
