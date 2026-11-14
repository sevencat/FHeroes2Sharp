using System.Buffers.Binary;
using FHeroes2.Assets.Audio;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers the WAV container of the sound effects (<c>LoadWAV</c> in <c>audio_manager.cpp</c>) and
/// the archive naming of <see cref="GameSound"/>. The integration test returns early when the game
/// data is unavailable (see docs/MISC.md).
/// </summary>
public sealed class WaveSoundTests
{
    [Fact]
    public void FromGameSound_WritesTheReferenceRiffHeader()
    {
        byte[] body = [0x80, 0x81, 0x82, 0x83];

        byte[] wave = WaveSound.FromGameSound(body);

        Assert.Equal(WaveSound.HeaderSize + body.Length, wave.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
        Assert.Equal((uint)(body.Length + 0x24), BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(4, 4)));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wave, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(wave, 12, 4));

        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(20, 2))); // PCM
        Assert.Equal((ushort)WaveSound.Channels, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(22, 2)));
        Assert.Equal((uint)WaveSound.SampleRate, BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(24, 4)));
        Assert.Equal((uint)WaveSound.SampleRate, BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(28, 4)));
        Assert.Equal((ushort)WaveSound.BitsPerSample, BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(34, 2)));

        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(wave, 36, 4));
        Assert.Equal((uint)body.Length, BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(40, 4)));
        Assert.Equal(body, wave[WaveSound.HeaderSize..]);
    }

    [Fact]
    public void FromGameSound_RejectsAnEmptyBody()
    {
        Assert.Throws<ArgumentException>(() => WaveSound.FromGameSound([]));
    }

    [Theory]
    [InlineData("pickup01", "PICKUP01.82M")]
    [InlineData("KILLFADE", "KILLFADE.82M")]
    [InlineData("AELM_WNCE", "AELM_WNCE.82M")]
    public void GetArchiveName_MatchesTheArchiveEntries(string soundName, string expected)
    {
        Assert.Equal(expected, GameSound.GetArchiveName(soundName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PICK UP")]
    [InlineData("PICKUP01.82M")]
    public void GetArchiveName_RejectsInvalidNames(string soundName)
    {
        Assert.ThrowsAny<ArgumentException>(() => GameSound.GetArchiveName(soundName));
    }

    [Fact]
    public void TryReadBody_ReturnsPlayablePcmForAKnownSoundEffect()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);

        byte[]? body = GameSound.TryReadBody(bundle, "PICKUP01");
        if (body is null)
        {
            // A stripped demo may not ship every sound effect; the naming test above still holds.
            return;
        }

        Assert.NotEmpty(body);

        byte[] wave = WaveSound.FromGameSound(body);

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
        Assert.Equal(body.Length + WaveSound.HeaderSize, wave.Length);

        // 8 bit mono at 22050 Hz: one byte per frame, so the duration follows from the body size.
        double seconds = body.Length / (double)WaveSound.SampleRate;
        Assert.InRange(seconds, 0.05, 10.0);
    }

    [Fact]
    public void TryReadBody_ReturnsNullForAnUnknownSoundEffect()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);

        Assert.Null(GameSound.TryReadBody(bundle, "NOTASOUND"));
    }
}
