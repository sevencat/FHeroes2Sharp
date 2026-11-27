using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The audio page of the system options run on the host's frame pump, the loop of
/// <c>Dialog::openAudioSettingsDialog()</c> (<c>dialog_audio.cpp:149-259</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="GraphicsSettingsModal"/> 同一条缝：一帧画一次，<see cref="AudioSettingsDialog.ProcessEvents"/> 说关窗就
/// 收摊，开窗期间有改动就记在 <see cref="AudioSettingsDialog.IsDirty"/> 上，由 <see cref="IsDirty"/> 交出去——
/// 参考的 <c>openAudioSettingsDialog()</c> 返回的正是这个 <c>saveConfig</c>。
/// </para>
/// <para>
/// 右键的四处说明摞在这个类自己的栈上；编辑器不是冒险地图，<c>environmentSoundMixer</c> 恒为
/// <see langword="null"/>，与参考传 <c>false</c> 一致。
/// </para>
/// </remarks>
internal sealed class AudioSettingsModal : IModalScreen
{
    private readonly AudioSettingsDialog _dialog;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the page.</summary>
    /// <param name="dialog">The page being run.</param>
    /// <param name="host">Where the explanation boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public AudioSettingsModal(AudioSettingsDialog dialog, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _host = host;
    }

    /// <summary>The page being run.</summary>
    public AudioSettingsDialog Dialog => _dialog;

    /// <summary>Whether a setting changed, the <c>saveConfig</c> the page reports.</summary>
    public bool IsDirty => _dialog.IsDirty;

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

        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        if (_dialog.ProcessEvents(LocalEvent.Current))
        {
            IsFinished = true;

            return;
        }

        if (_dialog.TakePopup() is { } popup &&
            _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }
}
