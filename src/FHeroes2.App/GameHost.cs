using System.Diagnostics;
using FHeroes2.Assets;
using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;
using FHeroes2.App.Audio;
using FHeroes2.App.Fonts;
using FHeroes2.App.Graphics;
using FHeroes2.App.Input;
using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Core.Timing;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.Game.H2D;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using SDL3;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App;

/// <summary>
/// Composition root and SDL3 host of the desktop client: owns the SDL lifetime, the window and
/// renderer (through <see cref="SdlPresenter"/>), the text backend, the frame loop and the
/// currently active scene.
/// </summary>
public sealed class GameHost : IDisposable
{
    /// <summary><c>SDL_BUTTON_LEFT</c>; picks a sign of the menu.</summary>
    private const int LeftMouseButton = 1;

    /// <summary><c>SDL_BUTTON_RIGHT</c>; shows the tooltip of the sign under it while held.</summary>
    private const int RightMouseButton = 3;

    /// <summary>Frame at which <c>--left-click</c> fires: late enough for the menu to be laid out.</summary>
    private const int LeftClickFrame = 10;

    private readonly CommandLineOptions _options;
    private readonly FrameTimer _timer = new();
    private readonly bool _syntheticCursor;

    /// <summary>
    /// The event state every frame is fed into, the reference's <c>LocalEvent::Get()</c>
    /// (<c>localevent.h:186</c>). It is process wide on purpose: <c>GameArea</c>, <c>ItemsBar</c> and
    /// the dialogs query it without being handed one.
    /// </summary>
    private readonly LocalEvent _le = LocalEvent.Current;

    private AssetBundle? _assets;
    private H2DResources? _h2d;
    private Palette _palette = new();
    private ScreenBuffer _frame = null!;
    private SdlPresenter? _presenter;
    private SdlTextRenderer? _text;
    private GameFonts? _fonts;
    private AudioPlayer? _audio;
    private IScene? _scene;
    private IScene? _drawnScene;
    private IModalScreen? _modal;
    private MainMenuScene? _menu;
    private DiagnosticsScene? _diagnostics;
    private AdventureMapScene? _adventure;
    private CastleScene? _castle;
    private EditorScene? _editor;
    private EditorMainMenuScene? _editorMenu;
    private SystemInfoRenderer? _systemInfo;
    private bool _supportsCjk;
    private bool _sdlInitialised;
    private bool _ttfInitialised;
    private bool _running;
    private int _screenshotFrame = -1;
    private bool _screenshotRequested;
    private bool _disposed;

    public GameHost(CommandLineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _syntheticCursor = options.Cursor is not null;

        if (options.ScreenshotPath is not null)
        {
            int captureFrame = options.ScreenshotFrame;
            if (options.SmokeFrames is int smokeFrames && smokeFrames < captureFrame)
            {
                captureFrame = smokeFrames;
            }

            _screenshotFrame = Math.Max(1, captureFrame);
        }
    }

    /// <summary>Process exit code; non zero when mandatory game data or SDL was missing.</summary>
    public int ExitCode { get; private set; }

    /// <summary>Runs the client until the window is closed or the smoke frame budget is spent.</summary>
    public void Run()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            GameLog.Error($"SDL_Init failed: {SDL.GetError()}");
            ExitCode = 1;
            return;
        }

        _sdlInitialised = true;

        if (!TTF.Init())
        {
            GameLog.Error($"TTF_Init failed: {SDL.GetError()}");
            ExitCode = 1;
            return;
        }

        _ttfInitialised = true;
        GameLog.Info($"{BuildInfo.DisplayName} starting (SDL {SDL.GetVersion()}, SDL_ttf {TTF.Version()})");

        LoadGameData();
        _fonts = GameFonts.TryLoad(_assets);

        try
        {
            _presenter = new SdlPresenter(ScreenBuffer.DefaultSize, _options.WindowScale, _options.Fullscreen);

            _frame = new ScreenBuffer(ScreenBuffer.DefaultSize, _palette);

            // The reference installs its engine on the event state once, from the application object;
            // the host does it as soon as the frame it presents exists.
            _le.Engine = new SdlEventEngine(PumpEvents, _ => _presenter?.Upload(_frame), _frame.Bounds);

            if (_options.SystemInfo)
            {
                _systemInfo = new SystemInfoRenderer();
                GameLog.Info("System info overlay enabled: clock and FPS in the bottom left corner.");
            }

            CreateTextRenderer();
            CreateScenes();
            StartAudio();

            GameLog.Info(
                $"Presentation ready: {_presenter.OutputSize} output, x{_presenter.Viewport.Scale:0.##} scale, " +
                $"logical {_presenter.LogicalSize}.");

            if (_options.SmokeFrames is int frames)
            {
                GameLog.Info($"Smoke mode: the client will exit after {frames} rendered frames.");
            }

            _running = true;
            Loop();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error("The client stopped because of an SDL error", exception);
            ExitCode = 1;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Textures belong to the renderer, so the text backend has to go first.
        _text?.Dispose();
        _text = null;

        _presenter?.Dispose();
        _presenter = null;

        // Music and sound effects own SDL resources of their own, cleaned up before SDL_Quit.
        _audio?.Dispose();
        _audio = null;

        _h2d?.Dispose();
        _h2d = null;
        H2DResources.Current = null;

        _assets?.Dispose();
        _assets = null;

        if (_ttfInitialised)
        {
            TTF.Quit();
            _ttfInitialised = false;
        }

        if (_sdlInitialised)
        {
            SDL.Quit();
            _sdlInitialised = false;
        }
    }

    /// <summary>
    /// Runs <paramref name="screen"/> on top of the current scene until it closes, the port's stand-in for
    /// the blocking dialogs of the reference (<c>Dialog::Message</c> opens its own loop and returns when the
    /// player has answered).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的对话框自己转事件循环，关掉才返回；端口的主循环是「每帧拉一次场景」的状态机，所以这里再起一层
    /// 同样的循环：取事件（只交给窗口）→ 场景重绘 → 窗口画上去 → 上传。窗口开着时输入不再流进下面的场景，
    /// 关掉后外层循环接着跑，场景状态自然就是对的。
    /// </para>
    /// <para>
    /// 可以在事件处理里调用（<c>IAdventureHost</c> 就是这样，宿主方法从 <see cref="PumpEvents"/> 的调用栈
    /// 里被唤起）：内层再取一次事件是安全的，SDL 的事件队列是全局的，<see cref="_modal"/> 保证不会重复分派。
    /// </para>
    /// </remarks>
    /// <param name="screen">Window to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Another window is already open.</exception>
    public void RunModal(IModalScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        if (_modal is not null)
        {
            // 参考同时只会有一个嵌套对话框；再开一个说明调用方把栈写错了，不能悄悄叠上去。
            throw new InvalidOperationException("Only one modal window can be open at a time.");
        }

        Stopwatch clock = Stopwatch.StartNew();
        double previous = clock.Elapsed.TotalSeconds;
        _modal = screen;

        try
        {
            while (!screen.IsFinished && _running)
            {
                // The window loop owns the frame state too: the events of this frame must not pile up
                // on the ones of the frozen scene below.
                _le.BeginFrame();

                if (!PumpEvents())
                {
                    _running = false;
                    break;
                }

                double now = clock.Elapsed.TotalSeconds;

                // 与主循环同样的钳位理由：拖动窗口不该让动画跳帧。
                double elapsed = Math.Clamp(now - previous, 0.0, 0.5);
                previous = now;

                screen.Update(_frame, elapsed);
                Draw();
            }
        }
        finally
        {
            _modal = null;
        }
    }

    private void Loop()
    {
        Stopwatch clock = Stopwatch.StartNew();
        double previous = clock.Elapsed.TotalSeconds;

        while (_running)
        {
            // The frame starts at the event state, like the reference: LocalEvent::HandleEvents resets
            // the one-shot states, lets the engine poll, and skips presenting because this loop draws
            // and uploads right after, which is the reference's HandleEvents( false ) + render() pair.
            if (!_le.HandleEvents(sleepAfterEventProcessing: false, allowExit: true))
            {
                break;
            }

            // --left-click replays one click from inside the loop, i.e. after the event batch and
            // before the scene update, which is the frame order a player produces. Doing it in
            // CreateScenes would skip the first frames of the new scene.
            // A town screen swallows the replay: it is modal, and its EXIT is the one click worth
            // replaying there (the arrows need a second click in the same run to be visible).
            if (_options.LeftClick && _castle is { } townScreen && _options.Cursor is { } townPoint && _timer.FrameCount == LeftClickFrame)
            {
                townScreen.SetCursor(townPoint);
                townScreen.SetLeftButton(true);
                townScreen.SetLeftButton(false);
            }
            else if (_options.LeftClick && _menu is not null && _timer.FrameCount == LeftClickFrame)
            {
                _menu.SetCursor(_options.Cursor);
                _menu.SetLeftButton(true);

                if (_menu.SetLeftButton(false) is { } clicked)
                {
                    HandleMenuAction(clicked);
                }
            }

            // The menu does not know about scenes: a confirmed scenario surfaces here as soon as the
            // input batch that confirmed it has been handled.
            TryEnterAdventureMap();
            TryOpenCastleFromAdventure();
            CloseCastleWhenDone();

            double now = clock.Elapsed.TotalSeconds;

            // Clamp the step so that dragging the window does not teleport the animation.
            double elapsed = Math.Clamp(now - previous, 0.0, 0.5);
            previous = now;

            Update(elapsed);
            _audio?.Update();
            Draw();
        }
    }

    /// <summary>Drains the SDL event queue; returns <see langword="false"/> when the client must stop.</summary>
    /// <param name="allowExit">
    /// Whether a close request may end the client. Dialogs pass <see langword="false"/> and leave the
    /// decision to <see cref="LocalEvent.ProcessQuitEvent"/>, the reference's quit hook
    /// (<c>localevent.cpp:1299-1305</c>).
    /// </param>
    private bool PumpEvents(bool allowExit = true)
    {
        while (SDL.PollEvent(out SDL.Event evt))
        {
            // The state is fed before any scene sees the event: the reference's engine pushes every
            // event into LocalEvent, and the scene handlers read the state afterwards
            // (localevent.cpp:1265-1313).
            FeedLocalEvent(evt);

            switch ((SDL.EventType)evt.Type)
            {
                case SDL.EventType.Quit:
                case SDL.EventType.WindowCloseRequested:
                    if (!allowExit && !_le.ProcessQuitEvent())
                    {
                        // A window without the exit hook ignores the close request, like the reference.
                        break;
                    }

                    return false;

                case SDL.EventType.KeyDown when HandleKeyDown(evt.Key):
                    return false;

                case SDL.EventType.MouseMotion:
                    // A modal window owns the pointer while it is open: the scene below keeps its state
                    // (it is frozen) and only the window reacts.
                    if (_modal is { } modal)
                    {
                        if (!_syntheticCursor)
                        {
                            modal.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }

                        break;
                    }

                    // With --cursor the synthetic pointer is what a screenshot should show, so the
                    // real one must not steal the hover state.
                    if (!_syntheticCursor)
                    {
                        if (_castle is { } townScreen)
                        {
                            townScreen.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                        else if (_editorMenu is { } editorMenu)
                        {
                            editorMenu.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                        else if (_editor is { } editing)
                        {
                            editing.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                        else if (_adventure is { } moving)
                        {
                            Point? cursor = ToLogical(evt.Motion.X, evt.Motion.Y);

                            // The View World window drags its map with the same left button, so the
                            // pointer goes to the window while it is open.
                            if (moving.ViewWorld is { } viewWorld)
                            {
                                viewWorld.DragTo(cursor);
                            }
                            else
                            {
                                moving.SetCursor(cursor);
                            }
                        }
                        else
                        {
                            _menu?.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                    }

                    break;

                case SDL.EventType.WindowMouseLeave:
                    if (_modal is { } leavingModal)
                    {
                        if (!_syntheticCursor)
                        {
                            leavingModal.SetCursor(null);
                        }

                        break;
                    }

                    if (!_syntheticCursor)
                    {
                        _castle?.SetCursor(null);
                        _editorMenu?.SetCursor(null);
                        _editor?.SetCursor(null);
                        _adventure?.SetCursor(null);
                        _adventure?.ViewWorld?.DragTo(null);
                        _menu?.SetCursor(null);
                    }

                    break;

                case SDL.EventType.MouseButtonDown:
                case SDL.EventType.MouseButtonUp:
                    if (_modal is { } clickedModal)
                    {
                        HandleModalButton(clickedModal, evt.Button);
                        break;
                    }

                    if (HandleMouseButton(evt.Button))
                    {
                        return false;
                    }

                    break;

                case SDL.EventType.MouseWheel:
                    // A modal window has no list to roll in this milestone, so the wheel stops at it
                    // instead of scrolling the frozen scene below.
                    if (_modal is not null)
                    {
                        break;
                    }

                    // SDL reports a positive y when the wheel turns away from the user, which rolls
                    // a list up and moves the view towards the top of the map. The town screen has
                    // nothing to scroll, so the wheel is dropped while it is open.
                    if (!_syntheticCursor && _castle is null && _editorMenu is null)
                    {
                        int notches = evt.Wheel.IntegerY != 0 ? evt.Wheel.IntegerY : (int)Math.Sign(evt.Wheel.Y);

                        if (notches != 0)
                        {
                            if (_editor is { } edited)
                            {
                                edited.ScrollByWheel(0, -notches);
                            }
                            else if (_adventure is { } scrolled)
                            {
                                // The wheel zooms the View World window instead of scrolling the map
                                // behind it; away from the user is zoom in, like isMouseWheelUp().
                                if (scrolled.ViewWorld is { } zooming)
                                {
                                    zooming.Wheel(notches);
                                }
                                else
                                {
                                    scrolled.ScrollByWheel(0, -notches);
                                }
                            }
                            else
                            {
                                _menu?.ScrollScenario(-notches);
                            }
                        }
                    }

                    break;

                default:
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Pushes one SDL event into <see cref="_le"/>, the translation the reference's engine does inside
    /// <c>handleEvents</c> (<c>localevent.cpp:1658-1747</c>).
    /// </summary>
    /// <param name="evt">Event taken from the SDL queue.</param>
    private void FeedLocalEvent(in SDL.Event evt)
    {
        switch ((SDL.EventType)evt.Type)
        {
            case SDL.EventType.MouseMotion:
                _le.OnMouseMotion(ToCursor(evt.Motion.X, evt.Motion.Y));
                break;

            case SDL.EventType.MouseButtonDown:
            case SDL.EventType.MouseButtonUp:
                {
                    MouseButtonType button = evt.Button.Button switch
                    {
                        LeftMouseButton => MouseButtonType.Left,
                        RightMouseButton => MouseButtonType.Right,
                        2 => MouseButtonType.Middle,
                        _ => MouseButtonType.Unknown,
                    };

                    if (button != MouseButtonType.Unknown)
                    {
                        _le.OnMouseButton(
                            (SDL.EventType)evt.Type == SDL.EventType.MouseButtonDown,
                            button,
                            ToCursor(evt.Button.X, evt.Button.Y));
                    }

                    break;
                }

            case SDL.EventType.MouseWheel:
                {
                    int notches = evt.Wheel.IntegerY != 0 ? evt.Wheel.IntegerY : (int)Math.Sign(evt.Wheel.Y);

                    // SDL reports a positive y when the wheel turns away from the user, which is a
                    // positive offset for the reference and what isMouseWheelUp() asks for.
                    if (notches != 0)
                    {
                        _le.OnMouseWheel(new Point(0, notches));
                    }

                    break;
                }

            case SDL.EventType.KeyDown:
            case SDL.EventType.KeyUp:
                {
                    Key key = SdlKeyMap.ToKey((int)evt.Key.Key);

                    if (key != Key.None)
                    {
                        _le.OnKeyboard(
                            key,
                            SdlKeyMap.ToModifier((int)evt.Key.Mod),
                            (SDL.EventType)evt.Type == SDL.EventType.KeyDown
                                ? KeyboardEventState.Down
                                : KeyboardEventState.Up);
                    }

                    break;
                }

            default:
                break;
        }
    }

    /// <summary>
    /// Cursor position in logical pixels; the raw position when the pointer is over the letterbox and
    /// the viewport refuses to convert it, which keeps the state machine out of screen bounds like the
    /// reference does.
    /// </summary>
    private Point ToCursor(float x, float y) =>
        ToLogical(x, y) ?? new Point((int)MathF.Round(x), (int)MathF.Round(y));

    /// <summary>Handles a key press; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleKeyDown(in SDL.KeyboardEvent key)
    {
        // Engine wide keys stay available on every screen, including the adventure map.
        switch (key.Key)
        {
            case SDL.Keycode.F11 when !key.Repeat:
                _presenter?.ToggleFullscreen();
                return false;

            case SDL.Keycode.F12 when !key.Repeat:
                _screenshotRequested = true;
                return false;

            default:
                break;
        }

        // Escape / Enter / Space are the standard buttons of whichever window is open: the modal owns
        // every key while it is up, the scene below is frozen.
        if (_modal is { } modal)
        {
            HandleModalKey(modal, key);
            return false;
        }

        if (_editor is { } editor)
        {
            return HandleEditorKey(key, editor);
        }

        if (_editorMenu is { } editorMenu)
        {
            // SDL key codes of the letters are their ASCII values, so the hot keys of
            // editor_mainmenu.cpp (N, L, S, R, S/M/L/X and Escape) can be forwarded as characters.
            if (!key.Repeat && editorMenu.Select((char)key.Key) is { } request)
            {
                HandleEditorMenuRequest(request);
            }

            return false;
        }

        if (_castle is { } townScreen)
        {
            // Escape and E leave the town screen or the window that is open above it, the arrows switch
            // town; the keys are the screen's own business.
            townScreen.HandleKey(key.Key, key.Repeat);

            return false;
        }

        if (_adventure is { } adventure)
        {
            return HandleAdventureKey(key, adventure);
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                // Escape closes an open modal window first, the CANCEL button of the original; only
                // a bare menu Escape leaves the client.
                if (_menu?.HasModalDialog == true)
                {
                    _menu.Select((char)SDL.Keycode.Escape);
                    return false;
                }

                return true;

            case SDL.Keycode.Up when _menu?.Scenario is not null:
                _menu.MoveScenarioSelection(-1);
                return false;

            case SDL.Keycode.Down when _menu?.Scenario is not null:
                _menu.MoveScenarioSelection(1);
                return false;

            case SDL.Keycode.Pageup when _menu?.Scenario is not null:
                _menu.MoveScenarioPage(-1);
                return false;

            case SDL.Keycode.Pagedown when _menu?.Scenario is not null:
                _menu.MoveScenarioPage(1);
                return false;

            case SDL.Keycode.Home when _menu?.Scenario is not null:
                _menu.SelectFirstScenario();
                return false;

            case SDL.Keycode.End when _menu?.Scenario is not null:
                _menu.SelectLastScenario();
                return false;

            // The language hot key rebuilds the menu, so it stays out of the way while a modal
            // window owns the text: the settings dialog has its own language row, the scenario list
            // is rebuilt in the new language the next time it opens.
            case SDL.Keycode.F2 when !key.Repeat && _menu?.HasModalDialog != true:
                _menu?.NextLanguage();
                return false;

            default:
                // SDL key codes of the letters are their ASCII values, so the hot keys of the
                // original main menu (N, L, H, C, Q) can be forwarded as characters.
                return _menu is not null
                    && !key.Repeat
                    && _menu.Select((char)key.Key) is { } hotKeyAction
                    && HandleMenuAction(hotKeyAction);
        }
    }

    /// <summary>Routes a mouse button event to the window that owns the frame.</summary>
    /// <remarks>
    /// 点击自带坐标，所以即使 <c>--cursor</c> 让移动事件被忽略，合成点击也能落到窗口里的按钮上——和城镇
    /// 界面同样的处理。
    /// </remarks>
    private void HandleModalButton(IModalScreen modal, in SDL.MouseButtonEvent button)
    {
        modal.SetCursor(ToLogical(button.X, button.Y));

        switch (button.Button)
        {
            case LeftMouseButton:
                modal.SetLeftButton(button.Down);
                break;

            case RightMouseButton:
                modal.SetRightButton(button.Down);
                break;

            default:
                break;
        }
    }

    /// <summary>Maps the keys that stand for the standard buttons of a window, <c>Dialog::YES_NO</c>'s Enter.</summary>
    private static void HandleModalKey(IModalScreen modal, in SDL.KeyboardEvent key)
    {
        if (key.Repeat)
        {
            return;
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                modal.HotKey(ButtonHotKey.Cancel);
                break;

            case SDL.Keycode.Return:
            case SDL.Keycode.KpEnter:
            case SDL.Keycode.Space:
                modal.HotKey(ButtonHotKey.Okay);
                break;

            default:
                break;
        }
    }

    /// <summary>Handles a mouse button event; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleMouseButton(in SDL.MouseButtonEvent button)
    {
        if (_editor is { } editor)
        {
            if (!_syntheticCursor)
            {
                editor.SetCursor(ToLogical(button.X, button.Y));
            }

            // The editor paints with the left button and pans with the right one, the way the
            // reference routes the left click to the active instrument and the right one to the
            // map drag.
            if (button.Button == LeftMouseButton)
            {
                if (editor.SetLeftButton(button.Down))
                {
                    GameLog.Info($"Painted with '{editor.Edits.Brush}'; {editor.Edits.UndoDepth} undoable strokes.");
                }
            }
            else if (button.Button == RightMouseButton)
            {
                if (editor.SetRightButton(button.Down))
                {
                    GameLog.Info($"Picked the '{editor.Edits.Brush}' brush from the map.");
                }
            }

            return false;
        }

        if (_editorMenu is { } editorMenu)
        {
            // The editor main menu answers both buttons: the left one walks its three states, the
            // right one shows the tooltip of the button it is held over.
            if (!_syntheticCursor)
            {
                editorMenu.SetCursor(ToLogical(button.X, button.Y));
            }

            switch (button.Button)
            {
                case LeftMouseButton:
                    if (editorMenu.SetLeftButton(button.Down) is { } request)
                    {
                        HandleEditorMenuRequest(request);
                    }

                    return false;

                case RightMouseButton:
                    editorMenu.SetRightButton(button.Down);
                    return false;

                default:
                    return false;
            }
        }

        if (_castle is { } townScreen)
        {
            // The town screen is modal: the left button clicks its areas, the right one is handed to the
            // windows that use it (the marketplace shows the kingdom income while it is held), and
            // everything else is dropped. The click carries its own position, so a synthetic click from
            // the command line finds its area even though motion events are ignored while --cursor is in use.
            townScreen.SetCursor(ToLogical(button.X, button.Y));

            if (button.Button == LeftMouseButton)
            {
                townScreen.SetLeftButton(button.Down);
            }
            else if (button.Button == RightMouseButton)
            {
                townScreen.SetRightButton(button.Down);
            }

            return false;
        }

        if (_adventure is { } adventure)
        {
            if (adventure.ViewWorld is { } viewWorld)
            {
                // The window answers the left button only: exit, zoom, the radar and the drag of the
                // map itself, view_world.cpp:723-762.
                if (button.Button == LeftMouseButton
                    && ToLogical(button.X, button.Y) is { } point
                    && viewWorld.SetLeftButton(button.Down, point))
                {
                    ZoomLevel? level = adventure.CloseViewWorld();

                    GameLog.Info($"The View World window closed at zoom level {level}.");
                }

                return false;
            }

            // The options dialog is modal: it takes the left button and nothing else reaches the map.
            if (adventure.AdventureOptions is { } options)
            {
                if (button.Button == LeftMouseButton && ToLogical(button.X, button.Y) is { } optionsPoint)
                {
                    DialogButton answer = options.SetLeftButton(button.Down, optionsPoint);

                    if (answer != DialogButton.None)
                    {
                        ResolveAdventureOptions(adventure, answer);
                    }
                }

                return false;
            }

            // The left button walks the hero, the right one shows the quick info of the tile under the
            // pointer for as long as it is held (Interface::MouseRightClick).
            if (!_syntheticCursor)
            {
                adventure.SetCursor(ToLogical(button.X, button.Y));
            }

            if (button.Button == LeftMouseButton)
            {
                adventure.SetLeftButton(button.Down);
            }
            else if (button.Button == RightMouseButton)
            {
                adventure.SetRightButton(button.Down);
            }

            return false;
        }

        if (_menu is null)
        {
            return false;
        }

        if (!_syntheticCursor)
        {
            _menu.SetCursor(ToLogical(button.X, button.Y));
        }

        switch (button.Button)
        {
            case RightMouseButton:
                _menu.SetRightButton(button.Down);
                return false;

            case LeftMouseButton:
                return _menu.SetLeftButton(button.Down) is { } action && HandleMenuAction(action);

            default:
                return false;
        }
    }

    /// <summary>Reacts to the choice of a main menu sign; returns <see langword="true"/> to stop.</summary>
    private bool HandleMenuAction(MainMenuAction action)
    {
        // The editor sign opens the editor's own main menu, the window `Editor::menuMain()` of
        // editor_mainmenu.cpp shows; the map is picked there, not by the game main menu.
        if (action == MainMenuAction.Editor)
        {
            OpenEditorMenu();
            return false;
        }

        if (action != MainMenuAction.Quit)
        {
            // The other signs are wired up from M3 on; the scene already shows this in its status bar.
            return false;
        }

        GameLog.Info("Quit selected, closing the client.");
        _running = false;
        return true;
    }

    /// <summary>
    /// Handles the keys of the adventure map: the arrow keys scroll by one tile, <c>TAB</c> takes the next
    /// hero (with <c>H</c> as an alias) and <c>E</c> ends the turn (the <c>WORLD_NEXT_HERO</c> and
    /// <c>WORLD_END_TURN</c> hot keys of the reference), Home returns to the top left corner and Escape goes
    /// back to the main menu (the reference opens the system dialog there; that dialog belongs to a later
    /// milestone).
    /// </summary>
    private bool HandleAdventureKey(in SDL.KeyboardEvent key, AdventureMapScene adventure)
    {
        // The View World window is modal and takes the whole keyboard: Escape closes it, everything
        // else is ignored, which is the entire key handling of the reference's loop
        // (view_world.cpp:723) plus its close hot key.
        if (adventure.IsViewWorldOpen)
        {
            if (key.Key == SDL.Keycode.Escape)
            {
                ZoomLevel? level = adventure.CloseViewWorld();

                GameLog.Info($"The View World window closed at zoom level {level}.");
            }

            return false;
        }

        // The options dialog is modal as well: Escape closes it and the map sees nothing else.
        if (adventure.IsAdventureOptionsOpen)
        {
            if (key.Key == SDL.Keycode.Escape)
            {
                adventure.CloseAdventureOptions();
                GameLog.Info("The adventure options dialog closed.");
            }

            return false;
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                ReturnToMenu();
                return false;

            // V opens the adventure options dialog, the door the reference paints into the bottom
            // border; the World button of that dialog opens the View World window, and --view-world
            // opens it directly for captures.
            case SDL.Keycode.V when !key.Repeat:
                if (adventure.OpenAdventureOptions())
                {
                    GameLog.Info("Opened the adventure options dialog.");
                }

                return false;

            case SDL.Keycode.E when !key.Repeat:
                adventure.EndTurn();
                return false;

            case SDL.Keycode.Tab or SDL.Keycode.H when !key.Repeat:
                adventure.NextHero();
                return false;

            case SDL.Keycode.Left:
                adventure.ScrollByTiles(-1, 0);
                return false;

            case SDL.Keycode.Right:
                adventure.ScrollByTiles(1, 0);
                return false;

            case SDL.Keycode.Up:
                adventure.ScrollByTiles(0, -1);
                return false;

            case SDL.Keycode.Down:
                adventure.ScrollByTiles(0, 1);
                return false;

            case SDL.Keycode.Home:
                adventure.ScrollToStart();
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Acts on the answer of the adventure options dialog and closes it, the way the reference returns
    /// from <c>Dialog::AdventureOptions()</c> and lets its caller open the window the answer names
    /// (<c>dialog_adventure.cpp:64-115</c>).
    /// </summary>
    /// <param name="adventure">Scene the dialog belongs to.</param>
    /// <param name="answer">Button that was pressed.</param>
    private static void ResolveAdventureOptions(AdventureMapScene adventure, DialogButton answer)
    {
        adventure.CloseAdventureOptions();

        switch (answer)
        {
            case DialogButton.World:
                if (adventure.OpenViewWorld(ViewWorldMode.ViewAll))
                {
                    GameLog.Info("Opened the View World window from the adventure options.");
                }

                break;

            case DialogButton.Cancel:
                break;

            default:
                // Puzzle, information and dig need the puzzle window, the puzzle map and the
                // excavation rules, none of which the port has yet.
                GameLog.Info($"The adventure options answer '{answer}' has no window in the port yet.");
                break;
        }
    }

    /// <summary>
    /// Handles the keys of the editor: the arrow keys scroll by one tile, Home returns to the top left
    /// corner, G toggles the grid, 1 to 9 pick a ground for the brush, Ctrl+Z and Ctrl+Y walk the undo
    /// history and Escape closes the editor. There is no menu to return to when the editor was started
    /// with <c>--scene editor</c>, so Escape then leaves the client.
    /// </summary>
    private bool HandleEditorKey(in SDL.KeyboardEvent key, EditorScene editor)
    {
        // The information box is modal, like the popup the reference shows on a right click: the keys
        // that step the data and the ones that close it are taken, and a key meant for the box cannot
        // paint or undo behind it.
        if (editor.IsObjectInfoOpen)
        {
            switch (key.Key)
            {
                case SDL.Keycode.I or SDL.Keycode.Escape when !key.Repeat:
                    editor.HideObjectInfo();
                    GameLog.Info("Editor info: closed.");
                    break;

                case SDL.Keycode.LeftBracket when !key.Repeat:
                case SDL.Keycode.RightBracket when !key.Repeat:
                    bool stepped = editor.AdjustObjectData(key.Key == SDL.Keycode.LeftBracket ? -1 : 1);

                    GameLog.Info(
                        $"Editor info: {(stepped ? "data changed" : "data unchanged")}, {editor.ObjectInfoSummary}.");

                    break;
            }

            return false;
        }

        if ((key.Mod & SDL.Keymod.Ctrl) != 0)
        {
            switch (key.Key)
            {
                case SDL.Keycode.Z when !key.Repeat:
                    GameLog.Info(editor.Undo() ? "Editor: undo." : "Editor: nothing to undo.");
                    return false;

                case SDL.Keycode.Y when !key.Repeat:
                    GameLog.Info(editor.Redo() ? "Editor: redo." : "Editor: nothing to redo.");
                    return false;

                case SDL.Keycode.S when !key.Repeat:
                    if (editor.Save(out string? saveError))
                    {
                        GameLog.Info($"Editor: saved '{editor.MapName}' to '{editor.Edits.SavePath}'.");
                    }
                    else
                    {
                        GameLog.Warn($"Editor: could not save '{editor.MapName}': {saveError}");
                    }

                    return false;

                default:
                    return false;
            }
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                CloseEditor();
                return _menu is null;

            case SDL.Keycode.Alpha1 or SDL.Keycode.Alpha2 or SDL.Keycode.Alpha3
                or SDL.Keycode.Alpha4 or SDL.Keycode.Alpha5 or SDL.Keycode.Alpha6
                or SDL.Keycode.Alpha7 or SDL.Keycode.Alpha8 or SDL.Keycode.Alpha9 when !key.Repeat:
                // The grounds are numbered 1 (water) to 9 (beach), the same order as the enum.
                editor.SetBrushByNumber((int)key.Key - (int)SDL.Keycode.Alpha1 + 1);
                return false;

            case SDL.Keycode.G when !key.Repeat:
                editor.ToggleGrid();
                return false;

            case SDL.Keycode.T when !key.Repeat:
                editor.SetTool(EditorTool.Terrain);
                return false;

            case SDL.Keycode.O when !key.Repeat:
                GameLog.Info($"Editor object: {editor.CycleObject(1).GetSummary()}.");
                return false;

            case SDL.Keycode.X when !key.Repeat:
                editor.SetTool(EditorTool.Erase);
                return false;

            case SDL.Keycode.I when !key.Repeat:
                if (editor.IsObjectInfoOpen)
                {
                    editor.HideObjectInfo();
                    GameLog.Info("Editor info: closed.");
                }
                else if (editor.ToggleObjectInfo())
                {
                    GameLog.Info($"Editor info: {editor.ObjectInfoSummary}.");
                }
                else
                {
                    GameLog.Info("Editor info: the pointer is not over the map.");
                }

                return false;

            case SDL.Keycode.Left:
                editor.ScrollByTiles(-1, 0);
                return false;

            case SDL.Keycode.Right:
                editor.ScrollByTiles(1, 0);
                return false;

            case SDL.Keycode.Up:
                editor.ScrollByTiles(0, -1);
                return false;

            case SDL.Keycode.Down:
                editor.ScrollByTiles(0, 1);
                return false;

            case SDL.Keycode.Home:
                editor.ScrollToStart();
                return false;

            default:
                return false;
        }
    }

    /// <summary>Closes the editor and returns to the menu, or to the diagnostics screen without one.</summary>
    private void CloseEditor()
    {
        if (_editor is null)
        {
            return;
        }

        GameLog.Info($"Closing the editor of '{_editor.MapName}'.");
        _editor = null;
        _scene = _menu ?? (IScene?)_diagnostics;
    }

    /// <summary>
    /// Opens the editor main menu, the <c>Editor::menuMain()</c> window of
    /// <c>editor_mainmenu.cpp</c>: the editor sign of the game main menu lands here, and the map is
    /// chosen inside the editor's own screen.
    /// </summary>
    private void OpenEditorMenu()
    {
        _editorMenu ??= EditorMainMenuScene.TryCreate(_frame, _assets, _fonts);

        if (_editorMenu is null)
        {
            // A menu without its artwork is a blank screen; the reference always ships the archive, so
            // this fallback only keeps a broken installation usable.
            GameLog.Warn("The editor main menu could not be created; opening the first map instead.");
            OpenEditor(null);

            if (_editor is { } fallback)
            {
                _scene = fallback;
            }

            return;
        }

        if (_options.Cursor is { } cursor)
        {
            _editorMenu.SetCursor(cursor);
        }

        _scene = _editorMenu;
        GameLog.Info("Opened the editor main menu.");
    }

    /// <summary>Leaves the editor main menu for the game main menu.</summary>
    private void LeaveEditorMenu()
    {
        _editorMenu = null;
        _scene = _menu ?? (IScene?)_diagnostics;
        GameLog.Info("Left the editor main menu.");
    }

    /// <summary>
    /// Acts on a request of the editor main menu, the <c>editNewMapFromScratch()</c>,
    /// <c>editNewRandomMap()</c> and <c>Editor::menuLoadMap()</c> branches of the reference.
    /// </summary>
    /// <param name="request">Action the user asked for and, for the map creation actions, the size.</param>
    private void HandleEditorMenuRequest(EditorMenuRequest request)
    {
        switch (request.Action)
        {
            case EditorMenuAction.MainMenu:
                LeaveEditorMenu();
                return;

            case EditorMenuAction.LoadMap:
                // Editor::menuLoadMap() shows Dialog::SelectScenario() with every map file and opens
                // the chosen one; the scenario list is not wired into the editor yet, so the first
                // map of the catalog is opened - the same map the game main menu starts on.
                GameLog.Info("The editor map list is not wired yet; opening the first map of the catalog.");
                _editorMenu = null;
                OpenEditor(null);

                if (_editor is { } loaded)
                {
                    _scene = loaded;
                }

                return;

            default:
                break;
        }

        int edgeLength = MapSizes.EdgeLength(request.MapSize);

        if (request.Action == EditorMenuAction.NewRandomMap)
        {
            // The generator itself (Maps::updateRandomMapConfiguration() and generateRandomMap()) is
            // not ported yet; a blank map of the requested size keeps the editor usable meanwhile.
            GameLog.Warn(
                $"The random map generator is not ported yet; creating a blank {edgeLength}x{edgeLength} map.");
        }

        EditorScene? scene = EditorScene.CreateNew(_frame, _assets, _fonts, edgeLength, out string? error);

        if (scene is null)
        {
            GameLog.Warn($"A new {edgeLength}x{edgeLength} map cannot be created: {error}");
            return;
        }

        _editorMenu = null;
        _editor = scene;

        if (_options.Cursor is { } cursor)
        {
            scene.SetCursor(cursor);
        }

        _scene = scene;
        GameLog.Info($"Editor main menu: new {MapSizes.Describe(request.MapSize)} map ('{scene.MapName}').");
    }

    /// <summary>
    /// Opens the editor: on a new empty map when <c>--editor-new</c> asked for one, otherwise on
    /// <paramref name="filePath"/>, or on the first map of the catalog when no file is given: an editor
    /// without a map would be an empty screen, and the first map is the one the scenario list shows
    /// first.
    /// </summary>
    private void OpenEditor(string? filePath)
    {
        EditorScene? scene;

        if (_options.EditorNewMapSize is { } size)
        {
            scene = EditorScene.CreateNew(_frame, _assets, _fonts, size, out string? error);
            if (scene is null)
            {
                GameLog.Warn($"A new {size}x{size} map cannot be created: {error}");
                return;
            }
        }
        else
        {
            scene = OpenEditorMap(filePath);
            if (scene is null)
            {
                return;
            }
        }

        // The synthetic pointer of a screenshot has to reach the editor as well, or the highlighted
        // tile and the hover text of the status bar would be missing from every baseline.
        if (_options.Cursor is { } cursor)
        {
            scene.SetCursor(cursor);
        }

        // --editor-paint is the scripted brush stroke: a screenshot cannot press a button, and the
        // point of the flag is to show that a painted tile reaches the frame.
        if (_options.EditorPaint is { } paint && scene.PaintArea(paint.Tile, paint.Width, paint.Height, paint.Ground))
        {
            GameLog.Info(
                $"Painted {paint.Width}x{paint.Height} tiles of '{paint.Ground}' at " +
                $"({paint.Tile.X}; {paint.Tile.Y}).");

            // The snapshot is taken from the pointer that the paint moved away from; put it back so
            // the outlined tile and the hover text keep describing the frame that is captured.
            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-object and --editor-place are the scripted object tool: the palette key O and a
        // click, which a screenshot cannot produce.
        if (_options.EditorObject is { } objectName)
        {
            if (scene.SelectObject(objectName))
            {
                GameLog.Info($"Editor object: {scene.Edits.ObjectBrush.GetSummary()}.");
            }
            else
            {
                GameLog.Warn(
                    $"The editor has no object called '{objectName}'; pass a name or 1..{Mp2ObjectPresets.Count}.");
            }
        }

        if (_options.EditorPlacements.Count > 0)
        {
            int placed = 0;

            foreach (Point placement in _options.EditorPlacements)
            {
                if (scene.PlaceObject(placement))
                {
                    ++placed;
                }
            }

            GameLog.Info($"Placed {placed} of {_options.EditorPlacements.Count} objects.");

            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-radar-click is the scripted radar click: the minimap only reacts to the pointer, so
        // a screenshot cannot show that a click on the panel moves the view.
        if (_options.EditorRadarClick is { } radar)
        {
            bool moved = scene.ClickRadar(radar.Start, radar.DragTo);

            GameLog.Info(
                $"Minimap {(radar.DragTo is null ? "click" : "drag")} at ({radar.Start.X}; {radar.Start.Y}) " +
                $"put the view at ({scene.View.Scroll.X}; {scene.View.Scroll.Y})" +
                $"{(moved ? "." : ", where it already was.")}");

            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-data writes the data word of one tile: the property of an object is a number in the
        // tile, and the information box below is the only way to see it, so the two belong together.
        if (_options.EditorData is { } data)
        {
            GameLog.Info(
                scene.SetObjectData(data.Tile, data.Value)
                    ? $"Wrote data {data.Value} into the object at ({data.Tile.X}; {data.Tile.Y})."
                    : $"Nothing at ({data.Tile.X}; {data.Tile.Y}) takes a data word.");

            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-info opens the information box on one tile: the box is drawn over the map and the
        // panel, so a screenshot of it shows what the I key reports.
        if (_options.EditorInfoTile is { } infoTile)
        {
            GameLog.Info(
                scene.ShowObjectInfo(infoTile)
                    ? $"Editor info: {scene.ObjectInfoSummary}."
                    : $"({infoTile.X}; {infoTile.Y}) is outside the map.");
        }

        // --editor-panel-click clicks the controls of the editor panel in order: the panel is drawn from
        // the artwork of the archive, so a screenshot alone cannot prove that its buttons respond, and
        // selecting an instrument before its picker needs more than one click.
        if (_options.EditorPanelClicks.Count > 0)
        {
            Rect panel = EditorScene.PanelBayOf(scene.FrameSize);

            foreach (Point panelPoint in _options.EditorPanelClicks)
            {
                EditorPanelHit hit = scene.ClickPanel(new Point(panel.X + panelPoint.X, panel.Y + panelPoint.Y));

                GameLog.Info(
                    $"Panel click at ({panelPoint.X}; {panelPoint.Y}) hit {hit.Kind}. " +
                    $"Tool {scene.Tool}, ground {scene.Brush}, size {scene.BrushSizeName}, object '{scene.ObjectName}'.");
            }
        }

        // --editor-save closes the loop that --editor-paint opens: the map is written back at once, so
        // a script can open, change and save a real map without a mouse.
        if (_options.EditorSavePath is { } savePath)
        {
            if (scene.SaveAs(savePath, out string? saveError))
            {
                GameLog.Info($"Saved '{scene.MapName}' to '{savePath}'.");
            }
            else
            {
                GameLog.Warn($"Could not save '{scene.MapName}' to '{savePath}': {saveError}");
            }
        }

        _editor = scene;
    }

    /// <summary>
    /// Opens the editor on an existing map file; <see langword="null"/> when there is nothing usable to
    /// open, with the reason in the log.
    /// </summary>
    private EditorScene? OpenEditorMap(string? filePath)
    {
        string? path = filePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            string? directory = _assets?.DataDirectory;
            path = directory is null ? null : MapCatalog.ReadAll([directory]).FirstOrDefault()?.FilePath;
        }

        if (path is null)
        {
            GameLog.Warn("The editor needs a map: pass --map <file.mp2>, --editor-new <size>, or install the map collection.");
            return null;
        }

        Mp2FileInfo mapInfo;
        try
        {
            mapInfo = Mp2FileInfo.Read(path);
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error($"The map file '{path}' could not be read", exception);
            return null;
        }

        EditorScene? scene = EditorScene.TryCreate(_frame, _assets, _fonts, mapInfo, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The map file '{path}' cannot be edited: {error}");
            return null;
        }

        return scene;
    }

    /// <summary>
    /// Starts the adventure map of a scenario the menu has just confirmed. The map or the terrain
    /// tiles may be unusable; the client then stays on the menu, which keeps its status bar, instead
    /// of showing a black screen.
    /// </summary>
    private void TryEnterAdventureMap()
    {
        if (_adventure is not null || _menu is null)
        {
            return;
        }

        if (_menu.TakeConfirmedScenario() is not { } map)
        {
            return;
        }

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, map, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The scenario '{map.Name}' cannot be started: {error}");
            return;
        }

        _adventure = scene;
        _scene = scene;
    }

    /// <summary>
    /// Opens the town screen a click on the map asked for, <c>Game::OpenCastleDialog</c>.
    /// </summary>
    private void TryOpenCastleFromAdventure()
    {
        if (_castle is not null || _adventure?.TakeCastleRequest() is not { } castle)
        {
            return;
        }

        if (_adventure.TryOpenCastle(castle) is not { } scene)
        {
            GameLog.Warn($"The town screen of '{castle.Name}' cannot be shown: its artwork is missing.");

            return;
        }

        _castle = scene;
        _scene = scene;

        GameLog.Info($"Opened the town screen of '{castle.Name}'.");
    }

    /// <summary>Goes back to the map when the town screen was left with EXIT or a close hot key.</summary>
    private void CloseCastleWhenDone()
    {
        if (_castle is not { IsClosed: true })
        {
            return;
        }

        _castle = null;
        _scene = _adventure ?? (IScene?)_menu ?? _diagnostics;
    }

    /// <summary>
    /// Opens one map file on the adventure map, bypassing the menu and the scenario list
    /// (<c>--map</c>). A file that cannot be read leaves the client where it was, which is what makes
    /// the flag safe to use in scripts.
    /// </summary>
    private void OpenMapFile(string filePath)
    {
        Mp2FileInfo mapInfo;
        try
        {
            mapInfo = Mp2FileInfo.Read(filePath);
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error($"The map file '{filePath}' could not be read", exception);
            return;
        }

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, mapInfo, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The map file '{filePath}' cannot be shown: {error}");
            return;
        }

        _adventure = scene;
        _scene = scene;
    }

    /// <summary>Leaves the adventure map and goes back to the main menu.</summary>
    private void ReturnToMenu()
    {
        if (_adventure is null)
        {
            return;
        }

        GameLog.Info($"Leaving the adventure map '{_adventure.MapName}'.");
        _castle = null;
        _adventure = null;
        _scene = _menu is not null ? _menu : _diagnostics;
    }

    /// <summary>Maps a window coordinate onto the 640x480 logical frame, or <see langword="null"/> outside it.</summary>
    private Point? ToLogical(float x, float y)
    {
        if (_presenter is null)
        {
            return null;
        }

        Point output = new((int)MathF.Round(x), (int)MathF.Round(y));

        return _presenter.Viewport.TryToLogical(output, out Point logical) ? logical : null;
    }

    private void Update(double elapsedSeconds)
    {
        _timer.Tick(elapsedSeconds);
        _scene?.Update(elapsedSeconds);

        if (_options.SmokeFrames is int maxFrames && _timer.FrameCount >= maxFrames)
        {
            GameLog.Info($"Smoke mode finished after {_timer.FrameCount} frames ({_timer.FramesPerSecond:0.0} FPS).");
            _running = false;
        }
    }

    private void Draw()
    {
        if (_presenter is null || _scene is null)
        {
            return;
        }

        DiagnosticsState state = new(
            _assets?.DataDirectory,
            _assets?.BaseArchive.Count ?? 0,
            _assets?.ExpansionArchive?.Count ?? 0,
            _text?.Name ?? "none",
            _fonts?.Description ?? "none",
            _presenter.OutputSize,
            _presenter.Viewport.Scale,
            _timer.FramesPerSecond,
            _timer.FrameCount);

        // One line per scene change: it tells whether a switch reached the renderer, which is the
        // difference between "the click did nothing" and "the new scene drew nothing" (ADR-042).
        if (_drawnScene != _scene)
        {
            _drawnScene = _scene;
            GameLog.Info($"Drawing the '{_scene.GetType().Name}'.");
        }

        _scene.DrawFrame(state);

        // The reference paints the readout over the finished frame and then draws that frame
        // (ui_tool.cpp:288 SystemInfoRenderer::preRender / postRender).
        _systemInfo?.Draw(_frame.Bounds, _frame.Pixels);

        _modal?.Draw(_frame);

        _presenter.Upload(_frame);

        _presenter.BeginLogicalFrame();
        _presenter.DrawFrameTexture();

        _text?.BeginFrame();
        _scene.DrawOverlay(state);

        CaptureScreenshot();

        _presenter.EndFrame();
    }

    /// <summary>
    /// Creates the screens of this milestone and picks the start screen. The main menu is the
    /// default; the diagnostics screen stays available for engine checks and as the fallback when
    /// the artwork or the fonts are missing.
    /// </summary>
    private void CreateScenes()
    {
        _diagnostics = new DiagnosticsScene(_frame, _text, _supportsCjk, _fonts);

        // The editor is a screen of its own, not a dialog of the menu: it starts without the menu so
        // that a screenshot of it is the editor and nothing else.
        if (_options.Scene == SceneKind.Editor)
        {
            OpenEditor(_options.MapFile);
            _scene = _editor ?? (IScene?)_diagnostics;

            GameLog.Info(_editor is null
                ? "Starting on the diagnostics screen (the editor could not open a map)."
                : $"Starting on the map editor ('{_editor.MapName}').");

            return;
        }

        if (_options.Scene == SceneKind.Menu)
        {
            _menu = MainMenuScene.TryCreate(_frame, _assets, _fonts, _options.Language);

            if (_menu is not null)
            {
                _menu.AnimationEnabled = !_options.FreezeAnimation;

                if (_options.OpenSettings)
                {
                    _menu.OpenSettings();
                }

                if (_options.OpenScenario || _options.ScenarioSelection is not null)
                {
                    if (_menu.OpenScenario() && _options.ScenarioSelection is { } selection)
                    {
                        _menu.Scenario?.Select(selection);
                    }
                }

                if (_options.Cursor is { } cursor)
                {
                    _menu.SetCursor(cursor);

                    // A screenshot of a tooltip needs the right button held: the message box of the
                    // original exists only while it is pressed.
                    if (_options.RightButton)
                    {
                        _menu.SetRightButton(true);
                    }
                }
            }
        }

        _scene = _menu is not null ? _menu : _diagnostics;

        if (_options.EnterMap && _menu is not null)
        {
            if (_menu.Scenario is null)
            {
                _menu.OpenScenario();
            }

            // Confirm what the list shows: the first map, or the one --scenario-select picked.
            _menu.ConfirmScenario();
            TryEnterAdventureMap();
        }

        if (!string.IsNullOrWhiteSpace(_options.MapFile))
        {
            OpenMapFile(_options.MapFile);
        }

        // The town screen normally opens by clicking one of the player's towns on the map; --enter-town
        // opens it here so its frame can be captured and smoke tested without a mouse.
        if (_options.EnterTown && _adventure is { } townMap)
        {
            if (townMap.TryEnterTown(_options.TownIndex) is { } townScene)
            {
                _castle = townScene;
                _scene = townScene;

                // --town-build adds a building a day-one town does not have yet, so the windows behind
                // those buildings can be reached without playing to them.
                if (_options.TownBuild is { } buildingName)
                {
                    if (Enum.TryParse(buildingName, ignoreCase: true, out BuildingType building))
                    {
                        townScene.Castle.AddBuildings(building);

                        GameLog.Info($"--town-build: added '{building}' to '{townScene.Castle.Name}'.");
                    }
                    else
                    {
                        GameLog.Warn($"--town-build: '{buildingName}' is not a building name.");
                    }
                }

                // --cursor also places the pointer on the town screen, so the status line of the hovered
                // area can be screenshotted the same way the map's hover is.
                if (_options.Cursor is { } start)
                {
                    townScene.SetCursor(start);
                }

                // The race is part of the message because a random town re-rolls it on every load, so smoke tests
                // that click fixed coordinates need to know which race they got.
                GameLog.Info(
                    $"Opened the town screen of '{townScene.Castle.Name}' ({townScene.Castle.Race}) from the command line.");

                // --town-window opens one of the windows above it, so their frames can be captured the
                // same way the screen's is.
                if (_options.TownWindow is { } windowName)
                {
                    if (windowName is "mageguild" or "mage-guild")
                    {
                        townScene.OpenMageGuild();
                    }
                    else if (windowName is "well")
                    {
                        townScene.OpenWell();
                    }
                    else if (windowName is "market" or "marketplace")
                    {
                        townScene.OpenMarketplace();
                    }
                    else if (windowName is "guild" or "thievesguild" or "thieves-guild")
                    {
                        townScene.OpenThievesGuild();
                    }
                    else if (windowName is "oracle")
                    {
                        townScene.OpenThievesGuild(oracle: true);
                    }
                    else if (windowName is "tavern")
                    {
                        townScene.OpenTavern();
                    }
                    else
                    {
                        GameLog.Warn($"--town-window: '{windowName}' is not a window the town screen opens yet.");
                    }
                }

                // --town-building goes through the click's own entry point, which is how the dwelling
                // recruitment window (and the windows of buildings still to come) is reached from a script.
                if (_options.TownBuilding is { } clickedName)
                {
                    if (Enum.TryParse(clickedName, ignoreCase: true, out BuildingType clicked))
                    {
                        townScene.OpenBuilding(clicked);
                    }
                    else
                    {
                        GameLog.Warn($"--town-building: '{clickedName}' is not a building name.");
                    }
                }

                // --town-hero opens the recruit hero dialog of one of the two heroes on offer, which
                // normally needs two clicks: the slot in the building list, then OKAY.
                if (_options.TownHero is { } heroIndex)
                {
                    if (!townScene.OpenRecruitHero(heroIndex))
                    {
                        GameLog.Warn($"--town-hero: '{townScene.Castle.Name}' has no hero {heroIndex} on offer.");
                    }
                }

                // The marketplace asks for the kingdom income only while the right button is held
                // (Dialog::ZERO); --right-button holds it so the popup can be captured.
                if (_options.RightButton && _options.Cursor is { } heldCursor)
                {
                    townScene.SetRightButton(true);
                    GameLog.Info($"The right button is held at {heldCursor.X},{heldCursor.Y}.");
                }
            }
            else
            {
                GameLog.Warn("--enter-town: the acting kingdom has no town whose screen can be shown.");
            }
        }

        // The window normally opens from the adventure options dialog; --view-world opens it here so its
        // frame can be captured and smoke tested.
        if (_options.ViewWorld && _adventure is { } viewWorld && viewWorld.OpenViewWorld(ViewWorldMode.ViewAll))
        {
            GameLog.Info("Opened the View World window from the command line.");
        }

        // The dialog normally opens from the map's View World door; --adventure-options opens it here so
        // its frame can be captured.
        if (_options.AdventureOptions && _adventure is { } optionsMap && optionsMap.OpenAdventureOptions())
        {
            GameLog.Info("Opened the adventure options dialog from the command line.");
        }

        // The popup lives only while the right button is held; --quick-info holds it on the tile under
        // --cursor so the frame can be captured.
        if (_options.QuickInfo && _adventure is { } infoMap && _options.Cursor is { } infoCursor)
        {
            infoMap.SetCursor(infoCursor);
            infoMap.SetRightButton(true);
            GameLog.Info(infoMap.QuickInfo is { } info
                ? $"Quick info of the tile under the cursor: '{info.Text}'."
                : "The quick info of the tile under the cursor is empty.");
        }

        GameLog.Info(_menu is null
            ? "Starting on the diagnostics screen."
            : $"Starting on the main menu (language '{_menu.Text.Culture}', " +
              $"{_menu.Languages.Count} available).");
    }

    private void CaptureScreenshot()
    {
        bool requestedByCommandLine = _screenshotFrame >= 0 && _timer.FrameCount >= _screenshotFrame;
        bool requestedByKey = _screenshotRequested;
        if (!requestedByCommandLine && !requestedByKey)
        {
            return;
        }

        _screenshotFrame = -1;
        _screenshotRequested = false;

        string path = requestedByKey
            ? $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png"
            : _options.ScreenshotPath!;

        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _presenter!.SaveLogicalFrame(path);
            GameLog.Info($"Screenshot saved to '{Path.GetFullPath(path)}'.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            GameLog.Error("Screenshot could not be written", exception);
            ExitCode = 1;
        }

        // --screenshot is a script, not a session: the shell that asked for the file has to get its
        // prompt back, so the client stops once the frame is written (or the write has failed). F12
        // keeps the window open, because a player takes one screenshot and carries on playing. The
        // flag decides, not the key, so that an F12 that beats the capture frame cannot leave a
        // scripted run waiting for a window to be closed by hand.
        if (_options.ScreenshotPath is not null)
        {
            GameLog.Info("Screenshot was requested on the command line, closing the client.");
            _running = false;
        }
    }

    private void LoadGameData()
    {
        try
        {
            _assets = AssetBundle.Open(_options.DataDirectory);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException or IOException or BinaryFormatException)
        {
            GameLog.Error("The original game data could not be loaded", exception);
            GameLog.Warn("Continuing with the fallback grayscale palette; pass --data <dir> to fix this.");
            ExitCode = 1;
            return;
        }

        // The ported screens read their frames through the process wide Assets.Current cache
        // (game_assets.cpp:65); the castle screen paints through it, so the host publishes the bundle
        // here, once, and keeps it for the life of the process.
        GameAssets.Current = _assets;

        LoadH2DResources();

        byte[]? paletteData = _assets.TryRead("KB.PAL");
        if (paletteData is null || paletteData.Length < Palette.RawPaletteSize)
        {
            GameLog.Warn("KB.PAL is missing or truncated; using the fallback grayscale palette.");

            // The tile renderer can still decode and blit the opaque terrain sprites without a palette; only the
            // alpha blended objects need one, and those fail loudly.
            MapSprites.Initialize(_assets, gamePalette: null);

            return;
        }

        _palette = Palette.FromGamePalette(paletteData);
        GameLog.Info($"Palette loaded from KB.PAL ({paletteData.Length} bytes, 6 bit VGA normalized).");

        // The map renderer of maps_tiles_render.cpp takes no bundle and no palette (game_assets.cpp / agg.cpp keep
        // them as globals there too), so the composition root installs them once, here, next to Assets.Current.
        MapSprites.Initialize(_assets, _palette);
    }

    /// <summary>
    /// Opens <c>files/data/resurrection.h2d</c>, the images fheroes2 adds on top of the original
    /// game data, <c>fheroes2::h2d::H2DInitializer</c>.
    /// </summary>
    /// <remarks>
    /// The reference treats the archive as mandatory and throws at start up without it. The port keeps
    /// going instead: a few screens (the editor button of the main menu, the icons of several buttons)
    /// then fall back to what they can build themselves, which is better than refusing to run.
    /// </remarks>
    private void LoadH2DResources()
    {
        string? dataDirectory = _assets?.DataDirectory;
        if (dataDirectory is null)
        {
            return;
        }

        string? path = H2DResources.FindArchive(dataDirectory);

        if (path is null)
        {
            H2DResources.Current = null;
            GameLog.Warn(
                $"'{H2DResources.FileName}' was not found next to '{dataDirectory}'; " +
                "the artwork it adds stays unavailable.");
            return;
        }

        try
        {
            _h2d = H2DResources.Open(path);
            H2DResources.Current = _h2d;
            GameLog.Info($"'{H2DResources.FileName}' loaded ({_h2d.Archive.Count} entries).");
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or BinaryFormatException or InvalidOperationException)
        {
            _h2d = null;
            H2DResources.Current = null;
            GameLog.Warn($"'{path}' is not usable ({exception.Message}); the added artwork stays unavailable.");
        }
    }

    /// <summary>
    /// Opens the audio device and starts the main menu theme. Everything is optional: a machine
    /// without audio, or a player without the separate soundtrack files, only logs a message.
    /// </summary>
    private void StartAudio()
    {
        if (_options.NoAudio)
        {
            GameLog.Info("Audio disabled by --no-audio.");
            return;
        }

        _audio = AudioPlayer.TryCreate(_assets);
        if (_audio is null)
        {
            GameLog.Warn("Continuing without music and sound effects.");
            return;
        }

        // The main menu theme is the only music track of M2; scenes from M3 on switch it.
        if (!_audio.PlayMusic(MusicTrack.MainMenu))
        {
            GameLog.Info("The main menu theme is not available; the menu stays silent.");
        }

        if (_options.PlaySound is string soundName && !_audio.PlaySound(soundName))
        {
            GameLog.Warn($"The sound effect '{soundName}' could not be played.");
        }
    }

    private void CreateTextRenderer()
    {
        if (_presenter is null)
        {
            return;
        }

        foreach (FontChoice candidate in SystemFontLocator.EnumerateCandidates())
        {
            try
            {
                _text = new SdlTextRenderer(_presenter.Renderer, candidate.Path);
                _supportsCjk = candidate.SupportsCjk;
                GameLog.Info($"Text backend ready: {_text.Name} (CJK: {(_supportsCjk ? "yes" : "no")}).");
                return;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException)
            {
                GameLog.Warn($"Font '{candidate.Path}' could not be used: {exception.Message}");
            }
        }

        GameLog.Warn("No usable TrueType font found; the diagnostics overlay stays disabled.");
    }
}
