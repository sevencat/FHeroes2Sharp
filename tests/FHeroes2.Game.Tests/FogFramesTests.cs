using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

public sealed class FogFramesTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(5, 7, 0)]
    [InlineData(7, 5, 0)]
    [InlineData(3, 8, 3)]
    public void FullFog_UsesTheCheckerboardOfTheFourTiles(int column, int row, int variant)
    {
        FogFrame frame = FogFrames.Resolve(Directions.All, column, row, 12);

        Assert.True(frame.IsFullTile);
        Assert.Equal((column + row) % FogFrames.FullTileCount, variant);
        Assert.Equal(variant, frame.Index);
        Assert.False(frame.Flipped);
    }

    [Fact]
    public void NoFog_ResolvesToTheIsolatedHolePiece()
    {
        // The renderer never asks for a visible tile, so the mask without the centre bit only has to
        // resolve to one of the thirty-four pieces.
        FogFrame frame = FogFrames.Resolve(Direction.Unknown, 2, 3, 10);

        Assert.False(frame.IsFullTile);
        Assert.Equal(10, frame.Index);
    }

    [Fact]
    public void AnImpossibleMask_FallsBackToAWholeTile()
    {
        // A mask without the centre bit but with both horizontal neighbours is not one of the
        // thirty-four pieces; the reference logs "invalid direction for fog" and paints a whole tile.
        FogFrame frame = FogFrames.Resolve(Direction.Left | Direction.Right, 2, 3, 10);

        Assert.True(frame.IsFullTile);
        Assert.Equal(1, frame.Index);
    }

    [Fact]
    public void AnIsolatedHole_HasItsOwnPiece()
    {
        FogFrame frame = FogFrames.Resolve(Direction.Center, 4, 4, 10);

        Assert.False(frame.IsFullTile);
        Assert.Equal(10, frame.Index);
        Assert.False(frame.Flipped);
    }

    [Theory]
    [InlineData(Direction.Top, 6)]
    [InlineData(Direction.Right, 7)]
    [InlineData(Direction.Bottom, 8)]
    public void ASingleStraightNeighbour_HasItsOwnPiece(Direction straight, int index)
    {
        FogFrame frame = FogFrames.Resolve(Direction.Center | straight, 4, 4, 10);

        Assert.False(frame.IsFullTile);
        Assert.Equal(index, frame.Index);
        Assert.False(frame.Flipped);
    }

    [Fact]
    public void TheLeftEdge_IsTheMirroredRightEdge()
    {
        FogFrame right = FogFrames.Resolve(Direction.Center | Direction.Right, 4, 4, 10);
        FogFrame left = FogFrames.Resolve(Direction.Center | Direction.Left, 4, 4, 10);

        Assert.Equal(7, right.Index);
        Assert.False(right.Flipped);
        Assert.Equal(right.Index, left.Index);
        Assert.True(left.Flipped);
    }

    [Fact]
    public void AStripWithoutTheSideNeighbours_UsesTheColumnOrRowPiece()
    {
        FogFrame column = FogFrames.Resolve(
            Direction.Center | Direction.Top | Direction.Bottom,
            4,
            4,
            10);
        FogFrame row = FogFrames.Resolve(
            Direction.Center | Direction.Left | Direction.Right,
            4,
            4,
            10);

        Assert.Equal(9, column.Index);
        Assert.False(column.Flipped);
        Assert.Equal(29, row.Index);
        Assert.False(row.Flipped);
    }

    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 15)]
    public void OneMissingDiagonal_UsesTheFifteenPiece(bool mirror, int index)
    {
        Direction direction = Directions.All & ~(mirror ? Direction.TopLeft : Direction.TopRight);

        FogFrame frame = FogFrames.Resolve(direction, 4, 4, 10);

        Assert.Equal(index, frame.Index);
        Assert.Equal(mirror, frame.Flipped);
    }

    [Theory]
    [InlineData(false, 22)]
    [InlineData(true, 22)]
    public void OneMissingDiagonalOfTheBottomRow_UsesTheTwentyTwoPiece(bool mirror, int index)
    {
        Direction direction = Directions.All & ~(mirror ? Direction.BottomLeft : Direction.BottomRight);

        FogFrame frame = FogFrames.Resolve(direction, 4, 4, 10);

        Assert.Equal(index, frame.Index);
        Assert.Equal(mirror, frame.Flipped);
    }

    [Theory]
    [InlineData(false, 13)]
    [InlineData(true, 13)]
    public void OneDiscoveredCorner_TakesThePieceOfThatCorner(bool mirror, int index)
    {
        // The three bits of a corner are discovered together, so the mask is the whole fog minus one
        // of the four corner sets rather than minus a single diagonal.
        Direction corner = mirror ? Directions.TopLeftCorner : Directions.TopRightCorner;

        FogFrame frame = FogFrames.Resolve(Directions.All & ~corner, 4, 4, 10);

        Assert.Equal(index, frame.Index);
        Assert.Equal(mirror, frame.Flipped);
    }

    [Theory]
    [InlineData(false, 14)]
    [InlineData(true, 14)]
    public void OneDiscoveredCornerOfTheBottomRow_TakesThePieceOfThatCorner(bool mirror, int index)
    {
        Direction corner = mirror ? Directions.BottomLeftCorner : Directions.BottomRightCorner;

        FogFrame frame = FogFrames.Resolve(Directions.All & ~corner, 4, 4, 10);

        Assert.Equal(index, frame.Index);
        Assert.Equal(mirror, frame.Flipped);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 0, 0)]
    public void TheBottomRowStrip_IsSplitByTheParityOfTheTileIndex(int column, int row, int index)
    {
        Direction direction = Directions.CenterRow | Directions.BottomRow;

        Assert.Equal(index, FogFrames.Resolve(direction, column, row, 10).Index);
    }

    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(1, 0, 4)]
    public void TheTopRowStrip_IsSplitByTheParityOfTheTileIndex(int column, int row, int index)
    {
        Direction direction = Directions.CenterRow | Directions.TopRow;

        Assert.Equal(index, FogFrames.Resolve(direction, column, row, 10).Index);
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 2)]
    public void TheLeftColumnStrip_IsSplitByTheParityOfTheTileIndex(int column, int row, int index)
    {
        Direction direction = Directions.CenterColumn | Directions.LeftColumn;

        Assert.Equal(index, FogFrames.Resolve(direction, column, row, 10).Index);
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 2)]
    public void TheRightColumnStrip_IsTheMirroredLeftColumnStrip(int column, int row, int index)
    {
        Direction direction = Directions.CenterColumn | Directions.RightColumn;

        FogFrame frame = FogFrames.Resolve(direction, column, row, 10);

        Assert.Equal(index, frame.Index);
        Assert.True(frame.Flipped);
    }
}
