using System.Buffers.Binary;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Minimal 8 bit indexed BMP writer, the counterpart of <c>fheroes2::Save()</c>
/// (<c>src/engine/image_tool.cpp</c>), which hands an 8 bpp surface to <c>SDL_SaveBMP</c>.
/// </summary>
/// <remarks>
/// The original engine exports indexed images, so this writer keeps the palette instead of
/// converting to true color: a 14 byte file header, a 40 byte <c>BITMAPINFOHEADER</c>, a 256 entry
/// BGRA palette and bottom up rows padded to a multiple of four bytes. That is the layout every
/// viewer expects for an 8 bpp bitmap, which is what makes the exports comparable with the
/// reference engine and with third party tools.
/// </remarks>
public static class BmpWriter
{
    /// <summary>Size of the <c>BITMAPFILEHEADER</c>.</summary>
    public const int FileHeaderSize = 14;

    /// <summary>Size of the <c>BITMAPINFOHEADER</c>.</summary>
    public const int InfoHeaderSize = 40;

    /// <summary>Size of one palette entry: blue, green, red and a reserved byte.</summary>
    public const int PaletteEntrySize = 4;

    /// <summary>Size of the table stored in the file, which always holds all 256 entries.</summary>
    public const int PaletteSize = Palette.ColorCount * PaletteEntrySize;

    /// <summary>Offset of the first pixel byte, i.e. the size of both headers plus the palette.</summary>
    public const int PixelDataOffset = FileHeaderSize + InfoHeaderSize + PaletteSize;

    private const int BitsPerPixel = 8;

    /// <summary>Encodes <paramref name="image"/> into an 8 bit indexed BMP file held in memory.</summary>
    public static byte[] EncodeIndexed(PixelBuffer image, Palette palette)
    {
        using MemoryStream stream = new();
        WriteIndexed(stream, image, palette);
        return stream.ToArray();
    }

    /// <summary>Writes <paramref name="image"/> into <paramref name="stream"/> as an indexed BMP file.</summary>
    public static void WriteIndexed(Stream stream, PixelBuffer image, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(palette);

        Write(stream, image, palette);
    }

    /// <summary>
    /// Encodes <paramref name="sprite"/> the way <c>fheroes2::Save( image, path, background )</c>
    /// does: a canvas filled with <paramref name="background"/> and the sprite blitted over it with
    /// its transform layer applied, so shadows and skipped pixels use the background.
    /// </summary>
    public static byte[] EncodeSprite(Sprite sprite, Palette palette, byte background)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        PixelBuffer canvas = new(sprite.Width, sprite.Height);
        canvas.Fill(background);
        ImageOps.BlitSprite(canvas, new Point(0, 0), sprite);

        return EncodeIndexed(canvas, palette);
    }

    /// <summary>Writes <paramref name="sprite"/> composed over a filled background.</summary>
    public static void WriteSprite(Stream stream, Sprite sprite, Palette palette, byte background)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(EncodeSprite(sprite, palette, background));
    }

    private static void Write(Stream stream, PixelBuffer image, Palette palette)
    {
        // One byte per pixel (8 bpp), padded so that every row starts on a 4 byte boundary.
        int rowStride = (image.Width + 3) & ~3;
        int imageSize = rowStride * image.Height;

        Span<byte> header = stackalloc byte[FileHeaderSize + InfoHeaderSize];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(header[2..], PixelDataOffset + imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[10..], PixelDataOffset);

        BinaryPrimitives.WriteInt32LittleEndian(header[14..], InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[18..], image.Width);
        // A positive height means the rows are stored bottom up, which is what SDL writes.
        BinaryPrimitives.WriteInt32LittleEndian(header[22..], image.Height);
        BinaryPrimitives.WriteInt16LittleEndian(header[26..], 1); // Planes.
        BinaryPrimitives.WriteInt16LittleEndian(header[28..], BitsPerPixel);
        BinaryPrimitives.WriteInt32LittleEndian(header[30..], 0); // BI_RGB, no compression.
        BinaryPrimitives.WriteInt32LittleEndian(header[34..], imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[38..], 0); // Horizontal pixels per meter.
        BinaryPrimitives.WriteInt32LittleEndian(header[42..], 0); // Vertical pixels per meter.
        BinaryPrimitives.WriteInt32LittleEndian(header[46..], Palette.ColorCount);
        BinaryPrimitives.WriteInt32LittleEndian(header[50..], 0); // Every palette entry is important.

        stream.Write(header);

        Span<byte> entry = stackalloc byte[PaletteEntrySize];
        for (int index = 0; index < Palette.ColorCount; ++index)
        {
            RgbaColor color = palette[index];
            entry[0] = color.B;
            entry[1] = color.G;
            entry[2] = color.R;
            entry[3] = 0; // Reserved, always zero.
            stream.Write(entry);
        }

        byte[] row = new byte[rowStride];
        for (int y = image.Height - 1; y >= 0; --y)
        {
            Array.Clear(row);
            image.Row(y).CopyTo(row);
            stream.Write(row);
        }
    }
}
