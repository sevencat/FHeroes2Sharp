using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// One object of a prefab, <c>Maps::Random_Generator::ObjectPlacement</c>
/// (<c>map_random_generator_info.h:294-299</c>).
/// </summary>
/// <param name="Offset">Where the object stands, relative to the group's anchor tile.</param>
/// <param name="Group">Which catalogue it comes from.</param>
/// <param name="ObjectIndex">Its index inside that group.</param>
public readonly record struct RandomMapObjectPlacement(Point Offset, ObjectGroup Group, int ObjectIndex);

/// <summary>
/// A prefab of objects placed together, <c>Maps::Random_Generator::ObjectSet</c>
/// (<c>map_random_generator_info.h:307-312</c>).
/// </summary>
/// <param name="Obstacles">The blocking parts, the trees and mountains around the prize.</param>
/// <param name="Valuables">The prize itself: treasures or a power-up.</param>
/// <param name="EntranceCheck">
/// Tiles that have to stay open when the prefab is dropped, so the player can still reach the prize.
/// </param>
public sealed record RandomMapObjectSet(
    IReadOnlyList<RandomMapObjectPlacement> Obstacles,
    IReadOnlyList<RandomMapObjectPlacement> Valuables,
    IReadOnlyList<Point> EntranceCheck);

/// <summary>
/// The prefabs the random map generator drops into a region, <c>prefabObjectSets</c> and
/// <c>powerupObjectSets</c> (<c>map_random_generator.cpp:113-211</c>).
/// </summary>
/// <remarks>
/// <para>
/// 每个「宝物组」是四到五个格子的对称小景：三到四个宝物围成一圈，外圈用树或山封住，再留一条入口让玩家走进去。
/// 参考把入口的三个格子在 <see cref="RandomMapObjectSet.EntranceCheck"/> 里点出来，落子前要确认它们不是障碍。
/// </para>
/// <para>
/// 宝物用的是 <c>ADVENTURE_TREASURES</c> 第 9 号（宝箱类的那一档），能力点组把其中一个换成
/// <c>ADVENTURE_POWER_UPS</c> 的不同编号（10 / 18 / 12 / 13 / 16），所以「宝物组」与「能力点组」的骨架一样、
/// 奖品不同。坐标与编号照参考原样抄，改动它们等于改动地图的手感。
/// </para>
/// </remarks>
public static class RandomMapObjectSets
{
    /// <summary>The index of the treasure used by every prefab, the <c>9</c> of the reference.</summary>
    public const int TreasureIndex = 9;

    /// <summary>The groups a prefab may put on the map.</summary>
    private static readonly ObjectGroup[] GroupsUsed =
    [
        ObjectGroup.LandscapeTrees,
        ObjectGroup.LandscapeMountains,
        ObjectGroup.AdventureTreasures,
        ObjectGroup.AdventurePowerUps,
    ];

    /// <summary>The five treasure prefabs, <c>prefabObjectSets</c>.</summary>
    public static IReadOnlyList<RandomMapObjectSet> Prefabs { get; } =
    [
        Set(
            obstacles:
            [
                At(0, -1, ObjectGroup.LandscapeTrees, 3),
                At(3, -1, ObjectGroup.LandscapeTrees, 2),
                At(3, 1, ObjectGroup.LandscapeTrees, 3),
            ],
            valuables:
            [
                At(1, -1, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, -1, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(-1, 0), P(-1, 1), P(0, 1)]),
        Set(
            obstacles:
            [
                At(-3, 0, ObjectGroup.LandscapeTrees, 3),
                At(-3, 1, ObjectGroup.LandscapeTrees, 0),
                At(0, 2, ObjectGroup.LandscapeTrees, 3),
            ],
            valuables:
            [
                At(-2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-2, 1, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-1, 1, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(0, -1), P(1, -1), P(1, 0)]),
        Set(
            obstacles:
            [
                At(3, -1, ObjectGroup.LandscapeTrees, 0),
                At(3, 1, ObjectGroup.LandscapeTrees, 1),
                At(1, 2, ObjectGroup.LandscapeTrees, 5),
                At(0, 2, ObjectGroup.LandscapeTrees, 4),
            ],
            valuables:
            [
                At(1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(1, 1, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 1, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(-1, -1), P(-1, 0), P(-1, 1), P(0, -1)]),
        Set(
            obstacles:
            [
                At(-3, -1, ObjectGroup.LandscapeTrees, 1),
                At(-1, 2, ObjectGroup.LandscapeTrees, 4),
                At(-3, 1, ObjectGroup.LandscapeTrees, 0),
                At(0, 2, ObjectGroup.LandscapeTrees, 5),
            ],
            valuables:
            [
                At(-1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-1, 1, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-2, 1, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(1, -1), P(1, 0), P(1, 1), P(0, -1)]),
        Set(
            obstacles:
            [
                At(-3, 0, ObjectGroup.LandscapeTrees, 3),
                At(-1, -1, ObjectGroup.LandscapeTrees, 5),
                At(-3, 1, ObjectGroup.LandscapeTrees, 2),
            ],
            valuables:
            [
                At(-2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(1, -1), P(1, 0), P(1, 1), P(0, -1), P(0, 1)]),
    ];

    /// <summary>The five power-up prefabs, <c>powerupObjectSets</c>.</summary>
    public static IReadOnlyList<RandomMapObjectSet> PowerUps { get; } =
    [
        Set(
            obstacles:
            [
                At(0, -1, ObjectGroup.LandscapeTrees, 3),
                At(3, -1, ObjectGroup.LandscapeTrees, 2),
                At(3, 1, ObjectGroup.LandscapeTrees, 3),
            ],
            valuables:
            [
                At(2, -1, ObjectGroup.AdventurePowerUps, 10),
                At(1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(-1, 0), P(-1, 1), P(0, 1)]),
        Set(
            obstacles:
            [
                At(0, -1, ObjectGroup.LandscapeTrees, 3),
                At(3, -1, ObjectGroup.LandscapeTrees, 2),
                At(3, 1, ObjectGroup.LandscapeTrees, 3),
            ],
            valuables:
            [
                At(1, -1, ObjectGroup.AdventurePowerUps, 18),
                At(1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(-1, 0), P(-1, 1), P(0, 1)]),
        Set(
            obstacles:
            [
                At(1, -2, ObjectGroup.LandscapeTrees, 1),
                At(3, -1, ObjectGroup.LandscapeTrees, 2),
                At(3, 1, ObjectGroup.LandscapeTrees, 3),
            ],
            valuables:
            [
                At(2, -1, ObjectGroup.AdventurePowerUps, 12),
                At(1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(-1, 0), P(-1, 1), P(0, 1)]),
        Set(
            obstacles:
            [
                At(0, -1, ObjectGroup.LandscapeTrees, 3),
                At(3, -1, ObjectGroup.LandscapeTrees, 2),
                At(3, 1, ObjectGroup.LandscapeTrees, 3),
            ],
            valuables:
            [
                At(1, -1, ObjectGroup.AdventurePowerUps, 13),
                At(1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(-1, 0), P(-1, 1), P(0, 1)]),
        Set(
            obstacles:
            [
                At(-3, -1, ObjectGroup.LandscapeMountains, 5),
                At(-1, -1, ObjectGroup.LandscapeMountains, 4),
                At(-3, 0, ObjectGroup.LandscapeTrees, 0),
            ],
            valuables:
            [
                At(-2, -1, ObjectGroup.AdventurePowerUps, 16),
                At(-1, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
                At(-2, 0, ObjectGroup.AdventureTreasures, TreasureIndex),
            ],
            entrance: [P(1, 0), P(-1, 1), P(0, 1), P(1, 1)]),
    ];

    /// <summary>Whether a group is one the prefabs are allowed to use.</summary>
    /// <param name="group">The group to check.</param>
    /// <returns><see langword="true"/> for trees, mountains, treasures and power-ups.</returns>
    public static bool IsPrefabGroup(ObjectGroup group) => Array.IndexOf(GroupsUsed, group) >= 0;

    private static RandomMapObjectSet Set(
        IReadOnlyList<RandomMapObjectPlacement> obstacles,
        IReadOnlyList<RandomMapObjectPlacement> valuables,
        IReadOnlyList<Point> entrance) => new(obstacles, valuables, entrance);

    private static RandomMapObjectPlacement At(int x, int y, ObjectGroup group, int index) =>
        new(new Point(x, y), group, index);

    private static Point P(int x, int y) => new(x, y);
}
