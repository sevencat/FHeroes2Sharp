using System.IO.Compression;
using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Core.Io;

/// <summary>
/// The zlib blocks a save file is made of, <c>Compression</c> (<c>src/engine/zzlib.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference links zlib; the port uses <see cref="ZLibStream"/> of the framework, the same choice the PNG and H2D
/// readers already make (<c>docs/DECISIONS.md</c>, ADR-062). A zlib stream is exactly what <c>compress2()</c> writes,
/// so the bytes on disk are the ones the original game and fheroes2 write.
/// </para>
/// <para>
/// <b>The block format.</b> A save file does not store a bare zlib stream: each block starts with the size it
/// decompresses to, the size it occupies, a format version and two unused bytes. The two sizes let the reader
/// allocate the result up front, which is why <see cref="UnzipStream"/> can insist on the exact size instead of
/// guessing.
/// </para>
/// <para>
/// <b>Nothing throws.</b> Every member answers with an empty result or <see langword="false"/> and logs, like the
/// reference: the caller of a corrupted save is a dialog, and an exception would take the message away from it.
/// </para>
/// </remarks>
public static class Compression
{
    /// <summary>The only block format version there is, <c>FORMAT_VERSION_0</c>.</summary>
    public const ushort FormatVersion0 = 0;

    /// <summary>
    /// The ceiling the port puts on a block whose size it does not know in advance.
    /// </summary>
    /// <remarks>
    /// The reference lets zlib decide how far it may grow. A save file the player can edit is a good enough reason to
    /// keep a number here: half a gigabyte is far more than any world of the game takes, and far less than a corrupted
    /// stream would like to allocate.
    /// </remarks>
    private const int MaximumDecompressedSize = 512 * 1024 * 1024;

    /// <summary>Decompresses a zlib stream, <c>Compression::unzipData()</c>.</summary>
    /// <param name="source">The zlib stream.</param>
    /// <param name="realSize">
    /// The size the result must have, or zero when it is not known; the reference's "guess and grow" loop is replaced
    /// by the framework stream, so the port only needs this to allocate once.
    /// </param>
    /// <returns>The bytes, empty when the stream is broken or does not decompress to <paramref name="realSize"/>.</returns>
    public static byte[] UnzipData(ReadOnlySpan<byte> source, int realSize = 0)
    {
        if (source.IsEmpty || realSize < 0)
        {
            return [];
        }

        if (realSize > 0)
        {
            byte[] result = new byte[realSize];

            return UnzipData(source, result, out int written) && written == realSize ? result : [];
        }

        try
        {
            using MemoryStream input = new(source.ToArray(), writable: false);
            using ZLibStream zlib = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();

            byte[] chunk = new byte[64 * 1024];
            int read;
            while ((read = zlib.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (output.Length + read > MaximumDecompressedSize)
                {
                    GameLog.Error("The decompressed data is too large.");

                    return [];
                }

                output.Write(chunk, 0, read);
            }

            return output.ToArray();
        }
        catch (InvalidDataException ex)
        {
            GameLog.Error($"The compressed data is broken: {ex.Message}");

            return [];
        }
    }

    /// <summary>Decompresses a zlib stream into a buffer of known size, <c>Compression::unzipData()</c>.</summary>
    /// <param name="source">The zlib stream.</param>
    /// <param name="destination">The buffer that has to hold the whole result.</param>
    /// <param name="decompressedSize">Receives how many bytes the result has.</param>
    /// <returns>
    /// Whether the stream decompressed and fit; a result larger than <paramref name="destination"/> is a failure, as in
    /// the reference, where zlib answers <c>Z_BUF_ERROR</c> and the caller gives up.
    /// </returns>
    public static bool UnzipData(ReadOnlySpan<byte> source, Span<byte> destination, out int decompressedSize)
    {
        decompressedSize = 0;

        if (source.IsEmpty || destination.IsEmpty)
        {
            return false;
        }

        try
        {
            using MemoryStream input = new(source.ToArray(), writable: false);
            using ZLibStream zlib = new(input, CompressionMode.Decompress);

            int total = 0;
            while (total < destination.Length)
            {
                int read = zlib.Read(destination[total..]);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            // Reading one more byte is how the port asks zlib whether it had more to give than the buffer holds.
            if (total == destination.Length && zlib.Read(stackalloc byte[1]) > 0)
            {
                GameLog.Error("The decompressed data does not fit the buffer.");

                return false;
            }

            decompressedSize = total;

            return true;
        }
        catch (InvalidDataException ex)
        {
            GameLog.Error($"The compressed data is broken: {ex.Message}");

            return false;
        }
    }

    /// <summary>Compresses bytes into a zlib stream, <c>Compression::zipData()</c>.</summary>
    /// <param name="source">The bytes to compress.</param>
    /// <param name="maximumCompression">
    /// Whether to use the strongest level, <c>Z_BEST_COMPRESSION</c>, instead of the default one
    /// (<c>Z_DEFAULT_COMPRESSION</c>); the save code passes <see langword="false"/>.
    /// </param>
    /// <returns>The zlib stream, empty when there is nothing to compress or the compressor failed.</returns>
    public static byte[] ZipData(ReadOnlySpan<byte> source, bool maximumCompression)
    {
        if (source.IsEmpty)
        {
            return [];
        }

        try
        {
            using MemoryStream output = new();

            using (ZLibStream zlib = new(
                output,
                maximumCompression ? CompressionLevel.SmallestSize : CompressionLevel.Optimal,
                leaveOpen: true))
            {
                zlib.Write(source);
            }

            return output.ToArray();
        }
        catch (IOException ex)
        {
            GameLog.Error($"Compression failed: {ex.Message}");

            return [];
        }
    }

    /// <summary>
    /// Writes the bytes left in a buffer as a compressed block, <c>Compression::zipStreamBuf()</c>.
    /// </summary>
    /// <param name="input">The buffer; everything from its read position on is compressed.</param>
    /// <param name="output">The stream that receives the block.</param>
    /// <returns>Whether the block was written.</returns>
    public static bool ZipStreamBuf(StreamBuf input, StreamBase output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        return WriteBlock(input.Data, output);
    }

    /// <summary>
    /// Writes the bytes left in a view as a compressed block, <c>Compression::zipStreamBuf()</c>.
    /// </summary>
    /// <param name="input">The view; everything from its read position on is compressed.</param>
    /// <param name="output">The stream that receives the block.</param>
    /// <returns>Whether the block was written.</returns>
    public static bool ZipStreamBuf(RoStreamBuf input, StreamBase output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        return WriteBlock(input.Data, output);
    }

    /// <summary>
    /// Reads a compressed block and writes the bytes it holds to another stream, <c>Compression::unzipStream()</c>.
    /// </summary>
    /// <param name="input">The stream to read the block from.</param>
    /// <param name="output">The stream that receives the decompressed bytes.</param>
    /// <returns>Whether the block was read, decompressed and written.</returns>
    public static bool UnzipStream(StreamBase input, StreamBase output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        int rawSize = input.ReadInt32();
        int zipSize = input.ReadInt32();

        if (zipSize <= 0 || rawSize < 0)
        {
            return false;
        }

        if (input.ReadUInt16() != FormatVersion0)
        {
            return false;
        }

        input.Skip(2); // The reference writes two unused bytes and only reads them to keep the offsets.

        byte[] zip = input.GetRaw(zipSize);
        byte[] raw = UnzipData(zip, rawSize);

        if (raw.Length != rawSize)
        {
            return false;
        }

        output.PutRaw(raw);

        return !output.Fail;
    }

    /// <summary>Writes one block: the two sizes, the version, two unused bytes and the zlib stream.</summary>
    /// <param name="raw">The bytes to compress.</param>
    /// <param name="output">The stream that receives the block.</param>
    /// <returns>Whether the block was written.</returns>
    private static bool WriteBlock(ReadOnlySpan<byte> raw, StreamBase output)
    {
        byte[] zip = ZipData(raw, maximumCompression: false);
        if (zip.Length == 0)
        {
            return false;
        }

        output.Write(raw.Length);
        output.Write(zip.Length);
        output.Write(FormatVersion0);
        output.Write((ushort)0); // Unused bytes.

        output.PutRaw(zip);

        return !output.Fail;
    }
}
