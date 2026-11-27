using System.Text;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// How the author left the save map window, the <c>int32_t</c> of <c>mapSaveSelectFile()</c>.
/// </summary>
public enum EditorSaveMapOutcome
{
    /// <summary>The window stays open.</summary>
    None,

    /// <summary>Accepted; the caller reads <see cref="EditorSaveMapWindow.FileName"/> and <see cref="EditorSaveMapWindow.MapName"/>.</summary>
    Accepted,

    /// <summary>Cancelled.</summary>
    Cancelled
}

/// <summary>
/// The editor's save map window, <c>mapSaveSelectFile()</c> (<c>editor_save_map_window.cpp:181-487</c>).
/// </summary>
/// <remarks>
/// <para>
/// 标准窗里是四块：标题「Save Map:」、地图名一行（点开改名）、已有地图的列表（<see cref="ListBox{T}"/>，每行是去掉扩展名的
/// 文件名加两张 <c>REQUESTS</c> 小图标）、文件名输入框（固定后缀 <see cref="MapFileExtension"/>），底边 OKAY/CANCEL。
/// 列表高度随地图数量在 11..22 行之间伸缩（<c>:210-216</c>）。
/// </para>
/// <para>
/// 与参考的偏差记在 ADR-312：列表只列 <c>.mp2</c>（参考列它自家的 <c>.fh2m</c>，端口没有那种格式，编辑器写的就是 mp2）；
/// 虚拟键盘的图案缺席时按钮整个不画。
/// </para>
/// <para>
/// 参考是阻塞式的，本类只做「按一帧输入 → 返回结果」，循环、删除文件的确认框、改名用的输入框、虚拟键盘都交回宿主
/// （<see cref="MapNameRequested"/>、<see cref="DeleteRequested"/>、<see cref="VirtualKeyboardRequested"/>），
/// 与编辑器其它窗口一致。写盘用 <see cref="SaveMap"/>。
/// </para>
/// </remarks>
public sealed class EditorSaveMapWindow : ListBox<Mp2FileInfo>
{
    private readonly List<Mp2FileInfo> _maps = [];

    /// <summary>Width of the file name field, <c>maxFileNameWidth</c> (<c>:31</c>).</summary>
    public const int MaxFileNameWidth = 300;

    /// <summary>Width of the list, <c>maxFileNameWidth + 9</c> (<c>:204</c>).</summary>
    public const int ListWidth = MaxFileNameWidth + 9;

    /// <summary>Width of the window, <c>listWidth + 75</c> (<c>:204</c>).</summary>
    public const int WindowWidth = ListWidth + 75;

    /// <summary>Gap between the top of the active area and the list, <c>listAreaOffsetY</c> (<c>:207</c>).</summary>
    public const int ListAreaOffsetY = 6;

    /// <summary>Room left under the list, <c>listAreaHeightDeduction</c> (<c>:208</c>).</summary>
    public const int ListAreaHeightDeduction = 8;

    /// <summary>Room taken by the file name field and the buttons, <c>listHeightDeduction</c> (<c>:209</c>).</summary>
    public const int ListHeightDeduction = 129;

    /// <summary>The fixed part of the dialog height, <c>estraDialogHeight</c> (<c>:214</c>).</summary>
    public const int ExtraDialogHeight = ListAreaOffsetY + ListAreaHeightDeduction + ListHeightDeduction;

    /// <summary>Fewest rows the list shows, <c>:215</c>.</summary>
    public const int MinimumVisibleRows = 11;

    /// <summary>Most rows the list shows, <c>:215</c>.</summary>
    public const int MaximumVisibleRows = 22;

    /// <summary>Longest file name the field takes, <c>:233</c>.</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>Longest map name the author may type, <c>:384</c>.</summary>
    public const int MaxMapNameLength = 40;

    /// <summary>
    /// Extension of a saved map. The reference uses <c>.fh2m</c> (<c>:301</c>); the port writes MP2, so the editor
    /// saves and lists <c>.mp2</c>.
    /// </summary>
    public const string MapFileExtension = ".mp2";

    /// <summary>The name a fresh map is saved under, <c>_( "My map" )</c> (<c>:327</c>).</summary>
    public const string DefaultFileName = "My map";

    /// <summary>Height of a scroll button, <c>:241</c>.</summary>
    public const int ScrollButtonHeight = 19;

    /// <summary>Title of the window, <c>_( "Save Map:" )</c> (<c>:294</c>).</summary>
    public const string HeaderText = "Save Map:";

    /// <summary>Title of the CANCEL button's message, <c>:418</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:422</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to accept the changes made.";

    /// <summary>Title of the map name's message, <c>:426</c>.</summary>
    public const string MapNameTitle = "Map Name";

    /// <summary>Body of the map name's message.</summary>
    public const string MapNameMessage = "Click to change the name of the map.";

    /// <summary>Title of the virtual keyboard button's message, <c>:434</c>.</summary>
    public const string VirtualKeyboardTitle = "Open Virtual Keyboard";

    /// <summary>Body of the virtual keyboard button's message.</summary>
    public const string VirtualKeyboardMessage = "Click to open the Virtual Keyboard dialog.";

    /// <summary>
    /// The question of an overwrite, <c>_( "Are you sure you want to overwrite the existing map?" )</c>
    /// (<c>editor_interface.cpp:3248</c>).
    /// </summary>
    public const string OverwriteMessage = "Are you sure you want to overwrite the existing map?";

    /// <summary>Body of the box shown when the maps directory cannot be located or created.</summary>
    public const string DirectoryErrorMessage = "Unable to create a directory to save the map.";

    /// <summary>Body of the box shown when the write fails, <c>_( "Failed to save the map." )</c>.</summary>
    public const string SaveFailedMessage = "Failed to save the map.";

    private const int ScrollBarRightInset = 35;

    private readonly StandardWindow _window;
    private readonly EditorSaveMapArtwork _artwork;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly Rect _listArea;
    private readonly Rect _mapNameArea;
    private readonly Rect _fileNameArea;
    private readonly TextInputField _textInput;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite? _virtualKeyboard;
    private readonly int _scrollBarX;
    private string _fileName;
    private int _cursorPosition;

    private EditorSaveMapWindow(Size screen, IReadOnlyList<Mp2FileInfo> maps, string fileName, string mapName,
        EditorSaveMapArtwork artwork, IGlyphSource headerFont, IGlyphSource bodyFont)
        : base(new Point(0, 0))
    {
        ArgumentNullException.ThrowIfNull(maps);

        _artwork = artwork;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _fileName = string.IsNullOrWhiteSpace(fileName) ? DefaultFileName : NormalizeFileName(fileName);
        _cursorPosition = _fileName.Length;
        MapName = mapName;

        int lineHeight = Math.Max(1, bodyFont.LineHeight) + 2;
        int maxDialogHeight = (lineHeight * Math.Clamp(maps.Count, MinimumVisibleRows, MaximumVisibleRows)) +
                              ExtraDialogHeight;
        int visibleRows = (Math.Min(screen.Height - 100, maxDialogHeight) - ExtraDialogHeight) / lineHeight;
        int dialogHeight = (lineHeight * visibleRows) + ExtraDialogHeight;

        _window = StandardWindow.CreateCentered(new Size(WindowWidth, dialogHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;
        _listArea = new Rect(active.X + 24, active.Y + 54, ListWidth, active.Height - ListHeightDeduction);
        _fileNameArea = new Rect(_listArea.X + 4, _listArea.Bottom + 14, MaxFileNameWidth, 21);
        _mapNameArea = new Rect(_listArea.X, active.Y + 28, _listArea.Width, bodyFont.LineHeight + 4);

        _scrollBarX = active.X + active.Width - ScrollBarRightInset;

        SetAreaItems(new Rect(_listArea.X, _listArea.Y, MaxFileNameWidth, _listArea.Height - ListAreaHeightDeduction));
        SetAreaMaxItems(Math.Max(1, visibleRows));
        SetTopLeft(new Point(_listArea.X, _listArea.Y));

        SetScrollButtonUp(new ButtonSprite(artwork.ScrollBarFrames[0], artwork.ScrollBarFrames[1], null,
            _scrollBarX + 1, _listArea.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollBarFrames[2], artwork.ScrollBarFrames[3], null,
            _scrollBarX + 1, _listArea.Bottom - 15));
        SetScrollBarArea(new Rect(_scrollBarX + 2, _listArea.Y + ScrollButtonHeight, 10,
            _listArea.Height - (ScrollButtonHeight * 2)));
        SetScrollBarImage(artwork.ScrollBarFrames[4]);

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);

        if (artwork.VirtualKeyboard is { } keyboard)
        {
            // 参考 :308-309：左下角 { 6, -20 }。
            _virtualKeyboard = new ButtonSprite(keyboard.Released, keyboard.Pressed);
            Point keyboardPosition = _window.RenderPosition(new Point(6, -20),
                new Size(_virtualKeyboard.Area.Width, _virtualKeyboard.Area.Height), WindowPadding.BottomLeft);

            _virtualKeyboard.SetPosition(keyboardPosition.X, keyboardPosition.Y);
        }

        Point okayPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_okay.Area.Width, _okay.Area.Height), WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_cancel.Area.Width, _cancel.Area.Height), WindowPadding.BottomRight);

        _okay.SetPosition(okayPosition.X, okayPosition.Y);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        _textInput = new TextInputField(_fileNameArea, isMultiLine: false, isCenterAligned: false);

        SetMaps(maps);

        // 参考打开窗口时列表没有选中项（:325-330 只可能改动 fileName），选中项由第一次点击产生。
        Unselect();
        SyncOkayButton();
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="screen">Screen size, <c>display.width()</c>/<c>height()</c> (<c>:206</c>).</param>
    /// <param name="maps">The saved maps to list.</param>
    /// <param name="fileName">The stem the field starts with; the empty string falls back to <see cref="DefaultFileName"/>.</param>
    /// <param name="mapName">The map name shown above the list.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="headerFont">The normal yellow font.</param>
    /// <param name="bodyFont">The normal white font, also the font of the rows.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorSaveMapWindow Create(Size screen, IReadOnlyList<Mp2FileInfo> maps, string fileName,
        string mapName, EditorSaveMapArtwork artwork, IGlyphSource headerFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(mapName);

        return new EditorSaveMapWindow(screen, maps, fileName, mapName, artwork, headerFont, bodyFont);
    }

    /// <summary>
    /// Turns what the author typed into a file stem: the extension is dropped and everything a file name may not
    /// carry becomes an underscore.
    /// </summary>
    /// <param name="fileName">Text of the field, with or without <see cref="MapFileExtension"/>.</param>
    /// <returns>The stem, possibly empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考直接把输入拼成路径（<c>editor_interface.cpp:3245</c>），没有这一步；端口在 Windows 上不能把
    /// <c>:</c>、<c>/</c> 之类写进文件名，故先净化再拼路径。
    /// </remarks>
    public static string SanitizeFileName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        // 目录分隔符也算非法字符（Windows 的 GetInvalidFileNameChars 里有 \ 与 /），
        // 一律换成下划线，免得把作者打的字悄悄丢掉。
        string stem = fileName.Trim();

        if (stem.EndsWith(MapFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^MapFileExtension.Length];
        }

        ReadOnlySpan<char> invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(stem.Length);

        foreach (char character in stem)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Builds the path of the file to write, <c>concatPath( mapDirectory, fileName + ".fh2m" )</c>
    /// (<c>editor_interface.cpp:3245</c>).
    /// </summary>
    /// <param name="directory">The <c>maps</c> directory of the writable data path.</param>
    /// <param name="fileName">Text of the field.</param>
    /// <returns>The full path of the map file.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string ComposeFilePath(string directory, string fileName)
    {
        ArgumentNullException.ThrowIfNull(directory);

        return Path.Combine(directory, SanitizeFileName(fileName) + MapFileExtension);
    }

    /// <summary>
    /// Writes the map, overwriting the file that is already there, the <c>Map_Format::saveMap()</c> of
    /// <c>editor_interface.cpp:3256</c>.
    /// </summary>
    /// <param name="directory">The <c>maps</c> directory of the writable data path; created when missing.</param>
    /// <param name="fileName">Text of the field.</param>
    /// <param name="map">The map to write.</param>
    /// <param name="sourcePath">Map the addons were read from, when it has to be reused.</param>
    /// <returns>The full path of the written file.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> has no usable characters.</exception>
    /// <remarks>
    /// 覆盖同名文件在参考里由调用方确认（<c>:3247-3250</c>），这里只负责写。
    /// </remarks>
    public static string SaveMap(string directory, string fileName, Mp2Map map, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(map);

        if (SanitizeFileName(fileName).Length == 0)
        {
            throw new ArgumentException("The map file name has no usable characters.", nameof(fileName));
        }

        string path = ComposeFilePath(directory, fileName);

        Directory.CreateDirectory(directory);
        Mp2MapWriter.Save(path, map, sourcePath, writeHeader: true);

        return path;
    }

    /// <summary>The window the list sits in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The rect of the map list.</summary>
    public Rect ListArea => _listArea;

    /// <summary>The rect of the map name line.</summary>
    public Rect MapNameArea => _mapNameArea;

    /// <summary>The rect of the file name field, <c>fileNameRoi</c> (<c>:386</c>).</summary>
    public Rect FileNameArea => _fileNameArea;

    /// <summary>The OKAY button; disabled while the file name is empty.</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>Whether OKAY can be clicked, <c>buttonOk.isEnabled()</c> (<c>:330-340</c>).</summary>
    public bool IsOkayEnabled => _okay.IsEnabled;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The map name above the list, the editor's <c>mapName</c> (<c>:383-388</c>).</summary>
    public string MapName { get; set; }

    /// <summary>
    /// The file name being typed, without <see cref="MapFileExtension"/>. Setting it clamps the length and moves the
    /// cursor to the end.
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = NormalizeFileName(value ?? string.Empty);
            _cursorPosition = _fileName.Length;
            SyncOkayButton();
        }
    }

    /// <summary>Where the typing cursor sits, <c>charInsertPos</c>.</summary>
    public int CursorPosition => _cursorPosition;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorSaveMapOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>Whether the author asked to delete the selected file, <c>DEFAULT_DELETE</c> (<c>:365-377</c>).</summary>
    public bool DeleteRequested { get; private set; }

    /// <summary>Whether the author clicked the map name and the host has to open the input box (<c>:383-396</c>).</summary>
    public bool MapNameRequested { get; private set; }

    /// <summary>Whether the author asked for the virtual keyboard, <c>openVirtualKeyboard( fileName, ... )</c> (<c>:336-344</c>);
    /// the host opens it and hands the result back through <see cref="FileName"/>.
    /// </summary>
    public bool VirtualKeyboardRequested { get; private set; }

    /// <summary>
    /// Answers OKAY from outside the event loop, the <c>DEFAULT_OKAY</c> the host folds into a click
    /// (<c>:311-315</c>).
    /// </summary>
    /// <returns><see cref="EditorSaveMapOutcome.Accepted"/> when OKAY is enabled, otherwise nothing.</returns>
    public EditorSaveMapOutcome Accept()
    {
        Result = _okay.IsEnabled ? EditorSaveMapOutcome.Accepted : EditorSaveMapOutcome.None;

        return Result;
    }

    /// <summary>Answers CANCEL from outside the event loop, the <c>DEFAULT_CANCEL</c> of <c>:311-315</c>.</summary>
    /// <returns><see cref="EditorSaveMapOutcome.Cancelled"/>.</returns>
    public EditorSaveMapOutcome RequestCancel() => Result = EditorSaveMapOutcome.Cancelled;

    /// <summary>The virtual keyboard button, or <see langword="null"/> when the artwork has no icon for it.</summary>
    public ButtonSprite? VirtualKeyboard => _virtualKeyboard;

    /// <summary>The map under the cursor, or <see langword="null"/>.</summary>
    public Mp2FileInfo? SelectedMap => CurrentId >= 0 && CurrentId < _maps.Count ? _maps[CurrentId] : null;

    /// <summary>
    /// Hands the list a new set of maps, <c>listbox.SetListContent()</c> plus the scroll bar refresh of <c>:447</c>.
    /// </summary>
    /// <param name="maps">The maps, already sorted the way the window shows them.</param>
    public void SetMaps(IReadOnlyList<Mp2FileInfo> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        _maps.Clear();
        _maps.AddRange(maps);
        SetListContent([.. _maps]);
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Selects the row whose file stem is <paramref name="stem"/>, ignoring case.
    /// </summary>
    /// <param name="stem">File name without its extension.</param>
    /// <returns>Whether a row was selected.</returns>
    public bool SelectMapByStem(string stem)
    {
        ArgumentNullException.ThrowIfNull(stem);

        for (int i = 0; i < _maps.Count; ++i)
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(_maps[i].FilePath), stem,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetCurrent(i);
                return true;
            }
        }

        Unselect();
        return false;
    }

    /// <summary>
    /// Drops the selected row after the host deleted its file, then points the selection at the row that took its
    /// place, the <c>RemoveSelected()</c> of <c>:422-426</c>.
    /// </summary>
    /// <returns>The map that was removed, or <see langword="null"/> when nothing was selected.</returns>
    public Mp2FileInfo? RemoveSelectedMap()
    {
        if (SelectedMap is not { } removed)
        {
            return null;
        }

        int index = CurrentId;

        _maps.RemoveAt(index);
        SetListContent([.. _maps]);
        UpdateScrollBarImage();

        if (_maps.Count == 0)
        {
            Unselect();
            FileName = string.Empty;
            return removed;
        }

        SetCurrent(Math.Min(index, _maps.Count - 1));
        _fileName = Path.GetFileNameWithoutExtension(_maps[CurrentId].FilePath);
        _cursorPosition = _fileName.Length;

        SyncOkayButton();
        return removed;
    }

    /// <summary>
    /// Draws the window, the <c>display.render()</c> calls of <c>:294-303</c>.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        _window.Draw(target);

        StandardWindow.ApplyTextBackgroundShading(target, _mapNameArea);
        StandardWindow.ApplyTextBackgroundShading(target,
            new Rect(_listArea.X, _listArea.Y, MaxFileNameWidth + 8, _listArea.Height));
        StandardWindow.ApplyTextBackgroundShading(target,
            new Rect(_listArea.X, _fileNameArea.Y - 2, MaxFileNameWidth + 8, _fileNameArea.Height));

        int headerWidth = GlyphText.Measure(_headerFont, HeaderText);
        GlyphText.DrawLine(_headerFont, target,
            new Point(ActiveArea.X + ((ActiveArea.Width - headerWidth) / 2), ActiveArea.Y + 10), HeaderText);

        // 参考把地图名左对齐在 mapNameRoi.x、纵向 +4（:293、:300）。
        GlyphText.DrawLine(_headerFont, target, new Point(_mapNameArea.X, _mapNameArea.Y + 4),
            GlyphText.FitToOneRow(_headerFont, MapName, _mapNameArea.Width), _mapNameArea);

        Redraw(target);

        _textInput.Draw(_fileName + MapFileExtension, _cursorPosition, target);

        if (_virtualKeyboard is not null)
        {
            PaintButton(_virtualKeyboard, target, events);
        }

        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, <c>:311-467</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The result of the frame; <see cref="EditorSaveMapOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorSaveMapOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Result = EditorSaveMapOutcome.None;
        Popup = null;
        DeleteRequested = false;
        MapNameRequested = false;
        VirtualKeyboardRequested = false;
        IsDoubleClicked = false;

        string previousSelection = SelectedMap is { } selected
            ? Path.GetFileNameWithoutExtension(selected.FilePath)
            : string.Empty;

        // 参考在循环开头认 CANCEL/OKAY（:311-315），OKAY 只在启用时才算；窗口自己不认键盘，热键留给宿主。
        if (events.MouseClickLeft(_cancel.Area))
        {
            Result = EditorSaveMapOutcome.Cancelled;
            return Result;
        }

        if (_okay.IsEnabled && events.MouseClickLeft(_okay.Area))
        {
            Result = EditorSaveMapOutcome.Accepted;
            return Result;
        }

        if (events.MouseClickLeft(_mapNameArea))
        {
            MapNameRequested = true;
        }

        // 参考 :334-341：虚拟键盘按钮一按就开窗；触屏那种「点输入框自动开」在端口没有判据，故不做。
        if (_virtualKeyboard is not null && events.MouseClickLeft(_virtualKeyboard.Area))
        {
            VirtualKeyboardRequested = true;
        }

        QueueEventProcessing(events);

        if (IsDoubleClicked)
        {
            Result = EditorSaveMapOutcome.Accepted;
            return Result;
        }

        Mp2FileInfo? current = SelectedMap;

        if (current is not null &&
            !string.Equals(previousSelection, Path.GetFileNameWithoutExtension(current.FilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            // 选中一行就把文件名换成它的（参考 :331-364 的 needFileNameRedraw）。
            _fileName = Path.GetFileNameWithoutExtension(current.FilePath);
            _cursorPosition = _fileName.Length;
        }

        if (events.MouseClickLeft(_fileNameArea))
        {
            // 点文件名框时把列表的选中撤掉，光标落在点上（:353-358）。
            Unselect();
            _cursorPosition = Math.Min(_textInput.GetCursorInTextPosition(events.CursorPosition), _fileName.Length);
        }
        else if (events.IsKeyPressed(Key.Delete) && current is not null)
        {
            DeleteRequested = true;
            return Result;
        }
        else if (events.IsAnyKeyPressed)
        {
            // 参考 :456-463：按下的键交给 InsertKeySym 插到光标处，先把列表的选中撤掉。
            StringBuilder builder = new(_fileName);
            int newPosition = KeyText.InsertKeySym(builder, _cursorPosition, events.PressedKeyValue,
                events.CurrentKeyModifiers);

            string typed = builder.ToString();

            if (typed.Length <= MaxFileNameLength)
            {
                Unselect();
                _fileName = typed;
                _cursorPosition = newPosition;
            }
        }

        if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            Popup = new DialogPopup(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_okay.Area))
        {
            Popup = new DialogPopup(OkayTitle, OkayMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_mapNameArea))
        {
            Popup = new DialogPopup(MapNameTitle, MapNameMessage);
        }
        else if (_virtualKeyboard is not null && events.IsMouseRightButtonPressedInArea(_virtualKeyboard.Area))
        {
            Popup = new DialogPopup(VirtualKeyboardTitle, VirtualKeyboardMessage);
        }

        SyncOkayButton();
        return Result;
    }

    /// <inheritdoc/>
    public override void RedrawItem(Mp2FileInfo item, int x, int y, bool current, PixelBuffer output)
    {
        IGlyphSource font = current ? _headerFont : _bodyFont;
        string stem = GlyphText.FitToOneRow(font, Path.GetFileNameWithoutExtension(item.FilePath), MaxFileNameWidth);

        GlyphText.DrawLine(font, output, new Point(x, y), stem, ItemsArea);

        // 参考 :144-151：玩家人数图标从 19 起，地图大小从 25 起（宽度 / 36）。
        if (_artwork.GetRequestsIcon(EditorSaveMapArtwork.PlayerCountFirstFrame +
                                     PlayerColors.Count(item.KingdomColors)) is { } players)
        {
            ImageOps.BlitSprite(output, new Point(x + MaxFileNameWidth + 8, y), players);
        }

        if (_artwork.GetRequestsIcon(EditorSaveMapArtwork.MapSizeFirstFrame +
                                     (item.Width / MapSizes.EdgeLength(MapSize.Small))) is { } size)
        {
            ImageOps.BlitSprite(output, new Point(x + MaxFileNameWidth + 33, y), size);
        }
    }

    /// <inheritdoc/>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _listArea);

        StandardWindow.RenderScrollbarBackground(output,
            new Rect(_scrollBarX, _listArea.Y, _listArea.Width, _listArea.Height),
            _artwork.ScrollBarBackground);
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
    public override void ActionListSingleClick(Mp2FileInfo item)
    {
    }

    /// <inheritdoc/>
    public override void ActionListDoubleClick(Mp2FileInfo item) => IsDoubleClicked = true;

    /// <summary>
    /// Shows the name, size, description and path of a row, the popup of <c>:430-445</c>.
    /// </summary>
    /// <param name="item">The row under the cursor.</param>
    public override void ActionListPressRight(Mp2FileInfo item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // 参考 :99-111 依次是 Map Type、Language、Size、Description、Location；「Resurrection」那一行在这里是
        // 「Price of Loyalty」（列表只收 mp2），Language 略去——Mp2FileInfo.mainLanguage 只是原始字节，
        // 端口还没有字节到 SupportedLanguage 的映射。
        Popup = new DialogPopup(
            item.Name,
            $"Map Type:\nPrice of Loyalty\n\nSize: {item.Width} x {item.Height}\n\n" +
            $"Description: {item.Description}\n\nLocation: {item.FilePath}");
    }

    /// <summary>Whether the last frame double clicked a row, <c>isDoubleClicked()</c> (<c>:312</c>).</summary>
    private bool IsDoubleClicked { get; set; }

    private static string NormalizeFileName(string fileName)
    {
        string trimmed = fileName.Trim();

        return trimmed.Length <= MaxFileNameLength ? trimmed : trimmed[..MaxFileNameLength];
    }

    private void SyncOkayButton()
    {
        if (_fileName.Length == 0)
        {
            _okay.Disable();
        }
        else
        {
            _okay.Enable();
        }
    }

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
