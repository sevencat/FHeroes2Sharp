using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.System;

/// <summary>
/// The scenario rating shown in the scenario info box, <c>Game::GetRating()</c> (<c>game.cpp:358-395</c>).
/// </summary>
/// <remarks>
/// <para>
/// 评分由两个难度叠出来：地图里写死的那个（制作者给这张图定的难度，<see cref="Mp2FileInfo.Difficulty"/>）与玩家
/// 在设置里选的那个（<see cref="Settings.GameDifficulty"/>）。两者各给一档加成，基线 50。
/// </para>
/// <para>
/// 参考从 <c>Settings::Get()</c> 两个地方取值，端口把两样都当参数传进来：调用方可能是设置对话框（玩家还没开局，
/// 只有设置里的难度），也可能是冒险地图（世界里的地图信息），两种情况拿的地图信息不是同一个对象。
/// </para>
/// </remarks>
public static class GameRating
{
    /// <summary>
    /// The rating of the scenario, <c>Game::GetRating()</c>.
    /// </summary>
    /// <param name="info">The map information block, for the difficulty written into the map.</param>
    /// <param name="difficulty">The difficulty the player picked.</param>
    /// <returns>The rating: 50 to 220.</returns>
    /// <remarks>
    /// Easy 在两边都不加分，所以最低是 50；不可能在两边都加满，最高是 50 + 80 + 90 = 220。
    /// </remarks>
    public static uint GetRating(Mp2FileInfo info, GameDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(info);

        uint rating = 50;

        rating += info.Difficulty switch
        {
            GameDifficulty.Normal => 20u,
            GameDifficulty.Hard => 40u,
            GameDifficulty.Expert or GameDifficulty.Impossible => 80u,
            _ => 0u,
        };

        rating += difficulty switch
        {
            GameDifficulty.Normal => 30u,
            GameDifficulty.Hard => 50u,
            GameDifficulty.Expert => 70u,
            GameDifficulty.Impossible => 90u,
            _ => 0u,
        };

        return rating;
    }
}
