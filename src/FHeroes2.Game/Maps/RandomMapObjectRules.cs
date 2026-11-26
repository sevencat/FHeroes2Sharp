using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The two rules the object phase uses on the grid: mark a tile as taken, and decide whether an object fits at a
/// position (<c>map_random_generator_helper.cpp:209-281</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把它们写成匿名命名空间里的 <c>markNodeAsType</c> / <c>canPlaceObject</c>；端口收进一个静态类，因为落子逻辑
/// 要按「放得下吗 → 占了哪些格 → 全放得下再提交」的次序调用它们，而这三步都要能在单测里单独驱动。
/// </para>
/// <para>
/// <b>为什么格子的类型不能随便改</b>：<c>BORDER</c> 是区域的边界、<c>ACTION</c> 是给可交互物件留的位置，
/// 它们比后来者优先；<c>PATH</c> 也不许盖到 <c>OBSTACLE</c> 上——否则路会从石头里面穿过去。这三条都是参考的注释里
/// 点名写下的规则。
/// </para>
/// </remarks>
public static class RandomMapObjectRules
{
    /// <summary>Marks a tile, respecting the priorities of <see cref="RandomMapNodeType"/>, <c>markNodeAsType()</c>.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="position">Tile to mark; a position outside the map is ignored.</param>
    /// <param name="type">What the tile becomes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static void MarkNodeAsType(RandomMapState state, in Point position, RandomMapNodeType type)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.IsValidPoint(position))
        {
            return;
        }

        MarkNodeIndexAsType(state, state.IndexAt(position), type);
    }

    /// <summary>Marks a tile by index, <c>markNodeIndexAsType()</c>.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="index">Tile to mark.</param>
    /// <param name="type">What the tile becomes.</param>
    /// <remarks>
    /// 边界与可交互位置从不被覆盖；路径不许穿过障碍。其余情况下后来者居上——树可以盖住空地，宝物可以盖住树。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static void MarkNodeIndexAsType(RandomMapState state, int index, RandomMapNodeType type)
    {
        ArgumentNullException.ThrowIfNull(state);

        RandomMapNode node = state.GetNodeToUpdate(index);

        if (node.Type is RandomMapNodeType.Border or RandomMapNodeType.Action)
        {
            return;
        }

        if (type == RandomMapNodeType.Path && node.Type == RandomMapNodeType.Obstacle)
        {
            return;
        }

        node.Type = type;
    }

    /// <summary>
    /// Whether <paramref name="info"/> fits at <paramref name="position"/>, <c>canPlaceObject()</c>
    /// (<c>map_random_generator_helper.cpp:239-281</c>).
    /// </summary>
    /// <param name="state">The grid the object would be placed on.</param>
    /// <param name="info">The object, with its ground and top level parts.</param>
    /// <param name="position">The object's anchor tile.</param>
    /// <returns><see langword="true"/> when every part covers a usable tile.</returns>
    /// <remarks>
    /// <para>
    /// 地面层要落在「有主人（region 非 0）、不是 ACTION/PATH、而且要么是空地、要么是边界且该物件不是可交互物件」的格子上：
    /// 边界格只许不可交互的装饰物压，否则玩家会把区域之间的通道堵死。顶层（悬空的树冠、屋顶）只要求格子在地图内且有主人。
    /// </para>
    /// <para>
    /// 可交互物件还要看正下方那一格（<c>position + (0,1)</c>）：它必须是空地或路，玩家才走得到物件跟前。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="info"/> is <see langword="null"/>.</exception>
    public static bool CanPlaceObject(RandomMapState state, ObjectInfo info, in Point position)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(info);

        bool isAction = Mp2ObjectTypes.IsInGameActionObject(info.ObjectType);

        foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
        {
            if (part.LayerType is ObjectLayerType.Shadow or ObjectLayerType.Terrain)
            {
                // 影子与地表层不占格子。
                continue;
            }

            RandomMapNode node = state.GetNode(Offset(position, part.TileOffset));

            if (node.Index == -1 || node.Region == 0)
            {
                return false;
            }

            if (node.Type is RandomMapNodeType.Action or RandomMapNodeType.Path)
            {
                return false;
            }

            if (node.Type != RandomMapNodeType.Open && (isAction || node.Type != RandomMapNodeType.Border))
            {
                return false;
            }
        }

        foreach (ObjectPartInfo part in info.TopLevelParts)
        {
            RandomMapNode node = state.GetNode(Offset(position, part.TileOffset));

            if (node.Index == -1 || node.Region == 0)
            {
                return false;
            }
        }

        if (isAction)
        {
            RandomMapNode below = state.GetNode(new Point(position.X, position.Y + 1));

            if (below.Index == -1 || below.Region == 0)
            {
                return false;
            }

            if (below.Type is not (RandomMapNodeType.Open or RandomMapNodeType.Path))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Which catalogue index a decoration has to use on a ground, <c>selectTerrainVariantForObject()</c>
    /// (<c>map_random_generator_helper.cpp:408-418</c>).
    /// </summary>
    /// <param name="group">The group of the object.</param>
    /// <param name="objectIndex">The index the prefab names.</param>
    /// <param name="ground">The ground the object stands on.</param>
    /// <returns>
    /// The index shifted into the ground's own block for the six tree kinds and the eight mountain kinds;
    /// every other object keeps its index.
    /// </returns>
    /// <remarks>
    /// 预制表里的「树 3」说的是「草地那套树里的第 3 棵」；换到雪地上就得换成雪地那套的第 3 棵，
    /// 也就是 <c>TreeIndexFor( ground ) + 3</c>。山同理（八种），其余物件不分地形。
    /// </remarks>
    public static int SelectTerrainVariant(ObjectGroup group, int objectIndex, MapGround ground) => group switch
    {
        ObjectGroup.LandscapeTrees when objectIndex < 6 => RandomMapDecorations.TreeIndexFor(ground) + objectIndex,
        ObjectGroup.LandscapeMountains when objectIndex < 8 => RandomMapDecorations.MountainIndexFor(ground) + objectIndex,
        _ => objectIndex,
    };

    /// <summary>
    /// Whether every placement of a list fits when the list is anchored at <paramref name="position"/>,
    /// <c>canPlaceAllObjects()</c> (<c>:689-695</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="objects">The placements, usually a prefab's obstacles or valuables.</param>
    /// <param name="position">The anchor of the list.</param>
    /// <param name="ground">The ground, which decides the tree and mountain variants.</param>
    /// <returns><see langword="true"/> when every object fits.</returns>
    /// <remarks>组内编号越界时返回 <see langword="false"/> 并记一条警告（参考在这里断言）。</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="objects"/> is <see langword="null"/>.</exception>
    public static bool CanPlaceAllObjects(
        RandomMapState state,
        IReadOnlyList<RandomMapObjectPlacement> objects,
        in Point position,
        MapGround ground)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(objects);

        foreach (RandomMapObjectPlacement placement in objects)
        {
            int index = SelectTerrainVariant(placement.Group, placement.ObjectIndex, ground);

            if (!TryGetObjectInfo(placement.Group, index, out ObjectInfo info))
            {
                return false;
            }

            if (!CanPlaceObject(state, info, Offset(position, placement.Offset)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a whole prefab fits, <c>canFitObjectSet()</c> (<c>:697-713</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="set">The prefab: obstacles, prizes and the tiles that have to stay open.</param>
    /// <param name="position">The anchor of the prefab.</param>
    /// <param name="ground">The ground, which decides the tree and mountain variants.</param>
    /// <returns><see langword="true"/> when the entrance is open and every object fits.</returns>
    /// <remarks>
    /// 入口格必须还是空地——玩家得走得进去；随后障碍、宝物与那只守宝物的怪物都要放得下。怪物永远以
    /// (0,0) 落在锚点上（参考的 <c>randomMonsterSet</c>），所以它其实是在查锚点本身能不能站一只怪物。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static bool CanFitObjectSet(
        RandomMapState state, RandomMapObjectSet set, in Point position, MapGround ground)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(set);

        foreach (Point offset in set.EntranceCheck)
        {
            RandomMapNode node = state.GetNode(Offset(position, offset));

            if (node.Index == -1 || node.Type != RandomMapNodeType.Open)
            {
                return false;
            }
        }

        return CanPlaceAllObjects(state, set.Obstacles, position, ground)
            && CanPlaceAllObjects(state, set.Valuables, position, ground)
            && CanPlaceAllObjects(state, RandomMonsterPlacement, position, ground);
    }

    /// <summary>
    /// Marks every tile an object covers, <c>markObjectPlacement()</c> (<c>:715-727</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="info">The object that was placed.</param>
    /// <param name="position">Its anchor tile.</param>
    /// <remarks>
    /// 地面层（不算影子与地表层）都标成障碍；带动作位的物件再把锚点标成 <c>ACTION</c>——那是它留给玩家站的位置。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="info"/> is <see langword="null"/>.</exception>
    public static void MarkObjectPlacement(RandomMapState state, ObjectInfo info, in Point position)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(info);

        foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
        {
            if (part.LayerType is ObjectLayerType.Shadow or ObjectLayerType.Terrain)
            {
                continue;
            }

            MarkNodeAsType(state, Offset(position, part.TileOffset), RandomMapNodeType.Obstacle);
        }

        // MP2::isOffGameActionObject() 只是动作位测试（mp2.cpp:650-653），不是「英雄能不能访问」。
        if (Mp2ObjectTypes.IsActionObject(info.ObjectType))
        {
            MarkNodeAsType(state, position, RandomMapNodeType.Action);
        }
    }


    /// <summary>
    /// Whether a decoration fits on a region's edge, <c>canPlaceBorderObstacle()</c> (<c>:660-687</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="info">The decoration.</param>
    /// <param name="position">Its anchor tile.</param>
    /// <returns><see langword="true"/> when every part covers a usable tile.</returns>
    /// <remarks>
    /// 与 <see cref="CanPlaceObject"/> 的差别只有一条：这里允许压在已有的障碍上，因为装饰物本来就是一丛一丛长的；
    /// <c>ACTION</c> 与 <c>PATH</c> 仍然不行，否则会盖掉可交互物件的入口或修好的路。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="info"/> is <see langword="null"/>.</exception>
    public static bool CanPlaceBorderObstacle(RandomMapState state, ObjectInfo info, in Point position)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(info);

        foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
        {
            if (part.LayerType is ObjectLayerType.Shadow or ObjectLayerType.Terrain)
            {
                continue;
            }

            RandomMapNode node = state.GetNode(Offset(position, part.TileOffset));

            if (node.Index == -1 || node.Region == 0)
            {
                return false;
            }

            if (node.Type is not (RandomMapNodeType.Open or RandomMapNodeType.Border or RandomMapNodeType.Obstacle))
            {
                return false;
            }
        }

        foreach (ObjectPartInfo part in info.TopLevelParts)
        {
            RandomMapNode node = state.GetNode(Offset(position, part.TileOffset));

            if (node.Index == -1 || node.Region == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The one placement every prefab adds: the guard monster, <c>randomMonsterSet</c> (<c>:74</c>).</summary>
    public static IReadOnlyList<RandomMapObjectPlacement> RandomMonsterPlacement { get; } =
    [
        new RandomMapObjectPlacement(new Point(0, 0), ObjectGroup.Monsters, 0),
    ];


    /// <summary>Resolves a catalogue entry, or answers <see langword="false"/> for an index that does not exist.</summary>
    /// <param name="group">Which table.</param>
    /// <param name="objectIndex">Which entry.</param>
    /// <param name="info">The object, when the entry exists.</param>
    /// <returns><see langword="true"/> when the entry exists.</returns>
    /// <remarks>
    /// 参考在这里断言：组与编号来自它自己的常量表，越界就是表写错了。端口的表是从参考抄来的，也应当越界即崩；
    /// 但生成器可以被编辑器以任意设置驱动，所以这里退一步记一条警告并返回 <see langword="false"/>，由调用方跳过这一组。
    /// </remarks>
    internal static bool TryGetObjectInfo(ObjectGroup group, int objectIndex, out ObjectInfo info)
    {
        info = null!;

        if (group <= ObjectGroup.None || group >= ObjectGroup.GroupCount)
        {
            GameLog.Warn($"The random map generator asked for objects of group {group}, which has no table.");

            return false;
        }

        IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(group);

        if (objectIndex < 0 || objectIndex >= objects.Count)
        {
            GameLog.Warn(
                $"The random map generator asked for object {objectIndex} of {group}, which has {objects.Count}.");

            return false;
        }

        info = objects[objectIndex];

        return true;
    }

    private static Point Offset(in Point position, in Point offset) =>
        new(position.X + offset.X, position.Y + offset.Y);
}

