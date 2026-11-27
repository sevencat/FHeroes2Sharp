using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The tally a series of auto playthroughs left behind, <c>displayResults()</c>
/// (<c>game_auto_playtest.cpp:107-256</c>), on the host's frame pump.
/// </summary>
/// <remarks>
/// 与配置窗同构：窗口自己读 <c>LocalEvent</c>，右键说明摞在自己的栈上。参考在这一屏还会放胜利音乐，端口的音乐队列
/// 不在这一层，所以只留窗口。
/// </remarks>
internal sealed class AutoPlaytestResultsModal : IModalScreen
{
    private readonly AutoPlaytestResultsWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="host">Where the message boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public AutoPlaytestResultsModal(AutoPlaytestResultsWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public AutoPlaytestResultsWindow Window => _window;

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

    /// <summary>
    /// Forwards a standard-button key to the window on top, or to the results window itself.
    /// </summary>
    /// <remarks>
    /// 与配置窗同一个理由：结果窗把 Enter / ESC 当参数收，自己并不读键，所以主窗口也要接。
    /// </remarks>
    /// <param name="hotKey">The key that was pressed.</param>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (Top is { } inner)
        {
            inner.HotKey(hotKey);

            return;
        }

        if (_window.ProcessEvents(LocalEvent.Current, hotKey))
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels);

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

        if (_window.ProcessEvents(LocalEvent.Current, HotKeyOf(LocalEvent.Current)))
        {
            IsFinished = true;

            return;
        }

        if (_window.TakePopup() is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>The standard-button key of the frame.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The key, or <see langword="null"/> when neither was pressed.</returns>
    private static ButtonHotKey? HotKeyOf(LocalEvent events)
    {
        if (events.IsKeyPressed(Key.Enter))
        {
            return ButtonHotKey.Okay;
        }

        return events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10) ? ButtonHotKey.Cancel : null;
    }
}
