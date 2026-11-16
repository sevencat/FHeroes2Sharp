using System.IO.Compression;
using System.Text;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.H2D;

/// <summary>
/// Writer for the Heroes 2 Data container, <c>H2DWriter</c> of <c>src/engine/h2d_file.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Bodies are added already decompressed and are zlib compressed here with the strongest level, which is what
/// <c>Compression::zipData( …, true )</c> asks zlib for. Entries are written in ordinal name order, matching the
/// <c>std::map</c> of the reference, so a rewritten archive is byte for byte what the reference would produce.
/// </para>
/// <para>This is tooling: the game itself only reads archives.</para>
/// </remarks>
public sealed class H2DWriter
{
    private readonly SortedDictionary<string, H2DWriterFile> _files = new(StringComparer.Ordinal);

    /// <summary>The names added so far, in ordinal order.</summary>
    public IReadOnlyCollection<string> FileNames => _files.Keys;

    /// <summary>Adds a decompressed body, replacing an entry with the same name.</summary>
    /// <param name="name">The entry name; must not be empty.</param>
    /// <param name="data">The body; must not be empty.</param>
    /// <param name="extraInfo">Free form note stored next to the name.</param>
    /// <returns>False when the name or the body is empty, like <c>H2DWriter::add</c>.</returns>
    public bool Add(string name, ReadOnlySpan<byte> data, string extraInfo = "")
    {
        if (string.IsNullOrEmpty(name) || data.IsEmpty)
        {
            return false;
        }

        using MemoryStream compressed = new();
        using (ZLibStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(data);
        }

        _files[name] = new H2DWriterFile(compressed.ToArray(), extraInfo);
        return true;
    }

    /// <summary>Copies every readable entry of an archive into this writer, <c>H2DWriter::add( H2DReader &amp; )</c>.</summary>
    /// <param name="archive">The archive to copy from.</param>
    /// <returns>
    /// False as soon as one body cannot be read or compressed, after the earlier entries have been added — the
    /// reference leaves the writer in the same partially filled state.
    /// </returns>
    public bool Add(H2DArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        foreach ((string name, H2DEntry entry) in archive.Entries)
        {
            byte[]? data = archive.TryRead(name);
            if (data is null || !Add(name, data, entry.Info))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Removes an entry.</summary>
    /// <param name="name">The entry name.</param>
    /// <returns>True when the entry was there.</returns>
    public bool Remove(string name) => _files.Remove(name);

    /// <summary>
    /// Serialises the archive, <c>H2DWriter::write()</c> without the file system.
    /// </summary>
    /// <returns>The archive bytes, or <see langword="null"/> when nothing was added.</returns>
    public byte[]? Serialize()
    {
        if (_files.Count == 0)
        {
            return null;
        }

        // The record table comes first, then the bodies; every offset is relative to the start of the file.
        long recordSectionSize = 0;
        foreach ((string name, H2DWriterFile file) in _files)
        {
            recordSectionSize += 4 + 4 + 4 + Encoding.ASCII.GetByteCount(name) + 4 + Encoding.ASCII.GetByteCount(file.Info);
        }

        long offset = recordSectionSize + 4 + 4;

        using MemoryStream output = new();
        using (LittleEndianWriter writer = new(output, leaveOpen: true))
        {
            writer.WriteByte((byte)'H');
            writer.WriteByte((byte)'2');
            writer.WriteByte((byte)'D');
            writer.WriteByte(H2DArchive.Version);
            writer.WriteUInt32((uint)_files.Count);

            foreach ((string name, H2DWriterFile file) in _files)
            {
                writer.WriteUInt32(checked((uint)offset));
                writer.WriteUInt32(checked((uint)file.Data.Length));
                WriteString(writer, name);
                WriteString(writer, file.Info);
                offset += file.Data.Length;
            }

            foreach (H2DWriterFile file in _files.Values)
            {
                writer.WriteBytes(file.Data);
            }
        }

        return output.ToArray();
    }

    /// <summary>Writes the archive to disk, <c>H2DWriter::write( const std::string &amp; )</c>.</summary>
    /// <param name="path">The file to create or overwrite.</param>
    /// <returns>False when nothing was added or the file cannot be written.</returns>
    public bool Write(string path)
    {
        byte[]? bytes = Serialize();
        if (bytes is null)
        {
            return false;
        }

        try
        {
            File.WriteAllBytes(path, bytes);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteString(LittleEndianWriter writer, string value)
    {
        writer.WriteUInt32((uint)Encoding.ASCII.GetByteCount(value));
        writer.WriteBytes(Encoding.ASCII.GetBytes(value));
    }

    private readonly record struct H2DWriterFile(byte[] Data, string Info);
}
