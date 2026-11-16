using FHeroes2.Assets.Audio;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Audio;

/// <summary>
/// The 64 bit identity of a music track, <c>getMusicUID()</c> (<c>audio_manager.cpp:57-60</c>).
/// </summary>
/// <remarks>
/// The high half is where the track comes from and the low half is the track itself, so that the same tune played
/// from the original archive and from the expansion archive are two different identities. The device uses the
/// identity as the key of the tracks it has already loaded.
/// </remarks>
public static class MusicIdentity
{
    /// <summary>Builds the identity of a track, <c>getMusicUID()</c>.</summary>
    /// <param name="track">The tune.</param>
    /// <param name="source">Where the tune comes from.</param>
    /// <returns>The identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The tune is a sentinel. The reference asserts on this in debug builds; the port refuses to build an identity
    /// that could collide with a real track.
    /// </exception>
    public static ulong Get(MusicTrack track, MusicSource source)
    {
        if (track is MusicTrack.Unknown or MusicTrack.Unused)
        {
            throw new ArgumentOutOfRangeException(nameof(track), track, "Only a playable tune has an identity.");
        }

        return ((ulong)source << 32) + (uint)track;
    }
}
