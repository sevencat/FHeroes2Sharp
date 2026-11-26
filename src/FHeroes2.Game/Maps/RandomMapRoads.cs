using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The last pass of the object phase: it reorders every temporary road so that each tile shows the joint its
/// neighbours need — <c>updateAllRoads()</c>, <c>getRoadObjectIndex()</c> and <c>updateRoadOnTile()</c>
/// (<c>map_format_helper.cpp:858-882</c>, <c>:1848-1897</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么最后才做</b>：择位时铺的路只是「这里能修出一条路」的凭证，全都是同一张直路（<c>ROADS</c> 的第 2 号）。
/// 等所有路都铺完，每一格真实的路况才定下来——这时按八邻域里「哪些格也有路」算一个方向掩码，掩码就是目录里那条
/// 直/弯/三叉/十字的编号。
/// </para>
/// <para>
/// <b>「有路」按对象表算，不按画面算</b>：一件路面物件除了锚点那一格，还会往相邻格写接头面片。相邻格因此会画出路面，
/// 但它自己没有路。参考的 <c>doesContainRoad()</c> 查的是对象表，端口查 <see cref="RandomMapState.Roads"/>——那是同一份
/// 东西，见 <see cref="RandomMapRoadPlacement"/>。
/// </para>
/// <para>
/// <b>城堡门口</b>：紧贴着城堡入口下方那一格不用方向掩码，固定用 <c>ROADS</c> 的第 <see cref="CastleEntranceIndex"/>
/// 号——那是一条斜着从城门里出来的路，方向由城堡决定。
/// </para>
/// </remarks>
public static class RandomMapRoads
{
    /// <summary>The road that leads out of a castle gate, <c>512</c> of <c>map_format_helper.cpp:862</c>.</summary>
    public const int CastleEntranceIndex = 512;

    /// <summary>How many variants of a joint the catalogue holds, the <c>256</c> of <c>:881</c>.</summary>
    public const int VariantStride = 256;

    /// <summary>The two object types a castle entrance is drawn with, the <c>KINGDOM_TOWNS</c> group of the reference.</summary>
    private static readonly Mp2ObjectType[] TownObjectTypes =
    [
        Mp2ObjectType.Castle,
        Mp2ObjectType.ActionCastle,
        Mp2ObjectType.RandomCastle,
        Mp2ObjectType.ActionRandomCastle,
        Mp2ObjectType.RandomTown,
        Mp2ObjectType.ActionRandomTown,
    ];

    /// <summary>
    /// Rewrites every road of the map, <c>updateAllRoads()</c> (<c>:1891-1897</c>).
    /// </summary>
    /// <param name="map">The map to edit.</param>
    /// <param name="state">The grid, whose <see cref="RandomMapState.Roads"/> lists the road of every tile.</param>
    /// <param name="randomGenerator">The generator, used to pick between the two variants of a joint.</param>
    /// <returns>How many tiles changed.</returns>
    /// <remarks>
    /// 掩码只看重排前的那份名单：重排一格会把接头面片写到相邻格，但那些面片不代表相邻格「有了路」。
    /// 所以整个过程的输入是不变的，处理顺序也就无关紧要。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int UpdateAll(Mp2Map map, RandomMapState state, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        int updated = 0;

        foreach (int tileIndex in state.Roads.Keys.Order().ToList())
        {
            if (UpdateTile(map, state, tileIndex, randomGenerator))
            {
                ++updated;
            }
        }

        return updated;
    }

    /// <summary>
    /// Rewrites one road, <c>updateRoadOnTile()</c> (<c>:1848-1889</c>).
    /// </summary>
    /// <param name="map">The map to edit.</param>
    /// <param name="state">The grid, whose <see cref="RandomMapState.Roads"/> lists the road of every tile.</param>
    /// <param name="tileIndex">The tile.</param>
    /// <param name="randomGenerator">The generator, used to pick the variant of the joint.</param>
    /// <returns><see langword="true"/> when the tile got a different road.</returns>
    /// <remarks>
    /// 旧件按 UID 整件撤掉（连同它撒在相邻格上的接头），新件用**同一个 UID** 写上——参考在
    /// <c>updateRoadOnTile()</c> 里把计数器退回去再写，就是为了让对象表里的 id 一直有效。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static bool UpdateTile(Mp2Map map, RandomMapState state, int tileIndex, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        if (!state.Roads.TryGetValue(tileIndex, out RandomMapRoadPlacement road))
        {
            return false;
        }

        int target = ObjectIndexAt(map, state, tileIndex, randomGenerator);

        if (target == road.ObjectIndex)
        {
            return false;
        }

        RandomMapObjectWriter.Remove(map, road.Uid);

        Point point = state.GetPoint(tileIndex);

        if (!RandomMapObjectWriter.TryWriteRoad(map, point, target, road.Uid))
        {
            return false;
        }

        state.Roads[tileIndex] = road with { ObjectIndex = target };

        return true;
    }

    /// <summary>
    /// Which entry of <c>ROADS</c> a tile needs, <c>getRoadObjectIndex()</c> (<c>:858-882</c>).
    /// </summary>
    /// <param name="map">The map, whose tiles say where the castle entrances are.</param>
    /// <param name="state">The grid, whose road list says which neighbours have a road.</param>
    /// <param name="tileIndex">The tile.</param>
    /// <param name="randomGenerator">The generator, used to pick between the two variants of a joint.</param>
    /// <returns>The catalogue index, <c>mask + variant * 256</c>.</returns>
    /// <remarks>
    /// 参考那一行 <c>roadDirection + Rand::Get( 1 ) * 256</c> 用的是**全局**随机，因此两套精灵的取舍不受种子控制；
    /// 端口改用生成器，同一种子才画得出同一张图（ADR-274 的同一立场）。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int ObjectIndexAt(Mp2Map map, RandomMapState state, int tileIndex, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        Point point = state.GetPoint(tileIndex);

        // 城堡入口正下方那一格：路是从城门里斜着出来的，不需要方向。
        if (point.Y > 0 && IsTownEntrance(map, point.X, point.Y - 1))
        {
            return CastleEntranceIndex;
        }

        int mask = 0;

        foreach (Point offset in RandomMapState.DirectionOffsets)
        {
            Point neighbour = new(point.X + offset.X, point.Y + offset.Y);
            int neighbourIndex = state.IsValidPoint(neighbour) ? state.IndexAt(neighbour) : -1;

            if (neighbourIndex < 0 || !state.Roads.ContainsKey(neighbourIndex))
            {
                continue;
            }

            mask |= (int)DirectionOf(offset);
        }

        return mask + (randomGenerator.NextInclusive(0, 1) * VariantStride);
    }

    /// <summary>Whether a tile holds a town, which makes the tile below it a castle entrance, <c>doesContainCastleEntrance()</c>.</summary>
    /// <param name="map">The map.</param>
    /// <param name="x">Column of the tile.</param>
    /// <param name="y">Row of the tile.</param>
    /// <returns><see langword="true"/> when the tile is a town or castle.</returns>
    private static bool IsTownEntrance(Mp2Map map, int x, int y) =>
        TownObjectTypes.Contains(map.Tile(x, y).ObjectType);

    /// <summary>The direction bit of a neighbour offset, the inverse of <see cref="RandomMapState.DirectionOffsets"/>.</summary>
    /// <param name="offset">Offset from a tile to its neighbour.</param>
    /// <returns>The matching <see cref="Direction"/>, <see cref="Direction.Unknown"/> for a zero offset.</returns>
    private static Direction DirectionOf(in Point offset) => offset switch
    {
        { X: 0, Y: -1 } => Direction.Top,
        { X: 1, Y: 0 } => Direction.Right,
        { X: 0, Y: 1 } => Direction.Bottom,
        { X: -1, Y: 0 } => Direction.Left,
        { X: -1, Y: -1 } => Direction.TopLeft,
        { X: 1, Y: -1 } => Direction.TopRight,
        { X: 1, Y: 1 } => Direction.BottomRight,
        { X: -1, Y: 1 } => Direction.BottomLeft,
        _ => Direction.Unknown,
    };
}
