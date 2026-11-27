using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Editor;

/// <summary>
/// One entry of a map's daily events, <c>Maps::Map_Format::DailyEvent</c> (<c>map_format_info.h:297-311</c>).
/// </summary>
/// <remarks>
/// <para>
/// 地图格式里「每天发生一次的事」：从 <see cref="FirstOccurrenceDay"/> 起、每 <see cref="RepeatPeriodInDays"/> 天重复
/// （0 表示只发生一次），给 <see cref="HumanPlayerColors"/> 与 <see cref="ComputerPlayerColors"/> 里的人类/电脑玩家
/// 发 <see cref="Resources"/> 并显示 <see cref="Message"/>。
/// </para>
/// <para>
/// 这是编辑器与地图文件的模型（<c>.fh2m</c> 里的 <c>dailyEvents</c>），不是运行时的 <see cref="World.EventDate"/>：
/// 后者已经把颜色拆成「每天该给谁」，而这里存的是作者填的两个颜色集，所以编辑器同时改两个集合。
/// 参考的字段默认值照抄：日期从第 1 天起、不重复、没有奖励、没有颜色。
/// </para>
/// </remarks>
public sealed class MapDailyEvent
{
    /// <summary>Days in a game year, <c>daysInYear</c> of the spec window (<c>editor_daily_event_spec_window.cpp:61</c>).</summary>
    /// <remarks>7 天一周、4 周一月、12 月一年；重复周期最多一年，再长就没意义。</remarks>
    public const uint DaysInYear = 7 * 4 * 12;

    /// <summary>
    /// Last day an event may start on, <c>lastDayForEvents</c> (<c>editor_daily_event_spec_window.cpp:62</c>).
    /// </summary>
    public const uint LastDayForEvents = DaysInYear * 100;

    /// <summary>
    /// The message shown when the event fires, <c>message</c>.
    /// </summary>
    /// <remarks>空串是合法的：列表窗只按日期成行，不要求有文字。</remarks>
    public string Message { get; set; } = string.Empty;

    /// <summary>The human players the event reaches, <c>humanPlayerColors</c>.</summary>
    public PlayerColor HumanPlayerColors { get; set; } = PlayerColor.None;

    /// <summary>The computer players the event reaches, <c>computerPlayerColors</c>.</summary>
    public PlayerColor ComputerPlayerColors { get; set; } = PlayerColor.None;

    /// <summary>The day the event fires first, <c>firstOccurrenceDay</c>; the reference default is 1.</summary>
    public uint FirstOccurrenceDay { get; set; } = 1;

    /// <summary>
    /// How often the event repeats, <c>repeatPeriodInDays</c>; 0 means it never repeats.
    /// </summary>
    public uint RepeatPeriodInDays { get; set; }

    /// <summary>The resources the event hands out, <c>resources</c>.</summary>
    public Funds Resources { get; set; }

    /// <summary>
    /// Throws away what the map no longer allows, <c>editDailyEvent()</c> of the spec window
    /// (<c>editor_daily_event_spec_window.cpp:70-77</c>).
    /// </summary>
    /// <param name="availableHumanColors">Colours the map lets humans play, <c>humanPlayerColors</c>.</param>
    /// <param name="availableComputerColors">Colours the map lets computers play, <c>computerPlayerColors</c>.</param>
    /// <remarks>
    /// <para>
    /// 事件存的是作者勾的颜色，删城堡/英雄后地图允许的颜色会变少，所以每次打开规格窗先把两个颜色集与地图允许集求交。
    /// </para>
    /// <para>
    /// 日期逻辑上无上限，但参考收在 <c>1..lastDayForEvents</c>（100 年）；重复周期收到一年以内。
    /// </para>
    /// </remarks>
    public void Clamp(PlayerColor availableHumanColors, PlayerColor availableComputerColors)
    {
        HumanPlayerColors &= availableHumanColors;
        ComputerPlayerColors &= availableComputerColors;
        FirstOccurrenceDay = Math.Clamp(FirstOccurrenceDay, 1, LastDayForEvents);
        RepeatPeriodInDays = Math.Min(RepeatPeriodInDays, DaysInYear);
    }

    /// <summary>Makes a copy; the list window sorts copies of the author's rows, not the rows themselves.</summary>
    /// <returns>A new instance with the same values.</returns>
    public MapDailyEvent Clone() => new()
    {
        Message = Message,
        HumanPlayerColors = HumanPlayerColors,
        ComputerPlayerColors = ComputerPlayerColors,
        FirstOccurrenceDay = FirstOccurrenceDay,
        RepeatPeriodInDays = RepeatPeriodInDays,
        Resources = Resources
    };
}
