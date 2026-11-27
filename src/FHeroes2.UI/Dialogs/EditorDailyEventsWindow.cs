using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The editor's daily events window, <c>openDailyEventsWindow()</c> (<c>editor_daily_events_window.cpp:160-319</c>).
/// </summary>
/// <remarks>
/// <para>
/// 550×415 的标准窗（活动区 550×415 里列表占 500×315）：标题「Daily events:」居中，下面 500×315 的事件列表
/// （<see cref="ListBox{T}"/>），底部一排三个 <c>CELLWIN</c> 小按钮（增、改、删）和标准的 OKAY/CANCEL。
/// </para>
/// <para>
/// 每行是「Day: 天数」加上可选的「&quot;消息&quot;」：日期部分原样画，消息部分按剩下的宽度截一行（参考用两个
/// <c>Text</c> 拼 <c>MultiFontText</c>，端口没有多段文本，于是量完日期宽度接着画第二段）。
/// </para>
/// <para>
/// 参考在增、改、删之后都对整个 <c>std::vector</c> 按首次发生日重排（<c>sortEvents</c>），端口一样在就地排序，
/// 所以列表始终按天递增。增改要弹的规格窗（<c>editor_daily_event_spec_window.cpp</c>）、热键都交回宿主：
/// 本类只发 <see cref="AddRequested"/> / <see cref="EditRequested"/>，再吃回 <see cref="AddEvent"/> /
/// <see cref="ReplaceSelected"/>。
/// </para>
/// </remarks>
public sealed class EditorDailyEventsWindow : ListBox<MapDailyEvent>
{
    /// <summary>Gap around the title and the list, <c>elementOffset</c> (<c>:53</c>).</summary>
    public const int ElementOffset = 9;

    /// <summary>Width of the list, <c>eventsArea.width</c> (<c>:55</c>).</summary>
    public const int EventsAreaWidth = 500;

    /// <summary>Height of the list, <c>eventsArea.height</c> (<c>:55</c>).</summary>
    public const int EventsAreaHeight = 315;

    /// <summary>Room left under the list for its last row, <c>listAreaHeightDeduction</c> (<c>:57</c>).</summary>
    public const int ListAreaHeightDeduction = 8;

    /// <summary>Rows on screen at once, <c>SetAreaMaxItems( 10 )</c> (<c>:198</c>).</summary>
    public const int MaxVisibleItemCount = 10;

    /// <summary>Width of the window's active area, <c>eventsArea.width + 50</c> (<c>:166</c>).</summary>
    public const int ActiveWidth = EventsAreaWidth + 50;

    /// <summary>Height of the window's active area, <c>eventsArea.height + 100</c> (<c>:166</c>).</summary>
    public const int ActiveHeight = EventsAreaHeight + 100;

    /// <summary>Height of a scroll button, <c>topPartHeight</c> (<c>:191</c>).</summary>
    public const int ScrollButtonHeight = 19;

    /// <summary>Left inset of a row's text, <c>posX + 5</c> (<c>:95</c>).</summary>
    public const int RowTextInsetX = 5;

    /// <summary>Top inset of a row's text, <c>posY + 5</c> (<c>:95</c>).</summary>
    public const int RowTextInsetY = 5;

    /// <summary>Room the row text leaves on both sides, the <c>10</c> of <c>:84</c> and <c>:91</c>.</summary>
    public const int RowTextInsetTotal = 10;

    /// <summary>The day part of a row, <c>_( "Day: %{day}" )</c> (<c>:75</c>).</summary>
    public const string DayTextFormat = "Day: {0}";

    /// <summary>Title of the window, <c>_( "Daily events:" )</c> (<c>:172</c>).</summary>
    public const string HeaderText = "Daily events:";

    /// <summary>Title of the CANCEL button's message, <c>:295</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:298</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to save the events.";

    /// <summary>Title of the "add" button's message, <c>:301</c>.</summary>
    public const string AddTitle = "Add Event";

    /// <summary>Body of the "add" button's message.</summary>
    public const string AddMessage = "Add an additional event.";

    /// <summary>Title of the "edit" button's message, <c>:304</c>.</summary>
    public const string EditTitle = "Edit Event";

    /// <summary>Body of the "edit" button's message.</summary>
    public const string EditMessage = "Edit an existing event.";

    /// <summary>Title of the "delete" button's message, <c>:307</c>.</summary>
    public const string DeleteTitle = "Delete Event";

    /// <summary>Body of the "delete" button's message.</summary>
    public const string DeleteMessage = "Delete an existing event.";

    private readonly List<MapDailyEvent> _events = [];

    private readonly StandardWindow _window;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly Sprite _scrollBarBackground;
    private readonly Rect _listArea;
    private readonly int _scrollBarX;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _add;
    private readonly ButtonSprite _edit;
    private readonly ButtonSprite _delete;

    private EditorDailyEventsWindow(Size screen, IReadOnlyList<MapDailyEvent> events, EditorListArtwork artwork,
        IGlyphSource headerFont, IGlyphSource bodyFont)
        : base(new Point(0, 0))
    {
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _scrollBarBackground = artwork.ScrollBarBackground;

        _window = StandardWindow.CreateCentered(new Size(ActiveWidth, ActiveHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        // 参考 :170-177：标题离顶 9 像素居中，列表紧接标题再空 9 像素。
        _listArea = new Rect(active.X + ElementOffset,
            active.Y + ElementOffset + Math.Max(1, headerFont.LineHeight) + ElementOffset,
            EventsAreaWidth, EventsAreaHeight);

        _scrollBarX = _listArea.Right + 5;

        SetAreaItems(new Rect(_listArea.X, _listArea.Y, _listArea.Width,
            _listArea.Height - ListAreaHeightDeduction));
        SetAreaMaxItems(MaxVisibleItemCount);
        SetTopLeft(new Point(_listArea.X, _listArea.Y));

        SetScrollButtonUp(new ButtonSprite(artwork.ScrollBarFrames[0], artwork.ScrollBarFrames[1], null,
            _scrollBarX + 1, _listArea.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollBarFrames[2], artwork.ScrollBarFrames[3], null,
            _scrollBarX + 1, _listArea.Bottom - 15));
        SetScrollBarArea(new Rect(_scrollBarX + 3, _listArea.Y + ScrollButtonHeight, 10,
            _listArea.Height - (ScrollButtonHeight * 2)));
        SetScrollBarImage(artwork.ScrollBarFrames[4]);

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);

        Point okayPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_okay.Area.Width, _okay.Area.Height), WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_cancel.Area.Width, _cancel.Area.Height), WindowPadding.BottomRight);

        _okay.SetPosition(okayPosition.X, okayPosition.Y);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        // 参考 :206-217：三个小按钮隔一行摆在列表底下，中间那个落在 ( width - 3 * buttonWidth ) / 2 + buttonWidth。
        int buttonOffset = ((EventsAreaWidth - (3 * artwork.ButtonWidth)) / 2) + artwork.ButtonWidth;
        int buttonY = _listArea.Bottom + 5;

        _add = new ButtonSprite(artwork.Add.Released, artwork.Add.Pressed);
        _add.SetPosition(_listArea.X, buttonY);

        _edit = new ButtonSprite(artwork.Edit.Released, artwork.Edit.Pressed);
        _edit.SetPosition(_listArea.X + buttonOffset, buttonY);

        _delete = new ButtonSprite(artwork.Delete.Released, artwork.Delete.Pressed);
        _delete.SetPosition(_listArea.Right - artwork.ButtonWidth, buttonY);

        SetEvents(events);
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="screen">Screen size, <c>display.width()</c>/<c>height()</c> (<c>:166</c>).</param>
    /// <param name="events">The events to list; they are copied and sorted by their first day.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="headerFont">The normal yellow font, also the font of the selected row.</param>
    /// <param name="bodyFont">The normal white font.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorDailyEventsWindow Create(Size screen, IReadOnlyList<MapDailyEvent> events,
        EditorListArtwork artwork, IGlyphSource headerFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        return new EditorDailyEventsWindow(screen, events, artwork, headerFont, bodyFont);
    }

    /// <summary>The window the list sits in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The rect of the event list, <c>eventsRoi</c> (<c>:177</c>).</summary>
    public Rect ListArea => _listArea;

    /// <summary>The OKAY button.</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The "add" button.</summary>
    public ButtonSprite AddButton => _add;

    /// <summary>The "edit" button.</summary>
    public ButtonSprite EditButton => _edit;

    /// <summary>The "delete" button.</summary>
    public ButtonSprite DeleteButton => _delete;

    /// <summary>The events as they stand, sorted by their first day.</summary>
    public IReadOnlyList<MapDailyEvent> Events => _events;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorListOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>
    /// Whether the author clicked "add" and the host has to open the event spec window (<c>:252-264</c>). What it
    /// returns goes to <see cref="AddEvent"/>; the host builds a fresh <see cref="MapDailyEvent"/>.
    /// </summary>
    public bool AddRequested { get; private set; }

    /// <summary>
    /// Whether the author asked to edit the selected event, by double clicking it or clicking "edit"
    /// (<c>:265-281</c>). The host edits a copy of <see cref="SelectedEvent"/> and hands it to
    /// <see cref="ReplaceSelected"/>.
    /// </summary>
    public bool EditRequested { get; private set; }

    /// <summary>The event under the selection, <c>eventList.GetCurrent()</c> (<c>:272</c>).</summary>
    public MapDailyEvent? SelectedEvent => HasSelection ? _events[CurrentId] : null;

    /// <summary>Whether a row is under the selection.</summary>
    private bool HasSelection => CurrentId >= 0 && CurrentId < _events.Count;

    /// <summary>Whether the last frame double clicked a row, <c>isDoubleClicked()</c> (<c>:118-121</c>).</summary>
    private bool IsDoubleClicked { get; set; }

    /// <summary>
    /// Replaces the list with copies of <paramref name="events"/>, sorted by their first day.
    /// </summary>
    /// <param name="events">The events; they are copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public void SetEvents(IReadOnlyList<MapDailyEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _events.Clear();

        foreach (MapDailyEvent dailyEvent in events)
        {
            ArgumentNullException.ThrowIfNull(dailyEvent);

            _events.Add(dailyEvent.Clone());
        }

        SortEvents();
        SetListContent(_events);
        Unselect();
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Appends an event the author built in the spec window and re-sorts, the <c>dailyEvents.emplace_back()</c> plus
    /// <c>sortEvents()</c> of <c>:255-257</c>.
    /// </summary>
    /// <param name="dailyEvent">The event; it is copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dailyEvent"/> is <see langword="null"/>.</exception>
    public void AddEvent(MapDailyEvent dailyEvent)
    {
        ArgumentNullException.ThrowIfNull(dailyEvent);

        _events.Add(dailyEvent.Clone());
        SortEvents();
        UpdateScrollBarImage();
        NeedRedraw = true;
    }

    /// <summary>
    /// Writes the event the author edited in the spec window over the selected one and re-sorts,
    /// <c>eventList.GetCurrent() = std::move( temp )</c> plus <c>sortEvents()</c> (<c>:274-276</c>).
    /// </summary>
    /// <param name="dailyEvent">The edited event; it is copied.</param>
    /// <returns><see langword="false"/> when nothing is selected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dailyEvent"/> is <see langword="null"/>.</exception>
    public bool ReplaceSelected(MapDailyEvent dailyEvent)
    {
        ArgumentNullException.ThrowIfNull(dailyEvent);

        if (!HasSelection)
        {
            return false;
        }

        _events[CurrentId] = dailyEvent.Clone();
        SortEvents();
        UpdateScrollBarImage();
        NeedRedraw = true;

        return true;
    }

    /// <summary>
    /// Drops the selected event and re-sorts, <c>eventList.RemoveSelected()</c> plus <c>sortEvents()</c>
    /// (<c>:287-288</c>).
    /// </summary>
    /// <returns>The event that was removed, or <see langword="null"/> when nothing was selected.</returns>
    public MapDailyEvent? RemoveSelectedEvent()
    {
        if (!HasSelection)
        {
            return null;
        }

        MapDailyEvent removed = _events[CurrentId];

        RemoveSelected();
        SortEvents();
        UpdateScrollBarImage();

        return removed;
    }

    /// <summary>
    /// Draws the window, the <c>display.render()</c> calls of <c>:173-225</c>.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        _window.Draw(target);

        int headerWidth = GlyphText.Measure(_headerFont, HeaderText);
        GlyphText.DrawLine(_headerFont, target,
            new Point(ActiveArea.X + ((ActiveArea.Width - headerWidth) / 2), ActiveArea.Y + ElementOffset), HeaderText);

        StandardWindow.ApplyTextBackgroundShading(target, _listArea);

        Redraw(target);

        // 三个小按钮是 fheroes2::Button，没有阴影（:210-217）；OKAY/CANCEL 由 renderOkayCancelButtons 带阴影（:223）。
        PaintMiniButton(_add, target, events);
        PaintMiniButton(_edit, target, events);
        PaintMiniButton(_delete, target, events);
        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, <c>:230-314</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The result of the frame; <see cref="EditorListOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorListOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Result = EditorListOutcome.None;
        Popup = null;
        AddRequested = false;
        EditRequested = false;
        IsDoubleClicked = false;

        // 参考在循环开头认 CANCEL/OKAY（:237-243）；窗口自己不认键盘，热键留给宿主。
        if (events.MouseClickLeft(_cancel.Area))
        {
            Result = EditorListOutcome.Cancelled;
            return Result;
        }

        if (events.MouseClickLeft(_okay.Area))
        {
            Result = EditorListOutcome.Accepted;
            return Result;
        }

        QueueEventProcessing(events);

        if (events.MouseClickLeft(_add.Area))
        {
            AddRequested = true;
        }
        else if (IsDoubleClicked || events.MouseClickLeft(_edit.Area))
        {
            // 参考 :266-268：没有选中行就什么也不做。
            EditRequested = HasSelection;
        }
        else if (events.MouseClickLeft(_delete.Area))
        {
            RemoveSelectedEvent();
        }

        if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            Popup = new DialogPopup(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_okay.Area))
        {
            Popup = new DialogPopup(OkayTitle, OkayMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_add.Area))
        {
            Popup = new DialogPopup(AddTitle, AddMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_edit.Area))
        {
            Popup = new DialogPopup(EditTitle, EditMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_delete.Area))
        {
            Popup = new DialogPopup(DeleteTitle, DeleteMessage);
        }

        return Result;
    }

    /// <inheritdoc/>
    public override void RedrawItem(MapDailyEvent item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        IGlyphSource font = current ? _headerFont : _bodyFont;

        // 参考 :75-82：日期行，事件有文字时补一个开引号。
        string date = string.Format(CultureInfo.InvariantCulture, DayTextFormat, item.FirstOccurrenceDay);

        if (!string.IsNullOrEmpty(item.Message))
        {
            date += " \"";
        }

        int dateLength = GlyphText.Measure(font, date);

        GlyphText.DrawLine(font, output, new Point(x + RowTextInsetX, y + RowTextInsetY), date, ItemsArea);

        if (!string.IsNullOrEmpty(item.Message))
        {
            // 参考 :90-92：消息按剩下的宽度截一行，再接上闭引号。
            int room = Math.Max(1, EventsAreaWidth - RowTextInsetTotal - dateLength);

            GlyphText.DrawLine(font, output, new Point(x + RowTextInsetX + dateLength, y + RowTextInsetY),
                GlyphText.FitToOneRow(font, item.Message + "\"", room), ItemsArea);
        }
    }

    /// <inheritdoc/>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _listArea);

        StandardWindow.RenderScrollbarBackground(output,
            new Rect(_scrollBarX, _listArea.Y, _listArea.Width, _listArea.Height), _scrollBarBackground);
    }

    /// <inheritdoc/>
    public override void ActionCurrentUp()
    {
    }

    /// <inheritdoc/>
    public override void ActionCurrentDn()
    {
    }

    /// <inheritdoc/>
    public override void ActionListSingleClick(MapDailyEvent item)
    {
    }

    /// <inheritdoc/>
    public override void ActionListDoubleClick(MapDailyEvent item) => IsDoubleClicked = true;

    /// <inheritdoc/>
    public override void ActionListPressRight(MapDailyEvent item)
    {
    }

    /// <summary>
    /// Sorts the rows by their first day, <c>sortEvents()</c> (<c>:151-155</c>).
    /// </summary>
    /// <remarks>
    /// 参考用的是 <c>std::sort</c>（不稳定排序），端口用 <see cref="List{T}.Sort(Comparison{T})"/>，同样不稳定；
    /// 排序就地做，列表看到的还是同一个 <see cref="List{T}"/> 实例。
    /// </remarks>
    private void SortEvents() =>
        _events.Sort(static (first, second) => first.FirstOccurrenceDay.CompareTo(second.FirstOccurrenceDay));

    private static void PaintMiniButton(ButtonSprite button, PixelBuffer target, LocalEvent events) =>
        button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);

    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent events)
    {
        ImageOps.AddGradientShadow(button.Released, target, button.Area.Position, new Point(-5, 5));

        if (button.IsEnabled)
        {
            button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
        }
        else
        {
            button.Draw(target);
        }
    }
}
