using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What a click on the new game menu asks the caller to do, the <c>fheroes2::GameMode</c> values
/// <c>Game::NewGame()</c> returns.
/// </summary>
/// <remarks>
/// The items that only switch the menu to another set of buttons - the campaign choice and the hot
/// seat choice - have no value here: they are answered with <see cref="None"/> and visible in
/// <see cref="NewGameMenuView.State"/>.
/// </remarks>
public enum NewGameMenuAction
{
    /// <summary>Nothing was picked.</summary>
    None,

    /// <summary>A single player game on a single map, <c>NEW_STANDARD</c>.</summary>
    StandardGame,

    /// <summary>A battle without a map, <c>NEW_BATTLE_ONLY</c>.</summary>
    BattleOnly,

    /// <summary>The settings of the game, <c>MAIN_MENU</c> after the settings window closes.</summary>
    GameSettings,

    /// <summary>Back to the main menu, <c>MAIN_MENU</c>.</summary>
    Cancel,

    /// <summary>Roland's or Archibald's campaign, <c>NEW_SUCCESSION_WARS_CAMPAIGN</c>.</summary>
    SuccessionWarsCampaign,

    /// <summary>One of the four expansion campaigns, <c>NEW_PRICE_OF_LOYALTY_CAMPAIGN</c>.</summary>
    PriceOfLoyaltyCampaign,

    /// <summary>
    /// A hot seat game with the number of players in <see cref="NewGameMenuChoice.PlayerCount"/>,
    /// <c>NewHotSeat()</c> of the reference.
    /// </summary>
    PlayerCount,
}

/// <summary>Answer of the new game menu to one frame of input.</summary>
/// <param name="Action">What the caller should do next.</param>
/// <param name="PlayerCount">Number of human players, only for <see cref="NewGameMenuAction.PlayerCount"/>.</param>
public readonly record struct NewGameMenuChoice(NewGameMenuAction Action, int PlayerCount = 0);

/// <summary>The part of the new game menu the right mouse button asks about.</summary>
public enum NewGameMenuHelpTarget
{
    /// <summary>Nothing was hit.</summary>
    None,

    /// <summary>The standard game button.</summary>
    StandardGame,

    /// <summary>The campaign game button.</summary>
    CampaignGame,

    /// <summary>The multi-player game button.</summary>
    MultiplayerGame,

    /// <summary>The battle only button.</summary>
    BattleOnly,

    /// <summary>The settings button.</summary>
    GameSettings,

    /// <summary>The hot seat button.</summary>
    HotSeat,

    /// <summary>The original campaign button.</summary>
    SuccessionWarsCampaign,

    /// <summary>The expansion campaign button.</summary>
    PriceOfLoyaltyCampaign,

    /// <summary>One of the player count buttons, see <see cref="NewGameMenuHelpRequest.PlayerCount"/>.</summary>
    PlayerCount,

    /// <summary>The cancel button.</summary>
    Cancel,
}

/// <summary>What the right mouse button asked about the new game menu.</summary>
/// <param name="Target">Block that was hit.</param>
/// <param name="PlayerCount">Number of players of <see cref="NewGameMenuHelpTarget.PlayerCount"/>.</param>
public readonly record struct NewGameMenuHelpRequest(NewGameMenuHelpTarget Target, int PlayerCount = 0);

/// <summary>Hot keys the new game menu reacts to, the <c>HotKeyEvent</c> set of <c>Game::NewGame()</c>.</summary>
public enum NewGameMenuHotKey
{
    /// <summary>Standard game.</summary>
    Standard,

    /// <summary>Campaign game.</summary>
    Campaign,

    /// <summary>Multi-player game.</summary>
    Multiplayer,

    /// <summary>Battle only.</summary>
    BattleOnly,

    /// <summary>Settings.</summary>
    Settings,

    /// <summary>Hot seat.</summary>
    HotSeat,

    /// <summary>Original campaign.</summary>
    OriginalCampaign,

    /// <summary>Expansion campaign.</summary>
    ExpansionCampaign,

    /// <summary>Back to the main menu.</summary>
    Cancel,

    /// <summary>Two players, <c>'2'</c> of the reference.</summary>
    TwoPlayers,

    /// <summary>Three players, <c>'3'</c> of the reference.</summary>
    ThreePlayers,

    /// <summary>Four players, <c>'4'</c> of the reference.</summary>
    FourPlayers,

    /// <summary>Five players, <c>'5'</c> of the reference.</summary>
    FivePlayers,

    /// <summary>Six players, <c>'6'</c> of the reference.</summary>
    SixPlayers,
}

/// <summary>
/// The new game menu as it is drawn: a stone window with the five game mode buttons of
/// <c>BUTTONS_NEW_GAME_MENU_GOOD</c>/<c>_EVIL</c> and the cancel button under them,
/// <c>Game::NewGame()</c> (<c>game_newgame.cpp:416-648</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference builds this in place and runs its own event loop, and it swaps the button set in
/// place as the player picks the campaign or the multi-player game. The port keeps the four sets -
/// modes, hot seat, player count, campaigns - side by side and shows the one
/// <see cref="State"/> names; the caller feeds the input to <see cref="SetLeftButton"/> and gets a
/// <see cref="NewGameMenuAction"/> back.
/// </para>
/// <para>
/// The window is sized from the five mode buttons with room for the cancel button under them, the
/// <c>StandardWindow( mainModeButtons, true, buttonStandardGame.area().height + 10 )</c> of
/// <c>game_newgame.cpp:447-449</c>. The cancel button sits on the same column, at the same distance
/// from the bottom edge the mode buttons keep from the top edge, which is the
/// <c>activeArea.y * 2 + activeArea.height - button.y - button.height</c> of line 467. The hot seat
/// and the two campaign buttons are stacked at the places of the first and the second mode button,
/// the player count buttons at the place of the first one with a gap of ten pixels.
/// </para>
/// <para>
/// The frames are those of the archive table, which keeps the original order: modes at 0-9, cancel
/// at 10-11, hot seat at 12-13, the five player counts at 14-23 and the two campaigns at 24-27,
/// released before pressed each.
/// </para>
/// </remarks>
public sealed class NewGameMenuView
{
    /// <summary>Frames of one interface set, 28 of them.</summary>
    public const int FrameCount = 28;

    /// <summary>Number of game mode buttons, the <c>i &lt; 5</c> loop of the reference.</summary>
    public const int ModeCount = 5;

    /// <summary>Number of human players the hot seat game supports at most.</summary>
    public const int MaxPlayerCount = 6;

    /// <summary>Gap between the mode buttons, the <c>spaceBetweenButtons</c> of the reference.</summary>
    public const int SpaceBetweenButtons = 10;

    /// <summary>Frame pairs are released, then pressed; frame of the released cancel button.</summary>
    public const int CancelReleasedFrame = 10;

    /// <summary>Frame of the released hot seat button.</summary>
    public const int HotSeatReleasedFrame = 12;

    /// <summary>Frame of the released two player button; the others follow two frames apart.</summary>
    public const int FirstPlayerCountReleasedFrame = 14;

    /// <summary>Frame of the released original campaign button.</summary>
    public const int SuccessionWarsReleasedFrame = 24;

    /// <summary>Frame of the released expansion campaign button.</summary>
    public const int PriceOfLoyaltyReleasedFrame = 26;

    /// <summary>
    /// The values of the mode buttons. They are only there because the button table of the port wants
    /// a <see cref="DialogButton"/> per button; the menu answers with the index, so no value is read.
    /// </summary>
    private static readonly DialogButton[] PlaceholderValues =
    [
        DialogButton.Ok, DialogButton.Cancel, DialogButton.Yes, DialogButton.No, DialogButton.Ok,
    ];

    private enum Page
    {
        Modes,
        HotSeat,
        PlayerCount,
        Campaigns,
    }

    private readonly StandardWindow _window;
    private readonly ButtonGroup _modeButtons;
    private readonly ButtonSprite _hotSeatButton;
    private readonly ButtonGroup _playerCountButtons;
    private readonly ButtonGroup? _campaignButtons;
    private readonly ButtonSprite _cancelButton;
    private readonly bool _isPriceOfLoyaltyPresent;
    private Page _page = Page.Modes;
    private int _pressedIndex = -1;
    private bool _cancelPressed;

    private NewGameMenuView(StandardWindow window, ButtonGroup modeButtons, ButtonSprite hotSeatButton,
        ButtonGroup playerCountButtons, ButtonGroup? campaignButtons, ButtonSprite cancelButton,
        bool isPriceOfLoyaltyPresent)
    {
        _window = window;
        _modeButtons = modeButtons;
        _hotSeatButton = hotSeatButton;
        _playerCountButtons = playerCountButtons;
        _campaignButtons = campaignButtons;
        _cancelButton = cancelButton;
        _isPriceOfLoyaltyPresent = isPriceOfLoyaltyPresent;
    }

    /// <summary>Whether the campaign choice is on screen, i.e. the two campaign buttons are shown.</summary>
    public bool IsChoosingCampaign => _page == Page.Campaigns;

    /// <summary>Whether the player count choice is on screen.</summary>
    public bool IsChoosingPlayerCount => _page == Page.PlayerCount;

    /// <summary>Whether the hot seat choice is on screen.</summary>
    public bool IsChoosingHotSeat => _page == Page.HotSeat;

    /// <summary>Area the window content goes into, <c>activeArea()</c> of the reference.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The five game mode buttons, already placed in the window.</summary>
    public ButtonGroup ModeButtons => _modeButtons;

    /// <summary>The cancel button, already placed in the window.</summary>
    public ButtonSprite CancelButton => _cancelButton;

    /// <summary>The hot seat button, already placed at the first mode button.</summary>
    public ButtonSprite HotSeatButton => _hotSeatButton;

    /// <summary>The five player count buttons, already placed.</summary>
    public ButtonGroup PlayerCountButtons => _playerCountButtons;

    /// <summary>The two campaign buttons, or <see langword="null"/> when the expansion is absent.</summary>
    public ButtonGroup? CampaignButtons => _campaignButtons;

    /// <summary>
    /// The buttons of the page that is shown, in the order they are laid out. The cancel button is not
    /// one of them, because it is shown on every page.
    /// </summary>
    public IReadOnlyList<ButtonBase> VisibleButtons => _page switch
    {
        Page.Modes => Collect(_modeButtons, ModeCount),
        Page.HotSeat => [_hotSeatButton],
        Page.PlayerCount => Collect(_playerCountButtons, PlayerCountButtonsCount),
        Page.Campaigns => _campaignButtons is null ? Array.Empty<ButtonBase>() : Collect(_campaignButtons, 2),
        _ => Array.Empty<ButtonBase>(),
    };

    /// <summary>
    /// Composes the menu from the frames of one interface set.
    /// </summary>
    /// <param name="frames">Frames of <see cref="FrameCount"/> at least, released before pressed each.</param>
    /// <param name="isSuccessionWarsPresent">Whether the original campaigns have their data files.</param>
    /// <param name="isPriceOfLoyaltyPresent">Whether the expansion campaigns have their data files.</param>
    /// <returns><see langword="null"/> when the frames are missing or too few.</returns>
    public static NewGameMenuView? TryCreate(IReadOnlyList<Sprite>? frames, bool isSuccessionWarsPresent,
        bool isPriceOfLoyaltyPresent)
    {
        if (frames is null || frames.Count < FrameCount)
        {
            return null;
        }

        ButtonGroup modes = ButtonGroup.CreateFromFrames(
            [.. frames.Take(ModeCount * 2)], PlaceholderValues);

        if (!isSuccessionWarsPresent)
        {
            modes[1].Disable();
        }

        int extraHeight = modes[0].Area.Height + SpaceBetweenButtons;
        StandardWindow window = StandardWindow.CreateForSymmetricButtons(modes, isSingleColumn: true,
            extraHeight: extraHeight);
        window.PlaceSymmetricButtons(modes, 0, isSingleColumn: true);

        int columnX = modes[0].OffsetX;
        int firstY = modes[0].OffsetY;
        int buttonHeight = modes[0].Area.Height;

        ButtonSprite cancel = new(frames[CancelReleasedFrame], frames[CancelReleasedFrame + 1]);
        int cancelY = (window.ActiveArea.Y * 2) + window.ActiveArea.Height - firstY - buttonHeight;
        cancel.SetPosition(columnX, cancelY);

        ButtonSprite hotSeat = new(frames[HotSeatReleasedFrame], frames[HotSeatReleasedFrame + 1]);
        hotSeat.SetPosition(columnX, firstY);
        hotSeat.Disable();

        ButtonGroup playerCounts = ButtonGroup.CreateFromFrames(
            [.. frames.Skip(FirstPlayerCountReleasedFrame).Take((MaxPlayerCount - 1) * 2)],
            PlaceholderValues);

        for (int index = 0; index < PlayerCountButtonsCount; ++index)
        {
            playerCounts[index].SetPosition(columnX, firstY + (index * (buttonHeight + SpaceBetweenButtons)));
            playerCounts[index].Disable();
        }

        ButtonGroup? campaigns = null;

        if (isPriceOfLoyaltyPresent)
        {
            campaigns = ButtonGroup.CreateFromFrames(
                [frames[SuccessionWarsReleasedFrame], frames[SuccessionWarsReleasedFrame + 1],
                    frames[PriceOfLoyaltyReleasedFrame], frames[PriceOfLoyaltyReleasedFrame + 1]],
                [DialogButton.Ok, DialogButton.Cancel]);
            campaigns[0].SetPosition(modes[0].OffsetX, modes[0].OffsetY);
            campaigns[1].SetPosition(modes[1].OffsetX, modes[1].OffsetY);
            campaigns.Disable();
        }

        return new NewGameMenuView(window, modes, hotSeat, playerCounts, campaigns, cancel,
            isPriceOfLoyaltyPresent);
    }

    /// <summary>Number of player count buttons, two to six players.</summary>
    public const int PlayerCountButtonsCount = MaxPlayerCount - 1;

    /// <summary>Moves the cursor over the menu; only the buttons of the shown set react.</summary>
    /// <param name="point">Cursor in logical pixels, <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? point)
    {
        bool visible = point is { } cursor && CurrentButtonAt(cursor) is not null;

        if (!visible && _pressedIndex >= 0)
        {
            CurrentButton(_pressedIndex).Release();
            _pressedIndex = -1;
        }
    }

    /// <summary>
    /// A left button transition over the menu.
    /// </summary>
    /// <remarks>
    /// A click has to press and release inside the same button, the <c>MouseClickLeft()</c> rule of
    /// the reference. Picking the campaign or the multi-player game only swaps the button set and
    /// answers <see cref="NewGameMenuAction.None"/>; the caller reads the new page from
    /// <see cref="IsChoosingCampaign"/> and friends, which is what the reference does by asking which
    /// group is enabled.
    /// </remarks>
    /// <param name="down">Whether the left button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>What the caller should do next.</returns>
    public NewGameMenuChoice SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            _cancelPressed = _cancelButton.Area.Contains(point);

            if (_cancelPressed)
            {
                _cancelButton.Press();
                return new NewGameMenuChoice(NewGameMenuAction.None);
            }

            _pressedIndex = IndexAt(point);

            if (_pressedIndex >= 0)
            {
                CurrentButton(_pressedIndex).Press();
            }

            return new NewGameMenuChoice(NewGameMenuAction.None);
        }

        if (_cancelPressed)
        {
            _cancelPressed = false;
            _cancelButton.Release();

            return new NewGameMenuChoice(_cancelButton.Area.Contains(point)
                ? NewGameMenuAction.Cancel
                : NewGameMenuAction.None);
        }

        int index = _pressedIndex;
        _pressedIndex = -1;

        if (index < 0)
        {
            return new NewGameMenuChoice(NewGameMenuAction.None);
        }

        ButtonBase button = CurrentButton(index);
        button.Release();

        return button.Area.Contains(point)
            ? Activate(index)
            : new NewGameMenuChoice(NewGameMenuAction.None);
    }

    /// <summary>
    /// Answers a hot key, the <c>HotKeyPressEvent()</c> calls of the reference loop.
    /// </summary>
    /// <remarks>
    /// Each key only counts on the page its button is shown on, like the reference, which tests the
    /// keys inside the branch of the button set that is currently enabled. The cancel key works
    /// everywhere.
    /// </remarks>
    /// <param name="hotKey">Key that was pressed.</param>
    /// <returns>What the caller should do next.</returns>
    public NewGameMenuChoice SetHotKey(NewGameMenuHotKey hotKey)
    {
        if (hotKey == NewGameMenuHotKey.Cancel)
        {
            return new NewGameMenuChoice(NewGameMenuAction.Cancel);
        }

        switch (_page)
        {
            case Page.Modes:
                switch (hotKey)
                {
                    case NewGameMenuHotKey.Standard:
                        return new NewGameMenuChoice(NewGameMenuAction.StandardGame);
                    case NewGameMenuHotKey.Campaign:
                        return Activate(1);
                    case NewGameMenuHotKey.Multiplayer:
                        return Activate(2);
                    case NewGameMenuHotKey.BattleOnly:
                        return new NewGameMenuChoice(NewGameMenuAction.BattleOnly);
                    case NewGameMenuHotKey.Settings:
                        return new NewGameMenuChoice(NewGameMenuAction.GameSettings);
                    default:
                        return new NewGameMenuChoice(NewGameMenuAction.None);
                }
            case Page.HotSeat:
                return hotKey == NewGameMenuHotKey.HotSeat
                    ? Activate(0)
                    : new NewGameMenuChoice(NewGameMenuAction.None);
            case Page.PlayerCount:
                return hotKey switch
                {
                    NewGameMenuHotKey.TwoPlayers => new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 2),
                    NewGameMenuHotKey.ThreePlayers => new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 3),
                    NewGameMenuHotKey.FourPlayers => new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 4),
                    NewGameMenuHotKey.FivePlayers => new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 5),
                    NewGameMenuHotKey.SixPlayers => new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 6),
                    _ => new NewGameMenuChoice(NewGameMenuAction.None),
                };
            case Page.Campaigns:
                return hotKey switch
                {
                    NewGameMenuHotKey.OriginalCampaign => new NewGameMenuChoice(
                        NewGameMenuAction.SuccessionWarsCampaign),
                    NewGameMenuHotKey.ExpansionCampaign => new NewGameMenuChoice(
                        NewGameMenuAction.PriceOfLoyaltyCampaign),
                    _ => new NewGameMenuChoice(NewGameMenuAction.None),
                };
            default:
                return new NewGameMenuChoice(NewGameMenuAction.None);
        }
    }

    /// <summary>
    /// The block the right mouse button asks about, the <c>isMouseRightButtonPressedInArea()</c>
    /// cascade of the reference loop.
    /// </summary>
    /// <param name="point">Cursor position in logical pixels.</param>
    /// <returns>The block that was hit, <see cref="NewGameMenuHelpRequest"/> with
    /// <see cref="NewGameMenuHelpTarget.None"/> when nothing was.</returns>
    public NewGameMenuHelpRequest ProcessRightClick(Point point)
    {
        if (_cancelButton.Area.Contains(point))
        {
            return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.Cancel);
        }

        switch (_page)
        {
            case Page.Modes:
                if (_modeButtons[0].Area.Contains(point))
                {
                    return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.StandardGame);
                }

                if (_modeButtons[1].IsEnabled && _modeButtons[1].Area.Contains(point))
                {
                    return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.CampaignGame);
                }

                if (_modeButtons[2].Area.Contains(point))
                {
                    return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.MultiplayerGame);
                }

                if (_modeButtons[3].Area.Contains(point))
                {
                    return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.BattleOnly);
                }

                return _modeButtons[4].Area.Contains(point)
                    ? new NewGameMenuHelpRequest(NewGameMenuHelpTarget.GameSettings)
                    : new NewGameMenuHelpRequest(NewGameMenuHelpTarget.None);
            case Page.HotSeat:
                return _hotSeatButton.Area.Contains(point)
                    ? new NewGameMenuHelpRequest(NewGameMenuHelpTarget.HotSeat)
                    : new NewGameMenuHelpRequest(NewGameMenuHelpTarget.None);
            case Page.PlayerCount:
                for (int index = 0; index < PlayerCountButtonsCount; ++index)
                {
                    if (_playerCountButtons[index].Area.Contains(point))
                    {
                        return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.PlayerCount, index + 2);
                    }
                }

                return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.None);
            case Page.Campaigns:
                if (_campaignButtons is null)
                {
                    return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.None);
                }

                if (_campaignButtons[0].Area.Contains(point))
                {
                    return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.SuccessionWarsCampaign);
                }

                return _campaignButtons[1].Area.Contains(point)
                    ? new NewGameMenuHelpRequest(NewGameMenuHelpTarget.PriceOfLoyaltyCampaign)
                    : new NewGameMenuHelpRequest(NewGameMenuHelpTarget.None);
            default:
                return new NewGameMenuHelpRequest(NewGameMenuHelpTarget.None);
        }
    }

    /// <summary>
    /// Draws the window, then the buttons of the shown set with their shadows and the cancel button,
    /// the order the reference renders them in.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        switch (_page)
        {
            case Page.Modes:
                DrawGroup(_modeButtons, target);
                break;
            case Page.HotSeat:
                _hotSeatButton.DrawShadow(target);
                _hotSeatButton.Draw(target);
                break;
            case Page.PlayerCount:
                DrawGroup(_playerCountButtons, target);
                break;
            case Page.Campaigns:
                if (_campaignButtons is not null)
                {
                    DrawGroup(_campaignButtons, target);
                }

                break;
            default:
                break;
        }

        _cancelButton.DrawShadow(target);
        _cancelButton.Draw(target);
    }

    private static ButtonBase[] Collect(ButtonGroup group, int count)
    {
        ButtonBase[] buttons = new ButtonBase[count];

        for (int index = 0; index < count; ++index)
        {
            buttons[index] = group[index];
        }

        return buttons;
    }

    private static void DrawGroup(ButtonGroup group, PixelBuffer target)
    {
        group.DrawShadows(target);
        group.Draw(target);
    }

    private ButtonBase CurrentButton(int index) => _page switch
    {
        Page.Modes => _modeButtons[index],
        Page.HotSeat => _hotSeatButton,
        Page.PlayerCount => _playerCountButtons[index],
        Page.Campaigns => _campaignButtons![index],
        _ => _cancelButton,
    };

    private ButtonBase? CurrentButtonAt(Point point)
    {
        int index = IndexAt(point);

        return index >= 0 ? CurrentButton(index) : null;
    }

    private int IndexAt(Point point)
    {
        switch (_page)
        {
            case Page.Modes:
                return IndexOf(_modeButtons, ModeCount, point);
            case Page.HotSeat:
                return _hotSeatButton.IsEnabled && _hotSeatButton.Area.Contains(point) ? 0 : -1;
            case Page.PlayerCount:
                return IndexOf(_playerCountButtons, PlayerCountButtonsCount, point);
            case Page.Campaigns:
                return _campaignButtons is null ? -1 : IndexOf(_campaignButtons, 2, point);
            default:
                return -1;
        }
    }

    private static int IndexOf(ButtonGroup group, int count, Point point)
    {
        for (int index = 0; index < count; ++index)
        {
            if (group[index].IsEnabled && group[index].Area.Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Answers a press that was released on its button, and swaps the page if need be.</summary>
    private NewGameMenuChoice Activate(int index)
    {
        switch (_page)
        {
            case Page.Modes:
                switch (index)
                {
                    case 0:
                        return new NewGameMenuChoice(NewGameMenuAction.StandardGame);
                    case 1:
                        if (!_isPriceOfLoyaltyPresent)
                        {
                            return new NewGameMenuChoice(NewGameMenuAction.SuccessionWarsCampaign);
                        }

                        ShowCampaigns();

                        return new NewGameMenuChoice(NewGameMenuAction.None);
                    case 2:
                        ShowHotSeat();

                        return new NewGameMenuChoice(NewGameMenuAction.None);
                    case 3:
                        return new NewGameMenuChoice(NewGameMenuAction.BattleOnly);
                    case 4:
                        return new NewGameMenuChoice(NewGameMenuAction.GameSettings);
                    default:
                        return new NewGameMenuChoice(NewGameMenuAction.None);
                }
            case Page.HotSeat:
                ShowPlayerCounts();

                return new NewGameMenuChoice(NewGameMenuAction.None);
            case Page.PlayerCount:
                return new NewGameMenuChoice(NewGameMenuAction.PlayerCount, index + 2);
            case Page.Campaigns:
                return new NewGameMenuChoice(index == 0
                    ? NewGameMenuAction.SuccessionWarsCampaign
                    : NewGameMenuAction.PriceOfLoyaltyCampaign);
            default:
                return new NewGameMenuChoice(NewGameMenuAction.None);
        }
    }

    private void ShowCampaigns()
    {
        _modeButtons.Disable();
        _campaignButtons?.Enable();
        _page = Page.Campaigns;
    }

    private void ShowHotSeat()
    {
        _modeButtons.Disable();
        _hotSeatButton.Enable();
        _page = Page.HotSeat;
    }

    private void ShowPlayerCounts()
    {
        _hotSeatButton.Disable();
        _playerCountButtons.Enable();
        _page = Page.PlayerCount;
    }
}
