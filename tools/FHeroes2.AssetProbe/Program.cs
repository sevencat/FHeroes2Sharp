using FHeroes2.Assets;
using FHeroes2.Assets.Agg;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.AssetProbe;

/// <summary>
/// Command line inspector for the original game archives. It is intentionally dependency free
/// (no MonoGame, no window) so it can run in CI and diff the parser behaviour.
///
/// <code>
///   FHeroes2.AssetProbe list    [--data &lt;dir&gt;] [--agg &lt;path&gt;] [--limit &lt;n&gt;]
///   FHeroes2.AssetProbe info    [--data &lt;dir&gt;] [--agg &lt;path&gt;]
///   FHeroes2.AssetProbe extract &lt;NAME&gt; &lt;output&gt; [--data &lt;dir&gt;] [--agg &lt;path&gt;]
///   FHeroes2.AssetProbe sprites &lt;NAME.ICN&gt; &lt;output-dir&gt; [--palette &lt;NAME&gt;]
///   FHeroes2.AssetProbe tiles   &lt;NAME.TIL&gt; &lt;output-dir&gt; [--palette &lt;NAME&gt;]
///   FHeroes2.AssetProbe compare &lt;left&gt; &lt;right&gt; [--diff &lt;dir&gt;]
/// </code>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        if (args[0] is "--help" or "-h" or "/?")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            ProbeArguments arguments = ProbeArguments.Parse(args);
            return arguments.Command switch
            {
                "list" => List(arguments),
                "info" => Info(arguments),
                "extract" => Extract(arguments),
                "sprites" => ExportSprites(arguments),
                "tiles" => ExportTiles(arguments),
                "compare" => Compare(arguments),
                _ => Unknown(arguments.Command),
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or BinaryFormatException
                or DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static int List(ProbeArguments arguments)
    {
        using AssetArchive archive = OpenArchive(arguments);
        Console.WriteLine($"# {archive.Path}: {archive.Count} entries");

        IEnumerable<AssetEntry> entries = arguments.Limit is int limit
            ? archive.Entries.Take(limit)
            : archive.Entries;

        foreach (AssetEntry entry in entries)
        {
            Console.WriteLine($"{entry.Name}\t{entry.Offset}\t{entry.Size}");
        }

        return 0;
    }

    private static int Info(ProbeArguments arguments)
    {
        using AssetArchive archive = OpenArchive(arguments);

        ulong totalBytes = 0;
        Dictionary<string, int> byExtension = new(StringComparer.OrdinalIgnoreCase);
        foreach (AssetEntry entry in archive.Entries)
        {
            totalBytes += entry.Size;

            string extension = Path.GetExtension(entry.Name).TrimStart('.');
            byExtension[extension] = byExtension.GetValueOrDefault(extension) + 1;
        }

        Console.WriteLine($"Archive: {archive.Path}");
        Console.WriteLine($"Entries: {archive.Count}");
        Console.WriteLine($"Payload: {totalBytes} bytes");
        Console.WriteLine("Extensions:");
        foreach ((string extension, int count) in byExtension.OrderByDescending(pair => pair.Value))
        {
            Console.WriteLine($"  .{extension,-6} {count}");
        }

        return 0;
    }

    private static int Extract(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 2)
        {
            throw new ArgumentException("extract expects exactly two arguments: <NAME> <output>.");
        }

        string name = arguments.Positional[0];
        string output = arguments.Positional[1];

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        File.WriteAllBytes(output, data);
        Console.WriteLine($"Wrote {data.Length} bytes of '{name}' to '{Path.GetFullPath(output)}'.");
        return 0;
    }

    /// <summary>
    /// Decodes every sprite of an ICN entry and writes it as a PNG, one file per sprite. Sprites
    /// whose transform layer entry is "skip" become transparent, so the images can be compared
    /// with the reference engine by eye.
    /// </summary>
    private static int ExportSprites(ProbeArguments arguments)
    {
        (string name, string outputDirectory) = ParseExportArguments(arguments, "sprites", "<NAME.ICN>");

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        IcnSpriteFile icn = IcnSpriteFile.Parse(data);
        Palette palette = LoadPalette(archive, arguments.PaletteName);
        Directory.CreateDirectory(outputDirectory);

        string stem = Path.GetFileNameWithoutExtension(name);
        Console.WriteLine($"# {name}: {icn.Count} sprites, palette '{arguments.PaletteName}'");

        for (int index = 0; index < icn.Count; ++index)
        {
            IcnHeader header = icn.Headers[index];
            Sprite sprite = icn.Sprites[index];
            string file = Path.Combine(outputDirectory, $"{stem}_{index:D3}.png");

            File.WriteAllBytes(file, PngWriter.EncodeRgba(sprite.Image, palette, sprite.Transform));

            Console.WriteLine(string.Join(
                '\t',
                index.ToString(),
                $"{header.Width}x{header.Height}",
                $"{header.OffsetX},{header.OffsetY}",
                header.IsMonochromatic ? "mono" : "color",
                sprite.HasTransformLayer ? "transform" : "single",
                Path.GetFileName(file)));
        }

        Console.WriteLine($"Wrote {icn.Count} PNG files to '{Path.GetFullPath(outputDirectory)}'.");
        return 0;
    }

    /// <summary>Decodes every tile of a TIL entry and writes it as a PNG.</summary>
    private static int ExportTiles(ProbeArguments arguments)
    {
        (string name, string outputDirectory) = ParseExportArguments(arguments, "tiles", "<NAME.TIL>");

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        TilFile til = TilFile.Parse(data);
        Palette palette = LoadPalette(archive, arguments.PaletteName);
        Directory.CreateDirectory(outputDirectory);

        string stem = Path.GetFileNameWithoutExtension(name);
        Console.WriteLine($"# {name}: {til.Count} tiles of {til.Width}x{til.Height}, palette '{arguments.PaletteName}'");

        for (int index = 0; index < til.Count; ++index)
        {
            string file = Path.Combine(outputDirectory, $"{stem}_{index:D3}.png");

            File.WriteAllBytes(file, PngWriter.EncodeRgba(til.Images[index], palette));

            Console.WriteLine($"{index}\t{til.Width}x{til.Height}\t{Path.GetFileName(file)}");
        }

        Console.WriteLine($"Wrote {til.Count} PNG files to '{Path.GetFullPath(outputDirectory)}'.");
        return 0;
    }

    /// <summary>
    /// Compares two PNG files, or two directories of PNG files with matching names, pixel by pixel.
    /// Returns 0 when everything matches and 3 when a size or a pixel differs, so the tool can gate a
    /// script that checks our decoder against the reference engine output.
    /// </summary>
    private static int Compare(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 2)
        {
            throw new ArgumentException("compare expects two paths: <left> <right> [--diff <dir>].");
        }

        string left = arguments.Positional[0];
        string right = arguments.Positional[1];
        string? diffDirectory = arguments.DiffDirectory;

        if (diffDirectory is not null)
        {
            Directory.CreateDirectory(diffDirectory);
        }

        if (Directory.Exists(left) && Directory.Exists(right))
        {
            return CompareDirectories(left, right, diffDirectory);
        }

        if (!File.Exists(left))
        {
            throw new FileNotFoundException($"'{left}' does not exist.");
        }

        if (!File.Exists(right))
        {
            throw new FileNotFoundException($"'{right}' does not exist.");
        }

        return CompareFiles(left, right, diffDirectory) ? 0 : 3;
    }

    private static int CompareDirectories(string left, string right, string? diffDirectory)
    {
        Dictionary<string, string> counterparts = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(right, "*.png", SearchOption.TopDirectoryOnly))
        {
            counterparts[Path.GetFileName(file)] = file;
        }

        string[] files = Directory
            .EnumerateFiles(left, "*.png", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            throw new FileNotFoundException($"'{left}' contains no PNG files.");
        }

        int identical = 0;
        int different = 0;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (!counterparts.Remove(name, out string? counterpart))
            {
                Console.WriteLine($"{name}\tmissing\tnot found in '{right}'");
                ++different;
            }
            else if (CompareFiles(file, counterpart, diffDirectory))
            {
                ++identical;
            }
            else
            {
                ++different;
            }
        }

        foreach (string name in counterparts.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{name}\tmissing\tnot found in '{left}'");
            ++different;
        }

        Console.WriteLine($"# {identical} identical, {different} different");
        return different == 0 ? 0 : 3;
    }

    private static bool CompareFiles(string leftPath, string rightPath, string? diffDirectory)
    {
        PngImage left = ReadPng(leftPath);
        PngImage right = ReadPng(rightPath);
        string name = Path.GetFileName(leftPath);

        if (left.Width != right.Width || left.Height != right.Height)
        {
            Console.WriteLine($"{name}\tsize mismatch\t{left.Width}x{left.Height} vs {right.Width}x{right.Height}");
            return false;
        }

        int differing = 0;
        int maxDelta = 0;
        int firstX = -1;
        int firstY = -1;
        byte[] diff = new byte[left.Rgba.Length];

        for (int y = 0; y < left.Height; ++y)
        {
            for (int x = 0; x < left.Width; ++x)
            {
                RgbaColor expected = left[x, y];
                RgbaColor actual = right[x, y];
                int delta = Math.Max(
                    Math.Max(Math.Abs(expected.R - actual.R), Math.Abs(expected.G - actual.G)),
                    Math.Max(Math.Abs(expected.B - actual.B), Math.Abs(expected.A - actual.A)));

                int target = ((y * left.Width) + x) * 4;

                if (delta == 0)
                {
                    diff[target] = expected.R;
                    diff[target + 1] = expected.G;
                    diff[target + 2] = expected.B;
                    diff[target + 3] = expected.A;
                    continue;
                }

                ++differing;
                maxDelta = Math.Max(maxDelta, delta);

                if (firstX < 0)
                {
                    firstX = x;
                    firstY = y;
                }

                // Mark the mismatch in magenta so it stands out in an image viewer.
                diff[target] = 255;
                diff[target + 2] = 255;
                diff[target + 3] = 255;
            }
        }

        if (differing == 0)
        {
            Console.WriteLine($"{name}\tidentical\t{left.Width}x{left.Height}");
            return true;
        }

        Console.WriteLine(
            $"{name}\t{differing} of {left.Width * left.Height} pixels differ\tmax channel delta {maxDelta}\tfirst at {firstX},{firstY}");

        if (diffDirectory is not null)
        {
            string file = Path.Combine(diffDirectory, $"diff_{name}");
            File.WriteAllBytes(file, PngWriter.Encode(new PngImage(left.Width, left.Height, diff)));
            Console.WriteLine($"{name}\tdiff\t{Path.GetFileName(file)}");
        }

        return false;
    }

    private static PngImage ReadPng(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return PngReader.Decode(stream);
    }

    private static (string Name, string OutputDirectory) ParseExportArguments(
        ProbeArguments arguments,
        string command,
        string namePlaceholder)
    {
        if (arguments.Positional.Count != 2)
        {
            throw new ArgumentException($"{command} expects exactly two arguments: {namePlaceholder} <output-dir>.");
        }

        return (arguments.Positional[0], arguments.Positional[1]);
    }

    /// <summary>Loads the palette entry, falling back to the grayscale ramp when it is missing.</summary>
    private static Palette LoadPalette(AssetArchive archive, string paletteName)
    {
        byte[]? raw = archive.TryRead(paletteName);
        if (raw is null)
        {
            Console.Error.WriteLine($"warning: palette '{paletteName}' was not found, falling back to grayscale.");
            return new Palette();
        }

        return Palette.FromGamePalette(raw);
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static AssetArchive OpenArchive(ProbeArguments arguments)
    {
        string? path = arguments.ArchivePath;

        if (path is null)
        {
            string? dataDirectory = AssetLocator.Resolve(arguments.DataDirectory)
                ?? throw new DirectoryNotFoundException(
                    "No game data directory found. Use --data <dir>, --agg <path> or set " +
                    $"{AssetLocator.DataDirectoryEnvironmentVariable}.");

            path = AssetLocator.FindArchive(dataDirectory, AssetLocator.BaseArchiveName)
                ?? throw new FileNotFoundException($"'{AssetLocator.BaseArchiveName}' not found in '{dataDirectory}'.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Archive '{path}' does not exist.");
        }

        return AssetArchive.Open(path);
    }

    private static void PrintUsage()
    {
        Console.WriteLine($"{BuildInfo.DisplayName} — asset probe");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  list    [--data <dir>] [--agg <path>] [--limit <n>]    Lists the entries.");
        Console.WriteLine("  info    [--data <dir>] [--agg <path>]                 Shows statistics.");
        Console.WriteLine("  extract <NAME> <output> [--data <dir>] [--agg <path>] Writes one entry to disk.");
        Console.WriteLine("  sprites <NAME.ICN> <dir> [--palette <NAME>]           Exports sprites as PNG files.");
        Console.WriteLine("  tiles   <NAME.TIL> <dir> [--palette <NAME>]           Exports tiles as PNG files.");
        Console.WriteLine("  compare <left> <right> [--diff <dir>]                 Diffs PNG files or folders pixel by pixel.");
        Console.WriteLine();
        Console.WriteLine("The palette defaults to KB.PAL, which is read from the same archive.");
        Console.WriteLine("compare exits with 0 when everything matches and 3 when a size or a pixel differs.");
        Console.WriteLine();
        Console.WriteLine($"The data directory is resolved from --data, {AssetLocator.DataDirectoryEnvironmentVariable}");
        Console.WriteLine("or a known local install (F:\\game\\fh2 during development).");
    }
}
