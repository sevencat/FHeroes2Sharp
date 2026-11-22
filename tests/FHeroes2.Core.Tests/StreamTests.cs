using System.Text;
using FHeroes2.Core.Io;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests of the streams of <c>src/engine/serialize.h</c>: <see cref="StreamBase"/> and its three concrete streams.
/// </summary>
/// <remarks>
/// The members these tests exercise are the ones a save file is written through, so each test is written the way the
/// loader will use them: write with one stream, read with another, and compare.
/// </remarks>
public sealed class StreamTests
{
    [Fact]
    public void IntegersRoundTripInBothByteOrders()
    {
        StreamBuf stream = new();

        stream.Write((ushort)0x1234);
        stream.SetBigEndian(true);
        stream.Write((ushort)0x1234);
        stream.Write(0x12345678u);
        stream.SetBigEndian(false);
        stream.Write(0x12345678u);
        stream.Write(-2);

        stream.Seek(0);

        Assert.Equal(0x34, stream.Read());
        Assert.Equal(0x12, stream.Read());

        stream.SetBigEndian(true);
        Assert.Equal((ushort)0x1234, stream.ReadUInt16());
        Assert.Equal(0x12345678u, stream.ReadUInt32());

        stream.SetBigEndian(false);
        Assert.Equal(0x12345678u, stream.ReadUInt32());
        Assert.Equal(-2, stream.ReadInt32());
    }

    [Fact]
    public void TheEndianSpecificMembersIgnoreTheFlag()
    {
        StreamBuf stream = new();

        stream.WriteBe16(0xABCD);
        stream.WriteLe16(0xABCD);
        stream.WriteBe32(0x01020304u);
        stream.WriteLe32(0x01020304u);

        stream.Seek(0);

        Assert.Equal((ushort)0xABCD, stream.ReadBe16());
        Assert.Equal((ushort)0xABCD, stream.ReadLe16());
        Assert.Equal(0x01020304u, stream.ReadBe32());
        Assert.Equal(0x01020304u, stream.ReadLe32());
    }

    [Fact]
    public void AStringIsALengthAndTheBytesWithoutAZero()
    {
        StreamBuf stream = new();

        stream.Write("hero");
        stream.Write(string.Empty);
        stream.Write((string?)null);

        Assert.Equal(16, stream.Written.Length);

        stream.Seek(0);

        Assert.Equal(4, stream.ReadInt32());
        Assert.Equal("hero", stream.ReadString(4));
        Assert.Equal(0, stream.ReadInt32());
        Assert.Equal(0, stream.ReadInt32());
        Assert.True(stream.Size == 0);
    }

    [Fact]
    public void AFixedSizeStringStopsAtTheFirstZero()
    {
        // A padded field of a map header: the reference cuts the run it read at the first zero (getString( size )).
        StreamBuf stream = new();

        stream.PutRaw("abc\0def"u8);
        stream.Seek(0);

        Assert.Equal("abc", stream.ReadString(7));
    }

    [Fact]
    public void AStringWithoutASizeIsALengthPrefix()
    {
        // serialize.cpp:135-142: operator>>( std::string & ) reads a 32 bit length and then that many bytes,
        // so the string may hold a zero and the stream may go on after it.
        StreamBuf stream = new();

        stream.Write("ab\0c");
        stream.Write(7);

        stream.Seek(0);

        Assert.Equal("ab\0c", stream.ReadString());
        Assert.Equal(7u, stream.ReadUInt32());
        Assert.True(stream.Size == 0);
    }

    [Fact]
    public void Latin1KeepsEveryByte()
    {
        // Every byte maps to one character and back, which is what a name read from a map file needs.
        StreamBuf stream = new();

        stream.Write("A\u00ffZ");
        stream.Seek(0);

        _ = stream.ReadInt32();

        Assert.Equal("A\u00ffZ", stream.ReadString(3));
    }

    [Fact]
    public void BooleansAreOneByte()
    {
        StreamBuf stream = new();

        stream.Write(true);
        stream.Write(false);

        Assert.Equal(2, stream.Written.Length);

        stream.Seek(0);

        Assert.True(stream.ReadBool());
        Assert.False(stream.ReadBool());
    }

    [Fact]
    public void TheBufferStartsWithOneKilobyteAndGrowsByHalf()
    {
        StreamBuf stream = new();

        stream.Write((byte)1);
        Assert.Equal(1024, stream.Capacity);

        // A full buffer grows by half when the new data is small...
        StreamBuf full = new(1024);
        full.PutRaw(new byte[1024]);
        full.Write((byte)1);
        Assert.Equal(1536, full.Capacity);

        // ...and by the size of the write when that is larger.
        full.PutRaw(new byte[2000]);
        Assert.Equal(3536, full.Capacity);
    }

    [Fact]
    public void TheBufferKeepsWhatWasWrittenWhenItGrows()
    {
        StreamBuf stream = new();

        byte[] data = new byte[5000];
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = (byte)i;
        }

        stream.PutRaw(data);

        Assert.True(stream.Written.SequenceEqual(data));
        Assert.Equal(data.Length, stream.Size);
    }

    [Fact]
    public void SeekingPastTheEndLandsOnTheEnd()
    {
        StreamBuf stream = new();

        stream.PutRaw(new byte[4]);

        stream.Seek(100);
        Assert.Equal(0, stream.Size);
        Assert.Equal(4, stream.Tell);

        stream.Seek(-1);
        Assert.Equal(0, stream.Tell);
    }

    [Fact]
    public void ASkippedReadStopsAtTheEnd()
    {
        StreamBuf stream = new();

        stream.PutRaw(new byte[4]);
        stream.Seek(0);
        stream.Skip(10);

        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void AShortReadOfABufferIsZeroPaddedAndDoesNotFail()
    {
        StreamBuf stream = new();

        stream.PutRaw([1, 2]);
        stream.Seek(0);

        byte[] raw = stream.GetRaw(4);

        Assert.Equal(new byte[] { 1, 2, 0, 0 }, raw);
        Assert.False(stream.Fail);
    }

    [Fact]
    public void AShortReadOfAFileFails()
    {
        string path = TempPath();

        using StreamFile file = new();

        Assert.True(file.OpenForWriting(path));

        try
        {
            file.PutRaw(new byte[] { 1, 2, 3 });
            file.Close();

            Assert.True(file.OpenForReading(path));
            Assert.Equal(3, file.Size);
            Assert.Equal(new byte[] { 1, 2, 3 }, file.GetRaw(3));
            Assert.False(file.Fail);

            Assert.Empty(file.GetRaw(1));
            Assert.True(file.Fail);
        }
        finally
        {
            file.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void TheFileStreamRoundTripsEverythingTheBaseWrites()
    {
        string path = TempPath();

        try
        {
            using (StreamFile writer = new())
            {
                Assert.True(writer.OpenForWriting(path));

                writer.Write(7);
                writer.Write("Beltway");
                writer.Write((ushort)1234);
                writer.Close();
            }

            using StreamFile reader = new();

            Assert.True(reader.OpenForReading(path));
            Assert.Equal(7, reader.ReadInt32());
            Assert.Equal("Beltway", reader.ReadString(reader.ReadInt32()));
            Assert.Equal((ushort)1234, reader.ReadUInt16());
            Assert.False(reader.Fail);
            Assert.Equal(0, reader.Size);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheViewReadsWithoutCopyingAndCutsAtTheZero()
    {
        byte[] data = Encoding.Latin1.GetBytes("name\0tail");
        RoStreamBuf view = new(data);

        ReadOnlySpan<byte> name = view.GetStringView(9);

        Assert.Equal("name"u8.ToArray(), name.ToArray());

        // The view moves by what was asked for, not by what the string turned out to be.
        Assert.Equal(9, view.Tell);
        Assert.Equal(0, view.Size);
    }

    [Fact]
    public void AViewIsReadOnly()
    {
        RoStreamBuf view = new(new byte[4]);

        _ = Assert.Throws<NotSupportedException>(() => view.PutRaw([1]));
        _ = Assert.Throws<NotSupportedException>(() => view.Write((byte)1));
    }

    [Fact]
    public void AViewClampsItsPosition()
    {
        RoStreamBuf view = new(new byte[4]);

        view.Seek(100);
        Assert.Equal(4, view.Tell);

        view.Seek(-5);
        Assert.Equal(0, view.Tell);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
}
