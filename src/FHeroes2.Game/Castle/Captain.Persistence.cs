using FHeroes2.Core.Io;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Castle;

/// <summary>
/// Reading and writing a captain, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>HeroBase</c>
/// (<c>heroes_base.cpp:618-680</c>).
/// </summary>
/// <remarks>
/// <para>
/// The captain has no operator of its own: it <b>is</b> a <c>HeroBase</c>, so a town writes it with the hero-base
/// operator, and the layout is what every hero and every captain share — primary skills, position, modes, spell points,
/// move points, the spell book and the artifact bag.
/// </para>
/// <para>
/// <b>The primary skills go attack, defence, knowledge, power</b> — knowledge before power, which is not the order of the
/// constructor or of the panel. Reading them in the obvious order gives every loaded hero a swapped knowledge and power,
/// and a round trip would not notice.
/// </para>
/// <para>
/// A captain has no place on the map and no movement points: the reference's <c>Captain</c> is constructed with the
/// centre <c>(-1, -1)</c>, and the port writes zeroes for modes and movement. They are read and thrown away, because the
/// port's captain does not keep them.
/// </para>
/// </remarks>
public sealed partial class Captain
{
    /// <summary>Writes the captain, <c>heroes_base.cpp:618</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, which a position is written relative to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(_attack);
        stream.Write(_defense);
        stream.Write(_knowledge);
        stream.Write(_power);

        // A captain is not on the map; the reference writes (-1, -1) and an all-zero UID.
        MapObject.WritePosition(stream, -1, 0, geometry);

        stream.Write(0u);
        stream.Write(_spellPoints);
        stream.Write(0u);
        _spellBook.WriteTo(stream);
        _bagArtifacts.WriteTo(stream);
    }

    /// <summary>Reads the captain, <c>heroes_base.cpp:630</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _attack = stream.ReadInt32();
        _defense = stream.ReadInt32();
        _knowledge = stream.ReadInt32();
        _power = stream.ReadInt32();

        _ = MapObject.ReadPosition(stream, version, geometry);

        // Modes and movement points: the port's captain has none.
        stream.ReadUInt32();
        _spellPoints = stream.ReadUInt32();
        stream.ReadUInt32();

        _spellBook.ReadFrom(stream);
        _bagArtifacts.ReadFrom(stream);
    }
}
