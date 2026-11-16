using FHeroes2.Assets.Audio;

namespace FHeroes2.Game.Audio;

/// <summary>
/// Where the manager gets its bytes from: the two archives and the folder of music files.
/// </summary>
/// <remarks>
/// The reference reads the archives through <c>getDataFromAggFile()</c> and converts the tracks with
/// <c>Music::Xmi2Mid()</c>. The port asks for the already converted data, so that the manager does not have to know
/// which archive a track lives in nor how an XMI file becomes a MIDI one.
/// </remarks>
public interface IAudioSource
{
    /// <summary>Whether the Price of Loyalty archive is there, <c>g_midiHeroes2xAGG.isGood()</c>.</summary>
    bool IsExpansionArchiveAvailable { get; }

    /// <summary>The folders searched for music files, <c>Settings::GetRootDirs()</c>.</summary>
    IReadOnlyList<string> MusicDirectories { get; }

    /// <summary>Reads the body of a sound effect, <c>getDataFromAggFile()</c>.</summary>
    /// <param name="sound">The sound effect.</param>
    /// <returns>The raw samples, or null when the archive does not have them.</returns>
    byte[]? TryGetSoundBody(M82Sound sound);

    /// <summary>Reads the MIDI data of a track, <c>LoadMID()</c>.</summary>
    /// <param name="track">The XMI track.</param>
    /// <param name="ignoreExpansion">
    /// True for the tracks that only the original archive has, which have to be read without the expansion in the way.
    /// </param>
    /// <returns>The MIDI file, or null when the track cannot be read or converted.</returns>
    byte[]? TryGetMidi(XmiTrack track, bool ignoreExpansion);
}
