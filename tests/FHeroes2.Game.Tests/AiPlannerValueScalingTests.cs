using FHeroes2.Assets.Maps;
using FHeroes2.Game.Ai;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The numbers of <c>scaleWithDistanceAndTime()</c> and <c>getDistanceModifier()</c> against the reference
/// (<c>ai_planner_hero.cpp:966-1023</c>).
/// </summary>
/// <remarks>
/// <para>
/// The two functions decide how much a target loses for being far away, which is what makes the AI pick one object
/// over another; "it does not crash and keeps acting" says nothing about that, so the expected values below are
/// computed once with the reference's own formula
/// (<c>value - d·modifier·time · log10( d·modifier·time )</c>) and then frozen here as literals. If the port's
/// formula drifts, for example by reading the day the wrong way or by dropping the <c>log10</c> factor, these
/// numbers stop matching while the rest of the suite keeps passing.
/// </para>
/// <para>
/// <see cref="Mp2ObjectType.None"/> has the default distance weight of 1.0, so the first theory isolates the
/// distance itself; the second one pins the weight of every group in the reference's switch; the third one pins the
/// day correction, including its 0.5 cap.
/// </para>
/// </remarks>
public sealed class AiPlannerValueScalingTests
{
    /// <summary>The reference works in <c>double</c>; six decimals is far tighter than any value the AI compares.</summary>
    private const int Precision = 6;

    /// <summary>How far the distance itself is allowed to push a value down (<c>scaleWithDistanceAndTime</c>).</summary>
    [Theory]
    [InlineData(0u, 1000.0)] // the hero is standing on it: no penalty at all
    [InlineData(1u, 1000.0)] // log10( 1 ) == 0, so one step is still free
    [InlineData(2u, 999.3979400086721)]
    [InlineData(10u, 990.0)]
    [InlineData(100u, 800.0)]
    [InlineData(300u, 256.8636235841012)]
    public void TheDistancePenaltyGrowsFasterThanTheDistance(uint distance, double expected)
    {
        Assert.Equal(
            expected,
            AiPlanner.ScaleWithDistanceAndTime(1000.0, distance, Mp2ObjectType.None, 0),
            Precision);
    }

    /// <summary>The weights of the reference's <c>getDistanceModifier()</c> switch, group by group.</summary>
    [Theory]
    [InlineData(Mp2ObjectType.Castle, 0.8)]
    [InlineData(Mp2ObjectType.AlchemistLab, 0.9)]
    [InlineData(Mp2ObjectType.Artifact, 0.9)]
    [InlineData(Mp2ObjectType.Hero, 0.9)]
    [InlineData(Mp2ObjectType.Mine, 0.9)]
    [InlineData(Mp2ObjectType.Sawmill, 0.9)]
    [InlineData(Mp2ObjectType.Barrel, 0.95)]
    [InlineData(Mp2ObjectType.Campfire, 0.95)]
    [InlineData(Mp2ObjectType.Flotsam, 0.95)]
    [InlineData(Mp2ObjectType.GenieLamp, 0.95)]
    [InlineData(Mp2ObjectType.Resource, 0.95)]
    [InlineData(Mp2ObjectType.SeaChest, 0.95)]
    [InlineData(Mp2ObjectType.TreasureChest, 0.95)]
    [InlineData(Mp2ObjectType.BlackCat, 1.1)]
    [InlineData(Mp2ObjectType.Buoy, 1.1)]
    [InlineData(Mp2ObjectType.FaerieRing, 1.1)]
    [InlineData(Mp2ObjectType.Fountain, 1.1)]
    [InlineData(Mp2ObjectType.Idol, 1.1)]
    [InlineData(Mp2ObjectType.Mermaid, 1.1)]
    [InlineData(Mp2ObjectType.Temple, 1.1)]
    [InlineData(Mp2ObjectType.None, 1.0)]  // not an action object
    [InlineData(Mp2ObjectType.Event, 1.0)] // an action object, but the reference excludes Event on purpose
    public void TheDistanceModifierTableMatchesTheReference(Mp2ObjectType objectType, double expected)
    {
        Assert.Equal(expected, AiPlanner.GetDistanceModifier(objectType), Precision);
    }

    /// <summary>The same distance is worth more as the game goes on, up to the reference's 0.5 cap.</summary>
    [Theory]
    [InlineData(0u, 800.0)]
    [InlineData(1000u, 824.1181741504607)]
    [InlineData(5000u, 915.051499783199)]
    [InlineData(9000u, 915.051499783199)]
    public void TheOlderTheGameTheLessTheDistanceCosts(uint dayCount, double expected)
    {
        Assert.Equal(
            expected,
            AiPlanner.ScaleWithDistanceAndTime(1000.0, 100, Mp2ObjectType.None, dayCount),
            Precision);
    }

    /// <summary>A value that is already negative loses the same absolute penalty, it does not flip sign.</summary>
    [Fact]
    public void ANegativeValueLosesTheSamePenalty()
    {
        Assert.Equal(
            -10200.0,
            AiPlanner.ScaleWithDistanceAndTime(-10000.0, 100, Mp2ObjectType.None, 0),
            Precision);
    }

    /// <summary>
    /// The weights of the table actually reach the value: at one hundred steps the castle keeps 1347 and the
    /// morale objects keep 1275 out of the same 1500.
    /// </summary>
    [Theory]
    [InlineData(Mp2ObjectType.Castle, 1347.7528010406445)]
    [InlineData(Mp2ObjectType.Artifact, 1324.1181741504608)]
    [InlineData(Mp2ObjectType.Resource, 1312.1162574975594)]
    [InlineData(Mp2ObjectType.Fountain, 1275.446804632595)]
    [InlineData(Mp2ObjectType.None, 1300.0)]
    public void TheWeightOfTheObjectTypeReachesTheValue(Mp2ObjectType objectType, double expected)
    {
        Assert.Equal(
            expected,
            AiPlanner.ScaleWithDistanceAndTime(1500.0, 100, objectType, 0),
            Precision);
    }
}
