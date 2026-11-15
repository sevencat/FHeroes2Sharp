using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Direction"/> and <see cref="Directions"/>, the port of
/// <c>heroes/direction.h</c> and <c>direction.cpp</c>. The values are a bit mask that the fog sprite
/// table and the map geometry both index with, so they are pinned here.
/// </summary>
public sealed class DirectionTests
{
    [Fact]
    public void DirectionIsABitMaskStartingAtTopLeft()
    {
        // Clockwise from the top left, then the tile itself.
        Assert.Equal(0x0001, (int)Direction.TopLeft);
        Assert.Equal(0x0002, (int)Direction.Top);
        Assert.Equal(0x0004, (int)Direction.TopRight);
        Assert.Equal(0x0008, (int)Direction.Right);
        Assert.Equal(0x0010, (int)Direction.BottomRight);
        Assert.Equal(0x0020, (int)Direction.Bottom);
        Assert.Equal(0x0040, (int)Direction.BottomLeft);
        Assert.Equal(0x0080, (int)Direction.Left);
        Assert.Equal(0x0100, (int)Direction.Center);
        Assert.Equal(0, (int)Direction.Unknown);
    }

    [Fact]
    public void TheMasksCoverTheEightNeighboursAndTheTileItself()
    {
        Assert.Equal(Direction.TopLeft | Direction.Top | Direction.TopRight, Directions.TopRow);
        Assert.Equal(Direction.BottomLeft | Direction.Bottom | Direction.BottomRight, Directions.BottomRow);
        Assert.Equal(Direction.Left | Direction.Center | Direction.Right, Directions.CenterRow);

        // DIRECTION_AROUND is everything but the tile itself, DIRECTION_ALL is everything.
        Assert.Equal(0x00FF, (int)Directions.Around);
        Assert.Equal(0x01FF, (int)Directions.All);
        Assert.Equal(Directions.Around, Directions.All & ~Direction.Center);

        // The four corner pieces are three bits each, the corner "all" is the four diagonals.
        Assert.Equal(0x0E, (int)Directions.TopRightCorner);
        Assert.Equal(0x83, (int)Directions.TopLeftCorner);
        Assert.Equal(0x38, (int)Directions.BottomRightCorner);
        Assert.Equal(0xE0, (int)Directions.BottomLeftCorner);
        Assert.Equal(0x55, (int)Directions.AllCorners);
    }

    [Fact]
    public void ContainsNeedsEveryBitOfTheMask()
    {
        Assert.True(Directions.Contains(Directions.All, Directions.CenterRow));
        Assert.True(Directions.Contains(Direction.Top | Direction.Left, Direction.Top));
        Assert.False(Directions.Contains(Direction.Top, Directions.TopRow));
        Assert.False(Directions.Contains(Direction.Unknown, Direction.Top));
    }

    [Fact]
    public void IsDiagonalIsAboutTheCornerBits()
    {
        Assert.True(Directions.IsDiagonal(Direction.TopLeft));
        Assert.True(Directions.IsDiagonal(Direction.Top | Direction.BottomRight));
        Assert.False(Directions.IsDiagonal(Direction.Top));
        Assert.False(Directions.IsDiagonal(Direction.Center));
        Assert.False(Directions.IsDiagonal(Direction.Unknown));
    }

    [Theory]
    [InlineData(Direction.TopLeft, Direction.BottomRight)]
    [InlineData(Direction.Top, Direction.Bottom)]
    [InlineData(Direction.TopRight, Direction.BottomLeft)]
    [InlineData(Direction.Right, Direction.Left)]
    [InlineData(Direction.BottomRight, Direction.TopLeft)]
    [InlineData(Direction.Bottom, Direction.Top)]
    [InlineData(Direction.BottomLeft, Direction.TopRight)]
    [InlineData(Direction.Left, Direction.Right)]
    [InlineData(Direction.Center, Direction.Center)]
    public void ReflectReturnsTheOppositeSide(Direction direction, Direction reflected) =>
        Assert.Equal(reflected, Directions.Reflect(direction));

    [Fact]
    public void ReflectOfASetIsUnknown()
    {
        Assert.Equal(Direction.Unknown, Directions.Reflect(Direction.Top | Direction.Left));
        Assert.Equal(Direction.Unknown, Directions.Reflect(Direction.Unknown));
    }

    [Theory]
    [InlineData(Direction.Top, Direction.Right, true)]
    [InlineData(Direction.Top, Direction.Bottom, true)]
    [InlineData(Direction.Top, Direction.TopLeft, false)]
    [InlineData(Direction.Left, Direction.Top, true)]
    [InlineData(Direction.Left, Direction.Bottom, false)]
    [InlineData(Direction.Bottom, Direction.Left, true)]
    [InlineData(Direction.Bottom, Direction.BottomRight, false)]
    [InlineData(Direction.Right, Direction.Bottom, true)]
    [InlineData(Direction.Right, Direction.TopRight, false)]
    public void ShortDistanceClockWiseIsHalfATurnAtMost(Direction from, Direction to, bool expected) =>
        Assert.Equal(expected, Directions.ShortDistanceClockWise(from, to));

    [Fact]
    public void StringListsTheSetBitsInTheReferenceOrder()
    {
        Assert.Equal("unknown", Directions.String(Direction.Unknown));
        Assert.Equal("center", Directions.String(Direction.Center));
        Assert.Equal("center, top, left", Directions.String(Direction.Center | Direction.Top | Direction.Left));
        Assert.Equal("top-right", Directions.String(Direction.TopRight));
        Assert.Equal("center, top, top-right, right, bottom-right, bottom, bottom-left, left, top-left", Directions.String(Directions.All));
    }

    [Fact]
    public void AllNeighboringDirectionsIsClockwiseFromTheTopLeft()
    {
        Assert.Equal(8, Directions.AllNeighboringDirections.Length);
        Assert.Equal((Direction)0x00FF, Directions.AllNeighboringDirections.Aggregate(Direction.Unknown, (sum, direction) => sum | direction));

        for (int i = 0; i < Directions.AllNeighboringDirections.Length; ++i)
        {
            Assert.Equal(1 << i, (int)Directions.AllNeighboringDirections[i]);
        }
    }
}
