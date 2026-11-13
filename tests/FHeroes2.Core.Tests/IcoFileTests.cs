using System.Buffers.Binary;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Structural tests for the Windows icon reader. Icons are assembled byte by byte in memory, so
/// every branch (PNG payload, 24 and 32 bit bitmap payloads, the AND mask, the legacy
/// all-zero-alpha rule and each rejection) runs without a file on disk.
/// </summary>
public sealed class IcoFileTests
{
    [Fact]
    public void Parse_PngPayload_DecodesTheImage()
    {
        PngImage source = SolidImage(16, new RgbaColor(0x12, 0x34, 0x56));

        IcoFile icon = IcoFile.Parse(BuildIcon((16, PngWriter.Encode(source), 32)));

        PngImage image = Assert.Single(icon.Images);
        Assert.Equal(16, image.Width);
        Assert.Equal(new RgbaColor(0x12, 0x34, 0x56), image[15, 15]);
    }

    [Fact]
    public void Parse_BitmapPayload_ReadsBottomUpRowsAndTheMask()
    {
        // Rows are handed to the builder top-down; a bitmap payload stores them bottom-up.
        RgbaColor[,] pixels =
        {
            { new(1, 1, 1, 0), new(2, 2, 2, 0) },
            { new(3, 3, 3, 0), new(4, 4, 4, 0) },
        };
        bool[,] transparent = { { false, true }, { false, false } };

        IcoFile icon = IcoFile.Parse(BuildIcon((2, BitmapPayload(pixels, 32, transparent), 32)));

        PngImage image = Assert.Single(icon.Images);
        Assert.Equal(new RgbaColor(1, 1, 1), image[0, 0]);
        Assert.Equal(new RgbaColor(2, 2, 2, 0), image[1, 0]);
        Assert.Equal(new RgbaColor(3, 3, 3), image[0, 1]);
        Assert.Equal(new RgbaColor(4, 4, 4), image[1, 1]);
    }

    [Fact]
    public void Parse_BitmapPayload_KeepsTheAlphaChannelWhenItIsUsed()
    {
        RgbaColor[,] pixels = { { new(10, 20, 30, 128) } };

        IcoFile icon = IcoFile.Parse(BuildIcon((1, BitmapPayload(pixels, 32), 32)));

        RgbaColor pixel = Assert.Single(icon.Images)[0, 0];
        Assert.Equal(new RgbaColor(10, 20, 30, 128), pixel);
    }

    [Fact]
    public void Parse_TwentyFourBitBitmap_SkipsTheRowPadding()
    {
        // Three pixels per row are nine bytes, padded to twelve: the padding must not leak into
        // the neighbouring row.
        RgbaColor[,] pixels =
        {
            { new(11, 11, 11), new(12, 12, 12), new(13, 13, 13) },
            { new(21, 21, 21), new(22, 22, 22), new(23, 23, 23) },
            { new(31, 31, 31), new(32, 32, 32), new(33, 33, 33) },
        };

        IcoFile icon = IcoFile.Parse(BuildIcon((3, BitmapPayload(pixels, 24), 24)));

        PngImage image = Assert.Single(icon.Images);
        Assert.Equal(new RgbaColor(11, 11, 11), image[0, 0]);
        Assert.Equal(new RgbaColor(13, 13, 13), image[2, 0]);
        Assert.Equal(new RgbaColor(21, 21, 21), image[0, 1]);
        Assert.Equal(new RgbaColor(33, 33, 33), image[2, 2]);
    }

    [Fact]
    public void Parse_TwoHundredFiftySixPixelEntry_IsNormalized()
    {
        PngImage source = SolidImage(256, RgbaColor.White);

        IcoFile icon = IcoFile.Parse(BuildIcon((256, PngWriter.Encode(source), 32)));

        PngImage image = Assert.Single(icon.Images);
        Assert.Equal(256, image.Width);
        Assert.Equal(256, image.Height);
    }

    [Fact]
    public void Parse_CursorFile_IsRejected()
    {
        byte[] data = BuildIcon((16, PngWriter.Encode(SolidImage(16, RgbaColor.White)), 32));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), 2);

        BinaryFormatException exception = Assert.Throws<BinaryFormatException>(() => IcoFile.Parse(data));

        Assert.Contains("cursor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TruncatedEntryTable_IsRejected()
    {
        byte[] data = BuildIcon((16, PngWriter.Encode(SolidImage(16, RgbaColor.White)), 32));

        BinaryFormatException exception = Assert.Throws<BinaryFormatException>(
            () => IcoFile.Parse(data.AsSpan(0, IcoFile.HeaderSize + 8)));

        Assert.Contains("entry table", exception.Message);
    }

    [Fact]
    public void Parse_PayloadOutsideTheFile_IsRejected()
    {
        byte[] data = BuildIcon((16, PngWriter.Encode(SolidImage(16, RgbaColor.White)), 32));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(IcoFile.HeaderSize + 12), (uint)data.Length);

        BinaryFormatException exception = Assert.Throws<BinaryFormatException>(() => IcoFile.Parse(data));

        Assert.Contains("does not fit", exception.Message);
    }

    [Fact]
    public void Parse_PaletteBitmap_IsRejected()
    {
        byte[] payload = BitmapPayload(new RgbaColor[,] { { RgbaColor.White } }, 32);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), 8);

        byte[] data = BuildIcon((1, payload, 8));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(IcoFile.HeaderSize + 6), 8);

        BinaryFormatException exception = Assert.Throws<BinaryFormatException>(() => IcoFile.Parse(data));

        Assert.Contains("8 bit bitmap", exception.Message);
    }

    [Fact]
    public void Parse_CompressedBitmap_IsRejected()
    {
        byte[] payload = BitmapPayload(new RgbaColor[,] { { RgbaColor.White } }, 32);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16), 3);

        byte[] data = BuildIcon((1, payload, 32));

        BinaryFormatException exception = Assert.Throws<BinaryFormatException>(() => IcoFile.Parse(data));

        Assert.Contains("compression 3", exception.Message);
    }

    [Fact]
    public void Parse_TooShortData_IsRejected()
    {
        BinaryFormatException exception = Assert.Throws<BinaryFormatException>(() => IcoFile.Parse([0x00, 0x00]));

        Assert.Contains("at least", exception.Message);
    }



    [Fact]
    public void Parse_RepositoryIcon_HasTheExpectedSizes()
    {
        string? path = FindRepositoryFile(Path.Combine("assets", "logo.ico"));
        if (path is null)
        {
            return;
        }

        using FileStream stream = File.OpenRead(path);
        IcoFile icon = IcoFile.Load(stream);

        Assert.Equal([16, 24, 32, 48, 64, 128, 256], icon.Images.Select(image => image.Width));
        Assert.Equal(256, icon.Largest.Width);

        // The artwork is opaque: a fully transparent icon would show up as a blank taskbar entry.
        foreach (PngImage image in icon.Images)
        {
            Assert.Equal(255, image[image.Width / 2, image.Height / 2].A);
        }
    }

    [Fact]
    public void Closest_ReturnsTheSmallestImageThatFits()
    {
        IcoFile icon = IcoFile.Parse(BuildIcon(
            (16, PngWriter.Encode(SolidImage(16, RgbaColor.White)), 32),
            (48, PngWriter.Encode(SolidImage(48, RgbaColor.White)), 32),
            (256, PngWriter.Encode(SolidImage(256, RgbaColor.White)), 32)));

        Assert.Equal(16, icon.Closest(1).Width);
        Assert.Equal(48, icon.Closest(32).Width);
        Assert.Equal(256, icon.Closest(200).Width);
    }

    [Fact]
    public void Closest_FallsBackToTheLargestImage()
    {
        IcoFile icon = IcoFile.Parse(BuildIcon(
            (16, PngWriter.Encode(SolidImage(16, RgbaColor.White)), 32),
            (48, PngWriter.Encode(SolidImage(48, RgbaColor.White)), 32)));

        Assert.Equal(48, icon.Closest(64).Width);
    }

    /// <summary>Builds a fully opaque square image of one colour.</summary>
    private static PngImage SolidImage(int size, RgbaColor color)
    {
        byte[] rgba = new byte[size * size * 4];
        for (int offset = 0; offset < rgba.Length; offset += 4)
        {
            rgba[offset] = color.R;
            rgba[offset + 1] = color.G;
            rgba[offset + 2] = color.B;
            rgba[offset + 3] = color.A;
        }

        return new PngImage(size, size, rgba);
    }

    /// <summary>Assembles an icon file around already encoded payloads.</summary>
    private static byte[] BuildIcon(params (int Size, byte[] Payload, int BitCount)[] images)
    {
        int payloadOffset = IcoFile.HeaderSize + (images.Length * IcoFile.EntrySize);
        byte[] data = new byte[payloadOffset + images.Sum(image => image.Payload.Length)];

        BinaryPrimitives.WriteUInt16LittleEndian(data, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), (ushort)images.Length);

        int offset = payloadOffset;
        for (int i = 0; i < images.Length; ++i)
        {
            (int size, byte[] payload, int bitCount) = images[i];
            Span<byte> entry = data.AsSpan(IcoFile.HeaderSize + (i * IcoFile.EntrySize), IcoFile.EntrySize);

            // 256 is stored as 0: the field is one byte wide.
            entry[0] = (byte)(size == 256 ? 0 : size);
            entry[1] = entry[0];
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], (ushort)bitCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], (uint)offset);

            payload.CopyTo(data.AsSpan(offset));
            offset += payload.Length;
        }

        return data;
    }

    /// <summary>
    /// Encodes a <c>BITMAPINFOHEADER</c> payload: bottom-up BGRA rows padded to four bytes and the
    /// matching AND mask.
    /// </summary>
    private static byte[] BitmapPayload(RgbaColor[,] pixels, int depth, bool[,]? transparent = null)
    {
        int height = pixels.GetLength(0);
        int width = pixels.GetLength(1);
        int bytesPerPixel = depth / 8;
        int colorStride = ((width * bytesPerPixel) + 3) & ~3;
        int maskStride = ((width + 31) / 32) * 4;

        byte[] payload = new byte[
            IcoFile.BitmapInfoHeaderSize + (colorStride * height) + (maskStride * height)];

        BinaryPrimitives.WriteInt32LittleEndian(payload, IcoFile.BitmapInfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), height * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), (ushort)depth);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(20), colorStride * height);

        int maskStart = IcoFile.BitmapInfoHeaderSize + (colorStride * height);
        for (int y = 0; y < height; ++y)
        {
            int colorRow = IcoFile.BitmapInfoHeaderSize + ((height - 1 - y) * colorStride);
            int maskRow = maskStart + ((height - 1 - y) * maskStride);

            for (int x = 0; x < width; ++x)
            {
                RgbaColor color = pixels[y, x];
                int target = colorRow + (x * bytesPerPixel);
                payload[target] = color.B;
                payload[target + 1] = color.G;
                payload[target + 2] = color.R;

                if (bytesPerPixel == 4)
                {
                    payload[target + 3] = color.A;
                }

                if (transparent is not null && transparent[y, x])
                {
                    payload[maskRow + (x / 8)] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        return payload;
    }

    /// <summary>Walks up from the test output folder to <c>assets/…</c>; null outside the repo.</summary>
    private static string? FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FHeroes2Sharp.slnx")))
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                return File.Exists(candidate) ? candidate : null;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
