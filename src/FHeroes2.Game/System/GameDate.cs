using System.Globalization;

namespace FHeroes2.Game.System;

/// <summary>
/// <c>Game::getDateDescription()</c> (<c>game_tools.cpp:28-40</c>): the day count of a game turned into the
/// <c>Day: %{day} Week: %{week} Month: %{month}</c> line.
/// </summary>
/// <remarks>
/// <para>
/// 参考把天数减一后按 28 天一个月、7 天一星期换算，再分别加一放回模板（<c>:31-37</c>）。
/// 端口直接拼出整句：编辑器没有本地化层，模板换成字符串插值，数值与参考一致。
/// </para>
/// <para>
/// 差异：参考对 0 或负数不校验（会算出一个没有意义的日子）；端口要求天数至少是 1，
/// 与 <c>ValueSelectionDialogElement</c> 的下限一致。
/// </para>
/// </remarks>
public static class GameDate
{
    /// <summary>Days in a game week, <c>numOfDaysPerWeek</c>.</summary>
    public const int DaysPerWeek = 7;

    /// <summary>Weeks in a game month, <c>numOfWeeksPerMonth</c>.</summary>
    public const int WeeksPerMonth = 4;

    /// <summary>Days in a game month, <c>numOfDaysPerWeek * numOfWeeksPerMonth</c>.</summary>
    public const int DaysPerMonth = DaysPerWeek * WeeksPerMonth;

    /// <summary>
    /// Describes <paramref name="day"/> as day, week and month.
    /// </summary>
    /// <param name="day">The day of the game, one based.</param>
    /// <returns>For example <c>Day: 1 Week: 1 Month: 1</c> for the first day.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="day"/> is not positive.</exception>
    public static string Describe(int day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);

        int days = day - 1;
        int month = days / DaysPerMonth;
        days -= month * DaysPerMonth;

        int dayOfWeek = (days % DaysPerWeek) + 1;
        int weekOfMonth = (days / DaysPerWeek) + 1;

        return string.Create(
            CultureInfo.InvariantCulture, $"Day: {dayOfWeek} Week: {weekOfMonth} Month: {month + 1}");
    }
}
