using FHeroes2.Core.Io;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.Highscores;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two high score tables, <c>fheroes2::HighScoreDataContainer</c> (<c>highscores.cpp:131-377</c>).
/// </summary>
public sealed class HighScoreDataContainerTests : IDisposable
{
    /// <summary>The magic number of the first layout, <c>highscoreFileMagicValueV1</c>.</summary>
    private const uint MagicValueV1 = 0xBADC0DE;

    /// <summary>The magic number of the layout written today, <c>highscoreFileMagicValueV2</c>.</summary>
    private const uint MagicValueV2 = 0xBADC1DE;

    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-highscores-{Guid.NewGuid():N}.hgs");

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public void ARowKeepsEveryFieldAcrossSaveAndLoad()
    {
        HighScoreDataContainer original = new();
        original.Clear();
        original.RegisterScoreStandard(new HighscoreData("de", "Roland", "Beltway", 1234, 70, 150, 99));

        // 少于十条时参考会用默认行补满，所以这里先补到十条再看那一行还在不在。
        Assert.True(original.Save(_filePath));

        HighScoreDataContainer loaded = new();
        Assert.True(loaded.Load(_filePath));

        HighscoreData row = Assert.Single(
            loaded.HighScoresStandard, entry => entry.PlayerName == "Roland");

        Assert.Equal("de", row.LanguageAbbreviation);
        Assert.Equal("Beltway", row.ScenarioName);
        Assert.Equal(1234u, row.CompletionTime);
        Assert.Equal(70u, row.DayCount);
        Assert.Equal(150u, row.Rating);
        Assert.Equal(99u, row.MapSeed);
    }

    [Fact]
    public void LoadRefusesAFileThatIsNotAHighScoreFile()
    {
        File.WriteAllBytes(_filePath, [1, 2, 3, 4, 5]);

        HighScoreDataContainer container = new();

        Assert.False(container.Load(_filePath));
        Assert.Empty(container.HighScoresStandard);
        Assert.Empty(container.HighScoresCampaign);
    }

    [Fact]
    public void MissingFileIsNotAnErrorButAnEmptyResult()
    {
        HighScoreDataContainer container = new();

        Assert.False(container.Load(_filePath));
    }

    /// <summary>
    /// 第一版没有语言字段（<c>highscores.cpp:114-119</c>），读的时候补英文；战役的评分那时恒为 0，读回来要用天数
    /// 顶上（<c>:178-184</c>）。
    /// </summary>
    [Fact]
    public void TheFirstLayoutGetsTheLanguageAndTheCampaignRatingFixedUp()
    {
        WriteFile(
            v1: true,
            [new HighscoreData(string.Empty, "Old Timer", "Beltway", 10, 50, 200, 7)],
            [new HighscoreData(string.Empty, "Old General", "Roland", 20, 42, 0, 8)]);

        HighScoreDataContainer container = new();
        Assert.True(container.Load(_filePath));

        HighscoreData standard = Assert.Single(
            container.HighScoresStandard, entry => entry.PlayerName == "Old Timer");
        Assert.Equal(HighscoreData.EnglishLanguageAbbreviation, standard.LanguageAbbreviation);
        Assert.Equal(200u, standard.Rating);

        HighscoreData campaign = Assert.Single(
            container.HighScoresCampaign, entry => entry.PlayerName == "Old General");
        Assert.Equal(42u, campaign.Rating);
    }

    /// <summary>少于十条就用默认行补满，超过十条就截断（<c>:186-198</c>）。</summary>
    [Fact]
    public void LoadTopsUpShortTablesAndTruncatesLongOnes()
    {
        WriteFile(
            v1: false,
            [new HighscoreData("en", "Only One", "Beltway", 1, 60, 200, 1)],
            []);

        HighScoreDataContainer toppedUp = new();
        Assert.True(toppedUp.Load(_filePath));
        Assert.Equal(10, toppedUp.HighScoresStandard.Count);
        Assert.Equal(10, toppedUp.HighScoresCampaign.Count);

        List<HighscoreData> longTable = [];
        for (uint i = 0; i < 12; ++i)
        {
            longTable.Add(new HighscoreData("en", $"Player {i}", "Beltway", 1, 60, 1000 - i, i));
        }

        WriteFile(v1: false, longTable, longTable);

        HighScoreDataContainer truncated = new();
        Assert.True(truncated.Load(_filePath));
        Assert.Equal(10, truncated.HighScoresStandard.Count);
        Assert.Equal(10, truncated.HighScoresCampaign.Count);
        Assert.Equal(1000u, truncated.HighScoresStandard[0].Rating);
    }

    /// <summary>标准图按分数从高到低排，只留前十（<c>:87-96</c>）。</summary>
    [Fact]
    public void TheStandardTableKeepsTheTenHighestScores()
    {
        HighScoreDataContainer container = new();

        // 依次登记 1 到 10 分，每一行都比已登记的分数高，所以每次都排在榜首。
        for (uint rating = 1; rating <= 10; ++rating)
        {
            int place = container.RegisterScoreStandard(
                new HighscoreData("en", $"Player {rating}", $"Map {rating}", 0, 60, rating, rating));

            Assert.Equal(0, place);
        }

        Assert.Equal(10, container.HighScoresStandard.Count);

        // 第十一行挤进榜首，垫底的那一行被丢掉。
        Assert.Equal(
            0,
            container.RegisterScoreStandard(new HighscoreData("en", "Best", "Best", 0, 60, 11, 11)));
        Assert.Equal(
            -1,
            container.RegisterScoreStandard(new HighscoreData("en", "Worst", "Worst", 0, 60, 0, 0)));

        Assert.Equal(10, container.HighScoresStandard.Count);
        Assert.Equal(11u, container.HighScoresStandard[0].Rating);
        Assert.Equal(2u, container.HighScoresStandard[^1].Rating);
        Assert.DoesNotContain(container.HighScoresStandard, entry => entry.Rating == 1);
    }

    /// <summary>战役反过来：天数越少越靠前（<c>:88-90</c>），评分字段在这里就是天数。</summary>
    [Fact]
    public void TheCampaignTableSortsTheOtherWay()
    {
        HighScoreDataContainer container = new();

        Assert.Equal(0, container.RegisterScoreCampaign(new HighscoreData("en", "Slow", "Roland", 0, 500, 500, 1)));
        Assert.Equal(0, container.RegisterScoreCampaign(new HighscoreData("en", "Fast", "Roland", 0, 300, 300, 2)));

        Assert.Equal(["Fast", "Slow"], container.HighScoresCampaign.Select(entry => entry.PlayerName));
    }

    /// <summary>
    /// 同一次通关（同图、同天数、同评分、同种子）再登记一次只替换那一行，不算新纪录（<c>:79-84</c>）。
    /// </summary>
    [Fact]
    public void RegisteringTheSameCompletionReplacesTheRowInPlace()
    {
        HighScoreDataContainer container = new();
        container.RegisterScoreStandard(new HighscoreData("en", "First", "Beltway", 0, 60, 200, 5));
        container.RegisterScoreStandard(new HighscoreData("en", "Second", "Beltway", 0, 60, 100, 6));

        int place = container.RegisterScoreStandard(new HighscoreData("en", "Renamed", "Beltway", 0, 60, 200, 5));

        Assert.Equal(0, place);
        Assert.Equal(2, container.HighScoresStandard.Count);
        Assert.Equal("Renamed", container.HighScoresStandard[0].PlayerName);
    }

    [Fact]
    public void TheDefaultsFillBothTables()
    {
        HighScoreDataContainer container = new();

        container.PopulateStandardDefaultHighScores();
        container.PopulateCampaignDefaultHighScores();

        Assert.Equal(10, container.HighScoresStandard.Count);
        Assert.Equal(10, container.HighScoresCampaign.Count);

        // 标准默认行从 150 分每行减 10（:341-350）；战役反过来，600 天到 3000 天（:352-377）。
        Assert.Equal(150u, container.HighScoresStandard[0].Rating);
        Assert.Equal(60u, container.HighScoresStandard[^1].Rating);
        Assert.Equal(600u, container.HighScoresCampaign[0].Rating);
        Assert.Equal(3000u, container.HighScoresCampaign[^1].Rating);

        // 战役默认行的场景名来自 Campaign::getCampaignName()，不是写死的字符串。
        Assert.Contains(
            container.HighScoresCampaign,
            entry => entry.ScenarioName == CampaignAwardData.GetCampaignName(CampaignId.Roland));
    }

    /// <summary>
    /// 分数到兵种的对照：农民 0 到 3、妖精 4 起、黑龙 228 起（<c>highscores.cpp:237-258</c> 的注释）。
    /// </summary>
    [Theory]
    [InlineData(0u, MonsterType.Peasant)]
    [InlineData(3u, MonsterType.Peasant)]
    [InlineData(4u, MonsterType.Goblin)]
    [InlineData(228u, MonsterType.BlackDragon)]
    [InlineData(100000u, MonsterType.BlackDragon)]
    public void TheRatingTableFollowsTheReferenceThresholds(uint rating, MonsterType expected)
    {
        Assert.Equal(expected, HighScoreDataContainer.GetMonsterByRating(rating));
    }

    /// <summary>
    /// 天数到兵种的对照：阈值是「本级起点 + 步长」，所以黑龙到 300、泰坦只有 301 到 320、德鲁伊 1100 起每级 100、
    /// 战斗矮人 2200 起每级 200、农民从 5801 起（<c>highscores.cpp:283-330</c>）。
    /// </summary>
    [Theory]
    [InlineData(0u, MonsterType.BlackDragon)]
    [InlineData(300u, MonsterType.BlackDragon)]
    [InlineData(301u, MonsterType.Titan)]
    [InlineData(320u, MonsterType.Titan)]
    [InlineData(321u, MonsterType.RedDragon)]
    [InlineData(1000u, MonsterType.SteelGolem)]
    [InlineData(1001u, MonsterType.Druid)]
    [InlineData(2000u, MonsterType.Nomad)]
    [InlineData(2001u, MonsterType.BattleDwarf)]
    [InlineData(5800u, MonsterType.Goblin)]
    [InlineData(5801u, MonsterType.Peasant)]
    [InlineData(99999u, MonsterType.Peasant)]
    public void TheDayTableFollowsTheReferenceThresholds(uint days, MonsterType expected)
    {
        Assert.Equal(expected, HighScoreDataContainer.GetMonsterByDay(days));
    }

    /// <summary>
    /// Writes a high score file by hand, so the two layouts can be exercised without a game playthrough.
    /// </summary>
    /// <param name="v1">True to write the first layout, which has no language field.</param>
    /// <param name="standard">The single-map rows.</param>
    /// <param name="campaign">The campaign rows.</param>
    private void WriteFile(
        bool v1, IReadOnlyList<HighscoreData> standard, IReadOnlyList<HighscoreData> campaign)
    {
        StreamBuf buffer = new();
        buffer.SetBigEndian(true);
        buffer.Write(v1 ? MagicValueV1 : MagicValueV2);

        WriteTable(buffer, standard, v1);
        WriteTable(buffer, campaign, v1);

        using StreamFile file = new();
        file.SetBigEndian(true);

        Assert.True(file.OpenForWriting(_filePath));
        Assert.True(Compression.ZipStreamBuf(buffer, file));
    }

    private static void WriteTable(StreamBuf buffer, IReadOnlyList<HighscoreData> rows, bool v1)
    {
        buffer.Write(rows.Count);

        foreach (HighscoreData row in rows)
        {
            if (v1)
            {
                // loadV1() 的字段顺序：名字、场景、完成时间、天数、评分、种子（语言缺席）。
                buffer.Write(row.PlayerName);
                buffer.Write(row.ScenarioName);
                buffer.Write(row.CompletionTime);
                buffer.Write(row.DayCount);
                buffer.Write(row.Rating);
                buffer.Write(row.MapSeed);
            }
            else
            {
                row.Write(buffer);
            }
        }
    }
}
