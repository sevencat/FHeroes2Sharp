using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork an open spell book needs, i.e. the six frames of <c>ICN::BOOK</c> the reference reads
/// (<c>spell_book.cpp:61-84</c>, <c>:151-181</c>) plus the spell icons of <c>ICN::SPELLS</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference reads the archive inside <c>SpellBook::Open</c>. The port keeps the window free of the
/// archive and hands the frames in, the same way <see cref="ItemSelectionWindowArtwork"/> does, so a test can
/// build the sprites without an AGG file.
/// </para>
/// <para>
/// Frame 1 and 2 of <c>ICN::BOOK</c> are not used by the book screen; the six that are: the page (0), the
/// adventure bookmark (3), the combat bookmark (4), the close bookmark (5), the spell point bookmark (6) and
/// the straight corner (7).
/// </para>
/// </remarks>
public sealed class SpellBookArtwork
{
    /// <summary>The book page, frame 0 of <c>ICN::BOOK</c>.</summary>
    public required Sprite Page { get; init; }

    /// <summary>The adventure spells bookmark, frame 3 of <c>ICN::BOOK</c>.</summary>
    public required Sprite AdventureBookmark { get; init; }

    /// <summary>The combat spells bookmark, frame 4 of <c>ICN::BOOK</c>.</summary>
    public required Sprite CombatBookmark { get; init; }

    /// <summary>The close bookmark, frame 5 of <c>ICN::BOOK</c>.</summary>
    public required Sprite CloseBookmark { get; init; }

    /// <summary>The bookmark the spell points are written on, frame 6 of <c>ICN::BOOK</c>.</summary>
    public required Sprite SpellPointsBookmark { get; init; }

    /// <summary>
    /// The corner that squares off the outer edge of a page with no spells, frame 7 of <c>ICN::BOOK</c>
    /// (<c>spell_book.cpp:155-166</c>).
    /// </summary>
    public required Sprite StraightCorner { get; init; }

    /// <summary>Every frame of <c>ICN::SPELLS</c>, indexed by <c>Spell::IndexSprite()</c>.</summary>
    public required IReadOnlyList<Sprite> SpellIcons { get; init; }

    /// <summary>Archive entry of the spell book pages, <c>ICN::BOOK</c>.</summary>
    public const string BookAssetName = "BOOK.ICN";

    /// <summary>Archive entry of the spell icons, <c>ICN::SPELLS</c>.</summary>
    public const string SpellsAssetName = "SPELLS.ICN";

    /// <summary>Frame of the straight corner in <see cref="BookAssetName"/>, <c>spell_book.cpp:155-166</c>.</summary>
    public const int StraightCornerFrame = 7;

    /// <summary>
    /// Reads the <c>BOOK</c> and <c>SPELLS</c> frames, <c>SpellBook::open()</c>'s artwork half.
    /// </summary>
    /// <param name="assets">Where the archives come from; <see langword="null"/> means no data.</param>
    /// <returns>The artwork, or <see langword="null"/> when either archive is missing or too short.</returns>
    /// <remarks>
    /// 参考每次打开魔法书都去取图；端口把「读哪几帧」收在这里，冒险地图与战斗两边共用同一套索引。
    /// </remarks>
    public static SpellBookArtwork? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        if (assets.TryReadIcn(BookAssetName) is not { } book || book.Count <= StraightCornerFrame)
        {
            return null;
        }

        if (assets.TryReadIcn(SpellsAssetName) is not { Count: > 0 } spells)
        {
            return null;
        }

        return new SpellBookArtwork
        {
            Page = book.Sprites[0],
            AdventureBookmark = book.Sprites[3],
            CombatBookmark = book.Sprites[4],
            CloseBookmark = book.Sprites[5],
            SpellPointsBookmark = book.Sprites[6],
            StraightCorner = book.Sprites[StraightCornerFrame],
            SpellIcons = spells.Sprites,
        };
    }

    /// <summary>
    /// The size of the composed book, <c>getSpellBookSize()</c> (<c>spell_book.cpp:61-84</c>).
    /// </summary>
    /// <remarks>
    /// Two pages wide; the height is the lowest bookmark that is drawn, because the bookmarks hang out of the
    /// page and the shadow follows them. The tabs that the filter hides do not count.
    /// </remarks>
    /// <param name="displayable">The filter the book was opened with.</param>
    /// <returns>The size of the composed sprite.</returns>
    public Size GetBookSize(SpellBookFilter displayable)
    {
        int maximumHeight = SpellBookWindow.InfoBookmarkOffset.Y + SpellPointsBookmark.Height;

        if (displayable != SpellBookFilter.Combat)
        {
            maximumHeight = Math.Max(maximumHeight,
                SpellBookWindow.AdventureBookmarkOffset.Y + AdventureBookmark.Height);
        }

        if (displayable != SpellBookFilter.Adventure)
        {
            maximumHeight = Math.Max(maximumHeight,
                SpellBookWindow.CombatBookmarkOffset.Y + CombatBookmark.Height);
        }

        maximumHeight = Math.Max(maximumHeight, SpellBookWindow.CloseBookmarkOffset.Y + CloseBookmark.Height);

        return new Size(Page.Width * 2, maximumHeight);
    }
}
