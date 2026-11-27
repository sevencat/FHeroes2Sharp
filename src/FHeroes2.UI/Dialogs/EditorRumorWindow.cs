using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The editor's rumor window, <c>openRumorWindow()</c> (<c>editor_rumor_window.cpp:135-305</c>).
/// </summary>
/// <remarks>
/// <para>
/// 600×400 的标准窗：标题「Rumors:」居中，下面 550×300 的传闻列表（<see cref="ListBox{T}"/>，每行一句传闻，选中行黄字），
/// 底部一排三个 <c>CELLWIN</c> 小按钮（增、改、删）和标准的 OKAY/CANCEL。
/// </para>
/// <para>
/// 参考是阻塞式的，本类只做「按一帧输入 → 返回结果」：增改要弹的输入框、重复传闻的提示、热键都交回宿主
/// （<see cref="AddRequested"/>、<see cref="EditRequested"/>、<see cref="Popup"/>），与编辑器其它窗口一致。
/// </para>
/// </remarks>
public sealed class EditorRumorWindow : ListBox<string>
{
    /// <summary>Gap around the title and the list, <c>elementOffset</c> (<c>:50</c>).</summary>
    public const int ElementOffset = 9;

    /// <summary>Width of the list, <c>rumorArea.width</c> (<c>:52</c>).</summary>
    public const int RumorAreaWidth = 550;

    /// <summary>Height of the list, <c>rumorArea.height</c> (<c>:52</c>).</summary>
    public const int RumorAreaHeight = 300;

    /// <summary>Room left under the list for its last row, <c>listAreaHeightDeduction</c> (<c>:54</c>).</summary>
    public const int ListAreaHeightDeduction = 8;

    /// <summary>Longest rumor the input box takes, <c>longestRumor</c> (<c>:56</c>).</summary>
    public const int LongestRumor = 200;

    /// <summary>Rows on screen at once, <c>SetAreaMaxItems( 10 )</c> (<c>:177</c>).</summary>
    public const int MaxVisibleItemCount = 10;

    /// <summary>Width of the window's active area, <c>rumorArea.width + 50</c> (<c>:145</c>).</summary>
    public const int ActiveWidth = RumorAreaWidth + 50;

    /// <summary>Height of the window's active area, <c>rumorArea.height + 100</c> (<c>:145</c>).</summary>
    public const int ActiveHeight = RumorAreaHeight + 100;

    /// <summary>Height of a scroll button, <c>topPartHeight</c> (<c>:170</c>).</summary>
    public const int ScrollButtonHeight = 19;

    /// <summary>Left inset of a row's text, <c>posX + 5</c> (<c>:76</c>).</summary>
    public const int RowTextInsetX = 5;

    /// <summary>Top inset of a row's text, <c>posY + 5</c> (<c>:76</c>).</summary>
    public const int RowTextInsetY = 5;

    /// <summary>Title of the window, <c>_( "Rumors:" )</c> (<c>:151</c>).</summary>
    public const string HeaderText = "Rumors:";

    /// <summary>Title of the input box, the empty header of <c>:234</c>.</summary>
    public const string InputTitle = "";

    /// <summary>Body of the input box, <c>_( "Rumor:" )</c> (<c>:234</c>).</summary>
    public const string InputBody = "Rumor:";

    /// <summary>Title of the CANCEL button's message, <c>:282</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:285</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to save the rumors.";

    /// <summary>Title of the "add" button's message, <c>:288</c>.</summary>
    public const string AddTitle = "Add Rumor";

    /// <summary>Body of the "add" button's message.</summary>
    public const string AddMessage = "Add an additional rumor.";

    /// <summary>Title of the "edit" button's message, <c>:291</c>.</summary>
    public const string EditTitle = "Edit Rumor";

    /// <summary>Body of the "edit" button's message.</summary>
    public const string EditMessage = "Edit an existing rumor.";

    /// <summary>Title of the "delete" button's message, <c>:294</c>.</summary>
    public const string DeleteTitle = "Delete Rumor";

    /// <summary>Body of the "delete" button's message.</summary>
    public const string DeleteMessage = "Delete an existing rumor.";

    /// <summary>Title of the duplicate rumor message, <c>:237</c>.</summary>
    public const string DuplicateTitle = "Rumor";

    /// <summary>Body of the duplicate rumor message, <c>:237</c>.</summary>
    public const string DuplicateMessage = "This rumor already exists in the list.";

    private readonly List<string> _rumors = [];

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

    private EditorRumorWindow(Size screen, IReadOnlyList<string> rumors, EditorListArtwork artwork,
        IGlyphSource headerFont, IGlyphSource bodyFont)
        : base(new Point(0, 0))
    {
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _scrollBarBackground = artwork.ScrollBarBackground;

        _window = StandardWindow.CreateCentered(new Size(ActiveWidth, ActiveHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        // 参考 :149-156：标题离顶 9 像素居中，列表紧接标题再空 9 像素。
        _listArea = new Rect(active.X + ElementOffset,
            active.Y + ElementOffset + Math.Max(1, headerFont.LineHeight) + ElementOffset,
            RumorAreaWidth, RumorAreaHeight);

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

        // 参考 :186-195：三个小按钮隔一行摆在列表底下，中间那个落在 ( width - 3 * buttonWidth ) / 2 + buttonWidth。
        int buttonOffset = ((RumorAreaWidth - (3 * artwork.ButtonWidth)) / 2) + artwork.ButtonWidth;
        int buttonY = _listArea.Bottom + 5;

        _add = new ButtonSprite(artwork.Add.Released, artwork.Add.Pressed);
        _add.SetPosition(_listArea.X, buttonY);

        _edit = new ButtonSprite(artwork.Edit.Released, artwork.Edit.Pressed);
        _edit.SetPosition(_listArea.X + buttonOffset, buttonY);

        _delete = new ButtonSprite(artwork.Delete.Released, artwork.Delete.Pressed);
        _delete.SetPosition(_listArea.Right - artwork.ButtonWidth, buttonY);

        SetRumors(rumors);
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="screen">Screen size, <c>display.width()</c>/<c>height()</c> (<c>:145</c>).</param>
    /// <param name="rumors">The rumors to list; the empty ones are dropped, as in <c>:137-140</c>.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="headerFont">The normal yellow font, also the font of the selected row.</param>
    /// <param name="bodyFont">The normal white font.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorRumorWindow Create(Size screen, IReadOnlyList<string> rumors, EditorListArtwork artwork,
        IGlyphSource headerFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(rumors);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        return new EditorRumorWindow(screen, rumors, artwork, headerFont, bodyFont);
    }

    /// <summary>The window the list sits in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The rect of the rumor list, <c>rumorsRoi</c> (<c>:156</c>).</summary>
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

    /// <summary>The rumors as they stand, in the order the list shows them.</summary>
    public IReadOnlyList<string> Rumors => _rumors;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorListOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>
    /// Whether the author clicked "add" and the host has to open the input box, <c>:231-247</c>. The text it returns
    /// goes to <see cref="TryAddRumor"/>.
    /// </summary>
    public bool AddRequested { get; private set; }

    /// <summary>
    /// Whether the author asked to edit the selected rumor, by double clicking it or clicking "edit" (<c>:248-270</c>).
    /// The text the input box returns goes to <see cref="TryReplaceSelected"/>.
    /// </summary>
    public bool EditRequested { get; private set; }

    /// <summary>The rumor being edited, <c>rumorList.GetCurrent()</c> (<c>:255</c>).</summary>
    public string? SelectedRumor => HasSelection ? _rumors[CurrentId] : null;

    /// <summary>Whether a row is under the selection.</summary>
    private bool HasSelection => CurrentId >= 0 && CurrentId < _rumors.Count;

    /// <summary>Whether the last frame double clicked a row, <c>isDoubleClicked()</c> (<c>:99-107</c>).</summary>
    private bool IsDoubleClicked { get; set; }

    /// <summary>
    /// Replaces the list, dropping the empty rumors, <c>:137-140</c>.
    /// </summary>
    /// <param name="rumors">The rumors; the empty ones are dropped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rumors"/> is <see langword="null"/>.</exception>
    public void SetRumors(IReadOnlyList<string> rumors)
    {
        ArgumentNullException.ThrowIfNull(rumors);

        _rumors.Clear();

        foreach (string rumor in rumors)
        {
            if (!string.IsNullOrEmpty(rumor))
            {
                _rumors.Add(rumor);
            }
        }

        SetListContent(_rumors);
        Unselect();
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Appends a rumor the author typed, the <c>rumors.emplace_back()</c> of <c>:241</c>.
    /// </summary>
    /// <param name="rumor">The text of the input box.</param>
    /// <returns>
    /// <see langword="false"/> when the list already has it; <see cref="Popup"/> then holds the message the host has to
    /// show.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="rumor"/> is <see langword="null"/>.</exception>
    public bool TryAddRumor(string rumor)
    {
        ArgumentNullException.ThrowIfNull(rumor);

        if (_rumors.Contains(rumor))
        {
            Popup = new DialogPopup(DuplicateTitle, DuplicateMessage);
            return false;
        }

        _rumors.Add(rumor);
        UpdateScrollBarImage();
        NeedRedraw = true;

        return true;
    }

    /// <summary>
    /// Writes the text of the input box over the selected rumor, <c>rumorList.GetCurrent() = std::move( temp )</c>
    /// (<c>:265</c>).
    /// </summary>
    /// <param name="rumor">The text of the input box.</param>
    /// <returns>
    /// <see langword="false"/> when nothing is selected or another row already has that text; <see cref="Popup"/> then
    /// holds the message the host has to show.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="rumor"/> is <see langword="null"/>.</exception>
    public bool TryReplaceSelected(string rumor)
    {
        ArgumentNullException.ThrowIfNull(rumor);

        if (!HasSelection)
        {
            return false;
        }

        int index = CurrentId;

        if (!string.Equals(_rumors[index], rumor, StringComparison.Ordinal) && _rumors.Contains(rumor))
        {
            Popup = new DialogPopup(DuplicateTitle, DuplicateMessage);
            return false;
        }

        _rumors[index] = rumor;
        NeedRedraw = true;

        return true;
    }

    /// <summary>
    /// Drops the selected rumor, <c>rumorList.RemoveSelected()</c> (<c>:276</c>).
    /// </summary>
    /// <returns>The rumor that was removed, or <see langword="null"/> when nothing was selected.</returns>
    public string? RemoveSelectedRumor()
    {
        if (!HasSelection)
        {
            return null;
        }

        string removed = _rumors[CurrentId];

        RemoveSelected();
        UpdateScrollBarImage();

        return removed;
    }

    /// <summary>
    /// Draws the window, the <c>display.render()</c> calls of <c>:152-204</c>.
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

        // 三个小按钮是 fheroes2::Button，没有阴影（:189-196）；OKAY/CANCEL 由 renderOkayCancelButtons 带阴影（:202）。
        PaintMiniButton(_add, target, events);
        PaintMiniButton(_edit, target, events);
        PaintMiniButton(_delete, target, events);
        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, <c>:209-302</c>.
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

        // 参考在循环开头认 CANCEL/OKAY（:216-222）；窗口自己不认键盘，热键留给宿主。
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
            // 参考 :248-250：没有选中行就什么也不做。
            EditRequested = HasSelection;
        }
        else if (events.MouseClickLeft(_delete.Area))
        {
            RemoveSelectedRumor();
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
    public override void RedrawItem(string item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        IGlyphSource font = current ? _headerFont : _bodyFont;

        GlyphText.DrawLine(font, output, new Point(x + RowTextInsetX, y + RowTextInsetY),
            GlyphText.FitToOneRow(font, item, RumorAreaWidth - (2 * RowTextInsetX)), ItemsArea);
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
    public override void ActionListSingleClick(string item)
    {
    }

    /// <inheritdoc/>
    public override void ActionListDoubleClick(string item) => IsDoubleClicked = true;

    /// <inheritdoc/>
    public override void ActionListPressRight(string item)
    {
    }

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
