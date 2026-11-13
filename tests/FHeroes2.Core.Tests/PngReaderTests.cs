using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests for the PNG decoder. The fixtures that are not produced by <see cref="PngWriter"/> were
/// written once with Pillow 13 (color types 0/2/4) and hand assembled from raw zlib data (indexed
/// 2 bit with <c>tRNS</c>, 16 bit grayscale, an interlaced header), so the decoder is checked
/// against files it could not have written itself.
/// </summary>
public sealed class PngReaderTests
{
    /// <summary>4x2 RGB (color type 2), Pillow 13.</summary>
    private const string RgbPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAIAAADwyuo0AAAAF0lEQVR4nGP4z8DAAMb//zMwMjIxwwEAXpAFHjpsJLYAAAAA" +
        "SUVORK5CYII=";

    /// <summary>3x2 grayscale (color type 0), Pillow 13.</summary>
    private const string GrayscalePng =
        "iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAAAAAC4HznGAAAAEElEQVR4nGNkCA1l+q89DQAHqAJu+utfBwAAAABJRU5ErkJg" +
        "gg==";

    /// <summary>2x2 grayscale + alpha (color type 4), Pillow 13.</summary>
    private const string GrayscaleAlphaPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAQAAADYv8WvAAAAEklEQVR4nGPk+s/VyCDnoMEAAA06AhzPXEOYAAAAAElFTkSuQmCC";

    /// <summary>
    /// 4x4 RGB file whose header declares Adam7 interlacing, assembled by hand: the decoder must
    /// refuse it before it touches the pixel data.
    /// </summary>
    private const string InterlacedPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAFRlDm/AAAADElEQVR4nGNgIB0AAAA0AAF2Xq7DAAAAAElFTkSuQmCC";

    /// <summary>
    /// 4x2 indexed image with a 2 bit depth, a four entry palette, <c>tRNS</c> alpha and the filter
    /// types 1 (sub) and 4 (Paeth), assembled by hand from raw zlib data.
    /// </summary>
    private const string IndexedPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACAgMAAAACxpXwAAAADFBMVEX/AAAA/wAAAP/////7AGD2AAAABHRSTlP/gP8AH4eGmQAAAA" +
        "xJREFUeNpjlGY5CQABKgDq6Wq6OwAAAABJRU5ErkJggg==";

    /// <summary>2x2 16 bit grayscale with the filter types 0 (none) and 2 (up), assembled by hand.</summary>
    private const string Grayscale16Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACEAAAAAAHTY67AAAAEklEQVR42mMQMvn/n+nd6TXnABeIBXqC/F3HAAAAAElFTkSuQmCC";

    [Fact]
    public void Decode_RoundTripsTheWriterOutput()
    {
        PixelBuffer image = new(3, 2);
        for (int i = 0; i < image.Pixels.Length; ++i)
        {
            image.Pixels[i] = (byte)(i * 37);
        }

        byte[] transform = [0, 1, 0, 0, 0, 1];
        Palette palette = new();

        PngImage decoded = PngReader.Decode(PngWriter.EncodeRgba(image, palette, transform));

        Assert.Equal(3, decoded.Width);
        Assert.Equal(2, decoded.Height);

        for (int y = 0; y < image.Height; ++y)
        {
            for (int x = 0; x < image.Width; ++x)
            {
                RgbaColor expected = palette[image[x, y]];
                byte alpha = transform[(y * image.Width) + x] == 1 ? (byte)0 : (byte)255;

                Assert.Equal(new RgbaColor(expected.R, expected.G, expected.B, alpha), decoded[x, y]);
            }
        }
    }

    [Fact]
    public void Decode_ReadsRgbPixels()
    {
        PngImage decoded = Decode(RgbPng);

        Assert.Equal(4, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal(RgbaColor.FromRgb(255, 0, 0), decoded[0, 0]);
        Assert.Equal(RgbaColor.FromRgb(0, 255, 0), decoded[1, 0]);
        Assert.Equal(RgbaColor.FromRgb(0, 0, 255), decoded[2, 0]);
        Assert.Equal(RgbaColor.FromRgb(255, 255, 0), decoded[3, 0]);
        Assert.Equal(RgbaColor.FromRgb(1, 2, 3), decoded[0, 1]);
        Assert.Equal(RgbaColor.FromRgb(10, 11, 12), decoded[3, 1]);
    }

    [Fact]
    public void Decode_ReadsGrayscalePixels()
    {
        PngImage decoded = Decode(GrayscalePng);

        Assert.Equal(3, decoded.Width);
        Assert.Equal(new RgbaColor(0, 0, 0), decoded[0, 0]);
        Assert.Equal(new RgbaColor(85, 85, 85), decoded[1, 0]);
        Assert.Equal(new RgbaColor(170, 170, 170), decoded[2, 0]);
        Assert.Equal(new RgbaColor(255, 255, 255), decoded[0, 1]);
        Assert.Equal(new RgbaColor(128, 128, 128), decoded[1, 1]);
        Assert.Equal(new RgbaColor(64, 64, 64), decoded[2, 1]);
    }

    [Fact]
    public void Decode_ReadsGrayscaleAlphaPixels()
    {
        PngImage decoded = Decode(GrayscaleAlphaPng);

        Assert.Equal(new RgbaColor(10, 10, 10, 255), decoded[0, 0]);
        Assert.Equal(new RgbaColor(20, 20, 20, 128), decoded[1, 0]);
        Assert.Equal(new RgbaColor(30, 30, 30, 64), decoded[0, 1]);
        Assert.Equal(new RgbaColor(40, 40, 40, 0), decoded[1, 1]);
    }

    [Fact]
    public void Decode_ReadsIndexedPixelsWithTransparency()
    {
        PngImage decoded = Decode(IndexedPng);

        Assert.Equal(new RgbaColor(255, 0, 0, 255), decoded[0, 0]);
        Assert.Equal(new RgbaColor(0, 255, 0, 128), decoded[1, 0]);
        Assert.Equal(new RgbaColor(0, 0, 255, 255), decoded[2, 0]);
        Assert.Equal(new RgbaColor(255, 255, 255, 0), decoded[3, 0]);
        Assert.Equal(new RgbaColor(255, 255, 255, 0), decoded[0, 1]);
        Assert.Equal(new RgbaColor(0, 0, 255, 255), decoded[1, 1]);
        Assert.Equal(new RgbaColor(0, 255, 0, 128), decoded[2, 1]);
        Assert.Equal(new RgbaColor(255, 0, 0, 255), decoded[3, 1]);
    }

    [Fact]
    public void Decode_RejectsInterlacedFiles()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Decode(InterlacedPng));

        Assert.Contains("interlaced", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_RejectsACorruptedChunk()
    {
        byte[] png = PngWriter.EncodeRgba(new PixelBuffer(2, 2), new Palette());
        png[^20] ^= 0xFF;

        Assert.Throws<BinaryFormatException>(() => PngReader.Decode(png));
    }

    [Fact]
    public void Decode_RejectsDataWithoutThePngSignature() =>
        Assert.Throws<BinaryFormatException>(() => PngReader.Decode("not a png file"u8));

    [Fact]
    public void Decode_RejectsATruncatedFile()
    {
        byte[] png = PngWriter.EncodeRgba(new PixelBuffer(2, 2), new Palette());

        Assert.Throws<BinaryFormatException>(() => PngReader.Decode(png.AsSpan(0, png.Length - 30)));
    }

    [Fact]
    public void Decode_ReadsFromAStream()
    {
        byte[] png = PngWriter.EncodeRgba(new PixelBuffer(2, 1), new Palette());
        using MemoryStream stream = new(png);

        Assert.Equal(2, PngReader.Decode(stream).Width);
    }

    [Fact]
    public void ToIndices_RestoresThePaletteIndices()
    {
        Palette palette = new();
        PixelBuffer source = new(4, 2);
        for (int i = 0; i < source.Pixels.Length; ++i)
        {
            source.Pixels[i] = (byte)(17 * i);
        }

        byte[] transform = [0, 0, 1, 0, 0, 1, 0, 0];
        PngImage decoded = PngReader.Decode(PngWriter.EncodeRgba(source, palette, transform));

        PixelBuffer restored = new(4, 2);
        int unmapped = decoded.ToIndices(palette, restored);

        Assert.Equal(0, unmapped);
        for (int y = 0; y < source.Height; ++y)
        {
            for (int x = 0; x < source.Width; ++x)
            {
                byte expected = transform[(y * source.Width) + x] == 1 ? (byte)0 : source[x, y];
                Assert.Equal(expected, restored[x, y]);
            }
        }
    }

    private static PngImage Decode(string base64) => PngReader.Decode(Convert.FromBase64String(base64));
}
