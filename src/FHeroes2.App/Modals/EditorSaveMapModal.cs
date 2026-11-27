using FHeroes2.App.Scenes;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The editor's save map window run on the host's frame pump, <c>Editor::mapSaveSelectFile()</c>
/// (<c>editor_save_map_window.cpp:181-487</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的窗口自己转循环：列表、文件名输入、地图名、虚拟键盘按钮、删除热键与四处右键说明全在里面；同名覆盖的确认框
/// 在 <c>saveMapToFile()</c> 里（<c>editor_interface.cpp:3247-3250</c>），不在窗口里。
/// </para>
/// <para>
/// 端口把窗口的四个请求分给宿主：地图名走输入框、删除走后跟一个 YES/NO 确认框（确认后删文件并让列表丢掉那一行）、
/// 右键说明走消息框，三者都摞在这个类自己的栈上。虚拟键盘没有窗口可开（端口的 <see cref="VirtualKeyboardView"/>
/// 只有数字键盘），只留一条日志。
/// </para>
/// </remarks>
internal sealed class EditorSaveMapModal : IModalScreen
{
    /// <summary>Title of the delete confirmation, <c>_( "Warning" )</c> of <c>:365-377</c>.</summary>
    public const string DeleteTitle = "Warning";

    /// <summary>Body of the delete confirmation, <c>_( "Are you sure you want to delete file:" )</c>.</summary>
    public const string DeleteBody = "Are you sure you want to delete file:";

    /// <summary>Header of the map name input box, <c>_( "Map Name:" )</c> of <c>:384-396</c>.</summary>
    public const string MapNameHeader = "Map Name:";

    private readonly EditorSaveMapWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private bool _deleteQuestionOpen;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="host">Where the input box and the message boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorSaveMapModal(EditorSaveMapWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public EditorSaveMapWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorSaveMapOutcome.None"/> while it is still open.</summary>
    public EditorSaveMapOutcome Outcome => _window.Result;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey)
    {
        if (Top is { } inner)
        {
            inner.HotKey(hotKey);

            return;
        }

        if (hotKey == ButtonHotKey.Cancel)
        {
            _ = _window.RequestCancel();
            IsFinished = true;

            return;
        }

        if (hotKey == ButtonHotKey.Okay && _window.Accept() == EditorSaveMapOutcome.Accepted)
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels, LocalEvent.Current);

        Top?.Draw(frame);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            return;
        }

        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
                Resolve(inner);
            }

            return;
        }

        EditorSaveMapOutcome outcome = _window.Process(LocalEvent.Current);

        if (outcome != EditorSaveMapOutcome.None)
        {
            IsFinished = true;

            return;
        }

        if (_window.MapNameRequested)
        {
            OpenMapNameInput();
        }

        if (_window.DeleteRequested)
        {
            AskDelete();
        }

        if (_window.VirtualKeyboardRequested)
        {
            GameLog.Warn("The save window asked for the virtual keyboard, and the port only has the number pad.");
        }

        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Opens the map name input box, the <c>Dialog::inputString</c> of <c>:384-396</c>.</summary>
    private void OpenMapNameInput()
    {
        InputStringDialog dialog = InputStringDialog.Create(string.Empty, MapNameHeader,
            EditorSaveMapWindow.MaxMapNameLength, isMultiLine: false, initialText: _window.MapName);

        if (_host.TryCreateInput(dialog) is { } input)
        {
            _inner.Push(input);
        }
    }

    /// <summary>
    /// Asks whether the selected map may be deleted, the YES/NO box of <c>:365-377</c>.
    /// </summary>
    private void AskDelete()
    {
        if (_window.SelectedMap is not { } selected)
        {
            return;
        }

        string body = $"{DeleteBody}\n\n{Path.GetFileName(selected.FilePath)}";

        if (_host.TryCreateMessageBox(DeleteTitle, body, DialogButton.Yes | DialogButton.No) is not { } box)
        {
            return;
        }

        _deleteQuestionOpen = true;
        _inner.Push(box);
    }

    /// <summary>Feeds the answer of a closed inner window back into the save window.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        switch (closed)
        {
            case InputStringModal { Window.Result: { } name }:
                _window.MapName = name;

                return;

            case MessageBoxModal box when _deleteQuestionOpen:
                _deleteQuestionOpen = false;

                if (box.Answer == DialogButton.Yes)
                {
                    DeleteSelectedMap();
                }

                return;

            default:
                return;
        }
    }

    /// <summary>
    /// Deletes the file of the selected row and drops it from the list, the <c>System::Unlink()</c> plus
    /// <c>RemoveSelected()</c> of <c>:372-377</c>.
    /// </summary>
    private void DeleteSelectedMap()
    {
        if (_window.SelectedMap is not { } selected)
        {
            return;
        }

        try
        {
            File.Delete(selected.FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 参考只记一条 ERROR_LOG，然后照样把那一行从列表里去掉。
            GameLog.Warn($"The map '{selected.FilePath}' could not be deleted: {exception.Message}");
        }

        _ = _window.RemoveSelectedMap();
    }
}
