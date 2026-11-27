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
/// <c>Dialog::inputString</c>（<c>:336</c>），以及两个数字框点开后 <c>openVirtualNumpad()</c>（<c>:223</c>、<c>:236</c>）。
/// 端口把三扇都摞在自己这一层的栈上：数量窗走 <see cref="SelectCountModal"/>、消息走
/// <see cref="InputStringModal"/>、数字键盘走 <see cref="EditorMapSpecsValueModal"/>（同一个
/// <c>openVirtualNumpad()</c>，所以直接复用那一层，否则错误框的「越界不关窗」逻辑要写第二遍）。
/// </para>
/// <para>
/// 栈上有窗时这一帧只归它，<see cref="EditorDailyEventSpecWindow.Process"/> 一个事件都不读。
/// </para>
/// </remarks>
internal sealed class EditorDailyEventSpecModal : IModalScreen
{
    private readonly EditorDailyEventSpecWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>The resource whose amount window is open, or <c>-1</c> when none is.</summary>
    private int _pendingResource = -1;

    /// <summary>The day field whose number pad is open, or <see cref="DailyEventValueField.None"/> when none is.</summary>
    private DailyEventValueField _pendingNumpad = DailyEventValueField.None;

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

        if (_window.ResourceCountRequest is { } resource)
        {
            OpenResourceCount(resource);

            return;
        }

        if (_window.NumpadRequest is { } numpad)
        {
            OpenNumpad(numpad);

            return;
        }

        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>
    /// Opens the amount window of one resource, <c>Dialog::SelectCount</c> of <c>:320</c>.
    /// </summary>
    /// <param name="request">The resource and the dialog the window filled in.</param>
    private void OpenResourceCount(DailyEventResourceRequest request)
    {
        if (_host.TryCreateSelectCount(request.Dialog, request.Value) is not { } window)
        {
            GameLog.Warn($"The count window of resource {request.ResourceType} cannot be built.");

            return;
        }

        _pendingResource = request.ResourceType;
        _inner.Push(window);
    }

    /// <summary>
    /// Opens the number pad of one of the two day fields, the <c>openVirtualNumpad()</c> of <c>:223</c> and <c>:236</c>.
    /// </summary>
    /// <param name="request">The field, its range and its current value.</param>
    private void OpenNumpad(DailyEventNumpadRequest request)
    {
        if (_host.TryCreateNumpad(request.Value, request.Minimum, request.Maximum) is not { } pad)
        {
            GameLog.Warn($"The number pad of {request.Field} cannot be built.");

            return;
        }

        _pendingNumpad = request.Field;
        _inner.Push(new EditorMapSpecsValueModal(pad, request.Minimum, request.Maximum, _host));
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

    /// <summary>Feeds the answer of a closed inner window back into the event.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        switch (closed)
        {
            case InputStringModal { Window.Result: { } text }:
                _window.SetMessage(text);

                return;

            case SelectCountModal count:
            {
                int resourceType = _pendingResource;
                _pendingResource = -1;

                if (count.Result is { } value)
                {
                    _window.SetResourceCount(resourceType, value);
                }

                return;
            }

            case EditorMapSpecsValueModal pad:
            {
                DailyEventValueField field = _pendingNumpad;
                _pendingNumpad = DailyEventValueField.None;

                if (pad.Value is { } value)
                {
                    _window.SetValueField(field, value);
                }

                return;
            }

            default:
                return;
        }
    }
}
