using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The language picker of the game: <c>fheroes2::selectLanguage()</c> of
/// <c>src/fheroes2/dialog/dialog_language_selection.cpp</c>, as a modal dialog.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="StandardWindow"/> holds the title, the list of language names, the name of the
/// selected language under the list and the standard OKAY/CANCEL row. Every row is written in its
/// own language (the reference switches the whole translation for each item before drawing it), so
/// the rows are plain strings chosen by the caller and not translated ids.
/// </para>
/// <para>
/// The geometry follows the reference: <see cref="ContentWidth"/> is the list plus its left margin
/// and the scroll bar band, the list starts <see cref="ListOffsetY"/> pixels below the top of the
/// content area, and the window always leaves room for <see cref="MinimumRows"/> rows even when the
/// game offers fewer languages - the original does the same, which is why a two language port gets
/// an eleven row window with two lines of text in it.
/// </para>
/// <para>
/// Two things of the reference are not ported: the scroll bar (<c>SCROLL.ICN</c> is not in the port
/// yet, so the list follows the selection instead of scrolling under a thumb) and the double click
/// that confirms the selection (the port has no double click timing). A click selects, OK or Enter
/// confirms, CANCEL or Escape drops the change.
/// </para>
/// <para>
/// Input and ownership mirror <see cref="SettingsDialog"/>: the scene feeds the cursor and the left
/// button and gets a <see cref="DialogButton"/> back. The dialog keeps the selection itself, so the
/// caller reads <see cref="SelectedIndex"/> after <see cref="DialogButton.Ok"/>.
/// </para>
/// </remarks>
public sealed class LanguageSelectionDialog
{
    /// <summary>Left margin of the list, <c>paddingLeftSide</c> of the reference.</summary>
    public const int ListPaddingLeft = 24;

    /// <summary>Width of the list; the rows are centred in it, <c>textAreaWidth</c>.</summary>
    public const int ListWidth = 270;

    /// <summary>Width the reference keeps for the scroll bar, <c>scrollBarAreaWidth</c>.</summary>
    public const int ScrollBarWidth = 48;

    /// <summary>Right margin the reference adds to the content width (<c>+ 3</c>).</summary>
    public const int RightMargin = 3;

    /// <summary>Width of the content area: list, its left margin and the scroll bar band.</summary>
    public const int ContentWidth = ListPaddingLeft + ListWidth + ScrollBarWidth + RightMargin;

    /// <summary>Distance from the top of the content area to the list, <c>listRoi.y - roi.y</c>.</summary>
    public const int ListOffsetY = 37;

    /// <summary>Height of the band the title is centred in, <c>verticalPaddingAreasHight</c>.</summary>
    public const int TitleBandHeight = 30;

    /// <summary>Gap between the top of the list area and its first row, <c>listAreaOffsetY</c>.</summary>
    public const int RowTopSlop = 3;

    /// <summary>Gap the reference leaves free below the last row (<c>listAreaHeightDeduction</c>).</summary>
    public const int RowBottomSlop = 4;

    /// <summary>Gap between the list and the band of the selected language name.</summary>
    public const int SelectedGap = 12;

    /// <summary>Height of the band the selected language name is centred in.</summary>
    public const int SelectedHeight = 21;

    /// <summary>Gap between the selected name band and the button row.</summary>
    public const int ButtonGap = 10;

    /// <summary>Rows the window leaves room for even when fewer languages exist.</summary>
    public const int MinimumRows = 11;

    /// <summary>Rows the window never exceeds, the upper bound of the <c>clamp</c> of the reference.</summary>
    public const int MaximumRows = 22;

    /// <summary>
    /// Height of the content area that is not taken by rows: the title band, the list paddings, the
    /// selected name band and the button band, the <c>119</c> the reference adds to the row height.
    /// </summary>
    public const int FixedHeight = ListOffsetY + RowTopSlop + RowBottomSlop + SelectedGap +
        SelectedHeight + ButtonGap + TitleBandHeight + 2;

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _rowFont;
    private readonly IGlyphSource _selectedFont;
    private readonly string[] _names;
    private readonly Rect _listArea;

    private int _selected;
    private int _scroll;
    private int _pressedRow = -1;
    private Point? _cursor;

    /// <summary>Composes the dialog inside an already laid out window.</summary>
    /// <param name="window">The stone window the dialog is drawn in.</param>
    /// <param name="buttons">Button row placed by <see cref="ButtonGroup.CreateStandard"/>.</param>
    /// <param name="titleFont">Font of the title, usually the yellow normal font.</param>
    /// <param name="rowFont">Font of the rows that are not selected, usually the white normal one.</param>
    /// <param name="selectedFont">Font of the selected row and name, usually the yellow normal one.</param>
    /// <param name="title">Translated title, e.g. <c>"Select Game Language:"</c>.</param>
    /// <param name="names">Language names in their own language, at least one.</param>
    /// <param name="selectedIndex">Index to select first; out of range values are clamped.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="names"/> is empty.</exception>
    public LanguageSelectionDialog(StandardWindow window, ButtonGroup buttons, IGlyphSource titleFont,
        IGlyphSource rowFont, IGlyphSource selectedFont, string title, IReadOnlyList<string> names,
        int selectedIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(rowFont);
        ArgumentNullException.ThrowIfNull(selectedFont);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(names);

        if (names.Count == 0)
        {
            throw new ArgumentException("A language list needs at least one name.", nameof(names));
        }

        _window = window;
        _buttons = buttons;
        _titleFont = titleFont;
        _rowFont = rowFont;
        _selectedFont = selectedFont;
        _names = [.. names];
        Title = title;
        _selected = Math.Clamp(selectedIndex, 0, _names.Length - 1);

        Rect area = window.ActiveArea;
        int listHeight = Math.Max(
            0, area.Height - (ListOffsetY + SelectedGap + SelectedHeight + ButtonGap + TitleBandHeight + 2));

        _listArea = new Rect(area.X + ListPaddingLeft, area.Y + ListOffsetY, ListWidth, listHeight);

        ScrollToSelection();
    }

    /// <summary>The window the dialog is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The standard button row of the dialog.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Translated title, drawn above the list.</summary>
    public string Title { get; }

    /// <summary>Number of languages to choose from.</summary>
    public int Count => _names.Length;

    /// <summary>The language names, as the caller passed them: each one in its own language.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>Index of the language that would be applied by <see cref="DialogButton.Ok"/>.</summary>
    public int SelectedIndex => _selected;

    /// <summary>The name of the selected language.</summary>
    public string SelectedName => _names[_selected];

    /// <summary>Line height of the rows, which is also the height of one row.</summary>
    public int LineHeight => _rowFont.LineHeight;

    /// <summary>Area the rows are drawn in: the list without the scroll bar band.</summary>
    public Rect ListArea => _listArea;

    /// <summary>Band the selected language name is centred in, below the list.</summary>
    public Rect SelectedArea =>
        new(_listArea.X, _listArea.Y + _listArea.Height + SelectedGap, ListWidth, SelectedHeight);

    /// <summary>Band the title is centred in, above the list.</summary>
    public Rect TitleArea => new(_listArea.X, _listArea.Y - TitleBandHeight, ListWidth, TitleBandHeight);

    /// <summary>Index of the first visible row, i.e. how far the list is scrolled.</summary>
    public int FirstVisibleIndex => _scroll;

    /// <summary>Number of rows that fit into the list area at once.</summary>
    public int VisibleRowCount => LineHeight <= 0
        ? 1
        : Math.Max(1, (_listArea.Height - RowTopSlop - RowBottomSlop) / LineHeight);

    /// <summary>Area of the row at <paramref name="index"/>.</summary>
    /// <param name="index">Zero based index of the language.</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no language at <paramref name="index"/>.</exception>
    public Rect RowArea(int index)
    {
        if ((uint)index >= (uint)_names.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The dialog has no such language.");
        }

        return new Rect(
            _listArea.X, _listArea.Y + RowTopSlop + ((index - _scroll) * LineHeight), ListWidth, LineHeight);
    }

    /// <summary>
    /// The row under <paramref name="cursor"/>, or <c>-1</c> when the cursor is not on a visible row.
    /// </summary>
    /// <param name="cursor">Cursor in logical pixels, or <see langword="null"/> when it left.</param>
    public int RowAt(Point? cursor)
    {
        if (cursor is not { } point)
        {
            return -1;
        }

        int last = Math.Min(_names.Length, _scroll + VisibleRowCount);

        for (int index = _scroll; index < last; ++index)
        {
            if (RowArea(index).Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Height a content area needs so that the list, the selected name and a button row fit, which is
    /// what a caller centres the window with.
    /// </summary>
    /// <param name="lineHeight">Line height of the rows.</param>
    /// <param name="count">Number of languages.</param>
    public static int GetRequiredHeight(int lineHeight, int count) =>
        (lineHeight * GetVisibleRows(count)) + FixedHeight;

    /// <summary>The same calculation for <see cref="Count"/> languages and the row font of this dialog.</summary>
    public int GetRequiredHeight() => GetRequiredHeight(LineHeight, Count);

    /// <summary>
    /// Number of rows the window shows room for: the reference clamps the language count to
    /// <see cref="MinimumRows"/>..<see cref="MaximumRows"/>, so the window does not jump by language.
    /// </summary>
    /// <param name="count">Number of languages.</param>
    public static int GetVisibleRows(int count) => Math.Clamp(count, MinimumRows, MaximumRows);

    /// <summary>Draws the window, the title, the list, the selected name and the buttons.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        DrawCentered(_titleFont, target, TitleArea, Title);

        int last = Math.Min(_names.Length, _scroll + VisibleRowCount);

        for (int index = _scroll; index < last; ++index)
        {
            IGlyphSource font = index == _selected ? _selectedFont : _rowFont;
            DrawCentered(font, target, RowArea(index), _names[index]);
        }

        DrawCentered(_selectedFont, target, SelectedArea, SelectedName);

        _buttons.Draw(target);
    }

    /// <summary>
    /// Records the cursor position and forwards it to the button row, so the pressed look of the
    /// buttons follows the mouse.
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
    /// Records the left mouse button. A press and a release inside the same row select that row; the
    /// return value comes from the button row, so only a click on OK or CANCEL closes the dialog.
    /// </summary>
    /// <param name="down">Whether the left mouse button is now held.</param>
    /// <returns>Value of the clicked button, <see cref="DialogButton.None"/> otherwise.</returns>
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
        // button row follows, so dragging off a row leaves the selection alone.
        if (_pressedRow >= 0 && releasedRow == _pressedRow)
        {
            Select(_pressedRow);
        }

        _pressedRow = -1;

        return clicked;
    }

    /// <summary>Selects the language at <paramref name="index"/> and scrolls it into view.</summary>
    /// <param name="index">Zero based index of the language.</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no language at <paramref name="index"/>.</exception>
    public void Select(int index)
    {
        if ((uint)index >= (uint)_names.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The dialog has no such language.");
        }

        _selected = index;
        ScrollToSelection();
    }

    /// <summary>
    /// Moves the selection by <paramref name="delta"/> languages, wrapping around like the arrow keys
    /// of the reference list box.
    /// </summary>
    /// <param name="delta">Number of languages to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        int count = _names.Length;
        Select((((_selected + delta) % count) + count) % count);
    }

    /// <summary>Closes the dialog without applying the selection, the Escape key of the reference.</summary>
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
    /// Draws <paramref name="text"/> centred in <paramref name="area"/>, clipped to it: a name wider
    /// than the list is cut off at the list edge, the same rule the settings rows follow.
    /// </summary>
    private static void DrawCentered(IGlyphSource font, PixelBuffer target, Rect area, string text)
    {
        int x = area.X + Math.Max(0, (area.Width - GlyphText.Measure(font, text)) / 2);

        GlyphText.DrawLine(font, target, new Point(x, area.Y), text, area);
    }

    /// <summary>
    /// Keeps the selected row inside the visible window, which is what the scroll bar of the
    /// reference would otherwise be needed for.
    /// </summary>
    private void ScrollToSelection()
    {
        int rows = VisibleRowCount;

        if (_selected < _scroll)
        {
            _scroll = _selected;
        }
        else if (_selected >= _scroll + rows)
        {
            _scroll = _selected - rows + 1;
        }

        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _names.Length - rows));
    }
}
