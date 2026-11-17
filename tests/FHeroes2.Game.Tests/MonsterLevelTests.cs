using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the random monster buckets of <c>monster.cpp:576-689</c>: ten creatures in the first tier, fifteen in
/// the second, twenty-seven in the third and fourteen in the fourth, sixty-six in all — every real creature
/// sits in exactly one of them.
/// </summary>
public class MonsterLevelTests
{
    private static readonly int[] TierSizes = [10, 15, 27, 14];

    [Fact]
    public void EveryRealCreatureHasALevel()
    {
        for (int id = (int)MonsterType.Peasant; id <= (int)MonsterType.WaterElement; ++id)
        {
            MonsterLevelType level = MonsterLevels.GetRandomUnitLevel((MonsterType)id);

            Assert.InRange(level, MonsterLevelType.Level1, MonsterLevelType.Level4);
        }
    }

    [Fact]
    public void TheTiersHoldSixtySixCreatures()
    {
        var counts = new Dictionary<MonsterLevelType, int>();

        for (int id = (int)MonsterType.Peasant; id <= (int)MonsterType.WaterElement; ++id)
        {
            MonsterLevelType level = MonsterLevels.GetRandomUnitLevel((MonsterType)id);
            counts[level] = counts.GetValueOrDefault(level) + 1;
        }

        Assert.Equal(TierSizes[0], counts[MonsterLevelType.Level1]);
        Assert.Equal(TierSizes[1], counts[MonsterLevelType.Level2]);
        Assert.Equal(TierSizes[2], counts[MonsterLevelType.Level3]);
        Assert.Equal(TierSizes[3], counts[MonsterLevelType.Level4]);
        Assert.Equal(4, counts.Count);
        Assert.Equal(66, counts.Values.Sum());
    }

    [Fact]
    public void TheUnknownMonsterAndTheMarkersHaveNoTier()
    {
        Assert.Equal(MonsterLevelType.Any, MonsterLevels.GetRandomUnitLevel(MonsterType.Unknown));
        Assert.Equal(MonsterLevelType.Any, MonsterLevels.GetRandomUnitLevel(MonsterType.MonsterCount));
    }

    [Fact]
    public void TheLevelMarkersAnswerWithTheirOwnTier()
    {
        Assert.Equal(MonsterLevelType.Level1, MonsterLevels.GetRandomUnitLevel(MonsterType.RandomMonsterLevel1));
        Assert.Equal(MonsterLevelType.Level2, MonsterLevels.GetRandomUnitLevel(MonsterType.RandomMonsterLevel2));
        Assert.Equal(MonsterLevelType.Level3, MonsterLevels.GetRandomUnitLevel(MonsterType.RandomMonsterLevel3));
        Assert.Equal(MonsterLevelType.Level4, MonsterLevels.GetRandomUnitLevel(MonsterType.RandomMonsterLevel4));
    }

    [Fact]
    public void TheGenericMarkerRollsATierEveryTime()
    {
        var seen = new HashSet<MonsterLevelType>();

        for (int i = 0; i < 64; ++i)
        {
            MonsterLevelType level = MonsterLevels.GetRandomUnitLevel(MonsterType.RandomMonster);

            Assert.InRange(level, MonsterLevelType.Level1, MonsterLevelType.Level4);
            seen.Add(level);
        }

        Assert.True(seen.Count > 1, "sixty-four rolls should not all land in one tier");
    }

    [Fact]
    public void ADrawFromATierStaysInTheTier()
    {
        HashSet<MonsterType> tier = [.. Enumerable.Range((int)MonsterType.Peasant, (int)MonsterType.WaterElement)
            .Select(id => (MonsterType)id)
            .Where(monster => MonsterLevels.GetRandomUnitLevel(monster) == MonsterLevelType.Level4)];

        for (int i = 0; i < 200; ++i)
        {
            Assert.Contains(MonsterLevels.GetRandom(MonsterLevelType.Level4), tier);
        }
    }

    [Fact]
    public void AnyDrawIsAlwaysARealCreature()
    {
        for (int i = 0; i < 200; ++i)
        {
            MonsterType monster = MonsterLevels.GetRandom(MonsterLevelType.Any);

            Assert.True(MonsterStatistics.IsValid(monster), $"{monster} came out of an any draw");
            Assert.False(MonsterStatistics.IsRandomMonster(monster), $"{monster} is a marker, not a creature");
        }
    }

    [Fact]
    public void ATierOutsideTheFourIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MonsterLevels.GetRandom((MonsterLevelType)5));
    }
}
