using System.Globalization;
using System.Text;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The frames the save/load window needs, all of them read from the original AGG files.
/// </summary>
/// <remarks>
/// 参考：<c>dialog_selectfile.cpp:359-400</c>。滚动条<b>不</b>使用 <c>SCROLL.ICN</c> 的固定几何，而是像参考一样
/// 自己摆放上下两个按钮、滑槽和滑块；排序标记来自 <c>CELLWIN.ICN</c>（邪恶界面为 <c>CELLWINE.ICN</c>）的第 4、5 帧，
/// 滑槽背景是 <c>ADVBORD.ICN</c> 第 0 帧的一条竖线，OK/CANCEL 是标准小按钮。
/// </remarks>
public sealed record SelectFileArtwork
{
    /// <summary>Frames of the scroll bar, <c>SCROLL.ICN</c> / <c>SCROLLE.ICN</c>.</summary>
    /// <remarks>第 0..3 帧是上/下按钮的松开与按下状态，第 4 帧是滑块。</remarks>
    public required IReadOnlyList<Sprite> ScrollBarFrames { get; init; }

    /// <summary>Frames of the sort mark table, <c>CELLWIN.ICN</c> / <c>CELLWINE.ICN</c>.</summary>
    /// <remarks>第 4 帧是空框，第 5 帧是勾选标记。</remarks>
    public required IReadOnlyList<Sprite> SortMarkFrames { get; init; }

    /// <summary>Single column strip of <c>ADVBORD.ICN</c> frame 0, the scroll bar groove background.</summary>
    public required Sprite ScrollBarBackground { get; init; }

    /// <summary>Source of the OK/CANCEL sprites, <c>BUTTON_SMALL_OKAY_GOOD</c> and friends.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window indexes actually exists.</summary>
    public bool IsComplete => ScrollBarFrames.Count > 4 && SortMarkFrames.Count > 5;
}

/// <summary>
/// The window of <c>SelectFileLoad</c> / <c>SelectFileSave</c>: a title, a name column, a date column, an
/// editable file name and the standard OK/CANCEL row. A port of the anonymous file list box and window of
/// <c>dialog_selectfile.cpp:359-733</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference builds a <c>StandardWindow</c> whose height depends on the number of files, then a
/// <c>FileInfoListBox</c>, then runs a blocking loop that repaints only the damaged parts. The port keeps
/// the layout but repaints the whole window every frame, which is why <see cref="Draw"/> owns the title and
/// the two column headers as well: there is no partial redraw to keep in sync, so the visible result is the
/// same while the code has one place per pixel. <see cref="RedrawBackground"/> still draws the shading and the
/// scroll bar groove, because <see cref="ListBox{T}.Redraw"/> calls it.
/// </para>
/// <para>
/// <b>主动偏差：</b>
/// </para>
/// <list type="bullet">
///     <item>
///     <description>
///     参考用全局热键 <c>DEFAULT_DELETE</c> 删档；移植版没有热键层，直接在 <see cref="ProcessEvents"/> 里识别
///     <c>Delete</c>，并由调用方弹确认框后调用 <see cref="RemoveSelectedFile"/>。
///     </description>
///     </item>
///     <item>
///     <description>
///     参考在文本输入框右侧放一个虚拟键盘按钮；移植版还没有虚拟键盘对话框的装配器，因此不画这个按钮，
///     文件名只能靠物理键盘输入。其余行为不变。
///     </description>
///     </item>
///     <item>
///     <description>
///     参考靠 <c>Text::fitToOneRow()</c> 截断过长的文件名；<see cref="GlyphText"/> 还没有对应方法，这里用
///     <see cref="FitToOneRow"/> 做同样的事（截断后补 <c>"..."</c>）。
///     </description>
///     </item>
/// </list>
/// </remarks>
public sealed class SelectFileView : ListBox<SaveFileEntry>
{
    /// <summary>宽度基准：文件名列的宽度，参考 <c>maxFileNameWidth = 260</c>。</summary>
    public const int MaxFileNameWidth = 260;

    /// <summary>文件名列之外的水平余量，<c>204</c>，即窗口宽 <see cref="MaxFileNameWidth"/> + 204。</summary>
    public const int WidthDeduction = 204;

    /// <summary>列表左边距，<c>activeArea.x + 24</c>。</summary>
    public const int ListInsetX = 24;

    /// <summary>列表上边距，<c>activeArea.y + 57</c>：标题、列头都在它上面。</summary>
    public const int ListTop = 57;

    /// <summary>列表比窗口内容窄多少，<c>activeArea.width - 75</c>，右侧留给滚动条。</summary>
    public const int ListWidthDeduction = 75;

    /// <summary>列表比窗口内容矮多少，<c>activeArea.height - 120</c>，下方留给文件名与按钮。</summary>
    public const int ListHeightDeduction = 120;

    /// <summary>行数少于此值时窗口按此值算高度，<c>11</c>。</summary>
    public const int MinimumVisibleRows = 11;

    /// <summary>行数多于此值时窗口按此值算高度，<c>22</c>。</summary>
    public const int MaximumVisibleRows = 22;

    /// <summary>列头高度，<c>28</c>。</summary>
    public const int HeaderHeight = 28;

    /// <summary>文件名输入框高度，<c>23</c>。</summary>
    public const int TextInputHeight = 23;

    /// <summary>输入框与列表之间、输入框内部与列表内部的细缝。</summary>
    public const int SmallGap = 3;

    /// <summary>日期列在文件名列之后额外的间隔，<c>8</c>，即输入框比文件名列宽 8 像素。</summary>
    public const int DateColumnGap = 8;

    /// <summary>滚动条距窗口内容右边缘的距离，<c>35</c>。</summary>
    public const int ScrollBarRightInset = 35;

    /// <summary>滑槽宽度，<c>10</c>。</summary>
    public const int ScrollBarGrooveWidth = 10;

    /// <summary>上下按钮占掉的高度，<c>19</c>。</summary>
    public const int ScrollBarButtonHeight = 19;

    /// <summary>日期列中月份、日期、小时、分钟相对列首的横向偏移，参考 <c>redrawDateTime()</c>。</summary>
    public const int MonthOffsetX = 4;

    /// <summary>日期列中“日”的居中基准偏移，<c>56</c>。</summary>
    public const int DayCenterOffsetX = 56;

    /// <summary>日期列中逗号的位置，<c>66</c>。</summary>
    public const int CommaOffsetX = 66;

    /// <summary>日期列中小时的居中基准偏移，<c>82</c>。</summary>
    public const int HourCenterOffsetX = 82;

    /// <summary>日期列中冒号的位置，<c>92</c>。</summary>
    public const int ColonOffsetX = 92;

    /// <summary>日期列中分钟的居中基准偏移，<c>105</c>。</summary>
    public const int MinuteCenterOffsetX = 105;

    private const string Ellipsis = "...";

    private readonly StandardWindow _window;
    private readonly SelectFileArtwork _artwork;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly TextInputField? _textInput;
    private readonly ButtonGroup _buttons;
    private readonly List<SaveFileEntry> _files = [];
    private readonly Rect _listArea;
    private readonly Rect _nameHeaderArea;
    private readonly Rect _dateHeaderArea;
    private readonly Rect _textInputArea;
    private readonly Rect _textInputTextArea;
    private readonly Rect _selectedDateArea;
    private readonly Rect _grooveArea;
    private readonly int _scrollBarX;

    private SaveFileSortingMethod _sorting;
    private DialogButton _result;
    private DialogPopup? _popup;
    private string _fileName = string.Empty;
    private int _cursorPosition;
    private bool _isDoubleClicked;
    private bool _deleteRequested;

    private SelectFileView(Size screen, string directory, int fileCount, SaveFileSortingMethod sorting,
        bool isEditing, SelectFileArtwork artwork, IGlyphSource headerFont, IGlyphSource bodyFont)
        : base(new Point(0, 0))
    {
        Directory = directory;
        IsEditing = isEditing;
        _sorting = sorting;
        _artwork = artwork;
        _headerFont = headerFont;
        _bodyFont = bodyFont;

        int visibleRows = Math.Clamp(fileCount, MinimumVisibleRows, MaximumVisibleRows);
        int height = Math.Min(screen.Height - 100,
            (bodyFont.LineHeight * visibleRows) + SmallGap + 4 + ListHeightDeduction);
        Size dialogSize = new(MaxFileNameWidth + WidthDeduction, Math.Max(1, height));

        _window = StandardWindow.CreateCentered(dialogSize, renderBackground: true, screen);
        Rect active = _window.ActiveArea;

        _listArea = new Rect(active.X + ListInsetX, active.Y + ListTop,
            active.Width - ListWidthDeduction, active.Height - ListHeightDeduction);

        _nameHeaderArea = new Rect(_listArea.X, _listArea.Y - HeaderHeight,
            MaxFileNameWidth + DateColumnGap, HeaderHeight);

        _textInputArea = new Rect(_listArea.X, _listArea.Y + _listArea.Height + SmallGap,
            MaxFileNameWidth + DateColumnGap, TextInputHeight);

        _dateHeaderArea = new Rect(_textInputArea.Right, _nameHeaderArea.Y,
            _listArea.Width - _textInputArea.Width, HeaderHeight);

        _selectedDateArea = new Rect(_textInputArea.Right, _textInputArea.Y,
            _listArea.Width - _textInputArea.Width, TextInputHeight);

        _textInputTextArea = new Rect(_textInputArea.X + 4, _textInputArea.Y + 2,
            MaxFileNameWidth, _textInputArea.Height - 2);

        // 参考：listRoi.x + 24, listRoi.y + 3, listRoi.width - 3, listRoi.height - 4。
        SetAreaItems(new Rect(_listArea.X, _listArea.Y + 3, _listArea.Width - 3, _listArea.Height - 4));
        SetAreaMaxItems(Math.Max(1, (_listArea.Height - 7) / Math.Max(1, bodyFont.LineHeight)));

        _scrollBarX = active.X + active.Width - ScrollBarRightInset;
        _grooveArea = new Rect(_scrollBarX + 3, _listArea.Y + ScrollBarButtonHeight,
            ScrollBarGrooveWidth, _listArea.Height - (ScrollBarButtonHeight * 2));

        SetScrollButtonUp(new ButtonSprite(artwork.ScrollBarFrames[0], artwork.ScrollBarFrames[1], null,
            _scrollBarX + 1, _listArea.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollBarFrames[2], artwork.ScrollBarFrames[3], null,
            _scrollBarX + 1, _listArea.Y + _listArea.Height - 15));
        SetScrollBarArea(_grooveArea);
        SetScrollBarImage(artwork.ScrollBarFrames[4]);

        _buttons = ButtonGroup.CreateStandard(active, DialogButton.Ok | DialogButton.Cancel, artwork.Buttons);

        if (isEditing)
        {
            _textInput = new TextInputField(_textInputTextArea, isMultiLine: false, isCenterAligned: false);
        }

        SyncOkayButton();
    }

    /// <summary>Directory the entries were read from, <c>saveDir</c> of the reference.</summary>
    public string Directory { get; }

    /// <summary>Whether the window edits names, i.e. <c>SelectFileSave</c> rather than <c>SelectFileLoad</c>.</summary>
    public bool IsEditing { get; }

    /// <summary>The window, for callers that need its active area, e.g. to draw a shadow.</summary>
    public StandardWindow Window => _window;

    /// <summary>Yellow font, the one the reference uses for selected names and dates.</summary>
    public IGlyphSource HeaderFont => _headerFont;

    /// <summary>White font, the one <see cref="TextInputField"/> and message boxes use for body text.</summary>
    public IGlyphSource BodyFont => _bodyFont;

    /// <summary>Buttons the OK/CANCEL row was built from, reused by the message boxes of the loop.</summary>
    public IDialogButtonArtworkSource ButtonArtwork => _artwork.Buttons;

    /// <summary>File name column header, also the click target of the "sort by name" toggle.</summary>
    public Rect NameHeaderArea => _nameHeaderArea;

    /// <summary>Date column header, also the click target of the "sort by date" toggle.</summary>
    public Rect DateHeaderArea => _dateHeaderArea;

    /// <summary>The list area, shading and rows included.</summary>
    public Rect ListArea => _listArea;

    /// <summary>The file name input box.</summary>
    public Rect TextInputArea => _textInputArea;

    /// <summary>Current sort order, persisted in <see cref="Settings.SaveFileSortType"/> by the caller.</summary>
    public SaveFileSortingMethod Sorting => _sorting;

    /// <summary>The name shown in the input field and, for a save, the name the game will be written as.</summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value ?? string.Empty;
            _cursorPosition = Math.Clamp(_cursorPosition, 0, Encoding.UTF8.GetByteCount(_fileName));
            SyncOkayButton();
        }
    }

    /// <summary>Caret offset in bytes inside <see cref="FileName"/>, counted from the start.</summary>
    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            _cursorPosition = Math.Clamp(value, 0, Encoding.UTF8.GetByteCount(_fileName));
            _textInput?.Set(_fileName, _cursorPosition);
        }
    }

    /// <summary>The answer a button or a double click produced during the last <see cref="ProcessEvents"/>.</summary>
    public DialogButton Result => _result;

    /// <summary>Tooltip the last frame asked for, usually a right click explanation, or <see langword="null"/>.</summary>
    public DialogPopup? Popup => _popup;

    /// <summary>Reads <see cref="Popup"/> and clears it, so that the message box is shown once.</summary>
    /// <returns>The tooltip, or <see langword="null"/> when the last frame asked for none.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;
        return popup;
    }

    /// <summary>Whether the player pressed <c>Delete</c> on a selected file during the last frame.</summary>
    public bool DeleteRequested => _deleteRequested;

    /// <summary>The selected entry, or <see langword="null"/> when the list has no selection.</summary>
    public SaveFileEntry? SelectedFile
    {
        get
        {
            int index = CurrentId;
            return index >= 0 && index < _files.Count ? _files[index] : null;
        }
    }

    /// <summary>The rows in their current order, the <c>getList()</c> of the reference's list box.</summary>
    public IReadOnlyList<SaveFileEntry> Files => _files;

    /// <summary>
    /// Lays out the window, the reference's <c>std::min( display.height() - 100, listHeight + 3 + 4 + 120 )</c>
    /// (<c>dialog_selectfile.cpp:359-400</c>).
    /// </summary>
    /// <param name="screen">Screen to centre the window on, only its height is used for the cap.</param>
    /// <param name="directory">Directory the file list belongs to.</param>
    /// <param name="fileCount">
    /// Number of entries the list will hold; it decides the window height, so <see cref="SetFiles"/> must be
    /// handed the same number of entries (or fewer, at the price of empty room).
    /// </param>
    /// <param name="sorting">Initial sort order, <c>Settings::getSaveFileSortingMethod()</c> in the reference.</param>
    /// <param name="isEditing">Whether the file name can be typed into.</param>
    /// <param name="artwork">Scroll bar, sort marks and buttons.</param>
    /// <param name="headerFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">White font, <c>FontType::normalWhite()</c>.</param>
    /// <returns>The window, or <see langword="null"/> when the artwork is incomplete.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SelectFileView? TryCreate(Size screen, string directory, int fileCount,
        SaveFileSortingMethod sorting, bool isEditing, SelectFileArtwork artwork, IGlyphSource headerFont,
        IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentOutOfRangeException.ThrowIfNegative(fileCount);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new SelectFileView(screen, directory, fileCount, sorting, isEditing, artwork, headerFont, bodyFont);
    }

    /// <summary>Fills the list and applies <paramref name="sorting"/>.</summary>
    /// <remarks>
    /// <see cref="ListBox{T}.SetListContent"/> selects the first row, exactly like the reference's
    /// <c>SetListContent()</c>; the caller decides afterwards whether that row stays selected.
    /// </remarks>
    /// <param name="files">Entries to show, in any order; the list sorts them.</param>
    /// <param name="sorting">Sort order to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is <see langword="null"/>.</exception>
    public void SetFiles(IReadOnlyList<SaveFileEntry> files, SaveFileSortingMethod sorting)
    {
        ArgumentNullException.ThrowIfNull(files);

        _files.Clear();
        _files.AddRange(files);
        _sorting = sorting;
        SelectFileDialog.SortSaveFiles(_files, sorting);
        SetListContent(_files);
        UpdateScrollBarImage();
        SyncOkayButton();
    }

    /// <summary>
    /// Selects the row of <paramref name="path"/> when it is in the list and moves the caret to the end of
    /// its name, the <c>findInMapInfos()</c> / <c>selectFileListSimple()</c> preselect block
    /// (<c>dialog_selectfile.cpp:407-421</c>).
    /// </summary>
    /// <param name="path">Save file the game remembers, or <see langword="null"/>.</param>
    /// <returns>Whether the file was found and selected.</returns>
    public bool SelectFile(string? path)
    {
        int index = SelectFileDialog.FindSaveFileIndex(_files, path);

        // 参考找不到时什么也不做：SetListContent() 选中的第一行留着，输入框由调用方填。
        if (index < 0)
        {
            return false;
        }

        SetCurrent(index);

        // 参考在同一段里用 GetStem( lastFile ) 填输入框，保留调用方给的大小写。
        FileName = SelectFileDialog.GetStem(path!);
        CursorPosition = Encoding.UTF8.GetByteCount(_fileName);
        return true;
    }

    /// <summary>Clears the selection and the input field, the "no file chosen yet" state of a load window.</summary>
    public void ClearSelection()
    {
        Unselect();
        FileName = string.Empty;
        CursorPosition = 0;
    }

    /// <summary>
    /// Removes the selected entry and selects its predecessor, the <c>Delete</c> branch of
    /// <c>dialog_selectfile.cpp:551-568</c>. The caller has to confirm first.
    /// </summary>
    /// <returns>Whether a row was removed.</returns>
    public bool RemoveSelectedFile()
    {
        SaveFileEntry? selected = SelectedFile;
        if (selected is null)
        {
            return false;
        }

        int index = CurrentId;
        RemoveSelected();
        SyncOkayButton();

        if (_files.Count == 0)
        {
            FileName = string.Empty;
            CursorPosition = 0;
        }
        else
        {
            SetCurrent(Math.Clamp(index - 1, 0, _files.Count - 1));
        }

        return true;
    }

    /// <summary>Switches the sort order and keeps the selected file selected, <c>switchFileSorting</c>.</summary>
    /// <param name="sorting">New order.</param>
    /// <returns>Whether the order actually changed.</returns>
    public bool SwitchSorting(SaveFileSortingMethod sorting)
    {
        if (_sorting == sorting)
        {
            return false;
        }

        SaveFileEntry? selected = SelectedFile;
        _sorting = sorting;
        SelectFileDialog.SortSaveFiles(_files, sorting);
        SetListContent(_files);
        Unselect();

        if (selected is not null)
        {
            int index = _files.FindIndex(file => string.Equals(file.Path, selected.Path,
                StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                SetCurrent(index);
            }
        }

        UpdateScrollBarImage();
        return selected is not null;
    }

    /// <summary>
    /// One frame of input: buttons first, then the two sort headers, then the file name field and the list,
    /// the order of <c>dialog_selectfile.cpp:470-733</c>.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="hotKey">Hot key pressed this frame, usually Enter or Escape.</param>
    /// <returns>Whether anything visible changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, ButtonHotKey? hotKey = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        _result = DialogButton.None;
        _popup = null;
        _deleteRequested = false;
        _isDoubleClicked = false;

        DialogButton answer = _buttons.ProcessEvents(events, hotKey);
        if (answer != DialogButton.None)
        {
            _result = answer;
            return true;
        }

        bool changed = false;

        if (events.MouseClickLeft(_nameHeaderArea))
        {
            changed |= SwitchSorting(SaveFileSortingMethod.Filename);
        }
        else if (events.MouseClickLeft(_dateHeaderArea))
        {
            changed |= SwitchSorting(SaveFileSortingMethod.Timestamp);
        }

        if (events.IsMouseRightButtonPressedInArea(_nameHeaderArea))
        {
            _popup = new DialogPopup("Sort by Name", "Click here if you wish to sort save files by their name.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_dateHeaderArea))
        {
            _popup = new DialogPopup("Sort by Date",
                "Click here if you you wish to sort save files by their last modified date.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_buttons[1].Area))
        {
            _popup = new DialogPopup("Cancel", "Exit this menu without doing anything.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = IsEditing
                ? new DialogPopup("Okay", "Click to save the current game.")
                : new DialogPopup("Okay", "Click to load a previously saved game.");
        }

        if (_popup is not null)
        {
            return true;
        }

        if (IsEditing && events.MouseClickLeft(_textInputArea))
        {
            // 参考：点击输入框会把光标放到点击处；原本选中的行随之取消选中，但名字保留下来继续编辑。
            CursorPosition = _textInput?.GetCursorInTextPosition(events.CursorPosition) ?? 0;
            if (CurrentId >= 0)
            {
                Unselect();
                changed = true;
            }

            return changed;
        }

        bool listEvent = QueueEventProcessing(events);

        if (listEvent)
        {
            if (_isDoubleClicked)
            {
                _result = DialogButton.Ok;
                return true;
            }

            if (CurrentId >= 0 && _files[CurrentId] is { } file)
            {
                FileName = file.Name;
                CursorPosition = Encoding.UTF8.GetByteCount(file.Name);
            }

            return true;
        }

        if (!IsEditing)
        {
            return changed;
        }

        if (events.PressedKeyValue == Key.Delete && CurrentId >= 0)
        {
            _deleteRequested = true;
            return true;
        }

        if (!events.IsAnyKeyPressed)
        {
            return changed;
        }

        if (events.PressedKeyValue == Key.Backspace && _fileName.Length == 0)
        {
            if (CurrentId >= 0)
            {
                Unselect();
                return true;
            }

            return changed;
        }

        StringBuilder builder = new(_fileName);
        int characterPosition = ByteOffsetToCharacterIndex(_fileName, _cursorPosition);
        int newCharacterPosition = KeyText.InsertKeySym(builder, characterPosition,
            events.PressedKeyValue, events.CurrentKeyModifiers);

        _fileName = builder.ToString();
        _cursorPosition = CharacterIndexToByteOffset(_fileName, newCharacterPosition);
        Unselect();
        SyncOkayButton();

        return true;
    }

    /// <summary>Draws one frame: window, title, column headers, rows, input field and buttons.</summary>
    /// <param name="target">Frame buffer of the scene.</param>
    /// <param name="header">Title, e.g. <c>"Save Game"</c> or <c>"Load Game"</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="header"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, string header)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(header);

        _window.Draw(target);

        StandardWindow.ApplyTextBackgroundShading(target, _nameHeaderArea);
        StandardWindow.ApplyTextBackgroundShading(target, _dateHeaderArea);
        StandardWindow.ApplyTextBackgroundShading(target, _textInputArea);
        StandardWindow.ApplyTextBackgroundShading(target, _selectedDateArea);

        Rect active = _window.ActiveArea;

        // 参考：dialogArea.x + ( dialogArea.width - title.width ) / 2, dialogArea.y + 9。
        int titleWidth = GlyphText.Measure(_headerFont, header);
        GlyphText.DrawLine(_headerFont, target,
            new Point(active.X + ((active.Width - titleWidth) / 2), active.Y + 9), header);

        DrawColumnHeader(target, _nameHeaderArea, "Name");
        DrawColumnHeader(target, _dateHeaderArea, "Date");

        ImageOps.BlitSprite(target, new Point(_nameHeaderArea.X + 5, _nameHeaderArea.Y + 6),
            _artwork.SortMarkFrames[4]);
        ImageOps.BlitSprite(target, new Point(_dateHeaderArea.X + 5, _dateHeaderArea.Y + 6),
            _artwork.SortMarkFrames[4]);

        if (_sorting == SaveFileSortingMethod.Filename)
        {
            ImageOps.BlitSprite(target, new Point(_nameHeaderArea.X + 8, _nameHeaderArea.Y + 9),
                _artwork.SortMarkFrames[5]);
        }
        else
        {
            ImageOps.BlitSprite(target, new Point(_dateHeaderArea.X + 8, _dateHeaderArea.Y + 9),
                _artwork.SortMarkFrames[5]);
        }

        Redraw(target);

        DrawTextInput(target);

        _buttons.DrawShadows(target);
        _buttons.Draw(target);
        SyncOkayButton();
    }

    /// <summary>
    /// Draws a <c>DateTime</c> the way <c>redrawDateTime()</c> does, with month, day, hour and minute at the
    /// fixed offsets the date column was designed around (<c>dialog_selectfile.cpp:126-162</c>).
    /// </summary>
    /// <param name="font">Font to use, normally the yellow one.</param>
    /// <param name="target">Frame buffer to draw into.</param>
    /// <param name="timestamp">Date and time of the save.</param>
    /// <param name="x">Left edge of the date column.</param>
    /// <param name="y">Top edge of the row.</param>
    internal static void DrawDateTime(IGlyphSource font, PixelBuffer target, DateTime timestamp, int x, int y)
    {
        string month = timestamp.ToString("MMM", CultureInfo.InvariantCulture);
        string day = timestamp.ToString("dd", CultureInfo.InvariantCulture);
        string hour = timestamp.ToString("HH", CultureInfo.InvariantCulture);
        string minute = timestamp.ToString("mm", CultureInfo.InvariantCulture);

        GlyphText.DrawLine(font, target, new Point(x + MonthOffsetX, y), month);
        DrawCenteredAt(font, target, day, x + DayCenterOffsetX, y);
        GlyphText.DrawLine(font, target, new Point(x + CommaOffsetX, y), ",");
        DrawCenteredAt(font, target, hour, x + HourCenterOffsetX, y);
        GlyphText.DrawLine(font, target, new Point(x + ColonOffsetX, y), ":");
        DrawCenteredAt(font, target, minute, x + MinuteCenterOffsetX, y);
    }

    /// <summary>Draws one row: centred file name, then the four date columns, <c>RedrawItem</c>.</summary>
    /// <param name="item">The row to draw.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is selected; it decides between the yellow and the white font.</param>
    /// <param name="output">Frame buffer.</param>
    public override void RedrawItem(SaveFileEntry item, int x, int y, bool current, PixelBuffer output)
    {
        IGlyphSource font = current ? _headerFont : _bodyFont;

        string name = FitToOneRow(font, item.Name, MaxFileNameWidth);
        int width = GlyphText.Measure(font, name);
        GlyphText.DrawLine(font, output,
            new Point(x + 4 + Math.Max(0, (MaxFileNameWidth - width) / 2), y + 2),
            name);

        DrawDateTime(font, output, item.Timestamp, x + MaxFileNameWidth + 9, y + 2);
    }

    /// <summary>Restores the shaded list and the scroll bar groove behind the rows.</summary>
    /// <param name="destination">Unused; the window redraws the whole list area, as <see cref="ItemSelectionWindow"/> does.</param>
    /// <param name="output">Frame buffer.</param>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _listArea);

        // 参考：renderScrollbarBackground( { scrollbarOffsetX, listRoi.y, listRoi.width, listRoi.height } )。
        StandardWindow.RenderScrollbarBackground(output,
            new Rect(_scrollBarX, _listArea.Y, _listArea.Width, _listArea.Height),
            _artwork.ScrollBarBackground);
    }

    /// <summary>Shows the map name, in-game date and path of the row, <c>Dialog::Message</c> of the reference.</summary>
    /// <param name="item">The row under the cursor.</param>
    public override void ActionListPressRight(SaveFileEntry item)
    {
        // 参考：dialog_selectfile.cpp:130-157，只有世界内的月/周/日不全为 0 时才补上那一行。
        string body = $"Map: {item.MapName}";

        if (item.WorldDay > 0 || item.WorldWeek > 0 || item.WorldMonth > 0)
        {
            body += $"\n\nMonth: {item.WorldMonth}, Week: {item.WorldWeek}, Day: {item.WorldDay}";
        }

        body += $"\n\nLocation: {item.Path}";

        _popup = new DialogPopup(item.Name, body);
    }

    /// <summary>Opens the double clicked save, the <c>MouseDoubleClick</c> branch of the reference.</summary>
    /// <param name="item">The double clicked row.</param>
    public override void ActionListDoubleClick(SaveFileEntry item)
    {
        _ = item;
        _isDoubleClicked = true;
    }

    /// <summary>Does nothing; a single click only moves the selection in the reference.</summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListSingleClick(SaveFileEntry item)
    {
        _ = item;
    }

    /// <summary>Does nothing; the reference leaves the arrow keys to the list base class.</summary>
    public override void ActionCurrentUp()
    {
    }

    /// <summary>Does nothing; the reference leaves the arrow keys to the list base class.</summary>
    public override void ActionCurrentDn()
    {
    }

    /// <summary>
    /// Draws the value of the name field: the yellow name of the selected row, the editable white text while
    /// typing, or nothing at all in a load window without a selection.
    /// </summary>
    private void DrawTextInput(PixelBuffer target)
    {
        if (SelectedFile is { } selected)
        {
            string name = FitToOneRow(_headerFont, selected.Name, MaxFileNameWidth);
            int width = GlyphText.Measure(_headerFont, name);
            GlyphText.DrawLine(_headerFont, target,
                new Point(_textInputTextArea.X + Math.Max(0, (MaxFileNameWidth - width) / 2),
                    _textInputTextArea.Y + 2),
                name);

            DrawDateTime(_headerFont, target, selected.Timestamp,
                _selectedDateArea.X + 4, _selectedDateArea.Y + 4);
            return;
        }

        if (!IsEditing)
        {
            return;
        }

        _textInput!.Draw(_fileName, _cursorPosition, target);
        DrawDateTime(_headerFont, target, DateTime.Now,
            _selectedDateArea.X + 4, _selectedDateArea.Y + 4);
    }

    private void DrawColumnHeader(PixelBuffer target, Rect area, string text)
    {
        // 参考：headerRoi.y + ( headerRoi.height - text.height() ) / 2 + 3。
        int y = area.Y + ((area.Height - _headerFont.LineHeight) / 2) + 3;
        GlyphText.DrawLine(_headerFont, target, new Point(area.X, y), text);
    }

    private static void DrawCenteredAt(IGlyphSource font, PixelBuffer target, string text, int centerX, int y)
    {
        int width = GlyphText.Measure(font, text);
        GlyphText.DrawLine(font, target, new Point(centerX - (width / 2), y), text);
    }

    /// <summary>Truncates <paramref name="text"/> so that it fits <paramref name="maxWidth"/>, <c>fitToOneRow()</c>.</summary>
    internal static string FitToOneRow(IGlyphSource font, string text, int maxWidth)
    {
        if (GlyphText.Measure(font, text, keepTrailingSpaces: true) <= maxWidth)
        {
            return text;
        }

        int ellipsisWidth = GlyphText.Measure(font, Ellipsis);
        int width = 0;

        for (int index = 0; index < text.Length; ++index)
        {
            int advance = font.GetAdvance(text[index]);
            if (width + advance + ellipsisWidth > maxWidth)
            {
                return text[..index] + Ellipsis;
            }

            width += advance;
        }

        return Ellipsis;
    }

    private void SyncOkayButton()
    {
        if (_buttons.Count == 0)
        {
            return;
        }

        if (_fileName.Length == 0)
        {
            _buttons[0].Disable();
        }
        else
        {
            _buttons[0].Enable();
        }
    }

    private static int CharacterIndexToByteOffset(string text, int characterIndex)
    {
        int clamped = Math.Clamp(characterIndex, 0, text.Length);
        return Encoding.UTF8.GetByteCount(text[..clamped]);
    }

    private static int ByteOffsetToCharacterIndex(string text, int byteOffset)
    {
        if (byteOffset <= 0)
        {
            return 0;
        }

        int bytes = 0;
        for (int index = 0; index < text.Length; ++index)
        {
            if (bytes >= byteOffset)
            {
                return index;
            }

            bytes += Encoding.UTF8.GetByteCount(text.AsSpan(index, 1));
        }

        return text.Length;
    }
}
