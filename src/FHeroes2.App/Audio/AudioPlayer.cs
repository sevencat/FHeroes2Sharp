using System.Runtime.InteropServices;
using FHeroes2.Assets;
using FHeroes2.Assets.Audio;
using FHeroes2.Core.Diagnostics;
using SDL3;

namespace FHeroes2.App.Audio;

/// <summary>
/// Music and sound effect playback of the desktop client, backed by SDL3_mixer.
///
/// <para>
/// This is the counterpart of <c>AudioManager</c> (<c>audio_manager.cpp</c>) restricted to what the
/// port can use today: one looping music track and one shot sound effects. Music is decoded by
/// SDL3_mixer from the external files in the <c>MUSIC</c> folder (see
/// <see cref="MusicTrackCatalog"/>), sound effects come from the archives as headerless PCM and are
/// wrapped by <see cref="WaveSound"/> before playback.
/// </para>
///
/// <para>
/// Everything fails soft: a machine without an audio device, or a player without the soundtrack,
/// logs a warning and keeps the client running silently. Nothing here may abort start up.
/// </para>
/// </summary>
public sealed class AudioPlayer : IDisposable
{
    /// <summary>Playback volume of the music relative to the mixer, matching the reference default.</summary>
    public const float DefaultMusicVolume = 1.0f;

    /// <summary>Playback volume of the sound effects relative to the mixer.</summary>
    public const float DefaultSoundVolume = 1.0f;

    private readonly AssetBundle? _assets;
    private readonly string[] _musicDirectories;
    private readonly Dictionary<string, IntPtr> _soundCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IntPtr> _activeSoundTracks = [];
    private readonly Dictionary<IntPtr, IntPtr> _transientAudio = [];

    private IntPtr _mixer;
    private IntPtr _musicTrack;
    private IntPtr _musicAudio;
    private MusicTrack? _currentTrack;
    private float _musicVolume = DefaultMusicVolume;
    private float _soundVolume = DefaultSoundVolume;
    private bool _mixerInitialised;
    private bool _disposed;

    private AudioPlayer(AssetBundle? assets, string[] musicDirectories)
    {
        _assets = assets;
        _musicDirectories = musicDirectories;
    }

    /// <summary>True while a mixer with an open audio device is behind this instance.</summary>
    public bool IsAvailable => !_disposed && _mixer != IntPtr.Zero;

    /// <summary>Music track that was last requested, or <see langword="null"/> when none is playing.</summary>
    public MusicTrack? CurrentTrack => _currentTrack;

    /// <summary>Music volume in the range 0..1.</summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0.0f, 1.0f);

            if (_musicTrack != IntPtr.Zero)
            {
                _ = Mixer.SetTrackGain(_musicTrack, _musicVolume);
            }
        }
    }

    /// <summary>Sound effect volume in the range 0..1.</summary>
    public float SoundVolume
    {
        get => _soundVolume;
        set => _soundVolume = Math.Clamp(value, 0.0f, 1.0f);
    }

    /// <summary>
    /// Opens the audio device and prepares the music lookup; returns <see langword="null"/> (after
    /// logging why) when the platform has no usable audio output.
    /// </summary>
    /// <param name="assets">Archive bundle used for the sound effects; may be <see langword="null"/>.</param>
    public static AudioPlayer? TryCreate(AssetBundle? assets)
    {
        string[] musicDirectories = ResolveMusicRoots(assets);
        AudioPlayer player = new(assets, musicDirectories);

        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
        {
            GameLog.Warn($"SDL audio subsystem is not available: {SDL.GetError()}");
            return null;
        }

        if (!Mixer.Init())
        {
            GameLog.Warn($"SDL3_mixer could not be initialised: {SDL.GetError()}");
            SDL.QuitSubSystem(SDL.InitFlags.Audio);
            return null;
        }

        player._mixerInitialised = true;
        player._mixer = Mixer.CreateMixerDevice(SDL.AudioDeviceDefaultPlayback, IntPtr.Zero);
        if (player._mixer == IntPtr.Zero)
        {
            GameLog.Warn($"No audio device for the mixer: {SDL.GetError()}");
            player.Dispose();
            return null;
        }

        GameLog.Info(
            $"Audio ready: SDL3_mixer {Mixer.Version()}, {musicDirectories.Length} music folder(s) probed.");

        return player;
    }

    private static string[] ResolveMusicRoots(AssetBundle? assets)
    {
        List<string> roots = [];

        if (assets?.DataDirectory is string dataDirectory)
        {
            // The data directory is usually <game>/Data, the soundtrack sits in <game>/MUSIC; both
            // the directory itself and its parent are probed by MusicTrackCatalog.
            roots.Add(dataDirectory);
        }

        foreach (string candidate in AssetLocator.CandidateDataDirectories(null))
        {
            if (!roots.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(candidate);
            }
        }

        return [.. roots];
    }

    /// <summary>
    /// Starts the looping playback of <paramref name="track"/>, unless it is already playing.
    /// Returns <see langword="false"/> when the player has no such music file or no audio device.
    /// </summary>
    public bool PlayMusic(MusicTrack track)
    {
        if (!IsAvailable)
        {
            return false;
        }

        if (_currentTrack == track && _musicTrack != IntPtr.Zero && Mixer.TrackPlaying(_musicTrack))
        {
            return true;
        }

        string? path = MusicTrackCatalog.FindFile(_musicDirectories, track);
        if (path is null)
        {
            GameLog.Info($"No music file for '{MusicTrackCatalog.GetDescription(track)}'; staying silent.");
            return false;
        }

        StopMusic();

        IntPtr audio = Mixer.LoadAudio(_mixer, path, false);
        if (audio == IntPtr.Zero)
        {
            GameLog.Warn($"Music file '{Path.GetFileName(path)}' could not be decoded: {SDL.GetError()}");
            return false;
        }

        IntPtr trackHandle = Mixer.CreateTrack(_mixer);
        if (trackHandle == IntPtr.Zero)
        {
            GameLog.Warn($"No mixer track for '{Path.GetFileName(path)}': {SDL.GetError()}");
            Mixer.DestroyAudio(audio);
            return false;
        }

        // Loop forever: the original game restarts a track only when the scene changes.
        if (!Mixer.SetTrackAudio(trackHandle, audio)
            || !Mixer.SetTrackLoops(trackHandle, -1)
            || !Mixer.SetTrackGain(trackHandle, _musicVolume)
            || !Mixer.PlayTrack(trackHandle, 0))
        {
            GameLog.Warn($"Music '{Path.GetFileName(path)}' could not be played: {SDL.GetError()}");
            Mixer.DestroyTrack(trackHandle);
            Mixer.DestroyAudio(audio);
            return false;
        }

        _musicTrack = trackHandle;
        _musicAudio = audio;
        _currentTrack = track;

        GameLog.Info($"Music playing: '{MusicTrackCatalog.GetDescription(track)}' from '{Path.GetFileName(path)}'.");
        return true;
    }

    /// <summary>Stops the music, if any, and releases its decoder.</summary>
    public void StopMusic()
    {
        if (_musicTrack != IntPtr.Zero)
        {
            _ = Mixer.StopTrack(_musicTrack, 0);
            Mixer.DestroyTrack(_musicTrack);
            _musicTrack = IntPtr.Zero;
        }

        if (_musicAudio != IntPtr.Zero)
        {
            Mixer.DestroyAudio(_musicAudio);
            _musicAudio = IntPtr.Zero;
        }

        _currentTrack = null;
    }

    /// <summary>
    /// Plays the sound effect <paramref name="soundName"/> (for example <c>PICKUP01</c>) once, on a
    /// track of its own so that overlapping effects do not cut each other off.
    /// </summary>
    public bool PlaySound(string soundName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);

        if (!IsAvailable)
        {
            return false;
        }

        IntPtr audio = LoadSound(soundName);
        if (audio == IntPtr.Zero)
        {
            return false;
        }

        IntPtr trackHandle = Mixer.CreateTrack(_mixer);
        if (trackHandle == IntPtr.Zero)
        {
            GameLog.Warn($"No mixer track for sound '{soundName}': {SDL.GetError()}");
            return false;
        }

        if (!Mixer.SetTrackAudio(trackHandle, audio)
            || !Mixer.SetTrackGain(trackHandle, _soundVolume)
            || !Mixer.PlayTrack(trackHandle, 0))
        {
            GameLog.Warn($"Sound '{soundName}' could not be played: {SDL.GetError()}");
            Mixer.DestroyTrack(trackHandle);
            return false;
        }

        _activeSoundTracks.Add(trackHandle);
        return true;
    }

    /// <summary>
    /// Plays a WAV file held in memory once, on a track of its own. The decoded audio is released with the track
    /// when the sound ends, unlike the cached sound effects of <see cref="PlaySound"/>.
    /// </summary>
    /// <param name="wave">A complete RIFF WAVE file.</param>
    /// <returns>Whether the sound started.</returns>
    public bool PlayWave(ReadOnlySpan<byte> wave)
    {
        if (!IsAvailable || wave.IsEmpty)
        {
            return false;
        }

        IntPtr audio = LoadWave(wave.ToArray());
        if (audio == IntPtr.Zero)
        {
            GameLog.Warn($"A wave could not be prepared: {SDL.GetError()}");
            return false;
        }

        IntPtr trackHandle = Mixer.CreateTrack(_mixer);
        if (trackHandle == IntPtr.Zero)
        {
            GameLog.Warn($"No mixer track for a wave: {SDL.GetError()}");
            Mixer.DestroyAudio(audio);
            return false;
        }

        if (!Mixer.SetTrackAudio(trackHandle, audio)
            || !Mixer.SetTrackGain(trackHandle, _soundVolume)
            || !Mixer.PlayTrack(trackHandle, 0))
        {
            GameLog.Warn($"A wave could not be played: {SDL.GetError()}");
            Mixer.DestroyTrack(trackHandle);
            Mixer.DestroyAudio(audio);
            return false;
        }

        _activeSoundTracks.Add(trackHandle);
        _transientAudio.Add(trackHandle, audio);

        return true;
    }

    /// <summary>Releases the tracks of the sound effects that finished playing; call it once per frame.</summary>
    public void Update()
    {
        for (int index = _activeSoundTracks.Count - 1; index >= 0; --index)
        {
            IntPtr trackHandle = _activeSoundTracks[index];
            if (Mixer.TrackPlaying(trackHandle))
            {
                continue;
            }

            Mixer.DestroyTrack(trackHandle);
            _activeSoundTracks.RemoveAt(index);

            if (_transientAudio.Remove(trackHandle, out IntPtr audio))
            {
                Mixer.DestroyAudio(audio);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopMusic();

        foreach (IntPtr trackHandle in _activeSoundTracks)
        {
            Mixer.DestroyTrack(trackHandle);
        }

        _activeSoundTracks.Clear();

        foreach (IntPtr audio in _transientAudio.Values)
        {
            Mixer.DestroyAudio(audio);
        }

        _transientAudio.Clear();

        foreach (IntPtr audio in _soundCache.Values)
        {
            Mixer.DestroyAudio(audio);
        }

        _soundCache.Clear();

        if (_mixer != IntPtr.Zero)
        {
            Mixer.DestroyMixer(_mixer);
            _mixer = IntPtr.Zero;
        }

        if (_mixerInitialised)
        {
            Mixer.Quit();
            _mixerInitialised = false;
        }

        SDL.QuitSubSystem(SDL.InitFlags.Audio);
    }

    private IntPtr LoadSound(string soundName)
    {
        string archiveName = GameSound.GetArchiveName(soundName);
        if (_soundCache.TryGetValue(archiveName, out IntPtr cached))
        {
            return cached;
        }

        if (_assets is null)
        {
            GameLog.Warn($"Sound '{archiveName}' skipped: the game data is not loaded.");
            return IntPtr.Zero;
        }

        byte[]? body = GameSound.TryReadBody(_assets, soundName);
        if (body is null)
        {
            GameLog.Warn($"Sound '{archiveName}' is not in the archive.");
            return IntPtr.Zero;
        }

        IntPtr audio = LoadWave(WaveSound.FromGameSound(body));
        if (audio == IntPtr.Zero)
        {
            GameLog.Warn($"Sound '{archiveName}' could not be prepared: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        _soundCache.Add(archiveName, audio);
        return audio;
    }

    private IntPtr LoadWave(byte[] wave)
    {
        GCHandle pinned = GCHandle.Alloc(wave, GCHandleType.Pinned);
        try
        {
            IntPtr stream = SDL.IOFromMem(pinned.AddrOfPinnedObject(), (UIntPtr)wave.Length);
            if (stream == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // The audio is decoded right away, so the memory stream can be closed with the call.
            IntPtr audio = Mixer.LoadAudioIO(_mixer, stream, true, true);
            if (audio == IntPtr.Zero)
            {
                _ = SDL.CloseIO(stream);
            }

            return audio;
        }
        finally
        {
            pinned.Free();
        }
    }
}
