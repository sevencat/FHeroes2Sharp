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
///
/// <para>
/// <b>The object type is <c>MP2::OBJ_MONSTER</c>, i.e. the action value</b> (<c>mp2.h:296</c>), which is
/// <see cref="Mp2ObjectType.ActionMonster"/> here; <see cref="Mp2ObjectType.Monster"/> is its non-action
/// twin and no map ever stores it. The same holds for the five random placeholders.
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
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionMonster);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)(monsterId - 1), new Point(0, 0), Mp2ObjectType.ActionMonster, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)monsterId;

            objects.Add(objectInfo);
        }

        // Random monsters.
        {
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionRandomMonster);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonster - 1U, new Point(0, 0), Mp2ObjectType.ActionRandomMonster, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonster;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionRandomMonsterWeak);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel1 - 1U, new Point(0, 0), Mp2ObjectType.ActionRandomMonsterWeak, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel1;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionRandomMonsterMedium);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel2 - 1U, new Point(0, 0), Mp2ObjectType.ActionRandomMonsterMedium, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel2;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionRandomMonsterStrong);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel3 - 1U, new Point(0, 0), Mp2ObjectType.ActionRandomMonsterStrong, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel3;

            objects.Add(objectInfo);
        }

        {
            ObjectInfo objectInfo = new(Mp2ObjectType.ActionRandomMonsterVeryStrong);
            objectInfo.AddGroundPart(Mp2ObjectIcnType.Mons32, (uint)MonsterType.RandomMonsterLevel4 - 1U, new Point(0, 0), Mp2ObjectType.ActionRandomMonsterVeryStrong, ObjectLayerType.Object);
            objectInfo.Metadata[0] = (uint)MonsterType.RandomMonsterLevel4;

            objects.Add(objectInfo);
        }
    }
}
