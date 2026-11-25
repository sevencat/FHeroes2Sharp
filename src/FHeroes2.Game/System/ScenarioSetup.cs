using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.System;

/// <summary>
/// The scenario setup the menu remembers between the map list and the start of the game: the player parameters the
/// scenario info screen edited for the last map, <c>Game::LoadPlayers()</c> / <c>Game::SavePlayers()</c>
/// (<c>game.cpp:93-146</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这就是 <c>game.cpp</c> 的两个文件级静态量：<c>lastMapFileName</c> 和 <c>savedPlayers</c>。换地图或人类玩家数
/// 变了就整套作废，这是刻意的——上一局把红方改成「人 + 精灵」，换一张只给红蓝两格的地图后这些参数没有意义。
/// </para>
/// <para>
/// 存档里也有一份同样的参数（<c>SaveFile</c> 的玩家块），本类只管菜单里的临时记忆，进程退出即丢。
/// </para>
/// </remarks>
public static class ScenarioSetup
{
    /// <summary>The map the saved parameters belong to, <c>lastMapFileName</c>.</summary>
    private static string lastMapFileName = string.Empty;

    /// <summary>The parameters of the last confirmed scenario, <c>savedPlayers</c>.</summary>
    private static readonly List<Player> savedPlayers = new();

    /// <summary>
    /// Restores the parameters of the last confirmed scenario when it is the same map with the same number of human
    /// players, <c>Game::LoadPlayers()</c> (<c>game.cpp:93-122</c>).
    /// </summary>
    /// <param name="mapFileName">The map the slots were initialized from.</param>
    /// <param name="players">The freshly initialized slots to overwrite.</param>
    /// <remarks>
    /// 参考在两边人类玩家数不同时也放弃恢复：玩家把「红方」从人改成电脑后，槽位数量虽然一样，但那套参数属于另一个
    /// 人类玩家配置。恢复是整表替换而不是逐字段覆盖，因为槽位的顺序由存档决定。
    /// </remarks>
    public static void LoadPlayers(string mapFileName, Players players)
    {
        if (lastMapFileName != mapFileName || savedPlayers.Count != players.All.Count)
        {
            // The map or human players count is changed - ignore previously set players parameters.
            return;
        }

        int newHumanCount = players.All.Count(player => player.Control == PlayerControl.Human);
        int savedHumanCount = savedPlayers.Count(player => player.Control == PlayerControl.Human);

        if (newHumanCount != savedHumanCount)
        {
            return;
        }

        players.Clear();

        foreach (Player saved in savedPlayers)
        {
            Player player = new(saved.Color)
            {
                Race = saved.Race,
                Control = saved.Control,
                Friends = saved.Friends
            };

            player.SetName(saved.Name);
            player.SetHandicapStatus(saved.Handicap);

            players.Append(player);
        }
    }

    /// <summary>
    /// Remembers the parameters of the scenario that is about to start, <c>Game::SavePlayers()</c>
    /// (<c>game.cpp:129-146</c>).
    /// </summary>
    /// <param name="mapFileName">The map that is starting.</param>
    /// <param name="players">The slots as the player left them.</param>
    public static void SavePlayers(string mapFileName, Players players)
    {
        lastMapFileName = mapFileName;
        savedPlayers.Clear();

        foreach (Player player in players.All)
        {
            Player saved = new(player.Color)
            {
                Race = player.Race,
                Control = player.Control,
                Friends = player.Friends
            };

            saved.SetName(player.Name);
            saved.SetHandicapStatus(player.Handicap);

            savedPlayers.Add(saved);
        }
    }

    /// <summary>
    /// Stores the difficulty the scenario starts with, <c>Game::saveDifficulty()</c> (<c>game.cpp:124-127</c>).
    /// </summary>
    /// <param name="difficulty">The difficulty the info screen shows.</param>
    public static void SaveDifficulty(GameDifficulty difficulty) =>
        Settings.Get().SetGameDifficulty(difficulty);

    /// <summary>Forgets the remembered scenario; used by tests and by the debug hooks that start a map directly.</summary>
    public static void Reset()
    {
        lastMapFileName = string.Empty;
        savedPlayers.Clear();
    }
}
