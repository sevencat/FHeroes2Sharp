using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The state of the castle details window and what it writes back
/// (<c>editor_castle_details_window.cpp:810-842</c>, <c>:927-991</c>).
/// </summary>
/// <remarks>
/// 用例盯三件事：打开时四个开关从元数据里读到什么、两个勾选框切过去顺手做了什么、写回时两个建筑列表长什么样。
/// </remarks>
public sealed class CastleDetailsStateTests
{
    [Fact]
    public void TheTogglesStartFromTheMetadata()
    {
        CastleMetadata metadata = new();
        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.None, new ArmyStack());

        // 新城堡没有要塞，所以是 town；没禁建、没挑建筑。
        Assert.True(state.IsTown);
        Assert.True(state.AllowCastle);
        Assert.False(state.CustomBuildings);
        Assert.True(state.IsNeutral);
        Assert.False(state.BuildingRestriction);

        // 元数据里那排守军类型是 0 而不是 -1 哨兵，所以「默认守军」一开始没勾上。
        Assert.False(state.DefaultArmy);
    }

    [Fact]
    public void AFortMakesItACastleAndAPlayerMakesItNonNeutral()
    {
        CastleMetadata metadata = new();
        metadata.BuiltBuildings.Add((uint)BuildingType.Castle);
        metadata.BannedBuildings.Add((uint)BuildingType.Castle);

        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.Red, new ArmyStack());

        Assert.False(state.IsTown);

        // 要塞已经在禁建表里（作者先禁后建的样子），开关照读。
        Assert.False(state.AllowCastle);
        Assert.False(state.IsNeutral);

        // 有主的城堡没有「默认守军」这个选项。
        Assert.False(state.DefaultArmy);
    }

    [Fact]
    public void WritingATownMetadataPutsTheTentAndTheBannedFort()
    {
        CastleMetadata metadata = new();
        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.None, new ArmyStack());

        state.AllowCastle = false;
        state.ToggleDefaultArmy();
        state.WriteMetadata();

        Assert.Equal([(uint)BuildingType.Tent], metadata.BuiltBuildings);
        Assert.Equal([(uint)BuildingType.Castle], metadata.BannedBuildings);
        Assert.False(metadata.CustomBuildings);

        // 中立城勾着默认守军，写回的是那串哨兵而不是军队本身。
        Assert.True(MapFormatArmy.IsDefaultCastleDefenderArmy(metadata));
    }

    [Fact]
    public void WritingACastleMetadataKeepsTheFort()
    {
        CastleMetadata metadata = new();
        metadata.BuiltBuildings.Add((uint)BuildingType.Castle);

        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.Red, new ArmyStack());
        state.WriteMetadata();

        Assert.Equal([(uint)BuildingType.Castle], metadata.BuiltBuildings);
        Assert.Empty(metadata.BannedBuildings);
    }

    [Fact]
    public void CustomBuildingsWriteEveryChainAndRestriction()
    {
        CastleMetadata metadata = new();
        metadata.BuiltBuildings.Add((uint)BuildingType.DwellingMonster1);
        metadata.BuiltBuildings.Add((uint)BuildingType.DwellingMonster2);
        metadata.BannedBuildings.Add((uint)BuildingType.DwellingMonster3);

        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.Red, new ArmyStack());

        // 关掉「默认建筑」，格子里读到的链与限制位才会写回去。
        Assert.False(state.ToggleDefaultBuildings());
        Assert.True(state.CustomBuildings);

        state.WriteMetadata();

        Assert.True(metadata.CustomBuildings);
        Assert.Equal(
            [(uint)BuildingType.Tent, (uint)BuildingType.DwellingMonster1, (uint)BuildingType.DwellingMonster2],
            metadata.BuiltBuildings);
        Assert.Equal([(uint)BuildingType.DwellingMonster3], metadata.BannedBuildings);
    }

    [Fact]
    public void SwitchingBackToDefaultBuildingsResetsTheCells()
    {
        CastleMetadata metadata = new();
        metadata.BuiltBuildings.Add((uint)BuildingType.DwellingMonster1);
        metadata.CustomBuildings = true;

        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.Red, new ArmyStack());
        Assert.Equal(0, state.Cells[0].BuiltLevel);

        Assert.True(state.ToggleDefaultBuildings());
        Assert.False(state.CustomBuildings);

        // 切回默认时参考把每一格复位，回不到刚挑的那些建筑。
        Assert.Equal(-1, state.Cells[0].BuiltLevel);
    }

    [Fact]
    public void SwitchingToTheDefaultArmyEmptiesIt()
    {
        CastleMetadata metadata = new();
        CastleDetailsState state = new(metadata, MapRace.Knight, PlayerColor.Red, new ArmyStack());

        state.Army.GetTroop(0).Set(new Troop(MonsterType.Peasant, 10));
        Assert.True(state.Army.GetTroop(0).IsValid);

        Assert.True(state.ToggleDefaultArmy());
        Assert.False(state.Army.GetTroop(0).IsValid);

        // 取消勾选不再清空，作者从空军队开始挑。
        Assert.False(state.ToggleDefaultArmy());
        Assert.False(state.Army.GetTroop(0).IsValid);
    }

    [Fact]
    public void TheResetButtonEmptiesTheArmy()
    {
        CastleDetailsState state = new(new CastleMetadata(), MapRace.Knight, PlayerColor.Red, new ArmyStack());
        state.Army.GetTroop(2).Set(new Troop(MonsterType.Archer, 4));

        state.ResetArmy();

        Assert.False(state.Army.GetTroop(2).IsValid);
    }

    [Fact]
    public void LayingOutTheCellsUsesTheGridAndTheRightPanel()
    {
        CastleDetailsState state = new(new CastleMetadata(), MapRace.Knight, PlayerColor.Red, new ArmyStack());

        state.LayoutCells(100, 100);

        Assert.Equal(105, state.Cells[0].Area.X);
        Assert.Equal(102, state.Cells[0].Area.Y);
        Assert.Equal(393, state.Cells[17].Area.X);
        Assert.Equal(487, state.Cells[17].Area.Y);
        Assert.Equal(571, state.Cells[^1].Area.X);
        Assert.Equal(332, state.Cells[^1].Area.Y);
    }
}
