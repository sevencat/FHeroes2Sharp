using System.Diagnostics;
using FHeroes2.Assets;
using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Video;
using FHeroes2.App.Audio;
using FHeroes2.App.Video;
using FHeroes2.App.Fonts;
using FHeroes2.App.Graphics;
using FHeroes2.App.Input;
using FHeroes2.App.Localization;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Core.Random;
using FHeroes2.Core.Timing;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Editor;
using FHeroes2.Game.H2D;
using FHeroes2.Game.Highscores;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;

// 两个 ZoomLevel 各有各的用处：这里是王国的视野缩放档位，Settings 的那个是界面缩放倍率。
using ZoomLevel = FHeroes2.Game.Kingdom.ZoomLevel;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;
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

    /// <summary>
    /// Window scale used when neither <c>--scale</c> nor a saved <c>videomode</c> asks for one; the pixel baselines
    /// run with <c>--no-config</c> and rely on this 2.
    /// </summary>
    private const int DefaultWindowScale = 2;

    /// <summary>
    /// How long the campaign rating waits before it appears over <c>WIN.SMK</c>, the <c>5000</c> milliseconds of
    /// <c>game_campaign.cpp:1269</c>.
    /// </summary>
    private const double CampaignSubtitleDelaySeconds = 5.0;

    /// <summary>
    /// Where the campaign rating is burnt into <c>WIN.SMK</c>, the <c>{ 475, 110 }</c> of
    /// <c>game_campaign.cpp:1269</c> — the black square the clip has just burned away.
    /// </summary>
    private static readonly Point CampaignSubtitlePosition = new(475, 110);

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

    /// <summary>
    /// The standard OKAY / CANCEL / YES / NO buttons, loaded once for the boxes the campaign flow puts up
    /// (<see cref="ShowCampaignYesNo"/>).
    /// </summary>
    private SystemButtonArtwork? _systemButtons;

    /// <summary>
    /// The fade-in being played over the frames, <c>fheroes2::fadeInDisplay()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// 参考的淡入阻塞跑完六帧、每帧自己 <c>Display::render()</c>；端口把呈现拆开：场景在
    /// <see cref="IScene.DrawFrame"/> 里把待播的淡入交出来，<see cref="Draw"/> 在推上屏之前播一帧。
    /// 播完就丢，一次请求只对应一轮六帧（约 100 毫秒，<see cref="DisplayFade.ScreenFadeTimeMs"/>）。
    /// </remarks>
    private DisplayFade? _fadeIn;

    /// <summary>
    /// Whether the Load Game file list is on screen, the one blocking window that is not an
    /// <see cref="IModalScreen"/> (<see cref="SelectFileDialog.Show"/> owns its own frame loop).
    /// </summary>
    /// <remarks>
    /// 主菜单开着读档窗口时，脚本点击不能再打回菜单：那会重新打开一次窗口，栈里套栈无限递归。
    /// <see cref="ReplayScriptedInput"/> 见到它就把点击推进 <see cref="LocalEvent"/>，和冒险地图一样。
    /// </remarks>
    private bool _loadDialogOpen;

    private MainMenuScene? _menu;
    private DiagnosticsScene? _diagnostics;
    private AdventureMapScene? _adventure;
    private BattleScene? _battle;
    private CastleScene? _castle;
    private EditorScene? _editor;
    private EditorMainMenuScene? _editorMenu;
    private SystemInfoRenderer? _systemInfo;
    private bool _supportsCjk;
    private bool _sdlInitialised;
    private bool _ttfInitialised;
    private bool _running;

    /// <summary>
    /// Whether <c>SDL_GetTouchDevices</c> found a touch screen, <c>System::isTouchInputAvailable()</c>; the
    /// greeting of the first run mentions the two-finger gesture only when it is set.
    /// </summary>
    private bool _touchInputAvailable;

    /// <summary>
    /// The configuration file of <c>fheroes2.cfg</c>, or <see langword="null"/> when there is none to use
    /// (<c>--no-config</c>, or no application data folder to put it in).
    /// </summary>
    private string? _configFile;

    private int _screenshotFrame = -1;
    private bool _screenshotRequested;
    private bool _disposed;

    /// <summary>
    /// The scripted left clicks of the run: the ones of <c>--left-click-at</c>, or the single replay of
    /// <c>--left-click</c> on <see cref="LeftClickFrame"/>.
    /// </summary>
    private readonly IReadOnlyList<ScriptedClick> _clicks;

    /// <summary>
    /// The scripted key presses of <c>--key-at</c>, in increasing frame order.
    /// </summary>
    private readonly IReadOnlyList<ScriptedKey> _keys;

    /// <summary>
    /// The key of a scripted press that went down into <see cref="FHeroes2.UI.Input.LocalEvent"/> and still
    /// needs its release on the next frame.
    /// </summary>
    private FHeroes2.UI.Input.Key? _pendingKeyRelease;

    /// <summary>
    /// The point of a scripted click whose press went into <see cref="FHeroes2.UI.Input.LocalEvent"/> and
    /// still needs its release on the next frame.
    /// </summary>
    private Point? _pendingClickRelease;

    /// <summary>
    /// The window a scripted click was pressed on; the matching release goes there on the next frame.
    /// </summary>
    private IModalScreen? _pendingModalClick;

    /// <summary>
    /// Whether <c>--sphinx-riddle</c> still has to open its window, which happens on the first loop frame
    /// rather than while the map is being set up: the window draws the scene behind itself and needs the renderer.
    /// </summary>
    private bool _pendingSphinxRiddle;

    /// <summary>
    /// Which window of the map's objects <c>--adventure-window</c> still has to open, for the same reason as
    /// <see cref="_pendingSphinxRiddle"/>: the window paints the map behind itself and needs the renderer.
    /// </summary>
    private string? _pendingAdventureWindow;

    /// <summary>
    /// The spell <c>--cast-spell</c> still has to cast, for the same reason as <see cref="_pendingSphinxRiddle"/>:
    /// the picker of the Dimension Door and the town list of the Town Portal paint the map behind themselves and
    /// run their own loops, so they need the renderer the loop installs on its first frame.
    /// </summary>
    private FHeroes2.Game.Spells.SpellType? _pendingSpellCast;

    /// <summary>
    /// The campaign <c>--campaign</c> still has to start, for the same reason as <see cref="_pendingSphinxRiddle"/>:
    /// the selection screen runs its own loop and paints the scene behind itself.
    /// </summary>
    private int? _pendingCampaign;

    /// <summary>
    /// True when the finished campaign scenario has been accounted for and the selection screen has to come back,
    /// the <c>SELECT_CAMPAIGN_SCENARIO</c> that <c>Game::CompleteCampaignScenario()</c> returns
    /// (<c>game_campaign.cpp:1281</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="_pendingCampaign"/> 同样是「等下一帧」：结算发生在冒险地图的 <c>Update</c> 里，那一帧还没画完，
    /// 选关屏要等主循环把地图换成主菜单之后再开。
    /// </remarks>
    private bool _pendingCampaignSelection;

    /// <summary>
    /// The high score table the loop still has to open, and the finished game to enter into it. It is a pending value
    /// for the same reason as <see cref="_pendingCampaign"/>: the screen runs its own loop and must not be opened from
    /// inside a scene's <c>Update</c>.
    /// </summary>
    private PendingHighScores? _pendingHighScores;

    /// <summary>Which table the next frame opens, and whether a finished game has to be entered into it.</summary>
    /// <param name="IsCampaign">The campaign table when <see langword="true"/>, the standard one otherwise.</param>
    /// <param name="Completion">The finished game, or <see langword="null"/> when the table is only being viewed.</param>
    private readonly record struct PendingHighScores(bool IsCampaign, HighScoreCompletion? Completion);

    /// <summary>
    /// Whether the frame being handled belongs to the main loop's own <c>HandleEvents</c>, see
    /// <see cref="OnFrameEnded"/>.
    /// </summary>
    private bool _mainLoopFrame;

    /// <summary>
    /// Wall clock of the frames the interface's own blocking loops produce, whose ticks nobody else counts.
    /// </summary>
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();

    /// <summary>Seconds of <see cref="_frameClock"/> at the last tick issued from <see cref="OnFrameEnded"/>.</summary>
    private double _lastFrameClockSeconds;

    public GameHost(CommandLineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _syntheticCursor = options.Cursor is not null;
        _clicks = options.ScriptedClicks.Count > 0
            ? options.ScriptedClicks
            : options.LeftClick && options.Cursor is { } clickPoint
                ? [new ScriptedClick(LeftClickFrame, clickPoint)]
                : [];
        _keys = options.ScriptedKeys;

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

        // 参考自己把手指事件翻译成鼠标（onTouchFingerEvent），所以关掉 SDL 的触摸合成，否则每次点按都会来两遍。
        SDL.SetHint(SDL.Hints.MouseTouchEvents, "0");
        SDL.SetHint(SDL.Hints.TouchMouseEvents, "0");

        if (!TTF.Init())
        {
            GameLog.Error($"TTF_Init failed: {SDL.GetError()}");
            ExitCode = 1;
            return;
        }

        _ttfInitialised = true;
        _touchInputAvailable = !_options.NoTouch && HasTouchInput();
        GameLog.Info($"{BuildInfo.DisplayName} starting (SDL {SDL.GetVersion()}, SDL_ttf {TTF.Version()})");

        if (_options.NoTouch)
        {
            GameLog.Info("Touch input disabled by --no-touch: the first-run greeting keeps the short layout.");
        }
        else if (_touchInputAvailable)
        {
            GameLog.Info("A touch screen was found: finger events drive the cursor and the two buttons.");
        }

        LoadGameData();
        LoadSettings();
        ApplySessionOptions();
        _fonts = GameFonts.TryLoad(_assets);

        try
        {
            _presenter = CreatePresenter();

            _frame = new ScreenBuffer(ScreenBuffer.DefaultSize, _palette);

            // The reference installs its engine on the event state once, from the application object;
            // the host does it as soon as the frame it presents exists.
            _le.Engine = new SdlEventEngine(PumpEvents, _ => _presenter?.Upload(_frame), _frame.Bounds,
                new SdlDisplayControl(_presenter));

            // 界面里的阻塞循环（次元门的选择方框、View World、音频设置）自己转帧，主循环轮不到它们：
            // 脚本输入与冒烟预算都得从这一层补，见 OnFrameEnded。
            _le.EndOfFrame = OnFrameEnded;

            // 光标主题是冒险地图的输入分类（点空地 = 走路、点怪物 = 交战），没有它地图上的左键什么都不做；
            // 指针图的绘制归窗口系统，这里只把位置与可见性接进来。
            CursorState.Current = new CursorState(new WindowCursorRenderer());

            if (_options.SystemInfo)
            {
                _systemInfo = new SystemInfoRenderer();
                GameLog.Info("System info overlay enabled: clock and FPS in the bottom left corner.");
            }

            // --seed has to run before anything is loaded: map loading draws hero army sizes and the
            // random objects from the thread generator (see Rand.SeedThread).
            if (_options.Seed is { } seed)
            {
                Rand.SeedThread(seed);
                GameLog.Info($"Random generator seeded with {seed}.");
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

            // 视频要等呈现器就绪才有画面，参考也是在主循环之前播这几个 logo（game_mainmenu.cpp:125-128）。
            PlayStartupVideos();

            Loop();

            // 参考在各修改点各自保存（设置窗、热键、雷达位置……），端口在退出时统一落一次，是所有修改点的超集。
            SaveSettings();
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
    /// Copyright logos the reference plays before the main menu, <c>Game::runMainGameLoop()</c>
    /// (<c>game_mainmenu.cpp:125-128</c>).
    /// </summary>
    private static readonly string[] StartupLogos = ["NWCLOGO.SMK", "CYLOGO.SMK", "H2XINTRO.SMK"];

    /// <summary>
    /// Plays the videos that come before the first frame of the menu: the one <c>--video</c> asked for, or the
    /// copyright logos of the reference.
    /// </summary>
    /// <remarks>
    /// 冒烟与截图是脚本，不是一局游戏：版权 logo 与要截的画面毫无关系，只会让一趟无人值守的运行多等几秒，
    /// 所以带 <c>--smoke-frames</c> / <c>--screenshot</c> 时一律跳过。<c>--video</c> 是显式要求，照播。
    /// </remarks>
    private void PlayStartupVideos()
    {
        if (_options.VideoFile is { } file)
        {
            PlayVideo(file, _options.VideoLoop);

            return;
        }

        if (_options.SmokeFrames is not null || _options.ScreenshotPath is not null)
        {
            return;
        }

        foreach (string logo in StartupLogos)
        {
            PlayVideo(logo);
        }
    }

    /// <summary>
    /// Plays the end-of-game clip of <c>GameOver::Result::checkGameOver()</c> (<c>game_over.cpp:544-559</c>):
    /// <c>LOSE.SMK</c> loops, <c>WIN.SMK</c> plays once and waits for a key.
    /// </summary>
    private void PlayGameOverVideos()
    {
        GameOverRequests requests = GameOverResult.Current.Requests;

        if (requests.LoseVideo)
        {
            GameLog.Info("Play the defeat movie.");
            PlayVideo("LOSE.SMK", loop: true);
        }

        if (requests.WinVideo)
        {
            GameLog.Info("Play the victory movie.");
            PlayVideo("WIN.SMK");
        }
    }

    /// <summary>
    /// Plays a Smacker video over the current scene, the port of <c>Video::ShowVideo()</c>: the clip is looked up in
    /// the <c>anim</c>, <c>anim2</c>, <c>heroes2/anim</c> and <c>data</c> folders of the game data, its soundtrack is
    /// mixed with the sound volume, and any key or mouse button stops it.
    /// </summary>
    /// <param name="fileName">File name such as <c>intro.smk</c>; the extension is added when missing.</param>
    /// <param name="loop">Whether the video starts over when it ends, the victory and defeat clips do.</param>
    /// <param name="waitForKey">
    /// Whether the last frame stays on screen until a key is pressed, the <c>PLAY_CUTSCENE_WAIT</c> of the campaign
    /// ending (<c>game_campaign.cpp:1272</c>).
    /// </param>
    /// <param name="subtitle">
    /// Text burnt over the video once it has been running for <see cref="CampaignSubtitleDelaySeconds"/>, the
    /// <c>Video::Subtitle</c> of the campaign rating (<c>game_campaign.cpp:1267-1269</c>).
    /// </param>
    /// <returns>Whether the clip was found and played.</returns>
    public bool PlayVideo(string fileName, bool loop = false, bool waitForKey = false, string? subtitle = null)
    {
        byte[]? data = FindVideo(fileName);

        if (data is null)
        {
            GameLog.Warn($"Video '{fileName}' is not in the game data.");
            return false;
        }

        SmkVideoSequence sequence;

        try
        {
            sequence = new SmkVideoSequence(data, _audio is not null);
        }
        catch (BinaryFormatException error)
        {
            GameLog.Warn($"Video '{fileName}' is corrupted: {error.Message}");

            return false;
        }

        GameLog.Info(
            $"Playing video '{fileName}' ({sequence.FrameCount} frame(s), {sequence.Width}x{sequence.Height}, " +
            $"{sequence.MicrosecondsPerFrame / 1000.0:0.0} ms per frame, {sequence.AudioChannels.Count} audio channel(s)" +
            $"{(loop ? ", looping" : string.Empty)}).");

        using (sequence)
        {
            RunModal(new VideoModalScreen(
                sequence,
                _audio,
                _audio is not null,
                loop,
                waitForKey,
                subtitle is null ? null : _fonts?.White,
                subtitle,
                CampaignSubtitleDelaySeconds,
                CampaignSubtitlePosition,
                VideoSubtitle.ReferenceWidth));
        }

        GameLog.Info($"Video '{fileName}' finished.");

        return true;
    }

    /// <summary>
    /// Finds a movie the way <c>Video::getVideoFilePath()</c> (<c>game_video.cpp:75-101</c>) does: the
    /// <c>anim</c>, <c>anim2</c>, <c>heroes2/anim</c> and <c>data</c> folders of every game data root, and the
    /// archives as the last resort.
    /// </summary>
    /// <remarks>
    /// 数据目录指的是 <c>Data</c>（聚合文件所在处），而 <c>anim</c> 在它的上一级：参考的根目录列表里两者都有，
    /// 端口于是把两级都当候选。
    /// </remarks>
    private byte[]? FindVideo(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string name = fileName.Trim();

        // A direct path is a debugging aid: the reference always looks the clip up by name.
        if (File.Exists(name))
        {
            return File.ReadAllBytes(name);
        }

        if (!name.EndsWith(".smk", StringComparison.OrdinalIgnoreCase))
        {
            name += ".smk";
        }

        string? dataDirectory = _assets?.DataDirectory;
        string? gameRoot = dataDirectory is null ? null : Path.GetDirectoryName(dataDirectory);

        string[] folders = ["anim", "anim2", Path.Combine("heroes2", "anim"), "data"];

        foreach (string folder in folders)
        {
            foreach (string? root in new[] { gameRoot, dataDirectory })
            {
                if (root is null)
                {
                    continue;
                }

                string path = Path.Combine(root, folder, name);

                if (File.Exists(path))
                {
                    GameLog.Debug($"Video found on disk: '{path}'.");

                    return File.ReadAllBytes(path);
                }
            }

            string entry = $"{folder.Replace(Path.DirectorySeparatorChar, '/')}/{name}";

            if (_assets?.TryRead(entry) is { } data)
            {
                GameLog.Debug($"Video found in the archives: '{entry}' ({data.Length} bytes).");

                return data;
            }
        }

        return null;
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
    /// 可以在事件处理里调用（覆盖层就是这样，从 <see cref="PumpEvents"/> 的调用栈里被唤起）：内层再取一次
    /// 事件是安全的，SDL 的事件队列是全局的，<see cref="_modal"/> 保证不会重复分派。
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

                // 窗口开着时输入只流向窗口，脚本回放也要在这一层跑一遍，否则窗口等不到点击。
                ReplayScriptedInput();

                double now = clock.Elapsed.TotalSeconds;

                // 与主循环同样的钳位理由：拖动窗口不该让动画跳帧。
                double elapsed = Math.Clamp(now - previous, 0.0, 0.5);
                previous = now;

                screen.Update(_frame, elapsed);

                // 窗口画的这一帧也算进 --smoke-frames：窗口若在等人（战斗的人类回合就是），预算花完也不能
                // 一直等下去，否则无人值守的冒烟会挂在这里。
                _timer.Tick(elapsed);
                SpendSmokeBudget();

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
            // 这一帧的脚本输入由 OnFrameEnded 在 HandleEvents 末尾回放；标出「这是主循环自己的帧」，
            // 它才会把计时让给下面的 Update。
            _mainLoopFrame = true;

            bool alive;
            try
            {
                alive = _le.HandleEvents(sleepAfterEventProcessing: false, allowExit: true);
            }
            finally
            {
                _mainLoopFrame = false;
            }

            if (!alive)
            {
                break;
            }

            // The menu does not know about scenes: a confirmed scenario surfaces here as soon as the
            // input batch that confirmed it has been handled.
            TryEnterAdventureMap();
            TryOpenCastleFromAdventure();
            CloseCastleWhenDone();
            CloseBattleWhenDone();

            // --sphinx-riddle 的窗口要等渲染器就位才开：它是阻塞式的，每帧自己重画场景，地图这才在窗后。
            if (_pendingSphinxRiddle)
            {
                _pendingSphinxRiddle = false;
                AskSphinxRiddle();
            }

            // --campaign 的选关屏同理：它是阻塞式模态，开在主循环的帧里。
            if (_pendingCampaign is { } campaignId)
            {
                _pendingCampaign = null;
                StartNewCampaign(campaignId);
            }

            // 战役一关打完之后，参考回到 SELECT_CAMPAIGN_SCENARIO，主循环接着开选关屏
            // （game_mainmenu.cpp:205-206 → 202-204）。这一屏同样是阻塞式模态，所以也排到帧里。
            if (_pendingCampaignSelection)
            {
                _pendingCampaignSelection = false;
                RunCampaignScenarioSelection(CampaignSaveData.Get());
            }

            // HIGH SCORES 的招牌同理：这一屏是阻塞式模态，排在帧里开，不能从事件回调里直接压栈。
            if (_pendingHighScores is { } highScores)
            {
                _pendingHighScores = null;
                ShowHighScores(highScores);
            }

            // --adventure-window 同理。
            if (_pendingAdventureWindow is { } window)
            {
                _pendingAdventureWindow = null;
                OpenAdventureWindow(window);
            }

            // --cast-spell 同理：法术读完到施放下去之间还有一个界面里的阻塞循环（次元门的选择方框）。
            if (_pendingSpellCast is { } spell)
            {
                _pendingSpellCast = null;
                CastScriptedSpell(spell);
            }

            double now = clock.Elapsed.TotalSeconds;

            // Clamp the step so that dragging the window does not teleport the animation.
            double elapsed = Math.Clamp(now - previous, 0.0, 0.5);
            previous = now;

            _lastFrameClockSeconds = _frameClock.Elapsed.TotalSeconds;

            Update(elapsed);
            _audio?.Update();
            Draw();
        }
    }

    /// <summary>
    /// The application's work at the end of a frame: replays the scripted input of the command line, and for the
    /// frames an interface loop of its own produces, counts them as well.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LocalEvent.HandleEvents"/> is the frame source of every blocking loop, and two of them live
    /// inside the interface: the Dimension Door picker (<c>interface_utilities.cpp</c>'s destination square) and
    /// the View World window. The main loop never runs during those, so the click of <c>--left-click-at</c> has to
    /// be replayed here — otherwise the loop never gets the click that ends it — and their frames have to be
    /// counted here: the picker has to draw one frame of its own before the click of the next can land on it, and
    /// <c>--screenshot-frame</c> can only name that frame if it counts.
    /// </para>
    /// <para>
    /// 窗口开着时帧归 <see cref="RunModal"/> 管：它自己就回放脚本并计时，这里什么都不做，否则同一次点击会被
    /// 送两遍。<c>--screenshot</c> 在命令行的截图里结束进程，这一点在内层循环里也照办——它关掉的是整个客户端。
    /// </para>
    /// </remarks>
    /// <returns><see langword="false"/> when the client has to stop, which also ends the caller's loop.</returns>
    private bool OnFrameEnded()
    {
        if (_modal is not null)
        {
            return _running;
        }

        // 读档窗口是从主循环的事件处理里开出来的（点标牌的那么一下就在 HandleEvents 之内），它的帧跑在这一层
        // 里面，_mainLoopFrame 仍是 true。不把它算成「界面自己的循环」，冒烟预算就永远扣不掉，脚本不给它第二下
        // 点击时窗口就再也退不出来。
        if (!_mainLoopFrame || _loadDialogOpen)
        {
            // 界面内的阻塞循环：没有 Update，帧号与预算都得在这里补，截图也只能在这里拍。
            double now = _frameClock.Elapsed.TotalSeconds;
            _timer.Tick(Math.Clamp(now - _lastFrameClockSeconds, 0.0, 0.5));
            _lastFrameClockSeconds = now;

            SpendSmokeBudget();
        }

        ReplayScriptedInput();

        if (_mainLoopFrame && !_loadDialogOpen)
        {
            return _running;
        }

        CaptureScreenshot(composedFrameOnly: true);

        return _running;
    }

    /// <summary>
    /// Casts the spell <c>--cast-spell</c> asked for, from the same entry the spell book leads to.
    /// </summary>
    /// <remarks>
    /// 书、法术与满法力都是补课：地图上的英雄未必带着次元门，法力也未必够，而这些都只是走到施法界面前的
    /// 门槛。过了门槛之后走的是与法术书上选完法术完全相同的一段，见 <see cref="AdventureMap.CastAdventureSpell"/>。
    /// </remarks>
    /// <param name="type">The spell to cast.</param>
    private void CastScriptedSpell(FHeroes2.Game.Spells.SpellType type)
    {
        if (_adventure is not { } map)
        {
            GameLog.Warn("--cast-spell: the adventure map is not running.");
            return;
        }

        if (InterfaceFocus.GetFocusHeroes() is not { } hero)
        {
            GameLog.Warn("--cast-spell: no hero is focused.");
            return;
        }

        FHeroes2.Game.Spells.Spell spell = new(type);

        if (!hero.HasSpellBook)
        {
            hero.SpellBookActivate();
        }

        if (!hero.HasSpell(spell))
        {
            hero.AppendSpellToBook(spell, ignoreWisdom: true);
        }

        if (!hero.HaveSpellPoints(spell) || !hero.HaveMovePoints(spell))
        {
            hero.SetSpellPoints(hero.GetMaxSpellPoints());
        }

        if (!hero.CanCastSpell(spell, World.Current))
        {
            GameLog.Warn($"--cast-spell: {type} cannot be cast here; the picker is opened anyway.");
        }

        int before = hero.MapIndex;
        uint points = hero.SpellPoints;

        GameLog.Info($"--cast-spell: casting {type} from tile {before} with {points} spell points.");
        map.CastAdventureSpell(spell);
        GameLog.Info(
            $"--cast-spell: {type} done, the hero is on tile {hero.MapIndex} with {hero.SpellPoints} spell points.");
    }

    /// <summary>
    /// Replays the input of the command line (<c>--cursor</c>, <c>--left-click-at</c>, <c>--key-at</c>) for the
    /// current frame into <see cref="LocalEvent"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// --cursor is the synthetic pointer of a screenshot: the real one must not steal the hover state, so the
    /// synthetic position is pushed into the event state after the real events. Scenes that read the pointer out
    /// of LocalEvent (the adventure map, the View World window) then hover exactly where the command line asked.
    /// </para>
    /// <para>
    /// <see cref="OnFrameEnded"/> 调它（主循环与界面内的阻塞循环都从这里过），<see cref="RunModal"/> 自己调：
    /// 窗口开着时输入只流向窗口，脚本回放也得走同一条路，否则窗口永远等不到那一下点击——战斗的人类回合、
    /// 战斗结果窗都是这种窗口。
    /// </para>
    /// </remarks>
    private void ReplayScriptedInput()
    {
        if (_syntheticCursor && _options.Cursor is { } syntheticCursor)
        {
            _le.OnMouseMotion(syntheticCursor);

            // --right-button holds the right button down. The menu and the town screen take that flag
            // directly, but the adventure map reads the event state: the quick info box of a tile only
            // exists while the button is pressed (Dialog::QuickInfo), and the scene drops it on the
            // frame the press is gone. Pushing the held press into LocalEvent is what keeps the box up
            // for --quick-info and for any other right-hold tooltip of the map.
            if (_options.RightButton && _adventure is not null)
            {
                _le.OnMouseButton(isPressed: true, MouseButtonType.Right, syntheticCursor);

                // 窗口开着时输入只流向窗口，和左键一样要在窗口自己身上按一次；地图上那两个窗口的右键提示
                // （市场的日收入框、盗贼公会的 EXIT 说明）正是「按住右键才在」，所以每帧都要补这一次。
                if (_modal is { } modal)
                {
                    modal.SetCursor(syntheticCursor);
                    modal.SetRightButton(down: true);
                }
            }
        }

        // A scripted click that went into LocalEvent releases on the frame after its press: one
        // physical click spans two frames, and the button row and the list of the first-run chooser
        // both fire on the release, not on the press.
        if (_pendingClickRelease is { } releasePoint)
        {
            _le.OnMouseButton(isPressed: false, MouseButtonType.Left, releasePoint);
            _pendingClickRelease = null;
        }

        // The press of a scripted click that went to an open window releases on this frame, the same way a
        // physical click spans two frames.
        if (_pendingModalClick is { } clickedModal)
        {
            _pendingModalClick = null;
            clickedModal.SetLeftButton(down: false);
        }

        // A scripted key press releases on the frame after its press, for the same reason: a real press
        // spans two frames, and the one-shot key state of LocalEvent is what the battle hot keys read.
        if (_pendingKeyRelease is { } releaseKey)
        {
            _le.OnKeyboard(releaseKey, FHeroes2.UI.Input.KeyModifier.None,
                FHeroes2.UI.Input.KeyboardEventState.Up);
            _pendingKeyRelease = null;
        }

        // --left-click replays one click at frame 10 on --cursor; --left-click-at replays a whole script
        // of them, because the first run needs three in one run (the language OKAY, the greeting OKAY,
        // then a row of the resolution chooser). The clicks are replayed from inside the loop, i.e.
        // after the event batch and before the scene update, which is the frame order a player
        // produces. Doing it in CreateScenes would skip the first frames of the new scene.
        foreach (ScriptedClick click in _clicks)
        {
            if (_timer.FrameCount != click.Frame)
            {
                continue;
            }

            // 窗口开着时输入只流向窗口（见 RunModal 的说明），脚本点击也一样：按下落这一帧、抬下落下一帧
            // （同一次点击的两个状态不能落在同一帧，不然按住的样子永远留着）。窗口的点击不走 LocalEvent，
            // 而是和真实事件一样直接交给窗口，所以这里自带坐标地调它的两个入口。
            if (_modal is { } modal)
            {
                modal.SetCursor(click.Point);
                modal.SetLeftButton(down: true);
                _pendingModalClick = modal;
            }

            // 读档窗口也是阻塞的（它自己压帧读 LocalEvent），而且它开在主菜单之上：点击再交给菜单就会重新
            // 打开一次窗口，栈里套栈一直递归下去。所以这条分支排在菜单之前，点击像冒险地图那样推进 LocalEvent，
            // 窗口的列表与按钮才收得到。
            else if (_loadDialogOpen)
            {
                _le.OnMouseMotion(click.Point);
                _le.OnMouseButton(isPressed: true, MouseButtonType.Left, click.Point);
                _pendingClickRelease = click.Point;
            }

            // A town screen swallows the replay: it is modal, and its EXIT is the one click worth
            // replaying there (the arrows need a second click in the same run to be visible).
            else if (_castle is { } townScreen)
            {
                townScreen.SetCursor(click.Point);
                townScreen.SetLeftButton(true);
                townScreen.SetLeftButton(false);
            }
            else if (ReferenceEquals(_scene, _adventure) && _adventure is not null)
            {
                // 冒险地图与战场一样从 LocalEvent 里读指针与点击，所以脚本点击要推进去；按下与抬起
                // 得落在同一帧（与真实点击一致），它按<b>点击</b>找瓦片：GameArea 先按光标位置取格，
                // 再看这次的按下与抬起是否都在这一格里（MouseClickLeft）。
                _le.OnMouseMotion(click.Point);
                _le.OnMouseButton(isPressed: true, MouseButtonType.Left, click.Point);
                _le.OnMouseButton(isPressed: false, MouseButtonType.Left, click.Point);
            }
            else if (_menu is not null && _battle is null)
            {
                _menu.SetCursor(click.Point);
                _menu.SetLeftButton(true);

                if (_menu.SetLeftButton(false) is { } clicked)
                {
                    HandleMenuAction(clicked);
                }
            }

            // The first-run resolution chooser is the one window that reads LocalEvent instead of the
            // scene's SetLeftButton API (its list drives the scrolling, the double click and the
            // right-click explanation from that state), so the click is pushed there as well. The
            // press goes in now and the release on the next frame: one physical click spans two
            // frames, and both the button row and the list fire on the release.
            if (_menu?.ResolutionPicker is not null)
            {
                _le.OnMouseButton(isPressed: true, MouseButtonType.Left, click.Point);
                _pendingClickRelease = click.Point;
            }

            // The battle screen reads the pointer and the click out of LocalEvent (its cells are hit
            // tested against the cell geometry), so a scripted click has to go in there as well.
            if (_battle is not null && _modal is null)
            {
                _le.OnMouseMotion(click.Point);
                _le.OnMouseButton(isPressed: true, MouseButtonType.Left, click.Point);
                _pendingClickRelease = click.Point;
            }
        }

        // --key-at is the keyboard twin of the script: the battle's R, S, C and Space are hot keys read
        // out of LocalEvent, so a smoke run can only reach the retreat and surrender dialogs this way.
        foreach (ScriptedKey key in _keys)
        {
            if (_timer.FrameCount != key.Frame)
            {
                continue;
            }

            _le.OnKeyboard(key.Key, FHeroes2.UI.Input.KeyModifier.None,
                FHeroes2.UI.Input.KeyboardEventState.Down);
            _pendingKeyRelease = key.Key;

            // 窗口开着时键盘只流向窗口（与点击同一个理由）：脚本键不进 SDL 的事件队列，所以在这里按
            // HandleModalKey 的规则再分派一次，输入框才收得到脚本打进去的字。
            if (_modal is { } modal)
            {
                DispatchModalKey(modal, key.Key);
            }
        }
    }

    /// <summary>
    /// Hands one key to the open window with the split of <see cref="HandleModalKey"/>.
    /// </summary>
    /// <remarks>
    /// 脚本回放的键没有 SDL <c>KeyboardEvent</c>，所以那套按 SDL <c>Keycode</c> 的分流在这里按游戏自己的
    /// <see cref="Key"/> 再做一遍：ESC 是取消、回车与空格是确认，其余键原样交给窗口。
    /// </remarks>
    /// <param name="modal">Window that owns the keyboard.</param>
    /// <param name="key">Key that was pressed.</param>
    private static void DispatchModalKey(IModalScreen modal, Key key)
    {
        switch (key)
        {
            case Key.Escape:
                modal.HotKey(ButtonHotKey.Cancel);

                break;

            case Key.Enter:
            case Key.Space:
                modal.HotKey(ButtonHotKey.Okay);

                break;

            default:
                modal.PressKey(key, KeyModifier.None);

                break;
        }
    }

    /// <summary>
    /// Hands a cursor position to the scene that owns the pointer, shared by the mouse and the touch screen.
    /// </summary>
    /// <param name="point">Cursor position in logical pixels, or <see langword="null"/> to ignore the event.</param>
    /// <remarks>
    /// A modal window owns the pointer while it is open: the scene below keeps its state (it is frozen) and only
    /// the window reacts. With <c>--cursor</c> the synthetic pointer is what a screenshot should show, so the real
    /// one must not steal the hover state.
    /// </remarks>
    private void MoveSceneCursor(Point? point)
    {
        if (point is not { } logical)
        {
            return;
        }

        if (_modal is { } modal)
        {
            if (!_syntheticCursor)
            {
                modal.SetCursor(logical);
            }

            return;
        }

        if (_syntheticCursor)
        {
            return;
        }

        if (_castle is { } townScreen)
        {
            townScreen.SetCursor(logical);
        }
        else if (_battle is not null)
        {
            // The battle ignores the pointer until its action loop exists.
        }
        else if (_editorMenu is { } editorMenu)
        {
            editorMenu.SetCursor(logical);
        }
        else if (_editor is { } editing)
        {
            editing.SetCursor(logical);
        }
        else if (_adventure is { } moving)
        {
            // The View World window drags its map with the same left button, so the pointer goes to the window
            // while it is open; the adventure map itself reads the pointer out of LocalEvent.
            if (moving.ViewWorld is { } viewWorld)
            {
                viewWorld.DragTo(logical);
            }
        }
        else
        {
            _menu?.SetCursor(logical);
        }
    }

    /// <summary>
    /// Turns one finger event into <see cref="LocalEvent"/> state and the scene calls a mouse event would make,
    /// the reference's <c>onTouchFingerEvent</c> plus the dispatch its menus get for free.
    /// </summary>
    /// <param name="finger">Finger event taken from the SDL queue.</param>
    /// <remarks>
    /// SDL reports the finger position normalized to the window, so it is scaled to the output pixels before
    /// <see cref="ToCursor"/> applies the logical viewport. SDL's own touch-to-mouse synthesis is turned off when
    /// the client starts, otherwise every finger would arrive twice.
    /// </remarks>
    private bool EmulateTouch(in SDL.TouchFingerEvent finger)
    {
        Size output = _presenter!.OutputSize;
        Point cursor = ToCursor(finger.X * output.Width, finger.Y * output.Height);

        LocalEvent.TouchFinger type = finger.Type switch
        {
            SDL.EventType.FingerDown => LocalEvent.TouchFinger.Down,
            SDL.EventType.FingerUp => LocalEvent.TouchFinger.Up,
            SDL.EventType.FingerMotion => LocalEvent.TouchFinger.Motion,
            _ => LocalEvent.TouchFinger.Unknown,
        };

        LocalEvent.TouchEmulation emulated = _le.OnTouchFinger(type, finger.TouchID, finger.FingerID, cursor);

        switch (emulated)
        {
            case LocalEvent.TouchEmulation.Motion:
                MoveSceneCursor(cursor);
                break;

            case LocalEvent.TouchEmulation.LeftPressed:
            case LocalEvent.TouchEmulation.LeftReleased:
            case LocalEvent.TouchEmulation.RightPressed:
            case LocalEvent.TouchEmulation.RightReleased:
                MoveSceneCursor(cursor);
                return DispatchButton(
                    emulated is LocalEvent.TouchEmulation.LeftPressed or LocalEvent.TouchEmulation.RightPressed,
                    emulated is LocalEvent.TouchEmulation.RightPressed or LocalEvent.TouchEmulation.RightReleased
                        ? RightMouseButton
                        : LeftMouseButton,
                    finger.X * output.Width,
                    finger.Y * output.Height);

            default:
                break;
        }

        return false;
    }

    /// <summary>
    /// Routes a button press or release to the scene that owns it; returns <see langword="true"/> when the client
    /// must stop. The touch screen goes through here with the button <see cref="LocalEvent"/> emulated.
    /// </summary>
    private bool DispatchButton(bool down, int buttonId, float x, float y)
    {
        SDL.MouseButtonEvent button = new()
        {
            Type = down ? SDL.EventType.MouseButtonDown : SDL.EventType.MouseButtonUp,
            Button = (byte)buttonId,
            Down = down,
            X = x,
            Y = y,
        };

        if (_modal is { } clickedModal)
        {
            HandleModalButton(clickedModal, button);
            return false;
        }

        return HandleMouseButton(button);
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
                    MoveSceneCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                    break;

                case SDL.EventType.FingerDown:
                case SDL.EventType.FingerUp:
                case SDL.EventType.FingerMotion:
                    if (EmulateTouch(evt.TFinger))
                    {
                        return false;
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

    /// <summary>
    /// Whether SDL knows at least one touch device, <c>System::isTouchInputAvailable()</c>.
    /// </summary>
    /// <remarks>
    /// SDL only learns about a device once it is used, so the answer can be <see langword="false"/> on the first
    /// run on a machine that does have a touch screen; the greeting then simply omits the touch paragraph, the
    /// same way the reference does.
    /// </remarks>
    private static bool HasTouchInput()
    {
        // The binding hands back a managed copy, so there is nothing to free here.
        ulong[]? devices = SDL.GetTouchDevices(out _);

        return devices is { Length: > 0 };
    }

    /// <summary>Handles a key press; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleKeyDown(in SDL.KeyboardEvent key)
    {
        // Engine wide keys stay available on every screen, including the adventure map.
        switch (key.Key)
        {
            case SDL.Keycode.F11 when !key.Repeat:
                if (_presenter is { } presenter)
                {
                    presenter.ToggleFullscreen();

                    // 参考的 Display::toggleFullScreen() 由引擎的按键分发调用，配置里的 fullscreen 一直是现状
                    // （settings.cpp:427 写的就是它），端口在同一个地方跟上，否则文件里永远停在开窗时那一档。
                    Settings.Get().SetFullScreen(presenter.IsFullscreen);
                }

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

        if (_battle is { } battleScene)
        {
            // The battle takes no keyboard input yet: Escape gives the screen back to the map, which is
            // the port's stand-in for the reference's exit button until the action loop lands.
            if (!key.Repeat && key.Key == SDL.Keycode.Escape)
            {
                battleScene.Close();
            }

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

            case SDL.Keycode.Up when _menu?.LanguagePicker is not null:
                _menu.MoveLanguageSelection(-1);
                return false;

            case SDL.Keycode.Down when _menu?.LanguagePicker is not null:
                _menu.MoveLanguageSelection(1);
                return false;

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
    /// <remarks>
    /// ESC/回车/空格折成按钮热键；其余键原样交给窗口，输入框才收得到字（<see cref="IModalScreen.PressKey"/>）。
    /// 修饰键取 <see cref="LocalEvent.CurrentKeyModifiers"/>，它已由本帧的事件处理填好，SDL 的
    /// <c>key.mod</c> 在脚本回放里拿不到。
    /// </remarks>
    private void HandleModalKey(IModalScreen modal, in SDL.KeyboardEvent key)
    {
        if (key.Repeat)
        {
            return;
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                modal.HotKey(ButtonHotKey.Cancel);

                return;

            case SDL.Keycode.Return:
            case SDL.Keycode.KpEnter:
            case SDL.Keycode.Space:
                modal.HotKey(ButtonHotKey.Okay);

                return;

            default:
                modal.PressKey(SdlKeyMap.ToKey((int)key.Key), _le.CurrentKeyModifiers);

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

        if (_battle is not null)
        {
            // The battle has no mouse handling yet: the click is dropped rather than reaching the map below.
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
            // pointer: both arrive through LocalEvent, which ProcessTurnFrame() dispatches to the three
            // cursor handlers (interface_events.cpp). The host has nothing left to hand over.
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
        // The LOAD GAME sign asks for a save file and reopens it, Game::LoadGame() (game_loadgame.cpp:91-101) with
        // Game::LoadStandard() behind it. The Standard / Campaign / Multi-Player sub-menu of the reference is not
        // drawn yet: the standard list is the only one that has saves to show.
        if (action == MainMenuAction.LoadGame)
        {
            OpenLoadGame();
            return false;
        }

        // The editor sign opens the editor's own main menu, the window `Editor::menuMain()` of
        // editor_mainmenu.cpp shows; the map is picked there, not by the game main menu.
        if (action == MainMenuAction.Editor)
        {
            OpenEditorMenu();
            return false;
        }

        // The HIGH SCORES sign opens the table, `Game::DisplayHighScores( false )` of the reference
        // (game_mainmenu.cpp:203-205). The screen is a modal of its own, so the loop opens it on the next frame.
        if (action == MainMenuAction.HighScores)
        {
            _pendingHighScores = new PendingHighScores(IsCampaign: false, Completion: null);
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
    /// Opens the high score table, <c>Game::DisplayHighScores()</c> (<c>game_highscores.cpp:462-476</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只读一遍文件（<see cref="HighScoreBoard.Load"/> 自带「只读一次」），把两张榜交给
    /// <see cref="HighScoresScreen"/>；换榜、登记与存盘都在那一屏里，宿主只等它关掉。
    /// </para>
    /// <para>
    /// <see cref="HighScoresScreen"/> 是模态：<see cref="RunModal"/> 自己取事件、自己重画。带着一局的成绩来时
    /// （<c>Completion</c> 不为 <see langword="null"/>）先问名字、登记新行，这正是参考
    /// <c>openHighScores()</c> 在 <c>isAfterGameCompletion</c> 分支里做的事（<c>:286-347</c>）。
    /// </para>
    /// </remarks>
    /// <param name="request">Which table to open, and the finished game to enter into it when there is one.</param>
    private void ShowHighScores(PendingHighScores request)
    {
        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            GameLog.Warn("The high score screen needs the game archives and the bitmap fonts; the sign does nothing.");

            return;
        }

        HighScoreBoard board = new();
        var screenSize = new Size(_frame.Pixels.Width, _frame.Pixels.Height);

        HighScoresScreen? screen = HighScoresScreen.TryCreate(
            assets,
            fonts,
            screenSize,
            board,
            request.IsCampaign,
            out string? error,
            campaignAvailable: IsSuccessionWarsCampaignPresent(assets),
            completion: request.Completion,
            text: _menu?.Text);

        if (screen is null)
        {
            GameLog.Warn($"The high score screen cannot be shown: {error}");

            return;
        }

        RunModal(screen);

        GameLog.Info(
            $"The high score screen closed after the {(request.IsCampaign ? "campaign" : "standard")} table, "
            + $"back to the main menu.");
    }

    /// <summary>
    /// Answers the map's won standard scenario: the map is left — which plays <c>WIN.SMK</c>, the order of
    /// <c>Game::DisplayHighScores()</c> — and the table opens on the next frame.
    /// </summary>
    /// <param name="completion">The score of the finished map.</param>
    private void ShowHighScoresAfterWin(HighScoreCompletion completion)
    {
        GameLog.Info(
            $"The map '{completion.ScenarioName}' is finished; the main menu comes back and the high score table follows.");

        _pendingHighScores = new PendingHighScores(IsCampaign: false, Completion: completion);
        ReturnToMenu();
    }

    /// <summary>
    /// Whether both original campaigns have their map files, <c>Game::isSuccessionWarsCampaignPresent()</c>; the switch
    /// button of the high score screen is disabled without them (<c>game_highscores.cpp:387-390</c>).
    /// </summary>
    /// <param name="assets">The archives, for the data directory the maps are searched in.</param>
    /// <returns><see langword="true"/> when both campaigns are playable.</returns>
    private static bool IsSuccessionWarsCampaignPresent(AssetBundle assets) =>
        assets.DataDirectory is { } directory
        && CampaignData.GetCampaignData(CampaignId.Roland).IsAllCampaignMapsPresent([directory])
        && CampaignData.GetCampaignData(CampaignId.Archibald).IsAllCampaignMapsPresent([directory]);

    /// <summary>
    /// Handles the keys of the two windows the scene still owns, View World and the adventure options dialog.
    /// </summary>
    /// <remarks>
    /// 其余热键都归屏幕：<c>PumpEvents()</c> 先把每个键写进 <see cref="LocalEvent"/>，
    /// <c>Interface::AdventureMap::ProcessHotKeys()</c> 在场景的 <c>Update</c> 里读它（Esc、E、Tab/H、方向键、
    /// Home、V 等，见 <c>game_startgame.cpp</c> 与 <c>interface_events.cpp</c>）。这两个浮层仍是场景自己压帧，
    /// 参考里它们的循环挂在地图循环之上，所以键盘在这里截住。
    /// </remarks>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="adventure">Scene the key belongs to.</param>
    /// <returns><see langword="true"/> when the client must stop, which no map key ever does.</returns>
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

        return false;
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

        // 从主菜单点进来的这一路就是参考的 Game::NewStandard()：玩法类型在开工前定成单图（game_newgame.cpp:149-157），
        // 存档的扩展名与头里的类型都跟着它走。
        Settings.Get().SetGameType(GameTypes.Standard);

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, map, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The scenario '{map.Name}' cannot be started: {error}");
            return;
        }

        ShowAdventureScene(scene);
    }

    /// <summary>
    /// Starts a campaign from its first scenario, <c>Game::NewSuccessionWarsCampaign()</c> and
    /// <c>Game::NewPriceOfLoyaltyCampaign()</c> up to the point where the scenario is chosen
    /// (<c>game_newgame.cpp:178-286</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在这之前还有两段选择屏：原版是 <c>CHOOSE.SMK</c> 里点 Roland 或 Archibald，资料片是
    /// <c>X_IVY</c> 上点四个战役之一。它们都是「影片/整屏 + 可点区域」，端口还没做，所以这里先由调用方给出战役号；
    /// 选择屏补上之后，这个参数就由它来。
    /// </para>
    /// <para>
    /// 存档对象是全局单例：开新战役先 <see cref="CampaignSaveData.Reset"/>，再把当前关卡设成第 0 关，
    /// 与参考的 <c>SaveData::Get().Reset()</c> 加 <c>setCurrentScenarioInfo</c> 一致。
    /// </para>
    /// </remarks>
    /// <param name="campaignId">The campaign to start, one of <see cref="CampaignId"/>.</param>
    private void StartNewCampaign(int campaignId)
    {
        if (_assets is null || _fonts is null)
        {
            GameLog.Warn("The campaign needs the game data and the fonts, neither of which is loaded.");

            return;
        }

        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.Reset();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(campaignId, 0));

        RunCampaignScenarioSelection(saveData);
    }

    /// <summary>
    /// Writes the text support block of the selection screen,
    /// <c>outputCampaignScenarioInfoInTextSupportMode()</c> (<c>game_campaign.cpp:1084-1138</c>).
    /// </summary>
    /// <param name="saveData">The campaign being selected.</param>
    /// <param name="allowToRestart">Whether the screen restarts the current scenario instead of starting it.</param>
    /// <remarks>
    /// 这一屏要引用的键名在 UI 层的 <see cref="GameHotKeys"/>，Game 层拿不到，所以由这里打包传进去。
    /// 模式位在 <see cref="Settings.IsTextSupportMode"/>，输出通道在 <see cref="TextSupport"/>，两者由
    /// <see cref="Settings.SetTextSupportMode"/> 一起翻。
    /// </remarks>
    private static void WriteCampaignSelectionTextSupport(CampaignSaveData saveData, bool allowToRestart)
    {
        if (!TextSupport.IsEnabled)
        {
            return;
        }

        CampaignScenarioTextSupport.Write(
            saveData,
            allowToRestart,
            new CampaignScenarioTextSupportKeys(
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.CampaignSelectFirstBonus),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.CampaignSelectSecondBonus),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.CampaignSelectThirdBonus),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.CampaignRestartScenario),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.DefaultOkay),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.CampaignViewIntro),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.CampaignSelectDifficulty),
                GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.DefaultCancel)));
    }

    /// <summary>
    /// Runs the campaign selection of <c>Game::SelectCampaignScenario()</c> until the player starts a scenario or
    /// leaves, one modal per step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考把这条循环写在一个函数里，难度窗、帮助窗、播片都在循环内部同步等待。端口只允许一层模态
    /// （<see cref="RunModal"/>），所以「选关屏」关掉之后按 <see cref="CampaignScenarioSelectionScreen.Result"/>
    /// 决定下一步：开难度窗、播片、还是重新开一屏。存档里的奖励与难度都活在 <see cref="CampaignSaveData"/> 上，
    /// 重开一屏不丢状态。
    /// </para>
    /// <para>
    /// 「换一关」这一支不重开循环：对话框已经把新的关卡写进存档，下一圈 <see cref="CampaignScenarioSelectionScreen.TryCreate"/>
    /// 就按新关卡重建。
    /// </para>
    /// <para>
    /// <c>allowToRestart</c> 只有冒险地图的「Scenario Information」入口传 true（<c>interface_events.cpp:378</c>）；
    /// 主菜单与读档都是 false。为真时：不播本关开场（<c>:1305</c>）、奖励必须已经选过（<c>:1402-1405</c>）、
    /// 按钮由 OKAY 换成 RESTART（<c>:1420-1429</c>）、点它要再确认一次（<c>:1598-1602</c>）。
    /// </para>
    /// </remarks>
    /// <param name="saveData">The save of the campaign being selected.</param>
    /// <param name="allowToRestart">
    /// True when the running scenario may be restarted instead of starting a new one,
    /// the <c>allowToRestart</c> of <c>Game::SelectCampaignScenario()</c> (<c>game_campaign.cpp:1284</c>).
    /// </param>
    private void RunCampaignScenarioSelection(CampaignSaveData saveData, bool allowToRestart = false)
    {
        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            return;
        }

        if (_menu is not { } menu)
        {
            GameLog.Warn("The campaign selection needs the main menu, which owns the translations.");

            return;
        }

        if (assets.DataDirectory is not { } dataDirectory)
        {
            GameLog.Warn("The campaign selection needs the game data directory.");

            return;
        }

        List<string> roots = [dataDirectory];
        IReadOnlyList<string> mapDirectories = MapCatalog.FindDirectories(roots);
        var screenSize = new Size(_frame.Pixels.Width, _frame.Pixels.Height);

        // game_campaign.cpp:1305-1307：进选关屏先放本关的开场，重开模式（allowToRestart）不播。
        // 放在循环外，换关重建屏幕时不重放——参考也只在这一处播。
        if (!allowToRestart)
        {
            PlayCampaignVideos(CampaignVideoPlayback.GetCurrentScenarioVideoFiles(saveData));
        }

        // :1309：text support 模式下把这一屏的信息写成裸文本行，块首尾各有一行短横线。
        WriteCampaignSelectionTextSupport(saveData, allowToRestart);

        // :1311：选关屏的主题曲。罗兰与阿基巴德各一首，价格联盟那几套没有专属曲，返回 null 就不动当前音乐。
        if (CampaignMusic.GetSelectionTrack(saveData.CampaignId) is { } theme)
        {
            _audio?.PlayMusic(theme);
        }

        // 参考把难度存在循环内的局部变量里（game_campaign.cpp:1513 的 currentDifficulty），
        // 挑难度只改它、不写档；写档要等到点 OK 开局（:1643）。放在循环外，「换一关」重建屏幕时不丢。
        int campaignDifficulty = saveData.Difficulty;

        while (true)
        {
            CampaignScenarioSelectionScreen? screen = CampaignScenarioSelectionScreen.TryCreate(
                assets, menu.Text, fonts, saveData, mapDirectories, screenSize, allowToRestart, out string? error);

            if (screen is null)
            {
                GameLog.Warn($"The campaign selection cannot be shown: {error}");

                return;
            }

            // 冒烟要点中奖励单选，得知道它落在哪；参考没有这条日志，但主菜单的「signs at [...]」是同一性质。
            GameLog.Info(
                $"The campaign selection shows scenario '{screen.Dialog.ScenarioInfoId}' with bonus areas " +
                $"[{string.Join(", ", screen.Dialog.BonusAreas)}].");

            RunModal(screen);

            if (!screen.IsFinished)
            {
                return;
            }

            switch (screen.Result)
            {
                case CampaignSelectionAction.Cancel:
                    GameLog.Info("Left the campaign selection.");

                    return;

                case CampaignSelectionAction.ChooseDifficulty:
                    campaignDifficulty = ChooseCampaignDifficulty(campaignDifficulty, menu.Text, fonts, screenSize);

                    break;

                case CampaignSelectionAction.ViewIntro:
                    // game_campaign.cpp:1675-1689：先放上一关的结尾，再放本关的开场。
                    PlayCampaignVideos(CampaignVideoPlayback.GetPreviousScenarioVideoFiles(saveData));
                    PlayCampaignVideos(CampaignVideoPlayback.GetCurrentScenarioVideoFiles(saveData));

                    break;

                case CampaignSelectionAction.StartScenario:
                    // :1589-1596：不是新开局（或允许重开）又改过难度，先问一句；NO 就留在这一屏。
                    if ((!saveData.IsStarting || allowToRestart) && campaignDifficulty != saveData.Difficulty
                        && !ShowCampaignYesNo(
                            "Difficulty",
                            "You have changed the campaign difficulty. The final high score will be calculated " +
                            "based solely on the lowest difficulty level for a completed scenario during the " +
                            "campaign. Do you want to proceed?"))
                    {
                        GameLog.Info("The campaign difficulty change was cancelled; the scenario is not started.");

                        continue;
                    }

                    // :1598-1602：重开再来一次确认。重开模式下 OKAY 是隐藏的（:1420-1429），能走到这里的
                    // StartScenario 只可能是那一颗 RESTART，所以用 allowToRestart 就能分辨。
                    if (allowToRestart
                        && !ShowCampaignYesNo("Restart", "Are you sure you want to restart this scenario?"))
                    {
                        GameLog.Info("The scenario restart was cancelled.");

                        continue;
                    }

                    StartCampaignScenarioMap(
                        saveData, mapDirectories, screen.Dialog.SelectedBonusIndex, campaignDifficulty,
                        resetMinDifficulty: saveData.IsStarting && !allowToRestart);

                    return;

                default:
                    // SelectOtherScenario 已经把新关卡写进存档，重开一屏即可。
                    break;
            }
        }
    }

    /// <summary>
    /// Opens the difficulty window of the campaign selection and hands back the pick,
    /// <c>Game::setCampaignDifficulty()</c> (<c>game_campaign.cpp:912-1082</c>).
    /// </summary>
    /// <remarks>
    /// 这个函数**不写存档**：参考把选择放回选关屏的局部变量 <c>currentDifficulty</c>
    /// （<c>game_campaign.cpp:1513</c>、<c>:1698-1699</c>），点 OK 开局时才 <c>setDifficulty</c>（<c>:1643</c>）。
    /// 挑一次就写的话，下面的「难度变了没有」永远为假，确认框也就永远不会出现。
    /// </remarks>
    /// <param name="currentDifficulty">The difficulty the selection screen holds.</param>
    /// <param name="text">Translations of the labels of the window.</param>
    /// <param name="fonts">The fonts of the window.</param>
    /// <param name="screenSize">The size of the screen the window is centred on.</param>
    /// <returns>The picked difficulty, or <paramref name="currentDifficulty"/> when the window cannot open.</returns>
    private int ChooseCampaignDifficulty(int currentDifficulty, GameText text, GameFonts fonts, Size screenSize)
    {
        CampaignDifficultyScreen? screen =
            CampaignDifficultyScreen.TryCreate(fonts, screenSize, currentDifficulty, text);

        if (screen is null)
        {
            return currentDifficulty;
        }

        RunModal(screen);

        GameLog.Info($"The campaign difficulty selection is {screen.CurrentDifficulty}.");

        return screen.CurrentDifficulty;
    }

    /// <summary>
    /// Asks a yes/no question on top of the campaign selection,
    /// <c>fheroes2::showStandardTextMessage( …, Dialog::YES | Dialog::NO )</c>.
    /// </summary>
    /// <param name="header">Title of the box.</param>
    /// <param name="body">The question.</param>
    /// <returns>
    /// <see langword="true"/> when YES was pressed. A box that cannot be shown also answers YES: the reference only
    /// aborts on NO, so treating a missing button set as NO would silently swallow the player's click.
    /// </returns>
    private bool ShowCampaignYesNo(string header, string body)
    {
        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            GameLog.Warn($"The question \"{body}\" needs the game data; it is taken as YES.");

            return true;
        }

        _systemButtons ??= SystemButtonArtwork.TryCreate(assets);

        if (_systemButtons is not { } buttons)
        {
            GameLog.Warn($"The question \"{body}\" needs the standard buttons; it is taken as YES.");

            return true;
        }

        MessageBoxModal box = MessageBoxModal.Create(
            header, body, fonts.Yellow, fonts.White, DialogButton.Yes | DialogButton.No, buttons);

        RunModal(box);

        return box.Answer == DialogButton.Yes;
    }

    /// <summary>
    /// Plays campaign clips one after another, <c>Video::ShowVideo()</c> as <c>game_campaign.cpp</c> uses it.
    /// </summary>
    /// <param name="files">The clips in playback order, from <see cref="CampaignVideoPlayback"/>.</param>
    /// <remarks>
    /// 冒烟与截图是脚本，不是一局游戏：影片会停在模态窗口里等按键，把预算耗在一段与要截的画面无关的动画上，
    /// 所以和启动 logo 同一条判据跳过（<see cref="PlayStartupVideos"/>），只留一行日志说明本来要播什么。
    /// </remarks>
    private void PlayCampaignVideos(IReadOnlyList<string> files)
    {
        foreach (string file in files)
        {
            if (_options.SmokeFrames is not null || _options.ScreenshotPath is not null)
            {
                GameLog.Info($"Skipping the campaign video '{file}' because the run is scripted.");

                continue;
            }

            PlayVideo(file);
        }
    }

    /// <summary>
    /// Loads the map of the selected campaign scenario and shows it, <c>game_campaign.cpp:1656-1710</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 顺序与参考一致：先处理「必须在读地图之前生效」的两条（起始种族、叛变关清空奖励，<c>:1631-1640</c>），
    /// 再按关卡取地图（战役地图要在读入时就补上版本差异）、把地图记进 <see cref="Settings"/>、玩法类型定成
    /// <see cref="GameTypes.Campaign"/>，然后才建场景。其余奖励需要实际的世界与英雄，所以放在场景建好之后，
    /// 最后才把选中的奖励编号记进存档（<c>:1665-1671</c>）。
    /// </para>
    /// <para>
    /// 奖励编号传进来而不是从存档读：存档里那个是「上一次这一关选的」，本次的选择还没写进去。
    /// </para>
    /// </remarks>
    /// <param name="saveData">The save whose current scenario is started.</param>
    /// <param name="mapDirectories">Where the campaign maps live.</param>
    /// <param name="bonusIndex">The bonus the player picked on the selection screen, -1 for none.</param>
    /// <param name="difficulty">The difficulty the selection screen holds, written to the save once the map is up.</param>
    /// <param name="resetMinDifficulty">
    /// Whether the lowest difficulty starts over, <c>campaignSaveData.isStarting() &amp;&amp; !allowToRestart</c>
    /// of <c>game_campaign.cpp:1643</c>.
    /// </param>
    private void StartCampaignScenarioMap(
        CampaignSaveData saveData, IReadOnlyList<string> mapDirectories, int bonusIndex, int difficulty,
        bool resetMinDifficulty)
    {
        CampaignData campaignData = CampaignData.GetCampaignData(saveData.CampaignId);

        if (saveData.CurrentScenarioId < 0 || saveData.CurrentScenarioId >= campaignData.AllScenarios.Count)
        {
            GameLog.Warn($"The campaign {saveData.CampaignId} has no scenario {saveData.CurrentScenarioId}.");

            return;
        }

        ScenarioData scenario = campaignData.AllScenarios[saveData.CurrentScenarioId];
        ScenarioInfoId scenarioInfo = saveData.CurrentScenarioInfoId;

        bool hasBonus = bonusIndex >= 0 && bonusIndex < scenario.Bonuses.Count;
        ScenarioBonusData bonus = hasBonus ? scenario.Bonuses[bonusIndex] : new ScenarioBonusData();

        // :1631-1635 起始种族（含「种族 + 部队」那一档）必须在读地图、玩家定阵容之前定下来。
        if (bonus.Type is ScenarioBonusData.BonusType.StartingRace or ScenarioBonusData.BonusType.StartingRaceAndArmy)
        {
            CampaignBonuses.SetStartingRaceBonus(
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, bonus.SubType, bonus.Amount));
        }

        // :1638-1640 叛变关（Rolebert）把之前攒的奖励全没收。
        if (CampaignData.IsBetrayalScenario(scenarioInfo))
        {
            saveData.RemoveAllAwards();
        }

        Mp2FileInfo? mapInfo = scenario.LoadPlayableMap(mapDirectories);

        if (mapInfo is null)
        {
            GameLog.Warn($"The map file of the scenario '{scenario.ScenarioName}' is not in the game data.");

            return;
        }

        // :1643 地图读到了才动存档：地图不在时参考也不会改难度。最低难度只有新战役第一关才重置
        // （isStarting() && !allowToRestart，端口 allowToRestart 恒为 false）。
        saveData.SetDifficulty(difficulty, resetMinDifficulty);

        GameLog.Info(
            $"The campaign difficulty is {saveData.Difficulty} with a minimum of {saveData.MinDifficulty}.");

        Settings settings = Settings.Get();
        settings.SetCurrentMapFileInfo(mapInfo);

        // 战役与单图在存档的扩展名和存档头里的类型上分家（game_io.cpp:227-231），开局前必须定下来。
        settings.SetGameType(GameTypes.Campaign);

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, mapInfo, out string? error);

        if (scene is null)
        {
            GameLog.Warn($"The campaign scenario '{scenario.ScenarioName}' cannot be started: {error}");

            return;
        }

        ApplyCampaignStartBonuses(scene.World, saveData, bonus, hasBonus);

        // :1671 选中的编号最后才记账：它会跟着「完成这一关」一起进 _bonusesForFinishedMaps。
        saveData.SetCurrentScenarioInfo(scenarioInfo, hasBonus ? bonusIndex : -1);

        ShowAdventureScene(scene);

        GameLog.Info(
            $"Started the campaign scenario '{scenario.ScenarioName}' (campaign {saveData.CampaignId}, " +
            $"scenario {saveData.CurrentScenarioId}, bonus {(hasBonus ? $"{bonusIndex} '{bonus.GetName()}'" : "none")}).");

        // --complete-campaign-scenario：把这一关直接宣布为打赢，替玩家跳过整场战斗。走的仍是真实的结算
        // 与写档那一段，只是不经过战斗（战役地图的胜利条件要打几十回合，截图与冒烟等不起）。
        if (_options.CompleteCampaignScenario)
        {
            GameLog.Info("--complete-campaign-scenario: the scenario is declared won right away.");
            CompleteCampaignScenario();
        }
    }

    /// <summary>
    /// Applies the bonus the player picked for the scenario and the awards earlier scenarios handed out,
    /// <c>Campaign::applyObtainedCampaignAwards</c> and <c>Campaign::setScenarioBonus</c>
    /// (<c>game_campaign.cpp:1664-1669</c>).
    /// </summary>
    /// <param name="world">The world of the scenario just built.</param>
    /// <param name="saveData">The save that holds the awards.</param>
    /// <param name="bonus">The bonus picked on the selection screen, ignored when <paramref name="hasBonus"/> is false.</param>
    /// <param name="hasBonus">Whether <paramref name="bonus"/> is a real choice.</param>
    private static void ApplyCampaignStartBonuses(
        World world, CampaignSaveData saveData, ScenarioBonusData bonus, bool hasBonus)
    {
        // 纯「起始种族」那一档在读地图之前已经设过（:1631-1635），这里跳过；「种族 + 部队」要再走一遍，
        // 因为部队得等英雄就位才发得出来（:1665 的条件只排除 STARTING_RACE）。
        if (hasBonus && bonus.Type != ScenarioBonusData.BonusType.StartingRace)
        {
            CampaignBonuses.SetScenarioBonus(world, saveData.CurrentScenarioInfoId, bonus);
        }

        CampaignBonuses.ApplyObtainedCampaignAwards(
            world, saveData.CurrentScenarioInfoId, saveData.GetObtainedCampaignAwards());
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
        _scene = (IScene?)_battle ?? _adventure ?? (IScene?)_menu ?? _diagnostics;

        // 参考离开城堡屏回地图时调 Game::setDisplayFadeIn()（game_startgame.cpp:376）：城堡屏是整屏窗口，
        // 关掉之后地图要淡回来。没有地图可回（主菜单）时标志留着不用，主菜单还没有淡入。
        GameDisplayFadeIn.SetDisplayFadeIn();
    }

    /// <summary>
    /// Goes back to the map when the battle is over, the <c>GameMode::BATTLE</c> return of
    /// <c>Battle::Loader()</c>.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>Battle::Loader</c> 返回之后，调用方（<c>Heroes::Action</c> 或城堡）接着把结果写回地图；
    /// 端口这一步只做画面，打完就回地图，瓦片与军队的结算留给接上 <c>BattleLoader</c> 的那一步。
    /// </remarks>
    private void CloseBattleWhenDone()
    {
        if (_battle is not { IsClosed: true })
        {
            return;
        }

        _battle = null;
        _scene = _adventure ?? (IScene?)_menu ?? _diagnostics;

        GameLog.Info("The battle is over; the adventure map is back.");

        // 参考的战果窗关掉之后调 Game::setDisplayFadeIn()（battle_interface.cpp:1534）：战场先淡出成黑，
        // 地图再淡回来。召唤它的是 heroes_action 那 12 处 renderWithFadeInOrPlanRender。
        GameDisplayFadeIn.SetDisplayFadeIn();

        // --battle-only 是一场打完就收工：无人值守跑一趟、结果留在日志里，不必再靠 --smoke-frames 掐停。
        if (_options.BattleOnly)
        {
            GameLog.Info("--battle-only: the single battle is finished, closing the client.");
            _running = false;
        }
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

        // --map is the port's stand-in for Game::SelectScenario(), and the reference records the chosen map in the
        // settings there (scenarioinfo.cpp): the save writer refuses to write without it, and the AI's territory
        // planning, the spells of a hero and the map-version checks all read it back. A map opened with --map has to
        // look like a chosen one, otherwise it cannot be saved at all.
        Settings settings = Settings.Get();
        settings.SetCurrentMapFileInfo(mapInfo);

        // 玩法类型也得先定下来，参考是在开工之前由 Game::NewStandard() 设的（game_newgame.cpp:149-157）。它不只是
        // 存档扩展名的输入，还写进存档头；留着 TYPE_MENU（0）写出的档是 .savm、头里类型 0，读档的类型校验
        // （game_io.cpp:227-231）连自己写的档都会拒掉。
        settings.SetGameType(GameTypes.Standard);

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, mapInfo, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The map file '{filePath}' cannot be shown: {error}");

            return;
        }

        ShowAdventureScene(scene);
    }

    /// <summary>
    /// Makes <paramref name="scene"/> the adventure map on screen and wires the host side of its turn loop.
    /// </summary>
    /// <param name="scene">The map to show.</param>
    private void ShowAdventureScene(AdventureMapScene scene)
    {
        scene.ExitEvent = LeaveAdventureMap;

        // L 的确认框答 YES 之后，参考只是把 LOAD_GAME 交回 StartGame()，主循环随后进读档屏
        // （game_startgame.cpp:1166-1168 → game_mainmenu.cpp:154-155）：地图先离开，列表画在主菜单背景上。
        scene.LoadGameEvent = LeaveAdventureMapForLoadGame;

        // 战役的一关打赢之后，参考把 COMPLETE_CAMPAIGN_SCENARIO 交回主循环，由 CompleteCampaignScenario()
        // 记账（game_mainmenu.cpp:205-206）。结算要世界与存档，所以只有宿主能接。
        scene.CampaignScenarioCompleted = CompleteCampaignScenario;

        // 战役局里的 Scenario Information 开的是同一张选关屏，只是 allowToRestart 为真
        // （interface_events.cpp:366-396 → game_campaign.cpp:1284）。取消就留在图上，重启则在里面重开这一关。
        scene.CampaignSelectionRequested = () => RunCampaignScenarioSelection(CampaignSaveData.Get(), allowToRestart: true);

        // 单图打赢：参考回 HIGH_SCORES_STANDARD，主循环先放 WIN.SMK 再开榜（game_mainmenu.cpp:203-205）。
        // 榜与片子都要宿主，场景只把成绩交上来；开榜排到下一帧（RunModal 不能在 Update 的栈上压）。
        scene.HighScoreRequested = ShowHighScoresAfterWin;

        if (scene.HeroActionHost is { } host)
        {
            host.RunModal = RunModal;
            host.OpenCastleScreen = scene.TryOpenCastle;
        }

        _adventure = scene;
        _scene = scene;
    }

    /// <summary>
    /// Asks for a save file and reopens it, <c>Game::LoadGame()</c> with <c>Game::LoadStandard()</c> behind it
    /// (<c>game_loadgame.cpp:91-125</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的 LOAD GAME 先弹 Standard / Campaign / Multi-Player 子菜单，端口还没有那个子菜单：这里直接进「标准
    /// 游戏」的存档列表 <c>Dialog::SelectFileLoad()</c>，那是唯一有存档可显示的一类。
    /// </para>
    /// <para>
    /// 读档窗口是阻塞的（<see cref="SelectFileDialog.Show"/> 自己画帧、自己收事件），与参考的
    /// <c>Dialog::SelectFileLoad()</c> 同形。读档成功后不再 <c>LoadMap</c>：世界已经在
    /// <see cref="GameSave.Load"/> 里恢复成存档那一刻，场景只负责把地图文件重新画出来。
    /// </para>
    /// </remarks>
    private void OpenLoadGame()
    {
        if (_assets is null)
        {
            // 没有素材就画不出列表窗；参考在没有游戏数据时根本起不来。
            GameLog.Warn("The save file list needs the game assets; the Load Game sign does nothing.");

            return;
        }

        // 参考的 Game::LoadStandard() 先把玩法类型设成单图再列档（game_loadgame.cpp:209-213）：列表按它筛扩展名，
        // 读回时存档头也拿它比对（game_io.cpp:227-231）。端口还没有 Standard / Campaign / Multi-Player 子菜单，
        // 所以这里就是「标准游戏」那一路。
        Settings.Get().SetGameType(GameTypes.Standard);

        // 窗口是阻塞的：它自己压帧、自己读 LocalEvent。这段里脚本点击不能再打回主菜单，否则就是
        // OnFrameEnded → OpenLoadGame → 窗口 → OnFrameEnded 的无限递归（见 ReplayScriptedInput 的分支）。
        AdventureMapDialogs dialogs = new(_frame.Pixels, _assets);
        string? file;

        _loadDialogOpen = true;

        try
        {
            // 窗口自己压帧：把「客户端还在跑」交给它，冒烟预算耗尽时窗口才能跟着退出。
            file = dialogs.SelectLoadFile(keepRunning: () => _running);
        }
        finally
        {
            _loadDialogOpen = false;
        }

        if (file is null)
        {
            GameLog.Info("No save file was chosen, staying in the main menu.");

            return;
        }

        if (!OpenLoadFile(file))
        {
            dialogs.ShowMessage(string.Empty, "The save file could not be loaded.");
        }
    }

    /// <summary>
    /// Reopens the save file <paramref name="file"/>, the half of <c>Game::LoadGame()</c> that runs once a file has
    /// been chosen (<c>game_io.cpp:299-311</c>).
    /// </summary>
    /// <param name="file">The save file to read.</param>
    /// <returns><see langword="true"/> when the map of the save is on screen.</returns>
    private bool OpenLoadFile(string file)
    {
        // 读档的类型校验发生在 Load 的最前面（game_io.cpp:227-231）：类型没定下来（TYPE_MENU，0）就什么都读不了。
        // 菜单那一路已经由主菜单的 LOAD GAME 设好，--load 没有那道工序，就按扩展名反推——反推表与写档时的
        // GameSave.GetSaveFileExtension 成对，.savm 兜住的网络/单场战斗档端口还不能读，所以在这里就停。
        Settings settings = Settings.Get();

        if (settings.GameType == GameTypes.None && !TrySetGameTypeFromSaveFile(file))
        {
            GameLog.Warn($"The save file '{file}' is not one this client can load.");

            return false;
        }

        if (!GameSave.Load(file))
        {
            GameLog.Warn($"The save file '{file}' could not be loaded.");

            return false;
        }

        // Load() 恢复了存档里的地图头；没有它就没有 FilePath，地图文件读不出来。
        if (Settings.Get().CurrentMapFileInfo is not { } mapInfo)
        {
            GameLog.Warn($"The save file '{file}' does not name a map.");

            return false;
        }

        // 一条「上一关刚打完」的战役存档不该在旧地图上接着打：参考在这里把 GameMode 换成
        // COMPLETE_CAMPAIGN_SCENARIO_FROM_LOAD_FILE（game_io.cpp:261-265，判据是「不是开局」且「当前关就是最后完成的那关」），
        // 主循环随即调 CompleteCampaignScenario( true )：不重复记账、只补发奖励，然后回选关屏
        // （game_mainmenu.cpp:208-213）。
        if (Settings.Get().GameType == GameTypes.Campaign)
        {
            CampaignSaveData campaign = CampaignSaveData.Get();

            if (!campaign.IsStarting && campaign.CurrentScenarioInfoId == campaign.GetLastCompletedScenarioInfoId())
            {
                GameLog.Info($"The save file '{file}' ends a campaign scenario, so the campaign returns to its scenario list.");

                CampaignProgress.CollectScenarioAwards(World.Current);
                CampaignProgress.AdvanceScenario(shouldSave: false);

                ReturnToMenu();
                _pendingCampaignSelection = true;

                return true;
            }
        }

        // 存档头里只留地图的文件名（maps_fileinfo.cpp:666-667），参考靠 Settings::GetLastFile( "maps", name )
        // 在数据目录里把它还原成完整路径（game_io.cpp:275）。端口还没有那张表，就照它探的两处找：数据目录本身与
        // 它下面的 maps（MapCatalog.FindDirectories 就是这两处的端口版）。找不到就照旧报错，不猜。
        if (!File.Exists(mapInfo.FilePath) && ResolveMapFile(mapInfo.FilePath) is { } mapPath)
        {
            mapInfo = mapInfo with { FilePath = mapPath };
            Settings.Get().SetCurrentMapFileInfo(mapInfo);
        }

        AdventureMapScene? scene = AdventureMapScene.TryCreateLoaded(_frame, _assets, _fonts, mapInfo, out string? error);

        if (scene is null)
        {
            GameLog.Warn($"The loaded game cannot be shown: {error}");

            return false;
        }

        GameLog.Info($"Loaded the game from '{file}'.");
        ShowAdventureScene(scene);

        return true;
    }

    /// <summary>
    /// Sets the type of game from the extension of a save file, the port's stand-in for the Standard / Campaign /
    /// Multi-Player choice in front of <c>Dialog::SelectFileLoad()</c> (<c>game_loadgame.cpp:68-78,209-213</c>).
    /// </summary>
    /// <param name="file">The save file about to be read.</param>
    /// <returns><see langword="true"/> when the extension names a type this client can play.</returns>
    /// <remarks>
    /// 三档的对应关系与写档时的 <see cref="GameSave.GetSaveFileExtension(GameTypes)"/> 成对：<c>.sav</c> 单图、
    /// <c>.savc</c> 战役、<c>.savh</c> 热座。<c>.savm</c> 是那一档的兜底，参考用它装网络、单场战斗与自动试玩，
    /// 端口都还不能继续玩，所以这里返回失败而不是猜一个类型。
    /// </remarks>
    private static bool TrySetGameTypeFromSaveFile(string file)
    {
        GameTypes gameType;

        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".sav":
                gameType = GameTypes.Standard;
                break;

            case ".savc":
                gameType = GameTypes.Campaign;
                break;

            case ".savh":
                gameType = GameTypes.HotSeat;
                break;

            default:
                return false;
        }

        Settings.Get().SetGameType(gameType);

        return true;
    }

    /// <summary>
    /// Finds the map file a save names by its bare file name, <c>Settings::GetLastFile( "maps", name )</c>
    /// (<c>settings.cpp:754-758</c>).
    /// </summary>
    /// <param name="fileName">The name stored in the save header, possibly already a full path.</param>
    /// <returns>The full path of an existing map file, or <see langword="null"/> when nothing matches.</returns>
    /// <remarks>
    /// 参考在所有「根目录 + <c>maps</c>」里找同名文件，端口能问的根目录只有素材目录，探法交给
    /// <see cref="MapCatalog.FindDirectories"/>。找不到就是「这张图不在本机」，由调用方报错——猜一个相近的名字
    /// 会让读档进到另一张图上，比失败更糟。
    /// </remarks>
    private string? ResolveMapFile(string fileName)
    {
        if (_assets?.DataDirectory is not { } dataDirectory)
        {
            return null;
        }

        foreach (string directory in MapCatalog.FindDirectories([dataDirectory]))
        {
            string candidate = Path.Combine(directory, fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Answers the exit request of the turn loop: the map goes back to the main menu and the loop is told that
    /// nothing is left to do, the port's stand-in for <c>Game::processExitEvent()</c> followed by
    /// <c>GameMode::MAIN_MENU</c>.
    /// </summary>
    /// <returns>Always <see cref="AdventureEventResult.Cancel"/>: the map is already behind the scene.</returns>
    private AdventureEventResult LeaveAdventureMap()
    {
        ReturnToMenu();

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Accounts for a finished campaign scenario, <c>Game::CompleteCampaignScenario( false )</c>
    /// (<c>game_campaign.cpp:1160-1282</c>), and decides where the player goes next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 四步按参考的顺序做：记完成与天数、写「下一关」存档、发本关奖励、挪到下一关并决定去向。
    /// 写档夹在第二与第三步之间不是笔误——参考正是在这里写（<c>:1169-1171</c>），此时存档里的
    /// <c>CurrentScenarioInfoId</c> 还等于 <c>GetLastCompletedScenarioInfoId</c>，读档时靠这个判据认出
    /// 「这一关已经打完」（<c>game_io.cpp:261-265</c>）；奖励写在它之后，所以存档里不含本关奖励，读档时补发。
    /// </para>
    /// <para>
    /// 存档还必须在离开地图之前写：文件名与存档头都取自 <see cref="Settings.CurrentMapFileInfo"/> 与
    /// <see cref="Settings.GameType"/>，一旦换了场景这两样就变了。
    /// </para>
    /// <para>
    /// 去向照参考：非最后一关回选关屏（<c>game_campaign.cpp:1279-1281</c>），最后一关进片尾与战役高分榜
    /// （<c>:1242-1277</c>）。片尾与高分榜端口还没有（P5 结局屏），所以最后一关先停在主菜单。
    /// </para>
    /// </remarks>
    private void CompleteCampaignScenario()
    {
        World world = World.Current;

        bool shouldSave = CampaignProgress.RecordFinishedScenario(world);

        if (shouldSave && !GameSave.SaveCompletedCampaignScenario())
        {
            GameLog.Warn("The inter-scenario save of the campaign could not be written.");
        }

        CampaignProgress.CollectScenarioAwards(world);

        // game_campaign.cpp:1240：发完奖励先放刚打完这一关的结尾，再判断是不是最后一关。
        PlayCampaignVideos(CampaignVideoPlayback.GetPreviousScenarioVideoFiles(CampaignSaveData.Get()));

        CampaignScenarioCompletion completion = CampaignProgress.AdvanceScenario(shouldSave);

        GameLog.Info(
            $"The campaign scenario is complete: campaign {CampaignSaveData.Get().CampaignId}, " +
            $"{(completion.IsCampaignFinished ? "the campaign is finished" : $"next scenario {completion.NextScenario}")}.");

        ReturnToMenu();

        if (completion.IsCampaignFinished)
        {
            // 战役打完最后一关：参考的顺序是「上一关的片 → Game::ShowCredits( false ) → 带评分字幕的 WIN.SMK
            // → 胜利音乐 → 战役高分榜」（game_campaign.cpp:1240-1276）。credits 屏还没移植（bd urk），其余照做。
            CampaignSaveData saveData = CampaignSaveData.Get();
            uint score = HighScoreBoard.GetCampaignRating(saveData);

            string difficulty = _menu is { } menu
                ? menu.Text.Get(CampaignEndingText.DifficultyMessageId(saveData.MinDifficulty))
                : CampaignEndingText.DifficultyMessageId(saveData.MinDifficulty);

            string rating = CampaignEndingText.Describe(saveData.DaysPassed, difficulty, score);

            GameLog.Info(
                $"The campaign is over after {saveData.DaysPassed} days; the score is {score} and the rating "
                + $"is '{CampaignEndingText.RatingName(score)}'.");

            if (!PlayVideo("WIN.SMK", loop: false, waitForKey: true, subtitle: rating))
            {
                GameLog.Warn("'WIN.SMK' is not in the game data; the campaign table follows without the ending clip.");
            }

            _audio?.PlayMusic(MusicTrack.Victory);

            _pendingHighScores = new PendingHighScores(
                IsCampaign: true,
                new HighScoreCompletion(
                    CampaignAwardData.GetCampaignName(saveData.CampaignId),
                    saveData.DaysPassed,
                    score,
                    world.MapSeed));

            return;
        }

        // 选关屏要等主循环的下一帧：这一帧还在地图的 Update 里，换场景由 ReturnToMenu() 刚刚做完。
        _pendingCampaignSelection = true;
    }

    /// <summary>
    /// Answers the map's confirmed load request: the map is dropped and the save file list opens, the two steps
    /// <c>StartGame()</c> and the main loop take on a <c>GameMode::LOAD_GAME</c>
    /// (<c>game_startgame.cpp:1166-1168</c>, <c>game_mainmenu.cpp:154-155</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 顺序与参考一致：先离开地图，再把列表画在主菜单背景上（<c>game_loadgame.cpp:215-246</c> 的
    /// <c>drawMainMenuScreen()</c> 加 <c>Dialog::SelectFileLoad()</c>）。确认框已经说过「当前游戏会丢」，
    /// 所以取消列表也不会回地图——参考的列表取消回到 Load Game 子菜单，那个子菜单端口还没有，这里停在主菜单。
    /// </para>
    /// <para>
    /// 这一段跑在地图的 <see cref="AdventureMapScene.Update"/> 里面，窗口的帧由
    /// <see cref="OnFrameEnded"/> 里 <see cref="_loadDialogOpen"/> 那条分支照常计时并回放脚本输入。
    /// </para>
    /// </remarks>
    private void LeaveAdventureMapForLoadGame()
    {
        ReturnToMenu();
        OpenLoadGame();
    }

    /// <summary>Leaves the adventure map and goes back to the main menu.</summary>
    private void ReturnToMenu()
    {
        if (_adventure is null)
        {
            return;
        }

        GameLog.Info($"Leaving the adventure map '{_adventure.MapName}'.");
        _battle = null;
        _castle = null;
        _adventure = null;
        _scene = _menu is not null ? _menu : _diagnostics;

        // The verdict of the scenario stays in GameOverResult until the next game starts, so the clip is played
        // when the map is left because the game is over, not when the player quits.
        PlayGameOverVideos();
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

        SpendSmokeBudget();
    }

    /// <summary>
    /// Stops the client once <c>--smoke-frames</c> rendered frames are spent, the scripted end of a run.
    /// </summary>
    /// <remarks>
    /// 主循环与 <see cref="RunModal"/> 的内层循环都要调它：预算只在一个地方算，模态窗口里等人输入的那几帧
    /// 也算数，否则挂在一个等不到输入的窗口上就永远退不出来。
    /// </remarks>
    private void SpendSmokeBudget()
    {
        if (_options.SmokeFrames is not int maxFrames || _timer.FrameCount < maxFrames || !_running)
        {
            return;
        }

        GameLog.Info($"Smoke mode finished after {_timer.FrameCount} frames ({_timer.FramesPerSecond:0.0} FPS).");
        _running = false;
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

        // 淡入压在最后一层上，再推上屏：参考的 fadeInDisplay() 也是画完这一帧之后才叠上去
        // （gui/interface_base.cpp:28-50）。场景每帧把该遮的遮好、该留的留好，这里只负责按帧推进。
        if (_scene.TakePendingFadeIn() is { } fadeIn)
        {
            _fadeIn = fadeIn;

            // 和 "Drawing the 'X'." 同一类诊断：没有这一行，冒烟脚本分不清「淡入播了」与「标志设了没人取」。
            GameLog.Info($"Fading the screen in over {fadeIn.FrameCount} frames, {fadeIn.Roi.Width}x{fadeIn.Roi.Height}.");
        }

        if (_fadeIn is not null)
        {
            // 淡入直接改索引像素，脏区上报不了自己。
            _frame.InvalidateAll();

            if (!_fadeIn.TryAdvance(_palette))
            {
                _fadeIn = null;
            }
        }

        _presenter.Upload(_frame);

        _presenter.BeginLogicalFrame();
        _presenter.DrawFrameTexture();

        _text?.BeginFrame();
        _scene.DrawOverlay(state);

        CaptureScreenshot();

        _presenter.EndFrame();
    }

    /// <summary>
    /// Opens the riddle window of the sphinx nearest the focus hero, the <c>--sphinx-riddle</c> of the command line.
    /// </summary>
    /// <remarks>
    /// 正常时机是英雄走到狮身人面像上（<c>heroes_action.cpp:3563</c>），那要走十几格；这个钩子替英雄把窗口开出来，
    /// 用的仍是同一个 <c>IHeroActionHost.AskRiddle</c>，所以窗口、输入与返回值走的都是真实路径。窗口是阻塞式的，
    /// 由主循环第一帧调用，等渲染器就位之后再把地图画在自己身后。
    /// </remarks>
    private void AskSphinxRiddle()
    {
        if (_adventure is not { } map || map.FindSphinxRiddle() is not { } riddle)
        {
            GameLog.Warn("--sphinx-riddle: the map has no sphinx whose riddle can be asked.");

            return;
        }

        string answer = map.HeroActionHost?.AskRiddle(riddle) ?? string.Empty;

        GameLog.Info($"The sphinx of '{map.MapName}' was asked from the command line: the answer was '{answer}'.");
    }

    /// <summary>
    /// Opens one of the windows the map's objects show, the <c>--adventure-window</c> of the command line.
    /// </summary>
    /// <remarks>
    /// 正常时机是英雄走到交易站或预言所上（<c>heroes_action.cpp:1288</c> 与 <c>:3039</c>），那要走几十格；这个钩子
    /// 替英雄把窗口开出来，走的仍是 <c>IHeroActionHost</c> 上那两个入口，所以窗口与返回值都是真实路径。与谜语窗
    /// 一样由主循环第一帧调用，等渲染器就位之后再把地图画在自己身后。
    /// </remarks>
    /// <param name="name">Name of the window: <c>market</c> or <c>guild</c>.</param>
    private void OpenAdventureWindow(string name)
    {
        if (_adventure is not { } map || map.HeroActionHost is not { } host)
        {
            GameLog.Warn($"--adventure-window {name}: the adventure map is not up.");

            return;
        }

        if (map.FocusHero is not { } hero)
        {
            GameLog.Warn($"--adventure-window {name}: the map has no focus hero whose kingdom could open it.");

            return;
        }

        Kingdom kingdom = map.World.Kingdoms.GetKingdom(hero.GetColor());

        switch (name.ToLowerInvariant())
        {
            case "market":
            case "marketplace":
                // 地图上的入口就是交易站，汇率固定三座市场（heroes_action.cpp:1288）。
                host.OpenMarketplace(kingdom, fromTradingPost: true);

                break;

            case "guild":
            case "thievesguild":
            case "oracle":
                host.OpenThievesGuild(kingdom);

                break;

            default:
                GameLog.Warn($"--adventure-window {name}: expected 'market' or 'guild'.");

                return;
        }

        GameLog.Info($"--adventure-window {name}: the window of '{map.MapName}' was closed again.");
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
            _menu = MainMenuScene.TryCreate(_frame, _assets, _fonts, StartLanguage);

            if (_menu is not null)
            {
                _menu.AnimationEnabled = !_options.FreezeAnimation;
                _menu.TouchInputAvailable = _touchInputAvailable;
                _menu.LanguageChanged = OnLanguageChanged;
                _menu.FirstRunFinished = OnFirstRunFinished;

                // 菜单在既没有 --lang、配置里也没有 lang 时会自己落到系统语言上（MainMenuScene.TryCreate）；
                // 把结果记回 Settings，文件里的 lang 才和界面一致，下一次启动也不必再猜一遍。
                if (Settings.Get().GameLanguage.Length == 0)
                {
                    Settings.Get().SetGameLanguage(_menu.Text.Culture);
                }

                if (_options.OpenSettings)
                {
                    _menu.OpenSettings();
                }

                if (_options.OpenNewGame)
                {
                    _menu.OpenNewGameMenu();
                }

                if (_options.OpenScenario || _options.ScenarioSelection is not null)
                {
                    if (_menu.OpenScenario() && _options.ScenarioSelection is { } selection)
                    {
                        _menu.Scenario?.Select(selection);
                    }
                }

                // 首运的语言选择与问候消息：参考的 isFirstGameRun 分支（game_mainmenu.cpp:265-300）。
                // 只有在真的读到了配置文件时才走——--no-config 时 Settings 连文件都没碰，旗标停在默认值上，
                // 让它出窗口会把像素基线和脚本一起卡住（见 ADR-202）。--open-settings / --open-scenario
                // 先占了窗口，BeginFirstRun 自己会让路，旗标留到下一次启动。
                if (ShouldStartFirstRun())
                {
                    _menu.BeginFirstRun();
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

        if ((_options.EnterMap || _options.OpenScenarioInfo) && _menu is not null)
        {
            if (_menu.Scenario is null)
            {
                _menu.OpenScenario();
            }

            // Confirm what the list shows: the first map, or the one --scenario-select picked. That opens the
            // scenario info screen (Game::SelectScenario() of the reference).
            bool confirmed = _menu.ConfirmScenario() is not null;

            // --scenario-info stops on that screen for the screenshot; --enter-map accepts it and starts the map.
            if (confirmed && _options.EnterMap)
            {
                _menu.AcceptScenarioInfo();
                TryEnterAdventureMap();
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.MapFile))
        {
            OpenMapFile(_options.MapFile);
        }

        // --load 跳过主菜单与存档列表，直接重开一个存档：走的还是读档的那一段（Load 之后 TryCreateLoaded）。
        if (!string.IsNullOrWhiteSpace(_options.LoadFile))
        {
            OpenLoadFile(_options.LoadFile);
        }

        // --skip-turns 在开机时就把回合推着往前走：轮到电脑时直接跑一整个电脑回合，轮到人类时直接结束，
        // 这样冒烟不点鼠标也能覆盖 AI::Planner::KingdomTurn()，而且能看到连着几天的决策。
        if (_options.SkipTurns is int skipTurns && _adventure is { } skipMap)
        {
            int ended = 0;

            while (ended < skipTurns && skipMap.Session is { IsGameOver: false } skippable)
            {
                if (skippable.IsAiTurn)
                {
                    AiTurnOutcome outcome = skippable.RunAiTurn();

                    if (outcome != AiTurnOutcome.EndTurn)
                    {
                        GameLog.Warn($"--skip-turns: the computer ended the game with {outcome}.");
                        break;
                    }
                }

                skipMap.EndTurn();
                ++ended;
            }

            GameLog.Info($"--skip-turns: ended {ended} turn(s); day {skipMap.Session?.DayCount.ToString() ?? "?"}.");
        }

        // The battle normally starts by walking a hero onto a monster tile, which the human action chain
        // cannot do yet (bd 归档（PROGRESS.md，FHeroes2Sharp-jwk） §7.4); --battle starts it here so the battle screen's frame can be
        // captured and smoke tested without a mouse.
        if (_options.Battle && _adventure is { } battleMap
            && battleMap.TryStartBattle(_options.FreezeAnimation, _options.BattleSiege, _options.BattleAuto) is { } battleScene)
        {
            _battle = battleScene;
            _scene = battleScene;

            GameLog.Info("Opened the battle screen from the command line.");
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

        // The popup lives only while the right button is held; --quick-info opens it on the tile under
        // --cursor (the button itself is held by --right-button, see the loop below).
        if (_options.QuickInfo && _adventure is { } infoMap && _options.Cursor is { } infoCursor)
        {
            GameLog.Info(infoMap.ShowQuickInfoAt(infoCursor)
                ? "The quick info of the tile under the cursor is open."
                : "--quick-info: the cursor is not over the map area.");
        }

        // The riddle window normally opens when a hero walks onto a sphinx, which needs a walk of a dozen
        // tiles; --sphinx-riddle asks the nearest one instead. The window blocks its caller and paints the
        // map behind itself, so it cannot open here where the renderer does not exist yet: the loop opens
        // it on its first frame.
        if (_options.SphinxRiddle)
        {
            _pendingSphinxRiddle = true;
        }

        // --adventure-window 的两个窗口同样要等渲染器：它们也由主循环的内层循环跑，关掉之后才回到地图。
        if (_options.AdventureWindow is { } adventureWindow)
        {
            _pendingAdventureWindow = adventureWindow;
        }

        // --cast-spell 的次元门要玩家在灰框里点一格，而那个方框也是界面里的阻塞循环：同样等渲染器。
        if (_options.CastSpell is { } castSpell && _adventure is not null)
        {
            _pendingSpellCast = castSpell;
        }

        // --campaign 的选关屏同理：它画在场景之上，还要读输入，只能由主循环开。
        if (_options.Campaign is { } campaign)
        {
            _pendingCampaign = campaign;
        }

        GameLog.Info(_menu is null
            ? "Starting on the diagnostics screen."
            : $"Starting on the main menu (language '{_menu.Text.Culture}', " +
              $"{_menu.Languages.Count} available).");
    }

    /// <summary>
    /// Writes the screenshot the command line or F12 asked for, and closes the client when a script asked for it.
    /// </summary>
    /// <param name="composedFrameOnly">
    /// Encodes <see cref="_frame"/> itself instead of reading the render target back, for the frames an interface
    /// loop of its own produced (see <see cref="OnFrameEnded"/>).
    /// </param>
    /// <remarks>
    /// 主循环在 <see cref="Draw"/> 里拍的是还没上屏的后缓冲（<c>SDL_RenderReadPixels</c>），读回来就是这一帧。
    /// 界面内层循环的帧在上一次循环里已经 present 过，后缓冲是空的，读回来只有一片白，所以那条路改成直接从
    /// <see cref="_frame"/> 编码。
    /// </remarks>
    private void CaptureScreenshot(bool composedFrameOnly = false)
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

            if (composedFrameOnly)
            {
                using FileStream stream = File.Create(path);
                PngWriter.WriteRgba(stream, _frame.Pixels, _frame.Palette);
            }
            else
            {
                _presenter!.SaveLogicalFrame(path);
            }

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

    /// <summary>
    /// Reads <c>fheroes2.cfg</c>, the <c>initConfig()</c> of <c>Game::Init()</c> (<c>game_init.cpp:217-230</c>):
    /// a missing or unreadable file is replaced by a freshly written default one, so the first run leaves a file
    /// to edit and later runs have something to remember the language in.
    /// </summary>
    private void LoadSettings()
    {
        if (_options.NoConfig)
        {
            GameLog.Info("Configuration file disabled (--no-config); preferences are not kept.");
            return;
        }

        string? path = ConfigurationFile.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _options.ConfigFile);

        if (path is null)
        {
            GameLog.Warn("No application data folder; preferences are not kept.");
            return;
        }

        Settings settings = Settings.Get();

        if (File.Exists(path) && settings.Read(path))
        {
            GameLog.Info($"Configuration read from '{path}' (interface language '{settings.GameLanguage}').");
        }
        else
        {
            if (!ConfigurationFile.EnsureDirectory(path) || !settings.Save(path))
            {
                GameLog.Warn($"The configuration file '{path}' cannot be written; preferences are not kept.");
                return;
            }

            GameLog.Info($"Configuration written to '{path}'.");
        }

        _configFile = path;

        // --lang 压过文件：它是端口给脚本选语言的入口，和菜单里依次翻语言等价。
        if (_options.Language is { } language)
        {
            settings.SetGameLanguage(language);
        }
    }

    /// <summary>
    /// Applies the switches that describe one run rather than a preference, so they stay out of the
    /// configuration file: <c>--difficulty</c> is the port's stand-in for the difficulty cursor of the
    /// reference's scenario info screen (<c>Game::SelectScenario</c>), whose number the starting resources
    /// are drawn from (<c>Kingdom::Init</c> reads <c>Game::getDifficulty()</c>).
    /// </summary>
    private void ApplySessionOptions()
    {
        if (_options.Difficulty is { } difficulty)
        {
            Settings.Get().SetGameDifficulty(difficulty);
            GameLog.Info($"Difficulty set to {difficulty} on the command line.");
        }
    }

    /// <summary>
    /// Creates the window the way the reference's <c>DisplayInitializer</c> restores it (<c>game_init.cpp:70-99</c>):
    /// the full screen state first (<c>engine().isFullScreen() != conf.FullScreen()</c> at <c>:89</c>), then the
    /// saved position (<c>display.setWindowPos</c> at <c>:96</c>), then the resolution
    /// (<c>display.setResolution</c> at <c>:98</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 命令行压过文件：<c>--fullscreen</c> 只会打开全屏，<c>--scale</c> 只改窗口倍数；给了 <c>--scale</c> 就不再
    /// 应用文件里的窗口尺寸。像素基线脚本走的是「<c>--no-config</c> + 默认 2 倍」，文件缺席时窗口仍是 2 倍。
    /// </para>
    /// <para>
    /// 参考能把帧缓冲也换成别的尺寸（<c>Display::setResolution</c> 会重建渲染目标），端口的场景全部按 640x480
    /// 画，所以只应用 <c>videomode</c> 的窗口那一半，帧缓冲换尺寸要等场景与界面支持别的逻辑尺寸。
    /// </para>
    /// </remarks>
    private SdlPresenter CreatePresenter()
    {
        Settings preferences = Settings.Get();
        Point savedPosition = preferences.SavedWindowPosition;
        bool hasPosition = savedPosition.X >= 0 && savedPosition.Y >= 0;

        SdlPresenter presenter = new(ScreenBuffer.DefaultSize, _options.WindowScale ?? DefaultWindowScale,
            _options.Fullscreen || preferences.IsFullScreen, hasPosition ? savedPosition : null);

        if (hasPosition)
        {
            GameLog.Info($"The window is at [ {savedPosition.X}, {savedPosition.Y} ] from the configuration file.");
        }

        if (_options.WindowScale is not null)
        {
            return presenter;
        }

        ResolutionInfo saved = preferences.CurrentResolutionInfo;

        if (saved.GameSize != ScreenBuffer.DefaultSize)
        {
            GameLog.Warn($"'videomode' asks for a {saved.GameSize} frame buffer; this build draws in "
                + $"{ScreenBuffer.DefaultSize}, so only the window size is applied.");
        }

        ResolutionInfo target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height,
            saved.ScreenWidth, saved.ScreenHeight);

        if (target.ScreenSize != presenter.OutputSize)
        {
            presenter.SetResolution(target);
            GameLog.Info($"The window is {presenter.OutputSize} from the configuration file.");
        }

        return presenter;
    }

    /// <summary>
    /// Writes the preferences, <c>conf.Save( Settings::configFileName )</c>; does nothing with <c>--no-config</c>.
    /// </summary>
    /// <remarks>
    /// <c>videomode</c> 与 <c>game window position</c> 现场问窗口（<c>settings.cpp:427-433</c> 问的是
    /// <c>Display::instance()</c>），全屏状态也是窗口的现状——参考在 <c>MainMenu</c> 关闭前把这两样记下来
    /// （<c>game_mainmenu.cpp:239-244</c>），端口在同一处写。
    /// </remarks>
    private void SaveSettings()
    {
        if (_configFile is not { } path || _presenter is not { } presenter)
        {
            return;
        }

        ResolutionInfo resolution = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height,
            presenter.OutputSize.Width, presenter.OutputSize.Height);

        Settings settings = Settings.Get();
        settings.SetFullScreen(presenter.IsFullscreen);

        if (settings.Save(path, resolution, presenter.WindowPosition))
        {
            GameLog.Info($"Preferences saved to '{path}'.");
        }
        else
        {
            GameLog.Warn($"Preferences could not be saved to '{path}'.");
        }
    }

    /// <summary>Records the language the settings dialog applied and writes the preferences out.</summary>
    /// <param name="text">The language that was applied.</param>
    private void OnLanguageChanged(GameText text)
    {
        Settings.Get().SetGameLanguage(text.Culture);
        SaveSettings();
    }

    /// <summary>
    /// Whether the menu has to walk the player through the onboarding: a configuration file was read and it
    /// still asks for it (<c>conf.isFirstGameRun()</c>, <c>game_mainmenu.cpp:265</c>).
    /// </summary>
    /// <remarks>
    /// <c>--no-config</c> 时 <see cref="LoadSettings"/> 提前返回，<see cref="Settings.IsFirstGameRun"/> 停在默认的
    /// <see langword="true"/> 上；若照它开窗，五个像素基线和所有脚本都会停在语言选择上，所以旗标只有在真的
    /// 读到了配置文件时才算数。
    /// </remarks>
    private bool ShouldStartFirstRun() => _configFile is not null && Settings.Get().IsFirstGameRun;

    /// <summary>
    /// Ends the first run: remembers the language the player settled on, clears the flag and writes the
    /// file, the <c>conf.resetFirstGameRun(); conf.Save( ... )</c> of <c>game_mainmenu.cpp:300</c>.
    /// </summary>
    private void OnFirstRunFinished()
    {
        Settings settings = Settings.Get();

        if (_menu is { } menu)
        {
            settings.SetGameLanguage(menu.Text.Culture);
        }

        settings.ResetFirstGameRun();
        SaveSettings();
        GameLog.Info("First run completed; the flag is cleared and the preferences are saved.");
    }

    /// <summary>
    /// The culture the main menu starts with: <c>--lang</c> first, then the configuration file, then English.
    /// </summary>
    /// <remarks>
    /// <c>--lang</c> 必须在这里再取一次：<c>--no-config</c> 时 <see cref="LoadSettings"/> 提前返回，配置文件里
    /// 的语言根本没有被读进来，而基线脚本正是「<c>--no-config</c> + <c>--lang zh_CN</c>」的组合。
    /// </remarks>
    private string? StartLanguage =>
        _options.Language ?? (Settings.Get().GameLanguage is { Length: > 0 } culture ? culture : null);

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
