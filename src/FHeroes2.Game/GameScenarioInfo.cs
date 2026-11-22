using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// The scenario setup screen's arithmetic, <c>game_scenarioinfo.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>game_scenarioinfo.cpp</c> 是一整屏「新游戏设置」对话框（约 700 行），是 S14-1 的活。这里先落它的
/// <c>Game::GetStep4Player()</c>，因为 <c>gui/player_info.cpp</c> 排玩家图标要用它——按「被依赖的目录先写好接口」的规矩，
/// 函数落在它 1:1 的目标文件里，等整屏搬过来时其余部分接在同一处。
/// </para>
/// <para>
/// 公式是「每个槽位按 6 个玩家的位置等距铺开，人少时整体居中」：<c>id * width * 6 / count</c> 是起点，
/// 后面那项补上右侧剩余的空槽，于是 2 个玩家时图标落在 1/4 与 3/4 处。
/// </para>
/// </remarks>
public static class GameScenarioInfo
{
    /// <summary>
    /// The x of one player's icon in a row of <paramref name="totalCount"/> icons, <c>Game::GetStep4Player()</c>
    /// (<c>game_scenarioinfo.cpp:526-529</c>).
    /// </summary>
    /// <param name="currentId">Zero-based index of the player.</param>
    /// <param name="width">Width of one icon.</param>
    /// <param name="totalCount">How many players share the row.</param>
    /// <returns>The x offset, relative to the row's own origin.</returns>
    /// <exception cref="DivideByZeroException"><paramref name="totalCount"/> is zero.</exception>
    public static int GetStep4Player(int currentId, int width, int totalCount) =>
        currentId * width * Players.MaxPlayers / totalCount
        + width * (Players.MaxPlayers - totalCount) / (2 * totalCount);
}
