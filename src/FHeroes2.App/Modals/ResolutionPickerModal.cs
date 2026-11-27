using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The resolution chooser run on the host's frame pump, the blocked loop of <c>Dialog::SelectResolution()</c>
/// (<c>dialog_resolution.cpp:196-300</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与其它几页同一条缝：一帧画一次，OK 与 Cancel 收摊。参考这一页的返回值是「有没有换分辨率」——OK 时先
/// <see cref="IDisplayControl.SetResolution"/>，换成功才算，与 <c>selectResolutionDialog()</c> 尾部一致；引擎拒绝时只留一条
/// 日志，同主菜单首运流程的处理。
/// </para>
/// <para>
/// 右键解释摞在这个类自己的栈上：参考是在窗口循环里阻塞开一个 <c>showStandardTextMessage</c>，端口不能在这里递归
/// <see cref="EditorSceneHost.RunModal"/>。
/// </para>
/// </remarks>
internal sealed class ResolutionPickerModal : IModalScreen
{
    private readonly ResolutionDialog _dialog;
    private readonly EditorSceneHost _host;
    private readonly IDisplayControl? _display;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the chooser.</summary>
    /// <param name="dialog">The chooser being run.</param>
    /// <param name="host">Where the explanation boxes are built from.</param>
    /// <param name="display">The window the choice is applied to; <see langword="null"/> when there is none.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>, save <paramref name="display"/>.</exception>
    public ResolutionPickerModal(ResolutionDialog dialog, EditorSceneHost host, IDisplayControl? display)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _host = host;
        _display = display;
    }

    /// <summary>The chooser being run.</summary>
    public ResolutionDialog Dialog => _dialog;

    /// <summary>Whether a new resolution was chosen and applied, the result of <c>Dialog::SelectResolution()</c>.</summary>
    public bool Accepted { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The explanation box on top of the chooser, or <see langword="null"/> when none is open.</summary>
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

        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        switch (_dialog.ProcessEvents(LocalEvent.Current))
        {
            case DialogButton.Ok when _dialog.IsSelected:
                Apply();
                IsFinished = true;

                return;

            case DialogButton.Cancel:
                GameLog.Info("Resolution selection cancelled, keeping the current window.");
                IsFinished = true;

                return;
        }

        if (_dialog.TakePopup() is { } popup &&
            _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Switches the window to the chosen resolution, the <c>engine().setResolution()</c> of the reference.</summary>
    private void Apply()
    {
        if (_display is null)
        {
            GameLog.Warn("There is no display to switch the resolution on; the choice is dropped.");

            return;
        }

        ResolutionInfo resolution = _dialog.SelectedResolution;

        try
        {
            _display.SetResolution(resolution);
            Accepted = true;

            GameLog.Info($"Window resolution {resolution.ScreenWidth}x{resolution.ScreenHeight}.");
        }
        catch (ArgumentException exception)
        {
            GameLog.Warn($"The window could not be resized: {exception.Message}");
        }
    }
}
