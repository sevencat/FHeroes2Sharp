using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapSpecsState"/>, the data half of the map specifications window
/// (<c>editor_map_specs_window.cpp:1880-2580</c>): what the state reads out of a header, the three states
/// of a player check box, how a condition that lost its target is repaired, and what the write back puts
/// into the header. The map is synthetic, so the towns and heroes a condition may point at are the two
/// or three a test puts into it.
/// </summary>
public sealed class MapSpecsStateTests
{
    private const PlayerColor FourColours =
        PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red | PlayerColor.Yellow;

    [Fact]
    public void TheStateStartsFromTheHeaderOfTheMap()
    {
        MapSpecsState state = new(CreateMap());

        Assert.Equal("Beltway", state.Name);
        Assert.Equal("Two roads", state.Description);
        Assert.Equal(GameDifficulty.Normal, state.Difficulty);
        Assert.Equal(FourColours, state.AvailablePlayers);
        Assert.Equal(PlayerColor.Blue, state.HumanPlayers);
        Assert.Equal(FourColours, state.ComputerPlayers);
        Assert.Equal(MapVictoryCondition.DefeatEveryone, state.VictoryCondition);
        Assert.Equal(MapLossCondition.Everything, state.LossCondition);
    }

    [Fact]
    public void TheRumorsEventsAndCreatorNotesStartEmpty()
    {
        MapSpecsState state = new(CreateMap());

        // MP2 的头部装不下传闻、每日事件与作者注记，新状态里它们都是空的。
        Assert.Empty(state.Rumors);
        Assert.Empty(state.DailyEvents);
        Assert.Equal(string.Empty, state.CreatorNotes);
    }

    [Fact]
    public void ReplacingTheRumorsCopiesThem()
    {
        MapSpecsState state = new(CreateMap());
        List<string> rumors = ["A rumor.", "Another one."];

        state.SetRumors(rumors);
        rumors.Add("A late one.");

        Assert.Equal(new[] { "A rumor.", "Another one." }, state.Rumors);
    }

    [Fact]
    public void ReplacingTheDailyEventsClonesThem()
    {
        MapSpecsState state = new(CreateMap());
        MapDailyEvent dailyEvent = new() { FirstOccurrenceDay = 3, Message = "The first day." };
        List<MapDailyEvent> events = [dailyEvent];

        state.SetDailyEvents(events);

        // 存的是副本：窗口之后再改它自己那条不影响状态。
        dailyEvent.Message = "Changed later.";

        Assert.Single(state.DailyEvents);
        Assert.Equal("The first day.", state.DailyEvents[0].Message);
    }

    [Fact]
    public void TheCreatorNotesTravelToTheHeader()
    {
        MapSpecsState state = new(CreateMap());

        state.CreatorNotes = "Made for the test.";

        Assert.Equal("Made for the test.", state.CreateInfo().CreatorNotes);
    }

    [Fact]
    public void ImpossibleIsNotAMapDifficulty()
    {
        MapSpecsState state = new(CreateMap());

        // The reference's difficulty list stops at expert: impossible is the player's choice, not the map's.
        state.Difficulty = GameDifficulty.Impossible;

        Assert.Equal(GameDifficulty.Expert, state.Difficulty);
    }

    [Fact]
    public void ColoursWithoutAKingdomAreDroppedOnLoad()
    {
        Mp2Map map = CreateMap(kingdom: PlayerColor.Blue | PlayerColor.Green);

        MapSpecsState state = new(map);

        Assert.Equal(PlayerColor.Blue | PlayerColor.Green, state.AvailablePlayers);
        Assert.False(state.IsAvailable(PlayerColor.Red));

        // A colour without a kingdom is not drawn, so a click on its (absent) check box changes nothing.
        state.TogglePlayerColor(PlayerColor.Red);
        Assert.False(state.IsHumanPlayer(PlayerColor.Red));
    }

    [Fact]
    public void TogglingAColourWalksTheThreeStatesOfItsCheckBox()
    {
        MapSpecsState state = new(CreateMap());
        Assert.True(state.IsHumanPlayer(PlayerColor.Blue));
        Assert.True(state.IsComputerPlayer(PlayerColor.Blue));

        // Both: the first click takes the computer away and leaves the colour to the human.
        state.TogglePlayerColor(PlayerColor.Blue);
        Assert.True(state.IsHumanPlayer(PlayerColor.Blue));
        Assert.False(state.IsComputerPlayer(PlayerColor.Blue));

        // Computer only: the click gives it to the human, keeping the computer, as the reference's else branch does.
        state.TogglePlayerColor(PlayerColor.Green);
        Assert.True(state.IsHumanPlayer(PlayerColor.Green));
        Assert.True(state.IsComputerPlayer(PlayerColor.Green));

        // Human only, and not the last human colour: it goes to the computer.
        state.TogglePlayerColor(PlayerColor.Blue);
        Assert.False(state.IsHumanPlayer(PlayerColor.Blue));
        Assert.True(state.IsComputerPlayer(PlayerColor.Blue));
        Assert.Equal(PlayerColor.Green, state.HumanPlayers);
    }

    [Fact]
    public void TheLastHumanColourCannotBeHandedToTheComputer()
    {
        MapSpecsState state = new(CreateMap());
        state.TogglePlayerColor(PlayerColor.Blue);
        Assert.Equal(PlayerColor.Blue, state.HumanPlayers);

        // Nobody could pick a colour any more, so the reference leaves this click without effect.
        state.TogglePlayerColor(PlayerColor.Blue);

        Assert.True(state.IsHumanPlayer(PlayerColor.Blue));
        Assert.False(state.IsComputerPlayer(PlayerColor.Blue));
    }

    [Fact]
    public void AColourThatIsNotASinglePlayerIsRejected()
    {
        MapSpecsState state = new(CreateMap());

        Assert.Throws<ArgumentOutOfRangeException>(() => state.TogglePlayerColor(PlayerColor.Blue | PlayerColor.Green));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.TogglePlayerColor(PlayerColor.None));
    }

    [Fact]
    public void AGoldTargetIsKeptInThousands()
    {
        Mp2Map map = CreateMap(victory: MapVictoryCondition.CollectGold);
        map.Info = map.Info with { VictoryConditionParameters = [250, 0] };

        MapSpecsState state = new(map);

        Assert.Equal(250, state.VictoryGoldInThousands);

        // The header stores two bytes per parameter; the widget cannot reach further either.
        state.VictoryGoldInThousands = 100_000;
        Assert.Equal(MapSpecsState.MaxConditionParameter, state.VictoryGoldInThousands);
    }

    [Fact]
    public void TheTargetOfATownConditionIsWhereTheFirstTownStands()
    {
        Mp2Map map = CreateMap(castles: [Castle(1, 2), Castle(5, 6)], victory: MapVictoryCondition.CaptureTown);
        map.Info = map.Info with { VictoryConditionParameters = [9, 9] };

        MapSpecsState state = new(map);

        Assert.Equal(new Point(1, 2), state.VictoryTarget);
    }

    [Fact]
    public void TheHeroTargetComesFromTheColoursOnlyTheComputerPlays()
    {
        Mp2Map map = CreateMap(
            heroes: [Hero(PlayerColor.Blue, 1, 1), Hero(PlayerColor.Green, 2, 2), Hero(PlayerColor.Red, 3, 3)],
            humans: PlayerColor.Blue,
            computers: PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
            victory: MapVictoryCondition.KillHero);
        map.Info = map.Info with { VictoryConditionParameters = [2, 2] };

        MapSpecsState state = new(map);

        Assert.Equal(PlayerColor.Green | PlayerColor.Red, state.ComputerOnlyPlayers);
        Assert.Equal(new Point(2, 2), state.VictoryTarget);

        // Green now plays for both, so the target has to move to the one hero left for the computer.
        state.TogglePlayerColor(PlayerColor.Green);

        Assert.Equal(PlayerColor.Red, state.ComputerOnlyPlayers);
        Assert.Equal(new Point(3, 3), state.VictoryTarget);
    }

    [Fact]
    public void TheLossTargetComesFromTheColoursOnlyAHumanPlays()
    {
        Mp2Map map = CreateMap(
            heroes: [Hero(PlayerColor.Blue, 1, 1), Hero(PlayerColor.Red, 2, 2)],
            humans: PlayerColor.Blue | PlayerColor.Red,
            computers: PlayerColor.Red,
            loss: MapLossCondition.LoseHero);
        map.Info = map.Info with { LossConditionParameters = [0, 0] };

        MapSpecsState state = new(map);

        Assert.Equal(PlayerColor.Blue, state.HumanOnlyPlayers);
        Assert.Equal(new Point(1, 1), state.LossTarget);
    }

    [Fact]
    public void AConditionWithoutACandidateFallsBackToTheDefault()
    {
        // No town and no hero on the map: the state cannot keep a condition that would name nothing.
        MapSpecsState state = new(CreateMap(
            victory: MapVictoryCondition.KillHero,
            loss: MapLossCondition.LoseTown));

        Assert.Equal(MapVictoryCondition.DefeatEveryone, state.VictoryCondition);
        Assert.Equal(MapLossCondition.Everything, state.LossCondition);
    }

    [Fact]
    public void AOneColourMapCannotBeSplitInTwoSides()
    {
        MapSpecsState state = new(CreateMap(
            kingdom: PlayerColor.Blue,
            computers: PlayerColor.Blue,
            victory: MapVictoryCondition.DefeatOtherSide));

        Assert.Equal(MapVictoryCondition.DefeatEveryone, state.VictoryCondition);
    }

    [Fact]
    public void TheSideSplitIsClampedToTheAvailableColours()
    {
        Mp2Map map = CreateMap(victory: MapVictoryCondition.DefeatOtherSide);
        map.Info = map.Info with { VictoryConditionParameters = [9, 0] };

        MapSpecsState state = new(map);

        // Four colours: at most three can fight the fourth.
        Assert.Equal(3, state.MaxSideCount);
        Assert.Equal(3, state.VictorySideCount);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red, state.VictorySide1);
        Assert.Equal(PlayerColor.Yellow, state.VictorySide2);

        state.VictorySideCount = 1;
        Assert.Equal(PlayerColor.Blue, state.VictorySide1);
    }

    [Fact]
    public void WriteBackStoresBothCoordinatesOfATownTarget()
    {
        MapSpecsState state = new(CreateMap(castles: [Castle(3, 4)], victory: MapVictoryCondition.CaptureTown));
        state.VictoryTarget = new Point(3, 4);

        Mp2FileInfo info = state.CreateInfo();

        Assert.Equal(MapVictoryCondition.CaptureTown, info.VictoryCondition);
        Assert.Equal(new[] { 3, 4 }, info.VictoryConditionParameters);
    }

    [Fact]
    public void WriteBackStoresTheGoldTargetAsASingleParameter()
    {
        MapSpecsState state = new(CreateMap(victory: MapVictoryCondition.CollectGold));
        state.VictoryGoldInThousands = 250;

        Mp2FileInfo info = state.CreateInfo();

        Assert.Equal(new[] { 250, 0 }, info.VictoryConditionParameters);
    }

    [Fact]
    public void TheArtifactConditionStoresTheIdInTheFirstParameter()
    {
        MapSpecsState state = new(CreateMap(victory: MapVictoryCondition.ObtainArtifact));
        Assert.Equal(MapSpecsState.UltimateArtifact, state.VictoryArtifact);

        state.VictoryArtifact = 17;
        state.ComputerAlsoWins = true;

        Mp2FileInfo info = state.CreateInfo();

        Assert.Equal(new[] { 17, 0 }, info.VictoryConditionParameters);
        Assert.False(info.ComputerAlsoWins);
    }

    [Fact]
    public void TheGoldConditionHonoursBothSwitches()
    {
        MapSpecsState state = new(CreateMap(victory: MapVictoryCondition.CollectGold))
        {
            ComputerAlsoWins = true,
            AllowNormalVictory = true,
        };

        Mp2FileInfo info = state.CreateInfo();

        Assert.True(info.ComputerAlsoWins);
        Assert.True(info.AllowNormalVictory);
    }

    [Fact]
    public void TheHeroConditionsNeverLetTheComputerWin()
    {
        Mp2Map map = CreateMap(
            heroes: [Hero(PlayerColor.Green, 2, 2)],
            humans: PlayerColor.Blue,
            computers: PlayerColor.Blue | PlayerColor.Green,
            victory: MapVictoryCondition.KillHero);
        MapSpecsState state = new(map)
        {
            ComputerAlsoWins = true,
            AllowNormalVictory = true,
        };

        Mp2FileInfo info = state.CreateInfo();

        Assert.Equal(MapVictoryCondition.KillHero, info.VictoryCondition);
        Assert.False(info.ComputerAlsoWins);
        Assert.False(info.AllowNormalVictory);
    }

    [Fact]
    public void DefeatingEveryoneTurnsBothSwitchesOff()
    {
        MapSpecsState state = new(CreateMap())
        {
            ComputerAlsoWins = true,
            AllowNormalVictory = true,
        };

        Mp2FileInfo info = state.CreateInfo();

        Assert.False(info.ComputerAlsoWins);
        Assert.False(info.AllowNormalVictory);
    }

    [Fact]
    public void OtherSideStoresTheSplitAndRebuildsTheUnionTable()
    {
        MapSpecsState state = new(CreateMap(victory: MapVictoryCondition.DefeatOtherSide));
        state.VictorySideCount = 2;

        Mp2FileInfo info = state.CreateInfo();

        Assert.Equal(new[] { 2, 0 }, info.VictoryConditionParameters);

        // Blue and green are the first side, red and yellow the second: every colour carries its side's union.
        Assert.Equal(
            new[]
            {
                PlayerColor.Blue | PlayerColor.Green,
                PlayerColor.Blue | PlayerColor.Green,
                PlayerColor.Red | PlayerColor.Yellow,
                PlayerColor.Red | PlayerColor.Yellow,
                PlayerColor.Orange,
                PlayerColor.Purple,
            },
            info.PlayerUnions);
    }

    [Fact]
    public void WriteBackKeepsTheBytesItDoesNotModel()
    {
        byte[] header = new byte[Mp2FileInfo.HeaderSize];
        header[Mp2FileInfo.HeaderSize - 1] = 0x5A;

        Mp2Map map = CreateMap();
        map.Info = map.Info with { HeaderBytes = header };

        MapSpecsState state = new(map);
        state.Name = "Renamed";

        byte[] written = state.CreateInfo().ToHeaderBytes();

        Assert.Equal(0x5A, written[Mp2FileInfo.HeaderSize - 1]);

        // The name field is the first modelled one; the magic in front of it is written from the constant.
        Assert.Equal((byte)'R', written[Mp2FileInfo.NameFieldOffset]);
        Assert.Equal(0x5C, written[0]);
    }

    [Fact]
    public void TheHeaderOfTheMapIsNotTouchedUntilItIsInstalled()
    {
        Mp2Map map = CreateMap();
        MapSpecsState state = new(map);
        state.Name = "Renamed";

        Assert.Equal("Beltway", map.Info.Name);

        Mp2FileInfo info = state.CreateInfo();
        Assert.Equal("Beltway", map.Info.Name);

        map.Info = info;
        Assert.Equal("Renamed", map.Info.Name);
    }

    [Fact]
    public void TheEditorWritesTheNewHeaderIntoTheFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"fh2-specs-{Guid.NewGuid():N}.mp2");

        try
        {
            Mp2MapWriter.WriteAll(filePath, CreateMap(size: 36));
            Mp2Map map = Mp2Map.Read(filePath);
            MapEditSession session = new(
                new MapEditorView(new AdventureMapView(map, SyntheticTerrain.Create(map.Width, 32), new Size(96, 96))));

            MapSpecsState state = new(session.Map);
            state.Name = "Renamed";
            state.Difficulty = GameDifficulty.Hard;
            session.ApplyMapInfo(state.CreateInfo());

            Assert.True(session.IsDirty);
            Assert.True(session.Save(out string? error), error);
            Assert.False(session.IsDirty);

            // The patch only rewrote the tiles and the header: the name of the file follows the state.
            Mp2Map saved = Mp2Map.Read(filePath);
            Assert.Equal("Renamed", saved.Info.Name);
            Assert.Equal(GameDifficulty.Hard, saved.Info.Difficulty);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AHeaderOfAnotherSizeCannotBeInstalled()
    {
        Mp2Map map = CreateMap();
        MapEditSession session = new(
            new MapEditorView(new AdventureMapView(map, SyntheticTerrain.Create(map.Width, 32), new Size(96, 96))));
        Mp2FileInfo info = map.Info with { Width = map.Width + 1 };

        Assert.Throws<ArgumentException>(() => session.ApplyMapInfo(info));
        Assert.Equal(map.Width, session.Map.Info.Width);
    }

    private static Mp2CastlePosition Castle(int x, int y) => new(x, y, 0, MapRace.Knight);

    private static Mp2MapHero Hero(PlayerColor color, int x, int y) =>
        Mp2MapHeroes.FromSpriteIndex(PlayerColors.IndexOf(color) * Mp2MapHeroes.SpritesPerColor, x, y);

    private static Mp2Map CreateMap(
        Mp2CastlePosition[]? castles = null,
        Mp2MapHero[]? heroes = null,
        PlayerColor? kingdom = null,
        PlayerColor humans = PlayerColor.Blue,
        PlayerColor? computers = null,
        MapVictoryCondition victory = MapVictoryCondition.DefeatEveryone,
        MapLossCondition loss = MapLossCondition.Everything,
        int size = 8)
    {
        Mp2Map map = SyntheticMap.Create(size, size) with
        {
            Castles = castles ?? [],
            Heroes = heroes ?? [],
        };

        map.Info = map.Info with
        {
            Name = "Beltway",
            Description = "Two roads",
            KingdomColors = kingdom ?? FourColours,
            HumanPlayers = humans,
            ComputerPlayers = computers ?? FourColours,
            VictoryCondition = victory,
            LossCondition = loss,
        };

        return map;
    }
}
