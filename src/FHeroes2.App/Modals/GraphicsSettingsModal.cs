using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The graphics page of the system options run on the host's frame pump, one turn of
/// <c>showConfigurationWindow()</c> (<c>dialog_graphics_settings.cpp:147-236</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>openGraphicsSettingsDialog()</c>（<c>:238-295</c>）是个 <c>SelectedWindow</c> 状态机：关窗就是
/// <c>Exit</c>，任何一项选完都回到 <c>Configuration</c> 重开同一页。端口的模态只跑一轮：一帧画一次、拿到的叶子不是
/// <see cref="GraphicsSettingsOption.None"/> 就收摊，交给调用方去 <see cref="GraphicsSettingsDialog.Apply"/> 再重开——
/// 与 <see cref="EditorOptionsModal"/> 同一条缝。
/// </para>
/// <para>
/// 右键的五处说明（<c>showStandardTextMessage</c>）摞在这个类自己的栈上：参考在窗口循环里直接阻塞开窗，端口不能在这里
/// 递归 <c>GameHost.RunModal</c>，只能自己叠一层。
/// </para>
/// </remarks>
internal sealed class GraphicsSettingsModal : IModalScreen
{
    private readonly GraphicsSettingsDialog _dialog;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the page.</summary>
    /// <param name="dialog">The page being run.</param>
    /// <param name="host">Where the explanation boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public GraphicsSettingsModal(GraphicsSettingsDialog dialog, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _host = host;
    }

    /// <summary>The page being run.</summary>
    public GraphicsSettingsDialog Dialog => _dialog;

    /// <summary>The leaf the page was left with, <see cref="GraphicsSettingsOption.None"/> until it reports one.</summary>
    public GraphicsSettingsOption Option { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The explanation box on top of the page, or <see langword="null"/> when none is open.</summary>
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

        // 说明框还开着：这一帧只归它，选项页既不读事件也不改状态。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        GraphicsSettingsOption option = _dialog.ProcessEvents(LocalEvent.Current);

        if (option != GraphicsSettingsOption.None)
        {
            Option = option;
            IsFinished = true;

            return;
        }

        // 右键说明：参考在循环里阻塞开窗再回到循环，这里只能叠一层，下一帧继续读事件。
        if (_dialog.TakePopup() is { } popup &&
            _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }
}
