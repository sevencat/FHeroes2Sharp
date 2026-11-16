namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The kingdom's mode bits, <c>Kingdom</c>'s anonymous enum (<c>kingdom.h:63-69</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>kingdom.h:63-69</c>。三个值里两个被注释掉了（<c>0x0001</c> 与 <c>0x0004</c> 是历史上的
/// <c>UNUSED</c>），所以这里不是连续的位序——照抄数值，不要重新编号。
/// </para>
/// <para>
/// <c>IdentifyHero</c> 记的是「本回合有英雄用过鉴定术」，<see cref="Kingdom.ActionNewDay"/> 每天清掉它；
/// <c>KingdomOverviewCastleSelection</c> 记的是王国总览界面里有没有选中过城堡，属于界面状态，
/// 但它和前者共享同一组位，所以一起搬过来。
/// </para>
/// </remarks>
[Flags]
public enum KingdomModes
{
    /// <summary>No mode set.</summary>
    None = 0,

    /// <summary>An «Identify Hero» spell was cast, <c>IDENTIFYHERO = 0x0002</c>.</summary>
    IdentifyHero = 0x0002,

    /// <summary>A castle was selected in the kingdom overview, <c>KINGDOM_OVERVIEW_CASTLE_SELECTION = 0x0008</c>.</summary>
    KingdomOverviewCastleSelection = 0x0008,
}
