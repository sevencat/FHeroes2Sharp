using FHeroes2.Core.Geometry;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing where an object stands, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of
/// <c>MapBaseObject</c> and <c>MapPosition</c> (<c>maps_objects.cpp:324-342</c>, <c>position.cpp:39-58</c>).
/// </summary>
/// <remarks>
/// <para>
/// The position is two <c>int16_t</c> — <b>not</b> the two <c>int32_t</c> of a <c>fheroes2::Point</c>, which is what
/// <c>position.cpp:41</c> notes for formats before 0.9.4 — followed by the UID as a <c>uint32_t</c>. The port keeps a
/// tile index instead of a point (ADR-068), so both directions go through <see cref="MapGeometry"/>: the map is needed
/// to turn one into the other, and the caller has it.
/// </para>
/// <para>
/// Before 1.1.03 the two were written in the other order and an <c>int32_t</c> came first
/// (<c>maps_objects.cpp:332-336</c>), so a reader of an older save has to swallow four bytes and read the UID before the
/// position.
/// </para>
/// </remarks>
public abstract partial class MapObject
{
    /// <summary>
    /// Writes the position of the object, <c>operator&lt;&lt;( MapBaseObject &amp; )</c> (<c>maps_objects.cpp:324</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn the tile index back into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WritePosition(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        WritePosition(stream, TileIndex, Uid, geometry);
    }

    /// <summary>
    /// Writes a position that is not an object's own — a hero's or a town's, which are <c>MapPosition</c> sub-objects in
    /// the reference rather than map objects (<c>heroes_base.cpp:628</c>, <c>castle.cpp:2420</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="tileIndex">The tile index, or a negative value for an object that is not on the map.</param>
    /// <param name="uid">The UID.</param>
    /// <param name="geometry">The map, to turn the tile index back into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal static void WritePosition(StreamBase stream, int tileIndex, uint uid, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (tileIndex < 0)
        {
            // A hero that is not on the map has no centre and is written as (-1, -1) (heroes.cpp:311-318).
            stream.Write((short)-1);
            stream.Write((short)-1);
            stream.Write(uid);
            return;
        }

        Point center = geometry.GetPoint(tileIndex);

        stream.Write((short)center.X);
        stream.Write((short)center.Y);
        stream.Write(uid);
    }

    /// <summary>
    /// Reads where the object stands, <c>operator&gt;&gt;( MapBaseObject &amp; )</c> (<c>maps_objects.cpp:329</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn the point into a tile index.</param>
    /// <returns>The tile index the object stands on and its UID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal static (int TileIndex, uint Uid) ReadPosition(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint uid;
        short x;
        short y;

        if (version < SaveFileFormat.PreRelease2_1_1_03)
        {
            // A word of nothing, then the UID, then the point (maps_objects.cpp:333-336).
            stream.ReadInt32();
            uid = stream.ReadUInt32();
            x = stream.ReadInt16();
            y = stream.ReadInt16();
        }
        else
        {
            x = stream.ReadInt16();
            y = stream.ReadInt16();
            uid = stream.ReadUInt32();
        }

        return (geometry.GetIndexFromAbsPoint(x, y), uid);
    }
}
