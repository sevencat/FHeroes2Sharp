using FHeroes2.Game.Heroes;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Heroes::GetExperienceFromLevel</c> / <c>GetLevelFromExperience</c> (<c>heroes.cpp:1501-1607</c>).
/// </summary>
public sealed class HeroExperienceTests
{
    [Theory]
    [InlineData(0, 0u)]
    [InlineData(1, 1000u)]
    [InlineData(2, 2000u)]
    [InlineData(3, 3200u)]
    [InlineData(4, 4500u)]
    [InlineData(5, 6000u)]
    [InlineData(6, 7700u)]
    [InlineData(7, 9000u)]
    [InlineData(8, 11000u)]
    [InlineData(9, 13200u)]
    [InlineData(10, 15500u)]
    [InlineData(11, 18500u)]
    [InlineData(12, 22100u)]
    [InlineData(13, 26400u)]
    [InlineData(14, 31600u)]
    [InlineData(15, 37800u)]
    [InlineData(16, 45300u)]
    [InlineData(17, 54200u)]
    [InlineData(18, 65000u)]
    [InlineData(19, 78000u)]
    [InlineData(20, 93600u)]
    [InlineData(21, 112300u)]
    [InlineData(22, 134700u)]
    [InlineData(23, 161600u)]
    [InlineData(24, 193900u)]
    [InlineData(25, 232700u)]
    [InlineData(26, 279300u)]
    [InlineData(27, 335200u)]
    [InlineData(28, 402300u)]
    [InlineData(29, 482800u)]
    [InlineData(30, 579400u)]
    [InlineData(31, 695300u)]
    [InlineData(32, 834400u)]
    [InlineData(33, 1001300u)]
    [InlineData(34, 1201600u)]
    [InlineData(35, 1442000u)]
    [InlineData(36, 1730500u)]
    [InlineData(37, 2076700u)]
    [InlineData(38, 2492100u)]
    [InlineData(39, 2990600u)]
    public void FromLevel_IsTheHardcodedTable(int level, uint experience)
    {
        // Levels 0 to 39 are a switch in the reference, not a formula: the table is the balance.
        Assert.Equal(experience, HeroExperience.FromLevel(level));
    }

    [Fact]
    public void MaxValueIsTheThresholdOfTheLastTableLevel()
    {
        Assert.Equal(HeroExperience.MaxValue, HeroExperience.FromLevel(HeroExperience.MaxTableLevel));
        Assert.Equal(2990600u, HeroExperience.MaxValue);
    }

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(999u, 1)]
    [InlineData(1000u, 2)]
    [InlineData(1999u, 2)]
    [InlineData(2000u, 3)]
    [InlineData(15499u, 10)]
    [InlineData(15500u, 11)]
    [InlineData(2990599u, 39)]
    [InlineData(2990600u, 40)]
    public void LevelFrom_ReturnsTheHighestThresholdNotAboveTheExperience(uint experience, int level)
    {
        Assert.Equal(level, HeroExperience.LevelFrom(experience));
    }

    [Fact]
    public void LevelAndExperienceAgreeOnEveryBoundaryOfTheTable()
    {
        for (int level = 1; level <= HeroExperience.MaxTableLevel; ++level)
        {
            uint threshold = HeroExperience.FromLevel(level);

            // A threshold is the experience at which the level is *left*: a hero with exactly the threshold of
            // level 1 (1000) is level 2, and one point less is still level 1.
            Assert.Equal(level + 1, HeroExperience.LevelFrom(threshold));
            Assert.Equal(level, HeroExperience.LevelFrom(threshold - 1));
        }
    }

    [Theory]
    [InlineData(40, 3588800u)]
    [InlineData(41, 4306600u)]
    [InlineData(42, 5168000u)]
    public void FromLevel_GrowsByOnePointTwoPercentRoundedToHundredsAboveTheTable(int level, uint expected)
    {
        // The tail of the reference's switch:
        //   level 40: 2990600 + round( ( 2990600 - 2492100 ) * 1.2 / 100 ) * 100
        //           = 2990600 + round( 5982.0 ) * 100 = 3588800
        //   level 41: 3588800 + round( ( 3588800 - 2990600 ) * 1.2 / 100 ) * 100
        //           = 3588800 + round( 7178.4 ) * 100 = 4306600
        //   level 42: 4306600 + round( ( 4306600 - 3588800 ) * 1.2 / 100 ) * 100
        //           = 4306600 + round( 8613.6 ) * 100 = 5168000
        Assert.Equal(expected, HeroExperience.FromLevel(level));
    }

    [Fact]
    public void FromLevel_RejectsANegativeLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroExperience.FromLevel(-1));
    }
}
