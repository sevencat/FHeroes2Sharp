using System.Buffers.Binary;
using System.Text;

namespace FHeroes2.Core.Io;

/// <summary>
/// Minimal little-endian writer, the counterpart of <see cref="LittleEndianReader"/>.
/// </summary>
public sealed class LittleEndianWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _scratch = new byte[8];

    public LittleEndianWriter(Stream stream, bool leaveOpen = true)
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

    public void WriteByte(byte value) => _stream.WriteByte(value);

    public void WriteBytes(ReadOnlySpan<byte> values) => _stream.Write(values);

    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_scratch, value);
        _stream.Write(_scratch, 0, 2);
    }

    public void WriteInt16(short value) => WriteUInt16(unchecked((ushort)value));

    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_scratch, value);
        _stream.Write(_scratch, 0, 4);
    }

    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    public void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_scratch, value);
        _stream.Write(_scratch, 0, 8);
    }

    /// <summary>Writes an ASCII string padded with NUL bytes up to <paramref name="length"/> bytes.</summary>
    public void WriteFixedAscii(string value, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        byte[] raw = Encoding.ASCII.GetBytes(value);
        if (raw.Length > length)
        {
            throw new ArgumentException(
                $"String '{value}' is longer than the fixed field of {length} bytes.", nameof(value));
        }

        _stream.Write(raw, 0, raw.Length);
        for (int i = raw.Length; i < length; ++i)
        {
            _stream.WriteByte(0);
        }
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
