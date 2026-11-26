using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The nineteen slots of the castle details window
/// (<c>editor_castle_details_window.cpp:729-768</c>).
/// </summary>
/// <remarks>
/// 用例只验证布局与那几处「读元数据得出的显示开关」：格子本身的行为在 <c>CastleBuildingCellTests</c> 里。
/// </remarks>
public sealed class CastleDetailsGridTests
{
    [Fact]
    public void ThereAreNineteenSlotsAndTheLastOneIsTheCaptain()
    {
        IReadOnlyList<BuildingType> slots = CastleDetailsGrid.GetSlotBuildings(MapRace.Knight);

        Assert.Equal(CastleDetailsGrid.SlotCount, slots.Count);
        Assert.Equal(BuildingType.DwellingMonster1, slots[0]);
        Assert.Equal(BuildingType.MageGuild1, slots[6]);
        Assert.Equal(BuildingType.Captain, slots[^1]);
    }

    [Fact]
    public void TheNecromancerGetsAShrineWhereTheOthersGetATavern()
    {
        Assert.Equal(BuildingType.Tavern, CastleDetailsGrid.GetSlotBuildings(MapRace.Knight)[7]);
        Assert.Equal(BuildingType.Shrine, CastleDetailsGrid.GetSlotBuildings(MapRace.Necromancer)[7]);
    }

    [Fact]
    public void EveryGridSlotSitsOnItsRowAndColumn()
    {
        // 行距 75，第一行与第四行之后多 5 像素。
        Assert.Equal(new Point(105, 102), CastleDetailsGrid.GetPosition(0, 100, 100));
        Assert.Equal(new Point(249, 102), CastleDetailsGrid.GetPosition(1, 100, 100));
        Assert.Equal(new Point(393, 102), CastleDetailsGrid.GetPosition(2, 100, 100));
        Assert.Equal(new Point(105, 177), CastleDetailsGrid.GetPosition(3, 100, 100));
        Assert.Equal(new Point(105, 257), CastleDetailsGrid.GetPosition(6, 100, 100));
        Assert.Equal(new Point(249, 332), CastleDetailsGrid.GetPosition(10, 100, 100));
        Assert.Equal(new Point(393, 407), CastleDetailsGrid.GetPosition(14, 100, 100));
        Assert.Equal(new Point(105, 487), CastleDetailsGrid.GetPosition(15, 100, 100));
        Assert.Equal(new Point(393, 487), CastleDetailsGrid.GetPosition(17, 100, 100));
    }

    [Fact]
    public void SlotsOutsideTheGridAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CastleDetailsGrid.GetPosition(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CastleDetailsGrid.GetPosition(CastleDetailsGrid.GridSlotCount, 0, 0));
    }

    [Fact]
    public void TheCaptainSitsOnTheRightPanel()
    {
        Assert.Equal(new Point(571, 332), CastleDetailsGrid.GetCaptainPosition(100, 100));
    }

    [Fact]
    public void TheCellsReadTheTwoMasksOfTheCastle()
    {
        CastleBuildingCell[] cells = CastleDetailsGrid.CreateCells(MapRace.Knight, BuildingType.DwellingMonster1, BuildingType.Shipyard);

        Assert.Equal(CastleDetailsGrid.SlotCount, cells.Length);
        Assert.Equal(0, cells[0].BuiltLevel);

        // 船坞在第九格（索引 9），链长 1，所以禁用线就停在第一级上。
        Assert.Equal(0, cells[9].RestrictedLevel);
        Assert.Equal(BuildingType.Shipyard, cells[9].RestrictedBuilding);
        Assert.Equal(-1, cells[1].BuiltLevel);
    }

    [Fact]
    public void ATownIsACastleWithoutAFort()
    {
        CastleMetadata town = new();
        CastleMetadata castle = new();
        castle.BuiltBuildings.Add((uint)BuildingType.Castle);

        Assert.True(CastleDetailsGrid.IsTown(town));
        Assert.False(CastleDetailsGrid.IsTown(castle));
    }

    [Fact]
    public void TheDefaultBuildingSignFollowsCustomBuildings()
    {
        CastleMetadata metadata = new();
        Assert.True(CastleDetailsGrid.ShowsDefaultBuildings(metadata));

        metadata.CustomBuildings = true;
        Assert.False(CastleDetailsGrid.ShowsDefaultBuildings(metadata));
    }

    [Fact]
    public void BanningTheFortForbidsBuildingIt()
    {
        CastleMetadata metadata = new();
        Assert.True(CastleDetailsGrid.IsCastleAllowed(metadata));

        metadata.BannedBuildings.Add((uint)BuildingType.Castle);
        Assert.False(CastleDetailsGrid.IsCastleAllowed(metadata));
    }
}
