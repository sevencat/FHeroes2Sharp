using System.IO.Compression;
using System.Text;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.H2D;

/// <summary>One record of an H2D archive, <c>H2DReader::EntryInfo</c>.</summary>
/// <param name="Offset">Where the compressed body starts, from the start of the file.</param>
/// <param name="Size">How many bytes the compressed body occupies.</param>
/// <param name="Info">Free form note stored next to the name.</param>
public readonly record struct H2DEntry(uint Offset, uint Size, string Info);

/// <summary>
/// Reader for the Heroes 2 Data container (<c>resurrection.h2d</c>), <c>H2DReader</c> of
/// <c>src/engine/h2d_file.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The format is not part of the original game: it is how fheroes2 ships the images and data it adds on top of it.
/// Layout:
/// </para>
/// <code>
///   'H' '2' 'D' 3        4 bytes; the version is part of the magic sequence
///   uint32 count
///   count * {
///       uint32 offset    from the start of the file
///       uint32 size      of the compressed body
///       uint32 length + name bytes
///       uint32 length + extra info bytes
///   }
///   count bodies, in entry order
/// </code>
/// <para>
/// Every body is a zlib stream, which is why the reference reaches for <c>zzlib.cpp</c>; the port uses
/// <see cref="ZLibStream"/>, the same choice PNG already makes. An entry with an empty name, a zero size or a body that
/// runs past the end of the file is skipped instead of failing the whole archive, exactly like
/// <c>H2DReader::open</c>; a duplicate name keeps the first record, which is what <c>std::map::try_emplace</c> does.
/// </para>
/// </remarks>
public sealed class H2DArchive : IDisposable
{
    /// <summary>The version byte that ends the magic sequence.</summary>
    public const byte Version = 3;

    /// <summary>
    /// The smallest file the reference accepts: magic, count, offset, size, a five byte name and one byte left over.
    /// It is a sanity floor rather than the size of a real archive.
    /// </summary>
    private const int MinFileSize = 4 + 4 + 4 + 4 + 5 + 1;

    /// <summary>
    /// The smallest record: offset, size and two empty length prefixed strings. Used to reject a corrupt count before
    /// the loop tries to read that many names.
    /// </summary>
    private const int MinEntrySize = 4 + 4 + 4 + 4;

    private readonly FileStream _stream;

    private readonly SortedDictionary<string, H2DEntry> _entries;

    private readonly object _syncRoot = new();

    private H2DArchive(string path, FileStream stream, SortedDictionary<string, H2DEntry> entries)
    {
        Path = path;
        _stream = stream;
        _entries = entries;
    }

    /// <summary>The file this archive was opened from.</summary>
    public string Path { get; }

    /// <summary>How many entries the archive holds.</summary>
    public int Count => _entries.Count;

    /// <summary>The records, ordered by name with an ordinal comparison like the <c>std::map</c> of the reference.</summary>
    public IReadOnlyDictionary<string, H2DEntry> Entries => _entries;

    /// <summary>The names of every entry, in ordinal order.</summary>
    public IReadOnlyCollection<string> FileNames => _entries.Keys;

    /// <summary>Opens an H2D archive from disk.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The archive; the caller owns it.</returns>
    /// <exception cref="BinaryFormatException">The file does not hold a readable H2D archive.</exception>
    public static H2DArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            SortedDictionary<string, H2DEntry> entries = Parse(stream, path);
            return new H2DArchive(path, stream, entries);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Whether the archive has an entry with this name.</summary>
    /// <param name="fileName">The name, case sensitive.</param>
    /// <returns>True when the entry is there.</returns>
    public bool Contains(string fileName) => _entries.ContainsKey(fileName);

    /// <summary>Looks up one record without reading its body.</summary>
    /// <param name="fileName">The name, case sensitive.</param>
    /// <param name="entry">The record that was found.</param>
    /// <returns>True when the entry is there.</returns>
    public bool TryGetEntry(string fileName, out H2DEntry entry) => _entries.TryGetValue(fileName, out entry);

    /// <summary>
    /// Reads and decompresses one body, <c>H2DReader::getFile()</c>.
    /// </summary>
    /// <param name="fileName">The name, case sensitive.</param>
    /// <returns>
    /// The decompressed bytes, or <see langword="null"/> when the entry does not exist or its body is not a readable
    /// zlib stream — the reference returns an empty vector in both cases.
    /// </returns>
    public byte[]? TryRead(string fileName)
    {
        if (!_entries.TryGetValue(fileName, out H2DEntry entry))
        {
            return null;
        }

        byte[] compressed;

        lock (_syncRoot)
        {
            _stream.Seek(entry.Offset, SeekOrigin.Begin);

            using LittleEndianReader reader = new(_stream);
            compressed = reader.ReadBytes(checked((int)entry.Size));
        }

        return Inflate(compressed);
    }

    /// <summary>Reads an entry and throws when it is missing or unreadable.</summary>
    /// <param name="fileName">The name, case sensitive.</param>
    /// <returns>The decompressed bytes.</returns>
    /// <exception cref="KeyNotFoundException">The archive has no such entry, or its body cannot be decompressed.</exception>
    public byte[] Read(string fileName) =>
        TryRead(fileName) ?? throw new KeyNotFoundException(
            $"Entry '{fileName}' was not found in '{Path}' or its body could not be decompressed.");

    public void Dispose() => _stream.Dispose();

    private static SortedDictionary<string, H2DEntry> Parse(FileStream stream, string path)
    {
        long fileLength = stream.Length;
        if (fileLength < MinFileSize)
        {
            throw new BinaryFormatException(
                $"'{path}' is {fileLength} bytes long, shorter than the {MinFileSize} byte H2D header.");
        }

        using LittleEndianReader reader = new(stream);

        if (reader.ReadByte() != 'H' || reader.ReadByte() != '2' || reader.ReadByte() != 'D' || reader.ReadByte() != Version)
        {
            throw new BinaryFormatException($"'{path}' does not start with the H2D sequence or is not version {Version}.");
        }

        uint count = reader.ReadUInt32();
        if (count == 0)
        {
            throw new BinaryFormatException($"'{path}' declares no entries.");
        }

        // Every record needs at least MinEntrySize bytes, so a count that cannot fit is a corrupt header rather than a
        // reason to read until the end of the file.
        if ((long)count * MinEntrySize > fileLength - 8)
        {
            throw new BinaryFormatException(
                $"'{path}' declares {count} entries but is only {fileLength} bytes long.");
        }

        SortedDictionary<string, H2DEntry> entries = new(StringComparer.Ordinal);

        for (uint index = 0; index < count; ++index)
        {
            uint offset = reader.ReadUInt32();
            uint size = reader.ReadUInt32();
            string name = ReadString(reader, path);
            string info = ReadString(reader, path);

            if (size == 0 || (long)offset + size > fileLength || name.Length == 0)
            {
                continue;
            }

            entries.TryAdd(name, new H2DEntry(offset, size, info));
        }

        return entries;
    }

    /// <summary>Reads a <c>uint32</c> length followed by that many bytes, <c>IStreamBase::operator&gt;&gt;( std::string )</c>.</summary>
    private static string ReadString(LittleEndianReader reader, string path)
    {
        uint length = reader.ReadUInt32();
        if (length > reader.Remaining)
        {
            throw new BinaryFormatException(
                $"'{path}' has a string of {length} bytes but only {reader.Remaining} bytes are left.");
        }

        return Encoding.ASCII.GetString(reader.ReadBytes((int)length));
    }

    private static byte[]? Inflate(byte[] compressed)
    {
        try
        {
            using MemoryStream input = new(compressed, writable: false);
            using ZLibStream inflate = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();

            inflate.CopyTo(output);

            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            // The reference returns an empty vector when zlib refuses the data.
            return null;
        }
    }
}
