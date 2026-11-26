using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The conversions between a map's metadata tables and the runtime castle and hero objects,
/// <c>Maps::getBuildingsFromVector()</c> (<c>map_format_helper.cpp:1749-1757</c>),
/// <c>setDefaultCastleDefenderArmy()</c> (<c>:1759-1767</c>), <c>isDefaultCastleDefenderArmy()</c>
/// (<c>:1769-1772</c>), <c>loadCastleArmy()</c> / <c>saveCastleArmy()</c> (<c>:1774-1787</c>) and the hero pair
/// (<c>:1789-1803</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的格式里，城堡与英雄的军队都是两排 <c>int32_t</c>：一排队形、一排数量，都按槽位对。装进运行时的
/// <see cref="Army"/> 时逐槽造 <see cref="Troop"/>；存回去时逐槽抄回来。两边都不做「合并同类」那种整理，因为地图
/// 作者在编辑器里看到的顺序就是他排的顺序。
/// </para>
/// <para>
/// <b>「默认」是一个哨兵</b>：城堡的守军全部类型为负表示「引擎按种族自己配」，不是「空军队」。所以
/// <see cref="LoadCastleArmy"/> 在默认时返回 <see langword="false"/> 并且一支不动，作者据此知道「这一项没改过」。
/// 英雄那边用的是另一个哨兵——全部 <c>&lt;= 0</c>（0 表示空槽），见 <see cref="LoadHeroArmy"/>。
/// </para>
/// <para>
/// <b>负数按空槽处理</b>：参考对着 <c>-1</c> 写断言，端口不抛——元数据可以来自一张手改过的地图，读的时候把它当成
/// 空槽比让编辑器带上崩掉好。
/// </para>
/// </remarks>
public static class MapFormatArmy
{
    /// <summary>
    /// Turns the list of buildings of a metadata table into a mask,
    /// <c>Maps::getBuildingsFromVector()</c> (<c>map_format_helper.cpp:1749-1757</c>).
    /// </summary>
    /// <param name="buildingsVector">The <c>builtBuildings</c> or <c>bannedBuildings</c> list.</param>
    /// <returns>The mask; <see cref="BuildingType.Nothing"/> for an empty list.</returns>
    public static BuildingType GetBuildings(IReadOnlyList<uint> buildingsVector)
    {
        ArgumentNullException.ThrowIfNull(buildingsVector);

        BuildingType buildings = BuildingType.Nothing;

        foreach (uint building in buildingsVector)
        {
            buildings |= (BuildingType)building;
        }

        return buildings;
    }

    /// <summary>The mask of the buildings a castle stands with, <c>getBuildingsFromVector( builtBuildings )</c>.</summary>
    /// <param name="metadata">The castle.</param>
    /// <returns>The mask.</returns>
    public static BuildingType GetBuiltBuildings(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return GetBuildings(metadata.BuiltBuildings);
    }

    /// <summary>The mask of the buildings that can never be built, <c>getBuildingsFromVector( bannedBuildings )</c>.</summary>
    /// <param name="metadata">The castle.</param>
    /// <returns>The mask.</returns>
    public static BuildingType GetBannedBuildings(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return GetBuildings(metadata.BannedBuildings);
    }

    /// <summary>
    /// Puts a castle back to the engine's default guard, <c>Maps::setDefaultCastleDefenderArmy()</c>
    /// (<c>map_format_helper.cpp:1759-1767</c>).
    /// </summary>
    /// <remarks>
    /// 类型写 <c>-1</c>、数量写 0：前一个是「按种族配」的哨兵，后一个让读者一眼看出这些数量没有意义。
    /// </remarks>
    /// <param name="metadata">The castle.</param>
    public static void SetDefaultCastleDefenderArmy(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        Array.Fill(metadata.DefenderMonsterType, -1);
        Array.Fill(metadata.DefenderMonsterCount, 0);
    }

    /// <summary>
    /// Whether a castle keeps the engine's default guard, <c>Maps::isDefaultCastleDefenderArmy()</c>
    /// (<c>map_format_helper.cpp:1769-1772</c>).
    /// </summary>
    /// <param name="metadata">The castle.</param>
    /// <returns><see langword="true"/> when every slot's type is negative.</returns>
    public static bool IsDefaultCastleDefenderArmy(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        foreach (int type in metadata.DefenderMonsterType)
        {
            if (type >= 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Fills an army with the guard of a castle, <c>Maps::loadCastleArmy()</c> (<c>map_format_helper.cpp:1774-1782</c>).
    /// </summary>
    /// <param name="army">Army to fill.</param>
    /// <param name="metadata">The castle.</param>
    /// <returns><see langword="false"/> when the castle keeps the default guard, in which case the army is untouched.</returns>
    public static bool LoadCastleArmy(ArmyStack army, CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(metadata);

        if (IsDefaultCastleDefenderArmy(metadata))
        {
            return false;
        }

        LoadArmyFromMetadata(army, metadata.DefenderMonsterType, metadata.DefenderMonsterCount);

        return true;
    }

    /// <summary>
    /// Writes an army into the guard of a castle, <c>Maps::saveCastleArmy()</c> (<c>:1784-1787</c>).
    /// </summary>
    /// <param name="army">Army to read.</param>
    /// <param name="metadata">The castle.</param>
    public static void SaveCastleArmy(ArmyStack army, CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(metadata);

        SaveArmyToMetadata(army, metadata.DefenderMonsterType, metadata.DefenderMonsterCount);
    }

    /// <summary>
    /// Fills an army with the army of a hero, <c>Maps::loadHeroArmy()</c> (<c>:1789-1798</c>).
    /// </summary>
    /// <remarks>
    /// 英雄用的哨兵与城堡不同：全部 <c>&lt;= 0</c> 表示「没有自定义军队」，所以 0（空槽）也算没改过。
    /// </remarks>
    /// <param name="army">Army to fill.</param>
    /// <param name="metadata">The hero.</param>
    /// <returns><see langword="false"/> when the hero keeps the default army, in which case the army is untouched.</returns>
    public static bool LoadHeroArmy(ArmyStack army, HeroMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(metadata);

        foreach (int type in metadata.ArmyMonsterType)
        {
            if (type > 0)
            {
                LoadArmyFromMetadata(army, metadata.ArmyMonsterType, metadata.ArmyMonsterCount);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes an army into the army of a hero, <c>Maps::saveHeroArmy()</c> (<c>:1800-1803</c>).
    /// </summary>
    /// <param name="army">Army to read.</param>
    /// <param name="metadata">The hero.</param>
    public static void SaveHeroArmy(ArmyStack army, HeroMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(metadata);

        SaveArmyToMetadata(army, metadata.ArmyMonsterType, metadata.ArmyMonsterCount);
    }

    /// <summary>
    /// The two lanes of a metadata table into an army, <c>loadArmyFromMetadata()</c> (<c>:75-84</c>).
    /// </summary>
    /// <param name="army">Army to fill.</param>
    /// <param name="unitType">Monster type of each slot.</param>
    /// <param name="unitCount">Count of each slot.</param>
    private static void LoadArmyFromMetadata(ArmyStack army, int[] unitType, int[] unitCount)
    {
        Troop[] troops = new Troop[unitType.Length];

        for (int i = 0; i < troops.Length; ++i)
        {
            int count = Math.Max(i < unitCount.Length ? unitCount[i] : 0, 0);
            troops[i] = new Troop((MonsterType)unitType[i], (uint)count);
        }

        army.Assign(troops);
    }

    /// <summary>
    /// The army into the two lanes of a metadata table, <c>saveArmyToMetadata()</c> (<c>:86-99</c>).
    /// </summary>
    /// <param name="army">Army to read.</param>
    /// <param name="unitType">Monster type of each slot.</param>
    /// <param name="unitCount">Count of each slot.</param>
    private static void SaveArmyToMetadata(ArmyStack army, int[] unitType, int[] unitCount)
    {
        // 参考断言两排与军队同长；端口按排的长度走，短的军队多出来的槽留 0。
        for (int i = 0; i < unitType.Length; ++i)
        {
            Troop troop = army.GetTroop(i);
            unitType[i] = (int)troop.Monster;
            unitCount[i] = i < unitCount.Length ? (int)troop.Count : 0;
        }
    }
}
