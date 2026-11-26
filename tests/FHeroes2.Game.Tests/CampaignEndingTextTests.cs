using FHeroes2.Game.Campaign;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The rating text the reference burns over <c>WIN.SMK</c> when a campaign ends
/// (<c>game_campaign.cpp:1245-1269</c>).
/// </summary>
public sealed class CampaignEndingTextTests
{
    [Fact]
    public void TheParagraphsAreTheOnesOfTheReference()
    {
        Assert.Equal("Congratulations!\n\nDays: 120\n", CampaignEndingText.DaysLine(120));
        Assert.Equal("\nDifficulty: Default\n\n", CampaignEndingText.DifficultyLine("Default"));
        Assert.Equal("Score: 250\n\nRating:\nBlack Dragon", CampaignEndingText.ScoreLine(250));
    }

    [Fact]
    public void DescribeJoinsTheThreeParagraphsWithoutExtraSpacing()
    {
        string text = CampaignEndingText.Describe(120, "Increased", 600);

        Assert.Equal(
            CampaignEndingText.DaysLine(120) + CampaignEndingText.DifficultyLine("Increased")
            + CampaignEndingText.ScoreLine(600),
            text);
    }

    [Theory]
    [InlineData(CampaignDifficulty.Decreased, "campaignDifficulty|Decreased")]
    [InlineData(CampaignDifficulty.Default, "campaignDifficulty|Default")]
    [InlineData(CampaignDifficulty.Increased, "campaignDifficulty|Increased")]
    [InlineData(99, "campaignDifficulty|Default")]
    public void TheDifficultyIsTheMsgIdOfTheReference(int difficulty, string expected)
    {
        Assert.Equal(expected, CampaignEndingText.DifficultyMessageId(difficulty));
    }

    [Theory]
    [InlineData(0u, "Black Dragon")]
    [InlineData(250u, "Black Dragon")]
    [InlineData(301u, "Titan")]
    [InlineData(5800u, "Goblin")]
    [InlineData(5801u, "Peasant")]
    public void TheRatingIsTheCreatureOfThatDayCount(uint score, string expected)
    {
        Assert.Equal(expected, CampaignEndingText.RatingName(score));
    }
}
