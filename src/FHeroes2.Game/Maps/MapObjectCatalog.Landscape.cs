using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The landscape part of the object tables: streams and trees.
///
/// <para>
/// <c>参考：src/fheroes2/maps/map_object_info.cpp:1113</c> (<c>populateStreams</c>) and
/// <c>:1891</c> (<c>populateLandscapeTrees</c>). Each block below corresponds to one block of the
/// reference in the same order; the order inside a group is part of the map format.
/// </para>
/// </summary>
public static partial class MapObjectCatalog
{
    /// <summary>
    /// Thirteen stream sprites, <c>populateStreams</c>. A stream has no direction combinations: the
    /// thirteen images cover every ending, crossing and corner.
    /// </summary>
    /// <param name="objects">Table of the stream group, must be empty.</param>
    private static void PopulateStreams(List<ObjectInfo> objects)
    {
        for (uint i = 0; i < 13; ++i)
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.None);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Stream, i, new Point(0, 0), Mp2ObjectType.None, ObjectLayerType.Terrain);

            objects.Add(objectInfo);
        }
    }

    /// <summary>
    /// Every tree, stump and log of the game, <c>populateLandscapeTrees</c>.
    ///
    /// <para>
    /// Forests have different appearances: deciduous, evil (dead), autumn, fir, jungle and snowy fir.
    /// Six ICN resources share the same layout of six objects each; the seven terrain-specific
    /// resources (<c>OBJNGRAS</c>, <c>OBJNSNOW</c>, …) follow one block per resource, as in the
    /// reference.
    /// </para>
    /// </summary>
    /// <param name="objects">Table of the landscape tree group, must be empty.</param>
    private static void PopulateLandscapeTrees(List<ObjectInfo> objects)
    {
        // Forests have different appearances: deciduous, evil (dead), autumn, fir, jungle and snowy fir.
        foreach (Mp2ObjectIcnType type in new[]
                 {
                     Mp2ObjectIcnType.TreDeci, Mp2ObjectIcnType.TreVil, Mp2ObjectIcnType.TreFall,
                     Mp2ObjectIcnType.TreFir, Mp2ObjectIcnType.TreJngl, Mp2ObjectIcnType.TreSnow,
                 })
        {
            // Big forest from top-left to bottom-right.
            {
                ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
                objectInfo.AddGroundPart(type, 5, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 4, new Point(-1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 8, new Point(0, 1), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 9, new Point(1, 1), Mp2ObjectType.Trees, ObjectLayerType.Object);

                objectInfo.AddGroundPart(type, 0, new Point(-2, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
                objectInfo.AddGroundPart(type, 3, new Point(-2, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);
                objectInfo.AddGroundPart(type, 7, new Point(-1, 1), Mp2ObjectType.None, ObjectLayerType.Shadow);

                objectInfo.AddTopPart(type, 1, new Point(-1, -1), Mp2ObjectType.Trees);
                objectInfo.AddTopPart(type, 2, new Point(0, -1), Mp2ObjectType.Trees);
                objectInfo.AddTopPart(type, 6, new Point(1, 0), Mp2ObjectType.Trees);

                objects.Add(objectInfo);
            }

            // Big forest from top-right to bottom-left.
            {
                ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
                objectInfo.AddGroundPart(type, 15, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 16, new Point(1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 18, new Point(-1, 1), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 19, new Point(0, 1), Mp2ObjectType.Trees, ObjectLayerType.Object);

                objectInfo.AddGroundPart(type, 10, new Point(-1, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
                objectInfo.AddGroundPart(type, 13, new Point(-2, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);
                objectInfo.AddGroundPart(type, 17, new Point(-2, 1), Mp2ObjectType.None, ObjectLayerType.Shadow);

                objectInfo.AddTopPart(type, 11, new Point(0, -1), Mp2ObjectType.Trees);
                objectInfo.AddTopPart(type, 12, new Point(1, -1), Mp2ObjectType.Trees);
                objectInfo.AddTopPart(type, 14, new Point(-1, 0), Mp2ObjectType.Trees);

                objects.Add(objectInfo);
            }

            // Medium forest from top-left to bottom-right.
            {
                ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
                objectInfo.AddGroundPart(type, 24, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 21, new Point(-1, -1), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 22, new Point(0, -1), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 25, new Point(1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

                objectInfo.AddGroundPart(type, 20, new Point(-2, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
                objectInfo.AddGroundPart(type, 23, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

                objects.Add(objectInfo);
            }

            // Medium forest from top-right to bottom-left.
            {
                ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
                objectInfo.AddGroundPart(type, 31, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 27, new Point(0, -1), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 28, new Point(1, -1), Mp2ObjectType.Trees, ObjectLayerType.Object);
                objectInfo.AddGroundPart(type, 30, new Point(-1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

                objectInfo.AddGroundPart(type, 26, new Point(-1, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
                objectInfo.AddGroundPart(type, 29, new Point(-2, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

                objects.Add(objectInfo);
            }

            // Small forest (2 variants).
            for (uint count = 0; count < 2; ++count)
            {
                uint offset = count * 2U + 32U;
                ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
                objectInfo.AddGroundPart(type, offset + 1U, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

                objectInfo.AddGroundPart(type, offset + 0U, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

                objects.Add(objectInfo);
            }
        }

        // Three trees, grass terrain, variant 1.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 84, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 83, new Point(-1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 85, new Point(1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 82, new Point(-2, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnGras, 79, new Point(-1, -1), Mp2ObjectType.Trees);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnGras, 80, new Point(0, -1), Mp2ObjectType.Trees);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnGras, 81, new Point(1, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Three trees, grass terrain, variant 2.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 89, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 90, new Point(1, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 88, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnGras, 86, new Point(0, -1), Mp2ObjectType.Trees);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnGras, 87, new Point(1, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Single tree, grass terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 93, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnGras, 92, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnGras, 91, new Point(0, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Two stumps, snow terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Stump);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 41, new Point(0, 0), Mp2ObjectType.Stump, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 40, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objects.Add(objectInfo);
        }

        // Single stump, snow terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Stump);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 42, new Point(0, 0), Mp2ObjectType.Stump, ObjectLayerType.Object);

            objects.Add(objectInfo);
        }

        // Dead tree (medium, wide), snow terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 49, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 50, new Point(1, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 48, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 46, new Point(0, -1), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 47, new Point(1, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Dead tree (tall, wide), snow terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 56, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 55, new Point(-1, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 57, new Point(1, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 54, new Point(-2, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 51, new Point(-1, -1), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 52, new Point(0, -1), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 53, new Point(1, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Dead tree (medium, 1-tile), snow terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 60, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 59, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 58, new Point(0, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Dead tree (medium, thinned, wide), snow terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 64, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 65, new Point(1, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, 63, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 61, new Point(0, -1), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, 62, new Point(1, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Dead trees (1-tile), snow terrain. 5 variants.
        for (uint count = 0; count < 5; ++count)
        {
            uint offset = 66U + count * 3U;

            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, offset + 2U, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSnow, offset + 1U, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSnow, offset + 0U, new Point(0, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Dead tree (tall in the pool), swamp terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSwmp, 167, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSwmp, 166, new Point(-1, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSwmp, 163, new Point(-2, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSwmp, 161, new Point(-1, -2), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSwmp, 162, new Point(0, -2), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSwmp, 164, new Point(-1, -1), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSwmp, 165, new Point(0, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Dead trees (2-tile), swamp terrain. 2 variants.
        for (uint count = 0; count < 2; ++count)
        {
            uint offset = 168U + count * 5U;

            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSwmp, offset + 3U, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSwmp, offset + 4U, new Point(1, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnSwmp, offset + 2U, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSwmp, offset + 0U, new Point(0, -1), Mp2ObjectType.DeadTree);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnSwmp, offset + 1U, new Point(1, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Two tall palms, desert terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDsrt, 3, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            // The next tile contents a shadow and the upper part of the tree. How to render it properly? What layer is better to use?
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDsrt, 2, new Point(-1, 0), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Tall palms, desert terrain. 3 variants.
        for (uint count = 0; count < 3; ++count)
        {
            uint offset = 4U + count * 3U;

            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDsrt, offset + 2U, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            // The next tile contents a shadow and the upper part of the tree. How to render it properly? What layer is better to use?
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDsrt, offset + 1U, new Point(-1, 0), Mp2ObjectType.Trees);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDsrt, offset + 0U, new Point(0, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // A lonely palm.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDsrt, 76, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDsrt, 75, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDsrt, 74, new Point(0, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Small palms, desert terrain. 3 variants.
        for (uint count = 0; count < 3; ++count)
        {
            uint offset = 23U + count * 2U;

            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDsrt, offset + 1U, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDsrt, offset + 0U, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objects.Add(objectInfo);
        }

        // Tall single tree, dirt terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDirt, 118, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDirt, 117, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 116, new Point(1, -1), Mp2ObjectType.Trees);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 115, new Point(0, -1), Mp2ObjectType.Trees);

            // The next tile contents the upper part of the tree. It is passable but should be rendered before the hero on this tile.
            // How to render it properly? What layer is better to use?
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 119, new Point(1, 0), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Two tall trees, dirt terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDirt, 123, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            // The next tile contents a shadow and the upper part of the tree. How to render it properly? What layer is better to use?
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 122, new Point(-1, 0), Mp2ObjectType.Trees);

            // The next tile contents the upper part of the tree. It is passable but should be rendered before the hero on this tile.
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 124, new Point(1, 0), Mp2ObjectType.Trees);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 120, new Point(0, -1), Mp2ObjectType.Trees);
            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 121, new Point(1, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Two medium trees, dirt terrain.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Trees);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDirt, 127, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnDirt, 126, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnDirt, 125, new Point(0, -1), Mp2ObjectType.Trees);

            objects.Add(objectInfo);
        }

        // Dead tree, generic.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMult, 2, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMult, 1, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objectInfo.AddTopPart(Mp2ObjectIcnType.ObjnMult, 0, new Point(0, -1), Mp2ObjectType.DeadTree);

            objects.Add(objectInfo);
        }

        // Log (dead tree), generic.
        {
            // We use Mp2ObjectType.DeadTree instead of Mp2ObjectType.NothingSpecial used by the original editor.
            ObjectInfo objectInfo = new(Mp2ObjectType.DeadTree);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMult, 4, new Point(0, 0), Mp2ObjectType.DeadTree, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMult, 3, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objects.Add(objectInfo);
        }

        // Three stumps, generic.
        {
            // We use Mp2ObjectType.Stump instead of Mp2ObjectType.DeadTree used by the original editor.
            ObjectInfo objectInfo = new(Mp2ObjectType.Stump);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMul2, 16, new Point(0, 0), Mp2ObjectType.Stump, ObjectLayerType.Object);

            objects.Add(objectInfo);
        }

        // Two stumps, generic.
        {
            // We use Mp2ObjectType.Stump instead of Mp2ObjectType.DeadTree used by the original editor.
            ObjectInfo objectInfo = new(Mp2ObjectType.Stump);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMul2, 18, new Point(0, 0), Mp2ObjectType.Stump, ObjectLayerType.Object);

            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMul2, 17, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);

            objects.Add(objectInfo);
        }

        // Single stump, generic.
        {
            // We use Mp2ObjectType.Stump instead of Mp2ObjectType.DeadTree used by the original editor.
            ObjectInfo objectInfo = new(Mp2ObjectType.Stump);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.ObjnMul2, 19, new Point(0, 0), Mp2ObjectType.Stump, ObjectLayerType.Object);

            objects.Add(objectInfo);
        }
    }
}
