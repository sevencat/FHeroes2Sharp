using System.Collections;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Army;

/// <summary>
/// <c>Army</c> of <c>src/fheroes2/army/army.h</c>: the five slots a hero or a town fights with.
/// </summary>
/// <remarks>
/// <para>
/// The reference <c>Army</c> derives from <c>Troops</c> (a <c>std::vector&lt;Troop *&gt;</c> of exactly
/// <c>maximumTroopCount</c> slots) and holds a <c>HeroBase * commander</c> back-pointer that supplies the
/// colour, the race, the morale and the luck. The port has neither a base class nor the back-pointer: the
/// slots live in an array, the race is a settable property, and the colour is what
/// <c>Army::GetColor()</c> returns for an army without a commander.
/// </para>
/// <para>
/// Every rule of <c>army.cpp</c> is ported. What is missing is not a rule but a dependency, kept in one list so
/// the boundary is visible: the battle helper <c>GetCurrentUnit</c>, the text of
/// <c>TroopSizeString</c>/<c>String</c> (plural monster names plus the translation layer), the three drawing
/// functions and all of <c>army_bar.cpp</c>/<c>army_ui_helper.cpp</c> (UI), the stream operators (no save format
/// yet) and <c>Army::GetControl</c> (no player object to ask). The castle and artifact terms of
/// <c>GetMoraleModificator()</c>/<c>GetLuckModificator()</c> are ported: they ask the commander for its
/// <see cref="IArmyCommander.InCastle"/> and its artifact bag, and stay at the neutral value when the commander has
/// neither. The
/// <c>Troops::Insert</c>/<c>PushBack</c>/<c>PopBack</c> trio needs no port: it mutates the reference's vector of
/// slots, while here the five slots are fixed and the open-ended containers are plain
/// <see cref="List{T}"/> objects.
/// </para>
/// <para>
/// The rest is split by subject rather than by file: <c>Army.Troops.cs</c> (the slot set),
/// <c>Army.Transfer.cs</c> (moving troops between armies), <c>Army.Split.cs</c> (spreading a stack over the free
/// slots), <c>Army.JoinSolution.cs</c> (whether neutral monsters join), <c>Army.Arrangement.cs</c> (the guardians
/// of a map object and the shape of a battle army) and <c>Army.Size.cs</c> (the nine size brackets).
/// </para>
/// <para>
/// The commander back-pointer of the reference is a nullable <see cref="IArmyCommander"/> here: the port has no
/// <c>HeroBase</c> to point at, and the strength calculation only needs four calls on it. An army without one is
/// a town's garrison or a wandering monster stack, and is worth what its troops are worth.
/// </para>
/// </remarks>
public sealed partial class Army : IEnumerable<Troop>
{
    /// <summary>Number of slots, <c>Army::maximumTroopCount</c> (<c>army.h:97</c>).</summary>
    public const int MaximumTroopCount = 5;

    private readonly Troop[] _slots = new Troop[MaximumTroopCount];

    /// <summary>An army of empty slots, <c>Army::Army()</c> (<c>army.cpp:1018</c>).</summary>
    public Army()
    {
        for (int i = 0; i < _slots.Length; ++i)
        {
            _slots[i] = new Troop();
        }
    }

    /// <summary>An army of a race, the port's stand-in for the commander's race.</summary>
    /// <param name="race">Race used by <see cref="Reset"/>.</param>
    public Army(MapRace race)
        : this()
    {
        Race = race;
    }

    /// <summary>
    /// The race the commander would report, <c>commander-&gt;GetRace()</c> in the reference. Used only by
    /// <see cref="Reset"/> to pick the monsters of the starting army.
    /// </summary>
    public MapRace Race { get; set; } = MapRace.None;

    /// <summary>
    /// The colour of the army, <c>Army::GetColor()</c> (<c>army.h</c>): the commander's colour in the
    /// reference, a stored value here.
    /// </summary>
    public PlayerColor Color { get; set; } = PlayerColor.None;

    /// <summary>
    /// Whether the army is deployed in the wide formation, <c>Army::isSpreadFormation()</c> /
    /// <c>SetSpreadFormation()</c> (<c>army.h:243-251</c>).
    /// </summary>
    /// <remarks>
    /// 只在开战摆位时用：铺开时五队兵每隔 22 格排一行，否则每 11 格一队（见 <c>BattleForce</c>）。
    /// 参考由竞技场（以及「军团旗帜」之类的地图效果）置位，端口先留一个可写属性。
    /// </remarks>
    public bool IsSpreadFormation { get; set; }

    /// <summary>
    /// Who commands the army, <c>Army::GetControl()</c> (<c>army.cpp:1387-1390</c>).
    /// </summary>
    /// <remarks>
    /// 有指挥官就问指挥官（英雄问王国、队长问城堡，两者最终都落到 <c>Players::GetPlayerControl</c>）；
    /// 没有指挥官的散兵（中立军）是电脑的。颜色为空的军队也按电脑算。
    /// </remarks>
    public PlayerControl GetControl() =>
        Commander?.GetControl() ?? (Color == PlayerColor.None ? PlayerControl.Ai : Players.GetPlayerControl(Color));

    /// <summary>The slots, in order. The array is fixed size; its slots are mutable.</summary>
    public IReadOnlyList<Troop> Slots => _slots;

    /// <summary>
    /// At least one slot holds something, <c>Troops::isValid()</c> (<c>army.cpp:368</c>).
    /// </summary>
    public bool IsValid
    {
        get
        {
            foreach (Troop troop in _slots)
            {
                if (troop.IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Number of slots holding something, <c>Troops::GetOccupiedSlotCount()</c> (<c>army.cpp:376</c>).
    /// </summary>
    public uint OccupiedSlotCount
    {
        get
        {
            uint count = 0;

            foreach (Troop troop in _slots)
            {
                if (troop.IsValid)
                {
                    ++count;
                }
            }

            return count;
        }
    }

    /// <summary>The slot at <paramref name="index"/>, <c>Troops::operator[]</c>.</summary>
    /// <param name="index">Zero-based slot index.</param>
    /// <returns>The slot; never <see langword="null"/>, empty slots are objects with no creature.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the five slots.</exception>
    public Troop this[int index] => GetTroop(index);

    /// <summary>The slot at <paramref name="index"/>, <c>Army::GetTroop()</c>.</summary>
    /// <param name="index">
    /// Zero-based slot index. A negative value counts from the end, as <c>GetTroop( -1 )</c> does in the
    /// reference's castle and battle code.
    /// </param>
    /// <returns>The slot.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the five slots.</exception>
    public Troop GetTroop(int index)
    {
        if (index < 0)
        {
            index += MaximumTroopCount;
        }

        if (index < 0 || index >= MaximumTroopCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Army has five slots.");
        }

        return _slots[index];
    }

    /// <summary>
    /// The first slot holding something, <c>Troops::GetFirstValid()</c> (<c>army.cpp:562</c>).
    /// </summary>
    /// <returns>The slot, or <see langword="null"/> when the army is empty.</returns>
    public Troop? GetFirstValidTroop()
    {
        foreach (Troop troop in _slots)
        {
            if (troop.IsValid)
            {
                return troop;
            }
        }

        return null;
    }

    /// <summary>
    /// The slowest valid stack, <c>Troops::GetSlowestTroop()</c> (<c>army.cpp:603</c>). Ties keep the earlier
    /// slot, which is what <c>Troops::getBestMatchToCondition()</c> does: the first valid stack is the
    /// baseline and a later stack must be strictly faster to lose.
    /// </summary>
    /// <returns>The stack, or <see langword="null"/> when the army is empty.</returns>
    public Troop? GetSlowestTroop() => GetBestMatch((troop, best) => troop.GetSpeed() < best.GetSpeed());

    /// <summary>
    /// The fastest valid stack, <c>Troops::GetFastestTroop()</c> (<c>army.cpp:608</c>).
    /// </summary>
    /// <returns>The stack, or <see langword="null"/> when the army is empty.</returns>
    public Troop? GetFastestTroop() => GetBestMatch((troop, best) => troop.GetSpeed() > best.GetSpeed());

    /// <summary>
    /// Total number of creatures, <c>Army::getTotalCount()</c> (<c>army.cpp:1392</c>): only valid slots are
    /// summed, so an empty slot contributes zero rather than whatever count it still carries.
    /// </summary>
    /// <returns>The sum.</returns>
    public uint GetCount()
    {
        uint count = 0;

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid)
            {
                count += troop.Count;
            }
        }

        return count;
    }

    /// <summary>Hit points of the whole army, <c>Troops::getTotalHP()</c> (<c>army.cpp:513</c>).</summary>
    /// <returns>The sum of the stacks' hit points.</returns>
    public uint GetTotalHitPoints()
    {
        uint hitPoints = 0;

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid)
            {
                hitPoints += troop.GetHitPoints();
            }
        }

        return hitPoints;
    }

    /// <summary>
    /// Whether every valid slot holds the same monster, <c>Troops::AllTroopsAreTheSame()</c>
    /// (<c>army.cpp:487</c>).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when no two valid slots differ. An empty army counts as the same: the
    /// reference's loop finds nothing to compare and returns <see langword="true"/>.
    /// </returns>
    public bool AllTroopsAreTheSame()
    {
        MonsterType first = MonsterType.Unknown;

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            if (first == MonsterType.Unknown)
            {
                first = troop.Monster;
            }
            else if (troop.Monster != first)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether every valid slot holds an undead stack, <c>Troops::AllTroopsAreUndead()</c>
    /// (<c>army.cpp:413</c>).
    /// </summary>
    /// <returns>
    /// <see langword="false"/> for an empty army — the reference keeps a "saw a valid slot" flag and
    /// returns it, because an army with nothing in it cannot be undead.
    /// </returns>
    public bool AllTroopsAreUndead()
    {
        bool hasValidTroop = false;

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            hasValidTroop = true;

            if (!troop.IsUndead)
            {
                return false;
            }
        }

        return hasValidTroop;
    }

    /// <summary>
    /// Whether no two valid slots hold the same monster, <c>Troops::areAllTroopsUnique()</c>
    /// (<c>army.cpp:384</c>).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when every valid stack is a different monster. An empty army counts as unique,
    /// like the same-monster query above — the reference has nothing to insert into its set and returns
    /// <see langword="true"/>. Only the AI planner asks this, right before it decides that a full army of five
    /// different stacks has no duplicate worth merging.
    /// </returns>
    public bool AreAllTroopsUnique()
    {
        HashSet<MonsterType> seen = [];

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            if (!seen.Add(troop.Monster))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The luck the army fights with, <c>Army::GetLuck()</c> (<c>army.cpp:1200</c>).
    /// </summary>
    /// <returns>
    /// With a commander: the commander's own luck, <c>commander-&gt;GetLuck()</c>; without one,
    /// <see cref="GetLuckModificator()"/> (the castle's term). A hero's army therefore takes no
    /// race/undead penalty from <see cref="GetLuckModificator()"/> — that is the reference's behaviour too,
    /// not a missing piece here.
    /// </returns>
    public int GetLuck() => Commander?.GetLuck() ?? GetLuckModificator();

    /// <summary>
    /// The luck an army has without its hero, <c>Army::GetLuckModificator()</c> (<c>army.cpp:1206</c>).
    /// </summary>
    /// <returns>
    /// <see cref="Luck.Normal"/> plus the castle's luck modifier when the army stands in a town. The reference has
    /// no other term here — luck comes from the hero, the artifacts and the town.
    /// </returns>
    public int GetLuckModificator()
    {
        int result = Luck.Normal;

        if (Commander?.InCastle is { } castle)
        {
            result += castle.GetLuckModificator();
        }

        return result;
    }

    /// <summary>
    /// The morale the army fights with, <c>Army::GetMorale()</c> (<c>army.cpp:1220</c>).
    /// </summary>
    /// <returns>
    /// With a commander: the commander's own morale, <c>commander-&gt;GetMorale()</c>; without one,
    /// <see cref="GetMoraleModificator()"/> — the multi-race and undead penalties only apply to an army
    /// nobody leads (<c>army.cpp:1220-1224</c>).
    /// </returns>
    public int GetMorale() => Commander?.GetMorale() ?? GetMoraleModificator();

    /// <summary>
    /// The morale an army has without its hero, <c>Army::GetMoraleModificator()</c>
    /// (<c>army.cpp:1226</c>): the castle's own modifier, a bonus for a single-race army, a penalty for every
    /// race past the second, and one more penalty for undead in the ranks.
    /// </summary>
    /// <returns>
    /// The sum, not clamped — the reference leaves the clamp to the caller
    /// (<c>Morale::Normalize()</c> in <c>Heroes::getMoraleWithModifiers</c>).
    /// </returns>
    /// <remarks>
    /// The castle term is added before the race terms, as in the reference. The undead penalty is decided after it,
    /// because the "Arm of the Martyr" curse turns a living army into one that takes the penalty: the reference
    /// folds the curse into <c>hasUndead</c> rather than subtracting twice.
    /// </remarks>
    public int GetMoraleModificator()
    {
        HashSet<MapRace> races = [];
        bool hasUndead = false;

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            races.Add(troop.GetRace());
            hasUndead = hasUndead || troop.IsUndead;
        }

        int result = Morale.Normal;

        if (Commander?.InCastle is { } castle)
        {
            result += castle.GetMoraleModificator();
        }

        // Artifact "Arm of the Martyr" adds the undead morale penalty to an army of the living.
        hasUndead = hasUndead
            || (Commander is not null && Commander.BagArtifacts.IsArtifactCursePresent(ArtifactCurseType.UndeadMoralePenalty));

        switch (races.Count)
        {
            case 0:
            case 2:
                break;

            case 1:
                // One race: "All troops +1", but not for undead and not for five stacks of one monster.
                if (!hasUndead && !AllTroopsAreTheSame())
                {
                    ++result;
                }

                break;

            default:
                result -= races.Count - 2;
                break;
        }

        if (hasUndead)
        {
            --result;
        }

        return result;
    }

    /// <summary>
    /// The commander of the army, <c>commander</c> (<c>army.h:305</c>).
    /// </summary>
    /// <remarks>
    /// A hero in the port's case. The reference also points it at a battle-only captain; both are behind
    /// <see cref="IArmyCommander"/>, which is the four methods the strength calculation needs.
    /// </remarks>
    public IArmyCommander? Commander { get; set; }

    /// <summary>
    /// Whether the army is a hero's and has only one stack left, <c>Army::SaveLastTroop()</c>
    /// (<c>army.cpp:1758-1761</c>).
    /// </summary>
    /// <remarks>
    /// The battle, the castle dialog and the AI all ask "can this army afford to give a stack away". A garrison
    /// answers no whatever its size — only a hero who loses his last stack loses the game — which is one of the
    /// two places <see cref="IArmyCommander.IsHeroes"/> is read.
    /// </remarks>
    /// <returns><see langword="true"/> for a hero's army with exactly one occupied slot.</returns>
    public bool SaveLastTroop() => Commander?.IsHeroes == true && OccupiedSlotCount == 1;

    /// <summary>
    /// The strength of the army, <c>Army::GetStrength()</c> (<c>army.cpp:1291-1333</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference walks the five stacks and for every one of them takes
    /// <c>Troop::GetStrengthWithBonus(attack, defense)</c>, scales shooters by the hero's archery with a square
    /// root, multiplies by the morale factor for the stacks morale applies to, multiplies everything by the luck
    /// factor, and finally adds <c>GetMagicStrategicValue()</c> once when the army is not empty and has a
    /// commander.
    /// </para>
    /// <para>
    /// Without a commander — a town's garrison, a wandering monster stack — the archery factor, the attack and
    /// defense bonuses and the magic value are all zero, which is what the reference computes too: every one of
    /// them is guarded by <c>commander != nullptr</c>. The morale and luck factors are the army's own numbers and
    /// stay in both cases.
    /// </para>
    /// <para>
    /// The reference's fifth term, <c>GetMagicStrategicValue()</c>, is currently zero in the port because the
    /// hero has no spell book yet; see <see cref="IArmyCommander.GetMagicStrategicValue"/>.
    /// </para>
    /// </remarks>
    /// <returns>The summed strength of the stacks, with the commander, morale and luck applied.</returns>
    public double GetStrength()
    {
        double result = 0;

        int morale = GetMorale();
        int luck = GetLuck();

        IArmyCommander? commander = Commander;
        uint archery = commander?.GetSecondarySkillValue(SecondarySkill.Archery) ?? 0;
        int bonusAttack = commander?.GetAttack() ?? 0;
        int bonusDefense = commander?.GetDefense() ?? 0;

        bool troopsExist = false;

        foreach (Troop troop in _slots)
        {
            if (troop.IsEmpty)
            {
                continue;
            }

            troopsExist = true;

            double strength = troop.GetStrengthWithBonus(bonusAttack, bonusDefense);

            if (archery > 0 && troop.IsArchers)
            {
                strength *= Math.Sqrt(1 + archery / 100.0);
            }

            if (troop.IsAffectedByMorale)
            {
                strength *= 1 + ((morale < 0) ? morale / 12.0 : morale / 24.0);
            }

            strength *= 1 + luck / 24.0;

            result += strength;
        }

        if (commander != null && troopsExist)
        {
            result += commander.GetMagicStrategicValue(result);
        }

        return result;
    }

    /// <summary>Whether a slot holds the monster, <c>Troops::HasMonster()</c> (<c>army.cpp:402</c>).</summary>
    /// <param name="monster">Monster to look for.</param>
    /// <returns><see langword="true"/> when a valid slot has it.</returns>
    public bool HasMonster(MonsterType monster)
    {
        foreach (Troop troop in _slots)
        {
            if (troop.IsValid && troop.IsMonster(monster))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the monster could join, <c>Troops::CanJoinTroop()</c> (<c>army.cpp:431</c>): either a slot
    /// already holds it (and would grow) or there is an empty slot.
    /// </summary>
    /// <param name="monster">Monster to test.</param>
    /// <returns><see langword="true"/> when <see cref="JoinTroop(MonsterType, uint, bool)"/> can succeed.</returns>
    public bool CanJoinTroop(MonsterType monster)
    {
        foreach (Troop troop in _slots)
        {
            if (troop.IsMonster(monster))
            {
                return true;
            }
        }

        return GetFirstEmptySlot() >= 0;
    }

    /// <summary>
    /// Adds creatures to the army, <c>Troops::JoinTroop( const Monster &amp;, uint32_t, bool )</c>
    /// (<c>army.cpp:437</c>).
    /// </summary>
    /// <param name="monster">Monster to add.</param>
    /// <param name="count">How many to add; zero is refused.</param>
    /// <param name="emptySlotFirst">
    /// Which preference wins when both an empty slot and a stack of the same monster exist. <c>false</c>
    /// (the value <c>Army::Reset()</c> passes) merges with the existing stack.
    /// </param>
    /// <returns><see langword="false"/> when the army is full and holds no such monster.</returns>
    public bool JoinTroop(MonsterType monster, uint count, bool emptySlotFirst = true)
    {
        if (!MonsterStatistics.IsValid(monster) || count == 0)
        {
            return false;
        }

        int sameSlot = -1;
        int emptySlot = -1;

        for (int i = 0; i < _slots.Length; ++i)
        {
            if (emptySlot < 0 && _slots[i].IsEmpty)
            {
                emptySlot = i;
            }

            if (sameSlot < 0 && _slots[i].IsValid && _slots[i].IsMonster(monster))
            {
                sameSlot = i;
            }
        }

        int slot = emptySlotFirst ? (emptySlot >= 0 ? emptySlot : sameSlot) : (sameSlot >= 0 ? sameSlot : emptySlot);

        if (slot < 0)
        {
            return false;
        }

        if (_slots[slot].IsValid)
        {
            _slots[slot].Count += count;
        }
        else
        {
            _slots[slot].Set(monster, count);
        }

        return true;
    }

    /// <summary>
    /// Adds a whole stack, <c>Troops::JoinTroop( const Troop &amp; )</c> (<c>army.cpp:478</c>). Unlike the
    /// other overload this one prefers an existing stack of the same monster.
    /// </summary>
    /// <param name="troop">The stack to add.</param>
    /// <returns><see langword="false"/> when the stack is invalid or the army is full.</returns>
    public bool JoinTroop(Troop troop)
    {
        ArgumentNullException.ThrowIfNull(troop);

        return troop.IsValid && JoinTroop(troop.Monster, troop.Count, false);
    }

    /// <summary>
    /// Empties every slot, <c>Troops::Clean()</c> (<c>army.cpp:523</c>).
    /// </summary>
    public void Clean()
    {
        foreach (Troop troop in _slots)
        {
            troop.Reset();
        }
    }

    /// <summary>
    /// Rebuilds the army from nothing, <c>Army::Reset( bool )</c> (<c>army.cpp:1335</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hero created without <paramref name="defaultArmy"/> starts with one creature of the first dwelling
    /// of his race; with it he gets a first-dwelling stack of a random size and, nine times out of ten, a
    /// second-dwelling stack as well.
    /// </para>
    /// <para>
    /// The reference draws those sizes from the global generator; the port takes an optional
    /// <paramref name="generator"/> so that map loading and tests can be reproducible, and falls back to
    /// <see cref="Rand"/> when it is absent.
    /// </para>
    /// <para>
    /// The reference returns early when the commander has no race, and that case is preserved: an army with
    /// <see cref="MapRace.None"/> is cleaned and left empty.
    /// </para>
    /// </remarks>
    /// <param name="defaultArmy">Whether to build the full starting army.</param>
    /// <param name="generator">Generator for the stack sizes; the thread generator when absent.</param>
    public void Reset(bool defaultArmy = false, Pcg32? generator = null)
    {
        Clean();

        if (Race == MapRace.None)
        {
            return;
        }

        if (!defaultArmy)
        {
            JoinTroop(DwellingMonsters.FromDwelling(Race, BuildingType.DwellingMonster1), 1, false);

            return;
        }

        JoinMonsters(DwellingMonsters.FromDwelling(Race, BuildingType.DwellingMonster1));

        if (GetRandom(1, 10) == 1)
        {
            return;
        }

        JoinMonsters(DwellingMonsters.FromDwelling(Race, BuildingType.DwellingMonster2));

        void JoinMonsters(MonsterType monster)
        {
            (uint min, uint max) = GetNumberOfMonstersInStartingArmy(monster);

            JoinTroop(monster, GetRandom(min, max), false);
        }

        uint GetRandom(uint min, uint max) =>
            generator is null ? Rand.Get(min, max) : Rand.GetWithGen(min, max, generator);
    }

    /// <summary>
    /// The size range of a starting stack, <c>getNumberOfMonstersInStartingArmy()</c>
    /// (<c>army.cpp:124</c>).
    /// </summary>
    /// <remarks>
    /// Only the first two levels can start an army, and the reference asserts that. The port keeps the
    /// <c>(0, 0)</c> fallback of the reference instead of throwing: a caller that asks about a dragon gets no
    /// creatures, and <see cref="Reset"/> is the only caller.
    /// </remarks>
    /// <param name="monster">Monster of the stack.</param>
    /// <returns>Inclusive lower and upper bounds of the starting count.</returns>
    public static (uint Min, uint Max) GetNumberOfMonstersInStartingArmy(MonsterType monster)
    {
        switch (MonsterStatistics.GetGeneralStats(monster).Level)
        {
            case 1:
                return monster switch
                {
                    MonsterType.Peasant => (30, 50),
                    MonsterType.Goblin => (15, 25),
                    MonsterType.Sprite => (10, 20),
                    _ => (6, 10),
                };

            case 2:
                return monster switch
                {
                    MonsterType.Archer or MonsterType.Orc => (3, 5),
                    _ => (2, 4),
                };

            default:
                return (0, 0);
        }
    }

    /// <inheritdoc />
    public IEnumerator<Troop> GetEnumerator() => ((IEnumerable<Troop>)_slots).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The slot that wins by the given comparison, <c>Troops::getBestMatchToCondition()</c>
    /// (<c>army.cpp:571</c>): the first valid slot is the baseline and is replaced only when a later slot
    /// satisfies the condition against it.
    /// </summary>
    /// <param name="better">Decides whether a slot beats the current best.</param>
    /// <returns>The winning slot, or <see langword="null"/> when nothing is valid.</returns>
    private Troop? GetBestMatch(Func<Troop, Troop, bool> better)
    {
        Troop? best = GetFirstValidTroop();

        if (best is null)
        {
            return null;
        }

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid && better(troop, best))
            {
                best = troop;
            }
        }

        return best;
    }

    /// <summary>The first empty slot, or <c>-1</c>.</summary>
    /// <returns>The index.</returns>
    private int GetFirstEmptySlot()
    {
        for (int i = 0; i < _slots.Length; ++i)
        {
            if (_slots[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }
}
