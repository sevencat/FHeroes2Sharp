using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The "set the amount" window, <c>Dialog::SelectCount()</c> (<c>dialog_selectcount.cpp:79-225</c>), as a modal.
/// </summary>
/// <remarks>
/// <para>
/// 与别的编辑器模态同构：窗口自己读 <see cref="LocalEvent.Current"/>，走
/// <see cref="SelectCountView.ProcessEvents"/> 那条完整的输入面——键盘直输、滚轮、上下键长按、数字框的两种请求。
/// 答案写在 <see cref="SelectCountView.Dialog"/> 的 <see cref="SelectCountDialog.Result"/> 上。
/// </para>
/// <para>
/// 数字框的两处请求各有落点：点一下开数字键盘（<see cref="EditorSceneHost.TryCreateNumpad"/>，答案回填进
/// <see cref="SelectCountView.SetValue"/>），右键弹范围提示（参考的 <c>showStandardTextMessage</c>）。两者都摞在
/// 这个模态自己的栈上。
/// </para>
/// </remarks>
internal sealed class SelectCountModal : IModalScreen
{
    private readonly SelectCountView _view;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private Point? _cursor;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="view">The window to run.</param>
    /// <param name="host">Where the number pad and the message boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public SelectCountModal(SelectCountView view, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(host);

        _view = view;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public SelectCountView View => _view;

    /// <summary>
    /// The number the author chose, or <see langword="null"/> when the window was cancelled; the
    /// <see cref="SelectCountDialog.Result"/> of the window.
    /// </summary>
    public int? Result => _view.Dialog.Result;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

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

        Finish(_view.ProcessHotKey(hotKey));
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier)
    {
        if (Top is { } inner)
        {
            inner.PressKey(key, modifier);

            return;
        }

        // 键盘直输由视图自己从这一帧的事件里读，所以这里只把键交给内层窗。
        _cursor ??= LocalEvent.Current.CursorPosition;
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _view.Draw(frame.Pixels);

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

        SelectCountFrame result = _view.ProcessEvents(LocalEvent.Current);

        if (result.Answer != DialogButton.None)
        {
            IsFinished = true;

            return;
        }

        switch (result.Request)
        {
            case ValueSelectionRequest.Numpad:
                OpenNumpad();

                break;

            case ValueSelectionRequest.Range:
                OpenRangeMessage();

                break;

            default:
                break;
        }

        if (result.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Opens the number pad of a click on the edit box, <c>openVirtualNumpad()</c> (<c>ui_dialog.cpp:1007</c>).</summary>
    private void OpenNumpad()
    {
        SelectCountDialog dialog = _view.Dialog;

        if (_host.TryCreateNumpad(_view.Value.Value, dialog.Minimum, dialog.Maximum) is not { } pad)
        {
            return;
        }

        // 与每日事件窗同一个包装：越界只弹错误框、键盘留着，关窗才把数字交回来。
        _inner.Push(new EditorMapSpecsValueModal(pad, dialog.Minimum, dialog.Maximum, _host));
    }

    /// <summary>Shows the range of the number, the <c>showStandardTextMessage()</c> of <c>ui_dialog.cpp:1013-1016</c>.</summary>
    private void OpenRangeMessage()
    {
        if (_host.TryCreateMessageBox(string.Empty, _view.RangeMessage) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Feeds the answer of a closed inner window back into the number.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        if (closed is EditorMapSpecsValueModal { Value: { } typed })
        {
            _view.SetValue(typed);
        }
    }

    /// <summary>Closes the window as soon as a button reports an answer.</summary>
    /// <param name="answer">The button that was pressed, or <see cref="DialogButton.None"/>.</param>
    private void Finish(DialogButton answer)
    {
        if (answer == DialogButton.None)
        {
            return;
        }

        IsFinished = true;
    }
}
