using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The mine index tables, <c>fheroes2::getMineObjectInfoId()</c> (<c>gui/ui_map_object.cpp:328-366</c>).
/// </summary>
public sealed class RandomMapMinesTests
{
    [Fact]
    public void TheCatalogueHasTheFiftySixEntriesTheTableAssumes()
    {
        // 参考在这里写了一条断言（size() == 56）：表的排法就是按这个数分块的。
        Assert.Equal(RandomMapMines.CatalogueSize, MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureMines).Count);
    }

    [Fact]
    public void OreFollowsTheGroundsBlock()
    {
        Assert.Equal(5, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Ore, MapGround.Grass));
        Assert.Equal(10, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Ore, MapGround.Snow));
        Assert.Equal(35, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Ore, MapGround.Wasteland));

        // 沙滩与水面都借「通用」那一块。
        Assert.Equal(0, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Ore, MapGround.Beach));
        Assert.Equal(0, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Ore, MapGround.Water));
    }

    [Fact]
    public void EachResourceSitsAtItsSlotInsideTheBlock()
    {
        Assert.Equal(5, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Ore, MapGround.Grass));
        Assert.Equal(6, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Sulfur, MapGround.Grass));
        Assert.Equal(7, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Crystal, MapGround.Grass));
        Assert.Equal(8, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Gems, MapGround.Grass));
        Assert.Equal(9, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Gold, MapGround.Grass));
    }

    [Fact]
    public void WoodMercuryAndAbandonedMinesUseTheOtherBlocks()
    {
        // 锯木厂：48 起，草地与沼泽共用一座，雪地是下一座。
        Assert.Equal(48, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Wood, MapGround.Grass));
        Assert.Equal(48, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Wood, MapGround.Swamp));
        Assert.Equal(49, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Wood, MapGround.Snow));

        // 炼金实验室：54 与雪地的 55。
        Assert.Equal(54, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Mercury, MapGround.Grass));
        Assert.Equal(55, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Mercury, MapGround.Snow));

        // 废弃矿：40 起的八座（生成器不产它，参考的映射把它归到这一块）。
        Assert.Equal(41, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.None, MapGround.Grass));
    }

    [Fact]
    public void EveryIndexLandsInsideTheCatalogue()
    {
        foreach (MapGround ground in Enum.GetValues<MapGround>())
        {
            foreach (Mp2ResourceType resource in Enum.GetValues<Mp2ResourceType>())
            {
                int index = RandomMapMines.ObjectInfoIndex(resource, ground);

                Assert.InRange(index, 0, RandomMapMines.CatalogueSize - 1);
            }
        }
    }
}
