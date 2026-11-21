using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game.H2D;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The main menu of Heroes II: the street scene of <c>HEROES.ICN</c>, the five hanging signs of
/// <c>BTNSHNGL.ICN</c> on top of it, a one line status bar drawn with the game fonts and the message
/// box the original shows while the right mouse button is held.
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
/// <para>
/// Holding the right mouse button over a sign or over the door shows the message box of
/// <c>showStandardTextMessage( ..., Dialog::ZERO )</c> (<see cref="SetRightButton"/>); releasing it
/// removes the box again, so the tooltip never outlives the button the way it does not in the
/// original either.
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

    /// <summary>
    /// The editor button of <c>BTNSHNGL.ICN</c>, <c>EDITOR_DEFAULT</c> of <c>Game::MainMenu()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original archive has twenty sprites (0..19) and no editor frame. The three frames 20, 21 and
    /// 22 are appended at load time from <c>resurrection.h2d</c> by <c>case ICN::BTNSHNGL</c> of
    /// <c>game_assets.cpp:3471</c>, together with the little easel that is painted into the street
    /// scene (<see cref="EditorIconImageName"/>). This is why the button cannot be found in the ICN file
    /// itself.
    /// </para>
    /// <para>
    /// Only the painted sign in <see cref="EditorFallbackArea"/> is a port addition, used when the
    /// archive is missing.
    /// </para>
    /// </remarks>
    public const int EditorFrame = 20;

    /// <summary>The released look of the editor button, <c>main_menu_editor_released_button.image</c>.</summary>
    public const string EditorReleasedImageName = "main_menu_editor_released_button.image";

    /// <summary>The highlighted look, <c>main_menu_editor_highlighted_button.image</c>.</summary>
    public const string EditorHighlightedImageName = "main_menu_editor_highlighted_button.image";

    /// <summary>The pressed look, <c>main_menu_editor_pressed_button.image</c>.</summary>
    public const string EditorPressedImageName = "main_menu_editor_pressed_button.image";

    /// <summary>The easel painted into the street scene, <c>main_menu_editor_icon.image</c>.</summary>
    public const string EditorIconImageName = "main_menu_editor_icon.image";

    /// <summary>msgid of the editor button, the <c>_( "Editor" )</c> of the reference.</summary>
    public const string EditorLabelId = "Editor";

    /// <summary>msgid of the editor tooltip, <c>_( "Create new or modify existing maps." )</c>.</summary>
    public const string EditorTooltipId = "Create new or modify existing maps.";

    /// <summary>
    /// Where the editor button sits when the artwork has no frame for it. Measured from the reference's
    /// own screenshot: the little easel at the bottom right, right of the potted plant and clear of the
    /// signs, of the settings door (0..86, 425..469) and of the status bar.
    /// </summary>
    public static readonly Rect EditorFallbackArea = new(527, 364, 76, 33);

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

    /// <summary>Tooltip title of the settings door, the msgid <c>Game::MainMenu()</c> passes.</summary>
    public const string DoorLabelId = "Game Settings";

    /// <summary>Tooltip text of the settings door, the msgid <c>Game::MainMenu()</c> passes.</summary>
    public const string DoorTooltipId = "Change language, resolution and settings of the game.";

    /// <summary>Title of the settings dialog, the msgid of the reference window.</summary>
    public const string SettingsTitleId = "Settings";

    /// <summary>Label of the language row, the msgid of the reference settings dialog.</summary>
    public const string SettingsLanguageId = "Language";

    /// <summary>Width of the settings dialog content, close to the 288 pixel window of the reference.</summary>
    public const int SettingsContentWidth = 300;

    /// <summary><c>SDL_Keycode.ESCAPE</c>, which arrives as a character through the hot key path.</summary>
    private const char EscapeCharacter = '\u001b';

    /// <summary><c>SDL_Keycode.RETURN</c>.</summary>
    private const char EnterCharacter = '\r';

    /// <summary><c>SDL_Keycode.RETURN</c> as a line feed, for terminals and tests.</summary>
    private const char LineFeedCharacter = '\n';

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
    private readonly FrameBoxChrome? _messageChrome;
    private readonly Button[] _buttons;
    private readonly Button? _editorSign;
    private readonly GameFonts _fonts;
    private readonly IReadOnlyList<GameText> _languages;
    private readonly Rect _statusBar;
    private readonly byte _statusBarColor;
    private readonly SettingsArtwork? _settingsArtwork;
    private readonly ScenarioDialogArtwork? _scenarioArtwork;
    private readonly string? _dataDirectory;

    private int _languageIndex;
    private int _pressedButton = -1;
    private Point? _cursor;
    private bool _leftButtonDown;
    private bool _doorPressed;
    private bool _editorPressed;
    private ButtonState _editorState = ButtonState.Released;
    private string? _status;
    private MessageBox? _tip;
    private SettingsDialog? _settings;
    private IReadOnlyList<Mp2FileInfo>? _maps;
    private ScenarioSelectionDialog? _scenario;
    private Mp2FileInfo? _confirmedScenario;

    private MainMenuScene(
        ScreenBuffer frame,
        Sprite background,
        Sprite? lanternBase,
        IcnAnimation? lantern,
        DoorHighlight? door,
        FrameBoxChrome? messageChrome,
        Button[] buttons,
        Button? editorSign,
        GameFonts fonts,
        IReadOnlyList<GameText> languages,
        string? culture,
        SettingsArtwork? settingsArtwork,
        ScenarioDialogArtwork? scenarioArtwork,
        string? dataDirectory)
    {
        _frame = frame;
        _background = background;
        _lanternBase = lanternBase;
        _lantern = lantern;
        _door = door;
        _messageChrome = messageChrome;
        _buttons = buttons;
        _editorSign = editorSign;
        _fonts = fonts;
        _languages = languages;
        _settingsArtwork = settingsArtwork;
        _scenarioArtwork = scenarioArtwork;
        _dataDirectory = dataDirectory;
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
    /// The area of the editor button: the artwork anchor when the data has the Price of Loyalty
    /// frames, the painted corner sign otherwise.
    /// </summary>
    public Rect EditorArea => _editorSign?.Area ?? EditorFallbackArea;

    /// <summary>Whether the editor button is painted by the menu instead of taken from the artwork.</summary>
    public bool IsEditorSignPainted => _editorSign is null;

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

            // The editor button: frames 20..22 are not in the ICN file at all, game_assets.cpp adds
            // them from resurrection.h2d. Missing artwork is not an error - the menu paints its own
            // sign in the corner instead of dropping the entry.
            Button? editorSign = CreateEditorButton(signs);
            Sprite streetScene = CreateStreetScene(background.Sprites[0]);

            IcnSpriteFile? lanternFile = assets.TryReadIcn(AnimationAssetName);
            (Sprite? lanternBase, IcnAnimation? lantern) = CreateLantern(lanternFile);
            DoorHighlight? door = CreateDoor(lanternFile);
            FrameBoxChrome? messageChrome = CreateMessageChrome(assets);

            IReadOnlyList<GameText> languages = SelectLanguages(GameText.LoadAll(assets.DataDirectory), fonts);

            ScenarioDialogArtwork? scenarioArtwork = ScenarioArtwork.TryCreate(assets);

            MainMenuScene scene = new(
                frame, streetScene, lanternBase, lantern, door, messageChrome, buttons, editorSign, fonts,
                languages, culture, SettingsArtwork.TryCreate(assets), scenarioArtwork, assets.DataDirectory);

            GameLog.Info(
                $"Main menu ready: {BackgroundAssetName} {background.Sprites[0].Width}x" +
                $"{background.Sprites[0].Height}, {Menu.Length} signs from {ButtonAssetName}, " +
                (lantern is null
                    ? "no lantern animation, "
                    : $"lantern glow {LanternFrameCount} frames every " +
                      $"{LanternFrameDuration.TotalMilliseconds:0} ms, ") +
                (door is null ? "settings door stays dark, " : "settings door lights up on hover, ") +
                (editorSign is null ? "editor sign drawn here, " : "editor sign from resurrection.h2d, ") +
                (messageChrome is null ? "no tooltips, " : "right click tooltips, ") +
                (scenarioArtwork is null ? "no scenario selection, " : "scenario selection ready, ") +
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
    /// Builds the editor button, the <c>EDITOR_DEFAULT</c> entry of <c>Game::MainMenu()</c>.
    /// </summary>
    /// <remarks>
    /// <c>case ICN::BTNSHNGL</c> of <c>game_assets.cpp</c> appends three frames to the table it has just
    /// read: the original <c>BTNSHNGL.ICN</c> stops at frame 19, so the editor look always comes from
    /// <c>resurrection.h2d</c>. The anchors are stored in the archive, which is why the button lands on
    /// the easel of the street scene without a single coordinate here. An asset pack that already
    /// carries the frames (a table longer than 22 sprites) is used as it is.
    /// </remarks>
    /// <param name="signs">The loaded <c>BTNSHNGL.ICN</c>.</param>
    /// <returns>The button, or <see langword="null"/> when neither the table nor the archive has it.</returns>
    private static Button? CreateEditorButton(IcnSpriteFile signs)
    {
        if (signs.Count > EditorFrame + 2)
        {
            return new Button(
                signs.Sprites[EditorFrame],
                signs.Sprites[EditorFrame + 1],
                signs.Sprites[EditorFrame + 2]);
        }

        // The reference appends the frames only to the twenty sprite table it knows; any other length
        // is data the port does not understand, and guessing would put the button in the wrong place.
        if (signs.Count != EditorFrame ||
            !H2DResources.TryReadCurrentImage(EditorReleasedImageName, out Sprite? released) ||
            !H2DResources.TryReadCurrentImage(EditorHighlightedImageName, out Sprite? highlighted) ||
            !H2DResources.TryReadCurrentImage(EditorPressedImageName, out Sprite? pressed))
        {
            return null;
        }

        return new Button(released, highlighted, pressed);
    }

    /// <summary>
    /// The street scene with the easel of the editor painted into it, <c>case ICN::HEROES</c> of
    /// <c>game_assets.cpp</c>.
    /// </summary>
    /// <remarks>
    /// The reference blits <c>main_menu_editor_icon.image</c> into its cached copy of the background,
    /// at the anchor stored in the archive. Here the background sprite belongs to the ICN cache, so a
    /// clone is painted instead of the cached sprite and the scene owns the result; leaving the cache
    /// alone keeps a second menu creation from stacking the easel twice.
    /// </remarks>
    /// <param name="artwork">Sprite 0 of <c>HEROES.ICN</c>.</param>
    /// <returns>The scene to draw, with the easel when the archive has it.</returns>
    private static Sprite CreateStreetScene(Sprite artwork)
    {
        if (!H2DResources.TryReadCurrentImage(EditorIconImageName, out Sprite? icon))
        {
            return artwork;
        }

        Sprite scene = artwork.Clone();
        ImageOps.BlitSprite(scene, new Point(icon.OffsetX, icon.OffsetY), icon);
        return scene;
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

        DrawEditorSign(pixels);

        DrawStatusBar(pixels, state);

        // The tooltip is modal in the original: it sits on top of everything, including the signs.
        _tip?.Draw(pixels);

        // The modal windows close the menu, so they are painted last: the settings dialog, then the
        // scenario list. The two can never be open at the same time, the order only keeps the code
        // in the order the host opens them.
        _settings?.Draw(pixels);
        _scenario?.Draw(pixels);

        _frame.InvalidateAll();
    }

    /// <summary>The menu draws all of its text with the game fonts, so there is no overlay.</summary>
    public void DrawOverlay(in DiagnosticsState state) => _ = state;

    /// <summary>Sets the cursor position in logical pixels, or <see langword="null"/> when it left.</summary>
    public void SetCursor(Point? cursor)
    {
        // A modal window owns the cursor while it is open.
        if (_settings is not null)
        {
            _settings.SetCursor(cursor);
            return;
        }

        if (_scenario is not null)
        {
            _scenario.SetCursor(cursor);
            return;
        }

        // The open tooltip freezes the menu: the reference sits inside the dialog loop, so no hover
        // state - and no door light - changes until the box is gone.
        if (_tip is not null)
        {
            return;
        }

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

        // The settings dialog owns the mouse while it is open; clicking its OK button applies the
        // changes, everything else stays inside the dialog.
        if (_settings is not null)
        {
            if (_settings.SetLeftButton(down) == DialogButton.Ok)
            {
                ApplySettings();
            }

            return null;
        }

        // The scenario list owns the mouse while it is open: OK picks the map, every other click
        // only moves the selection, the size filter or the scroll thumb.
        if (_scenario is not null)
        {
            if (_scenario.SetLeftButton(down) == DialogButton.Ok)
            {
                ConfirmScenario();
            }

            return null;
        }

        // While the tooltip is up the reference engine sits inside showStandardTextMessage() and
        // never sees the left button, so a click cannot pick a sign. The button state itself is
        // still tracked, otherwise a press that happened before the tooltip opened would stick.
        if (_tip is not null)
        {
            _pressedButton = -1;
            return null;
        }

        UpdateButtonStates();

        if (down)
        {
            _pressedButton = IndexAt(_cursor);

            // The door is not a sign: it opens the settings dialog, the way the reference treats the
            // bottom left corner of the street scene.
            _doorPressed = _pressedButton < 0 && (_door?.IsHovered(_cursor) ?? false);

            // The editor button is neither: a press on it only arms the release below.
            _editorPressed = !_doorPressed && _cursor is { } pressed && EditorArea.Contains(pressed);
            return null;
        }

        if (_doorPressed)
        {
            _doorPressed = false;

            if (_door?.IsHovered(_cursor) == true)
            {
                OpenSettings();
            }

            return null;
        }

        if (_editorPressed)
        {
            _editorPressed = false;

            if (_cursor is { } cursor && EditorArea.Contains(cursor))
            {
                GameLog.Info($"Main menu: '{Text.Get(EditorLabelId)}' opens the map editor.");
                return MainMenuAction.Editor;
            }

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

    /// <summary>Whether a tooltip message is on screen, i.e. the right mouse button is held over a sign.</summary>
    public MessageBox? Tip => _tip;

    /// <summary>
    /// Tracks the right mouse button of the original tooltips: pressing it over a sign (or over the
    /// settings door) opens the message box of that area, releasing it closes the box again, exactly
    /// like <c>Game::MainMenu()</c> calls <c>showStandardTextMessage( ..., Dialog::ZERO )</c> while
    /// the button is down.
    /// </summary>
    /// <param name="down">Whether the right mouse button is currently held.</param>
    public void SetRightButton(bool down) => _tip = down && _settings is null ? CreateTip() : null;

    /// <summary>
    /// Selects a sign through its hot key (<c>N</c>, <c>L</c>, <c>H</c>, <c>C</c>, <c>Q</c>), or the
    /// editor through <c>E</c>.
    /// </summary>
    public MainMenuAction? Select(char hotKey)
    {
        // While the settings dialog is open the keyboard belongs to it: Escape closes it without
        // applying, Enter is the OK hot key.
        if (_settings is not null)
        {
            return hotKey switch
            {
                EscapeCharacter => CloseSettings(),
                EnterCharacter or LineFeedCharacter => ConfirmSettings(),
                _ => null,
            };
        }

        // While the scenario list is open the keyboard belongs to it as well: Escape closes it
        // without a choice, Enter is the OKAY hot key. The arrow, page, Home and End keys are not
        // characters, the host routes them through MoveScenarioSelection / MoveScenarioPage.
        if (_scenario is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter:
                    CloseScenario();
                    break;

                case EnterCharacter or LineFeedCharacter:
                    ConfirmScenario();
                    break;

                default:
                    break;
            }

            return null;
        }

        // A hot key is swallowed by the open tooltip, like every key press inside the dialog loop.
        if (_tip is not null)
        {
            return null;
        }

        MainMenuAction? action = char.ToUpperInvariant(hotKey) switch
        {
            'N' => MainMenuAction.NewGame,
            'L' => MainMenuAction.LoadGame,
            'H' => MainMenuAction.HighScores,
            'C' => MainMenuAction.Credits,
            'Q' => MainMenuAction.Quit,
            'E' => MainMenuAction.Editor,
            _ => null,
        };

        if (action is not { } requested)
        {
            return null;
        }

        // The editor is not one of the signs, so it has no MenuEntry to choose.
        if (requested == MainMenuAction.Editor)
        {
            GameLog.Info($"Main menu: '{Text.Get(EditorLabelId)}' opens the map editor.");
            return requested;
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

    /// <summary>The open settings dialog, or <see langword="null"/> when the door has not been used.</summary>
    public SettingsDialog? Settings => _settings;

    /// <summary>
    /// Opens the settings dialog on top of the menu, which is what a click on the door of the street
    /// scene does in <c>Game::MainMenu()</c>.
    /// </summary>
    /// <remarks>
    /// The window is sized from the fonts and from the height of the OK button, then centred, so the
    /// dialog grows by itself when rows are added. Nothing is opened when the archive lacks the
    /// window or button artwork.
    /// </remarks>
    /// <returns><see langword="true"/> when the dialog is now open.</returns>
    public bool OpenSettings()
    {
        if (_settingsArtwork is null || _settings is not null)
        {
            return false;
        }

        string title = Text.Get(SettingsTitleId);
        IGlyphSource titleFont = _fonts.Yellow;
        IGlyphSource optionFont = _fonts.Normal;

        int titleHeight = GlyphText.WrapLines(titleFont, title, SettingsContentWidth).Count * titleFont.LineHeight;
        int contentHeight = SettingsDialog.GetRequiredHeight(
            titleHeight, optionFont.LineHeight, 1, _settingsArtwork.OkayHeight);

        StandardWindow window = StandardWindow.CreateCentered(
            _settingsArtwork.Chrome, new Size(SettingsContentWidth, contentHeight), true, _frame.Size);

        ButtonGroup buttons = ButtonGroup.CreateStandard(window.ActiveArea, DialogButton.Ok, _settingsArtwork);

        SettingsOption language = new(
            Text.Get(SettingsLanguageId),
            [.. _languages.Select(available => available.NativeName)],
            _languageIndex);

        _settings = new SettingsDialog(window, buttons, titleFont, optionFont, title, [language]);

        GameLog.Info(
            $"Settings dialog opened: language '{Text.Culture}', {_languages.Count} available, " +
            $"content {SettingsContentWidth}x{contentHeight}.");

        return true;
    }

    /// <summary>The open scenario list, or <see langword="null"/> when New Game has not been used.</summary>
    public ScenarioSelectionDialog? Scenario => _scenario;

    /// <summary>Whether a modal window (the settings dialog or the scenario list) owns the input.</summary>
    public bool HasModalDialog => _settings is not null || _scenario is not null;

    /// <summary>
    /// Opens the scenario selection window of the main menu, the <c>Dialog::SelectScenario()</c> the
    /// reference shows after <c>NEW_GAME</c>.
    /// </summary>
    /// <remarks>
    /// The map list is read on the first call and kept for the rest of the session, which is how the
    /// reference caches <c>Maps::getValidMaps</c> while the dialog is up. Nothing is opened when the
    /// artwork is missing, when another modal window is already open or when no map is installed.
    /// </remarks>
    /// <returns><see langword="true"/> when the dialog is now open.</returns>
    public bool OpenScenario()
    {
        if (_scenarioArtwork is null || HasModalDialog)
        {
            return false;
        }

        IReadOnlyList<Mp2FileInfo> maps = Maps;

        if (maps.Count == 0)
        {
            GameLog.Warn("No playable map was found; the scenario selection stays closed.");
            return false;
        }

        _scenario = new ScenarioSelectionDialog(
            _scenarioArtwork, _fonts.Normal, _fonts.Yellow, CreateScenarioText(), maps);

        GameLog.Info(
            $"Scenario selection opened: {maps.Count} maps, {_scenario.Maps.Count} listed, " +
            $"'{_scenario.Selected?.Name}' selected.");

        return true;
    }

    /// <summary>Closes the scenario list without a choice, the Escape key and CANCEL of the reference.</summary>
    /// <returns><see langword="true"/> when a dialog was open.</returns>
    public bool CloseScenario()
    {
        if (_scenario is null)
        {
            return false;
        }

        _scenario = null;
        GameLog.Info("Scenario selection closed without a choice.");

        return true;
    }

    /// <summary>
    /// Accepts the selected map and closes the dialog, the OKAY button and Enter of the reference.
    /// </summary>
    /// <returns>
    /// The chosen map, or <see langword="null"/> when the dialog is not open or the list is empty;
    /// the host takes it and starts the game with it.
    /// </returns>
    public Mp2FileInfo? ConfirmScenario()
    {
        if (_scenario?.Selected is not { } map)
        {
            return null;
        }

        _scenario = null;
        _status = $"{map.Name} - M4";
        _confirmedScenario = map;

        GameLog.Info(
            $"Scenario selected: '{map.Name}' ({map.Width}x{map.Height}, difficulty {map.Difficulty}, " +
            $"{map.PlayerCount} kingdom(s)) from '{map.FilePath}'.");

        return map;
    }

    /// <summary>
    /// Returns the map whose selection was confirmed since the last call and forgets it. The host
    /// polls this after every input batch: it is what turns a confirmed scenario into the adventure
    /// map, without the menu knowing anything about scenes.
    /// </summary>
    public Mp2FileInfo? TakeConfirmedScenario()
    {
        Mp2FileInfo? map = _confirmedScenario;
        _confirmedScenario = null;
        return map;
    }

    /// <summary>Moves the selection of the open scenario list by <paramref name="delta"/> rows.</summary>
    public void MoveScenarioSelection(int delta) => _scenario?.MoveSelection(delta);

    /// <summary>Moves the selection of the open scenario list by <paramref name="pages"/> pages.</summary>
    public void MoveScenarioPage(int pages) => _scenario?.MovePage(pages);

    /// <summary>Rolls the open scenario list by <paramref name="rows"/> rows, the mouse wheel.</summary>
    /// <param name="rows">Positive scrolls down; the selection does not change.</param>
    public void ScrollScenario(int rows) => _scenario?.ScrollBy(rows);

    /// <summary>Selects the first row of the open scenario list (the Home key).</summary>
    public void SelectFirstScenario() => _scenario?.Select(0);

    /// <summary>Selects the last row of the open scenario list (the End key).</summary>
    public void SelectLastScenario() => _scenario?.Select(int.MaxValue);

    /// <summary>Every map the dialog may offer, read on first use and cached for the session.</summary>
    private IReadOnlyList<Mp2FileInfo> Maps
    {
        get
        {
            if (_maps is null)
            {
                _maps = _dataDirectory is null ? [] : MapCatalog.ReadAll([_dataDirectory]);

                GameLog.Info(
                    $"Map catalog: {_maps.Count} playable map(s) under " +
                    $"'{_dataDirectory ?? "no data directory"}'.");
            }

            return _maps;
        }
    }

    /// <summary>The labels of the scenario dialog, resolved through the current text catalog.</summary>
    private ScenarioDialogText CreateScenarioText()
    {
        ScenarioDialogText untranslated = ScenarioDialogText.Default;

        return new ScenarioDialogText
        {
            DifficultyLabel = Text.Get(untranslated.DifficultyLabel),
            Difficulties = [.. untranslated.Difficulties.Select(Text.Get)],
            VictoryConditions = [.. untranslated.VictoryConditions.Select(Text.Get)],
            LossConditions = [.. untranslated.LossConditions.Select(Text.Get)],
        };
    }

    /// <summary>Switches to the language at <paramref name="index"/> and returns it.</summary>
    /// <param name="index">Index into <see cref="Languages"/>; out of range values are clamped.</param>
    public GameText SetLanguage(int index)
    {
        _languageIndex = Math.Clamp(index, 0, _languages.Count - 1);
        return Text;
    }

    /// <summary>Applies the open dialog and closes it, the OK button and Enter.</summary>
    private void ApplySettings()
    {
        if (_settings is { OptionCount: 1 } dialog)
        {
            SetLanguage(dialog.Option(0).SelectedIndex);
        }

        _settings = null;
        GameLog.Info($"Settings applied: interface language '{Text.Culture}'.");
    }

    /// <summary>Closes the open dialog without applying, the Escape key and CANCEL of the reference.</summary>
    private MainMenuAction? CloseSettings()
    {
        _settings?.Cancel();
        _settings = null;
        GameLog.Info("Settings dialog closed without changes.");

        return null;
    }

    /// <summary>Answers Enter through the button row, so the hot key rule stays the reference one.</summary>
    private MainMenuAction? ConfirmSettings()
    {
        if (_settings?.ProcessHotKey(ButtonHotKey.Okay) == DialogButton.Ok)
        {
            ApplySettings();
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

    /// <summary>
    /// Loads the pieces of the message frame. The menu works without them, so missing artwork only
    /// costs the right click tooltips.
    /// </summary>
    private static FrameBoxChrome? CreateMessageChrome(AssetBundle assets)
    {
        IcnSpriteFile? file = assets.TryReadIcn(FrameBoxChrome.AssetName);
        FrameBoxChrome? chrome = FrameBoxChrome.TryCreate(file?.Sprites);

        if (chrome is null)
        {
            GameLog.Warn(
                $"'{FrameBoxChrome.AssetName}' is missing or has fewer than " +
                $"{FrameBoxChrome.RequiredSpriteCount} sprites; the menu will not show tooltips.");
        }

        return chrome;
    }

    /// <summary>
    /// Builds the message box the reference shows while the right mouse button is held: the sign
    /// under the cursor wins over the settings door, and nothing else produces a tooltip.
    /// </summary>
    private MessageBox? CreateTip()
    {
        if (_messageChrome is null)
        {
            return null;
        }

        string header;
        string body;

        int index = IndexAt(_cursor);
        if (index >= 0)
        {
            header = Text.Get(Menu[index].LabelId);
            body = Text.Get(Menu[index].TooltipId);
        }
        else if (_cursor is { } cursor && DoorHighlight.HoverArea.Contains(cursor))
        {
            header = Text.Get(DoorLabelId);
            body = Text.Get(DoorTooltipId);
        }
        else if (_cursor is { } over && EditorArea.Contains(over))
        {
            header = Text.Get(EditorLabelId);
            body = Text.Get(EditorTooltipId);
        }
        else
        {
            return null;
        }

        return MessageBox.Create(header, body, _messageChrome, _fonts.Yellow, _fonts.Normal);
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

    /// <summary>
    /// Draws the editor button: the Price of Loyalty artwork when the data has it, a painted sign in
    /// the bottom right corner otherwise. Both cover <see cref="EditorArea"/>, so the click and the
    /// tooltip below are the same code for either look.
    /// </summary>
    private void DrawEditorSign(PixelBuffer pixels)
    {
        if (_editorSign is { } sign)
        {
            sign.Draw(pixels);
            return;
        }

        // The signs of the street are wood and gold; the painted editor sign borrows the same pair so
        // it does not read as a debug rectangle. The indices come from the palette, not from a guess.
        // The board stays dark so the white glyphs of GameFonts stay readable on it.
        Palette palette = _frame.Palette;
        byte board = palette.FindClosestIndex(new RgbaColor(74, 44, 18));
        byte frame = palette.FindClosestIndex(new RgbaColor(38, 22, 10));
        byte glow = palette.FindClosestIndex(new RgbaColor(238, 206, 120));

        Rect area = EditorFallbackArea;

        ImageOps.FillRect(pixels, area, board);
        ImageOps.DrawRect(pixels, area, frame);

        // A pressed sign sinks (dark inner edge), a hovered one gets a second gold ring.
        ImageOps.DrawRect(
            pixels,
            new Rect(area.X + 1, area.Y + 1, area.Width - 2, area.Height - 2),
            _editorState == ButtonState.Pressed ? frame : glow);

        if (_editorState == ButtonState.Hovered)
        {
            ImageOps.DrawRect(
                pixels,
                new Rect(area.X + 2, area.Y + 2, area.Width - 4, area.Height - 4),
                glow);
        }

        IGlyphSource font = _fonts.Normal;
        string label = Text.Get(EditorLabelId);
        int x = area.X + ((area.Width - GlyphText.Measure(font, label)) / 2);
        int y = area.Y + ((area.Height - font.LineHeight) / 2 + (_editorState == ButtonState.Pressed ? 2 : 1));

        GlyphText.DrawLine(font, pixels, new Point(x, y), label);
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

        ButtonState editor = _editorSign?.State ?? _editorState;
        if (editor == ButtonState.Hovered)
        {
            return Text.Get(EditorTooltipId);
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

        // New Game opens the scenario list of the original; the other signs are later milestones
        // (load game M8, high scores / credits M9) and only say so in the status bar.
        if (entry.Action == MainMenuAction.NewGame && OpenScenario())
        {
            _status = null;
            GameLog.Info($"Main menu: '{label}' opens the scenario selection.");
            return entry.Action;
        }

        _status = $"{label} - not available yet";
        GameLog.Info($"Main menu: '{label}' is not implemented yet.");
        return entry.Action;
    }

    private void UpdateButtonStates()
    {
        foreach (Button button in _buttons)
        {
            button.Update(_cursor, _leftButtonDown);
        }

        if (_editorSign is { } sign)
        {
            sign.Update(_cursor, _leftButtonDown);
            return;
        }

        _editorState = _cursor is { } point && EditorFallbackArea.Contains(point)
            ? (_leftButtonDown ? ButtonState.Pressed : ButtonState.Hovered)
            : ButtonState.Released;
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
