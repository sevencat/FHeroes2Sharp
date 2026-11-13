using System.Diagnostics;
using FHeroes2.Assets;
using FHeroes2.Client.Desktop.Fonts;
using FHeroes2.Client.Desktop.Graphics;
using FHeroes2.Client.Desktop.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Core.Timing;
using SDL3;

namespace FHeroes2.Client.Desktop;

/// <summary>
/// Composition root and SDL3 host of the desktop client: owns the SDL lifetime, the window and
/// renderer (through <see cref="SdlPresenter"/>), the text backend, the frame loop and the
/// currently active scene.
/// </summary>
public sealed class GameHost : IDisposable
{
    /// <summary><c>SDL_BUTTON_LEFT</c>; only the left button drives the menu.</summary>
    private const int LeftMouseButton = 1;

    private readonly CommandLineOptions _options;
    private readonly FrameTimer _timer = new();
    private readonly bool _syntheticCursor;

    private AssetBundle? _assets;
    private Palette _palette = new();
    private ScreenBuffer _frame = null!;
    private SdlPresenter? _presenter;
    private SdlTextRenderer? _text;
    private GameFonts? _fonts;
    private IScene? _scene;
    private MainMenuScene? _menu;
    private DiagnosticsScene? _diagnostics;
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
            _presenter.SetPalette(_palette);

            _frame = new ScreenBuffer(ScreenBuffer.DefaultSize, _palette);
            CreateTextRenderer();
            CreateScenes();

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

    private void Loop()
    {
        Stopwatch clock = Stopwatch.StartNew();
        double previous = clock.Elapsed.TotalSeconds;

        while (_running)
        {
            if (!PumpEvents())
            {
                break;
            }

            double now = clock.Elapsed.TotalSeconds;

            // Clamp the step so that dragging the window does not teleport the animation.
            double elapsed = Math.Clamp(now - previous, 0.0, 0.5);
            previous = now;

            Update(elapsed);
            Draw();
        }
    }

    /// <summary>Drains the SDL event queue; returns <see langword="false"/> when the client must stop.</summary>
    private bool PumpEvents()
    {
        while (SDL.PollEvent(out SDL.Event evt))
        {
            switch ((SDL.EventType)evt.Type)
            {
                case SDL.EventType.Quit:
                case SDL.EventType.WindowCloseRequested:
                    return false;

                case SDL.EventType.KeyDown when HandleKeyDown(evt.Key):
                    return false;

                case SDL.EventType.MouseMotion:
                    // With --cursor the synthetic pointer is what a screenshot should show, so the
                    // real one must not steal the hover state.
                    if (!_syntheticCursor)
                    {
                        _menu?.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                    }

                    break;

                case SDL.EventType.WindowMouseLeave:
                    if (!_syntheticCursor)
                    {
                        _menu?.SetCursor(null);
                    }

                    break;

                case SDL.EventType.MouseButtonDown:
                case SDL.EventType.MouseButtonUp:
                    if (HandleMouseButton(evt.Button))
                    {
                        return false;
                    }

                    break;

                default:
                    break;
            }
        }

        return true;
    }

    /// <summary>Handles a key press; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleKeyDown(in SDL.KeyboardEvent key)
    {
        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                return true;

            case SDL.Keycode.F2 when !key.Repeat:
                _menu?.NextLanguage();
                return false;

            case SDL.Keycode.F11 when !key.Repeat:
                _presenter?.ToggleFullscreen();
                return false;

            case SDL.Keycode.F12 when !key.Repeat:
                _screenshotRequested = true;
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

    /// <summary>Handles a mouse button event; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleMouseButton(in SDL.MouseButtonEvent button)
    {
        if (_menu is null || button.Button != LeftMouseButton)
        {
            return false;
        }

        if (!_syntheticCursor)
        {
            _menu.SetCursor(ToLogical(button.X, button.Y));
        }

        return _menu.SetLeftButton(button.Down) is { } action && HandleMenuAction(action);
    }

    /// <summary>Reacts to the choice of a main menu sign; returns <see langword="true"/> to stop.</summary>
    private bool HandleMenuAction(MainMenuAction action)
    {
        if (action != MainMenuAction.Quit)
        {
            // The other signs are wired up from M3 on; the scene already shows this in its status bar.
            return false;
        }

        GameLog.Info("Quit selected, closing the client.");
        _running = false;
        return true;
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

        _scene.DrawFrame(state);
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

        if (_options.Scene == SceneKind.Menu)
        {
            _menu = MainMenuScene.TryCreate(_frame, _assets, _fonts, _options.Language);

            if (_menu is not null)
            {
                _menu.AnimationEnabled = !_options.FreezeAnimation;

                if (_options.Cursor is { } cursor)
                {
                    _menu.SetCursor(cursor);
                }
            }
        }

        _scene = _menu is not null ? _menu : _diagnostics;

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

        byte[]? paletteData = _assets.TryRead("KB.PAL");
        if (paletteData is null || paletteData.Length < Palette.RawPaletteSize)
        {
            GameLog.Warn("KB.PAL is missing or truncated; using the fallback grayscale palette.");
            return;
        }

        _palette = Palette.FromGamePalette(paletteData);
        GameLog.Info($"Palette loaded from KB.PAL ({paletteData.Length} bytes, 6 bit VGA normalized).");
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
