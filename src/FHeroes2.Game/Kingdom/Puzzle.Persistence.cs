using System.Text;

using FHeroes2.Core.Io;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Reading and writing the jigsaw, <c>operator&lt;&lt;( OStreamBase &amp;, const Puzzle &amp; )</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Puzzle &amp; )</c> (<c>puzzle.cpp:365-426</c>).
/// </summary>
/// <remarks>
/// <para>
/// The uncovered tiles go out as the <c>std::bitset&lt;48&gt;::to_string()</c> of the reference — <b>fourty-eight
/// characters of <c>'0'</c> and <c>'1'</c>, most significant tile first</b> — inside the usual length prefixed
/// string. Then the four zone orders, each as one byte of length and that many bytes of tile indices.
/// </para>
/// <para>
/// The character order is the part that is easy to get wrong: tile 47 comes first and tile 0 last. The reference
/// reads it back by shifting left and setting bit 0 (<c>puzzle.cpp:311-321</c>), which is the same thing.
/// </para>
/// <para>
/// The zone orders are saved because they are shuffled, and a save that came back with a fresh shuffle would reveal
/// a different order of tiles for the same obelisks. A length that disagrees with the picture is not an error in the
/// reference (it just resizes the list); the port keeps whatever the file holds, but refuses a length larger than the
/// picture so that a corrupt count cannot grow an unbounded list.
/// </para>
/// </remarks>
public sealed partial class Puzzle
{
    /// <summary>
    /// Writes the jigsaw, <c>operator&lt;&lt;</c> (<c>puzzle.cpp:365</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        StringBuilder bits = new(NumOfPuzzleTiles);

        // std::bitset<>::to_string() prints the highest bit first, so tile 47 leads and tile 0 closes.
        for (int tile = NumOfPuzzleTiles - 1; tile >= 0; --tile)
        {
            bits.Append(_tiles[tile] ? '1' : '0');
        }

        stream.Write(bits.ToString());

        WriteZone(stream, _zone1Order);
        WriteZone(stream, _zone2Order);
        WriteZone(stream, _zone3Order);
        WriteZone(stream, _zone4Order);
    }

    /// <summary>
    /// Reads the jigsaw into this object, <c>operator&gt;&gt;</c> (<c>puzzle.cpp:392</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns><see langword="true"/> when the puzzle was read whole.</returns>
    public bool ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Fail)
        {
            return false;
        }

        string bits = stream.ReadString();

        // puzzle.cpp:313 — the reference walks the string and shifts; an empty string leaves every bit alone.
        if (bits.Length > 0)
        {
            Array.Clear(_tiles);

            int bit = NumOfPuzzleTiles - 1;

            foreach (char value in bits)
            {
                if (bit < 0)
                {
                    break;
                }

                _tiles[bit] = value == '1';
                --bit;
            }
        }

        bool ok = ReadZone(stream, _zone1Order) &&
                  ReadZone(stream, _zone2Order) &&
                  ReadZone(stream, _zone3Order) &&
                  ReadZone(stream, _zone4Order);

        return ok && !stream.Fail;
    }

    /// <summary>Writes one zone order, <c>operator&lt;&lt;</c> inside <c>puzzle.cpp:369-387</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="zone">The zone, in its shuffled order.</param>
    private static void WriteZone(StreamBase stream, List<byte> zone)
    {
        stream.Write((byte)zone.Count);

        foreach (byte tile in zone)
        {
            stream.Write(tile);
        }
    }

    /// <summary>Reads one zone order, <c>operator&gt;&gt;</c> inside <c>puzzle.cpp:399-423</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="zone">The zone to fill.</param>
    /// <returns><see langword="false"/> when the stored length cannot be a zone of this picture.</returns>
    private static bool ReadZone(StreamBase stream, List<byte> zone)
    {
        byte size = stream.ReadUInt8();

        if (size > NumOfPuzzleTiles)
        {
            stream.SetFail();
            zone.Clear();

            return false;
        }

        zone.Clear();

        for (int index = 0; index < size; ++index)
        {
            zone.Add(stream.ReadUInt8());
        }

        return true;
    }
}
