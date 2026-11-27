using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The town row of the condition panel, a port of <c>renderConditionsTownIconAndName()</c>
/// (<c>editor_map_specs_window.cpp:283-302</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一行由两半拼成：<see cref="MapSpecsConditionIcons.BuildTownIcon"/> 现拼的城镇图标贴在
/// (<c>roi.x</c>, <c>roi.y + 4</c>)，标题从图标右侧 5 像素、<c>roi.y + 12</c> 起，先用
/// <c>fitToOneRow( roi.width - 图标宽 - 5 )</c> 折行再按 <c>roi</c> 裁剪。参考立刻返回
/// <c>{ roi.x, roi.y + 4, 图标宽 + 5 + 文本宽, 图标高 }</c> 给调用方当可点区域。
/// </para>
/// <para>
/// 差异：参考对 <c>castleMetadata</c> 只有 <c>assert</c>；端口在美术件缺失（图标拼不出来）时跳过图标、
/// 只画标题，返回的区域宽度按 0 图标宽算——编辑器窗口不该因为美术件不全就画不出来。
/// </para>
/// </remarks>
public static class MapSpecsConditionTownRow
{
    /// <summary>Vertical offset of the icon from the panel area, <c>roi.y + 4</c> (<c>:294</c>).</summary>
    public const int IconOffsetY = 4;

    /// <summary>Vertical offset of the title from the panel area, <c>roi.y + 12</c> (<c>:299</c>).</summary>
    public const int TitleOffsetY = 12;

    /// <summary>Gap between the icon and the title, <c>:298-299</c>.</summary>
    public const int TitleGap = 5;

    /// <summary>
    /// Paints the row: the town icon and the title next to it.
    /// </summary>
    /// <param name="town">The town the row stands for.</param>
    /// <param name="mapWidth">Width of the map, needed by the title's position.</param>
    /// <param name="roi">The panel area the row is drawn in; the title is clipped to it.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork (<c>LOCATORE</c>) is used.</param>
    /// <returns>The area the reference hands back as <c>_selectConditionRoi</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/> or the town has no name string.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="town"/> has a combined colour.</exception>
    public static Rect Draw(in MapSpecsConditionTownRowData town, int mapWidth, in Rect roi, PixelBuffer output,
        bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(town.Name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);

        Sprite? icon = MapSpecsConditionIcons.BuildTownIcon(town.Race, town.Color, town.HasFort, isEvilInterface);
        int iconWidth = icon?.Width ?? 0;

        // 参考 296-298 行：标题走 getTownTitle，再 fitToOneRow( roi.width - 图标宽 - 5 )。
        Text title = new(
            MapSpecsConditionTitles.GetTownTitle(town.Name, town.Race, !town.HasFort, town.TileIndex, mapWidth),
            FontType.NormalWhite());
        title.FitToOneRow(roi.Width - iconWidth - TitleGap);

        if (icon is not null)
        {
            ImageOps.BlitSprite(output, new Point(roi.X, roi.Y + IconOffsetY), icon);
        }

        title.DrawInRoi(roi.X + iconWidth + TitleGap, roi.Y + TitleOffsetY, output, roi);

        return new Rect(roi.X, roi.Y + IconOffsetY, iconWidth + TitleGap + title.Width(), icon?.Height ?? title.Height());
    }
}
