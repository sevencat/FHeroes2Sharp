using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The two captions of <c>editor_castle_details_window.cpp</c>: the castle's name
/// (<c>getCastleName()</c>, <c>:309-316</c>) and the title of a mage guild level (<c>getMageGuildTitle()</c>,
/// <c>:496-512</c>).
/// </summary>
/// <remarks>
/// 名字这一格显示的是作者填的自定义名；没填就显示「随机城镇名 / 随机城堡名」，那是<b>占位提示</b>而不是城堡的真名——
/// 编辑器里还没定这张地图上真会刷出哪个名字，所以不能拿引擎的随机名生成器去猜。
/// </remarks>
public static class CastleDetailsLabels
{
    /// <summary>
    /// The name to show in the name box, <c>getCastleName()</c> (<c>:309-316</c>).
    /// </summary>
    /// <param name="customName">The custom name of the metadata, empty when the author did not type one.</param>
    /// <param name="isTown">Whether the object is a town, which only changes the placeholder text.</param>
    /// <returns>The custom name, or the placeholder of the object kind.</returns>
    public static string GetCastleName(string customName, bool isTown)
    {
        ArgumentNullException.ThrowIfNull(customName);

        if (!string.IsNullOrEmpty(customName))
        {
            return customName;
        }

        return isTown ? "Random Town Name" : "Random Castle Name";
    }

    /// <summary>
    /// The name of a mage guild level, <c>getMageGuildTitle()</c> (<c>:496-512</c>).
    /// </summary>
    /// <remarks>
    /// 参考写死用种族 0（骑士）取名字，因为法师公会的名字在不同种族里是一样的，只有图形不一样。
    /// </remarks>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <returns>The building name of that guild level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is not 1 to 5.</exception>
    public static string GetMageGuildLevelTitle(int spellLevel)
    {
        BuildingType building = spellLevel switch
        {
            1 => BuildingType.MageGuild1,
            2 => BuildingType.MageGuild2,
            3 => BuildingType.MageGuild3,
            4 => BuildingType.MageGuild4,
            5 => BuildingType.MageGuild5,
            _ => throw new ArgumentOutOfRangeException(nameof(spellLevel), spellLevel, "A mage guild has five levels.")
        };

        return CastleBuildingInfo.GetName(MapRace.Knight, building);
    }
}
