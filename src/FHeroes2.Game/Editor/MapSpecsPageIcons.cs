using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The frame of the player and difficulty icons of the map specifications window, a port of
/// <c>getPlayerIcnIndex</c>, <c>getDifficultyIndex</c> and <c>setDifficultyByIndex</c>
/// (<c>src/fheroes2/editor/editor_map_specs_window.cpp:1957-2025</c>).
/// </summary>
/// <remarks>
/// <para>
/// 玩家那一行是六个 <c>NGEXTRA</c> 图标：3 只有电脑、9 只有人类、82 两者皆可，还有 70——「这个颜色在这张图上不可用」。
/// 参考在另外两种走不到的分支上 <c>assert</c> 之后同样返回 70，端口不 assert，直接落到同一个帧，玩家在界面上只会
/// 看到那个灰掉的图标，不会崩。
/// </para>
/// <para>
/// 难度那一行是四个 <c>DIFFICULTY_ICON_*</c> 归档，与 <see cref="GameDifficulty"/> 的 0-3 一一对应；参考的
/// <c>setDifficultyByIndex</c> 对越界值 <c>assert</c> 后回到 Easy，端口照抄这个兜底。
/// </para>
/// </remarks>
public static class MapSpecsPageIcons
{
    /// <summary>The <c>NGEXTRA</c> frame of a colour this map does not use, <c>70</c>.</summary>
    public const int UnavailablePlayerIcon = 70;

    /// <summary>The <c>NGEXTRA</c> frame of an AI-only colour, <c>3</c>.</summary>
    public const int ComputerPlayerIcon = 3;

    /// <summary>The <c>NGEXTRA</c> frame of a human-only colour, <c>9</c>.</summary>
    public const int HumanPlayerIcon = 9;

    /// <summary>The <c>NGEXTRA</c> frame of a colour both can use, <c>82</c>.</summary>
    public const int AnyPlayerIcon = 82;

    /// <summary>
    /// The <c>NGEXTRA</c> frame of one colour of the state, <c>getPlayerIcnIndex()</c>
    /// (<c>editor_map_specs_window.cpp:1957-1985</c>).
    /// </summary>
    /// <param name="state">The window state.</param>
    /// <param name="color">The colour of the icon.</param>
    /// <returns>The frame index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static int GetPlayerIconIndex(MapSpecsState state, PlayerColor color)
    {
        ArgumentNullException.ThrowIfNull(state);

        return GetPlayerIconIndex(state.IsAvailable(color), state.IsHumanPlayer(color), state.IsComputerPlayer(color));
    }

    /// <summary>
    /// The <c>NGEXTRA</c> frame for the three flags of one colour, the body of <c>getPlayerIcnIndex()</c>.
    /// </summary>
    /// <param name="available">Whether the colour is used by the map at all.</param>
    /// <param name="human">Whether a human can play it.</param>
    /// <param name="computer">Whether the AI can play it.</param>
    /// <returns>The frame index.</returns>
    public static int GetPlayerIconIndex(bool available, bool human, bool computer)
    {
        if (!available)
        {
            return UnavailablePlayerIcon;
        }

        if (human)
        {
            return computer ? AnyPlayerIcon : HumanPlayerIcon;
        }

        if (computer)
        {
            return ComputerPlayerIcon;
        }

        // 参考在这里 assert：可用的颜色必然至少给一方控制。端口退回「不可用」的图标。
        return UnavailablePlayerIcon;
    }

    /// <summary>
    /// The position of a difficulty in the four-icon row, <c>getDifficultyIndex()</c>
    /// (<c>editor_map_specs_window.cpp:1987-2005</c>).
    /// </summary>
    /// <param name="difficulty">The difficulty.</param>
    /// <returns>0-3, or 0 for <see cref="GameDifficulty.Impossible"/> and unknown values.</returns>
    public static int GetDifficultyIndex(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Normal => 1,
        GameDifficulty.Hard => 2,
        GameDifficulty.Expert => 3,
        _ => 0,
    };

    /// <summary>
    /// The difficulty of a position in the four-icon row, <c>setDifficultyByIndex()</c>
    /// (<c>editor_map_specs_window.cpp:2007-2025</c>).
    /// </summary>
    /// <param name="index">The position.</param>
    /// <returns>The difficulty; out-of-range positions fall back to <see cref="GameDifficulty.Easy"/>.</returns>
    public static GameDifficulty GetDifficultyFromIndex(int index) => index switch
    {
        1 => GameDifficulty.Normal,
        2 => GameDifficulty.Hard,
        3 => GameDifficulty.Expert,
        _ => GameDifficulty.Easy,
    };

    /// <summary>
    /// The archive of a difficulty's icon; the four <c>DIFFICULTY_ICON_*</c> are separate files, not frames of one.
    /// </summary>
    /// <param name="difficulty">The difficulty.</param>
    /// <returns>The archive.</returns>
    public static IcnType GetDifficultyIconType(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Normal => IcnType.DifficultyIconNormal,
        GameDifficulty.Hard => IcnType.DifficultyIconHard,
        GameDifficulty.Expert => IcnType.DifficultyIconExpert,
        _ => IcnType.DifficultyIconEasy,
    };
}
