using FHeroes2.Core.Io;

namespace FHeroes2.Core.Tests;

public sealed class LittleEndianIoTests
{
    [Fact]
    public void WriterAndReader_RoundTripEveryPrimitive()
    {
        using MemoryStream stream = new();

        using (LittleEndianWriter writer = new(stream))
        {
            writer.WriteByte(0x12);
            writer.WriteUInt16(0xBEEF);
            writer.WriteInt16(-2);
            writer.WriteUInt32(0xDEADBEEF);
            writer.WriteInt32(-123456);
            writer.WriteUInt64(0x0123456789ABCDEF);
            writer.WriteFixedAscii("KB.PAL", 15);
        }

        stream.Position = 0;
        using LittleEndianReader reader = new(stream);

        Assert.Equal(0x12, reader.ReadByte());
        Assert.Equal(0xBEEF, reader.ReadUInt16());
        Assert.Equal(-2, reader.ReadInt16());
        Assert.Equal(0xDEADBEEFu, reader.ReadUInt32());
        Assert.Equal(-123456, reader.ReadInt32());
        Assert.Equal(0x0123456789ABCDEFul, reader.ReadUInt64());
        Assert.Equal("KB.PAL", reader.ReadFixedAscii(15));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ReadNullTerminatedAscii_StopsAtNul()
    {
        using MemoryStream stream = new();
        using (LittleEndianWriter writer = new(stream))
        {
            writer.WriteFixedAscii("SMALFONT.ICN", 15);
        }

        stream.Position = 0;
        using LittleEndianReader reader = new(stream);

        Assert.Equal("SMALFONT.ICN", reader.ReadNullTerminatedAscii(15));
    }

    [Fact]
    public void ReadBytes_TruncatedInput_Throws()
    {
        using MemoryStream stream = new([1, 2, 3]);
        using LittleEndianReader reader = new(stream);

        Assert.Throws<BinaryFormatException>(() => reader.ReadBytes(8));
    }

    [Fact]
    public void ReadUInt32_TruncatedInput_Throws()
    {
        using MemoryStream stream = new([0x01, 0x02]);
        using LittleEndianReader reader = new(stream);

        Assert.Throws<BinaryFormatException>(() => reader.ReadUInt32());
    }

    [Fact]
    public void WriteFixedAscii_RejectsOverlongStrings()
    {
        using MemoryStream stream = new();
        using LittleEndianWriter writer = new(stream);

        Assert.Throws<ArgumentException>(() => writer.WriteFixedAscii("TOO-LONG-NAME-XX", 15));
    }

    [Fact]
    public void Position_CanSeekBackwards()
    {
        using MemoryStream stream = new([0xAA, 0xBB, 0xCC]);
        using LittleEndianReader reader = new(stream);

        Assert.Equal(0xAA, reader.ReadByte());
        reader.Position = 2;
        Assert.Equal(0xCC, reader.ReadByte());
        Assert.Equal(0, reader.Remaining);
    }
}
