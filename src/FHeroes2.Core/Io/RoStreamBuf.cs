using System.Text;

namespace FHeroes2.Core.Io;

/// <summary>
/// A read-only view over bytes somebody else owns, <c>fheroes2::ROStreamBuf</c>
/// (<c>src/engine/serialize.h</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference uses it to hand a piece of an already loaded file to a parser without copying it - a save file
/// carries a compressed block whose header is read through a <c>StreamBuf</c> and whose body is then parsed in place.
/// The port has the same two accessors as spans, <see cref="GetRawView"/> and <see cref="GetStringView"/>, and they
/// allocate nothing.
/// </para>
/// <para>
/// The owning constructor of the reference only exists to say "this view owns the memory it points at". C# has a
/// garbage collector: a <see cref="ReadOnlyMemory{T}"/> over an array keeps the array alive, and a
/// <see cref="ReadOnlyMemory{T}"/> that has been sliced keeps the whole array alive with it, which is why the port
/// never slices - it moves <see cref="StreamBase.Tell"/> instead.
/// </para>
/// </remarks>
public sealed class RoStreamBuf : StreamBase
{
    private readonly ReadOnlyMemory<byte> _buffer;

    private int _position;

    /// <summary>Creates a view over the whole array.</summary>
    /// <param name="data">The bytes to read; the stream keeps a reference, it does not copy them.</param>
    public RoStreamBuf(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        _buffer = data;
    }

    /// <summary>Creates a view over a slice of memory somebody else owns.</summary>
    /// <param name="data">The bytes to read.</param>
    public RoStreamBuf(ReadOnlyMemory<byte> data) => _buffer = data;

    /// <summary>The number of bytes left to read, <c>sizeg()</c>.</summary>
    public override int Size => _buffer.Length - _position;

    /// <summary>The current read position, <c>tellg()</c>.</summary>
    public override int Tell => _position;

    /// <summary>The bytes that have not been read yet, <c>data()</c>.</summary>
    public ReadOnlySpan<byte> Data => _buffer.Span[_position..];

    /// <summary>Moves the read position, <c>seek()</c>.</summary>
    /// <param name="position">The position counted from the start of the view.</param>
    public override void Seek(int position) => _position = Math.Clamp(position, 0, _buffer.Length);

    /// <summary>Moves the read position forward, <c>skip()</c>.</summary>
    /// <param name="size">How many bytes to pass over; a value of zero or less does nothing.</param>
    public override void Skip(int size)
    {
        if (size <= 0)
        {
            return;
        }

        _position += Math.Min(size, Size);
    }

    /// <summary>Reads exactly <paramref name="size"/> bytes into a new array, <c>getRaw()</c>.</summary>
    /// <remarks>A short read is zero padded, like <see cref="StreamBuf"/>.</remarks>
    /// <param name="size">How many bytes to read, or zero for the rest of the view.</param>
    /// <returns>The bytes, empty when there is nothing left and zero bytes were asked for.</returns>
    public override byte[] GetRaw(int size) => GetRawView(size).ToArray();

    /// <summary>Reads a run of bytes without copying them, <c>getRawView()</c>.</summary>
    /// <param name="size">How many bytes to read, or zero for the rest of the view.</param>
    /// <returns>The bytes that are there, which is fewer than asked for when the view runs out.</returns>
    public ReadOnlySpan<byte> GetRawView(int size)
    {
        int resultSize = size > 0 ? size : Size;
        int toCopy = Math.Min(resultSize, Size);

        _position += toCopy;

        return _buffer.Span.Slice(_position - toCopy, toCopy);
    }

    /// <summary>
    /// Reads a run of bytes and cuts it at the first zero, without copying the run, <c>getStringView()</c>.
    /// </summary>
    /// <param name="size">How many bytes to read, or zero for the rest of the view.</param>
    /// <returns>The bytes before the first zero, empty when the view starts with one.</returns>
    public ReadOnlySpan<byte> GetStringView(int size)
    {
        ReadOnlySpan<byte> view = GetRawView(size);
        int length = view.IndexOf((byte)0);

        return length < 0 ? view : view[..length];
    }

    /// <summary>Reads a length prefixed string without allocating the raw run, <c>getString()</c>.</summary>
    /// <param name="size">How many bytes to read, or zero to read a 32 bit length first.</param>
    /// <returns>The string, empty when the view starts with a zero byte.</returns>
    public override string ReadString(int size = 0)
    {
        if (size > 0)
        {
            return Encoding.Latin1.GetString(GetStringView(size));
        }

        // serialize.cpp:135-142: a std::string is a 32 bit length and then exactly that many bytes.
        int length = ReadInt32();

        if (length <= 0)
        {
            if (length < 0)
            {
                SetFail();
            }

            return string.Empty;
        }

        return Encoding.Latin1.GetString(GetRawView(length));
    }

    /// <summary>A view is read-only; writing raises <see cref="NotSupportedException"/>.</summary>
    /// <param name="value">Ignored.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    protected override void Put8(byte value) => throw CreateWriteException();

    /// <summary>A view is read-only; writing raises <see cref="NotSupportedException"/>.</summary>
    /// <param name="data">Ignored.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void PutRaw(ReadOnlySpan<byte> data) => throw CreateWriteException();

    /// <summary>Reads one byte, <c>get8()</c>.</summary>
    /// <returns>The byte, or zero at the end of the view.</returns>
    protected override byte Get8()
    {
        if (Size < 1)
        {
            return 0;
        }

        return _buffer.Span[_position++];
    }

    /// <summary>
    /// The one difference the port makes to a read-only stream: the reference simply has no write side, the port
    /// inherits one and refuses to use it.
    /// </summary>
    private static NotSupportedException CreateWriteException() =>
        new("A read-only stream buffer cannot be written to (ROStreamBuf has no write side in the reference).");
}
