using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the object-aware passability checks of a tile (<c>maps_tiles.cpp:1007-1147</c>).
/// </summary>
public sealed class TileObjectPassabilityTests
{
    [Fact]
    public void APlainLandTileTakesAHeroOnFoot()
    {
        (World world, Tile tile) = GrassTile();
        tile.ClearFog(PlayerColor.Blue);

        Assert.True(tile.IsPassableFrom(world, Direction.Bottom, false, ignoreFog: false, PlayerColor.Blue));
        Assert.True(tile.IsSuitableForDisembarkation());
        Assert.False(tile.IsSuitableForSummoningBoat());
    }

    [Fact]
    public void APlainWaterTileTakesABoatButNotAHeroOnFoot()
    {
        (World world, Tile tile) = WaterTile();
        tile.ClearFog(PlayerColor.Blue);

        Assert.False(tile.IsPassableFrom(world, Direction.Bottom, false, ignoreFog: false, PlayerColor.Blue));
        Assert.True(tile.IsPassableFrom(world, Direction.Bottom, true, ignoreFog: false, PlayerColor.Blue));
        Assert.True(tile.IsSuitableForSummoningBoat());
        Assert.False(tile.IsSuitableForDisembarkation());
    }

    [Fact]
    public void AnExistingBoatBlocksTheTileForAnotherBoat()
    {
        (World world, Tile tile) = WaterTile();
        tile.ClearFog(PlayerColor.Blue);
        tile.SetMainObjectType(Mp2ObjectType.ActionBoat);
        tile.SetInitialPassability();

        Assert.False(tile.IsPassableFrom(world, Direction.Bottom, true, ignoreFog: false, PlayerColor.Blue));

        // Summoning a boat is only about the terrain and the parts: the reference never looks at the
        // object type here, so a tile that already carries a boat still answers "suitable". The caller
        // keeps the two boats apart by asking isPassableFrom() above.
        Assert.True(tile.IsSuitableForSummoningBoat());
    }

    [Fact]
    public void TheCoastIsAlwaysGoodForALanding()
    {
        (_, Tile tile) = WaterTile();
        tile.SetMainObjectType(Mp2ObjectType.ActionCoast);
        tile.SetInitialPassability();

        // A coast tile is water by its terrain image, yet the game trusts the sprite and lets the hero
        // land: the objects on it are never consulted.
        Assert.True(tile.IsWater);
        Assert.True(tile.IsSuitableForDisembarkation());
    }

    [Fact]
    public void AHeroTileIsNeverGoodForALanding()
    {
        (_, Tile tile) = GrassTile();
        tile.SetMainObjectType(Mp2ObjectType.ActionHero);

        Assert.False(tile.IsSuitableForDisembarkation());
        Assert.False(tile.IsSuitableForSummoningBoat());
    }

    [Fact]
    public void AStoneOnTheTileBlocksTheLanding()
    {
        (_, Tile tile) = GrassTile();

        // Index 1 is the body of the artifact object; index 0 is its shadow, which would not block.
        tile.GroundObjectParts.Add(new ObjectPart(ObjectLayerType.Object, 0, Mp2ObjectIcnType.ObjnArti, 1));

        Assert.False(tile.IsSuitableForDisembarkation());
    }

    [Fact]
    public void TheShadowOfAnObjectDoesNotBlockTheLanding()
    {
        (_, Tile tile) = GrassTile();
        tile.GroundObjectParts.Add(new ObjectPart(ObjectLayerType.Shadow, 0, Mp2ObjectIcnType.ObjnArti, 0));

        Assert.True(tile.IsSuitableForDisembarkation());
    }

    [Fact]
    public void ARoadOnTheTileDoesNotBlockTheLanding()
    {
        (_, Tile tile) = GrassTile();
        tile.GroundObjectParts.Add(new ObjectPart(ObjectLayerType.Terrain, 0, Mp2ObjectIcnType.Road, 0));

        Assert.True(tile.IsSuitableForDisembarkation());
    }

    [Fact]
    public void AFoggedTileIsClosedUnlessTheFogIsIgnored()
    {
        (World world, Tile tile) = GrassTile();

        // A fresh tile is under the fog of every player.
        Assert.False(tile.IsPassableFrom(world, Direction.Center, false, ignoreFog: false, PlayerColor.Blue));
        Assert.True(tile.IsPassableFrom(world, Direction.Center, false, ignoreFog: true, PlayerColor.Blue));

        tile.ClearFog(PlayerColor.Blue);

        Assert.True(tile.IsPassableFrom(world, Direction.Center, false, ignoreFog: false, PlayerColor.Blue));

        // The fog is per player: what blue has seen is still dark for green.
        Assert.False(tile.IsPassableFrom(world, Direction.Center, false, ignoreFog: false, PlayerColor.Green));
    }

    [Fact]
    public void AShadowOnTheTileDoesNotChangeThePassableDirections()
    {
        (_, Tile tile) = GrassTile();
        tile.GroundObjectParts.Add(new ObjectPart(ObjectLayerType.Shadow, 1, Mp2ObjectIcnType.ObjnArti, 0));

        // Shadows are drawn under everything and never block: the mask stays whole.
        Assert.Equal(Directions.All, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void AUltimateArtifactOnlyHidesOnAPlainDryTile()
    {
        (World world, Tile tile) = GrassTile();
        tile.ClearFog(PlayerColor.None);

        Assert.True(tile.IsSuitableForUltimateArtifact(world));

        tile.GroundObjectParts.Add(new ObjectPart(ObjectLayerType.Object, 0, Mp2ObjectIcnType.ObjnArti, 1));

        Assert.False(tile.IsSuitableForUltimateArtifact(world));
    }

    [Fact]
    public void WaterNeverHidesTheUltimateArtifact()
    {
        (World world, Tile tile) = WaterTile();
        tile.ClearFog(PlayerColor.None);

        Assert.False(tile.IsSuitableForUltimateArtifact(world));
    }

    private static (World World, Tile Tile) GrassTile()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(10);
        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetInitialPassability();

        return (world, tile);
    }

    private static (World World, Tile Tile) WaterTile()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(10);
        tile.SetTerrain(MapGrounds.WaterStartImageIndex, 0);
        tile.SetInitialPassability();

        return (world, tile);
    }
}
