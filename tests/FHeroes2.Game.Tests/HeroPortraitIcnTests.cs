using FHeroes2.Assets.Icn;
using FHeroes2.Game.Heroes;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>ICN::getHeroPortraitIcnId</c> (<c>src/fheroes2/agg/icn.cpp:726-882</c>): the seventy-one
/// portraits in a row, the debug hero borrowing Brax's, and <c>UNKNOWN</c> everywhere else.
/// </summary>
public class HeroPortraitIcnTests
{
    [Fact]
    public void TheUnknownHeroHasNoPortrait()
    {
        Assert.Equal(IcnType.Unknown, HeroPortraitIcn.Get(Hero.UnknownId));
    }

    [Theory]
    [InlineData(Hero.LordKilburn, IcnType.Port0000)]
    [InlineData(Hero.Brax, IcnType.Port0059)]
    [InlineData(Hero.Solmyr, IcnType.Port0060)]
    [InlineData(Hero.Jarkonas, IcnType.Port0070)]
    public void AHeroMapsToThePortraitAtTheReferenceCase(int heroId, IcnType expected)
    {
        Assert.Equal(expected, HeroPortraitIcn.Get(heroId));
    }

    [Fact]
    public void TheDebugHeroBorrowsThePortraitOfBrax()
    {
        // 参考：icn.cpp:873-874。
        Assert.Equal(IcnType.Port0059, HeroPortraitIcn.Get(Hero.DebugHero));
    }

    [Fact]
    public void EveryHeroOfTheRowHasItsOwnPortrait()
    {
        // 参考：LORDKILBURN(1) 到 JARKONAS(71) 依次是 PORT0000 到 PORT0070。
        for (int heroId = Hero.LordKilburn; heroId <= Hero.Jarkonas; ++heroId)
        {
            Assert.Equal((IcnType)((int)IcnType.Port0000 + (heroId - Hero.LordKilburn)), HeroPortraitIcn.Get(heroId));
        }
    }

    [Fact]
    public void AnIdWithoutAPortraitFallsBackToUnknown()
    {
        // 参考的 default 分支是 assert：端口选择返回 UNKNOWN 而不是崩。
        Assert.Equal(IcnType.Unknown, HeroPortraitIcn.Get(-1));
        Assert.Equal(IcnType.Unknown, HeroPortraitIcn.Get(Hero.DebugHero + 1));
        Assert.Equal(IcnType.Unknown, HeroPortraitIcn.Get(int.MaxValue));
    }
}
