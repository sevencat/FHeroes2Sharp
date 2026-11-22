using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
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
/// A row is a label on the left, its value on the right and a chevron on both sides: pressing the
/// left half of a row selects the previous value, the right half the next one. The chevrons are
/// drawn as pixel lines (<see cref="ChevronWidth"/>) because the <c>&lt;</c> and <c>&gt;</c> glyphs
/// of <c>FONT.ICN</c> are three by four pixels and unreadable at that size. Both texts are clipped
/// at <see cref="RowPadding"/>, so a translation wider than the window is cut off inside the frame
/// instead of running over the label.
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

    /// <summary>
    /// Width of the chevron that brackets a value, in logical pixels. The reference dialog uses
    /// the arrow buttons of an <c>OptionButtonGroup</c>; the port has no artwork for them, so the
    /// arrows are drawn as two pixel lines instead.
    /// </summary>
    public const int ChevronWidth = 4;

    /// <summary>Height of a chevron: its two arms are 45 degrees, so it is <c>2 * width - 1</c>.</summary>
    public const int ChevronHeight = (2 * ChevronWidth) - 1;

    /// <summary>Gap between a chevron and the value it brackets, about one glyph of <c>FONT.ICN</c>.</summary>
    public const int ChevronGap = 6;

    /// <summary>
    /// Colour of the chevrons: index <c>10</c> of <c>KB.PAL</c>, the brightest step of the grey
    /// text ramp of <c>FONT.ICN</c>, i.e. the white the row text is drawn with.
    /// </summary>
    public const byte ChevronColor = 10;

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
    /// Area of the left chevron of row <paramref name="index"/>, i.e. the one that selects the
    /// previous value; <see cref="Rect.Empty"/> when the row is too narrow to hold the value and
    /// both chevrons.
    /// </summary>
    /// <param name="index">Zero based row index.</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no row at <paramref name="index"/>.</exception>
    public Rect LeftChevron(int index)
    {
        Rect row = OptionArea(index);
        if (!ChevronsFit(index))
        {
            return Rect.Empty;
        }

        int x = ValueX(index) - ChevronGap - ChevronWidth;

        return new Rect(x, ChevronTop(row), ChevronWidth, ChevronHeight);
    }

    /// <summary>
    /// Area of the right chevron of row <paramref name="index"/>, i.e. the one that selects the
    /// next value; <see cref="Rect.Empty"/> when the row is too narrow to hold the value and both
    /// chevrons.
    /// </summary>
    /// <param name="index">Zero based row index.</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no row at <paramref name="index"/>.</exception>
    public Rect RightChevron(int index)
    {
        Rect row = OptionArea(index);
        if (!ChevronsFit(index))
        {
            return Rect.Empty;
        }

        int x = row.Right - RowPadding - ChevronWidth;

        return new Rect(x, ChevronTop(row), ChevronWidth, ChevronHeight);
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
            Rect text = TextArea(index);

            GlyphText.DrawLine(_optionFont, target, new Point(text.X, row.Y), _options[index].Label, text);
            GlyphText.DrawLine(_optionFont, target, new Point(ValueX(index), row.Y), _options[index].Value, text);

            Rect left = LeftChevron(index);
            if (!left.IsEmpty)
            {
                DrawChevron(target, left, pointingLeft: true);
            }

            Rect right = RightChevron(index);
            if (!right.IsEmpty)
            {
                DrawChevron(target, right, pointingLeft: false);
            }
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

    /// <summary>
    /// Runs the dialog until OK or CANCEL closes it, the event loop of
    /// <c>dialog::openGameSettings()</c> (<c>dialog_gamesettings.cpp:224-260</c>).
    /// </summary>
    /// <remarks>
    /// The rows change on a click, so the caller reads <see cref="Option"/> for every row after OK and
    /// applies what changed, exactly like the reference does after its loop.
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit; it must already contain the scene behind.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// <see cref="DialogButton.Ok"/> or <see cref="DialogButton.Cancel"/>; <see cref="DialogButton.None"/>
    /// when the client was asked to exit before the player answered.
    /// </returns>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public DialogButton Show(PixelBuffer target, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "SettingsDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        DialogButton answer = DialogButton.None;
        bool leftDown = false;

        while (answer == DialogButton.None)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return DialogButton.None;
            }

            if (eventState.HasMouseMoved)
            {
                SetCursor(eventState.CursorPosition);
            }

            bool down = eventState.IsMouseLeftButtonPressed;

            if (down != leftDown)
            {
                leftDown = down;
                answer = SetLeftButton(down);
            }

            if (answer == DialogButton.None)
            {
                if (eventState.IsKeyPressed(Key.Enter) || eventState.IsKeyPressed(Key.Space))
                {
                    answer = ProcessHotKey(ButtonHotKey.Okay);
                }
                else if (eventState.IsKeyPressed(Key.Escape))
                {
                    answer = Cancel();
                }
            }

            Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return answer;
    }

    /// <summary>
    /// X of the value of row <paramref name="index"/>: right aligned inside the row, but never left
    /// of the padding. A value too wide for the row is clipped by <see cref="TextArea"/> at its right
    /// edge instead of running backwards over the label, which is what a long translation does.
    /// </summary>
    private int ValueX(int index)
    {
        Rect row = OptionArea(index);
        int right = row.Right - RowPadding;

        if (ChevronsFit(index))
        {
            right -= ChevronWidth + ChevronGap;
        }

        return Math.Max(right - GlyphText.Measure(_optionFont, _options[index].Value), row.X + RowPadding);
    }

    /// <summary>
    /// The band row <paramref name="index"/> paints its label and value in: the row without the
    /// horizontal paddings. Texts are clipped to it, so a translation that is wider than the window
    /// is cut off at the padding instead of overwriting the label or the frame.
    /// </summary>
    private Rect TextArea(int index)
    {
        Rect row = OptionArea(index);

        return new Rect(row.X + RowPadding, row.Y, Math.Max(0, row.Width - (2 * RowPadding)), row.Height);
    }

    /// <summary>
    /// Whether the value and both chevrons fit between the paddings of row
    /// <paramref name="index"/>. A narrow window keeps the value and drops the arrows, which is the
    /// same degradation the caller gets from a clipped label.
    /// </summary>
    private bool ChevronsFit(int index)
    {
        Rect row = OptionArea(index);
        int available = row.Width - (2 * RowPadding);
        int needed = GlyphText.Measure(_optionFont, _options[index].Value) + (2 * (ChevronWidth + ChevronGap));

        return available >= needed;
    }

    /// <summary>Y of a chevron: vertically centred on the row's line.</summary>
    private static int ChevronTop(in Rect row) => row.Y + ((row.Height - ChevronHeight) / 2);

    /// <summary>
    /// Draws one chevron into <paramref name="area"/>: two 45 degree arms of
    /// <see cref="ChevronWidth"/> pixels that meet at the tip, which is on the left for
    /// <paramref name="pointingLeft"/> and on the right otherwise.
    /// </summary>
    private static void DrawChevron(PixelBuffer target, in Rect area, bool pointingLeft)
    {
        int centreY = area.Y + (area.Height / 2);

        for (int step = 0; step < area.Width; ++step)
        {
            int x = pointingLeft ? area.X + step : area.Right - 1 - step;

            target[x, centreY - step] = ChevronColor;
            target[x, centreY + step] = ChevronColor;
        }
    }

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
