using FHeroes2.Core.Io;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Castle;

/// <summary>
/// Reading and writing a mage guild, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>MageGuild</c>
/// (<c>mageguild.cpp:485-500</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference holds two <c>SpellStorage</c> vectors — the ordinary spells and the library extras — and writes them one
/// after the other: a <c>uint32_t</c> count and that many spell ids each.
/// </para>
/// <para>
/// <b>The port keeps them in fixed tables instead</b> (eleven ordinary slots plus five library slots, laid out by spell
/// level: <see cref="GuildMaxSpellsCount"/> and the offsets derived from it). Writing therefore skips the empty slots and
/// the count says how many are left; reading puts every spell back into the first free slot of its own level. The two
/// layouts hold the same spells, in the same order within a level, so the round trip is exact and a reference file lands
/// where the guild's own rules would have put it.
/// </para>
/// </remarks>
public sealed partial class MageGuild
{
    /// <summary>Writes the spells of the guild, <c>mageguild.cpp:485</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        WriteSpells(stream, _general);
        WriteSpells(stream, _library);
    }

    /// <summary>Reads the spells of the guild, <c>mageguild.cpp:492</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Array.Fill(_general, SpellType.None);
        Array.Fill(_library, SpellType.None);

        ReadSpells(stream, _general, _library);
        ReadLibrarySpells(stream, _library);
    }

    /// <summary>Writes the spells of one table, skipping the empty slots.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="spells">The table.</param>
    private static void WriteSpells(StreamBase stream, SpellType[] spells)
    {
        uint count = 0;

        foreach (SpellType spell in spells)
        {
            if (spell != SpellType.None)
            {
                ++count;
            }
        }

        stream.Write(count);

        foreach (SpellType spell in spells)
        {
            if (spell != SpellType.None)
            {
                stream.Write((int)spell);
            }
        }
    }

    /// <summary>
    /// Reads one list of ordinary spells into the first free slot of each spell's level.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="general">The ordinary table.</param>
    /// <param name="library">The library table, which takes the spell of a level whose slots are all full.</param>
    private static void ReadSpells(StreamBase stream, SpellType[] general, SpellType[] library)
    {
        uint count = stream.ReadUInt32();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            var spell = new Spell(stream.ReadInt32());
            int levelIndex = spell.Level() - 1;

            if (levelIndex is < 0 or > 4)
            {
                continue;
            }

            int offset = LevelOffsets[levelIndex];
            bool placed = false;

            for (int slot = 0; slot < GuildMaxSpellsCount[levelIndex] && !placed; ++slot)
            {
                if (general[offset + slot] == SpellType.None)
                {
                    general[offset + slot] = spell.Type;
                    placed = true;
                }
            }

            if (!placed && library[levelIndex] == SpellType.None)
            {
                library[levelIndex] = spell.Type;
            }
        }
    }

    /// <summary>
    /// Reads the library list, which has one slot per level rather than the level's full row of slots.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="library">The library table.</param>
    private static void ReadLibrarySpells(StreamBase stream, SpellType[] library)
    {
        uint count = stream.ReadUInt32();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            var spell = new Spell(stream.ReadInt32());
            int levelIndex = spell.Level() - 1;

            if (levelIndex is < 0 or > 4)
            {
                continue;
            }

            if (library[levelIndex] == SpellType.None)
            {
                library[levelIndex] = spell.Type;
            }
        }
    }
}
