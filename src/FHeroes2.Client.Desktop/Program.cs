using FHeroes2.Assets;
using FHeroes2.Assets.Agg;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Io;

namespace FHeroes2.Client.Desktop;

/// <summary>Entry point of the desktop client.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            CommandLineOptions.PrintUsage();
            return 2;
        }

        if (options.ShowHelp)
        {
            CommandLineOptions.PrintUsage();
            return 0;
        }

        if (options.ListAssets)
        {
            return PrintAssetInventory(options.DataDirectory);
        }

        using GameHost host = new(options);
        host.Run();

        return host.ExitCode;
    }

    private static int PrintAssetInventory(string? dataDirectory)
    {
        try
        {
            using AssetBundle bundle = AssetBundle.Open(dataDirectory);

            Console.WriteLine($"Data directory: {bundle.DataDirectory}");
            Console.WriteLine($"heroes2.agg:    {bundle.BaseArchive.Count} entries ({bundle.BaseArchive.Path})");

            if (bundle.ExpansionArchive is not null)
            {
                Console.WriteLine($"heroes2x.agg:   {bundle.ExpansionArchive.Count} entries ({bundle.ExpansionArchive.Path})");
            }

            Console.WriteLine();
            foreach (AssetEntry entry in bundle.BaseArchive.Entries)
            {
                Console.WriteLine($"{entry.Name,-16} offset=0x{entry.Offset:X8} size={entry.Size,10}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or BinaryFormatException or DirectoryNotFoundException or FileNotFoundException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
