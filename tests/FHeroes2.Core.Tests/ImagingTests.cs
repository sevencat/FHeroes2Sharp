using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

public sealed class ImagingTests
{
    [Fact]
    public void Palette_RoundTripsRawTable()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];
        for (int i = 0; i < 256; ++i)
        {
            raw[i * 3] = (byte)i;
            raw[(i * 3) + 1] = (byte)(255 - i);
            raw[(i * 3) + 2] = 0x42;
        }

        Palette palette = new(raw);

        Assert.Equal(new RgbaColor(0, 255, 0x42), palette[0]);
        Assert.Equal(new RgbaColor(255, 0, 0x42), palette[255]);
        Assert.Equal(raw, palette.ToRaw());
    }

    [Fact]
    public void FromGamePalette_NormalizesSixBitComponents()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];
        raw[0] = 63;
        raw[1] = 1;
        raw[2] = 0;

        Palette palette = Palette.FromGamePalette(raw);

        Assert.Equal(new RgbaColor(252, 4, 0), palette[0]);
    }

    [Fact]
    public void FromGamePalette_DuplicatesCyclingColors()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];
        for (int index = 0; index < Palette.ColorCount; ++index)
        {
            raw[index * 3] = (byte)index;
            raw[(index * 3) + 1] = (byte)index;
            raw[(index * 3) + 2] = (byte)index;
        }

        Palette palette = Palette.FromGamePalette(raw);

        // Water cycling colors.
        Assert.Equal(palette[231], palette[246]);
        Assert.Equal(palette[233], palette[248]);
        Assert.Equal(palette[235], palette[249]);

        // Lava cycling colors.
        Assert.Equal(palette[214], palette[250]);
        Assert.Equal(palette[217], palette[253]);
    }

    [Fact]
    public void Palette_RejectsShortInput()
    {
        Assert.Throws<ArgumentException>(() => new Palette(new byte[100]));
    }

    [Fact]
    public void Palette_DefaultIsGrayscaleRamp()
    {
        Palette palette = new();

        Assert.Equal(new RgbaColor(0, 0, 0), palette[0]);
        Assert.Equal(new RgbaColor(255, 255, 255), palette[255]);
    }

    [Fact]
    public void Palette_FindExtremeIndices_FollowLuminance()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];
        for (int index = 0; index < Palette.ColorCount; ++index)
        {
            raw[index * 3] = 40;
            raw[(index * 3) + 1] = 40;
            raw[(index * 3) + 2] = 40;
        }

        // A dark blue is darker than a medium grey even though the raw channel values suggest
        // otherwise; the brightest entry is the white one, not the most saturated one.
        raw[10 * 3] = 0;
        raw[(10 * 3) + 1] = 0;
        raw[(10 * 3) + 2] = 20;
        raw[200 * 3] = 63;
        raw[(200 * 3) + 1] = 63;
        raw[(200 * 3) + 2] = 63;

        Palette palette = Palette.FromGamePalette(raw);

        Assert.Equal(10, palette.FindDarkestIndex());
        Assert.Equal(200, palette.FindBrightestIndex());
    }

    [Fact]
    public void Palette_FindExtremeIndices_KeepTheLowestIndexOfATie()
    {
        Palette palette = new(rawRgb: new byte[Palette.RawPaletteSize]);

        Assert.Equal(0, palette.FindDarkestIndex());
        Assert.Equal(0, palette.FindBrightestIndex());
    }

    [Fact]
    public void RgbaColor_PacksAsAbgr8888()
    {
        RgbaColor color = new(0x11, 0x22, 0x33, 0x44);

        Assert.Equal(0x44332211u, color.ToPackedAbgr());
    }

    [Fact]
    public void PixelBuffer_IndexerWritesIntoRowMajorStorage()
    {
        PixelBuffer buffer = new(3, 2);

        buffer[1, 1] = 7;

        Assert.Equal(7, buffer.Pixels[4]);
        Assert.Equal(7, buffer[1, 1]);
        Assert.Equal(0, buffer[2, 1]);
    }

    [Fact]
    public void ImageOps_FillRect_ClipsToBounds()
    {
        PixelBuffer buffer = new(4, 4);

        ImageOps.FillRect(buffer, new Rect(-2, -2, 4, 4), 9);

        Assert.Equal(9, buffer[0, 0]);
        Assert.Equal(9, buffer[1, 1]);
        Assert.Equal(0, buffer[2, 0]);
        Assert.Equal(0, buffer[0, 2]);
    }

    [Fact]
    public void ImageOps_Blit_ClipsSourceAndDestination()
    {
        PixelBuffer source = new(2, 2);
        source[0, 0] = 1;
        source[1, 0] = 2;
        source[0, 1] = 3;
        source[1, 1] = 4;

        PixelBuffer destination = new(3, 3);
        ImageOps.Blit(destination, new Point(2, 2), source, new Rect(0, 0, 2, 2));

        Assert.Equal(1, destination[2, 2]);
        Assert.Equal(0, destination[0, 0]);
    }

    [Fact]
    public void ScreenBuffer_DirtyRegion_AccumulatesAndResets()
    {
        ScreenBuffer buffer = new(new Size(16, 16), new Palette());
        buffer.TakeDirtyRegion();

        buffer.MarkDirty(new Rect(0, 0, 4, 4));
        buffer.MarkDirty(new Rect(8, 8, 4, 4));

        Rect dirty = buffer.TakeDirtyRegion();

        Assert.Equal(new Rect(0, 0, 12, 12), dirty);
        Assert.False(buffer.HasDirtyRegion);
        Assert.True(buffer.TakeDirtyRegion().IsEmpty);
    }

    [Fact]
    public void ScreenBuffer_StartsFullyDirty()
    {
        ScreenBuffer buffer = new(new Size(8, 8), new Palette());

        Assert.True(buffer.HasDirtyRegion);
        Assert.Equal(buffer.Bounds, buffer.DirtyRegion);
    }
}
