using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="DwellingsBar"/>: the six dwellings, the layout of <c>gui/interface_itemsbar.h</c>, and what a click
/// would open — <c>buildinginfo.cpp:635-717</c>.
/// </summary>
public sealed class DwellingsBarTests
{
    [Fact]
    public void TheSixDwellingsAreInLevelOrder()
    {
        CastleTown castle = new(0, MapRace.Knight);
        DwellingsBar bar = new(castle, new Size(10, 12));

        Assert.Equal(6, bar.Items.Count);

        BuildingType[] expected =
        [
            BuildingType.DwellingMonster1,
            BuildingType.DwellingMonster2,
            BuildingType.DwellingMonster3,
            BuildingType.DwellingMonster4,
            BuildingType.DwellingMonster5,
            BuildingType.DwellingMonster6,
        ];

        for (int level = 0; level < expected.Length; ++level)
        {
            Assert.Equal(expected[level], bar.Items[level].Dwelling);
        }
    }

    [Fact]
    public void TheIconsAreLaidOutRowByRowWithTheGap()
    {
        CastleTown castle = new(0, MapRace.Knight);
        DwellingsBar bar = new(castle, new Size(10, 12))
        {
            InBetweenItemsSize = new Size(2, 3),
            TableSize = new Size(3, 2),
            RenderingOffset = new Point(100, 200),
        };

        // 3 * 10 + 2 * 2 wide, 2 * 12 + 1 * 3 high.
        Assert.Equal(new Rect(100, 200, 34, 27), bar.Area);

        Assert.Equal(new Rect(100, 200, 10, 12), bar.ItemAreas[0]);
        Assert.Equal(new Rect(112, 200, 10, 12), bar.ItemAreas[1]);
        Assert.Equal(new Rect(124, 200, 10, 12), bar.ItemAreas[2]);
        Assert.Equal(new Rect(100, 215, 10, 12), bar.ItemAreas[3]);
        Assert.Equal(new Rect(112, 215, 10, 12), bar.ItemAreas[4]);
        Assert.Equal(new Rect(124, 215, 10, 12), bar.ItemAreas[5]);
    }

    [Fact]
    public void TheItemUnderThePointerIsFoundByRowThenColumn()
    {
        CastleTown castle = new(0, MapRace.Knight);
        DwellingsBar bar = new(castle, new Size(10, 12))
        {
            InBetweenItemsSize = new Size(2, 3),
            TableSize = new Size(3, 2),
            RenderingOffset = new Point(100, 200),
        };

        Assert.Same(bar.Items[0], bar.GetItemAt(new Point(100, 200)));
        Assert.Same(bar.Items[4], bar.GetItemAt(new Point(118, 218)));
        Assert.Same(bar.Items[5], bar.GetItemAt(new Point(133, 226)));

        // The gaps between the icons and everything outside the bar hit nothing.
        Assert.Null(bar.GetItemAt(new Point(110, 200)));
        Assert.Null(bar.GetItemAt(new Point(100, 214)));
        Assert.Null(bar.GetItemAt(new Point(99, 199)));
    }

    [Fact]
    public void ABarWithoutATableHasNoIconsToHit()
    {
        CastleTown castle = new(0, MapRace.Knight);
        DwellingsBar bar = new(castle, new Size(10, 12));

        Assert.Empty(bar.ItemAreas);
        Assert.Null(bar.GetItemAt(new Point(0, 0)));
    }

    [Fact]
    public void TheWellsAddToTheGrowthAndTheLandmarkOnlyToTheFirstLevel()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle);
        DwellingsBar bar = new(castle, new Size(10, 12));

        uint first = bar.GetGrowth(bar.Items[0]);
        uint second = bar.GetGrowth(bar.Items[1]);

        castle.AddBuildings(BuildingType.Well);

        Assert.Equal(first + CastleTown.GrownWell, bar.GetGrowth(bar.Items[0]));
        Assert.Equal(second + CastleTown.GrownWell, bar.GetGrowth(bar.Items[1]));

        castle.AddBuildings(BuildingType.Well2);

        Assert.Equal(first + CastleTown.GrownWell + CastleTown.GrownWel2, bar.GetGrowth(bar.Items[0]));
        Assert.Equal(second + CastleTown.GrownWell, bar.GetGrowth(bar.Items[1]));
    }

    [Fact]
    public void TheClickAsksForACastleFirstThenForTheBuildingThenForCreatures()
    {
        CastleTown castle = new(0, MapRace.Knight);
        DwellingsBar bar = new(castle, new Size(10, 12));
        DwellingItem first = bar.Items[0];

        Assert.Equal(DwellingAction.NeedCastle, bar.GetLeftClickAction(first));

        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle);

        Assert.Equal(DwellingAction.Build, bar.GetLeftClickAction(first));

        castle.AddBuildings(BuildingType.DwellingMonster1);

        Assert.Equal(DwellingAction.Recruit, bar.GetLeftClickAction(first));
        Assert.Equal(DwellingAction.ShowInfo, bar.GetRightClickAction(first));
    }

    [Fact]
    public void AnUnbuiltDwellingHoldsNothing()
    {
        CastleTown castle = new(0, MapRace.Knight);
        DwellingsBar bar = new(castle, new Size(10, 12));

        Assert.False(bar.IsBuilt(bar.Items[0]));
        Assert.Equal(0u, bar.GetMonstersInDwelling(bar.Items[0]));

        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.DwellingMonster1);

        Assert.True(bar.IsBuilt(bar.Items[0]));

        // A dwelling that has just been built has not had its first new week yet.
        Assert.Equal(0u, bar.GetMonstersInDwelling(bar.Items[0]));
    }
}
