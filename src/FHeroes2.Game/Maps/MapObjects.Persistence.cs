using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing the table of data objects, the <c>std::map&lt;uint32_t, std::unique_ptr&lt;MapBaseObject&gt;&gt;</c>
/// operator applied to <c>MapObjects</c> (<c>world.cpp:1383-1446</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>uint32_t</c> count, then per object the UID, the type and the body. The UID is written <b>twice</b>: once as
/// the key of the map and once inside the body, because the body starts with <c>MapBaseObject</c>, which carries it
/// (<c>world.cpp:1394</c>). The reader takes the second one as the truth and fails when the two disagree
/// (<c>world.cpp:1432-1437</c>).
/// </para>
/// <para>
/// The type is a <c>MP2::MapObjectType</c>, a <c>uint16_t</c> (<c>mp2.h:363</c>) — except before 1.1.03, where it was
/// written as an <c>int32_t</c> (<c>world.cpp:1421-1427</c>).
/// </para>
/// <para>
/// An unknown type or a UID that does not match is corruption in the reference too: it fails the stream and skips the
/// entry instead of guessing (<c>world.cpp:1425-1442</c>).
/// </para>
/// </remarks>
public sealed partial class MapObjects
{
    /// <summary>
    /// Writes the table (<c>world.cpp:1383</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn the tile indexes into points.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The table holds an object type that has no save form.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_objects.Count);

        // std::map 是按 UID 排的，端口按瓦片索引存；写的时候要按 UID 排回去才是同样的字节序。
        foreach (MapObject mapObject in _objects.Values.OrderBy(static value => value.Uid))
        {
            stream.Write(mapObject.Uid);
            stream.Write((ushort)GetSerializedType(mapObject));

            switch (mapObject)
            {
                case MapEvent mapEvent:
                    mapEvent.WriteTo(stream, geometry);
                    break;

                case MapSphinx sphinx:
                    sphinx.WriteTo(stream, geometry);
                    break;

                case MapSign sign:
                    sign.WriteTo(stream, geometry);
                    break;

                default:
                    throw new NotSupportedException(
                        $"A {mapObject.GetType().Name} has no save form; MapObjects only carries events, sphinxes and signs.");
            }
        }
    }

    /// <summary>
    /// Reads the table (<c>world.cpp:1383</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn the points into tile indexes.</param>
    /// <returns>The table; a stream that failed on the way stops the loop and keeps its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal static MapObjects ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        MapObjects objects = new();
        uint count = stream.ReadUInt32();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            uint uid = stream.ReadUInt32();

            Mp2ObjectType type = version < SaveFileFormat.PreRelease2_1_1_03
                ? (Mp2ObjectType)stream.ReadInt32()
                : (Mp2ObjectType)stream.ReadUInt16();

            MapObject? mapObject = type switch
            {
                Mp2ObjectType.Event => MapEvent.ReadFrom(stream, version, geometry),
                Mp2ObjectType.Sphinx => MapSphinx.ReadFrom(stream, version, geometry),
                Mp2ObjectType.Sign => MapSign.ReadFrom(stream, version, geometry),
                _ => null,
            };

            if (mapObject is null)
            {
                // Most likely the save file is corrupted (world.cpp:1426).
                stream.SetFail();
                continue;
            }

            if (mapObject.Uid != uid)
            {
                // Most likely the save file is corrupted (world.cpp:1432).
                stream.SetFail();
                continue;
            }

            if (objects._objects.ContainsKey(mapObject.TileIndex))
            {
                // Most likely the save file is corrupted (world.cpp:1439).
                stream.SetFail();
                continue;
            }

            objects._objects.Add(mapObject.TileIndex, mapObject);
        }

        return objects;
    }

    /// <summary>
    /// Replaces the table with the one of the file, the <c>std::map</c> semantics of
    /// <c>stream &gt;&gt; w.map_objects</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="ReadFrom(StreamBase, SaveFileFormat, MapGeometry)"/> builds a new table; the reference reads into the
    /// one the map loader filled, which clears it first. Loading a save into a world that already has a map therefore
    /// drops the signs of that map instead of keeping them next to the loaded ones.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn the points into tile indexes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void LoadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        MapObjects objects = ReadFrom(stream, version, geometry);

        _objects.Clear();

        foreach (MapObject mapObject in objects._objects.Values)
        {
            _objects[mapObject.TileIndex] = mapObject;
        }
    }

    /// <summary>
    /// The type written beside an object, the third column of the write loop
    /// (<c>world.cpp:1394-1416</c>).
    /// </summary>
    /// <param name="mapObject">The object to name.</param>
    /// <returns>The type.</returns>
    /// <exception cref="NotSupportedException">The object has no save form.</exception>
    private static Mp2ObjectType GetSerializedType(MapObject mapObject) => mapObject switch
    {
        MapEvent => Mp2ObjectType.Event,
        MapSphinx => Mp2ObjectType.Sphinx,
        MapSign => Mp2ObjectType.Sign,
        _ => throw new NotSupportedException(
            $"A {mapObject.GetType().Name} has no save form; MapObjects only carries events, sphinxes and signs."),
    };
}
