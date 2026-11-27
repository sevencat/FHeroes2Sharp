using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// One daily event being edited, on the host's frame pump, the <c>editDailyEvent()</c> of the reference
/// (<c>editor_daily_event_spec_window.cpp:67-372</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的窗口在循环里自己阻塞开三个子窗：资源数量 <c>Dialog::SelectCount</c>（<c>:320</c>）、消息
/// <c>Dialog::inputString</c>（<c>:336</c>），以及两个数字框在窗口内就地把值改回去。端口只有消息这一层接得上
/// ——输入框已有；数量窗与数字键盘还没有（见 <c>qto.2</c> 拆出去的票），请求落在日志里，作者得靠窗口里的数字框
/// 微调。
/// </para>
/// <para>
/// 同 <see cref="EditorRumorModal"/>：栈上有窗时这一帧只归它，<see cref="EditorDailyEventSpecWindow.Process"/>
/// 一个事件都不读。
/// </para>
/// </remarks>
internal sealed class EditorDailyEventSpecModal : IModalScreen
{
    private readonly EditorDailyEventSpecWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the window.</summary>
    /// <param name="window">The event window to run.</param>
    /// <param name="host">Where the input box and the message box are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorDailyEventSpecModal(EditorDailyEventSpecWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public EditorDailyEventSpecWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorListOutcome.None"/> until it reports one.</summary>
    public EditorListOutcome Outcome { get; private set; }

    /// <summary>
    /// The event to write back, or <see langword="null"/> when the author cancelled, the <c>return false</c> of
    /// <c>:242</c>.
    /// </summary>
    public MapDailyEvent? AcceptedEvent => Outcome == EditorListOutcome.Accepted ? _window.Event : null;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether an input box or a message box is stacked above the window.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack; <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Top?.HotKey(hotKey);

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

        EditorListOutcome outcome = _window.Process(LocalEvent.Current);

        if (outcome != EditorListOutcome.None)
        {
            Outcome = outcome;
            IsFinished = true;

            return;
        }

        // 消息区（:332-344）先于右键说明（:345-368）：参考的顺序照抄。
        if (_window.MessageRequested)
        {
            OpenInput();

            return;
        }

        // 数量窗与数字键盘还没有，参考的 SelectCount 与 numpad 请求只能落在日志里。
        if (_window.ResourceCountRequest is { } resource)
        {
            GameLog.Warn($"The count window of resource {resource.ResourceType} is not wired up yet.");
        }

        if (_window.NumpadRequest is { } numpad)
        {
            GameLog.Warn($"The numpad for {numpad.Field} is not wired up yet.");
        }

        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Opens the message input box, <c>Dialog::inputString</c> of <c>:336</c>.</summary>
    private void OpenInput()
    {
        InputStringDialog dialog = InputStringDialog.Create(string.Empty,
            EditorDailyEventSpecWindow.MessageDialogHeader, EditorDailyEventSpecWindow.MessageCharLimit,
            isMultiLine: true, _window.Event.Message);

        if (_host.TryCreateInput(dialog) is not { } input)
        {
            return;
        }

        _inner.Push(input);
    }

    /// <summary>Feeds the message the input box produced back into the event, <c>:337</c>.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        if (closed is InputStringModal { Window.Result: { } text })
        {
            _window.SetMessage(text);
        }
    }
}
