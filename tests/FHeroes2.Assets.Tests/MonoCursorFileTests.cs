using FHeroes2.Assets.Cursor;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for the monochrome cursor decoder (<c>fheroes2::decodeBMPFile()</c>). The fixtures are
/// assembled by hand because these <c>.BMP</c> entries are not Windows bitmaps: only the pixel
/// codes 1 (white), 2 (black) and everything else (transparent) matter.
/// </summary>
public sealed class MonoCursorFileTests
{
    [Fact]
    public void Parse_MapsThePixelCodesToPaletteIndices()
    {
        // 4x2 cursor, black index 33.
        byte[] data = [33, 0, 4, 0, 2, 0, 1, 2, 0, 0, 0, 1, 1, 0];

        Sprite cursor = MonoCursorFile.Parse(data);

        Assert.Equal(4, cursor.Width);
        Assert.Equal(2, cursor.Height);
        Assert.Equal(0, cursor.OffsetX);
        Assert.Equal(0, cursor.OffsetY);
        Assert.Equal(
            [MonoCursorFile.WhitePaletteIndex, 33, 0, 0, 0, MonoCursorFile.WhitePaletteIndex, MonoCursorFile.WhitePaletteIndex, 0],
            cursor.Image.Pixels);
    }

    [Fact]
    public void Parse_MarksEverythingButWhiteAndBlackAsTransparent()
    {
        byte[] data = [17, 0, 3, 0, 1, 0, 1, 2, 7];

        Sprite cursor = MonoCursorFile.Parse(data);

        Assert.True(cursor.HasTransformLayer);
        Assert.Equal(new byte[] { 0, 0, 1 }, cursor.Transform);
    }

    [Fact]
    public void Parse_UsesTheBlackIndexFromTheHeader()
    {
        byte[] data = [200, 0, 1, 0, 1, 0, 2];

        Sprite cursor = MonoCursorFile.Parse(data);

        Assert.Equal(200, cursor.Image.Pixels[0]);
    }

    [Fact]
    public void Parse_RejectsATruncatedHeader()
    {
        Assert.Throws<BinaryFormatException>(() => MonoCursorFile.Parse(new byte[5]));
    }

    [Fact]
    public void Parse_RejectsAZeroSizedCursor()
    {
        byte[] data = new byte[MonoCursorFile.HeaderSize];

        Assert.Throws<BinaryFormatException>(() => MonoCursorFile.Parse(data));
    }

    [Fact]
    public void Parse_RejectsAMismatchedPayload()
    {
        // The header claims 4x2 pixels, i.e. 8 codes, but only 7 bytes follow.
        byte[] data = [33, 0, 4, 0, 2, 0, 1, 2, 0, 0, 0, 1, 1];

        BinaryFormatException error = Assert.Throws<BinaryFormatException>(() => MonoCursorFile.Parse(data));

        Assert.Contains("should be 14 bytes", error.Message);
    }
}
