using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Behaviour of <see cref="SpellBookWindow"/> / <see cref="SpellBookRenderer"/> against
/// <c>spell_book.cpp</c>: the two give-ups of <c>SpellBook::Open</c>, the layout of the bookmarks and of the
/// twelve rows, the mana rules and the edit screen.
/// </summary>
public sealed class SpellBookWindowTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TryOpen_GivesUpWithoutTheBookArtifact()
    {
        FakeOwner owner = new() { HasSpellBook = false };
        owner.MagicBook.Append(new Spell(SpellType.Fireball));

        Assert.Null(SpellBookWindow.TryOpen(owner, new SpellBook(), CreateArtwork(), CreateFonts(),
            SpellBookFilter.All, canCastSpell: true, restorePreviousState: false, Screen));

        Assert.Equal("You have no Magic Book, so you cannot cast a spell.",
            SpellBookWindow.NoMagicBookMessage);
    }

    [Fact]
    public void TryOpen_GivesUpOnAnEmptyBookWhenTheCallerWantsToCast()
    {
        FakeOwner owner = new();

        Assert.Null(SpellBookWindow.TryOpen(owner, new SpellBook(), CreateArtwork(), CreateFonts(),
            SpellBookFilter.All, canCastSpell: true, restorePreviousState: false, Screen));
        Assert.Equal("No spell to cast.", SpellBookWindow.NoSpellToCastMessage);

        // Looking at an empty book is fine: the reference only complains when it is going to be cast from.
        Assert.NotNull(SpellBookWindow.TryOpen(owner, new SpellBook(), CreateArtwork(), CreateFonts(),
            SpellBookFilter.All, canCastSpell: false, restorePreviousState: false, Screen));
    }

    [Fact]
    public void TryOpen_StartsOnTheAdventureTabWhenBothAreAllowed()
    {
        FakeOwner owner = CreateOwner();

        SpellBookWindow window = Open(owner);

        Assert.Equal(SpellBookFilter.Adventure, window.Filter);
        Assert.True(window.IsFirstPage);
        Assert.All(window.Spells, spell => Assert.True(new Spell(spell).IsAdventure()));
    }

    [Fact]
    public void TryOpen_KeepsThePageOnlyWhenAsked()
    {
        FakeOwner owner = CreateOwner();
        SpellBook book = new();

        // A book opened for both tabs always starts over (spell_book.cpp:203-206); the page is only kept when
        // the caller insists on one tab.
        SpellBookWindow first = Open(owner, book, displayable: SpellBookFilter.Adventure);
        first.ProcessEvents(WheelDown());

        Assert.Equal(12, first.StartIndex);

        SpellBookWindow restored = Open(owner, book, restorePreviousState: true,
            displayable: SpellBookFilter.Adventure);

        Assert.Equal(12, restored.StartIndex);

        SpellBookWindow fresh = Open(owner, book, restorePreviousState: false,
            displayable: SpellBookFilter.Adventure);

        Assert.Equal(0, fresh.StartIndex);
    }

    [Fact]
    public void TryOpen_DropsAPageIndexPastTheEnd()
    {
        FakeOwner owner = CreateOwner();
        SpellBook book = new();

        // Fourteen adventure spells fill two screens; hide them all and reopen on the second screen.
        Open(owner, book, displayable: SpellBookFilter.Adventure).ProcessEvents(WheelDown());
        Assert.Equal(12, book.StartIndex);

        owner.MagicBook.Clear();

        for (int i = 0; i < 3; ++i)
        {
            owner.MagicBook.Append(new Spell(SpellType.ViewMines + i));
        }

        SpellBookWindow window = Open(owner, book, restorePreviousState: true,
            displayable: SpellBookFilter.Adventure);

        Assert.Equal(0, window.StartIndex);
    }

    [Fact]
    public void Layout_PutsTheBookInTheMiddleAndTheBookmarksWhereTheReferenceDoes()
    {
        SpellBookWindow window = Open(CreateOwner());
        Rect pos = window.Area;

        // The page is 300x300, so a 600 wide book on a 640x480 screen starts at ( 20, 90 ).
        Assert.Equal(new Rect(20, 90, 600, 320), pos);
        Assert.Equal(new Rect(4, 90, 616, 336), window.BookArea);
        Assert.Equal(new Rect(50, 98, 30, 25), window.PreviousPageArea);
        Assert.Equal(new Rect(430, 98, 30, 25), window.NextPageArea);
        Assert.Equal(new Rect(143, 363, 14, 40), window.SpellPointsArea);
        Assert.Equal(SpellBookWindow.ViewPreviousPageText, "View previous page");
    }

    [Fact]
    public void SpellAreas_FollowTheTwoColumnGridOfTheReference()
    {
        SpellBookWindow window = Open(CreateOwner());

        Assert.Equal(12, window.SpellAreas.Count);

        // Row zero of the left page: the icon is 20x20, so it sits at 84 - 10 = 74 and 71 - 20 - 6 + 2 = 47.
        Assert.Equal(new Rect(20 + 74, 90 + 47, 20, 30), window.SpellAreas[0]);

        // The inner column of the right page carries the five-pixel `extraOffsetY` (spell_book.cpp:97-98), so
        // its first row is 66 - 20 - 6 + 2 = 42, five pixels above the outer column of the left page.
        Assert.Equal(new Rect(20 + 220 + 74, 90 + 42, 20, 30), window.SpellAreas[6]);
    }

    [Fact]
    public void SpellAreas_CoverOnlyTheSpellsOfTheScreen()
    {
        FakeOwner owner = CreateOwner();
        SpellBook book = new();
        SpellBookWindow window = Open(owner, book);

        window.ProcessEvents(WheelDown());

        Assert.Equal(2, window.SpellAreas.Count);
        Assert.True(window.IsLastPage);
    }

    [Fact]
    public void ClickingACastableSpellSelectsIt()
    {
        FakeOwner owner = CreateOwner();
        SpellBookWindow window = Open(owner);
        SpellType spell = window.Spells[0];

        SpellBookFrame frame = window.ProcessEvents(Click(Center(window.SpellAreas[0])));

        Assert.Equal(SpellBookAction.Select, frame.Action);
        Assert.Equal(spell, frame.Spell);
        Assert.Equal(spell, window.SelectedSpell);
        Assert.True(window.IsClosed);
    }

    [Fact]
    public void ClickingASpellWithoutManaExplainsWhyItCannotBeCast()
    {
        FakeOwner owner = CreateOwner();
        owner.SpellPoints = 0;
        SpellBookWindow window = Open(owner);
        Spell spell = new(window.Spells[0]);

        SpellBookFrame frame = window.ProcessEvents(Click(Center(window.SpellAreas[0])));

        Assert.Equal(SpellBookAction.ShowMessage, frame.Action);
        Assert.Equal(spell.Type, frame.Spell);
        Assert.Equal(
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                SpellBookWindow.NoSpellPointsMessage, spell.SpellPoints(owner.BagArtifacts)),
            frame.Text);
        Assert.False(window.IsClosed);
    }

    [Fact]
    public void ClickingASpellOfAReadOnlyBookShowsIt()
    {
        FakeOwner owner = CreateOwner();
        SpellBookWindow window = SpellBookWindow.TryEdit(owner, CreateArtwork(), CreateFonts(), Screen)!;

        SpellBookFrame frame = window.ProcessEvents(Click(Center(window.SpellAreas[0])));

        Assert.Equal(SpellBookAction.ShowSpell, frame.Action);
        Assert.Equal(window.Spells[0], frame.Spell);
        Assert.False(frame.ByRightClick);
    }

    [Fact]
    public void RightClickingASpellOpensTheTransientPopup()
    {
        SpellBookWindow window = Open(CreateOwner());

        SpellBookFrame frame = window.ProcessEvents(RightPress(Center(window.SpellAreas[0])));

        Assert.Equal(SpellBookAction.ShowSpell, frame.Action);
        Assert.Equal(window.Spells[0], frame.Spell);
        Assert.True(frame.ByRightClick);
    }

    [Fact]
    public void TheWheelTurnsThePage()
    {
        SpellBookWindow window = Open(CreateOwner());

        Assert.Equal(SpellBookAction.None, window.ProcessEvents(WheelDown()).Action);
        Assert.Equal(12, window.StartIndex);

        Assert.Equal(SpellBookAction.None, window.ProcessEvents(WheelUp()).Action);
        Assert.Equal(0, window.StartIndex);
    }

    [Fact]
    public void TheCombatTabSwitchesTheList()
    {
        SpellBookWindow window = Open(CreateOwner());

        SpellBookFrame frame = window.ProcessEvents(Click(Center(window.CombatTabArea)));

        Assert.Equal(SpellBookAction.None, frame.Action);
        Assert.Equal(SpellBookFilter.Combat, window.Filter);
        Assert.All(window.Spells, spell => Assert.True(new Spell(spell).IsCombat()));
    }

    [Fact]
    public void TheTabsOfAHalfBookDoNotSwitch()
    {
        FakeOwner owner = CreateOwner();
        SpellBookWindow window = SpellBookWindow.TryOpen(owner, new SpellBook(), CreateArtwork(),
            CreateFonts(), SpellBookFilter.Adventure, canCastSpell: true, restorePreviousState: false,
            Screen)!;

        window.ProcessEvents(Click(Center(window.CombatTabArea)));

        Assert.Equal(SpellBookFilter.Adventure, window.Filter);
    }

    [Fact]
    public void ClickingTheCloseBookmarkClosesOrShowsTheMana()
    {
        SpellBookWindow window = Open(CreateOwner());

        Assert.Equal(SpellBookAction.ShowMessage,
            window.ProcessEvents(Click(Center(window.SpellPointsArea))).Action);

        Assert.Equal(SpellBookAction.Close, window.ProcessEvents(Click(Center(window.CloseArea))).Action);
        Assert.True(window.IsClosed);
        Assert.Equal(SpellBookAction.None, window.ProcessEvents(Click(Center(window.CloseArea))).Action);
    }

    [Fact]
    public void TheStatusLineNamesWhatIsUnderTheCursor()
    {
        SpellBookWindow window = Open(CreateOwner());

        window.ProcessEvents(Hover(Center(window.SpellAreas[0])));

        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            SpellBookWindow.ViewSpellFormat, new Spell(window.Spells[0]).GetName()), window.StatusText);

        window.ProcessEvents(Hover(Center(window.SpellPointsArea)));

        Assert.Equal("Your hero has 120 spell points remaining out of 200.", window.StatusText);

        // The previous-page line is only offered once there is a page to go back to (:347).
        window.ProcessEvents(WheelDown());
        window.ProcessEvents(Hover(Center(window.PreviousPageArea)));

        Assert.Equal(SpellBookWindow.ViewPreviousPageText, window.StatusText);
    }

    [Fact]
    public void Edit_AsksForASpellOnAnEmptySpotAndRemovesItOnARightClick()
    {
        FakeOwner owner = new();
        SpellBookWindow window = SpellBookWindow.TryEdit(owner, CreateArtwork(), CreateFonts(), Screen)!;

        Assert.Empty(window.Spells);

        SpellBookFrame frame = window.ProcessEvents(Click(new Point(window.Area.X + 10, window.Area.Y + 10)));

        Assert.Equal(SpellBookAction.ChooseSpellToAdd, frame.Action);

        Assert.True(window.AddSpell(SpellType.Bless));
        Assert.Equal([SpellType.Bless], window.Spells);
        Assert.Contains(SpellType.Bless, window.ExcludedSpells);

        Assert.Equal(SpellBookAction.None,
            window.ProcessEvents(ClickCenter(window, window.SpellAreas[0], MouseButtonType.Right)).Action);
        Assert.Empty(window.Spells);
        Assert.Equal(0, owner.MagicBook.Count);
    }

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached()
    {
        SpellBookWindow window = Open(CreateOwner());

        Assert.Throws<InvalidOperationException>(() =>
            window.Show(new PixelBuffer(640, 480), new LocalEvent(() => 0)));
    }

    [Fact]
    public void Show_ReturnsThePickedSpell()
    {
        SpellBookWindow window = Open(CreateOwner());
        SpellType spell = window.Spells[0];
        Point point = Center(window.SpellAreas[0]);

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) =>
            {
                if (frame == 0)
                {
                    state.OnMouseMotion(point);
                    state.OnMouseButton(true, MouseButtonType.Left, point);
                }
                else
                {
                    state.OnMouseButton(false, MouseButtonType.Left, point);
                }
            },
        };

        events.Engine = engine;

        Assert.Equal(spell, window.Show(new PixelBuffer(640, 480), events));
        Assert.True(window.IsClosed);

        // Both frames were presented: the reference redraws the book every loop iteration.
        Assert.Equal(2, engine.Rendered.Count);
    }

    [Fact]
    public void Show_ReturnsNoneOnEscape()
    {
        SpellBookWindow window = Open(CreateOwner());

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) =>
                state.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down),
        };

        events.Engine = engine;

        Assert.Equal(SpellType.None, window.Show(new PixelBuffer(640, 480), events));
        Assert.True(window.IsClosed);
    }

    [Fact]
    public void Show_StopsOnChooseSpellToAdd()
    {
        SpellBookWindow window = SpellBookWindow.TryEdit(new FakeOwner(), CreateArtwork(), CreateFonts(),
            Screen)!;
        Point point = new(window.Area.X + 10, window.Area.Y + 10);

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) =>
            {
                if (frame == 0)
                {
                    state.OnMouseMotion(point);
                    state.OnMouseButton(true, MouseButtonType.Left, point);
                }
                else
                {
                    state.OnMouseButton(false, MouseButtonType.Left, point);
                }
            },
        };

        events.Engine = engine;

        Assert.Equal(SpellType.None, window.Show(new PixelBuffer(640, 480), events));

        // The editor's picker is the caller's job, so the loop leaves without closing the book.
        Assert.False(window.IsClosed);
    }

    /// <summary>An event engine that plays a scripted frame at a time and stops after a few of them.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public List<Rect> Rendered { get; } = [];

        public int Frames { get; private set; }

        public Action<LocalEvent, int>? OnFrame { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (Frames >= 8)
            {
                return false;
            }

            OnFrame?.Invoke(state, Frames++);

            return true;
        }

        public void Render(Rect area) => Rendered.Add(area);

        public void Sleep(ulong milliseconds)
        {
        }
    }

    [Fact]
    public void Edit_ClosesOnCancel()
    {
        SpellBookWindow window = SpellBookWindow.TryEdit(new FakeOwner(), CreateArtwork(), CreateFonts(),
            Screen)!;

        Assert.Equal(SpellBookAction.Close,
            window.ProcessEvents(new LocalEvent(() => 0), new SpellBookHotKeys(Cancel: true)).Action);
        Assert.True(window.IsClosed);
    }

    [Fact]
    public void TheBookIsAsTallAsItsLowestBookmark()
    {
        // With bookmarks 40 tall the close one ends lowest, at 280 + 40 = 320, for every filter.
        SpellBookArtwork artwork = CreateArtwork();

        Assert.Equal(new Size(600, 320), artwork.GetBookSize(SpellBookFilter.All));
        Assert.Equal(new Size(600, 320), artwork.GetBookSize(SpellBookFilter.Combat));
        Assert.Equal(new Size(600, 320), artwork.GetBookSize(SpellBookFilter.Adventure));

        // A close bookmark that ends above the tabs lets the filter show: the hidden tab no longer counts.
        SpellBookArtwork shortClose = new()
        {
            Page = artwork.Page,
            AdventureBookmark = artwork.AdventureBookmark,
            CombatBookmark = artwork.CombatBookmark,
            CloseBookmark = new Sprite(CreateBuffer(14, 10, 13)),
            SpellPointsBookmark = artwork.SpellPointsBookmark,
            StraightCorner = artwork.StraightCorner,
            SpellIcons = artwork.SpellIcons,
        };

        // Info ends at 313, adventure at 309, combat at 316, the short close at 290.
        Assert.Equal(new Size(600, 316), shortClose.GetBookSize(SpellBookFilter.All));
        Assert.Equal(new Size(600, 316), shortClose.GetBookSize(SpellBookFilter.Combat));
        Assert.Equal(new Size(600, 313), shortClose.GetBookSize(SpellBookFilter.Adventure));
    }

    [Fact]
    public void Draw_PaintsTheComposedBookAtTheShadowOffset()
    {
        SpellBookWindow window = Open(CreateOwner());
        PixelBuffer target = new(640, 480);
        target.Fill(1);

        window.Draw(target);

        // The page is filled with 7; the shadowed sprite starts 16 pixels left of the book.
        Assert.Equal(7, target[window.Area.X + 100, window.Area.Y + 100]);
        Assert.Equal(1, target[window.Area.X - 20, window.Area.Y - 20]);
    }

    [Fact]
    public void DescribeCantCast_WordsTheManaCases()
    {
        FakeOwner owner = new() { SpellPoints = 0 };
        Spell spell = new(SpellType.Fireball);

        // Fireball costs 9 spell points (spell_table.cpp).
        Assert.Equal("This spell costs 9 spell points. You have no spell points, so you cannot cast it.",
            SpellBookWindow.DescribeCantCast(owner, spell));

        owner.SpellPoints = 3;

        Assert.Equal("This spell costs 9 spell points. You only have 3 spell points, so you cannot cast it.",
            SpellBookWindow.DescribeCantCast(owner, spell));
    }

    [Fact]
    public void Guard_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => SpellBookWindow.TryOpen(null!, new SpellBook(),
            CreateArtwork(), CreateFonts(), SpellBookFilter.All, true, false, Screen));
        Assert.Throws<ArgumentNullException>(() => SpellBookWindow.TryOpen(new FakeOwner(), null!,
            CreateArtwork(), CreateFonts(), SpellBookFilter.All, true, false, Screen));
        Assert.Throws<ArgumentNullException>(() => SpellBookWindow.TryEdit(new FakeOwner(), null!,
            CreateFonts(), Screen));

        SpellBookWindow window = Open(CreateOwner());

        Assert.Throws<ArgumentNullException>(() => window.ProcessEvents(null!));
        Assert.Throws<ArgumentNullException>(() => window.Draw(null!));
    }

    private static SpellBookWindow Open(FakeOwner owner, SpellBook? book = null,
        bool restorePreviousState = false, SpellBookFilter displayable = SpellBookFilter.All)
    {
        return SpellBookWindow.TryOpen(owner, book ?? new SpellBook(), CreateArtwork(), CreateFonts(),
            displayable, canCastSpell: true, restorePreviousState, Screen)!;
    }

    /// <summary>An owner carrying fourteen adventure spells and three combat ones.</summary>
    private static FakeOwner CreateOwner()
    {
        FakeOwner owner = new() { SpellPoints = 120 };

        for (int i = 0; i < 14; ++i)
        {
            owner.MagicBook.Append(new Spell(SpellType.ViewMines + i));
        }

        owner.MagicBook.Append(new Spell(SpellType.Fireball));
        owner.MagicBook.Append(new Spell(SpellType.Cure));
        owner.MagicBook.Append(new Spell(SpellType.Bless));

        return owner;
    }

    private static SpellBookArtwork CreateArtwork()
    {
        return new SpellBookArtwork
        {
            Page = new Sprite(CreateBuffer(300, 300, 7)),
            AdventureBookmark = new Sprite(CreateBuffer(14, 40, 11)),
            CombatBookmark = new Sprite(CreateBuffer(14, 40, 12)),
            CloseBookmark = new Sprite(CreateBuffer(14, 40, 13)),
            SpellPointsBookmark = new Sprite(CreateBuffer(14, 40, 14)),
            StraightCorner = new Sprite(CreateBuffer(20, 20, 15)),
            SpellIcons = CreateIcons(),
        };
    }

    private static IReadOnlyList<Sprite> CreateIcons()
    {
        List<Sprite> icons = [];

        for (int i = 0; i < 80; ++i)
        {
            icons.Add(new Sprite(CreateBuffer(20, 20, i + 1)));
        }

        return icons;
    }

    private static SpellBookFonts CreateFonts()
    {
        FakeGlyph font = new();

        return new SpellBookFonts(font, font);
    }

    private static PixelBuffer CreateBuffer(int width, int height, int value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill((byte)value);

        return buffer;
    }

    private static LocalEvent Click(Point point)
    {
        LocalEvent events = new();

        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        return events;
    }

    private static LocalEvent ClickCenter(SpellBookWindow window, Rect area, MouseButtonType button)
    {
        Point point = Center(area);
        LocalEvent events = new();

        events.OnMouseMotion(point);
        events.OnMouseButton(true, button, point);
        events.OnMouseButton(false, button, point);

        return events;
    }

    private static LocalEvent RightPress(Point point)
    {
        LocalEvent events = new();

        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Right, point);

        return events;
    }

    private static LocalEvent Hover(Point point)
    {
        LocalEvent events = new();

        events.OnMouseMotion(point);

        return events;
    }

    private static LocalEvent WheelUp()
    {
        LocalEvent events = new();

        events.OnMouseWheel(new Point(0, 1));

        return events;
    }

    private static LocalEvent WheelDown()
    {
        LocalEvent events = new();

        events.OnMouseWheel(new Point(0, -1));

        return events;
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private sealed class FakeOwner : ISpellBookOwner
    {
        public SpellStorage MagicBook { get; } = new();

        public ArtifactBag BagArtifacts { get; } = new();

        public bool HasSpellBook { get; init; } = true;

        public uint SpellPoints { get; set; } = 100;

        public bool CanLearnSpell(Spell spell) => true;

        public uint GetMaxSpellPoints() => 200;

        public int GetPower() => 1;

        public bool CanCastSpell(Spell spell) => SpellPoints >= spell.SpellPoints(BagArtifacts);

        public bool AppendSpellToBook(Spell spell, bool ignoreWisdom = false)
        {
            MagicBook.Append(spell);

            return true;
        }
    }

    private sealed class FakeGlyph : IGlyphSource
    {
        private readonly Sprite _glyph = new(CreateBuffer(6, 12, 1));

        public int LineHeight => 12;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
