using System.Buffers.Binary;
using FHeroes2.Core.Io;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// A Windows icon (<c>.ico</c>) reader, used to turn the application icon into the window icon.
/// </summary>
/// <remarks>
/// <para>
/// The container is a six byte <c>ICONDIR</c> header (reserved, type, image count) followed by one
/// sixteen byte <c>ICONDIRENTRY</c> per image and the payloads themselves. The layout is not part
/// of the reference implementation — fheroes2 sets its window icon from a compiled resource — so
/// this file only has to agree with <see cref="PngReader"/> and with the generator in
/// <c>tools/scripts/GenerateAppIcon.ps1</c>.
/// </para>
/// <para>
/// A payload is either a whole PNG file or a device independent bitmap: a forty byte
/// <c>BITMAPINFOHEADER</c> whose height covers the colour rows <b>and</b> the AND mask, the
/// bottom-up colour rows, then the bottom-up mask. Every entry is decoded into a straight alpha
/// <see cref="PngImage"/>, so callers see one pixel layout whatever the payload format was.
/// </para>
/// <para>
/// Uncompressed 24 and 32 bit bitmaps are supported. Palette based and run length encoded bitmaps
/// are not, and neither are cursors (<c>type = 2</c>, which carry a hot spot instead of an icon):
/// producing those on purpose is not worth a decoder nobody would exercise. The repository icon
/// uses 32 bit bitmaps up to 64 pixels (universally readable) and PNG above that (Vista and later).
/// </para>
/// </remarks>
public sealed class IcoFile
{
    /// <summary>Size of the <c>ICONDIR</c> header: reserved, type and image count.</summary>
    public const int HeaderSize = 6;

    /// <summary>Size of one <c>ICONDIRENTRY</c>.</summary>
    public const int EntrySize = 16;

    /// <summary>Size of the <c>BITMAPINFOHEADER</c> that starts a bitmap payload.</summary>
    public const int BitmapInfoHeaderSize = 40;

    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private IcoFile(IReadOnlyList<PngImage> images)
    {
        Images = images;
        Largest = images[0];
        foreach (PngImage image in images)
        {
            if (image.Width * image.Height > Largest.Width * Largest.Height)
            {
                Largest = image;
            }
        }
    }

    /// <summary>The decoded images in the order they appear in the file.</summary>
    public IReadOnlyList<PngImage> Images { get; }

    /// <summary>The image with the most pixels, which is the one to use as a 100% scale icon.</summary>
    public PngImage Largest { get; }

    /// <summary>
    /// Picks the image for an icon of the given edge length: the smallest image that is at least
    /// <paramref name="size"/> pixels wide, or the largest image when every entry is smaller. That
    /// is the rule <c>SDL_SetWindowIcon</c> documents for its alternate representations.
    /// </summary>
    public PngImage Closest(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        PngImage? best = null;
        foreach (PngImage image in Images)
        {
            if (image.Width >= size && (best is null || image.Width < best.Width))
            {
                best = image;
            }
        }

        if (best is not null)
        {
            return best;
        }

        best = Images[0];
        foreach (PngImage image in Images)
        {
            if (image.Width > best.Width && image.Width < size)
            {
                best = image;
            }
        }

        return best;
    }

    /// <summary>
    /// Reads an icon file from <paramref name="stream"/>; the stream is consumed to its end.
    /// </summary>
    /// <exception cref="BinaryFormatException">The data is not a supported icon file.</exception>
    public static IcoFile Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return Parse(copy.GetBuffer().AsSpan(0, (int)copy.Length));
    }

    /// <summary>Parses an icon file held in memory.</summary>
    /// <exception cref="BinaryFormatException">The data is not a supported icon file.</exception>
    public static IcoFile Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new BinaryFormatException(
                $"An icon needs at least {HeaderSize} bytes but only {data.Length} were provided.");
        }

        int reserved = BinaryPrimitives.ReadUInt16LittleEndian(data);
        int type = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);

        if (reserved != 0)
        {
            throw new BinaryFormatException($"The icon header starts with {reserved} instead of the reserved 0.");
        }

        if (type != 1)
        {
            throw new BinaryFormatException(type == 2
                ? "The file is a cursor (type 2); only icons (type 1) are supported."
                : $"The file type is {type} instead of 1 (icon).");
        }

        if (count < 1)
        {
            throw new BinaryFormatException("The icon declares no images.");
        }

        long tableEnd = HeaderSize + ((long)count * EntrySize);
        if (data.Length < tableEnd)
        {
            throw new BinaryFormatException(
                $"The icon declares {count} images, so the entry table ends at {tableEnd} bytes, " +
                $"but the file is only {data.Length} bytes long.");
        }

        PngImage[] images = new PngImage[count];
        for (int i = 0; i < count; ++i)
        {
            ReadOnlySpan<byte> entry = data.Slice(HeaderSize + (i * EntrySize), EntrySize);

            // 0 encodes 256 pixels: the field is one byte wide.
            int width = entry[0] == 0 ? 256 : entry[0];
            int height = entry[1] == 0 ? 256 : entry[1];
            int bitCount = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]);
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);

            if (size < 1)
            {
                throw new BinaryFormatException($"Image {i} ({width}x{height}) declares no payload.");
            }

            if (offset < 0 || (long)offset + size > data.Length)
            {
                throw new BinaryFormatException(
                    $"Image {i} ({width}x{height}) claims {size} bytes at offset {offset}, " +
                    $"which does not fit in a file of {data.Length} bytes.");
            }

            images[i] = DecodeImage(data.Slice(offset, size), width, height, bitCount, i);
        }

        return new IcoFile(images);
    }

    private static PngImage DecodeImage(ReadOnlySpan<byte> payload, int width, int height, int bitCount, int index)
    {
        if (payload.StartsWith(PngSignature))
        {
            PngImage png = PngReader.Decode(payload);
            if (png.Width != width || png.Height != height)
            {
                throw new BinaryFormatException(
                    $"Image {index} is declared as {width}x{height} but contains a {png.Width}x{png.Height} PNG.");
            }

            return png;
        }

        return DecodeBitmap(payload, width, height, bitCount, index);
    }

    private static PngImage DecodeBitmap(ReadOnlySpan<byte> payload, int width, int height, int bitCount, int index)
    {
        if (payload.Length < BitmapInfoHeaderSize)
        {
            throw new BinaryFormatException(
                $"Image {index} is neither a PNG nor a bitmap: only {payload.Length} bytes long.");
        }

        int headerSize = BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (headerSize != BitmapInfoHeaderSize)
        {
            throw new BinaryFormatException(
                $"Image {index} uses a {headerSize} byte bitmap header; only the 40 byte " +
                "BITMAPINFOHEADER is supported.");
        }

        int bitmapWidth = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        int bitmapHeight = BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        int planes = BinaryPrimitives.ReadUInt16LittleEndian(payload[12..]);
        int depth = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
        int compression = BinaryPrimitives.ReadInt32LittleEndian(payload[16..]);

        if (planes != 1)
        {
            throw new BinaryFormatException($"Image {index} declares {planes} colour planes instead of 1.");
        }

        // The stored height covers both pages: the colour rows and the AND mask rows below them.
        if (bitmapWidth != width || bitmapHeight != height * 2)
        {
            throw new BinaryFormatException(
                $"Image {index} is declared as {width}x{height} but its bitmap is " +
                $"{bitmapWidth}x{bitmapHeight} (the bitmap height must be twice the icon height).");
        }

        if (compression != 0)
        {
            throw new BinaryFormatException(
                $"Image {index} uses bitmap compression {compression}; only uncompressed bitmaps are supported.");
        }

        if (depth != 24 && depth != 32)
        {
            throw new BinaryFormatException(
                $"Image {index} is a {depth} bit bitmap; only 24 and 32 bit bitmaps are supported.");
        }

        if (bitCount != 0 && bitCount != depth)
        {
            throw new BinaryFormatException(
                $"Image {index} declares {bitCount} bits per pixel but its bitmap uses {depth}.");
        }

        int bytesPerPixel = depth / 8;
        int colorStride = ((bitmapWidth * bytesPerPixel) + 3) & ~3;
        int maskStride = ((bitmapWidth + 31) / 32) * 4;
        long needed = BitmapInfoHeaderSize + ((long)colorStride * height) + ((long)maskStride * height);

        if (payload.Length < needed)
        {
            throw new BinaryFormatException(
                $"Image {index} needs {needed} bytes ({bitmapWidth}x{bitmapHeight} at {depth} bpp plus the mask) " +
                $"but its payload is {payload.Length} bytes long.");
        }

        ReadOnlySpan<byte> colors = payload[BitmapInfoHeaderSize..];
        ReadOnlySpan<byte> mask = payload[(BitmapInfoHeaderSize + (colorStride * height))..];

        // A 32 bit bitmap whose alpha channel is entirely zero predates alpha icons: its
        // transparency lives in the AND mask only, so the channel has to be ignored.
        bool useAlphaChannel = depth == 32 && HasAnyAlpha(colors, bitmapWidth, height, colorStride);

        byte[] rgba = new byte[width * height * 4];
        for (int y = 0; y < height; ++y)
        {
            int sourceRow = (height - 1 - y) * colorStride;
            int maskRow = (height - 1 - y) * maskStride;
            int targetRow = y * width * 4;

            for (int x = 0; x < width; ++x)
            {
                int source = sourceRow + (x * bytesPerPixel);
                int target = targetRow + (x * 4);
                bool transparent = (mask[maskRow + (x / 8)] & (0x80 >> (x % 8))) != 0;

                rgba[target] = colors[source + 2];
                rgba[target + 1] = colors[source + 1];
                rgba[target + 2] = colors[source];
                rgba[target + 3] = useAlphaChannel ? colors[source + 3] : transparent ? (byte)0 : (byte)255;
            }
        }

        return new PngImage(width, height, rgba);
    }

    private static bool HasAnyAlpha(ReadOnlySpan<byte> colors, int width, int height, int colorStride)
    {
        for (int y = 0; y < height; ++y)
        {
            ReadOnlySpan<byte> row = colors.Slice(y * colorStride, width * 4);
            for (int x = 3; x < row.Length; x += 4)
            {
                if (row[x] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
