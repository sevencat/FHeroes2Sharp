using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The two small fonts the rows of the book are written with: <c>smallWhite()</c> for a spell the owner can
/// cast and the grey small font for one it cannot (<c>spell_book.cpp:105-121</c>).
/// </summary>
/// <param name="SmallWhite">The small white font.</param>
/// <param name="SmallGray">The small grey font.</param>
public readonly record struct SpellBookFonts(IGlyphSource SmallWhite, IGlyphSource SmallGray);

/// <summary>What one frame of a spell book asked the caller to do.</summary>
public enum SpellBookAction
{
    /// <summary>Nothing; at most the page turned.</summary>
    None = 0,

    /// <summary>The book was closed.</summary>
    Close,

    /// <summary>A spell was picked to be cast, the <c>curspell</c> return of <c>SpellBook::Open</c>.</summary>
    Select,

    /// <summary>A spell popup has to be shown, <c>SpellDialogElement::showPopup()</c>.</summary>
    ShowSpell,

    /// <summary>A plain message has to be shown, <c>showStandardTextMessage()</c>.</summary>
    ShowMessage,

    /// <summary>An empty spot was clicked in edit mode: the caller has to ask for a spell to add.</summary>
    ChooseSpellToAdd,
}

/// <summary>What one frame of the spell book produced.</summary>
/// <param name="Action">What the caller has to do.</param>
/// <param name="Spell">The spell of <see cref="SpellBookAction.Select"/> and <see cref="SpellBookAction.ShowSpell"/>.</param>
/// <param name="Text">The sentence of <see cref="SpellBookAction.ShowMessage"/>.</param>
/// <param name="ByRightClick">Whether the reference opens the box with <c>Dialog::ZERO</c> rather than <c>Dialog::OK</c>.</param>
public readonly record struct SpellBookFrame(SpellBookAction Action, SpellType Spell = SpellType.None,
    string Text = "", bool ByRightClick = false);

/// <summary>The keyboard shortcuts the spell book answers, the <c>HotKeyPressEvent()</c> calls of the reference.</summary>
/// <param name="CloseWindow">Whether <c>Game::HotKeyCloseWindow()</c> fired, <c>:309</c>.</param>
/// <param name="Cancel">Whether <c>DEFAULT_CANCEL</c> fired, the close shortcut of the edit screen, <c>:453</c>.</param>
/// <param name="PreviousPage">Whether <c>DEFAULT_LEFT</c> fired, <c>:270</c>.</param>
/// <param name="NextPage">Whether <c>DEFAULT_RIGHT</c> fired, <c>:275</c>.</param>
public readonly record struct SpellBookHotKeys(bool CloseWindow = false, bool Cancel = false,
    bool PreviousPage = false, bool NextPage = false);

/// <summary>
/// An open spell book — <c>SpellBook::Open()</c> and <c>SpellBook::Edit()</c> of <c>spell_book.cpp:194-505</c>
/// as one window with two modes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Frames, not a loop.</b> The reference owns a <c>while ( le.HandleEvents() )</c> loop, restores the screen
/// behind the book itself and opens its message boxes from inside. The port draws with <see cref="Draw"/>, reads
/// one frame with <see cref="ProcessEvents"/> and reports what has to be shown through
/// <see cref="SpellBookFrame"/>, so the scene keeps the message boxes, the cursor and the status bar.
/// </para>
/// <para>
/// <b>Two modes.</b> <see cref="TryOpen"/> is the book a hero casts from: tabs, mana bookmark, grey entries, and
/// the chosen spell. <see cref="TryEdit"/> is the editor's book: both tabs, no mana rules, a click shows a spell,
/// a click on an empty spot asks the caller for one, a right click removes one. The reference writes the second
/// as a separate function with a local page index, and the port keeps that: an editing book stores no page.
/// </para>
/// <para>
/// <b>Which page.</b> The page and the filter of the casting mode live in the <see cref="SpellBook"/> the caller
/// hands in — the reference keeps them in the hero's book — so a caller that wants the book to reopen where it
/// was closed has to keep that object alive.
/// </para>
/// </remarks>
public sealed partial class SpellBookWindow
{
    /// <summary>Where the mana bookmark is drawn and clicked, <c>bookmarkInfoOffset</c>.</summary>
    public static readonly Point InfoBookmarkOffset = new(123, 273);

    /// <summary>Where the adventure tab is drawn, <c>bookmarkAdvOffset</c>.</summary>
    public static readonly Point AdventureBookmarkOffset = new(266, 269);

    /// <summary>Where the combat tab is drawn, <c>bookmarkCombatoOffset</c>.</summary>
    public static readonly Point CombatBookmarkOffset = new(299, 276);

    /// <summary>Where the close bookmark is drawn, <c>bookmarkCloseOffset</c>.</summary>
    public static readonly Point CloseBookmarkOffset = new(416, 280);

    /// <summary>The clickable rectangle of the close bookmark in edit mode, <c>pos + ( 420, 284 )</c>, <c>:429</c>.</summary>
    public static readonly Point EditCloseBookmarkOffset = new(420, 284);

    /// <summary>Offset of the drop shadow, <c>spellBookShadow</c>: the book is drawn at <c>position + shadow</c>.</summary>
    public static readonly Point ShadowOffset = new(-16, 16);

    /// <summary>How dark the shadow is, the third argument of the reference's <c>addShadow()</c>.</summary>
    public const byte ShadowTransform = 3;

    /// <summary>Shown when a hero without the book artifact asks to cast, <c>:199</c>.</summary>
    public const string NoMagicBookMessage = "You have no Magic Book, so you cannot cast a spell.";

    /// <summary>Shown when the book is empty, <c>:215</c>.</summary>
    public const string NoSpellToCastMessage = "No spell to cast.";

    /// <summary>The mana line, <c>:259-262</c>: <c>{0}</c> the mana left, <c>{1}</c> the cap.</summary>
    public const string SpellPointsInfoFormat = "Your hero has {0} spell points remaining out of {1}.";

    /// <summary>The reason with no mana at all, <c>heroes_base.cpp:392</c>: <c>{0}</c> the cost.</summary>
    public const string NoSpellPointsMessage =
        "This spell costs {0} spell points. You have no spell points, so you cannot cast it.";

    /// <summary>The reason with too little mana, <c>heroes_base.cpp:396</c>: <c>{0}</c> the cost, <c>{1}</c> the mana.</summary>
    public const string FewSpellPointsMessage =
        "This spell costs {0} spell points. You only have {1} spell points, so you cannot cast it.";

    /// <summary>The tooltip of the adventure tab, <c>:298</c>.</summary>
    public const string ViewAdventureSpellsText = "View Adventure Spells";

    /// <summary>The tooltip of the combat tab, <c>:301</c>.</summary>
    public const string ViewCombatSpellsText = "View Combat Spells";

    /// <summary>The tooltip of the previous page area, <c>:304</c>.</summary>
    public const string ViewPreviousPageText = "View previous page";

    /// <summary>The tooltip of the next page area, <c>:307</c>.</summary>
    public const string ViewNextPageText = "View next page";

    /// <summary>The tooltip of the close bookmark, <c>:354</c>.</summary>
    public const string CloseSpellBookText = "Close Spellbook";

    /// <summary>The tooltip of a spell, <c>:362</c>: <c>{0}</c> its name.</summary>
    public const string ViewSpellFormat = "View {0}";

    /// <summary>The tooltip of an empty spot of a book opened for both tabs, <c>:374</c>.</summary>
    public const string ViewSpellsText = "View Spells";

    private readonly ISpellBookOwner _owner;
    private readonly SpellBookArtwork _artwork;
    private readonly SpellBookRenderer _renderer;
    private readonly SpellBookFilter _displayable;
    private readonly bool _canCastSpell;
    private readonly SpellBook? _book;
    private readonly List<SpellType> _spells;
    private readonly List<Rect> _coords = [];

    private readonly Rect _pos;
    private readonly Rect _bookArea;
    private readonly Rect _previousPage;
    private readonly Rect _nextPage;
    private readonly Rect _spellPoints;
    private readonly Rect _adventureTab;
    private readonly Rect _combatTab;
    private readonly Rect _close;

    private int _pageIndex;
    private Sprite _composed;
    private bool _closed;

    private SpellBookWindow(ISpellBookOwner owner, SpellBookArtwork artwork, SpellBookFonts fonts,
        SpellBookFilter displayable, bool canCastSpell, SpellBook? book, IReadOnlyList<SpellType> spells,
        Size screen)
    {
        _owner = owner;
        _artwork = artwork;
        _displayable = displayable;
        _canCastSpell = canCastSpell;
        _book = book;
        _renderer = new SpellBookRenderer(artwork, fonts, owner, canCastSpell);
        _spells = [.. spells];

        Sprite page = artwork.Page;
        Size bookSize = artwork.GetBookSize(displayable);

        _pos = new Rect((screen.Width - (page.Width * 2)) / 2, (screen.Height - page.Height) / 2,
            bookSize.Width, bookSize.Height);
        _bookArea = new Rect(_pos.X + ShadowOffset.X, _pos.Y, _pos.Width - ShadowOffset.X,
            _pos.Height + ShadowOffset.Y);

        _previousPage = new Rect(_pos.X + 30, _pos.Y + 8, 30, 25);
        _nextPage = new Rect(_pos.X + 410, _pos.Y + 8, 30, 25);

        _spellPoints = new Rect(_pos.X + InfoBookmarkOffset.X, _pos.Y + InfoBookmarkOffset.Y,
            artwork.SpellPointsBookmark.Width, artwork.SpellPointsBookmark.Height);
        _adventureTab = new Rect(_pos.X + AdventureBookmarkOffset.X, _pos.Y + AdventureBookmarkOffset.Y,
            artwork.AdventureBookmark.Width, artwork.AdventureBookmark.Height);
        _combatTab = new Rect(_pos.X + CombatBookmarkOffset.X, _pos.Y + CombatBookmarkOffset.Y,
            artwork.CombatBookmark.Width, artwork.CombatBookmark.Height);

        Point closeOffset = book is null ? EditCloseBookmarkOffset : CloseBookmarkOffset;
        _close = new Rect(_pos.X + closeOffset.X, _pos.Y + closeOffset.Y, artwork.CloseBookmark.Width,
            artwork.CloseBookmark.Height);

        _composed = Compose();
    }

    /// <summary>
    /// Opens the book of a hero who is going to cast, <c>SpellBook::Open()</c> (<c>spell_book.cpp:194-403</c>).
    /// </summary>
    /// <remarks>
    /// The reference shows one of two messages and gives up when the hero has no book, or when the book is empty
    /// and the caller means to cast. The port returns <see langword="null"/> instead; the caller picks the
    /// sentence from <see cref="NoMagicBookMessage"/> and <see cref="NoSpellToCastMessage"/>.
    /// </remarks>
    /// <param name="owner">The hero or captain whose book it is.</param>
    /// <param name="book">The page state to continue from, kept by the caller.</param>
    /// <param name="artwork">Frames of <c>ICN::BOOK</c> and the spell icons.</param>
    /// <param name="fonts">The two small fonts of the rows.</param>
    /// <param name="displayable">The tabs the caller allows, <see cref="SpellBookFilter.All"/> for both.</param>
    /// <param name="canCastSpell">Whether the book is opened to cast, which greys out what cannot be cast.</param>
    /// <param name="restorePreviousState">Whether the stored page and filter may be kept.</param>
    /// <param name="screen">The screen to centre on; the logical screen when omitted.</param>
    /// <returns>The window, or <see langword="null"/> for one of the two give-ups.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SpellBookWindow? TryOpen(ISpellBookOwner owner, SpellBook book, SpellBookArtwork artwork,
        SpellBookFonts fonts, SpellBookFilter displayable, bool canCastSpell, bool restorePreviousState,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(artwork);

        if (!owner.HasSpellBook)
        {
            return null;
        }

        book.BeginSession(displayable, restorePreviousState);

        List<SpellType> spells = [.. SpellBook.SetFilter(owner.MagicBook, owner.BagArtifacts, book.Filter)];

        if (canCastSpell && spells.Count == 0)
        {
            return null;
        }

        book.ClampStartIndex(spells.Count);

        return new SpellBookWindow(owner, artwork, fonts, displayable, canCastSpell, book, spells,
            screen ?? ScreenBuffer.DefaultSize);
    }

    /// <summary>
    /// Opens the book for editing, <c>SpellBook::Edit()</c> (<c>spell_book.cpp:405-505</c>).
    /// </summary>
    /// <remarks>
    /// Both tabs always, no mana rules, and no remembered page — the reference uses a local index here, so
    /// nothing of this mode is written back, and it neither checks for the book artifact nor minds an empty book.
    /// </remarks>
    /// <param name="owner">The hero or captain whose book it is.</param>
    /// <param name="artwork">Frames of <c>ICN::BOOK</c> and the spell icons.</param>
    /// <param name="fonts">The two small fonts of the rows.</param>
    /// <param name="screen">The screen to centre on; the logical screen when omitted.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SpellBookWindow TryEdit(ISpellBookOwner owner, SpellBookArtwork artwork,
        SpellBookFonts fonts, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(artwork);

        List<SpellType> spells =
            [.. SpellBook.SetFilter(owner.MagicBook, owner.BagArtifacts, SpellBookFilter.All)];

        return new SpellBookWindow(owner, artwork, fonts, SpellBookFilter.All, false, null, spells,
            screen ?? ScreenBuffer.DefaultSize);
    }

    /// <summary>The area the book occupies, <c>pos</c>; the shadow reaches outside of it.</summary>
    public Rect Area => _pos;

    /// <summary>The area the book and its shadow occupy, the reference's <c>ImageRestorer</c> rectangle.</summary>
    public Rect BookArea => _bookArea;

    /// <summary>The clickable area that turns one page back, <c>previousPageRoi</c>.</summary>
    public Rect PreviousPageArea => _previousPage;

    /// <summary>The clickable area that turns one page forward, <c>nextPageRoi</c>.</summary>
    public Rect NextPageArea => _nextPage;

    /// <summary>The mana bookmark, <c>spellPointsRoi</c>.</summary>
    public Rect SpellPointsArea => _spellPoints;

    /// <summary>The adventure tab, <c>adventureSpellsRoi</c>; hidden and inert in a combat-only book.</summary>
    public Rect AdventureTabArea => _adventureTab;

    /// <summary>The combat tab, <c>combatSpellsRoi</c>; hidden and inert in an adventure-only book.</summary>
    public Rect CombatTabArea => _combatTab;

    /// <summary>The close bookmark, <c>closeRoi</c>.</summary>
    public Rect CloseArea => _close;

    /// <summary>The clickable rectangle of every spell on the screen, in screen coordinates, <c>coords</c>.</summary>
    public IReadOnlyList<Rect> SpellAreas => _coords;

    /// <summary>The whole filtered list the book pages through, <c>displayedSpells</c>.</summary>
    public IReadOnlyList<SpellType> Spells => _spells;

    /// <summary>Whether this is the edit screen, <c>SpellBook::Edit()</c>.</summary>
    public bool Editing => _book is null;

    /// <summary>The tab on screen: the stored filter, or all spells for the edit screen.</summary>
    public SpellBookFilter Filter => _book?.Filter ?? SpellBookFilter.All;

    /// <summary>The index of the first spell on the screen, <c>_startSpellIndex</c>.</summary>
    public int StartIndex => _book?.StartIndex ?? _pageIndex;

    /// <summary>Whether the first screen is shown, <c>_isFirstPage()</c>.</summary>
    public bool IsFirstPage => StartIndex == 0;

    /// <summary>Whether the last screen reaches the end of the list, <c>_isLastPage()</c>.</summary>
    public bool IsLastPage => StartIndex + SpellBook.SpellsPerScreen >= _spells.Count;

    /// <summary>The composed, shadowed book, ready to be blitted at <see cref="Area"/>.</summary>
    public Sprite Composed => _composed;

    /// <summary>Whether the book has closed.</summary>
    public bool IsClosed => _closed;

    /// <summary>The spell that was picked, <see cref="SpellType.None"/> until a click selects one.</summary>
    public SpellType SelectedSpell { get; private set; }

    /// <summary>
    /// The line a status bar should show, the argument of the reference's <c>statusCallback</c>
    /// (<c>spell_book.cpp:337-377</c>).
    /// </summary>
    /// <remarks>
    /// The reference only calls the callback while the cursor is over a part of the book; it never calls it with
    /// an empty string, the status bar owning the return to the default line. The port does the same, so the last
    /// line stays until something else of the book is hovered.
    /// </remarks>
    public string StatusText { get; private set; } = string.Empty;

    /// <summary>The mana line of the bookmark, <c>getHeroSpellPointsInfo()</c>.</summary>
    public string SpellPointsInfo => string.Format(CultureInfo.InvariantCulture, SpellPointsInfoFormat,
        _owner.SpellPoints, _owner.GetMaxSpellPoints());

    /// <summary>
    /// The spells the picker of the edit screen has to hide, the <c>spellsToExclude</c> set of
    /// <c>spell_book.cpp:468-471</c>: everything already written in the book.
    /// </summary>
    public IReadOnlySet<SpellType> ExcludedSpells
    {
        get
        {
            HashSet<SpellType> excluded = [];

            for (int i = 0; i < _owner.MagicBook.Count; ++i)
            {
                excluded.Add(_owner.MagicBook[i].Type);
            }

            return excluded;
        }
    }

    /// <summary>
    /// Why a spell cannot be cast, <c>HeroBase::CanCastSpell( spell, &amp;message )</c>.
    /// </summary>
    /// <remarks>
    /// The reference has a branch per reason, but with a human hero's open book only the mana ones can fire: the
    /// others are asserts for corrupt state (no book, spell not written, adventure spell rules, a ship in the
    /// way). The port therefore words the mana cases and leaves the rest to the caller's own checks.
    /// </remarks>
    /// <param name="owner">The caster.</param>
    /// <param name="spell">The spell that cannot be cast.</param>
    /// <returns>The sentence to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public static string DescribeCantCast(ISpellBookOwner owner, Spell spell)
    {
        ArgumentNullException.ThrowIfNull(owner);

        uint cost = spell.SpellPoints(owner.BagArtifacts);

        return owner.SpellPoints == 0
            ? string.Format(CultureInfo.InvariantCulture, NoSpellPointsMessage, cost)
            : string.Format(CultureInfo.InvariantCulture, FewSpellPointsMessage, cost, owner.SpellPoints);
    }

    /// <summary>Draws the book, the <c>Blit()</c> that follows <c>spellBookRedrawLists()</c>.</summary>
    /// <remarks>
    /// The reference restores the screen behind the book and repaints it in place; the port draws the composed
    /// sprite — shadow included — onto whatever the scene is building, so the scene decides what is underneath.
    /// </remarks>
    /// <param name="target">The screen buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_closed)
        {
            return;
        }

        ImageOps.BlitSprite(target, new Point(_pos.X + ShadowOffset.X, _pos.Y), _composed);
    }

    /// <summary>Handles one frame of input, the body of the reference's message loop.</summary>
    /// <remarks>
    /// The reference handles one action per frame — its chain of <c>else if</c>s — and repaints afterwards; the
    /// port does the same and reports what has to be shown. The status line is refreshed on the same frame from
    /// the cursor position, like the reference's <c>statusCallback</c> block.
    /// </remarks>
    /// <param name="events">What the player did this frame.</param>
    /// <param name="hotKeys">The keyboard shortcuts, the reference's <c>HotKeyPressEvent()</c> calls.</param>
    /// <returns>The action the caller has to take.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public SpellBookFrame ProcessEvents(LocalEvent events, SpellBookHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_closed)
        {
            return new SpellBookFrame(SpellBookAction.None);
        }

        return Editing ? ProcessEditEvents(events, hotKeys) : ProcessCastEvents(events, hotKeys);
    }

    /// <summary>
    /// Runs the book until it closes or a spell is picked, the <c>while ( le.HandleEvents() )</c> loop of
    /// <c>SpellBook::Open()</c> (<c>spell_book.cpp:269-397</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The frames of <see cref="ProcessEvents"/> that ask for a message box or the spell popup
    /// (<see cref="SpellBookAction.ShowMessage"/>, <see cref="SpellBookAction.ShowSpell"/>) are drawn and
    /// dropped: the port's message box needs its artwork passed in and this window does not own it. The loop
    /// therefore looks exactly like the reference apart from those two boxes, which the caller is expected to
    /// put on top of the book once it owns the fonts and the button plates.
    /// </para>
    /// <para>
    /// <see cref="SpellBookAction.ChooseSpellToAdd"/> only happens in the editor's book and needs
    /// <see cref="SpellSelectDialog"/>; the loop stops there and returns <see cref="SpellType.None"/>, leaving the
    /// caller to open the picker and call <see cref="AddSpell"/>.
    /// </para>
    /// <para>
    /// The keyboard shortcuts are the reference's defaults: <c>Escape</c> for
    /// <c>HotKeyCloseWindow()</c> / <c>DEFAULT_CANCEL</c>, the arrows for <c>DEFAULT_LEFT</c> and
    /// <c>DEFAULT_RIGHT</c>. The App does not have a rebindable hot key table yet.
    /// </para>
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit; it must already contain the scene behind.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The picked spell, or <see cref="SpellType.None"/> when the book was closed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public SpellType Show(PixelBuffer target, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "SpellBookWindow.Show() needs an event engine: attach LocalEvent.Engine first.");

        while (!IsClosed)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return SelectedSpell;
            }

            SpellBookFrame frame = ProcessEvents(eventState, ReadHotKeys(eventState));

            Draw(target);
            engine.Render(target.Bounds);

            // The book closes itself on the frame that picks a spell or asks for the editor's picker.
            if (IsClosed || frame.Action == SpellBookAction.ChooseSpellToAdd)
            {
                break;
            }

            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return SelectedSpell;
    }

    /// <summary>The default shortcuts of the reference, read off the frame.</summary>
    /// <param name="events">Event state of the frame.</param>
    /// <returns>The shortcuts <see cref="ProcessEvents"/> takes.</returns>
    private static SpellBookHotKeys ReadHotKeys(LocalEvent events) => new(
        CloseWindow: events.IsKeyPressed(Key.Escape),
        Cancel: events.IsKeyPressed(Key.Escape),
        PreviousPage: events.IsKeyPressed(Key.Left),
        NextPage: events.IsKeyPressed(Key.Right));

    /// <summary>The casting mode of the loop, <c>spell_book.cpp:269-397</c>.</summary>
    /// <param name="events">What the player did this frame.</param>
    /// <param name="hotKeys">The keyboard shortcuts.</param>
    /// <returns>The action of the frame.</returns>
    private SpellBookFrame ProcessCastEvents(LocalEvent events, SpellBookHotKeys hotKeys)
    {
        if (!IsFirstPage && (events.MouseClickLeft(_previousPage) || hotKeys.PreviousPage ||
            events.IsMouseWheelUp))
        {
            _book!.ShowPreviousPage();
            Refresh();

            return new SpellBookFrame(SpellBookAction.None);
        }

        if (!IsLastPage && (events.MouseClickLeft(_nextPage) || hotKeys.NextPage || events.IsMouseWheelDown))
        {
            _book!.ShowNextPage(_spells.Count);
            Refresh();

            return new SpellBookFrame(SpellBookAction.None);
        }

        // A tab of a book that was opened for one half does not switch: the reference neither draws it nor answers
        // it, and `SpellBook::SelectFilter()` refuses the same way.
        if (events.MouseClickLeft(_spellPoints))
        {
            return new SpellBookFrame(SpellBookAction.ShowMessage, Text: SpellPointsInfo);
        }

        if (events.MouseClickLeft(_adventureTab) && _displayable != SpellBookFilter.Combat &&
            _book!.SelectFilter(SpellBookFilter.Adventure, _displayable))
        {
            RefreshCastList();

            return new SpellBookFrame(SpellBookAction.None);
        }

        if (events.MouseClickLeft(_combatTab) && _displayable != SpellBookFilter.Adventure &&
            _book!.SelectFilter(SpellBookFilter.Combat, _displayable))
        {
            RefreshCastList();

            return new SpellBookFrame(SpellBookAction.None);
        }

        if (events.IsMouseRightButtonPressedInArea(_spellPoints))
        {
            return new SpellBookFrame(SpellBookAction.ShowMessage, Text: SpellPointsInfo, ByRightClick: true);
        }

        if (events.IsMouseRightButtonPressedInArea(_adventureTab) && _displayable != SpellBookFilter.Combat)
        {
            return new SpellBookFrame(SpellBookAction.ShowMessage, Text: ViewAdventureSpellsText,
                ByRightClick: true);
        }

        if (events.IsMouseRightButtonPressedInArea(_combatTab) && _displayable != SpellBookFilter.Adventure)
        {
            return new SpellBookFrame(SpellBookAction.ShowMessage, Text: ViewCombatSpellsText,
                ByRightClick: true);
        }

        if (!IsFirstPage && events.IsMouseRightButtonPressedInArea(_previousPage))
        {
            return new SpellBookFrame(SpellBookAction.ShowMessage, Text: ViewPreviousPageText,
                ByRightClick: true);
        }

        if (!IsLastPage && events.IsMouseRightButtonPressedInArea(_nextPage))
        {
            return new SpellBookFrame(SpellBookAction.ShowMessage, Text: ViewNextPageText, ByRightClick: true);
        }

        if (events.MouseClickLeft(_close) || hotKeys.CloseWindow)
        {
            _closed = true;

            return new SpellBookFrame(SpellBookAction.Close);
        }

        int index = events.MouseClickLeft(_pos) ? GetSpellIndex(events.CursorPosition) : -1;

        if (index >= 0)
        {
            SpellType spell = _spells[StartIndex + index];

            if (_canCastSpell)
            {
                if (!_owner.CanCastSpell(new Spell(spell)))
                {
                    return new SpellBookFrame(SpellBookAction.ShowMessage, spell,
                        DescribeCantCast(_owner, new Spell(spell)));
                }

                SelectedSpell = spell;
                _closed = true;

                return new SpellBookFrame(SpellBookAction.Select, spell);
            }

            return new SpellBookFrame(SpellBookAction.ShowSpell, spell);
        }

        UpdateStatus(events.CursorPosition);

        if (events.IsMouseRightButtonPressedInArea(_pos) &&
            GetSpellIndex(events.CursorPosition) is int hovered && hovered >= 0)
        {
            return new SpellBookFrame(SpellBookAction.ShowSpell, _spells[StartIndex + hovered], "",
                ByRightClick: true);
        }

        return new SpellBookFrame(SpellBookAction.None);
    }

    /// <summary>The edit mode of the loop, <c>spell_book.cpp:443-501</c>.</summary>
    /// <param name="events">What the player did this frame.</param>
    /// <param name="hotKeys">The keyboard shortcuts.</param>
    /// <returns>The action of the frame.</returns>
    private SpellBookFrame ProcessEditEvents(LocalEvent events, SpellBookHotKeys hotKeys)
    {
        if (_pageIndex > 0 && (events.MouseClickLeft(_previousPage) || hotKeys.PreviousPage ||
            events.IsMouseWheelUp))
        {
            _pageIndex = Math.Max(0, _pageIndex - SpellBook.SpellsPerScreen);
            Refresh();

            return new SpellBookFrame(SpellBookAction.None);
        }

        if (_spells.Count > _pageIndex + SpellBook.SpellsPerScreen &&
            (events.MouseClickLeft(_nextPage) || hotKeys.NextPage || events.IsMouseWheelDown))
        {
            _pageIndex += SpellBook.SpellsPerScreen;
            Refresh();

            return new SpellBookFrame(SpellBookAction.None);
        }

        if (events.MouseClickLeft(_close) || hotKeys.Cancel)
        {
            _closed = true;

            return new SpellBookFrame(SpellBookAction.Close);
        }

        if (events.MouseClickLeft(_pos))
        {
            int index = GetSpellIndex(events.CursorPosition);

            if (index < 0)
            {
                // The reference asks `Dialog::selectSpell()` right here; the port reports the request so the
                // caller can open `SpellSelectDialog` with `ExcludedSpells` and come back with AddSpell().
                return new SpellBookFrame(SpellBookAction.ChooseSpellToAdd);
            }

            return new SpellBookFrame(SpellBookAction.ShowSpell, _spells[_pageIndex + index]);
        }

        if (events.MouseClickRight(_pos))
        {
            int index = GetSpellIndex(events.CursorPosition);

            if (index >= 0 && RemoveSpell(_spells[_pageIndex + index]))
            {
                return new SpellBookFrame(SpellBookAction.None);
            }
        }

        return new SpellBookFrame(SpellBookAction.None);
    }

    /// <summary>
    /// Adds a spell to the book of an editing screen, the <c>Append( spell )</c> of
    /// <c>spell_book.cpp:475-477</c>.
    /// </summary>
    /// <param name="spell">The spell the picker returned.</param>
    /// <returns>Whether the book changed.</returns>
    public bool AddSpell(SpellType spell)
    {
        if (spell == SpellType.None)
        {
            return false;
        }

        int before = _owner.MagicBook.Count;
        _owner.MagicBook.Append(new Spell(spell));

        if (_owner.MagicBook.Count == before)
        {
            return false;
        }

        RefreshEditList();

        return true;
    }

    /// <summary>
    /// Removes a spell from the book of an editing screen, the <c>removeSpell( spell )</c> of
    /// <c>spell_book.cpp:488-491</c>.
    /// </summary>
    /// <param name="spell">The spell under the cursor.</param>
    /// <returns>Whether the book changed.</returns>
    public bool RemoveSpell(SpellType spell)
    {
        if (!_owner.MagicBook.Remove(new Spell(spell)))
        {
            return false;
        }

        RefreshEditList();

        return true;
    }

    /// <summary>The status line of the reference's callback block, <c>spell_book.cpp:337-377</c>.</summary>
    /// <param name="cursor">The cursor position.</param>
    private void UpdateStatus(Point cursor)
    {
        string text = string.Empty;

        if (_spellPoints.Contains(cursor))
        {
            text = SpellPointsInfo;
        }
        else if (_displayable != SpellBookFilter.Combat && _adventureTab.Contains(cursor))
        {
            text = ViewAdventureSpellsText;
        }
        else if (_displayable != SpellBookFilter.Adventure && _combatTab.Contains(cursor))
        {
            text = ViewCombatSpellsText;
        }
        else if (!IsFirstPage && _previousPage.Contains(cursor))
        {
            text = ViewPreviousPageText;
        }
        else if (!IsLastPage && _nextPage.Contains(cursor))
        {
            text = ViewNextPageText;
        }
        else if (_close.Contains(cursor))
        {
            text = CloseSpellBookText;
        }
        else
        {
            int index = GetSpellIndex(cursor);

            text = index >= 0
                ? string.Format(CultureInfo.InvariantCulture, ViewSpellFormat,
                    new Spell(_spells[StartIndex + index]).GetName())
                : _displayable == SpellBookFilter.All
                    ? ViewSpellsText
                    : string.Empty;
        }

        // The reference calls the callback only when it has something to say.
        if (text.Length > 0)
        {
            StatusText = text;
        }
    }

    /// <summary>Rebuilds the list of the casting book for its current filter, after a tab switch.</summary>
    private void RefreshCastList()
    {
        _spells.Clear();
        _spells.AddRange(SpellBook.SetFilter(_owner.MagicBook, _owner.BagArtifacts, _book!.Filter));
        _book.ClampStartIndex(_spells.Count);
        Refresh();
    }

    /// <summary>Rebuilds the list of the editing book — always both tabs, <c>spell_book.cpp:470</c>.</summary>
    private void RefreshEditList()
    {
        _spells.Clear();
        _spells.AddRange(SpellBook.SetFilter(_owner.MagicBook, _owner.BagArtifacts, SpellBookFilter.All));
        Refresh();
    }

    /// <summary>Redraws the composed book, the <c>spellBookRedrawLists()</c> call of the message loop.</summary>
    private void Refresh()
    {
        _composed = Compose();
    }

    /// <summary>Composes the book for the current page.</summary>
    /// <returns>The shadowed book.</returns>
    private Sprite Compose()
    {
        return _renderer.RenderPages(_spells, _coords, StartIndex, _pos.Position, _owner.SpellPoints,
            _displayable);
    }

    /// <summary>The index of the spell under the cursor, <c>GetRectIndex( coords, cursor )</c>.</summary>
    /// <param name="cursor">The cursor position.</param>
    /// <returns>The index within the screen, or <c>-1</c>.</returns>
    private int GetSpellIndex(Point cursor)
    {
        for (int i = 0; i < _coords.Count; ++i)
        {
            if (_coords[i].Contains(cursor))
            {
                return i;
            }
        }

        return -1;
    }
}
