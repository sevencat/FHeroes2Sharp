using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The hot keys of one frame of the town screen, the <c>Game::HotKeyPressEvent()</c> queries of
/// <c>castle_dialog.cpp:493-776</c>.
/// </summary>
/// <remarks>
/// 参考每帧多次读全局按键；端口一帧收一份答案，因为「哪个键算哪个」是屏幕的事。
/// </remarks>
/// <param name="CloseWindow">Whether the close key was pressed, <c>Event::CLOSE_WINDOW</c>.</param>
/// <param name="PreviousCastle">Whether the left arrow was pressed, <c>DEFAULT_LEFT</c>.</param>
/// <param name="NextCastle">Whether the right arrow was pressed, <c>DEFAULT_RIGHT</c>.</param>
/// <param name="SwapArmies">Whether the army-swap key was pressed, <c>Event::ARMY_SWAP</c>.</param>
/// <param name="MergeWithHero">Whether the "move the garrison into the hero" key was pressed.</param>
/// <param name="MergeWithGarrison">Whether the "move the hero into the garrison" key was pressed.</param>
/// <param name="Building">The building whose hot key was pressed, or <see cref="BuildingType.Nothing"/>.</param>
public readonly record struct CastleDialogHotKeys(
    bool CloseWindow = false,
    bool PreviousCastle = false,
    bool NextCastle = false,
    bool SwapArmies = false,
    bool MergeWithHero = false,
    bool MergeWithGarrison = false,
    BuildingType Building = BuildingType.Nothing);

/// <summary>
/// The window a click of the town screen asks its owner to open, <c>castle_dialog.cpp:530-772</c>.
/// </summary>
public enum CastleDialogWindow
{
    /// <summary>No window, <c>DoNothing</c>.</summary>
    Nothing,

    /// <summary>The kingdom's income, <c>showKingdomIncome()</c>.</summary>
    Income,

    /// <summary>The visiting hero's screen, <c>openHeroDialog()</c>.</summary>
    Hero,

    /// <summary>The visiting hero's quick information box, <c>Dialog::QuickInfo( hero )</c>.</summary>
    HeroQuickInfo,

    /// <summary>The captain's quick information box, <c>Dialog::QuickInfo( captain )</c>.</summary>
    CaptainQuickInfo,

    /// <summary>The mage guild, <c>mageGuildDialogHandler()</c>.</summary>
    MageGuild,

    /// <summary>A monster dwelling's recruitment window, <c>Dialog::RecruitMonster()</c>.</summary>
    Dwelling,

    /// <summary>A dwelling's right-click information, <c>Dialog::DwellingInfo()</c>.</summary>
    DwellingInfo,

    /// <summary>The thieves' guild, <c>Dialog::ThievesGuild()</c>.</summary>
    ThievesGuild,

    /// <summary>The tavern, <c>_openTavern()</c>.</summary>
    Tavern,

    /// <summary>The well, <c>_openWell()</c>.</summary>
    Well,

    /// <summary>The market place, <c>Dialog::Marketplace()</c>.</summary>
    Marketplace,

    /// <summary>The shipyard, <c>Dialog::BuyBoat()</c> plus <c>BuyBoat()</c>.</summary>
    Shipyard,

    /// <summary>Buying the castle proper, <c>DialogBuyCastle()</c> plus <c>BuyBuilding( BUILD_CASTLE )</c>.</summary>
    BuildCastle,

    /// <summary>The construction window, <c>constructionDialogHandler()</c>.</summary>
    Construction,

    /// <summary>A building's right-click information, <c>showStandardTextMessage()</c>.</summary>
    BuildingInfo,
}

/// <summary>
/// A window the town screen asks its owner to open.
/// </summary>
/// <param name="Window">The window.</param>
/// <param name="Building">
/// The building it is about: the actual dwelling for <see cref="CastleDialogWindow.Dwelling"/> and
/// <see cref="CastleDialogWindow.DwellingInfo"/>, the clicked one otherwise.
/// </param>
public readonly record struct CastleDialogRequest(CastleDialogWindow Window, BuildingType Building = BuildingType.Nothing);

/// <summary>
/// What one frame of <see cref="CastleDialog.ProcessEvents"/> asks its owner to do besides closing the screen.
/// </summary>
/// <remarks>
/// 参考在这些窗口里阻塞自己；端口不阻塞，所以每个子窗口都交回调用方，和 <see cref="HeroDialogRequests"/> 一致。
/// </remarks>
public sealed class CastleDialogRequests
{
    /// <summary>The help popup a right click asked for, <c>showStandardTextMessage()</c>.</summary>
    public DialogPopup? Popup { get; set; }

    /// <summary>The window a click asked for.</summary>
    public CastleDialogRequest? Window { get; set; }

    /// <summary>What an army bar asked for: a split question or the unit information window.</summary>
    public ArmyBarEvent? Army { get; set; }

    /// <summary>Whether the last frame asked for nothing at all.</summary>
    public bool IsEmpty => Popup is null && Window is null && Army is null;

    /// <summary>Drops everything a previous frame asked for.</summary>
    internal void Clear()
    {
        Popup = null;
        Window = null;
        Army = null;
    }
}

/// <summary>
/// The town screen, <c>Castle::OpenDialog()</c> (<c>castle_dialog.cpp:196-859</c>): the buildings, the two army
/// bars, the crest or captain, the hero's portrait, the status bar, the resource panel and the EXIT button.
/// </summary>
/// <remarks>
/// <para>
/// <b>One frame at a time</b>, like <see cref="HeroDialog"/>: <see cref="Draw"/> paints the whole screen into a frame
/// buffer, <see cref="ProcessEvents"/> answers one frame of input, <see cref="Show"/> runs both until the screen ends.
/// The painting itself belongs to <see cref="CastleScreenView"/>, the click handling to
/// <see cref="CastleScreenInput"/>; this class is the loop and the routing of the reference.
/// </para>
/// <para>
/// <b>Routing.</b> 参考用 660 行把「点了哪座建筑」翻译成「开哪个窗口」：魔法塔按 <c>BUILD_MAGEGUILD</c> 掩码、怪物
/// 住所按 <c>GetActualDwelling()</c>、其余按 id 分派。这一层照抄那套分派，但只吐 <see cref="CastleDialogRequest"/>，
/// 由调用方开窗口——它拥有场景与那些窗口。
/// </para>
/// <para>
/// <b>Not ported.</b> The fade-in/fade-out of the whole window (<c>fadeInDisplay</c>), the fast-scroll switch of the
/// adventure map, the <c>ButtonRestorer</c> guards and the sound effects. The hero's fade-in keeps the reference's
/// alpha ramp but the view blends the portrait only at its ends, so a recruited hero appears instead of fading.
/// </para>
/// </remarks>
public sealed class CastleDialog
{
    /// <summary>
    /// How the screen ends, <c>Castle::CastleDialogReturnValue</c> (<c>castle.h:138-149</c>).
    /// </summary>
    /// <remarks>
    /// The four "next" answers exist because the screen owns the arrows that walk through the kingdom's towns: a
    /// closed screen with a direction is not the same as a closed screen, since the caller reopens on another town.
    /// </remarks>
    public enum ReturnValue
    {
        /// <summary>Nothing happened, <c>DoNothing</c> — the screen stays open.</summary>
        DoNothing,

        /// <summary>The screen is closed, <c>Close</c>.</summary>
        Close,

        /// <summary>Close and open the next town's screen, <c>NextCastle</c>.</summary>
        NextCastle,

        /// <summary>Close and open the previous town's screen, <c>PreviousCastle</c>.</summary>
        PreviousCastle,

        /// <summary>Close and open the next town's construction window, <c>NextConstructionWindow</c>.</summary>
        NextConstructionWindow,

        /// <summary>Close and open the previous town's construction window, <c>PreviousConstructionWindow</c>.</summary>
        PreviousConstructionWindow,

        /// <summary>Close and open the next town's mage guild, <c>NextMageGuildWindow</c>.</summary>
        NextMageGuildWindow,

        /// <summary>Close and open the previous town's mage guild, <c>PreviousMageGuildWindow</c>.</summary>
        PreviousMageGuildWindow,
    }

    /// <summary>The top of the hero's fade-in, <c>alphaHero = 255</c>.</summary>
    public const int FullHeroAlpha = 255;

    /// <summary>How far the hero's alpha moves per accepted update, <c>alphaHero += 9</c>.</summary>
    public const int HeroAlphaStep = 9;

    /// <summary>The first frame of the buildings' animation, <c>castleAnimationIndex = 1</c>.</summary>
    public const uint FirstAnimationIndex = 1;

    private readonly CastleScreenView _view;
    private readonly CastleScreenInput _input;
    private readonly Castle _castle;
    private readonly Hero? _hero;
    private readonly World _world;
    private readonly Sprite? _heroPortrait;
    private readonly Sprite? _captainPortrait;
    private readonly FadeBuilding _fade = new();

    private int _heroAlpha;
    private uint _animationIndex = FirstAnimationIndex;
    private string _statusText = string.Empty;

    /// <summary>
    /// Builds the screen of one town and shows it in <paramref name="view"/>.
    /// </summary>
    /// <param name="view">The screen that paints it; its <see cref="CastleScreenView.Show"/> is called here.</param>
    /// <param name="castle">The town.</param>
    /// <param name="hero">The visiting hero, or <see langword="null"/> for an empty town.</param>
    /// <param name="world">The world the treasury of the owner is read from.</param>
    /// <param name="heroPortrait">The hero's big portrait, <c>hero.getPortrait( PORT_BIG )</c>.</param>
    /// <param name="captainPortrait">The captain's portrait, or <see langword="null"/> when the town has none.</param>
    /// <param name="heroAlpha">The starting alpha of the hero, 0 for a hero that was just recruited.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="heroAlpha"/> is outside 0…255.</exception>
    public CastleDialog(
        CastleScreenView view,
        Castle castle,
        Hero? hero,
        World world,
        Sprite? heroPortrait = null,
        Sprite? captainPortrait = null,
        int heroAlpha = FullHeroAlpha)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(heroAlpha);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(heroAlpha, FullHeroAlpha);

        _view = view;
        _castle = castle;
        _hero = hero;
        _world = world;
        _heroPortrait = heroPortrait;
        _captainPortrait = captainPortrait;
        _heroAlpha = heroAlpha;

        _input = new CastleScreenInput(view)
        {
            // 单城王国里两个箭头是灰的（castle_dialog.cpp:391-394）。
            CanSwitchCastle = world.GetKingdom(castle.Color).Castles.Count > 1,
        };

        view.Show(castle, hero);
    }

    /// <summary>The town the screen shows.</summary>
    public Castle Subject => _castle;

    /// <summary>The visiting hero, or <see langword="null"/> for an empty town.</summary>
    public Hero? Hero => _hero;

    /// <summary>The screen that paints this one.</summary>
    public CastleScreenView View => _view;

    /// <summary>The click handling of the screen.</summary>
    public CastleScreenInput Input => _input;

    /// <summary>What the last frame asked the owner to open.</summary>
    public CastleDialogRequests Requests { get; } = new();

    /// <summary>How the last frame ended the screen.</summary>
    public ReturnValue Result { get; private set; }

    /// <summary>The fade of a building that was just bought.</summary>
    public FadeBuilding Fade => _fade;

    /// <summary>How opaque the visiting hero is, <c>alphaHero</c>.</summary>
    public int HeroAlpha => _heroAlpha;

    /// <summary>Which frame of the buildings' animation is drawn, <c>castleAnimationIndex</c>.</summary>
    public uint AnimationIndex => _animationIndex;

    /// <summary>The line of the status bar, after the reference's "message or date" choice.</summary>
    public string StatusText => _statusText;

    /// <summary>
    /// The date line, <c>getDateString()</c> (<c>castle_dialog.cpp:158-166</c>).
    /// </summary>
    /// <remarks>
    /// Capitalised and spelled out, unlike <c>World.DateString()</c> which is the lower-case form used in logs. The
    /// reference reads the numbers from the world; the port takes them as arguments so that the formatting can be
    /// checked without a running game.
    /// </remarks>
    /// <param name="month">The month, <c>world.GetMonth()</c>.</param>
    /// <param name="week">The week, <c>world.GetWeek()</c>.</param>
    /// <param name="day">The day, <c>world.GetDay()</c>.</param>
    /// <returns>The line, for example <c>"Month: 1, Week: 3, Day: 5"</c>.</returns>
    public static string GetDateString(int month, int week, int day) =>
        $"Month: {month}, Week: {week}, Day: {day}";

    /// <summary>
    /// The status line under the pointer, <c>buildingStatusMessage()</c> (<c>castle_dialog.cpp:118-131</c>).
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building under the pointer.</param>
    /// <returns><c>"Recruit peasants"</c> for a dwelling, the building's name otherwise.</returns>
    public static string GetStatusLine(MapRace race, BuildingType building) =>
        BuildingInfo.GetStatusLine(race, building);

    /// <summary>
    /// Paints the whole screen, <c>CastleDialog::RedrawIcons()</c> plus the status bar and the resource panel.
    /// </summary>
    /// <param name="target">The frame buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _view.Draw(target, new CastleScreenFrame
        {
            Funds = _world.GetKingdom(_castle.Color).Treasury,
            AnimationIndex = _animationIndex,
            Fade = _fade,
            World = _world,
            StatusText = _statusText,
            HeroPortrait = _heroPortrait,
            CaptainPortrait = _captainPortrait,
            HeroAlpha = _heroAlpha,
            ExitPressed = _input.ExitPressed,
            PreviousPressed = _input.PreviousPressed,
            NextPressed = _input.NextPressed,
        });
    }

    /// <summary>
    /// Handles one frame of input, <c>castle_dialog.cpp:493-777</c>.
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <param name="hotKeys">The frame's hot keys.</param>
    /// <returns><see langword="true"/> when the picture has to be repainted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, CastleDialogHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        Requests.Clear();
        Result = ReturnValue.DoNothing;
        _statusText = string.Empty;

        bool redraw = false;

        // During the hero's fade-in or a building's fade the dialog takes no input (castle_dialog.cpp:497-498).
        if (_heroAlpha >= FullHeroAlpha && _fade.IsFadeDone)
        {
            _input.SetCursor(events.CursorPosition);
            _input.SetLeftButton(events.IsMouseLeftButtonPressed);

            CastleScreenAction? action = _input.TakeAction();

            if (TryTakeResult(action, hotKeys))
            {
                return false;
            }

            redraw |= ProcessArmyBars(events);
            redraw |= ProcessRightClicks(events);
            redraw |= ProcessMergeKeys(hotKeys);
            redraw |= ProcessBuildings(action, events, hotKeys);
        }

        UpdateStatusText();

        redraw |= UpdateAnimation();

        return redraw;
    }

    /// <summary>
    /// Runs the screen until it ends, <c>Castle::OpenDialog()</c>'s loop.
    /// </summary>
    /// <remarks>
    /// 返回 <see cref="ReturnValue.DoNothing"/> 表示调用方要先伺候 <see cref="Requests"/>：开那个窗口、做那笔买卖，然后
    /// 再调回来。参考在同一个循环里阻塞完成这些事，端口交回调用方。
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The reason the screen ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public ReturnValue Show(PixelBuffer target, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "CastleDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        while (true)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return ReturnValue.Close;
            }

            ProcessEvents(eventState, ReadHotKeys(eventState));

            Draw(target);
            engine.Render(target.Bounds);

            if (Result != ReturnValue.DoNothing)
            {
                return Result;
            }

            if (!Requests.IsEmpty)
            {
                return ReturnValue.DoNothing;
            }

            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }
    }

    /// <summary>Starts the fade of a building that was just bought.</summary>
    /// <param name="building">The building.</param>
    public void StartFadeBuilding(BuildingType building) => _fade.StartFadeBuilding(building);

    /// <summary>Starts the fade of a boat that was just bought.</summary>
    public void StartFadeBoat() => _fade.StartFadeBoat();

    /// <summary>The default shortcuts of the reference, read off the frame.</summary>
    /// <param name="events">Event state of the frame.</param>
    /// <returns>The shortcuts <see cref="ProcessEvents"/> takes.</returns>
    private static CastleDialogHotKeys ReadHotKeys(LocalEvent events) => new(
        CloseWindow: events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.Enter),
        PreviousCastle: events.IsKeyPressed(Key.Left),
        NextCastle: events.IsKeyPressed(Key.Right));

    /// <summary>
    /// The four ways out of the loop: EXIT, the close hot key and the two arrows (castle_dialog.cpp:509-528).
    /// </summary>
    /// <param name="action">The click of the frame, if any.</param>
    /// <param name="hotKeys">The frame's hot keys.</param>
    /// <returns>Whether the screen ended.</returns>
    private bool TryTakeResult(CastleScreenAction? action, CastleDialogHotKeys hotKeys)
    {
        if (action is { Target: CastleScreenTarget.Exit } || hotKeys.CloseWindow)
        {
            Result = ReturnValue.Close;

            return true;
        }

        if (hotKeys.PreviousCastle || action is { Target: CastleScreenTarget.PreviousCastle })
        {
            Result = ReturnValue.PreviousCastle;

            return true;
        }

        if (hotKeys.NextCastle || action is { Target: CastleScreenTarget.NextCastle })
        {
            Result = ReturnValue.NextCastle;

            return true;
        }

        if (action is { Target: CastleScreenTarget.Income })
        {
            Requests.Window = new CastleDialogRequest(CastleDialogWindow.Income);

            return false;
        }

        if (action is { Target: CastleScreenTarget.Hero })
        {
            Requests.Window = new CastleDialogRequest(CastleDialogWindow.Hero);
        }

        return false;
    }

    /// <summary>
    /// The two army bars, <c>castle_dialog.cpp:405-411</c>: a drag or a question from one bar about the other.
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>Whether something changed.</returns>
    private bool ProcessArmyBars(LocalEvent events)
    {
        ArmyBar? top = _view.CastleArmyBar;
        ArmyBar? bottom = _view.HeroArmyBar;

        if (top is null)
        {
            return false;
        }

        if (bottom is not null)
        {
            bool inTop = events.IsMouseCursorPosInArea(top.RenderingArea);
            bool inBottom = events.IsMouseCursorPosInArea(bottom.RenderingArea);

            if (inTop)
            {
                return TakeArmyEvent(top.ProcessEvents(events, bottom));
            }

            if (inBottom)
            {
                return TakeArmyEvent(bottom.ProcessEvents(events, top));
            }

            return false;
        }

        if (events.IsMouseCursorPosInArea(top.RenderingArea))
        {
            return TakeArmyEvent(top.ProcessEvents(events));
        }

        return false;
    }

    private bool TakeArmyEvent(ArmyBarEvent result)
    {
        if (!result.Handled)
        {
            return false;
        }

        Requests.Army = result;

        return true;
    }

    /// <summary>
    /// The right clicks of the screen: the income panel, the EXIT button, the arrows and the two portraits
    /// (castle_dialog.cpp:530-563).
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>Whether something changed.</returns>
    private bool ProcessRightClicks(LocalEvent events)
    {
        CastleDialogLayout layout = _view.Layout;

        if (events.IsMouseRightButtonPressedInArea(layout.ResourceHitArea))
        {
            Requests.Window = new CastleDialogRequest(CastleDialogWindow.Income);

            return false;
        }

        if (events.IsMouseRightButtonPressedInArea(_view.ExitButtonArea))
        {
            Requests.Popup = new DialogPopup("Exit", "Exit this menu.");

            return false;
        }

        if (events.IsMouseRightButtonPressedInArea(layout.NextCastleButton))
        {
            Requests.Popup = new DialogPopup("Show next town", "Click to show the next town.");

            return false;
        }

        if (events.IsMouseRightButtonPressedInArea(layout.PreviousCastleButton))
        {
            Requests.Popup = new DialogPopup("Show previous town", "Click to show the previous town.");

            return false;
        }

        if (_castle.IsBuild(BuildingType.Captain) && events.IsMouseRightButtonPressedInArea(layout.CrestArea))
        {
            Requests.Window = new CastleDialogRequest(CastleDialogWindow.CaptainQuickInfo);

            return false;
        }

        if (_hero is not null && events.IsMouseRightButtonPressedInArea(layout.HeroPortraitArea))
        {
            Requests.Window = new CastleDialogRequest(CastleDialogWindow.HeroQuickInfo);
        }

        return false;
    }

    /// <summary>
    /// Swapping the two armies and the two merge keys, <c>castle_dialog.cpp:553-601</c>.
    /// </summary>
    /// <param name="hotKeys">The frame's hot keys.</param>
    /// <returns>Whether something changed.</returns>
    private bool ProcessMergeKeys(CastleDialogHotKeys hotKeys)
    {
        if (_hero is null)
        {
            return false;
        }

        ArmyBar? top = _view.CastleArmyBar;
        ArmyBar? bottom = _view.HeroArmyBar;

        if (hotKeys.SwapArmies)
        {
            _hero.Army.SwapTroops(_castle.Army);
            top?.ResetSelected();
            bottom?.ResetSelected();

            return true;
        }

        // The stack the player picked is the one a merge keeps on top (castle_dialog.cpp:572-579).
        MonsterType keep = MonsterType.Unknown;

        if (top is { IsSelected: true })
        {
            keep = top.GetSelectedItem()?.Monster ?? MonsterType.Unknown;
        }
        else if (bottom is { IsSelected: true })
        {
            keep = bottom.GetSelectedItem()?.Monster ?? MonsterType.Unknown;
        }

        if (hotKeys.MergeWithHero)
        {
            _hero.Army.MoveTroops(_castle.Army, keep);
            top?.ResetSelected();
            bottom?.ResetSelected();

            return true;
        }

        if (hotKeys.MergeWithGarrison)
        {
            _castle.Army.MoveTroops(_hero.Army, keep);
            top?.ResetSelected();
            bottom?.ResetSelected();

            return true;
        }

        return false;
    }

    /// <summary>
    /// The buildings: their right-click information and their left-click windows
    /// (castle_dialog.cpp:614-776).
    /// </summary>
    /// <param name="action">The click of the frame, if any.</param>
    /// <param name="events">The frame's input.</param>
    /// <param name="hotKeys">The frame's hot keys.</param>
    /// <returns>Whether something changed.</returns>
    private bool ProcessBuildings(CastleScreenAction? action, LocalEvent events, CastleDialogHotKeys hotKeys)
    {
        Point cursor = events.CursorPosition;
        BuildingType underPointer = _view.FindBuildingAt(cursor);

        if (underPointer != BuildingType.Nothing && _castle.IsBuild(underPointer))
        {
            if (events.IsMouseRightButtonPressed)
            {
                BuildingType dwelling = _castle.GetActualDwelling(underPointer);

                if (dwelling != BuildingType.Nothing)
                {
                    Requests.Window = new CastleDialogRequest(CastleDialogWindow.DwellingInfo, dwelling);

                    return false;
                }

                Requests.Popup = new DialogPopup(
                    CastleBuildingInfo.GetName(_castle.Race, underPointer),
                    BuildingInfo.GetBuildingDescription(_castle.Race, underPointer));

                return false;
            }
        }

        // The building the player clicked: the click handler's answer, or a building hot key.
        BuildingType clicked = BuildingType.Nothing;

        if (action is { Target: CastleScreenTarget.Building })
        {
            clicked = action.Value.Building;
        }
        else if (hotKeys.Building != BuildingType.Nothing)
        {
            clicked = hotKeys.Building;
        }

        if (clicked == BuildingType.Nothing || !_castle.IsBuild(clicked))
        {
            return false;
        }

        _view.CastleArmyBar?.ResetSelected();
        _view.HeroArmyBar?.ResetSelected();

        OpenBuilding(clicked);

        return true;
    }

    /// <summary>
    /// Translates a clicked building into the window the reference opens, <c>castle_dialog.cpp:666-772</c>.
    /// </summary>
    /// <remarks>
    /// 参考的分派是一个 100 行的 <c>switch</c>，里面混着「开窗口」和「买建筑」。这里只保留前者，买建筑留给调用方，
    /// 于是分派成了纯函数，可以脱离屏幕测。
    /// </remarks>
    /// <param name="castle">The town the building belongs to.</param>
    /// <param name="building">The building that was clicked.</param>
    /// <returns>
    /// The window to open, or <see langword="null"/> when the click only deserves a message — then
    /// <paramref name="popup"/> carries it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    public static CastleDialogRequest? ResolveBuilding(Castle castle, BuildingType building, out DialogPopup? popup)
    {
        ArgumentNullException.ThrowIfNull(castle);

        popup = null;

        // 住所按 GetActualDwelling 归一：点哪一个等级都开最高已建的那一档（castle_dialog.cpp:667-671）。
        if (castle.GetActualDwelling(building) is var dwelling && dwelling != BuildingType.Nothing)
        {
            return new CastleDialogRequest(CastleDialogWindow.Dwelling, dwelling);
        }

        // 魔法塔带等级时点击落在哪一档由掩码决定（castle_dialog.cpp:673-677）。
        if ((building & BuildingType.MageGuild) != 0)
        {
            return new CastleDialogRequest(CastleDialogWindow.MageGuild, building);
        }

        switch (building)
        {
            case BuildingType.ThievesGuild:
                return new CastleDialogRequest(CastleDialogWindow.ThievesGuild, building);

            case BuildingType.Tavern:
                return new CastleDialogRequest(CastleDialogWindow.Tavern, building);

            case BuildingType.Well:
                return new CastleDialogRequest(CastleDialogWindow.Well, building);

            case BuildingType.Marketplace:
                return new CastleDialogRequest(CastleDialogWindow.Marketplace, building);

            case BuildingType.Shipyard:
                return new CastleDialogRequest(CastleDialogWindow.Shipyard, building);

            case BuildingType.Castle:
                return new CastleDialogRequest(CastleDialogWindow.Construction, building);

            case BuildingType.Tent:
                // 参考在开买城窗口之前先挡一次「这座城不许升级」（castle_dialog.cpp:740-745）。
                if (castle.IsBuildingDisabled(BuildingType.Castle))
                {
                    popup = new DialogPopup("Town", "This town may not be upgraded to a castle.");

                    return null;
                }

                return new CastleDialogRequest(CastleDialogWindow.BuildCastle, building);

            case BuildingType.Captain:
            case BuildingType.Statue:
            case BuildingType.Well2:
            case BuildingType.Moat:
            case BuildingType.Spec:
            case BuildingType.Shrine:
                popup = new DialogPopup(
                    CastleBuildingInfo.GetName(castle.Race, building),
                    BuildingInfo.GetBuildingDescription(castle.Race, building));

                return null;

            default:
                // 左右塔与住所的升级形式都在上面被住所与魔法塔分支吃掉，走到这里的是参考里 assert 的那几类。
                return null;
        }
    }

    private void OpenBuilding(BuildingType building)
    {
        CastleDialogRequest? window = ResolveBuilding(_castle, building, out DialogPopup? popup);

        Requests.Window = window;
        Requests.Popup = popup;
    }

    /// <summary>
    /// The status bar: an army bar's message, then the area under the pointer, then the date
    /// (castle_dialog.cpp:400-461).
    /// </summary>
    private void UpdateStatusText()
    {
        // 两条军队栏的消息优先于任何区域（castle_dialog.cpp:401-406）。
        string message = _view.CastleArmyBar?.Message ?? string.Empty;

        if (message.Length == 0)
        {
            message = _view.HeroArmyBar?.Message ?? string.Empty;
        }

        if (message.Length == 0 && _input.Cursor is { } cursor)
        {
            message = _view.GetStatusMessage(cursor);
        }

        if (message.Length == 0)
        {
            message = GetDateString((int)_world.Month, (int)_world.Week, (int)_world.Day);
        }

        _statusText = message;
    }

    /// <summary>
    /// The two animation clocks: the buildings' round and the hero's fade-in (castle_dialog.cpp:783-797).
    /// </summary>
    /// <returns>Whether something changed.</returns>
    private bool UpdateAnimation()
    {
        bool redraw = false;

        if (GameDelays.ValidateAnimationDelay(DelayType.CastleAroundDelay))
        {
            _animationIndex = _animationIndex >= byte.MaxValue ? FirstAnimationIndex : _animationIndex + 1;
            redraw = true;
        }

        if (_heroAlpha < FullHeroAlpha && GameDelays.ValidateAnimationDelay(DelayType.CastleItemUpdateDelay))
        {
            _heroAlpha = Math.Min(FullHeroAlpha, _heroAlpha + HeroAlphaStep);
            redraw = true;
        }

        redraw |= _fade.Update(GameDelays.ValidateAnimationDelay(DelayType.CastleItemUpdateDelay));

        return redraw;
    }
}
