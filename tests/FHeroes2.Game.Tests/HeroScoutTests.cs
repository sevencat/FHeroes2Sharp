using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the scouting that opens the game with a lit disc around every hero and town:
/// <c>Heroes::GetScoutingDistance()</c> (<c>heroes.cpp:1768</c>), <c>Heroes::Scout()</c>
/// (<c>heroes.cpp:1749</c>), <c>AllHeroes::Scout()</c> (<c>heroes.cpp:2435</c>),
/// <c>Castle::Scout()</c> (<c>castle.cpp:2213</c>), <c>AllCastles::Scout()</c> (<c>castle.cpp:2380</c>) and
/// <c>World::ClearFog( color )</c> (<c>world.cpp:838</c>).
/// </summary>
public sealed class HeroScoutTests
{
    private const int MapSize = 41;
    private const int CenterColumn = 20;
    private const int CenterRow = 20;
    private const int CenterTile = (CenterRow * MapSize) + CenterColumn;

    private const int OtherColumn = 35;
    private const int OtherRow = 5;
    private const int OtherTile = (OtherRow * MapSize) + OtherColumn;

    private static World FogWorld()
    {
        World world = new();
        world.GenerateUninitializedMap(MapSize);
        return world;
    }

    private static Hero BlueHero(int heroId = 1)
    {
        Hero hero = new(heroId, MapRace.Knight);
        hero.SetColor(PlayerColor.Blue);
        hero.SetMapIndex(CenterTile);

        return hero;
    }

    private static CastleTown BlueTown(PlayerColor color = PlayerColor.Blue)
    {
        CastleTown town = new(0, MapRace.Knight, MapVersion.PriceOfLoyalty);
        town.ChangeColor(color);

        return town;
    }

    [Fact]
    public void TheBaseScoutingDistanceIsFour()
    {
        Hero hero = new(1, MapRace.Knight);

        Assert.Equal(MapFog.HeroRevealDistance, hero.GetScoutingDistance());
        Assert.Equal(4, hero.GetScoutingDistance());
    }

    [Fact]
    public void TheScoutingSkillWidensTheDisc()
    {
        Hero basic = new(1, MapRace.Knight);
        basic.LearnSkill(new SecondarySkillLevel(SecondarySkill.Scouting, SkillLevel.Basic));

        Hero expert = new(2, MapRace.Wizard);
        expert.LearnSkill(new SecondarySkillLevel(SecondarySkill.Scouting, SkillLevel.Expert));

        Assert.Equal(5, basic.GetScoutingDistance());
        Assert.Equal(7, expert.GetScoutingDistance());
    }

    [Fact]
    public void TheTelescopeWidensTheDisc()
    {
        Hero hero = new(1, MapRace.Knight);

        Assert.True(hero.BagArtifacts.Push(ArtifactType.Telescope));
        Assert.Equal(5, hero.GetScoutingDistance());
    }

    [Fact]
    public void TheHeroUncoversTheDiscAroundHisTile()
    {
        World world = FogWorld();
        Hero hero = BlueHero();

        int revealed = hero.Scout(world, CenterTile);

        MapFog fog = world.Fog!;

        Assert.True(revealed > 0);
        Assert.False(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));

        // Four tiles of radius: the limit is (4+1)^2-4 = 21, so (4,0) is in and (5,0) is out.
        Assert.False(fog.IsFogged(CenterColumn + 4, CenterRow, PlayerColor.Blue));
        Assert.True(fog.IsFogged(CenterColumn + 5, CenterRow, PlayerColor.Blue));

        // Only the hero's own color: the same tiles stay dark for everybody else.
        Assert.True(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Red));
    }

    [Fact]
    public void TheScoutingSkillReachesFurther()
    {
        World world = FogWorld();
        Hero hero = BlueHero();
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Scouting, SkillLevel.Expert));
        hero.BagArtifacts.Push(ArtifactType.Telescope);

        hero.Scout(world, CenterTile);

        MapFog fog = world.Fog!;

        // Eight tiles of radius: the limit is (8+1)^2 = 81.
        Assert.False(fog.IsFogged(CenterColumn + 8, CenterRow, PlayerColor.Blue));
        Assert.True(fog.IsFogged(CenterColumn + 9, CenterRow, PlayerColor.Blue));
    }

    [Fact]
    public void AHeroWithoutAColorRevealsNothing()
    {
        World world = FogWorld();
        Hero hero = new(1, MapRace.Knight);

        Assert.Equal(PlayerColor.None, hero.Color);
        Assert.Equal(0, hero.Scout(world, CenterTile));
        Assert.True(world.Fog!.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));
    }

    [Fact]
    public void HeroesScoutOnlyForTheirOwnColor()
    {
        World world = FogWorld();
        world.Heroes.Init(MapVersion.PriceOfLoyalty);

        List<Hero> heroes = [.. world.Heroes];
        Hero blue = heroes[0];
        Hero red = heroes[1];

        blue.SetColor(PlayerColor.Blue);
        blue.SetMapIndex(CenterTile);
        red.SetColor(PlayerColor.Red);
        red.SetMapIndex(OtherTile);

        Assert.True(world.Heroes.Scout(world, PlayerColor.Blue) > 0);

        MapFog fog = world.Fog!;

        Assert.False(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));

        // The red hero is not scouted by a blue call, and scouting is not a move: his own tile stays dark.
        Assert.True(fog.IsFogged(OtherColumn, OtherRow, PlayerColor.Blue));
        Assert.True(fog.IsFogged(OtherColumn, OtherRow, PlayerColor.Red));
        Assert.Equal(0, world.Heroes.Scout(world, PlayerColor.None));
    }

    [Fact]
    public void TownsScoutOnlyForTheirOwner()
    {
        World world = FogWorld();
        world.Castles.Add(BlueTown());

        Assert.Equal(0, world.Castles.Scout(world, PlayerColor.Red));
        Assert.True(world.Castles.Scout(world, PlayerColor.Blue) > 0);

        MapFog fog = world.Fog!;

        Assert.False(fog.IsFogged(0, 0, PlayerColor.Blue));

        // Five tiles of radius: the limit is (5+1)^2-5 = 31, so (5,0) is in and (6,0) is out.
        Assert.False(fog.IsFogged(5, 0, PlayerColor.Blue));
        Assert.True(fog.IsFogged(6, 0, PlayerColor.Blue));
        Assert.True(fog.IsFogged(0, 0, PlayerColor.Red));
    }

    [Fact]
    public void AnOwnerlessTownRevealsNothing()
    {
        World world = FogWorld();
        world.Castles.Add(BlueTown(PlayerColor.None));

        Assert.Equal(0, world.Castles.Scout(world, PlayerColor.Blue));
        Assert.True(world.Fog!.IsFogged(0, 0, PlayerColor.Blue));
    }

    [Fact]
    public void TheWorldScoutsEveryTownAndHeroOfAColor()
    {
        World both = OwnedWorld(withHero: true, withTown: true).World;
        World heroOnly = OwnedWorld(withHero: true, withTown: false).World;
        World townOnly = OwnedWorld(withHero: false, withTown: true).World;

        int heroTiles = heroOnly.ClearFog(PlayerColor.Blue);
        int townTiles = townOnly.ClearFog(PlayerColor.Blue);

        Assert.True(heroTiles > 0);
        Assert.True(townTiles > 0);
        Assert.Equal(heroTiles + townTiles, both.ClearFog(PlayerColor.Blue));
        Assert.Equal(0, both.ClearFog(PlayerColor.None));
    }

    [Fact]
    public void TheWorldWideClearFogCoversBothSources()
    {
        (World world, _, _) = OwnedWorld(withHero: true, withTown: true);

        world.ClearFog(PlayerColor.Blue);

        MapFog fog = world.Fog!;

        Assert.False(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));
        Assert.False(fog.IsFogged(0, 0, PlayerColor.Blue));
        Assert.True(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Red));
    }

    [Fact]
    public void TheFogHandlerUncoversTheTileTheHeroStepsInto()
    {
        World world = FogWorld();
        Hero hero = BlueHero();
        hero.MovementHandler = new FogHeroMovementHandler(world);

        // _moveStep() hands the handler the tile the hero is about to enter, not the one he stands on.
        hero.MovementHandler.Scout(hero, CenterTile + 1);

        Assert.False(world.Fog!.IsFogged(CenterColumn + 1, CenterRow, PlayerColor.Blue));
        Assert.True(world.Fog!.IsFogged(CenterColumn, CenterRow + 10, PlayerColor.Blue));
    }

    private static (World World, Hero? Hero, CastleTown? Town) OwnedWorld(bool withHero, bool withTown)
    {
        World world = FogWorld();
        world.Heroes.Init(MapVersion.PriceOfLoyalty);

        Hero? hero = null;
        CastleTown? town = null;

        if (withHero)
        {
            hero = world.Heroes.First();
            hero.SetColor(PlayerColor.Blue);
            hero.SetMapIndex(CenterTile);
        }

        if (withTown)
        {
            town = BlueTown();
            world.Castles.Add(town);
        }

        return (world, hero, town);
    }
}
