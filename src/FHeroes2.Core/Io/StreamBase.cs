using System.Text;

namespace FHeroes2.Core.Io;

/// <summary>
/// The byte stream every save file is written through, <c>fheroes2::StreamBase</c>
/// (<c>src/engine/serialize.h</c>, <c>src/engine/serialize.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Structural difference.</b> The reference splits a stream into <c>IStreamBase</c> (reading) and
/// <c>OStreamBase</c> (writing) and joins them with virtual inheritance, so that a read-only stream simply does not
/// offer the write side. C# has no virtual inheritance: the port keeps one <see cref="StreamBase"/> and asks the
/// concrete stream only for <see cref="Get8"/> and <see cref="Put8"/>, everything else - the endian-aware 16 and 32
/// bit reads, the strings, the failure flag - is written once, here. A read-only stream overrides the write side with
/// <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// <b>Names.</b> The reference reaches for operator overloads (<c>stream &lt;&lt; value</c>, <c>stream &gt;&gt; value</c>).
/// C# cannot overload &lt;&lt; for a byte, so the port names the write side <c>Write</c> (one overload per type, in the
/// order of the reference) and the read side <c>ReadXxx</c> after the type the overload returns.
/// </para>
/// <para>
/// <b>A short read never throws.</b> The stream raises <see cref="Fail"/> and returns zeroes, exactly like the
/// reference: a truncated save file has to be reported to the player, not unwind the loader halfway through a world.
/// Every read of a save file is followed by a <see cref="Fail"/> check at the end.
/// </para>
/// <para>
/// <b>Byte strings.</b> The reference copies the bytes of a <c>std::string</c> as they are; the port uses
/// <see cref="Encoding.Latin1"/>, the one encoding that maps every byte to one char and back, so a name read from a
/// map file and written into a save file comes out unchanged whatever code page the map used.
/// </para>
/// </remarks>
public abstract class StreamBase
{
    private bool _fail;

    private bool _bigEndian;

    /// <summary>Whether a read or a write has failed since the stream was opened.</summary>
    /// <remarks>Named after <c>fail()</c> of the reference.</remarks>
    public bool Fail => _fail;

    /// <summary>
    /// Whether the 16 and 32 bit members read and write the most significant byte first,
    /// <c>StreamBase::setBigendian()</c>.
    /// </summary>
    /// <remarks>
    /// The flag exists because the save file mixes the two: the container is little endian, a few fields inside it
    /// (the map size, the dates of the world) are stored the way the original game stored them.
    /// </remarks>
    public bool BigEndian => _bigEndian;

    /// <summary>The number of bytes left to read, <c>sizeg()</c>.</summary>
    public abstract int Size { get; }

    /// <summary>The current read position, <c>tell()</c>.</summary>
    public abstract int Tell { get; }

    /// <summary>Raises the failure flag, <c>setFail()</c>.</summary>
    public void SetFail() => _fail = true;

    /// <summary>Selects the byte order of the 16 and 32 bit members, <c>setBigendian( bool )</c>.</summary>
    /// <param name="value">Whether the most significant byte comes first.</param>
    public void SetBigEndian(bool value) => _bigEndian = value;

    /// <summary>Moves the read position, <c>seek()</c>. Each stream clamps it to what it can reach.</summary>
    /// <param name="position">The position counted from the start of the readable data.</param>
    public abstract void Seek(int position);

    /// <summary>Moves the read position forward, <c>skip()</c>.</summary>
    /// <param name="size">How many bytes to pass over; a value of zero or less does nothing.</param>
    public abstract void Skip(int size);

    /// <summary>
    /// Reads exactly <paramref name="size"/> bytes, <c>getRaw()</c>.
    /// </summary>
    /// <remarks>
    /// A <paramref name="size"/> of zero means "everything that is left". The two streams of the reference disagree on
    /// a short read - the memory one zero-pads, the file one fails - so the semantics are documented on each class
    /// rather than shared here.
    /// </remarks>
    /// <param name="size">How many bytes to read, or zero for the rest of the stream.</param>
    /// <returns>The bytes, or an empty array when the stream has nothing left.</returns>
    public abstract byte[] GetRaw(int size);

    /// <summary>Reads one byte, <c>get()</c>.</summary>
    /// <returns>The byte, or zero at the end of the stream (with the failure flag raised).</returns>
    public byte Read() => Get8();

    /// <summary>Reads a boolean, <c>operator&gt;&gt;( StreamBase &amp;, bool &amp; )</c>: one byte, non-zero is true.</summary>
    /// <returns>The value, <see langword="false"/> at the end of the stream.</returns>
    public bool ReadBool() => Get8() > 0;

    /// <summary>Reads a signed byte, <c>operator&gt;&gt;( StreamBase &amp;, int8_t &amp; )</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public sbyte ReadInt8() => unchecked((sbyte)Get8());

    /// <summary>Reads an unsigned byte, <c>operator&gt;&gt;( StreamBase &amp;, uint8_t &amp; )</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public byte ReadUInt8() => Get8();

    /// <summary>Reads a 16 bit integer in the byte order selected by <see cref="BigEndian"/>, <c>get16()</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public ushort ReadUInt16() => _bigEndian ? ReadBe16() : ReadLe16();

    /// <summary>Reads a signed 16 bit integer, <c>operator&gt;&gt;( StreamBase &amp;, int16_t &amp; )</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public short ReadInt16() => unchecked((short)ReadUInt16());

    /// <summary>Reads a 16 bit integer, most significant byte first, <c>getBE16()</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public ushort ReadBe16() => (ushort)((Get8() << 8) | Get8());

    /// <summary>Reads a 16 bit integer, least significant byte first, <c>getLE16()</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public ushort ReadLe16() => (ushort)(Get8() | (Get8() << 8));

    /// <summary>Reads a 32 bit integer in the byte order selected by <see cref="BigEndian"/>, <c>get32()</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public uint ReadUInt32() => _bigEndian ? ReadBe32() : ReadLe32();

    /// <summary>Reads a signed 32 bit integer, <c>operator&gt;&gt;( StreamBase &amp;, int32_t &amp; )</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public int ReadInt32() => unchecked((int)ReadUInt32());

    /// <summary>Reads a 32 bit integer, most significant byte first, <c>getBE32()</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public uint ReadBe32() =>
        ((uint)Get8() << 24) | ((uint)Get8() << 16) | ((uint)Get8() << 8) | Get8();

    /// <summary>Reads a 32 bit integer, least significant byte first, <c>getLE32()</c>.</summary>
    /// <returns>The value, zero at the end of the stream.</returns>
    public uint ReadLe32() =>
        Get8() | ((uint)Get8() << 8) | ((uint)Get8() << 16) | ((uint)Get8() << 24);

    /// <summary>
    /// Reads a length prefixed string, <c>getString()</c> / <c>operator&gt;&gt;( StreamBase &amp;, std::string &amp; )</c>.
    /// </summary>
    /// <remarks>
    /// The reference hands out the bytes it read and lets the caller cut them at the first zero, because a
    /// <c>std::string</c> may hold them. The port cuts here: the game never stores a zero inside a name, and the
    /// streams that keep the bytes around (the read-only buffer) would otherwise copy them for nothing.
    /// </remarks>
    /// <param name="size">How many bytes to read, or zero for the rest of the stream.</param>
    /// <returns>The string, empty when the stream has nothing left.</returns>
    public virtual string ReadString(int size = 0)
    {
        if (size <= 0)
        {
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

            return Encoding.Latin1.GetString(GetRaw(length));
        }

        byte[] raw = GetRaw(size);
        int end = Array.IndexOf(raw, (byte)0);

        return end < 0
            ? Encoding.Latin1.GetString(raw)
            : Encoding.Latin1.GetString(raw, 0, end);
    }

    /// <summary>Writes a boolean as one byte, <c>operator&lt;&lt;( StreamBase &amp;, bool )</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(bool value) => Put8(value ? (byte)1 : (byte)0);

    /// <summary>Writes a signed byte, <c>operator&lt;&lt;( StreamBase &amp;, int8_t )</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(sbyte value) => Put8(unchecked((byte)value));

    /// <summary>Writes an unsigned byte, <c>operator&lt;&lt;( StreamBase &amp;, uint8_t )</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(byte value) => Put8(value);

    /// <summary>Writes a 16 bit integer in the byte order selected by <see cref="BigEndian"/>, <c>put16()</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(ushort value)
    {
        if (_bigEndian)
        {
            WriteBe16(value);
        }
        else
        {
            WriteLe16(value);
        }
    }

    /// <summary>Writes a signed 16 bit integer, <c>operator&lt;&lt;( StreamBase &amp;, int16_t )</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(short value) => Write(unchecked((ushort)value));

    /// <summary>Writes a 16 bit integer, most significant byte first, <c>putBE16()</c>.</summary>
    /// <param name="value">The value.</param>
    public void WriteBe16(ushort value)
    {
        Put8((byte)(value >> 8));
        Put8((byte)value);
    }

    /// <summary>Writes a 16 bit integer, least significant byte first, <c>putLE16()</c>.</summary>
    /// <param name="value">The value.</param>
    public void WriteLe16(ushort value)
    {
        Put8((byte)value);
        Put8((byte)(value >> 8));
    }

    /// <summary>Writes a 32 bit integer in the byte order selected by <see cref="BigEndian"/>, <c>put32()</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(uint value)
    {
        if (_bigEndian)
        {
            WriteBe32(value);
        }
        else
        {
            WriteLe32(value);
        }
    }

    /// <summary>Writes a signed 32 bit integer, <c>operator&lt;&lt;( StreamBase &amp;, int32_t )</c>.</summary>
    /// <param name="value">The value.</param>
    public void Write(int value) => Write(unchecked((uint)value));

    /// <summary>Writes a 32 bit integer, most significant byte first, <c>putBE32()</c>.</summary>
    /// <param name="value">The value.</param>
    public void WriteBe32(uint value)
    {
        Put8((byte)(value >> 24));
        Put8((byte)(value >> 16));
        Put8((byte)(value >> 8));
        Put8((byte)value);
    }

    /// <summary>Writes a 32 bit integer, least significant byte first, <c>putLE32()</c>.</summary>
    /// <param name="value">The value.</param>
    public void WriteLe32(uint value)
    {
        Put8((byte)value);
        Put8((byte)(value >> 8));
        Put8((byte)(value >> 16));
        Put8((byte)(value >> 24));
    }

    /// <summary>
    /// Writes a length prefixed string, <c>putString()</c> / <c>operator&lt;&lt;( StreamBase &amp;, std::string_view )</c>.
    /// </summary>
    /// <remarks>
    /// Four bytes of length follow the characters, and no terminating zero: an empty or missing string is four zero
    /// bytes, which is what the reference writes for a <c>std::string</c> of length zero.
    /// </remarks>
    /// <param name="value">The string, <see langword="null"/> being written as empty.</param>
    public void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Write(0);
            return;
        }

        byte[] raw = Encoding.Latin1.GetBytes(value);

        Write(raw.Length);
        PutRaw(raw);
    }

    /// <summary>
    /// Writes a collection, <c>operator&lt;&lt;( StreamBase &amp;, const std::vector&lt;T&gt; &amp; )</c> and friends.
    /// </summary>
    /// <remarks>
    /// The reference writes the size as a 32 bit integer and then every element through its own operator; the port
    /// asks for the element writer instead of leaning on an overload that C# cannot express for every container.
    /// </remarks>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="items">The elements to write, in the order the reader has to see them.</param>
    /// <param name="writeItem">Writes one element.</param>
    public void WriteList<T>(IReadOnlyCollection<T> items, Action<StreamBase, T> writeItem)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(writeItem);

        Write(items.Count);

        foreach (T item in items)
        {
            writeItem(this, item);
        }
    }

    /// <summary>
    /// Reads a collection, <c>operator&gt;&gt;( StreamBase &amp;, std::vector&lt;T&gt; &amp; )</c> and friends.
    /// </summary>
    /// <remarks>
    /// A negative size is the only value the port refuses: it raises the failure flag and returns nothing, because the
    /// reference would try to allocate for it. Everything else is trusted to the element reader, and a truncated
    /// element fails the stream like any other short read.
    /// </remarks>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="readItem">Reads one element.</param>
    /// <returns>The elements, empty when the stream is at its end or the size is negative.</returns>
    public List<T> ReadList<T>(Func<StreamBase, T> readItem)
    {
        ArgumentNullException.ThrowIfNull(readItem);

        int count = ReadInt32();
        if (count < 0)
        {
            SetFail();
            return [];
        }

        List<T> result = new(count);
        for (int i = 0; i < count; ++i)
        {
            result.Add(readItem(this));
        }

        return result;
    }

    /// <summary>Reads one byte from the concrete stream, <c>get8()</c>.</summary>
    /// <returns>The byte, or zero at the end of the stream.</returns>
    protected abstract byte Get8();

    /// <summary>Writes one byte to the concrete stream, <c>put8()</c>.</summary>
    /// <param name="value">The byte.</param>
    protected abstract void Put8(byte value);

    /// <summary>Writes a run of bytes, <c>putRaw()</c>.</summary>
    /// <param name="data">The bytes to append.</param>
    public abstract void PutRaw(ReadOnlySpan<byte> data);

    /// <summary>Raises the failure flag from a derived stream.</summary>
    /// <param name="value">The new state of <see cref="Fail"/>.</param>
    protected void SetFail(bool value) => _fail = value;
}
