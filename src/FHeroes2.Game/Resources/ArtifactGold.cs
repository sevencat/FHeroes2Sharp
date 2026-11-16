using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Resources;

/// <summary>
/// The gold a hero is paid when an object tries to hand him an artifact he has no room for,
/// <c>GoldInsteadArtifact()</c> (<c>artifact.cpp:1066</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>artifact.cpp:1066-1083</c>。只有六种对象会在背包满时把宝物折成金币，各自的价钱是硬编码的，
/// 与宝物本身的等级无关；其余对象返回 0，调用方拿到 0 就该当作「什么都没给」。
/// </para>
/// <para>
/// 这一层刻意只有一个纯函数，不碰英雄也不碰瓦片——参考也是这样，它被五个不同的动作函数共用。
/// </para>
/// </remarks>
public static class ArtifactGold
{
    /// <summary>
    /// The gold paid instead of the artifact, by the object that held it, <c>GoldInsteadArtifact()</c>
    /// (<c>artifact.cpp:1066</c>).
    /// </summary>
    /// <param name="objectType">The object the hero is visiting.</param>
    /// <returns>The gold, or <c>0</c> for an object that does not pay for artifacts.</returns>
    public static uint InsteadOf(Mp2ObjectType objectType) => objectType switch
    {
        Mp2ObjectType.Skeleton or Mp2ObjectType.TreasureChest or Mp2ObjectType.ShipwreckSurvivor => 1000,
        Mp2ObjectType.SeaChest => 1500,
        Mp2ObjectType.Graveyard => 2000,
        Mp2ObjectType.Shipwreck => 5000,
        _ => 0,
    };
}
