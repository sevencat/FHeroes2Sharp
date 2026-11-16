namespace FHeroes2.Game.Audio;

/// <summary>How a music track is started, <c>Music::PlaybackMode</c> (<c>audio.h:71-84</c>).</summary>
public enum MusicPlaybackMode : byte
{
    /// <summary>Plays the track once and then stops, <c>PLAY_ONCE</c>.</summary>
    PlayOnce = 0,

    /// <summary>
    /// Continues from wherever the track was left and then loops forever, <c>PLAY_INFINITE_RESUME</c>.
    /// </summary>
    ResumeAndPlayInfinite = 1,

    /// <summary>Starts the track from the beginning and then loops forever, <c>PLAY_INFINITE_REWIND</c>.</summary>
    RewindAndPlayInfinite = 2,
}
