using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The castle pass of <c>generateMap()</c>: the starting castle of every player and the optional town of a large
/// neutral region — <c>placeCastle()</c> (<c>map_random_generator_helper.cpp:824-895</c>) driven by step 5
/// (<c>map_random_generator.cpp:788-804</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的一座城堡是五件东西：地基（<c>LANDSCAPE_TOWN_BASEMENTS</c>）、城镇本体（<c>KINGDOM_TOWNS</c>）、左右两面旗
/// （<c>LANDSCAPE_FLAGS</c>）、城堡下方一格站着的英雄（<c>KINGDOM_HEROES</c>），外加门口的两格路。这些部件共用
/// 一个 UID，读图时才是一个整体。
/// </para>
/// <para>
/// <b>文件里的两处外挂</b>：城堡进 72 条的城镇位置表，颜色写进它自己的信息块（70 字节）；英雄站上瓦片还不够，
/// 还要一段 76 字节的英雄块，否则读图时 <c>World</c> 根本不会为它建对象。两段块都由瓦片的 13 位 metadata 指过来。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：一是英雄格在参考里不占 <c>mapState</c>，端口把它标成 <c>ACTION</c>——端口没有
/// <c>Map_Format</c> 兜底，不标的话装饰阶段会把一座山盖在英雄头上；二是参考的 <c>canPlaceObject</c> 不查地图边界
/// （越界由 <c>world.getTile()</c> 的断言兜着），而 MP2 的瓦片没有图外的位置，所以这里把锚点收进「整座城堡都放得下」
/// 的范围——<see cref="RandomMapRegion.AdjustRegionToFitCastle"/> 只留四格余量，城堡的影子却往左伸五格。
/// </para>
/// </remarks>
public static class RandomMapCastlePhase
{
    /// <summary>Index of the random castle in <c>KINGDOM_TOWNS</c>, <c>randomCastleIndex</c> (<c>:62</c>).</summary>
    public const int RandomCastleIndex = 12;

    /// <summary>Index of the random town in <c>KINGDOM_TOWNS</c>, <c>randomTownIndex</c> (<c>:63</c>).</summary>
    public const int RandomTownIndex = 13;

    /// <summary>Hero sprites per colour in <c>MINIHERO.ICN</c>, <c>randomHeroIndex</c> (<c>:64</c>).</summary>
    public const int HeroSpritesPerColor = 7;

    /// <summary>Size of a town block, <c>MP2_CASTLE_STRUCTURE_SIZE</c>.</summary>
    public const int CastleBlockSize = 70;

    /// <summary>Size of a hero block, <c>MP2_HEROES_STRUCTURE_SIZE</c>.</summary>
    public const int HeroBlockSize = 76;

    /// <summary>Offset of the owner byte inside a town block.</summary>
    private const int CastleOwnerOffset = 0;

    /// <summary>Offset of the race byte inside a town block.</summary>
    private const int CastleRaceOffset = 37;

    /// <summary>Offset of the "is a castle" byte inside a town block.</summary>
    private const int CastleFortOffset = 38;

    /// <summary>Owner byte of a town nobody owns.</summary>
    private const byte NeutralOwner = 0xFF;

    /// <summary>
    /// The basement sprite a ground uses, <c>fheroes2::getTownBasementId()</c>
    /// (<c>gui/ui_map_object.cpp:295-326</c>).
    /// </summary>
    /// <param name="ground">The ground of the castle.</param>
    /// <returns>Index into <see cref="ObjectGroup.LandscapeTownBasements"/>.</returns>
    /// <remarks>
    /// 水下的城堡在参考里是断言，端口按草地给一张；表外的地面也一样，不会返回一个目录里没有的编号。
    /// </remarks>
    public static int BasementIndex(MapGround ground) => ground switch
    {
        MapGround.Snow => 1,
        MapGround.Swamp => 2,
        MapGround.Lava => 3,
        MapGround.Desert => 4,
        MapGround.Dirt => 5,
        MapGround.Wasteland => 6,
        MapGround.Beach => 7,
        _ => 0,
    };

    /// <summary>
    /// Places one castle or town, <c>placeCastle()</c> (<c>:824-895</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region the castle belongs to; its colour decides the owner and its ground the basement.</param>
    /// <param name="position">Top left tile of the castle, the value <see cref="RandomMapRegion.AdjustRegionToFitCastle"/> returns.</param>
    /// <param name="isCastle">A fortified castle when <see langword="true"/>, a plain town otherwise.</param>
    /// <param name="randomGenerator">The generator, used to roll the race of the town.</param>
    /// <returns><see langword="true"/> when the castle landed.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static bool PlaceCastle(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        in Point position,
        bool isCastle,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        if (!state.IsValidPoint(position))
        {
            return false;
        }

        // 参考摆的是「随机城堡」，种族由信息块决定；端口直接选一种族，表与块写同一个，读图时不会再多摇一次。
        MapRace race = MapRaces.Basic[randomGenerator.NextInclusive(0, MapRaces.Basic.Count - 1)];
        int raceIndex = MapRaces.ToIndex(race) ?? 0;
        int townIndex = (raceIndex * 2) + (isCastle ? 0 : 1);

        if (!TryResolveObjects(map, position, townIndex, out Point anchor, out ObjectInfo basementInfo, out ObjectInfo townInfo))
        {
            return false;
        }

        if (!RandomMapObjectRules.CanPlaceObject(state, basementInfo, anchor)
            || !RandomMapObjectRules.CanPlaceObject(state, townInfo, anchor))
        {
            return false;
        }

        PlayerColor color = RegionColor(region.ColorIndex);
        int blockIndex = map.AddInfoBlock(BuildCastleBlock(color, race, isCastle));
        uint castleMetadata = (uint)(blockIndex + 1);
        uint heroMetadata = 0;

        // 五件东西共用一个 UID：参考把计数器退回去再写下一件（:848-875）。
        uint uid = map.TakeObjectUid();

        if (!RandomMapObjectWriter.TryWrite(map, anchor, basementInfo, uid)
            || !RandomMapObjectWriter.TryWrite(map, anchor, townInfo, uid))
        {
            return false;
        }

        RandomMapObjectWriter.SetMetadata(map, anchor, castleMetadata);
        RandomMapObjectRules.MarkObjectPlacement(state, basementInfo, anchor);
        RandomMapObjectRules.MarkObjectPlacement(state, townInfo, anchor);

        if (color != PlayerColor.None)
        {
            int colorIndex = PlayerColors.IndexOf(color);

            RandomMapObjectWriter.TryWrite(map, new Point(anchor.X - 1, anchor.Y), ObjectGroup.LandscapeFlags, colorIndex * 2);
            RandomMapObjectWriter.TryWrite(map, new Point(anchor.X + 1, anchor.Y), ObjectGroup.LandscapeFlags, (colorIndex * 2) + 1);

            heroMetadata = PlaceHero(map, state, new Point(anchor.X, anchor.Y + 1), colorIndex);
        }

        map.AddCastle(new Mp2CastlePosition(
            anchor.X, anchor.Y, raceIndex | (isCastle ? 0x80 : 0), race));

        // 锚点可能被收进来过，区域中心也要跟着摆在城堡下方两格——主矿的搜索环从这里起算。
        region.CenterIndex = state.IndexAt(new Point(anchor.X, anchor.Y + 2));

        // 城堡门口的两格先铺路：后面的矿与宝物都靠这些路面接上。
        Point entrance = new(anchor.X, anchor.Y + 1);
        Point roadStart = new(anchor.X, anchor.Y + 2);

        if (state.IsValidPoint(entrance))
        {
            RandomMapObjectPhase.ForceTempRoadOnTile(map, state, state.IndexAt(entrance));
        }

        if (state.IsValidPoint(roadStart))
        {
            RandomMapObjectPhase.ForceTempRoadOnTile(map, state, state.IndexAt(roadStart));
        }

        // 铺路会重写整张瓦片（路没有 metadata），指块的活儿只能放在它后面。
        if (heroMetadata != 0)
        {
            RandomMapObjectWriter.SetMetadata(map, entrance, heroMetadata);
        }

        return true;
    }

    /// <summary>
    /// Places the castles of every region and opens the way out of the small neutral ones, the castle half of step 5
    /// (<c>map_random_generator.cpp:788-804</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region; index 0 is the water placeholder.</param>
    /// <param name="regionSizeLimit">How many tiles a region aims for, <c>regionSizeLimit</c>.</param>
    /// <param name="density">The resource density, which decides whether a neutral region gets a fort or a town.</param>
    /// <param name="randomGenerator">The generator.</param>
    /// <returns><see langword="false"/> when a player's starting castle could not be placed, which aborts the generation.</returns>
    /// <remarks>
    /// 参考在这里 <c>return false</c>——一个玩家没有起点城堡的地图不该存在；端口同样让整个生成失败，
    /// 而不是留一张没法开局的地图。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static bool PlaceCastles(
        Mp2Map map,
        RandomMapState state,
        IReadOnlyList<RandomMapRegion> regions,
        int regionSizeLimit,
        RandomMapResourceDensity density,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        foreach (RandomMapRegion region in regions)
        {
            if (!PlaceRegionCastle(map, state, region, regionSizeLimit, density, randomGenerator))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Decides what one region gets and places it, the body of the loop of step 5
    /// (<c>map_random_generator.cpp:755-821</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region to fill; index 0 is skipped.</param>
    /// <param name="regionSizeLimit">How many tiles a region aims for, <c>regionSizeLimit</c>.</param>
    /// <param name="density">The resource density, which decides whether a neutral region gets a fort or a town.</param>
    /// <param name="randomGenerator">The generator.</param>
    /// <returns><see langword="false"/> when a player's starting castle could not be placed.</returns>
    /// <remarks>
    /// 三种区域三种待遇：玩家区必须有城堡（放不下就整趟失败）；大的中立区按密度放一座城堡或无主城镇，放不下也无所谓；
    /// 小的中立区不配城堡，只把中心格标成路，好让周围的路有地方接。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static bool PlaceRegionCastle(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        int regionSizeLimit,
        RandomMapResourceDensity density,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        if (region.Id == 0)
        {
            return true;
        }

        if (region.ColorIndex != RandomMapTerrainGenerator.NeutralColorIndex)
        {
            Point castle = region.AdjustRegionToFitCastle(state);

            return PlaceCastle(map, state, region, castle, true, randomGenerator);
        }

        if (region.Nodes.Count > regionSizeLimit)
        {
            Point castle = region.AdjustRegionToFitCastle(state);

            PlaceCastle(map, state, region, castle, density == RandomMapResourceDensity.Abundant, randomGenerator);
        }
        else
        {
            state.GetNodeToUpdate(region.CenterIndex).Type = RandomMapNodeType.Path;
        }

        return true;
    }

    /// <summary>
    /// Picks the basement and the town of a castle and moves its anchor inside the map, the half of
    /// <c>placeCastle()</c> that has to know the sprites (<c>:826-839</c>).
    /// </summary>
    /// <param name="map">The map, for the ground of the anchor tile.</param>
    /// <param name="position">The anchor the region chose.</param>
    /// <param name="townIndex">The entry of <c>KINGDOM_TOWNS</c> to place.</param>
    /// <param name="anchor">The anchor every part of the castle fits at.</param>
    /// <param name="basementInfo">The basement of the ground under <paramref name="anchor"/>.</param>
    /// <param name="townInfo">The town itself.</param>
    /// <returns><see langword="false"/> when the ground is water, the table has no entry, or the castle does not fit.</returns>
    private static bool TryResolveObjects(
        Mp2Map map,
        in Point position,
        int townIndex,
        out Point anchor,
        out ObjectInfo basementInfo,
        out ObjectInfo townInfo)
    {
        anchor = position;
        basementInfo = null!;
        townInfo = null!;

        if (!TryResolveBasement(map, anchor, out basementInfo)
            || !RandomMapObjectRules.TryGetObjectInfo(ObjectGroup.KingdomTowns, townIndex, out townInfo)
            || !TryFitIntoMap(map, anchor, basementInfo, townInfo, out Point fitted))
        {
            return false;
        }

        if (fitted == anchor)
        {
            return true;
        }

        // 挪过之后地面可能换了，地基图要跟着重挑一次。
        anchor = fitted;

        return TryResolveBasement(map, anchor, out basementInfo)
            && TryFitIntoMap(map, anchor, basementInfo, townInfo, out fitted)
            && fitted == anchor;
    }

    /// <summary>Picks the basement of the ground a tile stands on.</summary>
    /// <param name="map">The map.</param>
    /// <param name="position">The anchor tile.</param>
    /// <param name="basementInfo">The basement, when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> for a water tile or a missing catalogue entry.</returns>
    private static bool TryResolveBasement(Mp2Map map, in Point position, out ObjectInfo basementInfo)
    {
        MapGround ground = MapGrounds.FromTerrainImageIndex(map.Tile(position.X, position.Y).TerrainImageIndex);

        if (ground == MapGround.Water)
        {
            basementInfo = null!;

            return false;
        }

        return RandomMapObjectRules.TryGetObjectInfo(
            ObjectGroup.LandscapeTownBasements, BasementIndex(ground), out basementInfo);
    }

    /// <summary>
    /// Moves a castle anchor inside the map so that every part of the castle still lands on a tile,
    /// the guard the MP2 format needs and <c>adjustRegionToFitCastle()</c> does not give.
    /// </summary>
    /// <param name="map">The map, for its size.</param>
    /// <param name="position">The anchor the region chose.</param>
    /// <param name="basementInfo">The basement, whose shadow reaches past the entrance.</param>
    /// <param name="townInfo">The town, whose roof and shadow reach further.</param>
    /// <param name="fitted">The anchor that keeps every part inside the map.</param>
    /// <returns><see langword="false"/> when no anchor can.</returns>
    private static bool TryFitIntoMap(
        Mp2Map map, in Point position, ObjectInfo basementInfo, ObjectInfo townInfo, out Point fitted)
    {
        (int minX, int minY, int maxX, int maxY) = Footprint(basementInfo, townInfo);

        int lowestX = -minX;
        int lowestY = -minY;
        int highestX = map.Width - 1 - maxX;
        int highestY = map.Height - 1 - maxY;

        if (lowestX > highestX || lowestY > highestY)
        {
            fitted = position;

            return false;
        }

        fitted = new Point(Math.Clamp(position.X, lowestX, highestX), Math.Clamp(position.Y, lowestY, highestY));

        return true;
    }

    /// <summary>The bounding box of every part of two objects, in tile offsets.</summary>
    /// <param name="first">The basement.</param>
    /// <param name="second">The town.</param>
    /// <returns>The smallest and largest offset of both objects on each axis.</returns>
    private static (int MinX, int MinY, int MaxX, int MaxY) Footprint(ObjectInfo first, ObjectInfo second)
    {
        int minX = 0, minY = 0, maxX = 0, maxY = 0;

        foreach (ObjectInfo info in new[] { first, second })
        {
            foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
            {
                minX = Math.Min(minX, part.TileOffset.X);
                minY = Math.Min(minY, part.TileOffset.Y);
                maxX = Math.Max(maxX, part.TileOffset.X);
                maxY = Math.Max(maxY, part.TileOffset.Y);
            }

            foreach (ObjectPartInfo part in info.TopLevelParts)
            {
                minX = Math.Min(minX, part.TileOffset.X);
                minY = Math.Min(minY, part.TileOffset.Y);
                maxX = Math.Max(maxX, part.TileOffset.X);
                maxY = Math.Max(maxY, part.TileOffset.Y);
            }
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Puts the hero of a castle's owner on the tile below it, <c>placeCastle()</c>'s
    /// <c>KINGDOM_HEROES</c> write plus the hero block the loader needs (<c>:879-882</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="position">Tile the hero stands on.</param>
    /// <param name="colorIndex">Colour index of the owner, 0…5.</param>
    /// <returns>The metadata word that points at the hero block, <c>0</c> when no hero was written.</returns>
    private static uint PlaceHero(Mp2Map map, RandomMapState state, in Point position, int colorIndex)
    {
        if (!state.IsValidPoint(position))
        {
            return 0;
        }

        // 每种颜色 7 个精灵，最后一个是「随机英雄」——种族留给读图时按王国补。
        int spriteIndex = (colorIndex * HeroSpritesPerColor) + (HeroSpritesPerColor - 1);

        if (!RandomMapObjectWriter.TryWrite(map, position, ObjectGroup.KingdomHeroes, spriteIndex))
        {
            return 0;
        }

        // 全零的英雄块＝默认兵力、默认名字、零经验：读图时按种族给一份初始军队。
        int blockIndex = map.AddInfoBlock(new byte[HeroBlockSize]);

        // 参考不标这一格；端口靠它把装饰阶段挡在外面（见类注释）。
        RandomMapObjectRules.MarkNodeAsType(state, position, RandomMapNodeType.Action);

        return (uint)(blockIndex + 1);
    }

    /// <summary>
    /// Builds the 70 byte town block, <c>Castle::LoadFromMP2()</c>'s input
    /// (<c>castle.cpp:102-395</c>).
    /// </summary>
    /// <param name="color">The owner, <see cref="PlayerColor.None"/> for a town nobody owns.</param>
    /// <param name="race">The race the town is built of.</param>
    /// <param name="isCastle">A fortified castle when <see langword="true"/>.</param>
    /// <returns>The block.</returns>
    /// <remarks>
    /// 其余字节全部留零：自定义建筑位为 0 表示「按难度摇默认建筑」，自定义守军为 0 表示「默认守军」，
    /// 无自定义名字则读图时由 <c>World</c> 补一个默认城镇名。这三样都是参考对一张新图会写的东西。
    /// </remarks>
    private static byte[] BuildCastleBlock(PlayerColor color, MapRace race, bool isCastle)
    {
        byte[] block = new byte[CastleBlockSize];

        block[CastleOwnerOffset] = color == PlayerColor.None
            ? NeutralOwner
            : (byte)PlayerColors.IndexOf(color);
        block[CastleRaceOffset] = (byte)(MapRaces.ToIndex(race) ?? 0);
        block[CastleFortOffset] = isCastle ? (byte)1 : (byte)0;

        return block;
    }

    /// <summary>The player colour of a region's colour index, <c>Color::IndexToColor()</c>.</summary>
    /// <param name="colorIndex">The region's colour index; 0…5 are the players, anything else is neutral.</param>
    /// <returns>The colour, <see cref="PlayerColor.None"/> for a neutral region.</returns>
    private static PlayerColor RegionColor(int colorIndex) =>
        colorIndex >= 0 && colorIndex < PlayerColors.Ordered.Count
            ? PlayerColors.Ordered[colorIndex]
            : PlayerColor.None;
}
