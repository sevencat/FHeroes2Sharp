using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using WorldMap = FHeroes2.Game.World;

namespace FHeroes2.UI.Interface;

/// <summary>
/// 一帧的输入：<c>Interface::AdventureMap::HumanTurn()</c> 的循环体里跟游戏逻辑无关的那一半
/// （<c>game/game_startgame.cpp:1127-1405</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>HumanTurn()</c> 是一个六百行的循环：等事件、处理热键、把鼠标派给五个面板、驱动英雄移动动画、
/// 收尾开战或进城。端口把「一帧」拆成三块，调用方（场景）在循环里按顺序调：
/// </para>
/// <list type="number">
/// <item><see cref="IsHeroMovementInterrupted"/> —— 英雄正在走时，按任意键或点鼠标是否要他停下（<c>:1130-1138</c>）；</item>
/// <item><see cref="ProcessHotKeys"/> —— 键盘块（<c>:1141-1283</c>），返回要让循环做什么；</item>
/// <item><see cref="ProcessPanels"/> —— 面板派发（<c>:1289-1405</c>）：捕获、叠放次序、边缘卷屏与面板命中；</item>
/// <item><see cref="AnimateHeroMovement"/> —— 英雄移动动画（<c>:1412-1536</c>），带 <see cref="IsHeroMoving"/>；</item>
/// <item><see cref="ScrollGameArea"/> —— 卷屏帧（<c>:1538-1555</c>）；</item>
/// <item><see cref="AnimateMapObjects"/> —— 地图物体动画（<c>:1567-1572</c>）；</item>
/// <item><see cref="FinishHumanTurn"/> —— 返回前的收尾（<c>:1557-1560</c>、<c>:1584-1609</c>）。</item>
/// </list>
/// <para>
/// 这几块按参考的次序串起来就是 <see cref="ProcessTurnFrame"/> 加 <see cref="FinishHumanTurn"/>，调用方一帧只调它们两个。
/// </para>
/// <para>
/// <b>为什么拆开。</b> 参考靠 <c>LocalEvent::Get()</c> 单例和阻塞循环；端口的事件状态是显式参数，阻塞的拖动由
/// 调用方接手（见 <see cref="BorderWindow.QueueEventProcessing"/>）。所以这一层只负责「把输入翻成事件」，
/// 不自己跑循环：拖动以 <see cref="AdventurePanelResult.Drag"/> 的形式交给场景，场景画完地图后再把它画上去。
/// </para>
/// <para>
/// <b>还没搬的部分</b>：循环出口的收尾（开战、进城、胜负判定）与地图物体动画（<c>MAPS_DELAY</c> 那一步），
/// 它们要 <c>world</c> 与存档层配合。
/// </para>
/// </remarks>
public sealed partial class AdventureMap
{
    /// <summary>
    /// How many <c>SCROLL_START_DELAY</c> ticks the cursor has to sit in a scroll margin before the map starts to
    /// scroll, <c>fastScrollStartThreshold</c> (<c>game_startgame.cpp:1073</c>).
    /// </summary>
    private const int FastScrollStartThreshold = 2;

    /// <summary>
    /// How often the cursor was seen in a scroll margin, <c>fastScrollRepeatCount</c>
    /// (<c>game_startgame.cpp:1072</c>): a local of the reference's loop, kept on the instance here because the frame
    /// of the port is a method call.
    /// </summary>
    private int _fastScrollRepeatCount;

    /// <summary>
    /// 英雄这一步的动画还剩多少帧，<c>HumanTurn()</c> 的 <c>heroAnimationFrameCount</c>（<c>:1417</c>）。
    /// </summary>
    private int _heroAnimationFrameCount;

    /// <summary>这一步动画当前该显示的精灵号，<c>heroAnimationSpriteId</c>（<c>:1418</c>）。</summary>
    private int _heroAnimationSpriteId;

    /// <summary>这一步的方向（每帧一像素），<c>heroAnimationOffset</c>（<c>:1419</c>）。</summary>
    private Point _heroAnimationOffset;

    /// <summary>
    /// 焦点英雄是否正在走，<c>isHeroMoving</c>（<c>:1080</c>）：参考放在循环里，端口放在实例上，因为一帧是一次方法调用。
    /// </summary>
    private bool _isHeroMoving;

    /// <summary>
    /// 是否要英雄停下，<c>stopHero</c>（<c>:1081</c>）：他先把当前这一格走完。
    /// </summary>
    private bool _stopHero;

    /// <summary>焦点英雄是不是正在走，<c>isHeroMoving</c>。</summary>
    public bool IsHeroMoving => _isHeroMoving;

    /// <summary>
    /// 英雄走格子时，是否需要让他停下来，<c>HumanTurn()</c> 的 <c>isHeroMoving</c> 分支（<c>:1130-1138</c>）。
    /// </summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <returns>按了任意键，或者点了左键、按了右键时为 <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 参考在这条分支里同时把光标换成沙漏、并且不做别的任何事：走路期间没有热键、面板也不响应。端口的调用方先问
    /// 这一句，为真就跳过 <see cref="ProcessHotKeys"/>。
    /// </remarks>
    public static bool IsHeroMovementInterrupted(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events.IsAnyKeyPressed || events.MouseClickLeft() || events.IsMouseRightButtonPressed;
    }

    /// <summary>
    /// 键盘块，<c>HumanTurn()</c> 的 <c>if ( le.isAnyKeyPressed() )</c> 链（<c>:1141-1283</c>）。
    /// </summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <param name="exitEvent">
    /// 收到退出热键时做的事，参考的 <c>Game::processExitEvent()</c>：它会先弹「确认退出」再决定是否真的退出，
    /// 所以返回的 <see cref="AdventureEventResult"/> 由宿主给（玩家取消时仍是 <see cref="AdventureEventResult.Cancel"/>）。
    /// </param>
    /// <returns>
    /// 要让循环做什么：<see cref="AdventureEventResult.Cancel"/> 表示继续留在地图上。对话框类事件在这一层就地
    /// 兑现（参考同样如此，<c>Event*()</c> 自己开窗口），只有换模式的那几个值往上传。
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="events"/> 或 <paramref name="exitEvent"/> 为 <see langword="null"/>。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 顺序即参考的 <c>else if</c> 链顺序：退出、回合、英雄与城镇切换、存档与读档、四个对话框、视图开关、
    /// 英雄移动与挖宝、八个方向、四个卷屏、默认动作、打开焦点、快速选择英雄。
    /// </para>
    /// <para>
    /// <b>快速选择的差异。</b> 参考把 <c>WORLD_QUICK_SELECT_HERO</c> 的按住分支放在
    /// <c>if ( le.isAnyKeyPressed() )</c> 里面，而 <c>isAnyKeyPressed()</c> 只在按下那一帧为真，所以「按住再点」
    /// 只有在同一帧里才生效——这是参考自身的行为，端口照抄（同样只在按下的那一帧进这条分支）。
    /// </para>
    /// <para>
    /// <b>快速卷屏。</b> 参考的 <c>Game::HotKeyPressEvent()</c> 命中时顺手关掉快速卷屏
    /// （<c>game_hotkeys.cpp:388-392</c>），端口把它留在这一层，见 <see cref="HotKeyPressed"/>。
    /// </para>
    /// </remarks>
    public AdventureEventResult ProcessHotKeys(LocalEvent events, Func<AdventureEventResult> exitEvent)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(exitEvent);

        Settings settings = Settings.Get();
        AdventureEventResult result = AdventureEventResult.Cancel;

        if (!events.IsAnyKeyPressed)
        {
            return result;
        }

        // 冒险地图：全局与默认取消
        if (HotKeyPressed(events, HotKeyEvent.GlobalAppQuit) || HotKeyPressed(events, HotKeyEvent.DefaultCancel))
        {
            return exitEvent();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldEndTurn))
        {
            result = EventEndTurn();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldNextHero))
        {
            EventNextHero();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldNextTown))
        {
            EventNextTown();
        }
        else if (HotKeyPressed(events, HotKeyEvent.MainMenuNewGame))
        {
            result = EventNewGame();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldSaveGame))
        {
            EventSaveGame();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldQuickSave))
        {
            if (!GameSave.QuickSave())
            {
                _dialogs.ShowMessage(string.Empty, "There was an issue during saving.", events);
            }
        }
        else if (HotKeyPressed(events, HotKeyEvent.MainMenuLoadGame))
        {
            result = EventLoadGame();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldFileOptions))
        {
            result = EventFileDialog();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldAdventureOptions))
        {
            result = EventAdventureDialog();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldSystemOptions))
        {
            EventSystemDialog();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldPuzzleMap))
        {
            EventPuzzleMaps();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldScenarioInformation))
        {
            result = EventScenarioInformation();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldCastSpell))
        {
            EventCastSpell();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldKingdomSummary))
        {
            EventKingdomInfo();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldViewWorld))
        {
            EventViewWorld();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldToggleControlPanel))
        {
            EventSwitchShowControlPanel();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldToggleRadar))
        {
            EventSwitchShowRadar();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldToggleButtons))
        {
            EventSwitchShowButtons();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldToggleStatus))
        {
            EventSwitchShowStatus();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldToggleIcons))
        {
            EventSwitchShowIcons();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldStartHeroMovement))
        {
            result = EventHeroMovement();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldDigArtifact))
        {
            result = EventDigArtifact();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldSleepHero))
        {
            EventSwitchHeroSleeping();
        }

        // 英雄移动的八个方向
        else if (HotKeyPressed(events, HotKeyEvent.WorldLeft))
        {
            EventKeyArrowPress(Direction.Left);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldRight))
        {
            EventKeyArrowPress(Direction.Right);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldUp))
        {
            EventKeyArrowPress(Direction.Top);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldDown))
        {
            EventKeyArrowPress(Direction.Bottom);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldUpLeft))
        {
            EventKeyArrowPress(Direction.TopLeft);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldUpRight))
        {
            EventKeyArrowPress(Direction.TopRight);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldDownLeft))
        {
            EventKeyArrowPress(Direction.BottomLeft);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldDownRight))
        {
            EventKeyArrowPress(Direction.BottomRight);
        }

        // 冒险地图的卷屏：拖动卷屏进行中、或者把边缘卷屏关掉时不响应
        else if (HotKeyPressed(events, HotKeyEvent.WorldScrollLeft))
        {
            ScrollByHotKey(ScrollingType.Left);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldScrollRight))
        {
            ScrollByHotKey(ScrollingType.Right);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldScrollUp))
        {
            ScrollByHotKey(ScrollingType.Top);
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldScrollDown))
        {
            ScrollByHotKey(ScrollingType.Bottom);
        }

        // 默认动作、打开焦点
        else if (HotKeyPressed(events, HotKeyEvent.WorldDefaultAction))
        {
            result = EventDefaultAction();
        }
        else if (HotKeyPressed(events, HotKeyEvent.WorldOpenFocus))
        {
            EventOpenFocus();
        }
        else if (GameHotKeys.HotKeyHoldEvent(HotKeyEvent.WorldQuickSelectHero))
        {
            QuickSelectHero(events);
        }

        return result;
    }

    /// <summary>
    /// 卷屏热键，<c>HumanTurn()</c> 的 <c>WORLD_SCROLL_*</c> 分支（<c>:1243-1262</c>）。
    /// </summary>
    /// <param name="direction">往哪边卷。</param>
    private void ScrollByHotKey(ScrollingType direction)
    {
        GameArea gameArea = GetGameArea();

        if (!gameArea.IsDragScroll() && Settings.Get().ScrollSpeed != AdventureMapScrollSpeed.None)
        {
            gameArea.SetScroll(direction);
        }
    }

    /// <summary>
    /// 按住快速选择英雄时的分支，<c>HumanTurn()</c>（<c>:1271-1282</c>）。
    /// </summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <remarks>
    /// 光标正指着可以会面的英雄时，参考把它换成普通指针（<c>GetCursorTileIndex( index )</c>，不带快速选择的
    /// 第二参），再在点击时切换焦点。
    /// </remarks>
    private void QuickSelectHero(LocalEvent events)
    {
        int index = GetGameArea().GetValidTileIdFromPoint(events.CursorPosition);

        if (CursorState.Current?.Theme == CursorTheme.HeroMeet)
        {
            UpdateCursor(index);
        }

        if (events.MouseClickLeft())
        {
            EventSwitchFocusedHero(index);
        }
    }

    /// <summary>
    /// 某个热键这一帧是否被按下，<c>Game::HotKeyPressEvent()</c>（<c>game_hotkeys.cpp:386-394</c>）。
    /// </summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <param name="eventId">热键事件。</param>
    /// <returns>按下时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>差异。</b> 参考从 <c>LocalEvent::Get()</c> 单例里取按键；端口把帧的事件状态当参数接过来（
    /// <see cref="GameHotKeys.HotKeyPressEvent"/> 仍读单例，给不接事件的调用方用）。命中时顺手关掉快速卷屏，
    /// 免得按错键时地图一下跳很远——参考把这一句写在 <c>Game::HotKeyPressEvent()</c> 里，端口按
    /// <see cref="GameHotKeys"/> 文档里的约定留给调用方。
    /// </remarks>
    private bool HotKeyPressed(LocalEvent events, HotKeyEvent eventId)
    {
        if (!events.IsAnyKeyPressed || events.PressedKeyValue != GameHotKeys.GetHotKeyForEvent(eventId))
        {
            return false;
        }

        GetGameArea().SetFastScrollStatus(false);

        return true;
    }

    /// <summary>
    /// 把鼠标派给五个面板，<c>HumanTurn()</c> 的面板块（<c>:1289-1405</c>）。
    /// </summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <returns>要让循环做什么，以及需要调用方接手的拖动。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// <para>
    /// 叠放次序与绘制次序相反：状态面板在最上，然后按钮条、图标栏、雷达、控制面板，最底下是地图区。鼠标被任何一个
    /// 面板捕获时就只喂给它，别的一律不响应（<c>:1291-1325</c>）。
    /// </para>
    /// <para>
    /// <b>拖动的差异。</b> 参考在 <c>BorderWindow::QueueEventProcessing()</c> 里就地跑一个阻塞的拖动循环；端口把
    /// 拖动对象交出来（<see cref="AdventurePanelResult.Drag"/>），场景自己压帧、自己画轮廓，拖完再
    /// <see cref="BorderWindow"/> 的 <c>SetPosition</c>。
    /// </para>
    /// <para>
    /// <b>窗口焦点的差异。</b> 参考只在窗口有焦点时做边缘卷屏（<c>fheroes2::Cursor::isFocusActive()</c>）；端口没有
    /// 这个全局状态——失焦时宿主不投递输入帧——所以这里不判，其余照抄。
    /// </para>
    /// </remarks>
    public AdventurePanelResult ProcessPanels(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Settings settings = Settings.Get();
        GameArea gameArea = GetGameArea();

        // 鼠标被某个面板捕获：这一帧只属于它
        if (_statusPanel.IsMouseCaptured(events))
        {
            ResetCursor();

            return HandleStatusPanel(events);
        }

        if (_buttonsPanel is { } buttons && buttons.IsMouseCaptured(events))
        {
            ResetCursor();

            return HandleButtonsPanel(events);
        }

        if (_iconsPanel.IsMouseCaptured(events))
        {
            ResetCursor();

            return HandleIconsPanel(events);
        }

        if (Radar.IsMouseCaptured(events))
        {
            ResetCursor();

            return HandleRadar(events);
        }

        // 地图区正在被拖着卷屏
        if (gameArea.IsDragScroll())
        {
            gameArea.QueueEventProcessing(events);

            return AdventurePanelResult.Cancel;
        }

        ProcessScrollMargins(events, settings);

        bool hiddenInterface = settings.IsHideInterface;

        if ((!hiddenInterface || settings.IsShowStatus) && events.IsMouseCursorPosInArea(_statusPanel.GetRect))
        {
            ResetCursorIfNoNeedToScroll();

            return HandleStatusPanel(events);
        }

        if ((!hiddenInterface || settings.IsShowButtons) && _buttonsPanel is { } panel
            && events.IsMouseCursorPosInArea(panel.GetRect))
        {
            ResetCursorIfNoNeedToScroll();

            return HandleButtonsPanel(events);
        }

        if ((!hiddenInterface || settings.IsShowIcons) && events.IsMouseCursorPosInArea(_iconsPanel.GetRect))
        {
            ResetCursorIfNoNeedToScroll();

            return HandleIconsPanel(events);
        }

        if ((!hiddenInterface || settings.IsShowRadar) && events.IsMouseCursorPosInArea(Radar.GetRect))
        {
            ResetCursorIfNoNeedToScroll();

            return HandleRadar(events);
        }

        if (hiddenInterface && settings.ShowControlPanel && _controlPanel is { } control
            && events.IsMouseCursorPosInArea(control.Area))
        {
            ResetCursorIfNoNeedToScroll();

            return HandleControlPanel(events);
        }

        if (gameArea.NeedScroll)
        {
            return AdventurePanelResult.Cancel;
        }

        // 光标在地图区里，否则什么都不在
        if (events.IsMouseCursorPosInArea(gameArea.Roi))
        {
            gameArea.QueueEventProcessing(events);

            return AdventurePanelResult.Cancel;
        }

        ResetCursor();

        return AdventurePanelResult.Cancel;
    }

    /// <summary>光标压到屏幕边缘时的卷屏，<c>HumanTurn()</c>（<c>:1327-1363</c>）。</summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <param name="settings">The settings that say whether the edges scroll at all.</param>
    /// <remarks>
    /// 参考先数几帧再开始滚（<c>fastScrollRepeatCount</c> × <c>SCROLL_START_DELAY</c>），这样光标擦过边缘时地图不会
    /// 抖一下；鼠标一离开边缘，计数清零。计数跨帧保留，所以它是字段而不是局部变量。
    /// </remarks>
    private void ProcessScrollMargins(LocalEvent events, Settings settings)
    {
        Point cursor = events.CursorPosition;

        if (settings.ScrollSpeed != AdventureMapScrollSpeed.None)
        {
            ScrollingType direction = ScrollingType.None;

            if (BaseInterface.IsScrollLeft(cursor))
            {
                direction |= ScrollingType.Left;
            }
            else if (BaseInterface.IsScrollRight(cursor, new Size(_display.Width, _display.Height)))
            {
                direction |= ScrollingType.Right;
            }

            if (BaseInterface.IsScrollTop(cursor))
            {
                direction |= ScrollingType.Top;
            }
            else if (BaseInterface.IsScrollBottom(cursor, new Size(_display.Width, _display.Height)))
            {
                direction |= ScrollingType.Bottom;
            }

            if (direction != ScrollingType.None && GetGameArea().IsFastScrollEnabled())
            {
                if (GameDelays.ValidateAnimationDelay(DelayType.ScrollStartDelay) && _fastScrollRepeatCount < FastScrollStartThreshold)
                {
                    ++_fastScrollRepeatCount;
                }

                if (_fastScrollRepeatCount >= FastScrollStartThreshold)
                {
                    GetGameArea().SetScroll(direction);
                }
            }
            else
            {
                _fastScrollRepeatCount = 0;
            }
        }
        else
        {
            _fastScrollRepeatCount = 0;
        }

        // 往边缘推一下就把快速卷屏打开，这样下一次推可以真的滚
        if (!GetGameArea().IsFastScrollEnabled() && GetGameArea().MouseIndicatesFastScroll(cursor))
        {
            GetGameArea().SetFastScrollStatus(true);
        }
    }

    /// <summary>状态面板，<c>:1299-1303</c> 与 <c>:1366-1370</c>。</summary>
    /// <param name="events">本帧的事件状态。</param>
    private AdventurePanelResult HandleStatusPanel(LocalEvent events)
    {
        switch (_statusPanel.QueueEventProcessing(events))
        {
            case StatusPanelAction.DragStarted:
                return new AdventurePanelResult(AdventureEventResult.Cancel, _statusPanel.Dragging);

            case StatusPanelAction.ShowTooltip:
                _dialogs.ShowMessage(string.Empty, "Status Window", events);

                return AdventurePanelResult.Cancel;

            default:
                // 换过状态或者什么都没发生：面板自己已经排好重绘。
                return AdventurePanelResult.Cancel;
        }
    }

    /// <summary>按钮条，<c>:1309</c>、<c>:1375</c> 与 <c>interface_buttons.cpp:130-243</c>。</summary>
    /// <param name="events">本帧的事件状态。</param>
    private AdventurePanelResult HandleButtonsPanel(LocalEvent events)
    {
        if (_buttonsPanel is not { } buttons)
        {
            return AdventurePanelResult.Cancel;
        }

        // 参考先跑基类的边框拖动：拖起来了这一帧就不再判按钮。
        if (Settings.Get().IsShowButtons && ((BorderWindow)buttons).QueueEventProcessing(events) is { } drag)
        {
            return new AdventurePanelResult(AdventureEventResult.Cancel, drag);
        }

        if (buttons.QueueEventProcessing(events) is not { } action)
        {
            return AdventurePanelResult.Cancel;
        }

        if (action.Kind == ButtonsPanelInput.RightClick)
        {
            ShowButtonTooltip(action.Button, events);

            return AdventurePanelResult.Cancel;
        }

        return new AdventurePanelResult(ActivateButton(action.Button, action.Kind), null);
    }

    /// <summary>The <c>Event*()</c> of a clicked button, the chain of <c>interface_buttons.cpp:175-212</c>.</summary>
    /// <param name="button">The button that was clicked.</param>
    /// <param name="kind">Whether it was clicked or held down.</param>
    /// <returns>The game mode the button asks for.</returns>
    private AdventureEventResult ActivateButton(AdventureButton button, ButtonsPanelInput kind)
    {
        switch (button)
        {
            case AdventureButton.NextHero:
                EventNextHero();

                return AdventureEventResult.Cancel;

            case AdventureButton.HeroMovement:
                // 长按是「丢掉路线」，和热键 WORLD_START_HERO_MOVEMENT 的短按不是一件事。
                if (kind == ButtonsPanelInput.LongPress)
                {
                    EventResetHeroPath();

                    return AdventureEventResult.Cancel;
                }

                return EventHeroMovement();

            case AdventureButton.Kingdom:
                EventKingdomInfo();

                return AdventureEventResult.Cancel;

            case AdventureButton.Spell:
                EventCastSpell();

                return AdventureEventResult.Cancel;

            case AdventureButton.EndTurn:
                return EventEndTurn();

            case AdventureButton.Adventure:
                return EventAdventureDialog();

            case AdventureButton.File:
                return EventFileDialog();

            case AdventureButton.System:
                EventSystemDialog();

                return AdventureEventResult.Cancel;

            default:
                return AdventureEventResult.Cancel;
        }
    }

    /// <summary>The right press tooltips of the eight buttons, <c>interface_buttons.cpp:213-240</c>.</summary>
    /// <param name="button">The button under the cursor.</param>
    /// <param name="events">本帧的事件状态。</param>
    private void ShowButtonTooltip(AdventureButton button, LocalEvent events)
    {
        (string Title, string Body) text = button switch
        {
            AdventureButton.NextHero => ("Next Hero", "Select the next Hero."),
            AdventureButton.HeroMovement => (
                "Hero Movement",
                "Start the Hero's movement along the current path or re-visit the object occupied by the Hero. Press and hold this button to reset the Hero's path."),
            AdventureButton.Kingdom => ("Kingdom Summary", "View a summary of your Kingdom."),
            AdventureButton.Spell => ("Cast Spell", "Cast an adventure spell."),
            AdventureButton.EndTurn => ("End Turn", "End your turn and let the computer take its turn."),
            AdventureButton.Adventure => ("Adventure Options", "Bring up the adventure options menu."),
            AdventureButton.File => (
                "File Options",
                "Bring up the file options menu, allowing you to load, save, start a new game or quit."),
            _ => ("System Options", "Bring up the system options menu, allowing you to customize your game."),
        };

        _dialogs.ShowMessage(text.Title, text.Body, events);
    }

    /// <summary>图标栏，<c>:1311-1319</c> 与 <c>:1378-1382</c>。</summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <remarks>
    /// 参考的图标列表在 <c>QueueEventProcessing()</c> 里直接换焦点、开英雄/城堡窗口、弹速览
    /// （<c>interface_icons.cpp:251-270</c>）；端口让列表只报告动作，由这里落到对应的 <c>Event*()</c> 与对话框。
    /// </remarks>
    private AdventurePanelResult HandleIconsPanel(LocalEvent events)
    {
        IconsPanelAction action = _iconsPanel.QueueEventProcessing(
            events,
            GameHotKeys.HotKeyHoldEvent(HotKeyEvent.WorldMoveHeroPortraitUpModifier));

        switch (action.Kind)
        {
            case IconsPanelActionKind.StartedDragging:
                return new AdventurePanelResult(AdventureEventResult.Cancel, _iconsPanel.Dragging);

            case IconsPanelActionKind.FocusHero:
                SetFocus(action.Hero!, false);
                RedrawFocus();

                return AdventurePanelResult.Cancel;

            case IconsPanelActionKind.FocusCastle:
                SetFocus(action.Castle!);
                RedrawFocus();

                return AdventurePanelResult.Cancel;

            case IconsPanelActionKind.OpenHeroDialog:
                _dialogs.OpenHeroesDialog(action.Hero!, events);

                return AdventurePanelResult.Cancel;

            case IconsPanelActionKind.OpenCastleDialog:
                OpenCastleScreenOf(action.Castle!);

                return AdventurePanelResult.Cancel;

            case IconsPanelActionKind.ShowHeroQuickInfo:
                _dialogs.ShowQuickInfo(action.Hero!, events);

                return AdventurePanelResult.Cancel;

            case IconsPanelActionKind.ShowCastleQuickInfo:
                _dialogs.ShowQuickInfo(action.Castle!, events);

                return AdventurePanelResult.Cancel;

            default:
                return AdventurePanelResult.Cancel;
        }
    }

    /// <summary>雷达，<c>:1320</c> 与 <c>:1384-1388</c>。</summary>
    /// <param name="events">本帧的事件状态。</param>
    private AdventurePanelResult HandleRadar(LocalEvent events)
    {
        switch (Radar.QueueEventProcessing(events))
        {
            case RadarAction.ShowTooltip:
                _dialogs.ShowMessage(string.Empty, "World Map", events);

                return AdventurePanelResult.Cancel;

            default:
                // 地图已经居中了（radar.cpp 里做的），剩下的只是重绘。
                return AdventurePanelResult.Cancel;
        }
    }

    /// <summary>控制面板，<c>:1390-1394</c>。</summary>
    /// <param name="events">本帧的事件状态。</param>
    private AdventurePanelResult HandleControlPanel(LocalEvent events)
    {
        if (_controlPanel is not { } control)
        {
            return AdventurePanelResult.Cancel;
        }

        switch (control.QueueEventProcessing(events))
        {
            case ControlPanelButton.Radar:
                EventSwitchShowRadar();

                return AdventurePanelResult.Cancel;

            case ControlPanelButton.Icons:
                EventSwitchShowIcons();

                return AdventurePanelResult.Cancel;

            case ControlPanelButton.Buttons:
                EventSwitchShowButtons();

                return AdventurePanelResult.Cancel;

            case ControlPanelButton.Status:
                EventSwitchShowStatus();

                return AdventurePanelResult.Cancel;

            case ControlPanelButton.EndTurn:
                return new AdventurePanelResult(EventEndTurn(), null);

            default:
                return AdventurePanelResult.Cancel;
        }
    }

    /// <summary>The plain pointer of <c>resetCursor()</c>.</summary>
    /// <remarks>
    /// The reference also asks the game area for an update; the port's <see cref="UpdateCursor(int, bool)"/> is what
    /// the frame loop calls when it has a tile, so setting the theme here is enough.
    /// </remarks>
    private static void ResetCursor() => CursorState.Current?.SetThemes(CursorTheme.Pointer);

    /// <summary>
    /// <c>resetCursorIfNoNeedToScroll()</c>: the pointer comes back unless the map is still scrolling, so the scroll
    /// cursor is not overwritten mid-frame.
    /// </summary>
    private void ResetCursorIfNoNeedToScroll()
    {
        if (GetGameArea().NeedScroll)
        {
            return;
        }

        ResetCursor();
    }

    /// <summary>
    /// 英雄移动动画，<c>HumanTurn()</c> 的 <c>Animation of the hero's movement</c>（<c>:1412-1536</c>）。
    /// </summary>
    /// <param name="events">本帧的事件状态，用来在走完后按光标下的瓦片挑光标。</param>
    /// <returns>
    /// <see cref="AdventureEventResult.Cancel"/>。参考在这一段里可以用 <c>hero->isAction()</c> 引出胜负判定，
    /// 端口没有挂起的动作，所以永远是 <see cref="AdventureEventResult.Cancel"/>。
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// <para>
    /// 一步的动画是「帧计数倒数 + 视口每帧推一像素」：<c>heroAnimationFrameCount</c> 从 <c>32 - skip</c> 数到 0，
    /// 每 <c>skip</c> 帧换一次精灵，视口同步推 <c>offset * skip</c>，所以英雄看起来是原地不动、地图在走。
    /// </para>
    /// <para>
    /// <b>三处差异。</b> 参考在起步那一刻调 <c>Game::EnvironmentSoundMixer()</c> 混脚步，端口的环境音在 App 层，
    /// 由调用方按帧自己混；参考用 <c>hero->GetCenter()</c> 把视口对到英雄的像素中心，端口没有对象像素中心，
    /// 改用瓦片居中（锚点差半个格子）；参考走完会看 <c>hero->isAction()</c> 结算动作，端口的对象动作在点下目标格
    /// 时就地结算，这里只负责「走完复位」。
    /// </para>
    /// </remarks>
    public AdventureEventResult AnimateHeroMovement(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!GameDelays.ValidateAnimationDelay(DelayType.CurrentHeroDelay))
        {
            return AdventureEventResult.Cancel;
        }

        Hero? hero = InterfaceFocus.GetFocusHeroes();
        if (hero is null)
        {
            _isHeroMoving = false;
            _stopHero = false;

            return AdventureEventResult.Cancel;
        }

        bool resetHeroSprite = false;

        if (_heroAnimationFrameCount > 0)
        {
            int skip = GameDelays.HumanHeroAnimSpeedMultiplier();

            GetGameArea().ShiftCenter(new Point(_heroAnimationOffset.X * skip, _heroAnimationOffset.Y * skip));

            SetRedraw(Redraw.GameArea);

            // 参考在这里调 Game::EnvironmentSoundMixer() 混一步脚步声；端口的音频在 App 层，由调用方按帧自己混。

            _heroAnimationFrameCount -= skip;

            if ((_heroAnimationFrameCount & 0x3) == 0)
            {
                hero.SpriteIndex = _heroAnimationSpriteId;

                if (_heroAnimationFrameCount == 0)
                {
                    resetHeroSprite = true;
                }
                else
                {
                    ++_heroAnimationSpriteId;
                }
            }

            int offsetStep = (4 - (_heroAnimationFrameCount & 0x3)) & 0x3;

            hero.Offset = new Point(_heroAnimationOffset.X * offsetStep, _heroAnimationOffset.Y * offsetStep);
        }

        if (_heroAnimationFrameCount != 0)
        {
            return AdventureEventResult.Cancel;
        }

        if (resetHeroSprite)
        {
            hero.SpriteIndex = _heroAnimationSpriteId - 1;
        }

        if (!hero.IsMoveEnabled)
        {
            hero.SetMove(false);

            _isHeroMoving = false;
            _stopHero = false;

            GetGameArea().SetUpdateCursor();

            return AdventureEventResult.Cancel;
        }

        WorldMap world = WorldMap.Current;

        bool arrived = hero.Move(Settings.Get().HeroesMoveSpeed == 10, world);

        if (hero.MapIndex < 0)
        {
            // 输掉战斗的人会被 Dismiss 掉（heroes.cpp:1959-2002）：MapIndex 变 -1、移动状态清空、路径收起。
            // 参考走完这一步紧接着问 hero->isAction() 并判整局胜负（game_startgame.cpp:1521-1529），
            // 输了最后一个英雄就直接回主菜单，根本走不到 SetCenter() 与 updateCursor()；端口还没有那一步，
            // 所以这里认出「人已经离场」就收工：不拿 -1 去居中，也不让下一帧用站不在图上的焦点英雄重算寻路
            // （World.GetPath() 会以「The hero stands outside the map」拒绝它）。
            // 重新挑焦点用的正是 ResetFocus(HEROES) 里写明的那条「当前英雄可能刚输掉战斗、已不在英雄列表里」。
            _isHeroMoving = false;
            _stopHero = false;

            ResetFocus((int)GameFocus.Heroes, true);
            GetGameArea().SetUpdateCursor();

            return AdventureEventResult.Cancel;
        }

        if (arrived)
        {
            // 参考用 hero->GetCenter() 把视口对到英雄的像素中心；端口没有对象像素中心，改用瓦片居中。
            GetGameArea().SetCenter(new Point(hero.MapIndex % world.Width, hero.MapIndex / world.Width));

            if (_stopHero)
            {
                hero.SetMove(false);

                _stopHero = false;
            }
        }
        else
        {
            Point movement = hero.MovementDirection(world);

            if (movement != default)
            {
                int skip = GameDelays.HumanHeroAnimSpeedMultiplier();

                _heroAnimationOffset = movement;

                GetGameArea().ShiftCenter(movement);

                _heroAnimationFrameCount = 32 - skip;
                _heroAnimationSpriteId = hero.SpriteIndex;

                if (skip < 4)
                {
                    hero.SpriteIndex = _heroAnimationSpriteId - 1;
                    hero.Offset = new Point(movement.X * skip, movement.Y * skip);
                }
                else
                {
                    ++_heroAnimationSpriteId;
                }
            }

            SetRedraw(Redraw.GameArea);
        }

        _isHeroMoving = hero.IsMoveEnabled;

        if (_isHeroMoving)
        {
            return AdventureEventResult.Cancel;
        }

        hero.SetMove(false);

        // 参考在淡入时只 SetUpdateCursor() 等下一帧；端口没有淡入，直接按光标下的瓦片更新。
        if (events.IsMouseCursorPosInArea(GetGameArea().Roi))
        {
            UpdateCursor(GetGameArea().GetValidTileIdFromPoint(events.CursorPosition));
        }
        else
        {
            ResetCursor();
        }

        return AdventureEventResult.Cancel;
    }

    /// <summary>卷屏帧，<c>HumanTurn()</c> 的 <c>Scrolling the game area</c>（<c>:1538-1555</c>）。</summary>
    /// <param name="events">本帧的事件状态，用来按光标所在的边缘挑卷屏箭头。</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 英雄在走就不卷屏（参考的 <c>if ( !isHeroMoving )</c>）；拖动卷屏或惯性滚动的重绘请求走 <c>else if</c>
    /// 那条，两者都不排队画地图。
    /// </remarks>
    public void ScrollGameArea(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_isHeroMoving)
        {
            return;
        }

        GameArea gameArea = GetGameArea();

        if (gameArea.NeedScroll && GameDelays.ValidateAnimationDelay(DelayType.ScrollDelay))
        {
            Point cursor = events.CursorPosition;

            if (BaseInterface.IsScrollLeft(cursor) || BaseInterface.IsScrollRight(cursor, new Size(_display.Width, _display.Height))
                || BaseInterface.IsScrollTop(cursor) || BaseInterface.IsScrollBottom(cursor, new Size(_display.Width, _display.Height)))
            {
                CursorState.Current?.SetThemes((CursorTheme)gameArea.GetScrollCursor());
            }

            gameArea.Scroll();

            SetRedraw(Redraw.GameArea | Redraw.RadarCursor);
        }
        else if (gameArea.NeedDragScrollRedraw() || gameArea.UpdateInertia())
        {
            SetRedraw(Redraw.GameArea | Redraw.RadarCursor);
        }
    }

    /// <summary>
    /// 一帧，<c>HumanTurn()</c> 的循环体（<c>:1127-1560</c>）：停下英雄 → 键盘 → 面板 → 动画 → 卷屏。
    /// </summary>
    /// <param name="events">本帧的事件状态。</param>
    /// <param name="exitEvent">收到退出热键时做的事，见 <see cref="ProcessHotKeys"/>。</param>
    /// <returns>要让循环做什么，以及需要调用方接手的拖动。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="events"/> 或 <paramref name="exitEvent"/> 为 <see langword="null"/>。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 英雄在走时这一帧只做三件事：把光标换成沙漏、看要不要停、推动画——参考同样不进键盘块与面板块。
    /// 不在走时才轮到热键与面板，任何一个给出非 <see cref="AdventureEventResult.Cancel"/> 的结果都直接返回，
    /// 后面的动画与卷屏这一帧不做（参考的 <c>break</c>）。
    /// </para>
    /// <para>
    /// 最后一步推地图物体动画：<c>MAPS_DELAY</c>（250 毫秒）到点就把帧计数加一，并让地图重画一次
    /// （<c>:1567-1572</c>）。参考在这里还会 <c>redraw( 0 )</c> 把画面刷出去，端口由宿主的每帧绘制完成，
    /// 这一层只负责把 <see cref="Redraw.GameArea"/> 记进掩码。
    /// </para>
    /// </remarks>
    public AdventurePanelResult ProcessTurnFrame(LocalEvent events, Func<AdventureEventResult> exitEvent)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(exitEvent);

        _statusPanel.TimerEventProcessing();

        if (_isHeroMoving)
        {
            CursorState.Current?.SetThemes(CursorTheme.Wait);

            if (IsHeroMovementInterrupted(events))
            {
                _stopHero = true;
            }
        }
        else
        {
            AdventureEventResult result = ProcessHotKeys(events, exitEvent);

            if (result != AdventureEventResult.Cancel)
            {
                return new AdventurePanelResult(result, null);
            }

            AdventurePanelResult panel = ProcessPanels(events);

            if (panel.Mode != AdventureEventResult.Cancel || panel.Drag is not null)
            {
                return panel;
            }
        }

        AnimateHeroMovement(events);
        ScrollGameArea(events);
        AnimateMapObjects();

        return AdventurePanelResult.Cancel;
    }

    /// <summary>
    /// 地图物体动画，<c>HumanTurn()</c> 的 <c>Map objects animation</c>（<c>:1567-1572</c>）。
    /// </summary>
    /// <remarks>
    /// 参考的 <c>Game::validateAnimationDelay( MAPS_DELAY )</c> 只在 250 毫秒到点时为真；为真才推一帧并重画地图，
    /// 所以动画帧率与机器的帧率无关。帧计数是 <see cref="GameAnimation"/> 里的全局值，瓦片渲染按它取
    /// <c>animationFrames</c> 序列里的一帧（<c>maps_tiles_render.cpp:132</c>）。
    /// </remarks>
    private void AnimateMapObjects()
    {
        if (!GameDelays.ValidateAnimationDelay(DelayType.MapsDelay))
        {
            return;
        }

        GameAnimation.UpdateAdventureMapAnimationIndex();

        GetGameArea().SetRedraw();
    }

    /// <summary>
    /// 一帧之后的收尾，<c>HumanTurn()</c> 返回前的两段（<c>:1557-1560</c> 与 <c>:1584-1609</c>）。
    /// </summary>
    /// <param name="result">
    /// <see cref="ProcessTurnFrame"/> 给出的结果，通常是 <see cref="AdventureEventResult.Cancel"/>。
    /// </param>
    /// <param name="isKingdomVanquished">
    /// 当前玩家的王国是否已经没有英雄也没有城堡，参考的 <c>!myKingdom.isPlay()</c>：调用方从
    /// <see cref="Settings.Players"/> 的回合色查 <c>Kingdom::IsLoss</c> 后传进来（与键盘块的 <c>exitEvent</c>
    /// 一样，全局状态由宿主读，这一层只做判断）。
    /// </param>
    /// <returns>收尾之后要让循环做什么：可能把 <see cref="AdventureEventResult.Cancel"/> 换成结束回合。</returns>
    /// <remarks>
    /// <para>
    /// 两件事。其一，王国已经没有英雄也没有城堡时玩家没法再行动，参考在这一步把结果改成结束回合
    /// （<c>if ( res == CANCEL &amp;&amp; !myKingdom.isPlay() ) res = END_TURN;</c>）。
    /// </para>
    /// <para>
    /// 其二，回合真的结束时把焦点英雄的路线收起来，地图上不再留着一条走不了的路径
    /// （<c>GetFocusHeroes()-&gt;ShowPath( false )</c> 加 <c>setRedraw( REDRAW_GAMEAREA )</c>）。
    /// </para>
    /// <para>
    /// <b>没搬的一半。</b> 参考在结束回合时还会在「失去最后一座城」时提示玩家，并按设置自动存档
    /// （<c>:1591-1609</c>）；前者要 <c>Kingdom::GetLostTownDays</c> 与玩家提示窗，后者要存档层，都属于 App 层。
    /// </para>
    /// </remarks>
    public AdventureEventResult FinishHumanTurn(AdventureEventResult result, bool isKingdomVanquished)
    {
        if (result == AdventureEventResult.Cancel && isKingdomVanquished)
        {
            result = AdventureEventResult.EndTurn;
        }

        if (result != AdventureEventResult.EndTurn)
        {
            return result;
        }

        if (InterfaceFocus.GetFocusHeroes() is { } hero)
        {
            hero.ShowPath(false);

            SetRedraw(Redraw.GameArea);
        }

        return result;
    }
}
