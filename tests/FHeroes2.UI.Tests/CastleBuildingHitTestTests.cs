using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the town screen's building hit table, <c>castle_dialog.cpp:413-435</c>.
/// </summary>
public sealed class CastleBuildingHitTestTests
{
    [Fact]
    public void APointOverNothingFindsNothing()
    {
        Castle castle = new(0, MapRace.Knight);

        Assert.Equal(BuildingType.Nothing, CastleBuildingHitTest.Find(castle, new Point(0, 0), new Point(1, 1)));
    }

    [Fact]
    public void AnUnbuiltBuildingIsNotHitEvenThoughItsAreaExists()
    {
        Castle castle = new(0, MapRace.Knight);
        Rect area = FirstArea(castle.Race, BuildingType.Well);

        // 面积表是静态的：没建也有矩形，参考先跳过没建的。
        Assert.False(castle.IsBuild(BuildingType.Well));
        Assert.Equal(BuildingType.Nothing, CastleBuildingHitTest.Find(castle, new Point(0, 0), Center(area)));
    }

    [Fact]
    public void ABuiltBuildingIsHitInsideItsArea()
    {
        Castle castle = CastleWith(BuildingType.Castle);
        Rect area = FirstArea(castle.Race, BuildingType.Castle);

        Assert.Equal(BuildingType.Castle, CastleBuildingHitTest.Find(castle, new Point(0, 0), Center(area)));
    }

    [Fact]
    public void TheOffsetShiftsTheAreas()
    {
        Castle castle = CastleWith(BuildingType.Castle);
        Rect area = FirstArea(castle.Race, BuildingType.Castle);
        Point offset = new(20, 10);

        Assert.Equal(
            BuildingType.Castle,
            CastleBuildingHitTest.Find(castle, offset, new Point(area.X + offset.X + 1, area.Y + offset.Y + 1)));

        // 没有偏移时同一个点落在图外。
        Assert.Equal(
            BuildingType.Nothing,
            CastleBuildingHitTest.Find(castle, offset, new Point(area.X + 1, area.Y + 1)));
    }

    [Fact]
    public void TheTopmostBuildingWins()
    {
        // 井与魔法塔的位置都在面积表里；两者都建好之后，魔法塔在队列里更靠后，命中它。
        Castle castle = CastleWith(BuildingType.Castle, BuildingType.Well, BuildingType.MageGuild1);

        Rect well = FirstArea(castle.Race, BuildingType.Well);
        Rect guild = FirstArea(castle.Race, BuildingType.MageGuild1);

        if (!well.Intersect(guild).IsEmpty)
        {
            Point shared = Center(well.Intersect(guild));

            Assert.Equal(BuildingType.MageGuild1, CastleBuildingHitTest.Find(castle, new Point(0, 0), shared));
        }

        Assert.Equal(BuildingType.MageGuild1, CastleBuildingHitTest.Find(castle, new Point(0, 0), Center(guild)));
    }

    private static Rect FirstArea(MapRace race, BuildingType building)
    {
        IReadOnlyList<Rect> areas = CastleBuildingInfo.GetArea(race, building);

        Assert.NotEmpty(areas);

        return areas[0];
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static Castle CastleWith(params BuildingType[] buildings)
    {
        Castle castle = new(0, MapRace.Knight);
        Funds treasury = Rich();

        foreach (BuildingType building in buildings)
        {
            castle.ActionNewDay();

            Assert.True(castle.BuyBuilding(building, ref treasury), $"{building} should be buyable");
        }

        return castle;
    }

    private static Funds Rich() => new()
    {
        Wood = 999_999,
        Mercury = 999_999,
        Ore = 999_999,
        Sulfur = 999_999,
        Crystal = 999_999,
        Gems = 999_999,
        Gold = 999_999,
    };
}
