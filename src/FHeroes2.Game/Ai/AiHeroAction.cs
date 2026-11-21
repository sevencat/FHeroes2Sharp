using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Ai;

/// <summary>
/// What an AI hero does on the adventure map, the free functions of <c>ai_hero_action.h</c>
/// (<c>ai_hero_action.cpp</c>).
/// </summary>
/// <remarks>
/// <b>This is the contract, not the implementation</b> (ADR-161): the bodies throw until <c>ai_hero_action.cpp</c> is
/// ported. The file is the counterpart of <c>HeroActions</c> for the player (ADR-132) - the same objects, the same
/// tile rules, but the decisions are taken by the AI instead of by a click.
/// </remarks>
public static class AiHeroAction
{
    /// <summary>
    /// Performs the action of the object the hero stands next to, <c>AI::HeroesAction()</c>
    /// (<c>ai_hero_action.h:34</c>).
    /// </summary>
    /// <param name="hero">The hero that acts.</param>
    /// <param name="dstIndex">Tile index of the object.</param>
    public static void HeroesAction(Hero hero, int dstIndex) =>
        throw new NotImplementedException("AiHeroAction.HeroesAction — ai_hero_action.cpp 未移植（ADR-161）");

    /// <summary>
    /// Moves a hero for one turn, <c>AI::HeroesMove()</c> (<c>ai_hero_action.h:37</c>).
    /// </summary>
    /// <param name="hero">The hero to move.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when the hero has nothing left to do.</returns>
    public static AiTurnOutcome HeroesMove(Hero hero) =>
        throw new NotImplementedException("AiHeroAction.HeroesMove — ai_hero_action.cpp 未移植（ADR-161）");

    /// <summary>
    /// Makes a hero cast Dimension Door, <c>AI::HeroesCastDimensionDoor()</c> (<c>ai_hero_action.cpp:2423-2460</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hero jumps, pays for the spell, sees the new neighbourhood, and then the tile he landed on is settled like
    /// any other arrival — a monster guard there may still start a battle.
    /// </para>
    /// <para>
    /// The reference refuses a jump onto a tile whose land/water kind does not match the hero's (a landsman cannot
    /// land on water). The port has no <c>SHIPMASTER</c> mode yet, so it cannot tell whether the hero is on a boat and
    /// leaves that check out; the pathfinder already keeps boats out of a land hero's route.
    /// </para>
    /// <para>
    /// The fade-out/fade-in around the jump are animations of the adventure map and are not here.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that casts.</param>
    /// <param name="targetIndex">Tile to jump to.</param>
    public static void HeroesCastDimensionDoor(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;

        if (world.Geometry?.IsValidAbsIndex(targetIndex) != true)
        {
            return;
        }

        Spell dimensionDoor = new(SpellType.DimensionDoor);

        if (!hero.CanCastSpell(dimensionDoor))
        {
            // 没有这个法术就根本不该走到这里。
            Debug.Fail("Dimension door is cast by a hero who cannot cast it.");

            return;
        }

        hero.MovementHandler.Scout(hero, targetIndex);
        hero.MoveToDest(targetIndex, world);

        hero.SpellCasted(dimensionDoor);
        ++hero.DimensionDoorUses;
        hero.Path.Reset();

        hero.MovementHandler.ActionNewPosition(hero);
    }

    /// <summary>
    /// Makes a hero summon a boat, <c>AI::HeroesCastSummonBoat()</c> (<c>ai_hero_action.cpp:2461-2502</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boat that is nearest to the hero is taken off its tile and put down on the destination tile, flying the
    /// hero's flag. The caller has to make sure the hero may cast the spell and that there is a boat to summon; the
    /// function does not check either.
    /// </para>
    /// <para>
    /// The two object animations — the boat fading out on its old tile and in on the new one — are the adventure
    /// map's business and are not here.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that casts.</param>
    /// <param name="boatDestinationIndex">Tile the boat appears on.</param>
    /// <returns>Tile the boat was on before the summoning.</returns>
    public static int HeroesCastSummonBoat(Hero hero, int boatDestinationIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;
        MapGeometry geometry = world.Geometry!.Value;

        Debug.Assert(geometry.IsValidAbsIndex(boatDestinationIndex), "The boat lands on a tile of the map.");

        Spell summonBoat = new(SpellType.SummonBoat);

        Debug.Assert(hero.CanCastSpell(summonBoat), "The caller checked that the hero can cast the spell.");

        MapVersion mapVersion = Settings.Get().CurrentMapFileInfo?.Version ?? MapVersion.SuccessionWars;
        int boatSource = SpellInfo.GetSummonableBoat(world, hero, mapVersion, Players.Get(hero.Color));

        // 调用方必须已经确认有一条船可以召。
        Debug.Assert(geometry.IsValidAbsIndex(boatSource), "A boat must be summonable before it is summoned.");

        hero.SpellCasted(summonBoat);

        Tile tileSource = world.GetTile(boatSource);
        tileSource.RemoveMainObject(world);

        Tile tileDestination = world.GetTile(boatDestinationIndex);
        Debug.Assert(tileDestination.IsSuitableForSummoningBoat(), "The boat lands on a tile that can hold one.");

        tileDestination.SetBoat(world, Direction.Right, hero.Color);
        tileSource.ResetBoatOwnerColor();

        return boatSource;
    }
}
