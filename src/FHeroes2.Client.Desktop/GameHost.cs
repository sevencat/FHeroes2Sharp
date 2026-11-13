using FHeroes2.Assets;
using FHeroes2.Client.Desktop.Fonts;
using FHeroes2.Client.Desktop.Graphics;
using FHeroes2.Client.Desktop.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Core.Timing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace FHeroes2.Client.Desktop;

/// <summary>
/// Composition root and MonoGame host of the desktop client: owns the window, the presenter,
/// the text backend and the currently active scene.
/// </summary>
/// <remarks>
/// The base class is written as <see cref="XnaGame"/> because the namespace
/// <c>FHeroes2.Game</c> shadows <c>Microsoft.Xna.Framework.Game</c> inside the product namespaces.
/// </remarks>
public sealed class GameHost : XnaGame
{
    private readonly CommandLineOptions _options;
    private readonly GraphicsDeviceManager _graphics;
    private readonly FrameTimer _timer = new();

    private AssetBundle? _assets;
    private Palette _palette = new();
    private ScreenBuffer _frame = null!;
    private SpriteBatch? _spriteBatch;
    private MonoGamePresenter? _presenter;
    private FontStashTextRenderer? _text;
    private DiagnosticsScene? _scene;
    private bool _supportsCjk;
    private bool _fullscreen;
    private int _screenshotFrame = -1;
    private bool _screenshotRequested;

    public GameHost(CommandLineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ScreenBuffer.DefaultWidth * options.WindowScale,
            PreferredBackBufferHeight = ScreenBuffer.DefaultHeight * options.WindowScale,
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = options.Fullscreen,
            HardwareModeSwitch = false,
        };

        _fullscreen = options.Fullscreen;

        if (options.ScreenshotPath is not null)
        {
            int captureFrame = options.ScreenshotFrame;
            if (options.SmokeFrames is int smokeFrames && smokeFrames < captureFrame)
            {
                captureFrame = smokeFrames;
            }

            _screenshotFrame = Math.Max(1, captureFrame);
        }

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        Window.Title = BuildInfo.DisplayName;
        InactiveSleepTime = TimeSpan.FromMilliseconds(30);
    }

    /// <summary>Process exit code; non zero when mandatory game data was missing.</summary>
    public int ExitCode { get; private set; }

    protected override void Initialize()
    {
        GameLog.Info(BuildInfo.DisplayName + " starting");
        LoadGameData();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _presenter = new MonoGamePresenter(GraphicsDevice, ScreenBuffer.DefaultSize);
        _presenter.SetPalette(_palette);

        _frame = new ScreenBuffer(ScreenBuffer.DefaultSize, _palette);
        CreateTextRenderer();
        _scene = new DiagnosticsScene(_frame, _text, _supportsCjk);

        if (_options.SmokeFrames is int frames)
        {
            GameLog.Info($"Smoke mode: the client will exit after {frames} rendered frames.");
        }

        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        _timer.Tick(elapsed);
        _scene?.Update(elapsed);

        KeyboardState keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.F11))
        {
            ToggleFullscreen();
        }

        if (keyboard.IsKeyDown(Keys.F12))
        {
            _screenshotRequested = true;
        }

        if (_options.SmokeFrames is int maxFrames && _timer.FrameCount >= maxFrames)
        {
            GameLog.Info($"Smoke mode finished after {_timer.FrameCount} frames ({_timer.FramesPerSecond:0.0} FPS).");
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_presenter is null || _scene is null)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
            return;
        }

        _scene.DrawFrame();
        _presenter.Upload(_frame);

        _presenter.BeginLogicalFrame();
        _presenter.DrawFrameTexture();

        if (_spriteBatch is not null)
        {
            DiagnosticsState state = new(
                _assets?.DataDirectory,
                _assets?.BaseArchive.Count ?? 0,
                _assets?.ExpansionArchive?.Count ?? 0,
                _text?.Name ?? "none",
                _presenter.OutputSize,
                _presenter.Viewport.Scale,
                _timer.FramesPerSecond,
                _timer.FrameCount);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            _scene.DrawOverlay(_spriteBatch, state);
            _spriteBatch.End();
        }

        CaptureScreenshot();

        _presenter.EndFrame();

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _text?.Dispose();
        _text = null;

        _presenter?.Dispose();
        _presenter = null;

        _spriteBatch?.Dispose();
        _spriteBatch = null;

        _assets?.Dispose();
        _assets = null;

        base.UnloadContent();
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
        foreach (FontChoice candidate in SystemFontLocator.EnumerateCandidates())
        {
            try
            {
                _text = new FontStashTextRenderer(candidate.Path);
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

    private void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = _fullscreen;
        _graphics.ApplyChanges();
        GameLog.Info(_fullscreen ? "Switched to fullscreen." : "Switched to windowed mode.");
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
            _presenter!.SaveLogicalFrame(path);
            GameLog.Info($"Screenshot saved to '{Path.GetFullPath(path)}'.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            GameLog.Error("Screenshot could not be written", exception);
            ExitCode = 1;
        }
    }
}
