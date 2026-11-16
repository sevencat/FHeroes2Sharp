using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.System;

/// <summary>
/// The slots of the map being played, <c>class Players</c> (<c>players.h:207-268</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference class <b>is</b> a <c>std::vector&lt;Player *&gt;</c> and additionally owns a global array indexed
/// by colour, because half the game asks "who owns red" without having a container at hand. The port keeps both:
/// the list is an instance member (the owner is <see cref="Settings.Players"/>) and the colour lookup is static,
/// exactly as in the reference.
/// </para>
/// <para>
/// The array has <see cref="MaxPlayers"/> + 1 entries, not six: a player and a colour are both indexed by
/// <c>Color::GetIndex</c> in the reference, and <c>PlayerColor.None</c> — the neutral side — has to answer null
/// instead of throwing. The port short-circuits the neutral colour explicitly and sizes the array six.
/// </para>
/// <para>
/// Two colours of a map can be friends, and the friendship is stored on the player. For an MP2 map the header has
/// no field for it, so a slot is only its own friend; the editor's <c>FileInfo::unions</c> that the reference
/// copies here is part of the editor's own format.
/// </para>
/// </remarks>
public sealed class Players
{
    /// <summary>The number of colours a map can have, <c>maxNumOfPlayers</c>.</summary>
    public const int MaxPlayers = 6;

    /// <summary>
    /// The flag that marks a player as taking part, <c>ST_INGAME</c> (<c>players.cpp:45-48</c>).
    /// </summary>
    /// <remarks>
    /// It shares the bit word with nothing else in the port: the reference's anonymous enum starts at
    /// <c>0x2000</c> because <c>BitModes</c> is also the base of <c>Settings</c>, whose flags live below it.
    /// </remarks>
    public const uint StInGame = 0x2000;

    private static readonly Player?[] PlayersByColor = new Player?[MaxPlayers];

    private static PlayerColor humanColors;

    private readonly List<Player> players = new(MaxPlayers);

    /// <summary>The players in colour order, <c>getVector()</c>.</summary>
    public IReadOnlyList<Player> All => players;

    /// <summary>The colour whose turn it is, <c>getCurrentColor()</c>; <c>None</c> before the first turn.</summary>
    public PlayerColor CurrentColor { get; private set; } = PlayerColor.None;

    /// <summary>The player whose turn it is, <c>GetCurrent()</c>; null when the colour has no slot.</summary>
    public Player? Current => Get(CurrentColor);

    /// <summary>
    /// The colour set of the map, <c>Players::Init( PlayerColorsSet )</c> (<c>players.cpp:283-297</c>).
    /// </summary>
    /// <param name="colors">The colours to create slots for.</param>
    public void Init(PlayerColor colors)
    {
        Clear();

        foreach (PlayerColor color in PlayerColors.ToList(colors))
        {
            var player = new Player(color);

            players.Add(player);
            PlayersByColor[PlayerColors.IndexOf(color)] = player;
        }
    }

    /// <summary>
    /// The slots of a map header, <c>Players::Init( const Maps::FileInfo &amp; )</c> (<c>players.cpp:299-345</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every colour starts as a computer player and is handed to a person only when the header says so: a colour
    /// that is human-only in a hot-seat game, or any colour a person may be given. The last step promotes the first
    /// such colour to a plain human slot — the reference's way of making sure a game never starts with nobody at the
    /// keyboard.
    /// </para>
    /// <para>
    /// Unlike the reference, <paramref name="fileInfo"/> here is the MP2 header: it has no alliance field, so a slot
    /// is only its own friend. The reference's <c>fi.unions</c> belongs to the editor's format.
    /// </para>
    /// </remarks>
    /// <param name="fileInfo">The map header.</param>
    public void Init(Mp2FileInfo fileInfo)
    {
        if (fileInfo.KingdomColors == PlayerColor.None)
        {
            return;
        }

        Clear();

        Player? firstHuman = null;

        foreach (PlayerColor color in PlayerColors.ToList(fileInfo.KingdomColors))
        {
            int index = PlayerColors.IndexOf(color);

            var player = new Player(color)
            {
                Race = fileInfo.PlayerRaces[index],
                Control = PlayerControl.Ai,
                Friends = color
            };

            if ((fileInfo.HumanOnlyColors & color) != 0 && Settings.Get().IsGameType(GameTypes.Multi))
            {
                player.Control = PlayerControl.Human;
            }
            else if ((fileInfo.SharedColors & color) != 0)
            {
                player.Control = player.Control | PlayerControl.Human;
            }

            firstHuman ??= player.IsControlHuman ? player : null;

            players.Add(player);
            PlayersByColor[index] = player;
        }

        if (firstHuman != null)
        {
            firstHuman.Control = PlayerControl.Human;
        }
    }

    /// <summary>
    /// Drops every slot, <c>Players::clear()</c> (<c>players.cpp:271-281</c>).
    /// </summary>
    /// <remarks>
    /// The cached human colour set is dropped with them: the next question about it re-reads the slots, which is
    /// the reference's behaviour and the only way the cache cannot go stale across two games.
    /// </remarks>
    public void Clear()
    {
        players.Clear();
        Array.Clear(PlayersByColor);
        CurrentColor = PlayerColor.None;
        humanColors = PlayerColor.None;
    }

    /// <summary>
    /// Prepares the slots for the first turn, <c>Players::SetStartGame()</c> (<c>players.cpp:487-508</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four passes, in this order, and the order is the point: everybody joins, every camera is forgotten, the
    /// races that are already taken are struck off the list of free ones, the random races are given one of what is
    /// left, and only then does a slot that asks for both controllers fall back to the computer.
    /// </para>
    /// <para>
    /// Assigning the random races last is what keeps two "random" slots from drawing the same race: each draw
    /// removes its answer from the pool. Only when the pool runs dry — more random slots than free races, or seven
    /// players worth of them — does the reference fall back to <c>Race::Rand()</c>, which may repeat.
    /// </para>
    /// </remarks>
    public void SetStartGame()
    {
        List<MapRace> availableRaces = [.. MapRaces.Basic];

        foreach (Player player in players)
        {
            player.SetPlay(true);
        }

        foreach (Player player in players)
        {
            player.Focus.Reset();
        }

        foreach (Player player in players)
        {
            availableRaces.Remove(player.Race);
        }

        foreach (Player player in players)
        {
            FixRandomRace(player, availableRaces);
        }

        foreach (Player player in players)
        {
            FixMultiControl(player);
        }

        CurrentColor = PlayerColor.None;
        humanColors = PlayerColor.None;
    }

    /// <summary>
    /// The colour set of the players matching a controller, <c>Players::GetColors()</c>
    /// (<c>players.cpp:371-382</c>).
    /// </summary>
    /// <param name="control">
    /// The controller to match; null for "any", which is the reference's <c>0xFF</c>.
    /// </param>
    /// <param name="strong">
    /// True to require exactly this controller, false to accept any slot that includes it.
    /// </param>
    /// <returns>The colours as a set.</returns>
    public PlayerColor GetColors(PlayerControl? control = null, bool strong = false)
    {
        PlayerColor result = PlayerColor.None;

        foreach (Player player in players)
        {
            if (control == null
                || (strong && player.Control == control.Value)
                || (!strong && (player.Control & control.Value) != 0))
            {
                result |= player.Color;
            }
        }

        return result;
    }

    /// <summary>
    /// The colours still in the game, <c>Players::GetActualColors()</c> (<c>players.cpp:390-400</c>).
    /// </summary>
    /// <returns>The colours as a set.</returns>
    public PlayerColor GetActualColors()
    {
        PlayerColor result = PlayerColor.None;

        foreach (Player player in players)
        {
            if (player.IsPlay)
            {
                result |= player.Color;
            }
        }

        return result;
    }

    /// <summary>
    /// Sets the colour whose turn it is, <c>Players::setCurrentColor()</c> (<c>players.cpp:537-543</c>).
    /// </summary>
    /// <param name="color">One colour, or <see cref="PlayerColor.None"/> for the neutral side.</param>
    public void SetCurrentColor(PlayerColor color) => CurrentColor = color;

    /// <summary>
    /// The slots as the reference's debug dump prints them, <c>Players::String()</c>
    /// (<c>players.cpp:545-576</c>).
    /// </summary>
    /// <returns>One line listing every slot.</returns>
    public override string ToString()
    {
        var text = new StringBuilder("Players: ");

        foreach (Player player in players)
        {
            text.Append(PlayerColors.Describe(player.Color))
                .Append('(')
                .Append(MapRaces.Name(player.Race))
                .Append(", ");

            switch (player.EffectiveControl)
            {
            case PlayerControl.Ai | PlayerControl.Human:
                text.Append("ai|human, ").Append(player.PersonalityString);
                break;
            case PlayerControl.Ai:
                text.Append("ai, ").Append(player.PersonalityString);
                break;
            case PlayerControl.Human:
                text.Append("human");
                break;
            default:
                text.Append("unknown");
                break;
            }

            text.Append("), ");
        }

        return text.ToString();
    }

    /// <summary>
    /// Points a colour at a slot, <c>Players::Set()</c> (<c>players.cpp:347-350</c>).
    /// </summary>
    /// <param name="color">The colour.</param>
    /// <param name="player">The slot, or null to clear the colour.</param>
    public static void Set(PlayerColor color, Player? player)
    {
        if (color == PlayerColor.None)
        {
            return;
        }

        PlayersByColor[PlayerColors.IndexOf(color)] = player;
    }

    /// <summary>
    /// The slot of a colour, <c>Players::Get()</c> (<c>players.cpp:352-355</c>).
    /// </summary>
    /// <param name="color">The colour.</param>
    /// <returns>The slot, or null when the colour has none.</returns>
    public static Player? Get(PlayerColor color) =>
        color == PlayerColor.None ? null : PlayersByColor[PlayerColors.IndexOf(color)];

    /// <summary>The controller of a colour, <c>Players::GetPlayerControl()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The control flags, <see cref="PlayerControl.None"/> for a colour with no slot.</returns>
    public static PlayerControl GetPlayerControl(PlayerColor color) => Get(color)?.Control ?? PlayerControl.None;

    /// <summary>The race of a colour, <c>Players::GetPlayerRace()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The race, <see cref="MapRace.None"/> for a colour with no slot.</returns>
    public static MapRace GetPlayerRace(PlayerColor color) => Get(color)?.Race ?? MapRace.None;

    /// <summary>The friends of a colour, <c>Players::GetPlayerFriends()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The colour set, empty for a colour with no slot.</returns>
    public static PlayerColor GetPlayerFriends(PlayerColor color) => Get(color)?.Friends ?? PlayerColor.None;

    /// <summary>Whether a colour is still in the game, <c>Players::GetPlayerInGame()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>True when the slot exists and takes part.</returns>
    public static bool GetPlayerInGame(PlayerColor color) => Get(color)?.IsPlay ?? false;

    /// <summary>
    /// The colours still in the game that a colour may attack, <c>Players::getInPlayOpponents()</c>
    /// (<c>players.cpp:449-470</c>).
    /// </summary>
    /// <param name="color">The colour asking.</param>
    /// <returns>The colours of the players in the game that are not the asker's friends.</returns>
    public static IReadOnlyList<PlayerColor> GetInPlayOpponents(PlayerColor color)
    {
        var opponents = new List<PlayerColor>();

        Player? asker = Get(color);

        if (asker == null)
        {
            return opponents;
        }

        foreach (Player player in Settings.Get().Players.All)
        {
            if (player.IsPlay && (asker.Friends & player.Color) == 0)
            {
                opponents.Add(player.Color);
            }
        }

        return opponents;
    }

    /// <summary>Whether two colours are friends, <c>Players::isFriends()</c>.</summary>
    /// <param name="playerColor">The colour whose friends are asked.</param>
    /// <param name="colors">The colours to test.</param>
    /// <returns>True when any of them is a friend of the asker.</returns>
    public static bool IsFriends(PlayerColor playerColor, PlayerColor colors) =>
        ((Get(playerColor)?.Friends ?? PlayerColor.None) & colors) != 0;

    /// <summary>The race of a colour, <c>Players::SetPlayerRace()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="race">The new race.</param>
    public static void SetPlayerRace(PlayerColor color, MapRace race)
    {
        Player? player = Get(color);

        if (player != null)
        {
            player.Race = race;
        }
    }

    /// <summary>The controller of a colour, <c>Players::SetPlayerControl()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="control">The new control flags.</param>
    public static void SetPlayerControl(PlayerColor color, PlayerControl control)
    {
        Player? player = Get(color);

        if (player != null)
        {
            player.Control = control;
        }
    }

    /// <summary>Whether a colour takes part, <c>Players::SetPlayerInGame()</c>.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="isPlay">True to put the player in the game.</param>
    public static void SetPlayerInGame(PlayerColor color, bool isPlay) => Get(color)?.SetPlay(isPlay);

    /// <summary>
    /// The colours played by people, <c>Players::HumanColors()</c> (<c>players.cpp:510-518</c>).
    /// </summary>
    /// <remarks>
    /// The reference caches the answer in a file-level variable and clears it in <c>clear()</c> and
    /// <c>SetStartGame()</c>; the port does the same, so that a single call site in a per-frame path does not walk
    /// the slots every frame.
    /// </remarks>
    /// <returns>The colours whose slot is driven by a person.</returns>
    public static PlayerColor HumanColors()
    {
        if (humanColors == PlayerColor.None)
        {
            humanColors = Settings.Get().Players.GetColors(PlayerControl.Human, true);
        }

        return humanColors;
    }

    /// <summary>
    /// The friends of the colour whose turn it is, <c>Players::FriendColors()</c> (<c>players.cpp:520-527</c>).
    /// </summary>
    /// <returns>The colour set, empty when there is no current player.</returns>
    public static PlayerColor FriendColors() =>
        Settings.Get().Players.Current?.Friends ?? PlayerColor.None;

    /// <summary>
    /// Collapses a slot that asks for both controllers into a computer one, <c>fixMultiControl()</c>
    /// (<c>players.cpp:57-70</c>).
    /// </summary>
    /// <remarks>
    /// The "human and AI" state only exists before the game starts: it means "a person may be given this slot, the
    /// computer otherwise". Once the game has started the slot has to answer one way or the other, and the reference
    /// chooses the computer.
    /// </remarks>
    /// <param name="player">The slot to fix.</param>
    private static void FixMultiControl(Player player)
    {
        if (player.Control != (PlayerControl.Human | PlayerControl.Ai))
        {
            return;
        }

        player.Control = PlayerControl.Ai;
    }

    /// <summary>
    /// Gives a slot that asked for a random race one of the races nobody has yet, <c>fixRandomRace()</c>
    /// (<c>players.cpp:82-104</c>).
    /// </summary>
    /// <param name="player">The slot to fix.</param>
    /// <param name="availableRaces">The races still free; the chosen one is removed.</param>
    private static void FixRandomRace(Player player, List<MapRace> availableRaces)
    {
        if (player.Race != MapRace.Random)
        {
            return;
        }

        if (availableRaces.Count == 0)
        {
            player.Race = MapRaces.Random();

            return;
        }

        int index = Rand.Get(0, availableRaces.Count - 1);

        player.Race = availableRaces[index];
        availableRaces.RemoveAt(index);
    }
}
