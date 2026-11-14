using System.Buffers.Binary;
using System.IO.Compression;
using FHeroes2.Core.Io;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Minimal PNG writer for indexed images. It exists so that decoded assets can be exported and
/// compared against the reference engine without pulling a third party imaging dependency into
/// the game assembly, and so that golden image tests can inspect real pixels.
/// </summary>
/// <remarks>
/// Only the 8 bit RGBA flavor (color type 6, filter 0, no interlacing) is produced. Sprites are
/// written with a fully transparent background: every pixel whose transform layer entry is
/// <c>1</c> (skip) becomes alpha 0, which keeps the exported PNG visually identical to what the
/// engine renders.
/// </remarks>
public static class PngWriter
{
    private const int BitDepth = 8;
    private const int ColorTypeRgba = 6;
    private const int BytesPerPixel = 4;

    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Encodes <paramref name="image"/> into a PNG file held in memory.</summary>
    public static byte[] EncodeRgba(PixelBuffer image, Palette palette, ReadOnlySpan<byte> transformLayer = default)
    {
        using MemoryStream stream = new();
        WriteRgba(stream, image, palette, transformLayer);
        return stream.ToArray();
    }

    /// <summary>Writes <paramref name="image"/> into <paramref name="stream"/> as a PNG file.</summary>
    /// <param name="transformLayer">
    /// Optional transform layer of the same size as the image. Entries equal to <c>1</c> are written
    /// as transparent pixels, every other entry makes the pixel opaque.
    /// </param>
    public static void WriteRgba(Stream stream, PixelBuffer image, Palette palette, ReadOnlySpan<byte> transformLayer = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(palette);

        int width = image.Width;
        int height = image.Height;

        if (!transformLayer.IsEmpty && transformLayer.Length != width * height)
        {
            throw new ArgumentException(
                $"A transform layer needs {width * height} bytes but {transformLayer.Length} were provided.",
                nameof(transformLayer));
        }

        int stride = (width * BytesPerPixel) + 1;
        byte[] raw = new byte[checked(stride * height)];

        for (int y = 0; y < height; ++y)
        {
            int rowStart = y * stride;
            raw[rowStart] = 0; // Filter type: none.

            for (int x = 0; x < width; ++x)
            {
                int sourceIndex = (y * image.Stride) + x;
                RgbaColor color = palette[image.Pixels[sourceIndex]];
                int target = rowStart + 1 + (x * BytesPerPixel);

                raw[target] = color.R;
                raw[target + 1] = color.G;
                raw[target + 2] = color.B;
                raw[target + 3] = !transformLayer.IsEmpty && transformLayer[sourceIndex] == 1 ? (byte)0 : (byte)255;
            }
        }

        WriteScanlines(stream, width, height, raw);
    }

    /// <summary>Encodes an already decoded RGBA image, for example a diff image, into a PNG file.</summary>
    public static byte[] Encode(PngImage image)
    {
        using MemoryStream stream = new();
        WriteRgba(stream, image);
        return stream.ToArray();
    }

    /// <summary>Writes an RGBA image, keeping its alpha channel as is.</summary>
    public static void WriteRgba(Stream stream, PngImage image)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(image);

        int stride = (image.Width * BytesPerPixel) + 1;
        byte[] raw = new byte[checked(stride * image.Height)];

        for (int y = 0; y < image.Height; ++y)
        {
            int rowStart = y * stride;
            raw[rowStart] = 0; // Filter type: none.
            image.Row(y).CopyTo(raw.AsSpan(rowStart + 1, image.Stride));
        }

        WriteScanlines(stream, image.Width, image.Height, raw);
    }

    /// <summary>Writes the signature, the header, the deflated scanlines and the end chunk.</summary>
    private static void WriteScanlines(Stream stream, int width, int height, byte[] raw)
    {
        stream.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = BitDepth;
        header[9] = ColorTypeRgba;
        header[10] = 0; // Deflate compression.
        header[11] = 0; // Adaptive filtering.
        header[12] = 0; // No interlacing.
        WriteChunk(stream, "IHDR"u8, header);

        using MemoryStream compressed = new();
        using (ZLibStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        WriteChunk(stream, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(stream, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, Crc32.Compute(type, data));
        stream.Write(checksum);
    }
}
