using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// One tile of an MP2 map file, the port of <c>MP2::MP2TileInfo</c> (<c>mp2.h</c> plus
/// <c>mp2_helper.cpp</c>). The structure is exactly <see cref="StructureSize"/> bytes and is stored
/// row by row, <c>index = x + y * width</c>.
///
/// <para>
/// Every tile holds at most one ground object, one top object and a chain of add-ons whose pieces
/// belong to the same object. The chain starts at <see cref="NextAddonIndex"/>; index 0 means "no
/// more add-ons", which is why add-on 0 is never used.
/// </para>
/// </summary>
public readonly record struct Mp2TileInfo
{
    /// <summary>Size of one tile structure in the map file.</summary>
    public const int StructureSize = 20;

    /// <summary>Image index that marks a missing ground or top object sprite.</summary>
    public const byte EmptyImageIndex = 255;

    /// <summary>Terrain image index inside the terrain specific TIL file.</summary>
    public required ushort TerrainImageIndex { get; init; }

    /// <summary>
    /// Ground object: the top six bits are an <see cref="Mp2ObjectIcnType"/> (read with
    /// <c>&gt;&gt; 2</c>), the bottom two are unused. The layer lives in the low bits of
    /// <see cref="Quantity1"/>.
    /// </summary>
    public required byte ObjectName1 { get; init; }

    /// <summary>Sprite index of the ground object inside its ICN; 255 means "nothing".</summary>
    public required byte BottomIcnImageIndex { get; init; }

    /// <summary>
    /// The top two bits hold the ground object layer (0…3), the bottom five are the low bits of
    /// <see cref="Metadata"/>.
    /// </summary>
    public required byte Quantity1 { get; init; }

    /// <summary>The high bits of <see cref="Metadata"/>.</summary>
    public required byte Quantity2 { get; init; }

    /// <summary>
    /// Top object: the top six bits are an <see cref="Mp2ObjectIcnType"/> (read with
    /// <c>&gt;&gt; 2</c>), the bottom two are unused.
    /// </summary>
    public required byte ObjectName2 { get; init; }

    /// <summary>Sprite index of the top object inside its ICN; 255 means "nothing".</summary>
    public required byte TopIcnImageIndex { get; init; }

    /// <summary>
    /// The lowest two bits are the terrain shape (0…3); the third to fifth bits describe how water
    /// touches land (beach), a value only the original editor cares about.
    /// </summary>
    public required byte TerrainFlags { get; init; }

    /// <summary>
    /// The object the game shows information about, <c>mapObjectType</c> of the file. Only the low
    /// byte is stored, so Resurrection types never appear here.
    /// </summary>
    public required Mp2ObjectType ObjectType { get; init; }

    /// <summary>First add-on of this tile, 0 when there is none.</summary>
    public required ushort NextAddonIndex { get; init; }

    /// <summary>Object identifier of the ground object; shared by all pieces of a multi tile object.</summary>
    public required uint Level1ObjectUid { get; init; }

    /// <summary>Object identifier of the top object.</summary>
    public required uint Level2ObjectUid { get; init; }

    /// <summary>The object type is <c>MP2::OBJ_NONE</c>.</summary>
    public bool IsEmpty => ObjectType == Mp2ObjectType.None;

    /// <summary>ICN tile set of the ground object; <see cref="Mp2ObjectIcnType.Unknown"/> when there is none.</summary>
    public Mp2ObjectIcnType GroundIcnType => (Mp2ObjectIcnType)(ObjectName1 >> 2);

    /// <summary>ICN tile set of the top object; <see cref="Mp2ObjectIcnType.Unknown"/> when there is none.</summary>
    public Mp2ObjectIcnType TopIcnType => (Mp2ObjectIcnType)(ObjectName2 >> 2);

    /// <summary>Whether a ground object is present (the reference tests the ICN type, not the sprite index).</summary>
    public bool HasGroundObject => Mp2ObjectIcnType.Unknown != GroundIcnType;

    /// <summary>Whether a top object is present.</summary>
    public bool HasTopObject => Mp2ObjectIcnType.Unknown != TopIcnType;

    /// <summary>Layer of the ground object, <c>quantity1 &amp; 0x03</c>.</summary>
    public int GroundLayer => Quantity1 & 0x03;

    /// <summary>
    /// The 13 bit value the game keeps per tile, <c>((quantity2 &lt;&lt; 8) + quantity1) &gt;&gt; 3</c>;
    /// it is the UID of the extended metadata block for objects that need one.
    /// </summary>
    public uint Metadata => (((uint)Quantity2 << 8) + Quantity1) >> 3;

    /// <summary>Terrain shape index, the lowest two bits of <see cref="TerrainFlags"/>.</summary>
    public int TerrainShape => TerrainFlags & 0x03;

    /// <summary>Reads one tile structure.</summary>
    /// <exception cref="BinaryFormatException">The stream ends in the middle of the structure.</exception>
    public static Mp2TileInfo Read(LittleEndianReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new Mp2TileInfo
        {
            TerrainImageIndex = reader.ReadUInt16(),
            ObjectName1 = reader.ReadByte(),
            BottomIcnImageIndex = reader.ReadByte(),
            Quantity1 = reader.ReadByte(),
            Quantity2 = reader.ReadByte(),
            ObjectName2 = reader.ReadByte(),
            TopIcnImageIndex = reader.ReadByte(),
            TerrainFlags = reader.ReadByte(),
            ObjectType = (Mp2ObjectType)reader.ReadByte(),
            NextAddonIndex = reader.ReadUInt16(),
            Level1ObjectUid = reader.ReadUInt32(),
            Level2ObjectUid = reader.ReadUInt32(),
        };
    }

    /// <summary>Writes the tile structure back, the exact inverse of <see cref="Read"/>.</summary>
    /// <param name="writer">Destination, positioned at the start of the structure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void Write(LittleEndianWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteUInt16(TerrainImageIndex);
        writer.WriteByte(ObjectName1);
        writer.WriteByte(BottomIcnImageIndex);
        writer.WriteByte(Quantity1);
        writer.WriteByte(Quantity2);
        writer.WriteByte(ObjectName2);
        writer.WriteByte(TopIcnImageIndex);
        writer.WriteByte(TerrainFlags);
        writer.WriteByte((byte)ObjectType);
        writer.WriteUInt16(NextAddonIndex);
        writer.WriteUInt32(Level1ObjectUid);
        writer.WriteUInt32(Level2ObjectUid);
    }

    /// <summary>Short diagnostic line, used by the tests and by <c>AssetProbe map</c>.</summary>
    public string GetSummary() =>
        $"tile {TerrainImageIndex} terrain/{TerrainFlags:X2} | {ObjectType}" +
        $" | ground {(HasGroundObject ? $"{GroundIcnType}:{BottomIcnImageIndex}" : "-")}" +
        $" | top {(HasTopObject ? $"{TopIcnType}:{TopIcnImageIndex}" : "-")}" +
        $" | addon {NextAddonIndex} | metadata {Metadata}";
}
