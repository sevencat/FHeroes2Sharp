using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using FHeroes2.UI.Battle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The pixels of a unit's hit point bar, <see cref="BattleHitPointsBarPainter"/>:
/// <c>Battle::Interface::drawHitPointsBar()</c> (<c>battle_interface.cpp:2202-2228</c>).
/// </summary>
/// <remarks>
/// 计数条摆在 <c>(10,10)</c>、<c>30x12</c>，所以血量条占 <c>(10,22)</c> 起的 <c>30x4</c>：外框 117 号色，
/// 里面 <c>28x2</c> 先红后绿。绿红两色在这里用 200 与 201 两个假索引，免得测试依赖 <c>KB.PAL</c>。
/// </remarks>
public sealed class BattleHitPointsBarPainterTests
{
    private const byte Green = 200;
    private const byte Red = 201;

    private static readonly Point BarPosition = new(10, 10);
    private static readonly Size BarSize = new(30, 12);

    /// <summary>The frame goes all the way round the two inner rows.</summary>
    [Fact]
    public void TheFrameSurroundsTheFill()
    {
        PixelBuffer target = Draw(remainingHitPoints: 100, maximumHitPoints: 100, BarSize);

        Assert.Equal(BattleHitPointsBar.OutlineColor, target[10, 22]);
        Assert.Equal(BattleHitPointsBar.OutlineColor, target[39, 22]);
        Assert.Equal(BattleHitPointsBar.OutlineColor, target[10, 25]);
        Assert.Equal(BattleHitPointsBar.OutlineColor, target[39, 25]);
        Assert.Equal(BattleHitPointsBar.OutlineColor, target[10, 23]);

        // Nothing leaks above or below the bar, nor to the right of the counter.
        Assert.Equal(0, target[10, 21]);
        Assert.Equal(0, target[10, 26]);
        Assert.Equal(0, target[40, 22]);
    }

    /// <summary>Two thirds of the hit points fill two thirds of the inner width with green.</summary>
    [Fact]
    public void TheGreenPartTakesTwoThirdsOfTheBar()
    {
        PixelBuffer target = Draw(remainingHitPoints: 2, maximumHitPoints: 3, BarSize);

        Assert.Equal(Green, target[11, 23]);
        Assert.Equal(Green, target[28, 24]);
        Assert.Equal(Red, target[29, 23]);
        Assert.Equal(Red, target[38, 24]);
    }

    /// <summary>A full stack has no red left between the frame pixels.</summary>
    [Fact]
    public void AFullStackLeavesNoRed()
    {
        PixelBuffer target = Draw(remainingHitPoints: 100, maximumHitPoints: 100, BarSize);

        Assert.Equal(Green, target[38, 23]);
    }

    /// <summary>An empty stack, or a bar without room for a fill, paints only the frame and the red.</summary>
    [Fact]
    public void AnEmptyStackShowsOnlyTheMissingPart()
    {
        PixelBuffer target = Draw(remainingHitPoints: 0, maximumHitPoints: 10, BarSize);

        Assert.Equal(Red, target[11, 23]);
        Assert.Equal(Red, target[38, 24]);
        Assert.Equal(BattleHitPointsBar.OutlineColor, target[10, 22]);
    }

    /// <summary>A two pixel wide bar is all frame, with no inner fill to draw.</summary>
    [Fact]
    public void ATwoPixelWideBarIsOnlyItsFrame()
    {
        PixelBuffer target = Draw(remainingHitPoints: 5, maximumHitPoints: 10, new Size(2, 12));

        Assert.Equal(BattleHitPointsBar.OutlineColor, target[10, 22]);
        Assert.Equal(BattleHitPointsBar.OutlineColor, target[11, 25]);
        Assert.Equal(0, target[12, 22]);
    }

    /// <summary>Draws a bar into a fresh buffer and hands it back.</summary>
    /// <param name="remainingHitPoints">Hit points left on the stack.</param>
    /// <param name="maximumHitPoints">Hit points of one creature.</param>
    /// <param name="barSize">Size of the counter bar the hit point bar hangs under.</param>
    /// <returns>The buffer, 60x40, with the bar drawn at <see cref="BarPosition"/>.</returns>
    private static PixelBuffer Draw(uint remainingHitPoints, uint maximumHitPoints, Size barSize)
    {
        PixelBuffer target = new(60, 40);

        BattleHitPointsBarPainter.Draw(target, BarPosition, barSize, remainingHitPoints, maximumHitPoints, Green, Red);

        return target;
    }
}
