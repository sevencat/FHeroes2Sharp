using System.Buffers.Binary;

namespace FHeroes2.Assets.Icn;

/// <summary>
/// The 13 byte header of a single sprite inside an ICN file, identical to
/// <c>fheroes2::ICNHeader</c> (see <c>src/engine/agg_file.h</c>).
/// </summary>
/// <param name="OffsetX">Horizontal anchor offset, signed because sprites may be placed left of their origin.</param>
/// <param name="OffsetY">Vertical anchor offset.</param>
/// <param name="Width">Width of the decoded sprite.</param>
/// <param name="Height">Height of the decoded sprite.</param>
/// <param name="AnimationFrames">
/// Frame count of the animation, stored modulo 32. Bit <c>0x20</c> marks a monochromatic sprite.
/// </param>
/// <param name="OffsetData">Offset of the compressed sprite payload, relative to the end of the 6 byte file header.</param>
public readonly record struct IcnHeader(
    short OffsetX,
    short OffsetY,
    ushort Width,
    ushort Height,
    byte AnimationFrames,
    uint OffsetData)
{
    /// <summary>Size of an ICN header in bytes.</summary>
    public const int Size = 13;

    /// <summary>Mask of <see cref="AnimationFrames"/> that marks a monochromatic sprite.</summary>
    public const byte MonochromaticFlag = 0x20;

    /// <summary>
    /// A monochromatic sprite only carries the transform layer, the image layer stays black.
    /// Such sprites are used for shadows and silhouettes.
    /// </summary>
    public bool IsMonochromatic => (AnimationFrames & MonochromaticFlag) != 0;

    /// <summary>Reads a header from the first <see cref="Size"/> bytes of <paramref name="data"/>.</summary>
    public static IcnHeader Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new ArgumentException($"An ICN header needs {Size} bytes but only {data.Length} were provided.", nameof(data));
        }

        return new IcnHeader(
            BinaryPrimitives.ReadInt16LittleEndian(data),
            BinaryPrimitives.ReadInt16LittleEndian(data[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(data[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(data[6..]),
            data[8],
            BinaryPrimitives.ReadUInt32LittleEndian(data[9..]));
    }
}
