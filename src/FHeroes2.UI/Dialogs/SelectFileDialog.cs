using System.Globalization;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>One row of the save list: the file, its map and the in-game date it was written at.</summary>
/// <param name="Path">Full path of the save file, <c>Maps::FileInfo::filename</c>.</param>
/// <param name="Name">File name without directory and extension, <c>System::GetStem( filename )</c>.</param>
/// <param name="MapName">Name of the map the save belongs to, <c>Maps::FileInfo::name</c>.</param>
/// <param name="Timestamp">Local time the file was written, <c>Maps::FileInfo::timestamp</c>.</param>
/// <param name="WorldDay">Day of the in-game calendar, <c>Maps::FileInfo::worldDay</c>.</param>
/// <param name="WorldWeek">Week of the in-game calendar, <c>Maps::FileInfo::worldWeek</c>.</param>
/// <param name="WorldMonth">Month of the in-game calendar, <c>Maps::FileInfo::worldMonth</c>.</param>
public sealed record SaveFileEntry(string Path, string Name, string MapName, DateTime Timestamp,
    uint WorldDay, uint WorldWeek, uint WorldMonth)
{
    /// <summary>
    /// Reads the header of one save file, <c>LoadSAV2FileInfo()</c>.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <returns>The row, or <see langword="null"/> when the file is not a save of the current game type.</returns>
    /// <remarks>
    /// 参考的 <c>getSortedMapsFileInfoList()</c> 用同一份头部解析结果同时得到地图名、游戏内日期和时间戳，
    /// 读不出来的文件直接跳过，列表里不会出现占位行。时间戳按本地时区显示（参考用 <c>localtime</c>）。
    /// </remarks>
    public static SaveFileEntry? TryRead(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!GameSave.LoadSav2FileInfo(path, out Mp2FileInfo info))
        {
            return null;
        }

        DateTime timestamp = DateTimeOffset.FromUnixTimeSeconds(info.Timestamp).LocalDateTime;
        return new SaveFileEntry(path, SelectFileDialog.GetStem(path), info.Name, timestamp,
            info.WorldDay, info.WorldWeek, info.WorldMonth);
    }
}

/// <summary>
/// The save/load file chooser: listing, sorting and the blocking window loop of
/// <c>dialog_selectfile.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把「目录扫描 + 头部解析 + 排序」放在 <c>getSortedMapsFileInfoList()</c> 里，把窗口放到
/// <c>selectFileListSimple()</c> 里；移植版保持同样的分工：<see cref="GetSaveFiles"/> 只返回数据，
/// <see cref="SelectFileView"/> 负责布局与绘制，<see cref="Show"/> 只是把两边接起来的阻塞循环。
/// </para>
/// <para>
/// <b>主动偏差：</b>
/// </para>
/// <list type="bullet">
///     <item>
///     <description>
///     参考的「删除」是全局热键，移植版在 <see cref="SelectFileView.ProcessEvents"/> 里识别 <c>Delete</c>，
///     确认框由 <see cref="Show"/> 弹出；取消或删除失败都不会移除列表行。
///     </description>
///     </item>
///     <item>
///     <description>
///     参考在切换排序时立刻 <c>Game::SaveSettings()</c>；移植版的 <see cref="Settings"/> 还没有落盘层，
///     所以 <see cref="Show"/> 只更新内存里的排序方式。
///     </description>
///     </item>
///     <item>
///     <description>
///     参考用 <c>Game::HotKeyPressEvent( DEFAULT_OKAY / DEFAULT_CANCEL )</c>；移植版直接认
///     <c>Enter</c> 与 <c>Escape</c>，因为热键层尚未移植。
///     </description>
///     </item>
/// </list>
/// </remarks>
public static class SelectFileDialog
{
    /// <summary>The title of the save window, <c>_( "Save Game" )</c>.</summary>
    public const string SaveHeader = "Save Game";

    /// <summary>The title of the load window, <c>_( "Load Game" )</c>.</summary>
    public const string LoadHeader = "Load Game";

    /// <summary>The date format used by <see cref="DescribeDate"/>, <c>"%a, %d %b %Y %H:%M"</c>-like.</summary>
    private const string DatePattern = "ddd, d MMM yy HH:mm";

    /// <summary>
    /// Lists the save files of the current game type, newest first by default, <c>getSortedMapsFileInfoList()</c>
    /// (<c>dialog_selectfile.cpp:64-115</c>).
    /// </summary>
    /// <param name="sorting">Sort order to apply.</param>
    /// <param name="directory">Directory to scan; <see langword="null"/> means <see cref="GameSave.GetSaveDir"/>.</param>
    /// <returns>The readable saves, sorted; an empty list when the directory does not exist or is empty.</returns>
    /// <remarks>
    /// 读不出头部的文件会被跳过，和参考一致：选文件窗口里不会出现一个选不中的空行。扩展名由
    /// <see cref="GameSave.GetSaveFileExtension()"/> 按当前游戏类型给出。
    /// </remarks>
    public static List<SaveFileEntry> GetSaveFiles(SaveFileSortingMethod sorting, string? directory = null)
    {
        string saveDirectory = directory ?? GameSave.GetSaveDir();
        List<SaveFileEntry> files = [];

        if (!System.IO.Directory.Exists(saveDirectory))
        {
            return files;
        }

        foreach (string path in System.IO.Directory.EnumerateFiles(saveDirectory,
                     "*" + GameSave.GetSaveFileExtension()))
        {
            if (SaveFileEntry.TryRead(path) is { } info)
            {
                files.Add(info);
            }
        }

        SortSaveFiles(files, sorting);
        return files;
    }

    /// <summary>
    /// Sorts a save list in place, <c>std::sort( ..., Maps::FileInfo::CompareByFileName )</c> and
    /// <c>CompareByMostRecentFirst</c> (<c>maps_fileinfo.cpp</c>).
    /// </summary>
    /// <param name="files">List to sort.</param>
    /// <param name="sorting">Order to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is <see langword="null"/>.</exception>
    public static void SortSaveFiles(List<SaveFileEntry> files, SaveFileSortingMethod sorting)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (sorting == SaveFileSortingMethod.Timestamp)
        {
            // 参考只比时间戳；这里用名称兜底，让同一秒写出的两份存档也有稳定顺序。
            files.Sort(static (left, right) =>
            {
                int result = right.Timestamp.CompareTo(left.Timestamp);
                return result != 0
                    ? result
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
            return;
        }

        files.Sort(static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Finds a save by name, <c>findInMapInfos()</c> (<c>dialog_selectfile.cpp:117-124</c>).</summary>
    /// <param name="files">List to search.</param>
    /// <param name="name">File name, path, or stem; case is ignored.</param>
    /// <returns>The index, or <c>-1</c> when the list has no such entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is <see langword="null"/>.</exception>
    public static int FindSaveFileIndex(IReadOnlyList<SaveFileEntry> files, string? name)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        for (int index = 0; index < files.Count; ++index)
        {
            SaveFileEntry file = files[index];
            if (string.Equals(file.Path, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.Name, GetStem(name), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The entry <see cref="FindSaveFileIndex"/> points at, or <see langword="null"/>.</summary>
    /// <param name="files">List to search.</param>
    /// <param name="name">File name, path, or stem; case is ignored.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public static SaveFileEntry? FindSaveFile(IReadOnlyList<SaveFileEntry> files, string? name)
    {
        int index = FindSaveFileIndex(files, name);
        return index >= 0 ? files[index] : null;
    }

    /// <summary>File name without directory and extension, <c>System::GetStem()</c>.</summary>
    /// <param name="path">Path or file name.</param>
    /// <returns>The stem, possibly empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static string GetStem(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// Formats a timestamp as one line, the one-string form of the reference's date column.
    /// </summary>
    /// <param name="timestamp">Date and time to format.</param>
    /// <returns>The formatted date in the current culture's day and month names.</returns>
    /// <remarks>
    /// 窗口本身用的是 <see cref="SelectFileView.DrawDateTime"/> 的四段固定列布局；这个方法留给提示文字与测试。
    /// </remarks>
    public static string DescribeDate(DateTime timestamp) =>
        timestamp.ToString(DatePattern, CultureInfo.CurrentCulture);

    /// <summary>
    /// Runs the window until OK or CANCEL, <c>selectFileListSimple()</c> (<c>dialog_selectfile.cpp:323-733</c>).
    /// </summary>
    /// <param name="target">Frame buffer the window draws into.</param>
    /// <param name="view">Window built by <see cref="SelectFileView.TryCreate"/>.</param>
    /// <param name="header">Title to draw, normally <see cref="SaveHeader"/> or <see cref="LoadHeader"/>.</param>
    /// <param name="lastFile">
    /// Save the game remembers, used for the initial selection and the initial name
    /// (<c>Game::GetLastSaveName()</c>); <see langword="null"/> when there is none.
    /// </param>
    /// <param name="isEditing">Whether the window saves a game and the name can be typed.</param>
    /// <param name="files">
    /// Entries to show, or <see langword="null"/> to scan <paramref name="view"/>'s directory. The caller that
    /// already scanned for the window height (<see cref="SelectFileView.TryCreate"/>) passes the same list so
    /// that the headers are parsed once.
    /// </param>
    /// <param name="events">Input source; defaults to <see cref="LocalEvent.Current"/>.</param>
    /// <returns>The chosen file path, or <see langword="null"/> when the player cancelled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="view"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static string? Show(PixelBuffer target, SelectFileView view, string header, string? lastFile,
        bool isEditing, IReadOnlyList<SaveFileEntry>? files = null, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(view);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The save file window needs an event engine to run.");

        Settings settings = Settings.Get();

        view.SetFiles(files ?? GetSaveFiles(view.Sorting, view.Directory), view.Sorting);

        if (!string.IsNullOrEmpty(lastFile))
        {
            view.FileName = GetStem(lastFile);
            view.CursorPosition = view.FileName.Length;

            if (!view.SelectFile(lastFile))
            {
                view.Unselect();

                if (!isEditing)
                {
                    view.ClearSelection();
                }
            }
        }

        if (view.FileName.Length == 0 && view.SelectedFile is { } first)
        {
            view.FileName = first.Name;
            view.CursorPosition = view.FileName.Length;
        }

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            ButtonHotKey? hotKey = eventState.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay
                : eventState.IsKeyPressed(Key.Escape) ? ButtonHotKey.Cancel
                : null;

            view.ProcessEvents(eventState, hotKey);

            if (view.DeleteRequested && view.SelectedFile is { } doomed)
            {
                // 参考：showStandardTextMessage( _( "Warning" ), "Are you sure you want to delete file:\n\n" + name )。
                DialogButton answer = MessageBox.Show(target, "Warning",
                    $"Are you sure you want to delete file:\n\n{System.IO.Path.GetFileName(doomed.Path)}",
                    view.HeaderFont, view.BodyFont, DialogButton.Yes | DialogButton.No,
                    view.ButtonArtwork, eventState);

                if (answer == DialogButton.Yes)
                {
                    bool deleted = false;

                    try
                    {
                        System.IO.File.Delete(doomed.Path);
                        deleted = true;
                    }
                    catch (System.IO.IOException exception)
                    {
                        GameLog.Warn($"Could not delete save file '{doomed.Path}': {exception.Message}");
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        GameLog.Warn($"Could not delete save file '{doomed.Path}': {exception.Message}");
                    }

                    if (deleted)
                    {
                        view.RemoveSelectedFile();
                    }
                }
            }

            if (view.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, view.HeaderFont, view.BodyFont,
                    DialogButton.Ok, view.ButtonArtwork, eventState);
            }

            if (view.Result == DialogButton.Cancel)
            {
                return null;
            }

            if (view.Result == DialogButton.Ok)
            {
                if (view.SelectedFile is { } selected)
                {
                    return selected.Path;
                }

                if (view.FileName.Length > 0)
                {
                    return System.IO.Path.Combine(view.Directory,
                        view.FileName + GameSave.GetSaveFileExtension());
                }
            }

            if (settings.SaveFileSortType != view.Sorting)
            {
                settings.SaveFileSortType = view.Sorting;
            }

            view.Draw(target, header);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return null;
    }
}
