using System.Collections;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Spells;

/// <summary>
/// A set of spells, the <c>SpellStorage</c> of <c>spell_storage.h:33-43</c> and
/// <c>spell_storage.cpp:30-108</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>std::vector&lt;Spell&gt;</c> and adds five methods; the port wraps a
/// <see cref="List{T}"/> for the same reason — the order is the order the spells were learned, which is what
/// the spell book screen draws. Appending a spell that is already there does nothing, so the container is a
/// set with a history rather than a multiset.
/// </para>
/// <para>
/// <b>Spell scrolls carry no spell in the port</b>: <c>Artifact::getSpellId()</c>
/// (<c>artifact.cpp:417-430</c>) reads the <c>ADD_SPELL</c> bonus and falls back to the artifact's own
/// <c>ext</c> byte, the per-instance payload of a scroll. <see cref="ArtifactBag"/> stores artifact ids
/// without a payload, so a scroll in the bag adds <see cref="SpellType.None"/> and nothing is appended. The
/// two fixed spells of the table — <c>BATTLE_GARB</c>'s Town Portal and the blank
/// <c>SPELL_SCROLL</c> — do come through.
/// </para>
/// </remarks>
public sealed class SpellStorage : IEnumerable<Spell>
{
    private readonly List<Spell> _spells = [];

    /// <summary>An empty storage.</summary>
    public SpellStorage()
    {
    }

    /// <summary>Number of spells in the storage.</summary>
    public int Count => _spells.Count;

    /// <summary>The spell at the given position, in the order it was added.</summary>
    /// <param name="index">Position, from zero.</param>
    public Spell this[int index] => _spells[index];

    /// <summary>Empties the storage, <c>SpellStorage::clear()</c> (<c>spell_storage.h:30</c>).</summary>
    public void Clear() => _spells.Clear();

    /// <summary>
    /// Adds a spell, <c>SpellStorage::Append( const Spell &amp; )</c> (<c>spell_storage.cpp:47-58</c>).
    /// </summary>
    /// <param name="spell">The spell to add; nothing happens for <see cref="SpellType.None"/> or a duplicate.</param>
    public void Append(Spell spell)
    {
        if (spell.Type == SpellType.None || IsPresent(spell))
        {
            return;
        }

        _spells.Add(spell);
    }

    /// <summary>
    /// Adds every spell of another storage, <c>SpellStorage::Append( const SpellStorage &amp; )</c>
    /// (<c>spell_storage.cpp:60-67</c>).
    /// </summary>
    /// <param name="storage">The storage to copy from.</param>
    public void Append(SpellStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        foreach (Spell spell in storage._spells)
        {
            Append(spell);
        }
    }

    /// <summary>
    /// Adds the spell of every spell scroll the hero carries, <c>SpellStorage::Append( const BagArtifacts &amp; )</c>
    /// (<c>spell_storage.cpp:81-86</c>).
    /// </summary>
    /// <param name="bag">The artifacts of the hero.</param>
    public void Append(ArtifactBag bag)
    {
        ArgumentNullException.ThrowIfNull(bag);

        foreach (ArtifactType artifact in bag.Slots)
        {
            Append(new Spell(GetSpellId(artifact)));
        }
    }

    /// <summary>
    /// Removes a spell, <c>SpellStorage::removeSpell()</c> (<c>spell_storage.cpp:88-103</c>).
    /// </summary>
    /// <param name="spell">The spell to remove.</param>
    /// <returns>Whether the spell was there.</returns>
    public bool Remove(Spell spell) => spell.Type != SpellType.None && _spells.Remove(spell);

    /// <summary>
    /// Whether the storage holds the spell, <c>SpellStorage::isPresentSpell()</c>
    /// (<c>spell_storage.cpp:105-108</c>).
    /// </summary>
    /// <param name="spell">The spell to look for.</param>
    /// <returns>Whether the spell is there.</returns>
    public bool IsPresent(Spell spell) => _spells.Contains(spell);

    /// <summary>
    /// The spells of one level, <c>SpellStorage::GetSpells()</c> (<c>spell_storage.cpp:30-45</c>).
    /// </summary>
    /// <param name="level">Level to keep, or a negative number for all of them.</param>
    /// <returns>A new storage with the spells of that level.</returns>
    public SpellStorage GetSpells(int level)
    {
        if (level < 0)
        {
            SpellStorage all = new();
            all.Append(this);
            return all;
        }

        SpellStorage result = new();

        foreach (Spell spell in _spells)
        {
            if (spell.IsLevel(level))
            {
                result.Append(spell);
            }
        }

        return result;
    }

    /// <summary>Iterates the spells in the order they were added.</summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<Spell> GetEnumerator() => _spells.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The spell an artifact carries, <c>Artifact::getSpellId()</c> (<c>artifact.cpp:417-430</c>).
    /// </summary>
    /// <remarks>
    /// The reference falls back to the artifact's <c>ext</c> byte when the <c>ADD_SPELL</c> value is
    /// <c>NONE</c>, which is where a spell scroll keeps the spell it was bought with. The port has no payload
    /// per artifact, so the fallback has nothing to read and a scroll reads as no spell.
    /// </remarks>
    /// <param name="artifact">The artifact to ask.</param>
    /// <returns>The spell id, or zero.</returns>
    private static int GetSpellId(ArtifactType artifact)
        => ArtifactTable.Get(artifact).TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus bonus)
            ? bonus.Value
            : (int)SpellType.None;
}
