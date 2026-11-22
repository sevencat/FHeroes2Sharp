using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing a sign or a bottle, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>MapSign</c>
/// (<c>maps_objects.cpp:398-417</c>).
/// </summary>
/// <remarks>
/// <para>
/// The position and the UID, then the message. The message is a <see cref="LocalizedString"/>, so from 1.1.07 on it is
/// a string followed by a "has a language" flag and, when the flag is set, one byte of language
/// (<c>game_string.cpp:25-33</c>).
/// </para>
/// <para>
/// Before 1.1.07 only the text was written, so a reader of an older save takes the string and leaves the language
/// unset — which means "already translated", the same value the port gives a message read from an MP2 block
/// (<c>maps_objects.cpp:407-414</c>).
/// </para>
/// </remarks>
public sealed partial class MapSign
{
    /// <summary>
    /// Writes the sign, <c>operator&lt;&lt;</c> (<c>maps_objects.cpp:398</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn the tile index into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        WritePosition(stream, geometry);
        Message.Write(stream);
    }

    /// <summary>
    /// Reads a sign, <c>operator&gt;&gt;</c> (<c>maps_objects.cpp:403</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn the point into a tile index.</param>
    /// <returns>The sign; a stream that failed on the way leaves the defaults and its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal static MapSign ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        (int tileIndex, uint uid) = ReadPosition(stream, version, geometry);

        LocalizedString message = version < SaveFileFormat.Release1_1_07
            ? new LocalizedString(stream.ReadString())
            : LocalizedString.Read(stream);

        MapSign sign = new()
        {
            Message = message,
        };

        sign.PlaceOn(tileIndex, uid);

        return sign;
    }
}
