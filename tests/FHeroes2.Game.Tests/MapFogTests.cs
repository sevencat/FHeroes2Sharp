using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

public sealed class MapFogTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 7)]
    [InlineData(3, 13)]
    [InlineData(4, 21)]
    [InlineData(5, 31)]
    [InlineData(6, 49)]
    [InlineData(7, 66)]
    [InlineData(8, 90)]
    [InlineData(9, 100)]
    public void SquaredRevealLimit_FollowsTheReferenceTable(int distance, int expected)
    {
        // ( distance + 1 ) ^ 2 - distance while the radius is below six, the plain ( distance + 1 ) ^ 2 from
        // six on, and the two hardcoded exceptions getSquaredScoutingRadiusLimit makes for the scouting
        // values only artifacts can reach.
        Assert.Equal(expected, MapFog.SquaredRevealLimit(distance));
    }

    [Fact]
    public void Reveal_ClearsTheDiscAroundTheTile()
    {
        MapFog fog = FogTestGrid.Create(11, 11);

        fog.Reveal(5, 5, MapFog.HeroRevealDistance, PlayerColor.Blue);

        Assert.False(fog.IsFogged(5, 5, PlayerColor.Blue));
        Assert.False(fog.IsFogged(9, 5, PlayerColor.Blue));
        Assert.False(fog.IsFogged(5, 1, PlayerColor.Blue));
        Assert.False(fog.IsFogged(8, 8, PlayerColor.Blue));

        // 5 * 5 = 25 is above the limit of 21, so the corners of the bounding box stay under the fog.
        Assert.True(fog.IsFogged(9, 9, PlayerColor.Blue));
        Assert.True(fog.IsFogged(10, 5, PlayerColor.Blue));
    }

    [Fact]
    public void Reveal_WithZeroDistanceRevealsOnlyTheTile()
    {
        MapFog fog = FogTestGrid.Create(3, 3);

        fog.Reveal(1, 1, 0, PlayerColor.Blue);

        Assert.False(fog.IsFogged(1, 1, PlayerColor.Blue));
        Assert.True(fog.IsFogged(0, 1, PlayerColor.Blue));
        Assert.True(fog.IsFogged(1, 0, PlayerColor.Blue));
    }

    [Fact]
    public void IsFogged_RequiresEveryColorToKnowTheTile()
    {
        MapFog fog = FogTestGrid.Create(3, 3);

        fog.Reveal(1, 1, 0, PlayerColor.Blue);

        Assert.False(fog.IsFogged(1, 1, PlayerColor.Blue));
        Assert.True(fog.IsFogged(1, 1, PlayerColor.Green));
        Assert.False(fog.IsFogged(1, 1, PlayerColor.Blue | PlayerColor.Green));

        fog.Reveal(1, 1, 0, PlayerColor.Green);

        Assert.False(fog.IsFogged(1, 1, PlayerColor.Blue | PlayerColor.Green));
        Assert.True(fog.IsFogged(1, 1, PlayerColor.Red));
        Assert.False(fog.IsFogged(1, 1, PlayerColor.Green | PlayerColor.Red));
    }

    [Fact]
    public void UpdateDirections_KeepsAFullyFoggedMapUnderTheFog()
    {
        MapFog fog = FogTestGrid.Create(4, 4);

        fog.UpdateDirections(PlayerColor.Blue);

        for (int row = 0; row < fog.Height; ++row)
        {
            for (int column = 0; column < fog.Width; ++column)
            {
                Assert.Equal(Directions.All, fog.GetDirection(column, row));
            }
        }

        Assert.Equal(16, fog.CountFogged());
    }

    [Fact]
    public void UpdateDirections_MarksTheTilesAroundAHole()
    {
        MapFog fog = FogTestGrid.Create(5, 5);
        fog.Reveal(2, 2, 0, PlayerColor.Blue);

        fog.UpdateDirections(PlayerColor.Blue);

        Assert.Equal(Direction.Unknown, fog.GetDirection(2, 2));

        // The four tiles that touch the hole lose the bits that point at it.
        Assert.Equal(Directions.All & ~Direction.Right, fog.GetDirection(1, 2));
        Assert.Equal(Directions.All & ~Direction.Left, fog.GetDirection(3, 2));
        Assert.Equal(Directions.All & ~Direction.Bottom, fog.GetDirection(2, 1));
        Assert.Equal(Directions.All & ~Direction.Top, fog.GetDirection(2, 3));

        // Only the two neighbours that touch the hole are cleared, so the diagonal tiles keep the bits
        // that point at their fogged straight neighbours.
        Assert.Equal(Directions.All & ~Direction.BottomRight, fog.GetDirection(1, 1));
        Assert.Equal(Directions.All & ~Direction.BottomLeft, fog.GetDirection(3, 1));
        Assert.Equal(Directions.All & ~Direction.TopRight, fog.GetDirection(1, 3));
        Assert.Equal(Directions.All & ~Direction.TopLeft, fog.GetDirection(3, 3));
    }

    [Fact]
    public void UpdateDirections_TreatsTheTilesOutsideTheMapAsFogged()
    {
        // The reveal covers the whole map, so no tile keeps a bit set even though the tiles of the
        // edge have neighbours outside the map.
        MapFog fog = FogTestGrid.Create(3, 3);
        fog.Reveal(1, 1, 2, PlayerColor.Blue);

        fog.UpdateDirections(PlayerColor.Blue);

        Assert.Equal(0, fog.CountFogged());
        Assert.Equal(Direction.Unknown, fog.GetDirection(0, 0));
    }

    [Fact]
    public void UpdateDirections_WithoutColorsLeavesTheMapFogFree()
    {
        MapFog fog = FogTestGrid.Create(3, 3);

        fog.UpdateDirections(PlayerColor.None);

        Assert.Equal(0, fog.CountFogged());
        Assert.Equal(Direction.Unknown, fog.GetDirection(1, 1));
    }

    [Fact]
    public void ClearFog_WritesTheTilesOnly()
    {
        // The tiles are the only storage of the fog: one World.ClearFog has to answer the same through the tile
        // and through the MapFog view, with no second table to write (ADR-238). Before that the two disagreed
        // until SyncTilesFog copied one into the other.
        World world = new();
        world.GenerateUninitializedMap(8);

        int center = (4 * world.Width) + 4;

        Assert.True(world.ClearFog(center, 1, PlayerColor.Blue) > 0);

        Tile tile = world.GetTile(center);

        Assert.False(tile.IsFogForAll(PlayerColor.Blue));
        Assert.Equal(tile.FogDirection, world.Fog!.GetDirection(4, 4));
        Assert.False(world.Fog.IsFogged(4, 4, PlayerColor.Blue));
    }
}
