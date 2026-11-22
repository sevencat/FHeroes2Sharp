using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The scenario selection window of the main menu: the map list on the left, the details of the
/// selected map on the right and the row of map size filters on top, a port of
/// <c>Dialog::SelectScenario()</c> (<c>src/fheroes2/dialog/dialog_selectscenario.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The panel of <c>REQSBKG.ICN</c> is the whole window: it carries the frame, the five map size
/// buttons, the scroll bar groove, the boxes of the selected map and the OKAY button. Everything
/// that changes is painted on top - the nine visible rows, the details of the selection, the
/// description, the pressed map size button, the scroll bar thumb and the two scroll arrows. Every
/// coordinate of this class is relative to the panel, which is how the reference lays the dialog
/// out (<c>rt.x + …</c>), so the artwork and the layout cannot drift apart.
/// </para>
/// <para>
/// The list is filtered by map size, not sorted or searched: the four size buttons and the trailing
/// ALL button select which of the four buckets of <c>SelectScenario()</c> is shown. Switching to a
/// size without any map is refused - <see cref="SetFilter"/> returns <see langword="false"/>, which
/// is where the reference shows its "No maps exist at that size." message. The selection stays on
/// the same map when it survives the filter and falls back to the first row otherwise, the effective
/// behaviour of <c>ScenarioListBox::SelectMapSize()</c>.
/// </para>
/// <para>
/// Input follows the rest of the port: <see cref="SetCursor"/> and <see cref="SetLeftButton"/> for
/// the mouse, <see cref="ProcessHotKey"/> for the keyboard, and <see cref="MoveSelection"/> and
/// <see cref="MovePage"/> for the arrow and page keys the caller maps itself. Double clicking a row,
/// which the reference accepts as OK, is not ported: a click only selects.
/// </para>
/// </remarks>
public sealed class ScenarioSelectionDialog
{
    /// <summary>Offset of the list inside the panel, <c>SetAreaItems( { 55, 55, 270, 175 } )</c>.</summary>
    public const int ListOffsetX = 55;

    /// <summary>Vertical offset of the list inside the panel.</summary>
    public const int ListOffsetY = 55;

    /// <summary>Width of the list.</summary>
    public const int ListWidth = 270;

    /// <summary>Height of the list, <c>SCENARIO_LIST_COLUMN_HEIGHT</c> of the reference.</summary>
    public const int ListHeight = 175;

    /// <summary>Rows visible at once, <c>SetAreaMaxItems( 9 )</c>. It also shapes the whole dialog.</summary>
    public const int VisibleRowCount = 9;

    /// <summary>
    /// Average pitch of the rows. The rows are not evenly spaced - the reference lays row
    /// <c>i</c> out at <c>i * 175 / 9</c> - so <see cref="RowArea"/> truncates per row instead of
    /// multiplying by this value.
    /// </summary>
    public const int RowHeight = ListHeight / VisibleRowCount;

    /// <summary>
    /// Vertical padding of an icon inside its row. The reference blits the icons at the row top,
    /// only the name is nudged down (see <see cref="RowTextOffsetY"/>).
    /// </summary>
    public const int RowIconOffsetY = 0;

    /// <summary>Vertical padding of a name inside its row, <c>MAP_LIST_ROW_SPACING_Y - 1</c>.</summary>
    public const int RowTextOffsetY = MapRowSpacingY - 1;

    /// <summary><c>MAP_LIST_ROW_SPACING_Y</c>: the name sits three pixels below the row top.</summary>
    public const int MapRowSpacingY = 4;

    /// <summary>Offset of the player count icon of a row, <c>SCENARIO_LIST_COUNT_PLAYERS_OFFSET_X</c>.</summary>
    public const int RowPlayerIconOffsetX = 48;

    /// <summary>Offset of the map size icon of a row, <c>SCENARIO_LIST_MAP_SIZE_OFFSET_X</c>.</summary>
    public const int RowSizeIconOffsetX = 67;

    /// <summary>Offset of the map type icon of a row, <c>SCENARIO_LIST_MAP_TYPE_OFFSET_X</c>.</summary>
    public const int RowTypeIconOffsetX = 86;

    /// <summary>Left edge of the name column, <c>SCENARIO_LIST_MAP_NAME_OFFSET_X</c>.</summary>
    public const int RowNameOffsetX = 104;

    /// <summary>Width of the name column, <c>SCENARIO_LIST_MAP_NAME_WIDTH</c>.</summary>
    public const int RowNameWidth = 160;

    /// <summary>Offset of the victory condition icon of a row, <c>SCENARIO_LIST_VICTORY_CONDITION_OFFSET_X</c>.</summary>
    public const int RowVictoryIconOffsetX = 266;

    /// <summary>Offset of the loss condition icon of a row, <c>SCENARIO_LIST_LOSS_CONDITION_OFFSET_X</c>.</summary>
    public const int RowLossIconOffsetX = 285;

    /// <summary>Offset of the details of the selection, <c>SELECTED_SCENARIO_GENERAL_OFFSET_Y</c>.</summary>
    public const int SelectedOffsetY = 265;

    /// <summary>Offset of the player count icon of the selection, <c>SELECTED_SCENARIO_COUNT_PLAYERS_OFFSET_X</c>.</summary>
    public const int SelectedPlayerIconOffsetX = 45;

    /// <summary>Offset of the map size icon of the selection, <c>SELECTED_SCENARIO_MAP_SIZE_OFFSET_X</c>.</summary>
    public const int SelectedSizeIconOffsetX = 64;

    /// <summary>Offset of the map type icon of the selection, <c>SELECTED_SCENARIO_MAP_TYPE_OFFSET_X</c>.</summary>
    public const int SelectedTypeIconOffsetX = 83;

    /// <summary>Left edge of the selected map name, <c>SELECTED_SCENARIO_MAP_NAME_OFFSET_X</c>.</summary>
    public const int SelectedNameOffsetX = 108;

    /// <summary>Width of the selected map name, <c>SELECTED_SCENARIO_MAP_NAME_WIDTH</c>.</summary>
    public const int SelectedNameWidth = 160;

    /// <summary>Vertical nudge of the selected map name, the <c>+ 2</c> of the reference.</summary>
    public const int SelectedNameOffsetY = 2;

    /// <summary>Offset of the victory condition icon of the selection, <c>SELECTED_SCENARIO_VICTORY_CONDITION_OFFSET_X</c>.</summary>
    public const int SelectedVictoryIconOffsetX = 276;

    /// <summary>Offset of the loss condition icon of the selection, <c>SELECTED_SCENARIO_LOSS_CONDITION_OFFSET_X</c>.</summary>
    public const int SelectedLossIconOffsetX = 295;

    /// <summary>X the difficulty label is right aligned to, <c>dst.x + 210 - width</c> of the reference.</summary>
    public const int DifficultyLabelRight = 210;

    /// <summary>Left edge of the difficulty box, <c>SELECTED_SCENARIO_DIFFICULTY_OFFSET_X</c>.</summary>
    public const int DifficultyOffsetX = 220;

    /// <summary>Top edge of the difficulty box, <c>SELECTED_SCENARIO_DIFFICULTY_OFFSET_Y</c>.</summary>
    public const int DifficultyOffsetY = 293;

    /// <summary>Width of the difficulty box, <c>SELECTED_SCENARIO_DIFFICULTY_WIDTH</c>.</summary>
    public const int DifficultyWidth = 114;

    /// <summary>Left edge of the description box, <c>SELECTED_SCENARIO_DESCRIPTION_OFFSET_X</c>.</summary>
    public const int DescriptionOffsetX = 42;

    /// <summary>Top edge of the description box, <c>SELECTED_SCENARIO_DESCRIPTION_OFFSET_Y</c>.</summary>
    public const int DescriptionOffsetY = 318;

    /// <summary>Width of the description box, <c>SELECTED_SCENARIO_DESCRIPTION_BOX_WIDTH</c>.</summary>
    public const int DescriptionWidth = 292;

    /// <summary>Height of the description box, <c>SELECTED_SCENARIO_DESCRIPTION_HEIGHT</c>.</summary>
    public const int DescriptionHeight = 90;

    /// <summary>Horizontal padding of the description text, the <c>- 8</c> of <c>fitToArea()</c>.</summary>
    public const int DescriptionPaddingX = 4;

    /// <summary>Vertical padding of the description text, the <c>+ 3</c> of the reference.</summary>
    public const int DescriptionPaddingY = 3;

    /// <summary>Offset of the first map size button, <c>buttonSelectSmall</c> of the reference.</summary>
    public const int FilterOffsetX = 36;

    /// <summary>Vertical offset of the map size buttons, <c>MAP_SIZE_BUTTON_OFFSET_Y</c>.</summary>
    public const int FilterOffsetY = 23;

    /// <summary>Horizontal pitch of the map size buttons: 56 pixel frames 6 pixels apart.</summary>
    public const int FilterStepX = 62;

    /// <summary>Offset of the "scroll up" arrow, <c>SetScrollButtonUp( …, { rt.x + 327, rt.y + 55 } )</c>.</summary>
    public const int ScrollUpOffsetX = 327;

    /// <summary>Vertical offset of the "scroll up" arrow.</summary>
    public const int ScrollUpOffsetY = 55;

    /// <summary>Vertical offset of the "scroll down" arrow.</summary>
    public const int ScrollDownOffsetY = 217;

    /// <summary>Left edge of the scroll bar groove, <c>setScrollBarArea( { rt.x + 328, rt.y + 73, 12, 140 } )</c>.</summary>
    public const int ScrollBarOffsetX = 328;

    /// <summary>Top edge of the scroll bar groove.</summary>
    public const int ScrollBarOffsetY = 73;

    /// <summary>Width of the scroll bar groove.</summary>
    public const int ScrollBarWidth = 12;

    /// <summary>Height of the scroll bar groove, the <c>140</c> of <c>generateScrollbarSlider()</c>.</summary>
    public const int ScrollBarHeight = 140;

    /// <summary>Offset of the OKAY button, <c>fheroes2::Button buttonOk( rt.x + 140, rt.y + 410, … )</c>.</summary>
    public const int OkayOffsetX = 140;

    /// <summary>Vertical offset of the OKAY button.</summary>
    public const int OkayOffsetY = 410;

    /// <summary>How far the shadow hangs below and to the left of the panel, <c>fheroes2::shadowWidthPx</c>.</summary>
    public const int ShadowOffset = UiConstants.ShadowWidthPx;

    /// <summary>
    /// The five map size filters in the order of the buttons; <see cref="MapSize.None"/> is the
    /// "ALL" bucket, which is what <c>Maps::ZERO</c> means in the reference.
    /// </summary>
    public static IReadOnlyList<MapSize> Filters { get; } =
    [
        MapSize.Small,
        MapSize.Medium,
        MapSize.Large,
        MapSize.ExtraLarge,
        MapSize.None,
    ];

    private readonly ScenarioDialogArtwork _artwork;
    private readonly IGlyphSource _font;
    private readonly IGlyphSource _selectedFont;
    private readonly ScenarioDialogText _text;
    private readonly IReadOnlyList<Mp2FileInfo> _allMaps;
    private readonly ButtonGroup _buttons = new();
    private readonly ButtonSprite?[] _filterButtons = new ButtonSprite?[ScenarioDialogArtwork.FilterButtonCount];
    private readonly ButtonSprite? _scrollUp;
    private readonly ButtonSprite? _scrollDown;
    private readonly Rect _area;

    private List<Mp2FileInfo> _maps;
    private MapSize _filter;
    private int _selected;
    private int _scroll;
    private Point? _cursor;
    private Point? _pressPosition;
    private bool _leftHeld;
    private bool _thumbDragged;

    /// <summary>Composes the dialog over the given map list.</summary>
    /// <param name="artwork">The panel, the icons and the button frames the dialog is drawn with.</param>
    /// <param name="font">Font of the white texts, normally <c>GameFonts.Normal</c>.</param>
    /// <param name="selectedFont">Font of the current row, normally <c>GameFonts.Yellow</c>.</param>
    /// <param name="text">The translated labels of the dialog.</param>
    /// <param name="maps">Every map the game may offer; their order is the order of the list.</param>
    /// <param name="filter">Initial size filter; <see cref="MapSize.None"/> shows every map.</param>
    /// <param name="screen">Screen the panel is centred on; 640x480 when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/>, <paramref name="font"/>, <paramref name="selectedFont"/>,
    /// <paramref name="text"/> or <paramref name="maps"/> is <see langword="null"/>.
    /// </exception>
    public ScenarioSelectionDialog(
        ScenarioDialogArtwork artwork,
        IGlyphSource font,
        IGlyphSource selectedFont,
        ScenarioDialogText text,
        IReadOnlyList<Mp2FileInfo> maps,
        MapSize filter = MapSize.None,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(selectedFont);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(maps);

        Size surface = screen ?? ScreenBuffer.DefaultSize;

        _artwork = artwork;
        _font = font;
        _selectedFont = selectedFont;
        _text = text;
        _allMaps = maps;
        _area = new Rect(
            (surface.Width - artwork.Panel.Width) / 2,
            (surface.Height - artwork.Panel.Height) / 2,
            artwork.Panel.Width,
            artwork.Panel.Height);

        for (int index = 0; index < _filterButtons.Length; ++index)
        {
            if (artwork.FilterButton(index) is { } frames)
            {
                _filterButtons[index] = new ButtonSprite(
                    frames.Released,
                    frames.Pressed,
                    offsetX: _area.X + FilterOffsetX + (index * FilterStepX),
                    offsetY: _area.Y + FilterOffsetY);
            }
        }

        if (artwork.ScrollUpButton is { } up)
        {
            _scrollUp = new ButtonSprite(
                up.Released, up.Pressed, offsetX: _area.X + ScrollUpOffsetX, offsetY: _area.Y + ScrollUpOffsetY);
        }

        if (artwork.ScrollDownButton is { } down)
        {
            _scrollDown = new ButtonSprite(
                down.Released, down.Pressed, offsetX: _area.X + ScrollUpOffsetX,
                offsetY: _area.Y + ScrollDownOffsetY);
        }

        if (artwork.OkayButton is { } okay)
        {
            _buttons.Add(
                new ButtonSprite(
                    okay.Released, okay.Pressed, offsetX: _area.X + OkayOffsetX, offsetY: _area.Y + OkayOffsetY),
                DialogButton.Ok);
        }

        _filter = filter;
        _maps = BuildList(filter);
    }

    /// <summary>Area the panel covers on the screen, shadow excluded.</summary>
    public Rect Area => _area;

    /// <summary>Maps the list currently shows, in list order.</summary>
    public IReadOnlyList<Mp2FileInfo> Maps => _maps;

    /// <summary>Size filter of the list; <see cref="MapSize.None"/> shows every map.</summary>
    public MapSize Filter => _filter;

    /// <summary>Index of the selected row inside <see cref="Maps"/>; 0 for an empty list.</summary>
    public int SelectedIndex => _selected;

    /// <summary>Index of the first visible row; the selection is always one of the nine rows.</summary>
    public int Scroll => _scroll;

    /// <summary>The selected map, or <see langword="null"/> when the list is empty.</summary>
    public Mp2FileInfo? Selected => _selected >= 0 && _selected < _maps.Count ? _maps[_selected] : null;

    /// <summary>Click area of the visible row <paramref name="row"/> (0…8), the columns excluded.</summary>
    /// <param name="row">Zero based index of the visible row.</param>
    /// <remarks>
    /// <para>
    /// Row <c>i</c> starts at <c>i * 175 / 9</c> - the truncation of <c>ListBox::Redraw()</c>, which
    /// spreads nine 19.44 pixel rows over the 175 pixel column instead of using a fixed pitch of 19.
    /// Using a fixed pitch drifts three pixels by the last row and makes the click area disagree
    /// with what the player sees.
    /// </para>
    /// <para>
    /// This is the hit area of <c>SetAreaItems( { rt.x + 55, rt.y + 55, 270, 175 } )</c>, so its x
    /// is the list edge. The <b>drawn</b> columns of a row are not: the reference blits them at
    /// <c>ScenarioListBox::_offsetX</c>, which is the dialog's x, so paint them at
    /// <see cref="_area"/>.X and take only the row's y from here.
    /// </para>
    /// </remarks>
    public Rect RowArea(int row)
    {
        int top = row * ListHeight / VisibleRowCount;
        int bottom = (row + 1) * ListHeight / VisibleRowCount;

        return new Rect(_area.X + ListOffsetX, _area.Y + ListOffsetY + top, ListWidth, bottom - top);
    }

    /// <summary>Area of the map size button <paramref name="index"/> (0…4).</summary>
    /// <param name="index">Zero based index into <see cref="Filters"/>.</param>
    public Rect FilterArea(int index) =>
        index >= 0 && index < _filterButtons.Length && _filterButtons[index] is { } button
            ? new Rect(button.OffsetX, button.OffsetY, button.Released.Width, button.Released.Height)
            : Rect.Empty;

    /// <summary>Area of the OKAY button, empty when the artwork does not have one.</summary>
    public Rect OkayArea => _buttons.Count > 0 ? _buttons[0].Area : Rect.Empty;

    /// <summary>Area of the scroll bar groove the thumb moves in.</summary>
    public Rect ScrollBarArea =>
        new(_area.X + ScrollBarOffsetX, _area.Y + ScrollBarOffsetY, ScrollBarWidth, ScrollBarHeight);

    /// <summary>
    /// Area of the whole list, <c>SetAreaItems( { rt.x + 55, rt.y + 55, 270, 175 } )</c>: the rows
    /// and the wheel target, the columns of a row excluded.
    /// </summary>
    public Rect ListArea => new(_area.X + ListOffsetX, _area.Y + ListOffsetY, ListWidth, ListHeight);

    /// <summary>
    /// Shows only the maps of <paramref name="filter"/>; the selection moves to the first row when
    /// the current map is filtered out. A filter without any map is refused, which is the point
    /// where the reference shows its "No maps exist at that size." message.
    /// </summary>
    /// <param name="filter">Size to show, <see cref="MapSize.None"/> for all maps.</param>
    /// <returns><see langword="true"/> when the filter changed, <see langword="false"/> when the
    /// list is already filtered that way or that size has no map.</returns>
    public bool SetFilter(MapSize filter)
    {
        if (filter == _filter)
        {
            return false;
        }

        List<Mp2FileInfo> candidate = BuildList(filter);
        if (candidate.Count == 0)
        {
            return false;
        }

        Mp2FileInfo? keep = Selected;

        _maps = candidate;
        _filter = filter;
        _selected = keep is null ? 0 : Math.Max(0, candidate.IndexOf(keep));
        _scroll = 0;
        EnsureSelectionVisible();

        return true;
    }

    /// <summary>Selects the row <paramref name="index"/>, clamped to the list, and scrolls to it.</summary>
    /// <param name="index">Index inside <see cref="Maps"/>.</param>
    public void Select(int index)
    {
        _selected = Math.Clamp(index, 0, Math.Max(0, _maps.Count - 1));
        EnsureSelectionVisible();
    }

    /// <summary>Moves the selection by <paramref name="delta"/> rows, clamped to the list.</summary>
    /// <param name="delta">Negative to go up, positive to go down.</param>
    public void MoveSelection(int delta) => Select(_selected + delta);

    /// <summary>Moves the selection by <paramref name="pages"/> pages of nine rows.</summary>
    /// <param name="pages">Negative to go up, positive to go down.</param>
    public void MovePage(int pages) => Select(_selected + (pages * VisibleRowCount));

    /// <summary>Rebuilds the filtered list without touching the selection.</summary>
    private List<Mp2FileInfo> BuildList(MapSize filter)
    {
        List<Mp2FileInfo> maps = [];

        foreach (Mp2FileInfo map in _allMaps)
        {
            if (filter == MapSize.None || map.Width == MapSizes.EdgeLength(filter))
            {
                maps.Add(map);
            }
        }

        return maps;
    }

    /// <summary>Scrolls the list so that the selected row is one of the visible nine.</summary>
    private void EnsureSelectionVisible()
    {
        if (_selected < _scroll)
        {
            _scroll = _selected;
        }
        else if (_selected >= _scroll + VisibleRowCount)
        {
            _scroll = _selected - VisibleRowCount + 1;
        }

        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _maps.Count - VisibleRowCount));
    }

    /// <summary>
    /// Records the cursor. The pressed looks of the buttons follow immediately, even without a
    /// mouse button event, which is what <see cref="ButtonGroup"/> does for its own buttons.
    /// </summary>
    /// <param name="cursor">Cursor position in logical pixels, or <see langword="null"/>.</param>
    public void SetCursor(Point? cursor)
    {
        if (_cursor == cursor)
        {
            return;
        }

        _cursor = cursor;
        _buttons.SetCursor(cursor);

        // A drag of the thumb moves without a button event: the motion is the only position source,
        // exactly like the reference, which calls moveToPos() from its mouse loop while the button
        // is held.
        if (_thumbDragged && _leftHeld && cursor is { } point)
        {
            ScrollToCursor(point);
        }
    }

    /// <summary>
    /// Records the left mouse button and resolves the click when it is released: on the OKAY button
    /// it returns <see cref="DialogButton.Ok"/>, on a map size button or a row it changes the dialog
    /// and returns <see cref="DialogButton.None"/>.
    /// </summary>
    /// <param name="down">Whether the left mouse button is now held.</param>
    /// <returns>The value of a clicked dialog button, <see cref="DialogButton.None"/> otherwise.</returns>
    public DialogButton SetLeftButton(bool down)
    {
        _buttons.SetCursor(_cursor);
        DialogButton clicked = _buttons.SetLeftButton(down);

        if (down)
        {
            _leftHeld = true;
            _pressPosition ??= _cursor;

            // The reference calls moveToPos() as soon as the groove is pressed: a click next to the
            // thumb jumps there and a press on the thumb starts the drag.
            if (!_thumbDragged && _cursor is { } grab && IsScrollBarHit(grab))
            {
                _thumbDragged = true;
                ScrollToCursor(grab);
            }

            return DialogButton.None;
        }

        _leftHeld = false;

        if (_thumbDragged)
        {
            // Releasing a drag must not click the row that happens to be under the cursor.
            _thumbDragged = false;
            _pressPosition = null;
            return clicked;
        }

        Point? pressPosition = _pressPosition;
        _pressPosition = null;

        if (clicked != DialogButton.None || pressPosition is not { } press || _cursor is not { } cursor)
        {
            return clicked;
        }

        if (FilterAt(press) is var filterIndex and >= 0 && FilterAt(cursor) == filterIndex)
        {
            SetFilter(Filters[filterIndex]);
            return DialogButton.None;
        }

        // The arrows of the reference move _topId only (see the PgUp / PgDn branch of
        // ListBox::ActionListPress): a click rolls the view one row and leaves the selection alone,
        // exactly like the wheel and the thumb.
        if (IsInside(_scrollUp, press) && IsInside(_scrollUp, cursor))
        {
            ScrollRows(-1);
            return DialogButton.None;
        }

        if (IsInside(_scrollDown, press) && IsInside(_scrollDown, cursor))
        {
            ScrollRows(1);
            return DialogButton.None;
        }

        int row = RowAt(press);

        if (row >= 0 && row == RowAt(cursor))
        {
            Select(_scroll + row);
        }

        return DialogButton.None;
    }

    /// <summary>
    /// Rolls the list by <paramref name="rows"/> rows without touching the selection - the mouse
    /// wheel of the reference, which moves <c>_topId</c> only. Only a wheel over the list or over
    /// the scroll bar counts, which is the <c>isMouseWheelInArea</c> check of the reference.
    /// </summary>
    /// <param name="rows">Rows to scroll down; negative scrolls up.</param>
    public void ScrollBy(int rows)
    {
        if (rows == 0 || _maps.Count <= VisibleRowCount)
        {
            return;
        }

        if (_cursor is { } cursor && !ListArea.Contains(cursor) && !ScrollBarArea.Contains(cursor))
        {
            return;
        }

        ScrollRows(rows);
    }

    /// <summary>Moves the first visible row by <paramref name="rows"/>, clamped to the list.</summary>
    /// <param name="rows">Rows to scroll down; negative scrolls up.</param>
    private void ScrollRows(int rows)
    {
        int steps = _maps.Count - VisibleRowCount;

        if (rows == 0 || steps <= 0)
        {
            return;
        }

        _scroll = Math.Clamp(_scroll + rows, 0, steps);
    }

    /// <summary>
    /// Answers the OK and CANCEL keys. CANCEL is handled here because the dialog owns a single
    /// button, and a group with one OK button would answer the cancel key with OK.
    /// </summary>
    /// <param name="hotKey">Hot key that was pressed.</param>
    /// <returns>The value of the button the key stands for, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton ProcessHotKey(ButtonHotKey hotKey) =>
        hotKey == ButtonHotKey.Cancel ? DialogButton.Cancel : _buttons.ProcessHotKey(hotKey);

    /// <summary>Index of the map size button under <paramref name="point"/>, or -1.</summary>
    private int FilterAt(Point point)
    {
        for (int index = 0; index < _filterButtons.Length; ++index)
        {
            if (FilterArea(index).Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Index of the visible row under <paramref name="point"/>, or -1.</summary>
    private int RowAt(Point point)
    {
        for (int row = 0; row < VisibleRowCount; ++row)
        {
            if (_scroll + row < _maps.Count && RowArea(row).Contains(point))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>Whether <paramref name="point"/> is inside the area of <paramref name="button"/>.</summary>
    private static bool IsInside(ButtonBase? button, Point point) => button is not null && button.Area.Contains(point);

    /// <summary>Whether a press on <paramref name="point"/> grabs the scroll bar.</summary>
    private bool IsScrollBarHit(Point point) =>
        _maps.Count > VisibleRowCount && (_scrollUp is not null || _scrollDown is not null || _artwork.Slider is not null)
        && ScrollBarArea.Contains(point);

    /// <summary>
    /// Moves the list so that the thumb centres on <paramref name="point"/>, a port of
    /// <c>Scrollbar::moveToPos</c>: the index is the pressed offset inside the free travel of the
    /// groove, rounded to the nearest row.
    /// </summary>
    private void ScrollToCursor(Point point)
    {
        int steps = _maps.Count - VisibleRowCount;

        if (steps <= 0)
        {
            _scroll = 0;
            return;
        }

        Rect groove = ScrollBarArea;
        int thumbLength = ThumbLength();
        int travel = groove.Height - thumbLength;

        if (travel <= 0)
        {
            _scroll = 0;
            return;
        }

        int offset = Math.Clamp(point.Y - (thumbLength / 2) - groove.Y, 0, travel);
        _scroll = ((steps * offset) + (travel / 2)) / travel;
    }

    /// <summary>Length of the thumb along the groove, the height <see cref="CreateThumb"/> produces.</summary>
    private int ThumbLength() => CreateThumb()?.Height ?? ScrollBarHeight;

    /// <summary>
    /// Builds the thumb of the scroll bar, or <see langword="null"/> when the list fits - the same
    /// artwork <see cref="ScrollSlider.Create"/> gives the painter, so the drag uses the length that
    /// is on screen.
    /// </summary>
    private Sprite? CreateThumb()
    {
        if (_artwork.Slider is not { } slider || _maps.Count <= VisibleRowCount)
        {
            return null;
        }

        return ScrollSlider.Create(
            slider,
            horizontal: false,
            ScrollBarHeight,
            VisibleRowCount,
            _maps.Count,
            new Rect(0, 0, slider.Width, 8),
            new Rect(0, 7, slider.Width, 8));
    }

    /// <summary>
    /// Paints the whole dialog: the shadow, the panel, the nine rows, the details of the selection,
    /// the scroll bar thumb and the buttons. The panel is redrawn from scratch, so a caller only has
    /// to present the result - there is nothing to restore.
    /// </summary>
    /// <param name="target">Frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_artwork.Shadow is { } shadow)
        {
            ImageOps.BlitSprite(target, new Point(_area.X - ShadowOffset, _area.Y + ShadowOffset), shadow);
        }

        ImageOps.BlitSprite(target, _area.Position, _artwork.Panel);

        DrawRows(target);
        DrawSelectedScenario(target);
        DrawScrollBar(target);
        DrawButtons(target);
    }

    /// <summary>Paints the nine visible rows, the selected one in the yellow font.</summary>
    private void DrawRows(PixelBuffer target)
    {
        for (int row = 0; row < VisibleRowCount; ++row)
        {
            int index = _scroll + row;

            if (index >= _maps.Count)
            {
                return;
            }

            Rect area = RowArea(row);
            Mp2FileInfo map = _maps[index];

            // The reference measures the x of a row from the panel edge (ScenarioListBox::_offsetX
            // is the dialog position) and only the y from the list area. Adding ListOffsetX to the
            // x as well shifts every column 55 pixels to the right, which pushes the condition
            // icons onto the scroll bar.
            DrawIcons(
                target,
                map,
                new Point(_area.X, area.Y + RowIconOffsetY),
                RowPlayerIconOffsetX,
                RowSizeIconOffsetX,
                RowTypeIconOffsetX,
                RowVictoryIconOffsetX,
                RowLossIconOffsetX);

            DrawCenteredText(
                target,
                map.Name,
                index == _selected ? _selectedFont : _font,
                new Rect(_area.X + RowNameOffsetX, area.Y + RowTextOffsetY, RowNameWidth, _font.LineHeight));
        }
    }

    /// <summary>Paints the details of the selected map: icons, name, difficulty and description.</summary>
    private void DrawSelectedScenario(PixelBuffer target)
    {
        if (Selected is not { } map)
        {
            return;
        }

        Point origin = new(_area.X, _area.Y + SelectedOffsetY);

        DrawIcons(
            target,
            map,
            origin,
            SelectedPlayerIconOffsetX,
            SelectedSizeIconOffsetX,
            SelectedTypeIconOffsetX,
            SelectedVictoryIconOffsetX,
            SelectedLossIconOffsetX);

        DrawCenteredText(
            target,
            map.Name,
            _font,
            new Rect(_area.X + SelectedNameOffsetX, origin.Y + SelectedNameOffsetY, SelectedNameWidth,
                _font.LineHeight));

        string label = _text.DifficultyLabel;
        int labelWidth = GlyphText.Measure(_font, label);

        GlyphText.DrawLine(
            _font,
            target,
            new Point(_area.X + DifficultyLabelRight - labelWidth, _area.Y + DifficultyOffsetY),
            label,
            _area);

        DrawCenteredText(
            target,
            _text.Difficulty(map.Difficulty),
            _font,
            new Rect(_area.X + DifficultyOffsetX, _area.Y + DifficultyOffsetY, DifficultyWidth,
                _font.LineHeight));

        DrawDescription(target, map.Description);
    }

    /// <summary>Paints the description inside its box, wrapping it and dropping what does not fit.</summary>
    private void DrawDescription(PixelBuffer target, string description)
    {
        Rect box = new(
            _area.X + DescriptionOffsetX,
            _area.Y + DescriptionOffsetY,
            DescriptionWidth,
            DescriptionHeight);
        Rect textArea = new(
            box.X + DescriptionPaddingX,
            box.Y + DescriptionPaddingY,
            box.Width - (2 * DescriptionPaddingX),
            box.Height - DescriptionPaddingY);

        List<string> lines = GlyphText.WrapLines(_font, description, textArea.Width);
        int lineHeight = _font.LineHeight;

        for (int index = 0; index < lines.Count; ++index)
        {
            int y = textArea.Y + (index * lineHeight);

            if (y + lineHeight > textArea.Y + textArea.Height)
            {
                return;
            }

            GlyphText.DrawLine(_font, target, new Point(textArea.X, y), lines[index], textArea);
        }
    }

    /// <summary>Paints the five icons of a map, each column at its own offset from <paramref name="origin"/>.</summary>
    private void DrawIcons(
        PixelBuffer target,
        Mp2FileInfo map,
        Point origin,
        int playerOffsetX,
        int sizeOffsetX,
        int typeOffsetX,
        int victoryOffsetX,
        int lossOffsetX)
    {
        DrawIcon(target, _artwork.PlayerCountIcon(map.PlayerCount), new Point(origin.X + playerOffsetX, origin.Y));
        DrawIcon(
            target, _artwork.MapSizeIcon(MapSizes.FromWidth(map.Width)), new Point(origin.X + sizeOffsetX, origin.Y));
        DrawIcon(target, _artwork.MapTypeIcon(map.Version), new Point(origin.X + typeOffsetX, origin.Y));
        DrawIcon(
            target, _artwork.VictoryIcon(map.VictoryCondition), new Point(origin.X + victoryOffsetX, origin.Y));
        DrawIcon(target, _artwork.LossIcon(map.LossCondition), new Point(origin.X + lossOffsetX, origin.Y));
    }

    /// <summary>Blits <paramref name="icon"/> when the artwork has it.</summary>
    private static void DrawIcon(PixelBuffer target, Sprite? icon, Point position)
    {
        if (icon is not null)
        {
            ImageOps.BlitSprite(target, position, icon);
        }
    }

    /// <summary>Paints <paramref name="text"/> centred inside <paramref name="area"/>, clipped to it.</summary>
    private static void DrawCenteredText(PixelBuffer target, string text, IGlyphSource font, in Rect area)
    {
        int width = GlyphText.Measure(font, text);
        int x = area.X + (area.Width > width ? (area.Width - width) / 2 : 0);

        GlyphText.DrawLine(font, target, new Point(x, area.Y), text, area);
    }

    /// <summary>
    /// Paints the map size buttons, the two scroll arrows and the OKAY button. A map size button
    /// stays pressed while it is the active filter, which is how the reference shows the selection.
    /// </summary>
    private void DrawButtons(PixelBuffer target)
    {
        for (int index = 0; index < _filterButtons.Length; ++index)
        {
            ButtonSprite? button = _filterButtons[index];

            if (button is null)
            {
                continue;
            }

            UpdatePressedState(button, sticky: _filter == Filters[index]);
            button.Draw(target);
        }

        UpdatePressedState(_scrollUp, sticky: false);
        UpdatePressedState(_scrollDown, sticky: false);

        if (_scrollUp is not null)
        {
            _scrollUp.Draw(target);
        }

        if (_scrollDown is not null)
        {
            _scrollDown.Draw(target);
        }

        for (int index = 0; index < _buttons.Count; ++index)
        {
            _buttons[index].Draw(target);
        }
    }

    /// <summary>
    /// Looks pressed while <paramref name="sticky"/> is set or while the left mouse button is held
    /// inside the button, <c>drawOnState()</c> of the reference.
    /// </summary>
    private void UpdatePressedState(ButtonSprite? button, bool sticky)
    {
        if (button is null)
        {
            return;
        }

        bool held = _pressPosition is { } press
            && _cursor is { } cursor
            && button.Area.Contains(press)
            && button.Area.Contains(cursor);

        if (sticky || held)
        {
            button.Press();
        }
        else
        {
            button.Release();
        }
    }

    /// <summary>Paints the thumb of the scroll bar when the list is longer than the dialog.</summary>
    private void DrawScrollBar(PixelBuffer target)
    {
        if (CreateThumb() is not { } thumb)
        {
            return;
        }

        Rect groove = ScrollBarArea;

        Point position = ScrollSlider.SliderPosition(
            groove, thumb.Height, _scroll, VisibleRowCount, _maps.Count, horizontal: false);

        ImageOps.BlitSprite(target, new Point(position.X + ((groove.Width - thumb.Width) / 2), position.Y), thumb);
    }
}
