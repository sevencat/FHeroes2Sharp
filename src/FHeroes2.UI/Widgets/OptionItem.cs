using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The icon with a title above and a value below that every options dialog is made of, a port of
/// <c>fheroes2::drawOption</c> and its wrappers (<c>src/fheroes2/gui/ui_option_item.cpp:46-166</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The text is centred on the icon, not on the slot.</b> The reference computes one left border,
/// <c>optionRoi.x + ( icon.width() - textMaxWidth ) / 2</c>, and draws both lines from it, which is why a row of
/// options with icons of different widths keeps a straight text block: the icons are cut to the same width by the
/// caller, not by this function.
/// </para>
/// <para>
/// <b>The title is aligned to the bottom of its own wrapped block.</b> <c>optionRoi.y - 12 + height() - height( maxWidth )</c>
/// lifts a two line title by one line so the last line always sits 12 pixels above the icon, no matter how many lines
/// the title wrapped into. The value line below has no such correction: it starts 6 pixels under the icon.
/// </para>
/// <para>
/// The icon is copied without its sprite offset and then given a gradient shadow shifted 5 pixels left and 5 down,
/// the same order as the reference — the shadow lands over whatever the window already drew.
/// </para>
/// </remarks>
public static class OptionItem
{
    /// <summary>Distance between the icon and the bottom of the title, <c>textVerticalOffset</c>.</summary>
    public const int TitleVerticalOffset = 12;

    /// <summary>Distance between the icon and the top of the value line, <c>nameVerticalOffset</c>.</summary>
    public const int ValueVerticalOffset = 6;

    /// <summary>Text width of a row with two options, <c>UiOptionTextWidth::TWO_ELEMENTS_ROW</c> (113).</summary>
    public const int TwoElementsRowWidth = 113;

    /// <summary>Text width of a row with three options, <c>UiOptionTextWidth::THREE_ELEMENTS_ROW</c> (87).</summary>
    public const int ThreeElementsRowWidth = 87;

    /// <summary>Horizontal step between the icons of a three option row, <c>threeOptionsStepX</c> (92).</summary>
    public const int ThreeOptionsStepX = 92;

    /// <summary>Horizontal step between the icons of a two option row, <c>twoOptionsStepX</c> (118).</summary>
    public const int TwoOptionsStepX = 118;

    /// <summary>Vertical step between the icon rows, <c>optionsStepY</c> (110).</summary>
    public const int OptionsStepY = 110;

    /// <summary>Left offset of the first icon of a three option row, <c>threeOptionsOffsetX</c> (20).</summary>
    public const int ThreeOptionsOffsetX = 20;

    /// <summary>Left offset of the first icon of a two option row, <c>twoOptionsOffsetX</c> (53).</summary>
    public const int TwoOptionsOffsetX = 53;

    /// <summary>Top offset of the first icon row, <c>optionsOffsetY</c> (31).</summary>
    public const int OptionsOffsetY = 31;

    /// <summary>Side of an option icon, <c>optionIconSize</c> (65).</summary>
    public const int OptionIconSize = 65;

    /// <summary>
    /// Draws an icon with its title and value, <c>drawOption()</c> (<c>ui_option_item.cpp:46</c>).
    /// </summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon; the title goes above it, the value under it.</param>
    /// <param name="icon">The icon, drawn at <paramref name="optionArea"/>'s top left corner.</param>
    /// <param name="title">Text above the icon, wrapped to <paramref name="textMaxWidth"/>.</param>
    /// <param name="value">Text under the icon, wrapped to <paramref name="textMaxWidth"/>.</param>
    /// <param name="textMaxWidth">
    /// Widest line the two texts may use; <see cref="TwoElementsRowWidth"/> or <see cref="ThreeElementsRowWidth"/> for
    /// the two standard rows.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="display"/> or <paramref name="icon"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="textMaxWidth"/> is not positive; the reference asserts on it.</exception>
    public static void Draw(
        PixelBuffer display, in Rect optionArea, Sprite icon, string title, string value, int textMaxWidth)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(value);

        if (textMaxWidth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(textMaxWidth), textMaxWidth, "The text width has to be at least one pixel.");
        }

        // The reference computes one border from the icon width and draws both lines from it.
        int textX = optionArea.X + ((icon.Width - textMaxWidth) / 2);

        var titleText = new Text(title, FontType.SmallWhite());
        var valueText = new Text(value, FontType.SmallWhite());

        Rect displayArea = display.Bounds;

        titleText.DrawInRoi(
            textX,
            optionArea.Y - TitleVerticalOffset + titleText.Height() - titleText.Height(textMaxWidth),
            textMaxWidth,
            display,
            displayArea);

        valueText.DrawInRoi(textX, optionArea.Y + optionArea.Height + ValueVerticalOffset, textMaxWidth, display, displayArea);

        ImageOps.BlitSprite(display, new Point(optionArea.X, optionArea.Y), icon);
        ImageOps.AddGradientShadow(icon, display, new Point(optionArea.X, optionArea.Y), new Point(-5, 5));
    }

    /// <summary>Interface set option, <c>drawInterfaceType()</c> (<c>ui_option_item.cpp:101</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon">
    /// <c>SPANEL</c> frame 15 for the dynamic set, 16 for the good one, 17 for the evil one.
    /// </param>
    /// <param name="interfaceType">The set that is selected.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interfaceType"/> is not one of the three sets.</exception>
    public static void DrawInterfaceType(
        PixelBuffer display, in Rect optionArea, Sprite icon, InterfaceType interfaceType, int textMaxWidth)
    {
        string value = interfaceType switch
        {
            InterfaceType.Dynamic => "Dynamic",
            InterfaceType.Good => "Good",
            InterfaceType.Evil => "Evil",
            _ => throw new ArgumentOutOfRangeException(
                nameof(interfaceType), interfaceType, "Not an interface set."),
        };

        Draw(display, optionArea, icon, "Interface Type", value, textMaxWidth);
    }

    /// <summary>Scroll speed option, <c>drawScrollSpeed()</c> (<c>ui_option_item.cpp:63</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon">
    /// <c>SPANEL</c> frame 9 when the speed is off, <c>CSPANEL</c> frame 0 for slow and normal, 1 for fast,
    /// 2 for very fast.
    /// </param>
    /// <param name="speed">The speed that is selected.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawScrollSpeed(
        PixelBuffer display, in Rect optionArea, Sprite icon, AdventureMapScrollSpeed speed, int textMaxWidth)
    {
        string value = speed switch
        {
            AdventureMapScrollSpeed.None => "Off",
            AdventureMapScrollSpeed.Slow => "Slow",
            AdventureMapScrollSpeed.Normal => "Normal",
            AdventureMapScrollSpeed.Fast => "Fast",
            AdventureMapScrollSpeed.VeryFast => "Very Fast",
            _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, "Not a scroll speed."),
        };

        Draw(display, optionArea, icon, "Scroll Speed", value, textMaxWidth);
    }

    /// <summary>Cursor option, <c>drawCursorType()</c> (<c>ui_option_item.cpp:125</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>SPANEL</c> frame 20 for the monochrome cursor, 21 for the colour one.</param>
    /// <param name="isMonochromeCursor">Whether the black and white cursor is selected.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawCursor(
        PixelBuffer display, in Rect optionArea, Sprite icon, bool isMonochromeCursor, int textMaxWidth) =>
        Draw(
            display,
            optionArea,
            icon,
            "Mouse Cursor",
            isMonochromeCursor ? "Black & White" : "Color",
            textMaxWidth);

    /// <summary>Audio option, <c>drawAudioOptions()</c> (<c>ui_option_item.cpp:140</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>SPANEL</c> frame 1.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawAudio(PixelBuffer display, in Rect optionArea, Sprite icon, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Audio", "Settings", textMaxWidth);

    /// <summary>Graphics option, <c>drawGraphics()</c> (<c>ui_option_item.cpp:145</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>GAME_OPTION_ICON</c> frame 1.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawGraphics(PixelBuffer display, in Rect optionArea, Sprite icon, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Graphics", "Settings", textMaxWidth);

    /// <summary>Hot keys option, <c>drawHotKeyOptions()</c> (<c>ui_option_item.cpp:135</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>GAME_OPTION_ICON</c> frame 0.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawHotKeys(PixelBuffer display, in Rect optionArea, Sprite icon, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Hot Keys", "Configure", textMaxWidth);

    /// <summary>Text support option, <c>drawTextSupportModeOptions()</c> (<c>ui_option_item.cpp:158</c>).</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>CSPANEL</c> frame 4 when on, <c>SPANEL</c> frame 9 when off.</param>
    /// <param name="isEnabled">Whether text support is on.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawTextSupport(
        PixelBuffer display, in Rect optionArea, Sprite icon, bool isEnabled, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Text Support", isEnabled ? "On" : "Off", textMaxWidth);

    /// <summary>Language option, <c>drawLanguage()</c> (<c>ui_option_item.cpp:150</c>).</summary>
    /// <remarks>
    /// 参考按当前语言切换翻译表再取语言名（<c>LanguageSwitcher</c>），端口没有翻译层，名字由调用方给出。
    /// </remarks>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>SPANEL</c> frame 18.</param>
    /// <param name="languageName">Name of the current language, <c>getLanguageName()</c> of the reference.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawLanguage(
        PixelBuffer display, in Rect optionArea, Sprite icon, string languageName, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Language", languageName, textMaxWidth);

    /// <summary>Object animation option, <c>drawAnimationOptions()</c> of the editor's options.</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>ESPANEL</c> frame 1 when on, 0 when off.</param>
    /// <param name="isEnabled">Whether objects are animated.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawAnimation(
        PixelBuffer display, in Rect optionArea, Sprite icon, bool isEnabled, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Animation", isEnabled ? "On" : "Off", textMaxWidth);

    /// <summary>Passability overlay option, <c>drawPassabilityOptions()</c> of the editor's options.</summary>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="optionArea">Area of the icon.</param>
    /// <param name="icon"><c>ESPANEL</c> frame 5 when shown, 4 when hidden.</param>
    /// <param name="isEnabled">Whether the passability overlay is shown.</param>
    /// <param name="textMaxWidth">Widest line of the texts.</param>
    public static void DrawPassability(
        PixelBuffer display, in Rect optionArea, Sprite icon, bool isEnabled, int textMaxWidth) =>
        Draw(display, optionArea, icon, "Passability", isEnabled ? "Show" : "Hide", textMaxWidth);
}
