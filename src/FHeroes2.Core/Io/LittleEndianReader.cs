using System.Buffers.Binary;
using System.Text;

namespace FHeroes2.Core.Io;

/// <summary>
/// Minimal little-endian reader used for every binary asset of the original game.
/// <see cref="System.IO.BinaryReader"/> is deliberately not used: its endianness handling,
/// string helpers and buffering are not byte-exact for these legacy formats.
/// </summary>
public sealed class LittleEndianReader : IDisposable
{
    /// <summary>Maximum name length stored inside an AGG archive.</summary>
    public const int AggFileNameLength = 15;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _scratch = new byte[8];

    public LittleEndianReader(Stream stream, bool leaveOpen = true)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    public long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public long Length => _stream.Length;

    public long Remaining => _stream.Length - _stream.Position;

    public byte ReadByte()
    {
        int value = _stream.ReadByte();
        if (value < 0)
        {
            throw new BinaryFormatException("Unexpected end of stream while reading a byte.");
        }

        return (byte)value;
    }

    public byte[] ReadBytes(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        byte[] result = new byte[count];
        ReadExactly(result);
        return result;
    }

    public void ReadExactly(Span<byte> destination)
    {
        int read = 0;
        while (read < destination.Length)
        {
            int chunk = _stream.Read(destination[read..]);
            if (chunk <= 0)
            {
                throw new BinaryFormatException(
                    $"Unexpected end of stream: requested {destination.Length} bytes, got {read}.");
            }

            read += chunk;
        }
    }

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadScratch(2));

    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(ReadScratch(2));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadScratch(4));

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadScratch(4));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadScratch(8));

    public void Skip(long count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Skip length must not be negative.");
        }

        _stream.Seek(count, SeekOrigin.Current);
    }

    /// <summary>
    /// Reads a NUL terminated ASCII string, optionally truncated to <paramref name="maxLength"/> bytes.
    /// Equivalent to <c>StreamFile::getString( maxLength )</c>.
    /// </summary>
    public string ReadNullTerminatedAscii(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        Span<byte> buffer = maxLength <= 256 ? stackalloc byte[maxLength] : new byte[maxLength];

        int length = 0;
        while (length < maxLength)
        {
            byte value = ReadByte();
            if (value == 0)
            {
                return Encoding.ASCII.GetString(buffer[..length]);
            }

            buffer[length++] = value;
        }

        return Encoding.ASCII.GetString(buffer);
    }

    /// <summary>Reads a fixed size ASCII field and trims the NUL terminator.</summary>
    public string ReadFixedAscii(int length)
    {
        byte[] raw = ReadBytes(length);
        int terminator = Array.IndexOf(raw, (byte)0);
        int actual = terminator < 0 ? raw.Length : terminator;

        return Encoding.ASCII.GetString(raw, 0, actual);
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private ReadOnlySpan<byte> ReadScratch(int count)
    {
        ReadExactly(_scratch.AsSpan(0, count));
        return _scratch.AsSpan(0, count);
    }
}
