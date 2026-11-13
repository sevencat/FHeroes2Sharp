using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Structural tests for the PNG exporter. The file is parsed back chunk by chunk (including the
/// CRC checks) and the inflated scanlines are compared against the indexed source image, so the
/// tests fail if the writer ever produces a file that viewers would reject.
/// </summary>
public sealed class PngWriterTests
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void EncodeRgba_WritesAValidPngHeader()
    {
        PixelBuffer image = new(2, 3);

        DecodedPng png = Decode(PngWriter.EncodeRgba(image, new Palette()));

        Assert.Equal(2, png.Width);
        Assert.Equal(3, png.Height);
    }

    [Fact]
    public void EncodeRgba_MapsThePaletteToOpaquePixels()
    {
        PixelBuffer image = new(2, 1);
        image[0, 0] = 1;
        image[1, 0] = 2;

        DecodedPng png = Decode(PngWriter.EncodeRgba(image, new Palette()));

        Assert.Equal(new RgbaColor(1, 1, 1), png.Colors[0]);
        Assert.Equal(new RgbaColor(2, 2, 2), png.Colors[1]);
    }

    [Fact]
    public void EncodeRgba_WritesSkippedPixelsAsTransparent()
    {
        PixelBuffer image = new(2, 1);
        image[0, 0] = 10;
        image[1, 0] = 20;

        byte[] transform = [0, 1];

        DecodedPng png = Decode(PngWriter.EncodeRgba(image, new Palette(), transform));

        Assert.Equal(255, png.Colors[0].A);
        Assert.Equal(0, png.Colors[1].A);
    }

    [Fact]
    public void EncodeRgba_RejectsAMismatchedTransformLayer()
    {
        PixelBuffer image = new(2, 2);

        Assert.Throws<ArgumentException>(() => PngWriter.EncodeRgba(image, new Palette(), new byte[3]));
    }

    [Fact]
    public void EncodeRgba_UsesTheGamePaletteColors()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];
        raw[0] = 63;
        raw[1] = 0;
        raw[2] = 0;

        PixelBuffer image = new(1, 1);

        DecodedPng png = Decode(PngWriter.EncodeRgba(image, Palette.FromGamePalette(raw)));

        Assert.Equal(new RgbaColor(252, 0, 0), png.Colors[0]);
    }

    private static DecodedPng Decode(byte[] png)
    {
        Assert.True(png.Length > PngSignature.Length);
        Assert.Equal(PngSignature, png[..PngSignature.Length]);

        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;
        bool sawEnd = false;
        using MemoryStream idat = new();

        int position = PngSignature.Length;
        while (position < png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position, 4));
            string type = Encoding.ASCII.GetString(png, position + 4, 4);
            ReadOnlySpan<byte> data = png.AsSpan(position + 8, length);
            uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length, 4));

            Assert.Equal(Crc32(png.AsSpan(position + 4, 4 + length)), storedCrc);

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    bitDepth = data[8];
                    colorType = data[9];
                    break;

                case "IDAT":
                    idat.Write(data);
                    break;

                case "IEND":
                    sawEnd = true;
                    break;
            }

            position += 12 + length;
        }

        Assert.True(sawEnd, "The PNG file has no IEND chunk.");
        Assert.Equal(8, bitDepth);
        Assert.Equal(6, colorType);

        idat.Position = 0;
        using ZLibStream inflate = new(idat, CompressionMode.Decompress);
        using MemoryStream raw = new();
        inflate.CopyTo(raw);

        byte[] scanlines = raw.ToArray();
        int stride = (width * 4) + 1;
        Assert.Equal(stride * height, scanlines.Length);

        RgbaColor[] colors = new RgbaColor[width * height];

        for (int y = 0; y < height; ++y)
        {
            int rowStart = y * stride;
            Assert.Equal(0, scanlines[rowStart]);

            for (int x = 0; x < width; ++x)
            {
                int offset = rowStart + 1 + (x * 4);
                colors[(y * width) + x] = new RgbaColor(
                    scanlines[offset],
                    scanlines[offset + 1],
                    scanlines[offset + 2],
                    scanlines[offset + 3]);
            }
        }

        return new DecodedPng(width, height, colors);
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;

        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; ++bit)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private sealed record DecodedPng(int Width, int Height, RgbaColor[] Colors);
}
