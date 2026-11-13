using System.Buffers.Binary;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Cursor;

/// <summary>
/// A monochrome cursor stored in a <c>.BMP</c> entry of the game archive, the port of
/// <c>fheroes2::decodeBMPFile()</c> (<c>src/engine/image_tool.cpp</c>).
/// </summary>
/// <remarks>
/// These entries are <b>not</b> Windows bitmaps, as the reference source warns. The layout is a
/// 6 byte header followed by one code per pixel, row major:
/// <code>
/// uint8                           palette index used for the "black" pixels
/// uint8                           unused (always 0 in the original data)
/// uint16                          width
/// uint16                          height
/// width × height × uint8          pixel codes: 1 = white, 2 = black, anything else = transparent
/// </code>
/// White is the fixed palette index <see cref="WhitePaletteIndex"/>, black is the index taken from
/// the header, and transparency is expressed through the transform layer (the pixel is skipped when
/// the sprite is drawn), so the decoded sprite always carries a transform layer.
/// The known sets are <c>ADVMBWnn.BMP</c> (adventure map), <c>CMSEBWnn.BMP</c> (combat) and
/// <c>SPELBWnn.BMP</c> (spells), each entry being one 32×32 cursor of the theme selected through
/// <c>FHeroes2.Core.Imaging.CursorTheme</c>.
/// </remarks>
public static class MonoCursorFile
{
    /// <summary>Size of the file header: black index, an unused byte, width and height.</summary>
    public const int HeaderSize = 6;

    /// <summary>Palette index the "white" pixels of every monochrome cursor use.</summary>
    public const byte WhitePaletteIndex = 11;

    /// <summary>Pixel code of a white (opaque) pixel.</summary>
    public const byte WhitePixelCode = 1;

    /// <summary>Pixel code of a black (opaque) pixel.</summary>
    public const byte BlackPixelCode = 2;

    /// <summary>Decodes a monochrome cursor entry.</summary>
    /// <exception cref="BinaryFormatException">The entry is not a valid monochrome cursor.</exception>
    public static Sprite Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new BinaryFormatException(
                $"A monochrome cursor needs at least {HeaderSize} bytes but only {data.Length} were provided.");
        }

        byte blackIndex = data[0];
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);

        if (width < 1 || height < 1)
        {
            throw new BinaryFormatException(
                $"The monochrome cursor header is invalid: {width}x{height} pixels.");
        }

        long expectedSize = HeaderSize + ((long)width * height);
        if (expectedSize != data.Length)
        {
            throw new BinaryFormatException(
                $"The monochrome cursor should be {expectedSize} bytes ({width}x{height}) but is {data.Length} bytes.");
        }

        PixelBuffer image = new(width, height);
        byte[] transform = new byte[width * height];
        ReadOnlySpan<byte> codes = data[HeaderSize..];

        for (int index = 0; index < codes.Length; ++index)
        {
            switch (codes[index])
            {
                case WhitePixelCode:
                    image.Pixels[index] = WhitePaletteIndex;
                    break;

                case BlackPixelCode:
                    image.Pixels[index] = blackIndex;
                    break;

                default:
                    // Not a cursor pixel: keep the image layer at 0 and make the engine skip it.
                    transform[index] = 1;
                    break;
            }
        }

        return new Sprite(image, 0, 0, transform);
    }
}
