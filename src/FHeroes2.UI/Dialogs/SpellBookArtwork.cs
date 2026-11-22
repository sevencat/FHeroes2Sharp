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
