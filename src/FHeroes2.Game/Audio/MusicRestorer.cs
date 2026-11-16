using FHeroes2.Assets.Audio;

namespace FHeroes2.Game.Audio;

/// <summary>
/// Brings the background music back after a screen that played its own tune,
/// <c>MusicRestorer</c> (<c>audio_manager.cpp:823-833</c>).
/// </summary>
/// <remarks>
/// The constructor remembers the tune that was asked for before the screen took over; disposing asks for it again,
/// looping, because a background track is assumed to have been looping. This is a scope guard, so it is meant to be
/// used with <c>using</c>.
/// </remarks>
public sealed class MusicRestorer : IDisposable
{
    private readonly AudioManager _audio;

    private readonly MusicTrack _track;

    /// <summary>Takes a note of the tune that is playing right now.</summary>
    /// <param name="audio">The manager to restore the tune on.</param>
    public MusicRestorer(AudioManager audio)
    {
        _audio = audio;
        _track = audio.LastRequestedMusicTrack;
    }

    /// <summary>Asks for the remembered tune again, looping.</summary>
    public void Dispose() => _audio.PlayMusicAsync(_track, MusicPlaybackMode.ResumeAndPlayInfinite);
}
