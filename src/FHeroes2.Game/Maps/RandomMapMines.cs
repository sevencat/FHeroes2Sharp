using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Which entry of the <c>ADVENTURE_MINES</c> catalogue stands for a mine of a resource on a ground,
/// <c>fheroes2::getMineObjectInfoId()</c> (<c>gui/ui_map_object.cpp:328-366</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这张表是「表的表」，所以单独放一处：目录里 <c>ADVENTURE_MINES</c> 正好 56 条，排法是**按地面分块、块内按资源**——
/// 八块矿（通用、草地、雪地、沼泽、熔岩、沙漠、泥土、荒野）× 五资源 = 40，接着 8 座废弃矿，再接着 6 座锯木厂，
/// 最后 2 座炼金实验室（普通与雪地）。
/// </para>
/// <para>
/// 参考里有一段<b>注释即契约</b>的断言（<c>size() == 56</c>）与「加了新地面/新矿种就回来改这里」的警告，端口照抄了这两句的意思。
/// 沙滩借用「通用」那一块（矿）与沙漠那一座（锯木厂），水面在参考里是断言，端口退化成通用块。
/// </para>
/// </remarks>
public static class RandomMapMines
{
    /// <summary>How many entries the mine catalogue has, the reference's assertion (<c>:336</c>).</summary>
    public const int CatalogueSize = 56;

    /// <summary>Which mine a resource gets on a ground, <c>getMineObjectInfoId()</c>.</summary>
    /// <param name="resource">The resource the mine produces.</param>
    /// <param name="ground">The ground the mine stands on.</param>
    /// <returns>The index inside <see cref="ObjectGroup.AdventureMines"/>.</returns>
    /// <remarks>
    /// <c>Unknown</c> 资源是「废弃矿」：生成器不产它，但参考的映射把它归到 40 起的八座里。
    /// </remarks>
    public static int ObjectInfoIndex(Mp2ResourceType resource, MapGround ground) => resource switch
    {
        Mp2ResourceType.Ore => MineBlock(ground) * 5,
        Mp2ResourceType.Sulfur => MineBlock(ground) * 5 + 1,
        Mp2ResourceType.Crystal => MineBlock(ground) * 5 + 2,
        Mp2ResourceType.Gems => MineBlock(ground) * 5 + 3,
        Mp2ResourceType.Gold => MineBlock(ground) * 5 + 4,
        Mp2ResourceType.Wood => 48 + SawmillOf(ground),
        Mp2ResourceType.Mercury => ground == MapGround.Snow ? 55 : 54,
        _ => 40 + MineBlock(ground),
    };

    /// <summary>The block of eight mines a ground uses, <c>mineIndexFromGroundType()</c>.</summary>
    /// <param name="ground">The ground.</param>
    /// <returns>The block index, <c>0</c> for the generic block.</returns>
    public static int MineBlock(MapGround ground) => ground switch
    {
        MapGround.Grass => 1,
        MapGround.Snow => 2,
        MapGround.Swamp => 3,
        MapGround.Lava => 4,
        MapGround.Desert => 5,
        MapGround.Dirt => 6,
        MapGround.Wasteland => 7,
        _ => 0,
    };

    /// <summary>The sawmill of a ground, <c>sawmillIndexFromGroundType()</c>.</summary>
    /// <param name="ground">The ground.</param>
    /// <returns>
    /// The index inside the sawmill block; grass and swamp share one picture, the beach borrows the desert one.
    /// </returns>
    public static int SawmillOf(MapGround ground) => ground switch
    {
        MapGround.Snow => 1,
        MapGround.Lava => 2,
        MapGround.Desert or MapGround.Beach => 3,
        MapGround.Dirt => 4,
        MapGround.Wasteland => 5,
        _ => 0,
    };
}
