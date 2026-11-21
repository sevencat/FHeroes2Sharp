using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The run time assembly of the <c>EMPTY_*_BUTTON</c> tables
/// (<c>generateDefaultImages()</c>, <c>game_assets.cpp:4852-4976</c>) against the real archives.
/// </summary>
public sealed class EmptyButtonsTests
{
    [Fact]
    public void GoodButtonIsCutOutOfTheSystemFrames()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Assert.Equal(2, Assets.GetImageCount(EmptyButtons.GoodFileName));

            Sprite? released = Assets.GetImage(EmptyButtons.GoodFileName, 0);
            Sprite? pressed = Assets.GetImage(EmptyButtons.GoodFileName, 1);
            Sprite? source = Assets.GetImage(IcnCatalog.GetFileName(IcnType.System), 11);

            Assert.NotNull(released);
            Assert.NotNull(pressed);
            Assert.NotNull(source);

            // The released state is SYSTEM frame 11 itself, the pressed one is rebuilt at the size of frame 12.
            Assert.Equal(source!.Width, released!.Width);
            Assert.Equal(source.Height, released.Height);

            // The two pixels the reference repaints are the button background, not the archived artwork.
            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: true), released.Image[8, 7]);
            Assert.NotEqual(source.Image[8, 7], released.Image[8, 7]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void PressedGoodButtonLeavesItsCornersTransparent()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Assert.NotNull(Assets.GetImage(EmptyButtons.GoodFileName, 0));

            Sprite released = Assets.GetImage(EmptyButtons.GoodFileName, 0)!;
            Sprite pressed = Assets.GetImage(EmptyButtons.GoodFileName, 1)!;

            Assert.True(pressed.HasTransformLayer);

            // Top row from the first copied column on, the right column and the corner steps are skipped.
            Assert.Equal(1, pressed.Transform![pressed.Width - 1]);
            Assert.Equal(1, pressed.Transform[(2 * pressed.Width) - 1]);
            Assert.Equal(1, pressed.Transform[(pressed.Height * pressed.Width) - 2]);

            // The left column is copied from the released state, transparent corner included.
            Assert.Equal(released.Transform![0], pressed.Transform[0]);
            Assert.Equal(released.Transform[5 * pressed.Width], pressed.Transform[5 * pressed.Width]);

            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: false), pressed.Image[90, 5]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void EvilButtonUsesTheEvilFillingColours()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? released = Assets.GetImage(EmptyButtons.EvilFileName, 0);
            if (released is null)
            {
                return;
            }

            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: true, isGoodInterface: false), released.Image[8, 7]);
            Assert.NotEqual(EmptyButtons.GetButtonFillingColor(isReleasedState: true), released.Image[8, 7]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void PolButtonIsOnePixelWiderAndHigherThanItsSource()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? source = Assets.GetImage(IcnCatalog.GetFileName(IcnType.XCmpbtn), 4);
            if (source is null)
            {
                return;
            }

            Sprite? released = Assets.GetImage(EmptyButtons.PolFileName, 0);
            Sprite? pressed = Assets.GetImage(EmptyButtons.PolFileName, 1);

            Assert.NotNull(released);
            Assert.NotNull(pressed);
            Assert.Equal(source.Width + 2, released!.Width);
            Assert.Equal(source.Height + 1, released.Height);
            Assert.Equal(released.Width, pressed!.Width);
            Assert.Equal(released.Height, pressed.Height);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void GuildWellButtonLosesOneColumnOfItsSource()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? source = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Wellxtra), 0);
            if (source is null)
            {
                return;
            }

            Sprite? released = Assets.GetImage(EmptyButtons.GuildWellFileName, 0);

            Assert.NotNull(released);
            Assert.Equal(source.Width - 1, released!.Width);
            Assert.Equal(source.Height, released.Height);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void FramesAreCachedSoTheTableIsAssembledOnce()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? first = Assets.GetImage(EmptyButtons.GoodFileName, 0);

            Assert.NotNull(first);
            Assert.Same(first, Assets.GetImage(EmptyButtons.GoodFileName, 0));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheFillingColoursResolveToTheNearestPaletteEntries()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            byte[]? palette = Assets.TryReadRaw("KB.PAL");
            if (palette is null)
            {
                return;
            }

            AssertColourIsCloseTo(palette, EmptyButtons.GetColorId(216, 184, 152), 216, 184, 152);
            AssertColourIsCloseTo(palette, EmptyButtons.GetColorId(184, 136, 96), 184, 136, 96);
            AssertColourIsCloseTo(palette, EmptyButtons.GetColorId(180, 180, 180), 180, 180, 180);
            AssertColourIsCloseTo(palette, EmptyButtons.GetColorId(144, 144, 144), 144, 144, 144);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    /// <summary>
    /// Asserts that the palette entry is the colour asked for, allowing for the six bit storage of
    /// <c>KB.PAL</c> and for the nearest entry being a rounded one.
    /// </summary>
    private static void AssertColourIsCloseTo(byte[] palette, byte index, byte red, byte green, byte blue)
    {
        int offset = index * 3;

        Assert.InRange(palette[offset] << 2, red - 24, red + 24);
        Assert.InRange(palette[offset + 1] << 2, green - 24, green + 24);
        Assert.InRange(palette[offset + 2] << 2, blue - 24, blue + 24);
    }
}
