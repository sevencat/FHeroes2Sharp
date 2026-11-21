using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="GameBorder"/>, the frame of the map screens (<c>gui/interface_border.cpp:70-418</c>).
/// </summary>
public sealed class GameBorderTests
{
    [Fact]
    public void Redraw_ReproducesTheArtworkBorder_OnTheOriginalScreenSize()
    {
        if (LoadGoodArtwork() is not { } artwork)
        {
            return;
        }

        PixelBuffer canvas = new(640, 480);

        GameBorder.Redraw(canvas, artwork, evilInterface: false, hideInterface: false, viewWorldMode: false);

        // Every edge of the 640x480 artwork is the border itself, so the four strips have to match it.
        AssertStripsMatchArtwork(canvas, artwork, 640, 480);

        // The map, the radar and the panel keep whatever the caller painted there beforehand.
        Assert.All(Enumerable.Range(0, 64), y => Assert.Equal((byte)0, canvas[200, 16 + y]));
    }

    [Fact]
    public void Redraw_LeavesTheScreenAlone_WhenTheInterfaceIsHidden()
    {
        if (LoadGoodArtwork() is not { } artwork)
        {
            return;
        }

        PixelBuffer canvas = new(640, 480);

        GameBorder.Redraw(canvas, artwork, evilInterface: false, hideInterface: true, viewWorldMode: false);

        Assert.All(canvas.Pixels, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Redraw_KeepsTheRightEdgeFlush_OnALargerScreen()
    {
        if (LoadGoodArtwork() is not { } artwork)
        {
            return;
        }

        PixelBuffer canvas = new(672, 512);

        GameBorder.Redraw(canvas, artwork, evilInterface: false, hideInterface: false, viewWorldMode: false);

        // The last quiet piece of every run is glued to the far edge, so the outside corner pixels of the
        // artwork land on the outside corner pixels of the bigger screen.
        Assert.Equal(artwork.Image[0, 0], canvas[0, 0]);
        Assert.Equal(artwork.Image[639, 0], canvas[671, 0]);
        Assert.Equal(artwork.Image[0, 479], canvas[0, 511]);
        Assert.Equal(artwork.Image[639, 479], canvas[671, 511]);

        // The repeated middle is not the artwork at the same coordinate any more, but the inner map area
        // still has to be untouched.
        Assert.Equal((byte)0, canvas[300, 300]);
    }

    [Fact]
    public void Redraw_DrawsTheViewWorldPatchOverTheEmbranchment()
    {
        if (LoadGoodArtwork() is not { } artwork)
        {
            return;
        }

        PixelBuffer plain = new(640, 480);
        PixelBuffer viewWorld = new(640, 480);

        GameBorder.Redraw(plain, artwork, evilInterface: false, hideInterface: true, viewWorldMode: false);
        GameBorder.Redraw(viewWorld, artwork, evilInterface: false, hideInterface: true, viewWorldMode: true);

        // The «no interface» mode is ignored in the view world, and that mode patches the branch.
        Assert.All(plain.Pixels, value => Assert.Equal((byte)0, value));
        Assert.NotEqual((byte)0, viewWorld[0, 0]);
    }

    [Fact]
    public void RepeatPattern_EndsFlushWithTheRequestedSize()
    {
        PixelBuffer pattern = CreateCoordinateBuffer(4, 3);
        PixelBuffer target = new(10, 7);

        GameBorder.RepeatPattern(pattern, 0, 0, 4, 3, target, 0, 0, 10, 7);

        Assert.Equal(pattern[0, 0], target[0, 0]);
        Assert.Equal(pattern[3, 2], target[3, 2]);

        // The remainder is taken from the left of the pattern again and glued to the far edge.
        Assert.Equal(pattern[1, 0], target[9, 0]);
        Assert.Equal(pattern[1, 2], target[9, 5]);
        Assert.Equal(pattern[1, 0], target[9, 6]);
    }

    [Fact]
    public void RepeatPattern_IgnoresEmptyRequests()
    {
        PixelBuffer pattern = CreateCoordinateBuffer(4, 3);
        PixelBuffer target = new(10, 7);

        GameBorder.RepeatPattern(pattern, 0, 0, 4, 3, target, 0, 0, 0, 7);
        GameBorder.RepeatPattern(pattern, -1, 0, 4, 3, target, 0, 0, 10, 7);

        Assert.All(target.Pixels, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void TryLoad_ReturnsNothingWithoutAnArchive()
    {
        Assert.Null(GameBorder.TryLoad(null, evilInterface: false));
    }

    private static void AssertStripsMatchArtwork(PixelBuffer canvas, Sprite artwork, int width, int height)
    {
        for (int y = 0; y < GameBorder.BorderWidth; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                // The top and bottom runs are cut from the artwork and must land identically.
                Assert.Equal(artwork.Image[x < artwork.Width ? x : 0, y], canvas[x, y]);
            }
        }

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < GameBorder.BorderWidth; ++x)
            {
                byte expected = y < artwork.Height ? artwork.Image[x, y] : (byte)0;
                Assert.Equal(expected, canvas[x, y]);
            }
        }
    }

    private static PixelBuffer CreateCoordinateBuffer(int width, int height)
    {
        PixelBuffer buffer = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                buffer[x, y] = (byte)((y * width) + x + 1);
            }
        }

        return buffer;
    }

    private static Sprite? LoadGoodArtwork()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return null;
        }

        using AssetBundle? bundle = AssetBundle.Open(directory);

        return bundle is null ? null : GameBorder.TryLoad(bundle, evilInterface: false);
    }
}
