using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Client.Desktop.Fonts;
using FHeroes2.Client.Desktop.Localization;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Widgets;

namespace FHeroes2.Client.Desktop.Scenes;

/// <summary>
/// The main menu of Heroes II: the street scene of <c>HEROES.ICN</c>, the five hanging signs of
/// <c>BTNSHNGL.ICN</c> on top of it and a one line status bar drawn with the game fonts.
/// </summary>
/// <remarks>
/// <para>
/// The composition follows <c>Game::MainMenu()</c> (M2 of docs/PLAN.md): <c>HEROES.ICN</c> is one
/// 640x480 opaque sprite and every sign is a sprite whose anchor in the ICN header <i>is</i> its
/// position, so the layout needs no coordinates of its own. Each sign owns three frames: the
/// released look (<c>BTNSHNGL</c> 1, 5, 9, 13, 17), the highlighted look (+1) and the pressed look
/// (+2), exactly the offsets the reference engine passes to <c>fheroes2::Button</c>.
/// </para>
/// <para>
/// All text is drawn into the indexed frame with <see cref="GlyphText"/> and the fonts of
/// <see cref="GameFonts"/>, so a Chinese tooltip and the English sign art share one palette. The
/// status bar at the bottom is a port addition: the original shows tooltips on right click, and
/// the M0 GPU text overlay has to be retired (ADR-022).
/// </para>
/// <para>
/// Hovering the door in the bottom left corner lights it up (<see cref="DoorHighlight"/>): the part
/// of sprite 18 of <c>SHNGANIM.ICN</c> that sits below the lantern, which is how
/// <c>Game::MainMenu()</c> points at the settings entrance.
/// </para>
/// </remarks>
public sealed class MainMenuScene : IScene
{
    /// <summary>The full screen menu artwork, one opaque 640x480 sprite.</summary>
    public const string BackgroundAssetName = "HEROES.ICN";

    /// <summary>The table that holds the five signs, four frames each.</summary>
    public const string ButtonAssetName = "BTNSHNGL.ICN";

    /// <summary>Sprite index of the "New Game" sign; the highlighted and pressed frames follow it.</summary>
    public const int NewGameFrame = 1;

    public const int LoadGameFrame = 5;

    public const int HighScoresFrame = 9;

    public const int CreditsFrame = 13;

    public const int QuitFrame = 17;

    /// <summary>The lantern glow that flickers next to the street scene.</summary>
    public const string AnimationAssetName = "SHNGANIM.ICN";

    /// <summary>Sprite 0 of <c>SHNGANIM.ICN</c>: the unlit lantern, drawn once behind the glow.</summary>
    public const int LanternBaseFrame = 0;

    /// <summary>First glow frame. <c>ICN::getAnimatedIcnIndex</c> maps it to <c>1 + n % 39</c>.</summary>
    public const int LanternFirstFrame = 1;

    /// <summary>Number of glow frames in <c>SHNGANIM.ICN</c> (39 sprites after the base one).</summary>
    public const int LanternFrameCount = 39;

    /// <summary>
    /// One glow frame every <c>Game::DelayType::MAIN_MENU_DELAY</c> (250 ms), so the cycle takes
    /// 9.75 s.
    /// </summary>
    public static readonly TimeSpan LanternFrameDuration = TimeSpan.FromMilliseconds(250);

    /// <summary>Height of the status bar, which is exactly one line of <c>SMALFONT.ICN</c>.</summary>
    public const int StatusBarHeight = 11;

    private const int StatusBarPadding = 8;

    private const int StatusBarGap = 10;

    private static readonly MenuEntry[] Menu =
    [
        new(MainMenuAction.NewGame, NewGameFrame, "New Game", "Start a single or multi-player game."),
        new(MainMenuAction.LoadGame, LoadGameFrame, "Load Game", "Load a previously saved game."),
        new(MainMenuAction.HighScores, HighScoresFrame, "High Scores", "View the high scores screen."),
        new(MainMenuAction.Credits, CreditsFrame, "Credits", "View the credits screen."),
        new(
            MainMenuAction.Quit,
            QuitFrame,
            "Quit",
            "Quit Heroes of Might and Magic II and return to the operating system."),
    ];

    private readonly ScreenBuffer _frame;
    private readonly Sprite _background;
    private readonly Sprite? _lanternBase;
    private readonly IcnAnimation? _lantern;
    private readonly DoorHighlight? _door;
    private readonly Button[] _buttons;
    private readonly GameFonts _fonts;
    private readonly IReadOnlyList<GameText> _languages;
    private readonly Rect _statusBar;
    private readonly byte _statusBarColor;

    private int _languageIndex;
    private int _pressedButton = -1;
    private Point? _cursor;
    private bool _leftButtonDown;
    private string? _status;

    private MainMenuScene(
        ScreenBuffer frame,
        Sprite background,
        Sprite? lanternBase,
        IcnAnimation? lantern,
        DoorHighlight? door,
        Button[] buttons,
        GameFonts fonts,
        IReadOnlyList<GameText> languages,
        string? culture)
    {
        _frame = frame;
        _background = background;
        _lanternBase = lanternBase;
        _lantern = lantern;
        _door = door;
        _buttons = buttons;
        _fonts = fonts;
        _languages = languages;
        _statusBar = new Rect(0, frame.Size.Height - StatusBarHeight, frame.Size.Width, StatusBarHeight);
        _statusBarColor = frame.Palette.FindDarkestIndex();
        _languageIndex = IndexOfCulture(languages, culture);
    }

    /// <summary>The language currently used for tooltips and the status bar.</summary>
    public GameText Text => _languages[_languageIndex];

    /// <summary>Languages the menu can switch between; the first one is English.</summary>
    public IReadOnlyList<GameText> Languages => _languages;

    /// <summary>The status bar rectangle at the bottom of the logical frame.</summary>
    public Rect StatusBarArea => _statusBar;

    /// <summary>
    /// Builds the menu from the game data.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when the artwork or the fonts are missing; the host then keeps the
    /// diagnostics screen instead of showing a half empty menu.
    /// </returns>
    public static MainMenuScene? TryCreate(
        ScreenBuffer frame, AssetBundle? assets, GameFonts? fonts, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (assets is null || fonts is null)
        {
            GameLog.Warn("The main menu needs the game data and the bitmap fonts; keeping the diagnostics screen.");
            return null;
        }

        try
        {
            IcnSpriteFile? background = assets.TryReadIcn(BackgroundAssetName);
            IcnSpriteFile? signs = assets.TryReadIcn(ButtonAssetName);

            if (background is null || background.Count == 0 || signs is null)
            {
                GameLog.Warn(
                    $"The main menu artwork is missing ('{BackgroundAssetName}' or '{ButtonAssetName}').");
                return null;
            }

            int lastFrame = Menu[^1].Frame + 2;
            if (signs.Count <= lastFrame)
            {
                GameLog.Warn(
                    $"'{ButtonAssetName}' has {signs.Count} sprites but the menu needs {lastFrame + 1}.");
                return null;
            }

            Button[] buttons = new Button[Menu.Length];
            for (int index = 0; index < Menu.Length; ++index)
            {
                int released = Menu[index].Frame;
                buttons[index] = new Button(
                    signs.Sprites[released],
                    signs.Sprites[released + 1],
                    signs.Sprites[released + 2]);
            }

            IcnSpriteFile? lanternFile = assets.TryReadIcn(AnimationAssetName);
            (Sprite? lanternBase, IcnAnimation? lantern) = CreateLantern(lanternFile);
            DoorHighlight? door = CreateDoor(lanternFile);

            IReadOnlyList<GameText> languages = SelectLanguages(GameText.LoadAll(assets.DataDirectory), fonts);

            MainMenuScene scene = new(
                frame, background.Sprites[0], lanternBase, lantern, door, buttons, fonts, languages, culture);

            GameLog.Info(
                $"Main menu ready: {BackgroundAssetName} {background.Sprites[0].Width}x" +
                $"{background.Sprites[0].Height}, {Menu.Length} signs from {ButtonAssetName}, " +
                (lantern is null
                    ? "no lantern animation, "
                    : $"lantern glow {LanternFrameCount} frames every " +
                      $"{LanternFrameDuration.TotalMilliseconds:0} ms, ") +
                (door is null ? "settings door stays dark, " : "settings door lights up on hover, ") +
                $"language '{scene.Text.Culture}' ({scene.Text.TranslatedEntries} entries).");

            return scene;
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The main menu could not be created: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Whether the lantern glow animates. The host clears it for reproducible screenshots
    /// (<c>--freeze-animation</c>); the first glow frame then stays on screen.
    /// </summary>
    public bool AnimationEnabled { get; set; } = true;

    /// <summary>
    /// Advances the lantern glow. The scene keeps its own clock so that a slow frame does not
    /// stretch the animation: one frame per 250 ms, 39 frames per cycle, like the original.
    /// </summary>
    public void Update(double elapsedSeconds)
    {
        if (AnimationEnabled && elapsedSeconds > 0.0)
        {
            _lantern?.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        }
    }

    /// <summary>
    /// Draws the background, the lantern, the lit door, the signs in their current state and the
    /// status bar, in the order of <c>Game::MainMenu()</c>: the glow and the door light go below the
    /// signs so that a lit pixel can never repaint a sign.
    /// </summary>
    public void DrawFrame(in DiagnosticsState state)
    {
        PixelBuffer pixels = _frame.Pixels;

        ImageOps.BlitSprite(pixels, _background);

        if (_lanternBase is not null)
        {
            ImageOps.BlitSprite(pixels, _lanternBase);
        }

        if (_lantern is not null)
        {
            ImageOps.BlitSprite(pixels, _lantern.Current);
        }

        // Every frame, not once per lantern tick like the reference: the scene is recomposed from
        // the background anyway, and keeping the light out of the animation clock is what makes a
        // --freeze-animation screenshot of the lit door reproducible.
        _door?.Draw(pixels, _cursor);

        foreach (Button button in _buttons)
        {
            button.Draw(pixels);
        }

        DrawStatusBar(pixels, state);

        _frame.InvalidateAll();
    }

    /// <summary>The menu draws all of its text with the game fonts, so there is no overlay.</summary>
    public void DrawOverlay(in DiagnosticsState state) => _ = state;

    /// <summary>Sets the cursor position in logical pixels, or <see langword="null"/> when it left.</summary>
    public void SetCursor(Point? cursor)
    {
        if (_cursor == cursor)
        {
            return;
        }

        _cursor = cursor;
        UpdateButtonStates();
    }

    /// <summary>
    /// Records the left mouse button. A release inside the sign that was pressed returns the action
    /// of that sign, which is how a click is reported to the host.
    /// </summary>
    public MainMenuAction? SetLeftButton(bool down)
    {
        _leftButtonDown = down;
        UpdateButtonStates();

        if (down)
        {
            _pressedButton = IndexAt(_cursor);
            return null;
        }

        int released = _pressedButton;
        _pressedButton = -1;

        if (released < 0 || IndexAt(_cursor) != released)
        {
            return null;
        }

        return Choose(Menu[released]);
    }

    /// <summary>Selects a sign through its hot key (<c>N</c>, <c>L</c>, <c>H</c>, <c>C</c>, <c>Q</c>).</summary>
    public MainMenuAction? Select(char hotKey)
    {
        MainMenuAction? action = char.ToUpperInvariant(hotKey) switch
        {
            'N' => MainMenuAction.NewGame,
            'L' => MainMenuAction.LoadGame,
            'H' => MainMenuAction.HighScores,
            'C' => MainMenuAction.Credits,
            'Q' => MainMenuAction.Quit,
            _ => null,
        };

        if (action is not { } requested)
        {
            return null;
        }

        foreach (MenuEntry entry in Menu)
        {
            if (entry.Action == requested)
            {
                return Choose(entry);
            }
        }

        return null;
    }

    /// <summary>Switches to the next available language and returns it.</summary>
    public GameText NextLanguage()
    {
        if (_languages.Count > 1)
        {
            _languageIndex = (_languageIndex + 1) % _languages.Count;
            GameLog.Info($"Interface language: {Text.Culture} ({Text.NativeName}).");
        }

        return Text;
    }

    /// <summary>
    /// Loads the lantern: sprite 0 is drawn once and the following 39 sprites are the glow that
    /// flickers. The menu works without it, so missing artwork only costs the animation.
    /// </summary>
    /// <param name="file">
    /// <c>SHNGANIM.ICN</c>, read once by the caller because the door light comes from the same table.
    /// </param>
    private static (Sprite? Base, IcnAnimation? Animation) CreateLantern(IcnSpriteFile? file)
    {
        int required = LanternFirstFrame + LanternFrameCount;

        if (file is null || file.Count < required)
        {
            GameLog.Warn(
                $"'{AnimationAssetName}' is missing or has {file?.Count ?? 0} sprites instead of " +
                $"{required}; the lantern will not flicker.");
            return (null, null);
        }

        return (
            file.Sprites[LanternBaseFrame],
            IcnAnimation.FromFile(file, LanternFirstFrame, LanternFrameCount, LanternFrameDuration));
    }

    /// <summary>
    /// Loads the light of the settings door: sprite 18 of the same table, recoloured so that it does
    /// not cycle. The menu works without it, so missing artwork only costs the highlight.
    /// </summary>
    private static DoorHighlight? CreateDoor(IcnSpriteFile? file)
    {
        if (file is null || file.Count <= DoorHighlight.HighlightFrame)
        {
            GameLog.Warn(
                $"'{AnimationAssetName}' has {file?.Count ?? 0} sprites instead of " +
                $"{DoorHighlight.HighlightFrame + 1}; the settings door will not light up.");
            return null;
        }

        Sprite frame = file.Sprites[DoorHighlight.HighlightFrame];
        if (frame.Height <= DoorHighlight.SourceOffsetY)
        {
            GameLog.Warn(
                $"Sprite {DoorHighlight.HighlightFrame} of '{AnimationAssetName}' is " +
                $"{frame.Width}x{frame.Height}; the settings door will not light up.");
            return null;
        }

        return new DoorHighlight(DoorHighlight.CreateHighlight(frame));
    }

    private static IReadOnlyList<GameText> SelectLanguages(IReadOnlyList<GameText> languages, GameFonts fonts)
    {
        // Without a CJK fallback Chinese tooltips would be drawn as '?' glyphs, so the menu stays
        // English instead of showing mojibake.
        if (fonts.SupportsCjk || languages.Count <= 1)
        {
            return languages;
        }

        GameLog.Warn("No CJK glyphs are available; the main menu stays in English.");
        return [languages[0]];
    }

    private static int IndexOfCulture(IReadOnlyList<GameText> languages, string? culture)
    {
        if (!string.IsNullOrWhiteSpace(culture))
        {
            for (int index = 0; index < languages.Count; ++index)
            {
                if (string.Equals(languages[index].Culture, culture, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            GameLog.Warn($"The requested language '{culture}' is not installed; using the default.");
        }

        // A localized catalog is what makes the CJK path visible, so it wins over English.
        for (int index = 0; index < languages.Count; ++index)
        {
            if (languages[index].IsLocalized)
            {
                return index;
            }
        }

        return 0;
    }

    private void DrawStatusBar(PixelBuffer pixels, in DiagnosticsState state)
    {
        ImageOps.FillRect(pixels, _statusBar, _statusBarColor);

        IGlyphSource font = _fonts.Small;
        int top = _statusBar.Y;

        string right = FormattableString.Invariant(
            $"{state.FramesPerSecond:0} FPS  |  {Text.NativeName} (F2)  |  v{BuildInfo.Version}");
        int rightX = Math.Max(_statusBar.Right - StatusBarPadding - GlyphText.Measure(font, right), StatusBarPadding);
        GlyphText.DrawLine(font, pixels, new Point(rightX, top), right);

        if (StatusText() is not { Length: > 0 } left)
        {
            return;
        }

        // The left half shows the tooltip of the sign under the cursor, or the last action.
        int width = Math.Max(rightX - StatusBarGap - StatusBarPadding, 0);
        Rect line = new(StatusBarPadding, top, width, _statusBar.Height);
        GlyphText.DrawLine(font, pixels, new Point(line.X, top), left, line);
    }

    private string? StatusText()
    {
        for (int index = 0; index < _buttons.Length; ++index)
        {
            if (_buttons[index].State == ButtonState.Hovered)
            {
                return Text.Get(Menu[index].TooltipId);
            }
        }

        return _status;
    }

    private MainMenuAction Choose(in MenuEntry entry)
    {
        string label = Text.Get(entry.LabelId);

        if (entry.Action == MainMenuAction.Quit)
        {
            GameLog.Info($"Main menu: '{label}' selected, leaving the game.");
            return entry.Action;
        }

        // M3 onwards replaces this with the real screen; until then the status bar says so.
        _status = $"{label} - M3";
        GameLog.Info($"Main menu: '{label}' is not implemented before M3.");
        return entry.Action;
    }

    private void UpdateButtonStates()
    {
        foreach (Button button in _buttons)
        {
            button.Update(_cursor, _leftButtonDown);
        }
    }

    private int IndexAt(Point? point)
    {
        if (point is not { } position)
        {
            return -1;
        }

        for (int index = 0; index < _buttons.Length; ++index)
        {
            if (_buttons[index].HitTest(position))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>One sign of the menu: its action, its ICN frame and the msgids of fheroes2.</summary>
    private readonly record struct MenuEntry(MainMenuAction Action, int Frame, string LabelId, string TooltipId);
}
