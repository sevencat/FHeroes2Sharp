using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// The size classes of the scenario info window, <c>Maps::SizeString()</c> (<c>maps.cpp:208-224</c>).
/// </summary>
public sealed class MapSizeNameTests
{
    [Theory]
    [InlineData(36, "Small")]
    [InlineData(72, "Medium")]
    [InlineData(108, "Large")]
    [InlineData(144, "Extra Large")]
    public void TheFourClassesHaveNames(int width, string expected)
    {
        Assert.Equal(expected, MapSizes.GetName(width));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(100)]
    [InlineData(-1)]
    public void AnyOtherEdgeIsACustomSize(int width)
    {
        // 地图编辑器与战役做出来的图常常不是四档之一，参考把它们一律叫「自定义尺寸」。
        Assert.Equal("Custom Size", MapSizes.GetName(width));
    }

    [Fact]
    public void TheNameFollowsTheEnumerationAndNotTheOtherWayRound()
    {
        // 36/72/108/144 是四档的边长，换算成枚举之后仍然是同一档。
        Assert.Equal(MapSize.Small, MapSizes.FromWidth(36));
        Assert.Equal("Small", MapSizes.GetName(MapSizes.EdgeLength(MapSize.Small)));
        Assert.Equal("Extra Large", MapSizes.GetName(MapSizes.EdgeLength(MapSize.ExtraLarge)));
    }
}
