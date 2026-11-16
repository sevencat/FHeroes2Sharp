using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>Artifact::Level()</c>, <c>LoyaltyLevel()</c>, <c>isUltimate()</c> and <c>isPriceOfLoyaltyArtifact()</c>
/// (<c>artifact.cpp:98-267</c>, <c>artifact.cpp:1099-1102</c>).
/// </summary>
public class ArtifactLevelTests
{
    [Fact]
    public void TheSevenUltimatesAndTheGooseAreUltimate()
    {
        var ultimates = new[]
        {
            ArtifactType.UltimateBook,
            ArtifactType.UltimateSword,
            ArtifactType.UltimateCloak,
            ArtifactType.UltimateWand,
            ArtifactType.UltimateShield,
            ArtifactType.UltimateStaff,
            ArtifactType.UltimateCrown,
            ArtifactType.GoldenGoose,
        };

        Assert.All(ultimates, artifact =>
        {
            Assert.True(ArtifactLevels.IsUltimate(artifact));
            Assert.Equal(ArtifactLevel.Ultimate, ArtifactLevels.Level(artifact, MapVersion.SuccessionWars));
        });

        // The goose is not one of the named pieces but the ultimate object may still hold it; the artifacts of the
        // expansion next to the set are not ultimates, whatever their tier.
        Assert.False(ArtifactLevels.IsUltimate(ArtifactType.BlackPearl));
        Assert.False(ArtifactLevels.IsUltimate(ArtifactType.BattleGarb));
        Assert.False(ArtifactLevels.IsUltimate(ArtifactType.Unknown));
    }

    [Theory]
    [InlineData(ArtifactType.GoldenBow, ArtifactLevel.Treasure)]
    [InlineData(ArtifactType.IceCloak, ArtifactLevel.Treasure)]
    [InlineData(ArtifactType.StatesmanQuill, ArtifactLevel.Treasure)]
    [InlineData(ArtifactType.SpikedHelm, ArtifactLevel.Minor)]
    [InlineData(ArtifactType.CasterBracelet, ArtifactLevel.Minor)]
    [InlineData(ArtifactType.SailorsAstrolabeMobility, ArtifactLevel.Minor)]
    [InlineData(ArtifactType.EndlessSackGold, ArtifactLevel.Major)]
    [InlineData(ArtifactType.NomadBootsMobility, ArtifactLevel.Major)]
    [InlineData(ArtifactType.DragonSword, ArtifactLevel.Major)]
    [InlineData(ArtifactType.MagicBook, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.FizbinMisfortune, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.TaxLien, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.HideousMask, ArtifactLevel.NoRandom)]
    public void TheOrdinaryTiersAreTheReferenceOnes(ArtifactType artifact, ArtifactLevel expected)
    {
        // The version does not matter for the ordinary artifacts: their tier is a property of the artifact.
        Assert.Equal(expected, ArtifactLevels.Level(artifact, MapVersion.SuccessionWars));
        Assert.Equal(expected, ArtifactLevels.Level(artifact, MapVersion.PriceOfLoyalty));
        Assert.Equal(expected, ArtifactLevels.Level(artifact, MapVersion.Resurrection));
    }

    [Theory]
    [InlineData(ArtifactType.SpellScroll, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.ArmMartyr, ArtifactLevel.Major)]
    [InlineData(ArtifactType.BreastplateAnduran, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.BroachShielding, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.BattleGarb, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.CrystalBall, ArtifactLevel.Major)]
    [InlineData(ArtifactType.HelmetAnduran, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.HolyHammer, ArtifactLevel.Major)]
    [InlineData(ArtifactType.LegendaryScepter, ArtifactLevel.Major)]
    [InlineData(ArtifactType.Masthead, ArtifactLevel.Minor)]
    [InlineData(ArtifactType.SphereNegation, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.StaffWizardry, ArtifactLevel.Major)]
    [InlineData(ArtifactType.SwordBreaker, ArtifactLevel.Major)]
    [InlineData(ArtifactType.SwordAnduran, ArtifactLevel.NoRandom)]
    [InlineData(ArtifactType.SpadeNecromancy, ArtifactLevel.Minor)]
    [InlineData(ArtifactType.HeartFire, ArtifactLevel.Minor)]
    [InlineData(ArtifactType.HeartIce, ArtifactLevel.Minor)]
    public void AnExpansionArtifactHasNoTierOnASuccessionWarsMap(ArtifactType artifact, ArtifactLevel loyaltyLevel)
    {
        // The loyalty bit alone matches no random roll, which is how the original keeps the expansion artifacts
        // off the maps that never had them.
        Assert.Equal(ArtifactLevel.Loyalty, ArtifactLevels.Level(artifact, MapVersion.SuccessionWars));

        // On the two later map formats the same artifact is joined to the tier of its LoyaltyLevel().
        Assert.Equal(
            ArtifactLevel.Loyalty | loyaltyLevel,
            ArtifactLevels.Level(artifact, MapVersion.PriceOfLoyalty));
        Assert.Equal(
            ArtifactLevel.Loyalty | loyaltyLevel,
            ArtifactLevels.Level(artifact, MapVersion.Resurrection));

        Assert.Equal(loyaltyLevel, ArtifactLevels.LoyaltyLevel(artifact));
    }

    [Fact]
    public void TheLoyaltyLevelIsOnlyDefinedForTheExpansionArtifacts()
    {
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.LoyaltyLevel(ArtifactType.GoldenBow));
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.LoyaltyLevel(ArtifactType.Unknown));
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.LoyaltyLevel(ArtifactType.ArtifactCount));
    }

    [Fact]
    public void TheHolesAreNotArtifactsOfAnyTier()
    {
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.Level(ArtifactType.Unknown, MapVersion.Resurrection));
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.Level(ArtifactType.EditorAnyUltimateArtifact, MapVersion.Resurrection));
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.Level(ArtifactType.Unused84, MapVersion.Resurrection));
        Assert.Equal(ArtifactLevel.None, ArtifactLevels.Level(ArtifactType.ArtifactCount, MapVersion.Resurrection));
    }

    [Fact]
    public void EveryArtifactHasExactlyOneRarityBitAndTheExpansionAlsoTheLoyaltyOne()
    {
        const ArtifactLevel rarity = ArtifactLevel.Treasure | ArtifactLevel.Minor | ArtifactLevel.Major
            | ArtifactLevel.Ultimate | ArtifactLevel.NoRandom;

        int expansionArtifacts = 0;

        foreach (ArtifactType artifact in Enum.GetValues<ArtifactType>().Distinct())
        {
            if (!ArtifactTypes.IsValid(artifact))
            {
                continue;
            }

            ArtifactLevel level = ArtifactLevels.Level(artifact, MapVersion.Resurrection);

            // One bit out of the five, never two and never none: a dropped case would land in NONE.
            Assert.Equal(1, global::System.Numerics.BitOperations.PopCount((uint)(level & rarity)));

            Assert.Equal(ArtifactLevels.IsPriceOfLoyalty(artifact), (level & ArtifactLevel.Loyalty) != 0);

            if (ArtifactLevels.IsPriceOfLoyalty(artifact))
            {
                ++expansionArtifacts;
            }
        }

        // Seventeen artifacts of the expansion, from the scroll to the spade.
        Assert.Equal(17, expansionArtifacts);
    }

    [Fact]
    public void IsPriceOfLoyaltyIsTheContiguousBlockAtTheEnd()
    {
        Assert.True(ArtifactLevels.IsPriceOfLoyalty(ArtifactType.SpellScroll));
        Assert.True(ArtifactLevels.IsPriceOfLoyalty(ArtifactType.BattleGarb));
        Assert.True(ArtifactLevels.IsPriceOfLoyalty(ArtifactType.SpadeNecromancy));

        Assert.False(ArtifactLevels.IsPriceOfLoyalty(ArtifactType.MagicBook));
        Assert.False(ArtifactLevels.IsPriceOfLoyalty(ArtifactType.GoldenBow));
        Assert.False(ArtifactLevels.IsPriceOfLoyalty(ArtifactType.Unknown));
    }
}
