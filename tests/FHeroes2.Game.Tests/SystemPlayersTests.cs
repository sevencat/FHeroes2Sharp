using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>system/players.cpp</c>: the two ways the slots are built, the four passes of
/// <c>SetStartGame</c>, the controller filters and the colour indexed table.
/// </summary>
[Collection(SettingsSingletonCollection.Name)]
public sealed class PlayersTests
{
    /// <summary>A fresh <see cref="Settings"/>, so the slot list and the static table agree.</summary>
    private static Settings FreshSettings()
    {
        Settings.Reset();

        return Settings.Get();
    }

    private static Mp2FileInfo Map(
        PlayerColor kingdoms,
        PlayerColor humans,
        PlayerColor computers,
        params (PlayerColor Color, MapRace Race)[] races)
    {
        List<MapRace> all = [MapRace.None, MapRace.None, MapRace.None, MapRace.None, MapRace.None, MapRace.None];

        foreach ((PlayerColor color, MapRace race) in races)
        {
            all[PlayerColors.IndexOf(color)] = race;
        }

        return new Mp2FileInfo
        {
            FilePath = "test.mp2",
            Name = "Test",
            Description = string.Empty,
            Width = 72,
            Height = 72,
            Difficulty = GameDifficulty.Normal,
            KingdomColors = kingdoms,
            HumanPlayers = humans,
            ComputerPlayers = computers,
            PlayerRaces = all,
        };
    }

    [Fact]
    public void AColorSetCreatesTheSlotsInMapOrder()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Red | PlayerColor.Blue);

        Assert.Equal(2, players.All.Count);
        Assert.Equal(PlayerColor.Blue, players.All[0].Color);
        Assert.Equal(PlayerColor.Red, players.All[1].Color);

        Assert.Same(players.All[0], Players.Get(PlayerColor.Blue));
        Assert.Null(Players.Get(PlayerColor.Green));
        Assert.Null(Players.Get(PlayerColor.None));

        Assert.Equal(PlayerColor.None, players.CurrentColor);
        Assert.Null(players.Current);

        foreach (Player player in players.All)
        {
            Assert.Equal(player.Color, player.Friends);
            Assert.False(player.IsPlay);
            Assert.Equal(PlayerControl.None, player.Control);
        }
    }

    [Fact]
    public void AHeaderMakesTheHumanOnlySlotsHuman()
    {
        Settings settings = FreshSettings();
        settings.SetGameType(GameTypes.HotSeat);

        Players players = settings.Players;
        players.Init(Map(
            PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
            humans: PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
            computers: PlayerColor.Red,
            (PlayerColor.Blue, MapRace.Knight),
            (PlayerColor.Green, MapRace.Wizard),
            (PlayerColor.Red, MapRace.Barbarian)));

        Assert.Equal(PlayerControl.Human, Players.GetPlayerControl(PlayerColor.Blue));
        Assert.Equal(PlayerControl.Human, Players.GetPlayerControl(PlayerColor.Green));

        // Red may be given to either side before the game starts; it is the computer that keeps it once it does.
        Assert.Equal(PlayerControl.Human | PlayerControl.Ai, Players.GetPlayerControl(PlayerColor.Red));

        Assert.Equal(MapRace.Knight, Players.GetPlayerRace(PlayerColor.Blue));
        Assert.Equal(MapRace.Wizard, Players.GetPlayerRace(PlayerColor.Green));
        Assert.Equal(MapRace.Barbarian, Players.GetPlayerRace(PlayerColor.Red));
    }

    [Fact]
    public void ASharedSlotIsPromotedOnlyOnce()
    {
        Settings settings = FreshSettings();
        settings.SetGameType(GameTypes.HotSeat);

        Players players = settings.Players;
        players.Init(Map(
            PlayerColor.Blue | PlayerColor.Green,
            humans: PlayerColor.Blue | PlayerColor.Green,
            computers: PlayerColor.Blue | PlayerColor.Green));

        Assert.Equal(PlayerControl.Human, Players.GetPlayerControl(PlayerColor.Blue));
        Assert.Equal(PlayerControl.Human | PlayerControl.Ai, Players.GetPlayerControl(PlayerColor.Green));
    }

    [Fact]
    public void AHumanOnlySlotIsOnlyHonouredInAHotSeatGame()
    {
        Settings settings = FreshSettings();
        settings.SetGameType(GameTypes.Standard);

        Players players = settings.Players;
        players.Init(Map(
            PlayerColor.Blue | PlayerColor.Red,
            humans: PlayerColor.Blue,
            computers: PlayerColor.Red));

        // The reference gates the human-only colours on TYPE_MULTI, so a single-player game ignores them.
        Assert.Equal(PlayerControl.Ai, Players.GetPlayerControl(PlayerColor.Blue));
        Assert.Equal(PlayerControl.Ai, Players.GetPlayerControl(PlayerColor.Red));
    }

    [Fact]
    public void AHeaderWithoutKingdomsLeavesTheSlotsAlone()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue);
        players.Init(Map(PlayerColor.None, PlayerColor.None, PlayerColor.None));

        Assert.Single(players.All);
        Assert.Equal(PlayerColor.Blue, players.All[0].Color);
    }

    [Fact]
    public void TheColorFilterHonoursStrongAndWeakMatching()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red);
        Players.SetPlayerControl(PlayerColor.Blue, PlayerControl.Human);
        Players.SetPlayerControl(PlayerColor.Green, PlayerControl.Human | PlayerControl.Ai);
        Players.SetPlayerControl(PlayerColor.Red, PlayerControl.Ai);

        Assert.Equal(
            PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
            players.GetColors());

        Assert.Equal(PlayerColor.Blue | PlayerColor.Green, players.GetColors(PlayerControl.Human));
        Assert.Equal(PlayerColor.Blue, players.GetColors(PlayerControl.Human, strong: true));

        Assert.Equal(PlayerColor.Green | PlayerColor.Red, players.GetColors(PlayerControl.Ai));
        Assert.Equal(PlayerColor.Red, players.GetColors(PlayerControl.Ai, strong: true));
    }

    [Fact]
    public void APlayerWhoLeftTheGameIsNoLongerActual()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue | PlayerColor.Red);
        Players.SetPlayerInGame(PlayerColor.Blue, true);
        Players.SetPlayerInGame(PlayerColor.Red, true);

        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, players.GetActualColors());

        Players.SetPlayerInGame(PlayerColor.Red, false);

        Assert.Equal(PlayerColor.Blue, players.GetActualColors());
    }

    [Fact]
    public void TheRandomRacesAreDrawnWithoutRepeating()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red);
        Players.SetPlayerRace(PlayerColor.Blue, MapRace.Knight);
        Players.SetPlayerRace(PlayerColor.Green, MapRace.Random);
        Players.SetPlayerRace(PlayerColor.Red, MapRace.Random);
        Players.SetPlayerControl(PlayerColor.Green, PlayerControl.Human | PlayerControl.Ai);

        players.SetStartGame();

        foreach (Player player in players.All)
        {
            Assert.True(player.IsPlay);
            Assert.Contains(player.Race, MapRaces.Basic);
        }

        MapRace green = Players.GetPlayerRace(PlayerColor.Green);
        MapRace red = Players.GetPlayerRace(PlayerColor.Red);

        Assert.NotEqual(MapRace.Knight, green);
        Assert.NotEqual(MapRace.Knight, red);
        Assert.NotEqual(green, red);

        // The "either side" state only exists before the game; from here on the computer has it.
        Assert.Equal(PlayerControl.Ai, Players.GetPlayerControl(PlayerColor.Green));
        Assert.Equal(PlayerColor.None, players.CurrentColor);
    }

    [Fact]
    public void StartingTheGameForgetsEveryCamera()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue);
        players.All[0].Focus.Set(new Hero(1, MapRace.Knight));

        Assert.Equal(FocusKind.Heroes, players.All[0].Focus.Kind);

        players.SetStartGame();

        Assert.Equal(FocusKind.None, players.All[0].Focus.Kind);
        Assert.Null(players.All[0].Focus.GetHeroes());
    }

    [Fact]
    public void TheHumanColorsAreCachedUntilTheGameStarts()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue);
        Players.SetPlayerControl(PlayerColor.Blue, PlayerControl.Human);

        Assert.Equal(PlayerColor.Blue, Players.HumanColors());

        // The cache is the reference's: changing a controller does not invalidate it, starting the game does.
        Players.SetPlayerControl(PlayerColor.Blue, PlayerControl.Ai);
        Assert.Equal(PlayerColor.Blue, Players.HumanColors());

        players.SetStartGame();
        Assert.Equal(PlayerColor.None, Players.HumanColors());
    }

    [Fact]
    public void AFriendIsNotAnOpponent()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue | PlayerColor.Red);
        Players.SetPlayerInGame(PlayerColor.Blue, true);
        Players.SetPlayerInGame(PlayerColor.Red, true);

        Assert.Equal(new[] { PlayerColor.Red }, Players.GetInPlayOpponents(PlayerColor.Blue));

        // Friendship is one sided: red may not attack blue, which does not stop blue from attacking red.
        players.All[1].Friends = PlayerColor.Blue | PlayerColor.Red;

        Assert.Equal(new[] { PlayerColor.Red }, Players.GetInPlayOpponents(PlayerColor.Blue));
        Assert.Empty(Players.GetInPlayOpponents(PlayerColor.Red));
        Assert.False(Players.IsFriends(PlayerColor.Blue, PlayerColor.Red));
        Assert.True(Players.IsFriends(PlayerColor.Red, PlayerColor.Blue));

        Assert.Empty(Players.GetInPlayOpponents(PlayerColor.None));
        Assert.Empty(Players.GetInPlayOpponents(PlayerColor.Green));
    }

    [Fact]
    public void TheFriendsOfTheCurrentPlayerAreTheFriendColors()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue | PlayerColor.Red);
        players.All[1].Friends = PlayerColor.Blue | PlayerColor.Red;

        Assert.Equal(PlayerColor.None, Players.FriendColors());

        players.SetCurrentColor(PlayerColor.Red);

        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, Players.FriendColors());
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, Players.GetPlayerFriends(PlayerColor.Red));
        Assert.Equal(PlayerColor.None, Players.GetPlayerFriends(PlayerColor.Green));
    }

    [Fact]
    public void ClearingDropsTheSlotsAndTheTable()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue);
        Players.SetPlayerInGame(PlayerColor.Blue, true);
        players.SetCurrentColor(PlayerColor.Blue);
        Assert.True(Players.GetPlayerInGame(PlayerColor.Blue));

        players.Clear();

        Assert.Empty(players.All);
        Assert.Null(Players.Get(PlayerColor.Blue));
        Assert.Null(players.Current);
        Assert.Equal(PlayerColor.None, players.CurrentColor);
        Assert.False(Players.GetPlayerInGame(PlayerColor.Blue));
        Assert.Equal(MapRace.None, Players.GetPlayerRace(PlayerColor.Blue));
        Assert.Equal(PlayerColor.None, Players.HumanColors());
    }

    [Fact]
    public void TheDumpListsEverySlot()
    {
        Players players = FreshSettings().Players;

        players.Init(PlayerColor.Blue);

        string text = players.ToString();

        Assert.StartsWith("Players: ", text);
        Assert.Contains("Blue(Neutral, unknown)", text);
    }
}

/// <summary>Covers <c>system/players.cpp</c>: one slot, its name, its controller and its camera.</summary>
public sealed class PlayerTests
{
    [Fact]
    public void ANewSlotIsAFriendOfItselfAndHasNoController()
    {
        Player player = new(PlayerColor.Green);

        Assert.Equal(PlayerColor.Green, player.Color);
        Assert.Equal(PlayerColor.Green, player.Friends);
        Assert.Equal(PlayerControl.None, player.Control);
        Assert.Equal(MapRace.None, player.Race);
        Assert.Equal(Player.HandicapStatus.None, player.Handicap);
        Assert.False(player.IsPlay);
        Assert.False(player.IsAiAutoControlMode);
        Assert.InRange(
            (int)player.Personality,
            (int)AiPersonality.Warrior,
            (int)AiPersonality.Explorer);
    }

    [Fact]
    public void TheDefaultNameIsTheColorAndRenamingBackClearsTheName()
    {
        Player player = new(PlayerColor.Red);

        Assert.True(player.IsDefaultName);
        Assert.Equal("Red", player.DefaultName);
        Assert.Equal("Red", player.Name);

        player.SetName("Archibald");

        Assert.False(player.IsDefaultName);
        Assert.Equal("Archibald", player.Name);

        player.SetName(player.DefaultName);

        Assert.True(player.IsDefaultName);
        Assert.Equal(string.Empty, player.CustomName);
        Assert.Equal("Red", player.Name);
    }

    [Fact]
    public void TheComputerCanTakeOverAtOnceButOnlyHandBackAtTheEnd()
    {
        Player player = new(PlayerColor.Blue) { Control = PlayerControl.Human };

        Assert.True(player.IsControlHuman);
        Assert.False(player.IsControlAi);

        player.SetAiAutoControlMode(true);

        Assert.True(player.IsAiAutoControlMode);
        Assert.True(player.IsControlAi);
        Assert.False(player.IsControlHuman);

        // The stored flags are untouched, which is what lets the mode be switched off again.
        Assert.Equal(PlayerControl.Human, player.Control);

        player.SetAiAutoControlMode(false);

        Assert.True(player.IsAiAutoControlMode);
        Assert.True(player.IsControlAi);

        player.CommitAiAutoControlMode();

        Assert.False(player.IsAiAutoControlMode);
        Assert.True(player.IsControlHuman);
    }

    [Fact]
    public void CommittingWithoutTheModeChangesNothing()
    {
        Player player = new(PlayerColor.Purple) { Control = PlayerControl.Human };

        player.CommitAiAutoControlMode();

        Assert.False(player.IsAiAutoControlMode);
        Assert.Equal(PlayerControl.Human, player.EffectiveControl);
    }

    [Fact]
    public void TheModeWordCarriesTheInGameBit()
    {
        Player player = new(PlayerColor.Orange);

        player.SetPlay(true);

        Assert.True(player.IsPlay);
        Assert.Equal(Players.StInGame, player.Modes);

        player.SetPlay(false);

        Assert.False(player.IsPlay);
        Assert.Equal(0u, player.Modes);
    }

    [Fact]
    public void TheHandicapAndThePersonalityAreStored()
    {
        Player player = new(PlayerColor.Blue)
        {
            Personality = AiPersonality.Builder,
        };

        player.SetHandicapStatus(Player.HandicapStatus.Severe);

        Assert.Equal(Player.HandicapStatus.Severe, player.Handicap);
        Assert.Equal("Builder", player.PersonalityString);
        Assert.True(player.IsColor(PlayerColor.Blue));
        Assert.False(player.IsColor(PlayerColor.Red));
    }
}

/// <summary>Covers the ported <c>Focus</c> of <c>system/players.h</c>.</summary>
public sealed class PlayerFocusTests
{
    [Fact]
    public void ARestFocusHoldsNothing()
    {
        PlayerFocus focus = new();

        Assert.Equal(FocusKind.None, focus.Kind);
        Assert.Null(focus.Hero);
        Assert.Null(focus.Castle);
        Assert.Null(focus.GetHeroes());
        Assert.Null(focus.GetCastle());
    }

    [Fact]
    public void AHeroAndACastleReplaceEachOther()
    {
        PlayerFocus focus = new();
        Hero hero = new(1, MapRace.Knight);
        CastleTown castle = new(42, MapRace.Knight);

        focus.Set(hero);

        Assert.Equal(FocusKind.Heroes, focus.Kind);
        Assert.Same(hero, focus.GetHeroes());
        Assert.Null(focus.GetCastle());
        Assert.Null(focus.Castle);

        focus.Set(castle);

        Assert.Equal(FocusKind.Castle, focus.Kind);
        Assert.Same(castle, focus.GetCastle());
        Assert.Null(focus.GetHeroes());
        Assert.Null(focus.Hero);
    }

    [Fact]
    public void AResetForgetsTheSubjectEntirely()
    {
        PlayerFocus focus = new();

        focus.Set(new Hero(1, MapRace.Knight));
        focus.Reset();

        Assert.Equal(FocusKind.None, focus.Kind);
        Assert.Null(focus.GetHeroes());
        Assert.Null(focus.GetCastle());
    }
}

/// <summary>Covers <c>ai/ai_personality.cpp</c> and the two version headers of the directory.</summary>
public sealed class SystemMiscTests
{
    [Fact]
    public void TheRandomPersonalityNeverLooksHuman()
    {
        for (int draw = 0; draw < 200; ++draw)
        {
            Assert.InRange(AiPersonalityHelper.Random(), AiPersonality.Warrior, AiPersonality.Explorer);
        }
    }

    [Fact]
    public void ThePersonalitiesAreNamedAsInTheReference()
    {
        Assert.Equal("Warrior", AiPersonalityHelper.GetString(AiPersonality.Warrior));
        Assert.Equal("Builder", AiPersonalityHelper.GetString(AiPersonality.Builder));
        Assert.Equal("Explorer", AiPersonalityHelper.GetString(AiPersonality.Explorer));
        Assert.Equal("None", AiPersonalityHelper.GetString(AiPersonality.None));
        Assert.Equal("None", AiPersonalityHelper.GetString((AiPersonality)99));
    }

    [Fact]
    public void TheEngineVersionIsThePrintableOne()
    {
        Assert.Equal(1, EngineVersion.Major);
        Assert.Equal(1, EngineVersion.Minor);
        Assert.Equal(18, EngineVersion.Intermediate);
        Assert.Equal(0, EngineVersion.Build);
        Assert.Equal("1.1.18", EngineVersion.Version);
    }

    [Fact]
    public void TheSaveFormatNumbersAreTheReferenceOnes()
    {
        Assert.Equal(10034, (int)SaveFileFormat.Release1_1_18);
        Assert.Equal(10010, (int)SaveFileFormat.Release1_0_05);
        Assert.Equal(SaveFileFormat.Release1_1_18, SaveFileFormatInfo.Current);
        Assert.Equal(SaveFileFormat.Release1_0_05, SaveFileFormatInfo.LastSupported);
    }

    [Fact]
    public void AFewOfTheFlagWordsBehave()
    {
        BitModes modes = new();

        Assert.False(modes.HasModes(1));
        Assert.False(modes.HasAllModes(1));
        Assert.False(modes.HasModes(0));

        modes.SetModes(0x3);

        Assert.True(modes.HasModes(0x2));
        Assert.True(modes.HasAllModes(0x3));
        Assert.False(modes.HasAllModes(0x7));
        Assert.Equal(0x3u, modes.Modes);

        modes.ResetModes(0x1);

        Assert.False(modes.HasModes(0x1));
        Assert.True(modes.HasModes(0x2));
    }
}
