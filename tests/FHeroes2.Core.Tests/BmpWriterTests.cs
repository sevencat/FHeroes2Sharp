using System.Buffers.Binary;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Structural tests for the BMP exporter. The written file is parsed back the way an image viewer
/// reads it (headers, palette, bottom up padded rows) so the tests fail if the layout ever drifts
/// away from what the reference engine's <c>SDL_SaveBMP</c> output looks like.
/// </summary>
public sealed class BmpWriterTests
{
    [Fact]
    public void EncodeIndexed_WritesAn8BitBitmapHeader()
    {
        PixelBuffer image = new(2, 3);

        DecodedBmp bmp = Decode(BmpWriter.EncodeIndexed(image, new Palette()));

        Assert.Equal(2, bmp.Width);
        Assert.Equal(3, bmp.Height);
        Assert.Equal(8, bmp.BitsPerPixel);
        Assert.Equal(BmpWriter.PixelDataOffset, bmp.PixelDataOffset);
        Assert.Equal(2 * 3, bmp.Pixels.Length);
    }

    [Fact]
    public void EncodeIndexed_StoresThePaletteInBlueGreenRedOrder()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];
        raw[5 * 3] = 1;
        raw[(5 * 3) + 1] = 2;
        raw[(5 * 3) + 2] = 3;

        byte[] bmp = BmpWriter.EncodeIndexed(new PixelBuffer(1, 1), new Palette(raw));

        int entry = BmpWriter.FileHeaderSize + BmpWriter.InfoHeaderSize + (5 * BmpWriter.PaletteEntrySize);
        Assert.Equal(3, bmp[entry]); // Blue.
        Assert.Equal(2, bmp[entry + 1]); // Green.
        Assert.Equal(1, bmp[entry + 2]); // Red.
        Assert.Equal(0, bmp[entry + 3]); // Reserved.
    }

    [Fact]
    public void EncodeIndexed_StoresTheRowsBottomUp()
    {
        PixelBuffer image = new(3, 2);
        image.Pixels[0] = 1;
        image.Pixels[1] = 1;
        image.Pixels[2] = 1;
        image.Pixels[3] = 2;
        image.Pixels[4] = 2;
        image.Pixels[5] = 2;

        byte[] bmp = BmpWriter.EncodeIndexed(image, new Palette());

        // The first stored row is the bottom row of the image, padded to four bytes.
        Assert.Equal(new byte[] { 2, 2, 2, 0 }, bmp[BmpWriter.PixelDataOffset..(BmpWriter.PixelDataOffset + 4)]);
        Assert.Equal(new byte[] { 1, 1, 1, 0 }, bmp[(BmpWriter.PixelDataOffset + 4)..(BmpWriter.PixelDataOffset + 8)]);

        DecodedBmp decoded = Decode(bmp);

        Assert.Equal([1, 1, 1, 2, 2, 2], decoded.Pixels);
        Assert.Equal(new byte[] { 1, 1, 1 }, decoded.TopRow);
    }

    [Fact]
    public void EncodeIndexed_PadsEveryRowToFourBytes()
    {
        PixelBuffer image = new(3, 2);

        byte[] bmp = BmpWriter.EncodeIndexed(image, new Palette());

        // 3 pixels + 1 padding byte per row, two rows, no trailing data.
        Assert.Equal(BmpWriter.PixelDataOffset + 8, bmp.Length);
        Assert.Equal(0, bmp[^1]);
    }

    [Fact]
    public void EncodeSprite_ComposesTheSpriteOverTheBackground()
    {
        PixelBuffer image = new(2, 1);
        image.Pixels[0] = 10;
        image.Pixels[1] = 20;

        Sprite sprite = new(image, transform: [0, 1]);

        DecodedBmp bmp = Decode(BmpWriter.EncodeSprite(sprite, new Palette(), background: 23));

        Assert.Equal([10, 23], bmp.Pixels);
    }

    private static DecodedBmp Decode(byte[] bmp)
    {
        Assert.Equal((byte)'B', bmp[0]);
        Assert.Equal((byte)'M', bmp[1]);
        Assert.Equal(bmp.Length, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(2)));

        int pixelDataOffset = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(10));
        Assert.Equal(BmpWriter.InfoHeaderSize, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(14)));

        int width = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(18));
        int height = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(22));
        short planes = BinaryPrimitives.ReadInt16LittleEndian(bmp.AsSpan(26));
        int bitsPerPixel = BinaryPrimitives.ReadInt16LittleEndian(bmp.AsSpan(28));
        int compression = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(30));
        int imageSize = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(34));
        int paletteEntries = BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(46));

        Assert.Equal(1, (int)planes);
        Assert.Equal(0, compression);
        Assert.Equal(Palette.ColorCount, paletteEntries);
        Assert.Equal(bmp.Length - pixelDataOffset, imageSize);

        int rowStride = (width + 3) & ~3;
        Assert.Equal(rowStride * height, imageSize);

        byte[] pixels = new byte[width * height];
        for (int y = 0; y < height; ++y)
        {
            // The first row in the file is the last row of the image.
            int source = pixelDataOffset + ((height - 1 - y) * rowStride);
            bmp.AsSpan(source, width).CopyTo(pixels.AsSpan(y * width));
        }

        return new DecodedBmp(width, height, bitsPerPixel, pixelDataOffset, pixels);
    }

    private sealed record DecodedBmp(int Width, int Height, int BitsPerPixel, int PixelDataOffset, byte[] Pixels)
    {
        /// <summary>The first image row, i.e. the top row as a viewer shows it.</summary>
        public byte[] TopRow => Pixels[..Width];
    }
}
