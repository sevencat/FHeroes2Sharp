using System.Diagnostics;

using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The fourteen artifact slots of a hero, <c>BagArtifacts</c> (<c>artifact.h:298-348</c>,
/// <c>artifact.cpp:517-1438</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>std::vector&lt;ArtifactType&gt;</c> and constructs itself with fourteen
/// <c>UNKNOWN</c> entries; there is no per-artifact equipment slot, so any artifact fits any hole. The port
/// keeps the flat array for the same reason: the original game's weapon/shield/helm positions were already
/// gone before this code was written, and re-introducing them would change which artifacts a hero can carry.
/// </para>
/// <para>
/// The <c>description</c> overloads of the getters — which build "Ultimate Sword +12" strings — and
/// <c>String()</c> are not ported: the port does not render text yet. Everything else of the reference's bag is
/// here, including the pricing and set assembly the AI uses, and the exchange that a hero meet runs is next door in
/// <see cref="ArtifactExchange"/>.
/// </para>
/// </remarks>
public sealed class ArtifactBag
{
    /// <summary>Number of slots, <c>BagArtifacts::maxCapacity</c> (<c>artifact.h:300</c>).</summary>
    public const int MaxCapacity = 14;

    private readonly ArtifactType[] _slots = new ArtifactType[MaxCapacity];

    /// <summary>An empty bag, <c>BagArtifacts::BagArtifacts()</c> (<c>artifact.cpp:517</c>).</summary>
    public ArtifactBag()
    {
        // The array is already filled with ArtifactType.Unknown, which is zero.
    }

    /// <summary>The fourteen slots, in order; empty slots are <see cref="ArtifactType.Unknown"/>.</summary>
    public IReadOnlyList<ArtifactType> Slots => _slots;

    /// <summary>Whether every slot is taken, <c>BagArtifacts::isFull()</c> (<c>artifact.cpp:869</c>).</summary>
    public bool IsFull => Array.IndexOf(_slots, ArtifactType.Unknown) < 0;

    /// <summary>
    /// How many artifacts the bag holds, <c>CountArtifacts()</c> (<c>artifact.cpp:874</c>).
    /// </summary>
    /// <remarks>
    /// Counts valid artifacts, so a bag with two holes reports twelve rather than fourteen. The reference
    /// subtracts the magic book when it needs "transferable artifacts" (<c>heroes.cpp:891</c>), which is a
    /// question for the caller, not for the bag.
    /// </remarks>
    /// <returns>The number of non-empty slots.</returns>
    public int CountArtifacts()
    {
        int count = 0;

        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTypes.IsValid(artifact))
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>
    /// How many copies of the artifact are in the bag, <c>Count( art )</c> (<c>artifact.cpp:1061</c>).
    /// </summary>
    /// <param name="artifact">The artifact to count.</param>
    /// <returns>The number of slots holding it.</returns>
    public int Count(ArtifactType artifact)
    {
        int count = 0;

        foreach (ArtifactType slot in _slots)
        {
            if (slot == artifact)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>
    /// Whether the artifact is in the bag, <c>isPresentArtifact()</c> (<c>artifact.cpp:534</c>).
    /// </summary>
    /// <param name="artifact">The artifact to look for.</param>
    /// <returns>Whether a slot holds it.</returns>
    public bool IsPresent(ArtifactType artifact) => Array.IndexOf(_slots, artifact) >= 0;

    /// <summary>
    /// Puts an artifact into the first empty slot, <c>PushArtifact()</c> (<c>artifact.cpp:825-857</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The magic book is the one artifact with a place of its own: it goes into the first slot and pushes
    /// everything else one to the right, because the reference's UI draws the book at the top left. A second
    /// copy is refused rather than stored — the reference asserts a hero never has two, and the bag's screen
    /// would have no way to show them.
    /// </para>
    /// <para>
    /// Everything else goes into the first <see cref="ArtifactType.Unknown"/> slot, which is also the only
    /// "can it fit" rule the reference has.
    /// </para>
    /// </remarks>
    /// <param name="artifact">The artifact to add.</param>
    /// <returns>Whether it fitted.</returns>
    public bool Push(ArtifactType artifact)
    {
        if (!ArtifactTypes.IsValid(artifact))
        {
            Debug.Assert(false, "Pushing an id that is not an artifact.");
            return false;
        }

        if (artifact == ArtifactType.MagicBook && IsPresent(artifact))
        {
            return false;
        }

        int empty = Array.IndexOf(_slots, ArtifactType.Unknown);

        if (empty < 0)
        {
            return false;
        }

        if (artifact != ArtifactType.MagicBook)
        {
            _slots[empty] = artifact;
            return true;
        }

        // std::move_backward( begin(), firstEmpty, std::next( firstEmpty ) ): shift [0, empty) one to the right.
        Array.Copy(_slots, 0, _slots, 1, empty);
        _slots[0] = artifact;
        return true;
    }

    /// <summary>
    /// Takes the first copy of the artifact out of the bag, <c>RemoveArtifact()</c> (<c>artifact.cpp:859-867</c>).
    /// </summary>
    /// <remarks>
    /// Only the first copy is removed and the slot is emptied in place instead of shifting the others, so the
    /// remaining artifacts keep their positions — that is what the reference does, and the UI depends on it.
    /// </remarks>
    /// <param name="artifact">The artifact to remove.</param>
    public void Remove(ArtifactType artifact)
    {
        int index = Array.IndexOf(_slots, artifact);

        if (index >= 0)
        {
            _slots[index] = ArtifactType.Unknown;
        }
    }

    /// <summary>
    /// Whether an artifact with the spell is in the bag, <c>ContainSpell()</c>
    /// (<c>artifact.cpp:521-532</c>).
    /// </summary>
    /// <remarks>
    /// The reference asks every artifact for <c>getSpellId()</c>, which is the <c>ADD_SPELL</c> value for a
    /// named artifact and the <c>ext</c> field for a scroll. The port has no <c>ext</c> yet, so a blank scroll
    /// answers <see cref="SpellType.None"/> and never matches; a hero carrying the Battle Garb still reports
    /// its Town Portal.
    /// </remarks>
    /// <param name="spell">The spell to look for.</param>
    /// <returns>Whether some artifact grants it.</returns>
    public bool ContainSpell(SpellType spell)
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTable.Get(artifact).TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus row)
                && row.Spell == spell)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The sum of one bonus over the bag, <c>getTotalArtifactEffectValue( bonus )</c>
    /// (<c>artifact.cpp:563-597</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two ways to add: cumulative bonuses (the four primary skills and the seven incomes) count every copy,
    /// everything else counts one artifact id once no matter how many copies are in the bag. The reference
    /// uses a <c>std::set&lt;int&gt;</c> of ids for the second case, which is the same as ignoring the later
    /// copies.
    /// </para>
    /// <para>
    /// The reference asserts that the type is neither multiplied nor unique, because adding a percentage or a
    /// flag makes no sense. The port asserts too; in a release build the two wrong piles simply add up.
    /// </para>
    /// </remarks>
    /// <param name="bonus">The bonus to add up.</param>
    /// <returns>The total, zero when no artifact grants it.</returns>
    public int GetTotalArtifactEffectValue(ArtifactBonusType bonus)
    {
        Debug.Assert(!ArtifactBonuses.IsMultiplied(bonus) && !ArtifactBonuses.IsUnique(bonus));

        if (ArtifactBonuses.IsCumulative(bonus))
        {
            int total = 0;

            foreach (ArtifactType artifact in _slots)
            {
                if (ArtifactTable.Get(artifact).TryGetBonus(bonus, out ArtifactBonus row))
                {
                    total += row.Value;
                }
            }

            return total;
        }
        else
        {
            int total = 0;
            var usedIds = new HashSet<int>();

            foreach (ArtifactType artifact in _slots)
            {
                if (!usedIds.Add((int)artifact))
                {
                    continue;
                }

                if (ArtifactTable.Get(artifact).TryGetBonus(bonus, out ArtifactBonus row))
                {
                    total += row.Value;
                }
            }

            return total;
        }
    }

    /// <summary>
    /// Whether any artifact in the bag carries a curse, <c>isArtifactCursePresent()</c>
    /// (<c>artifact.cpp:551-564</c>).
    /// </summary>
    /// <remarks>
    /// The question is asked where the curse's value is irrelevant and only its presence matters — the army's morale
    /// wants to know whether the "Arm of the Martyr" is worn, not by how much. Unlike the sum below, this one is valid
    /// for every curse type, so it is also the way to ask about the multiplied and unique ones.
    /// </remarks>
    /// <param name="curse">The curse to look for.</param>
    /// <returns>Whether at least one artifact has it.</returns>
    public bool IsArtifactCursePresent(ArtifactCurseType curse)
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTable.Get(artifact).TryGetCurse(curse, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The sum of one curse over the bag, <c>getTotalArtifactEffectValue( curse )</c>
    /// (<c>artifact.cpp:653-687</c>).
    /// </summary>
    /// <remarks>
    /// The curse twin of the bonus overload, same two piles: <see cref="ArtifactCurseType.SpellPowerSkill"/>
    /// and <see cref="ArtifactCurseType.GoldPenalty"/> count every copy, the rest count once.
    /// </remarks>
    /// <param name="curse">The curse to add up.</param>
    /// <returns>The total, zero when no artifact has it.</returns>
    public int GetTotalArtifactEffectValue(ArtifactCurseType curse)
    {
        Debug.Assert(!ArtifactBonuses.IsCurseMultiplied(curse) && !ArtifactBonuses.IsCurseUnique(curse));

        if (ArtifactBonuses.IsCurseCumulative(curse))
        {
            int total = 0;

            foreach (ArtifactType artifact in _slots)
            {
                if (ArtifactTable.Get(artifact).TryGetCurse(curse, out ArtifactCurse row))
                {
                    total += row.Value;
                }
            }

            return total;
        }
        else
        {
            int total = 0;
            var usedIds = new HashSet<int>();

            foreach (ArtifactType artifact in _slots)
            {
                if (!usedIds.Add((int)artifact))
                {
                    continue;
                }

                if (ArtifactTable.Get(artifact).TryGetCurse(curse, out ArtifactCurse row))
                {
                    total += row.Value;
                }
            }

            return total;
        }
    }

    /// <summary>
    /// The values of one multiplied bonus over the bag, <c>getTotalArtifactMultipliedPercent( bonus )</c>
    /// (<c>artifact.cpp:743-769</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The multiplied bonuses do not add up — two "+50% to cold damage" artifacts do not make "+100%" in the
    /// reference, each one is applied to the spell on its own — so this returns the list of percentages rather than
    /// a sum, and the caller multiplies spell by spell. Duplicate ids are counted once, the same way the effect
    /// getters above count them.
    /// </para>
    /// <para>
    /// Asking for a bonus type that is not multiplied is a programming error: the reference asserts and answers an
    /// empty list. The port answers the empty list without the assert, so a test can pin the contract instead of the
    /// assert.
    /// </para>
    /// </remarks>
    /// <param name="bonus">The multiplied bonus to collect.</param>
    /// <returns>The percentages, in slot order, one per distinct artifact id.</returns>
    public IReadOnlyList<int> GetTotalMultipliedPercent(ArtifactBonusType bonus)
    {
        if (!ArtifactBonuses.IsMultiplied(bonus))
        {
            return [];
        }

        var values = new List<int>(MaxCapacity);
        var usedIds = new HashSet<int>();

        foreach (ArtifactType artifact in _slots)
        {
            if (!usedIds.Add((int)artifact))
            {
                continue;
            }

            if (ArtifactTable.Get(artifact).TryGetBonus(bonus, out ArtifactBonus row))
            {
                values.Add(row.Value);
            }
        }

        return values;
    }

    /// <summary>
    /// The values of one multiplied curse over the bag, <c>getTotalArtifactMultipliedPercent( curse )</c>
    /// (<c>artifact.cpp:771-797</c>).
    /// </summary>
    /// <remarks>
    /// The curse twin of the bonus overload: a list of percentages, one per distinct id, empty when the type is not
    /// multiplied. The reference asserts on the wrong type; the port only answers the empty list.
    /// </remarks>
    /// <param name="curse">The multiplied curse to collect.</param>
    /// <returns>The percentages, in slot order, one per distinct artifact id.</returns>
    public IReadOnlyList<int> GetTotalMultipliedPercent(ArtifactCurseType curse)
    {
        if (!ArtifactBonuses.IsCurseMultiplied(curse))
        {
            return [];
        }

        var values = new List<int>(MaxCapacity);
        var usedIds = new HashSet<int>();

        foreach (ArtifactType artifact in _slots)
        {
            if (!usedIds.Add((int)artifact))
            {
                continue;
            }

            if (ArtifactTable.Get(artifact).TryGetCurse(curse, out ArtifactCurse row))
            {
                values.Add(row.Value);
            }
        }

        return values;
    }

    /// <summary>
    /// Whether any artifact in the bag has the bonus, <c>isArtifactBonusPresent()</c>
    /// (<c>artifact.cpp:539-549</c>).
    /// </summary>
    /// <param name="bonus">The bonus to look for.</param>
    /// <returns>Whether some artifact grants it.</returns>
    public bool IsBonusPresent(ArtifactBonusType bonus)
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTable.Get(artifact).TryGetBonus(bonus, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether any artifact in the bag has the curse, <c>isArtifactCursePresent()</c>
    /// (<c>artifact.cpp:551-561</c>).
    /// </summary>
    /// <param name="curse">The curse to look for.</param>
    /// <returns>Whether some artifact has it.</returns>
    public bool IsCursePresent(ArtifactCurseType curse)
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTable.Get(artifact).TryGetCurse(curse, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The first artifact with the bonus, <c>getFirstArtifactWithBonus()</c> (<c>artifact.cpp:799-810</c>).
    /// </summary>
    /// <remarks>
    /// "First" is slot order, which is why the bag is a vector in the reference: the caller shows the name of the
    /// artifact it got and slot order is what the player sees.
    /// </remarks>
    /// <param name="bonus">The bonus to look for.</param>
    /// <returns>
    /// The artifact, or <see cref="ArtifactType.Unknown"/> when no artifact grants the bonus.
    /// </returns>
    public ArtifactType GetFirstArtifactWithBonus(ArtifactBonusType bonus)
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTable.Get(artifact).TryGetBonus(bonus, out _))
            {
                return artifact;
            }
        }

        return ArtifactType.Unknown;
    }

    /// <summary>
    /// The first artifact with the curse, <c>getFirstArtifactWithCurse()</c> (<c>artifact.cpp:812-823</c>).
    /// </summary>
    /// <param name="curse">The curse to look for.</param>
    /// <returns>
    /// The artifact, or <see cref="ArtifactType.Unknown"/> when no artifact has the curse.
    /// </returns>
    public ArtifactType GetFirstArtifactWithCurse(ArtifactCurseType curse)
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTable.Get(artifact).TryGetCurse(curse, out _))
            {
                return artifact;
            }
        }

        return ArtifactType.Unknown;
    }

    /// <summary>
    /// Whether the bag holds an ultimate artifact, <c>ContainUltimateArtifact()</c>
    /// (<c>artifact.cpp:1040-1043</c>).
    /// </summary>
    /// <remarks>
    /// The map's puzzle hides an ultimate, and the AI uses this to decide whether digging is still worth anything.
    /// </remarks>
    /// <returns>Whether some slot holds one of the eight ultimates.</returns>
    public bool ContainsUltimateArtifact()
    {
        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactLevels.IsUltimate(artifact))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The price of the bag, <c>getArtifactValue()</c> (<c>artifact.cpp:880-889</c>).
    /// </summary>
    /// <remarks>
    /// The sum of <see cref="ArtifactValues.Value(ArtifactType)"/> over the artifacts, which is the number the AI
    /// compares two heroes (or a hero and an offer) by. Empty slots add nothing.
    /// </remarks>
    /// <returns>The sum, zero for an empty bag.</returns>
    public double GetArtifactValue()
    {
        double result = 0;

        foreach (ArtifactType artifact in _slots)
        {
            if (ArtifactTypes.IsValid(artifact))
            {
                result += ArtifactValues.Value(artifact);
            }
        }

        return result;
    }

    /// <summary>
    /// Turns complete sets into their assembled artifact, <c>assembleArtifactSetIfPossible()</c>
    /// (<c>artifact.cpp:1383-1412</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bag is checked again after every assembly, because a set's parts can include another set: the reference's
    /// <c>while ( foundAllArtifacts )</c> is what lets three copies of the Anduran pieces assemble twice.
    /// </para>
    /// <para>
    /// The parts are removed before the assembled artifact is pushed, so a bag that was full has room for it. If the
    /// push still fails the parts are gone — the reference ignores the return value of
    /// <c>PushArtifact()</c> and so does the port; nothing in the game can reach that state, since the parts always
    /// free three slots for one.
    /// </para>
    /// </remarks>
    /// <returns>The sets that were assembled, in table order; empty when nothing matched.</returns>
    public IReadOnlyList<ArtifactSet> AssembleArtifactSets()
    {
        var assembled = new List<ArtifactSet>();

        foreach (ArtifactSet set in ArtifactSets.All)
        {
            while (IsSetPresent(set))
            {
                foreach (ArtifactType part in set.Parts)
                {
                    Remove(part);
                }

                assembled.Add(set);

                if (!Push(set.Assembled))
                {
                    Debug.Assert(false, "The assembled artifact did not fit into the bag it came from.");
                }
            }
        }

        return assembled;
    }

    /// <summary>
    /// Sorts the artifacts from the least valuable to the most, <c>sortFromWorstToBest()</c>
    /// (<c>artifact.cpp:1414-1429</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference sorts its whole vector with a comparator that answers "not less" whenever either side is the
    /// magic book. That comparator is not a strict weak ordering — it says the book is equivalent to everything, and
    /// empty slots sort to the front by value — so <c>std::sort</c> has an undefined result. The port defines it:
    /// the magic book keeps the first slot it was pushed into, the other artifacts are ordered by
    /// <see cref="ArtifactValues.Value(ArtifactType)"/>, and the empty slots stay at the end.
    /// </para>
    /// <para>
    /// That is the observable intent of the reference — the book at the top left, the rest from worst to best, holes
    /// after the artifacts — and it keeps <see cref="Push"/> filling from the first hole.
    /// </para>
    /// </remarks>
    public void SortFromWorstToBest()
    {
        var artifacts = new List<ArtifactType>(MaxCapacity - 1);
        bool hasBook = false;

        foreach (ArtifactType artifact in _slots)
        {
            if (!ArtifactTypes.IsValid(artifact))
            {
                continue;
            }

            if (artifact == ArtifactType.MagicBook)
            {
                hasBook = true;
            }
            else
            {
                artifacts.Add(artifact);
            }
        }

        artifacts.Sort(static (left, right) =>
            ArtifactValues.Value(left).CompareTo(ArtifactValues.Value(right)));

        int index = 0;

        if (hasBook)
        {
            _slots[index++] = ArtifactType.MagicBook;
        }

        foreach (ArtifactType artifact in artifacts)
        {
            _slots[index++] = artifact;
        }

        while (index < MaxCapacity)
        {
            _slots[index++] = ArtifactType.Unknown;
        }
    }

    /// <summary>Whether every part of the set is in the bag.</summary>
    /// <param name="set">The set to test.</param>
    /// <returns>Whether all of its parts are present.</returns>
    private bool IsSetPresent(ArtifactSet set)
    {
        foreach (ArtifactType part in set.Parts)
        {
            if (!IsPresent(part))
            {
                return false;
            }
        }

        return true;
    }
}
