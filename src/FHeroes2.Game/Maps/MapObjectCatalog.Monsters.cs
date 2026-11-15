using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Maps;

/// <summary>
/// <c>populateMonsters</c> of <c>src/fheroes2/maps/map_object_info.cpp</c>.
///
/// <para>
/// Not in the generated file: the reference walks the <c>Monster</c> enumeration, which the generator
/// does not translate yet. The ids are the <see cref="MonsterType"/> members the reference names, in
/// the same order, so only the spelling of the members differs.
/// </para>
/// </summary>
public static partial class MapObjectCatalog
{
    /// <summary><c>populateMonsters</c>: one object per creature, then the five random ones.</summary>
    /// <param name="objects">Table of the group, must be empty.</param>
    private static void PopulateMonsters(List<ObjectInfo> objects)
    {
        // Monsters are "unique" objects in terms of their ICN resources.
        // The Editor uses ICN::MON32 while the Adventure Map renderer uses modified ICN::MINIMON resources.
        for (int monsterId = (int)MonsterType.Peasant; monsterId <= (int)MonsterType.WaterElement; ++monsterId)
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.Monster);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)(monsterId - 1), new Point(0, 0), Mp2ObjectType.Monster, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)monsterId;

            objects.Add(objectInfo);
        }

        // Random monsters.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.RandomMonster);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonster - 1U, new Point(0, 0), Mp2ObjectType.RandomMonster, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonster;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.RandomMonsterWeak);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel1 - 1U, new Point(0, 0), Mp2ObjectType.RandomMonsterWeak, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel1;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.RandomMonsterMedium);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel2 - 1U, new Point(0, 0), Mp2ObjectType.RandomMonsterMedium, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel2;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.RandomMonsterStrong);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel3 - 1U, new Point(0, 0), Mp2ObjectType.RandomMonsterStrong, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel3;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.RandomMonsterVeryStrong);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel4 - 1U, new Point(0, 0), Mp2ObjectType.RandomMonsterVeryStrong, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel4;

            objects.Add(objectInfo);
        }
    }
}
