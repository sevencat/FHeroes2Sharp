using FHeroes2.Assets.Maps;
using Xunit;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers the object classification predicates of <c>mp2.cpp</c> (<c>:505-813</c>).
/// </summary>
public sealed class Mp2ObjectTypesClassificationTests
{
    [Fact]
    public void OnlyTheMagicWellIsRefilledDaily()
    {
        Assert.True(Mp2ObjectTypes.IsDayLife(Mp2ObjectType.ActionMagicWell));
        Assert.False(Mp2ObjectTypes.IsDayLife(Mp2ObjectType.ActionWindmill));
        Assert.False(Mp2ObjectTypes.IsDayLife(Mp2ObjectType.ActionWaterWheel));
        Assert.False(Mp2ObjectTypes.IsDayLife(Mp2ObjectType.ActionArtifact));
    }

    [Theory]
    [InlineData(Mp2ObjectType.ActionArtifact)]
    [InlineData(Mp2ObjectType.ActionMine)]
    [InlineData(Mp2ObjectType.ActionCastle)]
    [InlineData(Mp2ObjectType.ActionMonster)]
    [InlineData(Mp2ObjectType.ActionStoneLiths)]
    public void ActionObjectsTheGameKnowsAreVisitable(Mp2ObjectType type) =>
        Assert.True(Mp2ObjectTypes.IsInGameActionObject(type));

    [Fact]
    public void TheEventIsTheOneActionObjectThatIsNotVisitable()
    {
        // The event carries the action bit because the original editor reused the object id, but a hero
        // never triggers it by walking onto the tile.
        Assert.True(Mp2ObjectTypes.IsActionObject(Mp2ObjectType.ActionEvent));
        Assert.False(Mp2ObjectTypes.IsInGameActionObject(Mp2ObjectType.ActionEvent));
    }

    [Fact]
    public void ATerrainDecorationWithTheActionBitIsNotVisitable()
    {
        // Some editors store the action twin of a decoration; the base type has no action counterpart, so
        // the object stays unvisitable.
        Assert.True(Mp2ObjectTypes.IsActionObject(Mp2ObjectType.ActionTrees));
        Assert.False(Mp2ObjectTypes.IsInGameActionObject(Mp2ObjectType.ActionTrees));
    }

    [Fact]
    public void ANonActionObjectIsNeverVisitable()
    {
        Assert.False(Mp2ObjectTypes.IsActionObject(Mp2ObjectType.Artifact));
        Assert.False(Mp2ObjectTypes.IsInGameActionObject(Mp2ObjectType.Artifact));
    }

    [Fact]
    public void ACoastIsAWaterObjectEvenThoughTheGenericTestRejectsIt()
    {
        // The reference lists the coast in the water list precisely because the generic test rejects it:
        // a coast is not an in-game action object, but it is the one tile a hero lands on.
        Assert.True(Mp2ObjectTypes.IsWaterActionObject(Mp2ObjectType.ActionCoast));
        Assert.False(Mp2ObjectTypes.IsInGameActionObject(Mp2ObjectType.ActionCoast));
    }

    [Fact]
    public void ABoatAndACastleAreNotWaterObjects()
    {
        Assert.False(Mp2ObjectTypes.IsWaterActionObject(Mp2ObjectType.ActionBoat));
        Assert.False(Mp2ObjectTypes.IsWaterActionObject(Mp2ObjectType.ActionCastle));
    }

    [Fact]
    public void EverythingElseFallsBackToTheGenericTest()
    {
        // The default branch of the reference is isInGameActionObject(): a land object placed on water by
        // an editor is considered reachable from a boat rather than ignored. That is not a mistake, it is
        // how the original maps with a mine on a one tile island work.
        Assert.True(Mp2ObjectTypes.IsWaterActionObject(Mp2ObjectType.ActionMine));
        Assert.True(Mp2ObjectTypes.IsInGameActionObject(Mp2ObjectType.ActionMine, true));

        // A decoration is neither.
        Assert.False(Mp2ObjectTypes.IsWaterActionObject(Mp2ObjectType.Trees));
    }

    [Fact]
    public void AWhirlpoolRefusesARevisitFromEitherSide()
    {
        // isRevisitAllowedForObject() is the generic test with the whirlpool subtracted; the from-water
        // flag only changes which generic test is used, not the subtraction.
        Assert.False(Mp2ObjectTypes.IsRevisitAllowedForObject(Mp2ObjectType.ActionWhirlpool, false));
        Assert.False(Mp2ObjectTypes.IsRevisitAllowedForObject(Mp2ObjectType.ActionWhirlpool, true));

        Assert.True(Mp2ObjectTypes.IsRevisitAllowedForObject(Mp2ObjectType.ActionMine, false));

        // Not an action object at all: nothing to revisit.
        Assert.False(Mp2ObjectTypes.IsRevisitAllowedForObject(Mp2ObjectType.Artifact, false));
    }

    [Theory]
    [InlineData(Mp2ObjectType.Barrel)]
    [InlineData(Mp2ObjectType.ActionCampfire)]
    [InlineData(Mp2ObjectType.ActionFlotsam)]
    [InlineData(Mp2ObjectType.ActionWagon)]
    [InlineData(Mp2ObjectType.ActionWindmill)]
    public void TheObjectsWithARewardInTheirMetadataAreValuable(Mp2ObjectType type) =>
        Assert.True(Mp2ObjectTypes.IsValuableResourceObject(type));

    [Fact]
    public void ThePickupObjectsAreValuableToo()
    {
        // The valuable list ends in isPickupObject(): a pile of resources holds nothing in the lists above
        // but is obviously worth a detour.
        Assert.True(Mp2ObjectTypes.IsPickupObject(Mp2ObjectType.ActionResource));
        Assert.True(Mp2ObjectTypes.IsValuableResourceObject(Mp2ObjectType.ActionResource));
        Assert.False(Mp2ObjectTypes.IsValuableResourceObject(Mp2ObjectType.ActionMine));
    }

    [Fact]
    public void TheCaptureObjectsAreTheOnesTheWorldTracksAndTreasuresDoNot()
    {
        Assert.True(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionMine));
        Assert.True(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionSawmill));
        Assert.True(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionAlchemistLab));
        Assert.True(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionLighthouse));
        Assert.True(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionCastle));

        Assert.False(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionTreasureChest));
        Assert.False(Mp2ObjectTypes.IsCaptureObject(Mp2ObjectType.ActionArtifact));
    }

    [Fact]
    public void APickedUpObjectVanshesAndACapturedOneStays()
    {
        Assert.True(Mp2ObjectTypes.IsPickupObject(Mp2ObjectType.ActionArtifact));
        Assert.True(Mp2ObjectTypes.IsPickupObject(Mp2ObjectType.ActionTreasureChest));
        Assert.True(Mp2ObjectTypes.IsPickupObject(Mp2ObjectType.Barrel));

        Assert.False(Mp2ObjectTypes.IsPickupObject(Mp2ObjectType.ActionMine));
        Assert.False(Mp2ObjectTypes.IsPickupObject(Mp2ObjectType.ActionWindmill));
    }

    [Fact]
    public void TheArtifactObjectsAreTheOnesTheMetadataReaderUnderstands()
    {
        Assert.True(Mp2ObjectTypes.IsArtifactObject(Mp2ObjectType.ActionArtifact));
        Assert.True(Mp2ObjectTypes.IsArtifactObject(Mp2ObjectType.ActionTreasureChest));
        Assert.True(Mp2ObjectTypes.IsArtifactObject(Mp2ObjectType.ActionSkeleton));
        Assert.True(Mp2ObjectTypes.IsArtifactObject(Mp2ObjectType.ActionWagon));

        Assert.False(Mp2ObjectTypes.IsArtifactObject(Mp2ObjectType.ActionWindmill));
        Assert.False(Mp2ObjectTypes.IsArtifactObject(Mp2ObjectType.ActionMine));
    }

    [Fact]
    public void AnArtifactOrAGuardianForcesTheBattle()
    {
        // The reference asks this to decide whether the "flee" button may be offered.
        Assert.True(Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected(Mp2ObjectType.ActionArtifact));
        Assert.True(Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected(Mp2ObjectType.ActionMonster));

        // A capture object is added on top of the reference list: its garrison has to be beaten.
        Assert.True(Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected(Mp2ObjectType.ActionMine));

        Assert.False(Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected(Mp2ObjectType.ActionWindmill));
        Assert.False(Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected(Mp2ObjectType.ActionSign));
    }

    [Fact]
    public void TheTeleportsAndSignsAreSafeForFogDiscovery()
    {
        Assert.True(Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Mp2ObjectType.ActionStoneLiths));
        Assert.True(Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Mp2ObjectType.ActionWhirlpool));
        Assert.True(Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Mp2ObjectType.ActionSign));

        // Everything else that a hero can visit may be guarded or may cost something.
        Assert.False(Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Mp2ObjectType.ActionMine));
        Assert.False(Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Mp2ObjectType.ActionMonster));

        // An ordinary decoration is not an action object, so it is safe.
        Assert.True(Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Mp2ObjectType.Trees));
    }

    [Theory]
    [InlineData(Mp2ObjectType.ActionBarrier)]
    [InlineData(Mp2ObjectType.ActionBoat)]
    [InlineData(Mp2ObjectType.ActionBuoy)]
    [InlineData(Mp2ObjectType.ActionJail)]
    [InlineData(Mp2ObjectType.ActionMermaid)]
    [InlineData(Mp2ObjectType.ActionMonster)]
    [InlineData(Mp2ObjectType.ActionShipwreck)]
    [InlineData(Mp2ObjectType.ActionSirens)]
    [InlineData(Mp2ObjectType.ActionSkeleton)]
    public void TheObjectsThatNeedTheTileInFrontOfThemAreListed(Mp2ObjectType type) =>
        Assert.True(Mp2ObjectTypes.IsNeedStayFront(type));

    [Fact]
    public void ThePickupObjectsAreVisitedOnApproach()
    {
        // isNeedStayFront() ends in isPickupObject(): the reward of a chest is collected without standing
        // on its tile.
        Assert.True(Mp2ObjectTypes.IsNeedStayFront(Mp2ObjectType.ActionTreasureChest));
        Assert.True(Mp2ObjectTypes.IsNeedStayFront(Mp2ObjectType.ActionArtifact));
        Assert.True(Mp2ObjectTypes.IsNeedStayFront(Mp2ObjectType.Barrel));

        Assert.False(Mp2ObjectTypes.IsNeedStayFront(Mp2ObjectType.ActionMine));
        Assert.False(Mp2ObjectTypes.IsNeedStayFront(Mp2ObjectType.ActionWindmill));
        Assert.False(Mp2ObjectTypes.IsNeedStayFront(Mp2ObjectType.Trees));
    }
}
