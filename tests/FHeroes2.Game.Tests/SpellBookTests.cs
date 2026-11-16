using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the filter, the page transitions and the session start-up rule of <c>spell_book.cpp</c>
/// (<c>SetFilter</c> at 496-552, the page state at 200-262).
/// </summary>
public sealed class SpellBookTests
{
    [Fact]
    public void TheBookSplitsTheTwoKindsOfSpell()
    {
        SpellStorage book = Book(SpellType.DimensionDoor, SpellType.Fireball, SpellType.ViewMines, SpellType.Cure);

        // SetFilter() sorts by spell id (spell_book.cpp:549).
        Assert.Equal(
            [SpellType.ViewMines, SpellType.DimensionDoor],
            SpellBook.SetFilter(book, null, SpellBookFilter.Adventure));

        Assert.Equal(
            [SpellType.Fireball, SpellType.Cure],
            SpellBook.SetFilter(book, null, SpellBookFilter.Combat));

        Assert.Equal(
            [SpellType.Fireball, SpellType.Cure, SpellType.ViewMines, SpellType.DimensionDoor],
            SpellBook.SetFilter(book, null, SpellBookFilter.All));
    }

    [Fact]
    public void TheFilterLeavesTheHerosBookAlone()
    {
        SpellStorage book = Book(SpellType.Fireball);

        // The reference copies the storage first; the port returns a list, so the book must not change.
        Assert.Equal([SpellType.Fireball], SpellBook.SetFilter(book, null, SpellBookFilter.Combat));
        Assert.Equal(1, book.Count);
    }

    [Fact]
    public void TheBookReopensOnThePageItWasClosedOn()
    {
        var book = new SpellBook();

        book.SelectFilter(SpellBookFilter.Combat, SpellBookFilter.All);
        book.ShowNextPage(30);

        Assert.Equal(SpellBookFilter.Combat, book.Filter);
        Assert.Equal(SpellBook.SpellsPerScreen, book.StartIndex);

        // Opened again with the same tab: the page survives (restorePreviousState).
        book.BeginSession(SpellBookFilter.Combat, true, 30);

        Assert.Equal(SpellBookFilter.Combat, book.Filter);
        Assert.Equal(SpellBook.SpellsPerScreen, book.StartIndex);

        // Opened with a different tab: back to the first page of that tab.
        book.BeginSession(SpellBookFilter.Adventure, true, 30);

        Assert.Equal(SpellBookFilter.Adventure, book.Filter);
        Assert.Equal(0, book.StartIndex);
    }

    [Fact]
    public void TheAllBookAlwaysOpensOnAdventureAndTheFirstPage()
    {
        var book = new SpellBook();

        book.SelectFilter(SpellBookFilter.Combat, SpellBookFilter.All);
        book.ShowNextPage(30);

        book.BeginSession(SpellBookFilter.All, true, 30);

        Assert.Equal(SpellBookFilter.Adventure, book.Filter);
        Assert.Equal(0, book.StartIndex);
    }

    [Fact]
    public void AStalePageFallsBackToTheFirstOne()
    {
        var book = new SpellBook();

        book.ShowNextPage(30);
        Assert.Equal(SpellBook.SpellsPerScreen, book.StartIndex);

        // The new list is shorter than the stored index, so the book has to start over.
        book.BeginSession(SpellBookFilter.Adventure, true, 3);

        Assert.Equal(0, book.StartIndex);
    }

    [Fact]
    public void OnlyTheAllBookLetsThePlayerSwitchTabs()
    {
        var book = new SpellBook();

        // Opened as the combat book: the adventure tab does nothing.
        Assert.False(book.SelectFilter(SpellBookFilter.Combat, SpellBookFilter.Adventure));
        Assert.Equal(SpellBookFilter.Adventure, book.Filter);

        // The selected tab again does nothing either.
        Assert.False(book.SelectFilter(SpellBookFilter.Adventure, SpellBookFilter.All));
        Assert.False(book.SelectFilter(SpellBookFilter.Adventure, SpellBookFilter.All));

        Assert.True(book.SelectFilter(SpellBookFilter.Combat, SpellBookFilter.All));
        Assert.Equal(SpellBookFilter.Combat, book.Filter);
    }

    [Fact]
    public void ThePageTurnsStopAtBothEnds()
    {
        var book = new SpellBook();

        Assert.False(book.ShowPreviousPage());
        Assert.False(book.ShowNextPage(SpellBook.SpellsPerScreen));

        Assert.True(book.ShowNextPage(SpellBook.SpellsPerScreen + 1));
        Assert.Equal(SpellBook.SpellsPerScreen, book.StartIndex);
        Assert.False(book.ShowNextPage(SpellBook.SpellsPerScreen + 1));

        Assert.True(book.ShowPreviousPage());
        Assert.Equal(0, book.StartIndex);
        Assert.False(book.ShowPreviousPage());
    }

    [Fact]
    public void TheScreenSliceIsAtMostTwelveSpellsFromTheStartIndex()
    {
        var book = new SpellBook();
        List<SpellType> displayed = [];

        for (int i = 0; i < 20; ++i)
        {
            displayed.Add(SpellType.Fireball);
        }

        Assert.Equal(SpellBook.SpellsPerScreen, book.ScreenSpells(displayed).Count);

        book.ShowNextPage(displayed.Count);

        // The second screen has eight spells left.
        Assert.Equal(8, book.ScreenSpells(displayed).Count);
    }

    [Fact]
    public void TheScrollsOfTheBagJoinTheBook()
    {
        SpellStorage book = Book(SpellType.Fireball);

        // Scrolls do not carry their spell yet (ADR-142), so the bag contributes nothing.
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.SpellScroll);

        Assert.Equal([SpellType.Fireball], SpellBook.SetFilter(book, bag, SpellBookFilter.All));
    }

    /// <summary>A hero's storage holding exactly the given spells.</summary>
    private static SpellStorage Book(params SpellType[] spells)
    {
        SpellStorage book = new();

        foreach (SpellType spell in spells)
        {
            book.Append(new Spell(spell));
        }

        return book;
    }
}
