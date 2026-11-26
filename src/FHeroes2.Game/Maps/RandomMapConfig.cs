namespace FHeroes2.Game.Maps;

/// <summary>
/// The shape of a generated map, <c>Maps::Random_Generator::Layout</c> (<c>map_random_generator.h:33-40</c>).
/// </summary>
public enum RandomMapLayout : byte
{
    /// <summary>A multi-player map with equal distances and distribution of resources, <c>MIRRORED</c>.</summary>
    Mirrored,

    /// <summary>The standard option; some players might have an advantaged position, <c>BALANCED</c>.</summary>
    Balanced,

    /// <summary>A water-based map that needs coastal castles to be navigable, <c>ISLANDS</c>.</summary>
    Islands,

    /// <summary>A map whose AI opponents get resource advantages, <c>PYRAMID</c>.</summary>
    Pyramid,

    /// <summary>A single player map with a non-standard winning condition, <c>QUEST</c>.</summary>
    Quest,
}

/// <summary>
/// How many of what a region of a generated map holds, <c>Maps::Random_Generator::ResourceDensity</c>
/// (<c>map_random_generator.h:42-50</c>).
/// </summary>
public enum RandomMapResourceDensity : byte
{
    /// <summary>Two-tier resources are harder to come by, <c>SCARCE</c>.</summary>
    Scarce,

    /// <summary>Two-tier resources are common and gold mines are rare, <c>NORMAL</c>.</summary>
    Normal,

    /// <summary>Even neutral regions have mines and rare treasures are common, <c>ABUNDANT</c>.</summary>
    Abundant,
}

/// <summary>
/// How hard the neutral stacks of a generated map are, <c>Maps::Random_Generator::MonsterStrength</c>
/// (<c>map_random_generator.h:52-58</c>).
/// </summary>
public enum RandomMapMonsterStrength : byte
{
    /// <summary>A fairly open map, some mines might be unguarded, <c>WEAK</c>.</summary>
    Weak,

    /// <summary>Most monsters are tier two, mines and treasures are protected, <c>NORMAL</c>.</summary>
    Normal,

    /// <summary>Higher tier monsters on average, even wood and ore are protected, <c>STRONG</c>.</summary>
    Strong,

    /// <summary>Neutral stacks are significantly larger and a tier six stack is pre-determined, <c>DEADLY</c>.</summary>
    Deadly,
}

/// <summary>
/// What one region of a generated map holds, <c>Maps::Random_Generator::RegionalObjects</c>
/// (<c>map_random_generator_info.h:283-292</c>) together with the table of <c>map_random_generator.cpp:67-71</c>.
/// </summary>
/// <param name="CastleCount">How many castles the region may take, <c>castleCount</c>.</param>
/// <param name="MineCount">How many secondary mines, <c>mineCount</c>.</param>
/// <param name="GoldMineCount">How many gold mines, <c>goldMineCount</c>.</param>
/// <param name="ObjectCount">How many generic objects, <c>objectCount</c>.</param>
/// <param name="PowerUpsCount">How many power-ups (stat boosters), <c>powerUpsCount</c>.</param>
/// <param name="TreasureCount">How many treasures, <c>treasureCount</c>.</param>
/// <param name="TreasureValueLimit">The total value of the treasures, <c>treasureValueLimit</c>.</param>
public readonly record struct RandomMapRegionalObjects(
    byte CastleCount,
    byte MineCount,
    byte GoldMineCount,
    byte ObjectCount,
    byte PowerUpsCount,
    byte TreasureCount,
    int TreasureValueLimit)
{
    /// <summary>The row of <c>regionObjectSetup</c> of a density, <c>map_random_generator.cpp:67-71</c>.</summary>
    /// <param name="density">The density the map was configured with.</param>
    /// <returns>The counts of a region of that density.</returns>
    /// <remarks>
    /// 三档的规则写在参考的注释里：<c>SCARCE</c> 次等资源难找、<c>NORMAL</c> 金矿稀有、<c>ABUNDANT</c> 连中立区都有矿。
    /// </remarks>
    public static RandomMapRegionalObjects For(RandomMapResourceDensity density) => density switch
    {
        RandomMapResourceDensity.Scarce => new RandomMapRegionalObjects(1, 2, 0, 1, 1, 2, 8500),
        RandomMapResourceDensity.Normal => new RandomMapRegionalObjects(1, 4, 0, 2, 1, 3, 15000),
        RandomMapResourceDensity.Abundant => new RandomMapRegionalObjects(1, 4, 1, 2, 2, 5, 25000),
        _ => throw new ArgumentOutOfRangeException(nameof(density), density, "Unknown resource density."),
    };
}

/// <summary>
/// What the random map generator asks the player, <c>Maps::Random_Generator::Configuration</c>
/// (<c>map_random_generator.h:60-69</c>).
/// </summary>
/// <remarks>
/// 五个旋钮：玩家数、水面百分比、随机种子，以及三档风格。参考把默认值直接写在结构体里（两名玩家、无水、镜像布局、
/// 正常资源、正常怪物、种子 0 表示「开局再摇」），端口照抄，好让没动过的配置与参考给出同一张图。
/// </remarks>
public sealed class RandomMapConfig
{
    /// <summary>Fewest players a generated map supports, the <c>2</c> of <c>generateMap()</c>.</summary>
    public const int MinimumPlayers = 2;

    /// <summary>Most players a generated map supports, the <c>6</c> of <c>generateMap()</c>.</summary>
    public const int MaximumPlayers = 6;

    /// <summary>How many players share the map, <c>playerCount</c>.</summary>
    public int PlayerCount { get; set; } = MinimumPlayers;

    /// <summary>How much of the map is water, in percent, <c>waterPercentage</c>.</summary>
    public int WaterPercentage { get; set; }

    /// <summary>The seed of the generator, <c>seed</c>; <c>0</c> means one is drawn when the map is built.</summary>
    public int Seed { get; set; }

    /// <summary>The shape of the map, <c>mapLayout</c>.</summary>
    public RandomMapLayout Layout { get; set; } = RandomMapLayout.Mirrored;

    /// <summary>How rich the regions are, <c>resourceDensity</c>.</summary>
    public RandomMapResourceDensity ResourceDensity { get; set; } = RandomMapResourceDensity.Normal;

    /// <summary>How hard the neutral stacks are, <c>monsterStrength</c>.</summary>
    public RandomMapMonsterStrength MonsterStrength { get; set; } = RandomMapMonsterStrength.Normal;

    /// <summary>Whether the player count and the water percentage are usable, the guard of <c>generateMap()</c>.</summary>
    /// <returns><see langword="true"/> when the generator may start.</returns>
    public bool IsValid =>
        PlayerCount is >= MinimumPlayers and <= MaximumPlayers
        && WaterPercentage is >= 0 and <= 100;
}

/// <summary>
/// The names, the two sizing formulas and the region table of the random map generator,
/// <c>map_random_generator.cpp:57-109</c> and <c>:551-619</c>.
/// </summary>
/// <remarks>
/// <para>
/// 三个名字返回的是参考的 <b>msgid</b>（<c>map_layout|Mirrored</c> 这类带上下文的写法），由翻译层去掉上下文再查表；
/// 与战役、场景名那几处的做法一致。
/// </para>
/// <para>
/// 两个尺寸公式都是纯算术，端口逐字照抄参考的整数运算顺序——<c>requiredSpace * 100 / 60</c> 这类整数除法一旦换成浮点
/// 就会偏一两个格子，进而换掉整张图的区域划分。
/// </para>
/// </remarks>
public static class RandomMapSizing
{
    /// <summary>Smallest region that can hold one player's start, <c>smallestStartingRegionSize</c>.</summary>
    public const int SmallestStartingRegionSize = 200;

    /// <summary>How much of a region stays empty, <c>emptySpacePercentage</c>.</summary>
    public const int EmptySpacePercentage = 40;

    /// <summary>Tiles a castle's footprint takes when the region is measured, the <c>49</c> of <c>:82</c>.</summary>
    public const int CastleFootprint = 49;

    /// <summary>Tiles one secondary mine takes, the <c>15</c> of <c>:83</c>.</summary>
    public const int MineFootprint = 15;

    /// <summary>Tiles one generic object takes, the <c>6</c> of <c>:84</c>.</summary>
    public const int ObjectFootprint = 6;

    /// <summary>Nine tiles per power-up, the <c>9</c> of <c>:85</c>.</summary>
    public const int PowerUpFootprint = 9;

    /// <summary>Sixteen tiles per treasure, the <c>16</c> of <c>:86</c>.</summary>
    public const int TreasureFootprint = 16;

    /// <summary>A layout's display name, <c>layoutToString()</c> (<c>map_random_generator.cpp:551-571</c>).</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The msgid, e.g. <c>map_layout|Mirrored</c>.</returns>
    public static string LayoutName(RandomMapLayout layout) => layout switch
    {
        RandomMapLayout.Mirrored => "map_layout|Mirrored",
        RandomMapLayout.Balanced => "map_layout|Balanced",
        RandomMapLayout.Islands => "map_layout|Islands",
        RandomMapLayout.Pyramid => "map_layout|Pyramid",
        _ => "map_layout|Quest",
    };

    /// <summary>A density's display name, <c>resourceDensityToString()</c> (<c>:573-589</c>).</summary>
    /// <param name="density">The density.</param>
    /// <returns>The msgid, e.g. <c>resource_density|Normal</c>.</returns>
    public static string ResourceDensityName(RandomMapResourceDensity density) => density switch
    {
        RandomMapResourceDensity.Scarce => "resource_density|Scarce",
        RandomMapResourceDensity.Normal => "resource_density|Normal",
        _ => "resource_density|Abundant",
    };

    /// <summary>A monster strength's display name, <c>monsterStrengthToString()</c> (<c>:591-609</c>).</summary>
    /// <param name="strength">The strength.</param>
    /// <returns>The msgid, e.g. <c>monster_strength|Deadly</c>.</returns>
    public static string MonsterStrengthName(RandomMapMonsterStrength strength) => strength switch
    {
        RandomMapMonsterStrength.Weak => "monster_strength|Weak",
        RandomMapMonsterStrength.Normal => "monster_strength|Normal",
        RandomMapMonsterStrength.Strong => "monster_strength|Strong",
        _ => "monster_strength|Deadly",
    };

    /// <summary>
    /// The wettest a map of this size can be and still start every player, <c>calculateMaximumWaterPercentage()</c>
    /// (<c>:611-619</c>).
    /// </summary>
    /// <param name="playerCount">How many players have to get a start.</param>
    /// <param name="mapWidth">Edge length of the square map.</param>
    /// <returns>The percentage, never negative; <c>0</c> for an unusable map size.</returns>
    /// <remarks>
    /// 每位玩家（再加一个中立区）至少要 <see cref="SmallestStartingRegionSize"/> 格陆地，剩下的才是水。
    /// 地图小于这个下限时答案是 0，而不是负数。
    /// </remarks>
    public static int MaximumWaterPercentage(int playerCount, int mapWidth)
    {
        if (playerCount <= 0 || mapWidth <= 0)
        {
            return 0;
        }

        int minimumRegionCount = playerCount + 1;
        int tileCount = mapWidth * mapWidth;
        int waterTiles = tileCount - (SmallestStartingRegionSize * minimumRegionCount);

        return Math.Max(0, waterTiles * 100 / tileCount);
    }

    /// <summary>
    /// How many tiles one region of the generated map takes, <c>calculateRegionSizeLimit()</c>
    /// (<c>map_random_generator.cpp:73-108</c>).
    /// </summary>
    /// <param name="config">The generator configuration.</param>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map, the second half of the tile count.</param>
    /// <returns>The size of a region in tiles; <c>0</c> when the configuration cannot be used.</returns>
    /// <remarks>
    /// <para>
    /// 参考先按密度算出「一个区域要放多少东西」（城堡 49 格、矿 15 格、物件 6 格、属性点 9 格、宝箱 16 格），
    /// 留出 40% 空地，再按圆形周长补一圈边距，得到目标区域面积；然后用陆地块数除以目标面积估出能放几个区域，
    /// 夹在「玩家数 + 1」与「玩家数 × 3」之间，最后把陆地块数按这个区域数均分。
    /// </para>
    /// <para>
    /// 配置不合法（玩家数越界、水面百分比越界、尺寸非正）时返回 0：参考在 <c>generateMap()</c> 里正是用
    /// <c>regionSizeLimit &lt;= 0</c> 判失败，端口保留同一个判据。
    /// </para>
    /// </remarks>
    public static int RegionSizeLimit(RandomMapConfig config, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.IsValid || width <= 0 || height <= 0)
        {
            return 0;
        }

        RandomMapRegionalObjects objects = RandomMapRegionalObjects.For(config.ResourceDensity);

        int requiredSpace = (objects.CastleCount * CastleFootprint)
            + (objects.MineCount * MineFootprint)
            + (objects.ObjectCount * ObjectFootprint)
            + (objects.PowerUpsCount * PowerUpFootprint)
            + (objects.TreasureCount * TreasureFootprint);

        requiredSpace = requiredSpace * 100 / (100 - EmptySpacePercentage);

        double innerRadius = Math.Ceiling(Math.Sqrt(requiredSpace / Math.PI));
        int borderSize = (int)(2 * (innerRadius + 1) * Math.PI);
        int targetRegionSize = requiredSpace + borderSize;

        int upperLimit = config.PlayerCount * 3;
        int totalTileCount = width * height;
        int groundTiles = totalTileCount * (100 - config.WaterPercentage) / 100;

        if (targetRegionSize <= 0)
        {
            return 0;
        }

        int average = groundTiles / targetRegionSize;
        int canFit = Math.Min(Math.Max(config.PlayerCount + 1, average), upperLimit);

        return groundTiles / canFit;
    }
}
