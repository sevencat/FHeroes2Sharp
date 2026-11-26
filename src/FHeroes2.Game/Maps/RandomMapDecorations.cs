using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// A decorative scene dropped into the empty corners of a region, the <c>DecorationSet</c> of
/// <c>map_random_generator.cpp:213-549</c>.
/// </summary>
/// <param name="Obstacles">The blocks that always go down, the body of the scene.</param>
/// <param name="Optional">The small extra pieces, tried one by one with a coin toss after the body landed.</param>
public sealed record RandomMapDecorationSet(
    IReadOnlyList<RandomMapObjectPlacement> Obstacles,
    IReadOnlyList<RandomMapObjectPlacement> Optional);

/// <summary>
/// Which decorative block a ground uses, <c>treeTypeFromGroundType()</c> and <c>mountainTypeFromGroundType()</c>
/// (<c>map_random_generator_helper.cpp:128-186</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把装饰物按地形分块：草地用第 0 块树、雪地用第 30 块、沙漠用第 24 块……这些数字就是物件表里
/// <c>LANDSCAPE_TREES</c> / <c>LANDSCAPE_MOUNTAINS</c> 的起点，落装饰物时拿它当「同地形那一套」的偏移。
/// </para>
/// <para>
/// 水面没有装饰物（参考在这里断言并返回 0）；沼泽与草地共用第 0 块树，沙滩与沙漠共用第 24 块树。山在沙滩上是第 0 块、
/// 荒野上跳到第 44 块——照抄，改动它们等于改动地图的样子。
/// </para>
/// </remarks>
public static class RandomMapDecorations
{
    /// <summary>The tree block of a ground, <c>treeTypeFromGroundType()</c>.</summary>
    /// <param name="ground">The ground a decoration stands on.</param>
    /// <returns>The first index of that ground's tree block, <c>0</c> for water and unknown grounds.</returns>
    public static int TreeIndexFor(MapGround ground) => ground switch
    {
        MapGround.Grass or MapGround.Swamp => 0,
        MapGround.Snow => 30,
        MapGround.Lava => 6,
        MapGround.Desert or MapGround.Beach => 24,
        MapGround.Dirt => 18,
        MapGround.Wasteland => 12,
        _ => 0,
    };

    /// <summary>The mountain block of a ground, <c>mountainTypeFromGroundType()</c>.</summary>
    /// <param name="ground">The ground a decoration stands on.</param>
    /// <returns>The first index of that ground's mountain block, <c>0</c> for water and unknown grounds.</returns>
    public static int MountainIndexFor(MapGround ground) => ground switch
    {
        MapGround.Grass => 6,
        MapGround.Snow => 12,
        MapGround.Swamp => 18,
        MapGround.Lava => 24,
        MapGround.Desert => 30,
        MapGround.Dirt => 36,
        MapGround.Wasteland => 44,
        _ => 0,
    };

    /// <summary>
    /// The trees a border of a ground is decorated with, <c>obstaclesPerGround</c>
    /// (<c>map_random_generator_helper.cpp:68-72</c>).
    /// </summary>
    /// <param name="ground">The ground of the region's edge.</param>
    /// <returns>The six indices of that ground's tree block; empty for water.</returns>
    /// <remarks>
    /// 与 <see cref="TreeIndexFor"/> 是同一套分块：草地 0-5、熔岩 6-11、荒野 12-17、沼泽与泥土 18-23、
    /// 沙漠与沙滩 24-29、雪地 30-35。参考在边框上随机挑一棵，所以这六个数就是「这个地形能长什么树」。
    /// </remarks>
    public static IReadOnlyList<int> ObstacleIndicesFor(MapGround ground) => ground switch
    {
        MapGround.Grass => [0, 1, 2, 3, 4, 5],
        MapGround.Lava => [6, 7, 8, 9, 10, 11],
        MapGround.Wasteland => [12, 13, 14, 15, 16, 17],
        MapGround.Swamp or MapGround.Dirt => [18, 19, 20, 21, 22, 23],
        MapGround.Desert or MapGround.Beach => [24, 25, 26, 27, 28, 29],
        MapGround.Snow => [30, 31, 32, 33, 34, 35],
        _ => [],
    };

    /// <summary>
    /// The decorations every land region gets, the <c>Maps::Ground::ALL</c> entry of <c>decorationsPerGround</c>
    /// (<c>map_random_generator.cpp:214-279</c>).
    /// </summary>
    /// <remarks>
    /// 七组「山与树的骨架」，与地形无关：大山十字、双峰、纵向山链、两组小丘、横向树列、Y 形树丛。
    /// 参考对每个非水面的区域都再放一遍这一套，与它自己地形的装饰叠加，所以同一种地形里的空地不会只长一种东西。
    /// </remarks>
    public static IReadOnlyList<RandomMapDecorationSet> Common { get; } =
    [
        Set(
            obstacles: [Mountain(0, 0, 1), Mountain(-2, 2, 1), Mountain(-3, 0, 4), Mountain(1, 2, 2)],
            optional: []),
        Set(
            obstacles: [Mountain(0, 0, 0), Mountain(-3, 1, 1), Tree(-2, 2, 3)],
            optional: []),
        Set(
            obstacles: [Mountain(0, 0, 2), Mountain(0, 2, 3), Mountain(0, 4, 2)],
            optional: [Tree(-1, 0, 0), Tree(1, 3, 0), Tree(-1, 4, 0)]),
        Set(
            obstacles: [Mountain(0, 0, 2), Mountain(1, 1, 3), Mountain(-1, 1, 5)],
            optional: []),
        Set(
            obstacles: [Mountain(0, 0, 4), Mountain(-1, 1, 5), Tree(0, 1, 4)],
            optional: []),
        Set(
            obstacles: [Tree(-2, 0, 3), Tree(0, 0, 2), Tree(2, -1, 1), Tree(4, 0, 2)],
            optional: [Tree(-2, -1, 0)]),
        Set(
            obstacles: [Tree(0, 0, 0), Tree(-1, 1, 1)],
            optional: []),
    ];

    /// <summary>
    /// The decorations of one ground, <c>decorationsPerGround.at( ground )</c> (<c>:280-548</c>).
    /// </summary>
    /// <param name="ground">The ground of the region.</param>
    /// <returns>Its scenes; empty for water, desert and grounds the reference gives none.</returns>
    /// <remarks>
    /// 沙漠与水面在参考里没有条目——沙漠区只拿 <see cref="Common"/> 那一套，正好是「荒漠空得多」的意思。
    /// </remarks>
    public static IReadOnlyList<RandomMapDecorationSet> For(MapGround ground) => ground switch
    {
        MapGround.Grass =>
        [
            Set(
                obstacles:
                [
                    Misc(0, 0, 4), Mountain(-2, -1, 5), Mountain(0, -2, 5), Tree(1, -3, 0),
                    Tree(3, -2, 0), Tree(4, -1, 0), Tree(-3, -1, 3), Misc(2, 0, 14), Rocks(-2, 0, 19),
                ],
                optional: []),
            Set(
                obstacles: [Misc(0, 0, 5), Tree(-3, -1, 1), Tree(1, -1, 2), Tree(-1, -2, 3), Misc(0, 0, 14), Tree(0, 0, 4)],
                optional: []),
        ],
        MapGround.Snow =>
        [
            Set(
                obstacles:
                [
                    Misc(0, 0, 37), Tree(-1, 0, 0), Tree(1, 1, 0), Tree(-2, 2, 1),
                    Tree(0, 3, 2), Tree(1, 0, 3), Rocks(2, 0, 12),
                ],
                optional: []),
            Set(
                obstacles: [Misc(0, 0, 37), Tree(-1, -1, 41), Tree(2, 0, 2), Tree(-1, 0, 39)],
                optional: []),
            Set(
                obstacles: [Misc(0, 0, 38), Tree(2, -1, 0), Misc(1, 1, 33)],
                optional: []),
        ],
        MapGround.Swamp =>
        [
            Set(
                obstacles: [Misc(0, 0, 43), Tree(1, -1, 0), Tree(-2, 1, 50), Tree(0, 1, 4)],
                optional: []),
            Set(
                obstacles: [Misc(0, 0, 44), Misc(1, -4, 45), Tree(-1, -3, 0), Tree(0, -2, 0), Tree(-2, -1, 1)],
                optional: []),
        ],
        MapGround.Lava =>
        [
            Set(
                obstacles: [Mountain(0, 0, 1), Mountain(0, 2, 4), Mountain(3, -1, 3), Mountain(2, 0, 55)],
                optional:
                [
                    Misc(3, 2, 74), Tree(1, 1, 5), Mountain(0, -3, 2),
                    Tree(-1, -2, 2), Tree(-1, 2, 0), Tree(5, 0, 0),
                ]),
            Set(
                obstacles:
                [
                    Misc(0, 0, 74), Tree(-2, -1, 3), Mountain(-3, -1, 57), Mountain(2, -1, 2),
                    Tree(0, 1, 3), Misc(-2, 1, 80), Misc(2, 1, 81),
                ],
                optional: []),
            Set(
                obstacles:
                [
                    Misc(0, 0, 74), Misc(2, -1, 75), Mountain(0, -1, 57), Mountain(3, -1, 55),
                    Mountain(2, 0, 5), Misc(2, -2, 79), Tree(-3, 0, 0),
                ],
                optional: [Misc(2, 1, 81), Tree(-2, -1, 3), Tree(-3, 0, 3)]),
            Set(
                obstacles: [Misc(0, 0, 75), Misc(0, -1, 77), Misc(2, -1, 78), Tree(1, -1, 0)],
                optional: [Misc(1, 1, 77), Tree(2, 0, 0), Tree(-1, 1, 2)]),
            Set(
                obstacles: [Misc(0, 0, 69), Misc(0, 1, 70), Mountain(1, -1, 5)],
                optional: [Misc(4, -1, 69), Misc(1, -2, 70), Misc(2, 0, 79)]),
        ],
        MapGround.Dirt =>
        [
            Set(
                obstacles: [Misc(0, 0, 103), Tree(2, -2, 0), Tree(3, 1, 1), Tree(-4, -1, 1)],
                optional: [Tree(3, 1, 1), Tree(4, -1, 1)]),
        ],
        MapGround.Wasteland =>
        [
            Set(
                obstacles:
                [
                    Misc(0, 0, 138), Mountain(1, -1, 2), Mountain(-3, 1, 6), Mountain(1, -2, 6),
                    Tree(-2, -1, 3), Tree(-4, 1, 5), Rocks(-2, 1, 29),
                ],
                optional: [Tree(2, 1, 1), Tree(0, 3, 3), Rocks(-1, -2, 26), Tree(-4, -1, 1)]),
            Set(
                obstacles: [Misc(0, 0, 134), Misc(2, 0, 139), Misc(0, -1, 141)],
                optional: [Misc(-2, 1, 139), Misc(0, 1, 140), Rocks(-2, 0, 39), Misc(2, 0, 132)]),
            Set(
                obstacles: [Misc(0, 0, 138), Misc(0, -1, 139), Tree(-3, -1, 1), Tree(2, 0, 2), Tree(-1, 1, 4)],
                optional: [Tree(1, 1, 1), Tree(-3, 1, 2), Rocks(-4, 1, 30)]),
        ],
        MapGround.Beach =>
        [
            Set(
                obstacles:
                [
                    Misc(0, 0, 103), Misc(0, 1, 56), Rocks(-4, -1, 22), Mountain(1, -2, 5),
                    Tree(2, -2, 1), Tree(3, 1, 1), Tree(-4, 0, 0),
                ],
                optional: []),
            Set(
                obstacles: [Misc(0, 0, 5), Mountain(1, -1, 2), Mountain(1, 1, 5), Tree(-2, -1, 1), Tree(2, 0, 5), Tree(0, 1, 55)],
                optional: [Tree(-2, 1, 1), Tree(0, -2, 1)]),
        ],
        _ => [],
    };

    private static RandomMapDecorationSet Set(
        IReadOnlyList<RandomMapObjectPlacement> obstacles,
        IReadOnlyList<RandomMapObjectPlacement> optional) => new(obstacles, optional);

    private static RandomMapObjectPlacement Tree(int x, int y, int index) =>
        new(new Point(x, y), ObjectGroup.LandscapeTrees, index);

    private static RandomMapObjectPlacement Mountain(int x, int y, int index) =>
        new(new Point(x, y), ObjectGroup.LandscapeMountains, index);

    private static RandomMapObjectPlacement Rocks(int x, int y, int index) =>
        new(new Point(x, y), ObjectGroup.LandscapeRocks, index);

    private static RandomMapObjectPlacement Misc(int x, int y, int index) =>
        new(new Point(x, y), ObjectGroup.LandscapeMiscellaneous, index);
}
