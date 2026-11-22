using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// Reading and writing a daily event, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>EventDate</c>
/// (<c>world.cpp:1671-1690</c>).
/// </summary>
/// <remarks>
/// <para>
/// The order is the resources, the "applies to AI players as well" flag, the first day, the repeat period, the player
/// colours, the message and the title. The colours are a single byte — <c>PlayerColorsSet</c> is the underlying type of
/// <c>PlayerColor</c>, which is <c>uint8_t</c> (<c>color.h:47</c>) — but before 1.1.09 they were written as an
/// <c>int32_t</c>, so a reader of an older file has to swallow four bytes and keep the low one.
/// </para>
/// <para>
/// The reader cannot reuse the map-file constructor: a save carries no "structure size" byte, the strings are
/// length-prefixed and the colours are not spread over the six bytes of the MP2 block.
/// </para>
/// </remarks>
public sealed partial class EventDate
{
    /// <summary>
    /// Writes the event, <c>operator&lt;&lt;</c> (<c>world.cpp:1671</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Resources.WriteTo(stream);
        stream.Write(IsApplicableForAIPlayers);
        stream.Write(FirstOccurrenceDay);
        stream.Write(RepeatPeriodInDays);
        stream.Write((byte)Colors);
        stream.Write(Message);
        stream.Write(Title);
    }

    /// <summary>
    /// Reads an event, <c>operator&gt;&gt;</c> (<c>world.cpp:1676</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <returns>The event; a stream that failed on the way leaves the defaults and its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static EventDate ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Funds resources = Funds.ReadFrom(stream);
        bool appliesToAi = stream.ReadBool();
        uint firstDay = stream.ReadUInt32();
        uint repeatPeriod = stream.ReadUInt32();
        PlayerColor colors = version < SaveFileFormat.Release1_1_09
            ? (PlayerColor)stream.ReadInt32()
            : (PlayerColor)stream.ReadUInt8();

        return new EventDate
        {
            Resources = resources,
            IsApplicableForAIPlayers = appliesToAi,
            FirstOccurrenceDay = firstDay,
            RepeatPeriodInDays = repeatPeriod,
            Colors = colors,
            Message = stream.ReadString(),
            Title = stream.ReadString(),
        };
    }
}
