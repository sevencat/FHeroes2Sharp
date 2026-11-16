using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>UltimateArtifact</c> (<c>artifact_ultimate.cpp:30-56</c>): the tile, the artifact and the nudge of the
/// puzzle drawing.
/// </summary>
[Collection(ArtifactRandomCollection.Name)]
public class UltimateArtifactTests : IDisposable
{
    public UltimateArtifactTests() => ArtifactRandom.Reset();

    public void Dispose() => ArtifactRandom.Reset();

    [Fact]
    public void ANewMapHasNoUltimateArtifact()
    {
        var artifact = new UltimateArtifact();

        Assert.Equal(ArtifactType.Unknown, artifact.Artifact);
        Assert.Equal(-1, artifact.Position);
        Assert.False(artifact.IsFound);
        Assert.Equal(0, artifact.Offset.X);
        Assert.Equal(0, artifact.Offset.Y);

        // The reference refuses every position while the index is negative, tile -1 included.
        Assert.False(artifact.IsAt(-1));
        Assert.False(artifact.IsAt(0));
    }

    [Fact]
    public void SetKeepsTheNamedArtifactAndTurnsOffFound()
    {
        var artifact = new UltimateArtifact();
        artifact.Set(42, ArtifactType.GoldenGoose, MapVersion.SuccessionWars);

        Assert.Equal(ArtifactType.GoldenGoose, artifact.Artifact);
        Assert.Equal(42, artifact.Position);
        Assert.True(artifact.IsAt(42));
        Assert.False(artifact.IsAt(41));
        Assert.False(artifact.IsFound);
    }

    [Fact]
    public void SetReplacesAnInvalidArtifactWithARandomUltimate()
    {
        // The usual map says "the map's ultimate artifact" without naming one, which is an invalid id.
        var artifact = new UltimateArtifact();
        artifact.Set(7, ArtifactType.Unknown, MapVersion.PriceOfLoyalty);

        Assert.True(ArtifactLevels.IsUltimate(artifact.Artifact), $"{artifact.Artifact} is not an ultimate.");
        Assert.Equal(7, artifact.Position);
    }

    [Fact]
    public void TheOffsetStaysWithinTheCentralBlock()
    {
        var artifact = new UltimateArtifact();

        for (int i = 0; i < 50; ++i)
        {
            artifact.Set(i, ArtifactType.UltimateCrown, MapVersion.Resurrection);

            Assert.InRange(artifact.Offset.X, -2, 2);
            Assert.InRange(artifact.Offset.Y, -1, 1);
        }
    }

    [Fact]
    public void MarkAsFoundOnlyTouchesTheFlag()
    {
        var artifact = new UltimateArtifact();
        artifact.Set(3, ArtifactType.UltimateSword, MapVersion.SuccessionWars);

        artifact.MarkAsFound();

        Assert.True(artifact.IsFound);
        Assert.Equal(ArtifactType.UltimateSword, artifact.Artifact);
        Assert.Equal(3, artifact.Position);
    }

    [Fact]
    public void ResetForgetsTheArtifactTheTileAndTheOffset()
    {
        var artifact = new UltimateArtifact();
        artifact.Set(9, ArtifactType.UltimateWand, MapVersion.SuccessionWars);
        artifact.MarkAsFound();

        artifact.Reset();

        Assert.Equal(ArtifactType.Unknown, artifact.Artifact);
        Assert.Equal(-1, artifact.Position);
        Assert.False(artifact.IsFound);
        Assert.Equal(0, artifact.Offset.X);
        Assert.Equal(0, artifact.Offset.Y);
    }
}
