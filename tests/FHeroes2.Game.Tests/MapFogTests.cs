using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;

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
    [InlineData(6, 43)]
    [InlineData(7, 66)]
    [InlineData(8, 90)]
    public void SquaredRevealLimit_FollowsTheReferenceTable(int distance, int expected)
    {
        // ( distance + 1 ) ^ 2 - distance, with the two exceptions getSquaredScoutingRadiusLimit makes
        // for the scouting values only artifacts can reach.
        Assert.Equal(expected, MapFog.SquaredRevealLimit(distance));
    }

    [Fact]
    public void Reveal_ClearsTheDiscAroundTheTile()
    {
        MapFog fog = new(11, 11);

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
        MapFog fog = new(3, 3);

        fog.Reveal(1, 1, 0, PlayerColor.Blue);

        Assert.False(fog.IsFogged(1, 1, PlayerColor.Blue));
        Assert.True(fog.IsFogged(0, 1, PlayerColor.Blue));
        Assert.True(fog.IsFogged(1, 0, PlayerColor.Blue));
    }

    [Fact]
    public void IsFogged_RequiresEveryColorToKnowTheTile()
    {
        MapFog fog = new(3, 3);

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
        MapFog fog = new(4, 4);

        fog.UpdateDirections(PlayerColor.Blue);

        for (int row = 0; row < fog.Height; ++row)
        {
            for (int column = 0; column < fog.Width; ++column)
            {
                Assert.Equal(FogDirections.All, fog.GetDirection(column, row));
            }
        }

        Assert.Equal(16, fog.CountFogged());
    }

    [Fact]
    public void UpdateDirections_MarksTheTilesAroundAHole()
    {
        MapFog fog = new(5, 5);
        fog.Reveal(2, 2, 0, PlayerColor.Blue);

        fog.UpdateDirections(PlayerColor.Blue);

        Assert.Equal(FogDirection.Unknown, fog.GetDirection(2, 2));

        // The four tiles that touch the hole lose the bits that point at it.
        Assert.Equal(FogDirections.All & ~FogDirection.Right, fog.GetDirection(1, 2));
        Assert.Equal(FogDirections.All & ~FogDirection.Left, fog.GetDirection(3, 2));
        Assert.Equal(FogDirections.All & ~FogDirection.Bottom, fog.GetDirection(2, 1));
        Assert.Equal(FogDirections.All & ~FogDirection.Top, fog.GetDirection(2, 3));

        // Only the two neighbours that touch the hole are cleared, so the diagonal tiles keep the bits
        // that point at their fogged straight neighbours.
        Assert.Equal(FogDirections.All & ~FogDirection.BottomRight, fog.GetDirection(1, 1));
        Assert.Equal(FogDirections.All & ~FogDirection.BottomLeft, fog.GetDirection(3, 1));
        Assert.Equal(FogDirections.All & ~FogDirection.TopRight, fog.GetDirection(1, 3));
        Assert.Equal(FogDirections.All & ~FogDirection.TopLeft, fog.GetDirection(3, 3));
    }

    [Fact]
    public void UpdateDirections_TreatsTheTilesOutsideTheMapAsFogged()
    {
        // The reveal covers the whole map, so no tile keeps a bit set even though the tiles of the
        // edge have neighbours outside the map.
        MapFog fog = new(3, 3);
        fog.Reveal(1, 1, 2, PlayerColor.Blue);

        fog.UpdateDirections(PlayerColor.Blue);

        Assert.Equal(0, fog.CountFogged());
        Assert.Equal(FogDirection.Unknown, fog.GetDirection(0, 0));
    }

    [Fact]
    public void UpdateDirections_WithoutColorsLeavesTheMapFogFree()
    {
        MapFog fog = new(3, 3);

        fog.UpdateDirections(PlayerColor.None);

        Assert.Equal(0, fog.CountFogged());
        Assert.Equal(FogDirection.Unknown, fog.GetDirection(1, 1));
    }

    [Fact]
    public void CreateForMap_ScoutsTheHeroesOfTheGivenColorsOnly()
    {
        Mp2Map map = SyntheticMap.Create(
            9,
            9,
            index => index switch
            {
                2 + (2 * 9) => HeroTile(0),
                6 + (6 * 9) => HeroTile(14),
                _ => SyntheticMap.BlankTile(),
            });

        MapFog fog = MapFog.CreateForMap(map, PlayerColor.Blue);

        Assert.False(fog.IsFogged(2, 2, PlayerColor.Blue));
        Assert.Equal(FogDirection.Unknown, fog.GetDirection(2, 2));

        // The red hero is invisible for blue: ( 6; 6 ) stays under the fog, three tiles away from the
        // edge of the blue disc, and ( 8; 8 ) is a whole fogged tile.
        Assert.True(fog.IsFogged(6, 6, PlayerColor.Blue));
        Assert.Equal(FogDirections.All, fog.GetDirection(8, 8));

        Assert.Equal(81 - CountRevealed(2, 2, MapFog.HeroRevealDistance, 9, 9), fog.CountFogged());
    }

    [Fact]
    public void CreateForMap_LeavesAFogFreeMapWithoutHumanPlayer()
    {
        Mp2Map map = SyntheticMap.Create(5, 5, index => index == 12 ? HeroTile(0) : SyntheticMap.BlankTile());

        MapFog fog = MapFog.CreateForMap(map, PlayerColor.None);

        Assert.Equal(0, fog.CountFogged());
    }

    /// <summary>Number of tiles of a reveal disc that lie inside a map, the arithmetic of <c>ClearFog</c>.</summary>
    private static int CountRevealed(int column, int row, int distance, int width, int height)
    {
        int limit = MapFog.SquaredRevealLimit(distance);
        int count = 0;

        for (int y = Math.Max(0, row - distance); y <= Math.Min(height - 1, row + distance); ++y)
        {
            for (int x = Math.Max(0, column - distance); x <= Math.Min(width - 1, column + distance); ++x)
            {
                if (((x - column) * (x - column)) + ((y - row) * (y - row)) < limit)
                {
                    ++count;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// A hero tile of the given <c>MINIHERO.ICN</c> frame; seven frames per color in the order of
    /// <see cref="PlayerColors.Ordered"/>, so <c>0</c> is a blue knight and <c>14</c> a red knight.
    /// </summary>
    private static Mp2TileInfo HeroTile(int spriteIndex) =>
        SyntheticMap.BlankTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
            BottomIcnImageIndex = (byte)spriteIndex,
            ObjectType = Mp2ObjectType.Hero,
        };
}
