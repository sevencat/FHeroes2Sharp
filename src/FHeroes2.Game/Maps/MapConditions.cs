using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The scenario's conditions turned into the flags the game compares against: the port of
/// <c>Maps::FileInfo::ConditionWins()</c>, <c>ConditionLoss()</c>, <c>WinsCompAlsoWins()</c> and the accessors of the
/// two condition blocks (<c>maps_fileinfo.cpp:602-649</c>, <c>maps_fileinfo.h:104-129</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把这几个函数写成 <c>FileInfo</c> 的成员，端口把它们挪到这里：<see cref="Mp2FileInfo"/> 属于
/// <c>FHeroes2.Assets</c>，而 <see cref="GameOver"/> 的位掩码是游戏逻辑，往上依赖会反过来。文件头里存的是
/// 「哪一种条件 + 两个 16 位参数」，判定要的是「位掩码 + 点 + 天数」，这层翻译只做这一件事。
/// </para>
/// <para>
/// 两个参数的含义随条件变化：占城与杀将是坐标 (x, y)，找神器是神器 id（0 表示藏起来的那件终极神器），攒金是千枚金币，
/// 守城/守将/超时用同一套布局。参考的注释说原始游戏的神器 id 从 0 起、而胜利条件从 1 起，正好对上
/// <see cref="ArtifactType"/> 里 <c>Unknown</c> 之后的编号，所以这里直接强转。
/// </para>
/// </remarks>
public static class MapConditions
{
    /// <summary>
    /// The ways <paramref name="info"/> lets a kingdom win, <c>FileInfo::ConditionWins()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>The winning conditions as <see cref="GameOver"/> flags.</returns>
    /// <remarks>
    /// 除「灭掉同盟对面」外，每条特殊胜利都**额外**允许全灭对手——`allowNormalVictory` 为假时才不叠加
    /// <see cref="GameOver.WinsAll"/>。地图制作者关掉它的意思正是「只许按我定的规则赢」。
    /// </remarks>
    public static uint Wins(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        uint normal = info.AllowNormalVictory ? GameOver.WinsAll : GameOver.ConditionNone;

        return info.VictoryCondition switch
        {
            MapVictoryCondition.DefeatEveryone => GameOver.WinsAll,
            MapVictoryCondition.CaptureTown => GameOver.WinsTown | normal,
            MapVictoryCondition.KillHero => GameOver.WinsHero | normal,
            MapVictoryCondition.ObtainArtifact => GameOver.WinsArtifact | normal,
            MapVictoryCondition.DefeatOtherSide => GameOver.WinsSide,
            MapVictoryCondition.CollectGold => GameOver.WinsGold | normal,

            // 未知条件在参考里是 assert：发布构建返回 COND_NONE，即这张图没有胜利条件。
            _ => GameOver.ConditionNone,
        };
    }

    /// <summary>
    /// The ways <paramref name="info"/> makes a kingdom lose, <c>FileInfo::ConditionLoss()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>The losing conditions as <see cref="GameOver"/> flags.</returns>
    public static uint Loss(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return info.LossCondition switch
        {
            MapLossCondition.Everything => GameOver.LossAll,
            MapLossCondition.LoseTown => GameOver.LossTown,
            MapLossCondition.LoseHero => GameOver.LossHero,
            MapLossCondition.OutOfTime => GameOver.LossTime,

            _ => GameOver.ConditionNone,
        };
    }

    /// <summary>
    /// Whether the computer may win the same way a human may, <c>FileInfo::WinsCompAlsoWins()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>True when both sides are playing for the same prize.</returns>
    /// <remarks>
    /// 只有占城与攒金两种条件会把这个开关变成真：杀将、找神器与灭同盟在参考里对电脑没有意义（它不会主动去抢）。
    /// </remarks>
    public static bool ComputerAlsoWins(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return info.ComputerAlsoWins && (Wins(info) & (GameOver.WinsTown | GameOver.WinsGold)) != 0;
    }

    /// <summary>
    /// Position of the town to capture or the hero to defeat, <c>FileInfo::WinsMapsPositionObject()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>The absolute map position, or (-1, -1) when the condition has no position.</returns>
    public static Point WinsPosition(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new Point(info.VictoryConditionParameters[0], info.VictoryConditionParameters[1]);
    }

    /// <summary>
    /// Position of the town or hero the scenario protects, <c>FileInfo::LossMapsPositionObject()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>The absolute map position, or (-1, -1) when the condition has no position.</returns>
    public static Point LossPosition(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new Point(info.LossConditionParameters[0], info.LossConditionParameters[1]);
    }

    /// <summary>
    /// Whether the artifact to find is the one buried on the map, <c>FileInfo::WinsFindUltimateArtifact()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>True when the condition is a parameterless "find the ultimate artifact".</returns>
    public static bool WinsUltimateArtifact(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return info.VictoryConditionParameters[0] == 0;
    }

    /// <summary>
    /// The artifact to find, <c>FileInfo::WinsFindArtifactID()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>
    /// The artifact, <see cref="ArtifactType.Unknown"/> when the condition is the ultimate artifact instead.
    /// </returns>
    public static ArtifactType WinsArtifact(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return (ArtifactType)info.VictoryConditionParameters[0];
    }

    /// <summary>
    /// The gold a kingdom has to hold, <c>FileInfo::getWinningGoldAccumulationValue()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>The amount in gold pieces: the stored parameter is counted in thousands.</returns>
    public static uint WinningGold(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return (uint)info.VictoryConditionParameters[0] * 1000;
    }

    /// <summary>
    /// The day limit of the scenario, <c>FileInfo::LossCountDays()</c>.
    /// </summary>
    /// <param name="info">The map information block.</param>
    /// <returns>The number of days, counted as the reference counts them.</returns>
    public static uint LossDays(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return (uint)info.LossConditionParameters[0];
    }
}
