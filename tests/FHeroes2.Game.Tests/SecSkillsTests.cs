using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Skill::SecSkills</c> (<c>skill.cpp:621-780</c>).
/// </summary>
public sealed class SecSkillsTests
{
    [Fact]
    public void AKnightStartsWithLeadershipBeforeBallistics()
    {
        SecSkills skills = new(MapRace.Knight);

        // The order is the constructor's (leadership is the ninth `if`, ballistics the tenth), not the id
        // order and not the order they are listed in the skill table.
        Assert.Equal(2, skills.Count);
        Assert.Equal(SecondarySkill.Leadership, skills.Skills[0].Skill);
        Assert.Equal(SkillLevel.Basic, skills.Skills[0].Level);
        Assert.Equal(SecondarySkill.Ballistics, skills.Skills[1].Skill);
        Assert.Equal(SkillLevel.Basic, skills.Skills[1].Level);
    }

    [Fact]
    public void AMagicHeroStartsWithItsOwnSkillBeforeWisdom()
    {
        SecSkills sorceress = new(MapRace.Sorceress);

        Assert.Equal(SecondarySkill.Navigation, sorceress.Skills[0].Skill);
        Assert.Equal(SkillLevel.Advanced, sorceress.Skills[0].Level);
        Assert.Equal(SecondarySkill.Wisdom, sorceress.Skills[1].Skill);
        Assert.Equal(SkillLevel.Basic, sorceress.Skills[1].Level);
    }

    [Fact]
    public void ARaceWithoutPropertiesHasNoSkills()
    {
        Assert.Empty(new SecSkills().Skills);
        Assert.Empty(new SecSkills(MapRace.None).Skills);
        Assert.Empty(new SecSkills(MapRace.Random).Skills);
        Assert.Empty(new SecSkills(MapRace.Multiple).Skills);
        Assert.Equal(0, new SecSkills(MapRace.Random).Count);
    }

    [Fact]
    public void CountAndTotalLevelSkipTheEmptySlots()
    {
        SecSkills skills = new(MapRace.Knight);
        skills.FillMax(default);

        // Eight slots, two of them filled: the count is of learned skills, the total is of their levels.
        Assert.Equal(SecSkills.MaxCount, skills.Skills.Count);
        Assert.Equal(2, skills.Count);
        Assert.Equal(2, skills.TotalLevel);
    }

    [Fact]
    public void GetLevelAndGetValueReadTheSkillsOfTheHero()
    {
        SecSkills skills = new(MapRace.Barbarian);

        Assert.Equal(SkillLevel.Advanced, skills.GetLevel(SecondarySkill.Pathfinding));
        Assert.Equal(50u, skills.GetValue(SecondarySkill.Pathfinding));

        Assert.Equal(SkillLevel.None, skills.GetLevel(SecondarySkill.Estates));
        Assert.Equal(0u, skills.GetValue(SecondarySkill.Estates));
    }

    [Fact]
    public void AddingAnExistingSkillRaisesItsLevelWithoutGrowingTheSet()
    {
        SecSkills skills = new(MapRace.Knight);

        skills.Add(new SecondarySkillLevel(SecondarySkill.Leadership, SkillLevel.Expert));

        Assert.Equal(2, skills.Count);
        Assert.Equal(SkillLevel.Expert, skills.GetLevel(SecondarySkill.Leadership));

        // The other skill of the hero is untouched, and the raised one stayed in its slot.
        Assert.Equal(SecondarySkill.Ballistics, skills.Skills[1].Skill);
        Assert.Equal(SkillLevel.Basic, skills.Skills[1].Level);
    }

    [Fact]
    public void AddingANewSkillTakesTheFirstEmptySlot()
    {
        SecSkills skills = new(MapRace.Knight);
        skills.FillMax(default);

        skills.Add(new SecondarySkillLevel(SecondarySkill.Estates, SkillLevel.Basic));

        Assert.Equal(3, skills.Count);
        Assert.Equal(SecondarySkill.Estates, skills.Skills[2].Skill);
        Assert.Equal(SecondarySkill.Leadership, skills.Skills[0].Skill);
        Assert.Equal(SecondarySkill.Ballistics, skills.Skills[1].Skill);
    }

    [Fact]
    public void AddingANinthSkillIsDropped()
    {
        SecSkills skills = new(MapRace.Knight);

        // Eight distinct skills is the cap; the ninth is silently dropped, like the reference's append that
        // only happens while the vector is shorter than maxNumOfSecSkills.
        foreach (SecondarySkill skill in SecondarySkills.All.Take(SecSkills.MaxCount))
        {
            skills.Add(new SecondarySkillLevel(skill, SkillLevel.Basic));
        }

        skills.Add(new SecondarySkillLevel(SecondarySkill.Estates, SkillLevel.Expert));

        Assert.Equal(SecSkills.MaxCount, skills.Count);
        Assert.Equal(SkillLevel.None, skills.GetLevel(SecondarySkill.Estates));
    }

    [Fact]
    public void AnUnsetSkillIsNotAdded()
    {
        SecSkills skills = new();
        skills.Add(default);
        skills.Add(new SecondarySkillLevel(SecondarySkill.Estates, SkillLevel.None));
        skills.Add(new SecondarySkillLevel(SecondarySkill.Unknown, SkillLevel.Basic));

        Assert.Empty(skills.Skills);
        Assert.Equal(0, skills.Count);
    }

    [Fact]
    public void FillMaxPadsWithoutTouchingTheLearnedSkills()
    {
        SecSkills skills = new(MapRace.Wizard);

        skills.FillMax(default);
        skills.FillMax(default);

        Assert.Equal(SecSkills.MaxCount, skills.Skills.Count);
        Assert.Equal(1, skills.Count);
        Assert.Equal(2, skills.TotalLevel);
        Assert.Equal(SkillLevel.Advanced, skills.GetLevel(SecondarySkill.Wisdom));
    }

    [Fact]
    public void TheTotalLevelAddsTheLevelsNotTheSkills()
    {
        SecSkills skills = new();

        skills.Add(new SecondarySkillLevel(SecondarySkill.Estates, SkillLevel.Expert)); // 3
        skills.Add(new SecondarySkillLevel(SecondarySkill.Leadership, SkillLevel.Basic)); // 1
        skills.Add(new SecondarySkillLevel(SecondarySkill.Logistics, SkillLevel.Advanced)); // 2

        Assert.Equal(3, skills.Count);
        Assert.Equal(6, skills.TotalLevel);
    }
}
