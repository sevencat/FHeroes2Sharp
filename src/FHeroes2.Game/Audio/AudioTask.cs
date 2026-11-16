using FHeroes2.Assets.Audio;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Audio;

/// <summary>What the sound thread is doing, <c>AsyncSoundManager::TaskType</c> (<c>audio_manager.cpp:85-92</c>).</summary>
public enum AudioTaskType
{
    /// <summary>Nothing to do.</summary>
    None = 0,

    /// <summary>Starting a tune, <c>PLAY_MUSIC</c>.</summary>
    PlayMusic = 1,

    /// <summary>Playing one sound effect, <c>PLAY_SOUND</c>.</summary>
    PlaySound = 2,

    /// <summary>Updating the looping sound effects, <c>PLAY_LOOP_SOUND</c>.</summary>
    PlayLoopSound = 3,
}

/// <summary>A request to start a tune, <c>std::pair&lt;musicId, std::pair&lt;musicType, playbackMode&gt;&gt;</c>.</summary>
/// <param name="Track">The tune.</param>
/// <param name="Source">Where the tune should come from.</param>
/// <param name="Mode">How it should be started.</param>
public readonly record struct MusicRequest(MusicTrack Track, MusicSource Source, MusicPlaybackMode Mode);

/// <summary>A request to update the looping sound effects, the payload of <c>PLAY_LOOP_SOUND</c>.</summary>
/// <remarks>
/// The reference keeps the effects in a <c>std::map</c> keyed by the sound type, so that the update walks them in the
/// same order every time. The port keeps the same order, because the order decides which channel of a running sound
/// is reused for which new one.
/// </remarks>
public sealed class LoopSoundRequest
{
    private readonly SortedDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> _effects = new();

    /// <summary>Collects a request.</summary>
    /// <param name="effects">The sound effects to hear, keyed by the sound type.</param>
    /// <param name="is3DAudioEnabled">Whether the effects carry a position.</param>
    public LoopSoundRequest(
        IEnumerable<KeyValuePair<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>> effects,
        bool is3DAudioEnabled)
    {
        foreach (KeyValuePair<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> effect in effects)
        {
            _effects[effect.Key] = effect.Value;
        }

        Is3DAudioEnabled = is3DAudioEnabled;
    }

    /// <summary>Whether the effects carry a position.</summary>
    public bool Is3DAudioEnabled { get; }

    /// <summary>The sound effects, in the order of the sound types.</summary>
    public IReadOnlyDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> Effects => _effects;
}

/// <summary>One unit of work for the sound thread, <c>AsyncSoundManager::Task</c>.</summary>
public readonly struct AudioTask
{
    private AudioTask(AudioTaskType type, MusicRequest? music, M82Sound sound, LoopSoundRequest? loop)
    {
        Type = type;
        Music = music;
        Sound = sound;
        Loop = loop;
    }

    /// <summary>What has to be done.</summary>
    public AudioTaskType Type { get; }

    /// <summary>The tune to start, set only for <see cref="AudioTaskType.PlayMusic"/>.</summary>
    public MusicRequest? Music { get; }

    /// <summary>The sound effect to play, set only for <see cref="AudioTaskType.PlaySound"/>.</summary>
    public M82Sound Sound { get; }

    /// <summary>The looping effects to update, set only for <see cref="AudioTaskType.PlayLoopSound"/>.</summary>
    public LoopSoundRequest? Loop { get; }

    /// <summary>Builds a music task.</summary>
    /// <param name="request">The tune to start.</param>
    /// <returns>The task.</returns>
    public static AudioTask ForMusic(MusicRequest request) =>
        new(AudioTaskType.PlayMusic, request, default, null);

    /// <summary>Builds a sound effect task.</summary>
    /// <param name="sound">The sound effect to play.</param>
    /// <returns>The task.</returns>
    public static AudioTask ForSound(M82Sound sound) => new(AudioTaskType.PlaySound, null, sound, null);

    /// <summary>Builds a looping effects task.</summary>
    /// <param name="request">The effects to update.</param>
    /// <returns>The task.</returns>
    public static AudioTask ForLoop(LoopSoundRequest request) => new(AudioTaskType.PlayLoopSound, null, default, request);
}
