using FHeroes2.Core.Geometry;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="DimensionDoorArea"/>, the square a Dimension Door may land in
/// (<c>game_interface.cpp:206-217</c>).
/// </summary>
/// <remarks>
/// The square is pure geometry: it needs no map and no mouse, so the tests pin the four steps of the reference —
/// centre it on the hero, take <c>(distance + 1)</c> tiles, clip it to the visible map, and keep it inside the
/// border unless the interface is hidden.
/// </remarks>
public sealed class DimensionDoorAreaTests
{
    private static readonly Rect MapArea = new(16, 16, 480, 480);

    [Fact]
    public void TheSquareIsCentredOnTheHeroAndOneTileWiderThanTheDistance()
    {
        // distance 4 → 32 * (4 / 2) = 64 pixels either way, side 32 * 5 = 160.
        Rect area = DimensionDoorArea.Compute(new Point(320, 240), MapArea, 4, isHideInterface: false);

        Assert.Equal(new Rect(256, 176, 160, 160), area);
    }

    [Fact]
    public void AnOddDistanceStillHalvesDown()
    {
        // distance 3 → 32 * (3 / 2) = 32 pixels either way, side 32 * 4 = 128.
        Rect area = DimensionDoorArea.Compute(new Point(320, 240), MapArea, 3, isHideInterface: false);

        Assert.Equal(new Rect(288, 208, 128, 128), area);
    }

    [Fact]
    public void TheSquareIsClippedToTheVisibleMap()
    {
        Rect area = DimensionDoorArea.Compute(new Point(320, 240), new Rect(16, 16, 100, 90), 4, isHideInterface: false);

        Assert.Equal(100, area.Width);
        Assert.Equal(90, area.Height);
    }

    [Fact]
    public void TheSquareStaysInsideTheBorderWhileTheInterfaceIsShown()
    {
        Rect area = DimensionDoorArea.Compute(new Point(40, 40), MapArea, 4, isHideInterface: false);

        Assert.Equal(16, area.X);
        Assert.Equal(16, area.Y);
    }

    [Fact]
    public void TheSquareMayRunUnderThePanelsWhileTheInterfaceIsHidden()
    {
        Rect area = DimensionDoorArea.Compute(new Point(40, 40), MapArea, 4, isHideInterface: true);

        Assert.Equal(-24, area.X);
        Assert.Equal(-24, area.Y);
    }

    [Fact]
    public void AZeroDistanceGivesTheSingleTileAroundTheHero()
    {
        // distance 0 → offset 0, side 32: the hero's own tile.
        Rect area = DimensionDoorArea.Compute(new Point(320, 240), MapArea, 0, isHideInterface: false);

        Assert.Equal(new Rect(320, 240, 32, 32), area);
    }
}
