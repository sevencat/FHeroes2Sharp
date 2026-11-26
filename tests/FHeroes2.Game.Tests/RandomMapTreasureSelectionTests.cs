using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The treasure pool and the type-to-(group, index) conversion of the random map generator,
/// <c>getRandomTreasure()</c> and <c>convertMP2ToObjectInfo()</c> (<c>map_random_generator_helper.cpp:365-406</c>).
/// </summary>
public sealed class RandomMapTreasureSelectionTests
{
    [Fact]
    public void ThePoolIsTheOneOfTheReference()
    {
        Assert.Equal(
            [
                Mp2ObjectType.TreasureChest,
                Mp2ObjectType.TreasureChest,
                Mp2ObjectType.RandomResource,
                Mp2ObjectType.RandomResource,
                Mp2ObjectType.Campfire,
                Mp2ObjectType.RandomArtifactTreasure,
                Mp2ObjectType.RandomArtifactMinor,
                Mp2ObjectType.RandomArtifactMajor,
            ],
            RandomMapTreasureSelection.Candidates);
    }

    [Fact]
    public void EveryCandidateConvertsBackToItself()
    {
        foreach (Mp2ObjectType candidate in RandomMapTreasureSelection.Candidates.Distinct())
        {
            Assert.True(
                RandomMapTreasureSelection.TryConvert(candidate, out ObjectGroup group, out int index),
                $"{candidate} has no catalogue entry");

            Assert.Equal(
                candidate,
                Mp2ObjectTypes.NonActionType(MapObjectCatalog.GetObjectInfo(group, index).ObjectType));

            // 带动作位的写法也要查得到：目录里存的正是这一种。
            Assert.True(RandomMapTreasureSelection.TryConvert(
                (Mp2ObjectType)((int)candidate + (int)Mp2ObjectType.ActionObjectType),
                out ObjectGroup sameGroup,
                out int sameIndex));
            Assert.Equal(group, sameGroup);
            Assert.Equal(index, sameIndex);
        }
    }

    [Fact]
    public void AnObjectOutsideTheSixGroupsIsNotConverted()
    {
        Assert.False(RandomMapTreasureSelection.TryConvert(Mp2ObjectType.Sign, out _, out int index));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void OnlyAffordableTreasuresCanBePicked()
    {
        // 650 是资源堆的估价，池子里只有它买得起；连抽二十次都该是同一个物件。
        Pcg32 random = new(99);
        RandomMapObjectPlacement? first = null;

        for (int draw = 0; draw < 20; ++draw)
        {
            Assert.True(RandomMapTreasureSelection.TryPick(
                RandomMapObjectValues.MinimumTreasureValue, random, out RandomMapObjectPlacement placement));

            first ??= placement;

            Assert.Equal(first.Value, placement);
        }

        Assert.Equal(
            Mp2ObjectType.RandomResource,
            Mp2ObjectTypes.NonActionType(MapObjectCatalog.GetObjectInfo(first!.Value.Group, first.Value.ObjectIndex).ObjectType));
    }

    [Fact]
    public void ANothingAffordableLimitPicksNothing()
    {
        Assert.False(RandomMapTreasureSelection.TryPick(0, new Pcg32(5), out _));
        Assert.False(RandomMapTreasureSelection.TryPick(
            RandomMapObjectValues.MinimumTreasureValue - 1, new Pcg32(5), out _));
    }

    [Fact]
    public void ACheapLimitStillAdmitsTheChestAndTheCampfire()
    {
        // 一千金币能买到宝箱与篝火（都是一千），资源堆更便宜，三档随机宝物买不起。
        Pcg32 random = new(11);
        HashSet<Mp2ObjectType> seen = [];

        for (int draw = 0; draw < 200; ++draw)
        {
            Assert.True(RandomMapTreasureSelection.TryPick(1000, random, out RandomMapObjectPlacement placement));

            seen.Add(Mp2ObjectTypes.NonActionType(
                MapObjectCatalog.GetObjectInfo(placement.Group, placement.ObjectIndex).ObjectType));
        }

        Assert.Subset(
            new HashSet<Mp2ObjectType>
            {
                Mp2ObjectType.TreasureChest,
                Mp2ObjectType.RandomResource,
                Mp2ObjectType.Campfire,
            },
            seen);

        Assert.Contains(Mp2ObjectType.Campfire, seen);
        Assert.DoesNotContain(Mp2ObjectType.RandomArtifactMinor, seen);
    }

    [Fact]
    public void TheSameSeedPicksTheSameTreasure()
    {
        Assert.True(RandomMapTreasureSelection.TryPick(2000, new Pcg32(1234), out RandomMapObjectPlacement first));
        Assert.True(RandomMapTreasureSelection.TryPick(2000, new Pcg32(1234), out RandomMapObjectPlacement second));

        Assert.Equal(first, second);
    }
}
