using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.App;

/// <summary>
/// Command line of the desktop client.
///
/// <code>
///   FHeroes2Sharp [--data &lt;dir&gt;] [--scene menu|diagnostics|editor] [--lang &lt;culture&gt;]
///                 [--scale &lt;1..4&gt;] [--fullscreen] [--smoke-frames &lt;n&gt;] [--list-assets]
///                 [--cursor &lt;x,y&gt;] [--right-button] [--left-click] [--freeze-animation]
///                 [--open-scenario] [--scenario-select &lt;n&gt;] [--enter-map] [--map &lt;file&gt;]
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
    /// tooltips of the main menu only exist while the button is pressed, so this is what makes a
    /// screenshot of one possible; it has no effect without <c>--cursor</c>.
    /// </summary>
    public bool RightButton { get; private set; }

    /// <summary>
    /// Clicks the left mouse button once at the position of <c>--cursor</c> right after the main menu
    /// is built: press and release in the same frame. It is how a screenshot can walk through a
    /// click-only path (the editor entrance) and how the log shows whether the hit test matched;
    /// it has no effect without <c>--cursor</c>.
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
    /// Paints a rectangle of ground as soon as the editor opens (<c>--editor-paint
    /// &lt;x,y,width,height,ground&gt;</c>). It exists because a brush stroke otherwise needs a real
    /// mouse: a screenshot cannot show that changing a tile changes the frame, and the adventure map
    /// view reads the tiles on every frame, so this is also the cheapest regression for that path.
    /// </summary>
    public EditorPaintRequest? EditorPaint { get; private set; }

    /// <summary>Writes a PNG of the composed logical frame, then keeps running unless smoke mode is set.</summary>
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

                case "--editor-paint":
                    options.EditorPaint = ParseEditorPaint(RequireValue(arguments, ref i, argument), argument);
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
        Console.WriteLine("  --screenshot <png>  Saves the composed 640x480 frame after 30 rendered frames.");
        Console.WriteLine("  --screenshot-frame <n>  Frame that --screenshot captures (default 30).");
        Console.WriteLine("  --freeze-animation  Keeps the lantern on its first frame (reproducible screenshots).");
        Console.WriteLine("  --open-settings     Opens the settings dialog of the main menu at start up.");
        Console.WriteLine("  --open-scenario     Opens the scenario selection of the main menu (New Game).");
        Console.WriteLine("  --scenario-select <n>  Opens the scenario selection and selects map n (0 based).");
        Console.WriteLine("  --enter-map         Starts the selected scenario and opens its adventure map.");
        Console.WriteLine("  --map <file>        Opens this map file on the adventure map, skipping the menu.");
        Console.WriteLine("  --editor-paint <x,y,w,h,ground>  Paints that rectangle of tiles when the editor");
        Console.WriteLine("                      opens, e.g. '50,50,6,4,Snow' (ground name or 1..9).");
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
