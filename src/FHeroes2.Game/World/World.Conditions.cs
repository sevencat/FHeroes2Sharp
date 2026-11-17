using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game;

/// <summary>
/// The win and loss conditions of the scenario, the part of <c>World</c> that answers "has this kingdom won
/// yet": <c>GetHeroesCondWins</c>, <c>GetHeroesCondLoss</c>, <c>KingdomIsWins</c>, <c>KingdomIsLoss</c>,
/// <c>CheckKingdomWins</c> and <c>CheckKingdomLoss</c> (<c>world.cpp:941-1157</c>), plus
/// <c>setHeroIdsForMapConditions()</c> (<c>world_loadmap.cpp:1758</c>) which remembers the two heroes the
/// conditions name.
/// </summary>
/// <remarks>
/// <para>
/// 参考把地图条件当成两样东西：<c>Settings</c> 里那份 <c>Maps::FileInfo</c> 给出「位掩码 + 参数」，本类给出
/// 「拿世界的当前状态去比对」。所以每次判定都从 <see cref="MapConditions"/> 取一次位掩码，再按单个条件分支。
/// </para>
/// <para>
/// <b>谁是「实际在场」的颜色</b>：<c>WINS_ALL</c>/<c>WINS_SIDE</c> 关心同盟，参考问的是 <c>Game</c> 与
/// <c>Settings</c> 里的玩家槽。端口还没有开局流程去填那些槽，而槽里的颜色就是装载地图时由同一份地图文件头建立的，
/// 所以这里直接问王国（<see cref="Kingdoms.GetColors"/>）。
/// </para>
/// <para>
/// <b>没移植的部分</b>：战役（<c>Campaign</c>）的两条特殊条件——「占龙城即胜」与「女巫村全失即负」——还没有战役模块，
/// 因此 <see cref="CheckKingdomWins"/> 与 <see cref="CheckKingdomLoss"/> 直接从普通条件开始。
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>The heroes a <c>WINS_ALL</c> check walks, in the reference's order.</summary>
    private static readonly uint[] VictoryConditions =
    [
        GameOver.WinsAll,
        GameOver.WinsTown,
        GameOver.WinsHero,
        GameOver.WinsArtifact,
        GameOver.WinsSide,
        GameOver.WinsGold,
    ];

    /// <summary>The losses a <c>LOSS_*</c> check walks, in the reference's order.</summary>
    private static readonly uint[] DefeatConditions =
    [
        GameOver.LossAll,
        GameOver.LossTown,
        GameOver.LossHero,
        GameOver.LossTime,
    ];

    /// <summary>
    /// Victory conditions of the other kingdoms and the loss each of them causes,
    /// <c>enemyVictoryConditions</c> (<c>world.cpp:1121-1124</c>).
    /// </summary>
    private static readonly (uint Victory, uint Defeat)[] EnemyVictoryConditions =
    [
        (GameOver.WinsTown, GameOver.LossEnemyWinsTown),
        (GameOver.WinsArtifact, GameOver.LossEnemyWinsArtifact),
        (GameOver.WinsGold, GameOver.LossEnemyWinsGold),
    ];

    /// <summary>
    /// The map block the world was loaded from, <c>Settings::Get().getCurrentMapInfo()</c> 的替身.
    /// </summary>
    /// <remarks>
    /// 参考不把文件头存在世界里，它每次判定都去 <c>Settings</c> 拿。端口反过来：文件头就是 <see cref="LoadMap"/>
    /// 的入参，存在世界里既少一次全局查表，也让判定能在没有 <c>Settings</c> 的测试里跑。
    /// </remarks>
    public Mp2FileInfo? MapInfo { get; private set; }

    /// <summary>
    /// The buried ultimate artifact, <c>getUltimateArtifact()</c> (<c>world.h:358</c>).
    /// </summary>
    /// <remarks>
    /// The world owns it because its position is a property of the map, not of a hero: the same artifact
    /// answers the <c>WINS_ARTIFACT</c> check, the Eye of Magi and the digging. It holds nothing until a
    /// map is loaded — the port does not place it yet (see the note on <see cref="LoadMap"/>).
    /// </remarks>
    public UltimateArtifact UltimateArtifact => _ultimateArtifact;

    /// <summary>The hero whose defeat wins the scenario, <c>heroIdAsWinCondition</c> (<c>world.h:697</c>).</summary>
    private int _heroIdAsWinCondition = Hero.UnknownId;

    /// <summary>The hero whose loss loses the scenario, <c>heroIdAsLossCondition</c> (<c>world.h:698</c>).</summary>
    private int _heroIdAsLossCondition = Hero.UnknownId;

    /// <summary>
    /// The hero the victory condition names, <c>World::GetHeroesCondWins()</c> (<c>world.cpp:941</c>).
    /// </summary>
    /// <returns>The hero, or <see langword="null"/> when the scenario has no such condition or no such hero.</returns>
    public Hero? GetHeroesCondWins()
    {
        Mp2FileInfo? info = MapInfo;

        return info is not null && (MapConditions.Wins(info) & GameOver.WinsHero) != 0
            ? GetHero(_heroIdAsWinCondition)
            : null;
    }

    /// <summary>
    /// The hero the loss condition names, <c>World::GetHeroesCondLoss()</c> (<c>world.cpp:946</c>).
    /// </summary>
    /// <returns>The hero, or <see langword="null"/> when the scenario has no such condition or no such hero.</returns>
    public Hero? GetHeroesCondLoss()
    {
        Mp2FileInfo? info = MapInfo;

        return info is not null && (MapConditions.Loss(info) & GameOver.LossHero) != 0
            ? GetHero(_heroIdAsLossCondition)
            : null;
    }

    /// <summary>
    /// Whether one condition, on its own, is met by <paramref name="kingdom"/>,
    /// <c>World::KingdomIsWins()</c> (<c>world.cpp:951</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 一次只判一位，调用方（<see cref="CheckKingdomWins"/> 与 AI）决定问哪一位。参考对
    /// <c>WINS_ALL</c>/<c>WINS_HERO</c>/<c>WINS_SIDE</c> 断言「只有人类控制的王国才会被问」，端口不做这个断言：
    /// 断言在发布构建里什么都不做，而这里的取值逻辑对电脑控制的王国同样成立。
    /// </para>
    /// <para>
    /// <b>坐标从哪来</b>：占城看的是地图条件里的 (x, y)，换算成关卡门所在的那一格再查城镇表——条件记的是城门，
    /// 不是城镇中心。
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom being tested.</param>
    /// <param name="wins">Exactly one <see cref="GameOver"/> winning flag.</param>
    /// <returns>True when that condition is met.</returns>
    public bool KingdomIsWins(Kingdom.Kingdom kingdom, uint wins)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (MapInfo is not { } info)
        {
            return false;
        }

        switch (wins)
        {
            case GameOver.WinsAll:
                return kingdom.Color == _kingdoms.GetNotLossColors();

            case GameOver.WinsTown:
            {
                CastleTown? town = GetCastleEntrance(MapConditions.WinsPosition(info));

                return (kingdom.IsControlHuman || MapConditions.ComputerAlsoWins(info))
                       && town is not null
                       && town.Color == kingdom.Color;
            }

            case GameOver.WinsHero:
            {
                if (_heroIdAsWinCondition == Hero.UnknownId)
                {
                    return false;
                }

                Hero? hero = GetHeroesCondWins();

                // 参考这里 assert( hero != nullptr )：条件说有这么一位英雄，地图上却没有，属于坏地图。端口在
                // 装载时就已记过一条错误日志，判定时把「没有」当成「条件未满足」。
                return hero is not null && (hero.IsAvailableForHire || GetKingdom(hero.Color).IsControlHuman);
            }

            case GameOver.WinsArtifact:
            {
                if (MapConditions.WinsUltimateArtifact(info))
                {
                    return kingdom.Heroes.Any(hero => hero.BagArtifacts.ContainsUltimateArtifact());
                }

                ArtifactType artifact = MapConditions.WinsArtifact(info);

                return kingdom.Heroes.Any(hero => hero.BagArtifacts.IsPresent(artifact));
            }

            case GameOver.WinsSide:
            {
                // 参考这里只会问人类控制的王国，而且 <c>Settings</c> 里一定有对应的玩家槽（<c>Players::GetPlayerFriends</c>
                // 直接解引用）。端口还没有开局流程去建槽，没有槽时把「自己」当成唯一的盟友——这正是槽存在但不与人
                // 联机共享操作权时的取值（<c>Player::GetFriends()</c> 返回 <c>_friends | GetColor()</c>）。
                PlayerColor friends = Players.GetPlayerFriends(kingdom.Color);

                if (friends == PlayerColor.None)
                {
                    friends = kingdom.Color;
                }

                return (_kingdoms.GetColors() & ~friends) == PlayerColor.None;
            }

            case GameOver.WinsGold:
            {
                int gold = kingdom.Treasury.Gold;

                return (kingdom.IsControlHuman || MapConditions.ComputerAlsoWins(info))
                       && gold > 0
                       && (uint)gold >= MapConditions.WinningGold(info);
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether one loss condition, on its own, is met by <paramref name="kingdom"/>,
    /// <c>World::KingdomIsLoss()</c> (<c>world.cpp:1014</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>LOSS_HERO</c> 的语义是「守的那位英雄不在自己人手里了」：他回到酒馆（无处可雇）算输，被电脑控制的王国雇走
    /// 也算输，除非那个位置此刻由电脑代管人类玩家。参考在战役里还有一条「同阵营的电脑雇走不算输」的例外，端口没有
    /// 战役模块，跳过。
    /// </para>
    /// <para>
    /// 判定只管单个条件；「对手先达成了胜利条件，所以我输」的三位在 <see cref="CheckKingdomLoss"/> 里查。
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom being tested.</param>
    /// <param name="loss">Exactly one <see cref="GameOver"/> losing flag.</param>
    /// <returns>True when that condition is met.</returns>
    public bool KingdomIsLoss(Kingdom.Kingdom kingdom, uint loss)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (MapInfo is not { } info)
        {
            return false;
        }

        switch (loss)
        {
            case GameOver.LossAll:
                return kingdom.IsLoss;

            case GameOver.LossTown:
            {
                CastleTown? town = GetCastleEntrance(MapConditions.LossPosition(info));

                return town is not null && town.Color != kingdom.Color;
            }

            case GameOver.LossHero:
            {
                if (_heroIdAsLossCondition == Hero.UnknownId)
                {
                    return false;
                }

                Hero? hero = GetHeroesCondLoss();

                if (hero is null)
                {
                    return false;
                }

                if (hero.IsAvailableForHire)
                {
                    return true;
                }

                bool isHeroInAiAutoControlMode = Players.Get(hero.Color)?.IsAiAutoControlMode ?? false;

                return !GetKingdom(hero.Color).IsControlHuman && !isHeroInAiAutoControlMode;
            }

            case GameOver.LossTime:
                return DayCount > MapConditions.LossDays(info);

            default:
                return false;
        }
    }

    /// <summary>
    /// The winning condition <paramref name="kingdom"/> has met, <c>World::CheckKingdomWins()</c>
    /// (<c>world.cpp:1076</c>).
    /// </summary>
    /// <remarks>
    /// 按 <c>WINS_ALL</c>、<c>WINS_TOWN</c>、<c>WINS_HERO</c>、<c>WINS_ARTIFACT</c>、<c>WINS_SIDE</c>、
    /// <c>WINS_GOLD</c> 的顺序问，返回第一位满足的——所以一张同时允许全灭与占城的图，全灭会先报。条件里没有的位
    /// 不问，因为「只许按我定的规则赢」正是 <c>allowNormalVictory</c> 的用途。
    /// </remarks>
    /// <param name="kingdom">The kingdom being tested.</param>
    /// <returns>The condition met, or <see cref="GameOver.ConditionNone"/>.</returns>
    public uint CheckKingdomWins(Kingdom.Kingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (MapInfo is not { } info)
        {
            return GameOver.ConditionNone;
        }

        uint conditions = MapConditions.Wins(info);

        foreach (uint condition in VictoryConditions)
        {
            if ((conditions & condition) == condition && KingdomIsWins(kingdom, condition))
            {
                return condition;
            }
        }

        return GameOver.ConditionNone;
    }

    /// <summary>
    /// The losing condition <paramref name="kingdom"/> has met, <c>World::CheckKingdomLoss()</c>
    /// (<c>world.cpp:1108</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 先查「别人赢了所以我输」：条件里允许占城/找神器/攒金胜利时，问一遍所有王国有没有人已经做到，做到了而且不是我，
    /// 就返回对应的 <c>LOSS_ENEMY_WINS_*</c>。然后再按 <c>LOSS_ALL</c>、<c>LOSS_TOWN</c>、<c>LOSS_HERO</c>、
    /// <c>LOSS_TIME</c> 的顺序查自己的损失。
    /// </para>
    /// <para>
    /// 顺序不能换：参考把它写在前面，因为对手先达成条件时「我已经没有城镇了」也可能是同一天的事，先报的那条才是玩家
    /// 看到的失败原因。
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom being tested.</param>
    /// <returns>The condition met, or <see cref="GameOver.ConditionNone"/>.</returns>
    public uint CheckKingdomLoss(Kingdom.Kingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (MapInfo is not { } info)
        {
            return GameOver.ConditionNone;
        }

        uint wins = MapConditions.Wins(info);

        foreach ((uint victory, uint defeat) in EnemyVictoryConditions)
        {
            if ((wins & victory) == 0)
            {
                continue;
            }

            PlayerColor color = _kingdoms.FindWins(victory, this);

            if (color != PlayerColor.None && color != kingdom.Color)
            {
                return defeat;
            }
        }

        uint losses = MapConditions.Loss(info);

        foreach (uint condition in DefeatConditions)
        {
            if ((losses & condition) == condition && KingdomIsLoss(kingdom, condition))
            {
                return condition;
            }
        }

        return GameOver.ConditionNone;
    }

    /// <summary>
    /// Remembers the two heroes the map conditions name, <c>World::setHeroIdsForMapConditions()</c>
    /// (<c>world_loadmap.cpp:1758</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 条件里记的是英雄站的那一格，不是英雄 id，所以要在地图装完之后按位置反查一次；守的那位还额外打上
    /// <see cref="HeroModes.NotDismiss"/>，免得玩家把他解雇掉绕过失败条件（<see cref="HeroModes.Custom"/> 是参考
    /// 顺手打的，表示这位英雄的属性来自地图而不是随机生成）。
    /// </para>
    /// <para>
    /// <b>和参考的差别</b>：参考在找不到英雄时返回 <see langword="false"/>，让整张地图装载失败。端口的
    /// <see cref="LoadMap"/> 是「坏输入抛异常」的契约，而这里已经装了一半，抛出去只会留下半个世界；于是记一条错误日志
    /// 并继续，效果是该条件永远不满足，其它条件照常。等有开局错误对话框时，这里应该改成让装载失败。
    /// </para>
    /// </remarks>
    /// <returns>True when both heroes were found (or were not needed).</returns>
    private bool SetHeroIdsForMapConditions()
    {
        if (MapInfo is not { } info)
        {
            return true;
        }

        bool result = true;

        if ((MapConditions.Wins(info) & GameOver.WinsHero) != 0)
        {
            int tileIndex = ToTileIndex(MapConditions.WinsPosition(info));
            Hero? hero = tileIndex >= 0 ? _heroes.GetAtMapIndex(tileIndex) : null;

            if (hero is null)
            {
                GameLog.Error($"The hero whose defeat wins the map was not found at {MapConditions.WinsPosition(info)}.");

                result = false;
            }
            else
            {
                _heroIdAsWinCondition = hero.Id;
            }
        }

        if ((MapConditions.Loss(info) & GameOver.LossHero) != 0)
        {
            int tileIndex = ToTileIndex(MapConditions.LossPosition(info));
            Hero? hero = tileIndex >= 0 ? _heroes.GetAtMapIndex(tileIndex) : null;

            if (hero is null)
            {
                GameLog.Error($"The hero whose defeat loses the map was not found at {MapConditions.LossPosition(info)}.");

                result = false;
            }
            else
            {
                _heroIdAsLossCondition = hero.Id;
                hero.SetModes(HeroModes.NotDismiss | HeroModes.Custom);
            }
        }

        return result;
    }

    /// <summary>Tile index of an absolute map position, or -1 when it is off the map.</summary>
    /// <param name="position">The absolute position.</param>
    /// <returns>The index, or -1.</returns>
    private int ToTileIndex(Point position) =>
        IsInsideMap(position.X, position.Y) ? position.Y * Width + position.X : -1;
}
