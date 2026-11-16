using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the five «money or artifact» pickups: loose resources, campfires and barrels
/// (<c>ActionToPickupResource</c>, <c>heroes_action.cpp:752</c>), skeletons (<c>:866</c>), wagons
/// (<c>:911</c>), flotsam (<c>:964</c>) and shipwreck survivors (<c>:1570</c>).
/// </summary>
public sealed class HeroPickupTests
{
    /// <summary>A blue kingdom with a knight and one object on tile 0, with the given metadata lanes.</summary>
    private static HeroActions PickupActions(
        out PlayerKingdom kingdom,
        out World world,
        out Hero hero,
        Mp2ObjectType objectType,
        params uint[] metadata)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        hero = new Hero(1, MapRace.Knight);
        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);

        Tile tile = world.GetTile(0);

        // Tile 0 of a fresh map is water; keep every pickup on land unless a test says otherwise.
        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetMainObjectType(objectType);

        for (int lane = 0; lane < metadata.Length; ++lane)
        {
            tile.Metadata[lane] = metadata[lane];
        }

        return new HeroActions(hero, kingdoms);
    }

    private static void FillBag(Hero hero)
    {
        for (int index = 1; index <= ArtifactBag.MaxCapacity; ++index)
        {
            Assert.True(hero.BagArtifacts.Push((ArtifactType)index));
        }

        Assert.True(hero.BagArtifacts.IsFull);
    }

    [Fact]
    public void ALooseResourceGoesToTheTreasury()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out _,
            Mp2ObjectType.Resource,
            Resource.Wood,
            7);

        int before = kingdom.Treasury.Wood;
        Funds funds = actions.ActionToPickupResource(0, world);

        Assert.Equal(7, funds.Wood);
        Assert.Equal(before + 7, kingdom.Treasury.Wood);
        Assert.Equal(0u, world.GetTile(0).Metadata[0]);
    }

    [Fact]
    public void ACampfirePaysTheResourceAndItsWeightInGold()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out _,
            Mp2ObjectType.Campfire,
            Resource.Sulfur,
            2);

        int before = kingdom.Treasury.Gold;
        Funds funds = actions.ActionToPickupResource(0, world);

        Assert.Equal(2, funds.Sulfur);
        Assert.Equal(200, funds.Gold);
        Assert.Equal(before + 200, kingdom.Treasury.Gold);
    }

    [Fact]
    public void ABarrelPaysLikeACampfire()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out _,
            Mp2ObjectType.Barrel,
            Resource.Crystal,
            3);

        int before = kingdom.Treasury.Gold;
        Funds funds = actions.ActionToPickupResource(0, world);

        Assert.Equal(3, funds.Crystal);
        Assert.Equal(300, funds.Gold);
        Assert.Equal(before + 300, kingdom.Treasury.Gold);
    }

    [Fact]
    public void AResourcePickupIsNotRecordedInTheKingdom()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out _,
            Mp2ObjectType.Campfire,
            Resource.Gems,
            1);

        actions.ActionToPickupResource(0, world);

        Assert.False(kingdom.IsVisited(0, Mp2ObjectType.Campfire));
    }

    [Fact]
    public void ASkeletonGivesItsArtifact()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out Hero hero,
            Mp2ObjectType.Skeleton,
            (uint)ArtifactType.UltimateSword);

        ObjectPickupReward reward = actions.ActionToSkeleton(0, world);

        Assert.Equal(ArtifactType.UltimateSword, reward.Artifact);
        Assert.True(reward.Funds.GetValidItemsCount() == 0);
        Assert.True(hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
        Assert.Equal(0u, world.GetTile(0).Metadata[0]);
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.Skeleton));
    }

    [Fact]
    public void ASkeletonWithAFullBagPaysAGrand()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out Hero hero,
            Mp2ObjectType.Skeleton,
            (uint)ArtifactType.UltimateSword);

        FillBag(hero);

        int before = kingdom.Treasury.Gold;
        ObjectPickupReward reward = actions.ActionToSkeleton(0, world);

        Assert.Equal(1000, reward.Funds.Gold);
        Assert.Equal(ArtifactType.Unknown, reward.Artifact);
        Assert.Equal(before + 1000, kingdom.Treasury.Gold);
    }

    [Fact]
    public void AnEmptiedSkeletonGivesNothingButIsRecorded()
    {
        HeroActions actions = PickupActions(out PlayerKingdom kingdom, out World world, out _, Mp2ObjectType.Skeleton);

        ObjectPickupReward reward = actions.ActionToSkeleton(0, world);

        Assert.True(reward.IsEmpty);
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.Skeleton));
    }

    [Fact]
    public void AWagonPrefersItsArtifactToItsCargo()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out Hero hero,
            Mp2ObjectType.Wagon,
            (uint)ArtifactType.UltimateSword,
            Resource.Wood,
            5);

        int before = kingdom.Treasury.Wood;
        ObjectPickupReward reward = actions.ActionToWagon(0, world);

        Assert.Equal(ArtifactType.UltimateSword, reward.Artifact);
        Assert.True(reward.Funds.GetValidItemsCount() == 0);
        Assert.True(hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
        Assert.Equal(before, kingdom.Treasury.Wood);
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.Wagon));
    }

    [Fact]
    public void AWagonGivesItsCargoWhenItHasNoArtifact()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out _,
            Mp2ObjectType.Wagon,
            0,
            Resource.Gems,
            4);

        int before = kingdom.Treasury.Gems;
        ObjectPickupReward reward = actions.ActionToWagon(0, world);

        Assert.Equal(ArtifactType.Unknown, reward.Artifact);
        Assert.Equal(4, reward.Funds.Gems);
        Assert.Equal(before + 4, kingdom.Treasury.Gems);
    }

    [Fact]
    public void AFullBagEmptiesTheWagonWithoutPaying()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out Hero hero,
            Mp2ObjectType.Wagon,
            (uint)ArtifactType.UltimateSword);

        FillBag(hero);

        ObjectPickupReward reward = actions.ActionToWagon(0, world);

        // No gold conversion: the reference only says the wagon is empty and clears it anyway.
        Assert.True(reward.IsEmpty);
        Assert.Equal(0u, world.GetTile(0).Metadata[0]);
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.Wagon));
    }

    [Fact]
    public void FlotsamPaysWoodAndGold()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out _,
            Mp2ObjectType.Flotsam,
            3,
            250);

        int beforeWood = kingdom.Treasury.Wood;
        int beforeGold = kingdom.Treasury.Gold;

        Funds funds = actions.ActionToFlotsam(0, world);

        Assert.Equal(3, funds.Wood);
        Assert.Equal(250, funds.Gold);
        Assert.Equal(beforeWood + 3, kingdom.Treasury.Wood);
        Assert.Equal(beforeGold + 250, kingdom.Treasury.Gold);
        Assert.False(kingdom.IsVisited(0, Mp2ObjectType.Flotsam));
    }

    [Fact]
    public void AnEmptiedFlotsamPaysNothing()
    {
        HeroActions actions = PickupActions(out PlayerKingdom kingdom, out World world, out _, Mp2ObjectType.Flotsam);

        int before = kingdom.Treasury.Gold;
        Funds funds = actions.ActionToFlotsam(0, world);

        Assert.Equal(0u, funds.GetValidItemsCount());
        Assert.Equal(before, kingdom.Treasury.Gold);
        Assert.Equal(0u, world.GetTile(0).Metadata[0]);
    }

    [Fact]
    public void AShipwreckSurvivorGivesHisArtifact()
    {
        HeroActions actions = PickupActions(
            out _,
            out World world,
            out Hero hero,
            Mp2ObjectType.ShipwreckSurvivor,
            (uint)ArtifactType.UltimateSword);

        ObjectPickupReward reward = actions.ActionToShipwreckSurvivor(0, world);

        Assert.Equal(ArtifactType.UltimateSword, reward.Artifact);
        Assert.True(hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
        Assert.Equal(0u, world.GetTile(0).Metadata[0]);
    }

    [Fact]
    public void AShipwreckSurvivorWithAFullBagPaysAGrand()
    {
        HeroActions actions = PickupActions(
            out PlayerKingdom kingdom,
            out World world,
            out Hero hero,
            Mp2ObjectType.ShipwreckSurvivor,
            (uint)ArtifactType.UltimateSword);

        FillBag(hero);

        int before = kingdom.Treasury.Gold;
        ObjectPickupReward reward = actions.ActionToShipwreckSurvivor(0, world);

        Assert.Equal(1000, reward.Funds.Gold);
        Assert.Equal(ArtifactType.Unknown, reward.Artifact);
        Assert.Equal(before + 1000, kingdom.Treasury.Gold);
    }
}
