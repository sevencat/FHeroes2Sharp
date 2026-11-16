using FHeroes2.Game.Heroes;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the value rows of <c>game_static.cpp:91-104</c>, the witch's hut list and the diplomacy discount.
/// </summary>
public sealed class SecondarySkillsTests
{
    [Theory]
    [InlineData(SecondarySkill.Pathfinding, 25, 50, 100)]
    [InlineData(SecondarySkill.Archery, 10, 25, 50)]
    [InlineData(SecondarySkill.Logistics, 10, 20, 30)]
    [InlineData(SecondarySkill.Scouting, 1, 2, 3)]
    [InlineData(SecondarySkill.Diplomacy, 25, 50, 100)]
    [InlineData(SecondarySkill.Navigation, 33, 66, 100)]
    [InlineData(SecondarySkill.Leadership, 1, 2, 3)]
    [InlineData(SecondarySkill.Wisdom, 3, 4, 5)]
    [InlineData(SecondarySkill.Mysticism, 1, 2, 3)]
    [InlineData(SecondarySkill.Luck, 1, 2, 3)]
    [InlineData(SecondarySkill.Ballistics, 0, 0, 0)]
    [InlineData(SecondarySkill.EagleEye, 20, 30, 40)]
    [InlineData(SecondarySkill.Necromancy, 10, 20, 30)]
    [InlineData(SecondarySkill.Estates, 100, 250, 500)]
    public void GetValue_ReadsTheRowOfTheSkill(SecondarySkill skill, ushort basic, ushort advanced, ushort expert)
    {
        Assert.Equal(basic, SecondarySkills.GetValue(skill, SkillLevel.Basic));
        Assert.Equal(advanced, SecondarySkills.GetValue(skill, SkillLevel.Advanced));
        Assert.Equal(expert, SecondarySkills.GetValue(skill, SkillLevel.Expert));
    }

    [Fact]
    public void GetValue_IsZeroWithoutTheSkill()
    {
        // The reference returns zero for every level it does not know, NONE included: a hero without
        // estates earns no gold, one without pathfinding has no discount.
        Assert.Equal(0, SecondarySkills.GetValue(SecondarySkill.Estates, SkillLevel.None));
        Assert.Equal(0, SecondarySkills.GetValue(SecondarySkill.Pathfinding, SkillLevel.None));
    }

    [Fact]
    public void GetValue_RejectsTheUnknownSkill()
    {
        // The reference returns nullptr from its table lookup and GetValue turns that into zero; a caller
        // holding UNKNOWN has a bug, so the port throws.
        Assert.Throws<ArgumentOutOfRangeException>(() => SecondarySkills.GetValue(SecondarySkill.Unknown, SkillLevel.Basic));
    }

    [Fact]
    public void BallisticsIsTheOnlySkillThatIsWorthNothing()
    {
        // The catapult's accuracy is read from the level itself, never from the row, which is why the whole
        // row is zero. Any other zero row would be a transcription slip.
        foreach (SecondarySkill skill in SecondarySkills.All)
        {
            bool isZero = SecondarySkills.GetValue(skill, SkillLevel.Basic) == 0
                && SecondarySkills.GetValue(skill, SkillLevel.Advanced) == 0
                && SecondarySkills.GetValue(skill, SkillLevel.Expert) == 0;

            Assert.Equal(skill == SecondarySkill.Ballistics, isZero);
        }
    }

    [Fact]
    public void All_HoldsTheFourteenSkillsInIdOrder()
    {
        Assert.Equal(SecondarySkills.Count, SecondarySkills.All.Count);
        Assert.Equal(Enumerable.Range(1, SecondarySkills.Count), SecondarySkills.All.Select(skill => (int)skill));
    }

    [Fact]
    public void InitialFillOrder_HoldsEverySkillExactlyOnce()
    {
        // The order is the constructor's, not the id order; it only has to be a permutation of the fourteen.
        Assert.Equal(
            SecondarySkills.All.OrderBy(skill => skill),
            SecondarySkills.InitialFillOrder.OrderBy(skill => skill));
    }

    [Fact]
    public void WitchsHutSkills_CannotTeachLeadershipOrNecromancy()
    {
        Assert.Equal(12, SecondarySkills.WitchsHutSkills.Count);
        Assert.DoesNotContain(SecondarySkill.Leadership, SecondarySkills.WitchsHutSkills);
        Assert.DoesNotContain(SecondarySkill.Necromancy, SecondarySkills.WitchsHutSkills);

        // ... and everything else is in there.
        Assert.Equal(
            SecondarySkills.All.Except([SecondarySkill.Leadership, SecondarySkill.Necromancy]).Order(),
            SecondarySkills.WitchsHutSkills.Order());
    }

    [Theory]
    [InlineData(SkillLevel.None, 0)]
    [InlineData(SkillLevel.Basic, 40)]
    [InlineData(SkillLevel.Advanced, 30)]
    [InlineData(SkillLevel.Expert, 20)]
    public void GetDiplomacySurrenderCostDiscount_FollowsTheReferenceTable(SkillLevel level, int expected)
    {
        Assert.Equal(expected, SecondarySkills.GetDiplomacySurrenderCostDiscount(level));
    }

    [Theory]
    [InlineData(SecondarySkill.Pathfinding, "Pathfinding")]
    [InlineData(SecondarySkill.EagleEye, "Eagle Eye")]
    [InlineData(SecondarySkill.Estates, "Estates")]
    public void GetName_ReturnsTheReferenceText(SecondarySkill skill, string name)
    {
        Assert.Equal(name, SecondarySkills.GetName(skill));
    }

    [Fact]
    public void GetName_OfUnknownIsEmpty()
    {
        Assert.Equal(string.Empty, SecondarySkills.GetName(SecondarySkill.Unknown));
    }

    [Fact]
    public void GetIndexSprite1_IsTheIdItself()
    {
        Assert.Equal(0, SecondarySkills.GetIndexSprite1(SecondarySkill.Unknown));
        Assert.Equal(1, SecondarySkills.GetIndexSprite1(SecondarySkill.Pathfinding));
        Assert.Equal(14, SecondarySkills.GetIndexSprite1(SecondarySkill.Estates));
    }

    [Fact]
    public void GetIndexSprite2_IsOneLowerAndFFForUnknown()
    {
        Assert.Equal(0xFF, SecondarySkills.GetIndexSprite2(SecondarySkill.Unknown));
        Assert.Equal(0, SecondarySkills.GetIndexSprite2(SecondarySkill.Pathfinding));
        Assert.Equal(13, SecondarySkills.GetIndexSprite2(SecondarySkill.Estates));
    }
}
