using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The editor's AUTO PLAYTEST setup window on the host's frame pump, <c>openMapAutoPlayTest()</c>
/// (<c>game_auto_playtest.cpp:386-660</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的窗口自己转循环，右键说明当场调 <c>showStandardTextMessage()</c>（<c>:599-653</c>），也就是阻塞在窗口里面。
/// 端口把那些说明摞在自己的栈上，关掉之后回来接着读事件。
/// </para>
/// <para>
/// 跑试玩不在这里：窗口只交出 <see cref="Settings"/>，由场景决定跑多少轮、把结果放哪儿——参考的
/// <c>runPlayTest()</c> 与开窗的循环也在同一层（<c>:531-540</c> 调完 <c>runPlayTest()</c> 就 <c>return true</c>）。
/// </para>
/// </remarks>
internal sealed class AutoPlaytestModal : IModalScreen
{
    private readonly AutoPlaytestWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="host">Where the message boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public AutoPlaytestModal(AutoPlaytestWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public AutoPlaytestWindow Window => _window;

    /// <summary>The settings OKAY accepted, or <see langword="null"/> when the author cancelled.</summary>
    public PlaytestSettings? Settings => _window.Settings;

    /// <summary>How the author left the window; <see cref="AutoPlaytestOutcome.None"/> while it is open.</summary>
    public AutoPlaytestOutcome Outcome => _window.Result;

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
    /// Forwards a standard-button key to the window on top, or to the playtest window itself,
    /// <c>Game::HotKeyPressEvent( DEFAULT_OKAY / DEFAULT_CANCEL )</c> (<c>:537-540</c>).
    /// </summary>
    /// <remarks>
    /// 与别的模态不同，这里的主窗口<b>也</b>吃热键：它的 <see cref="AutoPlaytestWindow.ProcessEvents"/> 把
    /// 「Enter / ESC」当成参数收，不像编辑器别的窗口那样在 <c>Process</c> 里自己读 <c>LocalEvent</c>。
    /// 转给内层窗时不碰主窗口，否则一帧会被处理两次。
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

        // 内层说明框还开着：这一帧只归它，主窗口既不读事件也不改设置。
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

    /// <summary>
    /// The standard-button key of the frame, <c>Game::HotKeyPressEvent( DEFAULT_OKAY / DEFAULT_CANCEL )</c>
    /// (<c>:537-540</c>).
    /// </summary>
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
