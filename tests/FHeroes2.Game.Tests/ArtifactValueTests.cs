using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the weights of <c>Artifact::getArtifactValue()</c> and the bag total built on them
/// (<c>artifact.cpp:269-382</c>, <c>artifact.cpp:880-889</c>).
/// </summary>
public class ArtifactValueTests
{
    [Theory]
    // One point per point: the primary skills.
    [InlineData(ArtifactType.UltimateCloak, 12.0)]
    [InlineData(ArtifactType.UltimateSword, 12.0)]
    [InlineData(ArtifactType.UltimateBook, 12.0)]
    // A gold income is worth one point per 800 gold a day, an ore income one point per unit.
    [InlineData(ArtifactType.EndlessSackGold, 1.25)]
    [InlineData(ArtifactType.EndlessPouchSulfur, 1.0)]
    // Movement is one point per 200 points on land.
    [InlineData(ArtifactType.NomadBootsMobility, 3.0)]
    // A damage percentage is worth one point per 50 percent.
    [InlineData(ArtifactType.IceCloak, 1.0)]
    // A surrender discount is worth one point per 10 percent.
    [InlineData(ArtifactType.StatesmanQuill, 1.0)]
    // The flags are worth a point each, whatever number the row happens to carry.
    [InlineData(ArtifactType.GoldenBow, 1.0)]
    [InlineData(ArtifactType.CrystalBall, 1.0)]
    [InlineData(ArtifactType.SpellScroll, 1.0)]
    // Morale and luck caps are worth three.
    [InlineData(ArtifactType.SphereNegation, 3.0)]
    public void TheBonusWeightsAreTheReferenceOnes(ArtifactType artifact, double expected)
    {
        Assert.Equal(expected, ArtifactValues.Value(artifact), 6);
    }

    [Theory]
    // Two point skills that also carry a curse: the curse is subtracted, not ignored.
    [InlineData(ArtifactType.BroachShielding, -1.0)]
    // Curses: a gold penalty is one point per 200 gold a day, a morale step one point.
    [InlineData(ArtifactType.TaxLien, -1.25)]
    [InlineData(ArtifactType.FizbinMisfortune, -2.0)]
    [InlineData(ArtifactType.HideousMask, -1.0)]
    public void TheCurseWeightsAreTheReferenceOnes(ArtifactType artifact, double expected)
    {
        Assert.Equal(expected, ArtifactValues.Value(artifact), 6);
    }

    [Fact]
    public void TheBattleGarbAddsTheSkillsTheFlagsAndTheSpell()
    {
        // +5 attack, +5 defence, +5 spell power, the morale and luck caps and the town portal spell.
        Assert.Equal(22.0, ArtifactValues.Value(ArtifactType.BattleGarb), 6);
    }

    [Fact]
    public void AnArtifactThatDoesNothingIsWorthNothing()
    {
        Assert.Equal(0.0, ArtifactValues.Value(ArtifactType.MagicBook), 6);
        Assert.Equal(0.0, ArtifactValues.Value(ArtifactType.Unknown), 6);
        Assert.Equal(0.0, ArtifactValues.Value(ArtifactType.ArtifactCount), 6);
    }

    [Fact]
    public void TheBagTotalIsTheSumOfTheArtifacts()
    {
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.UltimateSword);
        bag.Push(ArtifactType.EndlessSackGold);
        bag.Push(ArtifactType.MagicBook);

        Assert.Equal(13.25, bag.GetArtifactValue(), 6);

        bag.Push(ArtifactType.TaxLien);
        Assert.Equal(12.0, bag.GetArtifactValue(), 6);
    }

    [Fact]
    public void AnEmptyBagIsWorthNothing()
    {
        Assert.Equal(0.0, new ArtifactBag().GetArtifactValue(), 6);
    }

    [Fact]
    public void TheMultipliedPercentagesAreListedNotSummed()
    {
        // Two quills still discount the surrender by ten percent each, so the list holds one entry: the values are
        // multiplied one by one by the battle code, and only distinct artifact ids count.
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.StatesmanQuill);
        bag.Push(ArtifactType.StatesmanQuill);

        Assert.Equal(new[] { 10 }, bag.GetTotalMultipliedPercent(ArtifactBonusType.SurrenderCostReductionPercent));

        // The curse twin behaves the same way.
        var cursed = new ArtifactBag();
        cursed.Push(ArtifactType.HeartFire);
        cursed.Push(ArtifactType.HeartFire);

        Assert.Equal(new[] { 100 }, cursed.GetTotalMultipliedPercent(ArtifactCurseType.ColdSpellExtraDamagePercent));

        Assert.Empty(new ArtifactBag().GetTotalMultipliedPercent(ArtifactBonusType.SurrenderCostReductionPercent));
        Assert.Empty(new ArtifactBag().GetTotalMultipliedPercent(ArtifactCurseType.ColdSpellExtraDamagePercent));
    }

    [Fact]
    public void AskingForANonMultipliedPercentageAnswersEmpty()
    {
        // The reference asserts and returns an empty vector; the port does the same, and the assert only fires in
        // debug builds, so the contract is the empty list.
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.UltimateSword);

        Assert.Empty(bag.GetTotalMultipliedPercent(ArtifactBonusType.AttackSkill));
        Assert.Empty(bag.GetTotalMultipliedPercent(ArtifactCurseType.Morale));
    }

    [Fact]
    public void ThePresenceQueriesSeeTheBagInSlotOrder()
    {
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.GoldenBow);
        bag.Push(ArtifactType.CrystalBall);
        bag.Push(ArtifactType.EndlessSackGold);

        Assert.True(bag.IsBonusPresent(ArtifactBonusType.NoShootingPenalty));
        Assert.True(bag.IsBonusPresent(ArtifactBonusType.ViewMonsterInformation));
        Assert.False(bag.IsBonusPresent(ArtifactBonusType.AttackSkill));

        Assert.Equal(ArtifactType.GoldenBow, bag.GetFirstArtifactWithBonus(ArtifactBonusType.NoShootingPenalty));
        Assert.Equal(ArtifactType.CrystalBall, bag.GetFirstArtifactWithBonus(ArtifactBonusType.ViewMonsterInformation));
        Assert.Equal(ArtifactType.Unknown, bag.GetFirstArtifactWithBonus(ArtifactBonusType.AttackSkill));

        bag.Push(ArtifactType.TaxLien);

        Assert.True(bag.IsCursePresent(ArtifactCurseType.GoldPenalty));
        Assert.False(bag.IsCursePresent(ArtifactCurseType.Morale));
        Assert.Equal(ArtifactType.TaxLien, bag.GetFirstArtifactWithCurse(ArtifactCurseType.GoldPenalty));
        Assert.Equal(ArtifactType.Unknown, bag.GetFirstArtifactWithCurse(ArtifactCurseType.Morale));
    }

    [Fact]
    public void TheBagKnowsWhetherItHoldsAnUltimate()
    {
        var bag = new ArtifactBag();
        Assert.False(bag.ContainsUltimateArtifact());

        bag.Push(ArtifactType.GoldenBow);
        Assert.False(bag.ContainsUltimateArtifact());

        // The goose counts, although it is not one of the eight named pieces.
        bag.Push(ArtifactType.GoldenGoose);
        Assert.True(bag.ContainsUltimateArtifact());
    }
}
