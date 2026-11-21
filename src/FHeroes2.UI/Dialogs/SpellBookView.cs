using FHeroes2.Core.Geometry;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The two-page magic book, a port of <c>SpellBook::Open()</c> and <c>SpellBook::Edit()</c>
/// (<c>spell_book.cpp:195-506</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>页面状态已经落好，画与点还没落。</b>参考把「翻到第几页、哪个书签、当前过滤哪种法术」存在
/// <c>SpellBook</c> 自己身上（<c>spell_book.cpp:507-533</c> 的 <c>resetState()</c> 与 <c>SetFilter()</c>），端口把这部分
/// 原样搬到了 <see cref="FHeroes2.Game.Spells.SpellBook"/>：<c>BeginSession</c>、<c>SelectFilter</c>、
/// <c>ShowPreviousPage</c>、<c>ShowNextPage</c>、<c>ScreenSpells</c>。这个类只负责「帧」：拿到当前页的法术列表，
/// 画书页与书签，把点击翻成状态机的调用。
/// </para>
/// <para>
/// 参考的 <c>Open()</c> 同时兼任事件循环：它自己 <c>LocalEvent::Get()</c>、自己算鼠标落在哪一页的哪个格、
/// 自己弹「法力不足」的提示。端口把这三件事拆开（见 <see cref="DialogElement"/> 的说明），所以这里的
/// <see cref="Open"/> 返回「玩家选了什么」，提示通过 <c>statusCallback</c> 交回给调用方去弹。
/// </para>
/// <para>
/// <b>当前状态：骨架。</b>布局常量与签名按参考落好，帧循环与绘制留到 S13（`dialog` 目录）统一做——这批要先把
/// 每个目录的落点建起来。骨架期间调用即抛 <see cref="NotImplementedException"/>，不做兜底。
/// </para>
/// </remarks>
public sealed class SpellBookView
{
    /// <summary>Spells on one page, <c>spellsPerPage</c> (<c>spell_book.cpp:52</c>).</summary>
    public const int SpellsPerPage = 6;

    /// <summary>Where the book's own shadow falls, <c>spellBookShadow</c> (<c>spell_book.cpp:59</c>).</summary>
    public static readonly Point ShadowOffset = new(-16, 16);

    /// <summary>Top left corner of the info bookmark, <c>bookmarkInfoOffset</c> (<c>spell_book.cpp:54</c>).</summary>
    public static readonly Point InfoBookmarkOffset = new(123, 273);

    /// <summary>Top left corner of the adventure bookmark, <c>bookmarkAdvOffset</c> (<c>spell_book.cpp:55</c>).</summary>
    public static readonly Point AdventureBookmarkOffset = new(266, 269);

    /// <summary>Top left corner of the combat bookmark, <c>bookmarkCombatoOffset</c> (<c>spell_book.cpp:56</c>).</summary>
    public static readonly Point CombatBookmarkOffset = new(299, 276);

    /// <summary>Top left corner of the close bookmark, <c>bookmarkCloseOffset</c> (<c>spell_book.cpp:57</c>).</summary>
    public static readonly Point CloseBookmarkOffset = new(416, 280);

    private readonly SpellBook _book = new();

    /// <summary>
    /// Opens the book and returns what the player picked, <c>SpellBook::Open()</c>
    /// (<c>spell_book.cpp:195-404</c>).
    /// </summary>
    /// <remarks>
    /// 三种拒绝与参考一致：没有魔法书、过滤后一本可用的都没有、以及翻页越界时回到第一页。
    /// 「法力不足」不在这里拦——参考也只是在点下去的时候调 <c>statusCallback</c> 提示一句，选中与否由调用方判断。
    /// </remarks>
    /// <param name="owner">Whoever carries the book (hero or captain).</param>
    /// <param name="bag">Artifacts that add spells to the book, or <see langword="null"/> when there are none.</param>
    /// <param name="displayableSpells">Which tabs the caller allows.</param>
    /// <param name="canCastSpell">Whether the caller may actually cast (false for a book being read only).</param>
    /// <param name="restorePreviousState">Whether to keep the page the book was left on.</param>
    /// <param name="statusCallback">Receives the text of a refused cast; the caller shows it.</param>
    /// <returns>The chosen spell, or <see cref="SpellType.None"/> when the book was closed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotImplementedException">The frame loop is not ported yet (S13).</exception>
    public SpellType Open(ISpellBookOwner owner, ArtifactBag? bag, SpellBookFilter displayableSpells, bool canCastSpell,
        bool restorePreviousState, Action<string>? statusCallback = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        throw new NotImplementedException("SpellBook::Open() 的帧循环与绘制未移植（planv4 S2-2 → S13）。");
    }

    /// <summary>
    /// Shows the book so that the owner can look at it but not cast, <c>SpellBook::Edit()</c>
    /// (<c>spell_book.cpp:405-506</c>).
    /// </summary>
    /// <param name="owner">Whoever carries the book.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotImplementedException">The frame loop is not ported yet (S13).</exception>
    public void Edit(ISpellBookOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        throw new NotImplementedException("SpellBook::Edit() 的帧循环与绘制未移植（planv4 S2-2 → S13）。");
    }
}
