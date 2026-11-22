using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Reading and writing the two tavern slots, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Recruit</c>
/// (<c>heroes_recruits.cpp:106-121</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each slot is a hero id (<c>int32_t</c>) and a surrender day (<c>uint32_t</c>); both slots are written back to back
/// because <c>Recruits</c> derives from <c>std::pair&lt;Recruit, Recruit&gt;</c>.
/// </para>
/// <para>
/// Before 1.0.10 the file holds the id <b>minus one</b>: <c>UNKNOWN</c> was 72 back then and became 0, so every id
/// shifted down by one and the reader adds it back (<c>heroes_recruits.cpp:115-118</c>). That gate is on reading only —
/// the reference always writes the id it has.
/// </para>
/// </remarks>
public sealed partial class Recruits
{
    /// <summary>
    /// Writes both slots (<c>heroes_recruits.cpp:106</c> for one of them).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        WriteRecruit(stream, First);
        WriteRecruit(stream, Second);
    }

    /// <summary>
    /// Reads both slots (<c>heroes_recruits.cpp:111</c> for one of them).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        First = ReadRecruit(stream, version);
        Second = ReadRecruit(stream, version);
    }

    /// <summary>Writes one slot: id then surrender day.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="recruit">The slot.</param>
    private static void WriteRecruit(StreamBase stream, Recruit recruit)
    {
        stream.Write(recruit.Id);
        stream.Write(recruit.SurrenderDay);
    }

    /// <summary>Reads one slot, with the 1.0.10 id shift.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <returns>The slot.</returns>
    private static Recruit ReadRecruit(StreamBase stream, SaveFileFormat version)
    {
        int id = stream.ReadInt32();
        uint surrenderDay = stream.ReadUInt32();

        if (version < SaveFileFormat.Release1_0_10)
        {
            // UNKNOWN was 72 before 1.0.10 and became 0, so the whole id space moved down by one.
            ++id;
        }

        return new Recruit(id, surrenderDay);
    }
}
