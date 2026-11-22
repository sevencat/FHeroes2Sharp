using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The kingdom overview window, <c>Kingdom::openOverviewDialog()</c>
/// (<c>kingdom_overview.cpp:763-959</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference runs a modal loop here. The port keeps the same layout, the same drawing order and the same order of
/// input handling, but <see cref="ProcessEvents"/> returns after one frame and <see cref="Draw"/> paints the frame, so
/// the scene keeps its own loop and can fade, play sounds or open dialogs between two frames.
/// </para>
/// <para>
/// Everything the reference opens from inside the click (hero dialog, town dialog, quick infos, income and lighthouse
/// dialogs, troop interactions) lands in <see cref="Requests"/> for the scene to open.
/// </para>
/// </remarks>
public sealed class KingdomOverviewWindow
{
    /// <summary>The width of the reference's dialog, <c>display.DEFAULT_WIDTH</c>.</summary>
    public const int Width = 640;

    /// <summary>The height of the reference's dialog, <c>display.DEFAULT_HEIGHT</c>.</summary>
    public const int Height = 480;

    private readonly StandardWindow _window;
    private readonly Point _offset;
    private readonly Kingdom _kingdom;
    private readonly World _world;
    private readonly KingdomOverviewArtwork _artwork;
    private readonly IGlyphSource _smallWhite;
    private readonly KingdomHeroesList _heroesList;
    private readonly KingdomCastlesList _castlesList;
    private readonly ButtonSprite _heroesButton;
    private readonly ButtonSprite _townsButton;
    private readonly ButtonSprite _exitButton;
    private readonly Rect _incomeArea;
    private readonly Rect _goldPerDayArea;
    private readonly Rect _lighthouseArea;
    private bool _showCastles;
    private bool _redraw = true;

    /// <summary>Builds the window, the setup half of <c>Kingdom::openOverviewDialog()</c> (<c>:763-840</c>).</summary>
    /// <param name="kingdom">The kingdom whose heroes, towns and funds are shown.</param>
    /// <param name="world">The world, asked for mines, lighthouses and guests.</param>
    /// <param name="artwork">The artwork of the window.</param>
    /// <param name="smallWhite">The font of every caption of the window.</param>
    /// <param name="smallYellow">The font of the weekly growth of a dwelling.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public KingdomOverviewWindow(Kingdom kingdom, World world, KingdomOverviewArtwork artwork,
        IGlyphSource smallWhite, IGlyphSource smallYellow)
    {
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallWhite);
        ArgumentNullException.ThrowIfNull(smallYellow);

        _kingdom = kingdom;
        _world = world;
        _artwork = artwork;
        _smallWhite = smallWhite;

        _window = StandardWindow.CreateCentered(new Size(Width, Height), false);
        _offset = _window.ActiveArea.Position;

        _heroesList = new KingdomHeroesList(artwork, smallWhite, world, _offset, kingdom.Heroes);
        _castlesList = new KingdomCastlesList(artwork, smallWhite, smallYellow, world, _offset, kingdom.Castles);

        _castlesList.SetTopVisibleItem(kingdom.TopCastleInKingdomView);
        _heroesList.SetTopVisibleItem(kingdom.TopHeroInKingdomView);

        // The three buttons sit in the lower right corner, 45 and 48 pixels apart (<c>:792-800</c>).
        _heroesButton = new ButtonSprite(
            artwork.HeroesButtonReleased, artwork.HeroesButtonPressed, null, _offset.X + 540, _offset.Y + 361);
        _townsButton = new ButtonSprite(
            artwork.TownsButtonReleased, artwork.TownsButtonPressed, null, _offset.X + 540, _offset.Y + 406);
        _exitButton = new ButtonSprite(
            artwork.ExitButtonReleased, artwork.ExitButtonPressed, null, _offset.X + 540, _offset.Y + 454);

        _incomeArea = new Rect(_offset.X + 1, _offset.Y + 360, 535, 60);
        _goldPerDayArea = new Rect(_offset.X + 124, _offset.Y + 459, 289, 16);

        Sprite lighthouse = artwork.Lighthouse;
        _lighthouseArea = new Rect(
            _offset.X + 100 - lighthouse.Width, _offset.Y + 459, lighthouse.Width + 10, lighthouse.Height);

        // The reference opens the town list when the caller asked for a town: KINGDOM_OVERVIEW_CASTLE_SELECTION.
        _showCastles = (kingdom.Modes & KingdomModes.KingdomOverviewCastleSelection) != 0;
    }

    /// <summary>What the window asked its owner to open.</summary>
    public KingdomOverviewRequests Requests { get; } = new();

    /// <summary>Whether the exit button or the window hotkey asked the window to close.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Whether the town list is shown, <c>Modes( KINGDOM_OVERVIEW_CASTLE_SELECTION )</c>.</summary>
    public bool ShowCastles => _showCastles;

    /// <summary>The area the dialog's contents are laid out in, <c>background.activeArea()</c>.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>
    /// What the adventure map has to repaint after the window closes, <c>worldMapRedrawMask</c> (<c>:914-915</c>).
    /// </summary>
    /// <remarks>
    /// The reference ORs the interface's current mask with <c>REDRAW_HEROES</c> when the hero list was rebuilt — a
    /// hero hired or dismissed changes the icons panel. The window only reports the bit it owns; the owner adds
    /// <c>getRedrawMask()</c> and does the <c>ResetFocus()</c> / <c>RedrawFocus()</c> calls of <c>:949-958</c>.
    /// </remarks>
    public Redraw WorldMapRedrawMask { get; private set; }

    /// <summary>Paints the whole window, <c>:783-832</c> and the redraw half of the loop.</summary>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _window.Draw(output);
        ImageOps.BlitSprite(output, _offset, _artwork.Background);

        RedrawIncomeInfo(output);
        RedrawFundsInfo(output);

        if (_showCastles)
        {
            _castlesList.Redraw(output);
        }
        else
        {
            _heroesList.Redraw(output);
        }

        if (_showCastles)
        {
            _townsButton.DrawOnPress(output);
            _heroesButton.DrawOnRelease(output);
        }
        else
        {
            _heroesButton.DrawOnPress(output);
            _townsButton.DrawOnRelease(output);
        }

        _exitButton.Draw(output);

        _redraw = false;
    }

    /// <summary>
    /// Consumes one frame, the body of the loop of <c>:847-940</c>.
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <param name="hotKeys">The hot keys pressed this frame.</param>
    /// <returns><see langword="true"/> when the window has to be painted again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, KingdomOverviewHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        Requests.Clear();

        _exitButton.UpdatePressState(events);

        // Right clicks only ever ask for a help dialog, <c>:850-864</c>.
        if (events.IsMouseRightButtonPressedInArea(_heroesButton.Area))
        {
            Requests.Popup = new DialogPopup("Heroes", "View Heroes.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_townsButton.Area))
        {
            Requests.Popup = new DialogPopup("Towns/Castles", "View Towns and Castles.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_exitButton.Area))
        {
            Requests.Popup = new DialogPopup("Exit", "Exit this menu.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_incomeArea) ||
                 events.IsMouseRightButtonPressedInArea(_goldPerDayArea))
        {
            Requests.Income = true;
        }
        else if (events.IsMouseRightButtonPressedInArea(_lighthouseArea))
        {
            Requests.Lighthouse = true;
        }

        if (events.MouseClickLeft(_exitButton.Area) || hotKeys.CloseWindow)
        {
            Close();

            return false;
        }

        bool redraw = false;

        // Switching the list, <c>:880-894</c>.
        if (!_heroesButton.IsPressed && events.MouseClickLeft(_heroesButton.Area))
        {
            _showCastles = false;
            _kingdom.ResetModes(KingdomModes.KingdomOverviewCastleSelection);
            redraw = true;
        }
        else if (!_townsButton.IsPressed && events.MouseClickLeft(_townsButton.Area))
        {
            _showCastles = true;
            _kingdom.SetModes(KingdomModes.KingdomOverviewCastleSelection);
            redraw = true;
        }

        if (_showCastles)
        {
            redraw |= _castlesList.QueueEventProcessing(events);
            MergeRequests(_castlesList.Requests);
        }
        else
        {
            redraw |= _heroesList.QueueEventProcessing(events);
            MergeRequests(_heroesList.Requests);
        }

        if (events.MouseClickLeft(_incomeArea) || events.MouseClickLeft(_goldPerDayArea))
        {
            Requests.Income = true;
        }

        if (events.MouseClickLeft(_lighthouseArea))
        {
            Requests.Lighthouse = true;
        }

        if (Requests.NeedFadeIn)
        {
            redraw = true;
        }

        if (!redraw && !_redraw && !(_showCastles ? _castlesList.IsNeedRedraw : _heroesList.IsNeedRedraw))
        {
            return false;
        }

        // A hired or dismissed hero changes the army bars of the towns, <c>:910-919</c>.
        if (_heroesList.Refresh(_kingdom.Heroes))
        {
            WorldMapRedrawMask |= Redraw.Heroes;

            _castlesList.UpdateHeroArmyBars();
        }

        _redraw = true;

        return true;
    }

    /// <summary>
    /// Runs the window until the player leaves it, the <c>while ( le.HandleEvents() )</c> loop of
    /// <c>:847-940</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference opens the hero and town screens, the quick infos, the income window, the lighthouse note and the
    /// troop interactions from inside its loop. Those windows belong to the owner - the port collects them in
    /// <see cref="Requests"/> - so the loop runs while it can answer a frame itself and stops as soon as a frame asks
    /// for one of them. The caller serves the requests and calls <see cref="Show"/> again; the scroll positions, the
    /// active list and the pressed buttons survive, so the window continues where it stopped.
    /// </para>
    /// <para>
    /// The fade-out of the closing path (and the fade-in of <see cref="KingdomOverviewRequests.NeedFadeIn"/>), the
    /// adventure map interface calls of <c>:949-958</c> and the sounds are left to the caller, exactly as documented
    /// on <see cref="Close"/>.
    /// </para>
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit; it must already contain the scene behind.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// <see langword="true"/> when the window closed; <see langword="false"/> when the caller has to serve
    /// <see cref="Requests"/> before the next <see cref="Show"/>, or when the client was asked to exit.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public bool Show(PixelBuffer target, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "KingdomOverviewWindow.Show() needs an event engine: attach LocalEvent.Engine first.");

        while (!IsClosed)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return false;
            }

            bool redraw = ProcessEvents(
                eventState, new KingdomOverviewHotKeys(CloseWindow: eventState.IsKeyPressed(Key.Escape)));

            if (IsClosed)
            {
                Draw(target);
                engine.Render(target.Bounds);

                break;
            }

            if (!Requests.IsEmpty)
            {
                // Drawing first keeps what the player clicked on screen while the caller opens the window.
                Draw(target);
                engine.Render(target.Bounds);

                return false;
            }

            if (redraw)
            {
                Draw(target);
                engine.Render(target.Bounds);
            }

            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return IsClosed;
    }

    /// <summary>
    /// Saves the scroll positions the reference remembers on the kingdom, <c>:942-943</c>.
    /// </summary>
    /// <remarks>
    /// The rest of the reference's exit path belongs to the owner, because it talks to the adventure map interface:
    /// <c>getGameArea().setFastScrollStatus( false )</c>, the fade out (<c>:869-875</c>), and, after the window is
    /// gone, <c>setRedraw( worldMapRedrawMask )</c>, <c>ResetFocus( GetFocusType(), false )</c> and
    /// <c>RedrawFocus()</c> (<c>:949-958</c>) — the last of which is unconditional, because the army of the focused
    /// hero or town may have been changed.
    /// </remarks>
    public void Close()
    {
        _kingdom.TopHeroInKingdomView = _heroesList.TopId;
        _kingdom.TopCastleInKingdomView = _castlesList.TopId;
        IsClosed = true;
    }

    /// <summary>
    /// Paints the seven captured-resource counters of the upper line, <c>RedrawIncomeInfo()</c>
    /// (<c>:687-713</c>).
    /// </summary>
    /// <param name="output">The frame to paint into.</param>
    private void RedrawIncomeInfo(PixelBuffer output)
    {
        Funds income = _kingdom.GetIncome(
            resource => _world.CountCapturedMines(resource, _kingdom.Color),
            IncomeType.Artifacts | IncomeType.HeroSkills | IncomeType.CampaignBonus);
        int offsetY = _offset.Y + 410;

        DrawCentred(CapturedExtInfoString(Resource.Wood, income), _offset.X + 54, offsetY, output);
        DrawCentred(CapturedExtInfoString(Resource.Mercury, income), _offset.X + 146, offsetY, output);
        DrawCentred(CapturedExtInfoString(Resource.Ore, income), _offset.X + 228, offsetY, output);
        DrawCentred(CapturedExtInfoString(Resource.Sulfur, income), _offset.X + 294, offsetY, output);
        DrawCentred(CapturedExtInfoString(Resource.Crystal, income), _offset.X + 360, offsetY, output);
        DrawCentred(CapturedExtInfoString(Resource.Gems, income), _offset.X + 428, offsetY, output);
        DrawCentred(CapturedExtInfoString(Resource.Gold, income), _offset.X + 494, offsetY, output);
    }

    /// <summary>
    /// Paints the funds panel, the day and the lighthouse counter, <c>RedrawFundsInfo()</c> (<c>:715-760</c>).
    /// </summary>
    /// <param name="output">The frame to paint into.</param>
    private void RedrawFundsInfo(PixelBuffer output)
    {
        Funds funds = _kingdom.Treasury;
        int offsetY = _offset.Y + 450;

        ImageOps.BlitSprite(
            output, new Point(_offset.X + 4, _offset.Y + 422), _artwork.Background, new Rect(4, 422, 530, 56));

        DrawCentred(funds.Wood.ToString(CultureInfo.InvariantCulture), _offset.X + 56, offsetY, output);
        DrawCentred(funds.Mercury.ToString(CultureInfo.InvariantCulture), _offset.X + 146, offsetY, output);
        DrawCentred(funds.Ore.ToString(CultureInfo.InvariantCulture), _offset.X + 226, offsetY, output);
        DrawCentred(funds.Sulfur.ToString(CultureInfo.InvariantCulture), _offset.X + 294, offsetY, output);
        DrawCentred(funds.Crystal.ToString(CultureInfo.InvariantCulture), _offset.X + 362, offsetY, output);
        DrawCentred(funds.Gems.ToString(CultureInfo.InvariantCulture), _offset.X + 428, offsetY, output);
        DrawCentred(funds.Gold.ToString(CultureInfo.InvariantCulture), _offset.X + 496, offsetY, output);

        offsetY += 14;

        int goldPerDay = _kingdom.GetIncome(
            resource => _world.CountCapturedMines(resource, _kingdom.Color), IncomeType.All).Gold;

        GlyphText.DrawLine(
            _smallWhite, output, new Point(_offset.X + 180, offsetY), "Gold Per Day: " + goldPerDay.ToString(CultureInfo.InvariantCulture));

        GlyphText.DrawLine(
            _smallWhite,
            output,
            new Point(_offset.X + 360, offsetY),
            "Day: " + _world.Day.ToString(CultureInfo.InvariantCulture));

        uint lighthouseCount = _world.CountCapturedObject(Mp2ObjectType.Lighthouse, _kingdom.Color);

        GlyphText.DrawLine(
            _smallWhite, output, new Point(_offset.X + 105, offsetY), lighthouseCount.ToString(CultureInfo.InvariantCulture));

        Sprite lighthouse = _artwork.Lighthouse;
        ImageOps.BlitSprite(output, new Point(_offset.X + 100 - lighthouse.Width, _offset.Y + 459), lighthouse);
    }

    /// <summary>
    /// Builds "captured (income)" for one resource, <c>CapturedExtInfoString()</c> (<c>:71-96</c>).
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="income">The income the kingdom reports.</param>
    /// <returns>The text.</returns>
    private string CapturedExtInfoString(int resource, Funds income)
    {
        int captured = (int)_world.CountCapturedMines(resource, _kingdom.Color);
        int extra = income.Get(resource);

        return extra == 0
            ? captured.ToString(CultureInfo.InvariantCulture)
            : string.Concat(
                captured.ToString(CultureInfo.InvariantCulture), " (+", extra.ToString(CultureInfo.InvariantCulture), ")");
    }

    private void DrawCentred(string text, int centreX, int y, PixelBuffer output) =>
        GlyphText.DrawLine(_smallWhite, output, new Point(centreX - (GlyphText.Measure(_smallWhite, text) / 2), y), text);

    /// <summary>Copies what a list asked for into the window's own bag.</summary>
    /// <param name="requests">The list's requests.</param>
    private void MergeRequests(KingdomOverviewRequests requests)
    {
        Requests.Army.AddRange(requests.Army);

        if (requests.HeroDialog is { } heroDialog)
        {
            Requests.HeroDialog = heroDialog;
        }

        if (requests.CastleDialog is { } castleDialog)
        {
            Requests.CastleDialog = castleDialog;
        }

        if (requests.HeroInfo is { } heroInfo)
        {
            Requests.HeroInfo = heroInfo;
        }

        if (requests.CastleInfo is { } castleInfo)
        {
            Requests.CastleInfo = castleInfo;
        }

        if (requests.CaptainInfo is { } captainInfo)
        {
            Requests.CaptainInfo = captainInfo;
        }

        if (requests.NeedFadeIn)
        {
            Requests.NeedFadeIn = true;
        }
    }
}

/// <summary>
/// The hot keys of the kingdom overview besides the pointer, <c>Game::HotKeyCloseWindow()</c> (<c>:867</c>).
/// </summary>
/// <remarks>
/// The port has no hot key table yet, so the scene decodes the frame's key press and hands the answer over.
/// </remarks>
/// <param name="CloseWindow"><c>Game::HotKeyCloseWindow()</c>: Escape or Enter.</param>
public readonly record struct KingdomOverviewHotKeys(bool CloseWindow = false);
