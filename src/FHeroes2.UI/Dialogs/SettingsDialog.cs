using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The game settings dialog: a <see cref="StandardWindow"/> with a title, one row per
/// <see cref="SettingsOption"/> and the standard button row at the bottom.
/// </summary>
/// <remarks>
/// <para>
/// The reference opens <c>dialog::openGameSettings()</c> from the door of the main menu and builds
/// its rows from option button groups and checkboxes, applying every change immediately. The port
/// starts with the subset the client can actually honour - the interface language - and applies it
/// when the dialog closes with OK, so a Cancel (or <see cref="Cancel"/>) leaves the game as it was.
/// Rows are drawn as <c>&lt; value &gt;</c>: pressing the left half of a row selects the previous
/// value, the right half the next one.
/// </para>
/// <para>
/// Input mirrors the rest of the port: the scene feeds <see cref="SetCursor"/> and
/// <see cref="SetLeftButton"/> and gets a <see cref="DialogButton"/> back, which is
/// <see cref="DialogButton.Ok"/> only when the OK button was clicked with the press and the release
/// inside it. The dialog itself owns nothing of the settings; the caller reads
/// <see cref="Option(int)"/> after OK and applies what changed.
/// </para>
/// </remarks>
public sealed class SettingsDialog
{
    /// <summary>Gap between the top of the content area and the title, <c>textOffsetY</c> of the reference.</summary>
    public const int TitleOffsetY = 8;

    /// <summary>Vertical gap between the title and the first row, and between two rows.</summary>
    public const int RowGap = 6;

    /// <summary>Horizontal margin of the labels and of the values.</summary>
    public const int RowPadding = 8;

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _optionFont;
    private readonly SettingsOption[] _options;
    private readonly int _titleHeight;

    private Point? _cursor;
    private int _pressedRow = -1;

    /// <summary>Composes the dialog inside an already laid out window.</summary>
    /// <param name="window">The stone window the dialog is drawn in.</param>
    /// <param name="buttons">Button row placed by <see cref="ButtonGroup.CreateStandard"/>.</param>
    /// <param name="titleFont">Font of the title, usually the yellow normal font.</param>
    /// <param name="optionFont">Font of the labels and values, usually the white normal font.</param>
    /// <param name="title">Translated title, e.g. <c>"Settings"</c>; empty leaves it out.</param>
    /// <param name="options">Rows of the dialog, top to bottom.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public SettingsDialog(StandardWindow window, ButtonGroup buttons, IGlyphSource titleFont,
        IGlyphSource optionFont, string title, IReadOnlyList<SettingsOption> options)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(optionFont);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(options);

        _window = window;
        _buttons = buttons;
        _titleFont = titleFont;
        _optionFont = optionFont;
        _options = [.. options];
        Title = title;

        int titleLines = title.Length == 0 ? 0 : GlyphText.WrapLines(titleFont, title, window.ActiveArea.Width).Count;
        _titleHeight = titleLines * titleFont.LineHeight;
    }

    /// <summary>The window the dialog is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The standard button row of the dialog.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Translated title.</summary>
    public string Title { get; }

    /// <summary>Number of option rows.</summary>
    public int OptionCount => _options.Length;

    /// <summary>Height of one row, including the gap below it.</summary>
    public int RowHeight => _optionFont.LineHeight + RowGap;

    /// <summary>Returns the row at <paramref name="index"/>.</summary>
    /// <param name="index">Zero based row index.</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no row at <paramref name="index"/>.</exception>
    public SettingsOption Option(int index) => (uint)index < (uint)_options.Length
        ? _options[index]
        : throw new ArgumentOutOfRangeException(nameof(index), index, "The dialog has no such option row.");

    /// <summary>
    /// Area of the row at <paramref name="index"/>, i.e. the band the cursor has to be in to change
    /// that row.
    /// </summary>
    /// <param name="index">Zero based row index.</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no row at <paramref name="index"/>.</exception>
    public Rect OptionArea(int index)
    {
        _ = Option(index);

        Rect area = _window.ActiveArea;
        int top = area.Y + TitleOffsetY + _titleHeight + RowGap + (index * RowHeight);

        return new Rect(area.X, top, area.Width, _optionFont.LineHeight);
    }

    /// <summary>
    /// Height a content area needs so that the title, the rows and a button of
    /// <paramref name="buttonHeight"/> pixels fit, which is what a caller centres the window with.
    /// </summary>
    /// <param name="buttonHeight">Height of the standard button row.</param>
    public int GetRequiredHeight(int buttonHeight) =>
        GetRequiredHeight(_titleHeight, _optionFont.LineHeight, OptionCount, buttonHeight);

    /// <summary>
    /// The same calculation without a dialog, for a caller that has to size the window before it can
    /// compose the dialog.
    /// </summary>
    /// <param name="titleHeight">Height of the wrapped title, zero when there is none.</param>
    /// <param name="optionLineHeight">Line height of the font the rows are drawn with.</param>
    /// <param name="optionCount">Number of rows.</param>
    /// <param name="buttonHeight">Height of the standard button row.</param>
    public static int GetRequiredHeight(int titleHeight, int optionLineHeight, int optionCount, int buttonHeight) =>
        TitleOffsetY + titleHeight + RowGap + (optionCount * (optionLineHeight + RowGap)) + RowGap + buttonHeight;

    /// <summary>Draws the window, the title, the rows and the buttons, in that order.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        Rect area = _window.ActiveArea;

        if (Title.Length > 0)
        {
            GlyphText.DrawMultiline(
                _titleFont, target, new Point(area.X, area.Y + TitleOffsetY), Title, area.Width, center: true);
        }

        for (int index = 0; index < _options.Length; ++index)
        {
            Rect row = OptionArea(index);

            GlyphText.DrawLine(_optionFont, target, new Point(row.X + RowPadding, row.Y), _options[index].Label);

            string value = $"< {_options[index].Value} >";
            int valueWidth = GlyphText.Measure(_optionFont, value);
            GlyphText.DrawLine(
                _optionFont, target, new Point(row.Right - RowPadding - valueWidth, row.Y), value);
        }

        _buttons.Draw(target);
    }

    /// <summary>
    /// Records the cursor position and forwards it to the button row, so the pressed look of the OK
    /// button follows the mouse.
    /// </summary>
    /// <param name="cursor">Cursor in logical pixels, or <see langword="null"/> when it left.</param>
    /// <returns><see cref="DialogButton.None"/>; moving the cursor never closes the dialog.</returns>
    public DialogButton SetCursor(Point? cursor)
    {
        _cursor = cursor;
        _buttons.SetCursor(cursor);

        return DialogButton.None;
    }

    /// <summary>
    /// Records the left mouse button. A press and a release inside the same row change that row; the
    /// return value comes from the button row, so only a click on OK closes the dialog.
    /// </summary>
    /// <param name="down">Whether the left mouse button is now held.</param>
    /// <returns><see cref="DialogButton.Ok"/> when OK was clicked, <see cref="DialogButton.None"/> otherwise.</returns>
    public DialogButton SetLeftButton(bool down)
    {
        if (down)
        {
            _pressedRow = RowAt(_cursor);
            return _buttons.SetLeftButton(true);
        }

        DialogButton clicked = _buttons.SetLeftButton(false);
        int releasedRow = RowAt(_cursor);

        // The row changes only when the press and the release both landed on it, the same rule the
        // buttons follow. The left half steps back, the right half steps forward.
        if (_pressedRow >= 0 && releasedRow == _pressedRow && _cursor is { } cursor)
        {
            Rect row = OptionArea(_pressedRow);
            _options[_pressedRow].Move(cursor.X < row.X + (row.Width / 2) ? -1 : 1);
        }

        _pressedRow = -1;

        return clicked;
    }

    /// <summary>Closes the dialog without applying the changes, the Escape key of the reference.</summary>
    /// <returns>Always <see cref="DialogButton.Cancel"/>.</returns>
    public DialogButton Cancel()
    {
        _pressedRow = -1;
        return DialogButton.Cancel;
    }

    /// <summary>Answers the OK and CANCEL hot keys, Enter included, through the button row.</summary>
    /// <param name="hotKey">Hot key that was pressed.</param>
    /// <returns>Value of the button the key stands for, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton ProcessHotKey(ButtonHotKey hotKey) => _buttons.ProcessHotKey(hotKey);

    /// <summary>Index of the row under <paramref name="point"/>, or -1 when there is none.</summary>
    private int RowAt(Point? point)
    {
        if (point is not { } position)
        {
            return -1;
        }

        for (int index = 0; index < _options.Length; ++index)
        {
            if (OptionArea(index).Contains(position))
            {
                return index;
            }
        }

        return -1;
    }
}
