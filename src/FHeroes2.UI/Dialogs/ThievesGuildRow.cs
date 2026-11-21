namespace FHeroes2.UI.Dialogs;

/// <summary>The rows of the thieves' guild ranking, in the order the reference draws them.</summary>
/// <remarks>
/// 参考 <c>dialog_thievesguild.cpp:319-403</c>：十行固定标签，藏在标签右边的是「这一项谁排第几」的旗帜组。
/// </remarks>
public enum ThievesGuildRow
{
    /// <summary>Number of Towns; <c>GetCountTown()</c>, i.e. towns without a fort.</summary>
    Towns,

    /// <summary>Number of Castles; <c>GetCountCastle()</c>, i.e. towns with a fort.</summary>
    Castles,

    /// <summary>Number of Heroes.</summary>
    Heroes,

    /// <summary>Gold in Treasury.</summary>
    Gold,

    /// <summary>Wood &amp; Ore.</summary>
    WoodAndOre,

    /// <summary>Gems, Cr, Slf &amp; Mer.</summary>
    RareResources,

    /// <summary>Obelisks Found.</summary>
    Obelisks,

    /// <summary>Artifacts.</summary>
    Artifacts,

    /// <summary>Total Army Strength.</summary>
    ArmyStrength,

    /// <summary>Income.</summary>
    Income,
}

/// <summary>The label of every row and the number of guilds it takes to reveal it.</summary>
/// <remarks>
/// 参考把这些门槛写成一句句 <c>if ( thievesGuildCount &gt; N )</c>；<c>0xFF</c> 是神谕处打开的哨兵值，
/// 因此那里所有行都显示（<c>dialog_thievesguild.cpp:279</c>）。
/// </remarks>
public static class ThievesGuildRows
{
    /// <summary>Every row, top to bottom.</summary>
    public static IReadOnlyList<ThievesGuildRow> All { get; } = Enum.GetValues<ThievesGuildRow>();

    /// <summary>The label printed to the left of the flags.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The English label, as the reference spells it.</returns>
    public static string Label(ThievesGuildRow row) => row switch
    {
        ThievesGuildRow.Towns => "Number of Towns:",
        ThievesGuildRow.Castles => "Number of Castles:",
        ThievesGuildRow.Heroes => "Number of Heroes:",
        ThievesGuildRow.Gold => "Gold in Treasury:",
        ThievesGuildRow.WoodAndOre => "Wood & Ore:",
        ThievesGuildRow.RareResources => "Gems, Cr, Slf & Mer:",
        ThievesGuildRow.Obelisks => "Obelisks Found:",
        ThievesGuildRow.Artifacts => "Artifacts:",
        ThievesGuildRow.ArmyStrength => "Total Army Strength:",
        ThievesGuildRow.Income => "Income:",
        _ => string.Empty,
    };

    /// <summary>How many guilds the kingdom needs before the flags of a row are shown.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The threshold the guild count has to exceed.</returns>
    public static int RequiredGuilds(ThievesGuildRow row) => row switch
    {
        ThievesGuildRow.Gold or ThievesGuildRow.WoodAndOre => 1,
        ThievesGuildRow.RareResources or ThievesGuildRow.Obelisks => 2,
        ThievesGuildRow.Artifacts or ThievesGuildRow.ArmyStrength => 3,
        ThievesGuildRow.Income => 4,
        _ => 0,
    };

    /// <summary>Whether a row's flags are shown at all.</summary>
    /// <param name="row">The row.</param>
    /// <param name="thievesGuilds">Guilds the kingdom owns, or <c>0xFF</c> at the oracle.</param>
    /// <returns><see langword="true"/> when the row is revealed.</returns>
    /// <remarks>
    /// 参考无条件地画 Towns、Castles、Heroes 三行（<c>dialog_thievesguild.cpp:327-347</c>），连一座公会都没有的
    /// 王国也看得见它们；其余七行才按 <see cref="RequiredGuilds"/> 判。
    /// </remarks>
    public static bool IsRevealed(ThievesGuildRow row, int thievesGuilds) =>
        row is ThievesGuildRow.Towns or ThievesGuildRow.Castles or ThievesGuildRow.Heroes
        || thievesGuilds > RequiredGuilds(row);
}
