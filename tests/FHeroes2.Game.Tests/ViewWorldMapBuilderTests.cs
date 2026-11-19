using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The production block drawer of the View World cache, <c>CacheForMapWithResources</c> of
/// <c>view_world.cpp:189-268</c>: the 18 tile blocks, the resize into every zoom level and the layers
/// the mode asks for.
/// </summary>
public sealed class ViewWorldMapBuilderTests
{
    private const int MapSize = 36;
    private const int TileSize = 32;

    [Fact]
    public void TheCacheHasOneImagePerZoomLevel()
    {
        ViewWorldMapCache cache = CreateBuilder().Build(ViewWorldMode.ViewAll);

        Assert.Equal(3, cache.ZoomLevelCount);
        Assert.Equal(MapSize * 4, cache.GetImage(ZoomLevel.ZoomLevel0).Width);
        Assert.Equal(MapSize * 6, cache.GetImage(ZoomLevel.ZoomLevel1).Width);
        Assert.Equal(MapSize * 12, cache.GetImage(ZoomLevel.ZoomLevel2).Width);
        Assert.Equal(MapSize * 12, cache.GetImage(ZoomLevel.ZoomLevel2).Height);
    }

    [Fact]
    public void EveryBlockIsPaintedInTheRightPlace()
    {
        // The tiles alternate between palette 1 and 2, so a block that was drawn at the wrong offset
        // or not at all shows up as the wrong colour at the four corners of the cached image.
        // The tiles alternate between terrain image 1 (palette 2) and 2 (palette 3), so a block that
        // was drawn at the wrong offset or not at all shows up as the wrong colour at the corners.
        PixelBuffer map = CreateBuilder().Build(ViewWorldMode.ViewAll).GetImage(ZoomLevel.ZoomLevel0);

        int last = (MapSize * 4) - 1;

        Assert.Equal(2, map[0, 0]);
        Assert.Equal(3, map[last, 0]);
        Assert.Equal(2, map[0, last]);
        Assert.Equal(3, map[last, last]);
    }

    [Fact]
    public void TheHeroesAreLeftOutOfTheCache()
    {
        // The reference switches LEVEL_HEROES off and draws the hero icons afterwards.
        Assert.Null(CreateBuilder().CreateView(ViewWorldMode.ViewAll).Heroes);
    }

    [Fact]
    public void TheWholeMapModeLeavesTheFogOut()
    {
        Assert.Null(CreateBuilder().CreateView(ViewWorldMode.ViewAll).Fog);
    }

    [Fact]
    public void TheOtherModesKeepTheFog()
    {
        Assert.NotNull(CreateBuilder().CreateView(ViewWorldMode.OnlyVisible).Fog);
        Assert.NotNull(CreateBuilder().CreateView(ViewWorldMode.ViewTowns).Fog);
        Assert.NotNull(CreateBuilder().CreateView(ViewWorldMode.ViewHeroes).Fog);
    }

    [Fact]
    public void TheViewIsAsBigAsOneBlock()
    {
        Assert.Equal(new Size(ViewWorldMapCache.BlockSizeInPixels, ViewWorldMapCache.BlockSizeInPixels), CreateBuilder().CreateView(ViewWorldMode.ViewAll).Viewport);
    }

    [Fact]
    public void TheModeItselfDecidesTheLayers()
    {
        Assert.True(CreateBuilder().Build(ViewWorldMode.ViewTowns).Layers.Towns);
        Assert.False(CreateBuilder().Build(ViewWorldMode.ViewAll).Layers.Towns);
        Assert.True(CreateBuilder().Build(ViewWorldMode.OnlyVisible).Layers.Fog);
    }

    [Fact]
    public void AMapThatIsNotMadeOfWholeBlocksIsRejected()
    {
        ViewWorldMapBuilder builder = new(SyntheticMap.Create(20, 20), SyntheticTerrain.Create(1, TileSize), LandWorld(36));

        Assert.Throws<ArgumentException>(() => builder.Build(ViewWorldMode.ViewAll));
    }

    /// <summary>A builder over a 36 tile map whose tiles alternate between palette 1 and 2.</summary>
    /// <returns>The builder.</returns>
    private static ViewWorldMapBuilder CreateBuilder()
    {
        Mp2Map map = SyntheticMap.Create(
            MapSize,
            MapSize,
            index => SyntheticMap.BlankTile() with { TerrainImageIndex = (ushort)((index % 2) + 1), TerrainFlags = 0 });

        // TerrainImageIndex is 1-based: it is the index of the image in the TIL, so entries 1 and 2
        // need an image file of three entries.
        return new ViewWorldMapBuilder(map, SyntheticTerrain.Create(3, TileSize), LandWorld(MapSize));
    }

    /// <summary>A world with the given side, which is where the fog comes from.</summary>
    /// <param name="size">Width and height in tiles.</param>
    /// <returns>The world.</returns>
    private static World LandWorld(int size)
    {
        var world = new World();
        world.GenerateUninitializedMap(size);

        return world;
    }
}
