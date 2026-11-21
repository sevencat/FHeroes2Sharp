using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="ButtonRestorer"/>, the scope that shows a button darkened and repaints it on the way
/// out (<c>ui_button.cpp:643-665</c>).
/// </summary>
public sealed class ButtonRestorerTests
{
    [Fact]
    public void Constructor_DarkensTheButtonAndRepaintsIt()
    {
        ButtonSprite button = CreateButton();
        PixelBuffer canvas = new(120, 60);

        using (new ButtonRestorer(button, canvas))
        {
            Assert.False(button.IsEnabled);

            // The constructor draws at once: the frame holds the darkened button, not just the state.
            Assert.Equal(DarkenedButtonColor, canvas[53, 34]);
        }
    }

    [Fact]
    public void Dispose_EnablesTheButtonAndRepaintsIt()
    {
        ButtonSprite button = CreateButton();
        PixelBuffer canvas = new(120, 60);

        using (new ButtonRestorer(button, canvas))
        {
        }

        Assert.True(button.IsEnabled);
        Assert.Equal(ButtonColor, canvas[53, 34]);
        Assert.Equal(StoneColor, canvas[50, 30]);
    }

    [Fact]
    public void Dispose_LeavesAnAlreadyDisabledButtonDisabled()
    {
        ButtonSprite button = CreateButton();
        button.Disable();

        PixelBuffer canvas = new(120, 60);
        using (new ButtonRestorer(button, canvas))
        {
        }

        // The reference captures isEnabled() once and restores nothing in that case.
        Assert.False(button.IsEnabled);
        Assert.Equal(0, canvas[53, 34]);
    }

    [Fact]
    public void Dispose_CanRunTwice()
    {
        ButtonSprite button = CreateButton();
        PixelBuffer canvas = new(120, 60);

        ButtonRestorer restorer = new(button, canvas);
        restorer.Dispose();
        restorer.Dispose();

        Assert.True(button.IsEnabled);
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ButtonRestorer(null!, new PixelBuffer(1, 1)));
        Assert.Throws<ArgumentNullException>(() => new ButtonRestorer(CreateButton(), null!));
    }

    private const byte ButtonColor = 5;

    private const byte StoneColor = 9;

    /// <summary><c>PAL::GetPalette( DARKENING )</c> applied to <see cref="ButtonColor"/>.</summary>
    private static readonly byte DarkenedButtonColor = Darken(ButtonColor);

    private static byte Darken(byte color)
    {
        Sprite probe = CreateFilledSprite(1, 1, color);
        ImageOps.ApplyPalette(probe.Image, PaletteId.Darkening);
        return probe.Image[0, 0];
    }

    private static ButtonSprite CreateButton() => ButtonFactory.MakeButtonWithBackground(
        50,
        30,
        CreateFilledSprite(40, 20, ButtonColor, offsetX: 3, offsetY: 4),
        CreateFilledSprite(40, 20, ButtonColor, offsetX: 3, offsetY: 4),
        CreateFilledSprite(200, 100, StoneColor));

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
