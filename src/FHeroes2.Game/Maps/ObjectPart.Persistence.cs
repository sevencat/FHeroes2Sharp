using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing one object part, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of
/// <c>Maps::ObjectPart</c> (<c>maps_tiles.cpp:1885-1908</c>).
/// </summary>
/// <remarks>
/// <para>
/// Seven bytes: the layer as <c>uint8_t</c>, the uid as <c>uint32_t</c>, the ICN type as <c>uint8_t</c> and the image
/// index as <c>uint8_t</c>. The field widths are the reference's <c>enum ... : uint8_t</c> declarations, not the port's
/// — <see cref="ObjectLayerType"/> and <see cref="Mp2ObjectIcnType"/> happen to be bytes as well, but the cast is kept
/// explicit so that widening one of them later cannot silently change the format.
/// </para>
/// <para>
/// Two things happened to this block in 1.0.09: the layer type was packed with two bits that no longer exist, and two
/// <c>bool</c> members between the ICN type and the index were dropped. Both are read gates, so a save written before
/// that release still lines up.
/// </para>
/// </remarks>
public partial struct ObjectPart
{
    /// <summary>How many bytes a part takes in a save file of the current format.</summary>
    public const int SerializedSize = sizeof(byte) + sizeof(uint) + sizeof(byte) + sizeof(byte);

    /// <summary>
    /// The bits the layer type was packed into before 1.0.09, <c>layerType &amp; 0x03</c>
    /// (<c>maps_tiles.cpp:1896</c>).
    /// </summary>
    private const byte LegacyLayerMask = 0x03;

    /// <summary>
    /// Writes the part, <c>operator&lt;&lt;</c> (<c>maps_tiles.cpp:1885</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <remarks>
    /// The reference writes <c>layerType</c> first and <c>_uid</c> second, which is not the order the members are
    /// declared in — the format is what matters, so the order is copied as it is written, not as it reads best.
    /// </remarks>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((byte)LayerType);
        stream.Write(Uid);
        stream.Write((byte)IcnType);
        stream.Write(IcnIndex);
    }

    /// <summary>
    /// Reads a part, <c>operator&gt;&gt;</c> (<c>maps_tiles.cpp:1890</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <returns>
    /// The part; a stream that failed on the way leaves the result at the empty defaults and its <c>Fail</c> flag set,
    /// so the caller decides whether that is fatal.
    /// </returns>
    public static ObjectPart ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte layerType = stream.ReadUInt8();

        if (version < SaveFileFormat.PreRelease2_1_0_09)
        {
            layerType &= LegacyLayerMask;
        }

        uint uid = stream.ReadUInt32();
        byte icnType = stream.ReadUInt8();

        if (version < SaveFileFormat.PreRelease2_1_0_09)
        {
            // Two bools that were part of the object animation state; nothing reads them any more.
            stream.ReadBool();
            stream.ReadBool();
        }

        byte icnIndex = stream.ReadUInt8();

        if (stream.Fail)
        {
            return new ObjectPart();
        }

        return new ObjectPart((ObjectLayerType)layerType, uid, (Mp2ObjectIcnType)icnType, icnIndex);
    }
}
