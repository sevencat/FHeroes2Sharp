namespace FHeroes2.Assets.Tests;

/// <summary>
/// A map file written into a fresh temporary folder and removed on dispose. The map tests use it for
/// both synthetic maps (<see cref="Mp2MapFileBuilder"/>) and deliberately broken content.
/// </summary>
internal sealed class TemporaryMapFile : IDisposable
{
    private readonly string _directory;

    private TemporaryMapFile(string directory, string filePath)
    {
        _directory = directory;
        FilePath = filePath;
    }

    /// <summary>Full path of the created map file.</summary>
    public string FilePath { get; }

    /// <summary>Writes the serialized map of <paramref name="builder"/>.</summary>
    public static TemporaryMapFile Create(Mp2MapFileBuilder builder) => Create(builder.ToArray());

    /// <summary>Writes <paramref name="content"/> as <c>BuilderMap.mp2</c>.</summary>
    public static TemporaryMapFile Create(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        string directory = Path.Combine(Path.GetTempPath(), "fh2-mapbody-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(directory, "BuilderMap.mp2");
        File.WriteAllBytes(filePath, content);

        return new TemporaryMapFile(directory, Path.GetFullPath(filePath));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary folder must not fail a test run.
        }
    }
}
