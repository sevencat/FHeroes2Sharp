using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// One combination artifact and the parts it replaces, <c>ArtifactSetData</c> (<c>artifact.h:350-366</c>,
/// <c>artifact.cpp:60-62</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keys its set table by this record's assembled id (<c>operator&lt;</c> compares
/// <c>_assembledArtifactID</c>) and carries a gettext message along with it. The port drops the message: the only
/// caller shows it in a dialog, and the port has no dialogs yet. The table has exactly one entry — the Anduran
/// pieces and the Battle Garb — and the reference has had no other since the original game.
/// </para>
/// <para>
/// The type is a record so that a set can be used as a dictionary key by value, which is how
/// <see cref="ArtifactBag.AssembleArtifactSets"/> reports what it assembled.
/// </para>
/// </remarks>
/// <param name="Assembled">The artifact the parts turn into, <c>_assembledArtifactID</c>.</param>
/// <param name="Parts">
/// The artifacts it consumes, <c>std::vector&lt;int32_t&gt;</c> of the table; all of them have to be in the bag.
/// </param>
public sealed record ArtifactSet(ArtifactType Assembled, IReadOnlyList<ArtifactType> Parts);

/// <summary>
/// The combination artifacts of the game, <c>artifactSets</c> (<c>artifact.cpp:60-62</c>).
/// </summary>
public static class ArtifactSets
{
    /// <summary>Every set, in the reference's order.</summary>
    /// <remarks>
    /// The reference keeps the table as a <c>std::map</c> keyed by the assembled artifact, so iteration is by
    /// assembled id; with a single entry the order is moot, and the list keeps the source order for a diff.
    /// </remarks>
    public static IReadOnlyList<ArtifactSet> All { get; } =
    [
        new ArtifactSet(
            ArtifactType.BattleGarb,
            [ArtifactType.HelmetAnduran, ArtifactType.SwordAnduran, ArtifactType.BreastplateAnduran]),
    ];
}
