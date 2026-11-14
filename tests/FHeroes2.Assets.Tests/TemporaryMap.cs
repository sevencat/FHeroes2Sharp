using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// A synthetic MP2 map file in the temporary folder. The information block is filled with known
/// values so a test can assert every field of <see cref="Mp2FileInfo"/> without the real game data.
/// </summary>
internal sealed class TemporaryMap : IDisposable
{
    private readonly string _directory;

    private TemporaryMap(string directory, string filePath)
    {
        _directory = directory;
        FilePath = filePath;
    }

    /// <summary>Full path of the created map file.</summary>
    public string FilePath { get; }

    /// <summary>Creates <c>TestMap&lt;extension&gt;</c> in a fresh temporary folder.</summary>
    public static TemporaryMap Create(
        string extension,
        Action<byte[]>? configureHeader = null,
        string name = "Test Map",
        string description = "A map made for the port.")
    {
        string directory = Path.Combine(Path.GetTempPath(), "fh2-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(directory, "TestMap" + extension);
        File.WriteAllBytes(filePath, CreateContent(configureHeader, name, description));

        return new TemporaryMap(directory, Path.GetFullPath(filePath));
    }

    /// <summary>
    /// The 428 byte information block with the values the tests rely on: 72x72 tiles, normal
    /// difficulty, blue and red kingdoms, blue is the only human player, capture town victory,
    /// lose after 30 days, a knight for blue, a random race for green and a necromancer for purple.
    /// </summary>
    public static byte[] CreateContent(
        Action<byte[]>? configureHeader = null,
        string name = "Test Map",
        string description = "A map made for the port.")
    {
        byte[] header = new byte[Mp2FileInfo.HeaderSize];

        BinaryPrimitives.WriteUInt32BigEndian(header, 0x5C00_0000);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), 1); // normal difficulty
        header[6] = 72;
        header[7] = 72;

        header[8] = 1; // kingdom: blue
        header[10] = 1; // kingdom: red
        header[14] = 1; // human: blue
        header[20] = 1; // computer: blue
        header[22] = 1; // computer: red

        header[29] = 1; // victory: capture town
        header[30] = 1; // computer also wins
        header[31] = 1; // normal victory is allowed too
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32), 12);
        header[34] = 3; // loss: out of time
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(35), 30);
        header[37] = 0; // start with a hero in the first castle

        header[38] = 0; // blue: knight
        header[39] = 7; // green: random
        header[43] = 5; // purple: necromancer

        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(44), 34);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(46), 21);

        WriteField(header, Mp2FileInfo.NameFieldOffset, Mp2FileInfo.NameFieldLength, name);
        WriteField(header, Mp2FileInfo.DescriptionFieldOffset, Mp2FileInfo.DescriptionFieldLength, description);

        configureHeader?.Invoke(header);
        return header;
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

    private static void WriteField(byte[] header, int offset, int length, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, header, offset, Math.Min(bytes.Length, length - 1));
    }
}
