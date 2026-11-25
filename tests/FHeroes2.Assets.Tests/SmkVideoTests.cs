using System.Buffers.Binary;
using FHeroes2.Assets.Video;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// The Smacker decoder against the real videos of the game. The tests return early when the game data is not
/// installed, like the other asset tests.
/// </summary>
public sealed class SmkVideoTests
{
    private const string VideoRelativePath = @"heroes2\Anim\Choose.smk";

    [Fact]
    public void TheHeaderOfARealVideo_IsParsed()
    {
        string? path = FindVideo();
        if (path is null)
        {
            return;
        }

        SmkFile file = SmkFile.Open(path)!;

        Assert.Equal('2', file.Version);
        Assert.True(file.Width > 0);
        Assert.True(file.Height > 0);
        Assert.True(file.FrameCount > 0);
        Assert.True(file.MicrosecondsPerFrame > 0);
        Assert.NotEmpty(file.AudioTracks);
        Assert.Contains(file.AudioTracks, track => track.Exists);
    }

    [Fact]
    public void TheFirstFrame_IsDecoded()
    {
        string? path = FindVideo();
        if (path is null)
        {
            return;
        }

        SmkFile file = SmkFile.Open(path)!;

        Assert.True(file.First());
        Assert.Equal(0, file.CurrentFrameId);

        // A decoded frame is not the zeroed buffer, and its palette has real colours.
        Assert.Contains(file.FramePixels.ToArray(), value => value != 0);
        Assert.Contains(file.Palette.ToArray(), value => value != 0);
    }

    [Fact]
    public void EveryFrame_IsDecoded()
    {
        string? path = FindVideo();
        if (path is null)
        {
            return;
        }

        SmkFile file = SmkFile.Open(path)!;

        Assert.True(file.First());

        for (int frame = 1; frame < file.FrameCount; ++frame)
        {
            Assert.True(file.Next(), $"Frame {frame} of {file.FrameCount} did not decode.");
        }

        Assert.Equal(file.FrameCount - 1, file.CurrentFrameId);
    }

    [Fact]
    public void TheSequence_CollectsTheSoundtrack()
    {
        string? path = FindVideo();
        if (path is null)
        {
            return;
        }

        using SmkVideoSequence sequence = new(path);

        Assert.True(sequence.IsValid);
        Assert.True(sequence.FrameCount > 0);
        Assert.NotEmpty(sequence.AudioChannels);

        foreach (byte[] channel in sequence.AudioChannels)
        {
            Assert.True(channel.Length > 44);
            Assert.Equal(0x46464952, BinaryPrimitives.ReadInt32LittleEndian(channel));
            Assert.Equal(0x45564157, BinaryPrimitives.ReadInt32LittleEndian(channel.AsSpan(8)));
            Assert.Equal(channel.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(channel.AsSpan(40)));
        }
    }

    [Fact]
    public void TheCurrentFrame_IsCopiedAndScaled()
    {
        string? path = FindVideo();
        if (path is null)
        {
            return;
        }

        using SmkVideoSequence sequence = new(path);

        Assert.True(sequence.IsValid);

        int width = sequence.Width;
        int height = sequence.Height;
        byte[] image = new byte[width * height];

        sequence.GetCurrentFrame(image, width, height, 0, 0, out int copiedWidth, out int copiedHeight);

        Assert.Equal(width, copiedWidth);
        Assert.Equal(height, copiedHeight);
        Assert.Contains(image, value => value != 0);

        // The palette of the placed frame travels with it.
        Assert.Contains(sequence.Palette.ToArray(), value => value != 0);
    }

    [Fact]
    public void AFeatureLengthVideo_DecodesItsFirstFrames()
    {
        string? path = FindFeatureLengthVideo();
        if (path is null)
        {
            return;
        }

        SmkFile file = SmkFile.Open(path)!;

        // The clips of <c>data</c> are the full movies of the game, unlike the small samples of
        // <c>heroes2/Anim</c> that the other tests use.
        Assert.True(file.FrameCount > 50, $"'{path}' has only {file.FrameCount} frame(s).");
        Assert.True(file.Width >= 320, $"'{path}' is {file.Width}x{file.Height}.");
        Assert.True(file.Height >= 160, $"'{path}' is {file.Width}x{file.Height}.");

        Assert.True(file.First());
        Assert.Contains(file.FramePixels.ToArray(), value => value != 0);

        int frames = Math.Min(file.FrameCount, 50);

        for (int frame = 1; frame < frames; ++frame)
        {
            Assert.True(file.Next(), $"Frame {frame} of {file.FrameCount} did not decode.");
        }
    }

    [Fact]
    public void AFileThatIsNotSmacker_IsRejected()
    {
        byte[] garbage = new byte[64];

        Assert.Throws<BinaryFormatException>(() => SmkFile.Open(garbage));
        Assert.Throws<BinaryFormatException>(() => SmkFile.Open(new byte[10]));
    }

    private static string? FindVideo()
    {
        string? directory = GameData.ResolveDataDirectory();
        if (directory is null)
        {
            return null;
        }

        string path = Path.Combine(directory, VideoRelativePath);

        return File.Exists(path) ? path : null;
    }

    private static string? FindFeatureLengthVideo()
    {
        string? directory = GameData.ResolveDataDirectory();
        if (directory is null)
        {
            return null;
        }

        // Resolve() points at the folder of the archives (…/Data on the GOG layout), the full movies live there or
        // in a "data" folder next to it.
        string[] candidates =
        [
            directory,
            Path.Combine(directory, "data"),
            Path.Combine(Path.GetDirectoryName(directory) ?? directory, "data"),
        ];

        foreach (string candidate in candidates)
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            string? video = Directory
                .GetFiles(candidate, "*.smk")
                .OrderBy(name => name, StringComparer.Ordinal)
                .FirstOrDefault();

            if (video is not null)
            {
                return video;
            }
        }

        return null;
    }
}
