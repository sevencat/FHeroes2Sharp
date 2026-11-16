using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the market place rate table of <c>resource_trading.cpp</c> (<see cref="ResourceTrading"/>).
/// </summary>
public sealed class ResourceTradingTests
{
    [Fact]
    public void OneMarketplaceTradesAtTheFirstColumn()
    {
        // resource_trading.cpp:80-90 with marketplaceCount == 1: the eight category pairs.
        Assert.Equal(10u, ResourceTrading.GetTradeCost(1, Resource.Wood, Resource.Ore));
        Assert.Equal(20u, ResourceTrading.GetTradeCost(1, Resource.Wood, Resource.Mercury));
        Assert.Equal(25u, ResourceTrading.GetTradeCost(1, Resource.Wood, Resource.Gold));
        Assert.Equal(5u, ResourceTrading.GetTradeCost(1, Resource.Mercury, Resource.Ore));
        Assert.Equal(10u, ResourceTrading.GetTradeCost(1, Resource.Mercury, Resource.Gems));
        Assert.Equal(50u, ResourceTrading.GetTradeCost(1, Resource.Mercury, Resource.Gold));
        Assert.Equal(2500u, ResourceTrading.GetTradeCost(1, Resource.Gold, Resource.Wood));
        Assert.Equal(5000u, ResourceTrading.GetTradeCost(1, Resource.Gold, Resource.Sulfur));
    }

    [Fact]
    public void NineMarketplacesTradesAtTheLastColumn()
    {
        Assert.Equal(2u, ResourceTrading.GetTradeCost(9, Resource.Wood, Resource.Ore));
        Assert.Equal(4u, ResourceTrading.GetTradeCost(9, Resource.Wood, Resource.Gems));
        Assert.Equal(124u, ResourceTrading.GetTradeCost(9, Resource.Ore, Resource.Gold));
        Assert.Equal(1u, ResourceTrading.GetTradeCost(9, Resource.Crystal, Resource.Wood));
        Assert.Equal(249u, ResourceTrading.GetTradeCost(9, Resource.Gems, Resource.Gold));
        Assert.Equal(500u, ResourceTrading.GetTradeCost(9, Resource.Gold, Resource.Wood));
        Assert.Equal(1000u, ResourceTrading.GetTradeCost(9, Resource.Gold, Resource.Crystal));
    }

    [Fact]
    public void WithinACategoryTheRateIsTheSameInBothDirections()
    {
        // The two within-category pairs share one table (resource_trading.cpp:46-48): 10..2 for every count.
        for (uint count = 1; count <= 9; ++count)
        {
            uint common = ResourceTrading.GetTradeCost(count, Resource.Wood, Resource.Ore);

            Assert.Equal(common, ResourceTrading.GetTradeCost(count, Resource.Ore, Resource.Wood));
            Assert.Equal(common, ResourceTrading.GetTradeCost(count, Resource.Mercury, Resource.Gems));
            Assert.Equal(common, ResourceTrading.GetTradeCost(count, Resource.Sulfur, Resource.Crystal));
            Assert.Equal(common, ResourceTrading.GetTradeCost(count, Resource.Crystal, Resource.Mercury));
        }
    }

    [Fact]
    public void MoreMarketplacesNeverGiveAWorseRate()
    {
        for (uint count = 2; count <= 9; ++count)
        {
            Assert.True(ResourceTrading.GetTradeCost(count, Resource.Wood, Resource.Ore)
                        <= ResourceTrading.GetTradeCost(count - 1, Resource.Wood, Resource.Ore));
            Assert.True(ResourceTrading.GetTradeCost(count, Resource.Mercury, Resource.Gold)
                        >= ResourceTrading.GetTradeCost(count - 1, Resource.Mercury, Resource.Gold));
            Assert.True(ResourceTrading.GetTradeCost(count, Resource.Gold, Resource.Mercury)
                        <= ResourceTrading.GetTradeCost(count - 1, Resource.Gold, Resource.Mercury));
        }
    }

    [Fact]
    public void MoreThanNineMarketplacesClampToNine()
    {
        // The reference clamps (resource_trading.cpp:72-74) instead of reading past the table.
        Assert.Equal(
            ResourceTrading.GetTradeCost(9, Resource.Wood, Resource.Gold),
            ResourceTrading.GetTradeCost(10, Resource.Wood, Resource.Gold));
        Assert.Equal(
            ResourceTrading.GetTradeCost(9, Resource.Gold, Resource.Gems),
            ResourceTrading.GetTradeCost(40, Resource.Gold, Resource.Gems));
    }

    [Fact]
    public void AMeaninglessTradeCostsNothing()
    {
        // The same resource twice, nothing selected, and no market at all (the reference asserts and returns 0).
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, Resource.Wood, Resource.Wood));
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, Resource.Unknown, Resource.Wood));
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, Resource.Wood, Resource.Unknown));
        Assert.Equal(0u, ResourceTrading.GetTradeCost(0, Resource.Wood, Resource.Gold));

        // A resource that is not one of the seven hits the reference's `default: assert( 0 )` and answers 0.
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, 0x4000, Resource.Gold));
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, Resource.Wood, 0x4000));
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, Resource.Mercury, 0x4000));
        Assert.Equal(0u, ResourceTrading.GetTradeCost(3, Resource.Gold, 0x4000));
    }
}
