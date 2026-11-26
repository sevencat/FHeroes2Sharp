using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The last step of <c>generateMap()</c>: the name and the description of the map, and the header fields that say who
/// plays it — <c>updatePlayerRelatedObjects()</c> and <c>updateMapPlayers()</c>
/// (<c>map_format_helper.cpp:1287-1334</c> and <c>:1336-1490</c>), plus the two strings of <c>:973-978</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么必须重算</b>：<see cref="Mp2Map.CreateBlank"/> 给的是一张「谁都能玩」的空白头——六个颜色都在局中。生成器
/// 造出来的图只有玩家数那么几个王国，不把表改小，读图时 <c>World</c> 会给没人坐的颜色也建王国，残局判定与 AI 都会跟着错。
/// </para>
/// <para>
/// <b>颜色的来源是物件，不是配置</b>：参考不看 <c>config.playerCount</c>，而是扫一遍地图上的城镇与英雄——有颜色的英雄
/// 说明这个颜色在局中，城镇的旗子说明这座城归谁。端口照做：英雄的 <c>MINIHERO</c> 精灵给出颜色与种族，城镇两侧的旗子
/// 给出颜色。这样生成器以后要是真的实现了「按配置只放几个玩家」，这一层不用改。
/// </para>
/// </remarks>
public static class RandomMapHeader
{
    /// <summary>Applies the header of a generated map.</summary>
    /// <param name="map">The map, with every object already placed.</param>
    /// <param name="config">The configuration, for the description.</param>
    /// <param name="seed">The seed the map was generated with.</param>
    /// <returns>The same map with its header rewritten.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="config"/> is <see langword="null"/>.</exception>
    public static Mp2Map Apply(Mp2Map map, RandomMapConfig config, uint seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(config);

        MapRace[] races = new MapRace[PlayerColors.Ordered.Count];
        bool[] present = new bool[PlayerColors.Ordered.Count];

        ScanHeroes(map, races, present);
        ScanTowns(map, present);

        PlayerColor colors = PlayerColor.None;

        for (int index = 0; index < present.Length; ++index)
        {
            if (present[index])
            {
                colors |= PlayerColors.Ordered[index];
            }
        }

        IReadOnlyList<MapRace> playerRaces = [.. races];

        // 参考在还有颜色可选时保证「至少一个人可以玩」：两个掩码先与可玩颜色取交集，谁都没被指定的颜色两边都给。
        // CreateBlank 的头本来就是「人类与电脑都能玩」，取交集之后正好等于在局中的颜色。
        Mp2FileInfo info = map.Info with
        {
            Name = FitName($"Random map {seed}"),
            Description = Describe(config, seed),
            KingdomColors = colors,
            HumanPlayers = colors,
            ComputerPlayers = colors,
            PlayerRaces = playerRaces,
        };

        return map with { Info = info };
    }

    /// <summary>
    /// Cuts the name down to what the header can hold, so that the map in memory and the file on disk agree.
    /// </summary>
    /// <param name="name">The name the reference would use.</param>
    /// <returns>The name as it comes back after a save and a load.</returns>
    /// <remarks>
    /// MP2 的场景名字段只有 <see cref="Mp2FileInfo.NameFieldLength"/> 字节（含结尾的 0），「Random map 20261126」放不下，
    /// 参考用的是自己的编辑器格式、没有这个限制。与其让内存里与盘上不一样，不如先按字段截断。
    /// </remarks>
    private static string FitName(string name) =>
        MapTextCodec.Decode(MapTextCodec.Encode(name, Mp2FileInfo.NameFieldLength));

    /// <summary>Collects the colours and races of the heroes on the map.</summary>
    /// <param name="map">The map.</param>
    /// <param name="races">Receives the race of every colour; a second race makes it <see cref="MapRace.Multiple"/>.</param>
    /// <param name="present">Receives which colours own at least one hero.</param>
    private static void ScanHeroes(Mp2Map map, MapRace[] races, bool[] present)
    {
        for (int index = 0; index < map.TileCount; ++index)
        {
            if (!Mp2MapHeroes.TryRead(map.Tiles[index], index % map.Width, index / map.Width, out Mp2MapHero hero)
                || hero.Color == PlayerColor.None)
            {
                continue;
            }

            int color = PlayerColors.IndexOf(hero.Color);

            if (color < 0 || color >= present.Length)
            {
                continue;
            }

            present[color] = true;

            if (races[color] == MapRace.None)
            {
                races[color] = hero.Race;
            }
            else if (races[color] != hero.Race)
            {
                races[color] = MapRace.Multiple;
            }
        }
    }

    /// <summary>Marks the colours of the towns that have no hero of their own.</summary>
    /// <param name="map">The map.</param>
    /// <param name="present">Receives which colours own at least one town.</param>
    /// <remarks>
    /// 城镇的颜色写在它两侧的旗子上（<c>LANDSCAPE_FLAGS</c> 的第 <c>颜色 × 2</c> 与 <c>颜色 × 2 + 1</c> 号），左侧那一面
    /// 就够判断主人。无主的城镇不点亮任何颜色——它本来就谁都不属于。
    /// </remarks>
    private static void ScanTowns(Mp2Map map, bool[] present)
    {
        for (int y = 0; y < map.Height; ++y)
        {
            for (int x = 1; x < map.Width; ++x)
            {
                if (map.Tile(x, y).ObjectType is not (
                    Mp2ObjectType.Castle or Mp2ObjectType.ActionCastle
                    or Mp2ObjectType.RandomCastle or Mp2ObjectType.ActionRandomCastle
                    or Mp2ObjectType.RandomTown or Mp2ObjectType.ActionRandomTown))
                {
                    continue;
                }

                Mp2TileInfo flag = map.Tile(x - 1, y);

                if (flag.GroundIcnType != Mp2ObjectIcnType.Flag32)
                {
                    continue;
                }

                int color = flag.BottomIcnImageIndex / 2;

                if (color >= 0 && color < present.Length)
                {
                    present[color] = true;
                }
            }
        }
    }

    /// <summary>The description the reference writes, <c>generateMap()</c>'s last assignment (<c>:975-978</c>).</summary>
    /// <param name="config">The configuration.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The text, which fits the 200 byte field of the header.</returns>
    /// <remarks>
    /// 参考用 <c>_()</c> 把三段风格名翻成玩家语言；端口取 <see cref="RandomMapSizing"/> 那几个 msgid 的英文部分，
    /// 与战役名、场景名的做法一致（端口没有翻译层，ADR-137）。
    /// </remarks>
    private static string Describe(RandomMapConfig config, uint seed) =>
        $"Randomly generated map with seed {seed}, {config.PlayerCount} players, "
        + $"up to {config.WaterPercentage}% of water, {EnglishOf(RandomMapSizing.LayoutName(config.Layout))} layout, "
        + $"{EnglishOf(RandomMapSizing.ResourceDensityName(config.ResourceDensity))} resource density and "
        + $"{EnglishOf(RandomMapSizing.MonsterStrengthName(config.MonsterStrength))} monster strength.";

    /// <summary>The part of a msgid after the context separator, e.g. <c>map_layout|Mirrored</c> becomes <c>Mirrored</c>.</summary>
    /// <param name="msgid">The identifier.</param>
    /// <returns>The English word.</returns>
    private static string EnglishOf(string msgid)
    {
        int separator = msgid.IndexOf('|', StringComparison.Ordinal);

        return separator < 0 ? msgid : msgid[(separator + 1)..];
    }
}
