using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The editor's options page run on the host's frame pump, one turn of <c>openEditorOptionsDialog()</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>openEditorOptionsDialog()</c>（<c>editor_options.cpp:44-231</c>）是个阻塞循环：每帧
/// <c>drawEditorOptionsDialog()</c>、认 OK/ESC/F10 关窗、再 <c>processEvents</c> 拿一个动作，动作不等于
/// <c>Configuration</c> 就 <c>return action</c>，关窗则 <c>return Close</c>。端口的模态只跑一轮：一帧画一次、
/// 拿到的动作不是 <see cref="EditorOptionsAction.None"/> 就收摊，把动作交给调用方——「重开一层」在
/// <c>openEditorSettings()</c> 的循环里（那是场景的活，要重画地图），与参考的分层一致。
/// </para>
/// <para>
/// 右键那九处说明（<c>showStandardTextMessage</c>）摞在这个类自己的栈上，同 <see cref="EditorMapSpecsModal"/>：
/// 参考在循环里直接阻塞开窗，端口不能在这里递归 <c>GameHost.RunModal</c>，只能自己摞一层。
/// </para>
/// </remarks>
internal sealed class EditorOptionsModal : IModalScreen
{
    private readonly EditorOptionsDialog _dialog;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the page.</summary>
    /// <param name="dialog">The page being run.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorOptionsModal(EditorOptionsDialog dialog, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _host = host;
    }

    /// <summary>The action the page was left with, <see cref="EditorOptionsAction.None"/> until it reports one.</summary>
    public EditorOptionsAction Action { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The page being run.</summary>
    public EditorOptionsDialog Dialog => _dialog;

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

        // 内层窗（右键说明）还开着：这一帧只归它，选项页既不读事件也不改状态。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        EditorOptionsAction action = _dialog.ProcessEvents(LocalEvent.Current);

        if (action != EditorOptionsAction.None)
        {
            Action = action;
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
