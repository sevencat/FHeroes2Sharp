namespace FHeroes2.Game.Audio;

/// <summary>
/// Where a looping sound is heard from, <c>AudioLoopEffectInfo</c> (<c>audio_manager.cpp:43-55</c>).
/// </summary>
/// <remarks>
/// The angle is the direction the sound comes from in degrees, the distance is the volume falloff. Two effects
/// with the same pair are the same effect: the manager reuses the channel of a running sound instead of starting a
/// second copy of it.
/// </remarks>
/// <param name="Angle">The direction of the sound in degrees.</param>
/// <param name="Distance">The distance of the sound, which is the volume falloff.</param>
public readonly record struct AudioLoopEffectInfo(short Angle = 0, byte Distance = 0);
