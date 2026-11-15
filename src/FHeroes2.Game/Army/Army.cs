using System.Collections;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Monsters;

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
/// Not ported, in one list, because they are all blocked on the same missing tables:
/// <c>GetStrength()</c> (needs <c>Troop::GetStrength()</c>, i.e. the monster abilities),
/// <c>GetMorale()</c>/<c>GetLuck()</c> (artifacts and secondary skills of the commander), the battle
/// helpers (<c>GetCurrentUnit</c>, <c>GetSlowestTroop</c> is here but <c>GetWeakestTroop</c> is strength
/// based), <c>UpgradeTroops</c> (castle dwellings), the join-by-condition helpers used by wandering
/// monsters (<c>GetMonsterJoinCondition</c>), <c>TroopSizeString</c> and <c>String</c> (translated text).
/// </para>
/// </remarks>
public sealed class Army : IEnumerable<Troop>
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
            JoinTroop(DwellingMonsters.FromDwelling(Race, Dwelling.Monster1), 1, false);

            return;
        }

        JoinMonsters(DwellingMonsters.FromDwelling(Race, Dwelling.Monster1));

        if (GetRandom(1, 10) == 1)
        {
            return;
        }

        JoinMonsters(DwellingMonsters.FromDwelling(Race, Dwelling.Monster2));

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
