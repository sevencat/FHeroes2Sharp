using System.Globalization;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// The two text producers of the reference: the short condition table <c>GameOver::GetString()</c>
/// (<c>game_over.cpp:278-306</c>) and the sentence built for the win/lose dialogs,
/// <c>GameOver::GetActualDescription()</c> (<c>game_over.cpp:308-467</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>GetActualDescription()</c> 返回 <c>std::vector&lt;LocalizedString&gt;</c>，因为地图里写死的名字（城名、英雄名）
/// 要按地图自己的语言单独本地化，模板与名字各是一段。端口没有这一层：名字直接替换进句子，返回一个字符串。
/// 拼接结果与参考一致，差别只在参考能把每一段分别换语言。
/// </para>
/// <para>
/// 参考在地图对象缺失时 <c>assert</c> 后把整句留空（城被拆了、英雄已被删除这类编辑器写坏的图）。端口不 assert，
/// 落到 <see cref="GetString"/> 的通用句式上——宁可显示「占领一座城」也不要给玩家一句空白。
/// </para>
/// </remarks>
public static partial class GameOver
{
    /// <summary>
    /// The one-line description of a single condition, <c>GameOver::GetString()</c> (<c>game_over.cpp:278</c>).
    /// </summary>
    /// <param name="condition">One condition of <see cref="GameOver"/>; anything else gives <c>"None"</c>.</param>
    /// <returns>The text, in the reference's English.</returns>
    public static string GetString(uint condition) => condition switch
    {
        WinsAll => "Defeat all enemy heroes and capture all enemy towns and castles.",
        WinsTown => "Capture a specific town.",
        WinsHero => "Defeat a specific hero.",
        WinsArtifact => "Find a specific artifact.",
        WinsSide => "Your side defeats the opposing side.",
        WinsGold => "Accumulate a large amount of gold.",
        LossAll => "Lose all your heroes and towns.",
        LossTown => "Lose a specific town.",
        LossHero => "Lose a specific hero.",
        LossTime => "Run out of time. (Fail to win by a certain point.)",
        _ => "None",
    };

    /// <summary>
    /// The sentence the win/lose dialogs print for a set of conditions, <c>GameOver::GetActualDescription()</c>
    /// (<c>game_over.cpp:308</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与参考一样只讲第一段匹配到的胜利条件（<c>if / else if</c> 链），再补一句「也可以全灭对手」；失败条件覆盖掉胜利部分，
    /// 因为被判负时玩家最需要知道的是怎么输的。
    /// </para>
    /// <para>
    /// 占城、杀将、找神器三种要地图对象的名字，所以需要 <paramref name="world"/>；只讲条件本身的几种（全灭、同盟、
    /// 攒金、超时）不碰世界，传 <see langword="null"/> 也能算。
    /// </para>
    /// </remarks>
    /// <param name="conditions">The verdict of <see cref="GameOverResult.CheckGameOver"/>.</param>
    /// <param name="info">The map information block the parameters come from.</param>
    /// <param name="world">The world to look objects up in; <see cref="World.Current"/> when omitted.</param>
    /// <returns>The sentence, or an empty string when the conditions carry no text at all.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public static string GetDescription(uint conditions, Mp2FileInfo info, World? world = null)
    {
        ArgumentNullException.ThrowIfNull(info);

        world ??= World.Current;

        string message = string.Empty;

        if (conditions == WinsAll)
        {
            message = GetString(WinsAll);
        }
        else if (conditions == WinsSide)
        {
            message = DescribeSide(conditions);
        }
        else if ((conditions & WinsTown) != 0)
        {
            Castle.Castle? town = world?.GetCastleEntrance(MapConditions.WinsPosition(info));

            if (town is not null)
            {
                message = (town.IsCastle ? "Capture the castle '{0}'." : "Capture the town '{0}'.").Replace("{0}", town.Name, StringComparison.Ordinal);
            }
            else
            {
                message = GetString(WinsTown);
            }
        }
        else if ((conditions & WinsHero) != 0)
        {
            Heroes.Hero? hero = world?.GetHeroesCondWins();

            message = hero is null
                ? GetString(WinsHero)
                : "Defeat the hero '{0}'.".Replace("{0}", hero.Name, StringComparison.Ordinal);
        }
        else if ((conditions & WinsArtifact) != 0)
        {
            message = MapConditions.WinsUltimateArtifact(info)
                ? "Find the ultimate artifact."
                : "Find the '{0}' artifact.".Replace(
                    "{0}", ArtifactTable.Get(MapConditions.WinsArtifact(info)).Name, StringComparison.Ordinal);
        }
        else if ((conditions & WinsGold) != 0)
        {
            message = "Accumulate {0} gold.".Replace(
                "{0}", MapConditions.WinningGold(info).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        if (WinsAll != conditions && (conditions & WinsAll) != 0)
        {
            message += ", or you may win by defeating all enemy heroes and capturing all enemy towns and castles.";
        }

        if (conditions == LossAll)
        {
            message = GetString(LossAll);
        }
        else if ((conditions & LossTown) != 0)
        {
            Castle.Castle? town = world?.GetCastleEntrance(MapConditions.LossPosition(info));

            if (town is not null)
            {
                message = (town.IsCastle ? "Lose the castle '{0}'." : "Lose the town '{0}'.").Replace("{0}", town.Name, StringComparison.Ordinal);
            }
            else
            {
                message = GetString(LossTown);
            }
        }
        else if ((conditions & LossHero) != 0)
        {
            Heroes.Hero? hero = world?.GetHeroesCondLoss();

            message = hero is null
                ? GetString(LossHero)
                : "Lose the hero: {0}.".Replace("{0}", hero.Name, StringComparison.Ordinal);
        }
        else if ((conditions & LossTime) != 0)
        {
            // 参考把最后一天算成「第 N 天结束」，所以先减一再拆成年月周。
            uint dayCount = MapConditions.LossDays(info) - 1;
            uint month = dayCount / (uint)(World.NumOfDaysPerWeek * World.NumOfWeeksPerMonth);
            uint week = (dayCount - month * (uint)(World.NumOfDaysPerWeek * World.NumOfWeeksPerMonth)) / World.NumOfDaysPerWeek;
            uint day = dayCount % World.NumOfDaysPerWeek;

            message = "Fail to win by the end of month {0}, week {1}, day {2}."
                .Replace("{0}", (month + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", (week + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{2}", (day + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        return message;
    }

    /// <summary>
    /// The body of the victory dialog, the <c>DialogWins()</c> text table of <c>game_over.cpp:71-155</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="GetDescription"/> 不同，这里只认完全相等的一条条件（参考是 <c>switch</c>）：判定结果每次只带一条，
    /// 所以相等成立，混着别的位就没有文案。
    /// </para>
    /// <para>
    /// <see cref="WinsAll"/> 故意没有文案——被全灭的对手只剩这一种可能，参考不为它弹窗。战役「龙之城」的特殊提示要
    /// 战役系统的当前关条件，端口还没有战役存档，等它落地再补。
    /// </para>
    /// </remarks>
    /// <param name="condition">The verdict of <see cref="GameOverResult.CheckGameOver"/>.</param>
    /// <param name="info">The map information block the parameters come from.</param>
    /// <param name="world">The world to look objects up in; <see cref="World.Current"/> when omitted.</param>
    /// <returns>The body text, or an empty string when this condition has no dialog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public static string GetVictoryMessage(uint condition, Mp2FileInfo info, World? world = null)
    {
        ArgumentNullException.ThrowIfNull(info);

        world ??= World.Current;

        switch (condition)
        {
            case WinsTown:
            {
                Castle.Castle? town = world?.GetCastleEntrance(MapConditions.WinsPosition(info));

                return town is null
                    ? string.Empty
                    : "You captured {0}!\nYou are victorious.".Replace("{0}", town.Name, StringComparison.Ordinal);
            }

            case WinsHero:
            {
                Heroes.Hero? hero = world?.GetHeroesCondWins();

                return hero is null
                    ? string.Empty
                    : "You have captured the enemy hero {0}!\nYour quest is complete."
                        .Replace("{0}", hero.Name, StringComparison.Ordinal);
            }

            case WinsArtifact:
                return "You have found the {0}.\nYour quest is complete.".Replace(
                    "{0}", MapConditions.WinsUltimateArtifact(info) ? "Ultimate Artifact" : ArtifactTable.Get(MapConditions.WinsArtifact(info)).Name,
                    StringComparison.Ordinal);

            case WinsSide:
                return "The enemy is beaten.\nYour side has triumphed!";

            case WinsGold:
                return "You have built up over {0} gold in your treasury.\nAll enemies bow before your wealth and power."
                    .Replace("{0}", MapConditions.WinningGold(info).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// The body of the defeat dialog, the <c>DialogLoss()</c> text table of <c>game_over.cpp:157-246</c>.
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetVictoryMessage"/> 一样按完全相等匹配。对「对手靠某条胜利条件赢下」的三条，参考去看的还是
    /// <c>WinsMapsPositionObject()</c> 与胜利参数——对手达到的就是这张图写明的那个目标。
    /// </remarks>
    /// <param name="condition">The verdict of <see cref="GameOverResult.CheckGameOver"/>.</param>
    /// <param name="info">The map information block the parameters come from.</param>
    /// <param name="world">The world to look objects up in; <see cref="World.Current"/> when omitted.</param>
    /// <returns>The body text, or an empty string when this condition has no dialog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public static string GetLossMessage(uint condition, Mp2FileInfo info, World? world = null)
    {
        ArgumentNullException.ThrowIfNull(info);

        world ??= World.Current;

        switch (condition)
        {
            case LossEnemyWinsTown:
            case LossTown:
            {
                Point position = condition == LossTown
                    ? MapConditions.LossPosition(info)
                    : MapConditions.WinsPosition(info);
                Castle.Castle? town = world?.GetCastleEntrance(position);

                return town is null
                    ? string.Empty
                    : "The enemy has captured {0}!\nThey are triumphant.".Replace("{0}", town.Name, StringComparison.Ordinal);
            }

            case LossEnemyWinsArtifact:
                return "The enemy has found the {0}.\nYour quest is a failure.".Replace(
                    "{0}", MapConditions.WinsUltimateArtifact(info) ? "Ultimate Artifact" : ArtifactTable.Get(MapConditions.WinsArtifact(info)).Name,
                    StringComparison.Ordinal);

            case LossEnemyWinsGold:
                // 参考原文就是 “bow done”，端口照抄，免得以后对着原文找不到这句。
                return "The enemy has built up over {0} gold in his treasury.\nYou must bow done in defeat before his wealth and power."
                    .Replace("{0}", MapConditions.WinningGold(info).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

            case LossAll:
                return "You have been eliminated from the game!!!";

            case LossHero:
            {
                Heroes.Hero? hero = world?.GetHeroesCondLoss();

                return hero is null
                    ? string.Empty
                    : "You have lost the hero {0}.\nYour quest is over.".Replace("{0}", hero.Name, StringComparison.Ordinal);
            }

            case LossTime:
                return "You have failed to complete your quest in time.\nAll is lost.";

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// The alliance sentence of <see cref="GetDescription"/>, <c>game_over.cpp:322-362</c>.
    /// </summary>
    /// <param name="conditions">The conditions, used only for the fallback text.</param>
    /// <returns>The sentence.</returns>
    private static string DescribeSide(uint conditions)
    {
        Players players = Settings.Get().Players;
        Player? current = players.Current;

        if (current is null)
        {
            return GetString(conditions);
        }

        PlayerColor friendColors = current.Friends;
        PlayerColor enemyColors = players.GetColors() & ~friendColors;

        (string allies, int allyCount) = ListPlayers(friendColors & ~current.Color);
        (string enemies, int enemyCount) = ListPlayers(enemyColors);

        if (enemyCount == 0)
        {
            return GetString(conditions);
        }

        // 参考用 `_n()` 在单数/复数两套句子里挑；端口按人数挑同一对句子。
        if (allyCount == 0)
        {
            return enemyCount == 1
                ? "You must defeat the enemy {0}.".Replace("{0}", enemies, StringComparison.Ordinal)
                : "You must defeat the enemy alliance of {0}.".Replace("{0}", enemies, StringComparison.Ordinal);
        }

        return enemyCount == 1
            ? "The alliance consisting of {0} and you must defeat the enemy {1}."
                .Replace("{0}", allies, StringComparison.Ordinal)
                .Replace("{1}", enemies, StringComparison.Ordinal)
            : "The alliance consisting of {0} and you must defeat the enemy alliance of {1}."
                .Replace("{0}", allies, StringComparison.Ordinal)
                .Replace("{1}", enemies, StringComparison.Ordinal);
    }

    /// <summary>
    /// Joins the names of the players of a colour set, <c>game_over.cpp:329-346</c>.
    /// </summary>
    /// <param name="colors">The colours to list.</param>
    /// <returns>The comma separated names and how many players were listed.</returns>
    private static (string Names, int Count) ListPlayers(PlayerColor colors)
    {
        string names = string.Empty;
        int count = 0;

        foreach (PlayerColor color in PlayerColors.Ordered)
        {
            if ((colors & color) == 0)
            {
                continue;
            }

            Player? player = Players.Get(color);

            if (player is null)
            {
                continue;
            }

            if (count > 0)
            {
                names += ", ";
            }

            names += player.Name;
            ++count;
        }

        return (names, count);
    }
}
