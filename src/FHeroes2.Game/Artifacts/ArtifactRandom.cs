using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The random artifact picker of <c>Artifact::Rand()</c> and the per-map status array that goes with it
/// (<c>artifact.cpp:44-65</c>, <c>434-462</c>, <c>1085-1097</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps one byte per artifact id in a file-scope array, and that global is the whole point of this
/// class: a map gives every randomly placed artifact out once before it starts repeating, which is what makes the
/// "artifact" objects on a map feel distributed rather than random. Two bits per id are enough — a disabled one
/// (the editor can ban an artifact) and a used one (this map already placed it).
/// </para>
/// <para>
/// Because the state is global in the reference and has to outlive every pick, the port keeps it global too, with
/// <see cref="Reset"/> standing in for <c>ResetArtifactStats()</c>, which the reference calls when a map starts.
/// A caller that forgets to reset reuses the previous map's history; the reference has the same failure mode, and
/// the port does not invent a per-map object because nothing would own it yet.
/// </para>
/// <para>
/// The tier of an artifact depends on the map version, so <see cref="Next"/> takes the version as a parameter
/// instead of reading it from a settings singleton — the same choice <see cref="ArtifactLevels.Level"/> documents.
/// </para>
/// </remarks>
public static class ArtifactRandom
{
    /// <summary>The editor banned this artifact from random placement, <c>ART_RNDDISABLED</c>.</summary>
    private const byte Disabled = 0x01;

    /// <summary>This map already gave the artifact out, <c>ART_RNDUSED</c>.</summary>
    private const byte Used = 0x02;

    private static readonly byte[] Status = new byte[(int)ArtifactType.ArtifactCount];

    /// <summary>
    /// Forgets the picks of the previous map, <c>ResetArtifactStats()</c> (<c>artifact.cpp:1085-1088</c>).
    /// </summary>
    /// <remarks>
    /// The reference calls this when a new map is loaded. Calling it in the middle of a map makes the next picks
    /// repeat artifacts the map already placed, which is a state the reference tolerates.
    /// </remarks>
    public static void Reset() => Array.Clear(Status);

    /// <summary>
    /// Bans the artifact from random placement on this map, <c>ExcludeArtifactFromRandom()</c>
    /// (<c>artifact.cpp:1090-1097</c>).
    /// </summary>
    /// <remarks>
    /// The map editor writes the ban into the map and the loader replays it here. The ban survives
    /// <see cref="Next"/> and is only cleared by <see cref="Reset"/>.
    /// </remarks>
    /// <param name="artifact">The artifact to keep out of the random rolls.</param>
    public static void Exclude(ArtifactType artifact)
    {
        if (artifact <= ArtifactType.Unknown || artifact >= ArtifactType.ArtifactCount)
        {
            return;
        }

        Status[(int)artifact] |= Disabled;
    }

    /// <summary>
    /// Picks a random artifact of the tier and marks it used, <c>Artifact::Rand()</c>
    /// (<c>artifact.cpp:434-462</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first pass only considers artifacts this map has not placed yet. When it comes up empty — a small map
    /// asking for more artifacts of a tier than the tier holds — the second pass drops that requirement and repeats
    /// instead. The ban is honoured by both passes, so a fully banned tier answers
    /// <see cref="ArtifactType.Unknown"/> rather than picking something banned.
    /// </para>
    /// <para>
    /// The picked artifact is marked used even when it came from the second pass, and
    /// <see cref="ArtifactType.Unknown"/> is marked used too (id zero), which the reference does as well and which
    /// nothing reads.
    /// </para>
    /// </remarks>
    /// <param name="level">The tier to pick from, usually <see cref="ArtifactLevel.AllNormal"/>.</param>
    /// <param name="version">Version of the current map, which decides the tier of expansion artifacts.</param>
    /// <returns>The artifact, <see cref="ArtifactType.Unknown"/> when every candidate is banned.</returns>
    public static ArtifactType Next(ArtifactLevel level, MapVersion version)
    {
        var candidates = new List<ArtifactType>(25);

        for (int id = (int)ArtifactType.Unknown + 1; id < (int)ArtifactType.ArtifactCount; ++id)
        {
            var artifact = (ArtifactType)id;

            if (ArtifactTypes.IsValid(artifact)
                && (level & ArtifactLevels.Level(artifact, version)) != 0
                && (Status[id] & (Disabled | Used)) == 0)
            {
                candidates.Add(artifact);
            }
        }

        if (candidates.Count == 0)
        {
            for (int id = (int)ArtifactType.Unknown + 1; id < (int)ArtifactType.ArtifactCount; ++id)
            {
                var artifact = (ArtifactType)id;

                if (ArtifactTypes.IsValid(artifact)
                    && (level & ArtifactLevels.Level(artifact, version)) != 0
                    && (Status[id] & Disabled) == 0)
                {
                    candidates.Add(artifact);
                }
            }
        }

        ArtifactType result = candidates.Count > 0 ? Rand.Get(candidates) : ArtifactType.Unknown;
        Status[(int)result] |= Used;

        return result;
    }
}
