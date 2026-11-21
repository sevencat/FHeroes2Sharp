using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Timing;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;

using ArmyText = FHeroes2.Game.Army.Army;
using RedrawMask = FHeroes2.UI.Interface.Redraw;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The stone panel under the buttons of the adventure screen, a port of <c>Interface::StatusPanel</c>
/// (<c>src/fheroes2/gui/interface_status.cpp:55-524</c>, <c>interface_status.h:33-90</c>).
/// </summary>
/// <remarks>
/// <para>
/// The panel shows one of five states: the date, the kingdom's resources, the troops of the focused hero or
/// castle, a transient «you found some resource» message, and the hourglass of an AI turn. A left click cycles
/// through the first three (the reference's <c>NextState()</c>), the panel grows with the window so that it can
/// show one, two or three of them at once.
/// </para>
/// <para>
/// <b>Deviations.</b> The world, the display, the sprite source and the mini-monster source are injected instead of
/// reached through <c>world</c>, <c>Display::instance()</c> and <c>Assets::getImage()</c>. The
/// «no interface» setting, the evil interface look and the autoplay flag are properties the orchestrator keeps in
/// sync with <see cref="Settings"/>, since the panel is created before those settings settle. The right click
/// returns <see cref="StatusPanelAction.ShowTooltip"/> instead of opening the message box itself, and a started
/// drag is handed back through <see cref="Dragging"/> — the reference keeps both on the global interface. Finally
/// <see cref="SavePosition"/> keeps the offset in the session settings only: writing it to <c>.fh2config</c> waits for
/// the configuration writer.
/// </para>
/// </remarks>
public sealed class StatusPanel : BorderWindow
{
    /// <summary>Width of the panel, one <c>STONBACK</c> frame, <c>144</c>.</summary>
    public const int Width = 144;

    /// <summary>Height of one state, one <c>STONBACK</c> frame, <c>72</c>.</summary>
    public const int Height = 72;

    /// <summary>
    /// The extra pixels the reference requires on top of the stone frames to show two or three states,
    /// <c>15</c> in <c>_redraw()</c>.
    /// </summary>
    public const int StateSlack = 15;

    /// <summary>How long the «you found some resource» message stays, <c>2500</c> ms.</summary>
    public const uint ResourceMessageDelayMs = 2_500;

    /// <summary>The background of the panel when the interface is hidden, <c>GetColorId( 0x51, 0x31, 0x18 )</c>.</summary>
    public const byte HiddenBackgroundRed = 0x51;
    /// <summary>Green channel of <see cref="HiddenBackgroundRed"/>.</summary>
    public const byte HiddenBackgroundGreen = 0x31;
    /// <summary>Blue channel of <see cref="HiddenBackgroundRed"/>.</summary>
    public const byte HiddenBackgroundBlue = 0x18;

    private readonly BaseInterface _interface;
    private readonly PixelBuffer _display;
    private readonly World _world;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly IMiniMonsterSprites _monsters;
    private readonly TimeDelay _showLastResourceDelay;

    private StatusType _state = StatusType.Unknown;
    private int _lastResource = Resource.Unknown;
    private uint _lastResourceCount;
    private uint _aiTurnProgress = 10;
    private uint _grainsAnimationIndexOffset;

    /// <summary>Creates the panel over the given area.</summary>
    /// <param name="interface">Owner, told to redraw the panel through <c>setRedraw( REDRAW_STATUS )</c>.</param>
    /// <param name="display">The frame the panel paints on.</param>
    /// <param name="world">The world whose date, kingdom and troop counts are shown.</param>
    /// <param name="sprites">Source of <c>STONBACK</c>, <c>RESSMALL</c>, <c>RESOURCE</c>, <c>HOURGLAS</c>, …</param>
    /// <param name="monsters">Source of the mini monster sprites of <c>Army::drawMultipleMonsterLines()</c>.</param>
    /// <param name="clockMs">
    /// Clock of the found-resource timeout in milliseconds. <see langword="null"/> uses the process clock, tests
    /// pass a fake one.
    /// </param>
    /// <remarks>
    /// The floating border artwork is not passed: the reference gets it from <c>Dialog::FrameBorder</c>, whose
    /// nine-slice source has no port yet. The panel therefore paints no stone frame when the interface is hidden —
    /// the same deviation as <see cref="Radar"/>.
    /// </remarks>
    public StatusPanel(BaseInterface @interface, PixelBuffer display, World world, IViewWorldSpriteSource sprites,
        IMiniMonsterSprites monsters, Func<long>? clockMs = null)
        : base(display, new Rect(0, 0, Width, Height), artwork: null)
    {
        ArgumentNullException.ThrowIfNull(@interface);
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(monsters);

        _interface = @interface;
        _display = display;
        _world = world;
        _sprites = sprites;
        _monsters = monsters;
        _showLastResourceDelay = new TimeDelay(ResourceMessageDelayMs, clockMs);
    }

    /// <summary>The state being shown, <c>_state</c>.</summary>
    public StatusType State => _state;

    /// <summary>The resource of the last «you found» message, <c>_lastResource</c>.</summary>
    public int LastResource => _lastResource;

    /// <summary>How much of <see cref="LastResource"/> was found, <c>_lastResourceCount</c>.</summary>
    public uint LastResourceCount => _lastResourceCount;

    /// <summary>
    /// Whether the panel is shown at all when the interface is hidden, <c>Settings::ShowStatus()</c>.
    /// </summary>
    /// <remarks>
    /// The reference reads the setting every frame. The port keeps a property so the panel can be painted in tests
    /// without touching the global settings; the orchestrator copies <c>Settings.IsShowStatus</c> here.
    /// </remarks>
    public bool ShowStatus { get; set; } = true;

    /// <summary>Whether the evil-interface artwork is used, <c>Settings::isEvilInterfaceEnabled()</c>.</summary>
    public bool EvilInterface { get; set; }

    /// <summary>
    /// Whether the game runs as an automatic playtest, <c>Settings::IsGameType( Game::TYPE_AUTO_PLAYTEST )</c>.
    /// </summary>
    /// <remarks>An autoplay has no human color, so the kingdom state would never be painted without this flag.</remarks>
    public bool IsAutoPlayTest { get; set; }

    /// <summary>The drag that started in the last <see cref="QueueEventProcessing"/>, or <see langword="null"/>.</summary>
    /// <remarks>
    /// The reference keeps the drag in <c>BorderWindow</c> and paints the floating window from the game loop. The
    /// port hands the drag to the orchestrator, which owns the floating windows (the same split as
    /// <see cref="BorderWindow.QueueEventProcessing"/>).
    /// </remarks>
    public BorderDrag? Dragging { get; private set; }

    /// <summary>Moves and, in the normal layout, stretches the panel to the bottom of the screen, <c>SetPos()</c>.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    public override void SetPos(int x, int y)
    {
        int height = Height;

        if (!HideInterfaceEnabled)
        {
            height = _display.Height - y - GameBorder.BorderWidth;
        }

        SetPosition(x, y, Width, height);
    }

    /// <summary>Stores the floating position, <c>SavePosition()</c>.</summary>
    public override void SavePosition() => Settings.Get().SetPosStatus(GetRect.Position);

    /// <summary>Asks for the panel to be repainted, <c>setRedraw()</c>.</summary>
    public void SetRedraw() => SetRedraw(RedrawMask.Status);

    /// <summary>Asks for the panel to be repainted with the given mask, <c>SetRedraw( uint32_t )</c>.</summary>
    /// <param name="mask">Extra redraw bits; only <see cref="RedrawMask.Status"/> is accepted.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mask"/> asks for anything else.</exception>
    public void SetRedraw(RedrawMask mask)
    {
        if ((mask & ~RedrawMask.Status) != RedrawMask.None)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), mask, "The status panel can only redraw itself.");
        }

        _interface.SetRedraw(mask | RedrawMask.Status);
    }

    /// <summary>Switches the state, <c>SetState()</c>.</summary>
    /// <param name="state">The new state.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="state"/> is <see cref="StatusType.Resource"/>: only <see cref="SetResource"/> may enter it,
    /// because it also starts the message timer.
    /// </exception>
    /// <remarks>
    /// A pending found-resource message wins over the requested state, as in the reference: the message stays until
    /// its timer expires.
    /// </remarks>
    public void SetState(StatusType state)
    {
        if (state == StatusType.Resource)
        {
            throw new ArgumentException("Use SetResource() to show a found resource.", nameof(state));
        }

        if (_state != StatusType.Resource)
        {
            _state = state;
        }
    }

    /// <summary>Shows the «you found some resource» message, <c>SetResource()</c>.</summary>
    /// <param name="resource">Resource flag, <see cref="Resource"/>, or <see cref="Resource.Unknown"/> to hide it.</param>
    /// <param name="count">How many were found.</param>
    public void SetResource(int resource, uint count)
    {
        _lastResource = resource;
        _lastResourceCount = count;
        _state = StatusType.Resource;

        _showLastResourceDelay.Reset();
    }

    /// <summary>Switches to the next of the cycling states, <c>NextState()</c>.</summary>
    /// <remarks>
    /// The order is date → resources → troops → date. When only two states fit, the date state is skipped in the
    /// troops → date step, and an empty castle army is skipped as well: its troop state would show nothing.
    /// </remarks>
    public void NextState()
    {
        int stoneHeight = StoneHeight();
        Rect area = Area;

        bool skipDayStatus = area.Height >= (stoneHeight * 2) + StateSlack && area.Height < (stoneHeight * 3) + StateSlack;

        if (_state == StatusType.Day)
        {
            _state = StatusType.Funds;
        }
        else if (_state == StatusType.Funds)
        {
            _state = InterfaceFocus.GetFocusType() == GameFocus.None ? StatusType.Day : StatusType.Army;
        }
        else if (_state == StatusType.Army)
        {
            _state = skipDayStatus ? StatusType.Funds : StatusType.Day;
        }
        else if (_state == StatusType.Resource)
        {
            _state = StatusType.Army;
        }

        if (_state == StatusType.Army && InterfaceFocus.GetFocusCastle() is { } castle && !castle.Army.IsValid)
        {
            NextState();
        }
    }

    /// <summary>Returns to the state a fresh game starts with, <c>Reset()</c>.</summary>
    public void Reset()
    {
        _state = StatusType.Day;
        _lastResource = Resource.Unknown;
        _lastResourceCount = 0;
    }

    /// <summary>Restarts the hourglass animation, <c>ResetAiTurnProgress()</c>.</summary>
    public void ResetAiTurnProgress() => _grainsAnimationIndexOffset = GameAnimation.AdventureMapAnimationIndex;

    /// <summary>Ends the found-resource message when its timer expires, <c>TimerEventProcessing()</c>.</summary>
    /// <remarks>The next state follows the focus: troops for a hero, resources for a castle, the date otherwise.</remarks>
    public void TimerEventProcessing()
    {
        if (_state != StatusType.Resource || !_showLastResourceDelay.IsPassed())
        {
            return;
        }

        _state = InterfaceFocus.GetFocusType() switch
        {
            GameFocus.Heroes => StatusType.Army,
            GameFocus.Castle => StatusType.Funds,
            _ => StatusType.Day,
        };

        SetRedraw();
    }

    /// <summary>Handles the mouse, <c>QueueEventProcessing()</c>.</summary>
    /// <param name="events">The event state of the frame.</param>
    /// <returns>What the caller has to do next.</returns>
    /// <remarks>
    /// The reference opens the «Status Window» message box on a right click and repaints itself after a click or a
    /// drag. The port reports both instead — the orchestrator owns the dialogs and the redraw scheduling — and hands
    /// the started drag back through <see cref="Dragging"/>.
    /// </remarks>
    public new StatusPanelAction QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Dragging = null;
        CaptureMouse(events);

        if (ShowStatus && base.QueueEventProcessing(events) is { } drag)
        {
            Dragging = drag;
            SetRedraw();
            return StatusPanelAction.DragStarted;
        }

        Rect area = Area;

        if (events.MouseClickLeft(area))
        {
            NextState();
            SetRedraw();
            return StatusPanelAction.StateCycled;
        }

        if (events.IsMouseRightButtonPressedInArea(GetRect))
        {
            return StatusPanelAction.ShowTooltip;
        }

        return StatusPanelAction.None;
    }

    /// <summary>Height of one state, taken from the stone artwork so a modded frame still fits.</summary>
    private int StoneHeight() => StoneFrame()?.Height ?? Height;

    /// <summary>The stone frame the panel is painted with, <c>Assets::getImage( STONBACK/STONBAKE, 0 )</c>.</summary>
    private Sprite? StoneFrame() =>
        _sprites.GetSprite(EvilInterface ? IcnType.Stonbake : IcnType.Stonback, 0);

    /// <summary>Blits with the offset of <c>fheroes2::Blit( sprite, display, x, y )</c>.</summary>
    private void Blit(Sprite sprite, int x, int y) =>
        ImageOps.BlitSprite(_display, new Point(x + sprite.OffsetX, y + sprite.OffsetY), sprite);

    /// <summary>Copies a part of a sprite, <c>fheroes2::Copy()</c>.</summary>
    /// <remarks>
    /// <c>Copy</c> ignores the transform layer and puts the source slice's top left corner on the destination, which
    /// is <see cref="ImageOps.Blit"/>; <see cref="ImageOps.BlitSprite(PixelBuffer, Point, Sprite, in Rect)"/> would
    /// shift by the slice's own offset instead.
    /// </remarks>
    private void Copy(Sprite sprite, int sourceX, int sourceY, int destinationX, int destinationY, int width, int height) =>
        ImageOps.Blit(_display, new Point(destinationX, destinationY), sprite.Image,
            new Rect(sourceX, sourceY, width, height));

    /// <summary>Draws a number centred on <paramref name="centerX"/>.</summary>
    private void DrawCentred(Text text, int centerX, int y) =>
        text.Draw(centerX - (text.Width() / 2), y, _display);

    /// <summary>Paints the current state, <c>_redraw()</c>.</summary>
    /// <remarks>
    /// The layout depends on how many states fit in <see cref="BorderWindow.Area"/>: three, two, or only the one of
    /// the current state. In the normal layout the panel is as tall as the space left under the buttons, so it shows
    /// one state; with the interface hidden it becomes a floating window that can show all three.
    /// </remarks>
    public void RedrawStatus()
    {
        if (HideInterfaceEnabled && !ShowStatus)
        {
            return;
        }

        Rect area = Area;

        if (HideInterfaceEnabled)
        {
            ImageOps.FillRect(_display, area,
                EmptyButtons.GetColorId(HiddenBackgroundRed, HiddenBackgroundGreen, HiddenBackgroundBlue));
            base.Redraw();
        }
        else
        {
            DrawBackground();
        }

        // A game that has not started has no date to show.
        if (_world.DayCount == 0)
        {
            return;
        }

        int stoneHeight = StoneHeight();
        int twoStates = (stoneHeight * 2) + StateSlack;

        if (_state == StatusType.AiTurn)
        {
            DrawAiTurns();
        }
        else if (_state != StatusType.Unknown && area.Height >= (stoneHeight * 3) + StateSlack)
        {
            DrawDayInfo(0);

            if ((Players.HumanColors() & Settings.Get().Players.CurrentColor) != PlayerColor.None || IsAutoPlayTest)
            {
                DrawKingdomInfo(stoneHeight + 5);

                if (_state != StatusType.Resource)
                {
                    DrawArmyInfo((stoneHeight * 2) + 10);
                }
                else
                {
                    DrawResourceInfo((stoneHeight * 2) + 10);
                }
            }
        }
        else if (_state != StatusType.Unknown && area.Height >= twoStates)
        {
            DrawDayInfo(0);

            switch (_state)
            {
            case StatusType.Funds:
                DrawKingdomInfo(stoneHeight + 5);
                break;
            case StatusType.Day:
            case StatusType.Army:
                DrawArmyInfo(stoneHeight + 5);
                break;
            case StatusType.Resource:
                DrawResourceInfo(stoneHeight + 5);
                break;
            case StatusType.Unknown:
            case StatusType.AiTurn:
                throw new InvalidOperationException("The state was checked to be neither unknown nor an AI turn.");
            }
        }
        else
        {
            int offsetY = area.Height > stoneHeight ? (area.Height - stoneHeight) / 2 : 0;

            switch (_state)
            {
            case StatusType.Day:
                DrawDayInfo(0);
                break;
            case StatusType.Funds:
                DrawKingdomInfo(offsetY);
                break;
            case StatusType.Army:
                DrawArmyInfo(offsetY);
                break;
            case StatusType.Resource:
                DrawResourceInfo(offsetY);
                break;
            case StatusType.Unknown:
            case StatusType.AiTurn:
                throw new InvalidOperationException("The state was checked to be neither unknown nor an AI turn.");
            }
        }
    }

    /// <summary>Paints the treasury and the castle/town counts, <c>_drawKingdomInfo()</c>.</summary>
    private void DrawKingdomInfo(int offsetY)
    {
        Point pos = Area.Position;
        Kingdom kingdom = _world.GetKingdom(Settings.Get().Players.CurrentColor);

        pos = new Point(pos.X, pos.Y + offsetY + 3);

        if (_sprites.GetSprite(IcnType.Ressmall, 0) is { } smallResources)
        {
            Blit(smallResources, pos.X + 6, pos.Y);
        }

        pos = new Point(pos.X, pos.Y + 27);

        var text = new Text(kingdom.CountCastles().ToString(CultureInfo.InvariantCulture), FontType.SmallWhite());
        DrawCentred(text, pos.X + 26, pos.Y);

        text.Set(kingdom.CountTowns().ToString(CultureInfo.InvariantCulture), FontType.SmallWhite());
        DrawCentred(text, pos.X + 78, pos.Y);

        text.Set(kingdom.Treasury.Get(Resource.Gold).ToString(CultureInfo.InvariantCulture), FontType.SmallWhite());
        DrawCentred(text, pos.X + 122, pos.Y);

        pos = new Point(pos.X, pos.Y + 30);

        text.Set(ArmyText.AbbreviateNumber((uint)kingdom.Treasury.Get(Resource.Wood)), FontType.SmallWhite());
        DrawCentred(text, pos.X + 15, pos.Y);

        text.Set(ArmyText.AbbreviateNumber((uint)kingdom.Treasury.Get(Resource.Mercury)), FontType.SmallWhite());
        DrawCentred(text, pos.X + 37, pos.Y);

        text.Set(ArmyText.AbbreviateNumber((uint)kingdom.Treasury.Get(Resource.Ore)), FontType.SmallWhite());
        DrawCentred(text, pos.X + 60, pos.Y);

        text.Set(ArmyText.AbbreviateNumber((uint)kingdom.Treasury.Get(Resource.Sulfur)), FontType.SmallWhite());
        DrawCentred(text, pos.X + 84, pos.Y);

        text.Set(ArmyText.AbbreviateNumber((uint)kingdom.Treasury.Get(Resource.Crystal)), FontType.SmallWhite());
        DrawCentred(text, pos.X + 108, pos.Y);

        text.Set(ArmyText.AbbreviateNumber((uint)kingdom.Treasury.Get(Resource.Gems)), FontType.SmallWhite());
        DrawCentred(text, pos.X + 130, pos.Y);
    }

    /// <summary>Paints the month, the week and the day, <c>_drawDayInfo()</c>.</summary>
    private void DrawDayInfo(int offsetY)
    {
        Rect pos = Area;

        uint day = _world.Day;
        uint week = _world.Week;
        uint month = _world.Month;
        IcnType icnType = EvilInterface ? IcnType.Sunmoone : IcnType.Sunmoon;

        uint icnId = day > 1 ? 0 : ((week - 1) % 4) + 1;

        // The very first day of the game has its own frame.
        if (day == 1 && week == 1 && month == 1)
        {
            icnId = 0;
        }

        if (_sprites.GetSprite(icnType, (int)icnId) is { } moon)
        {
            Blit(moon, pos.X, pos.Y + 1 + offsetY);
        }

        var text = new Text(
            string.Create(CultureInfo.InvariantCulture, $"Month: {month} Week: {week}"), FontType.SmallWhite());
        text.Draw(pos.X + ((pos.Width - text.Width()) / 2), pos.Y + 32 + offsetY, _display);

        text.Set(string.Create(CultureInfo.InvariantCulture, $"Day: {day}"), FontType.NormalWhite());
        text.Draw(pos.X + ((pos.Width - text.Width()) / 2), pos.Y + 48 + offsetY, _display);
    }

    /// <summary>Paints the «you found some resource» message, <c>_drawResourceInfo()</c>.</summary>
    private void DrawResourceInfo(int offsetY)
    {
        Rect pos = Area;

        string message = "You find a small\nquantity of " + ResourceText.Of(_lastResource) + ".";
        var text = new Text(message, FontType.SmallWhite());
        text.Draw(pos.X, pos.Y + 6 + offsetY, pos.Width, _display);

        int textHeight = text.Height(pos.Width);

        if (_sprites.GetSprite(IcnType.Resource, ResourceIconIndex.Of(_lastResource)) is { } icon)
        {
            Blit(icon, pos.X + ((pos.Width - icon.Width) / 2), pos.Y + 6 + offsetY + textHeight);

            textHeight = icon.Height;
        }

        text.Set(_lastResourceCount.ToString(CultureInfo.InvariantCulture), FontType.SmallWhite());
        text.Draw(pos.X + ((pos.Width - text.Width()) / 2),
            pos.Y + offsetY + (text.Height() * 2) + textHeight + 10, _display);
    }

    /// <summary>Paints the troops of the focused hero or castle, <c>_drawArmyInfo()</c>.</summary>
    private void DrawArmyInfo(int offsetY)
    {
        ArmyText? army = InterfaceFocus.GetFocusHeroes()?.Army ?? InterfaceFocus.GetFocusCastle()?.Army;

        if (army is null)
        {
            return;
        }

        Rect pos = Area;
        ArmyText.DrawMultipleMonsterLines(army, pos.X + 4, pos.Y + 1 + offsetY, 138,
            compact: true, detailedView: true, garrisonView: false, thievesGuildCount: 0,
            output: _display, sprites: _monsters);
    }

    /// <summary>Paints the hourglass of an AI turn, <c>_drawAITurns()</c>.</summary>
    private void DrawAiTurns()
    {
        DrawBackground();

        if (_sprites.GetSprite(IcnType.Hourglas, 0) is not { } glass)
        {
            return;
        }

        Rect statusRoi = Area;

        int posX = statusRoi.X + ((statusRoi.Width - glass.Width) / 2);
        int posY = statusRoi.Y + ((statusRoi.Height - glass.Height) / 2);

        Blit(glass, posX, posY);

        uint colorIndex = Settings.Get().Players.CurrentColor switch
        {
            PlayerColor.Blue => 0,
            PlayerColor.Green => 1,
            PlayerColor.Red => 2,
            PlayerColor.Yellow => 3,
            PlayerColor.Orange => 4,
            PlayerColor.Purple => 5,

            // The reference asserts: a new player color needs a crest frame here.
            _ => uint.MaxValue,
        };

        if (colorIndex == uint.MaxValue)
        {
            return;
        }

        if (_sprites.GetSprite(IcnType.Brcrest, (int)colorIndex) is { } crest)
        {
            Blit(crest, posX + 2, posY + 2);
        }

        posX += glass.Width - 3;

        uint animationIndex = GameAnimation.AdventureMapAnimationIndex - _grainsAnimationIndexOffset;

        // The first ten frames start the flow, the next ten loop it.
        animationIndex = animationIndex < 10 ? 11 + (animationIndex % 10) : 21 + (animationIndex % 10);

        if (_sprites.GetSprite(IcnType.Hourglas, (int)animationIndex) is { } grains)
        {
            Blit(grains, posX - grains.Width - grains.OffsetX, posY + grains.OffsetY);
        }

        if (_sprites.GetSprite(IcnType.Hourglas, (int)(1 + (_aiTurnProgress % 10))) is { } sand)
        {
            Blit(sand, posX - sand.Width - sand.OffsetX, posY + sand.OffsetY);
        }
    }

    /// <summary>Paints the stone background, <c>_drawBackground()</c>.</summary>
    /// <remarks>
    /// With the interface visible and enough room below the panel, the middle row of the stone frame is repeated so
    /// the panel keeps its texture while it stretches to the bottom of the screen.
    /// </remarks>
    private void DrawBackground()
    {
        if (StoneFrame() is not { } stone)
        {
            return;
        }

        Rect pos = Area;

        if (!HideInterfaceEnabled && _display.Height - GameBorder.BorderWidth - stone.Height > pos.Y)
        {
            const int startY = 11;
            const int copyHeight = 46;

            Copy(stone, 0, 0, pos.X, pos.Y, stone.Width, startY);

            int count = (pos.Height - (stone.Height - copyHeight)) / copyHeight;

            for (int i = 0; i < count; ++i)
            {
                Copy(stone, 0, startY, pos.X, pos.Y + (copyHeight * i) + startY, stone.Width, copyHeight);
            }

            Copy(stone, 0, startY, pos.X, pos.Y + pos.Height - (stone.Height - startY),
                stone.Width, stone.Height - startY);
        }
        else
        {
            Copy(stone, 0, 0, pos.X, pos.Y, stone.Width, stone.Height);
        }
    }

    /// <summary>Redraws the hourglass while the AI plays, <c>drawAITurnProgress()</c>.</summary>
    /// <param name="progressValue">Progress of the AI turn, <c>0</c> at its start.</param>
    /// <remarks>
    /// The reference also pumps the event queue and presents the frame; both belong to the host in the port, so this
    /// method only throttles on the map animation delay and queues the repaints.
    /// </remarks>
    public void DrawAiTurnProgress(uint progressValue)
    {
        bool updateProgress = progressValue != _aiTurnProgress;
        bool isMapAnimation = GameDelays.ValidateAnimationDelay(DelayType.MapsDelay);

        if (!updateProgress && !isMapAnimation)
        {
            return;
        }

        if (updateProgress)
        {
            if (progressValue == 0)
            {
                // A new turn starts the grains from the first frame.
                _grainsAnimationIndexOffset = GameAnimation.AdventureMapAnimationIndex;
            }

            _aiTurnProgress = progressValue;
        }

        if (isMapAnimation)
        {
            GameAnimation.UpdateAdventureMapAnimationIndex();

            _interface.SetRedraw(RedrawMask.GameArea);
        }

        _interface.RedrawInterface(RedrawMask.Status);
    }
}

/// <summary>Which of the five paintings the panel shows, <c>StatusType</c> (<c>interface_status.h:33-43</c>).</summary>
public enum StatusType
{
    /// <summary>Nothing yet, <c>STATUS_UNKNOWN</c>.</summary>
    Unknown = 0,

    /// <summary>The date, <c>STATUS_DAY</c>.</summary>
    Day,

    /// <summary>Castles, towns and the treasury, <c>STATUS_FUNDS</c>.</summary>
    Funds,

    /// <summary>The troops of the focused hero or castle, <c>STATUS_ARMY</c>.</summary>
    Army,

    /// <summary>A found resource, <c>STATUS_RESOURCE</c>.</summary>
    Resource,

    /// <summary>The hourglass of an AI turn, <c>STATUS_AITURN</c>.</summary>
    AiTurn,
}

/// <summary>What the caller of <see cref="StatusPanel.QueueEventProcessing"/> has to do, the actions the reference performs itself.</summary>
public enum StatusPanelAction
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>A drag of the floating window started; it is in <see cref="StatusPanel.Dragging"/>.</summary>
    DragStarted,

    /// <summary>The state changed and the panel has to be repainted.</summary>
    StateCycled,

    /// <summary>The «Status Window» message box has to be shown.</summary>
    ShowTooltip,
}
