using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The editor's language window, <c>Editor::openLanguageWindow()</c> (<c>editor_language_window.cpp:133-306</c>).
/// </summary>
/// <remarks>
/// <para>
/// 350×400 的标准窗：标题「Supported languages:」居中，下面是 300×300 的语言列表（右侧是滚动条），列表底下左右各一个
/// <c>CELLWIN</c> 小按钮（增、删），再往下是标准的 OKAY/CANCEL。列表的每一行是该语言自己的名字，选中行用黄色、
/// 其余白色。
/// </para>
/// <para>
/// 参考在 ADD 里直接调 <c>fheroes2::selectLanguage()</c>；端口把这件事交回宿主：本类只发
/// <see cref="LanguageAddRequested"/>，宿主弹 <see cref="LanguageSelectionDialog"/> 之后把结果喂给
/// <see cref="AddLanguage"/>。
/// </para>
/// <para>
/// 确定时照 <c>:227-241</c> 写回：先把列表里非当前项逐个 <c>changeLanguage</c>，再把当前项设成主语言，最后把原列表里
/// 已经没了的语言 <c>removeTranslation</c>。这一步由宿主在拿到 <see cref="EditorListOutcome.Accepted"/> 后调
/// <see cref="ApplyTo"/> 完成。
/// </para>
/// </remarks>
public sealed class EditorLanguageWindow : ListBox<EditorLanguageWindow.Row>
{
    /// <summary>Width of the language list, <c>languageArea.width</c> (<c>:63</c>).</summary>
    public const int LanguageAreaWidth = 300;

    /// <summary>Height of the language list, <c>languageArea.height</c> (<c>:63</c>).</summary>
    public const int LanguageAreaHeight = 300;

    /// <summary>Gap around the elements, <c>elementOffset</c> (<c>:61</c>).</summary>
    public const int ElementOffset = 9;

    /// <summary>Room left under the list, <c>listAreaHeightDeduction</c> (<c>:65</c>).</summary>
    public const int ListAreaHeightDeduction = 8;

    /// <summary>Rows on screen at once, <c>SetAreaMaxItems( 10 )</c> (<c>:170</c>).</summary>
    public const int MaxVisibleItemCount = 10;

    /// <summary>Height of a scroll button, <c>topPartHeight</c> (<c>:163</c>).</summary>
    public const int ScrollButtonHeight = 19;

    /// <summary>Width of the scroll bar band, the <c>10</c> of <c>:168</c> plus its frame.</summary>
    public const int ScrollBarWidth = 14;

    /// <summary>Left inset of a row's text, the <c>+ 5</c> of <c>:81</c>.</summary>
    public const int RowTextInsetX = 5;

    /// <summary>Top inset of a row's text, the <c>+ 5</c> of <c>:81</c>.</summary>
    public const int RowTextInsetY = 5;

    /// <summary>Room a row text leaves on both sides, the <c>10</c> of <c>:80</c>.</summary>
    public const int RowTextInsetTotal = 10;

    /// <summary>Gap under the list before the two small buttons, the <c>+ 5</c> of <c>:191</c>.</summary>
    public const int ButtonOffsetY = 5;

    /// <summary>Width of the window's active area, <c>languageArea.width + 50</c> (<c>:138</c>).</summary>
    public const int ActiveWidth = LanguageAreaWidth + 50;

    /// <summary>Height of the window's active area, <c>languageArea.height + 100</c> (<c>:138</c>).</summary>
    public const int ActiveHeight = LanguageAreaHeight + 100;

    /// <summary>The title above the list, <c>_( "Supported languages:" )</c> (<c>:144</c>).</summary>
    public const string Title = "Supported languages:";

    /// <summary>Title of the message about a language that is already in the list, <c>:261</c>.</summary>
    public const string LanguageTitle = "Language";

    /// <summary>
    /// Title of the picker the "add" button opens, <c>_( "Select Language:" )</c>
    /// (<c>dialog_language_selection.cpp:189</c>, the <c>isGameLanguage = false</c> branch the editor asks for).
    /// </summary>
    public const string LanguagePickerTitle = "Select Language:";

    /// <summary>Body of the message about a language that is already in the list, <c>:261</c>.</summary>
    public const string AlreadyExistsMessage = "This language already exists in the list.";

    /// <summary>Body of the message about deleting the only language, <c>:273</c>.</summary>
    public const string LastLanguageMessage = "A map should have at least one language.";

    /// <summary>Title of the CANCEL button's message, <c>:288</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message, <c>:288</c>.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:291</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message, <c>:291</c>.</summary>
    public const string OkayMessage = "Click to apply changes.";

    /// <summary>Title of the "add" button's message, <c>:294</c>.</summary>
    public const string AddTitle = "Add Language";

    /// <summary>Body of the "add" button's message, <c>:294</c>.</summary>
    public const string AddMessage = "Add an additional language.";

    /// <summary>Title of the "delete" button's message, <c>:297</c>.</summary>
    public const string DeleteTitle = "Delete Language";

    /// <summary>Body of the "delete" button's message, <c>:297</c>.</summary>
    public const string DeleteMessage = "Delete an existing language.";

    /// <summary>
    /// The rows: <c>languages</c> (<c>:172</c>), held as the values of <see cref="SupportedLanguage"/> because
    /// <see cref="ListBox{T}"/> takes reference types only.
    /// </summary>
    private readonly List<Row> _languages;

    private readonly SupportedLanguage[] _originalSelection;

    private readonly SupportedLanguage _mainLanguage;

    private readonly StandardWindow _window;
    private readonly EditorListArtwork _artwork;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly Rect _languageRoi;
    private readonly int _scrollBarX;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _add;
    private readonly ButtonSprite _delete;
    private readonly Point _titlePosition;
    private readonly int _titleWidth;

    private EditorLanguageWindow(MapTranslationState translations, EditorListArtwork artwork, Size screen,
        IGlyphSource headerFont, IGlyphSource bodyFont)
        : base(new Point(0, 0))
    {
        _artwork = artwork;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _mainLanguage = translations.MainLanguage;
        _languages = [.. translations.Languages.Select(static language => new Row(language))];
        _originalSelection = [.. translations.Languages];

        _window = StandardWindow.CreateCentered(new Size(ActiveWidth, ActiveHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        // 参考 :142-150：标题离顶 9 像素居中，列表从标题下 9 像素处开始。
        int titleWidth = GlyphText.Measure(headerFont, Title);
        int titleY = active.Y + ElementOffset;
        int labelHeight = Math.Max(1, headerFont.LineHeight);

        _titlePosition = new Point(active.X + ((active.Width - titleWidth) / 2), titleY);
        _titleWidth = titleWidth;
        _languageRoi = new Rect(active.X + ElementOffset, titleY + labelHeight + ElementOffset, LanguageAreaWidth,
            LanguageAreaHeight);

        // 参考 :159-169 把滚动条放在列表右侧 5 像素、槽占满列表宽度；照抄会整条压在窗外，端口把槽收进窗口内
        // （与 EditorSphinxWindow 的偏差同理）。
        _scrollBarX = _languageRoi.Right + 5;

        SetTopLeft(_languageRoi.Position);
        SetAreaItems(new Rect(_languageRoi.X, _languageRoi.Y, _languageRoi.Width,
            _languageRoi.Height - ListAreaHeightDeduction));
        SetAreaMaxItems(MaxVisibleItemCount);
        SetScrollButtonUp(new ButtonSprite(artwork.ScrollBarFrames[0], artwork.ScrollBarFrames[1], null,
            _scrollBarX + 1, _languageRoi.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollBarFrames[2], artwork.ScrollBarFrames[3], null,
            _scrollBarX + 1, _languageRoi.Bottom - 15));
        SetScrollBarArea(new Rect(_scrollBarX + 3, _languageRoi.Y + ScrollButtonHeight, 10,
            _languageRoi.Height - 21 - ScrollButtonHeight));
        SetScrollBarImage(artwork.ScrollBarFrames[4]);

        // 参考 :186-195：增贴在列表左下，删贴在列表右下，各自离列表底 5 像素。
        int buttonWidth = artwork.ButtonWidth;
        int buttonY = _languageRoi.Bottom + ButtonOffsetY;

        _add = new ButtonSprite(artwork.Add.Released, artwork.Add.Pressed);
        _add.SetPosition(_languageRoi.X, buttonY);

        _delete = new ButtonSprite(artwork.Delete.Released, artwork.Delete.Pressed);
        _delete.SetPosition(_languageRoi.Right - buttonWidth, buttonY);

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);

        Point okayPosition = _window.RenderPosition(new Point(20, 7), new Size(_okay.Area.Width, _okay.Area.Height),
            WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_cancel.Area.Width, _cancel.Area.Height), WindowPadding.BottomRight);

        _okay.SetPosition(okayPosition.X, okayPosition.Y);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        SetListContent(_languages);
        SetCurrent(0);
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="translations">The map's languages; its main language and translation list are copied.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="screen">Screen size, <c>display.width()</c>/<c>height()</c> (<c>:138</c>).</param>
    /// <param name="headerFont">The normal yellow font, the title and the selected row.</param>
    /// <param name="bodyFont">The normal white font, every other row.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorLanguageWindow Create(MapTranslationState translations, EditorListArtwork artwork, Size screen,
        IGlyphSource headerFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        return new EditorLanguageWindow(translations, artwork, screen, headerFont, bodyFont);
    }

    /// <summary>The window the list sits in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The rect of the language list, <c>rumorsRoi</c> (<c>:149</c>).</summary>
    public Rect LanguageArea => _languageRoi;

    /// <summary>The rect of the scroll bar band.</summary>
    public Rect ScrollBarArea => new(_scrollBarX, _languageRoi.Y, ScrollBarWidth, _languageRoi.Height);

    /// <summary>The OKAY button.</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The "add" button, <c>buttonAdd</c> (<c>:191</c>).</summary>
    public ButtonSprite AddButton => _add;

    /// <summary>The "delete" button, <c>buttonDelete</c> (<c>:194</c>).</summary>
    public ButtonSprite DeleteButton => _delete;

    /// <summary>The title above the list.</summary>
    public string TitleText => Title;

    /// <summary>The languages as they stand, main language first (<c>languages</c>, <c>:172-176</c>).</summary>
    public IReadOnlyList<SupportedLanguage> Languages =>
        [.. _languages.Select(static row => row.Language)];

    /// <summary>The languages the window opened with, <c>originalSelection</c> (<c>:178</c>).</summary>
    public IReadOnlyList<SupportedLanguage> OriginalLanguages => _originalSelection;

    /// <summary>The main language of the map when the window opened, <c>mapFormat.mainLanguage</c>.</summary>
    public SupportedLanguage MainLanguage => _mainLanguage;

    /// <summary>The language the author would make the main one, <c>languageList.GetCurrent()</c> (<c>:234</c>).</summary>
    public SupportedLanguage SelectedLanguage =>
        IsSelected ? _languages[CurrentId].Language : _mainLanguage;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorListOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>
    /// Whether the author clicked "add", so the host opens the language picker (<c>:253-256</c>). What it picks goes to
    /// <see cref="AddLanguage"/>.
    /// </summary>
    public bool LanguageAddRequested { get; private set; }

    /// <summary>The rect of the row at <paramref name="index"/>, as <see cref="ListBox{T}"/> lays the rows out.</summary>
    /// <param name="index">Zero for the first row.</param>
    /// <returns>The rect of the row.</returns>
    public Rect RowArea(int index) => new(ItemsArea.X, ItemsArea.Y + ((index * ItemsArea.Height) / MaxVisibleItemCount),
        ItemsArea.Width, ItemsArea.Height / MaxVisibleItemCount);

    /// <summary>
    /// Builds the window on the languages of a map specification state, the way the map specifications window opens it
    /// (<c>editor_map_specs_window.cpp:2297-2298</c>).
    /// </summary>
    /// <param name="specs">The state whose <see cref="MapSpecsState.Translations"/> the window edits.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="screen">Screen size, <c>display.width()</c>/<c>height()</c> (<c>:138</c>).</param>
    /// <param name="headerFont">The normal yellow font, the title and the selected row.</param>
    /// <param name="bodyFont">The normal white font, every other row.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorLanguageWindow Create(MapSpecsState specs, EditorListArtwork artwork, Size screen,
        IGlyphSource headerFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(specs);

        return Create(specs.Translations, artwork, screen, headerFont, bodyFont);
    }

    /// <summary>Appends a language the author picked in the language picker (<c>:258-269</c>).</summary>
    /// <param name="language">The language to add.</param>
    /// <returns>
    /// <see langword="false"/> when the language is the map's main one (silently ignored, <c>:256</c>) or is already in
    /// the list; <see cref="Popup"/> then says so.
    /// </returns>
    public bool AddLanguage(SupportedLanguage language)
    {
        if (language == _mainLanguage)
        {
            return false;
        }

        if (_languages.Exists(row => row.Language == language))
        {
            Popup = new DialogPopup(LanguageTitle, AlreadyExistsMessage);

            return false;
        }

        _languages.Add(new Row(language));
        UpdateScrollBarImage();
        NeedRedraw = true;

        return true;
    }

    /// <summary>Drops the selected language, <c>languageList.RemoveSelected()</c> (<c>:271-286</c>).</summary>
    /// <returns>
    /// <see langword="false"/> when it is the only language left or nothing is selected; <see cref="Popup"/> says so
    /// for the former.
    /// </returns>
    public bool RemoveSelectedLanguage()
    {
        if (_languages.Count == 1)
        {
            Popup = new DialogPopup(LanguageTitle, LastLanguageMessage);

            return false;
        }

        if (!IsSelected)
        {
            return false;
        }

        RemoveSelected();
        SetCurrent(0);
        UpdateScrollBarImage();
        NeedRedraw = true;

        return true;
    }

    /// <summary>
    /// Writes the list back, the <c>:222-241</c> block: every language but the selected one becomes a translation, then
    /// the selected one becomes the main language, and languages that were dropped leave the map.
    /// </summary>
    /// <param name="translations">The state to write into.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="translations"/> is <see langword="null"/>.</exception>
    public bool ApplyTo(MapTranslationState translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        SupportedLanguage selected = SelectedLanguage;
        SupportedLanguage[] current = [.. _languages.Select(static row => row.Language)];

        if (current.SequenceEqual(_originalSelection) && selected == _mainLanguage)
        {
            return false;
        }

        for (int i = 0; i < current.Length; ++i)
        {
            if (CurrentId != i)
            {
                _ = translations.ChangeLanguage(current[i]);
            }
        }

        _ = translations.ChangeLanguage(selected);

        foreach (SupportedLanguage language in _originalSelection)
        {
            if (!current.Contains(language))
            {
                _ = translations.RemoveTranslation(language);
            }
        }

        return true;
    }

    /// <summary>
    /// Writes the list back into a map specification state, the <c>:222-241</c> block for the caller that opened the
    /// window from <see cref="EditorMapSpecsWindow"/>.
    /// </summary>
    /// <remarks>
    /// 参考把名字与描述直接改在 <c>mapFormat</c> 上，规格窗返回后调 <c>renderMapName()</c> 重画；端口里
    /// <see cref="MapSpecsState.Name"/> / <see cref="MapSpecsState.Description"/> 就存在当前主语言的文本槽里
    /// （ADR-320），所以写回之后规格窗什么都不用搬——再读一遍即可拿到新主语言的文本。
    /// </remarks>
    /// <param name="specs">The state whose <see cref="MapSpecsState.Translations"/> takes the list.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="specs"/> is <see langword="null"/>.</exception>
    public bool ApplyTo(MapSpecsState specs)
    {
        ArgumentNullException.ThrowIfNull(specs);

        return ApplyTo(specs.Translations);
    }

    /// <summary>
    /// Assembles the language picker the "add" button asks the host to open (<c>:253-256</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>getSupportedLanguages()</c> 全集，初始选中主语言，<c>selectLanguage( ..., isGameLanguage = false )</c>
    /// 因而标题是「Select Language:」而不是「Select Game Language:」。按钮用 <see cref="EditorListArtwork"/> 里
    /// OKAY/CANCEL 的那两对帧，与语言窗自己底下那两个按钮同一套素材。
    /// </remarks>
    /// <param name="screen">Screen size, for centring the window.</param>
    /// <returns>The picker; the host runs its own loop and reads <see cref="LanguageSelectionDialog.SelectedIndex"/>.</returns>
    public LanguageSelectionDialog CreateLanguagePicker(Size screen)
    {
        SupportedLanguage[] all = Enum.GetValues<SupportedLanguage>();
        int contentHeight = LanguageSelectionDialog.GetRequiredHeight(_bodyFont.LineHeight, all.Length);
        StandardWindow window = StandardWindow.CreateCentered(
            new Size(LanguageSelectionDialog.ContentWidth, contentHeight), renderBackground: true, screen);
        ButtonGroup buttons = ButtonGroup.CreateStandard(
            window.ActiveArea, DialogButton.Ok | DialogButton.Cancel, new EditorButtonArtwork(_artwork));

        return new LanguageSelectionDialog(
            window, buttons, _headerFont, _bodyFont, _headerFont, LanguagePickerTitle,
            [.. all.Select(GameLanguage.GetName)], Array.IndexOf(all, _mainLanguage));
    }

    /// <summary>Draws the whole window, the <c>display.render()</c> calls of <c>:203</c> and <c>:300-304</c>.</summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        _window.Draw(target);

        GlyphText.DrawLine(_headerFont, target, _titlePosition, Title,
            new Rect(_titlePosition.X, _titlePosition.Y, _titleWidth, _headerFont.LineHeight));

        StandardWindow.ApplyTextBackgroundShading(target, _languageRoi);

        // 列表的重绘里带着自己那块底纹与滚动条槽（RedrawBackground）。
        Redraw(target);

        PaintButton(_add, target, events);
        PaintButton(_delete, target, events);
        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, the body of the <c>while ( le.HandleEvents() )</c> loop of <c>:208-305</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The result of the frame; <see cref="EditorListOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorListOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Result = EditorListOutcome.None;
        Popup = null;
        LanguageAddRequested = false;
        IsDoubleClicked = false;

        // 参考 :214-216：CANCEL 什么都不做。
        if (events.MouseClickLeft(_cancel.Area))
        {
            Result = EditorListOutcome.Cancelled;

            return Result;
        }

        // 参考 :218-244：OKAY 或双击列表项都算确定。
        if (events.MouseClickLeft(_okay.Area))
        {
            Result = EditorListOutcome.Accepted;

            return Result;
        }

        QueueEventProcessing(events);

        if (IsDoubleClicked)
        {
            Result = EditorListOutcome.Accepted;

            return Result;
        }

        // 参考 :253-270：ADD 交宿主弹语言选择窗。
        if (events.MouseClickLeft(_add.Area))
        {
            LanguageAddRequested = true;

            return Result;
        }

        // 参考 :271-286：DELETE。
        if (events.MouseClickLeft(_delete.Area))
        {
            _ = RemoveSelectedLanguage();

            return Result;
        }

        // 参考 :287-298：右键说明。
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
        else if (events.IsMouseRightButtonPressedInArea(_delete.Area))
        {
            Popup = new DialogPopup(DeleteTitle, DeleteMessage);
        }

        return Result;
    }

    /// <summary>Draws one row of the list, <c>LanguageListBox::RedrawItem()</c> (<c>:76-82</c>).</summary>
    /// <param name="item">The language.</param>
    /// <param name="x">Left of the row.</param>
    /// <param name="y">Top of the row.</param>
    /// <param name="current">Whether the row is selected.</param>
    /// <param name="output">Frame to draw into.</param>
    public override void RedrawItem(Row item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        IGlyphSource font = current ? _headerFont : _bodyFont;
        Rect area = new(x + RowTextInsetX, y + RowTextInsetY, LanguageAreaWidth - RowTextInsetTotal,
            Math.Max(1, font.LineHeight));
        string text = GlyphText.FitToOneRow(font, GameLanguage.GetName(item.Language), area.Width);

        GlyphText.DrawLine(font, output, area.Position, text, ItemsArea);
    }

    /// <summary>Draws the background of one row, <c>LanguageListBox::RedrawBackground()</c> (<c>:84-87</c>).</summary>
    /// <param name="destination">Top left of the list.</param>
    /// <param name="output">Frame to draw into.</param>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _languageRoi);
        StandardWindow.RenderScrollbarBackground(output, ScrollBarArea, _artwork.ScrollBarBackground);
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
    public override void ActionListSingleClick(Row item) => _ = item;

    /// <summary>Keeps the double click of the reference, <c>ActionListDoubleClick()</c> (<c>:99-102</c>).</summary>
    /// <param name="item">The language that was double clicked.</param>
    public override void ActionListDoubleClick(Row item)
    {
        _ = item;

        IsDoubleClicked = true;
    }

    /// <inheritdoc/>
    public override void ActionListPressRight(Row item) => _ = item;

    /// <summary>Whether the last frame double clicked a row, <c>isDoubleClicked()</c> (<c>:104-107</c>).</summary>
    private bool IsDoubleClicked { get; set; }

    /// <summary>Draws a small button, pressed when the pointer holds the left button over it.</summary>
    /// <param name="button">The button.</param>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame.</param>
    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent events) =>
        button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);

    /// <summary>One row of the list: <see cref="ListBox{T}"/> takes reference types, a language is an enum.</summary>
    public sealed class Row
    {
        /// <summary>Wraps a language.</summary>
        /// <param name="language">The language of the row.</param>
        public Row(SupportedLanguage language) => Language = language;

        /// <summary>The language of the row.</summary>
        public SupportedLanguage Language { get; }
    }

    /// <summary>Feeds <see cref="ButtonGroup"/> the OKAY/CANCEL frames of <see cref="EditorListArtwork"/>.</summary>
    private sealed class EditorButtonArtwork : IDialogButtonArtworkSource
    {
        private readonly EditorListArtwork _artwork;

        public EditorButtonArtwork(EditorListArtwork artwork) => _artwork = artwork;

        public DialogButtonArtwork Get(DialogButton button) => button switch
        {
            DialogButton.Ok => new DialogButtonArtwork(_artwork.Okay.Released, _artwork.Okay.Pressed),
            DialogButton.Cancel => new DialogButtonArtwork(_artwork.Cancel.Released, _artwork.Cancel.Pressed),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button,
                "The list artwork only has OKAY and CANCEL frames."),
        };
    }
}
