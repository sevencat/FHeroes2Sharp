using FHeroes2.Assets;
using FHeroes2.Assets.Agg;
using FHeroes2.Core.Diagnostics;
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
        Console.WriteLine();
        Console.WriteLine($"The data directory is resolved from --data, {AssetLocator.DataDirectoryEnvironmentVariable}");
        Console.WriteLine("or a known local install (F:\\game\\fh2 during development).");
    }
}
