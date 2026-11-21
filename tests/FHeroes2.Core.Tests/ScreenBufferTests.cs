using FHeroes2.Core.Imaging;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <c>Display::changePalette()</c> (<c>src/engine/screen.cpp:1622-1630</c>) on the frame buffer:
/// swap the palette the indices are drawn with and make sure the whole frame is re-uploaded.
/// </summary>
public sealed class ScreenBufferTests
{
    /// <summary>A new palette becomes the one the frame is drawn with and dirties every pixel.</summary>
    [Fact]
    public void ChangePalette_SwapsThePaletteAndInvalidatesTheFrame()
    {
        Palette gamePalette = BuildPalette(1);
        Palette videoPalette = BuildPalette(2);

        ScreenBuffer frame = new(new FHeroes2.Core.Geometry.Size(4, 4), gamePalette);
        Assert.Same(gamePalette, frame.Palette);

        frame.TakeDirtyRegion();
        frame.ChangePalette(videoPalette);

        Assert.Same(videoPalette, frame.Palette);
        Assert.Equal(frame.Bounds, frame.DirtyRegion);
    }

    /// <summary>The reference returns early when the very same palette pointer arrives again.</summary>
    [Fact]
    public void ChangePalette_WithTheSameInstanceChangesNothing()
    {
        Palette gamePalette = BuildPalette(1);
        ScreenBuffer frame = new(new FHeroes2.Core.Geometry.Size(4, 4), gamePalette);
        frame.TakeDirtyRegion();

        frame.ChangePalette(gamePalette);

        Assert.Same(gamePalette, frame.Palette);
        Assert.False(frame.HasDirtyRegion);
    }

    /// <summary>A palette where entry <paramref name="brightness"/> is the only non black colour.</summary>
    private static Palette BuildPalette(byte brightness)
    {
        byte[] raw = new byte[PaletteTable.Size * 3];
        raw[0] = brightness;

        return new Palette(raw);
    }
}
