using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The palette index of each army colour, <c>Battle::ArmyColor</c> and
/// <c>Battle::GetArmyColorFromPlayerColor()</c> (<c>battle_interface.h:47-58</c>, <c>battle_interface.cpp:723-750</c>).
/// </summary>
/// <remarks>
/// 这些索引是 <c>KB.PAL</c> 里那几组色的起点：同一组相邻的四色是亮/暗变体，单位的描边与顺序条边框都取起点。
/// </remarks>
public static class BattleArmyColor
{
    /// <summary>Colour of a stack without a player, <c>ARMY_COLOR_BLACK</c>.</summary>
    public const byte Black = 0x00;

    /// <summary>Blue army colour, <c>ARMY_COLOR_BLUE</c>.</summary>
    public const byte Blue = 0x47;

    /// <summary>Green army colour, <c>ARMY_COLOR_GREEN</c>.</summary>
    public const byte Green = 0x67;

    /// <summary>Red army colour, <c>ARMY_COLOR_RED</c>.</summary>
    public const byte Red = 0xBD;

    /// <summary>Yellow army colour, <c>ARMY_COLOR_YELLOW</c>.</summary>
    public const byte Yellow = 0x70;

    /// <summary>Orange army colour, <c>ARMY_COLOR_ORANGE</c>.</summary>
    public const byte Orange = 0xCD;

    /// <summary>Purple army colour, <c>ARMY_COLOR_PURPLE</c>.</summary>
    public const byte Purple = 0x87;

    /// <summary>Neutral colour, <c>ARMY_COLOR_GRAY</c>.</summary>
    public const byte Gray = 0x10;

    /// <summary>
    /// The palette index of a player colour, <c>GetArmyColorFromPlayerColor()</c>.
    /// </summary>
    /// <param name="color">A player colour, <c>unit.GetCurrentColor()</c> of its army.</param>
    /// <returns>The starting index of that colour group.</returns>
    /// <remarks>
    /// 两个不是「玩家的颜色」的值也各有去处：<see cref="PlayerColor.Unused"/> 是狂暴术控制下的单位，画成黑；
    /// <see cref="PlayerColor.None"/> 是没有玩家的一方，画成灰。参考对别的新颜色 <c>assert( 0 )</c> 之后仍返回灰，
    /// 端口直接返回灰，不中断发布版。
    /// </remarks>
    public static byte GetPaletteIndex(PlayerColor color) => color switch
    {
        PlayerColor.Unused => Black,
        PlayerColor.Blue => Blue,
        PlayerColor.Green => Green,
        PlayerColor.Red => Red,
        PlayerColor.Yellow => Yellow,
        PlayerColor.Orange => Orange,
        PlayerColor.Purple => Purple,
        _ => Gray,
    };
}
