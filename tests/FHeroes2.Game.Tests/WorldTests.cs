using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the <c>World</c> skeleton: the dimensions, the tile vector, the two tile accessors, the
/// uninitialized-map generator and <c>Reset()</c>
/// (<c>src/fheroes2/world/world.h:197</c>, <c>world.cpp:299</c> / <c>:358</c>).
/// </summary>
public sealed class WorldTests
{
    [Fact]
    public void AFreshWorldIsEmpty()
    {
        var world = new World();

        Assert.Equal(0, world.Width);
        Assert.Equal(0, world.Height);
        Assert.Equal(0, world.Size);
        Assert.Empty(world.Tiles);
        Assert.Null(world.Geometry);
    }

    [Fact]
    public void CurrentIsTheSharedWorld()
    {
        Assert.NotNull(World.Current);
        Assert.Same(World.Current, World.Current);
    }

    [Fact]
    public void GenerateUninitializedMapCreatesASquareGridOfDefaultTiles()
    {
        var world = new World();

        world.GenerateUninitializedMap(5);

        Assert.Equal(5, world.Width);
        Assert.Equal(5, world.Height);
        Assert.Equal(25, world.Size);
        Assert.Equal(25, world.Tiles.Count);

        // Every tile is its own object with the reference's default values.
        Assert.Equal(25, world.Tiles.Distinct().Count());

        foreach (Tile tile in world.Tiles)
        {
            Assert.Equal(Mp2ObjectType.None, tile.MainObjectType);
            Assert.Equal(Directions.All, tile.PassableDirections);
            Assert.True(tile.IsWater);
        }
    }

    [Fact]
    public void GenerateUninitializedMapLeavesTheTileIndexesAtZero()
    {
        var world = new World();

        world.GenerateUninitializedMap(4);

        // The reference resizes the vector and stops; the loader is the one that calls setIndex
        // (world_loadmap.cpp:279). A port that filled the indexes here would hide a loader that forgot.
        Assert.All(world.Tiles, tile => Assert.Equal(0, tile.Index));
    }

    [Fact]
    public void GeometryFollowsTheDimensions()
    {
        var world = new World();

        world.GenerateUninitializedMap(6);

        MapGeometry geometry = Assert.IsType<MapGeometry>(world.Geometry);

        Assert.Equal(6, geometry.Width);
        Assert.Equal(6, geometry.Height);
        Assert.Equal(36, geometry.TileCount);
    }

    [Fact]
    public void GetTileByCoordinatesMatchesTheIndexedTile()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        // 4 × 4 map: (2, 1) is index 6, the reference's y * width + x.
        Assert.Same(world.GetTile(6), world.GetTile(2, 1));
        Assert.Same(world.Tiles[9], world.GetTile(1, 2));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void GetTileOutsideTheVectorThrows(int tileIndex)
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.GetTile(tileIndex));
    }

    [Fact]
    public void GetTileByCoordinatesDoesNotValidateThem()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        // The reference only rejects the computed index (y * width + x), never the two coordinates:
        // x == width silently lands on the first tile of the next row, and a negative x wraps back.
        Assert.Same(world.GetTile(4), world.GetTile(4, 0));
        Assert.Same(world.GetTile(7), world.GetTile(-1, 2));
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(0, 4)]
    [InlineData(-1, 0)]
    public void GetTileOutsideTheGridThrows(int x, int y)
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.GetTile(x, y));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void GenerateUninitializedMapRejectsANonPositiveSize(int size)
    {
        var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.GenerateUninitializedMap(size));
    }

    [Fact]
    public void GenerateUninitializedMapStartsFromAnEmptyWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(5);
        world.GetTile(0).PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 255));

        world.GenerateUninitializedMap(3);

        Assert.Equal(3, world.Width);
        Assert.Equal(9, world.Size);
        Assert.All(world.Tiles, tile => Assert.Equal(Mp2ObjectType.None, tile.MainObjectType));
        Assert.All(world.Tiles, tile => Assert.Empty(tile.GroundObjectParts));
    }

    [Fact]
    public void ResetEmptiesTheWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(5);

        world.Reset();

        Assert.Equal(0, world.Width);
        Assert.Equal(0, world.Height);
        Assert.Empty(world.Tiles);
        Assert.Null(world.Geometry);
    }

    [Fact]
    public void AMapWithoutObelisksStillNeedsSixPuzzlePieces()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        // CountObeliskOnMaps() clamps to six so that the puzzle can always be solved (world.cpp:900).
        Assert.Equal(6u, world.CountObeliskOnMaps());
    }

    [Fact]
    public void ObelisksAreCountedOnTheMap()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);
        world.GetTile(0).SetMainObjectType(Mp2ObjectType.Obelisk);
        world.GetTile(5).SetMainObjectType(Mp2ObjectType.Obelisk);
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Mine);

        Assert.Equal(2u, world.CountObeliskOnMaps());
    }
}
