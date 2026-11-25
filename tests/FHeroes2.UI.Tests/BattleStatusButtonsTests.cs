using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Battle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The three buttons under the battlefield of <see cref="BattleStatusButtons"/> against
/// <c>battle_interface.cpp:1467-1490</c> (the layout) and <c>:3317-3361</c> (the hit boxes).
/// </summary>
public sealed class BattleStatusButtonsTests
{
    /// <summary>The <c>TEXTBAR</c> frames the three buttons use, in the sizes the real archive has.</summary>
    private sealed class TextBarSprites : IViewWorldSpriteSource
    {
        /// <summary>Palette index of the released frames.</summary>
        public const byte ReleasedValue = 3;

        /// <summary>Palette index of the pressed frames.</summary>
        public const byte PressedValue = 7;

        public Sprite? GetSprite(IcnType icn, int index) => (icn, index) switch
        {
            (IcnType.Textbar, 0) => Create(51, 37, ReleasedValue),
            (IcnType.Textbar, 1) => Create(51, 37, PressedValue),
            (IcnType.Textbar, 4) => Create(49, 18, ReleasedValue),
            (IcnType.Textbar, 5) => Create(49, 18, PressedValue),
            (IcnType.Textbar, 6) => Create(49, 19, ReleasedValue),
            (IcnType.Textbar, 7) => Create(49, 19, PressedValue),
            _ => null,
        };

        private static Sprite Create(int width, int height, byte value)
        {
            PixelBuffer image = new(width, height);
            ImageOps.Clear(image, value);

            return new Sprite(image);
        }
    }

    /// <summary>A source without any artwork.</summary>
    private sealed class EmptySprites : IViewWorldSpriteSource
    {
        public Sprite? GetSprite(IcnType icn, int index) => null;
    }

    [Fact]
    public void TheSettingsButtonSitsInTheBottomLeftCorner()
    {
        BattleStatusButtons buttons = new(new TextBarSprites(), 640, 480);

        Assert.Equal(0, buttons.SettingsArea.X);
        Assert.Equal(461, buttons.SettingsArea.Y);
        Assert.Equal(49, buttons.SettingsArea.Width);
        Assert.Equal(19, buttons.SettingsArea.Height);
    }

    [Fact]
    public void TheAutoButtonSitsOnTopOfTheSettingsButton()
    {
        BattleStatusButtons buttons = new(new TextBarSprites(), 640, 480);

        Assert.Equal(0, buttons.AutoArea.X);
        Assert.Equal(443, buttons.AutoArea.Y);
        Assert.Equal(49, buttons.AutoArea.Width);
        Assert.Equal(18, buttons.AutoArea.Height);
    }

    [Fact]
    public void TheSkipButtonSitsInTheBottomRightCorner()
    {
        BattleStatusButtons buttons = new(new TextBarSprites(), 640, 480);

        Assert.Equal(589, buttons.SkipArea.X);
        Assert.Equal(443, buttons.SkipArea.Y);
        Assert.Equal(51, buttons.SkipArea.Width);
        Assert.Equal(37, buttons.SkipArea.Height);
    }

    [Fact]
    public void HitTestFindsEachButtonAndNothingElse()
    {
        BattleStatusButtons buttons = new(new TextBarSprites(), 640, 480);

        Assert.Equal(BattleStatusButton.Auto, buttons.HitTest(new Point(10, 450)));
        Assert.Equal(BattleStatusButton.Settings, buttons.HitTest(new Point(10, 470)));
        Assert.Equal(BattleStatusButton.Skip, buttons.HitTest(new Point(600, 460)));
        Assert.Equal(BattleStatusButton.None, buttons.HitTest(new Point(300, 460)));
        Assert.Equal(BattleStatusButton.None, buttons.HitTest(new Point(10, 400)));
    }

    [Fact]
    public void DrawUsesThePressedFrameOfTheHeldButton()
    {
        BattleStatusButtons buttons = new(new TextBarSprites(), 640, 480);
        PixelBuffer frame = new(640, 480);

        buttons.Draw(frame, BattleStatusButton.Auto);

        Assert.Equal(TextBarSprites.PressedValue, frame.Pixels[(443 * 640) + 1]);
        Assert.Equal(TextBarSprites.ReleasedValue, frame.Pixels[(461 * 640) + 1]);
        Assert.Equal(TextBarSprites.ReleasedValue, frame.Pixels[(450 * 640) + 600]);
    }

    [Fact]
    public void WithoutArtworkTheButtonsKeepTheirFallbackBoxes()
    {
        BattleStatusButtons buttons = new(new EmptySprites(), 640, 480);

        Assert.Equal(49, buttons.AutoArea.Width);
        Assert.Equal(18, buttons.AutoArea.Height);
        Assert.Equal(461, buttons.SettingsArea.Y);
        Assert.Equal(589, buttons.SkipArea.X);
    }
}
