using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The inverse of the artifact object's sprite index, <c>Artifact::getArtifactFromMapSpriteIndex()</c>
/// (<c>artifact.cpp:464-500</c>).
/// </summary>
/// <remarks>
/// <para>
/// A map of the old format stores an artifact object as a sprite index in its object layer rather than as an
/// artifact id, and the index has two interleaved halves: 162 and below are the Succession Wars artifacts at two
/// indices each (the shadow sprite follows the artifact sprite), 172 to 205 are the expansion artifacts the same
/// way, and 163 to 171 are the five "random artifact" objects the editor offered — their indices are odd because
/// each of them also takes two.
/// </para>
/// <para>
/// The reference asks the settings singleton for the map version and for whether the expansion is installed. The
/// port takes both as parameters, the way <see cref="FHeroes2.Assets.Icn.IcnCatalog.GetAnimatedIndex(Icn.IcnType,uint,uint,bool,bool)"/>
/// does, so the reader can call this without a settings layer.
/// </para>
/// </remarks>
public static class ArtifactMapSprites
{
    /// <summary>One past the last sprite index of the artifacts of the expansion, <c>index &lt; 206</c>.</summary>
    private const uint LastExpansionIndex = 206;

    /// <summary>The sprite index of the magic book, which only the Resurrection format has.</summary>
    private const uint MagicBookIndex = 207;

    /// <summary>
    /// The artifact an object sprite index stands for, <c>Artifact::getArtifactFromMapSpriteIndex()</c>.
    /// </summary>
    /// <remarks>
    /// The five random indices go through <see cref="ArtifactRandom.Next"/>, which is what makes a map's "random
    /// artifact" objects deal out different artifacts instead of the same one: the picker remembers what it has
    /// already given to this map.
    /// </remarks>
    /// <param name="index">The sprite index stored by the map.</param>
    /// <param name="version">Version of the map being read.</param>
    /// <param name="priceOfLoyaltySupported">
    /// Whether the installation has the expansion, <c>Settings::isPriceOfLoyaltySupported()</c>. Without it the
    /// expansion indices are not artifacts at all.
    /// </param>
    /// <returns>
    /// The artifact, <see cref="ArtifactType.Unknown"/> for an index no artifact object uses.
    /// </returns>
    public static ArtifactType FromMapIndex(uint index, MapVersion version, bool priceOfLoyaltySupported)
    {
        if (index < 162 || (priceOfLoyaltySupported && index > 171 && index < LastExpansionIndex))
        {
            // Add 1 to all values to convert from the old map format: two sprite indices per artifact.
            return (ArtifactType)(((int)index - 1) / 2 + 1);
        }

        // The original game has no magic book sprite on the map and stores it as the "dummy" one; the Resurrection
        // format gives it an index of its own.
        if (version == MapVersion.Resurrection && index == MagicBookIndex)
        {
            return ArtifactType.MagicBook;
        }

        return index switch
        {
            163 => ArtifactRandom.Next(ArtifactLevel.AllNormal, version),
            164 => ArtifactRandom.Next(ArtifactLevel.Ultimate, version),
            167 => ArtifactRandom.Next(ArtifactLevel.Treasure, version),
            169 => ArtifactRandom.Next(ArtifactLevel.Minor, version),
            171 => ArtifactRandom.Next(ArtifactLevel.Major, version),
            _ => ArtifactType.Unknown,
        };
    }
}
