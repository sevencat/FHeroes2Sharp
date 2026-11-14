using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using FHeroes2.Core.Io;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Minimal PNG decoder used to read back the files we export and, more importantly, to compare
/// decoded assets against images produced by the reference engine or by a third party tool.
/// </summary>
/// <remarks>
/// Supported: color types 0 (grayscale), 2 (RGB), 3 (palette), 4 (grayscale + alpha) and
/// 6 (RGBA); bit depths 1/2/4/8/16 where the PNG specification allows them; all five row filters
/// (none, sub, up, average, Paeth); <c>tRNS</c> transparency for grayscale, RGB and palette images.
/// Adam7 interlacing is the only unimplemented piece and is rejected explicitly. Data that does not
/// follow the specification raises <see cref="BinaryFormatException"/> with the failing chunk name,
/// so a broken export is easy to diagnose.
/// </remarks>
public static class PngReader
{
    private const int ColorTypeGrayscale = 0;
    private const int ColorTypeRgb = 2;
    private const int ColorTypePalette = 3;
    private const int ColorTypeGrayscaleAlpha = 4;
    private const int ColorTypeRgba = 6;

    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Decodes a PNG file held in <paramref name="stream"/>.</summary>
    public static PngImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return Decode(copy.GetBuffer().AsSpan(0, (int)copy.Length));
    }

    /// <summary>Decodes a PNG file held in memory.</summary>
    public static PngImage Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < Signature.Length || !data[..Signature.Length].SequenceEqual(Signature))
        {
            throw new BinaryFormatException("The data does not start with the PNG signature.");
        }

        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = -1;
        byte[]? palette = null;
        byte[]? transparency = null;
        using MemoryStream imageData = new();

        bool sawHeader = false;
        bool sawEnd = false;

        int position = Signature.Length;
        while (position < data.Length)
        {
            if (position + 8 > data.Length)
            {
                throw new BinaryFormatException($"The PNG chunk header at offset {position} is truncated.");
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(data[position..]);
            if (length < 0 || position + 12 + length > data.Length)
            {
                throw new BinaryFormatException(
                    $"The PNG chunk at offset {position} is truncated (declared length {length}).");
            }

            ReadOnlySpan<byte> type = data.Slice(position + 4, 4);
            ReadOnlySpan<byte> payload = data.Slice(position + 8, length);
            uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(data[(position + 8 + length)..]);
            string name = Encoding.ASCII.GetString(type);

            if (Crc32.Compute(type, payload) != storedCrc)
            {
                throw new BinaryFormatException($"The PNG chunk '{name}' has an invalid CRC.");
            }

            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader)
                {
                    throw new BinaryFormatException("The PNG file contains more than one header chunk.");
                }

                if (length != 13)
                {
                    throw new BinaryFormatException($"The PNG header chunk is {length} bytes long instead of 13.");
                }

                width = BinaryPrimitives.ReadInt32BigEndian(payload);
                height = BinaryPrimitives.ReadInt32BigEndian(payload[4..]);
                bitDepth = payload[8];
                colorType = payload[9];

                if (width <= 0 || height <= 0)
                {
                    throw new BinaryFormatException($"The PNG image size {width}x{height} is not positive.");
                }

                if (payload[10] != 0 || payload[11] != 0)
                {
                    throw new BinaryFormatException("The PNG file uses an unknown compression or filter method.");
                }

                if (payload[12] != 0)
                {
                    throw new NotSupportedException("Adam7 interlaced PNG files are not supported.");
                }

                ValidateHeader(colorType, bitDepth);
                sawHeader = true;
            }
            else if (!sawHeader)
            {
                throw new BinaryFormatException($"The PNG file starts with the '{name}' chunk instead of a header.");
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (length == 0 || length % 3 != 0 || length > Palette.ColorCount * 3)
                {
                    throw new BinaryFormatException($"The PNG palette chunk has the invalid length {length}.");
                }

                palette = payload.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                transparency = payload.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                imageData.Write(payload);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
                position += 12 + length;
                break;
            }
            else if (char.IsUpper(name[0]))
            {
                // Unknown critical chunk: the specification forbids silently ignoring it.
                throw new NotSupportedException($"The PNG file uses the unsupported critical chunk '{name}'.");
            }

            position += 12 + length;
        }

        if (!sawHeader)
        {
            throw new BinaryFormatException("The PNG file has no header chunk.");
        }

        if (!sawEnd)
        {
            throw new BinaryFormatException("The PNG file has no end chunk.");
        }

        if (imageData.Length == 0)
        {
            throw new BinaryFormatException("The PNG file has no image data.");
        }

        if (colorType == ColorTypePalette && palette is null)
        {
            throw new BinaryFormatException("The indexed PNG file has no palette chunk.");
        }

        byte[] scanlines = Inflate(imageData, GetScanlineLength(width, height, colorType, bitDepth));
        byte[] samples = Unfilter(scanlines, width, colorType, bitDepth, out int rowLength);

        return new PngImage(
            width,
            height,
            Expand(samples, width, height, colorType, bitDepth, rowLength, palette, transparency));
    }

    private static void ValidateHeader(int colorType, int bitDepth)
    {
        bool valid = colorType switch
        {
            ColorTypeGrayscale => bitDepth is 1 or 2 or 4 or 8 or 16,
            ColorTypeRgb => bitDepth is 8 or 16,
            ColorTypePalette => bitDepth is 1 or 2 or 4 or 8,
            ColorTypeGrayscaleAlpha => bitDepth is 8 or 16,
            ColorTypeRgba => bitDepth is 8 or 16,
            _ => false,
        };

        if (!valid)
        {
            throw new BinaryFormatException(
                $"The PNG header combines the color type {colorType} with the unsupported bit depth {bitDepth}.");
        }
    }

    private static int GetChannels(int colorType) => colorType switch
    {
        ColorTypeGrayscale => 1,
        ColorTypeRgb => 3,
        ColorTypePalette => 1,
        ColorTypeGrayscaleAlpha => 2,
        ColorTypeRgba => 4,
        _ => throw new BinaryFormatException($"The PNG color type {colorType} is not supported."),
    };

    private static int GetRowLength(int width, int colorType, int bitDepth) =>
        ((width * GetChannels(colorType) * bitDepth) + 7) / 8;

    private static int GetScanlineLength(int width, int height, int colorType, int bitDepth) =>
        checked((GetRowLength(width, colorType, bitDepth) + 1) * height);

    private static byte[] Inflate(MemoryStream stream, int expectedLength)
    {
        stream.Position = 0;

        byte[] raw = new byte[expectedLength];
        int read = 0;

        using (ZLibStream inflate = new(stream, CompressionMode.Decompress, leaveOpen: true))
        {
            while (read < expectedLength)
            {
                int count = inflate.Read(raw, read, expectedLength - read);
                if (count == 0)
                {
                    throw new BinaryFormatException(
                        $"The PNG image data is too short: {read} of {expectedLength} bytes were decoded.");
                }

                read += count;
            }

            byte[] extra = new byte[1];
            if (inflate.Read(extra, 0, 1) != 0)
            {
                throw new BinaryFormatException(
                    $"The PNG image data is longer than the {expectedLength} bytes declared by the header.");
            }
        }

        return raw;
    }

    /// <summary>
    /// Reverses the per row filters, turning the scanlines into the raw sample rows described by the
    /// PNG specification. The filter of a row refers to the row above it, so the output buffer is
    /// filled in place.
    /// </summary>
    private static byte[] Unfilter(ReadOnlySpan<byte> scanlines, int width, int colorType, int bitDepth, out int rowLength)
    {
        rowLength = GetRowLength(width, colorType, bitDepth);
        int bytesPerPixel = Math.Max(1, (GetChannels(colorType) * bitDepth) / 8);
        int height = scanlines.Length / (rowLength + 1);
        byte[] samples = new byte[rowLength * height];

        for (int y = 0; y < height; ++y)
        {
            int filter = scanlines[y * (rowLength + 1)];
            ReadOnlySpan<byte> source = scanlines.Slice((y * (rowLength + 1)) + 1, rowLength);
            Span<byte> target = samples.AsSpan(y * rowLength, rowLength);
            ReadOnlySpan<byte> previous = y == 0
                ? ReadOnlySpan<byte>.Empty
                : samples.AsSpan((y - 1) * rowLength, rowLength);

            for (int x = 0; x < rowLength; ++x)
            {
                byte left = x >= bytesPerPixel ? target[x - bytesPerPixel] : (byte)0;
                byte above = previous.IsEmpty ? (byte)0 : previous[x];
                byte upperLeft = x >= bytesPerPixel && !previous.IsEmpty
                    ? previous[x - bytesPerPixel]
                    : (byte)0;

                target[x] = filter switch
                {
                    0 => source[x],
                    1 => (byte)(source[x] + left),
                    2 => (byte)(source[x] + above),
                    3 => (byte)(source[x] + ((left + above) >> 1)),
                    4 => (byte)(source[x] + Paeth(left, above, upperLeft)),
                    _ => throw new BinaryFormatException($"The PNG row {y} uses the unknown filter type {filter}."),
                };
            }
        }

        return samples;
    }

    private static byte Paeth(byte left, byte above, byte upperLeft)
    {
        int estimation = left + above - upperLeft;
        int distanceLeft = Math.Abs(estimation - left);
        int distanceAbove = Math.Abs(estimation - above);
        int distanceUpperLeft = Math.Abs(estimation - upperLeft);

        if (distanceLeft <= distanceAbove && distanceLeft <= distanceUpperLeft)
        {
            return left;
        }

        return distanceAbove <= distanceUpperLeft ? above : upperLeft;
    }

    /// <summary>Expands the unfiltered sample rows into straight RGBA pixels.</summary>
    private static byte[] Expand(
        ReadOnlySpan<byte> samples,
        int width,
        int height,
        int colorType,
        int bitDepth,
        int rowLength,
        byte[]? palette,
        byte[]? transparency)
    {
        byte[] rgba = new byte[checked(width * height * 4)];

        for (int y = 0; y < height; ++y)
        {
            ReadOnlySpan<byte> row = samples.Slice(y * rowLength, rowLength);

            for (int x = 0; x < width; ++x)
            {
                int target = ((y * width) + x) * 4;
                RgbaColor color = colorType switch
                {
                    ColorTypeGrayscale => ReadGrayscale(row, x, bitDepth, transparency),
                    ColorTypeRgb => ReadRgb(row, x, bitDepth, transparency),
                    ColorTypePalette => ReadPalette(row, x, bitDepth, palette!, transparency),
                    ColorTypeGrayscaleAlpha => ReadGrayscaleAlpha(row, x, bitDepth),
                    _ => ReadRgba(row, x, bitDepth),
                };

                rgba[target] = color.R;
                rgba[target + 1] = color.G;
                rgba[target + 2] = color.B;
                rgba[target + 3] = color.A;
            }
        }

        return rgba;
    }

    private static RgbaColor ReadGrayscale(ReadOnlySpan<byte> row, int x, int bitDepth, byte[]? transparency)
    {
        int maximum = (1 << bitDepth) - 1;
        int sample;

        if (bitDepth >= 8)
        {
            sample = bitDepth == 16 ? (row[x * 2] << 8) | row[(x * 2) + 1] : row[x];
        }
        else
        {
            int bit = x * bitDepth;
            sample = (row[bit >> 3] >> (8 - bitDepth - (bit & 7))) & maximum;
        }

        bool transparent = transparency is { Length: 2 }
            && (((transparency[0] << 8) | transparency[1]) == sample);

        byte value = bitDepth == 16 ? (byte)(sample >> 8) : (byte)(sample * 255 / maximum);

        return new RgbaColor(value, value, value, transparent ? (byte)0 : (byte)255);
    }

    private static RgbaColor ReadRgb(ReadOnlySpan<byte> row, int x, int bitDepth, byte[]? transparency)
    {
        byte red = WideSampleByte(row, (x * 3) + 0, bitDepth);
        byte green = WideSampleByte(row, (x * 3) + 1, bitDepth);
        byte blue = WideSampleByte(row, (x * 3) + 2, bitDepth);

        bool transparent = transparency is { Length: 6 }
            && ReadWideSample(row, (x * 3) + 0, bitDepth) == ((transparency[0] << 8) | transparency[1])
            && ReadWideSample(row, (x * 3) + 1, bitDepth) == ((transparency[2] << 8) | transparency[3])
            && ReadWideSample(row, (x * 3) + 2, bitDepth) == ((transparency[4] << 8) | transparency[5]);

        return new RgbaColor(red, green, blue, transparent ? (byte)0 : (byte)255);
    }

    private static RgbaColor ReadGrayscaleAlpha(ReadOnlySpan<byte> row, int x, int bitDepth)
    {
        byte value = WideSampleByte(row, x * 2, bitDepth);
        return new RgbaColor(value, value, value, WideSampleByte(row, (x * 2) + 1, bitDepth));
    }

    private static RgbaColor ReadRgba(ReadOnlySpan<byte> row, int x, int bitDepth) => new(
        WideSampleByte(row, (x * 4) + 0, bitDepth),
        WideSampleByte(row, (x * 4) + 1, bitDepth),
        WideSampleByte(row, (x * 4) + 2, bitDepth),
        WideSampleByte(row, (x * 4) + 3, bitDepth));

    private static RgbaColor ReadPalette(ReadOnlySpan<byte> row, int x, int bitDepth, byte[] palette, byte[]? transparency)
    {
        int index;

        if (bitDepth == 8)
        {
            index = row[x];
        }
        else
        {
            int bit = x * bitDepth;
            index = (row[bit >> 3] >> (8 - bitDepth - (bit & 7))) & ((1 << bitDepth) - 1);
        }

        int entry = index * 3;
        if (entry + 2 >= palette.Length)
        {
            throw new BinaryFormatException(
                $"The PNG pixel {x} references the palette entry {index} but only {palette.Length / 3} are defined.");
        }

        byte alpha = transparency is not null && index < transparency.Length ? transparency[index] : (byte)255;

        return new RgbaColor(palette[entry], palette[entry + 1], palette[entry + 2], alpha);
    }

    /// <summary>Reads one sample of an 8 or 16 bit row, keeping the full value.</summary>
    private static int ReadWideSample(ReadOnlySpan<byte> row, int index, int bitDepth) =>
        bitDepth == 16 ? (row[index * 2] << 8) | row[(index * 2) + 1] : row[index];

    /// <summary>Reads one sample of an 8 or 16 bit row as a byte, keeping the high byte.</summary>
    private static byte WideSampleByte(ReadOnlySpan<byte> row, int index, int bitDepth) =>
        bitDepth == 16 ? row[index * 2] : row[index];
}
