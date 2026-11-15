using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the per-player fog of a tile (<c>maps_tiles.h:301-306</c>, <c>:356</c>, <c>:421-427</c>,
/// <c>maps_tiles.cpp:1576</c>).
/// </summary>
public sealed class TileFogTests
{
    [Fact]
    public void AFreshTileIsUnderTheFogOfEveryPlayer()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(0);

        Assert.Equal(PlayerColors.All, tile.FogColors);
        foreach (PlayerColor color in PlayerColors.Ordered)
        {
            Assert.True(tile.IsFog(color), $"{color} has not revealed a fresh tile.");
        }
    }

    [Fact]
    public void RevealingForOnePlayerKeepsTheOthersInTheFog()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);
        Tile tile = world.GetTile(0);

        tile.ClearFog(PlayerColor.Blue);

        Assert.False(tile.IsFog(PlayerColor.Blue));
        Assert.True(tile.IsFog(PlayerColor.Green));
        Assert.Equal(PlayerColors.All & ~PlayerColor.Blue, tile.FogColors);
    }

    [Fact]
    public void RevealingTwiceIsHarmless()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);
        Tile tile = world.GetTile(0);

        tile.ClearFog(PlayerColor.Blue);
        tile.ClearFog(PlayerColor.Blue);

        Assert.False(tile.IsFog(PlayerColor.Blue));
        Assert.True(tile.IsFog(PlayerColor.Red));
    }

    [Fact]
    public void TheFogOfAnAllianceLiftsWhenOneMemberSeesTheTile()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);
        Tile tile = world.GetTile(0);
        PlayerColor alliance = PlayerColor.Blue | PlayerColor.Green;

        Assert.True(tile.IsFogForAll(alliance));

        tile.ClearFog(PlayerColor.Blue);

        // Blue has seen the tile, so the alliance no longer has it fully in the fog.
        Assert.False(tile.IsFogForAll(alliance));
        Assert.True(tile.IsFog(PlayerColor.Green));
    }

    [Fact]
    public void AFreshTileDrawsFogInEveryDirection()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);
        Tile tile = world.GetTile(0);

        Assert.Equal(Directions.All, tile.FogDirection);

        tile.SetFogDirection(Direction.Center);

        Assert.Equal(Direction.Center, tile.FogDirection);
    }
}
