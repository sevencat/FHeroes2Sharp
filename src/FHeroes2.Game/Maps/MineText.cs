using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The name of a mine by the resource it yields, <c>Maps::GetMineName()</c> (<c>maps.cpp:226-246</c>).
/// </summary>
/// <remarks>
/// <para>
/// 七种资源里只有五种有矿（水银与木材没有）：参考对它们返回通用的 "Mine"，不是「××矿」。所以这个函数不是
/// 「资源名 + Mine」的拼接，得一张表。
/// </para>
/// <para>
/// 归在 <c>Maps</c> 而不是 <c>ResourceText</c>：参考把它放在 <c>maps.cpp</c>，而且调用方全是地图对象的提示框
/// （矿的名字出现在「占领了××」里）。
/// </para>
/// </remarks>
public static class MineText
{
    /// <summary>The name of the mine that yields a resource.</summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The mine's name, or the generic <c>"Mine"</c> for the resources without one.</returns>
    public static string GetMineName(int resource) => resource switch
    {
        Resource.Ore => "Ore Mine",
        Resource.Sulfur => "Sulfur Mine",
        Resource.Crystal => "Crystal Mine",
        Resource.Gems => "Gems Mine",
        Resource.Gold => "Gold Mine",
        _ => "Mine",
    };
}
