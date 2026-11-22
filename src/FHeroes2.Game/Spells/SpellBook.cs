using FHeroes2.Game.Artifacts;

namespace FHeroes2.Game.Spells;

/// <summary>
/// Which half of a hero's spells the open spell book shows, <c>SpellBook::Filter</c>
/// (<c>spell_book.h:36-41</c>).
/// </summary>
/// <remarks>
/// The three tabs of the book: the adventure spells, the combat spells, and all of them. The values are bit
/// flags because <see cref="All"/> is the union of the other two and the dialog compares them by value.
/// </remarks>
[Flags]
public enum SpellBookFilter
{
    /// <summary>No tab at all — never shown.</summary>
    None = 0,

    /// <summary>The adventure spells tab.</summary>
    Adventure = 1,

    /// <summary>The combat spells tab.</summary>
    Combat = 2,

    /// <summary>The tab that lists both halves.</summary>
    All = Adventure | Combat,
}

/// <summary>
/// The page state of an open spell book, <c>SpellBook</c> (<c>spell_book.h:28-90</c>,
/// <c>spell_book.cpp:496-552</c>).
/// </summary>
/// <remarks>
/// <para>
/// In the reference a hero's book <b>is</b> this class — it derives from <c>SpellStorage</c> and carries the
/// filter and the index of the first spell on the screen. The port keeps the hero's storage a plain
/// <see cref="SpellStorage"/> and puts the page state here, because the page a player was looking at is UI
/// state that no game rule reads.
/// </para>
/// <para>
/// <b>What is ported</b>: the filter, the first-spell index, the four page transitions and the session
/// start-up rule of <c>SpellBook::Open</c> — the book keeps the page it was closed on unless the hero's spell
/// book is opened for a fixed filter that the stored page does not match.
/// </para>
/// <para>
/// <b>What is not</b>: the drawing and the input loop of <c>SpellBook::Open</c>, which belong to the batch
/// that ports the dialogs (the <c>ArtifactsBar</c> of the artifact file is in the same batch), and the
/// serialization operators.
/// </para>
/// </remarks>
public sealed class SpellBook
{
    /// <summary>How many spells fit on one book page, <c>spellsPerPage</c> (<c>spell_book.cpp:88</c>).</summary>
    public const int SpellsPerPage = 6;

    /// <summary>How many spells fit on the screen: the two pages, <c>spellsPerPage * 2</c>.</summary>
    public const int SpellsPerScreen = SpellsPerPage * 2;

    /// <summary>The filter of the last session, <c>_spellFilter</c>.</summary>
    public SpellBookFilter Filter { get; private set; } = SpellBookFilter.Adventure;

    /// <summary>The index of the first spell on the screen, <c>_startSpellIndex</c>.</summary>
    public int StartIndex { get; private set; }

    /// <summary>Whether the first page is shown, <c>_isFirstPage()</c>.</summary>
    public bool IsFirstPage => StartIndex == 0;

    /// <summary>Whether the last screen already shows the end of the list, <c>_isLastPage()</c>.</summary>
    /// <param name="spellCount">How many spells the filtered list has.</param>
    /// <returns>Whether turning forward would be a no-op.</returns>
    public bool IsLastPage(int spellCount) => StartIndex + SpellsPerScreen >= spellCount;

    /// <summary>
    /// Starts a session, the state block at the top of <c>SpellBook::Open</c> (<c>spell_book.cpp:200-215</c>).
    /// </summary>
    /// <remarks>
    /// The book reopens on the page it was closed on unless the caller asks for a fixed filter — that is what
    /// the <c>restorePreviousState</c> flag of the reference means — and a stale index past the end of the
    /// list falls back to the first page.
    /// </remarks>
    /// <param name="displayable">The filter the caller insists on, or <see cref="SpellBookFilter.All"/>.</param>
    /// <param name="restorePreviousState">Whether the previous page and filter may be kept.</param>
    /// <param name="spellCount">How many spells the new list has.</param>
    public void BeginSession(SpellBookFilter displayable, bool restorePreviousState, int spellCount)
    {
        BeginSession(displayable, restorePreviousState);
        ClampStartIndex(spellCount);
    }

    /// <summary>
    /// Starts a session without knowing how long the resulting list is, the state block at the top of
    /// <c>SpellBook::Open</c> (<c>spell_book.cpp:203-221</c>) up to the point where the reference builds the
    /// list.
    /// </summary>
    /// <remarks>
    /// The reference filters first and checks the stored index against the list afterwards; the port cannot,
    /// because the filter the session picks decides which list to build. A caller therefore starts the
    /// session with this overload, builds the list for <see cref="Filter"/> and then calls
    /// <see cref="ClampStartIndex"/>.
    /// </remarks>
    /// <param name="displayable">The filter the caller insists on, or <see cref="SpellBookFilter.All"/>.</param>
    /// <param name="restorePreviousState">Whether the previous page and filter may be kept.</param>
    public void BeginSession(SpellBookFilter displayable, bool restorePreviousState)
    {
        if (displayable == SpellBookFilter.All)
        {
            Filter = SpellBookFilter.Adventure;
            StartIndex = 0;
        }
        else if (!restorePreviousState || Filter != displayable)
        {
            Filter = displayable;
            StartIndex = 0;
        }
    }

    /// <summary>
    /// Drops a stored index that is past the end of the list, the
    /// <c>_startSpellIndex &gt;= displayedSpells.size()</c> guard of <c>SpellBook::Open</c>
    /// (<c>spell_book.cpp:219-221</c>).
    /// </summary>
    /// <param name="spellCount">How many spells the list of <see cref="Filter"/> has.</param>
    public void ClampStartIndex(int spellCount)
    {
        if (StartIndex >= spellCount)
        {
            StartIndex = 0;
        }
    }

    /// <summary>
    /// Switches the tab, which always goes back to the first page, <c>SetFilter()</c>
    /// (<c>spell_book.cpp:496-552</c>).
    /// </summary>
    /// <remarks>
    /// The reference only honours a tab the book is allowed to show: with the book opened as the combat book
    /// the adventure tab does nothing, and vice versa. Clicking the tab that is already selected does nothing
    /// either.
    /// </remarks>
    /// <param name="filter">The tab that was clicked.</param>
    /// <param name="displayable">The filter the book was opened with.</param>
    /// <returns>Whether the filter changed, so the caller has to rebuild the list.</returns>
    public bool SelectFilter(SpellBookFilter filter, SpellBookFilter displayable)
    {
        if (displayable != SpellBookFilter.All || Filter == filter)
        {
            return false;
        }

        Filter = filter;
        StartIndex = 0;

        return true;
    }

    /// <summary>Turns one screen back, <c>_startSpellIndex -= spellsPerPage * 2</c>.</summary>
    /// <returns>Whether the page changed.</returns>
    public bool ShowPreviousPage()
    {
        if (IsFirstPage)
        {
            return false;
        }

        StartIndex -= SpellsPerScreen;
        if (StartIndex < 0)
        {
            StartIndex = 0;
        }

        return true;
    }

    /// <summary>Turns one screen forward.</summary>
    /// <param name="spellCount">How many spells the filtered list has.</param>
    /// <returns>Whether the page changed.</returns>
    public bool ShowNextPage(int spellCount)
    {
        if (IsLastPage(spellCount))
        {
            return false;
        }

        StartIndex += SpellsPerScreen;

        return true;
    }

    /// <summary>Forgets the filter and the page, <c>resetState()</c> (<c>spell_book.cpp:100-104</c>).</summary>
    public void ResetState()
    {
        Filter = SpellBookFilter.Adventure;
        StartIndex = 0;
    }

    /// <summary>
    /// The spells of the current screen, in list order, <c>drawSpellList()</c>'s slicing
    /// (<c>spell_book.cpp:250-262</c>).
    /// </summary>
    /// <param name="displayed">The filtered list, sorted by id.</param>
    /// <returns>Up to <see cref="SpellsPerScreen"/> spells.</returns>
    public IReadOnlyList<SpellType> ScreenSpells(IReadOnlyList<SpellType> displayed)
    {
        List<SpellType> result = [];

        for (int i = StartIndex; i < displayed.Count && result.Count < SpellsPerScreen; ++i)
        {
            result.Add(displayed[i]);
        }

        return result;
    }

    /// <summary>
    /// The spells of one tab, <c>SpellBook::SetFilter()</c> (<c>spell_book.cpp:496-552</c>).
    /// </summary>
    /// <remarks>
    /// The reference starts from a copy of the hero's spell storage, appends the scrolls in the bag, drops the
    /// half the tab does not show, and sorts by spell id. The port takes the hero's book and bag and returns
    /// the list instead of copying into itself, so a caller can ask for a tab without disturbing the book.
    /// </remarks>
    /// <param name="book">The spells the hero knows.</param>
    /// <param name="bag">The artifacts of the hero, for the scrolls; may be null.</param>
    /// <param name="filter">The tab to list.</param>
    /// <returns>The spell ids of the tab, sorted.</returns>
    public static IReadOnlyList<SpellType> SetFilter(SpellStorage book, ArtifactBag? bag, SpellBookFilter filter)
    {
        SpellStorage spells = new();
        spells.Append(book);

        if (bag is not null)
        {
            spells.Append(bag);
        }

        List<SpellType> result = [];

        for (int i = 0; i < spells.Count; ++i)
        {
            Spell spell = spells[i];

            if (filter == SpellBookFilter.Adventure && spell.IsCombat())
            {
                continue;
            }

            if (filter == SpellBookFilter.Combat && spell.IsAdventure())
            {
                continue;
            }

            result.Add(spell.Type);
        }

        result.Sort();

        return result;
    }
}
