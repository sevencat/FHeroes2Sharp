using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The editor's file menu run on the host's frame pump, <c>EditorInterface::eventFileDialog()</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>eventFileDialog()</c>（<c>editor_interface.cpp:2068-2181</c>）是个阻塞循环：每帧
/// <c>drawOnState</c>、认七颗按钮、取消、五个热键与九处右键说明。端口的模态每帧调
/// <see cref="EditorFileDialog.ProcessEvents"/>，拿到不是 <see cref="EditorFileResult.None"/> 的动作就收摊，把
/// 结果交给调用方——「按动作去开确认框、去存图、去起新图」在场景里，与 a7l.7.4 的分层一致。
/// </para>
/// <para>
/// 右键说明摞在这个类自己的栈上，同 <see cref="EditorMapSpecsModal"/>：参考在循环里直接阻塞开窗，端口不能在这里
/// 递归 <c>GameHost.RunModal</c>。
/// </para>
/// <para>
/// 参考在存图那一支先 <c>hideWindow()</c> 再开保存窗（<c>:2112-2114</c>），因为两块窗会叠在一起。端口把这一步留给
/// 调用方：<see cref="Result"/> 为 <see cref="EditorFileResult.SaveMap"/> 时模态已经结束，主循环下一帧重画场景，
/// 保存窗自然不会再压着一块已经消失的菜单。
/// </para>
/// </remarks>
internal sealed class EditorFileModal : IModalScreen
{
    private readonly EditorFileDialog _dialog;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the menu.</summary>
    /// <param name="dialog">The menu being run.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorFileModal(EditorFileDialog dialog, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _host = host;
    }

    /// <summary>The button the menu was left with, <see cref="EditorFileResult.None"/> until it reports one.</summary>
    public EditorFileResult Result { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The menu being run.</summary>
    public EditorFileDialog Dialog => _dialog;

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

        _dialog.Draw(frame.Pixels);

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

        // 内层窗（右键说明）还开着：这一帧只归它，菜单既不读事件也不改状态。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        EditorFileResult result = _dialog.ProcessEvents(LocalEvent.Current);

        if (result != EditorFileResult.None)
        {
            Result = result;
            IsFinished = true;

            return;
        }

        // 说明框：参考在循环里阻塞开窗再回到循环，这里只能摞一层，下一帧继续读事件。
        if (_dialog.TakePopup() is { } popup &&
            _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }
}
