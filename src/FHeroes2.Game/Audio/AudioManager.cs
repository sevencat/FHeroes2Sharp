using FHeroes2.Assets.Audio;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Audio;

/// <summary>
/// Everything about hearing the game, <c>src/fheroes2/audio/audio_manager.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The manager is the one place that knows where a sound comes from and how a tune is found: a sound effect is a
/// body of samples in the archive that is wrapped in a WAV header, a tune is either a file in the music folder or a
/// track of the archives. Both are cached after the first read, because the archives are read from disk.
/// </para>
/// <para>
/// The reference spreads this over free functions and a few globals and starts a worker thread, so that starting a
/// MIDI tune does not freeze the map. The port keeps the same decisions in an object with an explicit
/// <see cref="Update"/>, which the host calls once a frame; the device is an injected interface, so that the whole
/// thing can be driven by a fake without a sound card.
/// </para>
/// </remarks>
public sealed class AudioManager
{
    /// <summary>How many sound effects can be heard at once, <c>Mixer::SetChannels( 32 )</c>.</summary>
    public const int ChannelCount = 32;

    /// <summary>How long a tune fades in, in milliseconds, <c>Music::SetFadeInMs( 900 )</c>.</summary>
    public const int MusicFadeInMs = 900;

    private readonly Dictionary<M82Sound, byte[]?> _waveCache = new();

    private readonly Dictionary<XmiTrack, byte[]?> _midiCache = new();

    private readonly IAudioDevice _device;

    private readonly IAudioSource _source;

    private readonly Settings _settings;

    private readonly AudioTaskQueue _tasks = new();

    private readonly AudioLoopEffects _loopEffects;

    private MusicTrack _currentMusicTrack = MusicTrack.Unknown;

    private MusicTrack _lastRequestedMusicTrack = MusicTrack.Unknown;

    /// <summary>Builds the manager.</summary>
    /// <param name="device">The sound card.</param>
    /// <param name="source">Where the bytes come from.</param>
    /// <param name="settings">The settings, which say where the music comes from and whether sound is positioned.</param>
    public AudioManager(IAudioDevice device, IAudioSource source, Settings settings)
    {
        _device = device;
        _source = source;
        _settings = settings;
        _loopEffects = new AudioLoopEffects(channel => _device.IsSoundPlaying(channel));
    }

    /// <summary>Whether there is a sound card to play on, <c>Audio::isValid()</c>.</summary>
    public bool IsValid => _device.IsValid;

    /// <summary>The tune that was asked for last, <c>lastRequestedMusicTrackId</c>.</summary>
    public MusicTrack LastRequestedMusicTrack => _lastRequestedMusicTrack;

    /// <summary>The tune that is playing, <c>currentMusicTrackId</c>.</summary>
    public MusicTrack CurrentMusicTrack => _currentMusicTrack;

    /// <summary>The work that is waiting, exposed for the tests and for hosts that drain one task a frame.</summary>
    public AudioTaskQueue Tasks => _tasks;

    /// <summary>The looping sound effects that are heard, exposed for the tests.</summary>
    public AudioLoopEffects LoopEffects => _loopEffects;

    /// <summary>Passes the music volume to the device, <c>Music::setVolume()</c>.</summary>
    /// <param name="percent">Volume from 0 to 100.</param>
    public void SetMusicVolume(int percent) => _device.SetMusicVolume(percent);

    /// <summary>Passes the sound effects volume to the device, <c>Mixer::setVolume()</c>.</summary>
    /// <param name="percent">Volume from 0 to 100.</param>
    public void SetSoundVolume(int percent) => _device.SetSoundVolume(percent);

    /// <summary>
    /// Whether the music folder has a file for a tune, <c>isExternalMusicFileAvailable()</c>.
    /// </summary>
    /// <param name="track">The tune.</param>
    /// <returns>True when a file is there.</returns>
    public bool IsExternalMusicFileAvailable(MusicTrack track) =>
        MusicTrackCatalog.FindFile(_source.MusicDirectories, track) is not null;

    /// <summary>
    /// Plays one sound effect right away, dropping the ones that were waiting, <c>PlaySound()</c>.
    /// </summary>
    /// <param name="sound">The sound effect.</param>
    /// <returns>The channel it plays on, or a negative number when nothing was started.</returns>
    public int PlaySound(M82Sound sound)
    {
        if (sound == M82Sound.Unknown || !IsValid)
        {
            return -1;
        }

        // The caller wants this sound now, so whatever was still waiting is stale.
        _tasks.RemoveSoundTasks();

        return PlaySoundImpl(sound);
    }

    /// <summary>Queues one sound effect, <c>PlaySoundAsync()</c>.</summary>
    /// <param name="sound">The sound effect.</param>
    public void PlaySoundAsync(M82Sound sound)
    {
        if (sound == M82Sound.Unknown || !IsValid)
        {
            return;
        }

        _tasks.PushSound(sound);
    }

    /// <summary>
    /// Plays a tune right away, <c>PlayMusic()</c>.
    /// </summary>
    /// <param name="track">The tune.</param>
    /// <param name="mode">How to start it.</param>
    public void PlayMusic(MusicTrack track, MusicPlaybackMode mode)
    {
        if (!IsValid)
        {
            return;
        }

        // The request is remembered even when it is not a tune: a screen that stopped the music and then wants it
        // back should not get the previous track by accident.
        _lastRequestedMusicTrack = track;

        if (track is MusicTrack.Unused or MusicTrack.Unknown)
        {
            return;
        }

        _tasks.RemoveMusicTask();

        PlayMusicImpl(track, _settings.MusicType, mode);
    }

    /// <summary>Queues a tune, <c>PlayMusicAsync()</c>.</summary>
    /// <param name="track">The tune.</param>
    /// <param name="mode">How to start it.</param>
    public void PlayMusicAsync(MusicTrack track, MusicPlaybackMode mode)
    {
        if (!IsValid)
        {
            return;
        }

        _lastRequestedMusicTrack = track;

        if (track is MusicTrack.Unused or MusicTrack.Unknown)
        {
            return;
        }

        _tasks.PushMusic(new MusicRequest(track, _settings.MusicType, mode));
    }

    /// <summary>
    /// Starts the tune that is playing again from the top, looping, <c>PlayCurrentMusic()</c>.
    /// </summary>
    public void PlayCurrentMusic()
    {
        if (!IsValid)
        {
            return;
        }

        _tasks.RemoveMusicTask();

        if (_currentMusicTrack is MusicTrack.Unused or MusicTrack.Unknown)
        {
            return;
        }

        MusicTrack track = _currentMusicTrack;
        _currentMusicTrack = MusicTrack.Unknown;

        PlayMusicImpl(track, _settings.MusicType, MusicPlaybackMode.ResumeAndPlayInfinite);
    }

    /// <summary>
    /// Asks for the looping sound effects to be updated, <c>playLoopSoundsAsync()</c>.
    /// </summary>
    /// <param name="effects">The effects to hear, keyed by the sound type.</param>
    public void PlayLoopSoundsAsync(IReadOnlyDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> effects)
    {
        if (!IsValid)
        {
            return;
        }

        _tasks.PushLoopSound(new LoopSoundRequest(effects, _settings.Is3DAudioEnabled));
    }

    /// <summary>Stops every sound effect, <c>stopSounds()</c>.</summary>
    public void StopSounds()
    {
        if (!IsValid)
        {
            return;
        }

        _tasks.RemoveAllSoundTasks();

        foreach (int channel in _loopEffects.Clear())
        {
            _device.StopSound(channel);
        }

        _device.StopAllSounds();
    }

    /// <summary>Stops everything and forgets the tunes, <c>ResetAudio()</c>.</summary>
    public void ResetAudio()
    {
        if (!IsValid)
        {
            return;
        }

        _tasks.RemoveAllTasks();

        foreach (int channel in _loopEffects.Clear())
        {
            _device.StopSound(channel);
        }

        _device.StopMusic();
        _device.StopAllSounds();

        _lastRequestedMusicTrack = MusicTrack.Unknown;
        _currentMusicTrack = MusicTrack.Unknown;
    }

    /// <summary>
    /// Does the work that is waiting, which is what the sound thread of the reference does.
    /// </summary>
    /// <remarks>
    /// A tune is started before the sound effects of the same frame, and the looping effects last, which is the order
    /// of <c>AsyncSoundManager::prepareTask()</c>.
    /// </remarks>
    public void Update()
    {
        if (!IsValid)
        {
            _tasks.RemoveAllTasks();
            return;
        }

        while (_tasks.TryTakeNext(out AudioTask task))
        {
            switch (task.Type)
            {
                case AudioTaskType.PlayMusic:
                    MusicRequest music = task.Music!.Value;
                    PlayMusicImpl(music.Track, music.Source, music.Mode);
                    break;

                case AudioTaskType.PlaySound:
                    PlaySoundImpl(task.Sound);
                    break;

                case AudioTaskType.PlayLoopSound:
                    PlayLoopSoundsImpl(task.Loop!);
                    break;
            }
        }
    }

    /// <summary>Reads the samples of a sound effect, <c>GetWAV()</c>.</summary>
    private byte[]? GetWave(M82Sound sound)
    {
        if (_waveCache.TryGetValue(sound, out byte[]? cached))
        {
            return cached;
        }

        byte[]? body = _source.TryGetSoundBody(sound);
        byte[]? wave = body is { Length: > 0 } ? WaveSound.FromGameSound(body) : null;

        _waveCache[sound] = wave;

        return wave;
    }

    /// <summary>Reads the MIDI data of a track, <c>GetMID()</c>.</summary>
    private byte[]? GetMidi(XmiTrack track)
    {
        if (_midiCache.TryGetValue(track, out byte[]? cached))
        {
            return cached;
        }

        // The original tracks are not in the expansion archive, so it must not be in the way.
        bool ignoreExpansion = track >= XmiTrack.MidiOriginalKnight;

        byte[]? data = _source.TryGetMidi(track, ignoreExpansion);

        if (data is { Length: 0 })
        {
            data = null;
        }

        _midiCache[track] = data;

        return data;
    }

    /// <summary>Plays one sound effect, <c>PlaySoundImpl()</c>.</summary>
    private int PlaySoundImpl(M82Sound sound)
    {
        byte[]? wave = GetWave(sound);

        if (wave is null)
        {
            return -1;
        }

        return _device.Play(wave, loop: false);
    }

    /// <summary>
    /// Plays a tune, falling back from the loaded tracks to the music folder and then to the archives,
    /// <c>PlayMusicImpl()</c> (<c>audio_manager.cpp:532-592</c>).
    /// </summary>
    private void PlayMusicImpl(MusicTrack track, MusicSource source, MusicPlaybackMode mode)
    {
        ulong identity = MusicIdentity.Get(track, source);

        if (_currentMusicTrack == track && _device.IsMusicPlaying)
        {
            return;
        }

        // The device keeps the tunes it has already loaded, which is what the music database of the reference is.
        if (_device.PlayMusicFromDatabase(identity, mode))
        {
            _currentMusicTrack = track;
            return;
        }

        if (source == MusicSource.External)
        {
            // Nothing in the folder for this tune means the archives are tried instead, not that there is no tune.
            string? filePath = MusicTrackCatalog.FindFile(_source.MusicDirectories, track);

            if (filePath is not null)
            {
                _device.PlayMusic(identity, filePath, mode);

                _currentMusicTrack = track;

                return;
            }
        }

        XmiTrack xmi = XmiTrack.Unknown;

        if (source == MusicSource.MidiExpansion)
        {
            xmi = XmiCatalog.FromMusicTrack(track, _source.IsExpansionArchiveAvailable);
        }

        if (xmi == XmiTrack.Unknown)
        {
            xmi = XmiCatalog.FromMusicTrack(track, false);
        }

        if (xmi == XmiTrack.Unknown)
        {
            return;
        }

        byte[]? midi = GetMidi(xmi);

        if (midi is null)
        {
            return;
        }

        _device.PlayMusic(identity, midi, mode);

        _currentMusicTrack = track;
    }

    /// <summary>Updates the looping sound effects, <c>playLoopSoundsImpl()</c>.</summary>
    private void PlayLoopSoundsImpl(LoopSoundRequest request)
    {
        AudioLoopPlan plan = _loopEffects.Plan(request.Effects, request.Is3DAudioEnabled);

        // Reused channels move first, then the ones that are no longer wanted stop, then the new ones start; that is
        // the order of the reference, and it keeps a moved channel from being stopped by the step before it.
        foreach (MovedLoopChannel moved in plan.ChannelsToMove)
        {
            _device.SetPosition(moved.Channel, moved.Info.Angle, moved.Info.Distance);
        }

        foreach (int channel in plan.ChannelsToStop)
        {
            _device.StopSound(channel);
        }

        foreach (PendingLoopEffect pending in plan.EffectsToStart)
        {
            byte[]? wave = GetWave(pending.Sound);

            if (wave is null)
            {
                continue;
            }

            int channel = _device.Play(wave, loop: true, pending.Info);

            if (channel < 0)
            {
                continue;
            }

            _loopEffects.Register(pending.Sound, pending.Info, channel);
        }
    }
}
