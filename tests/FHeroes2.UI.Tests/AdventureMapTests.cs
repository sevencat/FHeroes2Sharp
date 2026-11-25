using FHeroes2.Assets;
using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Audio;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;
using Xunit;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The adventure screen itself: the layout <c>AdventureMap::reset()</c> produces, the repaint mask
/// <c>AdventureMap::redraw()</c> drains and the cursor it shows (<c>game/game_interface.cpp:63-173</c>,
/// <c>game/game_startgame.cpp:477-730</c>).
/// </summary>
/// <remarks>
/// The panels resolve the colours of their buttons through <c>KB.PAL</c>, so these tests need the game data and
/// stay silent without it; they swap <see cref="GameAssets.Current"/> and therefore share the archive collection.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class AdventureMapTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly AssetBundle? _previousAssets;
    private readonly World _previousWorld;

    public AdventureMapTests()
    {
        Settings.Reset();
        _previousWorld = World.Current;
        _previousAssets = GameAssets.Current;
        _assets = OpenGameData();
    }

    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        GameAssets.Current = _previousAssets;
        World.Current = _previousWorld;
        CursorState.Current = null;
        MapSprites.Reset();
    }

    [Fact]
    public void Create_BuildsEveryPanelOfTheReferenceConstructor()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // AdventureMap::AdventureMap() constructs the icons bar, the status panel, the buttons bar, the hide-interface
        // strip and loads the border artwork; only the radar comes from the base interface.
        Assert.NotNull(map.GetIconsPanel());
        Assert.NotNull(map.GetStatusPanel());
        Assert.NotNull(map.GetButtonsPanel());
        Assert.NotNull(map.GetControlPanel());
        Assert.NotNull(map.GetRadar());
    }

    [Fact]
    public void Create_WithoutGameData_ReturnsNull()
    {
        Settings.Reset();

        AdventureMap? map = AdventureMap.TryCreate(new PixelBuffer(640, 480), null,
            new AudioManager(new StubAudioDevice(), new StubAudioSource(), Settings.Get()));

        Assert.Null(map);
    }

    [Fact]
    public void Create_WithoutASoundCard_BuildsTheScreen()
    {
        if (!HasGameData)
        {
            return;
        }

        // CreateMap 把全局世界与精灵都装好，随后的这一次只换掉声卡：参考在 Audio::isValid() == false 时照常开局，
        // 只是每一声、每一轨都被丢弃。
        _ = CreateMap();

        Assert.NotNull(AdventureMap.TryCreate(new PixelBuffer(640, 480), _assets, null));
    }

    [Fact]
    public void EventViewWorld_WithoutAttachedView_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // 参考的 EventViewWorld() 把 *this 交给 ViewWorld::ViewWorldWindow()，从界面取地图与视口；端口要先
        // AttachView。没接视图时它什么都不做，而不是去问宿主——IAdventureHost 已经不再有这一项。
        map.EventViewWorld();
    }

    [Fact]
    public void AttachView_WithoutView_Throws()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Assert.Throws<ArgumentNullException>(() => map.AttachView(null!, new Rect(0, 0, 1, 1)));
    }

    [Fact]
    public void IsHeroMovementInterrupted_WithoutInput_IsFalse()
    {
        Assert.False(AdventureMap.IsHeroMovementInterrupted(new LocalEvent()));
    }

    [Fact]
    public void IsHeroMovementInterrupted_WithAKeyPress_IsTrue()
    {
        // HumanTurn():1135 —— 走路期间按键就是「停下」。
        Assert.True(AdventureMap.IsHeroMovementInterrupted(Pressed(Key.Escape)));
    }

    [Fact]
    public void IsHeroMovementInterrupted_WithALeftClick_IsTrue()
    {
        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, new Point(10, 10));
        events.OnMouseButton(false, MouseButtonType.Left, new Point(10, 10));

        Assert.True(AdventureMap.IsHeroMovementInterrupted(events));
    }

    [Fact]
    public void IsHeroMovementInterrupted_WithARightPress_IsTrue()
    {
        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        Assert.True(AdventureMap.IsHeroMovementInterrupted(events));
    }

    [Fact]
    public void ProcessHotKeys_WithoutAKeyPress_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        bool askedToQuit = false;

        AdventureEventResult result = map.ProcessHotKeys(new LocalEvent(), () =>
        {
            askedToQuit = true;

            return AdventureEventResult.QuitGame;
        });

        Assert.Equal(AdventureEventResult.Cancel, result);
        Assert.False(askedToQuit);
    }

    [Fact]
    public void ProcessHotKeys_TogglingTheControlPanel_KeepsThePlayerOnTheMap()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        Settings settings = Settings.Get();

        // 五个 show/hide 事件只在「隐藏界面」模式下动设置（interface_events.cpp:582-590）。
        settings.SetHideInterface(true);
        settings.SetShowControlPanel(true);

        AdventureEventResult result = map.ProcessHotKeys(
            Pressed(GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldToggleControlPanel)),
            () => AdventureEventResult.QuitGame);

        Assert.Equal(AdventureEventResult.Cancel, result);
        Assert.False(settings.ShowControlPanel);
    }

    [Theory]
    [InlineData(HotKeyEvent.GlobalAppQuit)]
    [InlineData(HotKeyEvent.DefaultCancel)]
    public void ProcessHotKeys_QuitKeys_AskTheHostAndReportItsAnswer(HotKeyEvent hotKey)
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        int asked = 0;

        AdventureEventResult result = map.ProcessHotKeys(
            Pressed(GameHotKeys.GetHotKeyForEvent(hotKey)),
            () =>
            {
                ++asked;

                return AdventureEventResult.QuitGame;
            });

        // 参考把 Game::processExitEvent() 的返回值原样当结果（:1143-1145）：玩家取消时它照样是 CANCEL，
        // 所以端口把决定权整个交给宿主。
        Assert.Equal(1, asked);
        Assert.Equal(AdventureEventResult.QuitGame, result);
    }

    [Fact]
    public void ProcessPanels_WithoutInput_ReportsNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        AdventurePanelResult result = map.ProcessPanels(new LocalEvent());

        Assert.Equal(AdventureEventResult.Cancel, result.Mode);
        Assert.Null(result.Drag);
    }

    [Fact]
    public void ProcessPanels_ClickingTheRadarTileOfTheControlPanel_HidesTheRadar()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        Settings settings = Settings.Get();

        // The control panel only answers in the hidden-interface mode (game_startgame.cpp:1390).
        settings.SetHideInterface(true);
        settings.SetShowRadar(true);

        // Reset() lays the panels out for the current mode, the way rebildAdventureMap() does.
        map.Reset();

        ControlPanel? control = map.GetControlPanel();
        Assert.NotNull(control);

        Point centre = control.RadarRect.Position + new Point(control.RadarRect.Width / 2, control.RadarRect.Height / 2);
        LocalEvent events = Clicked(centre);

        AdventurePanelResult result = map.ProcessPanels(events);

        Assert.Equal(AdventureEventResult.Cancel, result.Mode);
        Assert.False(settings.IsShowRadar);
    }

    [Fact]
    public void ProcessPanels_GrabbingTheBorderOfTheButtonsPanel_HandsTheDragToTheCaller()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        Settings settings = Settings.Get();

        settings.SetHideInterface(true);
        settings.SetShowButtons(true);
        map.Reset();

        Interface.ButtonsPanel? buttons = map.GetButtonsPanel();
        Assert.NotNull(buttons);
        Assert.True(buttons.HideInterfaceEnabled);

        Rect area = buttons.GetRect;
        Point grab = new(area.X + (area.Width / 2), area.Y + 1);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, grab);

        AdventurePanelResult result = map.ProcessPanels(events);

        // The reference would run the loop here; the port hands the drag over and the caller owns it.
        Assert.NotNull(result.Drag);
        Assert.Equal(AdventureEventResult.Cancel, result.Mode);
    }

    [Fact]
    public void ProcessTurnFrame_WithoutInput_ReportsNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        AdventurePanelResult result = map.ProcessTurnFrame(new LocalEvent(), () => AdventureEventResult.QuitGame);

        Assert.Equal(AdventureEventResult.Cancel, result.Mode);
        Assert.Null(result.Drag);
        Assert.False(map.IsHeroMoving);
    }

    [Fact]
    public void ProcessTurnFrame_WhenTheMapDelayPassed_AdvancesTheAnimationIndex()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // 测试环境里的延时表默认全是 0 毫秒（任何一次校验都算到点），先按游戏的启动流程填上真实长度。
        GameDelays.InitAnimateDelays();

        uint before = GameAnimation.AdventureMapAnimationIndex;

        // MAPS_DELAY 到了才推一帧（game_startgame.cpp:1567-1572）；这里让它一定到点。
        GameDelays.PassAnimationDelay(DelayType.MapsDelay);

        map.ProcessTurnFrame(new LocalEvent(), () => AdventureEventResult.QuitGame);

        Assert.Equal(before + 1, GameAnimation.AdventureMapAnimationIndex);
    }

    [Fact]
    public void ProcessTurnFrame_WhenTheMapDelayHasNotPassed_KeepsTheAnimationIndex()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        GameDelays.InitAnimateDelays();

        uint before = GameAnimation.AdventureMapAnimationIndex;

        // 刚重设过的 250 毫秒延时这一帧一定还没到点，所以不推帧。
        map.ProcessTurnFrame(new LocalEvent(), () => AdventureEventResult.QuitGame);

        Assert.Equal(before, GameAnimation.AdventureMapAnimationIndex);
    }

    [Fact]
    public void ProcessTurnFrame_WithAQuitKey_ReportsTheHostsAnswer()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        int asked = 0;

        AdventurePanelResult result = map.ProcessTurnFrame(
            Pressed(GameHotKeys.GetHotKeyForEvent(HotKeyEvent.GlobalAppQuit)),
            () =>
            {
                ++asked;

                return AdventureEventResult.QuitGame;
            });

        // 键盘块排在面板与动画之前（game_startgame.cpp:1141），它的结果直接结束这一帧。
        Assert.Equal(1, asked);
        Assert.Equal(AdventureEventResult.QuitGame, result.Mode);
        Assert.Null(result.Drag);
    }

    [Fact]
    public void AnimateHeroMovement_WithoutAWalkingHero_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        AdventureEventResult result = map.AnimateHeroMovement(new LocalEvent());

        Assert.Equal(AdventureEventResult.Cancel, result);
        Assert.False(map.IsHeroMoving);
    }

    [Fact]
    public void ScrollGameArea_WithoutScrolling_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        map.ScrollGameArea(new LocalEvent());
    }

    [Fact]
    public void SetCenter_PutsTheMiddleOfTheTileInTheMiddleOfTheViewport()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        GameArea area = map.GetGameArea();

        // The area is laid out by BaseInterface::Reset() when the interface is resized; the tests drive it directly.
        area.SetAreaPosition(16, 16, 608, 448);

        Point tile = new(20, 24);

        area.SetCenter(tile);

        Point corner = area.GetRelativeTilePosition(tile);
        Point middle = new(area.Roi.X + area.MiddlePoint.X, area.Roi.Y + area.MiddlePoint.Y);

        // Interface::GameArea::_setCenterToTile() puts the centre of the viewport on the middle of the tile: this is
        // the "the map is centred on the hero" of a new turn, and it is what keeps a focus hero in the middle.
        Assert.Equal(middle.X, corner.X + (GameBorder.TileWidth / 2));
        Assert.Equal(middle.Y, corner.Y + (GameBorder.TileWidth / 2));
    }

    [Fact]
    public void FinishHumanTurn_WithoutInput_KeepsTheTurnGoing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Assert.Equal(AdventureEventResult.Cancel, map.FinishHumanTurn(AdventureEventResult.Cancel, false));
    }

    [Fact]
    public void FinishHumanTurn_ForAVanquishedKingdom_EndsTheTurn()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // 参考的 `if ( res == CANCEL && !myKingdom.isPlay() ) res = END_TURN;`（game_startgame.cpp:1557）。
        Assert.Equal(AdventureEventResult.EndTurn, map.FinishHumanTurn(AdventureEventResult.Cancel, true));
    }

    [Fact]
    public void FinishHumanTurn_WithAQuitRequest_PassesItThrough()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Assert.Equal(AdventureEventResult.QuitGame, map.FinishHumanTurn(AdventureEventResult.QuitGame, false));
    }

    [Fact]
    public void FinishHumanTurn_WithEndTurn_ReportsEndTurn()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // 结束回合时收起焦点英雄的路线；没有焦点英雄时只是原样返回。
        Assert.Equal(AdventureEventResult.EndTurn, map.FinishHumanTurn(AdventureEventResult.EndTurn, false));
    }

    [Fact]
    public void ProcessHotKeys_AKeyOutsideTheChain_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // 回车绑在 DefaultOkay 上，键盘块里没有它，所以这一帧什么都不该发生。
        AdventureEventResult result = map.ProcessHotKeys(
            Pressed(GameHotKeys.GetHotKeyForEvent(HotKeyEvent.DefaultOkay)),
            () => AdventureEventResult.QuitGame);

        Assert.Equal(AdventureEventResult.Cancel, result);
    }

    [Fact]
    public void ControlPanelAreaForInput_IsTheStripOfTheHideInterfaceMode()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        ControlPanel panel = Assert.IsType<ControlPanel>(map.GetControlPanel());
        Rect? area = map.ControlPanelAreaForInput;

        // The viewport asks for this area to swallow the clicks meant for the strip: it must be the strip itself,
        // not a copy of it.
        Assert.NotNull(area);
        Assert.Equal(panel.Area, area.Value);
    }

    [Fact]
    public void Reset_WithTheClassicLayout_StacksThePanelsDownTheRightEdge()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Settings.Get().SetHideInterface(false);

        map.Reset();

        Rect radar = map.GetRadar().GetRect;
        Rect icons = map.GetIconsPanel().GetRect;
        Rect status = map.GetStatusPanel().GetRect;

        // _radar.SetPos( px, borderWidth ) and then each panel below the previous one, all at the same px.
        Assert.Equal(radar.X, icons.X);
        Assert.Equal(radar.X, status.X);
        Assert.True(radar.Y < icons.Y);
        Assert.True(icons.Y < status.Y);
    }

    [Fact]
    public void Reset_WithTheHideInterfaceMode_UsesTheStoredFloatingPositions()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Settings settings = Settings.Get();
        settings.SetHideInterface(true);
        settings.SetPosRadar(new Point(10, 20));
        settings.SetPosButtons(new Point(30, 40));
        settings.SetPosIcons(new Point(50, 60));
        settings.SetPosStatus(new Point(70, 80));

        map.Reset();

        Rect radar = map.GetRadar().GetRect;
        Rect icons = map.GetIconsPanel().GetRect;
        Rect status = map.GetStatusPanel().GetRect;

        // The docking layout puts every panel at the same x, so a strictly increasing x proves that the stored
        // positions were used. The buttons panel is not built here; its position only has to be valid.
        Assert.True(radar.X < icons.X);
        Assert.True(icons.X < status.X);
        Assert.True(radar.Y < icons.Y);
        Assert.True(icons.Y < status.Y);
    }

    [Fact]
    public void Reset_WithoutStoredPositions_DocksTheFloatingPanelsFromTheTopLeftCorner()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // The configuration default is (-1, -1) for every floating panel, which reset() reads as "never moved".
        Settings.Get().SetHideInterface(true);

        map.Reset();

        Rect radar = map.GetRadar().GetRect;
        Rect icons = map.GetIconsPanel().GetRect;
        Rect status = map.GetStatusPanel().GetRect;

        Assert.True(radar.X < icons.X);
        Assert.Equal(icons.X, status.X);
        Assert.True(radar.Y < icons.Y);
        Assert.True(icons.Y < status.Y);
    }

    [Fact]
    public void RedrawLocker_CollectsTheRequestsInsteadOfServingThem()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Settings.Get().SetHideInterface(false);

        using (new AdventureMap.RedrawLocker(map))
        {
            map.RedrawInterface(Redraw.Buttons);

            Assert.True(map.GetRedrawMask().HasFlag(Redraw.Buttons));
        }

        // Released: the queued parts are served and the mask is drained.
        map.RedrawInterface(Redraw.None);

        Assert.Equal(Redraw.None, map.GetRedrawMask());
    }

    [Fact]
    public void UpdateCursor_WithoutARegisteredCursorDoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        CursorState.Current = null;

        map.UpdateCursor(-1);
    }

    [Fact]
    public void GetCursorTileIndex_IsThePointerOutsideTheMap()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Assert.Equal(CursorTheme.Pointer, map.GetCursorTileIndex(-1));
        Assert.Equal(CursorTheme.Pointer, map.GetCursorTileIndex(int.MaxValue));
    }

    [Fact]
    public void MouseCursorAreaClickLeft_WithoutACursorTheme_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        CursorState.Current = null;

        // No cursor means no theme, and every branch of the reference switch needs one.
        map.MouseCursorAreaClickLeft(0);
        map.MouseCursorAreaClickLeft(-1);

        Assert.Empty(LastQuickInfo);
    }

    [Fact]
    public void MouseCursorAreaPressRight_OutsideTheMap_DoesNotAskForAQuickInfo()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        map.MouseCursorAreaPressRight(-1);
        map.MouseCursorAreaPressRight(int.MaxValue);

        Assert.Empty(LastQuickInfo);
    }

    [Fact]
    public void MouseCursorAreaPressRight_OnAnEmptyTile_AsksForTheTileQuickInfo()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // The map is generated without objects, so every tile is "the default branch": fog or not, the reference
        // shows the tile window.
        map.MouseCursorAreaPressRight(0);

        // 空瓦片走 Dialog::QuickInfo( tile )：一个标准文字框，不是城堡/英雄的大面板。
        StandardPopupWindow popup = Assert.IsType<StandardPopupWindow>(Assert.Single(LastQuickInfo));
        Assert.True(popup.Area.Width > 0 && popup.Area.Height > 0);
    }

    [Fact]
    public void MouseCursorAreaLongPressLeft_WithoutAHero_KeepsTheFocus()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // An empty tile has no hero to switch to; the reference's EventSwitchFocusedHero() returns right away.
        map.MouseCursorAreaLongPressLeft(0);
        map.MouseCursorAreaLongPressLeft(-1);

        Assert.Null(InterfaceFocus.GetFocusHeroes());
    }

    [Fact]
    public void RedrawInterface_WithTheGameData_PaintsTheWholeScreen()
    {
        if (!HasGameData)
        {
            return;
        }

        InitializeMapSprites();

        AdventureMap map = CreateMap();

        Settings.Get().SetHideInterface(false);

        map.Reset();

        // Interface::AdventureMap::StartGame() (game_startgame.cpp:740-750) prepares the first frame this way: the
        // radar is generated, the icons bar is emptied and the status panel is reset. The turn loop itself is not
        // ported, so the scene has to do this by hand.
        map.GetRadar().Build(World.Current);
        map.GetRadar().SetHide(true);
        map.GetIconsPanel().HideIcons(HeroesCastlesIcons.Any);
        map.GetStatusPanel().Reset();

        PixelBuffer display = LastDisplay!;
        display.Fill(0);

        // game_interface.cpp:154-247: this one call paints the border, the radar, the map viewport, the icons bar,
        // the buttons bar and the status panel.
        map.RedrawInterface(Redraw.All);

        int painted = display.Pixels.Count(pixel => pixel != 0);

        Assert.True(painted > 1000, $"Only {painted} of {display.Pixels.Length} pixels were painted.");
    }

    [Fact]
    public void RedrawInterface_WithTheHideInterfaceMode_PaintsTheFloatingPanelsOverTheMap()
    {
        if (!HasGameData)
        {
            return;
        }

        InitializeMapSprites();

        AdventureMap map = CreateMap();

        Settings settings = Settings.Get();
        settings.SetHideInterface(true);
        settings.SetShowControlPanel(true);
        settings.SetShowRadar(true);
        settings.SetShowIcons(true);
        settings.SetShowButtons(true);
        settings.SetShowStatus(true);

        map.Reset();

        // Same preparation as StartGame(), except that the radar is left unfolded so that this frame regenerates the
        // map image instead of painting the folded logo.
        map.GetRadar().Build(World.Current);
        map.GetRadar().SetHide(false);
        map.GetIconsPanel().HideIcons(HeroesCastlesIcons.Any);
        map.GetStatusPanel().Reset();

        // The floating panels are painted whenever they are shown, and the control panel is alpha blended over the
        // map right after the viewport, so this frame needs the game palette as well as the artwork.
        map.RedrawInterface(Redraw.All);

        PixelBuffer display = LastDisplay!;
        int painted = display.Pixels.Count(pixel => pixel != 0);

        Assert.True(painted > 1000, $"Only {painted} of {display.Pixels.Length} pixels were painted.");
    }

    /// <summary>Whether the archives could be opened; every test here needs them.</summary>
    private bool HasGameData => _assets is not null;

    /// <summary>
    /// The quick info boxes the screen asked for, taken instead of the blocking loop of
    /// <see cref="QuickInfoPopup"/>: a test cannot wait for a right button to be released.
    /// </summary>
    private List<IQuickInfoPanel> LastQuickInfo { get; } = new();

    /// <summary>The frame the last <see cref="CreateMap"/> painted into.</summary>
    private PixelBuffer? LastDisplay { get; set; }

    /// <summary>A frame in which <paramref name="key"/> was just pressed.</summary>
    private static LocalEvent Pressed(Key key)
    {
        LocalEvent events = new();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }

    /// <summary>A frame in which the left button was pressed and released on <paramref name="position"/>.</summary>
    private static LocalEvent Clicked(Point position)
    {
        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    private AdventureMap CreateMap()
    {
        World world = new();
        world.GenerateUninitializedMap(72);
        world.NewDay();
        World.Current = world;

        Settings.Get().Players.SetCurrentColor(PlayerColor.Blue);

        PixelBuffer display = new(640, 480);
        LastDisplay = display;

        LastQuickInfo.Clear();

        // The screen now builds its own panels, the way the reference constructor does.
        AdventureMap? map = AdventureMap.TryCreate(display, _assets,
            new AudioManager(new StubAudioDevice(), new StubAudioSource(), Settings.Get()));

        if (map is null)
        {
            throw new InvalidOperationException("The game data does not hold the adventure screen artwork.");
        }

        // 速览的阻塞循环在测试里没有事件循环可泵，接管成只记录面板。
        map.Dialogs.PresentQuickInfo = LastQuickInfo.Add;

        return map;
    }

    /// <summary>
    /// Installs the bundle and the palette the map renderer reads out of its process wide globals, which
    /// <c>GameHost.LoadGameData()</c> does once at start up.
    /// </summary>
    private void InitializeMapSprites()
    {
        byte[]? paletteData = _assets?.TryRead("KB.PAL");

        MapSprites.Initialize(_assets, paletteData is null ? null : Palette.FromGamePalette(paletteData));
    }

    /// <summary>
    /// Opens the game archive and hands it to the asset layer; the button colours resolve through <c>KB.PAL</c>,
    /// so every test here needs real data.
    /// </summary>
    private static AssetBundle? OpenGameData()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return null;
        }

        AssetBundle? bundle = AssetBundle.Open(directory);
        GameAssets.Current = bundle;

        return bundle;
    }

    /// <summary>A sound card that never makes a noise.</summary>
    private sealed class StubAudioDevice : IAudioDevice
    {
        public bool IsValid => false;

        public bool IsMusicPlaying => false;

        public int Play(ReadOnlySpan<byte> wave, bool loop, AudioLoopEffectInfo? position = null) => -1;

        public void SetPosition(int channel, short angle, byte distance)
        {
        }

        public void StopSound(int channel)
        {
        }

        public void StopAllSounds()
        {
        }

        public bool IsSoundPlaying(int channel) => false;

        public bool PlayMusicFromDatabase(ulong uid, MusicPlaybackMode mode) => false;

        public void PlayMusic(ulong uid, ReadOnlySpan<byte> midi, MusicPlaybackMode mode)
        {
        }

        public void PlayMusic(ulong uid, string filePath, MusicPlaybackMode mode)
        {
        }

        public void StopMusic()
        {
        }

        public void SetMusicVolume(int percent)
        {
        }

        public void SetSoundVolume(int percent)
        {
        }
    }

    /// <summary>An archive set without a single tune.</summary>
    private sealed class StubAudioSource : IAudioSource
    {
        public bool IsExpansionArchiveAvailable => false;

        public IReadOnlyList<string> MusicDirectories => Array.Empty<string>();

        public byte[]? TryGetSoundBody(M82Sound sound) => null;

        public byte[]? TryGetMidi(XmiTrack track, bool ignoreExpansion) => null;
    }
}
