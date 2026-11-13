using System.Buffers.Binary;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for the flat TIL container: the 6 byte header and the raw tile payloads.
/// </summary>
public sealed class TilFileTests
{
    [Fact]
    public void Parse_ReturnsEveryTile()
    {
        byte[] data = BuildTil(3, 2, 2, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

        TilFile til = TilFile.Parse(data);

        Assert.Equal(3, til.Count);
        Assert.Equal(2, til.Width);
        Assert.Equal(2, til.Height);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, til.Images[0].Pixels);
        Assert.Equal(new byte[] { 9, 10, 11, 12 }, til.Images[2].Pixels);
    }

    [Fact]
    public void Parse_RejectsAnEntryThatIsTooShort()
    {
        Assert.Throws<BinaryFormatException>(() => TilFile.Parse(new byte[3]));
    }

    [Fact]
    public void Parse_RejectsAnInvalidHeader()
    {
        byte[] data = BuildTil(1, 1, 1, [0]);

        // Zero out the tile count.
        BinaryPrimitives.WriteUInt16LittleEndian(data, 0);

        Assert.Throws<BinaryFormatException>(() => TilFile.Parse(data));
    }

    [Fact]
    public void Parse_RejectsAMismatchedPayloadSize()
    {
        byte[] data = BuildTil(2, 2, 2, [1, 2, 3, 4, 5, 6, 7]);

        Assert.Throws<BinaryFormatException>(() => TilFile.Parse(data));
    }

    private static byte[] BuildTil(int count, int width, int height, byte[] pixels)
    {
        byte[] data = new byte[TilFile.HeaderSize + pixels.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)count);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), (ushort)height);
        pixels.CopyTo(data, TilFile.HeaderSize);

        return data;
    }
}
