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
using FHeroes2.Game.Campaign;
using FHeroes2.Game.H2D;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The main menu of Heroes II: the street scene of <c>HEROES.ICN</c>, the five hanging signs of
/// <c>BTNSHNGL.ICN</c> on top of it, a one line status bar drawn with the game fonts and the message
/// box the original shows while the right mouse button is held.
/// </summary>
/// <remarks>
/// <para>
/// The composition follows <c>Game::MainMenu()</c> (M2 of bd 归档（PLAN.md，FHeroes2Sharp-3so）): <c>HEROES.ICN</c> is one
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

    /// <summary>
    /// Title of the language picker, <c>isGameLanguage ? _( "Select Game Language:" ) : ...</c> of
    /// <c>fheroes2::selectLanguage()</c>.
    /// </summary>
    public const string LanguagePickerTitleId = "Select Game Language:";

    /// <summary>Title of the greeting of the first run, <c>_( "Greetings!" )</c> of <c>Game::MainMenu()</c>.</summary>
    public const string WelcomeTitleId = "Greetings!";

    /// <summary>
    /// Body of the greeting of the first run, <c>_( "Welcome to Heroes of Might and Magic II powered by
    /// fheroes2 engine!" )</c>. The reference appends two paragraphs - one about touch input and one about
    /// the resolution selection; the touch one is added only when a touch device is present (see
    /// <see cref="TouchInputAvailable"/> and <see cref="WelcomeTouchId"/>).
    /// </summary>
    public const string WelcomeBodyId = "Welcome to Heroes of Might and Magic II powered by fheroes2 engine!";

    /// <summary>
    /// Touch paragraph of the greeting, <c>_( "To simulate a right-click with a touch ..." )</c> of
    /// <c>Game::MainMenu()</c>, written only when <c>System::isTouchInputAvailable()</c> is true; here when
    /// <see cref="TouchInputAvailable"/> is set by the host.
    /// </summary>
    public const string WelcomeTouchId =
        "To simulate a right-click with a touch to get info on various items, you need to first touch and keep " +
        "touching on the item of interest and then touch anywhere else on the screen. While holding the second " +
        "finger, you can remove the first one from the screen and keep viewing the information on the item.";

    /// <summary>
    /// Second paragraph of the greeting, <c>_( "Before starting the game, please select a game resolution." )</c>,
    /// the line that introduces the resolution chooser of <c>Game::MainMenu()</c>.
    /// </summary>
    public const string WelcomeResolutionId = "Before starting the game, please select a game resolution.";

    /// <summary>Title of the Standard game button of the new game menu, <c>_( "Standard Game" )</c>.</summary>
    public const string StandardGameLabelId = "Standard Game";

    /// <summary>Tooltip of the Standard game button.</summary>
    public const string StandardGameTooltipId = "A single player game playing out a single map.";

    /// <summary>Title of the Campaign game button, <c>_( "Campaign Game" )</c>.</summary>
    public const string CampaignGameLabelId = "Campaign Game";

    /// <summary>Tooltip of the Campaign game button.</summary>
    public const string CampaignGameTooltipId = "A single player game playing through a series of maps.";

    /// <summary>Title of the Multi-player game button, <c>_( "Multi-Player Game" )</c>.</summary>
    public const string MultiplayerGameLabelId = "Multi-Player Game";

    /// <summary>Tooltip of the Multi-player game button.</summary>
    public const string MultiplayerGameTooltipId =
        "A multi-player game, with several human players competing against each other on a single map.";

    /// <summary>Title of the Battle only button, <c>_( "Battle Only" )</c>.</summary>
    public const string BattleOnlyLabelId = "Battle Only";

    /// <summary>Tooltip of the Battle only button.</summary>
    public const string BattleOnlyTooltipId = "Setup and play a battle without loading any map.";

    /// <summary>Title of the Settings button, <c>_( "Game Settings" )</c>.</summary>
    public const string GameSettingsLabelId = "Game Settings";

    /// <summary>Tooltip of the Settings button.</summary>
    public const string GameSettingsTooltipId = "Change language, resolution and settings of the game.";

    /// <summary>Title of the Hot seat button, <c>_( "Hot Seat" )</c>.</summary>
    public const string HotSeatLabelId = "Hot Seat";

    /// <summary>Tooltip of the Hot seat button.</summary>
    public const string HotSeatTooltipId =
        "Play a Hot Seat game, where 2 to 6 players play on the same device, switching into the 'Hot Seat' when " +
        "it is their turn.";

    /// <summary>Title of the original campaign button, <c>_( "Original Campaign" )</c>.</summary>
    public const string OriginalCampaignLabelId = "Original Campaign";

    /// <summary>Tooltip of the original campaign button.</summary>
    public const string OriginalCampaignTooltipId =
        "Either Roland's or Archibald's campaign from the original Heroes of Might and Magic II.";

    /// <summary>Title of the expansion campaign button, <c>_( "Expansion Campaign" )</c>.</summary>
    public const string ExpansionCampaignLabelId = "Expansion Campaign";

    /// <summary>Tooltip of the expansion campaign button.</summary>
    public const string ExpansionCampaignTooltipId =
        "One of the four new campaigns from the Price of Loyalty expansion set.";

    /// <summary>Title of the cancel button of the new game menu, <c>_( "Cancel" )</c>.</summary>
    public const string NewGameCancelLabelId = "Cancel";

    /// <summary>Tooltip of the cancel button of the new game menu.</summary>
    public const string NewGameCancelTooltipId = "Cancel back to the main menu.";

    /// <summary>Titles and tooltips of the five player count buttons, indexed by count minus two.</summary>
    private static readonly (string Header, string Body)[] PlayerCountHelpTexts =
    [
        ("2 Players", "Play with 2 human players, and optionally, up to 4 additional computer players."),
        ("3 Players", "Play with 3 human players, and optionally, up to 3 additional computer players."),
        ("4 Players", "Play with 4 human players, and optionally, up to 2 additional computer players."),
        ("5 Players", "Play with 5 human players, and optionally, up to 1 additional computer player."),
        ("6 Players", "Play with 6 human players."),
    ];

    /// <summary>Frames of the resolution chooser's scroll bar, <c>ICN::SCROLL</c>.</summary>
    public const string ResolutionScrollAssetName = "SCROLL.ICN";

    /// <summary>Board behind the resolution chooser, <c>ICN::ADVBORD</c> frame 0.</summary>
    public const string ResolutionBoardAssetName = "ADVBORD.ICN";

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
    private readonly bool _hasMessageFrame;
    private readonly Button[] _buttons;
    private readonly Button? _editorSign;
    private readonly GameFonts _fonts;
    private readonly IReadOnlyList<GameText> _languages;
    private readonly Rect _statusBar;
    private readonly byte _statusBarColor;
    private readonly SettingsArtwork? _settingsArtwork;
    private readonly ScenarioDialogArtwork? _scenarioArtwork;
    private readonly ResolutionArtwork? _resolutionArtwork;
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
    private LanguageSelectionDialog? _languagePicker;
    private MessageBox? _welcome;
    private ResolutionDialog? _resolutionPicker;
    private MessageBox? _resolutionTip;
    private bool _skipResolutionInput;
    private IReadOnlyList<Mp2FileInfo>? _maps;
    private ScenarioSelectionDialog? _scenario;
    private ScenarioInfoDialog? _scenarioInfo;
    private ScenarioInfoArtwork? _scenarioInfoArtwork;
    private MessageBox? _scenarioInfoTip;
    private Mp2FileInfo? _confirmedScenario;
    private readonly AssetBundle? _assets;
    private Sprite[]? _newGameFrames;
    private bool _newGameFramesEvil;
    private NewGameMenuView? _newGame;
    private MessageBox? _newGameTip;

    private MainMenuScene(
        ScreenBuffer frame,
        Sprite background,
        Sprite? lanternBase,
        IcnAnimation? lantern,
        DoorHighlight? door,
        bool hasMessageFrame,
        Button[] buttons,
        Button? editorSign,
        GameFonts fonts,
        IReadOnlyList<GameText> languages,
        string? culture,
        SettingsArtwork? settingsArtwork,
        ScenarioDialogArtwork? scenarioArtwork,
        ResolutionArtwork? resolutionArtwork,
        AssetBundle? assets,
        string? dataDirectory)
    {
        _frame = frame;
        _background = background;
        _lanternBase = lanternBase;
        _lantern = lantern;
        _door = door;
        _hasMessageFrame = hasMessageFrame;
        _buttons = buttons;
        _editorSign = editorSign;
        _fonts = fonts;
        _languages = languages;
        _settingsArtwork = settingsArtwork;
        _scenarioArtwork = scenarioArtwork;
        _resolutionArtwork = resolutionArtwork;
        _assets = assets;
        _dataDirectory = dataDirectory;
        _statusBar = new Rect(0, frame.Size.Height - StatusBarHeight, frame.Size.Width, StatusBarHeight);
        _statusBarColor = frame.Palette.FindDarkestIndex();
        _languageIndex = LanguageCatalog.IndexOfCulture(languages, culture);
    }

    /// <summary>The language currently used for tooltips and the status bar.</summary>
    public GameText Text => _languages[_languageIndex];

    /// <summary>Languages the menu can switch between; the first one is English.</summary>
    public IReadOnlyList<GameText> Languages => _languages;

    /// <summary>
    /// Raised when the settings dialog applied a different interface language, so the host can remember it in
    /// the configuration file (<c>Settings::setGameLanguage()</c> 加 <c>conf.Save()</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 <c>dialog_game_settings.cpp:264</c> 在应用后直接 <c>conf.Save()</c>；端口里场景不知道配置文件在哪
    /// （那是宿主的事），所以只把新语言交回去。
    /// </remarks>
    public Action<GameText>? LanguageChanged { get; set; }

    /// <summary>
    /// Raised when the onboarding of the first run is over, so the host can clear the flag and write the
    /// configuration: <c>conf.resetFirstGameRun(); conf.Save( ... )</c> at the end of the
    /// <c>isFirstGameRun</c> branch of <c>Game::MainMenu()</c>.
    /// </summary>
    /// <remarks>
    /// 参考在这一支里也把分辨率选择当成了首运的一部分（<c>showGameResolutionSelection()</c>），端口的顺序与它一致：
    /// 语言选择、问候、分辨率选择，最后才走到这里；此时宿主写出的 <c>videomode</c> 已经是玩家挑的那一档。
    /// </remarks>
    public Action? FirstRunFinished { get; set; }

    /// <summary>
    /// Whether a touch device is present, <c>System::isTouchInputAvailable()</c>, which adds the touch paragraph
    /// to the greeting of the first run. The host fills it from <c>SDL_GetTouchDevices</c> before the onboarding.
    /// </summary>
    public bool TouchInputAvailable { get; set; }

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
            bool hasMessageFrame = HasMessageFrame(assets);

            IReadOnlyList<GameText> languages = LanguageCatalog.Select(GameText.LoadAll(assets.DataDirectory), fonts);

            ScenarioDialogArtwork? scenarioArtwork = ScenarioArtwork.TryCreate(assets);
            SettingsArtwork? settingsArtwork = SettingsArtwork.TryCreate(assets);
            ResolutionArtwork? resolutionArtwork = CreateResolutionArtwork(assets, settingsArtwork);

            MainMenuScene scene = new(
                frame, streetScene, lanternBase, lantern, door, hasMessageFrame, buttons, editorSign, fonts,
                languages, culture, settingsArtwork, scenarioArtwork, resolutionArtwork, assets,
                assets.DataDirectory);

            GameLog.Info(
                $"Main menu ready: {BackgroundAssetName} {background.Sprites[0].Width}x" +
                $"{background.Sprites[0].Height}, {Menu.Length} signs from {ButtonAssetName}, " +
                (lantern is null
                    ? "no lantern animation, "
                    : $"lantern glow {LanternFrameCount} frames every " +
                      $"{LanternFrameDuration.TotalMilliseconds:0} ms, ") +
                (door is null ? "settings door stays dark, " : "settings door lights up on hover, ") +
                (editorSign is null ? "editor sign drawn here, " : "editor sign from resurrection.h2d, ") +
                (hasMessageFrame ? "right click tooltips, " : "no tooltips, ") +
                (scenarioArtwork is null ? "no scenario selection, " : "scenario selection ready, ") +
                (resolutionArtwork is null ? "no resolution chooser, " : "resolution chooser ready, ") +
                // 每个标牌的命中区打进日志：冒烟脚本需要知道点哪里，而坐标来自 BTNSHNGL.ICN 的精灵偏移，
                // 不写出来就只能靠截图量。
                $"signs at {string.Join(", ", scene._buttons.Select(static sign => sign.Area))}, " +
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
    /// <para>
    /// <c>case ICN::BTNSHNGL</c> of <c>game_assets.cpp</c> appends three frames to the table it has just
    /// read: the original <c>BTNSHNGL.ICN</c> stops at frame 19, so the editor look always comes from
    /// <c>resurrection.h2d</c>. The anchors are stored in the archive, which is why the button lands on
    /// the easel of the street scene without a single coordinate here. An asset pack that already
    /// carries the frames (a table longer than 22 sprites) is used as it is.
    /// </para>
    /// <para>
    /// The reference only adds the button to the interactive list when it thinks Price of Loyalty is
    /// installed (<c>conf.isPriceOfLoyaltySupported()</c>, <c>game_mainmenu.cpp:340</c>). The port has no
    /// such option at start up, so the entry stays available on every installation; the button itself is
    /// still the reference artwork, never a hand drawn rectangle.
    /// </para>
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
    /// at the anchor stored in the archive, and only when a Price of Loyalty resource is present
    /// (<c>game_assets.cpp:3463</c>). The port skips that gate for the same reason as
    /// <see cref="CreateEditorButton"/> and paints whenever the archive has the icon. The background
    /// sprite itself belongs to the ICN cache, so a clone is painted and the scene owns the result;
    /// leaving the cache alone keeps a second menu creation from stacking the easel twice.
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

        // 首运的分辨率窗口是场景级模态，但输入走 LocalEvent（见 DriveResolutionPicker 的备注）。
        DriveResolutionPicker();
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
        // scenario list, then the two windows of the first run. They can never be open at the same
        // time, the order only keeps the code in the order the host opens them.
        _settings?.Draw(pixels);
        _newGame?.Draw(pixels);
        _newGameTip?.Draw(pixels);
        _scenario?.Draw(pixels);
        _scenarioInfo?.Draw(pixels);
        _scenarioInfoTip?.Draw(pixels);
        _languagePicker?.Draw(pixels);
        _welcome?.Draw(pixels);
        _resolutionPicker?.Draw(pixels);
        _resolutionTip?.Draw(pixels);

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

        // 新游戏菜单的帮助框先于菜单本身：它开着时菜单的按钮不再亮起。
        if (_newGameTip is not null)
        {
            _newGameTip.SetCursor(cursor);
            return;
        }

        if (_newGame is not null)
        {
            _newGame.SetCursor(cursor);
            _cursor = cursor;
            return;
        }

        if (_scenario is not null)
        {
            _scenario.SetCursor(cursor);
            return;
        }

        // The scenario info screen has no hover state of its own: it reads the pointer from LocalEvent when it is
        // clicked, so only the position is remembered - and the cursor stays away from the signs behind it.
        if (_scenarioInfo is not null)
        {
            _cursor = cursor;
            return;
        }

        if (_languagePicker is not null)
        {
            _languagePicker.SetCursor(cursor);
            return;
        }

        if (_welcome is not null)
        {
            _welcome.SetCursor(cursor);
            return;
        }

        // The explanation of the resolution chooser sits on top of it and owns the cursor while it is up.
        if (_resolutionTip is not null)
        {
            _resolutionTip.SetCursor(cursor);
            return;
        }

        // The resolution chooser of the first run reads the pointer from LocalEvent itself, so the
        // scene only has to keep it away from the signs.
        if (_resolutionPicker is not null)
        {
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

        // 新游戏菜单的帮助框先关，再轮到菜单自己的按钮。
        if (_newGameTip is not null)
        {
            if (_newGameTip.SetLeftButton(down) == DialogButton.Ok)
            {
                _newGameTip = null;
            }

            return null;
        }

        // 新游戏菜单按自己的 Area 判定点击（DialogButton 只是占位），结果立刻分派。
        if (_newGame is not null)
        {
            NewGameMenuChoice choice = _newGame.SetLeftButton(down, _cursor ?? new Point(-1, -1));

            return choice.Action == NewGameMenuAction.None ? null : ApplyNewGameChoice(choice);
        }

        // The help box of the scenario info screen sits on top of it: its OK button closes the box and nothing
        // else is reached while it is up, the way <c>Dialog::Message</c> swallows the clicks inside its loop.
        if (_scenarioInfoTip is not null)
        {
            if (_scenarioInfoTip.SetLeftButton(down) == DialogButton.Ok)
            {
                _scenarioInfoTip = null;
            }

            return null;
        }

        // The scenario info screen owns the mouse while it is open: it answers with the screen to go to next.
        // 它从 LocalEvent 里读光标与按键（和窗口里的按钮同一套写法），而 SetCursor 只改菜单自己的 _cursor，
        // 所以这里先把光标位置推进 LocalEvent，脚本点击才能落到按钮上。
        if (_scenarioInfo is not null)
        {
            LocalEvent events = LocalEvent.Current;
            if (_cursor is { } cursor)
            {
                events.OnMouseMotion(cursor);
                events.OnMouseButton(down, MouseButtonType.Left, cursor);
            }

            if (down)
            {
                ApplyScenarioInfoResult(_scenarioInfo.ProcessEvents(events));
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

        // The language picker of the first run owns the mouse while it is open: OK applies the
        // language, CANCEL keeps the current one, both lead to the greeting.
        if (_languagePicker is not null)
        {
            switch (_languagePicker.SetLeftButton(down))
            {
                case DialogButton.Ok:
                    ConfirmLanguagePicker();
                    break;

                case DialogButton.Cancel:
                    CancelLanguagePicker();
                    break;

                default:
                    break;
            }

            return null;
        }

        // The right click explanation of the resolution chooser is a small OK box on top of it: its
        // button closes the box, nothing else happens while it is up.
        if (_resolutionTip is not null)
        {
            if (_resolutionTip.SetLeftButton(down) == DialogButton.Ok)
            {
                _resolutionTip = null;
                _skipResolutionInput = true;
            }

            return null;
        }

        // The resolution chooser of the first run owns the mouse through LocalEvent (see
        // DriveResolutionPicker); a click must not reach the signs behind it.
        if (_resolutionPicker is not null)
        {
            return null;
        }

        // The greeting has a single OK button; ESC is the same key through the hot key path.
        if (_welcome is not null)
        {
            if (_welcome.SetLeftButton(down) == DialogButton.Ok)
            {
                CloseWelcome();
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
    public void SetRightButton(bool down)
    {
        // 新游戏菜单的右键是 <c>Dialog::ZERO</c> 的帮助框：按下弹出，松开消失。
        if (_newGame is not null)
        {
            _newGameTip = down ? CreateNewGameTip() : null;
            return;
        }

        _tip = down && !HasModalDialog ? CreateTip() : null;
    }

    /// <summary>
    /// Selects a sign through its hot key (<c>N</c>, <c>L</c>, <c>H</c>, <c>C</c>, <c>Q</c>), or the
    /// editor through <c>E</c>.
    /// </summary>
    public MainMenuAction? Select(char hotKey)
    {
        // The explanation of the resolution chooser is an OK box: Escape and Enter close it, the
        // chooser behind it keeps its keys until the box is gone.
        if (_resolutionTip is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter or EnterCharacter or LineFeedCharacter:
                    _resolutionTip = null;
                    _skipResolutionInput = true;
                    break;

                default:
                    break;
            }

            return null;
        }

        // The resolution chooser of the first run reads the keys from LocalEvent itself (see
        // DriveResolutionPicker); Escape and Enter are answered there, not here.
        if (_resolutionPicker is not null)
        {
            return null;
        }

        // The language picker of the first run owns the keyboard: Escape is CANCEL, Enter goes through
        // the button row as OK. The arrow keys are not characters, the host routes them through
        // MoveLanguageSelection.
        if (_languagePicker is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter:
                    CancelLanguagePicker();
                    break;

                case EnterCharacter or LineFeedCharacter:
                    if (_languagePicker.ProcessHotKey(ButtonHotKey.Okay) == DialogButton.Ok)
                    {
                        ConfirmLanguagePicker();
                    }

                    break;

                default:
                    break;
            }

            return null;
        }

        // The greeting is an OK box: <c>HotKeyCloseWindow()</c> closes it with Escape as well.
        if (_welcome is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter or EnterCharacter or LineFeedCharacter:
                    CloseWelcome();
                    break;

                default:
                    break;
            }

            return null;
        }

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

        // 新游戏菜单的帮助框是一个 OK 框，Escape/Enter 关掉它；下面的菜单保留自己的按键。
        if (_newGameTip is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter or EnterCharacter or LineFeedCharacter:
                    _newGameTip = null;
                    break;

                default:
                    break;
            }

            return null;
        }

        // 新游戏菜单的按键照 <c>game_hotkeys.cpp:118-155</c>：S/C/M/T/B/H/O/E 与人数键 2-6。
        if (_newGame is not null)
        {
            if (hotKey == EscapeCharacter)
            {
                CloseNewGameMenu();
                return null;
            }

            if (MapNewGameHotKey(hotKey) is not { } menuKey)
            {
                return null;
            }

            NewGameMenuChoice choice = _newGame.SetHotKey(menuKey);

            return choice.Action == NewGameMenuAction.None ? null : ApplyNewGameChoice(choice);
        }

        // The help box of the scenario info screen is an OK box: Escape and Enter close it, the screen behind it
        // keeps its keys until the box is gone.
        if (_scenarioInfoTip is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter or EnterCharacter or LineFeedCharacter:
                    _scenarioInfoTip = null;
                    break;

                default:
                    break;
            }

            return null;
        }

        // While the scenario info screen is open the keyboard belongs to it: Escape is CANCEL, Enter is OKAY and M
        // is the "Select Maps" button (<c>game_scenarioinfo.cpp:395-420</c>).
        if (_scenarioInfo is not null)
        {
            switch (hotKey)
            {
                case EscapeCharacter:
                    CloseScenarioInfo();
                    break;

                case EnterCharacter or LineFeedCharacter:
                    AcceptScenarioInfo();
                    break;

                case 'm' or 'M':
                    ReopenScenarioList();
                    break;

                default:
                    break;
            }

            return null;
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
            new Size(SettingsContentWidth, contentHeight), true, _frame.Size);

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

    /// <summary>The open language picker of the first run, or <see langword="null"/>.</summary>
    public LanguageSelectionDialog? LanguagePicker => _languagePicker;

    /// <summary>The greeting of the first run, or <see langword="null"/> when it is not on screen.</summary>
    public MessageBox? Welcome => _welcome;

    /// <summary>
    /// Starts the onboarding of a first run: the language picker of <c>fheroes2::selectLanguage()</c>,
    /// followed by the greeting and the resolution chooser of <c>Game::MainMenu()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的首运分支（<c>game_mainmenu.cpp:265-300</c>）依次是 <c>selectLanguage( true )</c>、
    /// <c>showStandardTextMessage( _( "Greetings!" ), ... )</c>、<c>Dialog::SelectResolution()</c>，最后
    /// <c>conf.resetFirstGameRun()</c> 加 <c>conf.Save()</c>——清旗标与保存是宿主的事，场景只在收尾时抛
    /// <see cref="FirstRunFinished"/>（此时窗口已经是玩家挑的那一档，宿主写出去的就是它）。
    /// </para>
    /// <para>
    /// 问候消息的触摸段与参考一致：只有 <see cref="TouchInputAvailable"/>（宿主按 <c>SDL_GetTouchDevices</c>
    /// 给出的结果）为真时才写，因为这一段讲的两指手势在 <c>LocalEvent</c> 里真的被翻译成左右键；分辨率段照写，
    /// 因为后面真的会打开 <see cref="ResolutionDialog"/>。
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> when the picker is now open.</returns>
    public bool BeginFirstRun()
    {
        if (HasModalDialog)
        {
            GameLog.Info("First run: another window is open, the onboarding waits for the next start.");
            return false;
        }

        if (_settingsArtwork is null)
        {
            GameLog.Warn("First run: no button artwork, the onboarding is skipped.");
            return false;
        }

        int contentHeight = LanguageSelectionDialog.GetRequiredHeight(_fonts.Normal.LineHeight, _languages.Count);

        StandardWindow window = StandardWindow.CreateCentered(
            new Size(LanguageSelectionDialog.ContentWidth, contentHeight), true, _frame.Size);

        DialogButton row = _settingsArtwork.HasCancel ? DialogButton.Ok | DialogButton.Cancel : DialogButton.Ok;
        ButtonGroup buttons = ButtonGroup.CreateStandard(window.ActiveArea, row, _settingsArtwork);

        _languagePicker = new LanguageSelectionDialog(
            window, buttons, _fonts.Yellow, _fonts.Normal, _fonts.Yellow, Text.Get(LanguagePickerTitleId),
            [.. _languages.Select(language => language.NativeName)], _languageIndex);

        GameLog.Info(
            $"First run: language picker opened, '{Text.Culture}' selected of {_languages.Count} installed, " +
            $"content {LanguageSelectionDialog.ContentWidth}x{contentHeight}.");

        return true;
    }

    /// <summary>Moves the selection of the open language picker by <paramref name="delta"/> languages.</summary>
    /// <param name="delta">Negative moves up, positive down; the selection wraps like the reference list box.</param>
    public void MoveLanguageSelection(int delta) => _languagePicker?.MoveSelection(delta);

    /// <summary>The open scenario list, or <see langword="null"/> when New Game has not been used.</summary>
    public ScenarioSelectionDialog? Scenario => _scenario;

    /// <summary>
    /// Drops the cached map list so the next New Game scans the folders again, the reference's habit of asking
    /// <c>Maps::getValidMaps()</c> every time the dialog opens.
    /// </summary>
    /// <remarks>
    /// 生成器把新图写进 <c>maps</c> 目录之后调用它：不刷新的话，玩家回到主菜单点「新游戏」仍然看不到刚生成的那张图——
    /// 目录里有了、列表里没有，是最难查的那种「刚才明明成功了」。
    /// </remarks>
    public void InvalidateMapList()
    {
        _maps = null;

        GameLog.Info("Map catalog invalidated; the next scenario list re-reads the map folders.");
    }

    /// <summary>
    /// Whether a modal window owns the input: the settings dialog, the scenario list, the new game
    /// menu, or the language picker, greeting and resolution chooser of the first run.
    /// </summary>
    public bool HasModalDialog =>
        _settings is not null || _scenario is not null || _scenarioInfo is not null ||
        _scenarioInfoTip is not null || _newGame is not null || _newGameTip is not null ||
        _languagePicker is not null || _welcome is not null || _resolutionPicker is not null ||
        _resolutionTip is not null;

    /// <summary>The open new game menu, or <see langword="null"/> when New Game has not been used.</summary>
    public NewGameMenuView? NewGameMenu => _newGame;

    /// <summary>The explanation of a button of the new game menu, or <see langword="null"/>.</summary>
    public MessageBox? NewGameTip => _newGameTip;

    /// <summary>
    /// Opens the new game menu of <c>Game::NewGame()</c>, what a click on the New Game sign does in the
    /// reference (<c>game_newgame.cpp:416-648</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在主菜单循环里直接进 <c>Game::NewGame()</c>；端口把它拆成一块场景级模态：这里只按当前界面风格组装
    /// 28 帧（<see cref="NewGameMenuArtwork"/>），点击与热键由 <see cref="NewGameMenuView"/> 自己判定，结果回到
    /// <see cref="ApplyNewGameChoice"/>。
    /// </para>
    /// <para>
    /// 两个战役按钮的可用性照参考的两个检查：原版两大战役看地图是否齐；资料片四战役还要求
    /// <c>X_LOADCM.ICN</c> 在包里且四套地图都在（<c>Game::isPriceOfLoyaltyCampaignPresent()</c>，
    /// <c>game_campaign.cpp:1141-1157</c>）。
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> when the menu is now open.</returns>
    public bool OpenNewGameMenu()
    {
        if (_newGame is not null)
        {
            return true;
        }

        if (_assets is null || HasModalDialog)
        {
            return false;
        }

        bool isEvilInterface = Game.System.Settings.Get().IsEvilInterfaceEnabled;

        if (_newGameFrames is null || _newGameFramesEvil != isEvilInterface)
        {
            _newGameFrames = NewGameMenuArtwork.TryCreate(_assets, Text, isEvilInterface);
            _newGameFramesEvil = isEvilInterface;
        }

        if (_newGameFrames is null)
        {
            return false;
        }

        bool successionWars = IsCampaignPresent(CampaignId.Archibald) && IsCampaignPresent(CampaignId.Roland);

        bool priceOfLoyalty =
            _assets.TryReadIcn(NewGameMenuArtwork.CampaignAssetName) is not null &&
            IsCampaignPresent(CampaignId.PriceOfLoyalty) && IsCampaignPresent(CampaignId.Descendants) &&
            IsCampaignPresent(CampaignId.WizardsIsle) && IsCampaignPresent(CampaignId.VoyageHome);

        _newGame = NewGameMenuView.TryCreate(_newGameFrames, successionWars, priceOfLoyalty);

        if (_newGame is null)
        {
            return false;
        }

        _status = null;
        GameLog.Info(
            $"New game menu opened; original campaigns {(successionWars ? "available" : "missing")}, " +
            $"Price of Loyalty {(priceOfLoyalty ? "available" : "missing")}.");

        return true;
    }

    /// <summary>Closes the new game menu without a choice, the Cancel button and Escape of the reference.</summary>
    /// <returns><see langword="true"/> when the menu was open.</returns>
    public bool CloseNewGameMenu()
    {
        if (_newGame is null && _newGameTip is null)
        {
            return false;
        }

        _newGame = null;
        _newGameTip = null;
        GameLog.Info("New game menu closed without a choice.");

        return true;
    }

    /// <summary>Whether every map of one campaign is installed, <c>Game::is*CampaignPresent()</c>.</summary>
    private bool IsCampaignPresent(int campaignId)
    {
        if (_dataDirectory is null)
        {
            return false;
        }

        return CampaignData.GetCampaignData(campaignId).IsAllCampaignMapsPresent([_dataDirectory]);
    }

    /// <summary>
    /// Applies one answer of the new game menu.
    /// </summary>
    /// <param name="choice">What the player picked.</param>
    /// <returns>
    /// The action the host has to handle, or <see langword="null"/> when the menu stays open and the
    /// scene keeps the frame to itself.
    /// </returns>
    /// <remarks>
    /// Standard game goes to the scenario list of the third block. Battle only, the hot seat player
    /// count and the two campaigns need their own scenes and are not wired yet; they keep the menu up
    /// rather than pretending that something happened.
    /// </remarks>
    private MainMenuAction? ApplyNewGameChoice(NewGameMenuChoice choice)
    {
        switch (choice.Action)
        {
            case NewGameMenuAction.None:
                return null;

            case NewGameMenuAction.Cancel:
                CloseNewGameMenu();
                return null;

            case NewGameMenuAction.StandardGame:
                CloseNewGameMenu();

                if (OpenScenario())
                {
                    GameLog.Info("New game menu: Standard game opens the scenario selection.");
                    return MainMenuAction.NewGame;
                }

                return null;

            case NewGameMenuAction.GameSettings:
                // 参考在设置窗口返回后回到主菜单（GameMode::MAIN_MENU），所以这里也把新游戏菜单关掉。
                CloseNewGameMenu();
                OpenSettings();
                return null;

            default:
                GameLog.Info($"New game menu: {choice.Action} is not wired yet; the menu stays open.");
                _status = $"{choice.Action} - not available yet";
                return null;
        }
    }

    /// <summary>Builds the help box of a button of the new game menu, <c>Dialog::ZERO</c> of the reference.</summary>
    private MessageBox? CreateNewGameTip()
    {
        if (_newGame is not { } menu || _cursor is not { } cursor)
        {
            return null;
        }

        NewGameMenuHelpRequest request = menu.ProcessRightClick(cursor);

        (string header, string body)? message = request.Target switch
        {
            NewGameMenuHelpTarget.StandardGame => (StandardGameLabelId, StandardGameTooltipId),
            NewGameMenuHelpTarget.CampaignGame => (CampaignGameLabelId, CampaignGameTooltipId),
            NewGameMenuHelpTarget.MultiplayerGame => (MultiplayerGameLabelId, MultiplayerGameTooltipId),
            NewGameMenuHelpTarget.BattleOnly => (BattleOnlyLabelId, BattleOnlyTooltipId),
            NewGameMenuHelpTarget.GameSettings => (GameSettingsLabelId, GameSettingsTooltipId),
            NewGameMenuHelpTarget.HotSeat => (HotSeatLabelId, HotSeatTooltipId),
            NewGameMenuHelpTarget.SuccessionWarsCampaign => (OriginalCampaignLabelId, OriginalCampaignTooltipId),
            NewGameMenuHelpTarget.PriceOfLoyaltyCampaign => (ExpansionCampaignLabelId, ExpansionCampaignTooltipId),
            NewGameMenuHelpTarget.PlayerCount => PlayerCountHelpTexts[
                Math.Clamp(request.PlayerCount - 2, 0, PlayerCountHelpTexts.Length - 1)],
            NewGameMenuHelpTarget.Cancel => (NewGameCancelLabelId, NewGameCancelTooltipId),
            _ => null,
        };

        if (message is not { } texts)
        {
            return null;
        }

        return MessageBox.Create(Text.Get(texts.header), Text.Get(texts.body), _fonts.Yellow, _fonts.Normal);
    }

    /// <summary>Translates a character to one of the hot keys of the new game menu.</summary>
    /// <param name="hotKey">Key that was pressed.</param>
    /// <returns>The hot key, or <see langword="null"/> for keys the menu does not answer.</returns>
    private static NewGameMenuHotKey? MapNewGameHotKey(char hotKey) => char.ToUpperInvariant(hotKey) switch
    {
        'S' => NewGameMenuHotKey.Standard,
        'C' => NewGameMenuHotKey.Campaign,
        'M' => NewGameMenuHotKey.Multiplayer,
        'T' => NewGameMenuHotKey.Settings,
        'B' => NewGameMenuHotKey.BattleOnly,
        'H' => NewGameMenuHotKey.HotSeat,
        'O' => NewGameMenuHotKey.OriginalCampaign,
        'E' => NewGameMenuHotKey.ExpansionCampaign,
        '2' => NewGameMenuHotKey.TwoPlayers,
        '3' => NewGameMenuHotKey.ThreePlayers,
        '4' => NewGameMenuHotKey.FourPlayers,
        '5' => NewGameMenuHotKey.FivePlayers,
        '6' => NewGameMenuHotKey.SixPlayers,
        _ => null,
    };

    /// <summary>The open resolution chooser of the first run, or <see langword="null"/>.</summary>
    public ResolutionDialog? ResolutionPicker => _resolutionPicker;

    /// <summary>The explanation of a row of the resolution chooser, or <see langword="null"/>.</summary>
    public MessageBox? ResolutionTip => _resolutionTip;

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
    /// Accepts the row of the scenario list and goes on to the scenario info screen, the OKAY button and Enter of
    /// <c>Dialog::SelectScenario()</c> followed by <c>Game::SelectScenario()</c> (<c>game_scenarioinfo.cpp:95-212</c>).
    /// </summary>
    /// <returns>The map the info screen was opened for, or <see langword="null"/> when the list is not open or empty.</returns>
    public Mp2FileInfo? ConfirmScenario()
    {
        if (_scenario?.Selected is not { } map)
        {
            return null;
        }

        _scenario = null;

        GameLog.Info(
            $"Scenario selected: '{map.Name}' ({map.Width}x{map.Height}, difficulty {map.Difficulty}, " +
            $"{map.PlayerCount} kingdom(s)) from '{map.FilePath}'.");

        OpenScenarioInfo(map);

        return map;
    }

    /// <summary>The scenario info screen, or <see langword="null"/> when it is not open.</summary>
    public ScenarioInfoDialog? ScenarioInfo => _scenarioInfo;

    /// <summary>
    /// Accepts the scenario the info screen shows and remembers it for the host, which starts the game with it.
    /// </summary>
    /// <returns><see langword="true"/> when the screen was open and the game may start.</returns>
    public bool AcceptScenarioInfo()
    {
        if (_scenarioInfo is not { } dialog)
        {
            return false;
        }

        dialog.Accept();

        Mp2FileInfo map = dialog.Map;
        GameDifficulty difficulty = dialog.Difficulty;
        int slots = dialog.Players.All.Count;

        _scenarioInfo = null;
        _status = $"{map.Name} - M4";
        _confirmedScenario = map;

        GameLog.Info($"Scenario accepted: '{map.Name}', difficulty {difficulty}, {slots} slot(s); the game starts.");

        return true;
    }

    /// <summary>Goes back from the info screen to the main menu, Escape and CANCEL.</summary>
    /// <returns><see langword="true"/> when the screen was open.</returns>
    public bool CloseScenarioInfo()
    {
        if (_scenarioInfo is null)
        {
            return false;
        }

        _scenarioInfo = null;
        _scenarioInfoTip = null;
        GameLog.Info("Scenario info closed without starting a game.");

        return true;
    }

    /// <summary>
    /// Goes back from the info screen to the map list, the "Select Maps" button and the M key
    /// (<c>game_scenarioinfo.cpp:395-402</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the list was opened again.</returns>
    public bool ReopenScenarioList()
    {
        if (_scenarioInfo is not { } dialog)
        {
            return false;
        }

        Mp2FileInfo map = dialog.Map;

        _scenarioInfo = null;

        if (!OpenScenario())
        {
            return false;
        }

        for (int i = 0; i < _scenario!.Maps.Count; ++i)
        {
            if (_scenario.Maps[i].FilePath == map.FilePath)
            {
                _scenario.Select(i);
                break;
            }
        }

        return true;
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

    /// <summary>
    /// Opens the scenario info screen for <paramref name="map"/>, <c>Game::SelectScenario()</c>
    /// (<c>game_scenarioinfo.cpp:95-212</c>).
    /// </summary>
    /// <remarks>
    /// 这一屏要 NGEXTRA / NGHSBKG / MAP_SELECT 的图；缺图时 <see cref="ScenarioInfoArtwork"/> 会抛，此时不硬撑一个空窗口，
    /// 记一条警告后按旧行为直接开始这张图，跟 <see cref="OpenScenario"/> 在缺图时干脆不开窗口是同一个思路。
    /// </remarks>
    private void OpenScenarioInfo(Mp2FileInfo map)
    {
        try
        {
            _scenarioInfoArtwork ??= new ScenarioInfoArtwork(Game.System.Settings.Get().IsEvilInterfaceEnabled);

            ScenarioInfoDialog dialog = new(
                Maps,
                _scenarioInfoArtwork,
                _fonts.Normal,
                _fonts.SmallWhite,
                ShowScenarioInfoMessage,
                CreateScenarioInfoText());

            int index = IndexOfMap(map);
            if (index >= 0)
            {
                dialog.SelectMap(index);
            }

            _scenarioInfo = dialog;

            GameLog.Info($"Scenario info opened: '{dialog.Map.Name}', difficulty {dialog.Difficulty}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            GameLog.Warn($"The scenario info screen cannot be shown: {ex.Message}; '{map.Name}' starts directly.");

            _status = $"{map.Name} - M4";
            _confirmedScenario = map;
        }
    }

    /// <summary>Turns the answer of the info screen into the next screen of the menu.</summary>
    /// <param name="result">What the click asked for.</param>
    private void ApplyScenarioInfoResult(ScenarioInfoResult result)
    {
        switch (result)
        {
            case ScenarioInfoResult.Started:
                AcceptScenarioInfo();
                break;

            case ScenarioInfoResult.Cancelled:
                CloseScenarioInfo();
                break;

            case ScenarioInfoResult.SelectMaps:
                ReopenScenarioList();
                break;

            default:
                break;
        }
    }

    /// <summary>Shows a help message of the scenario info screen on top of it.</summary>
    /// <param name="title">The header of the box.</param>
    /// <param name="body">The text of the box.</param>
    private void ShowScenarioInfoMessage(string title, string body)
    {
        _scenarioInfoTip = MessageBox.Create(title, body, _fonts.Yellow, _fonts.Normal);
    }

    /// <summary>The index of <paramref name="map"/> in <see cref="Maps"/>, or -1 when it is not there.</summary>
    /// <param name="map">The map the info screen has to open with.</param>
    private int IndexOfMap(Mp2FileInfo map)
    {
        for (int i = 0; i < Maps.Count; ++i)
        {
            if (Maps[i].FilePath == map.FilePath)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The labels of the scenario info screen, resolved through the current text catalog.</summary>
    private ScenarioInfoText CreateScenarioInfoText()
    {
        ScenarioInfoText untranslated = ScenarioInfoText.Default;

        return new ScenarioInfoText
        {
            ScenarioLabel = Text.Get(untranslated.ScenarioLabel),
            DifficultyLabel = Text.Get(untranslated.DifficultyLabel),
            OpponentsLabel = Text.Get(untranslated.OpponentsLabel),
            ClassLabel = Text.Get(untranslated.ClassLabel),
            Difficulties = [.. untranslated.Difficulties.Select(Text.Get)],
            RatingTemplate = Text.Get(untranslated.RatingTemplate),
            SelectMapsButton = Text.Get(untranslated.SelectMapsButton),
            ScenarioHelpTitle = Text.Get(untranslated.ScenarioHelpTitle),
            ScenarioHelp = Text.Get(untranslated.ScenarioHelp),
            DifficultyHelpTitle = Text.Get(untranslated.DifficultyHelpTitle),
            DifficultyHelp = Text.Get(untranslated.DifficultyHelp),
            RatingHelpTitle = Text.Get(untranslated.RatingHelpTitle),
            RatingHelp = Text.Get(untranslated.RatingHelp),
            OkayHelpTitle = Text.Get(untranslated.OkayHelpTitle),
            OkayHelp = Text.Get(untranslated.OkayHelp),
            CancelHelpTitle = Text.Get(untranslated.CancelHelpTitle),
            CancelHelp = Text.Get(untranslated.CancelHelp),
        };
    }

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
        string previous = Text.Culture;

        if (_settings is { OptionCount: 1 } dialog)
        {
            SetLanguage(dialog.Option(0).SelectedIndex);
        }

        _settings = null;
        GameLog.Info($"Settings applied: interface language '{Text.Culture}'.");

        // 只有真换了语言才通知：参考的保存点也在“有改动就写”的那一支上。
        if (!string.Equals(previous, Text.Culture, StringComparison.Ordinal))
        {
            LanguageChanged?.Invoke(Text);
        }
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

    /// <summary>Applies the language of the open picker and moves on to the greeting, the OK button.</summary>
    private void ConfirmLanguagePicker()
    {
        string previous = Text.Culture;

        if (_languagePicker is { } picker)
        {
            SetLanguage(picker.SelectedIndex);
        }

        _languagePicker = null;
        GameLog.Info($"First run: language '{previous}' -> '{Text.Culture}'.");

        ShowWelcome();
    }

    /// <summary>Keeps the current language and moves on to the greeting, the CANCEL button and Escape.</summary>
    private void CancelLanguagePicker()
    {
        _languagePicker?.Cancel();
        _languagePicker = null;
        GameLog.Info($"First run: language selection cancelled, staying with '{Text.Culture}'.");

        ShowWelcome();
    }

    /// <summary>
    /// Shows the greeting of the first run, the <c>showStandardTextMessage( _( "Greetings!" ), ... )</c>
    /// that follows the language selection whether it was confirmed or cancelled.
    /// </summary>
    private void ShowWelcome()
    {
        if (!_hasMessageFrame || _settingsArtwork is null)
        {
            GameLog.Warn("First run: the greeting needs the message frame and the OK button; skipped.");
            FinishFirstRun();
            return;
        }

        // The paragraphs are appended in the reference's order: touch first, resolution last
        // (game_mainmenu.cpp:265-300).
        string body = Text.Get(WelcomeBodyId);

        if (TouchInputAvailable)
        {
            body += $"\n\n{Text.Get(WelcomeTouchId)}";
        }

        body += $"\n\n{Text.Get(WelcomeResolutionId)}";

        _welcome = MessageBox.Create(
            Text.Get(WelcomeTitleId),
            body,
            _fonts.Yellow,
            _fonts.Normal,
            buttons: DialogButton.Ok,
            buttonArtwork: _settingsArtwork);

        GameLog.Info("First run: greeting shown.");
    }

    /// <summary>
    /// Closes the greeting and opens the resolution chooser, the <c>Dialog::SelectResolution()</c> that
    /// follows <c>showStandardTextMessage( _( "Greetings!" ), ... )</c>; the onboarding ends when the
    /// chooser does, or right away when it cannot be built.
    /// </summary>
    private void CloseWelcome()
    {
        _welcome = null;

        if (!TryOpenResolutionPicker())
        {
            FinishFirstRun();
        }
    }

    /// <summary>
    /// Opens the resolution chooser of the first run, <c>Game::MainMenu()</c>'s
    /// <c>showGameResolutionSelection()</c> (<c>game_mainmenu.cpp:265-300</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在这里调 <c>Dialog::SelectResolution()</c>，是个阻塞窗口；端口沿用首运两个窗口的做法，把
    /// <see cref="ResolutionDialog"/> 当场景级模态挂起来：<see cref="Update"/> 每帧从
    /// <see cref="LocalEvent.Current"/> 取一次输入，<c>--smoke-frames</c> 与 <c>--screenshot</c> 照常跑得下去。
    /// </para>
    /// <para>
    /// 行是「640x480 的帧缓冲 + 某个窗口尺寸」（<see cref="IDisplayControl.GetAvailableResolutions"/>，ADR-205），
    /// 所以这里只保留游戏尺寸等于帧缓冲的那些行：其余的 <see cref="IDisplayControl.SetResolution"/> 会拒绝。
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> when the chooser is now open.</returns>
    private bool TryOpenResolutionPicker()
    {
        if (_resolutionArtwork is null)
        {
            GameLog.Warn("First run: the resolution chooser has no artwork, the step is skipped.");

            return false;
        }

        if (LocalEvent.Current.Engine?.Display is not { } display)
        {
            GameLog.Warn("First run: there is no display to read the resolutions from, the step is skipped.");

            return false;
        }

        Size frameSize = _frame.Size;
        List<ResolutionInfo> available = [];

        foreach (ResolutionInfo resolution in display.GetAvailableResolutions())
        {
            if (resolution.GameSize == frameSize)
            {
                available.Add(resolution);
            }
        }

        if (available.Count == 0)
        {
            GameLog.Warn(
                $"First run: the display offers no window for a {frameSize.Width}x{frameSize.Height} frame " +
                "buffer, the step is skipped.");

            return false;
        }

        ResolutionInfo current = new(frameSize.Width, frameSize.Height, display.ScreenSize.Width,
            display.ScreenSize.Height);

        _resolutionPicker = ResolutionDialog.TryCreate(frameSize, _resolutionArtwork, _fonts.Yellow,
            _fonts.Normal, _fonts.Yellow, available, current);

        if (_resolutionPicker is null)
        {
            GameLog.Warn("First run: the resolution chooser has no resolution to offer, the step is skipped.");

            return false;
        }

        // 打开它的那次按键（问候里的回车或 Esc）还在本帧的事件状态里；不跳过这一帧，
        // 同一次按键会立刻回答分辨率窗，跳过参考顺序里的一个窗口。
        _skipResolutionInput = true;

        GameLog.Info(
            $"First run: resolution chooser open, {available.Count} window(s), current " +
            $"{current.ScreenWidth}x{current.ScreenHeight}.");

        return true;
    }

    /// <summary>
    /// Pumps one frame of the open resolution chooser. OK switches the window, Cancel and Escape end the
    /// onboarding with the current resolution.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>Dialog::SelectResolution()</c> 由引擎的输入状态驱动，端口的其它场景级窗口是自己接
    /// <c>SetCursor</c>/<c>SetLeftButton</c>；这里直接用 <see cref="LocalEvent.Current"/>，因为
    /// <see cref="ResolutionDialog"/> 继承的 <c>ListBox</c> 的滚动条拖动、双击、右键解释全都挂在那一套输入上。
    /// 右键解释在参考里是窗口循环内的一次 <c>showStandardTextMessage</c>；端口把它挂成
    /// <see cref="_resolutionTip"/>，同样模态于列表之上，但只等一个按键或一次点击（见 <see cref="ShowResolutionTip"/>）。
    /// </remarks>
    private void DriveResolutionPicker()
    {
        if (_resolutionPicker is not { } picker)
        {
            _skipResolutionInput = false;

            return;
        }

        // The press that closed the greeting or the explanation box is still in this frame's event
        // state; answering the list with it would close a second window in one press.
        if (_skipResolutionInput)
        {
            _skipResolutionInput = false;

            return;
        }

        // The explanation box is modal over the list: while it is up the list must not see the same
        // press that closed the box, exactly like the blocking call of the reference.
        if (_resolutionTip is not null)
        {
            return;
        }

        DialogButton answer = picker.ProcessEvents(LocalEvent.Current);

        if (picker.TakePopup() is { } popup)
        {
            ShowResolutionTip(popup);

            return;
        }

        switch (answer)
        {
            case DialogButton.Ok when picker.IsSelected:
                ApplyFirstRunResolution(picker.SelectedResolution);
                break;

            case DialogButton.Cancel:
                _resolutionPicker = null;
                GameLog.Info("First run: resolution selection cancelled, keeping the current window.");
                FinishFirstRun();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Composes the explanation of a row into <see cref="_resolutionTip"/>, the
    /// <c>showStandardTextMessage( ..., Dialog::ZERO )</c> of <c>dialog_resolution.cpp:291-296</c>.
    /// </summary>
    /// <remarks>
    /// 参考的右键解释是带 OK 的模态框；端口没有可阻塞的窗口栈，于是把它当成场景级模态：
    /// <see cref="SetCursor"/>/<see cref="SetLeftButton"/>/<see cref="Select(char)"/> 都先让给它，
    /// <see cref="DriveResolutionPicker"/> 在它开着时不再驱动下面的列表。
    /// </remarks>
    /// <param name="popup">The header and body the dialog asked for.</param>
    private void ShowResolutionTip(DialogPopup popup)
    {
        if (_resolutionArtwork is null)
        {
            return;
        }

        _resolutionTip = MessageBox.Create(popup.Header, popup.Body, _fonts.Yellow, _fonts.Normal,
            buttons: DialogButton.Ok, buttonArtwork: _resolutionArtwork.Buttons);

        GameLog.Info($"First run: resolution explanation '{popup.Header}'.");
    }

    /// <summary>
    /// Switches the window to <paramref name="resolution"/> and ends the onboarding, the
    /// <c>engine().setResolution()</c> of <c>Dialog::SelectResolution()</c>.
    /// </summary>
    private void ApplyFirstRunResolution(ResolutionInfo resolution)
    {
        _resolutionPicker = null;

        try
        {
            LocalEvent.Current.Engine?.Display?.SetResolution(resolution);
            GameLog.Info($"First run: window resolution {resolution.ScreenWidth}x{resolution.ScreenHeight}.");
        }
        catch (ArgumentException exception)
        {
            GameLog.Warn($"First run: the window could not be resized: {exception.Message}");
        }

        FinishFirstRun();
    }

    /// <summary>Hands the end of the onboarding to the host, which clears the flag and saves the file.</summary>
    private void FinishFirstRun()
    {
        GameLog.Info($"First run finished, interface language '{Text.Culture}'.");
        FirstRunFinished?.Invoke();
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
    /// Whether the message frame <see cref="FrameBox"/> reads can be composed. The menu works without
    /// it, so missing artwork only costs the right click tooltips.
    /// </summary>
    private static bool HasMessageFrame(AssetBundle assets)
    {
        bool available = assets.TryReadIcn(FrameBox.ArtworkFileName) is { } file &&
            file.Count >= FrameBox.RequiredSpriteCount;

        if (!available)
        {
            GameLog.Warn(
                $"'{FrameBox.ArtworkFileName}' is missing or has fewer than " +
                $"{FrameBox.RequiredSpriteCount} sprites; the menu will not show tooltips.");
        }

        return available;
    }

    /// <summary>
    /// Builds the scroll bar, the board and the buttons of the resolution chooser of the first run.
    /// The menu works without it, so missing artwork only costs the resolution step.
    /// </summary>
    /// <param name="assets">Game data the sprites are read from.</param>
    /// <param name="buttons">Source of the OK and Cancel buttons; taken from the settings dialog.</param>
    /// <remarks>
    /// 参考这里读的是 <c>ICN::SCROLL</c> 与 <c>ICN::ADVBORD</c>，界面风格由
    /// <c>Settings::isEvilInterfaceEnabled()</c> 挑（<c>dialog_resolution.cpp:283-330</c>）。主菜单场景不持有设置，
    /// 菜单本身也一直用好的界面素材（消息框同理），所以这里固定读好界面的那两份。
    /// </remarks>
    private static ResolutionArtwork? CreateResolutionArtwork(AssetBundle assets,
        IDialogButtonArtworkSource? buttons)
    {
        IcnSpriteFile? scroll = assets.TryReadIcn(ResolutionScrollAssetName);
        IcnSpriteFile? board = assets.TryReadIcn(ResolutionBoardAssetName);

        if (buttons is null || scroll is null || scroll.Count <= 4 || board is null || board.Count == 0)
        {
            GameLog.Warn(
                $"The resolution chooser artwork is missing ('{ResolutionScrollAssetName}' or " +
                $"'{ResolutionBoardAssetName}'); the first run will not offer the resolution step.");

            return null;
        }

        return new ResolutionArtwork
        {
            ScrollBarFrames = scroll.Sprites,
            ScrollBarBackground = board.Sprites[0],
            Buttons = buttons,
        };
    }

    /// <summary>
    /// Builds the message box the reference shows while the right mouse button is held: the sign
    /// under the cursor wins over the settings door, and nothing else produces a tooltip.
    /// </summary>
    private MessageBox? CreateTip()
    {
        if (!_hasMessageFrame)
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

        return MessageBox.Create(header, body, _fonts.Yellow, _fonts.Normal);
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

        // New Game opens <c>Game::NewGame()</c>, the new game menu of the reference rather than the
        // scenario list directly. The original falls back to the list when the button archives are
        // missing, so a broken install still gets a playable path.
        if (entry.Action == MainMenuAction.NewGame)
        {
            _status = null;

            if (OpenNewGameMenu())
            {
                GameLog.Info($"Main menu: '{label}' opens the new game menu.");
                return entry.Action;
            }

            if (OpenScenario())
            {
                GameLog.Info($"Main menu: '{label}' falls back to the scenario selection.");
                return entry.Action;
            }
        }

        // LOAD GAME 的存档列表由宿主接管（GameHost.HandleMenuAction → OpenLoadGame）：场景只把选择交上去，
        // 不能在这里说「还没实现」，否则日志会把一条已经能走的路标成缺口。
        if (entry.Action == MainMenuAction.LoadGame)
        {
            _status = null;
            GameLog.Info($"Main menu: '{label}' opens the save file list.");
            return entry.Action;
        }

        // HIGH SCORES 同样是宿主接管的：屏幕在 GameHost.ShowHighScores 里开，场景只管把选择交上去。
        if (entry.Action == MainMenuAction.HighScores)
        {
            _status = null;
            GameLog.Info($"Main menu: '{label}' opens the high score table.");
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
