using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Core.Io;

/// <summary>
/// A stream over a file, <c>fheroes2::StreamFile</c> (<c>src/engine/serialize.h</c>,
/// <c>src/engine/serialize.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference wraps a <c>FILE *</c> with the <c>"rb"</c> / <c>"wb"</c> modes of C. The port keeps the methods and
/// hands the choice of the mode to <see cref="FileMode"/> of the framework, with the two helpers
/// <see cref="OpenForReading"/> and <see cref="OpenForWriting"/> naming the two calls the game really makes.
/// </para>
/// <para>
/// <b>Short reads fail here.</b> Unlike <see cref="StreamBuf"/>, which zero pads because an optional field may be
/// missing, a save file that ends in the middle of a required field is broken: the reference marks the stream failed
/// and returns nothing, and so does the port.
/// </para>
/// </remarks>
public sealed class StreamFile : StreamBase, IDisposable
{
    private FileStream? _file;

    /// <summary>Whether a file is open, <c>isValid()</c>.</summary>
    public bool IsValid => _file is not null;

    /// <summary>The number of bytes left to read, <c>sizeg()</c>.</summary>
    public override int Size => _file is null ? 0 : (int)Math.Max(_file.Length - _file.Position, 0);

    /// <summary>The current read position, <c>tell()</c>.</summary>
    public override int Tell => (int)(_file?.Position ?? 0);

    /// <summary>Opens a file, <c>open()</c>.</summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="mode">How the file is opened; the game uses <see cref="FileMode.Open"/> and <see cref="FileMode.Create"/>.</param>
    /// <returns>Whether the file is open.</returns>
    public bool Open(string path, FileMode mode)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        Close();

        try
        {
            FileAccess access = mode == FileMode.Open ? FileAccess.Read : FileAccess.ReadWrite;
            _file = new FileStream(path, mode, access);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            GameLog.Error($"Cannot open the save file '{path}': {ex.Message}");

            SetFail();

            return false;
        }
    }

    /// <summary>Opens an existing file for reading, <c>open( path, "rb" )</c>.</summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>Whether the file is open.</returns>
    public bool OpenForReading(string path) => Open(path, FileMode.Open);

    /// <summary>Creates or truncates a file for writing, <c>open( path, "wb" )</c>.</summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>Whether the file is open.</returns>
    public bool OpenForWriting(string path) => Open(path, FileMode.Create);

    /// <summary>Closes the file, <c>close()</c>.</summary>
    public void Close()
    {
        _file?.Dispose();
        _file = null;
    }

    /// <summary>Closes the file.</summary>
    public void Dispose() => Close();

    /// <summary>Moves the read position, <c>seek()</c>.</summary>
    /// <param name="position">The position counted from the start of the file.</param>
    public override void Seek(int position)
    {
        if (_file is null)
        {
            SetFail();
            return;
        }

        _file.Position = Math.Max(position, 0);
    }

    /// <summary>Moves the read position forward, <c>skip()</c>.</summary>
    /// <param name="size">How many bytes to pass over; a value of zero or less does nothing.</param>
    public override void Skip(int size)
    {
        if (size <= 0)
        {
            return;
        }

        Seek(Tell + size);
    }

    /// <summary>Reads exactly <paramref name="size"/> bytes, <c>getRaw()</c>.</summary>
    /// <param name="size">How many bytes to read, or zero for the rest of the file.</param>
    /// <returns>The bytes, empty when the file is shorter than asked for (and then the stream fails).</returns>
    public override byte[] GetRaw(int size)
    {
        if (_file is null)
        {
            SetFail();
            return [];
        }

        int chunkSize = size > 0 ? size : Size;
        if (chunkSize <= 0)
        {
            return [];
        }

        byte[] result = new byte[chunkSize];
        int read = _file.ReadAtLeast(result, chunkSize, throwOnEndOfStream: false);

        if (read != chunkSize)
        {
            SetFail();

            return [];
        }

        return result;
    }

    /// <summary>Reads one byte, <c>get8()</c>.</summary>
    /// <returns>The byte, or zero at the end of the file.</returns>
    protected override byte Get8()
    {
        if (_file is null)
        {
            SetFail();

            return 0;
        }

        int value = _file.ReadByte();
        if (value < 0)
        {
            SetFail();

            return 0;
        }

        return (byte)value;
    }

    /// <summary>Appends one byte, <c>put8()</c>.</summary>
    /// <param name="value">The byte.</param>
    protected override void Put8(byte value)
    {
        if (_file is null)
        {
            SetFail();
            return;
        }

        try
        {
            _file.WriteByte(value);
        }
        catch (IOException ex)
        {
            GameLog.Error($"Cannot write to the save file: {ex.Message}");

            SetFail();
        }
    }

    /// <summary>Appends bytes, <c>putRaw()</c>.</summary>
    /// <param name="data">The bytes to append.</param>
    public override void PutRaw(ReadOnlySpan<byte> data)
    {
        if (_file is null)
        {
            SetFail();
            return;
        }

        try
        {
            _file.Write(data);
        }
        catch (IOException ex)
        {
            GameLog.Error($"Cannot write to the save file: {ex.Message}");

            SetFail();
        }
    }
}
