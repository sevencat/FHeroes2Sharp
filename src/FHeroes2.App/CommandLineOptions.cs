using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.App;

/// <summary>
/// Command line of the desktop client.
///
/// <code>
///   FHeroes2Sharp [--data &lt;dir&gt;] [--config &lt;file&gt;|--no-config] [--scene menu|diagnostics|editor]
///                 [--lang &lt;culture&gt;]
///                 [--scale &lt;1..4&gt;] [--fullscreen] [--smoke-frames &lt;n&gt;] [--list-assets]
///                 [--cursor &lt;x,y&gt;] [--right-button] [--left-click] [--freeze-animation]
///                 [--system-info]
///                 [--open-scenario] [--scenario-select &lt;n&gt;] [--enter-map] [--map &lt;file&gt;]
///                 [--skip-turns &lt;n&gt;] [--view-world] [--enter-town] [--town-select &lt;n&gt;] [--town-build &lt;name&gt;]
///                 [--town-window &lt;name&gt;] [--town-building &lt;name&gt;] [--town-hero &lt;0|1&gt;]
///                 [--editor-paint &lt;x,y,w,h,ground&gt;] [--editor-save &lt;file&gt;]
///                 [--editor-new &lt;36|72|108|144&gt;] [--editor-object &lt;name|n&gt;]
///                 [--editor-place &lt;x,y[;x,y...]&gt;] [--editor-radar-click &lt;x,y&gt;]
///                 [--editor-panel-click &lt;x,y[;x,y...]&gt;] [--editor-info &lt;x,y&gt;]
///                 [--editor-data &lt;x,y,value&gt;]
///                 [--no-audio] [--play-sound &lt;name&gt;]
///                 [--screenshot &lt;png&gt;] [--screenshot-frame &lt;n&gt;]
/// </code>
/// </summary>
public sealed class CommandLineOptions
{
    public const int MinimumScale = 1;
    public const int MaximumScale = 4;

    private CommandLineOptions()
    {
    }

    /// <summary>Explicit game data directory passed with <c>--data</c>.</summary>
    public string? DataDirectory { get; private set; }

    /// <summary>
    /// The configuration file passed with <c>--config &lt;file&gt;</c>, overriding the reference's default
    /// <c>%APPDATA%\fheroes2\fheroes2.cfg</c>.
    /// </summary>
    /// <remarks>
    /// 参考没有这个开关：它只在 <c>%APPDATA%\fheroes2</c>（以及若干候选目录）里找 <c>fheroes2.cfg</c>
    /// （<c>system.cpp:105-118</c>、<c>settings.cpp:754-758</c>）。端口加它是因为基线截图与冒烟必须与开发机上
    /// 已有的偏好无关——同一个 exe 跑基线时用的是真实用户的配置文件。
    /// </remarks>
    public string? ConfigFile { get; private set; }

    /// <summary>
    /// Leaves the configuration file alone (<c>--no-config</c>): nothing is read at start up and nothing is
    /// written afterwards.
    /// </summary>
    /// <remarks>
    /// 同 <see cref="ConfigFile"/> 的理由，只是更彻底：连默认路径都不碰，基线因此连「写一份默认配置」都不会发生。
    /// </remarks>
    public bool NoConfig { get; private set; }

    /// <summary>Screen the client starts with.</summary>
    public SceneKind Scene { get; private set; } = SceneKind.Menu;

    /// <summary>Culture of the interface text passed with <c>--lang</c>, e.g. <c>zh_CN</c>.</summary>
    public string? Language { get; private set; }

    /// <summary>
    /// Initial cursor position in logical (640x480) pixels passed with <c>--cursor</c>. Smoke runs
    /// use it to capture the hover state of a widget, which no headless test can produce. The
    /// position is synthetic: the host then ignores real mouse motion and mouse-leave events, so
    /// the captured hover does not depend on where the physical pointer happens to be.
    /// </summary>
    public Point? Cursor { get; private set; }

    /// <summary>
    /// Pretends the right mouse button is held down at the position of <c>--cursor</c>. The
    /// tooltips of the main menu and the marketplace's kingdom income popup only exist while the
    /// button is pressed, so this is what makes a screenshot of one possible; it has no effect
    /// without <c>--cursor</c>.
    /// </summary>
    public bool RightButton { get; private set; }

    /// <summary>
    /// Clicks the left mouse button once at the position of <c>--cursor</c>, on the tenth frame of
    /// the loop: press and release in the same frame, in the same order the event loop produces. It
    /// is how a screenshot can walk through a click-only path (the editor entrance) and how the log
    /// shows whether the hit test matched; it has no effect without <c>--cursor</c>.
    /// </summary>
    public bool LeftClick { get; private set; }

    /// <summary>Window scale factor applied to the 640x480 logical resolution.</summary>
    public int WindowScale { get; private set; } = 2;

    public bool Fullscreen { get; private set; }

    /// <summary>When set the client exits after rendering the given number of frames.</summary>
    public int? SmokeFrames { get; private set; }

    /// <summary>
    /// Keeps the main menu lantern on its first glow frame. The animation is driven by wall clock
    /// time, so a pixel baseline would otherwise need to know which frame was captured.
    /// </summary>
    public bool FreezeAnimation { get; private set; }

    /// <summary>
    /// Opens the settings dialog of the main menu at start up (<c>--open-settings</c>). A click on
    /// the menu door does the same; the flag exists because a screenshot of a modal dialog otherwise
    /// needs a real mouse.
    /// </summary>
    public bool OpenSettings { get; private set; }

    /// <summary>
    /// Paints the clock and FPS readout in the bottom left corner (<c>--system-info</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>fheroes2::SystemInfoRenderer</c>，由 <c>Settings::isSystemInfoEnabled()</c> 开关
    /// （<c>src/fheroes2/game/game.cpp</c> 的主循环）。端口还没有设置窗口去存这个开关，先用命令行。
    /// </remarks>
    public bool SystemInfo { get; private set; }

    /// <summary>
    /// Opens the scenario selection window of the main menu at start up (<c>--open-scenario</c>).
    /// The New Game sign does the same; the flag exists because a screenshot of the dialog
    /// otherwise needs a real mouse click.
    /// </summary>
    public bool OpenScenario { get; private set; }

    /// <summary>
    /// Index of the map to select when the scenario window opens (<c>--scenario-select</c>), zero
    /// based and counted in the unfiltered list. It opens the window as well, so a screenshot can
    /// show any row of the list without a mouse.
    /// </summary>
    public int? ScenarioSelection { get; private set; }

    /// <summary>
    /// Confirms the scenario selection at start up and opens its adventure map (<c>--enter-map</c>).
    /// The scenario window is opened first when it is not already, and the first map is used unless
    /// <c>--scenario-select</c> picks another one. It is how M4 is screenshotted and smoke tested
    /// without a mouse.
    /// </summary>
    public bool EnterMap { get; private set; }

    /// <summary>
    /// Opens this map file directly on the adventure map (<c>--map &lt;file.mp2&gt;</c>), skipping the
    /// menu and the scenario list. It is what makes the adventure map baseline independent of the
    /// installed map collection: the frame only depends on the file that is passed in.
    /// </summary>
    public string? MapFile { get; private set; }

    /// <summary>
    /// Ends the human turn this many times right after the map opens (<c>--skip-turns &lt;n&gt;</c>).
    /// </summary>
    /// <remarks>
    /// 电脑的回合本来要玩家按沙漏才走；冒烟跑靠这一手把 AI 回合也覆盖到（AI 走子、拾取、进城都在
    /// <c>AI::Planner::KingdomTurn()</c> 里，见 <c>docs/PROGRESS.md</c> §6.1k）。跳过之后由场景的帧循环
    /// 把电脑的回合跑完，因此要与 <c>--smoke-frames</c> 一起用才有意义。
    /// </remarks>
    public int? SkipTurns { get; private set; }

    /// <summary>
    /// Opens the View World window as soon as the adventure map is up (<c>--view-world</c>). The window
    /// normally opens from the World button of the adventure options dialog, so this is how its frame is
    /// screenshotted and smoke tested without a mouse.
    /// </summary>
    public bool ViewWorld { get; private set; }

    /// <summary>
    /// Opens the adventure options dialog as soon as the adventure map is up
    /// (<c>--adventure-options</c>). The dialog normally opens with the V key or the door in the bottom
    /// border; this is how its frame is screenshotted.
    /// </summary>
    public bool AdventureOptions { get; private set; }

    /// <summary>
    /// Holds the right button on the tile under <c>--cursor</c> as soon as the adventure map is up
    /// (<c>--quick-info</c>). The popup only exists while the button is held, so this is how its frame is
    /// screenshotted and how the tile to text path is smoke tested without a mouse.
    /// </summary>
    public bool QuickInfo { get; private set; }

    /// <summary>
    /// Opens the town screen of the first town of the acting kingdom as soon as the adventure map is up
    /// (<c>--enter-town</c>). The screen normally opens by clicking one of the player's towns on the map,
    /// so this is how its frame is screenshotted and smoke tested without a mouse.
    /// </summary>
    public bool EnterTown { get; private set; }

    /// <summary>
    /// Opens one of the town screen's windows right after <c>--enter-town</c> (<c>--town-window
    /// mageguild</c>, <c>well</c>, <c>market</c>). The windows normally open by clicking a building, so
    /// this is how their frames are screenshotted and smoke tested without a mouse.
    /// </summary>
    public string? TownWindow { get; private set; }

    /// <summary>
    /// Which town of the acting kingdom <c>--enter-town</c> opens (<c>--town-select</c>), zero based in the
    /// order of <c>Kingdom.Castles</c>. Towns of a fresh map have random buildings, so picking the town is
    /// how a smoke run reaches one that has the building under test.
    /// </summary>
    public int TownIndex { get; private set; }

    /// <summary>
    /// The name of one <c>BuildingType</c> to add to the entered town (<c>--town-build</c>). A fresh map
    /// starts every town with nothing but its fort, so windows of built buildings need this to be reached.
    /// </summary>
    public string? TownBuild { get; private set; }

    /// <summary>
    /// The name of one <c>BuildingType</c> whose window is opened right after the town screen appears
    /// (<c>--town-building DwellingMonster1</c>). It goes through the same <c>OpenBuilding</c> a click uses,
    /// which is how the dwelling recruitment is reached without a mouse.
    /// </summary>
    public string? TownBuilding { get; private set; }

    /// <summary>
    /// Which of the two heroes on offer the recruit hero dialog shows (<c>--town-hero 0</c> or <c>1</c>).
    /// The dialog normally needs two clicks — the slot of the building list, then OKAY — so this is how a
    /// script reaches it in one run, and how a second run with <c>--left-click</c> hires the hero.
    /// </summary>
    public int? TownHero { get; private set; }

    /// <summary>
    /// Paints a rectangle of ground as soon as the editor opens (<c>--editor-paint
    /// &lt;x,y,width,height,ground&gt;</c>). It exists because a brush stroke otherwise needs a real
    /// mouse: a screenshot cannot show that changing a tile changes the frame, and the adventure map
    /// view reads the tiles on every frame, so this is also the cheapest regression for that path.
    /// </summary>
    public EditorPaintRequest? EditorPaint { get; private set; }

    /// <summary>
    /// Saves the map to this file as soon as the editor opens (<c>--editor-save &lt;file.mp2&gt;</c>,
    /// usually together with <c>--editor-paint</c>). Together they exercise the whole loop - open,
    /// change a tile, write it back - without a mouse, which is how the save path is checked on the
    /// real map collection.
    /// </summary>
    public string? EditorSavePath { get; private set; }

    /// <summary>
    /// Starts the editor on an empty map of this many tiles instead of a file
    /// (<c>--editor-new &lt;36|72|108|144&gt;</c>). Such a map has no file behind it, so saving it needs
    /// <c>--editor-save</c> and takes the whole-file write path rather than the tile patch.
    /// </summary>
    public int? EditorNewMapSize { get; private set; }

    /// <summary>
    /// Object the object tool places, by name or one based number (<c>--editor-object Tree</c>). It is
    /// the scripted counterpart of the <c>O</c> key, for screenshots and for the save round trip.
    /// </summary>
    public string? EditorObject { get; private set; }

    /// <summary>
    /// Tiles that receive <see cref="EditorObject"/> when the editor opens
    /// (<c>--editor-place &lt;x,y;x,y&gt;</c>). Placement is otherwise a mouse click, which a
    /// screenshot cannot produce.
    /// </summary>
    public IReadOnlyList<Point> EditorPlacements { get; private set; } = [];

    /// <summary>
    /// Gesture on the minimap panel the editor performs when it opens (<c>--editor-radar-click
    /// &lt;x,y[,x2,y2]&gt;</c>, panel coordinates from 0 to 143): the press lands on the first point,
    /// and the second one is the drag target when it is given. It is the scripted version of the radar
    /// of <c>Radar::QueueEventProcessing</c>, where the pointer centres the map while the button is
    /// held, so a click and a drag are the same branch.
    /// </summary>
    public EditorRadarRequest? EditorRadarClick { get; private set; }

    /// <summary>
    /// Points of the editor panel a click lands on when the editor opens (<c>--editor-panel-click
    /// &lt;x,y[;x,y...]&gt;</c>, panel coordinates from 0 to 143 by 0 to 319). Each click is the same one
    /// the left button produces, so the list can walk through controls in order — select an instrument
    /// and then its picker, which a single click cannot do.
    /// </summary>
    public IReadOnlyList<Point> EditorPanelClicks { get; private set; } = [];

    /// <summary>
    /// Tile whose information box the editor opens at start up (<c>--editor-info &lt;x,y&gt;</c>), the
    /// box the <c>I</c> key shows: the object on the tile, the parts it is drawn from, its data word
    /// and its coordinates.
    /// </summary>
    public Point? EditorInfoTile { get; private set; }

    /// <summary>
    /// Data word an editor tile receives at start up (<c>--editor-data &lt;x,y,value&gt;</c>), the
    /// scripted version of the <c>[</c> and <c>]</c> keys that step the same word while the
    /// information box is open.
    /// </summary>
    public EditorDataRequest? EditorData { get; private set; }

    /// <summary>
    /// Writes a PNG of the composed logical frame and then stops: the flag is a script, so it hands the
    /// prompt back to the shell instead of leaving a window to close by hand.
    /// </summary>
    public string? ScreenshotPath { get; private set; }

    /// <summary>Screenshot is taken after this many rendered frames (<c>--screenshot-frame</c>).</summary>
    public int ScreenshotFrame { get; private set; } = 30;

    /// <summary>Prints the archive contents and exits without opening a window.</summary>
    public bool ListAssets { get; private set; }

    /// <summary>
    /// Keeps the client silent (<c>--no-audio</c>): no music and no sound effects, not even an
    /// attempt to open the audio device. Useful on machines without (or with a broken) audio setup,
    /// and for the pixel baselines, which must not depend on a soundtrack.
    /// </summary>
    public bool NoAudio { get; private set; }

    /// <summary>
    /// Name of a sound effect of the original archives (for example <c>PICKUP01</c>) that is played
    /// once after start up (<c>--play-sound</c>). It exists because M2 has no gameplay yet: this is
    /// how the sound path is verified by ear without inventing a trigger.
    /// </summary>
    public string? PlaySound { get; private set; }

    public bool ShowHelp { get; private set; }

    public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        CommandLineOptions options = new();

        for (int i = 0; i < arguments.Count; ++i)
        {
            string argument = arguments[i];
            switch (argument)
            {
                case "--help" or "-h" or "/?":
                    options.ShowHelp = true;
                    break;

                case "--data":
                    options.DataDirectory = RequireValue(arguments, ref i, argument);
                    break;

                case "--config":
                    options.ConfigFile = RequireValue(arguments, ref i, argument);
                    break;

                case "--no-config":
                    options.NoConfig = true;
                    break;

                case "--scene":
                    string scene = RequireValue(arguments, ref i, argument);
                    options.Scene = scene switch
                    {
                        "menu" => SceneKind.Menu,
                        "diagnostics" => SceneKind.Diagnostics,
                        "editor" => SceneKind.Editor,
                        _ => throw new ArgumentException(
                            $"'{argument}' expects 'menu', 'diagnostics' or 'editor', got '{scene}'."),
                    };

                    break;

                case "--lang":
                    options.Language = RequireValue(arguments, ref i, argument);
                    break;

                case "--cursor":
                    options.Cursor = ParsePoint(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--right-button":
                    options.RightButton = true;
                    break;

                case "--left-click":
                    options.LeftClick = true;
                    break;

                case "--scale":
                    string rawScale = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawScale, out int scale) || scale < MinimumScale || scale > MaximumScale)
                    {
                        throw new ArgumentException(
                            $"'{argument}' expects an integer between {MinimumScale} and {MaximumScale}, got '{rawScale}'.");
                    }

                    options.WindowScale = scale;
                    break;

                case "--fullscreen":
                    options.Fullscreen = true;
                    break;

                case "--smoke-frames":
                    string rawFrames = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawFrames, out int frames) || frames <= 0)
                    {
                        throw new ArgumentException($"'{argument}' expects a positive frame count, got '{rawFrames}'.");
                    }

                    options.SmokeFrames = frames;
                    break;

                case "--screenshot":
                    options.ScreenshotPath = RequireValue(arguments, ref i, argument);
                    break;

                case "--screenshot-frame":
                    string rawCapture = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawCapture, out int captureFrame) || captureFrame <= 0)
                    {
                        throw new ArgumentException(
                            $"'{argument}' expects a positive frame count, got '{rawCapture}'.");
                    }

                    options.ScreenshotFrame = captureFrame;
                    break;

                case "--freeze-animation":
                    options.FreezeAnimation = true;
                    break;

                case "--system-info":
                    options.SystemInfo = true;
                    break;

                case "--open-settings":
                    options.OpenSettings = true;
                    break;

                case "--open-scenario":
                    options.OpenScenario = true;
                    break;

                case "--scenario-select":
                    string rawSelection = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawSelection, out int selection) || selection < 0)
                    {
                        throw new ArgumentException(
                            $"'{argument}' expects a zero based map index, got '{rawSelection}'.");
                    }

                    options.ScenarioSelection = selection;
                    break;

                case "--enter-map":
                    options.EnterMap = true;
                    break;

                case "--map":
                    options.MapFile = RequireValue(arguments, ref i, argument);
                    break;

                case "--skip-turns":
                    string rawTurns = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawTurns, out int turns) || turns <= 0)
                    {
                        throw new ArgumentException($"'{argument}' expects a positive turn count, got '{rawTurns}'.");
                    }

                    options.SkipTurns = turns;
                    break;

                case "--view-world":
                    options.ViewWorld = true;
                    break;

                case "--adventure-options":
                    options.AdventureOptions = true;
                    break;

                case "--quick-info":
                    options.QuickInfo = true;
                    break;

                case "--enter-town":
                    options.EnterTown = true;
                    break;

                case "--town-window":
                    options.TownWindow = RequireValue(arguments, ref i, argument);
                    break;

                case "--town-build":
                    options.TownBuild = RequireValue(arguments, ref i, argument);
                    break;

                case "--town-building":
                    options.TownBuilding = RequireValue(arguments, ref i, argument);
                    break;

                case "--town-hero":
                    string rawHero = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawHero, out int heroIndex) || heroIndex is < 0 or > 1)
                    {
                        throw new ArgumentException($"'{argument}' expects 0 or 1, got '{rawHero}'.");
                    }

                    options.TownHero = heroIndex;
                    break;

                case "--town-select":
                    string rawTown = RequireValue(arguments, ref i, argument);
                    if (!int.TryParse(rawTown, out int townIndex) || townIndex < 0)
                    {
                        throw new ArgumentException(
                            $"'{argument}' expects a zero based town index, got '{rawTown}'.");
                    }

                    options.TownIndex = townIndex;
                    break;

                case "--editor-paint":
                    options.EditorPaint = ParseEditorPaint(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--editor-save":
                    options.EditorSavePath = RequireValue(arguments, ref i, argument);
                    break;

                case "--editor-new":
                    options.EditorNewMapSize = ParseMapSize(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--editor-object":
                    options.EditorObject = RequireValue(arguments, ref i, argument);
                    break;

                case "--editor-place":
                    options.EditorPlacements = ParsePointList(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--editor-radar-click":
                    options.EditorRadarClick = ParseRadarClick(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--editor-panel-click":
                    options.EditorPanelClicks = ParsePointList(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--editor-info":
                    options.EditorInfoTile = ParsePoint(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--editor-data":
                    options.EditorData = ParseEditorData(RequireValue(arguments, ref i, argument), argument);
                    break;

                case "--list-assets":
                    options.ListAssets = true;
                    break;

                case "--no-audio":
                    options.NoAudio = true;
                    break;

                case "--play-sound":
                    options.PlaySound = RequireValue(arguments, ref i, argument);
                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{argument}'.");
            }
        }

        return options;
    }

    public static void PrintUsage()
    {
        Console.WriteLine($"{BuildInfo.DisplayName} — desktop client");
        Console.WriteLine($"Reference implementation: {BuildInfo.ReferenceImplementation}");
        Console.WriteLine("Platform: Windows x64 only (docs/DECISIONS.md ADR-025).");
        Console.WriteLine();
        Console.WriteLine("Usage: FHeroes2Sharp [options]");
        Console.WriteLine();
        Console.WriteLine("  --data <dir>        Folder that contains 'Data/Heroes2.agg'.");
        Console.WriteLine("                      Defaults to the FHEROES2_DATA environment variable,");
        Console.WriteLine("                      the executable folder or a known local install.");
        Console.WriteLine("  --config <file>     Configuration file to read and write; the default is");
        Console.WriteLine("                      %APPDATA%\\fheroes2\\fheroes2.cfg (created on the first run).");
        Console.WriteLine("  --no-config         Leaves the configuration file alone (nothing read, nothing written);");
        Console.WriteLine("                      used by the pixel baselines so a developer's own settings cannot leak in.");
        Console.WriteLine("  --scene <name>      'menu' (default), 'diagnostics' (the M0 engine screen) or");
        Console.WriteLine("                      'editor' (the map editor; pass --map to choose the map).");
        Console.WriteLine("  --lang <culture>    Interface language, e.g. 'zh_CN' or 'en'. A catalog must");
        Console.WriteLine("                      exist in <data>/files/lang; see BuildTranslations.ps1.");
        Console.WriteLine("  --cursor <x,y>      Puts the cursor at a logical pixel, e.g. to screenshot a hover.");
        Console.WriteLine("                      The position is synthetic: real mouse motion is ignored, so the");
        Console.WriteLine("                      hover state survives the physical pointer leaving the window.");
        Console.WriteLine("  --left-click        Clicks the left button once at --cursor, e.g. to open the editor from the menu.");
    Console.WriteLine("  --right-button      Holds the right mouse button at --cursor, which shows the tooltip");
        Console.WriteLine("                      of the sign under it (the original shows it only while pressed).");
        Console.WriteLine($"  --scale <1..{MaximumScale}>       Window scale of the 640x480 logical resolution (default 2).");
        Console.WriteLine("  --fullscreen        Starts in borderless fullscreen mode.");
        Console.WriteLine("  --smoke-frames <n>  Renders n frames and exits (used by CI / smoke tests).");
        Console.WriteLine("  --screenshot <png>  Saves the composed 640x480 frame after 30 rendered frames, then exits.");
        Console.WriteLine("  --screenshot-frame <n>  Frame that --screenshot captures (default 30).");
        Console.WriteLine("  --freeze-animation  Keeps the lantern on its first frame (reproducible screenshots).");
        Console.WriteLine("  --system-info       Paints the clock and FPS in the bottom left corner (the reference's F1 overlay).");
        Console.WriteLine("  --open-settings     Opens the settings dialog of the main menu at start up.");
        Console.WriteLine("  --open-scenario     Opens the scenario selection of the main menu (New Game).");
        Console.WriteLine("  --scenario-select <n>  Opens the scenario selection and selects map n (0 based).");
        Console.WriteLine("  --enter-map         Starts the selected scenario and opens its adventure map.");
        Console.WriteLine("  --map <file>        Opens this map file on the adventure map, skipping the menu.");
        Console.WriteLine("  --skip-turns <n>    Ends the human turn n times at start up, so the computer plays.");
        Console.WriteLine("  --view-world        Opens the View World window as soon as the map is up.");
        Console.WriteLine("  --enter-town        Opens the town screen of the first town as soon as the map is up.");
        Console.WriteLine("  --town-window <name>  Opens one of its windows, e.g. 'mageguild' or 'market', after --enter-town.");
        Console.WriteLine("  --town-select <n>   Opens the n-th town of the kingdom (0 based) instead of the first.");
        Console.WriteLine("  --town-build <name>  Adds one BuildingType to the entered town, e.g. 'MageGuild1'.");
        Console.WriteLine("  --town-building <name>  Opens that building's window, e.g. 'Well' or 'DwellingMonster1'.");
        Console.WriteLine("  --town-hero <0|1>   Opens the recruit hero dialog of one of the two heroes on offer.");
        Console.WriteLine("  --editor-paint <x,y,w,h,ground>  Paints that rectangle of tiles when the editor");
        Console.WriteLine("                      opens, e.g. '50,50,6,4,Snow' (ground name or 1..9).");
        Console.WriteLine("  --editor-save <file>  Saves the map to that file when the editor opens; combine with");
        Console.WriteLine("                      --editor-paint to write the whole open-edit-save loop from a script.");
        Console.WriteLine("  --editor-new <size>  Starts the editor on an empty grass map of 36, 72, 108 or 144");
        Console.WriteLine("                      tiles instead of a file; use --editor-save to give it a file.");
        Console.WriteLine("  --editor-object <name|n>  Selects the object the left button places, e.g. 'Tree' or");
        Console.WriteLine("                      '1'; the numbered list is what the O key cycles in the editor.");
        Console.WriteLine("  --editor-place <x,y[;x,y...]>  Places the selected object on those tiles when the");
        Console.WriteLine("                      editor opens, the scripted version of a click.");
        Console.WriteLine("  --editor-radar-click <x,y[,x2,y2]>  Presses that point of the 144x144 minimap when the");
        Console.WriteLine("                      editor opens, e.g. '20,20'; with a second point the press is dragged");
        Console.WriteLine("                      there first. The map centres on the tile under the pointer.");
        Console.WriteLine("  --editor-panel-click <x,y[;x,y...]>  Clicks those points of the 144x320 editor panel");
        Console.WriteLine("                      in order when the editor opens, e.g. '0,0' for the terrain button.");
        Console.WriteLine("  --editor-info <x,y>  Opens the information box of that tile, the box the I key shows;");
        Console.WriteLine("                      it lists the object, its parts, its data word and the coordinates.");
        Console.WriteLine("  --editor-data <x,y,value>  Writes that data word (0..8191) into the object on the tile");
        Console.WriteLine("                      when the editor opens, the scripted version of the [ and ] keys.");
        Console.WriteLine("  --no-audio          Keeps the client silent (no music device is opened).");
        Console.WriteLine("  --play-sound <name> Plays one sound effect of the archives, e.g. 'PICKUP01'.");
        Console.WriteLine("  --list-assets       Prints the archive inventory and exits.");
        Console.WriteLine("  --help              Shows this help.");
    }

    private static string RequireValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Count)
        {
            throw new ArgumentException($"'{option}' requires a value.");
        }

        ++index;
        return arguments[index];
    }

    private static Point ParsePoint(string value, string option)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out int x)
            && int.TryParse(parts[1], out int y))
        {
            return new Point(x, y);
        }

        throw new ArgumentException($"'{option}' expects '<x>,<y>', got '{value}'.");
    }

    private static EditorPaintRequest ParseEditorPaint(string value, string option)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 5
            && int.TryParse(parts[0], out int x)
            && int.TryParse(parts[1], out int y)
            && int.TryParse(parts[2], out int width)
            && int.TryParse(parts[3], out int height)
            && width > 0
            && height > 0
            && TryParseGround(parts[4], out MapGround ground))
        {
            return new EditorPaintRequest(new Point(x, y), width, height, ground);
        }

        throw new ArgumentException(
            $"'{option}' expects '<x>,<y>,<width>,<height>,<ground>', got '{value}'. " +
            "The ground is a name such as 'Snow' or a number from 1 (water) to 9 (beach).");
    }

    /// <summary>
    /// Parses <c>--editor-radar-click</c>: <c>x,y</c> for a click, <c>x1,y1,x2,y2</c> for a press that
    /// is dragged to a second point before the release.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <param name="option">Name of the option, for the error message.</param>
    /// <returns>The gesture on the minimap panel.</returns>
    /// <exception cref="ArgumentException">The value is not two or four integers.</exception>
    private static EditorRadarRequest ParseRadarClick(string value, string option)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is 2 or 4 && parts.All(part => int.TryParse(part, out _)))
        {
            int[] numbers = parts.Select(int.Parse).ToArray();
            Point start = new(numbers[0], numbers[1]);

            return parts.Length == 2
                ? new EditorRadarRequest(start, null)
                : new EditorRadarRequest(start, new Point(numbers[2], numbers[3]));
        }

        throw new ArgumentException(
            $"'{option}' expects '<x>,<y>' or '<x1>,<y1>,<x2>,<y2>' in panel pixels, got '{value}'.");
    }

    /// <summary>
    /// Parses <c>--editor-data</c>: <c>x,y,value</c>, the data word the object on the tile receives.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <param name="option">Name of the option, for the error message.</param>
    /// <returns>The tile and the value to write into it.</returns>
    /// <exception cref="ArgumentException">The value is not two coordinates and a number in range.</exception>
    private static EditorDataRequest ParseEditorData(string value, string option)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 3
            && int.TryParse(parts[0], out int x)
            && int.TryParse(parts[1], out int y)
            && int.TryParse(parts[2], out int data)
            && data is >= 0 and <= MapEditSession.MaxObjectData)
        {
            return new EditorDataRequest(new Point(x, y), data);
        }

        throw new ArgumentException(
            $"'{option}' expects '<x>,<y>,<value>' with a value from 0 to {MapEditSession.MaxObjectData}, " +
            $"got '{value}'.");
    }

    /// <summary>Parses a list of points: one or more <c>x,y</c> pairs, separated by semicolons.</summary>
    /// <param name="value">The option value.</param>
    /// <param name="option">Name of the option, for the error message.</param>
    /// <returns>The points in the order they were given.</returns>
    /// <exception cref="ArgumentException">No pair or a malformed pair.</exception>
    private static IReadOnlyList<Point> ParsePointList(string value, string option)
    {
        string[] parts = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            throw new ArgumentException($"'{option}' expects at least one '<x>,<y>' pair, got '{value}'.");
        }

        List<Point> points = new(parts.Length);

        foreach (string part in parts)
        {
            points.Add(ParsePoint(part, option));
        }

        return points;
    }

    private static int ParseMapSize(string value, string option)
    {
        if (int.TryParse(value, out int size) && Mp2Map.IsSupportedSize(size, size))
        {
            return size;
        }

        throw new ArgumentException(
            $"'{option}' expects a map size of 36, 72, 108 or 144 tiles, got '{value}'.");
    }

    /// <summary>Reads a ground by its enum name or by the number the editor's keys use.</summary>
    private static bool TryParseGround(string value, out MapGround ground)
    {
        if (int.TryParse(value, out int number))
        {
            ground = number is >= 1 and <= 9 ? (MapGround)number : MapGround.Unknown;
            return ground != MapGround.Unknown;
        }

        return Enum.TryParse(value, ignoreCase: true, out ground) && ground != MapGround.Unknown;
    }
}

/// <summary>
/// The rectangle of tiles <c>--editor-paint</c> paints when the editor opens, plus the ground.
/// A record rather than three loose options, so the host cannot receive a half parsed request.
/// </summary>
/// <param name="Tile">Top left tile of the rectangle.</param>
/// <param name="Width">Width of the rectangle in tiles.</param>
/// <param name="Height">Height of the rectangle in tiles.</param>
/// <param name="Ground">Ground the brush paints.</param>
public readonly record struct EditorPaintRequest(Point Tile, int Width, int Height, MapGround Ground);

/// <summary>
/// The minimap gesture of <c>--editor-radar-click</c>: where the press lands and, for a drag, where
/// the pointer is before the release. Panel pixels, not screen pixels, so the request does not
/// depend on where the frame puts the panel.
/// </summary>
/// <param name="Start">Point of the panel the press lands on.</param>
/// <param name="DragTo">Point the pointer moves to while the button is held, or <see langword="null"/>.</param>
public readonly record struct EditorRadarRequest(Point Start, Point? DragTo);

/// <summary>
/// The data word of <c>--editor-data</c>: which tile gets which value when the editor opens, so a map
/// can be scripted into a state where the information box has something to show.
/// </summary>
/// <param name="Tile">Tile that receives the value, must hold an object.</param>
/// <param name="Value">Data word to write, 0 to <c>MapEditSession.MaxObjectData</c>.</param>
public readonly record struct EditorDataRequest(Point Tile, int Value);
