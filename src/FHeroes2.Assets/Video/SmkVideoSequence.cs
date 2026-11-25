using System.Buffers.Binary;

namespace FHeroes2.Assets.Video;

/// <summary>
/// A played-back Smacker video: the port of <c>SMKVideoSequence</c> of <c>engine/smk_decoder.cpp</c>. It decodes the
/// file once to collect the soundtrack of every audio track into a WAV buffer and then serves the video frame by
/// frame, doubling the rows when the file asks for it.
/// </summary>
public sealed class SmkVideoSequence : IDisposable
{
    private const int AudioHeaderSize = 44;
    private const int AudioChannelCount = 7;

    private readonly SmkFile? _file;
    private readonly byte[][] _audioChannels;
    private readonly int _width;
    private readonly int _height;

    /// <summary>Opens a video file.</summary>
    /// <param name="filePath">Path of the <c>.smk</c> file.</param>
    /// <param name="enableAudio">Whether the soundtrack is decoded, which the video player does when sound is on.</param>
    public SmkVideoSequence(string filePath, bool enableAudio = true)
        : this(SmkFile.Open(filePath), enableAudio)
    {
    }

    /// <summary>Opens a video held in memory, such as one read from an archive.</summary>
    /// <param name="data">The bytes of the <c>.smk</c> file.</param>
    /// <param name="enableAudio">Whether the soundtrack is decoded, which the video player does when sound is on.</param>
    public SmkVideoSequence(ReadOnlySpan<byte> data, bool enableAudio = true)
        : this(SmkFile.Open(data), enableAudio)
    {
    }

    private SmkVideoSequence(SmkFile? file, bool enableAudio)
    {
        _file = file;

        if (_file is null)
        {
            _audioChannels = [];

            return;
        }

        HeightScaleFactor = Math.Clamp(_file.YScaleMode, 1, 2);
        _width = _file.Width;
        _height = _file.Height * HeightScaleFactor;

        if (_file.MicrosecondsPerFrame < 1)
        {
            MicrosecondsPerFrame = 1000000.0 / 15.0;
        }
        else
        {
            MicrosecondsPerFrame = _file.MicrosecondsPerFrame;
        }

        List<byte[]> channels = [];

        if (enableAudio)
        {
            CollectAudio(channels);
        }

        _audioChannels = [.. channels];

        _file.SetVideoEnabled(true);
        _file.First();
    }

    /// <summary>Width of the frames in pixels.</summary>
    public int Width => _width;

    /// <summary>Height of the frames in pixels, after the row doubling the file asks for.</summary>
    public int Height => _height;

    /// <summary>Number of frames.</summary>
    public int FrameCount => _file?.FrameCount ?? 0;

    /// <summary>Whether the file could be opened.</summary>
    public bool IsValid => _file is not null;

    /// <summary>Duration of one frame in microseconds.</summary>
    public double MicrosecondsPerFrame { get; }

    /// <summary>1, or 2 when every stored row is shown twice.</summary>
    public int HeightScaleFactor { get; }

    /// <summary>The soundtrack, one WAV buffer per audio track the file has.</summary>
    public IReadOnlyList<byte[]> AudioChannels => _audioChannels;

    /// <summary>The palette of the current frame, 768 bytes of 8 bit RGB.</summary>
    public ReadOnlySpan<byte> Palette => _file is null ? [] : _file.Palette;

    /// <summary>Index of the current frame.</summary>
    public int CurrentFrameId => _file?.CurrentFrameId ?? 0;

    /// <summary>Rewinds to the first frame, <c>resetFrame()</c>.</summary>
    public void ResetFrame() => _file?.First();

    /// <summary>Advances one frame, <c>getNextFrame()</c>.</summary>
    /// <returns>Whether a frame was decoded.</returns>
    public bool GetNextFrame() => _file is not null && _file.Next();

    /// <summary>Advances one frame without decoding its audio, used when sound is off.</summary>
    public void SkipFrame() => GetNextFrame();

    /// <summary>
    /// Copies the current frame into an 8 bit image, clipping to its bounds and doubling the rows when the file asks
    /// for it, <c>getCurrentFrame()</c>.
    /// </summary>
    /// <param name="image">The destination image, <paramref name="imageWidth"/> times <paramref name="imageHeight"/> indices.</param>
    /// <param name="imageWidth">Width of the destination image.</param>
    /// <param name="imageHeight">Height of the destination image.</param>
    /// <param name="x">Left edge of the frame in the image.</param>
    /// <param name="y">Top edge of the frame in the image.</param>
    /// <param name="width">Receives the copied width.</param>
    /// <param name="height">Receives the copied height.</param>
    public void GetCurrentFrame(Span<byte> image, int imageWidth, int imageHeight, int x, int y, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (_file is null || imageWidth <= 0 || imageHeight <= 0 || x < 0 || y < 0 || x >= imageWidth || y >= imageHeight ||
            image.Length < imageWidth * imageHeight)
        {
            return;
        }

        ReadOnlySpan<byte> data = _file.FramePixels;

        width = _width;
        height = _height;

        if (x + width > imageWidth)
        {
            width = imageWidth - x;
        }

        if (y + height > imageHeight)
        {
            height = imageHeight - y;
        }

        int sourceWidth = _file.Width;
        int rows = height / HeightScaleFactor;

        for (int row = 0; row < rows; ++row)
        {
            data.Slice(row * sourceWidth, width)
                .CopyTo(image.Slice(x + ((y + (row * HeightScaleFactor)) * imageWidth), width));
        }
    }

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

    private void CollectAudio(List<byte[]> channels)
    {
        SmkFile file = _file!;

        file.SetVideoEnabled(false);

        List<byte[]> buffers = new(AudioChannelCount);

        for (int i = 0; i < AudioChannelCount; ++i)
        {
            buffers.Add([]);
        }

        if (!file.First())
        {
            return;
        }

        for (int frame = 0; frame < file.FrameCount; ++frame)
        {
            if (frame > 0 && !file.Next())
            {
                break;
            }

            for (int track = 0; track < AudioChannelCount; ++track)
            {
                ReadOnlySpan<byte> chunk = file.GetAudio(track);

                if (chunk.IsEmpty)
                {
                    continue;
                }

                if (buffers[track].Length == 0)
                {
                    buffers[track] = new byte[AudioHeaderSize];
                }

                byte[] current = buffers[track];
                int offset = current.Length;
                Array.Resize(ref current, offset + chunk.Length);
                chunk.CopyTo(current.AsSpan(offset));
                buffers[track] = current;
            }
        }

        for (int i = 0; i < AudioChannelCount; ++i)
        {
            if (buffers[i].Length == AudioHeaderSize)
            {
                buffers[i] = [];

                continue;
            }

            SmkAudioTrack track = file.AudioTracks[i];

            if (buffers[i].Length > 0)
            {
                WriteWaveHeader(buffers[i], track);
                channels.Add(buffers[i]);
            }
        }
    }

    private static void WriteWaveHeader(byte[] buffer, SmkAudioTrack track)
    {
        int dataSize = buffer.Length - AudioHeaderSize;
        int channels = Math.Max(1, track.Channels);
        int bitDepth = Math.Max(8, track.BitDepth);
        int rate = Math.Max(1, track.Rate);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), 0x46464952); // "RIFF"
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), dataSize + 0x24);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), 0x45564157); // "WAVE"
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(12), 0x20746D66); // "fmt "
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16), 0x10);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(20), 0x01);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28), rate * bitDepth * channels / 8);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(32), (short)(bitDepth * channels / 8));
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(34), (short)bitDepth);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(36), 0x61746164); // "data"
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(40), dataSize);
    }
}
