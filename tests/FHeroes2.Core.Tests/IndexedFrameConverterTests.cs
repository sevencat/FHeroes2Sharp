using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// The presenter uploads only the dirty rectangle, so the conversion from palette indices to packed
/// pixels has to agree with the SDL texture format pixel by pixel. These tests pin the byte order
/// (<c>SDL_PIXELFORMAT_ABGR8888</c>) and the region handling.
/// </summary>
public sealed class IndexedFrameConverterTests
{
    [Fact]
    public void BuildLookup_PacksPaletteEntriesAsAbgr()
    {
        Palette palette = new();
        palette[7] = new RgbaColor(0x11, 0x22, 0x33);

        uint[] lookup = IndexedFrameConverter.BuildLookup(palette);

        Assert.Equal(Palette.ColorCount, lookup.Length);

        // 0xAABBGGRR: the little endian memory layout is R, G, B, A = 11 22 33 FF.
        Assert.Equal(0xFF332211u, lookup[7]);
        Assert.Equal(0xFF000000u, lookup[0]);
        Assert.Equal(0xFFFFFFFFu, lookup[255]);
    }

    [Fact]
    public void BuildLookup_KeepsTheAlphaChannel()
    {
        Palette palette = new();
        palette[3] = new RgbaColor(0x40, 0x50, 0x60, 0x00);

        uint[] lookup = IndexedFrameConverter.BuildLookup(palette);

        Assert.Equal(0x00605040u, lookup[3]);
    }

    [Fact]
    public void ConvertRegion_CopiesOnlyTheRequestedRectangle()
    {
        ScreenBuffer frame = CreateFrame(4, 3);
        uint[] lookup = IndexedFrameConverter.BuildLookup(frame.Palette);
        uint[] destination = new uint[4];

        Rect converted = IndexedFrameConverter.ConvertRegion(frame, new Rect(1, 1, 2, 2), lookup, destination);

        Assert.Equal(new Rect(1, 1, 2, 2), converted);
        Assert.Equal(lookup[5], destination[0]);
        Assert.Equal(lookup[6], destination[1]);
        Assert.Equal(lookup[9], destination[2]);
        Assert.Equal(lookup[10], destination[3]);
    }

    [Fact]
    public void ConvertRegion_ClipsRegionsThatLeaveTheFrame()
    {
        ScreenBuffer frame = CreateFrame(4, 3);
        uint[] lookup = IndexedFrameConverter.BuildLookup(frame.Palette);
        uint[] destination = new uint[4];

        Rect converted = IndexedFrameConverter.ConvertRegion(frame, new Rect(3, 2, 5, 5), lookup, destination);

        Assert.Equal(new Rect(3, 2, 1, 1), converted);
        Assert.Equal(lookup[11], destination[0]);
    }

    [Fact]
    public void ConvertRegion_SkipsEmptyRegions()
    {
        ScreenBuffer frame = CreateFrame(2, 2);
        uint[] lookup = IndexedFrameConverter.BuildLookup(frame.Palette);
        uint[] destination = new uint[1];

        Rect converted = IndexedFrameConverter.ConvertRegion(frame, new Rect(-8, -8, 4, 4), lookup, destination);

        Assert.True(converted.IsEmpty);
        Assert.Equal(0u, destination[0]);
    }

    [Fact]
    public void ConvertRegion_RejectsShortBuffers()
    {
        ScreenBuffer frame = CreateFrame(4, 4);
        uint[] lookup = IndexedFrameConverter.BuildLookup(frame.Palette);

        Assert.Throws<ArgumentException>(() =>
            IndexedFrameConverter.ConvertRegion(frame, new Rect(0, 0, 4, 4), lookup, new uint[15]));

        Assert.Throws<ArgumentException>(() =>
            IndexedFrameConverter.ConvertRegion(frame, new Rect(0, 0, 1, 1), lookup[..10], new uint[1]));
    }

    /// <summary>Frame whose index equals <c>x + (y * width)</c>, so the expected pixel is known.</summary>
    private static ScreenBuffer CreateFrame(int width, int height)
    {
        ScreenBuffer frame = new(new Size(width, height), new Palette());
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                frame.Pixels[x, y] = (byte)(x + (y * width));
            }
        }

        return frame;
    }
}
