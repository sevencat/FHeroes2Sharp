using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.System;

/// <summary>What kind of game is being played, <c>Game::TYPE_*</c> (<c>game.h:39-54</c>).</summary>
[Flags]
public enum GameTypes
{
    /// <summary>No game at all — the menu, <c>TYPE_MENU</c>.</summary>
    None = 0,

    /// <summary>A single-player game, <c>TYPE_STANDARD</c>.</summary>
    Standard = 0x01,

    /// <summary>A campaign, <c>TYPE_CAMPAIGN</c>.</summary>
    Campaign = 0x02,

    /// <summary>A hot-seat game, <c>TYPE_HOTSEAT</c>.</summary>
    HotSeat = 0x04,

    /// <summary>A network game, <c>TYPE_NETWORK</c>.</summary>
    Network = 0x08,

    /// <summary>A battle-only game, <c>TYPE_BATTLEONLY</c>.</summary>
    BattleOnly = 0x10,

    /// <summary>An automatic map playtest, <c>TYPE_AUTO_PLAYTEST</c>.</summary>
    AutoPlaytest = 0x20,

    /// <summary>A game restored from a save file, <c>TYPE_LOADFILE</c>.</summary>
    LoadFile = 0x80,

    /// <summary>The reference's name for <see cref="HotSeat"/>, <c>TYPE_MULTI</c>.</summary>
    Multi = HotSeat,
}

/// <summary>How fast the adventure map scrolls at its edges, <c>AdventureMapScrollSpeed</c>.</summary>
public enum AdventureMapScrollSpeed
{
    /// <summary>No edge scrolling, <c>SCROLL_SPEED_NONE</c>.</summary>
    None = 0,

    /// <summary>Slow, <c>SCROLL_SPEED_SLOW</c>.</summary>
    Slow = 1,

    /// <summary>The default, <c>SCROLL_SPEED_NORMAL</c>.</summary>
    Normal = 2,

    /// <summary>Fast, <c>SCROLL_SPEED_FAST</c>.</summary>
    Fast = 3,

    /// <summary>The fastest, <c>SCROLL_SPEED_VERY_FAST</c>.</summary>
    VeryFast = 4,
}

/// <summary>Where the music comes from, <c>MusicSource</c>.</summary>
public enum MusicSource
{
    /// <summary>The original tracks, <c>MUSIC_MIDI_ORIGINAL</c>.</summary>
    MidiOriginal = 0,

    /// <summary>The Price of Loyalty tracks, <c>MUSIC_MIDI_EXPANSION</c>.</summary>
    MidiExpansion = 1,

    /// <summary>Files from the music directory, <c>MUSIC_EXTERNAL</c>.</summary>
    External = 2,
}

/// <summary>How far the world map is zoomed in, <c>ZoomLevel</c>.</summary>
public enum ZoomLevel : byte
{
    /// <summary>Zoomed out.</summary>
    Level0 = 0,

    /// <summary>The default.</summary>
    Level1 = 1,

    /// <summary>Zoomed in.</summary>
    Level2 = 2,

    /// <summary>Maximum zoom, debug builds only.</summary>
    Level3 = 3,
}

/// <summary>Which interface graphics are used, <c>InterfaceType</c>.</summary>
public enum InterfaceType : byte
{
    /// <summary>The good interface.</summary>
    Good = 0,

    /// <summary>The evil interface.</summary>
    Evil = 1,

    /// <summary>The interface follows the current player's race, <c>DYNAMIC</c>.</summary>
    Dynamic = 2,
}

/// <summary>How the save file list is ordered, <c>SaveFileSortingMethod</c>.</summary>
public enum SaveFileSortingMethod : byte
{
    /// <summary>By name.</summary>
    Filename = 0,

    /// <summary>By modification time.</summary>
    Timestamp = 1,
}

/// <summary>Where the battle turn order bar is drawn, <c>BattleTurnOrderState</c>.</summary>
public enum BattleTurnOrderState : byte
{
    /// <summary>Not drawn.</summary>
    Off = 0,

    /// <summary>Above the battlefield.</summary>
    Top = 1,

    /// <summary>Below the battlefield.</summary>
    Bottom = 2,
}

/// <summary>
/// The option flags of a game, <c>enum GameOptions</c> (<c>settings.cpp:54-82</c>).
/// </summary>
/// <remarks>
/// The numbers are what a configuration file stores, so their gaps are part of them: the reference jumps from
/// <c>0x400</c> to <c>0x8000</c> and leaves the bits between to retired options. The port keeps both the gaps and
/// <c>UNUSED_1</c> rather than renumbering.
/// </remarks>
[Flags]
public enum GameOptions : uint
{
    /// <summary>Nothing has been configured yet, <c>GAME_FIRST_RUN</c>.</summary>
    FirstRun = 0x00000001,

    /// <summary>A retired flag, <c>UNUSED_1</c>.</summary>
    Unused1 = 0x00000002,

    /// <summary>The Price of Loyalty content is available, <c>GAME_PRICELOYALTY</c>.</summary>
    PriceOfLoyalty = 0x00000004,

    /// <summary>Rendering is synchronised with the display, <c>GAME_RENDER_VSYNC</c>.</summary>
    RenderVSync = 0x00000008,

    /// <summary>Glyphs come from the font files, <c>GAME_TEXT_SUPPORT_MODE</c>.</summary>
    TextSupportMode = 0x00000010,

    /// <summary>The cursor is black and white, <c>GAME_MONOCHROME_CURSOR</c>.</summary>
    MonochromeCursor = 0x00000020,

    /// <summary>The control panel is drawn, <c>GAME_SHOW_CONTROL_PANEL</c>.</summary>
    ShowControlPanel = 0x00000040,

    /// <summary>The radar is drawn, <c>GAME_SHOW_RADAR</c>.</summary>
    ShowRadar = 0x00000080,

    /// <summary>The map icons are drawn, <c>GAME_SHOW_ICONS</c>.</summary>
    ShowIcons = 0x00000100,

    /// <summary>The buttons are drawn, <c>GAME_SHOW_BUTTONS</c>.</summary>
    ShowButtons = 0x00000200,

    /// <summary>The status line is drawn, <c>GAME_SHOW_STATUS</c>.</summary>
    ShowStatus = 0x00000400,

    /// <summary>The window is full screen, <c>GAME_FULLSCREEN</c>.</summary>
    FullScreen = 0x00008000,

    /// <summary>Sound is positioned in three dimensions, <c>GAME_3D_AUDIO</c>.</summary>
    Audio3D = 0x00010000,

    /// <summary>The system information overlay is drawn, <c>GAME_SYSTEM_INFO</c>.</summary>
    SystemInfo = 0x00020000,

    /// <summary>The cursor is drawn by software, <c>GAME_CURSOR_SOFT_EMULATION</c>.</summary>
    CursorSoftwareEmulation = 0x00040000,

    /// <summary>The reachable area is highlighted, <c>GAME_BATTLE_HIGHLIGHT_MOVEMENT_AREA</c>.</summary>
    BattleHighlightMovementArea = 0x00080000,

    /// <summary>The adventure interface is hidden, <c>GAME_HIDE_INTERFACE</c>.</summary>
    HideInterface = 0x00100000,

    /// <summary>Damage numbers are shown, <c>GAME_BATTLE_SHOW_DAMAGE</c>.</summary>
    BattleShowDamage = 0x00200000,

    /// <summary>Hit point bars are shown, <c>GAME_BATTLE_SHOW_HIT_POINTS_BAR</c>.</summary>
    BattleShowHitPointsBar = 0x00400000,

    /// <summary>The battle grid is shown, <c>GAME_BATTLE_SHOW_GRID</c>.</summary>
    BattleShowGrid = 0x00800000,

    /// <summary>The mouse cursor casts a shadow, <c>GAME_BATTLE_SHOW_MOUSE_SHADOW</c>.</summary>
    BattleShowMouseShadow = 0x01000000,

    /// <summary>The selected unit casts a shadow, <c>GAME_BATTLE_SHOW_MOVE_SHADOW</c>.</summary>
    BattleShowMoveShadow = 0x02000000,

    /// <summary>Battles are resolved without being fought, <c>GAME_BATTLE_AUTO_RESOLVE</c>.</summary>
    BattleAutoResolve = 0x04000000,

    /// <summary>The computer casts without the book, <c>GAME_BATTLE_AUTO_SPELLCAST</c>.</summary>
    BattleAutoSpellcast = 0x08000000,

    /// <summary>The game saves itself every turn, <c>GAME_AUTO_SAVE_AT_BEGINNING_OF_TURN</c>.</summary>
    AutoSaveAtBeginningOfTurn = 0x10000000,

    /// <summary>Nearest neighbour scaling, <c>GAME_SCREEN_SCALING_TYPE_NEAREST</c>.</summary>
    ScreenScalingTypeNearest = 0x20000000,

    /// <summary>Armies are counted, <c>GAME_NUMERIC_ARMY_ESTIMATION_VIEW</c>.</summary>
    NumericArmyEstimationView = 0x40000000,
}

/// <summary>The option flags of the editor, <c>enum EditorOptions</c> (<c>settings.cpp:84-89</c>).</summary>
[Flags]
public enum EditorOptions : uint
{
    /// <summary>A retired flag, <c>EDITOR_UNUSED</c>.</summary>
    Unused = 0x00000001,

    /// <summary>Objects are animated, <c>EDITOR_ANIMATION</c>.</summary>
    Animation = 0x00000002,

    /// <summary>Passability is shown, <c>EDITOR_PASSABILITY</c>.</summary>
    Passability = 0x00000004,
}

/// <summary>
/// Every knob of the game, <c>class Settings</c> (<c>settings.h:31-480</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is a singleton built from the configuration file, and it is the only object that knows both the
/// session (game type, difficulty, current map) and the machine (volumes, speeds, window). The port keeps the same
/// split and the same singleton, because dozens of call sites ask it questions without being handed anything.
/// </para>
/// <para>
/// <b>What is not here</b>, with a note rather than a fake: <c>Read</c>/<c>Save</c>/<c>getOptionsString</c>, which
/// parse the configuration file with the unported <c>tinyconfig</c>; the resolution list, window position and the
/// rendered zoom, which belong to <c>screen.cpp</c>; and the set-up screen's file search, which walks the file
/// system through the unported <c>dir.cpp</c>.
/// </para>
/// <para>
/// The setters keep the reference's clamping instead of trusting their caller: a volume of 25 becomes 10 and a hero
/// speed of 0 becomes 1, because the configuration file is written by hand and a zero would stop the game rather
/// than slow it down.
/// </para>
/// </remarks>
public sealed class Settings
{
    /// <summary>The default speed of a battle, <c>defaultBattleSpeed</c>.</summary>
    public const int DefaultBattleSpeed = 4;

    /// <summary>The default delay of one step, <c>defaultSpeedDelay</c>.</summary>
    public const int DefaultSpeedDelay = 5;

    private static Settings? instance;

    private readonly BitModes gameOptions = new();
    private readonly BitModes editorOptions = new();

    /// <summary>
    /// The settings with the reference's first-run defaults, <c>Settings::Settings()</c>
    /// (<c>settings.cpp:94-146</c>).
    /// </summary>
    /// <remarks>
    /// The radar, the icons, the buttons, the status line, the battle grid and both battle shadows are on, and the
    /// game is marked as never configured. A configuration file then overrides what it mentions; everything else
    /// keeps these values. The reference switches several defaults for a handheld device; the port has no way to ask
    /// and keeps the desktop set.
    /// </remarks>
    private Settings()
    {
        gameOptions.SetModes((uint)(GameOptions.FirstRun
            | GameOptions.ShowRadar
            | GameOptions.ShowIcons
            | GameOptions.ShowButtons
            | GameOptions.ShowStatus
            | GameOptions.BattleShowGrid
            | GameOptions.BattleShowMouseShadow
            | GameOptions.BattleShowMoveShadow
            | GameOptions.BattleAutoResolve));

        editorOptions.SetModes((uint)EditorOptions.Animation);
    }

    /// <summary>The one set of settings, <c>Settings::Get()</c> (<c>settings.cpp:148-153</c>).</summary>
    /// <returns>The singleton.</returns>
    public static Settings Get() => instance ??= new Settings();

    /// <summary>
    /// Forgets the singleton so that the next <see cref="Get"/> builds the first-run defaults again.
    /// </summary>
    /// <remarks>
    /// Only the tests use this. The game builds its settings once — from the configuration file when one exists —
    /// and the reference never lets go of them either; there is no <c>Settings::reset()</c> to port.
    /// </remarks>
    internal static void Reset() => instance = null;

    /// <summary>The slots of the current map, <c>GetPlayers()</c>.</summary>
    public Players Players { get; } = new Players();

    /// <summary>The option flags of the game, <c>_gameOptions</c>.</summary>
    public uint GameOptionFlags => gameOptions.Modes;

    /// <summary>The option flags of the editor, <c>_editorOptions</c>.</summary>
    public uint EditorOptionFlags => editorOptions.Modes;

    /// <summary>The difficulty, <c>GameDifficulty()</c>.</summary>
    public GameDifficulty GameDifficulty { get; set; } = GameDifficulty.Normal;

    /// <summary>The sorting of the save list, <c>getSaveFileSortType()</c>.</summary>
    public SaveFileSortingMethod SaveFileSortType { get; set; } = SaveFileSortingMethod.Filename;

    /// <summary>How far the world map is zoomed, <c>ViewWorldZoomLevel()</c>.</summary>
    public ZoomLevel ViewWorldZoomLevel { get; set; } = ZoomLevel.Level1;

    /// <summary>Where the turn order bar is drawn, <c>battleTurnOrderState()</c>.</summary>
    public BattleTurnOrderState BattleTurnOrder { get; private set; } = BattleTurnOrderState.Off;

    /// <summary>Which interface graphics are used, <c>interfaceType()</c>.</summary>
    public InterfaceType InterfaceType { get; private set; } = InterfaceType.Good;

    /// <summary>Sets the interface set, <c>setInterfaceType()</c>.</summary>
    /// <param name="type">The interface set.</param>
    public void SetInterfaceType(InterfaceType type) => InterfaceType = type;

    /// <summary>
    /// Whether the graphics on screen should be the evil ones, <c>isEvilInterfaceEnabled()</c>
    /// (<c>settings.cpp:1014-1045</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two fixed interface sets answer directly. The dynamic set follows the race of the player whose turn it is,
    /// because the reference wants the screen to match the kingdom being played.
    /// </para>
    /// <para>
    /// During an AI turn there is no human to follow, so the reference keeps the interface of the last human player in
    /// the slot order — the screen must not flip to evil just because the computer took a turn. With no human slot at
    /// all, and with a current slot that has dropped out of the game, the answer is the good interface.
    /// </para>
    /// </remarks>
    public bool IsEvilInterfaceEnabled
    {
        get
        {
            if (InterfaceType == InterfaceType.Good)
            {
                return false;
            }

            if (InterfaceType == InterfaceType.Evil)
            {
                return true;
            }

            Player? current = Players.Current;

            if (current == null || !current.IsPlay)
            {
                return false;
            }

            if (current.IsControlHuman)
            {
                return MapRaces.IsEvil(current.Race);
            }

            for (int index = Players.All.Count - 1; index >= 0; --index)
            {
                Player player = Players.All[index];

                if (player.IsControlHuman)
                {
                    return MapRaces.IsEvil(player.Race);
                }
            }

            return false;
        }
    }

    /// <summary>The music source, <c>MusicType()</c>.</summary>
    public MusicSource MusicType { get; private set; } = MusicSource.External;

    /// <summary>The language of the texts, <c>GameLanguage()</c>.</summary>
    public string GameLanguage { get; private set; } = string.Empty;

    /// <summary>The map being played, <c>getCurrentMapFileInfo()</c>; null before one is chosen.</summary>
    public Mp2FileInfo? CurrentMapFileInfo { get; private set; }

    /// <summary>Smooth scrolling of the adventure map, <c>isMapSmoothScrollingEnabled()</c>.</summary>
    public bool IsMapSmoothScrollingEnabled { get; private set; }

    /// <summary>The type of the game, <c>GameType()</c>.</summary>
    public GameTypes GameType { get; private set; } = GameTypes.None;

    /// <summary>The speed of a hero's steps, 1 to 10, <c>HeroesMoveSpeed()</c>.</summary>
    public int HeroesMoveSpeed { get; private set; } = DefaultSpeedDelay;

    /// <summary>The speed of the computer's actions, 0 to 10, <c>AIMoveSpeed()</c>.</summary>
    public int AiMoveSpeed { get; private set; } = DefaultSpeedDelay;

    /// <summary>How fast the map scrolls at its edges, <c>ScrollSpeed()</c>.</summary>
    public AdventureMapScrollSpeed ScrollSpeed { get; private set; } = AdventureMapScrollSpeed.Normal;

    /// <summary>The speed of a battle, 1 to 10, <c>BattleSpeed()</c>.</summary>
    public int BattleSpeed { get; private set; } = DefaultBattleSpeed;

    /// <summary>The sound volume, 0 to 10, <c>SoundVolume()</c>.</summary>
    public int SoundVolume { get; private set; } = 6;

    /// <summary>The music volume, 0 to 10, <c>MusicVolume()</c>.</summary>
    public int MusicVolume { get; private set; } = 6;

    /// <summary>The speed of the controller pointer, <c>ControllerPointerSpeed()</c>.</summary>
    public int ControllerPointerSpeed { get; private set; } = 10;

    /// <summary>Whether the game is a campaign, <c>isCampaignGameType()</c>.</summary>
    public bool IsCampaignGameType => (GameType & GameTypes.Campaign) != 0;

    /// <summary>Whether nothing has been configured yet, <c>isFirstGameRun()</c>.</summary>
    public bool IsFirstGameRun => gameOptions.HasModes((uint)GameOptions.FirstRun);

    /// <summary>Whether the Price of Loyalty content is available, <c>isPriceOfLoyaltySupported()</c>.</summary>
    public bool IsPriceOfLoyaltySupported => gameOptions.HasModes((uint)GameOptions.PriceOfLoyalty);

    /// <summary>Whether sound effects are positioned in three dimensions, <c>is3DAudioEnabled()</c>.</summary>
    public bool Is3DAudioEnabled => gameOptions.HasModes((uint)GameOptions.Audio3D);

    /// <summary>
    /// Whether the music comes from the MIDI files of the archives rather than from the music folder,
    /// <c>isMusicTypeMidi()</c> (<c>settings.h:249</c>).
    /// </summary>
    public bool IsMusicTypeMidi => MusicType is MusicSource.MidiOriginal or MusicSource.MidiExpansion;

    /// <summary>Whether rendering is synchronised with the display, <c>isVSyncEnabled()</c>.</summary>
    public bool IsVSyncEnabled => gameOptions.HasModes((uint)GameOptions.RenderVSync);

    /// <summary>Whether the window is full screen, <c>isFullScreen()</c>.</summary>
    public bool IsFullScreen => gameOptions.HasModes((uint)GameOptions.FullScreen);

    /// <summary>Whether the control panel is drawn, <c>ShowControlPanel()</c>.</summary>
    public bool ShowControlPanel => gameOptions.HasModes((uint)GameOptions.ShowControlPanel);

    /// <summary>Whether the radar is drawn, <c>isShowRadar()</c>.</summary>
    public bool IsShowRadar => gameOptions.HasModes((uint)GameOptions.ShowRadar);

    /// <summary>Whether the map icons are drawn, <c>isShowIcons()</c>.</summary>
    public bool IsShowIcons => gameOptions.HasModes((uint)GameOptions.ShowIcons);

    /// <summary>Whether the buttons are drawn, <c>isShowButtons()</c>.</summary>
    public bool IsShowButtons => gameOptions.HasModes((uint)GameOptions.ShowButtons);

    /// <summary>Whether the status line is drawn, <c>isShowStatus()</c>.</summary>
    public bool IsShowStatus => gameOptions.HasModes((uint)GameOptions.ShowStatus);

    /// <summary>Whether the adventure interface is hidden, <c>isHideInterface()</c>.</summary>
    public bool IsHideInterface => gameOptions.HasModes((uint)GameOptions.HideInterface);

    /// <summary>Whether damage numbers are shown, <c>isBattleShowDamage()</c>.</summary>
    public bool IsBattleShowDamage => gameOptions.HasModes((uint)GameOptions.BattleShowDamage);

    /// <summary>Whether hit point bars are shown, <c>isBattleHitPointsBarVisible()</c>.</summary>
    public bool IsBattleShowHitPointsBar =>
        gameOptions.HasModes((uint)GameOptions.BattleShowHitPointsBar);

    /// <summary>Whether the battle grid is drawn, <c>isBattleShowGrid()</c>.</summary>
    public bool IsBattleShowGrid => gameOptions.HasModes((uint)GameOptions.BattleShowGrid);

    /// <summary>Whether the mouse shadow is drawn, <c>isBattleShowMouseShadow()</c>.</summary>
    public bool IsBattleShowMouseShadow => gameOptions.HasModes((uint)GameOptions.BattleShowMouseShadow);

    /// <summary>Whether the movement shadow is drawn, <c>isBattleShowMoveShadow()</c>.</summary>
    public bool IsBattleShowMoveShadow => gameOptions.HasModes((uint)GameOptions.BattleShowMoveShadow);

    /// <summary>Whether battles are auto resolved, <c>BattleAutoResolve()</c>.</summary>
    public bool IsBattleAutoResolve => gameOptions.HasModes((uint)GameOptions.BattleAutoResolve);

    /// <summary>Whether the computer casts without the book, <c>BattleAutoSpellcast()</c>.</summary>
    public bool IsBattleAutoSpellcast => gameOptions.HasModes((uint)GameOptions.BattleAutoSpellcast);

    /// <summary>Whether the game saves itself every turn, <c>isAutoSaveAtBeginningOfTurnEnabled()</c>.</summary>
    public bool IsAutoSaveAtBeginningOfTurnEnabled =>
        gameOptions.HasModes((uint)GameOptions.AutoSaveAtBeginningOfTurn);

    /// <summary>Whether nearest neighbour scaling is used, <c>isScreenScalingTypeNearest()</c>.</summary>
    public bool IsScreenScalingTypeNearest =>
        gameOptions.HasModes((uint)GameOptions.ScreenScalingTypeNearest);

    /// <summary>Whether armies are counted, <c>isNumericArmyEstimationView()</c>.</summary>
    public bool IsNumericArmyEstimationView =>
        gameOptions.HasModes((uint)GameOptions.NumericArmyEstimationView);

    /// <summary>Whether the monochrome cursor is used, <c>isMonochromeCursorEnabled()</c>.</summary>
    public bool IsMonochromeCursor => gameOptions.HasModes((uint)GameOptions.MonochromeCursor);

    /// <summary>Whether the system information overlay is drawn, <c>isSystemInfo()</c>.</summary>
    public bool IsSystemInfo => gameOptions.HasModes((uint)GameOptions.SystemInfo);

    /// <summary>Whether the cursor is drawn by software, <c>isCursorSoftwareEmulationEnabled()</c>.</summary>
    public bool IsCursorSoftwareEmulation =>
        gameOptions.HasModes((uint)GameOptions.CursorSoftwareEmulation);

    /// <summary>Whether the reachable area is highlighted, <c>isBattleMovementAreaHighlightEnabled()</c>.</summary>
    public bool IsBattleMovementAreaHighlight =>
        gameOptions.HasModes((uint)GameOptions.BattleHighlightMovementArea);

    /// <summary>Whether objects are animated in the editor, <c>isEditorAnimationEnabled()</c>.</summary>
    public bool IsEditorAnimationEnabled => editorOptions.HasModes((uint)EditorOptions.Animation);

    /// <summary>Whether passability is shown in the editor, <c>isEditorPassabilityEnabled()</c>.</summary>
    public bool IsEditorPassabilityEnabled => editorOptions.HasModes((uint)EditorOptions.Passability);

    /// <summary>Whether a game of the given type is running, <c>IsGameType()</c> (<c>settings.h:345-348</c>).</summary>
    /// <param name="gameType">The type to test; <see cref="GameTypes.None"/> always answers false.</param>
    /// <returns>True when any of the bits is set.</returns>
    public bool IsGameType(GameTypes gameType) => (GameType & gameType) != 0;

    /// <summary>Sets the type of the game, <c>SetGameType()</c>.</summary>
    /// <param name="gameType">The new type.</param>
    public void SetGameType(GameTypes gameType) => GameType = gameType;

    /// <summary>Clears the "never configured" mark, <c>resetFirstGameRun()</c>.</summary>
    public void ResetFirstGameRun() => gameOptions.ResetModes((uint)GameOptions.FirstRun);

    /// <summary>Records the map being played, <c>setCurrentMapFileInfo()</c>.</summary>
    /// <param name="fileInfo">The map header.</param>
    public void SetCurrentMapFileInfo(Mp2FileInfo fileInfo) => CurrentMapFileInfo = fileInfo;

    /// <summary>
    /// Records the language of the texts, <c>setGameLanguage()</c>.
    /// </summary>
    /// <remarks>
    /// The reference hands the language to the translation layer as well; the port has none, so only the name is kept
    /// (ADR-137).
    /// </remarks>
    /// <param name="language">The language name, for example <c>"de"</c>.</param>
    public void SetGameLanguage(string language) => GameLanguage = language;

    /// <summary>Sets the music source, <c>setMusicType()</c>.</summary>
    /// <param name="source">The source.</param>
    public void SetMusicType(MusicSource source) => MusicType = source;

    /// <summary>Switches the smooth scrolling of the map, <c>setMapSmoothScrolling()</c>.</summary>
    /// <param name="enable">True to scroll smoothly.</param>
    public void SetMapSmoothScrolling(bool enable) => IsMapSmoothScrollingEnabled = enable;

    /// <summary>Sets the sound volume, clamped to 0 to 10, <c>SetSoundVolume()</c>.</summary>
    /// <param name="volume">The volume.</param>
    public void SetSoundVolume(int volume) => SoundVolume = Math.Clamp(volume, 0, 10);

    /// <summary>Sets the music volume, clamped to 0 to 10, <c>SetMusicVolume()</c>.</summary>
    /// <param name="volume">The volume.</param>
    public void SetMusicVolume(int volume) => MusicVolume = Math.Clamp(volume, 0, 10);

    /// <summary>
    /// Sets the speed of the computer, clamped to 0 to 10, <c>SetAIMoveSpeed()</c>.
    /// </summary>
    /// <remarks>
    /// Zero is allowed here and nowhere else: a configuration file may ask for an instant computer, and the game
    /// reads zero as "no delay" rather than as an error.
    /// </remarks>
    /// <param name="speed">The speed.</param>
    public void SetAiMoveSpeed(int speed) => AiMoveSpeed = Math.Clamp(speed, 0, 10);

    /// <summary>Sets the speed of a hero's steps, clamped to 1 to 10, <c>SetHeroesMoveSpeed()</c>.</summary>
    /// <param name="speed">The speed.</param>
    public void SetHeroesMoveSpeed(int speed) => HeroesMoveSpeed = Math.Clamp(speed, 1, 10);

    /// <summary>Sets the speed of a battle, clamped to 1 to 10, <c>SetBattleSpeed()</c>.</summary>
    /// <param name="speed">The speed.</param>
    public void SetBattleSpeed(int speed) => BattleSpeed = Math.Clamp(speed, 1, 10);

    /// <summary>
    /// Sets the edge scrolling speed, clamped to the five values of the enum, <c>SetScrollSpeed()</c>.
    /// </summary>
    /// <param name="speed">The speed as an integer, as a configuration file stores it.</param>
    public void SetScrollSpeed(int speed) =>
        ScrollSpeed = (AdventureMapScrollSpeed)Math.Clamp(
            speed,
            (int)AdventureMapScrollSpeed.None,
            (int)AdventureMapScrollSpeed.VeryFast);

    /// <summary>Sets the difficulty, <c>SetGameDifficulty()</c>.</summary>
    /// <param name="difficulty">The difficulty.</param>
    public void SetGameDifficulty(GameDifficulty difficulty) => GameDifficulty = difficulty;

    /// <summary>
    /// Turns the Price of Loyalty content on or off, <c>EnablePriceOfLoyaltySupport()</c>
    /// (<c>settings.cpp:1212-1226</c>).
    /// </summary>
    /// <remarks>
    /// Switching it off also moves the music off the expansion tracks: those tracks are files of the expansion, and the
    /// reference refuses to keep pointing at them.
    /// </remarks>
    /// <param name="enable">True when the expansion content is available.</param>
    public void EnablePriceOfLoyaltySupport(bool enable)
    {
        SetGameOption(GameOptions.PriceOfLoyalty, enable);

        if (!enable && MusicType == MusicSource.MidiExpansion)
        {
            MusicType = MusicSource.MidiOriginal;
        }
    }

    /// <summary>Switches full screen, <c>setFullScreen()</c>.</summary>
    /// <param name="enable">True for full screen.</param>
    public void SetFullScreen(bool enable) => SetGameOption(GameOptions.FullScreen, enable);

    /// <summary>Switches render synchronisation, <c>setVSync()</c>.</summary>
    /// <param name="enable">True to synchronise.</param>
    public void SetVSync(bool enable) => SetGameOption(GameOptions.RenderVSync, enable);

    /// <summary>Switches the control panel, <c>SetShowControlPanel()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetShowControlPanel(bool enable) => SetGameOption(GameOptions.ShowControlPanel, enable);

    /// <summary>Switches the radar, <c>SetShowRadar()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetShowRadar(bool enable) => SetGameOption(GameOptions.ShowRadar, enable);

    /// <summary>Switches the map icons, <c>SetShowIcons()</c>.</summary>
    /// <param name="enable">True to draw them.</param>
    public void SetShowIcons(bool enable) => SetGameOption(GameOptions.ShowIcons, enable);

    /// <summary>Switches the buttons, <c>SetShowButtons()</c>.</summary>
    /// <param name="enable">True to draw them.</param>
    public void SetShowButtons(bool enable) => SetGameOption(GameOptions.ShowButtons, enable);

    /// <summary>Switches the status line, <c>SetShowStatus()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetShowStatus(bool enable) => SetGameOption(GameOptions.ShowStatus, enable);

    /// <summary>Switches the hidden adventure interface, <c>setHideInterface()</c>.</summary>
    /// <param name="enable">True to hide it.</param>
    public void SetHideInterface(bool enable) => SetGameOption(GameOptions.HideInterface, enable);

    /// <summary>Switches the battle grid, <c>SetBattleGrid()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetBattleGrid(bool enable) => SetGameOption(GameOptions.BattleShowGrid, enable);

    /// <summary>Switches the movement shadow, <c>SetBattleMovementShaded()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetBattleMovementShaded(bool enable) =>
        SetGameOption(GameOptions.BattleShowMoveShadow, enable);

    /// <summary>Switches the mouse shadow, <c>SetBattleMouseShaded()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetBattleMouseShaded(bool enable) =>
        SetGameOption(GameOptions.BattleShowMouseShadow, enable);

    /// <summary>Switches damage numbers, <c>setBattleShowDamage()</c>.</summary>
    /// <param name="enable">True to show them.</param>
    public void SetBattleShowDamage(bool enable) => SetGameOption(GameOptions.BattleShowDamage, enable);

    /// <summary>Switches the hit point bars, <c>setBattleShowHitPointsBar()</c>.</summary>
    /// <param name="enable">True to show them.</param>
    public void SetBattleShowHitPointsBar(bool enable) =>
        SetGameOption(GameOptions.BattleShowHitPointsBar, enable);

    /// <summary>Switches the three dimensional sound effects, <c>set3DAudio()</c>.</summary>
    /// <param name="enable">True to position sound effects in the world.</param>
    public void Set3DAudio(bool enable) => SetGameOption(GameOptions.Audio3D, enable);

    /// <summary>Switches auto resolved battles, <c>setBattleAutoResolve()</c>.</summary>
    /// <param name="enable">True to resolve battles automatically.</param>
    public void SetBattleAutoResolve(bool enable) =>
        SetGameOption(GameOptions.BattleAutoResolve, enable);

    /// <summary>Switches the computer's silent spell casting, <c>setBattleAutoSpellcast()</c>.</summary>
    /// <param name="enable">True to cast without the book.</param>
    public void SetBattleAutoSpellcast(bool enable) =>
        SetGameOption(GameOptions.BattleAutoSpellcast, enable);

    /// <summary>Switches the reachable area highlight, <c>setBattleMovementAreaHighlight()</c>.</summary>
    /// <param name="enable">True to highlight it.</param>
    public void SetBattleMovementAreaHighlight(bool enable) =>
        SetGameOption(GameOptions.BattleHighlightMovementArea, enable);

    /// <summary>Switches the turn-start save, <c>setAutoSaveAtBeginningOfTurn()</c>.</summary>
    /// <param name="enable">True to save every turn.</param>
    public void SetAutoSaveAtBeginningOfTurn(bool enable) =>
        SetGameOption(GameOptions.AutoSaveAtBeginningOfTurn, enable);

    /// <summary>Switches nearest neighbour scaling, <c>setScreenScalingTypeNearest()</c>.</summary>
    /// <param name="enable">True for nearest neighbour.</param>
    public void SetScreenScalingTypeNearest(bool enable) =>
        SetGameOption(GameOptions.ScreenScalingTypeNearest, enable);

    /// <summary>Switches counted armies, <c>setNumericArmyEstimationView()</c>.</summary>
    /// <param name="enable">True to count instead of estimating.</param>
    public void SetNumericArmyEstimationView(bool enable) =>
        SetGameOption(GameOptions.NumericArmyEstimationView, enable);

    /// <summary>Switches the monochrome cursor, <c>setMonochromeCursorEnabled()</c>.</summary>
    /// <param name="enable">True for the black and white cursor.</param>
    public void SetMonochromeCursor(bool enable) => SetGameOption(GameOptions.MonochromeCursor, enable);

    /// <summary>Switches the software cursor, <c>setCursorSoftwareEmulation()</c>.</summary>
    /// <param name="enable">True to draw the cursor in software.</param>
    public void SetCursorSoftwareEmulation(bool enable) =>
        SetGameOption(GameOptions.CursorSoftwareEmulation, enable);

    /// <summary>Switches the system information overlay, <c>setSystemInfo()</c>.</summary>
    /// <param name="enable">True to draw it.</param>
    public void SetSystemInfo(bool enable) => SetGameOption(GameOptions.SystemInfo, enable);

    /// <summary>Switches object animation in the editor, <c>setEditorAnimation()</c>.</summary>
    /// <param name="enable">True to animate.</param>
    public void SetEditorAnimation(bool enable) => SetEditorOption(EditorOptions.Animation, enable);

    /// <summary>Switches the passability overlay in the editor, <c>setEditorPassability()</c>.</summary>
    /// <param name="enable">True to show it.</param>
    public void SetEditorPassability(bool enable) => SetEditorOption(EditorOptions.Passability, enable);

    /// <summary>
    /// Cycles the interface set, <c>switchToNextInterfaceType()</c> (<c>settings.cpp:1051-1070</c>).
    /// </summary>
    /// <remarks>
    /// The order is dynamic, good, evil: the first press leaves the dynamic mode, the second goes evil, the third
    /// comes back round to dynamic.
    /// </remarks>
    public void SwitchToNextInterfaceType() => InterfaceType = InterfaceType switch
    {
        InterfaceType.Dynamic => InterfaceType.Good,
        InterfaceType.Good => InterfaceType.Evil,
        _ => InterfaceType.Dynamic
    };

    /// <summary>
    /// Cycles the turn order bar, <c>switchToNextBattleTurnOrderState()</c> (<c>settings.cpp:1072-1086</c>).
    /// </summary>
    public void SwitchToNextBattleTurnOrderState() => BattleTurnOrder = BattleTurnOrder switch
    {
        BattleTurnOrderState.Off => BattleTurnOrderState.Top,
        BattleTurnOrderState.Top => BattleTurnOrderState.Bottom,
        _ => BattleTurnOrderState.Off
    };

    /// <summary>Sets one game flag, the body every <c>Set…( bool )</c> of the reference reduces to.</summary>
    /// <param name="option">The flag.</param>
    /// <param name="enable">True to set it, false to clear it.</param>
    private void SetGameOption(GameOptions option, bool enable)
    {
        if (enable)
        {
            gameOptions.SetModes((uint)option);
        }
        else
        {
            gameOptions.ResetModes((uint)option);
        }
    }

    /// <summary>Sets one editor flag, the body of the two <c>set…( bool )</c> of the reference.</summary>
    /// <param name="option">The flag.</param>
    /// <param name="enable">True to set it, false to clear it.</param>
    private void SetEditorOption(EditorOptions option, bool enable)
    {
        if (enable)
        {
            editorOptions.SetModes((uint)option);
        }
        else
        {
            editorOptions.ResetModes((uint)option);
        }
    }
}
