using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Army;

/// <summary>
/// How an army is filled when the player is not the one filling it: the guardians of a map object,
/// <c>Army::setFromTile()</c> (<c>army.cpp:1040-1192</c>), and the three arrangements of
/// <c>army.cpp:1783-1975</c>.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is about the <i>shape</i> of an army rather than its members: which monsters stand on which
/// slots, how a fixed count is spread over three to five stacks, and which stack gets upgraded. The map's object
/// handlers and the castle logic call these; nothing here draws.
/// </para>
/// <para>
/// The randomness is seeded rather than global — <c>Rand::PCG32( world.GetMapSeed() + tileIndex )</c> — so the
/// same object always holds the same guardians on the same map, in the reference and here. The upgrade uses a
/// second generator (seed + tile + monster id) on purpose: changing the stack count must not also reshuffle
/// whether the centre stack is the upgraded version.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// Fills the army with the guardians of a map object, <c>Army::setFromTile()</c>
    /// (<c>army.cpp:1040-1192</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The eight objects with hand-written guardians are listed explicitly because their stacks are fixed and
    /// their counts are part of the map's design — a pyramid always holds ten vampire lords and three stacks of
    /// ten royal mummies, in that order, because the middle one is the stack that may be upgraded. Every other
    /// object falls back to the guardians the map gave it, either in the captured-object record or on the tile.
    /// </para>
    /// <para>
    /// The two fallbacks differ in the <c>allowUpgrade</c> flag: a stack the map placed on a tile may be the
    /// upgraded version (the original game's flavour), while the guardians recorded for a capturable object are
    /// exactly what the map file says.
    /// </para>
    /// <para>
    /// <b>Not ported:</b> the dragon city's campaign variant (<c>army.cpp:1152-1158</c>), which doubles the
    /// guardians when the scenario's victory condition is <c>CAPTURE_DRAGON_CITY</c>. Campaigns are not ported,
    /// so the count stays at one; the rest of the <c>switch</c> is complete.
    /// </para>
    /// </remarks>
    /// <param name="world">The world, for the captured-object records and the tile metadata.</param>
    /// <param name="tile">The tile of the object whose guardians are wanted.</param>
    public void SetFromTile(World world, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tile);

        Debug.Assert(Commander is null, "Only an army without a commander can be rebuilt from a tile.");

        Clean();

        bool isCaptureObject = Mp2ObjectTypes.IsCaptureObject(tile.MainObjectType);

        Color = isCaptureObject ? tile.GetColor(world) : PlayerColor.None;

        switch (tile.MainObjectType)
        {
        case Mp2ObjectType.Pyramid:
            _slots[0].Set(MonsterType.VampireLord, 10);
            _slots[1].Set(MonsterType.RoyalMummy, 10);
            _slots[2].Set(MonsterType.RoyalMummy, 10);
            _slots[3].Set(MonsterType.RoyalMummy, 10);
            _slots[4].Set(MonsterType.VampireLord, 10);
            break;

        case Mp2ObjectType.Graveyard:
            _slots[0].Set(MonsterType.MutantZombie, 20);
            _slots[1].Set(MonsterType.MutantZombie, 20);
            _slots[2].Set(MonsterType.MutantZombie, 20);
            _slots[3].Set(MonsterType.MutantZombie, 20);
            _slots[4].Set(MonsterType.MutantZombie, 20);
            break;

        case Mp2ObjectType.Shipwreck:
        {
            ShipwreckCaptureCondition condition = tile.GetShipwreckCaptureCondition();

            uint ghostCount = condition switch
            {
                ShipwreckCaptureCondition.Fight10GhostsAndGet1000Gold => 10,
                ShipwreckCaptureCondition.Fight15GhostsAndGet2000Gold => 15,
                ShipwreckCaptureCondition.Fight25GhostsAndGet5000Gold => 25,
                ShipwreckCaptureCondition.Fight50GhostsAndGet2000GoldWithArtifact => 50,

                // Either the guardians were already defeated, or the tile stores something that is not a
                // guardian condition at all. Both mean "no army".
                _ => 0,
            };

            if (ghostCount == 0)
            {
                return;
            }

            ArrangeForBattle(MonsterType.Ghost, ghostCount, tile.Index, false, world);
            break;
        }

        case Mp2ObjectType.DerelictShip:
            ArrangeForBattle(MonsterType.Skeleton, 200, tile.Index, false, world);
            break;

        case Mp2ObjectType.Artifact:
        {
            (MonsterType guardian, uint guardianCount) = tile.GetArtifactCaptureCondition(world) switch
            {
                ArtifactCaptureCondition.Fight50Rogues => (MonsterType.Rogue, 50u),
                ArtifactCaptureCondition.Fight1Genie => (MonsterType.Genie, 1u),
                ArtifactCaptureCondition.Fight1Paladin => (MonsterType.Paladin, 1u),
                ArtifactCaptureCondition.Fight1Cyclops => (MonsterType.Cyclops, 1u),
                ArtifactCaptureCondition.Fight1Phoenix => (MonsterType.Phoenix, 1u),
                ArtifactCaptureCondition.Fight1GreenDragon => (MonsterType.GreenDragon, 1u),
                ArtifactCaptureCondition.Fight1Titan => (MonsterType.Titan, 1u),
                ArtifactCaptureCondition.Fight1BoneDragon => (MonsterType.BoneDragon, 1u),

                // No guardian condition: the artifact is guarded by gold, by skills, by nothing, or it is a
                // scroll. The reference falls through to the end of the switch.
                _ => (MonsterType.Unknown, 0u),
            };

            if (guardianCount > 0)
            {
                ArrangeForBattle(guardian, guardianCount, tile.Index, false, world);
            }

            break;
        }

        case Mp2ObjectType.CityOfDead:
            _slots[0].Set(MonsterType.Zombie, 20);
            _slots[1].Set(MonsterType.VampireLord, 5);
            _slots[2].Set(MonsterType.PowerLich, 5);
            _slots[3].Set(MonsterType.VampireLord, 5);
            _slots[4].Set(MonsterType.Zombie, 20);
            break;

        case Mp2ObjectType.TrollBridge:
            _slots[0].Set(MonsterType.Troll, 4);
            _slots[1].Set(MonsterType.WarTroll, 4);
            _slots[2].Set(MonsterType.Troll, 4);
            _slots[3].Set(MonsterType.WarTroll, 4);
            _slots[4].Set(MonsterType.Troll, 4);
            break;

        case Mp2ObjectType.DragonCity:
        {
            // The campaign's CAPTURE_DRAGON_CITY scenario doubles these; campaigns are not ported.
            const uint dragonCityCount = 1;

            _slots[0].Set(MonsterType.GreenDragon, dragonCityCount);
            _slots[1].Set(MonsterType.GreenDragon, dragonCityCount);
            _slots[2].Set(MonsterType.GreenDragon, dragonCityCount);
            _slots[3].Set(MonsterType.RedDragon, dragonCityCount);
            _slots[4].Set(MonsterType.BlackDragon, dragonCityCount);
            break;
        }

        case Mp2ObjectType.DaemonCave:
            // Four stacks of two, not five: the reference leaves the last slot empty here and only here.
            _slots[0].Set(MonsterType.EarthElement, 2);
            _slots[1].Set(MonsterType.EarthElement, 2);
            _slots[2].Set(MonsterType.EarthElement, 2);
            _slots[3].Set(MonsterType.EarthElement, 2);
            break;

        default:
            if (isCaptureObject)
            {
                Troop guardians = world.GetCapturedObject(tile.Index).Guardians;

                if (guardians.IsValid)
                {
                    ArrangeForBattle(guardians.Monster, guardians.Count, tile.Index, false, world);
                }
            }
            else
            {
                Troop monsters = tile.GetTroop(world);

                if (monsters.IsValid)
                {
                    ArrangeForBattle(monsters.Monster, monsters.Count, tile.Index, true, world);
                }
            }

            break;
        }
    }

    /// <summary>
    /// Moves the garrison's troops into a visiting hero's army, <c>Army::ArrangeForCastleDefense()</c>
    /// (<c>army.cpp:1783-1864</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs when a hero enters a castle: the <i>hero's</i> army (the instance this is called on) receives
    /// troops from <paramref name="garrison"/>, and the garrison is left as empty as needed — it is a transfer
    /// dressed as an arrangement.
    /// </para>
    /// <para>
    /// Two behaviours, and the second is why the caller must say who is playing: an AI hero gets as many garrison
    /// stacks as will fit, strongest first, merging duplicates in his own army when a slot runs out; a human
    /// hero's army only receives the garrison stacks that land on the matching slot number, so the player's
    /// arrangement is otherwise untouched. That is not a limitation but the point — a human player would not
    /// accept entering a castle silently reshuffling his army.
    /// </para>
    /// <para>
    /// The result says whether anything moved; a full human army entering an empty garrison moves nothing, and
    /// the castle keeps its troops.
    /// </para>
    /// </remarks>
    /// <param name="garrison">The castle's garrison army.</param>
    /// <param name="isControlAi">
    /// Whether the hero's player is the AI, <c>isControlAI()</c>. The port's armies have no player to ask, so the
    /// caller supplies the answer.
    /// </param>
    /// <returns>Whether any troops moved.</returns>
    public bool ArrangeForCastleDefense(Army garrison, bool isControlAi)
    {
        ArgumentNullException.ThrowIfNull(garrison);

        Debug.Assert(!ReferenceEquals(this, garrison), "An army cannot reinforce itself.");
        Debug.Assert(Commander?.IsHeroes == true, "Only a hero's army can be reinforced from a garrison.");
        Debug.Assert(garrison.Commander is null || !garrison.Commander.IsHeroes,
            "Reinforcements must come from a garrison or a captain, which may be left empty.");

        bool result = false;

        // If the guest hero's army is controlled by AI, then try to squeeze as many garrison troops in as
        // possible.
        if (isControlAi)
        {
            List<Troop> garrisonTroops = [];

            foreach (Troop troop in garrison._slots)
            {
                if (troop.IsValid)
                {
                    garrisonTroops.Add(troop);
                }
            }

            // Sort the garrison troops by their strength (most powerful stacks first).
            garrisonTroops.Sort(static (left, right) => right.GetStrength().CompareTo(left.GetStrength()));

            // Try to reinforce the guest hero's army with garrison troops (most powerful stacks first).
            foreach (Troop troop in garrisonTroops)
            {
                if (JoinTroop(troop))
                {
                    troop.Reset();

                    result = true;

                    continue;
                }

                // If there is no space for another garrison stack, we will try to combine some existing
                // stacks... and try again.
                if (MergeSameMonsterOnce())
                {
                    if (JoinTroop(troop))
                    {
                        troop.Reset();

                        result = true;
                    }
                    else
                    {
                        Debug.Assert(false, "Merging a stack makes room, so the next join must succeed.");
                    }
                }
            }

            // The reference asserts a full army here, because it also asserts on entry that both armies are
            // full. The port accepts a partially filled hero army (Castle::ActionPreBattle can hand over a
            // garrison of fewer stacks), so the invariant is the weaker one: troops are only left behind when
            // there is no slot and no duplicate to merge.
            Debug.Assert(garrison.OccupiedSlotCount == 0 || OccupiedSlotCount == MaximumTroopCount,
                "Garrison troops are only left behind by a full army.");

            return result;
        }

        // Otherwise, try to move the garrison troops to exactly the same slots of the guest hero's army,
        // provided that these slots are empty.
        for (int slot = 0; slot < MaximumTroopCount; ++slot)
        {
            Troop troop = _slots[slot];
            Troop garrisonTroop = garrison._slots[slot];

            if (!garrisonTroop.IsValid || troop.IsValid)
            {
                continue;
            }

            troop.Set(garrisonTroop.Monster, garrisonTroop.Count);
            garrisonTroop.Reset();

            result = true;
        }

        return result;
    }

    /// <summary>
    /// Merges duplicates and splits the weakest stack in two, <c>Army::ArrangeForWhirlpool()</c>
    /// (<c>army.cpp:1866-1910</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whirlpool drowns a share of the army, and the original game's tale is that what survives is
    /// reorganised: one stack per monster, then — if that left room — the weakest monster loses a single creature
    /// to a new stack. The point is to hurt a little and reshuffle, never to leave an obviously optimal army
    /// behind.
    /// </para>
    /// <para>
    /// The split is skipped when the weakest stack holds one creature: taking it would empty the stack, and the
    /// reference prefers a smaller change to a lost creature. A full house is left alone for the same reason —
    /// there is no room to reshuffle into.
    /// </para>
    /// </remarks>
    public void ArrangeForWhirlpool()
    {
        // Make an "optimized" version first (each unit type occupies just one slot).
        List<Troop> optimizedTroops = GetOptimized();

        Debug.Assert(optimizedTroops.Count > 0 && optimizedTroops.Count <= MaximumTroopCount);

        // Already a full house, there is no room for further optimization.
        if (optimizedTroops.Count >= MaximumTroopCount)
        {
            return;
        }

        Assign(optimizedTroops);

        // Look for a troop consisting of the weakest units.
        Troop? troopOfWeakestUnits = null;
        double weakestStrength = 0;

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            double strength = MonsterStatistics.GetMonsterStrength(troop.Monster);

            if (troopOfWeakestUnits is null || weakestStrength > strength)
            {
                troopOfWeakestUnits = troop;
                weakestStrength = strength;
            }
        }

        Debug.Assert(troopOfWeakestUnits is not null, "An army with no troops has nothing to rearrange.");

        if (troopOfWeakestUnits is null || troopOfWeakestUnits.Count == 1)
        {
            // There is already just one unit in this troop, let's leave it as it is.
            return;
        }

        // Move one unit from this troop's slot...
        --troopOfWeakestUnits.Count;

        // ...to the separate slot.
        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                troop.Set(troopOfWeakestUnits.Monster, 1);
                return;
            }
        }

        Debug.Assert(false, "The optimized army left a free slot.");
    }

    /// <summary>
    /// Spreads a count of one monster over a given number of stacks,
    /// <c>Army::ArrangeForBattle( const Monster &amp;, uint32_t, uint32_t )</c> (<c>army.cpp:1912-1941</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stacks are centred — <c>(5 - stacksCount) / 2</c> empty slots on each side, rounding down, so five
    /// stacks fill everything and one stack sits in the middle. The remainder goes to the leftmost stacks one
    /// creature each, the same convention as <see cref="AddNewTroopsToFreeSlots"/>: the biggest stack is always
    /// the first one.
    /// </para>
    /// <para>
    /// A count smaller than the requested number of stacks quietly uses fewer stacks: the reference walks
    /// <c>stacks</c> down until <c>count / stacks &gt; 0</c>, which is the same as "never leave a stack empty".
    /// </para>
    /// </remarks>
    /// <param name="monster">The monster of every stack.</param>
    /// <param name="monstersCount">How many creatures in total; must be positive.</param>
    /// <param name="stacksCount">How many stacks to spread them over, 1 to 5.</param>
    public void ArrangeForBattle(MonsterType monster, uint monstersCount, uint stacksCount)
    {
        Debug.Assert(stacksCount > 0 && stacksCount <= MaximumTroopCount);
        Debug.Assert(monstersCount > 0);

        foreach (Troop troop in _slots)
        {
            Debug.Assert(!troop.IsValid, "An arrangement needs an empty army.");
        }

        uint stacks;

        for (stacks = stacksCount; stacks > 0; --stacks)
        {
            if (monstersCount / stacks > 0)
            {
                break;
            }
        }

        Debug.Assert(stacks > 0, "A positive count always fills at least one stack.");

        if (stacks == 0)
        {
            return;
        }

        uint quotient = monstersCount / stacks;
        uint remainder = monstersCount % stacks;
        uint shift = (MaximumTroopCount - stacksCount) / 2;

        for (uint stack = 0; stack < stacks; ++stack)
        {
            _slots[stack + shift].Set(monster, stack < remainder ? quotient + 1 : quotient);
        }
    }

    /// <summary>
    /// Fills the army with the guardians of one map object, choosing the number of stacks from the object's
    /// position, <c>Army::ArrangeForBattle( const Monster &amp;, uint32_t, int32_t, bool )</c>
    /// (<c>army.cpp:1943-1975</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Archers are always split into all five stacks — five stacks of shooters shoot five times, so the original
    /// game never gives the player the mercy of two big ones. Everything else gets three to five stacks from a
    /// generator seeded with the object's position, which is what makes the same graveyard look the same every
    /// time the player loads the map.
    /// </para>
    /// <para>
    /// The upgrade is decided by a second generator seeded with the position <i>and</i> the monster, and only the
    /// centre stack — the one a player's eye lands on — may be the upgraded version, at fifty percent.
    /// </para>
    /// </remarks>
    /// <param name="monster">The monster of the guardians.</param>
    /// <param name="monstersCount">How many in total.</param>
    /// <param name="tileIndex">The tile of the object, which seeds both generators.</param>
    /// <param name="allowUpgrade">Whether the centre stack may be the upgraded monster.</param>
    /// <param name="world">The world, for the map seed.</param>
    public void ArrangeForBattle(MonsterType monster, uint monstersCount, int tileIndex, bool allowUpgrade, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        uint stacksCount;

        // Archers should always be divided into as many stacks as possible.
        if (MonsterStatistics.GetBattleStats(monster).IsArchers)
        {
            stacksCount = MaximumTroopCount;
        }
        else
        {
            Pcg32 seededGenerator = new(world.MapSeed + (uint)tileIndex);

            stacksCount = Rand.GetWithGen(3, 5, seededGenerator);
        }

        ArrangeForBattle(monster, monstersCount, stacksCount);

        if (!allowUpgrade)
        {
            return;
        }

        // An upgraded stack can be located only in the center.
        Troop troopToUpgrade = _slots[MaximumTroopCount / 2];

        if (troopToUpgrade.IsValid && troopToUpgrade.IsAllowUpgrade)
        {
            Pcg32 seededGenerator = new(world.MapSeed + (uint)tileIndex + (uint)monster);

            // 50% chance to get an upgraded stack.
            if (Rand.GetWithGen(0, 1, seededGenerator) == 1)
            {
                troopToUpgrade.Upgrade();
            }
        }
    }
}
