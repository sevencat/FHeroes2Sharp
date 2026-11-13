using System.Buffers.Binary;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for the ICN container: the 6 byte file header, the header table and the payload slicing.
/// All inputs are synthetic so that malformed entries can be tested explicitely.
/// </summary>
public sealed class IcnSpriteFileTests
{
    [Fact]
    public void Parse_DecodesEverySpriteInFileOrder()
    {
        byte[] data = BuildContainer(
            (new byte[] { 0x02, 10, 11, 0x80 }, 2, 1, -1, 2, 0),
            (new byte[] { 0x01, 20, 0x80 }, 1, 1, 3, -4, 0));

        IcnSpriteFile icn = IcnSpriteFile.Parse(data);

        Assert.Equal(2, icn.Count);
        Assert.Equal(data.Length - IcnSpriteFile.HeaderSize, icn.DataBlockSize);
        Assert.Equal(-1, icn.Headers[0].OffsetX);
        Assert.Equal(2, icn.Headers[0].OffsetY);
        Assert.Equal(20, icn.Sprites[1].Image[0, 0]);
        Assert.Equal(3, icn.Sprites[1].OffsetX);
        Assert.Equal(-4, icn.Sprites[1].OffsetY);
    }

    [Fact]
    public void Parse_KeepsTheMonochromeFlag()
    {
        // One black pixel, two transparent ones, one more black pixel.
        byte[] data = BuildContainer((new byte[] { 0x01, 0x82, 0x01, 0x80 }, 4, 1, 0, 0, IcnHeader.MonochromaticFlag));

        IcnSpriteFile icn = IcnSpriteFile.Parse(data);

        Assert.True(icn.Headers[0].IsMonochromatic);
        Assert.True(icn.Sprites[0].HasTransformLayer);
        Assert.Equal(new byte[] { 0, 1, 1, 0 }, icn.Sprites[0].Transform);
        Assert.All(icn.Sprites[0].Image.Pixels, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Parse_RejectsAnEntryThatIsTooShort()
    {
        Assert.Throws<BinaryFormatException>(() => IcnSpriteFile.Parse(new byte[5]));
    }

    [Fact]
    public void Parse_RejectsAnEmptyFile()
    {
        byte[] data = new byte[IcnSpriteFile.HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(2), 0);

        Assert.Throws<BinaryFormatException>(() => IcnSpriteFile.Parse(data));
    }

    [Fact]
    public void Parse_RejectsATruncatedHeaderTable()
    {
        byte[] data = new byte[IcnSpriteFile.HeaderSize + IcnHeader.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(data, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(2), (uint)IcnHeader.Size);

        Assert.Throws<BinaryFormatException>(() => IcnSpriteFile.Parse(data));
    }

    [Fact]
    public void Parse_RejectsAPayloadOutsideTheEntry()
    {
        byte[] data = BuildContainer((new byte[] { 0x01, 5, 0x80 }, 1, 1, 0, 0, 0));

        // Point the payload past the end of the data block.
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(IcnSpriteFile.HeaderSize + 9), 4096);

        Assert.Throws<BinaryFormatException>(() => IcnSpriteFile.Parse(data));
    }

    /// <summary>
    /// Assembles a valid ICN entry: the file header, the header table and the contiguous payloads.
    /// </summary>
    private static byte[] BuildContainer(
        params (byte[] Payload, ushort Width, ushort Height, short OffsetX, short OffsetY, byte Frames)[] sprites)
    {
        int dataBlockSize = (sprites.Length * IcnHeader.Size) + sprites.Sum(sprite => sprite.Payload.Length);
        byte[] data = new byte[IcnSpriteFile.HeaderSize + dataBlockSize];

        BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)sprites.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(2), (uint)dataBlockSize);

        int payloadOffset = sprites.Length * IcnHeader.Size;
        for (int index = 0; index < sprites.Length; ++index)
        {
            (byte[] payload, ushort width, ushort height, short offsetX, short offsetY, byte frames) = sprites[index];

            Span<byte> header = data.AsSpan(IcnSpriteFile.HeaderSize + (index * IcnHeader.Size), IcnHeader.Size);
            BinaryPrimitives.WriteInt16LittleEndian(header, offsetX);
            BinaryPrimitives.WriteInt16LittleEndian(header[2..], offsetY);
            BinaryPrimitives.WriteUInt16LittleEndian(header[4..], width);
            BinaryPrimitives.WriteUInt16LittleEndian(header[6..], height);
            header[8] = frames;
            BinaryPrimitives.WriteUInt32LittleEndian(header[9..], (uint)payloadOffset);

            payload.CopyTo(data, IcnSpriteFile.HeaderSize + payloadOffset);
            payloadOffset += payload.Length;
        }

        return data;
    }
}
