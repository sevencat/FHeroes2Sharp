using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Reading and writing the seven kingdoms, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Kingdoms</c>
/// (<c>kingdom.cpp:948-956</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both operators are one line in the reference: the whole thing is the <c>std::vector&lt;Kingdom&gt;</c> member, so the
/// file holds a <c>uint32_t</c> count and that many kingdoms.
/// </para>
/// <para>
/// <b>The port's table is a fixed array of seven</b> — six colours plus the bin for the unknown ones (ADR-111). A count
/// that is not seven therefore cannot be laid out, and the reference would silently grow its vector past the slots the
/// game reads; the port fails the stream instead of half-filling the table.
/// </para>
/// </remarks>
public sealed partial class Kingdoms
{
    /// <summary>
    /// Writes every kingdom (<c>kingdom.cpp:948</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_kingdoms.Length);

        foreach (Kingdom kingdom in _kingdoms)
        {
            kingdom.WriteTo(stream);
        }
    }

    /// <summary>
    /// Reads every kingdom (<c>kingdom.cpp:953</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="world">The world the castles and the heroes are looked up in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="world"/> is
    /// <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, World world)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(world);

        uint count = stream.ReadUInt32();

        if (count != _kingdoms.Length)
        {
            stream.SetFail();
            return;
        }

        for (int i = 0; i < _kingdoms.Length && !stream.Fail; ++i)
        {
            _kingdoms[i] = Kingdom.ReadFrom(stream, version, world);
        }
    }
}
