using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Serializes the tests that drive <see cref="ArtifactRandom"/>: it keeps the picks of the current map in a static
/// array, the way the reference's <c>artifactGlobalStatus</c> does, and every test here starts from
/// <see cref="ArtifactRandom.Reset"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ArtifactRandomCollection
{
    /// <summary>Name of the collection.</summary>
    public const string Name = "Artifact random";
}

/// <summary>
/// Pins the random picker (<c>artifact.cpp:434-462</c>) and its two bits of state
/// (<c>artifact.cpp:44-65</c>, <c>artifact.cpp:1085-1097</c>).
/// </summary>
[Collection(ArtifactRandomCollection.Name)]
public class ArtifactRandomTests : IDisposable
{
    public ArtifactRandomTests() => ArtifactRandom.Reset();

    public void Dispose() => ArtifactRandom.Reset();

    [Theory]
    [InlineData(MapVersion.SuccessionWars)]
    [InlineData(MapVersion.PriceOfLoyalty)]
    [InlineData(MapVersion.Resurrection)]
    public void APickAlwaysHasTheRequestedTier(MapVersion version)
    {
        for (int pick = 0; pick < 40; ++pick)
        {
            ArtifactType artifact = ArtifactRandom.Next(ArtifactLevel.AllNormal, version);

            Assert.True(ArtifactTypes.IsValid(artifact));
            Assert.NotEqual(ArtifactType.Unknown, artifact);
            Assert.NotEqual(ArtifactLevel.None, ArtifactLevels.Level(artifact, version) & ArtifactLevel.AllNormal);
        }
    }

    [Fact]
    public void AnUltimatePickIsOneOfTheEight()
    {
        for (int pick = 0; pick < 20; ++pick)
        {
            ArtifactType artifact = ArtifactRandom.Next(ArtifactLevel.Ultimate, MapVersion.PriceOfLoyalty);

            Assert.True(ArtifactLevels.IsUltimate(artifact));
        }
    }

    [Fact]
    public void EveryArtifactOfTheTierIsGivenOutOnceBeforeAnyRepeats()
    {
        int treasure = CountTier(ArtifactLevel.Treasure, MapVersion.SuccessionWars);
        Assert.True(treasure > 10, $"The treasure tier holds {treasure} artifacts.");

        var picks = new HashSet<ArtifactType>();

        for (int pick = 0; pick < treasure; ++pick)
        {
            Assert.True(picks.Add(ArtifactRandom.Next(ArtifactLevel.Treasure, MapVersion.SuccessionWars)));
        }

        // The pool is exhausted, so the next pick has to repeat: the second pass drops the "not used yet" rule.
        ArtifactType repeat = ArtifactRandom.Next(ArtifactLevel.Treasure, MapVersion.SuccessionWars);
        Assert.Contains(repeat, picks);
    }

    [Fact]
    public void AnExcludedArtifactIsNeverGivenOut()
    {
        // The map editor bans artifacts; a banned one must not be reachable even when the tier runs dry.
        ArtifactRandom.Exclude(ArtifactType.GoldenBow);

        int treasure = CountTier(ArtifactLevel.Treasure, MapVersion.SuccessionWars);

        for (int pick = 0; pick < treasure * 3; ++pick)
        {
            Assert.NotEqual(ArtifactType.GoldenBow, ArtifactRandom.Next(ArtifactLevel.Treasure, MapVersion.SuccessionWars));
        }
    }

    [Fact]
    public void ABannedTierAnswersUnknownInsteadOfSomethingBanned()
    {
        var treasure = new List<ArtifactType>();

        foreach (ArtifactType artifact in Enum.GetValues<ArtifactType>().Distinct())
        {
            if (ArtifactTypes.IsValid(artifact)
                && (ArtifactLevels.Level(artifact, MapVersion.SuccessionWars) & ArtifactLevel.Treasure) != 0)
            {
                treasure.Add(artifact);
            }
        }

        Assert.NotEmpty(treasure);

        foreach (ArtifactType artifact in treasure)
        {
            ArtifactRandom.Exclude(artifact);
        }

        Assert.Equal(ArtifactType.Unknown, ArtifactRandom.Next(ArtifactLevel.Treasure, MapVersion.SuccessionWars));
    }

    [Fact]
    public void ResetClearsThePicksAndTheBansTogether()
    {
        // The reference has one array for both bits and clears it whole, so a ban does not survive the end of a
        // map: the loader replays the bans of the next map from scratch.
        ArtifactRandom.Exclude(ArtifactType.GoldenBow);
        ArtifactRandom.Reset();

        int treasure = CountTier(ArtifactLevel.Treasure, MapVersion.SuccessionWars);
        var picks = new HashSet<ArtifactType>();

        for (int pick = 0; pick < treasure; ++pick)
        {
            picks.Add(ArtifactRandom.Next(ArtifactLevel.Treasure, MapVersion.SuccessionWars));
        }

        Assert.Contains(ArtifactType.GoldenBow, picks);
    }

    [Fact]
    public void TheExpansionArtifactsOnlyTakePartOnTheLaterMapVersions()
    {
        // SPADE_NECROMANCY is a loyalty artifact: on a .mp2 map its level carries no tier bit, so the random
        // picker of the treasure tier can never return it.
        Assert.Equal(ArtifactLevel.Loyalty, ArtifactLevels.Level(ArtifactType.SpadeNecromancy, MapVersion.SuccessionWars));

        int loyaltyPicksOnOldMap = 0;

        for (int pick = 0; pick < 300; ++pick)
        {
            if (ArtifactLevels.IsPriceOfLoyalty(ArtifactRandom.Next(ArtifactLevel.AllNormal, MapVersion.SuccessionWars)))
            {
                ++loyaltyPicksOnOldMap;
            }
        }

        Assert.Equal(0, loyaltyPicksOnOldMap);
    }

    [Fact]
    public void TheLoyaltyTierPicksOnlyTheExpansionArtifacts()
    {
        for (int pick = 0; pick < 20; ++pick)
        {
            ArtifactType artifact = ArtifactRandom.Next(ArtifactLevel.Loyalty, MapVersion.SuccessionWars);

            Assert.True(ArtifactLevels.IsPriceOfLoyalty(artifact), $"{artifact} is not an expansion artifact.");
        }
    }

    /// <summary>How many artifacts carry the tier on the version.</summary>
    /// <param name="level">The tier to count.</param>
    /// <param name="version">The map version.</param>
    /// <returns>The number of candidates.</returns>
    private static int CountTier(ArtifactLevel level, MapVersion version)
    {
        int count = 0;

        foreach (ArtifactType artifact in Enum.GetValues<ArtifactType>().Distinct())
        {
            if (ArtifactTypes.IsValid(artifact) && (ArtifactLevels.Level(artifact, version) & level) != 0)
            {
                ++count;
            }
        }

        return count;
    }
}
