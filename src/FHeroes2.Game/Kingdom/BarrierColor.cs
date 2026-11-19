namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The colors of the magic barriers and the traveller's tents, <c>BarrierColor</c> and the two name functions
/// (<c>color.cpp:35-222</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is <b>not</b> <c>PlayerColor</c>. The eight colors of a barrier are their own small enum — aqua, blue, brown,
/// gold, green, orange, purple, red — and their numbers are the raw values stored in <c>metadata[0]</c> of the barrier
/// or tent tile (<c>maps_tiles_helper.cpp:774</c>), which is why the two name functions take a plain <see cref="int"/>
/// and not an enum of the port.
/// </para>
/// <para>
/// 0 and everything outside 1…8 answer <c>"None"</c>, exactly like the reference's <c>default:</c> branch. The
/// kingdom remembers the visited tents by shifting 1 by this same number
/// (<c>Kingdom.MarkTravellerTentVisited()</c>), so the two have to agree.
/// </para>
/// </remarks>
public static class BarrierColors
{
    /// <summary>The number of a barrier or tent color, <c>BarrierColor</c> (<c>color.cpp:37-48</c>).</summary>
    public enum BarrierColor
    {
        /// <summary>No color, <c>NONE</c>.</summary>
        None = 0,

        /// <summary><c>AQUA</c>.</summary>
        Aqua = 1,

        /// <summary><c>BLUE</c>.</summary>
        Blue = 2,

        /// <summary><c>BROWN</c>.</summary>
        Brown = 3,

        /// <summary><c>GOLD</c>.</summary>
        Gold = 4,

        /// <summary><c>GREEN</c>.</summary>
        Green = 5,

        /// <summary><c>ORANGE</c>.</summary>
        Orange = 6,

        /// <summary><c>PURPLE</c>.</summary>
        Purple = 7,

        /// <summary><c>RED</c>.</summary>
        Red = 8,
    }

    /// <summary>
    /// The word the quick info screen puts in place of <c>%{color}</c> for a barrier,
    /// <c>fheroes2::getBarrierColorName()</c> (<c>color.cpp:172-196</c>).
    /// </summary>
    /// <param name="color">The raw <c>metadata[0]</c> of the barrier tile.</param>
    /// <returns>The English name, or <c>"None"</c>.</returns>
    public static string GetBarrierName(int color) => color switch
    {
        1 => "Aqua",
        2 => "Blue",
        3 => "Brown",
        4 => "Gold",
        5 => "Green",
        6 => "Orange",
        7 => "Purple",
        8 => "Red",
        _ => "None",
    };

    /// <summary>
    /// The word the quick info screen puts in place of <c>%{color}</c> for a traveller's tent,
    /// <c>fheroes2::getTentColorName()</c> (<c>color.cpp:198-222</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetBarrierName"/> 同一张表、同一批词——参考里就是复制的一份，翻译键不同
    /// （<c>tent|…</c> 与 <c>barrier|…</c>），端口没有翻译层，两者因此给出相同的英文。
    /// </remarks>
    /// <param name="color">The raw <c>metadata[0]</c> of the tent tile.</param>
    /// <returns>The English name, or <c>"None"</c>.</returns>
    public static string GetTentName(int color) => GetBarrierName(color);
}
