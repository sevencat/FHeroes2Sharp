using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the object metadata readers and writers of a tile (<c>maps_tiles_helper.cpp:570-599</c>,
/// <c>:719-761</c>, <c>:774-854</c>).
/// </summary>
public sealed class TileMetadataTests
{
    [Fact]
    public void AMineStoresItsResourceInTheFirstTwoLanes()
    {
        Tile tile = MakeTile(Mp2ObjectType.Mine, out _);

        tile.SetResourceOnTile(Resource.Gems, 4);

        Assert.Equal((uint)Resource.Gems, tile.Metadata[0]);
        Assert.Equal(4u, tile.Metadata[1]);
    }

    [Fact]
    public void ACampfirePaysItsResourceAndAHundredGoldPerUnit()
    {
        Tile tile = MakeTile(Mp2ObjectType.Campfire, out World world);
        tile.Metadata[0] = Resource.Ore;
        tile.Metadata[1] = 3;

        Funds funds = tile.GetFundsFromTile(world);

        Assert.Equal(3, funds.Ore);
        Assert.Equal(300, funds.Gold);
    }

    [Fact]
    public void AFlotsamHoldsWoodAndGold()
    {
        Tile tile = MakeTile(Mp2ObjectType.Flotsam, out World world);
        tile.Metadata[0] = 5;
        tile.Metadata[1] = 500;

        Funds funds = tile.GetFundsFromTile(world);

        Assert.Equal(5, funds.Wood);
        Assert.Equal(500, funds.Gold);
    }

    [Fact]
    public void ATreasureChestKeepsItsGoldInTheSecondLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.TreasureChest, out World world);
        tile.Metadata[0] = (uint)ArtifactType.UltimateSword;
        tile.Metadata[1] = 1500;

        Funds funds = tile.GetFundsFromTile(world);

        Assert.Equal(1500, funds.Gold);
        Assert.Equal(0, funds.Wood);
    }

    [Fact]
    public void AWagonKeepsTheResourceTypeAndItsAmountInTheLastTwoLanes()
    {
        Tile tile = MakeTile(Mp2ObjectType.Wagon, out World world);
        tile.Metadata[0] = (uint)ArtifactType.UltimateSword;
        tile.Metadata[1] = Resource.Wood;
        tile.Metadata[2] = 750;

        Funds funds = tile.GetFundsFromTile(world);

        Assert.Equal(750, funds.Wood);
        Assert.Equal(0, funds.Gold);
    }

    [Fact]
    public void APileOfResourcesReadsItsTwoLanes()
    {
        Tile tile = MakeTile(Mp2ObjectType.Resource, out World world);
        tile.Metadata[0] = Resource.Crystal;
        tile.Metadata[1] = 7;

        Funds funds = tile.GetFundsFromTile(world);

        Assert.Equal(7, funds.Crystal);
        Assert.Equal(0, funds.Gold);
    }

    [Fact]
    public void AnArtifactWithTheSpellConditionIsASpellScroll()
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[0] = (uint)ArtifactType.UltimateSword;
        tile.Metadata[1] = 5;
        tile.Metadata[2] = (uint)ArtifactCaptureCondition.ContainsSpell;

        Assert.Equal(ArtifactType.SpellScroll, tile.GetArtifactFromTile(world));
    }

    [Fact]
    public void APlainArtifactReadsTheFirstLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[0] = (uint)ArtifactType.UltimateSword;
        tile.Metadata[2] = (uint)ArtifactCaptureCondition.Fight1Genie;

        Assert.Equal(ArtifactType.UltimateSword, tile.GetArtifactFromTile(world));
    }

    [Fact]
    public void AChestWithoutAnArtifactAnswersUnknown()
    {
        Tile tile = MakeTile(Mp2ObjectType.TreasureChest, out World world);

        Assert.Equal(ArtifactType.Unknown, tile.GetArtifactFromTile(world));
    }

    [Fact]
    public void AVisitedWindmillHoldsNothing()
    {
        Tile tile = MakeTile(Mp2ObjectType.Windmill, out World world);
        tile.Metadata[1] = 1;

        Assert.True(tile.DoesTileContainValuableItems(world));

        tile.Metadata[1] = 0;

        Assert.False(tile.DoesTileContainValuableItems(world));
    }

    [Fact]
    public void APyramidIsValuableUntilItsSpellIsTaken()
    {
        Tile tile = MakeTile(Mp2ObjectType.Pyramid, out World world);
        tile.Metadata[0] = 12;

        Assert.True(tile.DoesTileContainValuableItems(world));

        tile.ResetObjectMetadata();

        Assert.False(tile.DoesTileContainValuableItems(world));
    }

    [Fact]
    public void ObjectsThatCarryTheirRewardInTheirTypeAreAlwaysWorthAVisit()
    {
        // The first group of the reference answers true regardless of its lanes: the object type itself
        // is the reward, and visiting it changes the type.
        foreach (Mp2ObjectType type in new[]
                 {
                     Mp2ObjectType.Artifact, Mp2ObjectType.Barrel, Mp2ObjectType.Campfire, Mp2ObjectType.Flotsam,
                     Mp2ObjectType.Resource, Mp2ObjectType.SeaChest, Mp2ObjectType.ShipwreckSurvivor,
                     Mp2ObjectType.TreasureChest,
                 })
        {
            Tile tile = MakeTile(type, out World world);

            Assert.True(tile.DoesTileContainValuableItems(world), $"{type} should still be worth a visit.");
        }
    }

    [Fact]
    public void ABarrierKeepsTheColorThatOpensIt()
    {
        Tile tile = MakeTile(Mp2ObjectType.Barrier, out World world);

        tile.SetBarrierColorOnTile(world, 3);

        Assert.Equal(3, tile.GetBarrierColorFromTile(world));
    }

    [Fact]
    public void ATravellerTentKeepsTheColorThatOpensIt()
    {
        Tile tile = MakeTile(Mp2ObjectType.TravellerTent, out World world);

        tile.SetBarrierColorOnTile(world, 5);

        Assert.Equal(5, tile.GetBarrierColorFromTile(world));
        Assert.Equal(5u, tile.Metadata[0]);
    }

    [Fact]
    public void ResettingTheMetadataZeroesEveryLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.Wagon, out _);
        tile.Metadata[0] = 1;
        tile.Metadata[1] = 2;
        tile.Metadata[2] = 3;

        tile.ResetObjectMetadata();

        Assert.All(tile.Metadata, lane => Assert.Equal(0u, lane));
    }

    private static Tile MakeTile(Mp2ObjectType type, out World world)
    {
        world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(5);
        tile.SetMainObjectType(type);

        return tile;
    }
}
