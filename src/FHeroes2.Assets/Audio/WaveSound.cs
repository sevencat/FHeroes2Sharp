using System.Buffers.Binary;

namespace FHeroes2.Assets.Audio;

/// <summary>
/// Builds the WAV container of a sound effect of the original game.
///
/// <para>
/// The archives store the sound effects as headerless PCM bodies (<c>*.82M</c> entries): 8 bit
/// unsigned mono at 22050 Hz. The reference implementation wraps them in a 44 byte RIFF/WAVE header
/// before handing them to the audio device (<c>LoadWAV</c> in <c>audio_manager.cpp</c>), and so does
/// this type; the byte layout below is a literal port of that function.
/// </para>
/// </summary>
public static class WaveSound
{
    /// <summary>Sample rate of every sound effect of the original game.</summary>
    public const int SampleRate = 22050;

    /// <summary>Channels of every sound effect of the original game.</summary>
    public const int Channels = 1;

    /// <summary>Bits per sample of every sound effect of the original game.</summary>
    public const int BitsPerSample = 8;

    /// <summary>Size of the RIFF header that precedes the PCM body.</summary>
    public const int HeaderSize = 44;

    /// <summary>
    /// Wraps a headerless PCM body read from the archives into a complete WAV file, ready for
    /// in-memory playback.
    /// </summary>
    /// <param name="body">Raw sound body, as stored in the <c>*.82M</c> archive entry.</param>
    /// <returns>The RIFF/WAVE file: <see cref="HeaderSize"/> bytes of header followed by the body.</returns>
    /// <exception cref="ArgumentException">The body is empty.</exception>
    public static byte[] FromGameSound(ReadOnlySpan<byte> body)
    {
        if (body.IsEmpty)
        {
            throw new ArgumentException("A sound effect body must not be empty.", nameof(body));
        }

        byte[] wave = new byte[HeaderSize + body.Length];
        Span<byte> header = wave.AsSpan(0, HeaderSize);

        // "RIFF", the file size minus the eight bytes of these two fields, "WAVE".
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], 0x46464952);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), (uint)body.Length + 0x24);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), 0x45564157);

        // "fmt " sub chunk: PCM, 8 bit, mono, 22050 Hz.
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), 0x20746D66);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), 0x10);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(20, 2), 0x01);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(22, 2), Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(24, 4), SampleRate);

        // Byte rate, block align, bits per sample.
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(28, 4), SampleRate * Channels * BitsPerSample / 8);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(32, 2), (ushort)(Channels * BitsPerSample / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(34, 2), BitsPerSample);

        // "data" sub chunk and its size.
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(36, 4), 0x61746164);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(40, 4), (uint)body.Length);

        body.CopyTo(wave.AsSpan(HeaderSize));

        return wave;
    }
}
