namespace FHeroes2.Core.Io;

/// <summary>
/// A growable in-memory stream, <c>fheroes2::RWStreamBuf</c> (<c>src/engine/serialize.h</c>,
/// <c>src/engine/serialize.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The memory the reference keeps around for one save or one load: the data is written from the start, read back from
/// the same buffer, and the read position is the only thing <see cref="Seek"/> and <see cref="Skip"/> move. The buffer
/// grows by half its capacity, or by the size of the write when that is larger, which is the reference's policy and
/// the reason a save file does not reallocate on every field.
/// </para>
/// <para>
/// <b>Short reads are zero padded.</b> Asking for more bytes than are left hands back the bytes that are there and
/// fills the rest with zeroes without raising <see cref="StreamBase.Fail"/> - the reference's memory stream does not,
/// because the padding is what makes a save file that ends in the middle of an optional field readable at all. The
/// file stream behaves the other way round; see <see cref="StreamFile"/>.
/// </para>
/// </remarks>
public sealed class StreamBuf : StreamBase
{
    /// <summary>The smallest buffer the stream ever allocates, <c>MINIMUM_BUFFER_SIZE</c>.</summary>
    private const int MinimumBufferSize = 1024;

    private byte[] _buffer = [];

    private int _get;

    private int _put;

    /// <summary>Creates an empty stream, <c>RWStreamBuf()</c>.</summary>
    public StreamBuf()
    {
    }

    /// <summary>Creates a stream that starts with room for <paramref name="capacity"/> bytes, <c>RWStreamBuf( size_t )</c>.</summary>
    /// <param name="capacity">The initial capacity in bytes.</param>
    public StreamBuf(int capacity)
    {
        if (capacity > 0)
        {
            _buffer = new byte[capacity];
        }
    }

    /// <summary>The number of bytes left to read, <c>sizeg()</c>.</summary>
    public override int Size => _put - _get;

    /// <summary>The current read position, <c>tellg()</c>.</summary>
    public override int Tell => _get;

    /// <summary>The capacity of the buffer in bytes, <c>capacity()</c>.</summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// The bytes that have not been read yet, <c>data()</c> paired with <see cref="Size"/>.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="Compression.ZipStreamBuf"/> compresses: everything written so far, from the read
    /// position on, so a stream can be written in chunks and each chunk saved on its own.
    /// </remarks>
    public ReadOnlySpan<byte> Data => _buffer.AsSpan(_get, _put - _get);

    /// <summary>Everything written so far, read position included.</summary>
    /// <remarks>
    /// The reference has no such member; it hands <c>data()</c> and <c>size()</c> around. The port needs the whole
    /// buffer to write a finished save to a file after the padding has been squeezed out of it.
    /// </remarks>
    public ReadOnlySpan<byte> Written => _buffer.AsSpan(0, _put);

    /// <summary>Moves the read position, <c>seek()</c>.</summary>
    /// <remarks>
    /// Positions past the end of the written data land on the end, as in the reference, and a negative one lands on
    /// the start: the reference takes a <c>size_t</c> and cannot be handed one.
    /// </remarks>
    /// <param name="position">The position counted from the start of the buffer.</param>
    public override void Seek(int position) => _get = position <= 0 ? 0 : Math.Min(position, _put);

    /// <summary>Moves the read position forward, <c>skip()</c>.</summary>
    /// <param name="size">How many bytes to pass over; a value of zero or less does nothing.</param>
    public override void Skip(int size)
    {
        if (size <= 0)
        {
            return;
        }

        _get += Math.Min(size, Size);
    }

    /// <summary>Empties the stream and keeps the allocated buffer, <c>reset()</c>.</summary>
    public void Reset()
    {
        _get = 0;
        _put = 0;
    }

    /// <summary>Reads exactly <paramref name="size"/> bytes, zero padding a short read, <c>getRaw()</c>.</summary>
    /// <param name="size">How many bytes to read, or zero for the rest of the stream.</param>
    /// <returns>The bytes, empty when the stream has nothing left and zero bytes were asked for.</returns>
    public override byte[] GetRaw(int size)
    {
        int resultSize = size > 0 ? size : Size;
        if (resultSize == 0)
        {
            return [];
        }

        int toCopy = Math.Min(resultSize, Size);

        byte[] result = new byte[resultSize];
        _buffer.AsSpan(_get, toCopy).CopyTo(result);
        _get += toCopy;

        return result;
    }

    /// <summary>Appends one byte, growing the buffer when it is full, <c>put8()</c>.</summary>
    /// <param name="value">The byte.</param>
    protected override void Put8(byte value)
    {
        EnsureCapacity(1);
        _buffer[_put++] = value;
    }

    /// <summary>Appends bytes, growing the buffer when it is full, <c>putRaw()</c>.</summary>
    /// <param name="data">The bytes to append.</param>
    public override void PutRaw(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_put));
        _put += data.Length;
    }

    /// <summary>Reads one byte, <c>get8()</c>.</summary>
    /// <returns>The byte, or zero at the end of the stream.</returns>
    protected override byte Get8()
    {
        if (Size < 1)
        {
            return 0;
        }

        return _buffer[_get++];
    }

    /// <summary>
    /// Makes room for <paramref name="size"/> more bytes, <c>reallocBuf()</c>.
    /// </summary>
    /// <remarks>
    /// The policy of the reference is kept for the same reason it has it: a field that only needs a few bytes must not
    /// double a megabyte buffer, and a field that needs a lot must not grow by half a dozen times in a row.
    /// </remarks>
    /// <param name="size">How many bytes have to fit after the written data.</param>
    private void EnsureCapacity(int size)
    {
        if (_buffer.Length - _put >= size)
        {
            return;
        }

        if (_buffer.Length == 0)
        {
            _buffer = new byte[Math.Max(size, MinimumBufferSize)];
            return;
        }

        int grown = size < _buffer.Length / 2 ? _buffer.Length + (_buffer.Length / 2) : _buffer.Length + size;

        byte[] replacement = new byte[grown];
        _buffer.AsSpan(0, _put).CopyTo(replacement);
        _buffer = replacement;
    }
}
