namespace FHeroes2.Client.Desktop.Fonts;

/// <summary>A font file that can be handed to the text backend.</summary>
/// <param name="Path">Absolute path to the TrueType/OpenType file.</param>
/// <param name="SupportsCjk">Whether the UI may show Chinese/Japanese/Korean text with this font.</param>
public readonly record struct FontChoice(string Path, bool SupportsCjk);

/// <summary>
/// Finds a usable TrueType font without shipping one: an explicitly configured font wins,
/// then files bundled in <c>assets/fonts</c>, then well known Windows fonts.
/// </summary>
public static class SystemFontLocator
{
    public const string FontOverrideEnvironmentVariable = "FHEROES2_FONT";

    private static readonly (string FileName, bool SupportsCjk)[] WindowsFonts =
    [
        ("simhei.ttf", true),
        ("msyh.ttc", true),
        ("msyhbd.ttc", true),
        ("simsun.ttc", true),
        ("arialuni.ttf", true),
        ("segoeui.ttf", false),
        ("consola.ttf", false),
        ("arial.ttf", false),
    ];

    /// <summary>Yields candidates in priority order; callers try to load them one by one.</summary>
    public static IEnumerable<FontChoice> EnumerateCandidates()
    {
        string? overridePath = Environment.GetEnvironmentVariable(FontOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            yield return new FontChoice(Path.GetFullPath(overridePath), true);
        }

        string bundledDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        if (Directory.Exists(bundledDirectory))
        {
            IEnumerable<string> files = Directory
                .EnumerateFiles(bundledDirectory)
                .Where(IsSupportedFontFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                yield return new FontChoice(Path.GetFullPath(file), LooksLikeCjkFont(Path.GetFileNameWithoutExtension(file)));
            }
        }

        string systemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(systemFonts))
        {
            foreach ((string fileName, bool supportsCjk) in WindowsFonts)
            {
                string path = Path.Combine(systemFonts, fileName);
                if (File.Exists(path))
                {
                    yield return new FontChoice(path, supportsCjk);
                }
            }
        }
    }

    private static bool IsSupportedFontFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCjkFont(string nameWithoutExtension) =>
        nameWithoutExtension.Contains("cjk", StringComparison.OrdinalIgnoreCase)
        || nameWithoutExtension.Contains("han", StringComparison.OrdinalIgnoreCase)
        || nameWithoutExtension.Contains("hei", StringComparison.OrdinalIgnoreCase)
        || nameWithoutExtension.Contains("song", StringComparison.OrdinalIgnoreCase)
        || nameWithoutExtension.Contains("yahei", StringComparison.OrdinalIgnoreCase)
        || nameWithoutExtension.Contains("noto", StringComparison.OrdinalIgnoreCase);
}
