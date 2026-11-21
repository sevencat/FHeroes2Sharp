using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The button generator of <c>src/fheroes2/gui/ui_button.cpp</c>: <c>getCustomNormalButton()</c> and
/// <c>makeSymmetricBackgroundSprites()</c>. The label itself is drawn with the bitmap font of the game,
/// so the tests hand the plates in and look at the geometry only.
/// </summary>
public sealed class ButtonFactoryTests
{
    [Fact]
    public void GetCustomNormalButton_ReturnsTheTextAreaPlusTheHorizontalMargin()
    {
        (Sprite released, Sprite pressed, _, _) = CreateButton(new Size(100, 30));

        Assert.Equal(110, released.Width);
        Assert.Equal(30, released.Height);
        Assert.Equal(released.Width, pressed.Width);
        Assert.Equal(released.Height, pressed.Height);
    }

    [Fact]
    public void GetCustomNormalButton_SqueezesTheHeightToTheMinimumButtonHeight()
    {
        (Sprite released, _, _, _) = CreateButton(new Size(100, 4));

        Assert.Equal(ButtonFactory.MinimumHeight, released.Height);
    }

    [Fact]
    public void GetCustomNormalButton_ClampsTheWidthToTheMaximumButtonSize()
    {
        (Sprite released, _, _, _) = CreateButton(new Size(500, 30));

        Assert.Equal(ButtonFactory.MaximumSize, released.Width);
    }

    [Fact]
    public void GetCustomNormalButton_ReportsTheLabelOffsetsOfTheReference()
    {
        (_, _, Point released, Point pressed) = CreateButton(new Size(100, 30));

        Assert.Equal(new Point(7, 5), released);
        Assert.Equal(new Point(6, 6), pressed);
    }

    [Fact]
    public void GetCustomNormalButton_FillsTheTransparentCornerOfThePressedButtonWithTheBackground()
    {
        // The background is cropped from its centre, so it has to be larger than the button.
        Sprite stone = CreateFilledSprite(200, 100, StoneColor);
        Sprite emptyReleased = CreateBlankButton();
        Sprite emptyPressed = CreateBlankButton();

        // The pressed plate shifts by one pixel, which leaves a transparent corner under the released
        // plate: that pixel takes the texture behind the button.
        emptyReleased.Transform![0] = 0;
        emptyPressed.Transform![0] = 1;

        (Sprite released, Sprite pressed, _, _) = ButtonFactory.GetCustomNormalButton(
            emptyReleased, emptyPressed, DialogButtonTemplate.Good, new Size(100, 30), stone);

        // Only the pressed plate is patched, exactly as makeTransparentBackground() does it.
        Assert.Equal(ButtonColor, released.Image[0, 0]);
        Assert.Equal(StoneColor, pressed.Image[0, 0]);
        Assert.Equal(0, pressed.Transform![0]);
    }

    [Fact]
    public void GetCustomNormalButton_RejectsAMissingPlate()
    {
        Assert.Throws<ArgumentNullException>(() => ButtonFactory.GetCustomNormalButton(
            null!, CreateBlankButton(), DialogButtonTemplate.Good, new Size(100, 30)));
    }

    [Fact]
    public void MakeSymmetricBackgroundSprites_RejectsASingleLabel()
    {
        Assert.Throws<ArgumentException>(() => ButtonFactory.MakeSymmetricBackgroundSprites(
            ["OK"], isEvilInterface: false, minimumWidth: 60));
    }

    [Fact]
    public void MakeButtonWithBackground_PastesTheButtonOntoTheCroppedBackground()
    {
        Sprite released = CreateFilledSprite(40, 20, ButtonColor, offsetX: 3, offsetY: 4);
        Sprite pressed = CreateFilledSprite(40, 20, ButtonColor, offsetX: 2, offsetY: 5);

        ButtonSprite button = ButtonFactory.MakeButtonWithBackground(
            50,
            30,
            released,
            pressed,
            CreateFilledSprite(200, 100, StoneColor));

        // The composite keeps the size of the button and sits where the caller asked for.
        Assert.Equal(50, button.OffsetX);
        Assert.Equal(30, button.OffsetY);
        Assert.Equal(40, button.Released.Width);
        Assert.Equal(20, button.Released.Height);

        // The button lands where its own anchor points inside the cropped background.
        Assert.Equal(StoneColor, button.Released.Image[2, 3]);
        Assert.Equal(ButtonColor, button.Released.Image[3, 4]);
        Assert.Equal(ButtonColor, button.Pressed.Image[2, 5]);
    }

    [Fact]
    public void MakeButtonWithBackground_DarkensTheDisabledState()
    {
        Sprite released = CreateFilledSprite(40, 20, ButtonColor, offsetX: 3, offsetY: 4);

        ButtonSprite button = ButtonFactory.MakeButtonWithBackground(
            50,
            30,
            released,
            released,
            CreateFilledSprite(200, 100, StoneColor));

        button.Disable();

        PixelBuffer canvas = new(120, 60);
        Assert.True(button.Draw(canvas));

        // The disabled state is the released composite with its button pixel pushed through the DARKENING
        // palette, while the rest of the composite stays the stone texture.
        Sprite probe = CreateFilledSprite(1, 1, ButtonColor);
        ImageOps.ApplyPalette(probe.Image, PaletteId.Darkening);

        Assert.Equal(probe.Image[0, 0], canvas[53, 34]);
        Assert.NotEqual(ButtonColor, canvas[53, 34]);
        Assert.Equal(StoneColor, canvas[50, 30]);
    }

    [Fact]
    public void MakeButtonWithShadow_ShiftsTheCompositeByTheShadowAnchor()
    {
        Sprite released = CreateFilledSprite(40, 20, ButtonColor, offsetX: 3, offsetY: 4);

        ButtonSprite button = ButtonFactory.MakeButtonWithShadow(
            50,
            30,
            released,
            released,
            CreateFilledSprite(200, 100, StoneColor),
            new Point(-2, 3));

        // makeShadow() puts its anchor at (sprite + offset): the composite follows, two pixels to the
        // left and four rows down.
        Assert.Equal(50 + 1, button.OffsetX);
        Assert.Equal(30 + 4, button.OffsetY);

        // The shadow is two pixels wider and three rows taller than the button.
        Assert.Equal(42, button.Released.Width);
        Assert.Equal(23, button.Released.Height);

        // The button is drawn at (anchor - shadow anchor) inside the composite.
        Assert.Equal(ButtonColor, button.Released.Image[3 + 2, 4]);
    }

    [Fact]
    public void GetSupportedText_FallsBackToTheOriginalLabelWhenTheFontMissesGlyphs()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;
        try
        {
            FontType font = FontType.ButtonReleasedWhite();

            Assert.Equal("OKAY", ButtonFactory.GetSupportedText("OKAY", "OKAY", font));

            // The button font only holds the Latin alphabet, so a translated label of another script
            // falls back to the original one, which is what the reference does for a half translated
            // build.
            Assert.Equal("OKAY", ButtonFactory.GetSupportedText("确定", "OKAY", font));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void GetBlankAssetName_NamesThePlateOfEveryTemplate()
    {
        Assert.Equal("EMPTY_GOOD_BUTTON.ICN", ButtonFactory.GetBlankAssetName(DialogButtonTemplate.Good));
        Assert.Equal("EMPTY_EVIL_BUTTON.ICN", ButtonFactory.GetBlankAssetName(DialogButtonTemplate.Evil));
        Assert.Equal("EMPTY_POL_BUTTON.ICN", ButtonFactory.GetBlankAssetName(DialogButtonTemplate.Pol));
        Assert.Equal(
            "EMPTY_GUILDWELL_BUTTON.ICN", ButtonFactory.GetBlankAssetName(DialogButtonTemplate.GuildWell));
        Assert.Equal(
            "EMPTY_MAP_SELECT_BUTTON.ICN", ButtonFactory.GetBlankAssetName(DialogButtonTemplate.MapSelect));
        Assert.Equal(
            "EMPTY_VERTICAL_GOOD_BUTTON.ICN",
            ButtonFactory.GetBlankAssetName(DialogButtonTemplate.VerticalGood));
    }

    private const int BlankWidth = 90;

    private const int BlankHeight = 25;

    private const byte ButtonColor = 5;

    private const byte StoneColor = 9;

    private static (Sprite Released, Sprite Pressed, Point ReleasedTextOffset, Point PressedTextOffset)
        CreateButton(Size textArea)
    {
        return ButtonFactory.GetCustomNormalButton(
            CreateBlankButton(),
            CreateBlankButton(),
            DialogButtonTemplate.Good,
            textArea,
            CreateFilledSprite(200, 100, StoneColor));
    }

    private static Sprite CreateBlankButton() => CreateFilledSprite(BlankWidth, BlankHeight, ButtonColor);

    private static Sprite CreateFilledSprite(int width, int height, byte color, int offsetX = 0, int offsetY = 0)
    {
        PixelBuffer image = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                image[x, y] = color;
            }
        }

        return new Sprite(image, offsetX, offsetY, new byte[width * height]);
    }
}
