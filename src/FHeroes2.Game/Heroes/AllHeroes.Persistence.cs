using FHeroes2.Core.Io;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Reading and writing every hero, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>AllHeroes</c>
/// (<c>heroes.cpp:2648-2697</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>uint32_t</c> count and that many whole heroes, in slot order. Both sides have seventy-three slots and the count is
/// checked against them: a table of another width cannot be laid out, and the reference would grow its vector past the
/// slots the game indexes.
/// </para>
/// <para>
/// <b>1.0.10 moved <c>UNKNOWN</c> from the end of the list to the front</b>, so an older file stores the first hero last
/// (<c>heroes.cpp:2670-2686</c>). Reading an old file therefore takes the remaining heroes first and the last one into
/// the first slot.
/// </para>
/// <para>
/// The port's slots are <see langword="null"/> for the heroes a map never placed — including the unknown hero the
/// reference keeps in slot zero — while the file still writes a whole object for each of them. A null slot is therefore
/// written from an empty hero, and read into a scratch hero that is dropped unless the file has a real hero id there
/// (a table read without <see cref="Init"/>), in which case that hero is kept.
/// </para>
/// </remarks>
public sealed partial class AllHeroes
{
    /// <summary>Writes every slot, <c>heroes.cpp:2648</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn a position into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_slots.Length);

        foreach (Hero? hero in _slots)
        {
            (hero ?? new Hero()).WriteTo(stream, geometry);
        }
    }

    /// <summary>Reads every slot, <c>heroes.cpp:2661</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint count = stream.ReadUInt32();

        if (count != _slots.Length)
        {
            stream.SetFail();
            return;
        }

        if (version < SaveFileFormat.Release1_0_10)
        {
            // The unknown hero was the last object written before 1.0.10 (heroes.cpp:2670-2686).
            for (int i = 1; i < _slots.Length && !stream.Fail; ++i)
            {
                ReadInto(stream, version, geometry, i);
            }

            ReadInto(stream, version, geometry, 0);
            return;
        }

        for (int i = 0; i < _slots.Length && !stream.Fail; ++i)
        {
            ReadInto(stream, version, geometry, i);
        }
    }

    /// <summary>Reads one hero into a slot, or into a scratch hero when the slot holds nothing.</summary>
    /// <remarks>
    /// A slot the map never placed holds an <c>UNKNOWN</c> placeholder in the reference, and stays
    /// <see langword="null"/> in the port: the scratch hero is dropped. A file that has a real hero id in a slot the
    /// port has not filled (a container read without <see cref="Init"/>) is kept, so the table does not depend on
    /// having been initialized first. The other direction is the same rule: a hero the file says is not there is
    /// dropped, which is what makes loading a save into a loaded world replace its heroes instead of keeping them.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    /// <param name="slot">The slot.</param>
    private void ReadInto(StreamBase stream, SaveFileFormat version, MapGeometry geometry, int slot)
    {
        Hero? existing = _slots[slot];
        Hero target = existing ?? new Hero();

        target.ReadFrom(stream, version, geometry);

        bool placed = Hero.IsValidId(target.Id);

        if (placed && existing is null)
        {
            _slots[slot] = target;
            ++Count;
        }
        else if (!placed && existing is not null)
        {
            _slots[slot] = null;
            --Count;
        }
    }
}
