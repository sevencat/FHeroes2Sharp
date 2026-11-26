using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using ArmyStack = FHeroes2.Game.Army.Army;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The bridges between a map's metadata and the runtime castle and hero objects
/// (<c>map_format_helper.cpp:75-99</c> and <c>:1749-1803</c>).
/// </summary>
/// <remarks>
/// 用例盯的是两件事：建筑列表怎么并成一本文掩码，以及「默认守军」这条哨兵线——城堡用「类型全负」，英雄用
/// 「类型全 &lt;= 0」，两个哨兵不一样，混了就分不出「作者清空了军队」和「作者没碰过」。
/// </remarks>
public sealed class MapFormatArmyTests
{
    [Fact]
    public void BuildingsOfAListAreOredTogether()
    {
        BuildingType mask = MapFormatArmy.GetBuildings([(uint)BuildingType.Castle, (uint)BuildingType.Tavern]);

        Assert.True((mask & BuildingType.Castle) != 0);
        Assert.True((mask & BuildingType.Tavern) != 0);
        Assert.True((mask & BuildingType.Statue) == 0);
    }

    [Fact]
    public void AnEmptyListIsNoBuildings()
    {
        Assert.Equal(BuildingType.Nothing, MapFormatArmy.GetBuildings([]));
    }

    [Fact]
    public void BuiltAndBannedMasksComeFromTheirOwnLists()
    {
        CastleMetadata metadata = new();
        metadata.BuiltBuildings.Add((uint)BuildingType.Castle);
        metadata.BannedBuildings.Add((uint)BuildingType.Tavern);

        Assert.Equal(BuildingType.Castle, MapFormatArmy.GetBuiltBuildings(metadata));
        Assert.Equal(BuildingType.Tavern, MapFormatArmy.GetBannedBuildings(metadata));
    }

    [Fact]
    public void AFreshCastleHasACustomGuardNotADefaultOne()
    {
        CastleMetadata metadata = new();

        // 参考的初值就是五个 0，也就是「作者配了五支空军队」，不是「按种族配」。
        Assert.False(MapFormatArmy.IsDefaultCastleDefenderArmy(metadata));
    }

    [Fact]
    public void TheDefaultGuardIsEveryTypeNegative()
    {
        CastleMetadata metadata = new();
        metadata.DefenderMonsterType[2] = (int)MonsterType.Peasant;
        metadata.DefenderMonsterCount[2] = 12;

        MapFormatArmy.SetDefaultCastleDefenderArmy(metadata);

        Assert.True(MapFormatArmy.IsDefaultCastleDefenderArmy(metadata));
        Assert.All(metadata.DefenderMonsterType, type => Assert.Equal(-1, type));
        Assert.All(metadata.DefenderMonsterCount, count => Assert.Equal(0, count));
    }

    [Fact]
    public void LoadingTheDefaultGuardLeavesTheArmyAlone()
    {
        CastleMetadata metadata = new();
        MapFormatArmy.SetDefaultCastleDefenderArmy(metadata);

        ArmyStack army = new();
        army.Assign([new Troop(MonsterType.Peasant, 7)]);

        Assert.False(MapFormatArmy.LoadCastleArmy(army, metadata));

        Assert.Equal(MonsterType.Peasant, army.GetTroop(0).Monster);
        Assert.Equal(7u, army.GetTroop(0).Count);
    }

    [Fact]
    public void ACustomGuardFillsTheArmySlotBySlot()
    {
        CastleMetadata metadata = new();
        metadata.DefenderMonsterType[0] = (int)MonsterType.Peasant;
        metadata.DefenderMonsterCount[0] = 20;
        metadata.DefenderMonsterType[3] = (int)MonsterType.Archer;
        metadata.DefenderMonsterCount[3] = 4;

        ArmyStack army = new();

        Assert.True(MapFormatArmy.LoadCastleArmy(army, metadata));

        Assert.Equal(MonsterType.Peasant, army.GetTroop(0).Monster);
        Assert.Equal(20u, army.GetTroop(0).Count);
        Assert.False(army.GetTroop(1).IsValid);
        Assert.Equal(MonsterType.Archer, army.GetTroop(3).Monster);
        Assert.Equal(4u, army.GetTroop(3).Count);
    }

    [Fact]
    public void SavingWritesBothLanes()
    {
        CastleMetadata metadata = new();
        ArmyStack army = new();
        army.Assign([new Troop(MonsterType.Peasant, 3), new Troop(MonsterType.Archer, 9)]);

        MapFormatArmy.SaveCastleArmy(army, metadata);

        Assert.Equal((int)MonsterType.Peasant, metadata.DefenderMonsterType[0]);
        Assert.Equal(3, metadata.DefenderMonsterCount[0]);
        Assert.Equal((int)MonsterType.Archer, metadata.DefenderMonsterType[1]);
        Assert.Equal(9, metadata.DefenderMonsterCount[1]);
        Assert.Equal(0, metadata.DefenderMonsterCount[4]);
    }

    [Fact]
    public void NegativeTypesAndCountsLoadAsEmptySlots()
    {
        CastleMetadata metadata = new();
        metadata.DefenderMonsterType[0] = (int)MonsterType.Peasant;
        metadata.DefenderMonsterCount[0] = 5;
        metadata.DefenderMonsterType[1] = -1;
        metadata.DefenderMonsterCount[1] = -3;

        ArmyStack army = new();

        Assert.True(MapFormatArmy.LoadCastleArmy(army, metadata));

        Assert.Equal(5u, army.GetTroop(0).Count);
        Assert.False(army.GetTroop(1).IsValid);
    }

    [Fact]
    public void AHeroWithoutACustomArmyIsNotLoaded()
    {
        HeroMetadata metadata = new();
        ArmyStack army = new();

        // 英雄的哨兵是「全部 <= 0」：0 是空槽，所以全 0 也算没改过。
        Assert.False(MapFormatArmy.LoadHeroArmy(army, metadata));

        Array.Fill(metadata.ArmyMonsterType, -1);
        Assert.False(MapFormatArmy.LoadHeroArmy(army, metadata));
    }

    [Fact]
    public void AHeroArmyRoundTrips()
    {
        HeroMetadata metadata = new();
        ArmyStack army = new();
        army.Assign([new Troop(MonsterType.Archer, 11)]);

        MapFormatArmy.SaveHeroArmy(army, metadata);

        Assert.Equal((int)MonsterType.Archer, metadata.ArmyMonsterType[0]);
        Assert.Equal(11, metadata.ArmyMonsterCount[0]);

        ArmyStack loaded = new();
        Assert.True(MapFormatArmy.LoadHeroArmy(loaded, metadata));
        Assert.Equal(MonsterType.Archer, loaded.GetTroop(0).Monster);
        Assert.Equal(11u, loaded.GetTroop(0).Count);
    }
}
