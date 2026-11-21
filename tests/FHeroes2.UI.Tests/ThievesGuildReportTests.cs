using FHeroes2.Assets.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The thieves' guild ranking: the ten rows, their thresholds and the flag grouping
/// (<c>dialog_thievesguild.cpp:63-136</c>, <c>319-403</c>).
/// </summary>
public sealed class ThievesGuildReportTests
{
    [Fact]
    public void Rows_AreTheTenOfTheReference()
    {
        Assert.Equal(
            new[]
            {
                ThievesGuildRow.Towns,
                ThievesGuildRow.Castles,
                ThievesGuildRow.Heroes,
                ThievesGuildRow.Gold,
                ThievesGuildRow.WoodAndOre,
                ThievesGuildRow.RareResources,
                ThievesGuildRow.Obelisks,
                ThievesGuildRow.Artifacts,
                ThievesGuildRow.ArmyStrength,
                ThievesGuildRow.Income,
            },
            ThievesGuildRows.All);
    }

    [Theory]
    [InlineData(ThievesGuildRow.Towns, "Number of Towns:")]
    [InlineData(ThievesGuildRow.Castles, "Number of Castles:")]
    [InlineData(ThievesGuildRow.Heroes, "Number of Heroes:")]
    [InlineData(ThievesGuildRow.Gold, "Gold in Treasury:")]
    [InlineData(ThievesGuildRow.WoodAndOre, "Wood & Ore:")]
    [InlineData(ThievesGuildRow.RareResources, "Gems, Cr, Slf & Mer:")]
    [InlineData(ThievesGuildRow.Obelisks, "Obelisks Found:")]
    [InlineData(ThievesGuildRow.Artifacts, "Artifacts:")]
    [InlineData(ThievesGuildRow.ArmyStrength, "Total Army Strength:")]
    [InlineData(ThievesGuildRow.Income, "Income:")]
    public void Labels_AreTheReferenceStrings(ThievesGuildRow row, string label) =>
        Assert.Equal(label, ThievesGuildRows.Label(row));

    [Theory]
    [InlineData(ThievesGuildRow.Gold, 1)]
    [InlineData(ThievesGuildRow.WoodAndOre, 1)]
    [InlineData(ThievesGuildRow.RareResources, 2)]
    [InlineData(ThievesGuildRow.Obelisks, 2)]
    [InlineData(ThievesGuildRow.Artifacts, 3)]
    [InlineData(ThievesGuildRow.ArmyStrength, 3)]
    [InlineData(ThievesGuildRow.Income, 4)]
    public void RequiredGuilds_FollowTheReferenceIfs(ThievesGuildRow row, int guilds) =>
        Assert.Equal(guilds, ThievesGuildRows.RequiredGuilds(row));

    [Fact]
    public void TheFirstThreeRows_AreAlwaysRevealed()
    {
        Assert.True(ThievesGuildRows.IsRevealed(ThievesGuildRow.Towns, 0));
        Assert.True(ThievesGuildRows.IsRevealed(ThievesGuildRow.Castles, 0));
        Assert.True(ThievesGuildRows.IsRevealed(ThievesGuildRow.Heroes, 0));

        Assert.False(ThievesGuildRows.IsRevealed(ThievesGuildRow.Gold, 0));
        Assert.False(ThievesGuildRows.IsRevealed(ThievesGuildRow.Gold, 1));
        Assert.True(ThievesGuildRows.IsRevealed(ThievesGuildRow.Gold, 2));

        Assert.False(ThievesGuildRows.IsRevealed(ThievesGuildRow.RareResources, 2));
        Assert.True(ThievesGuildRows.IsRevealed(ThievesGuildRow.RareResources, 3));

        Assert.False(ThievesGuildRows.IsRevealed(ThievesGuildRow.Income, 4));
        Assert.True(ThievesGuildRows.IsRevealed(ThievesGuildRow.Income, 5));
    }

    [Fact]
    public void Rankings_MergeEqualValuesAndSortDown()
    {
        ThievesGuildReport report = ThievesGuildReport.Create(
            [
                CreatePlayer(PlayerColor.Blue, gold: 100),
                CreatePlayer(PlayerColor.Red, gold: 50),
                CreatePlayer(PlayerColor.Green, gold: 100),
            ],
            2);

        IReadOnlyList<ThievesGuildGroup> groups = report.Ranking(ThievesGuildRow.Gold);

        Assert.Equal(2, groups.Count);
        Assert.Equal(100, groups[0].Value);
        Assert.Equal(new[] { PlayerColor.Blue, PlayerColor.Green }, PlayerColors.ToList(groups[0].Colors));
        Assert.Equal(50, groups[1].Value);
        Assert.Equal(new[] { PlayerColor.Red }, PlayerColors.ToList(groups[1].Colors));
    }

    [Fact]
    public void Rankings_PutEverybodyInOneGroupWhenNobodyDiffers()
    {
        ThievesGuildReport report = ThievesGuildReport.Create(
            [CreatePlayer(PlayerColor.Blue), CreatePlayer(PlayerColor.Red)],
            0);

        IReadOnlyList<ThievesGuildGroup> groups = report.Ranking(ThievesGuildRow.Heroes);

        Assert.Single(groups);
        Assert.Equal(2, PlayerColors.ToList(groups[0].Colors).Count);
    }

    [Fact]
    public void RankingCount_IsHowManyColoursShareTheValue()
    {
        IReadOnlyList<ThievesGuildGroup> groups = ThievesGuildRanking.Build(
            [PlayerColor.Blue, PlayerColor.Red, PlayerColor.Green],
            color => color == PlayerColor.Red ? 7 : 3);

        Assert.Equal(2, groups.Count);
        Assert.Equal(1, ThievesGuildRanking.Count(groups[0].Colors));
        Assert.Equal(2, ThievesGuildRanking.Count(groups[1].Colors));
    }

    [Theory]
    [InlineData(0, "1st")]
    [InlineData(1, "2nd")]
    [InlineData(2, "3rd")]
    [InlineData(3, "4th")]
    [InlineData(4, "5th")]
    [InlineData(5, "6th")]
    public void Ordinals_UseTheReferenceSuffixes(int column, string ordinal) =>
        Assert.Equal(ordinal, ThievesGuildRanking.Ordinal(column));

    [Fact]
    public void Value_MapsEveryRowToItsColumn()
    {
        ThievesGuildPlayer player = new(
            PlayerColor.Blue,
            Towns: 1,
            Castles: 2,
            Heroes: 3,
            Gold: 4,
            WoodAndOre: 5,
            RareResources: 6,
            Obelisks: 7,
            Artifacts: 8,
            ArmyStrength: 9,
            Income: 10,
            Personality: "Human",
            BestHero: null,
            StrongestMonster: null);

        Assert.Equal(1, player.Value(ThievesGuildRow.Towns));
        Assert.Equal(2, player.Value(ThievesGuildRow.Castles));
        Assert.Equal(3, player.Value(ThievesGuildRow.Heroes));
        Assert.Equal(4, player.Value(ThievesGuildRow.Gold));
        Assert.Equal(5, player.Value(ThievesGuildRow.WoodAndOre));
        Assert.Equal(6, player.Value(ThievesGuildRow.RareResources));
        Assert.Equal(7, player.Value(ThievesGuildRow.Obelisks));
        Assert.Equal(8, player.Value(ThievesGuildRow.Artifacts));
        Assert.Equal(9, player.Value(ThievesGuildRow.ArmyStrength));
        Assert.Equal(10, player.Value(ThievesGuildRow.Income));
    }

    [Fact]
    public void Titles_DistinguishTheGuildFromTheOracle()
    {
        Assert.Equal(
            "Thieves' Guild: Player Rankings",
            ThievesGuildReport.Create([CreatePlayer(PlayerColor.Blue)], 1).Title);
        Assert.Equal(
            "Oracle: Player Rankings",
            ThievesGuildReport.Create([CreatePlayer(PlayerColor.Blue)], ThievesGuildReport.OracleGuilds).Title);
    }

    [Fact]
    public void Oracle_RevealsEveryRow()
    {
        ThievesGuildReport report = ThievesGuildReport.Create(
            [CreatePlayer(PlayerColor.Blue)],
            ThievesGuildReport.OracleGuilds);

        Assert.True(report.IsOracle);

        foreach (ThievesGuildRow row in ThievesGuildRows.All)
        {
            Assert.True(report.IsRevealed(row));
        }

        Assert.True(report.ShowsBestHero);
        Assert.True(report.ShowsHeroStats);
        Assert.True(report.ShowsPersonality);
        Assert.True(report.ShowsBestMonster);

        // The oracle passes 0xFF, which is the sentinel and not a real guild count.
        Assert.Equal(ThievesGuildReport.OracleGuilds, report.ThievesGuilds);
    }

    [Theory]
    [InlineData(0, false, false, false, false)]
    [InlineData(1, true, false, false, false)]
    [InlineData(2, true, true, false, false)]
    [InlineData(3, true, true, true, false)]
    [InlineData(4, true, true, true, true)]
    public void TheFourExtraRows_OpenOneGuildAtATime(
        int guilds,
        bool bestHero,
        bool heroStats,
        bool personality,
        bool bestMonster)
    {
        ThievesGuildReport report = ThievesGuildReport.Create([CreatePlayer(PlayerColor.Blue)], guilds);

        Assert.Equal(bestHero, report.ShowsBestHero);
        Assert.Equal(heroStats, report.ShowsHeroStats);
        Assert.Equal(personality, report.ShowsPersonality);
        Assert.Equal(bestMonster, report.ShowsBestMonster);
    }

    [Fact]
    public void Find_ReturnsTheColumnOfItsColour()
    {
        ThievesGuildReport report = ThievesGuildReport.Create(
            [CreatePlayer(PlayerColor.Blue, heroes: 2), CreatePlayer(PlayerColor.Red, heroes: 5)],
            0);

        Assert.Equal(5, report.Find(PlayerColor.Red)!.Heroes);
        Assert.Null(report.Find(PlayerColor.Green));
    }

    [Fact]
    public void Create_RejectsMissingPlayers() =>
        Assert.Throws<ArgumentNullException>(() => ThievesGuildReport.Create(null!, 1));

    private static ThievesGuildPlayer CreatePlayer(
        PlayerColor color,
        int heroes = 0,
        int gold = 0) => new(
        color,
        Towns: 0,
        Castles: 0,
        Heroes: heroes,
        Gold: gold,
        WoodAndOre: 0,
        RareResources: 0,
        Obelisks: 0,
        Artifacts: 0,
        ArmyStrength: 0,
        Income: 0,
        Personality: "Human",
        BestHero: null,
        StrongestMonster: null);
}
