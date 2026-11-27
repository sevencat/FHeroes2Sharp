using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The <c>NGEXTRA</c> artwork the players panel draws, <c>gui/player_info.cpp</c>.
/// </summary>
/// <remarks>
/// 参考处处直接 <c>Assets::getImage( ICN::NGEXTRA, n )</c>；端口只暴露「取某一帧」。
/// </remarks>
public interface IPlayersInfoArtwork
{
    /// <summary>
    /// A frame of <c>NGEXTRA</c>.
    /// </summary>
    /// <param name="frame">The frame index.</param>
    /// <returns>The frame.</returns>
    Sprite GetIcon(uint frame);
}

/// <summary>
/// What processing one event produced, <c>Interface::PlayersInfo::QueueEventProcessing()</c>'s answer in the
/// reference is a bare <c>bool</c>; the port has to carry the popup and the name prompt out as well.
/// </summary>
public enum PlayersInfoAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The panel changed and must be redrawn.</summary>
    Redraw,

    /// <summary>The player right-clicked something; show <see cref="PlayersInfoResult.Title"/> and
    /// <see cref="PlayersInfoResult.Body"/> as a popup.</summary>
    ShowHelp,

    /// <summary>The player clicked a name; run <see cref="PlayersInfoResult.NameDialog"/> and feed the answer to
    /// <see cref="PlayersInfo.ApplyName"/>.</summary>
    AskPlayerName,
}

/// <summary>
/// One slot of the panel, <c>Interface::PlayerInfo</c> (<c>player_info.h:36-44</c>).
/// </summary>
/// <param name="Player">The player shown in the slot.</param>
/// <param name="PlayerTypeRoi">Where the human/AI/computer-or-human icon goes.</param>
/// <param name="ClassRoi">Where the class (race) icon goes.</param>
/// <param name="NameRoi">Where the name plate is, directly under <paramref name="PlayerTypeRoi"/>.</param>
/// <param name="HandicapRoi">Where the handicap icon goes.</param>
public readonly record struct PlayerInfoEntry(Player Player, Rect PlayerTypeRoi, Rect ClassRoi, Rect NameRoi,
    Rect HandicapRoi);

/// <summary>
/// The answer of <see cref="PlayersInfo.QueueEventProcessing"/> and
/// <see cref="PlayersInfo.ReadOnlyEventProcessing"/>.
/// </summary>
/// <param name="Action">What the caller has to do.</param>
/// <param name="Title">Popup title, for <see cref="PlayersInfoAction.ShowHelp"/>.</param>
/// <param name="Body">Popup body, for <see cref="PlayersInfoAction.ShowHelp"/>.</param>
/// <param name="Player">The player whose name is being edited, for <see cref="PlayersInfoAction.AskPlayerName"/>.</param>
/// <param name="NameDialog">The dialog to run, for <see cref="PlayersInfoAction.AskPlayerName"/>.</param>
public readonly record struct PlayersInfoResult(PlayersInfoAction Action, string Title = "", string Body = "",
    Player? Player = null, InputStringDialog? NameDialog = null)
{
    /// <summary>Whether anything has to be done at all.</summary>
    public bool Handled => Action != PlayersInfoAction.None;

    /// <summary>The no-op answer.</summary>
    public static PlayersInfoResult None => default;

    /// <summary>The "redraw the panel" answer.</summary>
    public static PlayersInfoResult Redraw { get; } = new(PlayersInfoAction.Redraw);
}

/// <summary>
/// The row of player slots of the scenario setup and of the in-game "opponents" view,
/// <c>Interface::PlayersInfo</c> (<c>player_info.cpp:47-462</c>, <c>player_info.h:36-70</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考让 <c>PlayersInfo</c> 从 <c>std::vector&lt;PlayerInfo&gt;</c> 派生，端口同样从
/// <see cref="List{T}"/> 派生，槽位顺序就是玩家顺序。
/// </para>
/// <para>
/// 参考从 <c>Settings::Get()</c> 里拿 <c>getCurrentMapInfo()</c> 与 <c>GetPlayers()</c>；端口把这两样当参数传进来，
/// 只有 <see cref="Settings.Get"/> 的玩家列表例外——那是参考里唯一会被写的地方（<c>SetPlayerControl</c>），
/// 端口也只在 <see cref="SwapPlayers"/> 与单人切换里动它。
/// </para>
/// </remarks>
public sealed class PlayersInfo : List<PlayerInfoEntry>
{
    private const byte SelectionBorderColor = 214;

    /// <summary>
    /// The slot the player picked first in a swap, <c>currentSelectedPlayer</c>.
    /// </summary>
    public Player? CurrentSelectedPlayer { get; set; }

    /// <summary>
    /// Lays the slots out for the given players, <c>UpdateInfo()</c> (<c>player_info.cpp:47-75</c>).
    /// </summary>
    /// <param name="players">The players, in play order.</param>
    /// <param name="playerTypeOffset">Origin of the icon row.</param>
    /// <param name="classOffset">Origin of the class row.</param>
    /// <param name="artwork">The artwork, for the three icon sizes the layout needs.</param>
    public void UpdateInfo(Players players, Point playerTypeOffset, Point classOffset,
        IPlayersInfoArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(artwork);

        Clear();

        Sprite playerTypeImage = artwork.GetIcon(3);
        Sprite classImage = artwork.GetIcon(51);
        Sprite handicapImage = artwork.GetIcon(0);

        int playerCount = players.All.Count;

        for (int i = 0; i < playerCount; ++i)
        {
            Player player = players.All[i];

            int playerTypeX = playerTypeOffset.X
                + Game.GameScenarioInfo.GetStep4Player(i, playerTypeImage.Width, playerCount);
            Rect playerTypeRoi = new(playerTypeX, playerTypeOffset.Y, playerTypeImage.Width,
                playerTypeImage.Height);

            int classX = classOffset.X + Game.GameScenarioInfo.GetStep4Player(i, classImage.Width, playerCount);

            Add(new PlayerInfoEntry(
                player,
                playerTypeRoi,
                new Rect(classX, classOffset.Y, classImage.Width, classImage.Height),
                new Rect(playerTypeRoi.X, playerTypeRoi.Y + playerTypeRoi.Height, playerTypeRoi.Width, 10),
                new Rect(classX, classOffset.Y + 69, handicapImage.Width, handicapImage.Height)));
        }
    }

    /// <summary>
    /// Forgets the first pick of a swap, <c>resetSelection()</c> (<c>player_info.h:60-63</c>).
    /// </summary>
    public void ResetSelection() => CurrentSelectedPlayer = null;

    /// <summary>
    /// Swaps two players' side and identity, <c>SwapPlayers()</c> (<c>player_info.cpp:77-129</c>).
    /// </summary>
    /// <param name="player1">The first player.</param>
    /// <param name="player2">The second player.</param>
    /// <param name="mapInfo">The map, for its human/computer overlap and its random races.</param>
    /// <param name="players">All players, for the human colors.</param>
    /// <returns>True when they were swapped.</returns>
    /// <remarks>
    /// 只有当两边同类（都是 AI 或都是人类），或者两边都允许人机互换时才能换；后者顺带把「人类」标记挪过去。
    /// 换过之后种族、手气与名字一起换，名字只在对面用的是默认名时换成自己的默认名——这样「Red 玩家」这类默认名
    /// 始终跟着颜色走。
    /// </remarks>
    public bool SwapPlayers(Player player1, Player player2, Mp2FileInfo mapInfo, Players players)
    {
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);
        ArgumentNullException.ThrowIfNull(mapInfo);
        ArgumentNullException.ThrowIfNull(players);

        PlayerColor player1Color = player1.Color;
        PlayerColor player2Color = player2.Color;

        bool swap = false;

        if (player1.IsControlAi == player2.IsControlAi)
        {
            swap = true;
        }
        else if ((mapInfo.SharedColors & player1Color) != PlayerColor.None
            && (mapInfo.SharedColors & player2Color) != PlayerColor.None)
        {
            PlayerColor humans = players.GetColors(PlayerControl.Human, true);

            if ((humans & player1Color) != PlayerColor.None)
            {
                Players.SetPlayerControl(player1Color, PlayerControl.Ai | PlayerControl.Human);
                Players.SetPlayerControl(player2Color, PlayerControl.Human);
            }
            else
            {
                Players.SetPlayerControl(player2Color, PlayerControl.Ai | PlayerControl.Human);
                Players.SetPlayerControl(player1Color, PlayerControl.Human);
            }

            swap = true;
        }

        if (!swap)
        {
            return false;
        }

        MapRace player1Race = player1.Race;
        MapRace player2Race = player2.Race;

        Player.HandicapStatus player1Handicap = player1.Handicap;
        Player.HandicapStatus player2Handicap = player2.Handicap;

        player2.SetHandicapStatus(player1Handicap);
        player1.SetHandicapStatus(player2Handicap);

        if (player1Race != player2Race && mapInfo.AllowChangeRace(player1Color)
            && mapInfo.AllowChangeRace(player2Color))
        {
            player1.Race = player2Race;
            player2.Race = player1Race;
        }

        // Both names are read before either is written, exactly like the reference.
        string player1NewName = player2.IsDefaultName ? player1.DefaultName : player2.Name;
        string player2NewName = player1.IsDefaultName ? player2.DefaultName : player1.Name;

        player1.SetName(player1NewName);
        player2.SetName(player2NewName);

        return true;
    }

    /// <summary>
    /// The player whose type icon is under the point, <c>GetFromOpponentClick()</c>
    /// (<c>player_info.cpp:131-142</c>).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The player, or null.</returns>
    public Player? GetFromOpponentClick(Point point) => Find(point, static entry => entry.PlayerTypeRoi);

    /// <summary>
    /// The player whose name is under the point, <c>GetFromOpponentNameClick()</c>
    /// (<c>player_info.cpp:144-155</c>).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The player, or null.</returns>
    public Player? GetFromOpponentNameClick(Point point) => Find(point, static entry => entry.NameRoi);

    /// <summary>
    /// The player whose class icon is under the point, <c>GetFromClassClick()</c>
    /// (<c>player_info.cpp:157-168</c>).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The player, or null.</returns>
    public Player? GetFromClassClick(Point point) => Find(point, static entry => entry.ClassRoi);

    /// <summary>
    /// The player whose handicap icon is under the point, <c>getPlayerFromHandicapRoi()</c>
    /// (<c>player_info.cpp:170-181</c>).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The player, or null.</returns>
    public Player? GetPlayerFromHandicapRoi(Point point) => Find(point, static entry => entry.HandicapRoi);

    /// <summary>
    /// Draws every slot, <c>RedrawInfo()</c> (<c>player_info.cpp:183-276</c>).
    /// </summary>
    /// <param name="displayInGameInfo">
    /// Whether this is the in-game view: there the class icon is "active" only for players still in the game,
    /// on the setup screen for every player whose race may still change.
    /// </param>
    /// <param name="output">The target, the reference's <c>Display</c>.</param>
    /// <param name="mapInfo">The map, for its computer-only colors.</param>
    /// <param name="players">All players, for the human colors.</param>
    /// <param name="artwork">The artwork.</param>
    /// <remarks>
    /// 阴影画在图标的左边，所以整排必须从右往左画，否则左边那格的阴影会盖到右邻的图标上。
    /// </remarks>
    public void RedrawInfo(bool displayInGameInfo, PixelBuffer output, Mp2FileInfo mapInfo, Players players,
        IPlayersInfoArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(mapInfo);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(artwork);

        int playerCount = players.All.Count;
        PlayerColor humanColors = players.GetColors(PlayerControl.Human, true);

        for (int i = Count - 1; i >= 0; --i)
        {
            PlayerInfoEntry info = this[i];
            PlayerColor color = info.Player.Color;

            uint playerTypeIcnIndex;
            if ((humanColors & color) != PlayerColor.None)
            {
                // Current human.
                playerTypeIcnIndex = (uint)(9 + PlayerColors.GetIndex(color));
            }
            else if ((mapInfo.ComputerOnlyColors & color) != PlayerColor.None)
            {
                // Computer only.
                playerTypeIcnIndex = (uint)(15 + PlayerColors.GetIndex(color));
            }
            else
            {
                // Computer or human.
                playerTypeIcnIndex = (uint)(3 + PlayerColors.GetIndex(color));
            }

            // Wide sprite offset.
            playerTypeIcnIndex += 24;

            Sprite playerIconShadow = artwork.GetIcon(60);
            Sprite playerIcon = artwork.GetIcon(playerTypeIcnIndex);

            ImageOps.BlitSprite(output, new Point(info.PlayerTypeRoi.X - 5, info.PlayerTypeRoi.Y + 3),
                playerIconShadow);
            ImageOps.BlitSprite(output, new Point(info.PlayerTypeRoi.X, info.PlayerTypeRoi.Y), playerIcon);

            if (CurrentSelectedPlayer is not null && ReferenceEquals(info.Player, CurrentSelectedPlayer))
            {
                // 只留一圈框：先 Reset 成全透明再画，否则整张不透明的黑会盖住玩家图标。
                Sprite selection = new(playerIcon.Width, playerIcon.Height);
                selection.Reset();
                ImageOps.DrawBorder(selection, SelectionBorderColor);
                ImageOps.BlitSprite(output, new Point(info.PlayerTypeRoi.X, info.PlayerTypeRoi.Y), selection);
            }

            // The name, shrunk to the icon when it does not fit.
            Text name = new(info.Player.Name, FontType.SmallWhite());
            int maximumTextWidth = playerIcon.Width - 4;
            name.FitToOneRow(maximumTextWidth);
            name.Draw(
                info.PlayerTypeRoi.X + 2 + (maximumTextWidth - name.Width()) / 2,
                info.PlayerTypeRoi.Y + info.PlayerTypeRoi.Height + 1,
                output);

            // The class.
            bool isActivePlayer = displayInGameInfo
                ? info.Player.IsPlay
                : mapInfo.AllowChangeRace(color);

            Sprite classIcon = artwork.GetIcon(MapRaces.IcnIndex(info.Player.Race, isActivePlayer));
            Sprite classIconShadow = artwork.GetIcon(61);

            ImageOps.BlitSprite(output, new Point(info.ClassRoi.X - 5, info.ClassRoi.Y + 3), classIconShadow);
            ImageOps.BlitSprite(output, new Point(info.ClassRoi.X, info.ClassRoi.Y), classIcon);

            string raceName = playerCount > 4
                ? MapRaces.DoubleLinedName(info.Player.Race)
                : MapRaces.Name(info.Player.Race);

            int maxClassNameTextWidth = classIcon.Width + 60;
            Text classText = new(raceName, FontType.SmallWhite());
            classText.Draw(info.ClassRoi.X - 31, info.ClassRoi.Y + info.ClassRoi.Height + 4,
                maxClassNameTextWidth, output);

            // The handicap.
            uint handicapIcnIndex;
            if ((humanColors & color) != PlayerColor.None)
            {
                handicapIcnIndex = info.Player.Handicap switch
                {
                    Player.HandicapStatus.None => 0u,
                    Player.HandicapStatus.Mild => 1u,
                    Player.HandicapStatus.Severe => 2u,
                    _ => throw new ArgumentOutOfRangeException(nameof(info), info.Player.Handicap,
                        "A new handicap status needs a frame here."),
                };
            }
            else
            {
                // The computer has no handicap; the reference asserts that and pins this frame.
                handicapIcnIndex = 78;
            }

            Sprite handicapIcon = artwork.GetIcon(handicapIcnIndex);
            Sprite handicapIconShadow = artwork.GetIcon(59);

            ImageOps.BlitSprite(output, new Point(info.HandicapRoi.X - 5, info.HandicapRoi.Y + 3),
                handicapIconShadow);
            ImageOps.BlitSprite(output, new Point(info.HandicapRoi.X, info.HandicapRoi.Y), handicapIcon);
        }
    }

    /// <summary>
    /// Handles one event, <c>QueueEventProcessing()</c> (<c>player_info.cpp:278-428</c>).
    /// </summary>
    /// <param name="events">The event state of this frame.</param>
    /// <param name="mapInfo">The map.</param>
    /// <param name="players">All players.</param>
    /// <returns>What the caller has to do.</returns>
    /// <remarks>
    /// <para>
    /// 参考不查左键：能走到这一段就说明调用方是在按钮事件里调它的，纯移动不会进这里。端口保持同样的前提。
    /// </para>
    /// <para>
    /// 参考在「单人局里人类颜色不止一个」处 <c>assert( 0 )</c>，端口抛
    /// <see cref="InvalidOperationException"/>——那是数据坏了，不是玩家能做出来的状态。
    /// </para>
    /// </remarks>
    public PlayersInfoResult QueueEventProcessing(LocalEvent events, Mp2FileInfo mapInfo, Players players)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(mapInfo);
        ArgumentNullException.ThrowIfNull(players);

        Point point = events.CursorPosition;

        if (events.IsMouseRightButtonPressed)
        {
            if (GetFromOpponentClick(point) is not null)
            {
                return new PlayersInfoResult(PlayersInfoAction.ShowHelp, "Opponents",
                    "This lets you change player starting positions and colors. A particular color will always start in a particular location. Some positions may only be played by a computer player or only by a human player.");
            }

            if (GetFromClassClick(point) is not null)
            {
                return new PlayersInfoResult(PlayersInfoAction.ShowHelp, "Class",
                    "This lets you change the class of a player. Classes are not always changeable. Depending on the scenario, a player may receive additional towns and/or heroes not of their primary alignment.");
            }

            if (GetPlayerFromHandicapRoi(point) is not null)
            {
                return new PlayersInfoResult(PlayersInfoAction.ShowHelp, "Handicap",
                    "This lets you change the handicap of a particular player. Only human players may have a handicap. Handicapped players start with fewer resources and earn 15 or 30% fewer resources per turn for mild and severe handicaps, respectively.");
            }

            return PlayersInfoResult.None;
        }

        if (events.IsMouseWheelUp)
        {
            Player? player = GetFromClassClick(point);

            if (player is not null && mapInfo.AllowChangeRace(player.Color))
            {
                player.Race = MapRaces.Previous(player.Race);

                return PlayersInfoResult.Redraw;
            }

            return PlayersInfoResult.None;
        }

        if (events.IsMouseWheelDown)
        {
            Player? player = GetFromClassClick(point);

            if (player is not null && mapInfo.AllowChangeRace(player.Color))
            {
                player.Race = MapRaces.Next(player.Race);

                return PlayersInfoResult.Redraw;
            }

            return PlayersInfoResult.None;
        }

        Player? clickedPlayer = GetFromOpponentClick(point);
        if (clickedPlayer is not null)
        {
            if (Settings.Get().IsGameType(GameTypes.Multi))
            {
                if (CurrentSelectedPlayer is null)
                {
                    CurrentSelectedPlayer = clickedPlayer;
                }
                else if (ReferenceEquals(CurrentSelectedPlayer, clickedPlayer))
                {
                    CurrentSelectedPlayer = null;
                }
                else if (SwapPlayers(clickedPlayer, CurrentSelectedPlayer, mapInfo, players))
                {
                    CurrentSelectedPlayer = null;
                }
            }
            else
            {
                // Single-player game: hand the human slot over to the clicked color.
                PlayerColor clickedColor = clickedPlayer.Color;

                if ((mapInfo.HumanPlayers & clickedColor) != PlayerColor.None)
                {
                    PlayerColor humanControlledColors = players.GetColors(PlayerControl.Human, true);

                    if (PlayerColors.Count(humanControlledColors) != 1)
                    {
                        // How could there be more than one active human player in a single-player game?
                        throw new InvalidOperationException(
                            $"A single-player game has {PlayerColors.Count(humanControlledColors)} human colors.");
                    }

                    PlayerColor currentColor = humanControlledColors;

                    if (clickedColor != currentColor)
                    {
                        Player currentPlayer = Players.Get(currentColor)
                            ?? throw new InvalidOperationException($"There is no player for {currentColor}.");
                        Player nextPlayer = Players.Get(clickedColor)
                            ?? throw new InvalidOperationException($"There is no player for {clickedColor}.");

                        Player.HandicapStatus currentHandicapStatus = currentPlayer.Handicap;

                        Players.SetPlayerControl(currentColor, PlayerControl.Ai | PlayerControl.Human);
                        Players.SetPlayerControl(clickedColor, PlayerControl.Human);

                        nextPlayer.SetHandicapStatus(currentHandicapStatus);
                        currentPlayer.SetHandicapStatus(Player.HandicapStatus.None);
                    }
                }
            }

            return PlayersInfoResult.Redraw;
        }

        clickedPlayer = GetFromOpponentNameClick(point);
        if (clickedPlayer is not null)
        {
            string title = $"{PlayerColors.Describe(clickedPlayer.Color)} player";

            return new PlayersInfoResult(PlayersInfoAction.AskPlayerName, string.Empty, title, clickedPlayer,
                InputStringDialog.Create(string.Empty, title, 0, false, clickedPlayer.Name));
        }

        clickedPlayer = GetFromClassClick(point);
        if (clickedPlayer is not null && mapInfo.AllowChangeRace(clickedPlayer.Color))
        {
            clickedPlayer.Race = MapRaces.Next(clickedPlayer.Race);

            return PlayersInfoResult.Redraw;
        }

        clickedPlayer = GetPlayerFromHandicapRoi(point);
        if (clickedPlayer is not null)
        {
            if (!clickedPlayer.Control.IsAi())
            {
                clickedPlayer.SetHandicapStatus(clickedPlayer.Handicap switch
                {
                    Player.HandicapStatus.None => Player.HandicapStatus.Mild,
                    Player.HandicapStatus.Mild => Player.HandicapStatus.Severe,
                    Player.HandicapStatus.Severe => Player.HandicapStatus.None,
                    _ => throw new ArgumentOutOfRangeException(nameof(clickedPlayer), clickedPlayer.Handicap,
                        "A new handicap status needs a step here."),
                });
            }

            return PlayersInfoResult.Redraw;
        }

        return PlayersInfoResult.None;
    }

    /// <summary>
    /// Handles one right click when the panel is read only, <c>readOnlyEventProcessing()</c>
    /// (<c>player_info.cpp:430-462</c>).
    /// </summary>
    /// <param name="events">The event state of this frame.</param>
    /// <returns>What the caller has to do.</returns>
    public PlayersInfoResult ReadOnlyEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // Read only mode works only for right click events.
        if (!events.IsMouseRightButtonPressed)
        {
            return PlayersInfoResult.None;
        }

        Player? player = GetPlayerFromHandicapRoi(events.CursorPosition);
        if (player is null)
        {
            return PlayersInfoResult.None;
        }

        return player.Handicap switch
        {
            Player.HandicapStatus.None => new PlayersInfoResult(PlayersInfoAction.ShowHelp, "No Handicap",
                "No special restrictions on starting resources and resource income per turn."),
            Player.HandicapStatus.Mild => new PlayersInfoResult(PlayersInfoAction.ShowHelp, "Mild Handicap",
                "Players with mild handicap start with fewer resources and earn 15% fewer resources per turn."),
            Player.HandicapStatus.Severe => new PlayersInfoResult(PlayersInfoAction.ShowHelp, "Severe Handicap",
                "Players with severe handicap start with fewer resources and earn 30% fewer resources per turn."),
            _ => throw new ArgumentOutOfRangeException(nameof(events), player.Handicap,
                "A new handicap status needs a text here."),
        };
    }

    /// <summary>
    /// Stores the name the player typed, <c>player-&gt;SetName( std::move( res ) )</c>
    /// (<c>player_info.cpp:391-393</c>).
    /// </summary>
    /// <param name="player">The player to rename.</param>
    /// <param name="typedName">What the dialog produced, null when it was cancelled.</param>
    /// <remarks>
    /// 参考只在输入框返回真、且结果非空时才改名；取消与清空都保持原名。
    /// </remarks>
    public static void ApplyName(Player player, string? typedName)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (typedName is not null && typedName.Length > 0)
        {
            player.SetName(typedName);
        }
    }

    private Player? Find(Point point, Func<PlayerInfoEntry, Rect> roi)
    {
        foreach (PlayerInfoEntry entry in this)
        {
            if (roi(entry).Contains(point))
            {
                return entry.Player;
            }
        }

        return null;
    }
}
