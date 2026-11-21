using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The generated text buttons of <c>src/fheroes2/gui/ui_button.cpp</c>: <c>makeButtonSprites()</c>
/// stretches the blank <c>SYSTEM.ICN</c> button to the size of the label and prints the label on it.
/// </summary>
public sealed class TextButtonArtworkTests
{
    [Fact]
    public void Create_ReturnsTheTextAreaPlusTheHorizontalMargin()
    {
        (Sprite released, Sprite pressed) = CreateButton("OK", 100, 30);

        Assert.Equal(110, released.Width);
        Assert.Equal(30, released.Height);
        Assert.Equal(released.Width, pressed.Width);
        Assert.Equal(released.Height, pressed.Height);
    }

    [Fact]
    public void Create_SqueezesTheHeightToTheMinimumButtonHeight()
    {
        (Sprite released, _) = CreateButton("OK", 100, 4);

        Assert.Equal(TextButtonArtwork.MinimumButtonHeight, released.Height);
    }

    [Fact]
    public void Create_KeepsTheBlankWidth_WhenTheLabelIsNarrowerThanTheBlank()
    {
        (Sprite released, _) = CreateButton("OK", 1, 30);

        // The reference squeezes down to 16 pixels; the port stops at the blank's own width because
        // ButtonArtwork.Resize() cannot cut a blank narrower than its end pieces.
        Assert.Equal(BlankWidth, released.Width);
    }

    [Fact]
    public void Create_PrintsTheLabelOntoTheBlankButton()
    {
        (Sprite released, Sprite pressed) = CreateButton("OK", 100, 30);

        Assert.True(CountPixels(released, GlyphColor) > 0, "The released button has no label.");
        Assert.True(CountPixels(pressed, GlyphColor) > 0, "The pressed button has no label.");
    }

    [Fact]
    public void Create_FillsTheTransparentCornerOfThePressedButtonWithTheStoneTexture()
    {
        // The background is cropped from its centre, so it has to be larger than the button.
        Sprite stone = CreateFilledSprite(200, 100, StoneColor);
        Sprite emptyReleased = CreateBlankButton();
        Sprite emptyPressed = CreateBlankButton();

        // The pressed plate shifts by one pixel, which leaves a transparent corner under the released
        // plate: that pixel takes the stone texture behind the button.
        emptyReleased.Transform![0] = 0;
        emptyPressed.Transform![0] = 1;

        StubFont font = new();
        (Sprite released, Sprite pressed) = TextButtonArtwork.Create(
            "OK", 100, 30, stone, emptyReleased, emptyPressed, font, font);

        // Only the pressed plate is patched, exactly as makeTransparentBackground() does it.
        Assert.Equal(ButtonColor, released.Image[0, 0]);
        Assert.Equal(StoneColor, pressed.Image[0, 0]);
        Assert.Equal(0, pressed.Transform![0]);
    }

    [Fact]
    public void Create_RejectsANullLabel()
    {
        StubFont font = new();

        Assert.Throws<ArgumentNullException>(() => TextButtonArtwork.Create(
            null!, 100, 30, CreateFilledSprite(90, 25, StoneColor), CreateBlankButton(), CreateBlankButton(),
            font, font));
    }

    private const int BlankWidth = 90;

    private const int BlankHeight = 25;

    private const byte ButtonColor = 5;

    private const byte StoneColor = 9;

    private const byte GlyphColor = 1;

    private static (Sprite Released, Sprite Pressed) CreateButton(string text, int textAreaWidth, int textAreaHeight)
    {
        Sprite stone = CreateFilledSprite(200, 100, StoneColor);
        Sprite emptyReleased = CreateBlankButton();
        Sprite emptyPressed = CreateBlankButton();
        StubFont font = new();

        return TextButtonArtwork.Create(
            text, textAreaWidth, textAreaHeight, stone, emptyReleased, emptyPressed, font, font);
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

    private static int CountPixels(Sprite sprite, byte color)
    {
        int count = 0;

        for (int y = 0; y < sprite.Height; ++y)
        {
            for (int x = 0; x < sprite.Width; ++x)
            {
                if (sprite.Image[x, y] == color)
                {
                    ++count;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// A font with a solid three by three glyph, enough to see that the label was printed without
    /// pulling the bitmap tables of the game in.
    /// </summary>
    private sealed class StubFont : IGlyphSource
    {
        public int LineHeight => 7;

        public int SpaceWidth => 2;

        public bool IsAvailable(char character) => !char.IsControl(character);

        public int GetAdvance(char character) => 3;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            Sprite sprite = new(3, 3);

            for (int y = 0; y < sprite.Height; ++y)
            {
                for (int x = 0; x < sprite.Width; ++x)
                {
                    sprite.Image[x, y] = GlyphColor;
                }
            }

            glyph = sprite;
            return true;
        }
    }
}
