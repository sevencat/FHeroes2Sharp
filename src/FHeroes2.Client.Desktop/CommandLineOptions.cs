using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Client.Desktop;

/// <summary>
/// Command line of the desktop client.
///
/// <code>
///   FHeroes2Sharp [--data &lt;dir&gt;] [--scale &lt;1..4&gt;] [--fullscreen] [--smoke-frames &lt;n&gt;] [--list-assets]
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

    /// <summary>Window scale factor applied to the 640x480 logical resolution.</summary>
    public int WindowScale { get; private set; } = 2;

    public bool Fullscreen { get; private set; }

    /// <summary>When set the client exits after rendering the given number of frames.</summary>
    public int? SmokeFrames { get; private set; }

    /// <summary>Writes a PNG of the composed logical frame, then keeps running unless smoke mode is set.</summary>
    public string? ScreenshotPath { get; private set; }

    /// <summary>Screenshot is taken after this many rendered frames.</summary>
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
        Console.WriteLine();
        Console.WriteLine("Usage: FHeroes2Sharp [options]");
        Console.WriteLine();
        Console.WriteLine("  --data <dir>        Folder that contains 'Data/Heroes2.agg'.");
        Console.WriteLine("                      Defaults to the FHEROES2_DATA environment variable,");
        Console.WriteLine("                      the executable folder or a known local install.");
        Console.WriteLine($"  --scale <1..{MaximumScale}>       Window scale of the 640x480 logical resolution (default 2).");
        Console.WriteLine("  --fullscreen        Starts in borderless fullscreen mode.");
        Console.WriteLine("  --smoke-frames <n>  Renders n frames and exits (used by CI / smoke tests).");
        Console.WriteLine("  --screenshot <png>  Saves the composed 640x480 frame after 30 rendered frames.");
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
}
