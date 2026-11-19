using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Spells;

/// <summary>
/// The numbers a spell produces with a given spell power, <c>spell_info.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the six formulas of <c>spell_info.cpp:69-184</c> as free functions and takes a
/// <c>HeroBase *</c> for the artifact bonuses. The port takes the hero's <see cref="ArtifactBag"/> instead:
/// it is the only part of the hero the formulas read, and it keeps the file free of <see cref="Hero"/>.
/// A <see langword="null"/> bag is the <c>hero == nullptr</c> branch — the number without any artifact.
/// </para>
/// <para>
/// The four map queries below (<see cref="GetNearestCastle"/>, <see cref="GetPossibleBoatPosition"/>,
/// <see cref="GetSummonableBoat"/>, <see cref="IsHeroNearWater"/>) are the rest of the same file.
/// </para>
/// <para>
/// <b>Not ported</b>: <c>getSpellDescription</c> (<c>spell_info.cpp:214-427</c>), the long sentence that wraps these
/// numbers with the caster's state — it wants a <c>HeroBase</c>, the nearest castle and the map, so it waits for the
/// spell book screen. The spell's own short description is <see cref="SpellText.GetDescription"/>.
/// </para>
/// </remarks>
public static class SpellInfo
{
    /// <summary>
    /// The damage of the spell at that spell power, <c>getSpellDamage()</c>
    /// (<c>spell_info.cpp:69-110</c>).
    /// </summary>
    /// <remarks>
    /// Cold, fire and lightning damage are the three kinds an artifact can raise — one list per kind, applied
    /// one artifact at a time and truncated. Everything else is multiplied by nothing: the reference has no
    /// check for "is this a damage spell", so neither has this, and a spell whose <c>damage</c> is zero comes
    /// out zero.
    /// </remarks>
    /// <param name="spell">The spell cast.</param>
    /// <param name="spellPower">The spell power of the caster, at least one.</param>
    /// <param name="bag">The artifacts of the caster, or null for none.</param>
    /// <returns>The damage; zero for a spell that does no damage.</returns>
    public static uint GetDamage(Spell spell, uint spellPower, ArtifactBag? bag = null)
    {
        Debug.Assert(spellPower > 0);

        uint damage = spell.Damage() * spellPower;

        ArtifactBonusType type = spell.Type switch
        {
            SpellType.ColdRay or SpellType.ColdRing => ArtifactBonusType.ColdSpellExtraEffectivenessPercent,
            SpellType.Fireball or SpellType.FireBlast => ArtifactBonusType.FireSpellExtraEffectivenessPercent,
            SpellType.LightningBolt or SpellType.ChainLightning
                => ArtifactBonusType.LightningSpellExtraEffectivenessPercent,
            _ => ArtifactBonusType.None,
        };

        if (bag is null || type == ArtifactBonusType.None)
        {
            return damage;
        }

        foreach (int percent in bag.GetTotalMultipliedPercent(type))
        {
            damage = (uint)(damage * (100 + percent) / 100);
        }

        return damage;
    }

    /// <summary>
    /// How many monsters the summoning spell brings, <c>getSummonMonsterCount()</c>
    /// (<c>spell_info.cpp:112-130</c>).
    /// </summary>
    /// <remarks>
    /// <c>extraValue * spellPower</c> raised by the summoning artifacts. Like the reference it does not check
    /// that the spell is a summoning spell — the caller does.
    /// </remarks>
    /// <param name="spell">The spell cast.</param>
    /// <param name="spellPower">The spell power of the caster, at least one.</param>
    /// <param name="bag">The artifacts of the caster, or null for none.</param>
    /// <returns>The number of monsters the spell brings.</returns>
    public static uint GetSummonMonsterCount(Spell spell, uint spellPower, ArtifactBag? bag = null)
    {
        Debug.Assert(spellPower > 0);

        uint monsterCount = spell.ExtraValue() * spellPower;

        if (bag is null)
        {
            return monsterCount;
        }

        foreach (int percent in bag.GetTotalMultipliedPercent(ArtifactBonusType.SummoningSpellExtraEffectivenessPercent))
        {
            monsterCount = (uint)(monsterCount * (100 + percent) / 100);
        }

        return monsterCount;
    }

    /// <summary>
    /// How many hit points Cure and Mass Cure heal, <c>getHPRestorePoints()</c>
    /// (<c>spell_info.cpp:132-139</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Restore() * spellPower</c>. The hero parameter of the reference is explicitly unused — there is no
    /// artifact that makes healing better — so the port has no bag parameter either.
    /// </para>
    /// <para>
    /// The reference does not check that the spell heals; a spell whose <c>Restore()</c> is zero answers zero,
    /// which is what this returns.
    /// </para>
    /// </remarks>
    /// <param name="spell">The spell cast.</param>
    /// <param name="spellPower">The spell power of the caster, at least one.</param>
    /// <returns>The hit points healed.</returns>
    public static uint GetHPRestorePoints(Spell spell, uint spellPower)
    {
        Debug.Assert(spellPower > 0);

        return spell.Restore() * spellPower;
    }

    /// <summary>
    /// How many hit points of fallen troops a resurrection brings back, <c>getResurrectPoints()</c>
    /// (<c>spell_info.cpp:141-159</c>).
    /// </summary>
    /// <param name="spell">The spell cast.</param>
    /// <param name="spellPower">The spell power of the caster, at least one.</param>
    /// <param name="bag">The artifacts of the caster, or null for none.</param>
    /// <returns>The hit points resurrected.</returns>
    public static uint GetResurrectPoints(Spell spell, uint spellPower, ArtifactBag? bag = null)
    {
        Debug.Assert(spellPower > 0);

        uint points = spell.Resurrect() * spellPower;

        if (bag is null)
        {
            return points;
        }

        foreach (int percent in bag.GetTotalMultipliedPercent(ArtifactBonusType.ResurrectSpellExtraEffectivenessPercent))
        {
            points = (uint)(points * (100 + percent) / 100);
        }

        return points;
    }

    /// <summary>
    /// How many elementals a guardian spell leaves on the tile, <c>getGuardianMonsterCount()</c>
    /// (<c>spell_info.cpp:161-166</c>).
    /// </summary>
    /// <remarks>
    /// <c>extraValue * spellPower</c>. The reference takes no hero: no artifact raises a guardian.
    /// </remarks>
    /// <param name="spell">The spell cast, one of the four guardians.</param>
    /// <param name="spellPower">The spell power of the caster, at least one.</param>
    /// <returns>The number of elementals.</returns>
    public static uint GetGuardianMonsterCount(Spell spell, uint spellPower)
    {
        Debug.Assert(spellPower > 0);

        return spell.ExtraValue() * spellPower;
    }

    /// <summary>
    /// How many hit points of troops Hypnotize takes over, <c>getHypnotizeMonsterHPPoints()</c>
    /// (<c>spell_info.cpp:168-184</c>).
    /// </summary>
    /// <param name="spell">The spell cast, which the reference asserts is Hypnotize.</param>
    /// <param name="spellPower">The spell power of the caster, at least one.</param>
    /// <param name="bag">The artifacts of the caster, or null for none.</param>
    /// <returns>The hit points controlled.</returns>
    public static uint GetHypnotizeMonsterHPPoints(Spell spell, uint spellPower, ArtifactBag? bag = null)
    {
        Debug.Assert(spell.Type == SpellType.Hypnotize);
        Debug.Assert(spellPower > 0);

        uint points = spell.ExtraValue() * spellPower;

        if (bag is null)
        {
            return points;
        }

        foreach (int percent in bag.GetTotalMultipliedPercent(ArtifactBonusType.HypnotizeSpellExtraEffectivenessPercent))
        {
            points = (uint)(points * (100 + percent) / 100);
        }

        return points;
    }

    /// <summary>
    /// The castle of the kingdom closest to a point, <c>getNearestCastleTownGate()</c>
    /// (<c>spell_info.cpp:186-212</c>).
    /// </summary>
    /// <remarks>
    /// Squared tile distance, first castle wins a tie — that is what the reference's
    /// <c>minDistance &lt; 0 || distance &lt; minDistance</c> does. The hero is replaced by its kingdom's
    /// castle list and its centre, so the caller decides what "its castles" means.
    /// </remarks>
    /// <param name="geometry">The geometry of the world, for the castle centres.</param>
    /// <param name="castles">The castles to look at, in kingdom order.</param>
    /// <param name="from">The point to measure from — the hero's centre.</param>
    /// <returns>The nearest castle, or null when the list has none.</returns>
    public static CastleTown? GetNearestCastle(MapGeometry geometry, IReadOnlyList<CastleTown> castles, Point from)
    {
        CastleTown? nearest = null;
        int minDistance = -1;

        foreach (CastleTown castle in castles)
        {
            Point position = geometry.GetPoint(castle.TileIndex);
            int offsetX = from.X - position.X;
            int offsetY = from.Y - position.Y;
            int distance = (offsetX * offsetX) + (offsetY * offsetY);

            if (minDistance < 0 || distance < minDistance)
            {
                minDistance = distance;
                nearest = castle;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The tile a Summon Boat cast from that hero should put the boat on, <c>getPossibleBoatPosition()</c>
    /// (<c>spell_info.cpp:323-367</c>).
    /// </summary>
    /// <remarks>
    /// The nearest neighbour the hero can sail into and a boat may be summoned on. The reference checks the
    /// passability of the <b>hero's</b> tile towards the neighbour (the hero has to be able to leave in that
    /// direction), not the neighbour's own passability.
    /// </remarks>
    /// <param name="world">The world the hero stands in.</param>
    /// <param name="hero">The hero casting the spell.</param>
    /// <returns>The tile index, or -1 when there is nowhere to put the boat.</returns>
    public static int GetPossibleBoatPosition(World world, Hero hero)
    {
        // The reference asks MayCastAdventureSpells(), which is "the hero is placed on the map": a hero
        // without a colour is not in anybody's service yet.
        if (hero.Color == PlayerColor.None)
        {
            return -1;
        }

        if (world.Geometry is not MapGeometry geometry)
        {
            return -1;
        }

        int heroIndex = hero.MapIndex;
        Direction passability = world.GetTile(heroIndex).PassableDirections;
        Point heroPoint = geometry.GetPoint(heroIndex);

        int best = -1;
        int bestDistance = -1;

        foreach (int tileIndex in geometry.GetAroundIndexes(heroIndex))
        {
            if ((passability & geometry.GetDirection(heroIndex, tileIndex)) == 0)
            {
                continue;
            }

            if (!world.GetTile(tileIndex).IsSuitableForSummoningBoat())
            {
                continue;
            }

            Point point = geometry.GetPoint(tileIndex);
            int offsetX = point.X - heroPoint.X;
            int offsetY = point.Y - heroPoint.Y;
            int distance = (offsetX * offsetX) + (offsetY * offsetY);

            if (bestDistance < 0 || distance < bestDistance)
            {
                bestDistance = distance;
                best = tileIndex;
            }
        }

        return best;
    }

    /// <summary>
    /// The boat a hero may summon, <c>getSummonableBoat()</c> (<c>spell_info.cpp:369-420</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boats of the hero's own kingdom always can be summoned, the boats of any other kingdom never can,
    /// and the neutral boats — the ones the map placed — are the interesting case: on a Resurrection map
    /// anyone may summon them, on an original map only a human player may, and not even then while the AI
    /// runs the kingdom on the player's behalf.
    /// </para>
    /// <para>
    /// <b>A quirk of the reference, kept</b>: it returns the first boat further than one tile away rather
    /// than the nearest one, walking the boats in the order <c>GetObjectPositions</c> hands them out.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the hero stands in.</param>
    /// <param name="hero">The hero casting the spell.</param>
    /// <param name="version">The version of the map, which decides whether neutral boats are summonable.</param>
    /// <param name="player">
    /// The player of the hero's colour, for the AI check; null is treated as a human player.
    /// </param>
    /// <returns>The boat tile index, or -1 when none may be summoned.</returns>
    public static int GetSummonableBoat(World world, Hero hero, MapVersion version, Player? player = null)
    {
        int center = hero.MapIndex;
        PlayerColor heroColor = hero.Color;
        bool isResurrectionMap = version == MapVersion.Resurrection;
        bool isControlAi = player?.IsControlAi ?? false;
        bool isAiAutoControlMode = player?.IsAiAutoControlMode ?? false;

        if (world.Geometry is not MapGeometry geometry)
        {
            return -1;
        }

        // OBJ_BOAT carries the action bit (mp2.h:315), and with ignoreHeroes = false the cached type is what
        // is compared — a boat placed by SetBoat() is an ActionBoat.
        foreach (int boatSource in world.GetObjectPositions(center, Mp2ObjectType.ActionBoat, false))
        {
            PlayerColor boatColor = world.GetTile(boatSource).BoatOwnerColor;

            // Boats belonging to the hero's kingdom can always be summoned.
            if (boatColor != heroColor)
            {
                // Non-neutral boats (belonging to any other kingdom) can never be summoned.
                if (boatColor != PlayerColor.None)
                {
                    continue;
                }

                // Original maps: the AI may not summon neutral boats, because the original AI could not cast
                // the spell at all and the maps were built around that. It may while the player runs it.
                if (!isResurrectionMap && isControlAi && !isAiAutoControlMode)
                {
                    continue;
                }
            }

            if (geometry.GetStraightLineDistance(boatSource, center) > 1)
            {
                return boatSource;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether any neighbour of the hero is water, <c>isHeroNearWater()</c> (<c>spell_info.cpp:422-426</c>).
    /// </summary>
    /// <param name="world">The world the hero stands in.</param>
    /// <param name="hero">The hero to look around.</param>
    /// <returns>Whether a neighbour of the hero is water.</returns>
    public static bool IsHeroNearWater(World world, Hero hero)
    {
        if (world.Geometry is not MapGeometry geometry)
        {
            return false;
        }

        foreach (int tileIndex in geometry.GetAroundIndexes(hero.MapIndex))
        {
            if (world.GetTile(tileIndex).IsWater)
            {
                return true;
            }
        }

        return false;
    }
}
