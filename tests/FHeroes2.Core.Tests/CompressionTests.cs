using FHeroes2.Core.Io;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests of <see cref="Compression"/>, the port of <c>src/engine/zzlib.cpp</c>.
/// </summary>
/// <remarks>
/// The port links the framework's zlib instead of the C library, so the tests check two things: that a zlib stream
/// made by the framework is read back unchanged, and that the block layout the save files use - two sizes, a version
/// and two unused bytes - is the one the reference writes.
/// </remarks>
public sealed class CompressionTests
{
    [Fact]
    public void DataRoundTripsThroughZlib()
    {
        byte[] data = CreateRepetitiveData(4096);

        byte[] zip = Compression.ZipData(data, maximumCompression: false);

        Assert.NotEmpty(zip);
        Assert.Equal(data, Compression.UnzipData(zip));
        Assert.Equal(data, Compression.UnzipData(zip, data.Length));
    }

    [Fact]
    public void NothingIsCompressedIntoNothing()
    {
        Assert.Empty(Compression.ZipData(default, maximumCompression: false));
        Assert.Empty(Compression.UnzipData(default));
        Assert.Empty(Compression.UnzipData(new byte[] { 1, 2, 3 }, 0));
    }

    [Fact]
    public void ABlockOfTheWrongSizeIsRejected()
    {
        byte[] data = CreateRepetitiveData(1024);
        byte[] zip = Compression.ZipData(data, maximumCompression: false);

        // Too small: the decompressed data does not fit.
        Assert.Empty(Compression.UnzipData(zip, data.Length - 1));

        // Too large: the block does not fill the buffer the caller promised.
        Assert.Empty(Compression.UnzipData(zip, data.Length + 1));
    }

    [Fact]
    public void ABlockKeepsTheLayoutOfTheReference()
    {
        StreamBuf payload = new();
        payload.PutRaw(CreateRepetitiveData(1000));

        StreamBuf file = new();
        Assert.True(Compression.ZipStreamBuf(payload, file));
        Assert.False(file.Fail);

        file.Seek(0);

        Assert.Equal(1000, file.ReadInt32());

        int zipSize = file.ReadInt32();
        Assert.Equal(file.Written.Length - 12, zipSize);
        Assert.Equal(Compression.FormatVersion0, file.ReadUInt16());
        Assert.Equal(0, file.ReadUInt16());
    }

    [Fact]
    public void ABlockRoundTripsThroughTheStreamHelpers()
    {
        byte[] data = CreateRepetitiveData(5000);

        StreamBuf payload = new();
        payload.PutRaw(data);

        StreamBuf file = new();
        Assert.True(Compression.ZipStreamBuf(payload, file));

        // The save code keeps the block in a file and reads it back through a view over the file's bytes.
        RoStreamBuf view = new(file.Written.ToArray());
        StreamBuf restored = new();

        Assert.True(Compression.UnzipStream(view, restored));
        Assert.Equal(data, restored.Written.ToArray());
    }

    [Fact]
    public void AnUnknownVersionIsRejected()
    {
        StreamBuf payload = new();
        payload.PutRaw(CreateRepetitiveData(64));

        StreamBuf file = new();
        Assert.True(Compression.ZipStreamBuf(payload, file));

        // The version field is the two bytes right after the two sizes; a reader that meets a value it does not know
        // must give up before it looks at the zlib stream.
        byte[] block = file.Written.ToArray();
        block[8] = 1;

        RoStreamBuf view = new(block);
        StreamBuf restored = new();

        Assert.False(Compression.UnzipStream(view, restored));
        Assert.Empty(restored.Written.ToArray());
    }

    [Fact]
    public void ATruncatedBlockIsRejected()
    {
        StreamBuf payload = new();
        payload.PutRaw(CreateRepetitiveData(256));

        StreamBuf file = new();
        Assert.True(Compression.ZipStreamBuf(payload, file));

        // Losing the tail of the zlib stream is what a half written save file looks like.
        RoStreamBuf view = new(file.Written[..^8].ToArray());
        StreamBuf restored = new();

        Assert.False(Compression.UnzipStream(view, restored));
        Assert.Empty(restored.Written.ToArray());
    }

    [Fact]
    public void ABlockWithoutDataIsRejected()
    {
        // A zero zip size is what an interrupted write leaves behind; the reference refuses it before reading anything.
        StreamBuf file = new();
        file.Write(0); // rawSize
        file.Write(0); // zipSize
        file.Seek(0);

        StreamBuf restored = new();

        Assert.False(Compression.UnzipStream(file, restored));
    }

    private static byte[] CreateRepetitiveData(int size)
    {
        byte[] data = new byte[size];
        for (int i = 0; i < size; ++i)
        {
            data[i] = (byte)(i % 16);
        }

        return data;
    }
}
