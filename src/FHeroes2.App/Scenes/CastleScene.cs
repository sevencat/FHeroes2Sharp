using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using SDL3;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The town screen of one castle: <see cref="CastleScreenView"/> paints it, <see cref="CastleScreenInput"/>
/// decides what a click means, and this class runs the loop of <c>Castle::OpenDialog()</c>
/// (<c>castle_dialog.cpp:470-859</c>) - switch to the previous or the next town, close on EXIT.
/// </summary>
/// <remarks>
/// <para>
/// 参考的循环里还有六个子窗口（招募、建造、魔法塔、市场、盗贼公会、水井）与两种淡入（英雄、建筑）。这一段先
/// 把「进得去、出得来、切得动」跑通：EXIT 关闭、两个箭头在多个城之间循环；点建筑与面板只记一条日志，等下一批
/// 接各自的窗口。
/// </para>
/// <para>
/// 状态条照参考：指针在某个可交互区域上时显示它的名字，否则显示日期（<c>castle_dialog.cpp:455-457</c>）。
/// </para>
/// </remarks>
public sealed class CastleScene : IScene
{
    /// <summary>How long one frame of the town's animation stays, <c>CASTLE_AROUND_DELAY</c> (200 ms).</summary>
    private const double AroundDelay = 0.2;

    private readonly ScreenBuffer _frame;
    private readonly World _world;
    private readonly Kingdom? _kingdom;
    private readonly AssetBundle? _assets;
    private readonly GameFonts _fonts;
    private readonly CastleScreenView _view;
    private readonly CastleScreenInput _input;

    private MageGuildWindow? _mageGuild;
    private WellWindow? _well;
    private RecruitWindow? _recruit;
    private MarketplaceWindow? _marketplace;
    private ThievesGuildWindow? _thievesGuild;
    private BuildWindow? _build;
    private ConstructionWindow? _construction;
    private RecruitHeroWindow? _recruitHero;
    private HeroQuickInfoView? _heroInfo;
    private HeroQuickInfoArtwork? _heroInfoArtwork;
    private Mons32Sprites? _miniMonsters;
    private Sprite? _captainPortrait;
    private Castle? _captainPortraitOwner;
    private PlayerColor _captainPortraitColor;
    private bool _captainPortraitTried;
    private MessageBox? _message;
    private bool? _hasMessageFrame;
    private IReadOnlyList<Sprite>? _messageResourceIcons;
    private bool _messageHeldByRightButton;
    private MessageBox? _tavern;
    private PixelBuffer? _townSnapshot;
    private Castle? _snapshotCastle;
    private double _animationTimer;
    private uint _animationIndex = 1;

    private CastleScene(
        ScreenBuffer frame,
        World world,
        Castle castle,
        Kingdom? kingdom,
        AssetBundle? assets,
        CastleScreenArtwork artwork,
        GameFonts fonts)
    {
        _frame = frame;
        _world = world;
        _kingdom = kingdom;
        _assets = assets;
        _fonts = fonts;
        _view = new CastleScreenView(artwork, fonts.Small);
        _input = new CastleScreenInput(_view);

        Show(castle);
    }

    /// <summary>
    /// Builds the screen of a town, <c>Castle::OpenDialog()</c> up to the start of its loop.
    /// </summary>
    /// <param name="frame">The buffer the screen paints.</param>
    /// <param name="world">The world, for the date line and the shipyard's boat.</param>
    /// <param name="castle">The town to show.</param>
    /// <param name="kingdom">The town's kingdom, for the treasury and the switching; may be <see langword="null"/>.</param>
    /// <param name="assets">The archives the sub-windows take their sprites from; may be <see langword="null"/>.</param>
    /// <param name="fonts">The game fonts: the small face for the numbers and the name, all three for the windows.</param>
    /// <returns>The screen, or <see langword="null"/> when a frame of the town screen is missing.</returns>
    public static CastleScene? TryCreate(
        ScreenBuffer frame,
        World world,
        Castle castle,
        Kingdom? kingdom,
        AssetBundle? assets,
        GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(fonts);

        return CastleScreenArtwork.TryCreate(castle.Color) is { } artwork
            ? new CastleScene(frame, world, castle, kingdom, assets, artwork, fonts)
            : null;
    }

    /// <summary>The town on screen.</summary>
    public Castle Castle => _view.Castle!;

    /// <summary>Whether the player left the screen with EXIT; the host then goes back to the map.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Whether a sub-window is covering the town screen.</summary>
    public bool IsWindowOpen =>
        _mageGuild is not null || _well is not null || _recruit is not null || _marketplace is not null
        || _thievesGuild is not null || _build is not null || _construction is not null || _recruitHero is not null
        || _message is not null || _tavern is not null;

    /// <summary>Records the pointer, which also picks the status line.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor)
    {
        // 酒馆是 showStandardTextMessage 的模态框，压在所有窗口之上。
        if (_tavern is { } tavern)
        {
            tavern.SetCursor(cursor);

            return;
        }

        // 招募英雄的确认框盖在建造列表上（castle_town.cpp:670 的 Dialog::BuyBuilding 先于列表收起来），
        // 所以它排在列表前面。
        if (_recruitHero is { } recruitHero)
        {
            recruitHero.SetCursor(cursor);

            return;
        }

        if (_marketplace is { } market)
        {
            market.SetCursor(cursor);

            return;
        }

        if (_recruit is { } recruit)
        {
            recruit.SetCursor(cursor);

            return;
        }

        if (_mageGuild is { } window)
        {
            window.SetCursor(cursor);

            return;
        }

        if (_well is { } well)
        {
            well.SetCursor(cursor);

            return;
        }

        if (_thievesGuild is { } guild)
        {
            guild.SetCursor(cursor);

            return;
        }

        if (_build is { } build)
        {
            build.SetCursor(cursor);

            return;
        }

        if (_construction is { } construction)
        {
            construction.SetCursor(cursor);

            return;
        }

        _input.SetCursor(cursor);
    }

    /// <summary>
    /// Records the left button and acts on a completed click: EXIT closes the screen, the arrows switch
    /// town, a building opens the window it stands for.
    /// </summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down)
    {
        // 酒馆的 OK 按钮：参考的 showStandardTextMessage( ..., Dialog::OK ) 只认按钮上的那一下。
        if (_tavern is { } tavern)
        {
            if (tavern.SetLeftButton(down) == DialogButton.Ok)
            {
                CloseTavern();
            }

            return;
        }

        // 说明框是模态的：参考的 Dialog::OK 也是「按一下就收起」。按住右键才显示的收入框不理会左键。
        if (_message is not null)
        {
            if (down && !_messageHeldByRightButton)
            {
                _message = null;
            }

            return;
        }

        if (_recruitHero is { } recruitHero)
        {
            recruitHero.SetLeftButton(down);

            return;
        }

        if (_marketplace is { } market)
        {
            market.SetLeftButton(down);

            return;
        }

        if (_recruit is { } recruit)
        {
            recruit.SetLeftButton(down);

            return;
        }

        if (_mageGuild is { } window)
        {
            window.SetLeftButton(down);

            return;
        }

        if (_well is { } well)
        {
            well.SetLeftButton(down);

            return;
        }

        if (_thievesGuild is { } guild)
        {
            guild.SetLeftButton(down);

            return;
        }

        if (_build is { } build)
        {
            build.SetLeftButton(down);

            return;
        }

        if (_construction is { } construction)
        {
            construction.SetLeftButton(down);

            return;
        }

        _input.SetLeftButton(down);

        if (_input.TakeAction() is not { } action)
        {
            return;
        }

        switch (action.Target)
        {
            case CastleScreenTarget.Exit:
                Close();
                break;

            case CastleScreenTarget.PreviousCastle:
                ShowPreviousCastle();
                break;

            case CastleScreenTarget.NextCastle:
                ShowNextCastle();
                break;

            case CastleScreenTarget.Income:
                ShowIncome();
                break;

            case CastleScreenTarget.Hero:
                GameLog.Info("The hero window is not wired up yet.");
                break;

            case CastleScreenTarget.Building:
                OpenBuilding(action.Building);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Records the right button. The marketplace asks for the kingdom income window while it is held,
    /// <c>showStandardTextMessage( ..., Dialog::ZERO )</c> (<c>dialog_marketplace.cpp:660-666</c>).
    /// </summary>
    /// <param name="down">Whether the right button is now held.</param>
    public void SetRightButton(bool down)
    {
        // 英雄速览只活在右键按下的这段时间里，松开就收（参考的事件循环等右键松开才退出）。
        if (!down)
        {
            _heroInfo = null;
        }

        // 酒馆只有 OK 按钮，右键没有第二个说法（参考的 showMessage 也不拿右键收框）。
        if (_tavern is not null)
        {
            return;
        }

        // 招募英雄的确认框在最上层：OKAY 上是「Recruit X the Y.」，CANCEL 上是退出说明
        // （castle_town.cpp:150-156），相框上是英雄速览。
        if (_recruitHero is { } recruitHero)
        {
            if (down)
            {
                if (recruitHero.IsPortraitUnder(_input.Cursor))
                {
                    ShowHeroQuickInfo(recruitHero.Dialog.Hero);
                }
                else
                {
                    ShowRecruitHeroHelp(recruitHero.SetRightButton(true));
                }
            }
            else if (_messageHeldByRightButton)
            {
                _message = null;
                _messageHeldByRightButton = false;
            }

            return;
        }

        // 建造说明框的右键帮助活在右键按下的这段时间里：按下时摆框，松开时收起。
        if (_build is { } build)
        {
            if (down)
            {
                switch (build.SetRightButton(true))
                {
                    case DialogButton.Ok:
                        ShowBuildOkayHelp(build);
                        break;

                    case DialogButton.Cancel:
                        ShowBuildCancelHelp();
                        break;

                    default:
                        break;
                }
            }
            else if (_messageHeldByRightButton)
            {
                _message = null;
                _messageHeldByRightButton = false;
            }

            return;
        }

        // 建造列表的右键说明同样活在右键按下这段时间里（castle_town.cpp:636-743 用的是 Dialog::ZERO）。
        if (_construction is { } construction)
        {
            if (down)
            {
                FollowConstructionHelp(construction.SetRightButton(true));
            }
            else if (_messageHeldByRightButton)
            {
                _message = null;
                _messageHeldByRightButton = false;
            }

            return;
        }

        if (_thievesGuild is { } guild)
        {
            guild.SetRightButton(down);

            if (down || !_messageHeldByRightButton)
            {
                return;
            }

            _message = null;
            _messageHeldByRightButton = false;

            return;
        }

        // 没有子窗口时，城镇屏的两处右键都是速览：先是左侧徽记上的船长，再是卫戍栏的英雄像
        // （<c>castle_dialog.cpp:547-551</c>，参考的顺序也是先 rectSign1 后 rectSign2）。
        if (down)
        {
            if (Castle is { } castle &&
                _input.Cursor is { } pointer &&
                castle.IsBuild(BuildingType.Captain) &&
                _view.Layout.CrestArea.Contains(pointer))
            {
                ShowCaptainQuickInfo(castle.Captain);
            }
            else if (_view.Hero is { } visitingHero && _input.HitTest(_input.Cursor) == CastleScreenTarget.Hero)
            {
                ShowHeroQuickInfo(visitingHero);
            }

            return;
        }

        if (_marketplace is not { } market)
        {
            return;
        }

        market.SetRightButton(down);

        if (down || !_messageHeldByRightButton)
        {
            return;
        }

        // 参考的 ZERO 框活在右键按下的这段时间里，松开就消失。
        _message = null;
        _messageHeldByRightButton = false;
    }

    /// <summary>
    /// Handles one key press, <c>Game::HotKeyCloseWindow</c> and the two town arrows.
    /// </summary>
    /// <remarks>打开子窗口时按键先给窗口：ESC 与 Enter 关窗口，左右方向键换城；窗口没开时 ESC 与 E 退出城镇屏。</remarks>
    /// <param name="key">The key that went down.</param>
    /// <param name="repeat">Whether it is an auto-repeat of a held key.</param>
    public void HandleKey(SDL.Keycode key, bool repeat)
    {
        if (repeat)
        {
            return;
        }

        // 酒馆认 ESC 与 Enter（参考的 Game::HotKeyCloseWindow 就映射到 Dialog::OK）。
        if (_tavern is not null)
        {
            if (key is SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter)
            {
                CloseTavern();
            }

            return;
        }

        if (_message is not null)
        {
            if (!_messageHeldByRightButton && key is SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter)
            {
                _message = null;
            }

            return;
        }

        if (_recruitHero is { } recruitHero)
        {
            switch (key)
            {
                case SDL.Keycode.Escape:
                    recruitHero.PressClose();
                    break;

                case SDL.Keycode.Return or SDL.Keycode.KpEnter:
                    recruitHero.PressConfirm();
                    break;

                default:
                    break;
            }

            return;
        }

        if (_marketplace is { } market)
        {
            if (key is SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter)
            {
                market.PressClose();
            }

            return;
        }

        if (_recruit is { } recruit)
        {
            if (key is SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter)
            {
                recruit.PressClose();
            }

            return;
        }

        if (_mageGuild is { } window)
        {
            switch (key)
            {
                case SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter:
                    window.PressClose();
                    break;

                case SDL.Keycode.Left:
                    window.PressPreviousTown();
                    break;

                case SDL.Keycode.Right:
                    window.PressNextTown();
                    break;

                default:
                    break;
            }

            return;
        }

        if (_well is { } well)
        {
            if (key is SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter)
            {
                well.PressClose();
            }

            return;
        }

        if (_thievesGuild is { } guild)
        {
            if (key is SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter)
            {
                guild.PressClose();
            }

            return;
        }

        if (_construction is { } construction)
        {
            switch (key)
            {
                case SDL.Keycode.Escape or SDL.Keycode.Return or SDL.Keycode.KpEnter:
                    construction.PressClose();
                    break;

                case SDL.Keycode.Left:
                    construction.PressPreviousTown();
                    break;

                case SDL.Keycode.Right:
                    construction.PressNextTown();
                    break;

                default:
                    break;
            }

            return;
        }

        if (_build is { } build)
        {
            switch (key)
            {
                case SDL.Keycode.Escape:
                    build.PressClose();
                    break;

                case SDL.Keycode.Return or SDL.Keycode.KpEnter:
                    build.PressConfirm();
                    break;

                default:
                    break;
            }

            return;
        }

        if (key is SDL.Keycode.Escape or SDL.Keycode.E)
        {
            Close();

            return;
        }

        // 三个城镇热键，game_hotkeys.cpp:319-323：W 水井、M 市场、S 魔法塔。
        switch (key)
        {
            case SDL.Keycode.W:
                OpenWell();
                break;

            case SDL.Keycode.M:
                OpenMarketplace();
                break;

            case SDL.Keycode.S:
                OpenMageGuild();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Opens the mage guild window, <c>Castle::_openMageGuild( hero )</c> of the building click branch.
    /// </summary>
    /// <returns><see langword="true"/> when the window opened.</returns>
    public bool OpenMageGuild()
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The mage guild window needs the archives, and they were not loaded.");

            return false;
        }

        if (Castle.GetLevelMageGuild() <= 0)
        {
            GameLog.Info($"'{Castle.Name}' has no mage guild.");

            return false;
        }

        MageGuildWindow? window = MageGuildWindow.TryCreate(
            assets,
            Castle,
            _view.Hero?.HasSpellBook ?? false,
            _kingdom?.Castles.Count ?? 1,
            _fonts.Small);

        if (window is null)
        {
            GameLog.Warn($"The mage guild artwork of '{Castle.Name}' is missing.");

            return false;
        }

        RememberTownFrame();
        _mageGuild = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the mage guild of '{Castle.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the well window, <c>Castle::_openWell()</c> of the building click branch.
    /// </summary>
    /// <returns><see langword="true"/> when the window opened.</returns>
    public bool OpenWell()
    {
        if (_well is not null)
        {
            return true;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The well window needs the archives, and they were not loaded.");

            return false;
        }

        if (!Castle.IsBuild(BuildingType.Well))
        {
            GameLog.Info($"'{Castle.Name}' has no well.");

            return false;
        }

        WellWindow? window = WellWindow.TryCreate(assets, Castle, _fonts);

        if (window is null)
        {
            GameLog.Warn($"The well artwork of '{Castle.Name}' is missing.");

            return false;
        }

        RememberTownFrame();
        _well = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the well of '{Castle.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the marketplace window, the <c>BUILD_MARKETPLACE</c> branch of <c>castle_dialog.cpp:630-640</c>
    /// (<c>Dialog::Marketplace( kingdom, false )</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the window opened.</returns>
    public bool OpenMarketplace()
    {
        if (_marketplace is not null)
        {
            return true;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The marketplace window needs the archives, and they were not loaded.");

            return false;
        }

        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("The marketplace window needs a kingdom, and the scene has none.");

            return false;
        }

        if (!Castle.IsBuild(BuildingType.Marketplace))
        {
            GameLog.Info($"'{Castle.Name}' has no marketplace.");

            return false;
        }

        MarketplaceWindow? window = MarketplaceWindow.TryCreate(assets, kingdom, ActivePlayers(), _fonts);

        if (window is null)
        {
            GameLog.Warn($"The marketplace artwork of '{Castle.Name}' is missing.");

            return false;
        }

        RememberTownFrame();
        _marketplace = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the marketplace of '{Castle.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the thieves' guild ranking, the <c>BUILD_THIEVESGUILD</c> branch of <c>castle_dialog.cpp:642-660</c>
    /// (<c>Dialog::ThievesGuild( false )</c>).
    /// </summary>
    /// <param name="oracle">Whether the ranking is opened at the oracle instead, <c>Dialog::ThievesGuild( true )</c>.</param>
    /// <returns><see langword="true"/> when the window opened.</returns>
    public bool OpenThievesGuild(bool oracle = false)
    {
        if (_thievesGuild is not null)
        {
            return true;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The thieves' guild window needs the archives, and they were not loaded.");

            return false;
        }

        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("The thieves' guild window needs a kingdom, and the scene has none.");

            return false;
        }

        if (!oracle && !Castle.IsBuild(BuildingType.ThievesGuild))
        {
            GameLog.Info($"'{Castle.Name}' has no thieves' guild.");

            return false;
        }

        ThievesGuildWindow? window = ThievesGuildWindow.TryCreate(
            assets,
            ThievesGuildReport.FromWorld(_world, kingdom.Color, oracle),
            _fonts);

        if (window is null)
        {
            GameLog.Warn($"The thieves' guild artwork of '{Castle.Name}' is missing.");

            return false;
        }

        RememberTownFrame();
        _thievesGuild = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the thieves' guild of '{Castle.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the tavern window, <c>Castle::_openTavern()</c> (<c>castle_tavern.cpp:39-54</c>): the barkeep
    /// of <c>TAVWIN.ICN</c> over this week's rumor, with a single OK button.
    /// </summary>
    /// <returns><see langword="true"/> when the window opened.</returns>
    public bool OpenTavern()
    {
        if (_tavern is not null)
        {
            return true;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The tavern window needs the archives, and they were not loaded.");

            return false;
        }

        if (!Castle.IsBuild(BuildingType.Tavern))
        {
            GameLog.Info($"'{Castle.Name}' has no tavern.");

            return false;
        }

        if (!HasMessageFrame())
        {
            GameLog.Warn($"The tavern window needs the frame of {FrameBox.ArtworkFileName}, and it is missing.");

            return false;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Tavwin)) is not { } tavwin)
        {
            GameLog.Warn("The tavern window needs the barkeep of TAVWIN.ICN, and it is missing.");

            return false;
        }

        if (SystemButtonArtwork.TryCreate(assets) is not { } buttons)
        {
            GameLog.Warn("The tavern window needs the OK button, and its artwork is missing.");

            return false;
        }

        MessageBox? window = TavernView.TryCreate(TavernDialog.Create(_world), tavwin.Sprites,
            _fonts.Yellow, _fonts.Normal, buttons);

        if (window is null)
        {
            GameLog.Warn("The barkeep of TAVWIN.ICN has no animation frames.");

            return false;
        }

        // 酒馆是模态框，浮在城镇屏上；按住右键才显示的收入框这时没有位置。
        _message = null;
        _messageHeldByRightButton = false;
        _heroInfo = null;

        _tavern = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the tavern of '{Castle.Name}'.");

        return true;
    }

    /// <summary>Closes the tavern window, the OK button and the ESC key of <c>Dialog::OK</c>.</summary>
    private void CloseTavern()
    {
        if (_tavern is null)
        {
            return;
        }

        _tavern = null;

        GameLog.Info($"Closed the tavern of '{Castle.Name}'.");
    }

    /// <summary>How many players are still in the game, <c>Settings::Get().GetPlayers()</c> of the gift test.</summary>
    /// <remarks>
    /// 参考问的是剧本里在局的玩家槽；端口的槽还没被开局流程填满，于是问王国自己——
    /// <see cref="Kingdoms.GetColors"/> 给的正是 <see cref="Kingdoms.Init"/> 建的同一份表（ADR-111）。
    /// </remarks>
    /// <returns>The number of kingdoms in play; one or zero in a single player game.</returns>
    private int ActivePlayers()
    {
        int count = 0;

        foreach (PlayerColor color in PlayerColors.ToList(_world.Kingdoms.GetColors()))
        {
            if (_world.Kingdoms.GetKingdom(color).IsPlay)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Leaves the screen, the EXIT button and the close hot key.</summary>
    public void Close()
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;

        GameLog.Info($"Closing the town screen of '{Castle.Name}'.");
    }

    /// <summary>Shows the previous town of the kingdom, <c>CastleDialogReturnValue::PreviousCastle</c>.</summary>
    /// <returns><see langword="true"/> when there is another town to show.</returns>
    public bool ShowPreviousCastle() => SwitchTo(-1);

    /// <summary>Shows the next town of the kingdom, <c>CastleDialogReturnValue::NextCastle</c>.</summary>
    /// <returns><see langword="true"/> when there is another town to show.</returns>
    public bool ShowNextCastle() => SwitchTo(1);

    /// <summary>Advances the town's animation, <c>CASTLE_AROUND_DELAY</c> per step.</summary>
    /// <param name="elapsedSeconds">Time since the previous frame.</param>
    public void Update(double elapsedSeconds)
    {
        // 酒馆停在参考的 showMessage 循环里：只有吧台手的动画在动，城镇屏本身不走帧。
        if (_tavern is { } tavern)
        {
            tavern.Update(_frame.Pixels, TimeSpan.FromSeconds(elapsedSeconds));

            return;
        }

        // 说明框模态：参考停在 showStandardTextMessage() 的循环里，底下的窗口一动不动。
        if (_message is not null)
        {
            return;
        }

        if (_recruitHero is { } recruitHero)
        {
            FollowRecruitHero(recruitHero.Update());

            return;
        }

        if (_marketplace is { } market)
        {
            FollowMarketplace(market.Update());

            return;
        }

        if (_recruit is { } recruit)
        {
            recruit.Update();

            if (recruit.IsClosed)
            {
                CloseRecruit(recruit);
            }

            return;
        }

        if (_mageGuild is { } window)
        {
            FollowMageGuild(window.Update());

            return;
        }

        if (_well is { } well)
        {
            FollowWell(well.Update());

            return;
        }

        if (_thievesGuild is { } guild)
        {
            FollowThievesGuild(guild.Update());

            return;
        }

        if (_build is { } build)
        {
            FollowBuild(build.Update());

            return;
        }

        if (_construction is { } construction)
        {
            FollowConstruction(construction.Update());

            return;
        }

        _animationTimer += elapsedSeconds;

        while (_animationTimer >= AroundDelay)
        {
            _animationTimer -= AroundDelay;
            ++_animationIndex;
        }
    }

    /// <summary>Paints the whole screen: the town, the ports, the armies, the panel and the status line.</summary>
    /// <param name="state">Unused: the town screen draws all of its text with the game fonts.</param>
    public void DrawFrame(in DiagnosticsState state)
    {
        DrawWindows(state);

        // 英雄速览浮在一切之上（参考的 Dialog::QuickInfo 就画在最后），它只活在右键按下的这段时间里。
        _heroInfo?.Draw(_frame.Pixels);
        _frame.InvalidateAll();
    }

    /// <summary>Paints the town screen or the one sub-window that is open.</summary>
    /// <param name="state">Unused: the town screen draws all of its text with the game fonts.</param>
    private void DrawWindows(in DiagnosticsState state)
    {
        _ = state;

        // 酒馆的框只盖住屏幕中间一块：`showStandardTextMessage` 是在当前画面上摆框，
        // 所以城镇屏每帧照常重画一遍，再把框叠上去。
        if (_tavern is { } tavern)
        {
            DrawTown();
            tavern.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        // 招募英雄的确认框叠在建造列表上：列表是整屏的，所以先贴回开窗前的城镇屏再画列表，最后画确认框。
        if (_recruitHero is { } recruitHero)
        {
            if (_construction is { } list)
            {
                if (_townSnapshot is { } snapshot && ReferenceEquals(_snapshotCastle, Castle))
                {
                    ImageOps.Blit(_frame.Pixels, new Point(0, 0), snapshot, snapshot.Bounds);
                }

                list.Draw(_frame.Pixels);
            }

            recruitHero.Draw(_frame.Pixels);
            _message?.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        // 市场窗口也只盖住屏幕中间一块，四周还看得见城镇屏；底图同样留在 _townSnapshot 里，
        // 每帧只重画窗口本身（FrameBox 是不透明的整块石面），说明框再叠在它上面。
        if (_marketplace is { } market)
        {
            market.Draw(_frame.Pixels);
            _message?.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        // 招募窗口只盖住屏幕中间那一块，底下的水井屏要照旧画出来；从城镇屏开的时候底图已经留在
        // _townSnapshot 里，不能每帧重画城镇屏——底栏的透明口子会把上一帧的窗口按钮当画布留下来。
        if (_recruit is { } recruit)
        {
            _well?.Draw(_frame.Pixels);
            recruit.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        // 子窗口的背景是一整张 640×480 的石面，盖住城镇屏，所以只画它一个。
        if (_mageGuild is { } window)
        {
            window.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        if (_well is { } well)
        {
            well.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        if (_thievesGuild is { } guild)
        {
            guild.Draw(_frame.Pixels);
            _message?.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        // 建造说明框和市场的框一样只盖住中间一块，底图留在 _townSnapshot 里，每帧只重画框本身。
        if (_build is { } build)
        {
            build.Draw(_frame.Pixels);
            _message?.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        // 建造列表是整屏的窗口，但底栏与状态条带透明口子；每帧先把开窗前的城镇屏贴回去再画窗口，
        // 免得上一帧的状态文字从透明处透出来（参考在干净的 surface 上整块重画）。
        if (_construction is { } construction)
        {
            if (_townSnapshot is { } snapshot && ReferenceEquals(_snapshotCastle, Castle))
            {
                ImageOps.Blit(_frame.Pixels, new Point(0, 0), snapshot, snapshot.Bounds);
            }

            construction.Draw(_frame.Pixels);
            _message?.Draw(_frame.Pixels);
            _frame.InvalidateAll();

            return;
        }

        DrawTown();

        // 城镇屏自己的「每日收入」说明框浮在底栏之上，底下的城照旧画出来。
        _message?.Draw(_frame.Pixels);
    }

    /// <summary>The town screen draws all of its text with the game fonts.</summary>
    /// <param name="state">Unused.</param>
    public void DrawOverlay(in DiagnosticsState state)
    {
    }

    /// <summary>
    /// Opens the window a building stands for: the <c>BUILD_MAGEGUILD</c> and dwelling branches of
    /// <c>castle_dialog.cpp:613-687</c>.
    /// </summary>
    /// <param name="building">The building under the click.</param>
    /// <returns><see langword="true"/> when a window opened.</returns>
    public bool OpenBuilding(BuildingType building)
    {
        // 没建好的建筑（城建窗口里才会点到）走确认框，`BuildingInfo::QueueEventProcessing()`
        // 的 <c>Dialog::BuyBuilding( true )</c>（buildinginfo.cpp:396-403）。
        if (building is not BuildingType.Nothing && !Castle.IsBuild(building))
        {
            return OpenBuild(building);
        }

        // 点城门/城堡本身开建造列表，`castle_dialog.cpp:761-764` 的 `BUILD_CASTLE` 分支。
        if (building is BuildingType.Castle)
        {
            return OpenConstruction();
        }

        if (IsMageGuild(building))
        {
            return OpenMageGuild();
        }

        if (building is BuildingType.Well)
        {
            return OpenWell();
        }

        if (building is BuildingType.Marketplace)
        {
            return OpenMarketplace();
        }

        if (building is BuildingType.ThievesGuild)
        {
            return OpenThievesGuild();
        }

        // 酒馆点开的是吧台后的闲话，`castle_dialog.cpp:697-701` 的 `BUILD_TAVERN` 分支。
        if (building is BuildingType.Tavern)
        {
            return OpenTavern();
        }

        // 住宅（含升级后的）点了就开招募窗口，`castle_dialog.cpp:683-687`。
        if (Castle.GetActualDwelling(building) is var dwelling and not BuildingType.Nothing)
        {
            return OpenRecruit(dwelling);
        }

        GameLog.Info($"The '{building}' window of '{Castle.Name}' is not wired up yet.");

        return false;
    }

    /// <summary>
    /// Opens the confirm dialog of a building, <c>Dialog::BuyBuilding( true )</c>
    /// (<c>buildinginfo.cpp:435-537</c>).
    /// </summary>
    /// <param name="building">The building to offer.</param>
    /// <returns><see langword="true"/> when the dialog opened.</returns>
    public bool OpenBuild(BuildingType building)
    {
        if (_build is not null)
        {
            return true;
        }

        if (building is BuildingType.Nothing)
        {
            return false;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The building dialog needs the archives, and they were not loaded.");

            return false;
        }

        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("The building dialog needs a kingdom, and the scene has none.");

            return false;
        }

        BuildingInfo info = new(Castle, building, kingdom.Treasury);
        BuildWindow? window = BuildWindow.TryCreate(assets, info, _fonts);

        if (window is null)
        {
            GameLog.Warn($"The building dialog of '{Castle.Name}' cannot show '{building}'.");

            return false;
        }

        RememberTownFrame();
        _build = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the '{building}' dialog of '{Castle.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the construction list, <c>Castle::_openConstructionDialog()</c>
    /// (<c>castle_town.cpp:171-186</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the window is on screen.</returns>
    public bool OpenConstruction()
    {
        if (!Castle.IsBuild(BuildingType.Castle))
        {
            GameLog.Info($"'{Castle.Name}' has no castle to build in.");

            return false;
        }

        if (ConstructionWindow.TryCreate(Castle, _kingdom, _world, _fonts) is not { } window)
        {
            GameLog.Warn($"The construction window of '{Castle.Name}' is missing a frame.");

            return false;
        }

        RememberTownFrame();
        _construction = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the construction window of '{Castle.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the recruit hero dialog of a hero slot, <c>Castle::DialogBuyHero( hero )</c>
    /// (<c>castle_town.cpp:66-160</c>) as asked for by the slot at <c>castle_town.cpp:691-694</c>.
    /// </summary>
    /// <param name="hero">The hero on offer, one of <c>Kingdom::GetRecruits()</c>.</param>
    /// <returns><see langword="true"/> when the dialog is on screen.</returns>
    public bool OpenRecruitHero(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (_recruitHero is not null)
        {
            return true;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The recruit hero dialog needs the archives, and they were not loaded.");

            return false;
        }

        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("The recruit hero dialog needs a kingdom, and the scene has none.");

            return false;
        }

        // 参考把 AllowBuyHero() 的结果交给对话框决定 OKAY 的灰化（castle_town.cpp:141）。
        RecruitHeroDialog dialog = RecruitHeroDialog.Create(hero, Castle.AllowBuyHero(_view.Hero, kingdom));
        RecruitHeroWindow? window = RecruitHeroWindow.TryCreate(assets, dialog, _fonts);

        if (window is null)
        {
            GameLog.Warn($"The recruit hero dialog of '{Castle.Name}' cannot show '{hero.Name}'.");

            return false;
        }

        _recruitHero = window;
        window.SetCursor(_input.Cursor);

        GameLog.Info($"Opened the recruit hero dialog of '{hero.Name}'.");

        return true;
    }

    /// <summary>
    /// Opens the recruit hero dialog of the n-th hero on offer, <c>--town-hero</c>.
    /// </summary>
    /// <param name="index">0 for <c>hero1</c>, 1 for <c>hero2</c>.</param>
    /// <returns><see langword="true"/> when the dialog is on screen.</returns>
    public bool OpenRecruitHero(int index)
    {
        if (_kingdom is not { } kingdom || _world is not { } world)
        {
            GameLog.Warn("The recruit hero dialog needs a kingdom and a world, and the scene has neither.");

            return false;
        }

        Recruits recruits = kingdom.GetRecruits(world);
        Hero? hero = index switch
        {
            0 => recruits.GetHero1(world.Heroes),
            1 => recruits.GetHero2(world.Heroes),
            _ => null,
        };

        return hero is not null && OpenRecruitHero(hero);
    }

    /// <summary>
    /// Shows the hero quick info box next to the cursor, <c>Dialog::QuickInfo( const Hero &amp; )</c>
    /// (<c>dialog_quickinfo.cpp:693-911</c>).
    /// </summary>
    /// <remarks>
    /// 参考是个阻塞的模态框：按住右键期间一直显示，松开就退出。端口把它做成一帧一帧画的浮层，由
    /// <see cref="SetRightButton"/> 在按下时建、松开时收（那一句「松开就收」在方法开头）。
    /// </remarks>
    /// <param name="hero">The hero to describe.</param>
    private void ShowHeroQuickInfo(Hero hero)
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The hero quick info box needs the archives, and the scene has none.");

            return;
        }

        // 小肖像就是 MINIPORT 的「编号 - 1」帧（heroes.cpp 的 GetPortrait( PORT_SMALL )）。
        Sprite? portrait = assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Miniport)) is { } miniPortraits &&
                           miniPortraits.Count >= hero.Id
            ? miniPortraits.Sprites[hero.Id - 1]
            : null;

        ShowQuickInfo(HeroQuickInfoSubject.FromHero(hero, portrait));
    }

    /// <summary>
    /// Shows the quick info box of the town's captain, <c>Dialog::QuickInfo( GetCaptain() )</c>
    /// (<c>castle_dialog.cpp:547-548</c>).
    /// </summary>
    /// <remarks>
    /// 船长的框与英雄的框是同一个，区别只在内容：没有等级后缀、没有移动力那一行，名字是城镇名，头像是
    /// <c>MINICAPT</c> 的种族帧（<c>dialog_quickinfo.cpp:756</c> 的 <c>GetPortrait( PORT_SMALL )</c>）。
    /// </remarks>
    /// <param name="captain">The captain of the town on screen.</param>
    private void ShowCaptainQuickInfo(Captain captain)
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The captain quick info box needs the archives, and the scene has none.");

            return;
        }

        ShowQuickInfo(HeroQuickInfoSubject.FromCaptain(captain, CaptainPortrait.TryCreateSmall(assets, captain.Race)));
    }

    /// <summary>Builds and shows the quick info box of a hero or of a captain.</summary>
    /// <param name="subject">The hero or captain to describe.</param>
    private void ShowQuickInfo(HeroQuickInfoSubject subject)
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The quick info box needs the archives, and the scene has none.");

            return;
        }

        _heroInfoArtwork ??= HeroQuickInfoArtwork.TryCreate(assets, evilInterface: false);

        if (_heroInfoArtwork is not { } artwork)
        {
            GameLog.Warn("The hero quick info box needs QWIKHERO, LOCATORS, MINILKMR and FLAG32, and one of them was not loaded.");

            return;
        }

        _miniMonsters ??= Mons32Sprites.TryCreate(assets);

        if (_miniMonsters is not { } sprites)
        {
            GameLog.Warn("The hero quick info box needs MONS32 for the army line, and it was not loaded.");

            return;
        }

        Rect interfaceArea = new(0, 0, _frame.Pixels.Width, _frame.Pixels.Height);

        if (HeroQuickInfoView.TryCreate(
                subject,
                fullInfo: true,
                artwork,
                _fonts.Small,
                sprites,
                _input.Cursor ?? new Point(interfaceArea.Width / 2, interfaceArea.Height / 2),
                interfaceArea) is not { } view)
        {
            GameLog.Warn($"The quick info box of '{subject.Name}' does not fit the {interfaceArea.Width}x{interfaceArea.Height} screen.");

            return;
        }

        _heroInfo = view;
    }

    /// <summary>Acts on the request of the construction list, <c>castle_town.cpp:600-768</c>.</summary>
    /// <param name="action">What the window asked for.</param>
    private void FollowConstruction(ConstructionAction action)
    {
        switch (action.Target)
        {
            case ConstructionActionTarget.Exit:
                _construction = null;
                RestoreTownFrame();

                GameLog.Info("Closing the construction window.");
                break;

            case ConstructionActionTarget.Build:
                // 参考点格子只是弹价格框（castle_town.cpp:665-676 的 Dialog::BuyBuilding( true )），真正扣钱在那边。
                _construction = null;
                OpenBuild(action.Building);
                break;

            case ConstructionActionTarget.PreviousTown:
                if (SwitchTo(-1))
                {
                    _construction = null;
                    OpenConstruction();
                }

                break;

            case ConstructionActionTarget.NextTown:
                if (SwitchTo(1))
                {
                    _construction = null;
                    OpenConstruction();
                }

                break;

            case ConstructionActionTarget.Income:
                ShowIncome();
                break;

            case ConstructionActionTarget.FirstHero:
                if (_construction?.FirstHero is { } first)
                {
                    OpenRecruitHero(first);
                }

                break;

            case ConstructionActionTarget.SecondHero:
                if (_construction?.SecondHero is { } second)
                {
                    OpenRecruitHero(second);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Shows the help behind a right click of the construction list, <c>castle_town.cpp:636-743</c>.
    /// </summary>
    /// <param name="help">What the right click landed on.</param>
    private void FollowConstructionHelp(ConstructionHelp help)
    {
        switch (help.Target)
        {
            case ConstructionHelpTarget.Income:
                ShowIncome(heldByRightButton: true);
                break;

            case ConstructionHelpTarget.Exit:
            case ConstructionHelpTarget.Building:
            case ConstructionHelpTarget.SpreadFormation:
            case ConstructionHelpTarget.GroupedFormation:
                if (!HasMessageFrame())
                {
                    GameLog.Warn("The construction help window needs the message frame, and it was not loaded.");

                    return;
                }

                ShowMessage(
                    MessageBox.Create(help.Title, help.Text, _fonts.Yellow, _fonts.Normal),
                    heldByRightButton: true);
                break;

            case ConstructionHelpTarget.FirstHero:
                if (_construction?.FirstHero is { } first)
                {
                    ShowHeroQuickInfo(first);
                }

                break;

            case ConstructionHelpTarget.SecondHero:
                if (_construction?.SecondHero is { } second)
                {
                    ShowHeroQuickInfo(second);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Shows why the building cannot be built while OKAY is right clicked,
    /// <c>showStandardTextMessage( _( "Okay" ), GetConditionDescription(), Dialog::ZERO )</c>
    /// (<c>buildinginfo.cpp:553-555</c>).
    /// </summary>
    /// <param name="build">The dialog whose OKAY is under the pointer.</param>
    private void ShowBuildOkayHelp(BuildWindow build)
    {
        if (!HasMessageFrame())
        {
            GameLog.Warn("The building help window needs the message frame, and it was not loaded.");

            return;
        }

        ShowMessage(
            MessageBox.Create("Okay", build.Info.GetConditionDescription(), _fonts.Yellow, _fonts.Normal),
            heldByRightButton: true);

        GameLog.Info($"Showing the condition of '{build.Info.Type}'.");
    }

    /// <summary>
    /// Shows what CANCEL does while it is right clicked,
    /// <c>showStandardTextMessage( _( "Cancel" ), _( "Exit this menu without doing anything." ), Dialog::ZERO )</c>
    /// (<c>buildinginfo.cpp:556-558</c>).
    /// </summary>
    private void ShowBuildCancelHelp()
    {
        if (!HasMessageFrame())
        {
            GameLog.Warn("The cancel help window needs the message frame, and it was not loaded.");

            return;
        }

        ShowMessage(
            MessageBox.Create(
                "Cancel",
                "Exit this menu without doing anything.",
                _fonts.Yellow,
                _fonts.Normal),
            heldByRightButton: true);
    }

    /// <summary>Acts on the answer of the building dialog.</summary>
    /// <param name="action">What the dialog asked for.</param>
    private void FollowBuild(BuildAction action)
    {
        switch (action)
        {
            case BuildAction.Exit:
                _build = null;
                RestoreTownFrame();

                GameLog.Info("Closing the building dialog.");
                break;

            case BuildAction.Build:
                BuyBuilding();
                break;

            default:
                break;
        }
    }

    /// <summary>Acts on the answer of the recruit hero dialog, <c>recruitHeroDialog()</c> (<c>castle_town.cpp:485-500</c>).</summary>
    /// <param name="action">What the dialog asked for.</param>
    private void FollowRecruitHero(RecruitHeroAction action)
    {
        switch (action)
        {
            case RecruitHeroAction.Exit:
                // 取消只收起确认框，建造列表照旧开着（参考的循环里 DialogBuyHero 只是个模态框）。
                _recruitHero = null;

                GameLog.Info("Closing the recruit hero dialog.");
                break;

            case RecruitHeroAction.Hire:
                HireRecruitedHero();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Hires the hero the dialog shows and goes back to the town screen,
    /// <c>castle->RecruitHero( hero )</c> of <c>recruitHeroDialog()</c> (<c>castle_town.cpp:491-496</c>) and the
    /// <c>ConstructionDialogResult::RecruitHero</c> branch of <c>castle_dialog.cpp:790-800</c>.
    /// </summary>
    private void HireRecruitedHero()
    {
        if (_recruitHero is not { } dialog)
        {
            return;
        }

        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("Recruiting a hero needs a kingdom, and the scene has none.");

            return;
        }

        Hero hero = dialog.Dialog.Hero;

        // 建造列表在参考里是同一次调用返回的，招到人之后整屏回到城镇，英雄位换成新英雄的像。
        _recruitHero = null;
        _construction = null;

        if (Castle.RecruitHero(hero, kingdom, _world, _view.Hero) is null)
        {
            GameLog.Warn($"'{Castle.Name}' did not recruit '{hero.Name}'.");
        }
        else
        {
            GameLog.Info($"Recruited '{hero.Name}' in '{Castle.Name}'.");
        }

        _townSnapshot = null;
        DrawTown();
    }

    /// <summary>
    /// Shows the help behind a right click of the recruit hero dialog, <c>castle_town.cpp:150-158</c>.
    /// </summary>
    /// <param name="button">The button the right click landed on.</param>
    private void ShowRecruitHeroHelp(DialogButton button)
    {
        if (_recruitHero is not { } dialog)
        {
            return;
        }

        (string Title, string Text) help = button switch
        {
            DialogButton.Ok => ("Okay", dialog.Dialog.ConfirmText),
            DialogButton.Cancel => ("Cancel", "Exit this menu without doing anything."),
            _ => default,
        };

        if (help.Title is null || !HasMessageFrame())
        {
            return;
        }

        ShowMessage(
            MessageBox.Create(help.Title, help.Text, _fonts.Yellow, _fonts.Normal),
            heldByRightButton: true);
    }

    /// <summary>
    /// Pays for the building the dialog shows and repaints the town, <c>Castle::BuyBuilding()</c> of the OKAY
    /// branch (<c>buildinginfo.cpp:404-408</c>).
    /// </summary>
    private void BuyBuilding()
    {
        if (_build is not { } window || _kingdom is not { } kingdom)
        {
            return;
        }

        BuildingType building = window.Info.Type;

        if (!Castle.BuyBuilding(building, ref kingdom.Treasury))
        {
            GameLog.Warn($"'{Castle.Name}' did not build '{building}'.");

            return;
        }

        _build = null;

        // 城里的建筑换了，留底的画面已经过期，直接按新状态重画。
        _townSnapshot = null;
        _snapshotCastle = null;
        DrawTown();

        GameLog.Info($"Built '{building}' in '{Castle.Name}'.");
    }

    /// <summary>Whether a building is one of the five levels of the mage guild, the <c>BUILD_MAGEGUILD</c> mask.</summary>
    /// <param name="building">The building.</param>
    /// <returns><see langword="true"/> for any level of the guild.</returns>
    private static bool IsMageGuild(BuildingType building) => building
        is BuildingType.MageGuild1 or BuildingType.MageGuild2 or BuildingType.MageGuild3
        or BuildingType.MageGuild4 or BuildingType.MageGuild5;

    /// <summary>Acts on the answer of the mage guild window, <c>castle_dialog.cpp:596-612</c>.</summary>
    /// <param name="frame">The frame the window just produced.</param>
    private void FollowMageGuild(MageGuildFrame frame)
    {
        switch (frame.Action)
        {
            case MageGuildDialogAction.Exit:
                _mageGuild = null;
                RestoreTownFrame();

                GameLog.Info("Closing the mage guild window.");
                break;

            case MageGuildDialogAction.PreviousTown:
                if (SwitchTo(-1))
                {
                    OpenMageGuild();
                }

                break;

            case MageGuildDialogAction.NextTown:
                if (SwitchTo(1))
                {
                    OpenMageGuild();
                }

                break;

            case MageGuildDialogAction.ShowSpell:
                GameLog.Info($"The description of '{frame.Spell}' is not wired up yet.");
                break;

            default:
                break;
        }
    }

    /// <summary>Acts on the answer of the well window, the body of <c>Castle::_openWell()</c>'s loop.</summary>
    /// <param name="frame">The frame the window just produced.</param>
    private void FollowWell(WellDialogFrame frame)
    {
        switch (frame.Action)
        {
            case WellDialogAction.Exit:
                _well = null;
                RestoreTownFrame();

                GameLog.Info("Closing the well window.");
                break;

            case WellDialogAction.Max:
                RecruitEverything();
                break;

            case WellDialogAction.Recruit:
                OpenRecruit(frame.DwellingLevel);
                break;

            case WellDialogAction.DwellingInfo:
                GameLog.Info($"The description of dwelling {frame.DwellingLevel} is not wired up yet.");
                break;

            default:
                break;
        }
    }

    /// <summary>Acts on the answer of the marketplace window, <c>dialog_marketplace.cpp:385-685</c>.</summary>
    /// <param name="action">What the window just produced.</param>
    private void FollowMarketplace(MarketplaceAction action)
    {
        switch (action)
        {
            case MarketplaceAction.Exit:
                _marketplace = null;
                RestoreTownFrame();

                GameLog.Info("Closing the marketplace window.");
                break;

            case MarketplaceAction.Trade:
                GameLog.Info($"Made a trade in the marketplace of '{Castle.Name}'.");
                break;

            case MarketplaceAction.ResourceIncome:
                ShowIncome(heldByRightButton: true);
                break;

            case MarketplaceAction.ExchangeRate:
                ShowExchangeRate();
                break;

            case MarketplaceAction.Gift:
                GameLog.Info("The gift window is not wired up yet.");
                break;

            default:
                break;
        }
    }

    /// <summary>Acts on the answer of the thieves' guild window, <c>dialog_thievesguild.cpp:448-473</c>.</summary>
    /// <param name="action">What the window just produced.</param>
    private void FollowThievesGuild(ThievesGuildAction action)
    {
        switch (action)
        {
            case ThievesGuildAction.Exit:
                _thievesGuild = null;
                RestoreTownFrame();

                GameLog.Info("Closing the thieves' guild window.");
                break;

            case ThievesGuildAction.ExitHelp:
                ShowThievesGuildExitHelp();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Shows "Exit this menu." while EXIT is right clicked, <c>showStandardTextMessage( _( "Exit this menu." ), …,
    /// Dialog::ZERO )</c> (<c>dialog_thievesguild.cpp:465-470</c>).
    /// </summary>
    private void ShowThievesGuildExitHelp()
    {
        if (!HasMessageFrame())
        {
            GameLog.Warn("The exit help window needs the message frame, and it was not loaded.");

            return;
        }

        ShowMessage(
            MessageBox.Create("Exit", ThievesGuildView.ExitHelpText, _fonts.Yellow, _fonts.Normal),
            heldByRightButton: true);
    }

    /// <summary>
    /// Shows the daily income of the kingdom, <c>Kingdom::GetIncome( INCOME_ALL )</c> in a message box: the
    /// income target of the town screen (<c>Dialog::OK</c>) and the marketplace's right click
    /// (<c>Dialog::ZERO</c>, <c>dialog_marketplace.cpp:657-666</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 OK 框是「点一下收起来」，ZERO 框是「松开右键就消失」；端口的 <see cref="MessageBox"/> 没有按钮，
    /// 这两种生命周期都由场景记（<see cref="_messageHeldByRightButton"/>）。
    /// </remarks>
    /// <param name="heldByRightButton">Whether the box lives only while the right button is held.</param>
    private void ShowIncome(bool heldByRightButton = false)
    {
        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("The income window needs a kingdom, and the scene has none.");

            return;
        }

        if (MessageResourceIcons() is not { } icons || !HasMessageFrame())
        {
            GameLog.Warn("The income window needs RESOURCE and the message frame, and they were not loaded.");

            return;
        }

        Funds income = kingdom.GetIncome(resource => _world.CountCapturedMines(resource, kingdom.Color));

        ShowMessage(KingdomIncome.Create(income, icons, _fonts.Yellow, _fonts.Normal), heldByRightButton);

        GameLog.Info($"Showing the daily income of '{Castle.Name}'.");
    }

    /// <summary>
    /// Shows the exchange rate sentence,
    /// <c>showStandardTextMessage( _( "Marketplace" ), getExchangeRateText( … ) )</c>
    /// (<c>dialog_marketplace.cpp:596-604</c>).
    /// </summary>
    private void ShowExchangeRate()
    {
        if (_marketplace is not { } market)
        {
            return;
        }

        if (!HasMessageFrame())
        {
            GameLog.Warn("The exchange rate window needs the message frame, and it was not loaded.");

            return;
        }

        ShowMessage(
            MessageBox.Create(
                MarketplaceView.HeaderText,
                market.Dialog.ExchangeRateText,
                _fonts.Yellow,
                _fonts.Normal),
            heldByRightButton: false);

        GameLog.Info($"Showing the exchange rate of '{Castle.Name}'.");
    }

    /// <summary>Puts a message box over the screen; the next press or ESC takes it down again.</summary>
    private void ShowMessage(MessageBox box, bool heldByRightButton)
    {
        _message = box;
        _messageHeldByRightButton = heldByRightButton;
    }

    /// <summary>
    /// Whether the archive can provide the message frame every box is drawn in, checked once. The
    /// pieces themselves are read by <see cref="FrameBox"/>.
    /// </summary>
    private bool HasMessageFrame()
    {
        if (_hasMessageFrame is null && _assets is { } assets)
        {
            _hasMessageFrame = assets.TryReadIcn(FrameBox.ArtworkFileName) is { } file &&
                file.Count >= FrameBox.RequiredSpriteCount;
        }

        return _hasMessageFrame == true;
    }

    /// <summary>Frames of <c>RESOURCE.ICN</c>, loaded once for the income box.</summary>
    private IReadOnlyList<Sprite>? MessageResourceIcons()
    {
        if (_messageResourceIcons is null && _assets is { } assets)
        {
            _messageResourceIcons = assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Resource))?.Sprites;
        }

        return _messageResourceIcons;
    }

    /// <summary>
    /// Opens the recruit window of one dwelling of the well, the <c>Dialog::RecruitMonster()</c> call of
    /// <c>castle_well.cpp:263-271</c>.
    /// </summary>
    /// <param name="level">The dwelling level, 1 to 6.</param>
    /// <returns><see langword="true"/> when the window opened.</returns>
    private bool OpenRecruit(int level)
    {
        if (_well is not { } well)
        {
            return false;
        }

        if (well.GetDwelling(level) is not { IsBuilt: true } row)
        {
            GameLog.Info($"Dwelling {level} of '{Castle.Name}' is not built.");

            return false;
        }

        return ShowRecruit(
            row.Monster,
            row.Available,
            RecruitWindow.WellWindowOffsetY,
            $"dwelling {level}");
    }

    /// <summary>
    /// Opens the recruit window of a dwelling of the town picture, the <c>Dialog::RecruitMonster()</c> call of
    /// <c>castle_dialog.cpp:683-687</c>.
    /// </summary>
    /// <param name="dwelling">The dwelling, already resolved by <see cref="Castle.GetActualDwelling"/>.</param>
    /// <returns><see langword="true"/> when the window opened.</returns>
    private bool OpenRecruit(BuildingType dwelling) => ShowRecruit(
        DwellingMonsters.FromDwelling(Castle.Race, dwelling),
        Castle.GetMonstersInDwelling(dwelling),
        RecruitWindow.CastleWindowOffsetY,
        dwelling.ToString());

    /// <summary>Builds the window both entries share, <c>Dialog::RecruitMonster()</c> up to its loop.</summary>
    /// <param name="monster">The unit on offer.</param>
    /// <param name="available">How many units the dwelling holds.</param>
    /// <param name="windowOffsetY">How far the window is pushed down.</param>
    /// <param name="source">What opened it, for the log.</param>
    /// <returns><see langword="true"/> when the window opened.</returns>
    private bool ShowRecruit(MonsterType monster, uint available, int windowOffsetY, string source)
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The recruit window needs the archives, and they were not loaded.");

            return false;
        }

        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("The recruit window needs a kingdom, and the scene has none.");

            return false;
        }

        _recruit = RecruitWindow.TryCreate(assets, Castle, monster, available, kingdom, _fonts, windowOffsetY);

        if (_recruit is not { } window)
        {
            GameLog.Warn($"The recruit window of {source} in '{Castle.Name}' could not be built.");

            return false;
        }

        window.SetCursor(_input.Cursor);

        // 从城镇屏开的时候窗口底下就是城镇屏本身，先留底，关窗时贴回去；
        // 从水井开的时候底下是整屏的水井，每帧重画即可，不需要留底。
        if (_well is null)
        {
            RememberTownFrame();
        }

        GameLog.Info($"Opened the recruit window of {source} in '{Castle.Name}': {monster} x{available}.");

        return true;
    }

    /// <summary>
    /// Closes the recruit window and books its answer, <c>Castle::RecruitMonster( result )</c> of the well's loop.
    /// </summary>
    /// <param name="window">The window that just closed.</param>
    private void CloseRecruit(RecruitWindow window)
    {
        _recruit = null;

        if (window.Selected is { Count: > 0 } troop)
        {
            if (_kingdom is { } kingdom && Castle.RecruitMonster(troop, ref kingdom.Treasury))
            {
                GameLog.Info($"Recruited {troop.Count} '{troop.Monster}' in '{Castle.Name}'.");

                // 住宅的存货变了，水井屏的那几行得按新数重排。
                _well?.Refresh(Castle);
            }
            else
            {
                GameLog.Warn($"The recruitment of {troop.Count} '{troop.Monster}' in '{Castle.Name}' was refused.");
            }
        }
        else
        {
            GameLog.Info("Closing the recruit window.");
        }

        // 从城镇屏开的窗口底下没有整屏的底图，关窗后得把留底贴回去：底栏的透明口子在下一帧
        // 会被当作画布，不还原就会露出窗口的按钮（参考在这一步重画整个城镇屏）。
        if (_well is null)
        {
            RestoreTownFrame();
        }
    }

    /// <summary>
    /// The MAX button, <c>Castle::_recruitCastleMax( GetArmy() )</c> of <c>castle_well.cpp:125-201</c>.
    /// </summary>
    /// <remarks>
    /// 参考先把计划算好再一次扣钱入兵，并在完成后弹一条「招募了多少」的说明；端口没有消息框，
    /// 结论走日志。<see cref="CastleMaxRecruitment"/> 会在副本上模拟驻军腾位置，所以这里得先复制一份驻军，
    /// 不能把 <see cref="Castle.Army"/> 直接交出去。
    /// </remarks>
    private void RecruitEverything()
    {
        if (_kingdom is not { } kingdom)
        {
            GameLog.Warn("Recruiting everything needs a kingdom, and the scene has none.");

            return;
        }

        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(Castle, CopyArmy(Castle.Army), null, kingdom.Treasury);

        if (!plan.HasSomethingToRecruit)
        {
            GameLog.Info(plan.RefusalText ?? "There is nothing to recruit.");

            return;
        }

        int stacks = 0;

        foreach (CastleMaxRecruitment.Entry entry in plan.Entries)
        {
            if (Castle.RecruitMonster(new Troop(entry.Monster, entry.Count), ref kingdom.Treasury))
            {
                ++stacks;
            }
        }

        _well?.Refresh(Castle);

        GameLog.Info($"Recruited {stacks} stack(s) in '{Castle.Name}': {plan.Summary}");
    }

    /// <summary>Copies a garrison, so that a recruitment plan can be simulated without touching it.</summary>
    /// <param name="army">The army to copy.</param>
    /// <returns>An army with the same colour and stacks.</returns>
    private static Army CopyArmy(Army army)
    {
        Army copy = new(army.Race)
        {
            Color = army.Color,
        };

        foreach (Troop troop in army)
        {
            if (troop.IsValid)
            {
                copy.JoinTroop(troop);
            }
        }

        return copy;
    }

    /// <summary>
    /// Keeps the town picture of this frame, so that closing a window can put it back.
    /// </summary>
    /// <remarks>
    /// 城镇屏的底栏是 640×205 的贴图，几个兵栏的口子是透明的，透出来的是帧缓冲里原有的东西。
    /// 窗口铺满整屏之后再重画城镇屏，那些口子就会露出窗口的文字和图标（<c>castle_dialog.cpp:136</c> 的
    /// <c>Blit</c> 也是跳过透明像素的，参考同样吃这一口）。留一份开窗前的画面，关窗时贴回去，
    /// 表现就和没开过窗口一样。
    /// </remarks>
    private void RememberTownFrame()
    {
        // 换城之后帧缓冲里还是上一座城的画面，先按本城重画一遍再留底。
        DrawTown();

        _townSnapshot = _frame.Pixels.Clone();
        _snapshotCastle = Castle;
    }

    /// <summary>Paints the town screen into the frame buffer.</summary>
    private void DrawTown()
    {
        string status = _input.Cursor is { } cursor ? _view.GetStatusMessage(cursor) : string.Empty;

        CastleScreenFrame frame = new()
        {
            Funds = _kingdom is { } kingdom ? kingdom.Treasury : default,
            AnimationIndex = _animationIndex,
            World = _world,
            StatusText = status.Length == 0 ? _world.DateString() : status,
            ExitPressed = _input.ExitPressed,
            PreviousPressed = _input.PreviousPressed,
            NextPressed = _input.NextPressed,
            CaptainPortrait = Castle is { } castle ? TownCaptainPortrait(castle) : null,
        };

        _view.Draw(_frame.Pixels, frame);
        _frame.InvalidateAll();
    }

    /// <summary>Puts back the picture kept by <see cref="RememberTownFrame"/>; a town switch drops it.</summary>
    private void RestoreTownFrame()
    {
        if (_townSnapshot is { } snapshot && ReferenceEquals(_snapshotCastle, Castle))
        {
            ImageOps.Blit(_frame.Pixels, new Point(0, 0), snapshot, snapshot.Bounds);
            _frame.InvalidateAll();
        }

        _townSnapshot = null;
        _snapshotCastle = null;
    }

    private bool SwitchTo(int direction)
    {
        if (_kingdom is not { } kingdom || kingdom.Castles.Count < 2)
        {
            return false;
        }

        int index = -1;

        for (int i = 0; i < kingdom.Castles.Count; ++i)
        {
            if (ReferenceEquals(kingdom.Castles[i], Castle))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return false;
        }

        int next = (index + direction + kingdom.Castles.Count) % kingdom.Castles.Count;
        Show(kingdom.Castles[next]);

        GameLog.Info($"Showing the town '{Castle.Name}'.");

        return true;
    }

    private void Show(Castle castle)
    {
        _view.Show(castle, StandingHero(castle));

        _input.CanSwitchCastle = (_kingdom?.Castles.Count ?? 0) > 1;

        _animationIndex = 1;
        _animationTimer = 0;
    }

    /// <summary>
    /// The big captain portrait of the town on screen, with its color flag on it,
    /// <c>GetCaptain().GetPortrait( PORT_BIG )</c> (<c>castle_dialog.cpp:138-141</c>).
    /// </summary>
    /// <remarks>
    /// 每座城只合成一次：同样的城与同样的颜色直接复用缓存。船长住处是这一屏里建的，所以缓存在画的时候才建立，
    /// 而不是切城的时候——建好住处之后第一帧就能看到船长。
    /// </remarks>
    /// <param name="castle">The town on screen.</param>
    /// <returns>The portrait, or <see langword="null"/> when the town has no captain.</returns>
    private Sprite? TownCaptainPortrait(Castle castle)
    {
        if (!castle.IsBuild(BuildingType.Captain))
        {
            return null;
        }

        if (_captainPortraitTried && ReferenceEquals(_captainPortraitOwner, castle) && _captainPortraitColor == castle.Color)
        {
            return _captainPortrait;
        }

        _captainPortraitOwner = castle;
        _captainPortraitColor = castle.Color;
        _captainPortraitTried = true;
        _captainPortrait = _assets is { } assets ? CaptainPortrait.TryCreateBig(assets, castle.Captain) : null;

        if (_captainPortrait is null)
        {
            GameLog.Warn($"The big captain portrait of {castle.Race} could not be loaded; the crest is drawn instead.");
        }

        return _captainPortrait;
    }

    /// <summary>
    /// The hero of the kingdom standing in a town, <c>Castle::GetHero()</c>: the hero whose tile belongs
    /// to the town's footprint.
    /// </summary>
    /// <param name="castle">The town.</param>
    /// <returns>The hero, or <see langword="null"/> when nobody stands there.</returns>
    private Hero? StandingHero(Castle castle)
    {
        if (_kingdom is not { } kingdom)
        {
            return null;
        }

        foreach (Hero hero in kingdom.Heroes)
        {
            if (hero.MapIndex >= 0 && ReferenceEquals(_world.GetCastle(hero.MapIndex), castle))
            {
                return hero;
            }
        }

        return null;
    }
}
