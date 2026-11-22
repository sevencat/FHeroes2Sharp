using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>One row of the hot key list: an event and the group it belongs to.</summary>
/// <param name="Event">The event the row binds a key to.</param>
/// <param name="Category">Group of the event, used by the category column and the popup.</param>
public sealed record HotKeyRow(HotKeyEvent Event, HotKeyCategory Category);

/// <summary>Artwork of <see cref="HotKeysDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// <see cref="ScrollBarFrames"/> is <c>SCROLL.ICN</c> (or <c>SCROLLE.ICN</c> for the evil interface) in the order the
/// reference uses it: 0 and 1 are the up button, 2 and 3 the down one, 4 the thumb. <see cref="ScrollBarBackground"/>
/// is the stone plate of <c>BOARD.ICN</c> the groove is stamped on.
/// </remarks>
public sealed class HotKeysArtwork
{
    /// <summary>Frames of the scroll bar, five in the reference order.</summary>
    public required IReadOnlyList<Sprite> ScrollBarFrames { get; init; }

    /// <summary>Plate behind the scroll bar groove.</summary>
    public required Sprite ScrollBarBackground { get; init; }

    /// <summary>Source of the OK button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether enough scroll bar frames were supplied.</summary>
    public bool IsComplete => ScrollBarFrames.Count >= 5;
}

/// <summary>
/// The hot key configuration window, <c>fheroes2::openHotkeysDialog()</c>
/// (<c>src/fheroes2/dialog/dialog_hotkeys.cpp:307-483</c>).
/// </summary>
/// <remarks>
/// <para>
/// A category column on the left («All Categories» first, then one line per group) and a two column list on the
/// right: the event name and the key it is bound to. Clicking a category filters the list; clicking a row that is
/// already selected — the port's double click, since <see cref="ListBox{T}"/> has no timing of its own — starts
/// waiting for a key, and the next key pressed becomes the binding. The reference shows that wait in a message box
/// with a custom element; the port keeps it in the window and swaps the title for a prompt, so no extra window has to
/// be laid out.
/// </para>
/// <para>
/// The table itself lives in <see cref="GameHotKeys"/>; the dialog never saves a file. A changed binding sets
/// <see cref="IsDirty"/>, and the host writes <see cref="GameHotKeys.HotKeySave"/> out when the window closes — the
/// port has no config directory of its own, the same split as the hot key table itself.
/// </para>
/// <para>
/// Layout follows the reference: the three column widths start at 150/280/120 pixels and grow proportionally up to
/// what the longest texts need, but never past the screen width minus 150; the height is the screen height minus 100,
/// at least 480. There is no Cancel button, as in the reference: Escape closes the window and keeps what was set.
/// </para>
/// </remarks>
public sealed class HotKeysDialog : ListBox<HotKeyRow>
{
    /// <summary>Narrowest the event name column gets, <c>minKeyDescriptionWidth</c>.</summary>
    public const int MinKeyDescriptionWidth = 280;

    /// <summary>Narrowest the hot key column gets, <c>minHotKeyWidth</c>.</summary>
    public const int MinHotKeyWidth = 120;

    /// <summary>Narrowest the category column gets, <c>minCategoryWidth</c>.</summary>
    public const int MinCategoryWidth = 150;

    /// <summary>Width the window adds around the three columns, <c>dialogExtraWidth</c>.</summary>
    public const int DialogExtraWidth = 90;

    /// <summary>Reference width of a dialog screen, <c>Display::DEFAULT_WIDTH</c>.</summary>
    public const int DefaultDisplayWidth = 640;

    /// <summary>Reference height of a dialog screen, <c>Display::DEFAULT_HEIGHT</c>.</summary>
    public const int DefaultDisplayHeight = 480;

    /// <summary>Left inset of the lists inside the active area, <c>roi.x + 24</c>.</summary>
    public const int ListInsetX = 24;

    /// <summary>Top inset of the lists, <c>roi.y + 37</c>: the title sits above.</summary>
    public const int ListTop = 37;

    /// <summary>Height the lists leave below for the title and the buttons, <c>roi.height - 75</c>.</summary>
    public const int ListHeightDeduction = 75;

    /// <summary>Width the lists add to the three columns, <c>+ 16</c>.</summary>
    public const int ListExtraWidth = 16;

    /// <summary>Width of the category column beyond the text, <c>categoryWidth + 8</c>.</summary>
    public const int CategoryExtraWidth = 8;

    /// <summary>Width of the hot key column beyond the two texts, <c>keyDescriptionWidth + hotKeyWidth + 8</c>.</summary>
    public const int HotKeyExtraWidth = 8;

    /// <summary>Distance of the scroll bar from the right edge of the active area, <c>roi.width - 35</c>.</summary>
    public const int ScrollBarRightInset = 35;

    /// <summary>Height a scroll bar button takes at the top and the bottom, <c>19</c>.</summary>
    public const int ScrollBarButtonHeight = 19;

    /// <summary>Width of the scroll bar groove, <c>10</c>.</summary>
    public const int ScrollBarGrooveWidth = 10;

    /// <summary>Gap between two category lines, <c>+ 4</c> above the line height.</summary>
    public const int CategoryGap = 4;

    /// <summary>Gap between the top of the category column and its first line, <c>categoryRoi.y + 4</c>.</summary>
    public const int CategoryTopSlop = 4;

    /// <summary>Gap between a row top and its text, <c>offsetY += 2</c>.</summary>
    public const int RowTextOffsetY = 2;

    /// <summary>Gap between the event name column and the hot key column, <c>+ 9</c>.</summary>
    public const int KeyColumnGap = 9;

    /// <summary>Left margin of a row's event name, <c>offsetX + 4</c>.</summary>
    public const int RowTextInsetX = 4;

    /// <summary>Top inset of the title, <c>roi.y + 16</c>.</summary>
    public const int TitleTop = 16;

    /// <summary>Distance from the top of a list area to its first row, <c>+ 3</c>.</summary>
    public const int RowTopSlop = 3;

    /// <summary>Distance from the bottom of a list area to its last row, <c>- 4</c>.</summary>
    public const int RowBottomSlop = 4;

    private const string Ellipsis = "...";

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly HotKeysArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _rowFont;
    private readonly IGlyphSource _selectedFont;
    private readonly int _categoryWidth;
    private readonly int _keyDescriptionWidth;
    private readonly int _hotKeyWidth;
    private readonly Rect _categoryRoi;
    private readonly Rect _hotKeyRoi;
    private readonly int _scrollBarX;
    private readonly List<HotKeyRow> _allRows;
    private readonly List<HotKeyRow> _rows;
    private readonly List<CategoryEntry> _categories = [];

    private HotKeyCategory? _selectedCategory;
    private DialogPopup? _popup;
    private HotKeyEvent _waitingEvent = HotKeyEvent.None;

    private HotKeysDialog(Size screen, HotKeysArtwork artwork, IGlyphSource titleFont, IGlyphSource rowFont,
        IGlyphSource selectedFont, IReadOnlyList<HotKeyRow> rows, int categoryWidth, int keyDescriptionWidth,
        int hotKeyWidth)
        : base(new Point(0, 0))
    {
        _artwork = artwork;
        _titleFont = titleFont;
        _rowFont = rowFont;
        _selectedFont = selectedFont;
        _categoryWidth = categoryWidth;
        _keyDescriptionWidth = keyDescriptionWidth;
        _hotKeyWidth = hotKeyWidth;
        _allRows = [.. rows];
        _rows = [];

        int width = categoryWidth + keyDescriptionWidth + hotKeyWidth + DialogExtraWidth;
        int height = Math.Max(screen.Height - 100, DefaultDisplayHeight);

        _window = StandardWindow.CreateCentered(new Size(width, height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;

        _categoryRoi = new Rect(roi.X + ListInsetX, roi.Y + ListTop, categoryWidth + CategoryExtraWidth,
            roi.Height - ListHeightDeduction);
        _hotKeyRoi = new Rect(_categoryRoi.Right, _categoryRoi.Y,
            keyDescriptionWidth + hotKeyWidth + HotKeyExtraWidth, _categoryRoi.Height);

        // 参考：listRoi 宽为三列之和 + 16，比两块底板之和少 8 像素，留作两列之间的缝。
        SetAreaItems(new Rect(_hotKeyRoi.X, _hotKeyRoi.Y + RowTopSlop,
            _hotKeyRoi.Width - RowTopSlop, _hotKeyRoi.Height - RowBottomSlop));
        SetAreaMaxItems(Math.Max(1, (_hotKeyRoi.Height - 7) / Math.Max(1, rowFont.LineHeight)));

        _scrollBarX = roi.X + roi.Width - ScrollBarRightInset;
        Rect grooveArea = new(_scrollBarX + 2, _hotKeyRoi.Y + ScrollBarButtonHeight, ScrollBarGrooveWidth,
            _hotKeyRoi.Height - (ScrollBarButtonHeight * 2));

        SetScrollButtonUp(new ButtonSprite(artwork.ScrollBarFrames[0], artwork.ScrollBarFrames[1], null,
            _scrollBarX + 1, _hotKeyRoi.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollBarFrames[2], artwork.ScrollBarFrames[3], null,
            _scrollBarX + 1, _hotKeyRoi.Y + _hotKeyRoi.Height - 15));
        SetScrollBarArea(grooveArea);
        SetScrollBarImage(artwork.ScrollBarFrames[4]);

        _buttons = ButtonGroup.CreateStandard(roi, DialogButton.Ok, artwork.Buttons);

        BuildCategories();
        SetCategory(null);
    }

    /// <summary>The window the dialog is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The standard button row of the dialog.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Title, <c>_( "Hot Keys:" )</c> of the reference.</summary>
    public string Title => "Hot Keys:";

    /// <summary>Prompt shown instead of the title while a key is being awaited.</summary>
    public string WaitingPrompt => "Press any key to set the hotkey:";

    /// <summary>Yellow font, used for the title, the selected row and the selected category.</summary>
    public IGlyphSource TitleFont => _titleFont;

    /// <summary>White font, used for rows and categories that are not selected.</summary>
    public IGlyphSource RowFont => _rowFont;

    /// <summary>Buttons the window was built from, reused by the message boxes of the loop.</summary>
    public IDialogButtonArtworkSource ButtonArtwork => _artwork.Buttons;

    /// <summary>Every event the dialog can show, in enumeration order.</summary>
    public IReadOnlyList<HotKeyRow> AllRows => _allRows;

    /// <summary>The rows the list currently shows, i.e. <see cref="AllRows"/> filtered by the category.</summary>
    public IReadOnlyList<HotKeyRow> Rows => _rows;

    /// <summary>The category the list is filtered by; <see langword="null"/> shows all of them.</summary>
    public HotKeyCategory? SelectedCategory => _selectedCategory;

    /// <summary>Whether the next key press becomes the binding of <see cref="WaitingEvent"/>.</summary>
    public bool IsWaitingForHotKey => _waitingEvent != HotKeyEvent.None;

    /// <summary>The event whose binding is being awaited, or <see cref="HotKeyEvent.None"/>.</summary>
    public HotKeyEvent WaitingEvent => _waitingEvent;

    /// <summary>Whether a binding was changed since the dialog opened; the host saves the file when it is set.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Popup the last frame asked for, usually the category explanation, or <see langword="null"/>.</summary>
    public DialogPopup? Popup => _popup;

    /// <summary>Reads <see cref="Popup"/> and clears it, so that the message box is shown once.</summary>
    /// <returns>The popup, or <see langword="null"/> when the last frame asked for none.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;
        return popup;
    }

    /// <summary>Area of the category column, shading included.</summary>
    public Rect CategoryArea => _categoryRoi;

    /// <summary>Area the rows and the scroll bar live in.</summary>
    public Rect ListArea => _hotKeyRoi;

    /// <summary>
    /// Builds the dialog, or returns <see langword="null"/> when the artwork is incomplete or there is nothing to
    /// list, the layout of <c>openHotkeysDialog()</c> (<c>dialog_hotkeys.cpp:307-370</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on and cap the size against.</param>
    /// <param name="artwork">Scroll bar frames, its background and the buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="rowFont">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="selectedFont">Yellow font, the same as <paramref name="titleFont"/> in the reference.</param>
    /// <param name="rows">Rows to show; <see cref="GameHotKeys.GetAllHotKeyEvents"/> when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when a scroll bar frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> or a font is <see langword="null"/>.</exception>
    public static HotKeysDialog? TryCreate(Size screen, HotKeysArtwork artwork, IGlyphSource titleFont,
        IGlyphSource rowFont, IGlyphSource selectedFont, IReadOnlyList<HotKeyRow>? rows = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(rowFont);
        ArgumentNullException.ThrowIfNull(selectedFont);

        if (!artwork.IsComplete)
        {
            return null;
        }

        HotKeyRow[] allRows = rows is null
            ? [.. GameHotKeys.GetAllHotKeyEvents().Select(entry => new HotKeyRow(entry.Event, entry.Category))]
            : [.. rows];

        if (allRows.Length == 0)
        {
            return null;
        }

        // 参考按内容算三列的最大宽度，再按屏幕余量等比放大，见 dialog_hotkeys.cpp:315-360。
        int maxCategoryWidth = MinCategoryWidth;

        foreach (CategoryOption option in EnumerateCategories())
        {
            maxCategoryWidth = Math.Max(maxCategoryWidth,
                GlyphText.Measure(rowFont, GetCategoryName(option.Category)));
        }

        int maxKeyDescriptionWidth = MinKeyDescriptionWidth;

        foreach (HotKeyRow row in allRows)
        {
            maxKeyDescriptionWidth = Math.Max(maxKeyDescriptionWidth,
                GlyphText.Measure(rowFont, GameHotKeys.GetHotKeyEventNameByEventId(row.Event)));
        }

        int maxHotKeyWidth = MinHotKeyWidth;

        for (int keyValue = 0; keyValue < (int)Key.LastKey; ++keyValue)
        {
            string name = KeyNames.Get((Key)keyValue);

            if (name.Length > 0)
            {
                maxHotKeyWidth = Math.Max(maxHotKeyWidth, GlyphText.Measure(rowFont, name));
            }
        }

        int maxDialogWidth = Math.Max(screen.Width - 150, DefaultDisplayWidth);
        int listsAllowedWidening = maxDialogWidth - DefaultDisplayWidth;
        int categoryMaxWidening = maxCategoryWidth - MinCategoryWidth;
        int keyDescriptionMaxWidening = maxKeyDescriptionWidth - MinKeyDescriptionWidth;
        int hotKeyMaxWidening = maxHotKeyWidth - MinHotKeyWidth;
        int totalListsMaxWidening = categoryMaxWidening + keyDescriptionMaxWidening + hotKeyMaxWidening;

        // 参考：std::min( 1.0F, allowed / total )；总需求为 0 时那次除法不执行，放大系数取 1。
        float wideningCoefficient = totalListsMaxWidening <= 0
            ? 1.0F
            : Math.Min(1.0F, (float)listsAllowedWidening / totalListsMaxWidening);

        int categoryWidth = MinCategoryWidth + (int)(categoryMaxWidening * wideningCoefficient);
        int keyDescriptionWidth = MinKeyDescriptionWidth + (int)(keyDescriptionMaxWidening * wideningCoefficient);
        int hotKeyWidth = MinHotKeyWidth + (int)(hotKeyMaxWidening * wideningCoefficient);

        return new HotKeysDialog(screen, artwork, titleFont, rowFont, selectedFont, allRows, categoryWidth,
            keyDescriptionWidth, hotKeyWidth);
    }

    /// <summary>Filters the list by <paramref name="category"/>; <see langword="null"/> shows every event.</summary>
    /// <param name="category">Category to keep, or <see langword="null"/> for all of them.</param>
    public void SetCategory(HotKeyCategory? category)
    {
        _selectedCategory = category;
        _rows.Clear();

        foreach (HotKeyRow row in _allRows)
        {
            if (category is null || row.Category == category)
            {
                _rows.Add(row);
            }
        }

        SetListContent(_rows);
        UpdateScrollBarImage();
    }

    /// <summary>Starts waiting for the key that will be bound to <paramref name="eventId"/>.</summary>
    /// <param name="eventId">The event to rebind.</param>
    /// <exception cref="ArgumentOutOfRangeException">Not a real event.</exception>
    public void BeginWaitingForHotKey(HotKeyEvent eventId)
    {
        if (eventId is <= HotKeyEvent.None or >= HotKeyEvent.NoEvent)
        {
            throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "Not a hot key event.");
        }

        _waitingEvent = eventId;
    }

    /// <summary>Stops waiting without changing the binding, the Cancel of the reference's message box.</summary>
    public void CancelWaitingForHotKey() => _waitingEvent = HotKeyEvent.None;

    /// <summary>
    /// Binds <paramref name="key"/> to <see cref="WaitingEvent"/> and stops waiting, the OK of the reference's
    /// message box (<c>dialog_hotkeys.cpp:275-281</c>).
    /// </summary>
    /// <param name="key">The key that becomes the new binding.</param>
    public void CommitHotKey(Key key)
    {
        if (_waitingEvent == HotKeyEvent.None)
        {
            return;
        }

        GameHotKeys.SetHotKeyForEvent(_waitingEvent, key);
        _waitingEvent = HotKeyEvent.None;
        IsDirty = true;
        NeedRedraw = true;
    }

    /// <summary>Draws the window, the title, the categories, the rows and the buttons.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        Rect roi = _window.ActiveArea;
        string title = IsWaitingForHotKey ? WaitingPrompt : Title;
        int titleWidth = GlyphText.Measure(_titleFont, title);

        GlyphText.DrawLine(_titleFont, target,
            new Point(roi.X + ((roi.Width - titleWidth) / 2), roi.Y + TitleTop), title);

        // RedrawBackground 先压两块底板再画行；分类文字必须压在底板之上，所以排在 Redraw 之后。
        Redraw(target);
        DrawCategories(target);

        _buttons.DrawShadows(target);
        _buttons.Draw(target);
    }

    /// <summary>
    /// One frame of input: the button row, then the list, then the categories; a row that is already selected
    /// starts waiting for a key, the double click of the reference.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="hotKey">OK/CANCEL key the caller recognised, ignored while a key is awaited.</param>
    /// <returns>The clicked button, <see cref="DialogButton.None"/> otherwise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public DialogButton ProcessEvents(LocalEvent events, ButtonHotKey? hotKey = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (IsWaitingForHotKey)
        {
            // 参考在等待时把 OKAY / CANCEL / F4 / F10 临时置空，好让任何键都能被绑上；端口在这一支里先吃掉
            // 这一帧的按键并且不处理 hotKey，效果相同。
            if (events.IsAnyKeyPressed)
            {
                CommitHotKey(events.PressedKeyValue);
            }

            return DialogButton.None;
        }

        ButtonHotKey? resolved = hotKey;

        if (resolved is null)
        {
            resolved = events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay
                : events.IsKeyPressed(Key.Escape) ? ButtonHotKey.Cancel
                : null;
        }

        DialogButton clicked = _buttons.ProcessEvents(events, resolved);

        QueueEventProcessing(events);

        foreach (CategoryEntry entry in _categories)
        {
            if (events.MouseClickLeft(entry.Area))
            {
                if (entry.Category != _selectedCategory)
                {
                    SetCategory(entry.Category);
                }

                return DialogButton.None;
            }

            if (events.IsMouseRightButtonPressedInArea(entry.Area))
            {
                _popup = new DialogPopup("Select hotkeys category",
                    "Click here to select:\n" + GetCategoryName(entry.Category));

                return DialogButton.None;
            }
        }

        if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Exit this menu.");

            return DialogButton.None;
        }

        return clicked;
    }

    /// <summary>
    /// Runs the window until OK or Escape, the event loop of <c>openHotkeysDialog()</c>
    /// (<c>dialog_hotkeys.cpp:415-482</c>).
    /// </summary>
    /// <param name="target">Indexed frame buffer the scene draws and renders.</param>
    /// <param name="dialog">The window to run.</param>
    /// <param name="events">Input source; defaults to <see cref="LocalEvent.Current"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="dialog"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static void Show(PixelBuffer target, HotKeysDialog dialog, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dialog);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The hot keys window needs an event engine to run.");

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            DialogButton result = dialog.ProcessEvents(eventState);

            if (dialog.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, dialog.TitleFont, dialog.RowFont,
                    DialogButton.Ok, dialog.ButtonArtwork, eventState);
            }

            if (result is DialogButton.Ok or DialogButton.Cancel)
            {
                return;
            }

            dialog.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }
    }

    /// <summary>Draws one row: the event name on the left, its key centred in the right column.</summary>
    /// <param name="item">The row to draw.</param>
    /// <param name="x">Left edge of the rows area.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is selected; it decides between the yellow and the white font.</param>
    /// <param name="output">Frame buffer.</param>
    public override void RedrawItem(HotKeyRow item, int x, int y, bool current, PixelBuffer output)
    {
        IGlyphSource font = current ? _selectedFont : _rowFont;
        int textY = y + RowTextOffsetY;

        string name = FitToOneRow(font, GameHotKeys.GetHotKeyEventNameByEventId(item.Event),
            _keyDescriptionWidth);

        GlyphText.DrawLine(font, output, new Point(x + RowTextInsetX, textY), name);

        string hotKey = IsWaitingForHotKey && item.Event == _waitingEvent
            ? Ellipsis
            : GameHotKeys.GetHotKeyNameByEventId(item.Event);

        int hotKeyX = x + _keyDescriptionWidth + KeyColumnGap;
        int hotKeyWidth = GlyphText.Measure(font, hotKey);

        GlyphText.DrawLine(font, output,
            new Point(hotKeyX + Math.Max(0, (_hotKeyWidth - hotKeyWidth) / 2), textY), hotKey);
    }

    /// <summary>Stamps the two shading plates and the scroll bar groove behind the rows.</summary>
    /// <param name="destination">Unused; the window repaints the whole list area.</param>
    /// <param name="output">Frame buffer.</param>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _categoryRoi);
        StandardWindow.ApplyTextBackgroundShading(output, _hotKeyRoi);
        StandardWindow.RenderScrollbarBackground(output,
            new Rect(_scrollBarX, _hotKeyRoi.Y, _hotKeyRoi.Width, _hotKeyRoi.Height),
            _artwork.ScrollBarBackground);
    }

    /// <summary>Shows the category, event and key of the row under the cursor, the right click of the reference.</summary>
    /// <param name="item">The row that was right clicked.</param>
    public override void ActionListPressRight(HotKeyRow item)
    {
        _popup = new DialogPopup(
            "Category: " + GameHotKeys.GetHotKeyCategoryName(item.Category),
            "Event: " + GameHotKeys.GetHotKeyEventNameByEventId(item.Event) +
            "\n\nHotkey: " + GameHotKeys.GetHotKeyNameByEventId(item.Event));
    }

    /// <summary>Starts waiting for a new key for the clicked row, the double click of the reference.</summary>
    /// <param name="item">The row that was clicked twice.</param>
    public override void ActionListDoubleClick(HotKeyRow item) => BeginWaitingForHotKey(item.Event);

    /// <summary>Does nothing; a single click only moves the selection in the reference.</summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListSingleClick(HotKeyRow item) => _ = item;

    /// <summary>Does nothing; the reference leaves the arrow keys to the list base class.</summary>
    public override void ActionCurrentUp()
    {
    }

    /// <summary>Does nothing; the reference leaves the arrow keys to the list base class.</summary>
    public override void ActionCurrentDn()
    {
    }

    /// <summary>Draws the category column, the selected line in yellow.</summary>
    /// <param name="target">Frame buffer.</param>
    internal void DrawCategories(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (CategoryEntry entry in _categories)
        {
            IGlyphSource font = entry.Category == _selectedCategory ? _selectedFont : _rowFont;
            string name = FitToOneRow(font, GetCategoryName(entry.Category), _categoryWidth);

            GlyphText.DrawLine(font, target, new Point(_categoryRoi.X + RowTextInsetX, entry.Area.Y), name);
        }
    }

    private void BuildCategories()
    {
        _categories.Clear();

        int offsetY = _categoryRoi.Y + CategoryTopSlop;

        AddCategory(null, ref offsetY);

        foreach (CategoryOption option in EnumerateCategories())
        {
            AddCategory(option.Category, ref offsetY);
        }
    }

    private void AddCategory(HotKeyCategory? category, ref int offsetY)
    {
        int stepY = _rowFont.LineHeight + CategoryGap;

        _categories.Add(new CategoryEntry(category, new Rect(_categoryRoi.X, offsetY, _categoryRoi.Width, stepY)));
        offsetY += stepY;
    }

    /// <summary>
    /// The categories the dialog offers, in reference order, skipping <see cref="HotKeyCategory.Editor"/> when the
    /// game has no editor (<c>dialog_hotkeys.cpp:315-328</c>).
    /// </summary>
    private static IEnumerable<CategoryOption> EnumerateCategories()
    {
        bool hasEditor = Settings.Get().IsPriceOfLoyaltySupported;

        for (int value = (int)HotKeyCategory.Default; value <= (int)HotKeyCategory.Editor; ++value)
        {
            HotKeyCategory category = (HotKeyCategory)value;

            if (category == HotKeyCategory.Editor && !hasEditor)
            {
                continue;
            }

            yield return new CategoryOption(category);
        }
    }

    /// <summary>Name of a category, «All Categories» for <see langword="null"/>.</summary>
    /// <param name="category">The category, or <see langword="null"/> for the ungrouped list.</param>
    private static string GetCategoryName(HotKeyCategory? category) =>
        category is { } value ? GameHotKeys.GetHotKeyCategoryName(value) : "All Categories";

    /// <summary>Shortens <paramref name="text"/> to <paramref name="maxWidth"/>, the <c>fitToOneRow()</c> helper.</summary>
    private static string FitToOneRow(IGlyphSource font, string text, int maxWidth)
    {
        if (maxWidth <= 0 || GlyphText.Measure(font, text) <= maxWidth)
        {
            return text;
        }

        int ellipsisWidth = GlyphText.Measure(font, Ellipsis);

        if (ellipsisWidth >= maxWidth)
        {
            return string.Empty;
        }

        int width = 0;
        int end = 0;

        for (int index = 0; index < text.Length; ++index)
        {
            int advance = font.GetAdvance(text[index]);

            if (width + advance + ellipsisWidth > maxWidth)
            {
                break;
            }

            width += advance;
            end = index + 1;
        }

        return string.Concat(text.AsSpan(0, end), Ellipsis);
    }

    private readonly record struct CategoryOption(HotKeyCategory? Category);

    private readonly record struct CategoryEntry(HotKeyCategory? Category, Rect Area);
}
