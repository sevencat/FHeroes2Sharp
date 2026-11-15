using System.Diagnostics;

using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The fourteen artifact slots of a hero, <c>BagArtifacts</c> (<c>artifact.h:298-348</c>,
/// <c>artifact.cpp:517-889</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>std::vector&lt;ArtifactType&gt;</c> and constructs itself with fourteen
/// <c>UNKNOWN</c> entries; there is no per-artifact equipment slot, so any artifact fits any hole. The port
/// keeps the flat array for the same reason: the original game's weapon/shield/helm positions were already
/// gone before this code was written, and re-introducing them would change which artifacts a hero can carry.
/// </para>
/// <para>
/// The <c>description</c> overloads of the getters (which build "Ultimate Sword +12" strings) are not ported:
/// the port does not render text yet. Neither is <c>getArtifactValue()</c>, the double an AI uses to price a
/// bag, nor <c>getTotalArtifactMultipliedPercent()</c>, which needs the percentage resolution the battle
/// engine will bring.
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
}
