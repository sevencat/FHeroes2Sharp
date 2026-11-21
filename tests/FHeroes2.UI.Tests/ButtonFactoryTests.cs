using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

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

    private static Sprite CreateFilledSprite(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                image[x, y] = color;
            }
        }

        return new Sprite(image, 0, 0, new byte[width * height]);
    }
}
