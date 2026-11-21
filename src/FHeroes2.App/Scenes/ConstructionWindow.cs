using FHeroes2.App.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Scenes;

/// <summary>What the construction window asks its owner to do.</summary>
public enum ConstructionActionTarget
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>Close the window, <c>castle_town.cpp:613-615</c>.</summary>
    Exit,

    /// <summary>Buy the building in <see cref="ConstructionAction.Building"/>, <c>castle_town.cpp:642-720</c>.</summary>
    Build,

    /// <summary>Show the previous town, <c>castle_town.cpp:620-622</c>.</summary>
    PreviousTown,

    /// <summary>Show the next town, <c>castle_town.cpp:627-629</c>.</summary>
    NextTown,

    /// <summary>Open the income window, <c>castle_town.cpp:632-635</c>.</summary>
    Income,

    /// <summary>Recruit the first hero of the tavern, <c>castle_town.cpp:700-722</c>.</summary>
    FirstHero,

    /// <summary>Recruit the second hero of the tavern.</summary>
    SecondHero,
}

/// <summary>One request of the construction window.</summary>
/// <param name="Target">What the window asks for.</param>
/// <param name="Building">The building to buy, or <see cref="BuildingType.Nothing"/>.</param>
public readonly record struct ConstructionAction(ConstructionActionTarget Target, BuildingType Building);

/// <summary>The window behind a right click, or <see cref="None"/>.</summary>
public enum ConstructionHelpTarget
{
    /// <summary>Nothing worth a window.</summary>
    None,

    /// <summary>"Exit" / "Exit this menu.", <c>castle_town.cpp:639-641</c>.</summary>
    Exit,

    /// <summary>The kingdom income, <c>castle_town.cpp:636-638</c>.</summary>
    Income,

    /// <summary>The description of a building, <c>BuildingInfo::QueueEventProcessing()</c>.</summary>
    Building,

    /// <summary>The first hero on offer.</summary>
    FirstHero,

    /// <summary>The second hero on offer.</summary>
    SecondHero,

    /// <summary>The description of the "spread" formation, <c>castle_town.cpp:738-740</c>.</summary>
    SpreadFormation,

    /// <summary>The description of the "grouped" formation, <c>castle_town.cpp:741-743</c>.</summary>
    GroupedFormation,
}

/// <summary>One right-click request of the construction window.</summary>
/// <param name="Target">What the right click asks for.</param>
/// <param name="Title">The title of the message box, empty when the target has none.</param>
/// <param name="Text">The body of the message box, empty when the target has none.</param>
public readonly record struct ConstructionHelp(ConstructionHelpTarget Target, string Title, string Text);

/// <summary>
/// The construction window of a town, <c>Castle::_openConstructionDialog()</c>
/// (<c>castle_town.cpp:171-800</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和建造确认框一样，这一层只做「窗口本体」：<see cref="ConstructionView"/> 负责画，<see cref="ConstructionInput"/>
/// 负责鼠标，窗口自己把鼠标、键盘、状态栏和右键说明拼起来，动作交给场景——窗口不扣钱、不改城，也不换城。
/// </para>
/// <para>
/// 两个英雄位的左键只上报 <see cref="ConstructionActionTarget.FirstHero"/>/
/// <see cref="ConstructionActionTarget.SecondHero"/>，人由场景交来的 <see cref="FirstHero"/>/
/// <see cref="SecondHero"/> 决定；右键的「英雄信息」同样只上报，由场景记一条日志（那个窗口还没搬）。
/// </para>
/// </remarks>
public sealed class ConstructionWindow
{
    private readonly ConstructionView _view;
    private readonly ConstructionInput _input;
    private readonly World? _world;
    private readonly Kingdom? _kingdom;
    private readonly GameFonts _fonts;

    private Hero? _firstHero;
    private Hero? _secondHero;
    private Sprite? _firstPortrait;
    private Sprite? _secondPortrait;
    private bool _canRecruitFirst;
    private bool _canRecruitSecond;
    private bool _closePressed;
    private ConstructionActionTarget _pendingTarget = ConstructionActionTarget.None;

    private ConstructionWindow(
        ConstructionView view,
        World? world,
        Kingdom? kingdom,
        GameFonts fonts)
    {
        _view = view;
        _input = new ConstructionInput(view) { CanSwitchCastle = (kingdom?.Castles.Count ?? 0) > 1 };
        _world = world;
        _kingdom = kingdom;
        _fonts = fonts;

        RefreshHeroes();
    }

    /// <summary>The town the window shows.</summary>
    public Castle Castle => _view.Castle;

    /// <summary>The first hero on offer, <c>hero1</c> of <c>castle_town.cpp:193-200</c>.</summary>
    public Hero? FirstHero => _firstHero;

    /// <summary>The second hero on offer, <c>hero2</c> of <c>castle_town.cpp:193-200</c>.</summary>
    public Hero? SecondHero => _secondHero;

    /// <summary>The window's own layout, exposed for tests and for the scene's status line.</summary>
    public ConstructionView View => _view;

    /// <summary>
    /// Builds the construction window of a town.
    /// </summary>
    /// <param name="castle">The town, which must already own a castle.</param>
    /// <param name="kingdom">The owner's kingdom, for the treasury and the recruits; may be <see langword="null"/>.</param>
    /// <param name="world">The world, for the date line and the town picture's ground; may be <see langword="null"/>.</param>
    /// <param name="fonts">The game fonts.</param>
    /// <returns>The window, or <see langword="null"/> when a frame of the window is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> or <paramref name="fonts"/> is <see langword="null"/>.</exception>
    public static ConstructionWindow? TryCreate(Castle castle, Kingdom? kingdom, World? world, GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(fonts);

        if (ConstructionArtwork.TryCreate(castle.Race, castle.Color) is not { } artwork)
        {
            return null;
        }

        Funds treasury = kingdom is null ? default : kingdom.Treasury;

        ConstructionView view = ConstructionView.Create(castle, artwork, fonts.Small, treasury);

        return new ConstructionWindow(view, world, kingdom, fonts);
    }

    /// <summary>Records the pointer, which also picks the status line.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor) => _input.SetCursor(cursor);

    /// <summary>Records the left button; the answer is read by <see cref="Update"/>.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down) => _input.SetLeftButton(down);

    /// <summary>
    /// Records the right button and answers which help the press asks for.
    /// </summary>
    /// <param name="down">Whether the right button is now held.</param>
    /// <returns>The help to show, or <see cref="ConstructionHelpTarget.None"/>.</returns>
    public ConstructionHelp SetRightButton(bool down)
    {
        if (!down || _input.Cursor is not { } cursor)
        {
            return new ConstructionHelp(ConstructionHelpTarget.None, string.Empty, string.Empty);
        }

        ConstructionHit hit = _view.HitTest(cursor);

        switch (hit.Target)
        {
            case ConstructionTarget.Exit:
                return new ConstructionHelp(ConstructionHelpTarget.Exit, "Exit", ConstructionView.ExitHelpText);

            case ConstructionTarget.Income:
                return new ConstructionHelp(ConstructionHelpTarget.Income, string.Empty, string.Empty);

            case ConstructionTarget.FirstHero when _firstHero is not null:
                return new ConstructionHelp(ConstructionHelpTarget.FirstHero, _firstHero.Name, HeroHelpText(_firstHero));

            case ConstructionTarget.SecondHero when _secondHero is not null:
                return new ConstructionHelp(ConstructionHelpTarget.SecondHero, _secondHero.Name, HeroHelpText(_secondHero));

            case ConstructionTarget.SpreadFormation:
                return new ConstructionHelp(
                    ConstructionHelpTarget.SpreadFormation,
                    ConstructionView.SpreadFormationHelpTitle,
                    ConstructionView.SpreadFormationHelpText);

            case ConstructionTarget.GroupedFormation:
                return new ConstructionHelp(
                    ConstructionHelpTarget.GroupedFormation,
                    ConstructionView.GroupedFormationHelpTitle,
                    ConstructionView.GroupedFormationHelpText);

            case ConstructionTarget.Building:
                ConstructionCell cell = _view.Cells[hit.CellIndex];

                return cell.Info.Type == BuildingType.Nothing
                    ? new ConstructionHelp(ConstructionHelpTarget.None, string.Empty, string.Empty)
                    : new ConstructionHelp(ConstructionHelpTarget.Building, cell.Info.Name, cell.Info.Description);

            default:
                return new ConstructionHelp(ConstructionHelpTarget.None, string.Empty, string.Empty);
        }
    }

    /// <summary>Marks the close hot key, ESCAPE or RETURN, <c>Game::HotKeyCloseWindow()</c>.</summary>
    public void PressClose() => _closePressed = true;

    /// <summary>Marks the "previous town" hot key, <c>Game::HotKeyEvent::DEFAULT_LEFT</c>.</summary>
    public void PressPreviousTown() => _pendingTarget = ConstructionActionTarget.PreviousTown;

    /// <summary>Marks the "next town" hot key, <c>Game::HotKeyEvent::DEFAULT_RIGHT</c>.</summary>
    public void PressNextTown() => _pendingTarget = ConstructionActionTarget.NextTown;

    /// <summary>Consumes the events of this frame.</summary>
    /// <returns>What the owner should do next; the window does not close itself.</returns>
    public ConstructionAction Update()
    {
        ConstructionAction action;

        if (_pendingTarget != ConstructionActionTarget.None)
        {
            action = new ConstructionAction(_pendingTarget, BuildingType.Nothing);
        }
        else if (_closePressed)
        {
            action = new ConstructionAction(ConstructionActionTarget.Exit, BuildingType.Nothing);
        }
        else if (_input.TakeAction() is { } hit)
        {
            action = ToAction(hit);
        }
        else
        {
            action = new ConstructionAction(ConstructionActionTarget.None, BuildingType.Nothing);
        }

        _closePressed = false;
        _pendingTarget = ConstructionActionTarget.None;

        return action;
    }

    /// <summary>Paints the whole window.</summary>
    /// <param name="target">The frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        string status = _view.GetStatusMessage(
            _input.Cursor,
            _firstHero,
            _secondHero,
            _canRecruitFirst,
            _canRecruitSecond,
            _input.CanSwitchCastle);

        if (status.Length == 0)
        {
            status = _world?.DateString() ?? string.Empty;
        }

        ConstructionFrame frame = new()
        {
            Funds = _kingdom is { } kingdom ? kingdom.Treasury : default,
            World = _world,
            FirstHeroPortrait = _firstPortrait,
            SecondHeroPortrait = _secondPortrait,
            CanRecruitFirstHero = _canRecruitFirst,
            CanRecruitSecondHero = _canRecruitSecond,
            StatusText = status,
            ExitPressed = _input.ExitPressed,
            PreviousPressed = _input.PreviousPressed,
            NextPressed = _input.NextPressed,
        };

        _view.Draw(target, frame);
    }

    /// <summary>Turns a hit into what the scene has to do, <c>castle_town.cpp:642-768</c>.</summary>
    private ConstructionAction ToAction(ConstructionHit hit) => hit.Target switch
    {
        ConstructionTarget.Exit => new ConstructionAction(ConstructionActionTarget.Exit, BuildingType.Nothing),
        ConstructionTarget.PreviousTown => new ConstructionAction(ConstructionActionTarget.PreviousTown, BuildingType.Nothing),
        ConstructionTarget.NextTown => new ConstructionAction(ConstructionActionTarget.NextTown, BuildingType.Nothing),
        ConstructionTarget.Income => new ConstructionAction(ConstructionActionTarget.Income, BuildingType.Nothing),
        ConstructionTarget.FirstHero when _firstHero is not null => new ConstructionAction(
            ConstructionActionTarget.FirstHero,
            BuildingType.Nothing),
        ConstructionTarget.SecondHero when _secondHero is not null => new ConstructionAction(
            ConstructionActionTarget.SecondHero,
            BuildingType.Nothing),
        ConstructionTarget.SpreadFormation => SetFormation(spread: true),
        ConstructionTarget.GroupedFormation => SetFormation(spread: false),
        ConstructionTarget.Building => new ConstructionAction(
            ConstructionActionTarget.Build,
            _view.Cells[hit.CellIndex].Info.Type),
        _ => new ConstructionAction(ConstructionActionTarget.None, BuildingType.Nothing),
    };

    /// <summary>The two formation icons, which the window flips itself, <c>castle_town.cpp:727-737</c>.</summary>
    private ConstructionAction SetFormation(bool spread)
    {
        Castle.Army.IsSpreadFormation = spread;

        return new ConstructionAction(ConstructionActionTarget.None, BuildingType.Nothing);
    }

    private static string HeroHelpText(Hero hero) =>
        $"Level {hero.Level}, {(hero.HasSpellBook ? "has a spell book" : "has no spell book")}.";

    /// <summary>Re-reads the tavern's offer and the portraits, which change after a town switch or a hire.</summary>
    private void RefreshHeroes()
    {
        if (_kingdom is { } kingdom && _world is { } world)
        {
            // 参考在城堡界面里现问 Kingdom::GetRecruits()（castle_town.cpp:193），那个函数顺手补位；
            // 端口把补位放在带 World 的重载里。
            Recruits recruits = kingdom.GetRecruits(world);

            _firstHero = recruits.GetHero1(world.Heroes);
            _secondHero = recruits.GetHero2(world.Heroes);
        }
        else
        {
            _firstHero = null;
            _secondHero = null;
        }

        _firstPortrait = PortraitOf(_firstHero);
        _secondPortrait = PortraitOf(_secondHero);

        _canRecruitFirst = _firstHero is not null && _kingdom is { } k1 && Castle.AllowBuyHero(null, k1);
        _canRecruitSecond = _secondHero is not null && _kingdom is { } k2 && Castle.AllowBuyHero(null, k2);
    }

    private static Sprite? PortraitOf(Hero? hero)
    {
        if (hero is null)
        {
            return null;
        }

        IcnType icn = HeroPortraitIcn.Get(hero.PortraitId);

        return icn == IcnType.Unknown ? null : GameAssets.GetImage(IcnCatalog.GetFileName(icn), 0);
    }
}
