using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Picks the treasure a region can still afford, <c>getRandomTreasure()</c> and <c>convertMP2ToObjectInfo()</c>
/// (<c>map_random_generator_helper.cpp:365-406</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考先列一张「宝物池」：两个宝箱、两堆资源、一个篝火、三档随机宝物共八项；按区域还能花多少钱把池子滤一遍，
/// 再随机抽一个，最后换成「物件组 + 组内编号」交给落子。<see cref="TryConvert"/> 就是那个换算：在六张冒险物件表里
/// 找第一个类型相同的物件。
/// </para>
/// <para>
/// <b>为什么换算要固定表序</b>：同一类型可能在多张表里都有（例如宝物与能力点里都可能有同一件东西），参考按
/// 饰品→住处→矿→能力点→宝物→怪物 的顺序扫一遍，命中第一个就停。端口照抄这个顺序，否则同一张图会生成出不同的编号。
/// </para>
/// </remarks>
public static class RandomMapTreasureSelection
{
    /// <summary>The pool the reference picks from, <c>possibilities</c> (<c>:367-370</c>).</summary>
    /// <remarks>宝箱与资源各出现两次，所以它们被抽中的概率是别的宝物的两倍。</remarks>
    public static IReadOnlyList<Mp2ObjectType> Candidates { get; } =
    [
        Mp2ObjectType.TreasureChest,
        Mp2ObjectType.TreasureChest,
        Mp2ObjectType.RandomResource,
        Mp2ObjectType.RandomResource,
        Mp2ObjectType.Campfire,
        Mp2ObjectType.RandomArtifactTreasure,
        Mp2ObjectType.RandomArtifactMinor,
        Mp2ObjectType.RandomArtifactMajor,
    ];

    /// <summary>The groups the conversion scans, <c>limitedGroupList</c> (<c>:388-389</c>).</summary>
    private static readonly ObjectGroup[] LookupGroups =
    [
        ObjectGroup.AdventureArtifacts,
        ObjectGroup.AdventureDwellings,
        ObjectGroup.AdventureMines,
        ObjectGroup.AdventurePowerUps,
        ObjectGroup.AdventureTreasures,
        ObjectGroup.Monsters,
    ];

    /// <summary>Type to its first (group, index) pair, the <c>lookup</c> the reference caches.</summary>
    private static readonly Dictionary<Mp2ObjectType, (ObjectGroup Group, int Index)> Lookup = BuildLookup();

    /// <summary>
    /// Turns an object type into the group and index the map format uses, <c>convertMP2ToObjectInfo()</c>.
    /// </summary>
    /// <param name="objectType">The object type, e.g. <see cref="Mp2ObjectType.RandomResource"/>.</param>
    /// <param name="group">The group the first matching object belongs to.</param>
    /// <param name="index">Its index inside that group.</param>
    /// <returns><see langword="false"/> when none of the six groups holds such an object.</returns>
    /// <remarks>
    /// 端口的物件目录存的是带动作位的类型（<c>ActionTreasureChest</c>），而宝物池与估价表用的是不带动作位的写法；
    /// 查表前一律 <see cref="Mp2ObjectTypes.NonActionType"/> 归一，于是两种写法都能查。
    /// </remarks>
    public static bool TryConvert(Mp2ObjectType objectType, out ObjectGroup group, out int index)
    {
        if (Lookup.TryGetValue(Mp2ObjectTypes.NonActionType(objectType), out (ObjectGroup Group, int Index) entry))
        {
            group = entry.Group;
            index = entry.Index;

            return true;
        }

        group = ObjectGroup.None;
        index = -1;

        return false;
    }

    /// <summary>
    /// Picks a treasure worth no more than <paramref name="goldValueLimit"/>, <c>getRandomTreasure()</c>.
    /// </summary>
    /// <param name="goldValueLimit">What the region may still spend, in gold.</param>
    /// <param name="randomGenerator">The generator's random source.</param>
    /// <param name="placement">The object to drop, its offset always <c>(0, 0)</c>.</param>
    /// <returns><see langword="false"/> when nothing in the pool is cheap enough or the pick cannot be converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    public static bool TryPick(int goldValueLimit, Pcg32 randomGenerator, out RandomMapObjectPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(randomGenerator);

        placement = default;

        List<Mp2ObjectType> affordable = Candidates
            .Where(candidate => RandomMapObjectValues.GoldValue(candidate) <= goldValueLimit)
            .ToList();

        if (affordable.Count == 0)
        {
            return false;
        }

        Mp2ObjectType picked = affordable[randomGenerator.NextInclusive(0, affordable.Count - 1)];

        if (!TryConvert(picked, out ObjectGroup group, out int index))
        {
            return false;
        }

        placement = new RandomMapObjectPlacement(new Point(0, 0), group, index);

        return true;
    }

    private static Dictionary<Mp2ObjectType, (ObjectGroup Group, int Index)> BuildLookup()
    {
        Dictionary<Mp2ObjectType, (ObjectGroup Group, int Index)> lookup = [];

        foreach (ObjectGroup group in LookupGroups)
        {
            IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(group);

            for (int index = 0; index < objects.Count; ++index)
            {
                // 参考用的是 try_emplace：先出现的表赢，后面的同名不覆盖；类型按不带动作位的写法归一。
                lookup.TryAdd(Mp2ObjectTypes.NonActionType(objects[index].ObjectType), (group, index));
            }
        }

        return lookup;
    }
}
