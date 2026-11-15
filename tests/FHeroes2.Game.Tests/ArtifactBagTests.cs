using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>BagArtifacts</c> (<c>artifact.cpp:517-889</c>): fourteen flat slots, the magic book's fixed place,
/// and the three piles of the summed effect value.
/// </summary>
public class ArtifactBagTests
{
    [Fact]
    public void ANewBagIsFourteenHoles()
    {
        var bag = new ArtifactBag();

        Assert.Equal(ArtifactBag.MaxCapacity, bag.Slots.Count);
        Assert.All(bag.Slots, slot => Assert.Equal(ArtifactType.Unknown, slot));
        Assert.False(bag.IsFull);
        Assert.Equal(0, bag.CountArtifacts());
    }

    [Fact]
    public void FourteenArtifactsFillTheBagAndTheFifteenthIsRefused()
    {
        var bag = new ArtifactBag();

        for (int i = 0; i < ArtifactBag.MaxCapacity; ++i)
        {
            Assert.True(bag.Push(ArtifactType.UltimateSword));
        }

        Assert.True(bag.IsFull);
        Assert.Equal(ArtifactBag.MaxCapacity, bag.CountArtifacts());
        Assert.False(bag.Push(ArtifactType.UltimateBook));
    }

    [Fact]
    public void TheMagicBookTakesTheFirstSlotAndPushesTheRestRight()
    {
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.UltimateSword);
        bag.Push(ArtifactType.MagicBook);

        // The book went first, the sword slid one to the right.
        Assert.Equal(ArtifactType.MagicBook, bag.Slots[0]);
        Assert.Equal(ArtifactType.UltimateSword, bag.Slots[1]);
        Assert.Equal(ArtifactType.Unknown, bag.Slots[2]);
    }

    [Fact]
    public void ASecondMagicBookIsRefused()
    {
        var bag = new ArtifactBag();

        Assert.True(bag.Push(ArtifactType.MagicBook));
        Assert.False(bag.Push(ArtifactType.MagicBook));
        Assert.Equal(1, bag.CountArtifacts());
    }

    [Fact]
    public void RemoveEmptiesTheFirstCopyOnly()
    {
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.UltimateSword);
        bag.Push(ArtifactType.GoldenBow);
        bag.Push(ArtifactType.UltimateSword);

        bag.Remove(ArtifactType.UltimateSword);

        Assert.Equal(ArtifactType.Unknown, bag.Slots[0]);
        Assert.Equal(ArtifactType.GoldenBow, bag.Slots[1]);
        Assert.Equal(ArtifactType.UltimateSword, bag.Slots[2]);
        Assert.Equal(1, bag.Count(ArtifactType.UltimateSword));

        bag.Remove(ArtifactType.UltimateSword);
        Assert.Equal(0, bag.Count(ArtifactType.UltimateSword));
        Assert.False(bag.IsPresent(ArtifactType.UltimateSword));
    }

    [Fact]
    public void CountAndPresenceSeeEveryCopy()
    {
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.UltimateBook);
        bag.Push(ArtifactType.UltimateBook);

        Assert.Equal(2, bag.Count(ArtifactType.UltimateBook));
        Assert.True(bag.IsPresent(ArtifactType.UltimateBook));
        Assert.False(bag.IsPresent(ArtifactType.UltimateSword));
        Assert.Equal(2, bag.CountArtifacts());
    }

    [Fact]
    public void ThePrimarySkillBonusCountsEveryCopy()
    {
        // Two ultimate swords are worth +24 attack, not +12: ATTACK_SKILL is cumulative.
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.UltimateSword);
        bag.Push(ArtifactType.UltimateSword);

        Assert.Equal(24, bag.GetTotalArtifactEffectValue(ArtifactBonusType.AttackSkill));
    }

    [Fact]
    public void TheOtherBonusesCountEachArtifactOnce()
    {
        // Two pairs of nomad boots still add 600 movement points, not 1200: LAND_MOBILITY is in the fourth
        // pile, which getTotalArtifactEffectValue() sums once per artifact id (artifact.cpp:579-594).
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.NomadBootsMobility);
        bag.Push(ArtifactType.NomadBootsMobility);

        Assert.Equal(600, bag.GetTotalArtifactEffectValue(ArtifactBonusType.LandMobility));

        // Two different artifacts of the same pile do add up, so the rule is "once per id", not "once".
        bag.Push(ArtifactType.TravelerBootsMobility);
        Assert.Equal(900, bag.GetTotalArtifactEffectValue(ArtifactBonusType.LandMobility));
    }

    [Fact]
    public void TheCurseTwinHasTheSameTwoPiles()
    {
        // Gold penalty is cumulative: two tax liens take 500 a day.
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.TaxLien);
        bag.Push(ArtifactType.TaxLien);

        Assert.Equal(500, bag.GetTotalArtifactEffectValue(ArtifactCurseType.GoldPenalty));

        // Morale is neither cumulative nor unique: two misfortunes still only count once.
        var misfortunes = new ArtifactBag();
        misfortunes.Push(ArtifactType.FizbinMisfortune);
        misfortunes.Push(ArtifactType.FizbinMisfortune);

        Assert.Equal(2, misfortunes.GetTotalArtifactEffectValue(ArtifactCurseType.Morale));
    }

    [Fact]
    public void ABlankBagSummsToZero()
    {
        var bag = new ArtifactBag();

        Assert.Equal(0, bag.GetTotalArtifactEffectValue(ArtifactBonusType.AttackSkill));
        Assert.Equal(0, bag.GetTotalArtifactEffectValue(ArtifactCurseType.GoldPenalty));
    }

    [Fact]
    public void ContainSpellFindsTheNamedSpellButNotABlankScroll()
    {
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.SpellScroll);

        // A blank scroll carries Spell::NONE, which is not a spell anybody asks for; the reference's
        // getSpellId() would answer NONE for it too.
        Assert.False(bag.ContainSpell(SpellType.TownPortal));
        Assert.True(bag.ContainSpell(SpellType.None));

        bag.Push(ArtifactType.BattleGarb);
        Assert.True(bag.ContainSpell(SpellType.TownPortal));
    }
}
