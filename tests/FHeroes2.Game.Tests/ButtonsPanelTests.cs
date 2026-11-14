using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="ButtonsPanel"/>: the layout of <c>Interface::ButtonsPanel::SetPos</c> for a 640x480
/// display, the frame pairs of <c>ADVBTNS.ICN</c>, the three glyphs of the movement button
/// (<c>_setButtonStatus</c>) and the darkened look of a disabled button.
/// </summary>
public sealed class ButtonsPanelTests
{
    private static readonly Size Screen = new(640, 480);

    private static ButtonsPanel Panel => new(new MapFrame(Screen, null).Panel);

    [Fact]
    public void ThePanelSitsOneBorderBelowTheIconColumns()
    {
        // The icon columns run from 176 to 304; the reference puts the buttons one border (16 pixels in
        // the artwork of this frame) below them.
        Assert.Equal(new Rect(480, 320, 144, 72), Panel.Area);
    }

    [Fact]
    public void TheButtonsFillFourColumnsOfTwoRows()
    {
        ButtonsPanel panel = Panel;

        Assert.Equal(new Rect(480, 320, 36, 36), panel.AreaOf(AdventureButton.NextHero));
        Assert.Equal(new Rect(516, 320, 36, 36), panel.AreaOf(AdventureButton.HeroMovement));
        Assert.Equal(new Rect(588, 320, 36, 36), panel.AreaOf(AdventureButton.Spell));
        Assert.Equal(new Rect(480, 356, 36, 36), panel.AreaOf(AdventureButton.EndTurn));
        Assert.Equal(new Rect(588, 356, 36, 36), panel.AreaOf(AdventureButton.System));
    }

    [Fact]
    public void EveryButtonOwnsAReleasedAndAPressedFrame()
    {
        // setICNInfo( icnbtn, 0, 1 ) … setICNInfo( icnbtn, 14, 15 ), in the order of the enum.
        Assert.Equal(0, ButtonsPanel.ReleasedFrame(AdventureButton.NextHero));
        Assert.Equal(1, ButtonsPanel.PressedFrame(AdventureButton.NextHero));
        Assert.Equal(2, ButtonsPanel.ReleasedFrame(AdventureButton.HeroMovement));
        Assert.Equal(3, ButtonsPanel.PressedFrame(AdventureButton.HeroMovement));
        Assert.Equal(4, ButtonsPanel.ReleasedFrame(AdventureButton.Kingdom));
        Assert.Equal(7, ButtonsPanel.PressedFrame(AdventureButton.Spell));
        Assert.Equal(8, ButtonsPanel.ReleasedFrame(AdventureButton.EndTurn));
        Assert.Equal(10, ButtonsPanel.ReleasedFrame(AdventureButton.Adventure));
        Assert.Equal(12, ButtonsPanel.ReleasedFrame(AdventureButton.File));
        Assert.Equal(15, ButtonsPanel.PressedFrame(AdventureButton.System));
    }

    [Fact]
    public void TheMovementButtonHasThreeGlyphs()
    {
        Assert.Equal(2, ButtonsPanel.ReleasedFrame(HeroMovementIcon.Walk));
        Assert.Equal(16, ButtonsPanel.ReleasedFrame(HeroMovementIcon.Action));
        Assert.Equal(18, ButtonsPanel.ReleasedFrame(HeroMovementIcon.Reset));
    }

    [Fact]
    public void WithoutAHeroOnlyTheFiveUnconditionalButtonsAreEnabled()
    {
        AdventureButtonStatus status = AdventureButtonStatus.WithoutHero;

        Assert.Equal(HeroMovementIcon.Reset, status.Movement);
        Assert.False(status.IsMovementEnabled);
        Assert.False(status.IsSpellEnabled);
        Assert.False(status.IsNextHeroEnabled);
    }

    [Fact]
    public void EnabledButtonsShowTheirReleasedFrameAndDisabledOnesTheDarkenedCopy()
    {
        ButtonsPanel panel = Panel;
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        panel.Draw(pixels, new FakeButtons(), AdventureButtonStatus.WithoutHero);

        // The five unconditional buttons are painted with the released frame (0x11).
        foreach (AdventureButton button in new[]
                 {
                     AdventureButton.Kingdom,
                     AdventureButton.EndTurn,
                     AdventureButton.Adventure,
                     AdventureButton.File,
                     AdventureButton.System,
                 })
        {
            Assert.Equal(0x11, pixels[panel.AreaOf(button).X + 1, panel.AreaOf(button).Y + 1]);
        }

        // The three that need a hero are disabled and painted with the darkened frame (0x22).
        foreach (AdventureButton button in new[]
                 {
                     AdventureButton.NextHero,
                     AdventureButton.HeroMovement,
                     AdventureButton.Spell,
                 })
        {
            Assert.Equal(0x22, pixels[panel.AreaOf(button).X + 1, panel.AreaOf(button).Y + 1]);
        }
    }

    [Fact]
    public void TheMovementButtonAsksForTheFrameItsIconNames()
    {
        ButtonsPanel panel = Panel;
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        FakeButtons buttons = new();

        panel.Draw(
            pixels,
            buttons,
            new AdventureButtonStatus(HeroMovementIcon.Action, IsMovementEnabled: true, IsSpellEnabled: true, IsNextHeroEnabled: true));

        // The action glyph instead of the walking one, and every button painted while all are enabled.
        Assert.Contains(ButtonsPanel.ActionFrame, buttons.Released);
        Assert.DoesNotContain(ButtonsPanel.ReleasedFrame(AdventureButton.HeroMovement), buttons.Released);
        Assert.Equal(ButtonsPanel.ButtonCount, buttons.Released.Count);

        Rect movement = panel.AreaOf(AdventureButton.HeroMovement);
        Assert.Equal(0x11, pixels[movement.X, movement.Y]);
    }

    [Fact]
    public void AFrameTheFileDoesNotHoldLeavesTheSlotAlone()
    {
        // The original ADVBTNS.ICN stops at frame 15, so the action and reset glyphs of the movement
        // button have no artwork; the reference draws an empty sprite there as well.
        ButtonsPanel panel = Panel;
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        panel.Draw(pixels, new FakeButtons(missing: true), AdventureButtonStatus.WithoutHero);

        Rect movement = panel.AreaOf(AdventureButton.HeroMovement);
        Assert.Equal(3, pixels[movement.X, movement.Y]);
        Assert.Equal(0x22, pixels[panel.AreaOf(AdventureButton.Spell).X, panel.AreaOf(AdventureButton.Spell).Y]);
    }

    [Fact]
    public void ASourceOfNothingPaintsNothing()
    {
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        Panel.Draw(pixels, null, AdventureButtonStatus.WithoutHero);

        Assert.Equal(3, pixels[Panel.Area.X, Panel.Area.Y]);
    }

    /// <summary>Frames of constant colour: <c>0x11</c> released, <c>0x22</c> disabled.</summary>
    private sealed class FakeButtons(bool missing = false) : IButtonSpriteSource
    {
        /// <summary>Frames that were asked for, in the order the panel asked for them.</summary>
        public List<int> Released { get; } = [];

        public Sprite? GetReleased(int frame)
        {
            Released.Add(frame);

            if (missing && frame >= 16)
            {
                return null;
            }

            Sprite sprite = new(ButtonsPanel.ButtonSize, ButtonsPanel.ButtonSize);
            sprite.Image.Fill(0x11);
            return sprite;
        }

        public Sprite? GetDisabled(int frame)
        {
            if (missing && frame >= 16)
            {
                return null;
            }

            Sprite sprite = new(ButtonsPanel.ButtonSize, ButtonsPanel.ButtonSize);
            sprite.Image.Fill(0x22);
            return sprite;
        }
    }
}

/// <summary>
/// Covers <see cref="ButtonSpriteSet"/> against the installed archives: the button frames themselves
/// and the <c>DARKENING</c> copy of them, which is how <c>ButtonBase::_getDisabled</c> greys a button.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class ButtonSpriteSetTests
{
    [Fact]
    public void ThePanelTakesTheSixteenFramesOfAdvbtns()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        ButtonSpriteSet? set = ButtonSpriteSet.TryCreate(assets);

        Assert.NotNull(set);

        Sprite? released = set!.GetReleased(ButtonsPanel.ReleasedFrame(AdventureButton.NextHero));

        Assert.NotNull(released);
        Assert.Equal(ButtonsPanel.ButtonSize, released!.Width);
        Assert.Equal(ButtonsPanel.ButtonSize, released.Height);
        Assert.Empty(set.Missing);

        // The original ADVBTNS.ICN ends at frame 15: the action and reset glyphs of the movement button
        // are an extension of the reference asset packs, so this port draws an empty slot for them.
        Assert.Null(set.GetReleased(ButtonsPanel.ResetFrame));
        Assert.Null(set.GetDisabled(ButtonsPanel.ResetFrame));
    }

    [Fact]
    public void ADisabledButtonIsTheReleasedFrameWithTheDarkeningPalette()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        ButtonSpriteSet? set = ButtonSpriteSet.TryCreate(assets);
        Assert.NotNull(set);

        Sprite released = set!.GetReleased(ButtonsPanel.ReleasedFrame(AdventureButton.System))!;
        Sprite disabled = set.GetDisabled(ButtonsPanel.ReleasedFrame(AdventureButton.System))!;

        Assert.Equal(released.Width, disabled.Width);
        Assert.Equal(released.Height, disabled.Height);
        Assert.NotSame(released.Image, disabled.Image);

        // The darkening table is not the identity, so at least one pixel has to differ; the released
        // frame itself must stay untouched.
        byte original = released.Image[0, 0];
        Assert.Equal(PaletteTable.Apply(PaletteId.Darkening, original), disabled.Image[0, 0]);
        Assert.Equal(original, released.Image[0, 0]);
    }
}
