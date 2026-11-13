using System.Text;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Agg;

/// <summary>
/// Reader for the Heroes II AGG container format (<c>heroes2.agg</c>, <c>heroes2x.agg</c>, …).
///
/// Layout:
/// <code>
///   uint16 count
///   count * { uint32 nameHash, uint32 offset, uint32 size }
///   at EOF: count * 15 byte NUL padded, upper case file names (same order as the records)
/// </code>
/// Every record is validated against the stored hash; a mismatch means the archive is corrupt,
/// which is exactly what <c>AGGFile::open</c> does.
/// </summary>
public sealed class AssetArchive : IDisposable
{
    private const int RecordSize = 12;

    private readonly FileStream _stream;
    private readonly Dictionary<string, AssetEntry> _entries;
    private readonly object _syncRoot = new();

    private AssetArchive(string path, FileStream stream, Dictionary<string, AssetEntry> entries, AssetEntry[] ordered)
    {
        Path = path;
        _stream = stream;
        _entries = entries;
        Entries = ordered;
    }

    public string Path { get; }

    public int Count => _entries.Count;

    /// <summary>All records in file order (the name table order), useful for diagnostics tools.</summary>
    public IReadOnlyList<AssetEntry> Entries { get; }

    public static AssetArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            (Dictionary<string, AssetEntry> entries, AssetEntry[] ordered) = Parse(stream, path);
            return new AssetArchive(path, stream, entries, ordered);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public bool Contains(string fileName) => _entries.ContainsKey(fileName);

    public bool TryGetEntry(string fileName, out AssetEntry entry) => _entries.TryGetValue(fileName, out entry);

    /// <summary>Reads the full contents of an asset, or <see langword="null"/> when it does not exist.</summary>
    public byte[]? TryRead(string fileName)
    {
        if (!_entries.TryGetValue(fileName, out AssetEntry entry))
        {
            return null;
        }

        if (entry.Size == 0)
        {
            return Array.Empty<byte>();
        }

        lock (_syncRoot)
        {
            _stream.Seek(entry.Offset, SeekOrigin.Begin);

            using LittleEndianReader reader = new(_stream);
            return reader.ReadBytes(checked((int)entry.Size));
        }
    }

    /// <summary>Reads an asset and throws when it is missing.</summary>
    public byte[] Read(string fileName) =>
        TryRead(fileName) ?? throw new KeyNotFoundException($"Asset '{fileName}' was not found in '{Path}'.");

    public void Dispose() => _stream.Dispose();

    private static (Dictionary<string, AssetEntry> Entries, AssetEntry[] Ordered) Parse(FileStream stream, string path)
    {
        using LittleEndianReader reader = new(stream);

        long fileLength = reader.Length;
        if (fileLength < 2)
        {
            throw new BinaryFormatException($"'{path}' is too short to be an AGG archive.");
        }

        int count = reader.ReadUInt16();
        long nameTableStart = fileLength - ((long)count * AggFileNameHash.MaxFileNameLength);
        long recordsEnd = 2 + ((long)count * RecordSize);

        if (nameTableStart < 0)
        {
            throw new BinaryFormatException(
                $"'{path}' declares {count} entries but is only {fileLength} bytes long.");
        }

        if (recordsEnd > nameTableStart)
        {
            throw new BinaryFormatException($"'{path}' has overlapping record and name tables.");
        }

        Dictionary<string, AssetEntry> entries = new(StringComparer.Ordinal);
        AssetEntry[] ordered = new AssetEntry[count];

        for (int index = 0; index < count; ++index)
        {
            reader.Position = 2 + ((long)index * RecordSize);

            uint expectedHash = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            uint size = reader.ReadUInt32();

            reader.Position = nameTableStart + ((long)index * AggFileNameHash.MaxFileNameLength);
            string name = ReadName(reader);

            uint actualHash = AggFileNameHash.Compute(name);
            if (actualHash != expectedHash)
            {
                throw new BinaryFormatException(
                    $"'{path}' is corrupted: name '{name}' hashes to 0x{actualHash:X8} but 0x{expectedHash:X8} is stored.");
            }

            if ((long)offset + size > fileLength)
            {
                throw new BinaryFormatException(
                    $"'{path}' is corrupted: '{name}' points outside of the file ({offset}+{size} > {fileLength}).");
            }

            AssetEntry entry = new(name, offset, size);
            if (!entries.TryAdd(name, entry))
            {
                throw new BinaryFormatException($"'{path}' contains the duplicate entry '{name}'.");
            }

            ordered[index] = entry;
        }

        return (entries, ordered);
    }

    private static string ReadName(LittleEndianReader reader)
    {
        Span<byte> raw = stackalloc byte[AggFileNameHash.MaxFileNameLength];
        reader.ReadExactly(raw);

        int length = raw.IndexOf((byte)0);
        if (length < 0)
        {
            length = raw.Length;
        }

        return Encoding.ASCII.GetString(raw[..length]);
    }
}
