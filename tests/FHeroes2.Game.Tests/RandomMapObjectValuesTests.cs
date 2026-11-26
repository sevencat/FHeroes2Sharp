using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The gold values the random map generator uses and the guard ladder,
/// <c>objectGoldValue</c> and <c>getMonstersByValue()</c> (<c>map_random_generator_helper.cpp:79-363</c>).
/// </summary>
public sealed class RandomMapObjectValuesTests
{
    [Theory]
    [InlineData(Mp2ObjectType.AlchemistLab, 3500)]
    [InlineData(Mp2ObjectType.Sawmill, 1000)]
    [InlineData(Mp2ObjectType.AbandonedMine, 7500)]
    [InlineData(Mp2ObjectType.Mine, 3500)]
    [InlineData(Mp2ObjectType.TreasureChest, 1500)]
    [InlineData(Mp2ObjectType.Campfire, 1000)]
    [InlineData(Mp2ObjectType.GenieLamp, 7000)]
    [InlineData(Mp2ObjectType.RandomArtifactTreasure, 2000)]
    [InlineData(Mp2ObjectType.RandomArtifactMinor, 5000)]
    [InlineData(Mp2ObjectType.RandomArtifactMajor, 10000)]
    [InlineData(Mp2ObjectType.PeasantHut, 650)]
    [InlineData(Mp2ObjectType.TreeOfKnowledge, 5000)]
    [InlineData(Mp2ObjectType.Xanadu, 10000)]
    [InlineData(Mp2ObjectType.WaterAltar, 5000)]
    [InlineData(Mp2ObjectType.AirAltar, 5000)]
    public void TheTablePricesTheObjectsOfTheReference(Mp2ObjectType objectType, int expected)
    {
        Assert.True(RandomMapObjectValues.HasGoldValue(objectType));
        Assert.Equal(expected, RandomMapObjectValues.GoldValue(objectType));
    }

    [Fact]
    public void TheActionSpellingIsPricedLikeThePlainOne()
    {
        // 物件目录里存的是带动作位的类型；两种写法都要查得到同一行。
        Assert.Equal(
            RandomMapObjectValues.GoldValue(Mp2ObjectType.TreasureChest),
            RandomMapObjectValues.GoldValue(Mp2ObjectType.ActionTreasureChest));
        Assert.True(RandomMapObjectValues.HasGoldValue(Mp2ObjectType.ActionTreasureChest));
        Assert.True(RandomMapObjectValues.HasGoldValue(Mp2ObjectType.ActionMine));
    }

    [Fact]
    public void AnUnpricedObjectIsWorthNothing()
    {
        Assert.False(RandomMapObjectValues.HasGoldValue(Mp2ObjectType.Sign));
        Assert.Equal(0, RandomMapObjectValues.GoldValue(Mp2ObjectType.Sign));
    }

    [Fact]
    public void TheTreasureWindowIsTheOneOfTheReference()
    {
        Assert.Equal(650, RandomMapObjectValues.MinimumTreasureValue);
        Assert.Equal(14000, RandomMapObjectValues.MaximumTreasureGroupValue);
    }

    [Theory]
    [InlineData(13000, MonsterType.RandomMonsterLevel4, 7, MonsterType.Giant, MonsterType.BlackDragon)]
    [InlineData(12999, MonsterType.RandomMonsterLevel4, 9, MonsterType.Archmage, MonsterType.BoneDragon)]
    [InlineData(11500, MonsterType.RandomMonsterLevel4, 9, MonsterType.Archmage, MonsterType.BoneDragon)]
    [InlineData(11499, MonsterType.RandomMonster, 13, MonsterType.Lich, MonsterType.Genie)]
    [InlineData(10000, MonsterType.RandomMonster, 13, MonsterType.Lich, MonsterType.Genie)]
    [InlineData(9999, MonsterType.RandomMonsterLevel3, 15, MonsterType.GreaterDruid, MonsterType.Ghost)]
    [InlineData(8500, MonsterType.RandomMonsterLevel3, 15, MonsterType.GreaterDruid, MonsterType.Ghost)]
    [InlineData(8499, MonsterType.RandomMonsterLevel3, 15, MonsterType.Griffin, MonsterType.MinotaurKing)]
    [InlineData(7000, MonsterType.RandomMonsterLevel3, 15, MonsterType.Griffin, MonsterType.MinotaurKing)]
    [InlineData(6999, MonsterType.RandomMonster, 17, MonsterType.VeteranPikeman, MonsterType.FireElement)]
    [InlineData(5500, MonsterType.RandomMonster, 17, MonsterType.VeteranPikeman, MonsterType.FireElement)]
    public void TheGuardLadderFollowsTheValue(
        int value, MonsterType monster, int allowedCount, MonsterType first, MonsterType last)
    {
        RandomMapMonsterSelection selection = RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Normal, value);

        Assert.Equal(monster, selection.MonsterId);
        Assert.Equal(allowedCount, selection.AllowedMonsters.Count);
        Assert.Equal(first, selection.AllowedMonsters[0]);
        Assert.Equal(last, selection.AllowedMonsters[^1]);
    }

    [Theory]
    [InlineData(5499, MonsterType.RandomMonsterLevel2)]
    [InlineData(3500, MonsterType.RandomMonsterLevel2)]
    [InlineData(3499, MonsterType.RandomMonsterLevel1)]
    [InlineData(2000, MonsterType.RandomMonsterLevel1)]
    public void TheLowBandsOnlyNameTheRandomLevel(int value, MonsterType expected)
    {
        RandomMapMonsterSelection selection = RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Normal, value);

        Assert.Equal(expected, selection.MonsterId);
        Assert.Empty(selection.AllowedMonsters);
    }

    [Fact]
    public void ACheapObjectGetsNoGuardAtAll()
    {
        RandomMapMonsterSelection selection = RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Normal, 1999);

        Assert.True(selection.IsEmpty);
        Assert.Equal(MonsterType.Unknown, selection.MonsterId);
        Assert.Empty(selection.AllowedMonsters);
    }

    [Fact]
    public void ThePlayerStrengthMovesTheValueBeforeTheLookup()
    {
        // 10500 本身落在「10 种里挑」那一档，加了 2500 就升到最强的七种。
        Assert.Equal(
            7,
            RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Deadly, 10500).AllowedMonsters.Count);

        // 10000 加 1500 刚好进第二档（九种，泰坦以下）。
        Assert.Equal(
            9,
            RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Strong, 10000).AllowedMonsters.Count);

        // 5000 减 1500 只剩二级随机兵。
        Assert.Equal(
            MonsterType.RandomMonsterLevel2,
            RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Weak, 5000).MonsterId);

        // 3499 减 1500 低于 2000，什么都不放。
        Assert.True(RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Weak, 3499).IsEmpty);
    }

    [Fact]
    public void NormalStrengthKeepsTheValueItWasGiven()
    {
        Assert.Equal(
            RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Normal, 8500).MonsterId,
            RandomMapMonsterPools.ByValue(RandomMapMonsterStrength.Normal, 8499).MonsterId);
    }
}
