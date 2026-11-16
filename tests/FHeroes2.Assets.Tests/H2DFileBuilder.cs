using System.IO.Compression;
using System.Text;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Builds H2D archives byte by byte so that malformed layouts — a bad magic, a body outside the file, a duplicate
/// name — can be tested explicitely, and so that the reader and the writer can be checked against each other.
/// </summary>
internal static class H2DFileBuilder
{
    public const byte Version = 3;

    /// <summary>zlib compresses a body the way <c>Compression::zipData( …, true )</c> does.</summary>
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using MemoryStream output = new();
        using (ZLibStream stream = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>Writes a well formed archive; the bodies are stored exactly as they are given.</summary>
    public static byte[] Build(params (string Name, string Info, byte[] Body)[] entries)
    {
        List<byte> header = [];
        using (MemoryStream records = new())
        {
            using BinaryWriter writer = new(records, Encoding.ASCII, leaveOpen: true);

            long recordsSize = entries.Sum(entry =>
                4 + 4 + 4 + Encoding.ASCII.GetByteCount(entry.Name) + 4 + Encoding.ASCII.GetByteCount(entry.Info));

            long offset = 4 + 4 + recordsSize;
            foreach ((string name, string info, byte[] body) in entries)
            {
                writer.Write((uint)offset);
                writer.Write((uint)body.Length);
                WriteString(writer, name);
                WriteString(writer, info);
                offset += body.Length;
            }

            header.AddRange(records.ToArray());
        }

        using MemoryStream output = new();
        using (BinaryWriter writer = new(output, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write((byte)'H');
            writer.Write((byte)'2');
            writer.Write((byte)'D');
            writer.Write(Version);
            writer.Write((uint)entries.Length);
            writer.Write(header.ToArray());

            foreach ((string _, string _, byte[] body) in entries)
            {
                writer.Write(body);
            }
        }

        return output.ToArray();
    }

    public static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }
}
