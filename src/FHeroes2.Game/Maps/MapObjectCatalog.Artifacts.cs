using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// <c>populateAdventureArtifacts</c> of <c>src/fheroes2/maps/map_object_info.cpp</c>.
///
/// <para>
/// Not in the generated file: the reference builds every artifact with the <c>addArtifactObject</c>
/// lambda, which has no counterpart the one-line-per-line generator can produce. The lambda became a
/// local helper, everything else is copied statement by statement, and the loop bounds are the
/// <see cref="ArtifactType"/> members the reference uses.
/// </para>
///
/// <para>
/// <b>The object type is <c>MP2::OBJ_ARTIFACT</c>, i.e. the action value</b> (<c>mp2.h:313</c>), which is
/// <see cref="Mp2ObjectType.ActionArtifact"/> here; <see cref="Mp2ObjectType.Artifact"/> is the non-action
/// twin, which no map stores. The same holds for the random artifacts and the ultimate one — the
/// reference names <c>MP2::OBJ_RANDOM_ARTIFACT</c> and friends, never the <c>NON_ACTION</c> variants.
/// </para>
/// </summary>
public static partial class MapObjectCatalog
{
    /// <summary><c>populateAdventureArtifacts</c>.</summary>
    /// <param name="objects">Table of the group, must be empty.</param>
    private static void PopulateAdventureArtifacts(List<ObjectInfo> objects)
    {
        // All artifacts before Magic Book have their own images.
        // Magic Book is not present in the ICN resources but it is present in artifact IDs.

        for (uint artifactId = (uint)ArtifactType.ArcaneNecklace; artifactId < (uint)ArtifactType.MagicBook; ++artifactId)
        {
            AddArtifactObject(objects, artifactId, Mp2ObjectType.ActionArtifact, artifactId * 2U - 1U);
        }

        // Assign temporary sprites for the Magic Book.
        AddArtifactObject(objects, (uint)ArtifactType.MagicBook, Mp2ObjectType.ActionArtifact, 207U);

        for (uint artifactId = (uint)ArtifactType.SpellScroll; artifactId < (uint)ArtifactType.ArtifactCount; ++artifactId)
        {
            AddArtifactObject(objects, artifactId, Mp2ObjectType.ActionArtifact, artifactId * 2U - 1U);
        }

        // Random artifacts.
        AddArtifactObject(objects, (uint)ArtifactType.Unknown, Mp2ObjectType.ActionRandomArtifact, 163U);
        AddArtifactObject(objects, (uint)ArtifactType.Unknown, Mp2ObjectType.ActionRandomArtifactTreasure, 167U);
        AddArtifactObject(objects, (uint)ArtifactType.Unknown, Mp2ObjectType.ActionRandomArtifactMinor, 169U);
        AddArtifactObject(objects, (uint)ArtifactType.Unknown, Mp2ObjectType.ActionRandomArtifactMajor, 171U);

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionRandomUltimateArtifact);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnArti, 164U, new Point(0, 0), Mp2ObjectType.ActionRandomUltimateArtifact, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnArti, 165U, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objects.Add(objectInfo);
        }
    }

    /// <summary>The <c>addArtifactObject</c> lambda of the reference.</summary>
    /// <param name="objects">Table of the group that receives the object.</param>
    /// <param name="artifactId">Metadata, <c>Artifact::UNKNOWN</c> for the random ones.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="mainIcnIndex">Sprite of the artifact, its shadow follows the one before it.</param>
    private static void AddArtifactObject(List<ObjectInfo> objects, uint artifactId, Mp2ObjectType objectType, uint mainIcnIndex)
    {
        ObjectInfo objectInfo = new(objectType);
        objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnArti, mainIcnIndex, new Point(0, 0), objectType, ObjectLayerType.Object);
        objectInfo.Metadata[0] = artifactId;
        objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnArti, mainIcnIndex - 1U, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

        objects.Add(objectInfo);
    }
}
