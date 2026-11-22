using FHeroes2.Core.Io;

namespace FHeroes2.Game.Spells;

/// <summary>
/// Reading and writing a spell storage, the <c>std::vector&lt;Spell&gt;</c> operators <c>SpellStorage</c> inherits
/// (<c>spell_storage.h:37-52</c>, <c>spell.cpp:812</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>SpellStorage</c> is a <c>std::vector&lt;Spell&gt;</c> and nothing else, so the file holds a <c>uint32_t</c> count and
/// that many spell ids as <c>int32_t</c> — the generic vector operator of <c>serialize.h</c> does the whole thing.
/// </para>
/// <para>
/// The ids go through <see cref="Spell"/>, whose constructor turns anything outside the table into
/// <see cref="SpellType.None"/>, exactly as <c>Spell( const int32_t )</c> does; a corrupt id therefore cannot index the
/// table of the reference either.
/// </para>
/// </remarks>
public sealed partial class SpellStorage
{
    /// <summary>Writes the spells, in book order.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_spells.Count);

        foreach (Spell spell in _spells)
        {
            stream.Write((int)spell.Type);
        }
    }

    /// <summary>
    /// Reads the spells, replacing what the storage held.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint count = stream.ReadUInt32();

        _spells.Clear();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            _spells.Add(new Spell(stream.ReadInt32()));
        }
    }
}
