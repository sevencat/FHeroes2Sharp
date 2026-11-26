using FHeroes2.Assets.Maps;
using FHeroes2.Game.Highscores;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The session around the two tables, <c>Game::DisplayHighScores()</c>
/// (<c>game_highscores.cpp:245-476</c>) and the two score formulas of <c>game.cpp:358-439</c>.
/// </summary>
public sealed class HighScoreBoardTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-highscore-board-{Guid.NewGuid():N}.hgs");

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public void Load_FillsBothTablesWithTheDefaultsWhenThereIsNoFile()
    {
        HighScoreBoard board = new(_filePath);

        Assert.False(board.Load());
        Assert.True(board.IsLoaded);
        Assert.Equal(HighScoreBoard.MaximumEntries, board.Container.HighScoresStandard.Count);
        Assert.Equal(HighScoreBoard.MaximumEntries, board.Container.HighScoresCampaign.Count);

        // 参考只在使用时写文件：光看榜不会生成 fheroes2.hgs。
        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public void Load_ReadsTheFileOnlyOnce()
    {
        HighScoreBoard first = new(_filePath);
        first.PopulateDefaults();
        Assert.True(first.Register("Beltway", "Roland", 70, 150, 5, completionTime: 1000) >= 0);
        Assert.True(first.Save());

        HighScoreBoard board = new(_filePath);
        Assert.True(board.Load());

        // 文件删掉也不影响：isInternalUpdate 之后参考不会再读第二次。
        File.Delete(_filePath);

        Assert.True(board.Load());
        Assert.Contains(board.Entries, row => row.PlayerName == "Roland" && row.MapSeed == 5);
    }

    [Fact]
    public void Accepts_ComparesAStandardScoreWithTheLastRow()
    {
        HighScoreBoard board = new(_filePath);
        board.PopulateDefaults();

        // 默认标准榜从 150 分降到 60 分，最后一行就是下限。
        Assert.True(board.Accepts(60));
        Assert.True(board.Accepts(61));
        Assert.True(board.Accepts(1000));
        Assert.False(board.Accepts(59));
    }

    [Fact]
    public void Accepts_UsesTheOtherDirectionForCampaigns()
    {
        HighScoreBoard board = new(_filePath) { Mode = HighScoreMode.Campaign };
        board.PopulateDefaults();

        // 战役榜按天数升序，最后一行是上限：更慢的不上榜。
        Assert.True(board.Accepts(600));
        Assert.True(board.Accepts(1));
        Assert.True(board.Accepts(3000));
        Assert.False(board.Accepts(3001));
    }

    [Fact]
    public void Register_PutsTheRowIntoTheTableOnScreen()
    {
        HighScoreBoard board = new(_filePath) { Mode = HighScoreMode.Campaign };
        board.PopulateDefaults();

        int index = board.Register("Roland", "Astra", 500, 500, 42);

        Assert.Equal(0, index);

        HighscoreData row = board.Entries[0];

        Assert.Equal(HighscoreData.EnglishLanguageAbbreviation, row.LanguageAbbreviation);
        Assert.Equal("Astra", row.PlayerName);
        Assert.Equal("Roland", row.ScenarioName);
        Assert.Equal(500u, row.DayCount);
        Assert.Equal(500u, row.Rating);
        Assert.Equal(42u, row.MapSeed);

        // 标准榜那一张不受影响。
        Assert.DoesNotContain(board.Container.HighScoresStandard, entry => entry.PlayerName == "Astra");
    }

    [Fact]
    public void Register_UsesTheUnknownHeroForABlankName()
    {
        HighScoreBoard board = new(_filePath);

        Assert.Equal(0, board.Register("Beltway", string.Empty, 70, 500, 1));
        Assert.Equal(HighScoreBoard.DefaultPlayerName, board.Entries[0].PlayerName);

        Assert.Equal(HighScoreBoard.DefaultPlayerName, HighScoreBoard.NormalizePlayerName(null));
        Assert.Equal("Roland", HighScoreBoard.NormalizePlayerName("Roland"));
    }

    [Fact]
    public void Save_WritesTheRowBackIntoTheFile()
    {
        HighScoreBoard board = new(_filePath) { Mode = HighScoreMode.Campaign };
        board.PopulateDefaults();
        board.Register("Astra", "Astra", 500, 500, 42, completionTime: 1234);

        Assert.True(board.Save());

        HighScoreBoard loaded = new(_filePath) { Mode = HighScoreMode.Campaign };

        Assert.True(loaded.Load());
        Assert.Contains(loaded.Entries, row => row.PlayerName == "Astra" && row.MapSeed == 42);
    }

    [Theory]
    [InlineData(MapSize.Small, 30u, 158u)]
    [InlineData(MapSize.Medium, 60u, 140u)]
    [InlineData(MapSize.Small, 100u, 105u)]
    [InlineData(MapSize.Large, 500u, 45u)]
    [InlineData(MapSize.ExtraLarge, 1000u, 20u)]
    [InlineData(MapSize.None, 100u, 200u)]
    public void GetGameOverScoreFactor_FollowsTheFourDayBands(MapSize size, uint dayCount, uint expected)
    {
        Assert.Equal(expected, HighScoreBoard.GetGameOverScoreFactor(size, dayCount));
    }

    [Fact]
    public void GetStandardRating_MultipliesTheScenarioRating()
    {
        // 100 分的地图、中号地图、三十天：系数 170，于是成绩 170。
        Assert.Equal(170u, HighScoreBoard.GetStandardRating(100, MapSize.Medium, 30));
        Assert.Equal(200u, HighScoreBoard.GetStandardRating(100, MapSize.None, 30));
    }

    [Fact]
    public void GetCampaignRating_MultipliesTheDaysByTheDifficultyPercent()
    {
        Assert.Equal(750u, HighScoreBoard.GetCampaignRating(600, 125));
        Assert.Equal(600u, HighScoreBoard.GetCampaignRating(600, 100));
        Assert.Equal(450u, HighScoreBoard.GetCampaignRating(601, 75));
    }
}
