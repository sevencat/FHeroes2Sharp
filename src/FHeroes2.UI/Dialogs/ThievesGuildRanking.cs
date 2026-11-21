using FHeroes2.Assets.Maps;

namespace FHeroes2.UI.Dialogs;

/// <summary>One place of a ranking: the value and every colour that has it.</summary>
/// <param name="Value">The value the colours share.</param>
/// <param name="Colors">The colours, as a set; empty never happens.</param>
public readonly record struct ThievesGuildGroup(int Value, PlayerColor Colors);

/// <summary>The ranking maths of the thieves' guild, <c>getInfo()</c> and <c>drawFlags()</c>' order.</summary>
/// <remarks>
/// 参考 <c>dialog_thievesguild.cpp:63-135</c>：逐个玩家取值，等值合并成一组（颜色按位或），再按数值从大到小排；
/// 画的时候第 <c>i</c> 组落在第 <c>i</c> 列，组内颜色依次排开。这里只做前半段，纯函数，方便单测。
/// </remarks>
public static class ThievesGuildRanking
{
    /// <summary>Groups the colours of a kingdom by a value, best first.</summary>
    /// <param name="colors">The colours in play, in draw order.</param>
    /// <param name="value">Reads the value of one colour.</param>
    /// <returns>One group per distinct value, sorted by value descending.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<ThievesGuildGroup> Build(
        IReadOnlyList<PlayerColor> colors,
        Func<PlayerColor, int> value)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(value);

        List<ThievesGuildGroup> groups = new(colors.Count);

        foreach (PlayerColor color in colors)
        {
            int item = value(color);
            int at = -1;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Value == item)
                {
                    at = i;
                    break;
                }
            }

            if (at < 0)
            {
                groups.Add(new ThievesGuildGroup(item, color));
            }
            else
            {
                groups[at] = groups[at] with { Colors = groups[at].Colors | color };
            }
        }

        // 参考用 SortValueGreat：数值大的在前。等值已经合并，所以排序结果唯一，不存在并列稳定性问题。
        groups.Sort(static (left, right) => right.Value.CompareTo(left.Value));

        return groups;
    }

    /// <summary>How many flags a group paints, <c>PlayerColorsVector( colors ).size()</c>.</summary>
    /// <param name="colors">The colours of one group.</param>
    /// <returns>The number of colours.</returns>
    public static int Count(PlayerColor colors) => PlayerColors.ToList(colors).Count;

    /// <summary>The ordinal printed above a column, <c>getPlayerOrderString()</c>.</summary>
    /// <param name="player">Zero-based column.</param>
    /// <returns>"1st" … "6th", or the bare number past the sixth.</returns>
    public static string Ordinal(int player) => player switch
    {
        0 => "1st",
        1 => "2nd",
        2 => "3rd",
        3 => "4th",
        4 => "5th",
        5 => "6th",
        _ => (player + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
