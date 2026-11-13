using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;

namespace FHeroes2.App;

/// <summary>
/// Command line of the desktop client.
///
/// <code>
///   FHeroes2Sharp [--data &lt;dir&gt;] [--scene menu|diagnostics] [--lang &lt;culture&gt;]
///                 [--scale &lt;1..4&gt;] [--fullscreen] [--smoke-frames &lt;n&gt;] [--list-assets]
///                 [--cursor &lt;x,y&gt;] [--right-button] [--freeze-animation]
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

    /// <summary>Writes a PNG of the composed logical frame, then keeps running unless smoke mode is set.</summary>
    public string? ScreenshotPath { get; private set; }

    /// <summary>Screenshot is taken after this many rendered frames (<c>--screenshot-frame</c>).</summary>
    public int ScreenshotFrame { get; private set; } = 30;

    /// <summary>Prints the archive contents and exits without opening a window.</summary>
    public bool ListAssets { get; private set; }

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
                        _ => throw new ArgumentException(
                            $"'{argument}' expects 'menu' or 'diagnostics', got '{scene}'."),
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

                case "--list-assets":
                    options.ListAssets = true;
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
        Console.WriteLine("  --scene <name>      'menu' (default) or 'diagnostics' (the M0 engine screen).");
        Console.WriteLine("  --lang <culture>    Interface language, e.g. 'zh_CN' or 'en'. A catalog must");
        Console.WriteLine("                      exist in <data>/files/lang; see BuildTranslations.ps1.");
        Console.WriteLine("  --cursor <x,y>      Puts the cursor at a logical pixel, e.g. to screenshot a hover.");
        Console.WriteLine("                      The position is synthetic: real mouse motion is ignored, so the");
        Console.WriteLine("                      hover state survives the physical pointer leaving the window.");
        Console.WriteLine("  --right-button      Holds the right mouse button at --cursor, which shows the tooltip");
        Console.WriteLine("                      of the sign under it (the original shows it only while pressed).");
        Console.WriteLine($"  --scale <1..{MaximumScale}>       Window scale of the 640x480 logical resolution (default 2).");
        Console.WriteLine("  --fullscreen        Starts in borderless fullscreen mode.");
        Console.WriteLine("  --smoke-frames <n>  Renders n frames and exits (used by CI / smoke tests).");
        Console.WriteLine("  --screenshot <png>  Saves the composed 640x480 frame after 30 rendered frames.");
        Console.WriteLine("  --screenshot-frame <n>  Frame that --screenshot captures (default 30).");
        Console.WriteLine("  --freeze-animation  Keeps the lantern on its first frame (reproducible screenshots).");
        Console.WriteLine("  --open-settings     Opens the settings dialog of the main menu at start up.");
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
}
