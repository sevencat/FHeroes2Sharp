namespace FHeroes2.Game.Audio;

/// <summary>
/// The sound card, which is what the reference reaches through <c>Audio::isValid()</c>, the <c>Mixer::</c> namespace
/// and the <c>Music::</c> namespace.
/// </summary>
/// <remarks>
/// <para>
/// The reference talks to SDL_mixer directly from the manager. The port keeps the manager free of the library: it
/// decides what should be heard and when, this interface only makes the noise. That is also what makes the manager
/// testable without a sound card.
/// </para>
/// <para>
/// A channel is a number the device hands out for one playing sound effect; it stays valid until the sound stops or
/// the device is told to stop it. The reference uses the SDL channel number, the port only needs an opaque handle.
/// </para>
/// </remarks>
public interface IAudioDevice
{
    /// <summary>Whether there is a sound card to play on, <c>Audio::isValid()</c>.</summary>
    bool IsValid { get; }

    /// <summary>Starts one sound effect, <c>Mixer::Play()</c>.</summary>
    /// <param name="wave">The WAV file, header included.</param>
    /// <param name="loop">True to repeat the sound until it is stopped.</param>
    /// <param name="position">Where the sound is heard from, null for no positioning.</param>
    /// <returns>The channel, or a negative number when nothing was started.</returns>
    int Play(ReadOnlySpan<byte> wave, bool loop, AudioLoopEffectInfo? position = null);

    /// <summary>Moves a playing sound to another position, <c>Mixer::setPosition()</c>.</summary>
    /// <param name="channel">The channel of the sound.</param>
    /// <param name="angle">The direction of the sound in degrees.</param>
    /// <param name="distance">The distance of the sound.</param>
    void SetPosition(int channel, short angle, byte distance);

    /// <summary>Stops one sound effect, <c>Mixer::Stop( channel )</c>.</summary>
    /// <param name="channel">The channel of the sound.</param>
    void StopSound(int channel);

    /// <summary>Stops every sound effect, <c>Mixer::Stop()</c>.</summary>
    void StopAllSounds();

    /// <summary>Whether a sound effect is still playing, <c>Mixer::isPlaying()</c>.</summary>
    /// <param name="channel">The channel of the sound.</param>
    /// <returns>True while the sound can be heard.</returns>
    bool IsSoundPlaying(int channel);

    /// <summary>
    /// Starts a track the device already holds, <c>Music::Play( uid, mode )</c>.
    /// </summary>
    /// <param name="uid">The identity of the track, see <see cref="MusicIdentity"/>.</param>
    /// <param name="mode">How to start it.</param>
    /// <returns>False when the device does not hold a track with this identity.</returns>
    bool PlayMusicFromDatabase(ulong uid, MusicPlaybackMode mode);

    /// <summary>Starts a track from MIDI data, <c>Music::Play( uid, data, mode )</c>.</summary>
    /// <param name="uid">The identity of the track.</param>
    /// <param name="midi">The MIDI file.</param>
    /// <param name="mode">How to start it.</param>
    void PlayMusic(ulong uid, ReadOnlySpan<byte> midi, MusicPlaybackMode mode);

    /// <summary>Starts a track from a file, <c>Music::Play( uid, file, mode )</c>.</summary>
    /// <param name="uid">The identity of the track.</param>
    /// <param name="filePath">The music file.</param>
    /// <param name="mode">How to start it.</param>
    void PlayMusic(ulong uid, string filePath, MusicPlaybackMode mode);

    /// <summary>Stops the music, <c>Music::Stop()</c>.</summary>
    void StopMusic();

    /// <summary>Whether music is playing, <c>Music::isPlaying()</c>.</summary>
    bool IsMusicPlaying { get; }

    /// <summary>Sets the music volume, <c>Music::setVolume()</c>.</summary>
    /// <param name="percent">Volume from 0 to 100, the <c>100 * MusicVolume() / 10</c> of the settings window.</param>
    void SetMusicVolume(int percent);

    /// <summary>Sets the sound effects volume, <c>Mixer::setVolume()</c>.</summary>
    /// <param name="percent">Volume from 0 to 100, the <c>100 * SoundVolume() / 10</c> of the settings window.</param>
    void SetSoundVolume(int percent);
}
