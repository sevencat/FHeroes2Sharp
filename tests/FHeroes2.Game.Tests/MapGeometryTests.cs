using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the geometry half of <c>src/fheroes2/maps/maps.cpp</c>: index ↔ point conversion, the
/// direction queries and <c>getAroundIndexes</c>. The reference reads the dimensions from the global
/// <c>world</c>; here they come from the <see cref="MapGeometry"/> instance, so a 4x3 map is enough to
/// pin every border case.
/// </summary>
public sealed class MapGeometryTests
{
    private static MapGeometry Map => new(4, 3);

    [Fact]
    public void DimensionsHaveToBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapGeometry(0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapGeometry(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapGeometry(-1, 3));

        Assert.Equal(12, Map.TileCount);
        Assert.Equal(4, Map.Width);
        Assert.Equal(3, Map.Height);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 3, 0)]
    [InlineData(4, 0, 1)]
    [InlineData(7, 3, 1)]
    [InlineData(11, 3, 2)]
    public void IndexAndPointAreInverses(int index, int x, int y)
    {
        Assert.Equal(new Point(x, y), Map.GetPoint(index));
        Assert.Equal(index, Map.GetIndexFromAbsPoint(x, y));
        Assert.Equal(index, Map.GetIndexFromAbsPoint(new Point(x, y)));
    }

    [Fact]
    public void GetIndexFromAbsPointRejectsNegativeCoordinates()
    {
        // The reference returns -1 for a negative coordinate instead of asserting, because the map
        // frame code calls it with points that are one tile outside the viewport.
        Assert.Equal(-1, Map.GetIndexFromAbsPoint(-1, 0));
        Assert.Equal(-1, Map.GetIndexFromAbsPoint(0, -1));

        // A point to the right of the map is not rejected: the caller has to check the index.
        Assert.Equal(4, Map.GetIndexFromAbsPoint(4, 0));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(11, true)]
    [InlineData(-1, false)]
    [InlineData(12, false)]
    public void IsValidAbsIndexIsTheHalfOpenRange(int index, bool valid) => Assert.Equal(valid, Map.IsValidAbsIndex(index));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(4, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(-1, 0, false)]
    public void IsValidAbsPointIsTheRectangle(int x, int y, bool valid) => Assert.Equal(valid, Map.IsValidAbsPoint(x, y));

    [Theory]
    [InlineData(5, 0, Direction.TopLeft)]
    [InlineData(5, 1, Direction.Top)]
    [InlineData(5, 2, Direction.TopRight)]
    [InlineData(5, 4, Direction.Left)]
    [InlineData(5, 5, Direction.Center)]
    [InlineData(5, 6, Direction.Right)]
    [InlineData(5, 8, Direction.BottomLeft)]
    [InlineData(5, 9, Direction.Bottom)]
    [InlineData(5, 10, Direction.BottomRight)]
    public void GetDirectionFindsTheStep(int from, int to, Direction expected) => Assert.Equal(expected, Map.GetDirection(from, to));

    [Fact]
    public void GetDirectionIsUnknownAsSoonAsTheTilesAreNotNeighbours()
    {
        // The reference compares the index difference against the width instead of looking at x and y, so
        // a difference that is a step on another map size is not one here.
        Assert.Equal(Direction.Unknown, Map.GetDirection(5, 15));
        Assert.Equal(Direction.Unknown, Map.GetDirection(0, 11));
        Assert.Equal(Direction.Unknown, Map.GetDirection(0, 2));
    }

    [Fact]
    public void GetDirectionIndexIsTheInverseOfGetDirection()
    {
        foreach (Direction direction in Directions.AllNeighboringDirections)
        {
            int neighbour = Map.GetDirectionIndex(5, direction);

            Assert.Equal(direction, Map.GetDirection(5, neighbour));
        }

        // CENTER is not a step and has no index, exactly like the reference.
        Assert.Equal(-1, Map.GetDirectionIndex(5, Direction.Center));
        Assert.Equal(-1, Map.GetDirectionIndex(5, Direction.Unknown));
    }

    [Theory]
    [InlineData(Direction.TopLeft, 0)]
    [InlineData(Direction.Top, 1)]
    [InlineData(Direction.TopRight, 2)]
    [InlineData(Direction.Left, 4)]
    [InlineData(Direction.Right, 6)]
    [InlineData(Direction.BottomLeft, 8)]
    [InlineData(Direction.Bottom, 9)]
    [InlineData(Direction.BottomRight, 10)]
    [InlineData(Direction.Center, 5)]
    public void GetDirectionPointStepsOnThePointGrid(Direction direction, int expectedIndex) =>
        Assert.Equal(new Point(expectedIndex % 4, expectedIndex / 4), MapGeometry.GetDirectionPoint(new Point(1, 1), direction));

    [Fact]
    public void GetDirectionPointRejectsWhatIsNotADirection()
    {
        // The reference asserts and then returns (-1, 0); the port throws, because the value cannot be
        // produced by any caller that follows the enumeration.
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometry.GetDirectionPoint(new Point(1, 1), Direction.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometry.GetDirectionPoint(new Point(1, 1), Direction.Top | Direction.Left));
    }

    [Theory]
    [InlineData(0, Direction.Top, false)]
    [InlineData(0, Direction.Left, false)]
    [InlineData(0, Direction.TopLeft, false)]
    [InlineData(0, Direction.Right, true)]
    [InlineData(0, Direction.Bottom, true)]
    [InlineData(0, Direction.BottomRight, true)]
    [InlineData(3, Direction.Right, false)]
    [InlineData(3, Direction.TopRight, false)]
    [InlineData(3, Direction.BottomRight, false)]
    [InlineData(3, Direction.BottomLeft, true)]
    [InlineData(11, Direction.Bottom, false)]
    [InlineData(11, Direction.BottomLeft, false)]
    [InlineData(11, Direction.BottomRight, false)]
    [InlineData(11, Direction.TopLeft, true)]
    [InlineData(5, Direction.Center, false)]
    public void IsValidDirectionStaysOnTheMap(int from, Direction vector, bool valid) =>
        Assert.Equal(valid, Map.IsValidDirection(from, vector));

    [Fact]
    public void IsValidDirectionAgreesWithIsValidAbsIndex()
    {
        // Every step the geometry allows must also land on a valid index of the map.
        for (int index = 0; index < Map.TileCount; ++index)
        {
            foreach (Direction direction in Directions.AllNeighboringDirections)
            {
                if (!Map.IsValidDirection(index, direction))
                {
                    continue;
                }

                Assert.True(Map.IsValidAbsIndex(Map.GetDirectionIndex(index, direction)));
                Assert.Equal(direction, Map.GetDirection(index, Map.GetDirectionIndex(index, direction)));
            }
        }
    }

    [Fact]
    public void GetAroundIndexesReturnsTheRingWithoutTheCentre()
    {
        MapGeometry map = new(5, 5);

        int[] around = [.. map.GetAroundIndexes(12)];

        Assert.Equal(8, around.Length);
        Assert.DoesNotContain(12, around);
        Assert.Equal([6, 7, 8, 11, 13, 16, 17, 18], around);
    }

    [Fact]
    public void GetAroundIndexesIsClippedAtTheBorders()
    {
        MapGeometry map = new(5, 5);

        Assert.Equal([1, 5, 6], map.GetAroundIndexes(0));

        // The radius is a Chebyshev square, so a radius of two is 24 tiles in the middle.
        Assert.Equal(24, map.GetAroundIndexes(12, 2).Count);
        Assert.Equal(8, map.GetAroundIndexes(0, 2).Count);
    }

    [Fact]
    public void GetAroundIndexesNeedsAPositiveRadiusAndAnIndexInsideTheMap()
    {
        MapGeometry map = new(5, 5);

        Assert.Empty(map.GetAroundIndexes(12, 0));
        Assert.Empty(map.GetAroundIndexes(-1));

        // The reference compares with '>' and not '>=', so only an index past the last tile is dropped.
        Assert.Empty(map.GetAroundIndexes(26));
    }

    [Fact]
    public void GetAroundIndexesHasAnOverloadForAMapThatIsNotLoadedYet()
    {
        // The reference uses this overload to look at the neighbourhood of a tile while the map is still
        // being read, so it takes the dimensions instead of a MapGeometry.
        Assert.Equal(Map.GetAroundIndexes(4), MapGeometry.GetAroundIndexes(4, 4, 3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometry.GetAroundIndexes(0, 0, 3, 1));
    }
}
