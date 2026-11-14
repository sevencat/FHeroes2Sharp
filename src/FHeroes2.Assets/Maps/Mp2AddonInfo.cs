using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// One add-on of an MP2 map file, the port of <c>MP2::MP2AddonInfo</c> (<c>mp2.h</c> plus
/// <c>mp2_helper.cpp</c>). Add-ons carry the extra pieces of multi tile objects: a tile points at its
/// first add-on and every add-on points at the next one, so the pieces of one object form a linked
/// list.
///
/// <para>
/// The structure is <see cref="StructureSize"/> bytes. <see cref="ObjectNameN1"/> is multiplied by
/// two while loading — the reference does the same and does not document why; the comment is kept
/// here so a future reader does not "fix" it.
/// </para>
/// </summary>
public readonly record struct Mp2AddonInfo
{
    /// <summary>Size of one add-on structure in the map file.</summary>
    public const int StructureSize = 15;

    /// <summary>Next add-on of the same object, 0 when this is the last piece.</summary>
    public required ushort NextAddonIndex { get; init; }

    /// <summary>Ground object: layer and <see cref="Mp2ObjectIcnType"/>, already multiplied by two like the reference.</summary>
    public required byte ObjectNameN1 { get; init; }

    /// <summary>Sprite index of the ground object inside its ICN.</summary>
    public required byte BottomIcnImageIndex { get; init; }

    /// <summary>Metadata of the piece; the lowest two bits are the layer of the ground object.</summary>
    public required byte QuantityN { get; init; }

    /// <summary>Top object: layer and <see cref="Mp2ObjectIcnType"/>.</summary>
    public required byte ObjectNameN2 { get; init; }

    /// <summary>Sprite index of the top object inside its ICN.</summary>
    public required byte TopIcnImageIndex { get; init; }

    /// <summary>Object identifier of the ground object.</summary>
    public required uint Level1ObjectUid { get; init; }

    /// <summary>Object identifier of the top object.</summary>
    public required uint Level2ObjectUid { get; init; }

    /// <summary>ICN tile set of the ground piece.</summary>
    public Mp2ObjectIcnType GroundIcnType => (Mp2ObjectIcnType)(ObjectNameN1 >> 2);

    /// <summary>ICN tile set of the top piece.</summary>
    public Mp2ObjectIcnType TopIcnType => (Mp2ObjectIcnType)(ObjectNameN2 >> 2);

    /// <summary>Layer of the ground piece, <c>quantityN &amp; 0x03</c>.</summary>
    public int GroundLayer => QuantityN & 0x03;

    /// <summary>Reads one add-on structure.</summary>
    /// <exception cref="BinaryFormatException">The stream ends in the middle of the structure.</exception>
    public static Mp2AddonInfo Read(LittleEndianReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new Mp2AddonInfo
        {
            NextAddonIndex = reader.ReadUInt16(),

            // The reference multiplies this byte by two; see the type summary.
            ObjectNameN1 = (byte)(reader.ReadByte() * 2),
            BottomIcnImageIndex = reader.ReadByte(),
            QuantityN = reader.ReadByte(),
            ObjectNameN2 = reader.ReadByte(),
            TopIcnImageIndex = reader.ReadByte(),
            Level1ObjectUid = reader.ReadUInt32(),
            Level2ObjectUid = reader.ReadUInt32(),
        };
    }

    /// <summary>Writes the add-on structure back, the exact inverse of <see cref="Read"/>.</summary>
    /// <param name="writer">Destination, positioned at the start of the structure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void Write(LittleEndianWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteUInt16(NextAddonIndex);

        // Read multiplies this byte by two, so writing it back has to undo that; the value is always
        // even because it came from a byte, so the division cannot lose a bit.
        writer.WriteByte((byte)(ObjectNameN1 / 2));

        writer.WriteByte(BottomIcnImageIndex);
        writer.WriteByte(QuantityN);
        writer.WriteByte(ObjectNameN2);
        writer.WriteByte(TopIcnImageIndex);
        writer.WriteUInt32(Level1ObjectUid);
        writer.WriteUInt32(Level2ObjectUid);
    }

    /// <summary>Short diagnostic line, used by the tests and by <c>AssetProbe map</c>.</summary>
    public string GetSummary() =>
        $"ground {GroundIcnType}:{BottomIcnImageIndex} layer {GroundLayer}" +
        $" | top {TopIcnType}:{TopIcnImageIndex}" +
        $" | next {NextAddonIndex} | uid {Level1ObjectUid}/{Level2ObjectUid}";
}
