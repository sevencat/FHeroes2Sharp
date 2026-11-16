using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>Artifact::getArtifactFromMapSpriteIndex()</c> (<c>artifact.cpp:464-500</c>): the old format stores an
/// artifact object as a sprite index, two indices per artifact, plus the five "random artifact" objects.
/// </summary>
[Collection(ArtifactRandomCollection.Name)]
public class ArtifactMapSpritesTests : IDisposable
{
    public ArtifactMapSpritesTests() => ArtifactRandom.Reset();

    public void Dispose() => ArtifactRandom.Reset();

    [Theory]
    // Two sprite indices per artifact, so index 1 and 2 are both the first artifact of the list.
    [InlineData(0u, ArtifactType.UltimateBook)]
    [InlineData(1u, ArtifactType.UltimateBook)]
    [InlineData(2u, ArtifactType.UltimateBook)]
    [InlineData(3u, ArtifactType.UltimateSword)]
    [InlineData(127u, ArtifactType.GoldenBow)]
    [InlineData(128u, ArtifactType.GoldenBow)]
    [InlineData(161u, ArtifactType.BlackPearl)]
    public void TheFirstBlockIsTwoIndicesPerArtifact(uint index, ArtifactType expected)
    {
        Assert.Equal(expected, ArtifactMapSprites.FromMapIndex(index, MapVersion.SuccessionWars, false));
        Assert.Equal(expected, ArtifactMapSprites.FromMapIndex(index, MapVersion.Resurrection, true));
    }

    [Fact]
    public void TheExpansionBlockIsOnlyReadableWhenTheExpansionIsInstalled()
    {
        // Index 173 is the first expansion artifact, the scroll; 172 is the unused sprite before it, and the two
        // indices of the battle garb are 181 and 182.
        Assert.Equal(ArtifactType.SpellScroll, ArtifactMapSprites.FromMapIndex(173, MapVersion.PriceOfLoyalty, true));
        Assert.Equal(ArtifactType.BattleGarb, ArtifactMapSprites.FromMapIndex(181, MapVersion.PriceOfLoyalty, true));

        // Without the expansion the index is not an artifact at all, which is how the reference refuses to read a
        // map the installation cannot satisfy.
        Assert.Equal(ArtifactType.Unknown, ArtifactMapSprites.FromMapIndex(173, MapVersion.SuccessionWars, false));
    }

    [Fact]
    public void TheMagicBookHasItsOwnIndexOnlyInTheResurrectionFormat()
    {
        Assert.Equal(ArtifactType.MagicBook, ArtifactMapSprites.FromMapIndex(207, MapVersion.Resurrection, true));
        Assert.Equal(ArtifactType.Unknown, ArtifactMapSprites.FromMapIndex(207, MapVersion.PriceOfLoyalty, true));
        Assert.Equal(ArtifactType.Unknown, ArtifactMapSprites.FromMapIndex(207, MapVersion.SuccessionWars, false));
    }

    [Theory]
    [InlineData(163u, ArtifactLevel.AllNormal)]
    [InlineData(164u, ArtifactLevel.Ultimate)]
    [InlineData(167u, ArtifactLevel.Treasure)]
    [InlineData(169u, ArtifactLevel.Minor)]
    [InlineData(171u, ArtifactLevel.Major)]
    public void TheFiveRandomObjectsPickFromTheirTier(uint index, ArtifactLevel level)
    {
        for (int pick = 0; pick < 10; ++pick)
        {
            ArtifactType artifact = ArtifactMapSprites.FromMapIndex(index, MapVersion.PriceOfLoyalty, true);

            Assert.True(ArtifactTypes.IsValid(artifact), $"Index {index} answered {artifact}.");
            Assert.NotEqual(ArtifactLevel.None, ArtifactLevels.Level(artifact, MapVersion.PriceOfLoyalty) & level);
        }
    }

    [Theory]
    [InlineData(162u)]
    [InlineData(165u)]
    [InlineData(206u)]
    [InlineData(208u)]
    public void AnIndexNoArtifactObjectUsesAnswersUnknown(uint index)
    {
        Assert.Equal(ArtifactType.Unknown, ArtifactMapSprites.FromMapIndex(index, MapVersion.Resurrection, true));
    }

    [Fact]
    public void TheIndexBeforeTheExpansionBlockStillGoesThroughTheArithmetic()
    {
        // 172 is not an object of its own: the reference subtracts one and halves before checking the random objects,
        // so it lands on id 86, the unused slot that follows the last Succession Wars artifact. The port keeps that
        // arithmetic as it is rather than special-casing an index no map writes.
        Assert.Equal(ArtifactType.Unused86, ArtifactMapSprites.FromMapIndex(172, MapVersion.Resurrection, true));
    }
}
