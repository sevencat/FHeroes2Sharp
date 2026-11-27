using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the reverse index of <see cref="MapObjectCatalog"/>: the <c>( group, index )</c> address the
/// <c>.fh2m</c> format stores for a tile, rebuilt from the ICN image of the tile.
/// </summary>
/// <remarks>
/// 参考建的是 <c>objectInfoByIcn</c>（部件），端口的 <see cref="MapObjectCatalog.TryGetObjectIndex"/> 多带一个组内下标；
/// 两者同一趟构建、同一条「先到先得」规则，所以这里同时钉住「组还找得到」与「下标对得上」。
/// </remarks>
public sealed class MapObjectCatalogIndexTests
{
    [Fact]
    public void TheMonsterGroupHasEveryCreatureAndTheFiveRandomOnes()
    {
        int count = MapObjectCatalog.GetObjectCount(ObjectGroup.Monsters);

        // Peasant..WaterElement 是 66 个，再加五个随机怪物。
        Assert.Equal(71, count);
    }

    [Theory]
    [InlineData(MonsterType.Peasant, 0u)]
    [InlineData(MonsterType.Archer, 1u)]
    [InlineData(MonsterType.WaterElement, 65u)]
    [InlineData(MonsterType.RandomMonster, 66u)]
    [InlineData(MonsterType.RandomMonsterLevel4, 70u)]
    public void AMonsterIsAddressedByItsFrameIndex(MonsterType monster, uint expected)
    {
        // 参考的 populateMonsters 是纯循环：icnIndex = monsterId - 1，组内下标就等于那个帧号。
        Assert.True(MapObjectCatalog.TryGetObjectIndex(Mp2ObjectIcnType.Mons32, (uint)monster - 1, out
            MapObjectIndex index));

        Assert.Equal(ObjectGroup.Monsters, index.Group);
        Assert.Equal(expected, index.Index);
    }

    [Fact]
    public void TheHeroGroupHasSixColoursTimesSevenRaces()
    {
        Assert.Equal(42, MapObjectCatalog.GetObjectCount(ObjectGroup.KingdomHeroes));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(7u)]
    [InlineData(41u)]
    public void AHeroIsAddressedByItsFrameIndex(uint frame)
    {
        // 参考的 populateKingdomHeroes 也是纯循环：icnIndex = color * 7 + race，下标与帧号相同。
        Assert.True(MapObjectCatalog.TryGetObjectIndex(Mp2ObjectIcnType.MiniHero, frame, out MapObjectIndex index));

        Assert.Equal(ObjectGroup.KingdomHeroes, index.Group);
        Assert.Equal(frame, index.Index);
    }

    [Fact]
    public void TheAddressAgreesWithTheGroupOnlyLookup()
    {
        // 两个反查表是同一趟建的，所以「只问组」与「问组 + 下标」必须给同一个组。
        Assert.True(MapObjectCatalog.TryGetObjectIndex(Mp2ObjectIcnType.Mons32, 3, out MapObjectIndex index));
        Assert.Equal(MapObjectCatalog.GetObjectGroupByIcn(Mp2ObjectIcnType.Mons32, 3), index.Group);
    }

    [Fact]
    public void AnImageNobodyUsesHasNoAddress()
    {
        Assert.False(MapObjectCatalog.TryGetObjectIndex(Mp2ObjectIcnType.Mons32, 999, out MapObjectIndex index));
        Assert.Equal(ObjectGroup.None, index.Group);
        Assert.Equal(0u, index.Index);

        Assert.False(MapObjectCatalog.TryGetObjectIndex(Mp2ObjectIcnType.Unknown, 0, out _));
    }

    [Fact]
    public void EveryObjectOfTheTwoLoopedGroupsIsReachableFromItsFrame()
    {
        // 两组都是「一帧一个对象」，所以整张表必须能被逐项反查回来，一条不落。
        for (int group = 0; group < (int)ObjectGroup.GroupCount; ++group)
        {
            ObjectGroup id = (ObjectGroup)group;
            IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(id);

            for (int objectIndex = 0; objectIndex < objects.Count; ++objectIndex)
            {
                ObjectInfo info = objects[objectIndex];

                if (info.GroundLevelParts.Count != 1)
                {
                    continue;
                }

                ObjectPartInfo part = info.GroundLevelParts[0];

                Assert.True(
                    MapObjectCatalog.TryGetObjectIndex(part.IcnType, part.IcnIndex, out MapObjectIndex found),
                    $"{id}[{objectIndex}] uses {part.IcnType}/{part.IcnIndex} and cannot be found again");

                // 先到先得：同一帧被两个对象用到时，反查回的是先注册的那一个，组可能不同但对象必须存在。
                Assert.True(
                    MapObjectCatalog.GetObjectInfo(found.Group, (int)found.Index).GroundLevelParts.Count > 0);
            }
        }
    }
}
