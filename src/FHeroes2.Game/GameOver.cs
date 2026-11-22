namespace FHeroes2.Game;

/// <summary>
/// The conditions a map can end on, <c>GameOver</c> (<c>game_over.h:42-68</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用一个 <c>uint32_t</c> 位掩码同时装「怎么赢」与「怎么输」：低字节是六种赢法，<c>0x100</c> 起是四种输法，
/// <c>0x10000</c> 起是「对手因某条赢法获胜，所以我判负」——后者只有对手赢的时候才算，见
/// <see cref="LossEnemyWins"/>。
/// </para>
/// <para>
/// 一个国家可以同时满足多条（占城胜利同时允许全灭，于是条件里 `WINS_TOWN | WINS_ALL` 一起出现），所以判断一律用
/// 位与，不能用相等。端口用常量而不是 <c>enum</c>：这些值只做位运算，套一层枚举反而要在每处转换。
/// </para>
/// </remarks>
public static partial class GameOver
{
    /// <summary>No condition at all.</summary>
    public const uint ConditionNone = 0x0000_0000;

    /// <summary>Defeat every other kingdom.</summary>
    public const uint WinsAll = 0x0000_0001;

    /// <summary>Capture a specific town.</summary>
    public const uint WinsTown = 0x0000_0002;

    /// <summary>Defeat a specific hero.</summary>
    public const uint WinsHero = 0x0000_0004;

    /// <summary>Find a specific artifact (or the ultimate artifact).</summary>
    public const uint WinsArtifact = 0x0000_0008;

    /// <summary>Defeat the other side of the alliance.</summary>
    public const uint WinsSide = 0x0000_0010;

    /// <summary>Accumulate a specific amount of gold.</summary>
    public const uint WinsGold = 0x0000_0020;

    /// <summary>Every way a kingdom can win.</summary>
    public const uint Wins = WinsAll | WinsTown | WinsHero | WinsArtifact | WinsSide | WinsGold;

    /// <summary>Lose every town and hero.</summary>
    public const uint LossAll = 0x0000_0100;

    /// <summary>Lose a specific town.</summary>
    public const uint LossTown = 0x0000_0200;

    /// <summary>Lose a specific hero.</summary>
    public const uint LossHero = 0x0000_0400;

    /// <summary>Lose after a fixed number of days.</summary>
    public const uint LossTime = 0x0000_0800;

    /// <summary>An enemy captured the town the scenario protects.</summary>
    public const uint LossEnemyWinsTown = 0x0001_0000;

    /// <summary>An enemy found the artifact the scenario protects.</summary>
    public const uint LossEnemyWinsArtifact = 0x0002_0000;

    /// <summary>An enemy accumulated the gold the scenario protects.</summary>
    public const uint LossEnemyWinsGold = 0x0004_0000;

    /// <summary>Every way a kingdom can lose.</summary>
    public const uint Loss = LossAll | LossTown | LossHero | LossTime | LossEnemyWinsTown | LossEnemyWinsArtifact | LossEnemyWinsGold;

    /// <summary>The loss conditions that only fire once an enemy met the matching victory condition.</summary>
    public const uint LossEnemyWins = LossEnemyWinsTown | LossEnemyWinsArtifact | LossEnemyWinsGold;
}
