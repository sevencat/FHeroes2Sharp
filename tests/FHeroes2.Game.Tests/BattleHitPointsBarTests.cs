using FHeroes2.Game.Battle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two fills of a unit's hit point bar, <see cref="BattleHitPointsBar"/>:
/// <c>Battle::Interface::drawHitPointsBar()</c> (<c>battle_interface.cpp:2202-2228</c>).
/// </summary>
public sealed class BattleHitPointsBarTests
{
    /// <summary>The bar is four pixels tall with a one pixel frame, <c>hitPointsBarHeight = 4</c>.</summary>
    [Fact]
    public void TheBarIsFourPixelsTallWithAOnePixelFrame()
    {
        Assert.Equal(4, BattleHitPointsBar.Height);
        Assert.Equal(1, BattleHitPointsBar.Frame);
        Assert.Equal(2, BattleHitPointsBar.InnerHeight);
        Assert.Equal(117, BattleHitPointsBar.OutlineColor);
    }

    /// <summary>The inner width drops the two frame pixels (<c>battle_interface.cpp:2214</c>).</summary>
    [Fact]
    public void TheInnerWidthDropsBothFramePixels()
    {
        Assert.Equal(28, BattleHitPointsBar.GetInnerWidth(30));
        Assert.Equal(0, BattleHitPointsBar.GetInnerWidth(2));
        Assert.Equal(-1, BattleHitPointsBar.GetInnerWidth(1));
    }

    /// <summary>A stack at full health fills the whole inner width.</summary>
    [Fact]
    public void AFullStackFillsTheWholeBar()
    {
        Assert.Equal(28, BattleHitPointsBar.GetRemainingWidth(30, 100, 100));
    }

    /// <summary>Two thirds of the hit points take two thirds of the inner width, rounded down.</summary>
    [Fact]
    public void TwoThirdsOfTheHitPointsTakeTwoThirdsOfTheBar()
    {
        Assert.Equal(18, BattleHitPointsBar.GetRemainingWidth(30, 2, 3));
    }

    /// <summary>One hit point left still paints a pixel, the reference's <c>clamp</c> lower bound (<c>:2217</c>).</summary>
    [Fact]
    public void TheLastHitPointStillPaintsAPixel()
    {
        Assert.Equal(1, BattleHitPointsBar.GetRemainingWidth(30, 1, 500));
    }

    /// <summary>More hit points than the maximum cannot overrun the inner width (<c>:2217</c> upper bound).</summary>
    [Fact]
    public void HitPointsAboveTheMaximumDoNotOverrunTheBar()
    {
        Assert.Equal(28, BattleHitPointsBar.GetRemainingWidth(30, 200, 100));
    }

    /// <summary>A bar too narrow for a fill has no green part at all.</summary>
    [Fact]
    public void ABarWithoutRoomHasNoRemainingPart()
    {
        Assert.Equal(0, BattleHitPointsBar.GetRemainingWidth(2, 5, 10));
    }

    /// <summary>A dead stack (or an unknown maximum) paints nothing, where the reference asserts.</summary>
    [Fact]
    public void AnEmptyStackHasNoRemainingPart()
    {
        Assert.Equal(0, BattleHitPointsBar.GetRemainingWidth(30, 0, 10));
        Assert.Equal(0, BattleHitPointsBar.GetRemainingWidth(30, 5, 0));
    }

    /// <summary>The colours are the reference's <c>GetColorId( 0, 200, 0 )</c> and <c>GetColorId( 200, 0, 0 )</c>.</summary>
    [Fact]
    public void TheColoursAreTheReferenceOnes()
    {
        Assert.Equal((0, 200, 0),
            (BattleHitPointsBar.RemainingRed, BattleHitPointsBar.RemainingGreen, BattleHitPointsBar.RemainingBlue));
        Assert.Equal((200, 0, 0),
            (BattleHitPointsBar.MissingRed, BattleHitPointsBar.MissingGreen, BattleHitPointsBar.MissingBlue));
    }
}
