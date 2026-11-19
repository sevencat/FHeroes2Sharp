using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The one artifact a map hides behind its puzzle, <c>UltimateArtifact</c> (<c>artifact_ultimate.h:35-75</c>,
/// <c>artifact_ultimate.cpp:30-56</c>).
/// </summary>
/// <remarks>
/// <para>
/// The direction of the reference's inheritance is the interesting part: <c>UltimateArtifact</c> is an
/// <c>Artifact</c> with a tile and a "found" flag bolted on, so "the artifact of this map" and "where it lies" are
/// one object. The port has no <c>Artifact</c> class — an artifact is an <see cref="ArtifactType"/> — so this class
/// holds the id instead, and the map keeps it next to the tile index it was placed on.
/// </para>
/// <para>
/// <see cref="Offset"/> is not the artifact's position: it nudges the drawing of the puzzle map's central pieces by
/// up to two pixels either way, so that the hidden artifact is not always dead centre. The reference randomises it
/// once, in <see cref="Set"/>, and never touches it again; the port does the same.
/// </para>
/// <para>
/// <c>GetPuzzleMapSurface()</c> is left out: it renders a 16-piece puzzle from the map's tiles through
/// <c>Interface::GameArea</c>, which the port has not reached yet. Everything that decides the puzzle's answer —
/// which tile, which artifact, whether it was found — is here.
/// </para>
/// </remarks>
public sealed class UltimateArtifact
{
    /// <summary>
    /// The artifact the puzzle hides; <see cref="ArtifactType.Unknown"/> before the map places one.
    /// </summary>
    public ArtifactType Artifact { get; private set; } = ArtifactType.Unknown;

    /// <summary>
    /// The name of the artifact, <c>Artifact::GetName()</c> (<c>artifact.cpp:93-96</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference reads <c>fheroes2::getArtifactData( id ).untranslatedName</c>, the first column of the 104-row
    /// table in <c>artifact_info.cpp:47-150</c>. The port has no such column: <see cref="ArtifactTable"/> keeps the
    /// numbers only, and <c>ArtifactBag.String()</c> is unported for the same reason — nothing draws artifact names
    /// yet. The tavern's rumor about the ultimate artifact is the first place that does, so the names it can reach are
    /// here.
    /// </para>
    /// <para>
    /// That is a closed set: <see cref="Set"/> draws one of the eight ultimates when the map names none, and the map
    /// editor only offers "random ultimate", so <see cref="ArtifactType.Unknown"/> (a map with no puzzle) and the
    /// eight are all this property can be asked about. Anything else answers with the reference's own placeholder
    /// word, <c>"Invalid Artifact"</c>, rather than inventing a name the game would show wrongly.
    /// </para>
    /// </remarks>
    public string Name => Artifact switch
    {
        ArtifactType.UltimateBook => "Ultimate Book of Knowledge",
        ArtifactType.UltimateSword => "Ultimate Sword of Dominion",
        ArtifactType.UltimateCloak => "Ultimate Cloak of Protection",
        ArtifactType.UltimateWand => "Ultimate Wand of Magic",
        ArtifactType.UltimateShield => "Ultimate Shield",
        ArtifactType.UltimateStaff => "Ultimate Staff",
        ArtifactType.UltimateCrown => "Ultimate Crown",
        ArtifactType.GoldenGoose => "Golden Goose",
        _ => "Invalid Artifact",
    };

    /// <summary>
    /// The nudge of the puzzle drawing, <c>_offset</c>; each axis is at most two pixels.
    /// </summary>
    public Point Offset { get; private set; }

    /// <summary>
    /// The tile index of the artifact, <c>_index</c>; <c>-1</c> before the map places one.
    /// </summary>
    public int Position { get; private set; } = -1;

    /// <summary>Whether a hero has dug it up, <c>_isFound</c>.</summary>
    public bool IsFound { get; private set; }

    /// <summary>
    /// Whether this is the tile of the artifact, <c>UltimateArtifact::isPosition()</c>
    /// (<c>artifact_ultimate.h:38-41</c>).
    /// </summary>
    /// <remarks>
    /// The reference also requires <c>_index &gt;= 0</c>, so a map without an ultimate artifact does not answer yes
    /// for tile <c>-1</c>.
    /// </remarks>
    /// <param name="position">The tile index to compare.</param>
    /// <returns>Whether the artifact lies there.</returns>
    public bool IsAt(int position) => Position >= 0 && position == Position;

    /// <summary>
    /// Marks the artifact as dug up, <c>UltimateArtifact::markAsFound()</c>
    /// (<c>artifact_ultimate.h:53-56</c>).
    /// </summary>
    /// <remarks>
    /// Only the flag changes: the artifact is not removed from the map's object layer here, because the caller
    /// (the dig action) does that when it gives the artifact to the hero.
    /// </remarks>
    public void MarkAsFound() => IsFound = true;

    /// <summary>
    /// Places the artifact on a tile, <c>UltimateArtifact::Set()</c> (<c>artifact_ultimate.cpp:30-42</c>).
    /// </summary>
    /// <remarks>
    /// An invalid artifact — the usual case when the map says "give the map's ultimate artifact" without naming one
    /// — is replaced by a random ultimate, so a map always hides one of the eight. The random pick goes through
    /// <see cref="ArtifactRandom.Next"/> and therefore counts towards the map's "already given out" bookkeeping.
    /// </remarks>
    /// <param name="position">The tile index the artifact lies on.</param>
    /// <param name="artifact">The artifact, or an invalid id to pick a random ultimate.</param>
    /// <param name="version">Version of the map, which decides what "ultimate" means for the expansion.</param>
    public void Set(int position, ArtifactType artifact, MapVersion version)
    {
        Artifact = ArtifactTypes.IsValid(artifact) ? artifact : ArtifactRandom.Next(ArtifactLevel.Ultimate, version);
        Position = position;
        IsFound = false;

        // "The artifact should not always be shown right in the centre of the puzzle to make it harder to find,
        // but behind one of the last four central pieces": two draws, both inclusive, shifted to [-2, 2] and
        // [-1, 1].
        Offset = new Point((int)Rand.Get(0, 4) - 2, (int)Rand.Get(0, 2) - 1);
    }

    /// <summary>
    /// Forgets the artifact, <c>UltimateArtifact::Reset()</c> (<c>artifact_ultimate.cpp:49-56</c>).
    /// </summary>
    /// <remarks>
    /// The reference's <c>Artifact::Reset()</c> sets the id to <c>UNKNOWN</c> and the spell of a scroll to
    /// <c>NONE</c>; the port has no scroll spell to clear.
    /// </remarks>
    public void Reset()
    {
        Artifact = ArtifactType.Unknown;
        Offset = default;
        Position = -1;
        IsFound = false;
    }
}
