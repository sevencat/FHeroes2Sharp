using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The rumors window run on the host's frame pump, <c>openRumorWindow()</c> of the reference.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>openRumorWindow()</c>（<c>editor_rumor_window.cpp:135-305</c>）是个阻塞循环，加一条与改一条都在循环里
/// 直接 <c>Dialog::inputString</c> 阻塞开窗（<c>:231-270</c>），重复了再 <c>showStandardTextMessage</c> 骂一句
/// （<c>:237</c>、<c>:261</c>）。端口的输入框与提示框都摞在这个类自己的栈上，同 <see cref="EditorMapSpecsModal"/>：
/// 栈上有窗口时这一帧只归它，<see cref="EditorRumorWindow.Process"/> 一个事件都不读。
/// </para>
/// <para>
/// 列表本身（选择、双击、删除、滚动、右键说明）全在 <see cref="EditorRumorWindow"/> 里；这里只答它举起来的两面旗
/// —— <see cref="EditorRumorWindow.AddRequested"/> 与 <see cref="EditorRumorWindow.EditRequested"/>——以及被拒时的
/// <see cref="EditorRumorWindow.Popup"/>。
/// </para>
/// <para>
/// 关窗后要不要写回由调用方决定：<see cref="AcceptedRumors"/> 只在 OKAY 时不是 <see langword="null"/>，取消与参考的
/// <c>return false</c> 一样什么都不写。
/// </para>
/// </remarks>
internal sealed class EditorRumorModal : IModalScreen
{
    private readonly EditorRumorWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private bool _addPending;

    /// <summary>Wraps the window.</summary>
    /// <param name="window">The rumors window to run.</param>
    /// <param name="host">Where the input box and the message box are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorRumorModal(EditorRumorWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public EditorRumorWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorListOutcome.None"/> until it reports one.</summary>
    public EditorListOutcome Outcome { get; private set; }

    /// <summary>
    /// The rumors to write back, or <see langword="null"/> when the author cancelled, the <c>return false</c> of
    /// <c>:304</c>.
    /// </summary>
    public IReadOnlyList<string>? AcceptedRumors =>
        Outcome == EditorListOutcome.Accepted ? _window.Rumors : null;

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

        // 内层窗（输入框、重复提示）还开着：这一帧只归它，列表既不读事件也不改。
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

        // 加一条 / 改一条先于右键说明：参考的 :231-270 在 :281-295 之前。
        if (_window.AddRequested)
        {
            OpenInput(string.Empty, isAdd: true);

            return;
        }

        if (_window.EditRequested)
        {
            OpenInput(_window.SelectedRumor ?? string.Empty, isAdd: false);

            return;
        }

        // 右键说明：参考在循环里阻塞开窗再回来，这里只能摞一层，下一帧继续读事件。
        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Opens the input box an add or an edit asks for, the two <c>Dialog::inputString</c> of the reference.</summary>
    /// <param name="initialText">The text the field starts with; empty for a new rumor.</param>
    /// <param name="isAdd">Whether the answer appends a rumor instead of replacing the selected one.</param>
    private void OpenInput(string initialText, bool isAdd)
    {
        InputStringDialog dialog = InputStringDialog.Create(EditorRumorWindow.InputTitle, EditorRumorWindow.InputBody,
            EditorRumorWindow.LongestRumor, isMultiLine: true, initialText);

        if (_host.TryCreateInput(dialog) is not { } input)
        {
            return;
        }

        _addPending = isAdd;

        _inner.Push(input);
    }

    /// <summary>Feeds the answer of a closed inner window back into the list, <c>:235-269</c>.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        bool isAdd = _addPending;

        _addPending = false;

        // 消息框没有答案。
        if (closed is not InputStringModal input)
        {
            return;
        }

        // 取消（Result 为 null）与参考的 inputString 返回 false 一样，不动列表。
        if (input.Window.Result is not { } text)
        {
            return;
        }

        bool applied = isAdd ? _window.TryAddRumor(text) : _window.TryReplaceSelected(text);

        if (applied)
        {
            return;
        }

        // 重复的条目：参考骂一句再回循环（:237、:261）。
        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }
}
