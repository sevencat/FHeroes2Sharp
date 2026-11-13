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

    public string? DiffDirectory { get; private set; }

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

                case "--diff":
                    parsed.DiffDirectory = NextValue(args, ref i, "--diff");
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
}
