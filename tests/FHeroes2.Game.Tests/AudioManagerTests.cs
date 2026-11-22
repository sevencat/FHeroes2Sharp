using FHeroes2.Assets.Audio;
using FHeroes2.Game.Audio;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the decisions of <c>audio_manager.cpp</c>: what is played, in which order, and what is skipped. The device
/// and the archives are fakes, so no sound card and no game data are needed.
/// </summary>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AudioManagerTests : IDisposable
{
    private static readonly byte[] Body = [0x01, 0x02, 0x03, 0x04];

    private static readonly AudioLoopEffectInfo Origin = new(0, 0);

    private readonly FakeDevice _device = new();

    private readonly FakeSource _source = new();

    private readonly AudioManager _audio;

    private string? _musicFolder;

    public AudioManagerTests()
    {
        Settings.Reset();

        _source.Sounds[M82Sound.Aelmattk] = Body;

        _audio = new AudioManager(_device, _source, Settings.Get());
    }

    public void Dispose()
    {
        if (_musicFolder is not null && Directory.Exists(_musicFolder))
        {
            Directory.Delete(_musicFolder, recursive: true);
        }
    }

    [Fact]
    public void ASoundEffectIsWrappedInAWavHeaderOnce()
    {
        int channel = _audio.PlaySound(M82Sound.Aelmattk);

        Assert.True(channel >= 0);

        FakeDevice.SoundPlay played = Assert.Single(_device.Sounds);
        Assert.False(played.Loop);
        Assert.Null(played.Position);
        Assert.Equal(Body.Length + WaveSound.HeaderSize, played.Wave.Length);
        Assert.Equal((byte)'R', played.Wave[0]);
        Assert.Equal((byte)'I', played.Wave[1]);
        Assert.Equal((byte)'F', played.Wave[2]);
        Assert.Equal((byte)'F', played.Wave[3]);

        // The second request is answered from the cache rather than from the archive.
        Assert.True(_audio.PlaySound(M82Sound.Aelmattk) >= 0);
        Assert.Equal(1, _source.SoundReads);
    }

    [Fact]
    public void AnUnknownSoundIsIgnored()
    {
        Assert.Equal(-1, _audio.PlaySound(M82Sound.Unknown));
        Assert.Empty(_device.Sounds);

        _audio.PlaySoundAsync(M82Sound.Unknown);

        Assert.Equal(0, _audio.Tasks.PendingSoundCount);
    }

    [Fact]
    public void ASilentDeviceSwallowsEverything()
    {
        _device.IsValid = false;

        Assert.Equal(-1, _audio.PlaySound(M82Sound.Aelmattk));

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);
        _audio.PlayMusicAsync(MusicTrack.BarbarianCastle, MusicPlaybackMode.PlayOnce);
        _audio.PlayLoopSoundsAsync(new Dictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>
        {
            [M82Sound.Aelmattk] = [Origin],
        });

        Assert.Empty(_device.Sounds);
        Assert.Empty(_device.MusicPlays);
        Assert.False(_audio.Tasks.HasMusicTask);
        Assert.False(_audio.Tasks.HasLoopSoundTask);
    }

    [Fact]
    public void TheMusicFolderIsUsedWhenItHasTheTune()
    {
        CreateMusicFolder(MusicTrack.KnightCastle);
        Settings.Get().SetMusicType(MusicSource.External);

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);

        FakeDevice.MusicPlay played = Assert.Single(_device.MusicPlays);
        Assert.NotNull(played.FilePath);
        Assert.Null(played.Midi);
        Assert.Equal(MusicTrack.KnightCastle, _audio.CurrentMusicTrack);
        Assert.True(_audio.IsExternalMusicFileAvailable(MusicTrack.KnightCastle));
        Assert.False(_audio.IsExternalMusicFileAvailable(MusicTrack.BarbarianCastle));
    }

    [Fact]
    public void TheArchivesAreTheFallbackWhenTheFolderIsEmpty()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        XmiTrack xmi = XmiCatalog.FromMusicTrack(MusicTrack.KnightCastle, false);
        _source.Midi[xmi] = [0x4D, 0x54, 0x68, 0x64];

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);

        FakeDevice.MusicPlay played = Assert.Single(_device.MusicPlays);
        Assert.Null(played.FilePath);
        Assert.NotNull(played.Midi);
        Assert.Equal(MusicTrack.KnightCastle, _audio.CurrentMusicTrack);
    }

    [Fact]
    public void AnUnreadableTuneStartsNothing()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);

        Assert.Empty(_device.MusicPlays);
        Assert.Equal(MusicTrack.Unknown, _audio.CurrentMusicTrack);
    }

    [Fact]
    public void ATuneThatIsAlreadyPlayingIsNotRestarted()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        XmiTrack xmi = XmiCatalog.FromMusicTrack(MusicTrack.KnightCastle, false);
        _source.Midi[xmi] = [0x4D, 0x54, 0x68, 0x64];

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);
        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);

        Assert.Single(_device.MusicPlays);
    }

    [Fact]
    public void TheDatabaseOfTheDeviceIsAskedFirst()
    {
        ulong identity = MusicIdentity.Get(MusicTrack.KnightCastle, MusicSource.External);
        _device.Database.Add(identity);

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);

        Assert.Empty(_device.MusicPlays);
        Assert.Equal(identity, Assert.Single(_device.DatabaseRequests));
        Assert.Equal(MusicTrack.KnightCastle, _audio.CurrentMusicTrack);
    }

    [Fact]
    public void PlayingTheCurrentTuneStartsItOver()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        XmiTrack xmi = XmiCatalog.FromMusicTrack(MusicTrack.KnightCastle, false);
        _source.Midi[xmi] = [0x4D, 0x54, 0x68, 0x64];

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);
        _audio.PlayCurrentMusic();

        // The tune is forgotten before it is started again, so the "already playing" check does not stop it.
        Assert.Equal(2, _device.MusicPlays.Count);
        Assert.Equal(MusicTrack.KnightCastle, _audio.CurrentMusicTrack);
    }

    [Fact]
    public void PlayingTheCurrentTuneOfNothingDoesNothing()
    {
        _audio.PlayCurrentMusic();

        Assert.Empty(_device.MusicPlays);
        Assert.Equal(MusicTrack.Unknown, _audio.CurrentMusicTrack);
    }

    [Fact]
    public void StoppingSilencesTheLoopingEffects()
    {
        _audio.PlayLoopSoundsAsync(new Dictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>
        {
            [M82Sound.Aelmattk] = [Origin],
        });

        _audio.Update();

        FakeDevice.SoundPlay loop = Assert.Single(_device.Sounds);
        Assert.True(loop.Loop);
        Assert.Equal(1, _audio.LoopEffects.ChannelCount);

        _audio.StopSounds();

        Assert.Equal(loop.Channel, Assert.Single(_device.StoppedChannels));
        Assert.Equal(1, _device.StopAllSoundsCalls);
        Assert.Equal(0, _audio.LoopEffects.ChannelCount);
    }

    [Fact]
    public void ResettingForgetsTheTunes()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        XmiTrack xmi = XmiCatalog.FromMusicTrack(MusicTrack.KnightCastle, false);
        _source.Midi[xmi] = [0x4D, 0x54, 0x68, 0x64];

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);
        _audio.PlayLoopSoundsAsync(new Dictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>
        {
            [M82Sound.Aelmattk] = [Origin],
        });
        _audio.Update();

        Assert.Equal(1, _audio.LoopEffects.ChannelCount);

        _audio.ResetAudio();

        Assert.Equal(1, _device.StopMusicCalls);
        Assert.Equal(MusicTrack.Unknown, _audio.CurrentMusicTrack);
        Assert.Equal(MusicTrack.Unknown, _audio.LastRequestedMusicTrack);
        Assert.Equal(0, _audio.LoopEffects.ChannelCount);
        Assert.False(_audio.Tasks.HasMusicTask);
    }

    [Fact]
    public void ASentinelTuneIsRememberedButNotPlayed()
    {
        _audio.PlayMusic(MusicTrack.Unknown, MusicPlaybackMode.PlayOnce);

        Assert.Equal(MusicTrack.Unknown, _audio.LastRequestedMusicTrack);
        Assert.Empty(_device.MusicPlays);

        _audio.PlayMusic(MusicTrack.Unused, MusicPlaybackMode.PlayOnce);

        Assert.Equal(MusicTrack.Unused, _audio.LastRequestedMusicTrack);
        Assert.Empty(_device.MusicPlays);
    }

    [Fact]
    public void TheRestorerPlaysTheRememberedTuneAgain()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        XmiTrack castle = XmiCatalog.FromMusicTrack(MusicTrack.KnightCastle, false);
        XmiTrack battle = XmiCatalog.FromMusicTrack(MusicTrack.Battle1, false);
        _source.Midi[castle] = [0x4D, 0x54, 0x68, 0x64];
        _source.Midi[battle] = [0x4D, 0x54, 0x68, 0x65];

        _audio.PlayMusic(MusicTrack.KnightCastle, MusicPlaybackMode.ResumeAndPlayInfinite);

        using (new MusicRestorer(_audio))
        {
            _audio.PlayMusic(MusicTrack.Battle1, MusicPlaybackMode.PlayOnce);
        }

        // The restorer queues the background tune instead of cutting the battle tune off.
        FakeDevice.MusicPlay battlePlay = _device.MusicPlays[^1];
        Assert.NotNull(battlePlay.Midi);
        Assert.Equal(2, _device.MusicPlays.Count);

        _audio.Update();

        Assert.Equal(MusicTrack.KnightCastle, _audio.CurrentMusicTrack);
        Assert.Equal(3, _device.MusicPlays.Count);
    }

    [Fact]
    public void TheQueueIsDrainedInTheOrderOfTheReference()
    {
        Settings.Get().SetMusicType(MusicSource.External);

        XmiTrack xmi = XmiCatalog.FromMusicTrack(MusicTrack.KnightCastle, false);
        _source.Midi[xmi] = [0x4D, 0x54, 0x68, 0x64];

        _audio.PlaySoundAsync(M82Sound.Aelmattk);
        _audio.PlayMusicAsync(MusicTrack.KnightCastle, MusicPlaybackMode.PlayOnce);
        _audio.PlayLoopSoundsAsync(new Dictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>
        {
            [M82Sound.Aelmattk] = [Origin],
        });

        // Nothing happens until the queue is drained.
        Assert.Empty(_device.Log);

        _audio.Update();

        Assert.Equal(new[] { "music", "sound", "loop" }, _device.Log);
    }

    [Fact]
    public void ASoundEffectThatCannotBeReadIsSkipped()
    {
        Assert.Equal(-1, _audio.PlaySound(M82Sound.Badluck));

        _audio.PlayLoopSoundsAsync(new Dictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>
        {
            [M82Sound.Badluck] = [Origin],
        });

        _audio.Update();

        Assert.Empty(_device.Sounds);
        Assert.Equal(0, _audio.LoopEffects.ChannelCount);
    }

    private void CreateMusicFolder(MusicTrack track)
    {
        string folder = Path.Combine(Path.GetTempPath(), "fh2-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        string name = MusicTrackCatalog.EnumerateCandidateFileNames(track).First();
        File.WriteAllBytes(Path.Combine(folder, name), [0x4F, 0x67, 0x67]);

        _musicFolder = folder;
        _source.MusicDirectories = [folder];
    }

    private sealed class FakeDevice : IAudioDevice
    {
        public sealed record SoundPlay(int Channel, bool Loop, AudioLoopEffectInfo? Position, byte[] Wave);

        public sealed record MusicPlay(ulong Uid, string? FilePath, byte[]? Midi, MusicPlaybackMode Mode);

        private readonly HashSet<int> _playing = [];

        private int _nextChannel = 100;

        public List<SoundPlay> Sounds { get; } = [];

        public List<MusicPlay> MusicPlays { get; } = [];

        public List<string> Log { get; } = [];

        public List<int> StoppedChannels { get; } = [];

        public List<ulong> DatabaseRequests { get; } = [];

        public HashSet<ulong> Database { get; } = [];

        public int StopAllSoundsCalls { get; private set; }

        public int StopMusicCalls { get; private set; }

        public bool IsValid { get; set; } = true;

        public bool IsMusicPlaying { get; private set; }

        public int Play(ReadOnlySpan<byte> wave, bool loop, AudioLoopEffectInfo? position = null)
        {
            if (wave.Length == 0)
            {
                return -1;
            }

            int channel = _nextChannel++;

            Sounds.Add(new SoundPlay(channel, loop, position, wave.ToArray()));
            Log.Add(loop ? "loop" : "sound");
            _playing.Add(channel);

            return channel;
        }

        public void SetPosition(int channel, short angle, byte distance)
        {
            Assert.Contains(channel, _playing);
        }

        public void StopSound(int channel)
        {
            StoppedChannels.Add(channel);
            _playing.Remove(channel);
        }

        public void StopAllSounds()
        {
            ++StopAllSoundsCalls;
            _playing.Clear();
        }

        public bool IsSoundPlaying(int channel) => _playing.Contains(channel);

        public bool PlayMusicFromDatabase(ulong uid, MusicPlaybackMode mode)
        {
            DatabaseRequests.Add(uid);

            if (!Database.Contains(uid))
            {
                return false;
            }

            IsMusicPlaying = true;
            Log.Add("music");

            return true;
        }

        public void PlayMusic(ulong uid, ReadOnlySpan<byte> midi, MusicPlaybackMode mode)
        {
            MusicPlays.Add(new MusicPlay(uid, null, midi.ToArray(), mode));
            IsMusicPlaying = true;
            Log.Add("music");
        }

        public void PlayMusic(ulong uid, string filePath, MusicPlaybackMode mode)
        {
            MusicPlays.Add(new MusicPlay(uid, filePath, null, mode));
            IsMusicPlaying = true;
            Log.Add("music");
        }

        public void StopMusic()
        {
            ++StopMusicCalls;
            IsMusicPlaying = false;
        }

        public int MusicVolume { get; private set; } = 100;

        public int SoundVolume { get; private set; } = 100;

        public void SetMusicVolume(int percent) => MusicVolume = percent;

        public void SetSoundVolume(int percent) => SoundVolume = percent;
    }

    private sealed class FakeSource : IAudioSource
    {
        public Dictionary<M82Sound, byte[]> Sounds { get; } = [];

        public Dictionary<XmiTrack, byte[]> Midi { get; } = [];

        public List<(XmiTrack Track, bool IgnoreExpansion)> MidiReads { get; } = [];

        public int SoundReads { get; private set; }

        public bool IsExpansionArchiveAvailable { get; set; }

        public IReadOnlyList<string> MusicDirectories { get; set; } = [];

        public byte[]? TryGetSoundBody(M82Sound sound)
        {
            ++SoundReads;

            return Sounds.TryGetValue(sound, out byte[]? body) ? body : null;
        }

        public byte[]? TryGetMidi(XmiTrack track, bool ignoreExpansion)
        {
            MidiReads.Add((track, ignoreExpansion));

            return Midi.TryGetValue(track, out byte[]? data) ? data : null;
        }
    }
}
