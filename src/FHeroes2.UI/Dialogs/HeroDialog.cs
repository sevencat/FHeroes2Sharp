using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the hero screen asks its owner to do, the <c>return</c> values of the reference's event loop
/// (<c>heroes_dialog.cpp:459-831</c>).
/// </summary>
public enum HeroDialogResult
{
    /// <summary>The dialog stays open.</summary>
    None,

    /// <summary>The player closed the screen, <c>Dialog::CANCEL</c>.</summary>
    Cancel,

    /// <summary>The player asked for the previous hero, <c>Dialog::PREV</c>.</summary>
    Previous,

    /// <summary>The player asked for the next hero, <c>Dialog::NEXT</c>.</summary>
    Next,
}

/// <summary>
/// The hot keys of one frame of the hero screen, the <c>Game::HotKeyPressEvent()</c> queries of the reference.
/// </summary>
/// <remarks>
/// The reference reads the global key state several times per frame; the port takes one answer per frame, because a
/// screen maps its own keyboard onto these keys. Which key is which belongs to the screen, not to the dialog.
/// </remarks>
/// <param name="CloseWindow">Whether the close key was pressed, <c>Event::CLOSE_WINDOW</c>.</param>
/// <param name="PreviousHero">Whether the previous hero key was pressed, <c>Event::HEROES_PREV</c>.</param>
/// <param name="NextHero">Whether the next hero key was pressed, <c>Event::HEROES_NEXT</c>.</param>
/// <param name="Dismiss">Whether the dismiss key was pressed, <c>Event::HEROES_DISMISS</c>.</param>
public readonly record struct HeroDialogHotKeys(
    bool CloseWindow = false,
    bool PreviousHero = false,
    bool NextHero = false,
    bool Dismiss = false);

/// <summary>
/// What one frame of <see cref="HeroDialog.ProcessEvents"/> asks its owner to do besides closing the screen.
/// </summary>
/// <remarks>
/// The reference opens these windows itself and blocks in them. The port never blocks, so every popup, the quick
/// information window, the dismissal question and the unit information window of the army bar are handed back to the
/// caller, which owns the scene and its dialogs.
/// </remarks>
public sealed class HeroDialogRequests
{
    /// <summary>The help popup a click asked for, <c>showStandardTextMessage()</c>.</summary>
    public DialogPopup? Popup { get; set; }

    /// <summary>Whether a right click on the portrait asked for the quick information window.</summary>
    public bool ShowQuickInfo { get; set; }

    /// <summary>Whether the dismiss button asked for the confirmation of the dismissal.</summary>
    public bool Dismiss { get; set; }

    /// <summary>
    /// What the army bar asked for: a split question, the unit information window or, in the editor, a monster
    /// chooser (<c>army_bar.cpp</c>).
    /// </summary>
    public ArmyBarEvent? Army { get; set; }

    /// <summary>Whether the last frame asked for nothing at all.</summary>
    public bool IsEmpty => Popup is null && !ShowQuickInfo && !Dismiss && Army is null;

    /// <summary>Drops everything a previous frame asked for.</summary>
    internal void Clear()
    {
        Popup = null;
        ShowQuickInfo = false;
        Dismiss = false;
        Army = null;
    }
}

/// <summary>
/// The hero screen, <c>Heroes::OpenDialog()</c> (<c>heroes_dialog.cpp:60-831</c>): portrait, title, the four primary
/// skills, the morale and luck icons, the army formation, experience and spell points, the flag crest, the army, the
/// secondary skills, the artifact bag, the status bar and the four buttons.
/// </summary>
/// <remarks>
/// <para>
/// <b>One frame at a time.</b> The reference owns its loop, draws the static artwork once and then repaints what the
/// input touched. The port separates the two: <see cref="Draw"/> paints the whole dialog into a frame buffer, and
/// <see cref="ProcessEvents"/> answers one frame of input. A screen calls <see cref="Draw"/> every time it wants a
/// fresh picture and <see cref="ProcessEvents"/> once per frame, then follows <see cref="Result"/> and
/// <see cref="Requests"/>. Repainting everything is what makes the status message and the button press looks correct
/// without the reference's dirty-rectangle bookkeeping.
/// </para>
/// <para>
/// <b>Editor.</b> Only the game path is ported. The reference's editor branches - the race panel with its
/// <c>ICN::NGEXTRA</c> picker, the patrol button, the default-value states of the bars and the dismissal prohibition
/// that only the editor sets - belong to the editor pass, which the project runs after every core directory. The
/// <c>isEditor</c> coordinates are already in <see cref="HeroDialogLayout"/> for it.
/// </para>
/// <para>
/// <b>What is not ported</b> in this pass, because the widgets it needs are not ported either: the artifact bar takes
/// no input yet (<c>ArtifactsBar</c> documents the same), so a click on the bag does nothing here; the skill bars take
/// their messages from this class instead of from themselves (they have no input either), which is why the two
/// <c>View ... Info</c> messages and the two skill popups are built here.
/// </para>
/// </remarks>
public sealed class HeroDialog
{
    /// <summary>The title of a named hero, <c>heroes_dialog.cpp:144</c>.</summary>
    public const string TitleFormat = "{name} the {race} (Level {level})";

    /// <summary>The title of a random hero whose race is random too, <c>heroes_dialog.cpp:158</c>.</summary>
    public const string RandomHeroTitleFormat = "Random hero (Level {level})";

    /// <summary>The title of a random hero with a set race, <c>heroes_dialog.cpp:161</c>.</summary>
    public const string RandomRaceHeroTitleFormat = "Random {race} hero (Level {level})";

    /// <summary>The status bar's own message, <c>heroes_dialog.cpp:755</c>.</summary>
    public const string DefaultMessage = "Hero Screen";

    private readonly Hero _hero;
    private readonly HeroDialogArtwork _artwork;
    private readonly IGlyphSource _font;
    private readonly IGlyphSource _smallWhite;
    private readonly IArmyBarSprites _armySprites;
    private readonly ArmyBar _armyBar;
    private readonly PrimarySkillsBar _primarySkills;
    private readonly SecondarySkillsBar _secondarySkills;
    private readonly ArtifactsBar _artifactsBar;
    private readonly ButtonBase _previousHeroButton;
    private readonly ButtonBase _nextHeroButton;
    private readonly ButtonSprite _dismissButton;
    private readonly ButtonSprite _exitButton;
    private readonly TimedEventValidator _previousHeroTiming = new();
    private readonly TimedEventValidator _nextHeroTiming = new();
    private readonly bool _inCastle;
    private readonly bool _dismissProhibited;

    private MoraleIndicator? _morale;
    private LuckIndicator? _luck;
    private ExperienceIndicator? _experience;
    private SpellPointsIndicator? _spellPoints;
    private string _message = string.Empty;

    /// <summary>
    /// Creates the screen of one hero.
    /// </summary>
    /// <param name="hero">The hero to show.</param>
    /// <param name="artwork">The sprites, see <see cref="HeroDialogArtwork.Load"/>.</param>
    /// <param name="armySprites">The creature artwork of the army bar.</param>
    /// <param name="artifactSprites">The artifact artwork of the bag.</param>
    /// <param name="skillSprites">The skill artwork of the two skill bars.</param>
    /// <param name="font">The normal white font of the title and the status bar.</param>
    /// <param name="smallWhite">The small white font of the morale and luck numbers.</param>
    /// <param name="layout">The geometry; the offset defaults to the top-left corner of the frame.</param>
    /// <param name="canSwitchHeroes">
    /// Whether the previous and next buttons work, <c>!disableSwitch</c> and at least two heroes of the
    /// <c>GetKingdom().GetHeroes().size()</c> test of <c>heroes_dialog.cpp:408</c>.
    /// </param>
    /// <param name="canDismiss">Whether the dismissal is allowed at all, <c>!disableDismiss</c>.</param>
    /// <param name="readOnly">Whether the screen is a read-only view, <c>readonly</c>.</param>
    /// <param name="inCastle">Whether the hero visits a castle, <c>inCastle()</c>; it forbids the dismissal.</param>
    /// <param name="dismissProhibited">Whether the scenario forbids the dismissal, <c>Modes( NOTDISMISS )</c>.</param>
    /// <param name="necromancyBonus">
    /// The extra levels necromancy gives the hero's army, used by the secondary skill bar's level names
    /// (<c>Skill::Level::StringWithBonus()</c>).
    /// </param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public HeroDialog(
        Hero hero,
        HeroDialogArtwork artwork,
        IArmyBarSprites armySprites,
        IArtifactsBarSprites artifactSprites,
        ISkillBarSprites skillSprites,
        IGlyphSource font,
        IGlyphSource smallWhite,
        HeroDialogLayout? layout = null,
        bool canSwitchHeroes = true,
        bool canDismiss = true,
        bool readOnly = false,
        bool inCastle = false,
        bool dismissProhibited = false,
        uint necromancyBonus = 0)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(armySprites);
        ArgumentNullException.ThrowIfNull(artifactSprites);
        ArgumentNullException.ThrowIfNull(skillSprites);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(smallWhite);

        _hero = hero;
        _artwork = artwork;
        _font = font;
        _smallWhite = smallWhite;
        _armySprites = armySprites;
        Layout = layout ?? new HeroDialogLayout();
        Requests = new HeroDialogRequests();

        // The primary skill bar takes the hero, so it reads the four values itself (heroes_dialog.cpp:174).
        _primarySkills = new PrimarySkillsBar(hero, useSmallSize: false, skillSprites);
        _primarySkills.SetTableSize(HeroDialogLayout.PrimarySkillsTableSize);
        _primarySkills.SetInBetweenItemsOffset(HeroDialogLayout.PrimarySkillsBetweenOffset);
        _primarySkills.SetRenderingOffset(Layout.PrimarySkillsOffset);

        // The army bar is the full size one: it draws the ICN::STRIP frames, so it needs both before its first redraw.
        _armyBar = new ArmyBar(hero.Army, miniSprites: false, readOnly: readOnly, canChange: false, saveLastTroop: true);
        _armyBar.SetStripBackground(artwork.ArmySlot, artwork.ArmyCursor);
        _armyBar.SetTableSize(HeroDialogLayout.ArmyTableSize);
        _armyBar.SetInBetweenItemsOffset(HeroDialogLayout.ArmyBetweenOffset);
        _armyBar.SetRenderingOffset(Layout.ArmyOffset);

        _secondarySkills = new SecondarySkillsBar(hero, skillSprites, mini: false, necromancyBonus);
        _secondarySkills.SetTableSize(HeroDialogLayout.SecondarySkillsTableSize);
        _secondarySkills.SetInBetweenItemsOffset(HeroDialogLayout.SecondarySkillsBetweenOffset);
        _secondarySkills.SetRenderingOffset(Layout.SecondarySkillsOffset);

        _artifactsBar = new ArtifactsBar(
            hero, mini: false, readOnly: readOnly, canChange: false, allowOpeningMagicBook: true, artifactSprites);
        _artifactsBar.SetTableSize(HeroDialogLayout.ArtifactsTableSize);
        _artifactsBar.SetInBetweenItemsOffset(HeroDialogLayout.ArtifactsBetweenOffset);
        _artifactsBar.SetRenderingOffset(Layout.ArtifactsOffset);

        // The reference disables the two arrow buttons for a single hero and for a disabled switch
        // (heroes_dialog.cpp:408-411).
        _previousHeroButton = new ButtonSprite(artwork.PreviousHeroReleased, artwork.PreviousHeroPressed);
        _previousHeroButton.SetPosition(Layout.PreviousHeroOffset.X, Layout.PreviousHeroOffset.Y);

        _nextHeroButton = new ButtonSprite(artwork.NextHeroReleased, artwork.NextHeroPressed);
        _nextHeroButton.SetPosition(Layout.NextHeroOffset.X, Layout.NextHeroOffset.Y);

        if (!canSwitchHeroes)
        {
            _previousHeroButton.Disable();
            _nextHeroButton.Disable();
        }

        // The two vertical buttons are anchored on their middle, so the reference lifts them by half their height
        // (heroes_dialog.cpp:372, :404).
        _dismissButton = new ButtonSprite(artwork.DismissReleased, artwork.DismissPressed, artwork.DismissDisabled);
        _dismissButton.SetPosition(
            Layout.DismissOffset.X,
            Layout.DismissOffset.Y - (artwork.DismissReleased.Height / 2));

        _inCastle = inCastle;
        _dismissProhibited = dismissProhibited;

        if (inCastle || !canDismiss || readOnly || dismissProhibited)
        {
            _dismissButton.Disable();
        }

        _exitButton = new ButtonSprite(artwork.ExitReleased, artwork.ExitPressed);
        _exitButton.SetPosition(Layout.ExitOffset.X, Layout.ExitOffset.Y - (artwork.ExitReleased.Height / 2));
    }

    /// <summary>The hero the screen shows, <c>_hero</c> of the reference.</summary>
    public Hero Hero => _hero;

    /// <summary>The geometry, see <see cref="HeroDialogLayout"/>.</summary>
    public HeroDialogLayout Layout { get; }

    /// <summary>What the last <see cref="ProcessEvents"/> call asked for.</summary>
    public HeroDialogRequests Requests { get; }

    /// <summary>What the last <see cref="ProcessEvents"/> call decided.</summary>
    public HeroDialogResult Result { get; private set; }

    /// <summary>The status bar message of the last frame.</summary>
    public string Message => _message;

    /// <summary>The exit button's rectangle, for a screen that sets the mouse cursor.</summary>
    public Rect ExitArea => _exitButton.Area;

    /// <summary>The dismiss button's rectangle.</summary>
    public Rect DismissArea => _dismissButton.Area;

    /// <summary>The previous hero button's rectangle.</summary>
    public Rect PreviousHeroArea => _previousHeroButton.Area;

    /// <summary>The next hero button's rectangle.</summary>
    public Rect NextHeroArea => _nextHeroButton.Area;

    /// <summary>The spread formation icon's rectangle.</summary>
    public Rect SpreadFormationArea => new(
        Layout.SpreadFormationOffset.X,
        Layout.SpreadFormationOffset.Y,
        _artwork.SpreadFormation.Width,
        _artwork.SpreadFormation.Height);

    /// <summary>The grouped formation icon's rectangle.</summary>
    public Rect GroupedFormationArea => new(
        Layout.GroupedFormationOffset.X,
        Layout.GroupedFormationOffset.Y,
        _artwork.GroupedFormation.Width,
        _artwork.GroupedFormation.Height);

    /// <summary>
    /// The title of the hero, the lambda of <c>heroes_dialog.cpp:136-168</c>.
    /// </summary>
    /// <remarks>
    /// An empty name is what the editor stores for a hero that will be generated on the first turn, so the title then
    /// either names the random race or stays silent about the race at all. Outside the editor a hero always has a name,
    /// but the branch is kept because the map loader can produce the same state.
    /// </remarks>
    /// <param name="heroName">The name of the hero.</param>
    /// <param name="race">The race of the hero.</param>
    /// <param name="level">The level of the hero.</param>
    /// <returns>The title line.</returns>
    public static string GetTitle(string heroName, MapRace race, int level)
    {
        if (!string.IsNullOrEmpty(heroName))
        {
            return TitleFormat
                .Replace("{name}", heroName)
                .Replace("{race}", MapRaces.Name(race))
                .Replace("{level}", level.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        string format = race == MapRace.Random ? RandomHeroTitleFormat : RandomRaceHeroTitleFormat;

        return format
            .Replace("{race}", MapRaces.Name(race))
            .Replace("{level}", level.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Paints the whole dialog, <c>heroes_dialog.cpp:96-420</c>.
    /// </summary>
    /// <param name="target">The frame to paint into; it stays the frame the indicators restore from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Point origin = Layout.Offset;

        ImageOps.BlitSprite(target, origin, _artwork.Background);
        ImageOps.BlitSprite(target, origin, _artwork.BackgroundExtension);

        // Portrait, then the title (heroes_dialog.cpp:104-170).
        ImageOps.BlitSprite(target, Layout.PortraitArea.Position, _artwork.Portrait);
        DrawTitle(target);

        _primarySkills.Redraw(target);

        // The indicators need the finished background under them: their restorer saves the pixels they cover, so they
        // are built on the first frame, after the two background sprites went down.
        EnsureIndicators(target);
        _morale!.Redraw(target, _artwork.Indicators);
        _luck!.Redraw(target, _artwork.Indicators);

        DrawFormation(target);

        _experience!.Redraw(target, _artwork.Indicators);
        _spellPoints!.Redraw(target, _artwork.Indicators);

        ImageOps.BlitSprite(target, Layout.CrestArea.Position, _artwork.Crest);

        _armyBar.Redraw(target, _armySprites);
        _secondarySkills.Redraw(target);

        DrawStatusBar(target);

        _artifactsBar.Redraw(target);

        // The dismiss button casts the shorter shadow of the two vertical buttons (heroes_dialog.cpp:379).
        if (_dismissButton.IsVisible)
        {
            ImageOps.AddGradientShadow(
                _artwork.DismissReleased,
                target,
                new Point(_dismissButton.OffsetX, _dismissButton.OffsetY),
                new Point(-3, 5));
        }

        _dismissButton.Draw(target);
        _previousHeroButton.Draw(target);
        _nextHeroButton.Draw(target);
        _exitButton.Draw(target);
    }

    /// <summary>
    /// Handles one frame of input, the <c>while ( le.HandleEvents() )</c> body of the reference
    /// (<c>heroes_dialog.cpp:459-831</c>).
    /// </summary>
    /// <remarks>
    /// The status message and the pressed looks are worked out here but painted by <see cref="Draw"/>, so the caller
    /// is expected to repaint after a frame that reports <see langword="true"/>.
    /// </remarks>
    /// <param name="events">The frame's input.</param>
    /// <param name="hotKeys">The frame's hot keys.</param>
    /// <returns>Whether the picture has to be repainted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, HeroDialogHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        Requests.Clear();
        Result = HeroDialogResult.None;
        _message = string.Empty;

        // Exit (heroes_dialog.cpp:459-466). A click closes the screen even if the button was not drawn yet.
        if (events.MouseClickLeft(_exitButton.Area) || hotKeys.CloseWindow)
        {
            Result = HeroDialogResult.Cancel;

            return false;
        }

        bool redraw = SetPressed(_exitButton, events.IsMouseLeftButtonPressedAndHeldInArea(_exitButton.Area));

        // Army bar (heroes_dialog.cpp:468-480). The cursor message comes from the bar itself.
        if (events.IsMouseCursorPosInArea(_armyBar.RenderingArea))
        {
            ArmyBarEvent armyEvent = _armyBar.ProcessEvents(events);

            if (armyEvent.Handled)
            {
                _artifactsBar.ResetSelected();
                Requests.Army = armyEvent;
                redraw = true;
            }

            if (_armyBar.Message.Length > 0)
            {
                _message = _armyBar.Message;
            }
        }

        // Dismiss (heroes_dialog.cpp:513-563). The reference asks the dismissal question here and returns DISMISS when
        // the answer is yes; the port hands the question to its owner.
        if (_dismissButton.IsVisible && _dismissButton.IsEnabled)
        {
            bool pressed = events.IsMouseLeftButtonPressedAndHeldInArea(_dismissButton.Area) || hotKeys.Dismiss;
            redraw |= SetPressed(_dismissButton, pressed);

            if (events.MouseClickLeft(_dismissButton.Area) || hotKeys.Dismiss)
            {
                Requests.Dismiss = true;
            }
        }

        // Previous and next hero (heroes_dialog.cpp:565-605). A held button repeats after the reference's delay.
        if (_previousHeroButton.IsEnabled)
        {
            redraw |= SetPressed(_previousHeroButton, events.IsMouseLeftButtonPressedAndHeldInArea(_previousHeroButton.Area));

            bool clicked = events.MouseClickLeft(_previousHeroButton.Area) || hotKeys.PreviousHero;
            bool repeated = _previousHeroTiming.IsDelayPassed(_previousHeroButton.IsPressed);

            if (clicked || repeated)
            {
                Result = HeroDialogResult.Previous;

                return redraw;
            }
        }

        if (_nextHeroButton.IsEnabled)
        {
            redraw |= SetPressed(_nextHeroButton, events.IsMouseLeftButtonPressedAndHeldInArea(_nextHeroButton.Area));

            bool clicked = events.MouseClickLeft(_nextHeroButton.Area) || hotKeys.NextHero;
            bool repeated = _nextHeroTiming.IsDelayPassed(_nextHeroButton.IsPressed);

            if (clicked || repeated)
            {
                Result = HeroDialogResult.Next;

                return redraw;
            }
        }

        // Morale, luck, experience and spell points (heroes_dialog.cpp:607-660). The first hit wins, exactly like the
        // else-if chain of the reference.
        if (_morale is { } morale && events.IsMouseCursorPosInArea(morale.Area))
        {
            _message = Morale.GetLabel(morale.Morale);
            ShowIndicatorHelp(morale.QueueEventProcessing(events));
        }
        else if (_luck is { } luck && events.IsMouseCursorPosInArea(luck.Area))
        {
            _message = Luck.GetLabel(luck.Luck);
            ShowIndicatorHelp(luck.QueueEventProcessing(events));
        }
        else if (_experience is { } experience && events.IsMouseCursorPosInArea(experience.Area))
        {
            _message = "View Experience Info";
            ShowIndicatorHelp(experience.QueueEventProcessing(events));
        }
        else if (_spellPoints is { } spellPoints && events.IsMouseCursorPosInArea(spellPoints.Area))
        {
            _message = "View Spell Points Info";
            ShowIndicatorHelp(spellPoints.QueueEventProcessing(events));
        }
        else if (TryProcessFormation(events))
        {
            redraw = true;
        }
        else if (events.IsMouseCursorPosInArea(_secondarySkills.RenderingArea) ||
                 events.IsMouseCursorPosInArea(_primarySkills.RenderingArea))
        {
            ProcessSkillBars(events);
        }
        else if (events.IsMouseRightButtonPressedInArea(Layout.PortraitArea))
        {
            // The reference opens the quick information window of the same hero (heroes_dialog.cpp:748-751).
            Requests.ShowQuickInfo = true;
        }
        else if (events.IsMouseRightButtonPressedInArea(SpreadFormationArea))
        {
            Requests.Popup = new DialogPopup(
                "Spread Formation",
                "'Spread' combat formation spreads the hero's units from the top to the bottom of the battlefield, "
                    + "with at least one empty space between each unit.");
        }
        else if (events.IsMouseRightButtonPressedInArea(GroupedFormationArea))
        {
            Requests.Popup = new DialogPopup(
                "Grouped Formation",
                "'Grouped' combat formation bunches the hero's army together in the center of their side of the "
                    + "battlefield.");
        }

        // Status messages (heroes_dialog.cpp:712-745). A button or a formation icon under the cursor replaces whatever
        // the bars said, so the chain overwrites instead of appending.
        if (events.IsMouseCursorPosInArea(_exitButton.Area))
        {
            _message = "Exit Hero Screen";
        }
        else if (events.IsMouseCursorPosInArea(_dismissButton.Area))
        {
            if (_inCastle)
            {
                _message = "You cannot dismiss a hero in a castle";
            }
            else if (_dismissProhibited)
            {
                _message = DismissProhibitedMessage;
            }
            else if (_dismissButton.IsEnabled)
            {
                _message = DismissMessage;
            }
        }
        else if (_previousHeroButton.IsEnabled && events.IsMouseCursorPosInArea(_previousHeroButton.Area))
        {
            _message = "Show previous hero";
        }
        else if (_nextHeroButton.IsEnabled && events.IsMouseCursorPosInArea(_nextHeroButton.Area))
        {
            _message = "Show next hero";
        }
        else if (events.IsMouseCursorPosInArea(SpreadFormationArea))
        {
            _message = "Set army combat formation to 'Spread'";
        }
        else if (events.IsMouseCursorPosInArea(GroupedFormationArea))
        {
            _message = "Set army combat formation to 'Grouped'";
        }

        // An empty message means the status bar shows its own title (heroes_dialog.cpp:774-780).
        if (_message.Length == 0)
        {
            _message = DefaultMessage;
        }

        return redraw;
    }

    /// <summary>
    /// Runs the screen until the player leaves it, the <c>while ( le.HandleEvents() )</c> loop of
    /// <c>Heroes::OpenDialog()</c> (<c>heroes_dialog.cpp:435-831</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference serves every window the frame asks for inside its loop: the help popup, the quick information
    /// panel, the dismissal question and the army bar's own windows. Those windows are not part of this class - the
    /// port hands them out through <see cref="Requests"/> - so the loop runs while it can answer a frame itself and
    /// returns <see cref="HeroDialogResult.None"/> as soon as the frame needs one of them. The caller serves the
    /// request and calls <see cref="Show"/> again; nothing of the screen's state is lost, the dialog keeps the
    /// pressed buttons, the cursor message and the formation it had.
    /// </para>
    /// <para>
    /// The shortcuts are the reference's defaults: <c>Escape</c> for <c>HotKeyCloseWindow()</c>, the arrows for
    /// <c>DEFAULT_LEFT</c> and <c>DEFAULT_RIGHT</c>. The dismissal key is left to the caller, which knows whether
    /// the scenario allows it.
    /// </para>
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit; it must already contain the scene behind.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// The reason the screen ended, or <see cref="HeroDialogResult.None"/> when the caller has to serve
    /// <see cref="Requests"/> first.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public HeroDialogResult Show(PixelBuffer target, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "HeroDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        while (true)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return HeroDialogResult.Cancel;
            }

            ProcessEvents(eventState, ReadHotKeys(eventState));

            Draw(target);
            engine.Render(target.Bounds);

            if (Result != HeroDialogResult.None)
            {
                return Result;
            }

            if (!Requests.IsEmpty)
            {
                return HeroDialogResult.None;
            }

            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }
    }

    /// <summary>The default shortcuts of the reference, read off the frame.</summary>
    /// <param name="events">Event state of the frame.</param>
    /// <returns>The shortcuts <see cref="ProcessEvents"/> takes.</returns>
    private static HeroDialogHotKeys ReadHotKeys(LocalEvent events) => new(
        CloseWindow: events.IsKeyPressed(Key.Escape),
        PreviousHero: events.IsKeyPressed(Key.Left),
        NextHero: events.IsKeyPressed(Key.Right));

    /// <summary>The dismissal prohibition message, <c>heroes_dialog.cpp:721</c>.</summary>
    private string DismissProhibitedMessage =>
        $"Dismissal of {_hero.Name} the {MapRaces.Name(_hero.Race)} is prohibited by scenario";

    /// <summary>The dismissal question, <c>heroes_dialog.cpp:726</c>.</summary>
    private string DismissMessage => $"Dismiss {_hero.Name} the {MapRaces.Name(_hero.Race)}";

    /// <summary>
    /// The title line, centred on the title region and clipped by it (<c>heroes_dialog.cpp:136-168</c>).
    /// </summary>
    /// <remarks>
    /// A long name is not shortened: the reference lets the region's clip cut it, which is why the centring may start
    /// left of the region.
    /// </remarks>
    private void DrawTitle(PixelBuffer target)
    {
        string title = GetTitle(_hero.Name, _hero.Race, _hero.Level);
        int width = GlyphText.Measure(_font, title);

        Point position = new(
            Layout.TitleArea.X + ((Layout.TitleArea.Width - width) / 2),
            Layout.TitleArea.Y + 2);

        GlyphText.DrawLine(_font, target, position, title, Layout.TitleArea);
    }

    /// <summary>
    /// Builds the four indicators on the first frame, once the background under them is down
    /// (<c>heroes_dialog.cpp:207-262</c>).
    /// </summary>
    private void EnsureIndicators(PixelBuffer target)
    {
        if (_morale is not null)
        {
            return;
        }

        _morale = new MoraleIndicator(_hero, _smallWhite, target);
        _morale.SetPos(Layout.MoraleOffset(isEditor: false));

        _luck = new LuckIndicator(_hero, _smallWhite, target);
        _luck.SetPos(Layout.LuckOffset(isEditor: false));

        _experience = new ExperienceIndicator(_hero, _smallWhite, target);
        _experience.SetPos(Layout.ExperienceOffset(isEditor: false));

        _spellPoints = new SpellPointsIndicator(_hero, _smallWhite, target);
        _spellPoints.SetPos(Layout.SpellPointsOffset(isEditor: false));
    }

    /// <summary>
    /// The two formation icons and the frame around the chosen one (<c>heroes_dialog.cpp:213-235</c>).
    /// </summary>
    private void DrawFormation(PixelBuffer target)
    {
        ImageOps.BlitSprite(target, Layout.SpreadFormationOffset, _artwork.SpreadFormation);
        ImageOps.BlitSprite(target, Layout.GroupedFormationOffset, _artwork.GroupedFormation);

        // The cursor is drawn one pixel up and left of the icon it frames.
        Point icon = _hero.Army.IsSpreadFormation ? Layout.SpreadFormationOffset : Layout.GroupedFormationOffset;

        ImageOps.BlitSprite(target, new Point(icon.X - 1, icon.Y - 1), _artwork.FormationCursor);
    }

    /// <summary>
    /// The status bar plate and its message, <c>heroes_dialog.cpp:329-337</c> and <c>StatusBar::updateMessage()</c>
    /// (<c>statusbar.cpp:40-68</c>).
    /// </summary>
    private void DrawStatusBar(PixelBuffer target)
    {
        ImageOps.BlitSprite(target, Layout.StatusBarOffset, _artwork.StatusBarPlate);

        // The plate carries extra art on both sides, so the message keeps sixteen pixels off each edge.
        int left = Layout.StatusBarOffset.X + HeroDialogLayout.StatusBarInset;
        int width = _artwork.StatusBarPlate.Width - (HeroDialogLayout.StatusBarInset * 2);

        string text = FitToWidth(_font, _message, width);
        int textWidth = GlyphText.Measure(_font, text);

        Rect messageRoi = new(left + ((width - textWidth) / 2), Layout.StatusBarOffset.Y + 1, textWidth, _font.LineHeight);

        GlyphText.DrawLine(_font, target, new Point(messageRoi.X, messageRoi.Y + 2), text, messageRoi);
    }

    /// <summary>
    /// The formation icons: a click switches the army's formation (<c>heroes_dialog.cpp:662-704</c>).
    /// </summary>
    /// <returns>Whether something changed, which is the reference's <c>needRedraw</c>.</returns>
    private bool TryProcessFormation(LocalEvent events)
    {
        if (_armyBar.IsReadOnly)
        {
            return false;
        }

        if (!_hero.Army.IsSpreadFormation && events.MouseClickLeft(SpreadFormationArea))
        {
            _hero.Army.IsSpreadFormation = true;
            _armyBar.ResetSelected();

            return true;
        }

        if (_hero.Army.IsSpreadFormation && events.MouseClickLeft(GroupedFormationArea))
        {
            _hero.Army.IsSpreadFormation = false;
            _armyBar.ResetSelected();

            return true;
        }

        return false;
    }

    /// <summary>
    /// The two skill bars (<c>heroes_dialog.cpp:706-745</c>). The reference asks the bars themselves; the port's bars
    /// have no input (see <c>SkillBars.cs</c>), so the slot under the cursor is resolved here.
    /// </summary>
    private void ProcessSkillBars(LocalEvent events)
    {
        if (events.IsMouseCursorPosInArea(_secondarySkills.RenderingArea))
        {
            int index = _secondarySkills.IndexAt(events.CursorPosition);
            IReadOnlyList<SecondarySkillLevel> skills = _hero.SecondarySkills.Skills;

            if (index < 0 || index >= skills.Count || !skills[index].IsValid)
            {
                return;
            }

            SecondarySkillLevel skill = skills[index];
            _message = $"View {SecondarySkills.GetName(skill.Skill)} Info";

            if (events.MouseClickLeft(_secondarySkills.RenderingArea) ||
                events.IsMouseRightButtonPressedInArea(_secondarySkills.RenderingArea))
            {
                Requests.Popup = new DialogPopup(
                    SecondarySkills.GetFullName(skill.Skill, skill.Level),
                    SecondarySkills.GetDescription(skill.Skill, skill.Level));
            }

            return;
        }

        int primaryIndex = _primarySkills.IndexAt(events.CursorPosition);

        // The bar's slots are the four primary skills in their enum order, which starts at Attack
        // (SkillBar::PrimarySkillBar::GetSkills()).
        if (primaryIndex < 0 || primaryIndex > (int)PrimarySkill.Knowledge - 1)
        {
            return;
        }

        PrimarySkill primary = (PrimarySkill)(primaryIndex + 1);
        _message = $"View {PrimarySkillNames.Get(primary)} Skill Info";

        if (events.MouseClickLeft(_primarySkills.RenderingArea) ||
            events.IsMouseRightButtonPressedInArea(_primarySkills.RenderingArea))
        {
            Requests.Popup = new DialogPopup(PrimarySkillNames.Get(primary), PrimarySkillNames.GetDescription(primary));
        }
    }

    /// <summary>Puts the help popup of an indicator into the requests.</summary>
    private void ShowIndicatorHelp(HeroIndicatorHelp? help)
    {
        if (help is { } value)
        {
            Requests.Popup = new DialogPopup(value.Title, value.Body);
        }
    }

    /// <summary>
    /// A formation change drops the selected army slot, <c>selectArmy.ResetSelected()</c>
    /// (<c>heroes_dialog.cpp:684</c>).
    /// </summary>
    private void ResetArmyCursor() => _armyBar.ResetSelected();

    /// <summary>Applies a pressed look and reports whether it was a change.</summary>
    private static bool SetPressed(ButtonBase button, bool pressed)
    {
        if (pressed == button.IsPressed)
        {
            return false;
        }

        if (pressed)
        {
            button.Press();
        }
        else
        {
            button.Release();
        }

        return true;
    }

    /// <summary>Cuts a line down to the number of pixels a plate can hold.</summary>
    private static string FitToWidth(IGlyphSource font, string text, int width)
    {
        if (GlyphText.Measure(font, text) <= width)
        {
            return text;
        }

        int end = text.Length - 1;

        while (end > 0 && GlyphText.Measure(font, text[..end]) > width)
        {
            --end;
        }

        return text[..end];
    }
}
