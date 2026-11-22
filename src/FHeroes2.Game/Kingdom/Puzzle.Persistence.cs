using FHeroes2.Core.Io;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Reading and writing the puzzle map, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Puzzle</c>
/// (<c>puzzle.cpp:365-430</c>).
/// </summary>
/// <remarks>
/// <para>
/// A string of forty-eight <c>'0'</c>/<c>'1'</c> — what <c>std::bitset&lt;48&gt;::to_string()</c> gives — followed by the
/// four zone orders, each a <c>uint8_t</c> count and that many <c>uint8_t</c> tile numbers.
/// </para>
/// <para>
/// <b>The first character is tile 47, not tile 0</b>: <c>to_string()</c> prints the highest bit first, and the reader
/// (<c>puzzle.cpp:311-321</c>) shifts the set left on every character it takes, so the last character ends up in bit 0.
/// Getting this backwards reads every save with the picture upside down, and a round trip would not notice — hence the
/// hand-written bytes in the test.
/// </para>
/// <para>
/// The zone lists already hold a full deck at construction time; reading <b>replaces</b> them, which is what the
/// reference's <c>resize()</c> then per-element read amounts to.
/// </para>
/// </remarks>
public sealed partial class Puzzle
{
    /// <summary>
    /// Writes the puzzle map (<c>puzzle.cpp:365</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var bits = new char[NumOfPuzzleTiles];

        for (int i = 0; i < NumOfPuzzleTiles; ++i)
        {
            bits[i] = _tiles[NumOfPuzzleTiles - 1 - i] ? '1' : '0';
        }

        stream.Write(new string(bits));
        WriteZone(stream, _zone1Order);
        WriteZone(stream, _zone2Order);
        WriteZone(stream, _zone3Order);
        WriteZone(stream, _zone4Order);
    }

    /// <summary>
    /// Reads the puzzle map (<c>puzzle.cpp:392</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string bits = stream.ReadString();

        if (bits.Length != NumOfPuzzleTiles)
        {
            // A bitset of the wrong width cannot be trusted; the reference would silently leave the rest as it was.
            stream.SetFail();
            return;
        }

        for (int i = 0; i < NumOfPuzzleTiles; ++i)
        {
            _tiles[NumOfPuzzleTiles - 1 - i] = bits[i] == '1';
        }

        ReadZone(stream, _zone1Order);
        ReadZone(stream, _zone2Order);
        ReadZone(stream, _zone3Order);
        ReadZone(stream, _zone4Order);
    }

    /// <summary>Writes one zone order, <c>uint8_t</c> count then that many <c>uint8_t</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="zone">The zone.</param>
    private static void WriteZone(StreamBase stream, List<byte> zone)
    {
        stream.Write((byte)zone.Count);

        foreach (byte tile in zone)
        {
            stream.Write(tile);
        }
    }

    /// <summary>Reads one zone order into an existing list.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="zone">The zone.</param>
    private static void ReadZone(StreamBase stream, List<byte> zone)
    {
        byte size = stream.ReadUInt8();

        zone.Clear();

        for (int i = 0; i < size && !stream.Fail; ++i)
        {
            zone.Add(stream.ReadUInt8());
        }
    }
}
