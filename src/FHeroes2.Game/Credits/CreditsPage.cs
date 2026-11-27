namespace FHeroes2.Game.Credits;

/// <summary>One title/body pair of a credits page, the two arguments of <c>renderText()</c>
/// (<c>game_credits.cpp:173-183</c>).</summary>
/// <param name="Title">The yellow heading, e.g. <c>Development</c>.</param>
/// <param name="Body">
/// The white text under it; <c>\n</c> starts a new line, which is how the reference lists a whole team.
/// </param>
public readonly record struct CreditsEntry(string Title, string Body);

/// <summary>
/// One page of the credits: a background plus a set of entries laid out in columns,
/// the <c>generate*Credits*Page()</c> functions of <c>game_credits.cpp:182-697</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考每一页都是一个函数：贴 <c>CBKGLAVA</c> 背景、按列摆文本、再在空隙里贴几只怪物当插图。端口把「文本条目」
/// 抽成数据（本类），插图与背景留给绘制方——插图是装饰，缺一两张不影响读名单，而文本的排布必须可测。
/// </para>
/// <para>
/// 两种排布：<see cref="IsCentered"/> 为假时按列摆（列距 210、起始纵向 42、列宽 200，<c>:189-191</c>）；
/// 为真时是一整列居中（起始纵向 60、宽 300，<c>:269-273</c>）。
/// </para>
/// </remarks>
public sealed class CreditsPage
{
    /// <summary>Distance between two columns, <c>columnStep</c> of <c>:189</c>.</summary>
    public const int ColumnStep = 210;

    /// <summary>Height the first entry starts at in a column page, <c>textInitialOffsetY</c> of <c>:190</c>.</summary>
    public const int ColumnPageTop = 42;

    /// <summary>Width of one column, <c>textWidth</c> of <c>:191</c>.</summary>
    public const int ColumnWidth = 200;

    /// <summary>Height the single centred column starts at, <c>textInitialOffsetY</c> of <c>:271</c>.</summary>
    public const int CenteredPageTop = 60;

    /// <summary>Width of the single centred column, <c>textWidth</c> of <c>:272</c>.</summary>
    public const int CenteredWidth = 300;

    /// <summary>Gap between two entries of a column, the <c>+= 10</c> of <c>:196</c>.</summary>
    public const int EntryGap = 10;

    /// <summary>What the page shows, in drawing order.</summary>
    public required IReadOnlyList<CreditsEntry> Entries { get; init; }

    /// <summary>
    /// Whether the page is one centred column instead of the three of the first page, the
    /// <c>textInitialOffsetX = output.width() / 2</c> of <c>:270</c>.
    /// </summary>
    public bool IsCentered { get; init; }

    /// <summary>How many columns the page lays its entries out in.</summary>
    /// <remarks>参考的列页固定三列，条目按「先填第一列、填满换下一列」的顺序摆；居中页只有一列。</remarks>
    public int ColumnCount => IsCentered ? 1 : 3;

    /// <summary>Width of one column of this page.</summary>
    public int TextWidth => IsCentered ? CenteredWidth : ColumnWidth;

    /// <summary>Height the first entry starts at.</summary>
    public int TextTop => IsCentered ? CenteredPageTop : ColumnPageTop;
}
