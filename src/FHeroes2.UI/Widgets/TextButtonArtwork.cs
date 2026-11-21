using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Draws the text buttons the engine has to generate itself, a port of <c>makeButtonSprites()</c> and
/// <c>getCustomNormalButton()</c> of <c>ui_button.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Several button tables of the game are not in the archives at all: the engine takes the blank button
/// of <c>SYSTEM.ICN</c> (frames 11 and 12), stretches it to the size of the label and prints the label
/// on it. The editor main menu needs it for its Main Menu and Back buttons, hence this helper next to
/// the other widgets.
/// </para>
/// <para>
/// The stretching and the transparency are <see cref="ButtonArtwork"/>, which is the port of
/// <c>resizeButton()</c> and <c>makeTransparentBackground()</c>. Not ported yet: the shine lines of
/// <c>addButtonShine()</c> and the reference's one-pixel <c>Fill()</c> of the blank button, both purely
/// cosmetic.
/// </para>
/// </remarks>
public static class TextButtonArtwork
{
    /// <summary>The shortest text button the reference builds, <c>minimumButtonHeight</c>.</summary>
    public const int MinimumButtonHeight = 25;

    /// <summary>The longest text button the reference builds, <c>maximumButtonHeight</c>.</summary>
    public const int MaximumButtonSize = 200;

    /// <summary>The smallest width a text button can be squeezed to, <c>minimumButtonWidth</c>.</summary>
    public const int MinimumWidth = 16;

    /// <summary>The button is 10 pixels wider than its text area, <c>getCustomNormalButton()</c>.</summary>
    public const int HorizontalMargin = 10;

    /// <summary>
    /// Builds the released and pressed state of a text button.
    /// </summary>
    /// <param name="text">Label to print, <c>\n</c> separated for several lines.</param>
    /// <param name="textAreaWidth">Width of the text area, without <see cref="HorizontalMargin"/>.</param>
    /// <param name="textAreaHeight">Height of the text area.</param>
    /// <param name="stoneBackground">
    /// Stone texture the blank button's transparent pixels are filled with, so that the button does not
    /// show a hole in the window behind it.
    /// </param>
    /// <param name="emptyReleased">Blank released button, <c>SYSTEM.ICN</c> frame 11.</param>
    /// <param name="emptyPressed">Blank pressed button, <c>SYSTEM.ICN</c> frame 12.</param>
    /// <param name="releasedFont">Font of the released state.</param>
    /// <param name="pressedFont">
    /// Font of the pressed state, which the reference prints one pixel lower and to the left.
    /// </param>
    /// <returns>The two states, both without a sprite offset; the caller positions them.</returns>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public static (Sprite Released, Sprite Pressed) Create(
        string text,
        int textAreaWidth,
        int textAreaHeight,
        Sprite stoneBackground,
        Sprite emptyReleased,
        Sprite emptyPressed,
        IGlyphSource releasedFont,
        IGlyphSource pressedFont)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(stoneBackground);
        ArgumentNullException.ThrowIfNull(emptyReleased);
        ArgumentNullException.ThrowIfNull(emptyPressed);
        ArgumentNullException.ThrowIfNull(releasedFont);
        ArgumentNullException.ThrowIfNull(pressedFont);

        int width = Math.Clamp(textAreaWidth + HorizontalMargin, MinimumWidth, MaximumButtonSize);
        int height = Math.Clamp(textAreaHeight, MinimumButtonHeight, MaximumButtonSize);

        // The reference squeezes a button down to 16 pixels, but ButtonArtwork.Resize() cannot stitch a
        // blank that is narrower than its own end pieces yet, so the blank's own width is the floor.
        width = Math.Max(width, Math.Max(emptyReleased.Width, emptyPressed.Width));
        Size size = new(width, height);

        Sprite released = ButtonArtwork.Resize(emptyReleased, size);
        Sprite pressed = ButtonArtwork.Resize(emptyPressed, size);

        ButtonArtwork.MakeTransparentBackground(released, pressed, stoneBackground);

        // The released label is printed one pixel to the right of the text area, the pressed one one
        // pixel lower and to the left; see renderTextOnButton() of ui_button.cpp.
        DrawCentered(releasedFont, released, text, 8, 0);
        DrawCentered(pressedFont, pressed, text, 7, 1);

        return (released, pressed);
    }

    /// <summary>Draws the label centred in the button, one line at a time.</summary>
    private static void DrawCentered(IGlyphSource font, Sprite button, string text, int left, int top)
    {
        int textWidth = button.Width - HorizontalMargin;
        List<string> lines = GlyphText.WrapLines(font, text, textWidth);

        if (lines.Count == 0)
        {
            return;
        }

        int textHeight = lines.Count * font.LineHeight;
        int y = top + ((button.Height - textHeight) / 2);

        foreach (string line in lines)
        {
            int lineWidth = GlyphText.Measure(font, line);
            int x = left + Math.Max(0, (textWidth - lineWidth) / 2);

            GlyphText.DrawLine(font, button.Image, new Point(x, y), line);
            y += font.LineHeight;
        }
    }
}
