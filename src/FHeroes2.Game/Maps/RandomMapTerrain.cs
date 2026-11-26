using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The terrain of a generated map before any object is placed, <c>Maps::Random_Generator</c>'s steps 1 to 4
/// (<c>map_random_generator.cpp:657-749</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把地形直接写进编辑器格式（<c>Maps::setTerrainOnTile( mapFormat, index, ground )</c>）；端口还没有那个格式，
/// 所以这一步先产出一张 <see cref="MapGround"/> 表与区域表，往文件里写的活儿留给物件阶段（<c>4f2.1.3</c>）。
/// 未铺过的格子是草地——参考的新图本来就是整张草地（<c>EditorInterface::generateNewMap()</c>）。
/// </para>
/// </remarks>
public sealed class RandomMapTerrain
{
    private readonly MapGround[] _grounds;

    /// <summary>Creates an all grass map of the given size, then applies the painted tiles.</summary>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map.</param>
    /// <param name="regionSizeLimit">How many tiles one region aims for.</param>
    /// <param name="regions">Every region, the neutral water one included.</param>
    /// <param name="state">The grid the regions grew on; the object passes keep marking it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="regions"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    internal RandomMapTerrain(
        int width, int height, int regionSizeLimit, IReadOnlyList<RandomMapRegion> regions, RandomMapState state)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(state);

        Width = width;
        Height = height;
        RegionSizeLimit = regionSizeLimit;
        Regions = regions;
        State = state;
        _grounds = new MapGround[width * height];

        Array.Fill(_grounds, MapGround.Grass);
    }

    /// <summary>Edge length of the map.</summary>
    public int Width { get; }

    /// <summary>Height of the map.</summary>
    public int Height { get; }

    /// <summary>How many tiles one region aimed for, <c>regionSizeLimit</c>.</summary>
    public int RegionSizeLimit { get; }

    /// <summary>Every region; index 0 is the neutral water region the reference inserts first.</summary>
    public IReadOnlyList<RandomMapRegion> Regions { get; }

    /// <summary>
    /// The grid the regions grew on, <c>mapState</c> of <c>generateMap()</c>. It still carries the region id of every
    /// tile, which is what the object passes ask for when they need to keep a route inside one region.
    /// </summary>
    public RandomMapState State { get; }

    /// <summary>Tile coordinates of an index, <c>Maps::GetPoint()</c>.</summary>
    /// <param name="index">Absolute index.</param>
    /// <returns>The coordinates.</returns>
    public Point GetPoint(int index) => new(index % Width, index / Width);

    /// <summary>Index of a tile, <c>Maps::GetIndexFromAbsPoint()</c>.</summary>
    /// <param name="point">Tile coordinates.</param>
    /// <returns>The index, or <c>-1</c> for a negative coordinate.</returns>
    public int IndexAt(in Point point) =>
        point.X < 0 || point.Y < 0 ? -1 : (point.Y * Width) + point.X;

    /// <summary>The ground of a tile.</summary>
    /// <param name="index">Absolute index.</param>
    /// <returns>The ground; <see cref="MapGround.Unknown"/> outside the map.</returns>
    public MapGround GroundAt(int index) =>
        index < 0 || index >= _grounds.Length ? MapGround.Unknown : _grounds[index];

    /// <summary>The ground of a tile.</summary>
    /// <param name="point">Tile coordinates.</param>
    /// <returns>The ground; <see cref="MapGround.Unknown"/> outside the map.</returns>
    public MapGround GroundAt(in Point point) => GroundAt(IndexAt(point));

    /// <summary>Paints one tile, <c>Maps::setTerrainOnTile()</c>.</summary>
    /// <param name="index">Absolute index.</param>
    /// <param name="ground">The ground to paint.</param>
    internal void SetGround(int index, MapGround ground)
    {
        if (index < 0 || index >= _grounds.Length)
        {
            return;
        }

        _grounds[index] = ground;
    }
}

/// <summary>
/// Lays the regions of a random map out and paints their terrain, steps 1 to 4 of <c>generateMap()</c>
/// (<c>map_random_generator.cpp:657-749</c>).
/// </summary>
/// <remarks>
/// <para>
/// 四步的顺序照参考：算区域大小与密度表（步 1）、按内外两圈摆区域中心（步 2）、所有区域轮流各长一圈（步 3）、
/// 把区域的地形铺到格子上并补齐邻居的双向登记（步 4）。
/// </para>
/// <para>
/// <b>中心点用的是参考的 <c>mapBoundsCheck()</c></b>：它先把坐标夹进地图，再算 <c>x * width + y</c>——注意是<b>转置</b>
/// 的乘加顺序，不是通常的 <c>y * width + x</c>。方形地图上两种写法都在界内，只是中心点关于对角线镜像；端口照抄，
/// 免得生成的区域位置与参考不同。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：参考在步 2 末尾用 <c>assert( placedPlayers == config.playerCount )</c> 兜底，端口返回
/// <see langword="null"/> 并记一条警告——生成失败不该把进程带走。
/// </para>
/// </remarks>
public static class RandomMapTerrainGenerator
{
    /// <summary>
    /// The colour index of a region nobody owns, <c>neutralColorIndex</c> (<c>map_random_generator_info.h:50</c>).
    /// </summary>
    /// <remarks>
    /// 参考写的是 <c>Color::GetIndex( PlayerColor::UNUSED )</c>，那就是 <c>Color::GetIndex</c> 的默认分支——
    /// <b>6</b>，也就是 MP2 里「无主」的那个颜色槽。0 是蓝色（第一个玩家），不能拿它当「中立」。
    /// </remarks>
    public const int NeutralColorIndex = 6;

    /// <summary>The grounds a player's starting region may have, <c>playerStartingTerrain</c> (<c>:63</c>).</summary>
    public static readonly MapGround[] PlayerTerrains =
    [
        MapGround.Grass,
        MapGround.Dirt,
        MapGround.Snow,
        MapGround.Lava,
        MapGround.Wasteland,
    ];

    /// <summary>The grounds a neutral region may have, <c>neutralTerrain</c> (<c>:64-65</c>).</summary>
    public static readonly MapGround[] NeutralTerrains =
    [
        MapGround.Grass,
        MapGround.Dirt,
        MapGround.Snow,
        MapGround.Lava,
        MapGround.Wasteland,
        MapGround.Beach,
        MapGround.Swamp,
        MapGround.Desert,
    ];

    /// <summary>Builds the regions and paints their terrain, steps 1 to 4.</summary>
    /// <param name="config">The generator configuration.</param>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map.</param>
    /// <param name="randomGenerator">The generator's random source, seeded by the caller.</param>
    /// <returns>
    /// The terrain and the regions, or <see langword="null"/> when the configuration cannot fill the map.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    public static RandomMapTerrain? Generate(
        RandomMapConfig config, int width, int height, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        // Step 1: the size of a region decides how many of them the land is cut into.
        int regionSizeLimit = RandomMapSizing.RegionSizeLimit(config, width, height);

        if (regionSizeLimit <= 0)
        {
            return null;
        }

        RandomMapRegionalObjects regionConfiguration = RandomMapRegionalObjects.For(config.ResourceDensity);
        int groundTiles = width * height * (100 - config.WaterPercentage) / 100;
        int expectedRegionCount = groundTiles / regionSizeLimit;

        RandomMapState state = new(width, height);
        List<RandomMapRegion> regions =
        [
            // The reference inserts the water region first so that region ids line up with the list.
            new RandomMapRegion(
                0, state.GetNodeToUpdate(0), NeutralColorIndex, MapGround.Water, 1, 0, RandomMapRegionType.Neutral),
        ];

        PlaceRegions(config, state, regions, regionConfiguration, expectedRegionCount, regionSizeLimit, width, height,
            randomGenerator);

        if (CountStartingRegions(regions) != config.PlayerCount)
        {
            return null;
        }

        GrowRegions(regions, state, randomGenerator);

        RandomMapTerrain terrain = new(width, height, regionSizeLimit, regions, state);

        PaintTerrain(regions, terrain);

        return terrain;
    }

    private static int CountStartingRegions(List<RandomMapRegion> regions) =>
        regions.Count(region => region.Type == RandomMapRegionType.Starting);

    /// <summary>
    /// Places the centres of the regions on two rings around the middle of the map, step 2 of <c>generateMap()</c>
    /// (<c>map_random_generator.cpp:663-718</c>).
    /// </summary>
    /// <remarks>
    /// 内圈放玩家起点（<c>innerLayer</c> 个），外圈放中立区；两圈的半径都由区域面积与「要放几个区域」估出来，
    /// 水多时外圈再收回 0.8 倍。每个区域的起点地形从两条表里随机取，玩家区用起点表、中立区用中立表。
    /// </remarks>
    private static void PlaceRegions(
        RandomMapConfig config,
        RandomMapState state,
        List<RandomMapRegion> regions,
        RandomMapRegionalObjects regionConfiguration,
        int expectedRegionCount,
        int regionSizeLimit,
        int width,
        int height,
        Pcg32 randomGenerator)
    {
        int neutralRegionCount = Math.Max(1, expectedRegionCount - config.PlayerCount);
        int innerLayer = Math.Min(neutralRegionCount, config.PlayerCount);
        int outerLayer = Math.Max(Math.Min(neutralRegionCount, innerLayer * 2), config.PlayerCount);
        double distanceModifier = config.WaterPercentage > 20 ? 0.8 : 0.9;

        double radius = Math.Sqrt((innerLayer + outerLayer) * (double)regionSizeLimit / Math.PI);
        double outerRadius = (innerLayer + outerLayer) > expectedRegionCount
            ? Math.Max(width, height) * 0.47
            : radius * distanceModifier;
        double innerRadius = innerLayer == 1 ? 0 : outerRadius / 3;

        (int Count, double Radius)[] layers = [(innerLayer, innerRadius), (outerLayer, outerRadius)];

        int placedPlayers = 0;

        for (int layer = 0; layer < layers.Length; ++layer)
        {
            int regionCount = layers[layer].Count;

            if (regionCount <= 0)
            {
                continue;
            }

            double startingAngle = randomGenerator.NextInclusive(0, 360);
            double offsetAngle = 360.0 / regionCount;
            bool regionCountIsEven = regionCount % config.PlayerCount == 0;

            for (int i = 0; i < regionCount; ++i)
            {
                double radians = (startingAngle + (offsetAngle * i)) * Math.PI / 180;
                double distance = layers[layer].Radius;

                int x = (width / 2) + (int)(Math.Cos(radians) * distance);
                int y = (height / 2) + (int)(Math.Sin(radians) * distance);
                int centreTile = ClampedIndex(x, y, width, height);

                int factor = regionCount * placedPlayers / config.PlayerCount;
                bool isPlayerRegion = layer == 1 && factor == i;

                // 参考的 Rand::GetWithGen( vector, gen ) 就是在这两条表里挑一个。
                MapGround ground = isPlayerRegion
                    ? PlayerTerrains[randomGenerator.NextInclusive(0, PlayerTerrains.Length - 1)]
                    : NeutralTerrains[randomGenerator.NextInclusive(0, NeutralTerrains.Length - 1)];

                int colour = isPlayerRegion ? placedPlayers : NeutralColorIndex;
                int treasureLimit = isPlayerRegion
                    ? regionConfiguration.TreasureValueLimit
                    : regionConfiguration.TreasureValueLimit * 2;

                uint regionId = (uint)regions.Count;
                RandomMapNode centreNode = state.GetNodeToUpdate(centreTile);

                RandomMapRegionType neutralType = layer == 1 && regionCountIsEven
                    ? RandomMapRegionType.Expansion
                    : RandomMapRegionType.Neutral;
                RandomMapRegionType type = isPlayerRegion ? RandomMapRegionType.Starting : neutralType;

                // 稀缺资源下玩家起点就是标准大小，其余一律放大两成，让区域装得下那一堆物件。
                int regionSize = type == RandomMapRegionType.Starting
                                 && config.ResourceDensity == RandomMapResourceDensity.Scarce
                    ? regionSizeLimit
                    : regionSizeLimit * 6 / 5;

                regions.Add(new RandomMapRegion(regionId, centreNode, colour, ground, regionSize, treasureLimit, type));

                if (isPlayerRegion)
                {
                    ++placedPlayers;
                }
            }
        }
    }

    /// <summary>
    /// Turns a ring coordinate into a tile, the <c>mapBoundsCheck()</c> lambda of <c>map_random_generator.cpp:651-655</c>.
    /// </summary>
    /// <remarks>
    /// 参考写的是 <c>x * width + y</c>：clamp 之后按「行主序反过来」的乘加取索引，与 <c>y * width + x</c> 互为转置。
    /// 方形地图上仍在界内，只是中心点关于对角线镜像；端口照抄，免得区域落点与参考不同。
    /// </remarks>
    private static int ClampedIndex(int x, int y, int width, int height)
    {
        int clampedX = Math.Clamp(x, 0, width - 1);
        int clampedY = Math.Clamp(y, 0, height - 1);

        return (clampedX * width) + clampedY;
    }

    /// <summary>
    /// Lets every region grow one ring at a time so that they compete for space, step 3
    /// (<c>map_random_generator.cpp:720-730</c>).
    /// </summary>
    /// <param name="regions">Every region; index 0 is the water placeholder and never grows.</param>
    /// <param name="state">The grid the regions grow on.</param>
    /// <param name="randomGenerator">The generator's random source.</param>
    private static void GrowRegions(List<RandomMapRegion> regions, RandomMapState state, Pcg32 randomGenerator)
    {
        bool stillRoomToExpand = true;

        while (stillRoomToExpand)
        {
            stillRoomToExpand = false;

            for (int regionId = 1; regionId < regions.Count; ++regionId)
            {
                if (regions[regionId].RegionExpansion(state, randomGenerator))
                {
                    stillRoomToExpand = true;
                }
            }
        }
    }

    /// <summary>
    /// Paints the ground of every region and adds the reverse links of the neighbourhoods, step 4
    /// (<c>map_random_generator.cpp:732-749</c>).
    /// </summary>
    /// <param name="regions">Every region; index 0 is skipped like in the reference.</param>
    /// <param name="terrain">The terrain to paint into.</param>
    private static void PaintTerrain(IReadOnlyList<RandomMapRegion> regions, RandomMapTerrain terrain)
    {
        foreach (RandomMapRegion region in regions)
        {
            if (region.Id == 0)
            {
                continue;
            }

            foreach (RandomMapNode node in region.Nodes)
            {
                terrain.SetGround(node.Index, region.GroundType);
            }

            // 参考在这里记一条「缺少反向链接」的警告；端口直接把反向那一半补上，邻居表才是对称的。
            foreach (uint adjacent in region.Neighbours)
            {
                regions[(int)adjacent].Neighbours.Add(region.Id);
            }
        }
    }
}
