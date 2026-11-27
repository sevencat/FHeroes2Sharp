using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The run time assembly of the <c>EMPTY_*_BUTTON</c> tables
/// (<c>generateDefaultImages()</c>, <c>game_assets.cpp:4852-4976</c>) against the real archives.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
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
    public void VerticalGoodButtonErasesTheLabelOfItsSource()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? sourceReleased = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 2);
            Sprite? sourcePressed = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 3);
            if (sourceReleased is null || sourcePressed is null)
            {
                return;
            }

            Sprite? released = Assets.GetImage(EmptyButtons.VerticalGoodFileName, 0);
            Sprite? pressed = Assets.GetImage(EmptyButtons.VerticalGoodFileName, 1);

            Assert.NotNull(released);
            Assert.NotNull(pressed);

            // One column wider so that resizeButton() divides the width by three, the same height as the
            // source after the last seven rows are copied one row higher.
            Assert.Equal(sourceReleased.Width + 1, released!.Width);
            Assert.Equal(sourceReleased.Height, released.Height);
            Assert.Equal(sourcePressed.Width + 1, pressed!.Width);
            Assert.Equal(sourcePressed.Height, pressed.Height);

            // Both states are labelled in the source and blank in the generated table.
            Assert.True(released.HasTransformLayer);
            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: true), released.Image[6, 4]);
            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: true), released.Image[23, 30]);
            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: false), pressed.Image[5, 5]);

            // The right column of the pressed state is dropped to the mask.
            Assert.Equal(1, pressed.Transform![(3 * pressed.Width) + pressed.Width - 1]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void MapSelectButtonIsOnePixelWiderThanItsSource()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? sourceReleased = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Ngextra), 64);
            Sprite? sourcePressed = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Ngextra), 65);
            if (sourceReleased is null || sourcePressed is null)
            {
                return;
            }

            Sprite? released = Assets.GetImage(EmptyButtons.MapSelectFileName, 0);
            Sprite? pressed = Assets.GetImage(EmptyButtons.MapSelectFileName, 1);

            Assert.NotNull(released);
            Assert.NotNull(pressed);
            Assert.Equal(sourceReleased.Width + 1, released!.Width);
            Assert.Equal(sourceReleased.Height, released.Height);
            Assert.Equal(sourcePressed.Width + 1, pressed!.Width);

            // The label area of the source is painted over with the button background.
            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: true), released.Image[6, 2]);
            Assert.Equal(EmptyButtons.GetButtonFillingColor(isReleasedState: false), pressed.Image[5, 4]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void SmallExitButtonIsTrimmedOutOfTheViewArmyFrames()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? sourceReleased = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Viewarmy), 3);
            Sprite? sourcePressed = Assets.GetImage(IcnCatalog.GetFileName(IcnType.Viewarmy), 4);
            if (sourceReleased is null || sourcePressed is null)
            {
                return;
            }

            Sprite? released = Assets.GetImage(EmptyButtons.SmallExitGoodFileName, 0);
            Sprite? pressed = Assets.GetImage(EmptyButtons.SmallExitGoodFileName, 1);

            Assert.NotNull(released);
            Assert.NotNull(pressed);

            // Two columns and one row are cut off the left and the bottom, as the reference resizes them.
            Assert.Equal(sourceReleased.Width - 2, released!.Width);
            Assert.Equal(sourceReleased.Height - 1, released.Height);
            Assert.Equal(sourcePressed.Width - 2, pressed!.Width);
            Assert.Equal(sourcePressed.Height - 1, pressed.Height);

            // Both states lose their four corner steps.
            Assert.True(released.HasTransformLayer);
            Assert.True(pressed.HasTransformLayer);

            Assert.Equal(1, released.Transform![0]);
            Assert.Equal(1, released.Transform[released.Width - 1]);
            Assert.Equal(1, released.Transform[(released.Height * released.Width) - 1]);

            Assert.Equal(1, pressed.Transform![0]);
            Assert.Equal(1, pressed.Transform[(pressed.Height * pressed.Width) - 1]);

            // The two columns the source had cut away are dropped: column six lands on column four.
            // (The first four columns are deliberately avoided: MakeButtonCornersTransparent() clears them.)
            Assert.Equal(sourceReleased.Image[6, 0], released.Image[4, 0]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void DismissAndUpgradeButtonsComeFromTheirOwnViewArmyFrames()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            string originalName = IcnCatalog.GetFileName(IcnType.Viewarmy);

            // The reference picks frames 1/2 for DISMISS, 3/4 for EXIT and 5/6 for UPGRADE
            // (game_assets.cpp:1194-1205).
            AssertSmallTextButtonMatches(originalName, EmptyButtons.SmallDismissGoodFileName, 1);
            AssertSmallTextButtonMatches(originalName, EmptyButtons.SmallExitGoodFileName, 3);
            AssertSmallTextButtonMatches(originalName, EmptyButtons.SmallUpgradeGoodFileName, 5);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    /// <summary>
    /// Asserts that a generated small text button is the two frames of <paramref name="originalName"/> at
    /// <paramref name="releasedFrame"/> and the next one, trimmed by two columns and one row.
    /// </summary>
    private static void AssertSmallTextButtonMatches(string originalName, string fileName, int releasedFrame)
    {
        Sprite? sourceReleased = Assets.GetImage(originalName, releasedFrame);
        Sprite? sourcePressed = Assets.GetImage(originalName, releasedFrame + 1);
        if (sourceReleased is null || sourcePressed is null)
        {
            return;
        }

        Sprite? released = Assets.GetImage(fileName, 0);
        Sprite? pressed = Assets.GetImage(fileName, 1);

        Assert.NotNull(released);
        Assert.NotNull(pressed);
        Assert.Equal(sourceReleased.Width - 2, released!.Width);
        Assert.Equal(sourceReleased.Height - 1, released.Height);
        Assert.Equal(sourcePressed.Width - 2, pressed!.Width);
        Assert.Equal(sourcePressed.Height - 1, pressed.Height);
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
