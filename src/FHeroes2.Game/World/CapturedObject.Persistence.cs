using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;

namespace FHeroes2.Game;

/// <summary>
/// Reading and writing one captured object, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of
/// <c>CapturedObject</c> (<c>world.cpp:1327-1335</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>objCol</c> is a <c>std::pair&lt;MP2::MapObjectType, PlayerColor&gt;</c>: the pair operator writes its two members in
/// declaration order (<c>serialize.cpp</c>), so the object type comes as a <c>uint16_t</c> and the owner as a single
/// byte. The guardians follow as a normal <c>Troop</c>.
/// </para>
/// <para>
/// The object type is stored at its full width here, unlike in the tile, because this block never had a narrower form:
/// the pair operator has always written the enum at its declared width.
/// </para>
/// </remarks>
public sealed partial class CapturedObject
{
    /// <summary>
    /// Writes the entry, <c>operator&lt;&lt;</c> (<c>world.cpp:1327</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((ushort)ObjectType);
        stream.Write((byte)Color);
        Guardians.WriteTo(stream);
    }

    /// <summary>
    /// Reads an entry, <c>operator&gt;&gt;</c> (<c>world.cpp:1332</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The entry; a stream that ran out leaves the defaults and its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static CapturedObject ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        CapturedObject capturedObject = new();

        capturedObject.ObjectType = (Mp2ObjectType)stream.ReadUInt16();
        capturedObject.Color = (PlayerColor)stream.ReadUInt8();
        capturedObject.Guardians.ReadFrom(stream);

        return capturedObject;
    }
}

/// <summary>
/// Reading and writing the whole table, the <c>std::map</c> operator applied to <c>CapturedObjects</c>
/// (<c>world.cpp:1443-1444</c>).
/// </summary>
/// <remarks>
/// The reference derives from <c>std::map&lt;int32_t, CapturedObject&gt;</c>, so the generic map operator is what writes
/// it: a <c>uint32_t</c> count, then a key and a value per entry, in tile index order. The port keeps a
/// <see cref="SortedDictionary{TKey,TValue}"/> for the same reason, so the byte order is the same without sorting here.
/// </remarks>
public sealed partial class CapturedObjects
{
    /// <summary>
    /// Writes the table (<c>world.cpp:1443</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_objects.Count);

        foreach (KeyValuePair<int, CapturedObject> entry in _objects)
        {
            stream.Write(entry.Key);
            entry.Value.WriteTo(stream);
        }
    }

    /// <summary>
    /// Reads the table (<c>world.cpp:1444</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The table; a stream that ran out stops the loop and leaves its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static CapturedObjects ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        CapturedObjects objects = new();
        uint count = stream.ReadUInt32();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            int index = stream.ReadInt32();
            CapturedObject capturedObject = CapturedObject.ReadFrom(stream);

            if (!stream.Fail)
            {
                objects._objects[index] = capturedObject;
            }
        }

        return objects;
    }

    /// <summary>
    /// Replaces the table with the one of the file, the <c>std::map</c> semantics of
    /// <c>stream &gt;&gt; w.map_captureobj</c>.
    /// </summary>
    /// <remarks>
    /// The reference reads into the map the map loader filled, clearing it first, so the mines a save does not mention
    /// do not survive the load.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void LoadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        CapturedObjects objects = ReadFrom(stream);

        _objects.Clear();

        foreach (KeyValuePair<int, CapturedObject> entry in objects._objects)
        {
            _objects[entry.Key] = entry.Value;
        }
    }
}
