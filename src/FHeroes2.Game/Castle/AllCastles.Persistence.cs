using FHeroes2.Core.Io;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Castle;

/// <summary>
/// Reading and writing every town, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>AllCastles</c>
/// (<c>castle.cpp:2462-2500</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>uint32_t</c> count and that many whole towns. The reader starts from an empty table — the reference clears its
/// vector — so a town that the file does not mention is gone, which is what makes loading a save replace the map's own
/// towns rather than add to them.
/// </para>
/// <para>
/// Each town is read into a freshly constructed one: the position is the first field of the file and the port's town
/// cannot be moved after construction, so <see cref="Castle"/> gains a town that knows no place and the reader fills it
/// in. <see cref="Castle.Version"/> keeps the default, because the reference's towns take the map version from the
/// settings when they are loaded, not from the file.
/// </para>
/// <para>
/// A town that registers on a tile another town already holds would corrupt the footprint lookup, so the stream fails
/// rather than overwriting — a save file cannot contain that, and neither can a map.
/// </para>
/// </remarks>
public sealed partial class AllCastles
{
    /// <summary>Writes every town, <c>castle.cpp:2462</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn a position into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_castles.Count);

        foreach (Castle castle in _castles)
        {
            castle.WriteTo(stream, geometry);
        }
    }

    /// <summary>
    /// Empties the table, footprints included, <c>castles.clear()</c> in the reference's reader
    /// (<c>castle.cpp:2480</c>).
    /// </summary>
    /// <remarks>
    /// Both halves have to go: the list alone would leave the footprint lookup pointing at towns that no longer exist,
    /// and the next map that places a town on one of those tiles would find it taken.
    /// </remarks>
    internal void Clear()
    {
        _castles.Clear();
        _castleTiles.Clear();
    }

    /// <summary>Reads every town, <c>castle.cpp:2478</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint count = stream.ReadUInt32();

        if (count > (uint)MaximumAllowed)
        {
            // The reference resizes its vector; the port cannot hold more towns than the map can (castle.cpp:2481).
            stream.SetFail();
            return;
        }

        Clear();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            var castle = new Castle();

            castle.ReadFrom(stream, version, geometry);

            if (!stream.Fail)
            {
                Add(castle);
            }
        }
    }
}
