using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using Xunit;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the six formulas of <c>spell_info.cpp:69-184</c> and the four map queries of
/// <c>spell_info.cpp:186-426</c>.
/// </summary>
public sealed class SpellInfoTests
{
    [Fact]
    public void TheElementalArtifactsRaiseOnlyTheirOwnKindOfDamage()
    {
        // Damage() is extraValue per point of spell power (Fireball 10, Cold Ray 20, Lightning Bolt 25).
        Assert.Equal(30u, SpellInfo.GetDamage(new Spell(SpellType.Fireball), 3));
        Assert.Equal(40u, SpellInfo.GetDamage(new Spell(SpellType.ColdRay), 2));
        Assert.Equal(50u, SpellInfo.GetDamage(new Spell(SpellType.LightningBolt), 2));

        // A second bag has to be built per case: GetTotalMultipliedPercent walks the whole bag.
        var fire = new ArtifactBag();
        fire.Push(ArtifactType.EverhotLavaRock);

        var cold = new ArtifactBag();
        cold.Push(ArtifactType.EvercoldIcicle);

        var lightning = new ArtifactBag();
        lightning.Push(ArtifactType.LightningRod);

        Assert.Equal(45u, SpellInfo.GetDamage(new Spell(SpellType.Fireball), 3, fire));
        Assert.Equal(60u, SpellInfo.GetDamage(new Spell(SpellType.ColdRay), 2, cold));
        Assert.Equal(75u, SpellInfo.GetDamage(new Spell(SpellType.LightningBolt), 2, lightning));

        // The three artifacts are per kind: a fire artifact does nothing for a magic arrow.
        Assert.Equal(30u, SpellInfo.GetDamage(new Spell(SpellType.Arrow), 3, fire));
        Assert.Equal(0u, SpellInfo.GetDamage(new Spell(SpellType.Bless), 3, fire));
    }

    [Fact]
    public void TheSummoningArtifactDoublesTheElementals()
    {
        // extraValue 3 per point of spell power.
        Assert.Equal(12u, SpellInfo.GetSummonMonsterCount(new Spell(SpellType.SummonEElement), 4));

        var bag = new ArtifactBag();
        bag.Push(ArtifactType.BookElements);

        Assert.Equal(24u, SpellInfo.GetSummonMonsterCount(new Spell(SpellType.SummonEElement), 4, bag));
        Assert.Equal(24u, SpellInfo.GetSummonMonsterCount(new Spell(SpellType.SummonWElement), 4, bag));
    }

    [Fact]
    public void TheHealingAndResurrectionSpellsCarryTheirOwnNumbers()
    {
        // Cure restores 5 HP per point of spell power; no artifact makes healing better.
        Assert.Equal(15u, SpellInfo.GetHPRestorePoints(new Spell(SpellType.Cure), 3));
        Assert.Equal(0u, SpellInfo.GetHPRestorePoints(new Spell(SpellType.Fireball), 3));

        // Resurrect brings 50 HP per point; the Ankh doubles it.
        Assert.Equal(100u, SpellInfo.GetResurrectPoints(new Spell(SpellType.Resurrect), 2));

        var ankh = new ArtifactBag();
        ankh.Push(ArtifactType.Ankh);

        Assert.Equal(200u, SpellInfo.GetResurrectPoints(new Spell(SpellType.Resurrect), 2, ankh));
        Assert.Equal(0u, SpellInfo.GetResurrectPoints(new Spell(SpellType.Fireball), 2));
    }

    [Fact]
    public void TheGuardiansAndHypnotizeBringTheirExtraValue()
    {
        // The four guardians bring extraValue 4 per point of spell power, with no artifact in the way.
        Assert.Equal(8u, SpellInfo.GetGuardianMonsterCount(new Spell(SpellType.SetEGuardian), 2));
        Assert.Equal(8u, SpellInfo.GetGuardianMonsterCount(new Spell(SpellType.SetWGuardian), 2));

        // Hypnotize controls extraValue 25 HP per point; the Gold Watch doubles it.
        Assert.Equal(50u, SpellInfo.GetHypnotizeMonsterHPPoints(new Spell(SpellType.Hypnotize), 2));

        var watch = new ArtifactBag();
        watch.Push(ArtifactType.GoldWatch);

        Assert.Equal(100u, SpellInfo.GetHypnotizeMonsterHPPoints(new Spell(SpellType.Hypnotize), 2, watch));
    }

    [Fact]
    public void TheNearestCastleIsTheFirstOfTheClosestOnes()
    {
        var world = new World();
        world.GenerateUninitializedMap(5);

        MapGeometry geometry = Assert.IsType<MapGeometry>(world.Geometry);
        CastleTown west = new(0, MapRace.Knight);
        CastleTown east = new(4, MapRace.Knight);

        // Every castle of the list is measured from the point; the first one wins a tie.
        Assert.Same(east, SpellInfo.GetNearestCastle(geometry, [west, east], new Point(4, 0)));
        Assert.Same(west, SpellInfo.GetNearestCastle(geometry, [west, east], new Point(2, 0)));
        Assert.Same(east, SpellInfo.GetNearestCastle(geometry, [east, west], new Point(2, 0)));
        Assert.Null(SpellInfo.GetNearestCastle(geometry, [], new Point(2, 0)));
    }

    [Fact]
    public void TheBoatGoesToTheWaterNeighbourTheHeroCanReach()
    {
        World world = HeroOnGrass(out Hero hero, out MapGeometry geometry);

        // Every neighbour is grass, so there is nowhere to put a boat.
        Assert.Equal(-1, SpellInfo.GetPossibleBoatPosition(world, hero));

        // One neighbour of the hero becomes water: that is the only candidate.
        world.GetTile(13).SetTerrain(MapGrounds.WaterStartImageIndex, 0);

        Assert.Equal(13, SpellInfo.GetPossibleBoatPosition(world, hero));
        Assert.True(SpellInfo.IsHeroNearWater(world, hero));
        Assert.Contains(13, geometry.GetAroundIndexes(12));
    }

    [Fact]
    public void AHeroWithoutAKingdomSummonsNeitherBoatNorWater()
    {
        World world = HeroOnGrass(out Hero hero, out _);
        hero.SetColor(PlayerColor.None);

        world.GetTile(13).SetTerrain(MapGrounds.WaterStartImageIndex, 0);

        Assert.Equal(-1, SpellInfo.GetPossibleBoatPosition(world, hero));
    }

    [Fact]
    public void TheSummonableBoatObeysTheOwnerAndTheMapVersion()
    {
        // The hero stands at (3, 3) of a 7 × 7 map; the boat sits two tiles to the west, which is the first
        // distance the reference accepts (spell_info.cpp:409).
        const int heroIndex = 24;
        const int boatIndex = 22;

        Assert.Equal(boatIndex, SummonBoat(MapVersion.Resurrection, PlayerColor.Blue, null));
        Assert.Equal(boatIndex, SummonBoat(MapVersion.SuccessionWars, PlayerColor.Blue, null));

        // Another kingdom's boat is never summonable, on either version.
        Assert.Equal(-1, SummonBoat(MapVersion.Resurrection, PlayerColor.Red, null));
        Assert.Equal(-1, SummonBoat(MapVersion.SuccessionWars, PlayerColor.Red, null));

        // A neutral boat on an original map is refused while the AI runs the human slot, and allowed once the
        // player takes it back.
        Player ai = new(PlayerColor.Blue) { Control = PlayerControl.Ai };
        Assert.Equal(-1, SummonBoat(MapVersion.SuccessionWars, PlayerColor.None, ai));

        Player served = new(PlayerColor.Blue) { Control = PlayerControl.Ai };
        served.SetAiAutoControlMode(true);
        Assert.Equal(boatIndex, SummonBoat(MapVersion.SuccessionWars, PlayerColor.None, served));

        // On a Resurrection map even the AI may take a neutral boat.
        Assert.Equal(boatIndex, SummonBoat(MapVersion.Resurrection, PlayerColor.None, ai));

        int SummonBoat(MapVersion version, PlayerColor boatColor, Player? player)
        {
            World world = BoatField(heroIndex, boatIndex, boatColor);
            Hero hero = new(1, MapRace.Knight);
            hero.SetColor(PlayerColor.Blue);
            hero.SetMapIndex(heroIndex);

            return SpellInfo.GetSummonableBoat(world, hero, version, player);
        }
    }

    /// <summary>A 5 × 5 grass field with the hero at (2, 2) and every neighbour walkable.</summary>
    private static World HeroOnGrass(out Hero hero, out MapGeometry geometry)
    {
        var world = new World();
        world.GenerateUninitializedMap(5);

        for (int index = 0; index < 25; ++index)
        {
            world.GetTile(index).SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        world.GetTile(12).SetInitialPassability();

        hero = new Hero(1, MapRace.Knight);
        hero.SetColor(PlayerColor.Blue);
        hero.SetMapIndex(12);

        geometry = Assert.IsType<MapGeometry>(world.Geometry);

        return world;
    }

    /// <summary>A 7 × 7 all-water map with one boat on the given tile.</summary>
    private static World BoatField(int heroIndex, int boatIndex, PlayerColor color)
    {
        var world = new World();
        world.GenerateUninitializedMap(7);

        // A fresh tile's terrain image is zero, which is water: the boat can be placed as is.
        world.GetTile(boatIndex).SetBoat(world, Direction.Right, color);

        return world;
    }
}
