using FHeroes2.Core.Geometry;

namespace FHeroes2.AssetProbe;

/// <summary>Parsed command line of the asset probe.</summary>
internal sealed class ProbeArguments
{
    private ProbeArguments(string command)
    {
        Command = command;
    }

    public string Command { get; }

    public List<string> Positional { get; } = [];

    public string? DataDirectory { get; private set; }

    public string? ArchivePath { get; private set; }

    public int? Limit { get; private set; }

    public string PaletteName { get; private set; } = "KB.PAL";

    /// <summary>File format of the exported images: <c>png</c> (default) or <c>bmp</c>.</summary>
    public string Format { get; private set; } = "png";

    /// <summary>
    /// Palette index used as the background of the exported sprites when the BMP format is chosen.
    /// The reference engine recommends 23; the default leaves the raw image layer visible.
    /// </summary>
    public byte? Background { get; private set; }

    public string? DiffDirectory { get; private set; }

    /// <summary>
    /// Object type a <c>map</c> run lists the positions of, e.g. <c>Sphinx</c>. Matched against
    /// <see cref="object.ToString"/> case-insensitively; <see langword="null"/> lists the counts only.
    /// </summary>
    public string? ObjectType { get; private set; }

    /// <summary>
    /// Areas that <c>compare</c> skips, in pixels of the compared image. Screenshots contain
    /// values that change from run to run (the FPS counter of the status bar), so a pixel baseline
    /// has to exclude them instead of pinning them.
    /// </summary>
    public List<Rect> IgnoreAreas { get; } = [];

    public static ProbeArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProbeArguments parsed = new(args[0].ToLowerInvariant());

        for (int i = 1; i < args.Length; ++i)
        {
            switch (args[i])
            {
                case "--data":
                    parsed.DataDirectory = NextValue(args, ref i, "--data");
                    break;

                case "--agg":
                    parsed.ArchivePath = NextValue(args, ref i, "--agg");
                    break;

                case "--limit":
                    string raw = NextValue(args, ref i, "--limit");
                    if (!int.TryParse(raw, out int limit) || limit <= 0)
                    {
                        throw new ArgumentException($"'--limit' expects a positive integer, got '{raw}'.");
                    }

                    parsed.Limit = limit;
                    break;

                case "--palette":
                    parsed.PaletteName = NextValue(args, ref i, "--palette");
                    break;

                case "--format":
                    string format = NextValue(args, ref i, "--format").ToLowerInvariant();
                    if (format is not ("png" or "bmp"))
                    {
                        throw new ArgumentException($"'--format' expects 'png' or 'bmp', got '{format}'.");
                    }

                    parsed.Format = format;
                    break;

                case "--background":
                    string rawBackground = NextValue(args, ref i, "--background");
                    if (!byte.TryParse(rawBackground, out byte background))
                    {
                        throw new ArgumentException(
                            $"'--background' expects a palette index between 0 and 255, got '{rawBackground}'.");
                    }

                    parsed.Background = background;
                    break;

                case "--diff":
                    parsed.DiffDirectory = NextValue(args, ref i, "--diff");
                    break;

                case "--type":
                    parsed.ObjectType = NextValue(args, ref i, "--type");
                    break;

                case "--ignore":
                    parsed.IgnoreAreas.Add(ParseRect(NextValue(args, ref i, "--ignore")));
                    break;

                default:
                    parsed.Positional.Add(args[i]);
                    break;
            }
        }

        return parsed;
    }

    private static string NextValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"'{option}' requires a value.");
        }

        return args[++index];
    }

    /// <summary>Parses <c>x,y,width,height</c>; the format used by <c>--ignore</c>.</summary>
    private static Rect ParseRect(string value)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], out int x) ||
            !int.TryParse(parts[1], out int y) ||
            !int.TryParse(parts[2], out int width) ||
            !int.TryParse(parts[3], out int height))
        {
            throw new ArgumentException(
                $"'--ignore' expects 'x,y,width,height', got '{value}'.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException(
                $"'--ignore' needs a positive width and height, got '{value}'.");
        }

        return new Rect(x, y, width, height);
    }
}
