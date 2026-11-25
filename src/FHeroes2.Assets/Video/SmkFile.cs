using System.Buffers.Binary;
using System.Runtime.InteropServices;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Video;

/// <summary>One audio track of a Smacker file, the values libsmacker keeps in <c>smk_audio_t</c>.</summary>
/// <param name="Exists">Whether the file has this track.</param>
/// <param name="Channels">1 for mono, 2 for stereo.</param>
/// <param name="BitDepth">8 or 16 bits per sample.</param>
/// <param name="Rate">Samples per second.</param>
/// <param name="Compressed">Whether the samples are DPCM compressed.</param>
/// <param name="Bink">Whether the track uses the unsupported Bink audio codec.</param>
/// <param name="MaxBufferSize">Largest decoded chunk this track ever produces.</param>
public readonly record struct SmkAudioTrack(
    bool Exists,
    int Channels,
    int BitDepth,
    int Rate,
    bool Compressed,
    bool Bink,
    int MaxBufferSize);

/// <summary>
/// A Smacker video file: the C# port of the parts of <c>thirdparty/libsmacker/smacker.c</c> the engine uses, namely the
/// header, the Huffman trees, the palette, the video and audio chunks, and the frame advance.
/// </summary>
/// <remarks>
/// <para>
/// The whole file is read into memory once (libsmacker's <c>SMK_MODE_MEMORY</c>) and the frame chunks are kept as
/// offsets into that buffer. Decoding a frame patches the retained frame buffer in place, because the void blocks of a
/// Smacker frame mean "keep the pixels that were there": the previous frame is the base of the next one.
/// </para>
/// <para>
/// The palette is expanded the way libsmacker's <c>palmap</c> does it, so <see cref="Palette"/> already holds 8 bits per
/// component.
/// </para>
/// </remarks>
public sealed partial class SmkFile
{
    internal const int AudioTrackCount = 7;
    internal const int PaletteSizeBytes = 256 * 3;
    internal const int TreeMmap = 0;
    internal const int TreeMclr = 1;
    internal const int TreeFull = 2;
    internal const int TreeType = 3;

    private const int MinimumFileSize = 56;

    /// <summary>Expands the 6 bit colour components of Smacker to 8 bits, libsmacker's <c>palmap</c>.</summary>
    internal static readonly byte[] PaletteMap =
    [
        0x00, 0x04, 0x08, 0x0C, 0x10, 0x14, 0x18, 0x1C,
        0x20, 0x24, 0x28, 0x2C, 0x30, 0x34, 0x38, 0x3C,
        0x41, 0x45, 0x49, 0x4D, 0x51, 0x55, 0x59, 0x5D,
        0x61, 0x65, 0x69, 0x6D, 0x71, 0x75, 0x79, 0x7D,
        0x82, 0x86, 0x8A, 0x8E, 0x92, 0x96, 0x9A, 0x9E,
        0xA2, 0xA6, 0xAA, 0xAE, 0xB2, 0xB6, 0xBA, 0xBE,
        0xC3, 0xC7, 0xCB, 0xCF, 0xD3, 0xD7, 0xDB, 0xDF,
        0xE3, 0xE7, 0xEB, 0xEF, 0xF3, 0xF7, 0xFB, 0xFF,
    ];

    /// <summary>Row/column count of the block shapes of a video chunk, <c>sizetable</c>.</summary>
    internal static readonly int[] BlockSizes =
    [
        1, 2, 3, 4, 5, 6, 7, 8,
        9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24,
        25, 26, 27, 28, 29, 30, 31, 32,
        33, 34, 35, 36, 37, 38, 39, 40,
        41, 42, 43, 44, 45, 46, 47, 48,
        49, 50, 51, 52, 53, 54, 55, 56,
        57, 58, 59, 128, 256, 512, 1024, 2048,
    ];

    private readonly byte[] _data;
    private readonly int[] _chunkOffset;
    private readonly int[] _chunkSize;
    private readonly byte[] _frameType;
    private readonly bool[] _keyFrame;
    private readonly SmkHuff16[] _trees = [new(), new(), new(), new()];
    private readonly byte[][] _audioBuffers = new byte[AudioTrackCount][];
    private readonly int[] _audioSizes = new int[AudioTrackCount];
    private readonly bool[] _audioEnabled = new bool[AudioTrackCount];
    private readonly SmkAudioTrack[] _tracks = new SmkAudioTrack[AudioTrackCount];
    private readonly byte[] _frame;
    private readonly byte[] _palette = new byte[PaletteSizeBytes];

    private bool _videoEnabled = true;
    private int _currentFrame;

    private SmkFile(byte[] data)
    {
        _data = data;

        if (data.Length < MinimumFileSize)
        {
            throw new BinaryFormatException(
                $"A Smacker file needs at least {MinimumFileSize} bytes, but only {data.Length} were provided.");
        }

        if (data[0] != (byte)'S' || data[1] != (byte)'M' || data[2] != (byte)'K')
        {
            throw new BinaryFormatException("The file does not start with the SMK signature.");
        }

        byte version = data[3];
        Version = (char)(version == (byte)'4' ? '4' : '2');

        int cursor = 4;
        int width = ReadInt32(data, ref cursor);
        int height = ReadInt32(data, ref cursor);

        if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
        {
            throw new BinaryFormatException($"The Smacker file declares a frame of {width}x{height}.");
        }

        Width = width;
        Height = height;

        int frames = ReadInt32(data, ref cursor);

        if (frames < 0)
        {
            throw new BinaryFormatException("The Smacker file declares a negative number of frames.");
        }

        FrameCount = frames;

        int frameTime = ReadInt32(data, ref cursor);

        // A positive value is milliseconds, a negative one is units of ten microseconds, zero means ten frames a second.
        MicrosecondsPerFrame = frameTime > 0
            ? frameTime * 1000.0
            : frameTime < 0
                ? frameTime * -10.0
                : 100000.0;

        int flags = ReadInt32(data, ref cursor);
        HasRingFrame = (flags & 0x01) != 0;
        YScaleMode = (flags & 0x02) != 0 ? 1 : (flags & 0x04) != 0 ? 2 : 0;

        int[] maxBuffer = new int[AudioTrackCount];

        for (int i = 0; i < AudioTrackCount; ++i)
        {
            maxBuffer[i] = ReadInt32(data, ref cursor);
        }

        int treeChunkSize = ReadInt32(data, ref cursor);

        if (treeChunkSize < 0)
        {
            throw new BinaryFormatException("The Smacker file declares a negative Huffman tree chunk.");
        }

        int[] treeSizes = new int[4];

        for (int i = 0; i < 4; ++i)
        {
            treeSizes[i] = ReadInt32(data, ref cursor);
        }

        for (int i = 0; i < AudioTrackCount; ++i)
        {
            int value = ReadInt32(data, ref cursor);

            if ((value & 0x40000000) == 0)
            {
                _tracks[i] = new SmkAudioTrack(false, 0, 0, 0, false, false, 0);

                continue;
            }

            _tracks[i] = new SmkAudioTrack(
                true,
                (value & 0x10000000) != 0 ? 2 : 1,
                (value & 0x20000000) != 0 ? 16 : 8,
                value & 0x00FFFFFF,
                (value & unchecked((int)0x80000000)) != 0,
                (value & 0x0C000000) != 0,
                Math.Max(0, maxBuffer[i]));

            _audioBuffers[i] = new byte[Math.Max(1, maxBuffer[i])];
            _audioEnabled[i] = true;
        }

        // One unused field, then the per-frame chunk sizes with the key frame markers.
        _ = ReadInt32(data, ref cursor);

        int entries = FrameCount + (HasRingFrame ? 1 : 0);

        _chunkOffset = new int[entries];
        _chunkSize = new int[entries];
        _frameType = new byte[entries];
        _keyFrame = new bool[entries];

        for (int i = 0; i < entries; ++i)
        {
            int value = ReadInt32(data, ref cursor);

            _keyFrame[i] = (value & 0x01) != 0;
            _chunkSize[i] = value & unchecked((int)0xFFFFFFFC);
        }

        for (int i = 0; i < entries; ++i)
        {
            if (cursor >= data.Length)
            {
                throw new BinaryFormatException("The frame type table of the Smacker file is truncated.");
            }

            _frameType[i] = data[cursor++];
        }

        if (treeChunkSize > data.Length - cursor)
        {
            throw new BinaryFormatException("The Huffman tree chunk of the Smacker file runs past the end of the file.");
        }

        SmkBitReader treeReader = new(data.AsSpan(cursor, treeChunkSize));

        for (int i = 0; i < 4; ++i)
        {
            if (!_trees[i].Build(ref treeReader, treeSizes[i]))
            {
                throw new BinaryFormatException($"The Smacker file has a corrupted Huffman tree {i}.");
            }
        }

        cursor += treeChunkSize;

        for (int i = 0; i < entries; ++i)
        {
            _chunkOffset[i] = cursor;
            cursor += _chunkSize[i];

            if (cursor > data.Length)
            {
                throw new BinaryFormatException($"Frame {i} of the Smacker file runs past the end of the file.");
            }
        }

        _frame = new byte[Width * Height];
    }

    /// <summary>Width of the frame in pixels.</summary>
    public int Width { get; }

    /// <summary>Height of the frame in pixels, as stored; see <see cref="YScaleMode"/>.</summary>
    public int Height { get; }

    /// <summary>Number of frames, without the optional ring frame.</summary>
    public int FrameCount { get; }

    /// <summary>Duration of one frame in microseconds.</summary>
    public double MicrosecondsPerFrame { get; }

    /// <summary>Whether the file ends with a ring frame that loops back to the second frame.</summary>
    public bool HasRingFrame { get; }

    /// <summary>SMK version, <c>'2'</c> or <c>'4'</c>.</summary>
    public char Version { get; }

    /// <summary>How the stored rows are displayed: 0 unscaled, 1 doubles every row, 2 interlaced.</summary>
    public int YScaleMode { get; }

    /// <summary>The audio tracks of the file; tracks the file does not have report <see cref="SmkAudioTrack.Exists"/> unset.</summary>
    public IReadOnlyList<SmkAudioTrack> AudioTracks => _tracks;

    /// <summary>Index of the frame the decoder currently holds.</summary>
    public int CurrentFrameId => _currentFrame;

    /// <summary>The retained frame buffer, <see cref="Width"/> times <see cref="Height"/> palette indices.</summary>
    public ReadOnlySpan<byte> FramePixels => _frame.AsSpan(0, Width * Height);

    /// <summary>The palette of the current frame, 768 bytes of 8 bit RGB.</summary>
    public ReadOnlySpan<byte> Palette => _palette;

    /// <summary>The audio chunk of the current frame of a track, or an empty span when there is none.</summary>
    /// <param name="track">Track index, 0 to 6.</param>
    public ReadOnlySpan<byte> GetAudio(int track)
    {
        if ((uint)track >= AudioTrackCount || !_tracks[track].Exists)
        {
            return [];
        }

        int size = Math.Min(_audioSizes[track], _audioBuffers[track].Length);

        return size <= 0 ? [] : _audioBuffers[track].AsSpan(0, size);
    }

    /// <summary>Turns video decoding on or off, <c>smk_enable_video()</c>.</summary>
    /// <param name="enabled">Whether frames are decoded.</param>
    public void SetVideoEnabled(bool enabled) => _videoEnabled = enabled;

    /// <summary>Turns decoding of one audio track on or off, <c>smk_enable_audio()</c>.</summary>
    /// <param name="track">Track index, 0 to 6.</param>
    /// <param name="enabled">Whether the track is decoded.</param>
    public void SetAudioEnabled(int track, bool enabled)
    {
        if ((uint)track < AudioTrackCount && _tracks[track].Exists)
        {
            _audioEnabled[track] = enabled;
        }
    }

    /// <summary>Rewinds to the first frame and decodes it, <c>smk_first()</c>.</summary>
    /// <returns>Whether the frame could be decoded.</returns>
    public bool First()
    {
        _currentFrame = 0;

        return RenderFrame();
    }

    /// <summary>Advances one frame and decodes it, <c>smk_next()</c>.</summary>
    /// <returns>Whether a frame was decoded; <see langword="false"/> at the end of a file without a ring frame.</returns>
    public bool Next()
    {
        if (_currentFrame + 1 < _chunkSize.Length)
        {
            ++_currentFrame;

            return RenderFrame();
        }

        if (!HasRingFrame)
        {
            return false;
        }

        // The ring frame loops back to the second frame.
        _currentFrame = 1;

        return RenderFrame();
    }

    /// <summary>Opens a Smacker file from disk.</summary>
    /// <param name="filePath">Path of the file.</param>
    /// <returns>The opened file, or <see langword="null"/> when the file does not exist.</returns>
    /// <exception cref="BinaryFormatException">The file is not a valid Smacker file.</exception>
    public static SmkFile? Open(string filePath) => File.Exists(filePath) ? new SmkFile(File.ReadAllBytes(filePath)) : null;

    /// <summary>Opens a Smacker file held in memory.</summary>
    /// <param name="data">The bytes of the file.</param>
    /// <exception cref="BinaryFormatException">The data is not a valid Smacker file.</exception>
    public static SmkFile Open(ReadOnlySpan<byte> data) => new(data.ToArray());

    private static int ReadInt32(byte[] data, ref int cursor)
    {
        if (cursor + 4 > data.Length)
        {
            throw new BinaryFormatException("The header of the Smacker file is truncated.");
        }

        int value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(cursor));
        cursor += 4;

        return value;
    }

    private bool RenderFrame()
    {
        int remaining = _chunkSize[_currentFrame];

        if (remaining == 0)
        {
            return false;
        }

        int position = _chunkOffset[_currentFrame];
        byte type = _frameType[_currentFrame];

        // The palette record comes first; its first byte counts 4 byte units.
        if ((type & 0x01) != 0)
        {
            if (remaining < 1)
            {
                return false;
            }

            int paletteSize = 4 * _data[position];

            if (paletteSize < 1 || paletteSize > remaining)
            {
                return false;
            }

            if (_videoEnabled)
            {
                RenderPalette(_data.AsSpan(position + 1, paletteSize - 1));
            }

            position += paletteSize;
            remaining -= paletteSize;
        }

        for (int track = 0; track < AudioTrackCount; ++track)
        {
            if ((type & (0x02 << track)) == 0)
            {
                _audioSizes[track] = 0;

                continue;
            }

            if (remaining < 4)
            {
                return false;
            }

            int audioSize = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(position));

            if (audioSize < 4 || audioSize > remaining)
            {
                return false;
            }

            if (_audioEnabled[track])
            {
                RenderAudio(track, _data.AsSpan(position + 4, audioSize - 4));
            }

            position += audioSize;
            remaining -= audioSize;
        }

        return !_videoEnabled || remaining == 0 || RenderVideo(_data.AsSpan(position, remaining));
    }

    private void RenderAudio(int track, ReadOnlySpan<byte> data)
    {
        SmkAudioTrack info = _tracks[track];
        byte[] buffer = _audioBuffers[track];

        if (!info.Compressed)
        {
            int length = Math.Min(data.Length, buffer.Length);
            data[..length].CopyTo(buffer);
            _audioSizes[track] = length;

            return;
        }

        _audioSizes[track] = 0;

        if (data.Length < 4)
        {
            return;
        }

        int outputSize = BinaryPrimitives.ReadInt32LittleEndian(data);

        if (outputSize <= 0 || outputSize > buffer.Length)
        {
            return;
        }

        SmkBitReader reader = new(data[4..]);

        if (reader.ReadBit() != 1)
        {
            return;
        }

        _ = reader.ReadBit();
        _ = reader.ReadBit();

        SmkHuff8[] trees = [new(), new(), new(), new()];

        if (!trees[0].Build(ref reader))
        {
            return;
        }

        if (info.BitDepth == 16 && !trees[1].Build(ref reader))
        {
            return;
        }

        if (info.Channels == 2)
        {
            if (!trees[2].Build(ref reader))
            {
                return;
            }

            if (info.BitDepth == 16 && !trees[3].Build(ref reader))
            {
                return;
            }
        }

        Span<short> samples16 = MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, buffer.Length & ~1));

        // The first sample, or the first pair of a stereo track, is stored uncompressed.
        if (info.Channels == 2)
        {
            int high = reader.ReadByte();

            if (high < 0)
            {
                return;
            }

            if (info.BitDepth == 16)
            {
                int low = reader.ReadByte();

                if (low < 0)
                {
                    return;
                }

                samples16[1] = (short)((high << 8) | low);
            }
            else
            {
                buffer[1] = (byte)high;
            }
        }

        int first = reader.ReadByte();

        if (first < 0)
        {
            return;
        }

        if (info.BitDepth == 16)
        {
            int low = reader.ReadByte();

            if (low < 0)
            {
                return;
            }

            samples16[0] = (short)((first << 8) | low);
        }
        else
        {
            buffer[0] = (byte)first;
        }

        int index = 1;
        int written = 1;
        int limit = Math.Min(outputSize, buffer.Length);

        while (written < limit)
        {
            if (info.BitDepth == 8)
            {
                int value = trees[0].Lookup(ref reader);

                if (value < 0)
                {
                    return;
                }

                buffer[index] = unchecked((byte)(buffer[index - info.Channels] + value));
                ++index;
                ++written;
            }
            else
            {
                if (index >= samples16.Length)
                {
                    break;
                }

                int low = trees[0].Lookup(ref reader);
                int high = trees[1].Lookup(ref reader);

                if (low < 0 || high < 0)
                {
                    return;
                }

                samples16[index] = unchecked((short)(samples16[index - info.Channels] + (low | (high << 8))));
                ++index;
                written += 2;
            }

            if (info.Channels != 2)
            {
                continue;
            }

            if (info.BitDepth == 8)
            {
                if (index >= buffer.Length)
                {
                    break;
                }

                int value = trees[2].Lookup(ref reader);

                if (value < 0)
                {
                    return;
                }

                buffer[index] = unchecked((byte)(buffer[index - 2] + value));
                ++index;
                ++written;
            }
            else
            {
                if (index >= samples16.Length)
                {
                    break;
                }

                int low = trees[2].Lookup(ref reader);
                int high = trees[3].Lookup(ref reader);

                if (low < 0 || high < 0)
                {
                    return;
                }

                samples16[index] = unchecked((short)(samples16[index - 2] + (low | (high << 8))));
                ++index;
                written += 2;
            }
        }

        _audioSizes[track] = Math.Min(outputSize, buffer.Length);
    }

    private bool DecodeMapBlock(Span<byte> frame, ref SmkBitReader reader, int skip, out bool failed)
    {
        failed = false;

        int colours = _trees[TreeMclr].Lookup(ref reader);

        if (colours < 0)
        {
            failed = true;

            return false;
        }

        byte first = (byte)((colours & 0xFF00) >> 8);
        byte second = (byte)(colours & 0x00FF);

        int map = _trees[TreeMmap].Lookup(ref reader);

        if (map < 0)
        {
            failed = true;

            return false;
        }

        int mask = 0x01;

        for (int row = 0; row < 4; ++row)
        {
            for (int column = 0; column < 4; ++column)
            {
                frame[skip + column] = (map & mask) != 0 ? first : second;
                mask <<= 1;
            }

            skip += Width;
        }

        return true;
    }
}
