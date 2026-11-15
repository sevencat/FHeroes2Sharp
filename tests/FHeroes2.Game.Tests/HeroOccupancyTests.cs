using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the hero occupancy of a tile: <c>Tile::getHero()</c> / <c>Tile::setHero()</c>
/// (<c>src/fheroes2/maps/maps_tiles.cpp:429</c>), the two forms of <c>Tile::getMainObjectType()</c>
/// (<c>:463</c>), <c>Tile::_getMainObjectTypeUnderHero()</c> (<c>:467</c>), the hero branch of
/// <c>Tile::fixMP2MapTileObjectType()</c> (<c>:1353</c>), <c>Heroes::isValidId()</c> /
/// <c>setObjectTypeUnderHero()</c> (<c>heroes.h:723</c> / <c>:410</c>) and the lookup
/// <c>World::GetHeroes()</c> does through <c>AllHeroes::Get()</c> (<c>heroes.cpp:2411</c>).
/// </summary>
public sealed class HeroOccupancyTests
{
    private const int MapSize = 5;

    /// <summary>
    /// A 5 × 5 all grass world. The indexes are assigned by hand because
    /// <see cref="World.GenerateUninitializedMap"/> leaves them at zero on purpose — the map loader is
    /// the one that sets them, and every neighbour lookup is computed from <see cref="Tile.Index"/>.
    /// </summary>
    /// <returns>The world.</returns>
    private static World CreateLandWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int i = 0; i < world.Size; ++i)
        {
            Tile tile = world.Tiles[i];
            tile.Index = i;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        // World::Defaults() -> vec_heroes.Init(): a world that was built by hand has no heroes until the
        // table is created, and the tile lookups below go through it.
        world.Heroes.Init(MapVersion.SuccessionWars);

        return world;
    }

    /// <summary>
    /// Puts one object on a tile and promotes it to the main part. Sprite 255 of <c>OBJNCRCK</c> carries
    /// no object data, so the object type comes out as <see cref="Mp2ObjectType.None"/> and the tile is
    /// impassable — a mask that the hero branch of the passability check must not change.
    /// </summary>
    /// <param name="tile">Tile to fill.</param>
    private static void PutBlockingObject(Tile tile) =>
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 255));

    [Fact]
    public void ATileWithoutAHeroReportsNone()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];

        Assert.Equal(0, tile.OccupantHeroId);
        Assert.Null(tile.GetHero(world));
        Assert.Equal(Mp2ObjectType.None, tile.GetMainObjectTypeUnderHero(world));
        Assert.Equal(Mp2ObjectType.None, tile.GetMainObjectType(world, false));
    }

    [Fact]
    public void PlacingAHeroParksTheObjectTypeOnTheHero()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];
        tile.SetMainObjectType(Mp2ObjectType.ActionResource);

        Hero hero = world.GetHero(12)!;
        tile.SetHero(world, hero);

        // The tile shows a hero; the object it covers is remembered by the hero, not by the tile.
        Assert.Equal(Mp2ObjectType.ActionHero, tile.MainObjectType);
        Assert.Equal(12, tile.OccupantHeroId);
        Assert.Same(hero, tile.GetHero(world));
        Assert.Equal(Mp2ObjectType.ActionResource, hero.ObjectTypeUnderHero);

        // getMainObjectType( true ) is the cached "hero", getMainObjectType( false ) looks through it.
        Assert.Equal(Mp2ObjectType.ActionHero, tile.GetMainObjectType(world));
        Assert.Equal(Mp2ObjectType.ActionResource, tile.GetMainObjectType(world, false));
        Assert.Equal(Mp2ObjectType.ActionResource, tile.GetMainObjectTypeUnderHero(world));
    }

    [Fact]
    public void RemovingAHeroPutsTheObjectBack()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];
        tile.SetMainObjectType(Mp2ObjectType.ActionArtifact);

        Hero hero = world.GetHero(3)!;
        tile.SetHero(world, hero);

        tile.SetHero(world, null);

        Assert.Equal(Mp2ObjectType.ActionArtifact, tile.MainObjectType);
        Assert.Equal(Mp2ObjectType.None, hero.ObjectTypeUnderHero);
        Assert.Equal(0, tile.OccupantHeroId);
        Assert.Null(tile.GetHero(world));
    }

    [Fact]
    public void RemovingAHeroTheWorldDoesNotKnowRebuildsTheTypeFromTheParts()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];

        // A savegame can leave OBJ_HERO behind without a matching hero; the removal then has nothing to
        // restore and falls back to updateObjectType().
        tile.SetMainObjectType(Mp2ObjectType.ActionHero);

        tile.SetHero(world, null);

        Assert.Equal(0, tile.OccupantHeroId);
        Assert.Equal(Mp2ObjectType.None, tile.MainObjectType);
    }

    [Fact]
    public void AHeroIdThatIsNotInTheWorldIsNotAHero()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];

        // The type says hero and the id is a hero id, but the world has no such hero: the tile reports
        // none, and the type under the hero cannot be resolved.
        tile.SetMainObjectType(Mp2ObjectType.ActionHero);

        Assert.Null(tile.GetHero(world));
        Assert.Equal(Mp2ObjectType.None, tile.GetMainObjectTypeUnderHero(world));
    }

    [Fact]
    public void TheHeroTypeOnATileIsTheActionOne()
    {
        // MP2::OBJ_HERO is OBJ_NON_ACTION_HERO + 128 (mp2.h:327), i.e. 183, and that is what a tile
        // stores; 55 is the twin the game never writes.
        Assert.Equal(183, (int)Mp2ObjectType.ActionHero);
        Assert.Equal(55, (int)Mp2ObjectType.Hero);
        Assert.True(Mp2ObjectTypes.IsActionObject(Mp2ObjectType.ActionHero));
        Assert.False(Mp2ObjectTypes.IsActionObject(Mp2ObjectType.Hero));
    }

    [Fact]
    public void AWaterTileMarkedAsCoastKeepsTheHeroAndClearsTheTypeUnderIt()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];
        tile.SetTerrain(MapGrounds.WaterStartImageIndex, 0);

        Hero hero = world.GetHero(5)!;

        // The broken map marks the water tile as coast; the hero then parks that false type.
        tile.SetMainObjectType(Mp2ObjectType.Coast);
        tile.SetHero(world, hero);
        Assert.Equal(Mp2ObjectType.Coast, hero.ObjectTypeUnderHero);

        tile.FixMP2MapTileObjectType(world);

        // The hero stays, and the tile underneath becomes empty instead of exposing a coast.
        Assert.Equal(Mp2ObjectType.ActionHero, tile.MainObjectType);
        Assert.Same(hero, tile.GetHero(world));
        Assert.Equal(Mp2ObjectType.None, hero.ObjectTypeUnderHero);
    }

    [Fact]
    public void PassabilityLooksThroughTheHero()
    {
        World world = CreateLandWorld();
        Tile tile = world.Tiles[12];
        PutBlockingObject(tile);
        tile.SortObjectParts();

        world.UpdatePassabilities();
        var blocked = tile.PassableDirections;

        Hero hero = world.GetHero(7)!;
        tile.SetHero(world, hero);

        world.UpdatePassabilities();

        // A hero is a temporary object (maps_tiles.cpp:686): what it covers still decides the mask, so
        // standing on a blocked tile does not make it walkable.
        Assert.Equal(Mp2ObjectType.ActionHero, tile.MainObjectType);
        Assert.Equal(Mp2ObjectType.None, tile.GetMainObjectType(world, false));
        Assert.Equal(blocked, tile.PassableDirections);
    }

    [Fact]
    public void ASlotHoldsOneHeroObjectPerId()
    {
        World world = CreateLandWorld();

        // AllHeroes is indexed by id (heroes.cpp:2411), so a second hero of one id cannot even be built.
        Assert.Same(world.GetHero(9), world.GetHero(9));
        Assert.NotSame(world.GetHero(9), world.GetHero(10));
    }

    [Fact]
    public void AHeroIdOutsideTheTableIsNotAHero()
    {
        World world = CreateLandWorld();

        Assert.Null(world.GetHero(0));
        Assert.Null(world.GetHero(-1));
        Assert.Null(world.GetHero(Hero.IdCount));
        Assert.NotNull(world.GetHero(9));
    }

    [Fact]
    public void TheHeroIdTableMatchesTheReference()
    {
        // heroes.h: 54 heroes of the six original races, 6 campaign heroes, 11 The Price of Loyalty
        // heroes and DEBUG_HERO, plus UNKNOWN at 0.
        Assert.Equal(73, Hero.IdCount);

        Assert.False(Hero.IsValidId(Hero.UnknownId));
        Assert.True(Hero.IsValidId(1));
        Assert.True(Hero.IsValidId(Hero.IdCount - 1));
        Assert.False(Hero.IsValidId(Hero.IdCount));

        Assert.Throws<ArgumentOutOfRangeException>(() => new Hero(Hero.UnknownId, MapRace.Knight));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hero(Hero.IdCount, MapRace.Knight));
    }

    [Fact]
    public void AHeroCannotParkAnotherHero()
    {
        Hero hero = CreateLandWorld().GetHero(4)!;

        // The reference writes OBJ_NONE instead of OBJ_HERO: a hero stands on one tile only, so a hero
        // under a hero would lose the type of the tile below.
        hero.SetObjectTypeUnderHero(Mp2ObjectType.ActionHero);
        Assert.Equal(Mp2ObjectType.None, hero.ObjectTypeUnderHero);

        hero.SetObjectTypeUnderHero(Mp2ObjectType.ActionResource);
        Assert.Equal(Mp2ObjectType.ActionResource, hero.ObjectTypeUnderHero);
    }

    [Fact]
    public void ResettingTheWorldDropsTheHeroes()
    {
        World world = CreateLandWorld();
        Assert.NotEmpty(world.Heroes);

        // AllHeroes::Clear() (heroes.h:845): the table is rebuilt by the next load, not kept across worlds.
        world.Reset();

        Assert.Empty(world.Heroes);
        Assert.Null(world.GetHero(2));
    }
}
