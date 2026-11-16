using FHeroes2.Game.Army;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the size brackets of <c>army.cpp:74-240</c>: <c>getArmySize()</c>, the numeric
/// <c>troopSizeNumbers</c> and the verbal <c>Army::SizeString()</c>.
/// </summary>
public sealed class ArmySizeTests
{
    [Theory]
    [InlineData(0u, ArmySize.Few)]
    [InlineData(1u, ArmySize.Few)]
    [InlineData(4u, ArmySize.Few)]
    [InlineData(5u, ArmySize.Several)]
    [InlineData(9u, ArmySize.Several)]
    [InlineData(10u, ArmySize.Pack)]
    [InlineData(19u, ArmySize.Pack)]
    [InlineData(20u, ArmySize.Lots)]
    [InlineData(49u, ArmySize.Lots)]
    [InlineData(50u, ArmySize.Horde)]
    [InlineData(99u, ArmySize.Horde)]
    [InlineData(100u, ArmySize.Throng)]
    [InlineData(249u, ArmySize.Throng)]
    [InlineData(250u, ArmySize.Swarm)]
    [InlineData(499u, ArmySize.Swarm)]
    [InlineData(500u, ArmySize.Zounds)]
    [InlineData(999u, ArmySize.Zounds)]
    [InlineData(1000u, ArmySize.Legion)]
    [InlineData(100000u, ArmySize.Legion)]
    public void TheBracketsStartAtTheirLowerBound(uint count, ArmySize size)
    {
        Assert.Equal(size, ArmyStack.GetArmySize(count));
    }

    [Theory]
    [InlineData(0u, "1-4")]
    [InlineData(4u, "1-4")]
    [InlineData(5u, "5-9")]
    [InlineData(10u, "10-19")]
    [InlineData(20u, "20-49")]
    [InlineData(50u, "50-99")]
    [InlineData(100u, "100-249")]
    [InlineData(250u, "250-499")]
    [InlineData(500u, "500-999")]
    [InlineData(1000u, "1000+")]
    public void TheNumericWordingIsTheReferenceRange(uint count, string range)
    {
        Assert.Equal(range, ArmyStack.SizeRangeString(count));
    }

    [Theory]
    [InlineData(1u, "Few")]
    [InlineData(9u, "Several")]
    [InlineData(19u, "Pack")]
    [InlineData(49u, "Lots")]
    [InlineData(99u, "Horde")]
    [InlineData(249u, "Throng")]
    [InlineData(499u, "Swarm")]
    [InlineData(999u, "Zounds")]
    [InlineData(1000u, "Legion")]
    public void TheVerbalWordingIsOneWord(uint count, string word)
    {
        Assert.Equal(word, ArmyStack.SizeString(count));
    }
}
