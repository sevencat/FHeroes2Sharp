using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Dialogs;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The routing of the town screen, <c>castle_dialog.cpp:196-859</c>: which window a click on a building asks for
/// (<see cref="CastleDialog.ResolveBuilding"/>), the status bar's two lines, and the request bag.
/// </summary>
/// <remarks>
/// 这些都能脱离屏幕测：分派是纯函数，状态栏的两行只读种族与建筑。
/// </remarks>
public sealed class CastleDialogTests
{
    [Fact]
    public void TheDateLineIsTheReferenceOne()
    {
        // castle_dialog.cpp:158-166 的三段式，参考那条注释里写的正是 "Month: 1, Week: 3, Day: 5"。
        Assert.Equal("Month: 1, Week: 3, Day: 5", CastleDialog.GetDateString(1, 3, 5));
    }

    [Fact]
    public void ADwellingSaysWhatItRecruits()
    {
        Assert.Equal("Recruit peasants", CastleDialog.GetStatusLine(MapRace.Knight, BuildingType.DwellingMonster1));
    }

    [Fact]
    public void AnyOtherBuildingSaysItsName()
    {
        // 参考的非住所分支就是 getBuildingName()，端口把这两个函数串起来。
        Assert.Equal(
            CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Tavern),
            CastleDialog.GetStatusLine(MapRace.Knight, BuildingType.Tavern));
    }

    [Fact]
    public void AClickedDwellingAsksForTheHighestBuiltOne()
    {
        // 第二座住所有升级时，点基础住所也开升级后的那一档（Castle.GetActualDwelling，castle.cpp:1931-1958）。
        Castle castle = Town(BuildingType.DwellingMonster2 | BuildingType.DwellingUpgrade2);

        CastleDialogRequest? window = CastleDialog.ResolveBuilding(castle, BuildingType.DwellingMonster2, out DialogPopup? popup);

        Assert.Null(popup);
        Assert.Equal(CastleDialogWindow.Dwelling, window!.Value.Window);
        Assert.Equal(BuildingType.DwellingUpgrade2, window.Value.Building);
    }

    [Fact]
    public void AClickedMageGuildAsksForTheMageGuildWindow()
    {
        Castle castle = Town(BuildingType.MageGuild1);

        CastleDialogRequest? window = CastleDialog.ResolveBuilding(castle, BuildingType.MageGuild1, out DialogPopup? popup);

        Assert.Null(popup);
        Assert.Equal(CastleDialogWindow.MageGuild, window!.Value.Window);
        Assert.Equal(BuildingType.MageGuild1, window.Value.Building);
    }

    [Theory]
    [InlineData(BuildingType.Tavern, CastleDialogWindow.Tavern)]
    [InlineData(BuildingType.ThievesGuild, CastleDialogWindow.ThievesGuild)]
    [InlineData(BuildingType.Well, CastleDialogWindow.Well)]
    [InlineData(BuildingType.Marketplace, CastleDialogWindow.Marketplace)]
    [InlineData(BuildingType.Shipyard, CastleDialogWindow.Shipyard)]
    [InlineData(BuildingType.Castle, CastleDialogWindow.Construction)]
    public void TheSingleWindowBuildingsNameTheirWindow(BuildingType building, CastleDialogWindow expected)
    {
        Castle castle = Town(building);

        CastleDialogRequest? window = CastleDialog.ResolveBuilding(castle, building, out DialogPopup? popup);

        Assert.Null(popup);
        Assert.Equal(expected, window!.Value.Window);
    }

    [Fact]
    public void TheEmptyCasteSlotAsksForThePurchaseWindow()
    {
        // 没建城堡的城（BUILD_TENT）点下去是买城（castle_dialog.cpp:740-745）。
        Castle castle = Town(BuildingType.Tent);

        CastleDialogRequest? window = CastleDialog.ResolveBuilding(castle, BuildingType.Tent, out DialogPopup? popup);

        Assert.Null(popup);
        Assert.Equal(CastleDialogWindow.BuildCastle, window!.Value.Window);
        Assert.Equal(BuildingType.Tent, window.Value.Building);
    }

    [Fact]
    public void ACaptainOnlyGetsItsDescription()
    {
        Castle castle = Town(BuildingType.Captain);

        CastleDialogRequest? window = CastleDialog.ResolveBuilding(castle, BuildingType.Captain, out DialogPopup? popup);

        Assert.Null(window);
        Assert.NotNull(popup);
        Assert.Equal(CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.Captain), popup.Value.Header);
    }

    [Fact]
    public void AnUnknownBuildingAsksForNothing()
    {
        Castle castle = Town(BuildingType.Nothing);

        Assert.Null(CastleDialog.ResolveBuilding(castle, BuildingType.Nothing, out DialogPopup? popup));
        Assert.Null(popup);
    }

    [Fact]
    public void TheRequestBagStartsEmptyAndClears()
    {
        var requests = new CastleDialogRequests();

        Assert.True(requests.IsEmpty);

        requests.Window = new CastleDialogRequest(CastleDialogWindow.Income);
        Assert.False(requests.IsEmpty);

        requests.Popup = new DialogPopup("Exit", "Exit this menu.");
        requests.Army = new FHeroes2.UI.Widgets.ArmyBarEvent(Handled: true);
        Assert.False(requests.IsEmpty);
    }

    [Fact]
    public void TheDefaultShortcutsPressNothing()
    {
        CastleDialogHotKeys hotKeys = default;

        Assert.False(hotKeys.CloseWindow);
        Assert.False(hotKeys.PreviousCastle);
        Assert.False(hotKeys.NextCastle);
        Assert.False(hotKeys.SwapArmies);
        Assert.False(hotKeys.MergeWithHero);
        Assert.False(hotKeys.MergeWithGarrison);
        Assert.Equal(BuildingType.Nothing, hotKeys.Building);
    }

    private static Castle Town(BuildingType buildings)
    {
        var castle = new Castle(0, MapRace.Knight);

        castle.AddBuildings(buildings);

        return castle;
    }
}
