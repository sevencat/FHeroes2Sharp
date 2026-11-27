using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The hot keys page of the system options run on the host's frame pump, the loop of
/// <c>fheroes2::openHotkeysDialog()</c> (<c>dialog_hotkeys.cpp:415-482</c>).
/// </summary>
/// <remarks>
/// 与 <see cref="AudioSettingsModal"/> 同一条缝：一帧画一次，OK 或 Escape 收摊，右键的说明摞在自己的栈上。参考这一页
/// 没有返回值——改键当场写进 <c>Game::HotKey</c>，不落配置，所以这里也没有「脏」要交出去。
/// </remarks>
internal sealed class HotKeysModal : IModalScreen
{
    private readonly HotKeysDialog _dialog;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the page.</summary>
    /// <param name="dialog">The page being run.</param>
    /// <param name="host">Where the explanation boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public HotKeysModal(HotKeysDialog dialog, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _host = host;
    }

    /// <summary>The page being run.</summary>
    public HotKeysDialog Dialog => _dialog;

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

        if (_dialog.ProcessEvents(LocalEvent.Current) is DialogButton.Ok or DialogButton.Cancel)
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
