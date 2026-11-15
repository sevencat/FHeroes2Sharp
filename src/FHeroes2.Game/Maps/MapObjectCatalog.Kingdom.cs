using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// <c>populateKingdomTowns</c> of <c>src/fheroes2/maps/map_object_info.cpp</c>.
///
/// <para>
/// Not in the generated file: the reference builds every town with the <c>addTown</c> lambda, and a
/// lambda has no counterpart that the one-line-per-line generator can produce. The lambda became a
/// local helper, everything else is copied statement by statement, and the order of the calls is the
/// order of the table.
/// </para>
/// </summary>
public static partial class MapObjectCatalog
{
    /// <summary><c>populateKingdomTowns</c>: six races, castle and town each, plus the two random ones.</summary>
    /// <param name="objects">Table of the group, must be empty.</param>
    private static void PopulateKingdomTowns(List<ObjectInfo> objects)
    {
        // Castle and town sprites: Knight, Barbarian, Sorceress, Warlock, Wizard, Necromancer.
        // First goes castle, then town, then the next race.
        for (uint i = 0; i < 6 * 2; ++i)
        {
            uint icnOffset = i * 16;
            uint race = i / 2;
            bool isCastle = (i % 2) == 0;

            AddTown(objects, Mp2ObjectType.Castle, race, icnOffset, icnOffset, Mp2ObjectIcnType.ObjnTown, Mp2ObjectIcnType.ObjnTwsh, isCastle);
        }

        // Random castle/town.
        AddTown(objects, Mp2ObjectType.RandomCastle, 7, 0, 32, Mp2ObjectIcnType.ObjnTwrd, Mp2ObjectIcnType.ObjnTwrd, true);
        AddTown(objects, Mp2ObjectType.RandomTown, 7, 16, 16 + 32, Mp2ObjectIcnType.ObjnTwrd, Mp2ObjectIcnType.ObjnTwrd, false);
    }

    /// <summary>The <c>addTown</c> lambda of the reference.</summary>
    /// <param name="objects">Table of the group that receives the object.</param>
    /// <param name="mainObjectType">Action type of the town, <c>OBJ_CASTLE</c> and so on.</param>
    /// <param name="race">0 to 5 for a race, 7 for the random town.</param>
    /// <param name="townIcnOffset">Sprite offset of the town inside <paramref name="townIcnType"/>.</param>
    /// <param name="shadowIcnOffset">Sprite offset of the shadow inside <paramref name="shadowIcnType"/>.</param>
    /// <param name="townIcnType">ICN of the town.</param>
    /// <param name="shadowIcnType">ICN of the shadow.</param>
    /// <param name="isCastle">A castle has a fortification, a town has not.</param>
    private static void AddTown(List<ObjectInfo> objects, Mp2ObjectType mainObjectType, uint race, uint townIcnOffset, uint shadowIcnOffset,
                                Mp2ObjectIcnType townIcnType, Mp2ObjectIcnType shadowIcnType, bool isCastle)
    {
        // MP2::isOffGameActionObject( mainObjectType ) holds for every type the lambda is called with.
        Mp2ObjectType secondaryObjectType = (Mp2ObjectType)((int)mainObjectType - (int)Mp2ObjectType.ActionObjectType);

        ObjectInfo objectInfo = new(mainObjectType);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 13, new Point(0, 0), mainObjectType, ObjectLayerType.Object);

        // Castle/town shadow.
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 13, new Point(-3, 1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 14, new Point(-2, 1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 15, new Point(-1, 1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 9, new Point(-4, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 10, new Point(-3, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 11, new Point(-2, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 12, new Point(-1, 0), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 4, new Point(-5, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 5, new Point(-4, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 6, new Point(-3, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 7, new Point(-2, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 8, new Point(-1, -1), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 0, new Point(-4, -2), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 1, new Point(-3, -2), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 2, new Point(-2, -2), Mp2ObjectType.None, ObjectLayerType.Shadow);
        objectInfo.AddGroundPart(shadowIcnType, shadowIcnOffset + 3, new Point(-1, -2), Mp2ObjectType.None, ObjectLayerType.Shadow);

        // Castle/town main sprite.
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 11, new Point(-2, 0), secondaryObjectType, ObjectLayerType.Object);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 12, new Point(-1, 0), secondaryObjectType, ObjectLayerType.Object);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 14, new Point(1, 0), secondaryObjectType, ObjectLayerType.Object);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 15, new Point(2, 0), secondaryObjectType, ObjectLayerType.Object);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 7, new Point(-1, -1), secondaryObjectType, ObjectLayerType.Object);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 8, new Point(0, -1), secondaryObjectType, ObjectLayerType.Object);
        objectInfo.AddGroundPart(townIcnType, townIcnOffset + 9, new Point(1, -1), secondaryObjectType, ObjectLayerType.Object);

        objectInfo.AddTopPart(townIcnType, townIcnOffset + 6, new Point(-2, -1), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 10, new Point(2, -1), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 1, new Point(-2, -2), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 2, new Point(-1, -2), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 3, new Point(0, -2), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 4, new Point(1, -2), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 5, new Point(2, -2), secondaryObjectType);
        objectInfo.AddTopPart(townIcnType, townIcnOffset + 0, new Point(0, -3), secondaryObjectType);

        objectInfo.Metadata[0] = race;
        objectInfo.Metadata[1] = isCastle ? 1u : 0u;

        objects.Add(objectInfo);
    }
}
