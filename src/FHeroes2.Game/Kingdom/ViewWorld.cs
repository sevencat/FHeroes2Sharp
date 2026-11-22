using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// What the View World window shows, <c>ViewWorldMode</c> of <c>view_world.h:37-48</c>.
/// </summary>
public enum ViewWorldMode
{
    /// <summary>Only what is not under fog of war, <c>OnlyVisible = 0</c>.</summary>
    OnlyVisible = 0,

    /// <summary>Reveal artifacts, <c>ViewArtifacts = 1</c>.</summary>
    ViewArtifacts = 1,

    /// <summary>Reveal mines, <c>ViewMines = 2</c>.</summary>
    ViewMines = 2,

    /// <summary>Reveal resources, <c>ViewResources = 3</c>.</summary>
    ViewResources = 3,

    /// <summary>Reveal heroes, <c>ViewHeroes = 4</c>.</summary>
    ViewHeroes = 4,

    /// <summary>Reveal towns, <c>ViewTowns = 5</c>.</summary>
    ViewTowns = 5,

    /// <summary>Reveal everything, <c>ViewAll = 6</c>.</summary>
    ViewAll = 6,
}

/// <summary>
/// The four scales the View World window can show the map at, <c>ZoomLevel</c> of <c>screen.h</c>.
/// </summary>
/// <remarks>
/// 参考的枚举没有显式取值，序号就是「缩放到第几级」，`tileSizePerZoomLevel` 用它做下标。
/// </remarks>
public enum ZoomLevel
{
    /// <summary>Map pixels shown one to four, <c>ZoomLevel0</c>.</summary>
    ZoomLevel0 = 0,

    /// <summary>Map pixels shown one to six, <c>ZoomLevel1</c>.</summary>
    ZoomLevel1 = 1,

    /// <summary>Map pixels shown one to twelve, <c>ZoomLevel2</c>.</summary>
    ZoomLevel2 = 2,

    /// <summary>Map pixels shown one to thirty two, i.e. the adventure map scale, <c>ZoomLevel3</c>.</summary>
    ZoomLevel3 = 3,
}

/// <summary>
/// The fixed tables and the two index conversions of <c>view_world.cpp:72-187</c>.
/// </summary>
/// <remarks>
/// 这一层只有常量与纯函数：四张缩放表（tile 尺寸、地形、字母、旗帜）、`computeROI`、两个相邻档位查询、
/// 颜色/资源到图号偏移的映射。渲染本身在后续批次里接。
/// </remarks>
public static class ViewWorldZoomLevels
{
    /// <summary>Width of one map tile in map pixels, <c>fheroes2::tileWidthPx</c>.</summary>
    public const int TileWidthPx = 32;

    /// <summary>How many zoom levels exist with the 1:1 debug level, <c>totalZoomLevels</c>.</summary>
    public const int TotalZoomLevels = 4;

    /// <summary>How many zoom levels exist normally, i.e. without the 1:1 debug level.</summary>
    public const int RegularZoomLevels = 3;

    /// <summary>Index of an unknown color or resource, <c>unknownIndex = UINT32_MAX</c>.</summary>
    public const uint UnknownIndex = uint.MaxValue;

    private static readonly int[] TileSizes = [4, 6, 12, 32];

    /// <summary>Size in map pixels of one map pixel at a zoom level, <c>tileSizePerZoomLevel</c>.</summary>
    /// <param name="level">The zoom level, zero to three.</param>
    /// <returns>4, 6, 12 or 32.</returns>
    public static int TileSize(ZoomLevel level) => TileSizes[Validate(level)];

    /// <summary>The terrain image set of a zoom level, <c>icnPerZoomLevel</c>.</summary>
    /// <param name="level">The zoom level, zero to three.</param>
    /// <returns><c>MISC4</c>, <c>MISC6</c> or <c>MISC12</c>; the last two levels share <c>MISC12</c>.</returns>
    public static IcnType TerrainIcn(ZoomLevel level) => Validate(level) switch
    {
        0 => IcnType.Misc4,
        1 => IcnType.Misc6,
        _ => IcnType.Misc12,
    };

    /// <summary>The resource letter image set of a zoom level, <c>icnLetterPerZoomLevel</c>.</summary>
    /// <param name="level">The zoom level, zero to three.</param>
    /// <returns><c>LETTER4</c>, <c>LETTER6</c> or <c>LETTER12</c>.</returns>
    public static IcnType LetterIcn(ZoomLevel level) => Validate(level) switch
    {
        0 => IcnType.Letter4,
        1 => IcnType.Letter6,
        _ => IcnType.Letter12,
    };

    /// <summary>The player flag image set of a zoom level, <c>icnPerZoomLevelFlags</c>.</summary>
    /// <param name="level">The zoom level, zero to three.</param>
    /// <returns><c>VWFLAG4</c>, <c>VWFLAG6</c> or <c>VWFLAG12</c>.</returns>
    public static IcnType FlagIcn(ZoomLevel level) => Validate(level) switch
    {
        0 => IcnType.Vwflag4,
        1 => IcnType.Vwflag6,
        _ => IcnType.Vwflag12,
    };

    /// <summary>
    /// The part of the map visible through a window of a given size, <c>computeROI()</c> of <c>view_world.cpp:86-96</c>.
    /// </summary>
    /// <param name="centerInPixels">Where the window looks, in map pixels.</param>
    /// <param name="zoomLevel">The current zoom level.</param>
    /// <param name="visibleRoi">The window itself, in screen pixels.</param>
    /// <returns>The map rectangle shown, in map pixels; it may stick out of the map.</returns>
    /// <remarks>
    /// 参考用 `${窗口宽} * 32 / 缩放` 换算，整除与 `- 宽/2` 的顺序都照抄：先算窗口能盖住多少 map 像素，
    /// 再以中心减一半。改成浮点或先减半会差一像素，缩放的锚点也跟着歪。
    /// </remarks>
    public static Rect ComputeRoi(Point centerInPixels, ZoomLevel zoomLevel, Rect visibleRoi)
    {
        int tileSize = TileSize(zoomLevel);
        int pixelsW = visibleRoi.Width * TileWidthPx / tileSize;
        int pixelsH = visibleRoi.Height * TileWidthPx / tileSize;

        return new Rect(centerInPixels.X - (pixelsW / 2), centerInPixels.Y - (pixelsH / 2), pixelsW, pixelsH);
    }

    /// <summary>
    /// The zoom level one step closer, <c>GetNextZoomLevel()</c> of <c>view_world.cpp:98-119</c>.
    /// </summary>
    /// <param name="level">The current zoom level.</param>
    /// <param name="cycle">Whether the last level wraps back to the first one.</param>
    /// <param name="zoomLevelCount">Three levels without the 1:1 debug level, four with it.</param>
    /// <returns>The next level.</returns>
    /// <remarks>
    /// 三级模式下的「最后一档」是 `ZoomLevel2`，所以从二级再放大只会在二级停住（除非允许循环）。
    /// 参考在这里写的是 `assert( 3 || 4 )`，端口换成抛 `ArgumentOutOfRangeException`。
    /// </remarks>
    public static ZoomLevel Next(ZoomLevel level, bool cycle, int zoomLevelCount)
    {
        ValidateCount(zoomLevelCount);

        return level switch
        {
            ZoomLevel.ZoomLevel0 => ZoomLevel.ZoomLevel1,
            ZoomLevel.ZoomLevel1 => ZoomLevel.ZoomLevel2,
            _ when zoomLevelCount == RegularZoomLevels => cycle ? ZoomLevel.ZoomLevel0 : ZoomLevel.ZoomLevel2,
            ZoomLevel.ZoomLevel2 => ZoomLevel.ZoomLevel3,
            _ => cycle ? ZoomLevel.ZoomLevel0 : ZoomLevel.ZoomLevel3,
        };
    }

    /// <summary>
    /// The zoom level one step further away, <c>GetPreviousZoomLevel()</c> of <c>view_world.cpp:121-140</c>.
    /// </summary>
    /// <param name="level">The current zoom level.</param>
    /// <param name="cycle">Whether the first level wraps back to the last one.</param>
    /// <param name="zoomLevelCount">Three levels without the 1:1 debug level, four with it.</param>
    /// <returns>The previous level.</returns>
    /// <remarks>
    /// 与放大不对称：三级模式下 `ZoomLevel3` 不在表内，但一旦传进来仍然退到二级，而不是循环到零级。
    /// </remarks>
    public static ZoomLevel Previous(ZoomLevel level, bool cycle, int zoomLevelCount)
    {
        ValidateCount(zoomLevelCount);

        return level switch
        {
            ZoomLevel.ZoomLevel1 => ZoomLevel.ZoomLevel0,
            ZoomLevel.ZoomLevel2 => ZoomLevel.ZoomLevel1,
            ZoomLevel.ZoomLevel0 => cycle
                ? (zoomLevelCount == RegularZoomLevels ? ZoomLevel.ZoomLevel2 : ZoomLevel.ZoomLevel3)
                : ZoomLevel.ZoomLevel0,
            _ => ZoomLevel.ZoomLevel2,
        };
    }

    /// <summary>
    /// Converts a player color to the offset inside <c>ICN::VWFLAG*</c> / <c>ICN::MISC*</c>,
    /// <c>colorToOffsetICN()</c> of <c>view_world.cpp:143-164</c>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>Zero to five for the six players, six for an unowned object, <see cref="UnknownIndex"/> otherwise.</returns>
    /// <remarks>
    /// 中立（`NONE`）与 `UNUSED` 都落到第六格；其余非六色的值（包括 `Blue | Green` 这类组合）走 `Unknown` 分支。
    /// </remarks>
    public static uint ColorToOffsetIcn(PlayerColor color) => color switch
    {
        PlayerColor.Blue => 0,
        PlayerColor.Green => 1,
        PlayerColor.Red => 2,
        PlayerColor.Yellow => 3,
        PlayerColor.Orange => 4,
        PlayerColor.Purple => 5,
        PlayerColor.None => 6,
        PlayerColor.Unused => 6,
        _ => UnknownIndex,
    };

    /// <summary>
    /// Converts a resource to the offset inside <c>ICN::LETTER*</c>, <c>resourceToOffsetICN()</c> of
    /// <c>view_world.cpp:167-187</c>.
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> bits.</param>
    /// <returns>Zero to six, or <see cref="UnknownIndex"/> for anything else.</returns>
    /// <remarks>
    /// 传的是单个位（`Resource.Wood` 等），不是组合：组合值在参考里同样落到 `default`。
    /// </remarks>
    public static uint ResourceToOffsetIcn(int resource) => resource switch
    {
        Resource.Wood => 0,
        Resource.Mercury => 1,
        Resource.Ore => 2,
        Resource.Sulfur => 3,
        Resource.Crystal => 4,
        Resource.Gems => 5,
        Resource.Gold => 6,
        _ => UnknownIndex,
    };

    private static int Validate(ZoomLevel level)
    {
        int value = (int)level;
        if (value < 0 || value >= TotalZoomLevels)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown zoom level.");
        }

        return value;
    }

    private static void ValidateCount(int zoomLevelCount)
    {
        if (zoomLevelCount is not (RegularZoomLevels or TotalZoomLevels))
        {
            throw new ArgumentOutOfRangeException(nameof(zoomLevelCount), zoomLevelCount, "Only three or four zoom levels exist.");
        }
    }
}

/// <summary>
/// The visible rectangle of every zoom level and the centre it is built around, <c>ViewWorld::ZoomROIs</c> of
/// <c>view_world.cpp:508-587</c>.
/// </summary>
/// <remarks>
/// 状态只有「当前档位 + 中心」，其余四档矩形都由 `ComputeRoi` 现算。换档时先换档位、再按**新**档位的矩形
/// 重新夹中心（`_changeZoom` 的顺序），所以从 1:4 放大到 1:32 时贴边的中心会被再推进一次。
/// </remarks>
public sealed class ViewWorldZoom
{
    private readonly Rect[] _roiForZoomLevels;
    private readonly Rect _visibleRoi;
    private readonly int _mapWidthInPixels;
    private readonly int _mapHeightInPixels;

    private ZoomLevel _zoomLevel;
    private Point _center;

    /// <summary>Creates the view and clamps its centre into the map.</summary>
    /// <param name="zoomLevel">The starting zoom level.</param>
    /// <param name="centerInPixels">Where to look, in map pixels.</param>
    /// <param name="visibleScreenInPixels">The window, in screen pixels.</param>
    /// <param name="zoomLevelCount">Three levels, or four with the 1:1 debug level.</param>
    /// <param name="mapWidthInTiles">Map width in tiles, <c>world.w()</c>.</param>
    /// <param name="mapHeightInTiles">Map height in tiles, <c>world.h()</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The level count is neither three nor four.</exception>
    public ViewWorldZoom(ZoomLevel zoomLevel, Point centerInPixels, Rect visibleScreenInPixels, int zoomLevelCount, int mapWidthInTiles, int mapHeightInTiles)
    {
        if (zoomLevelCount is not (ViewWorldZoomLevels.RegularZoomLevels or ViewWorldZoomLevels.TotalZoomLevels))
        {
            throw new ArgumentOutOfRangeException(nameof(zoomLevelCount), zoomLevelCount, "Only three or four zoom levels exist.");
        }

        if ((int)zoomLevel >= zoomLevelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomLevel), zoomLevel, "The zoom level is not available in this view.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidthInTiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapHeightInTiles);

        _zoomLevel = zoomLevel;
        _center = centerInPixels;
        _visibleRoi = visibleScreenInPixels;
        _roiForZoomLevels = new Rect[zoomLevelCount];
        _mapWidthInPixels = mapWidthInTiles * ViewWorldZoomLevels.TileWidthPx;
        _mapHeightInPixels = mapHeightInTiles * ViewWorldZoomLevels.TileWidthPx;

        UpdateZoomLevels();
        UpdateCenter();
    }

    /// <summary>Number of zoom levels this view can use, three or four.</summary>
    public int ZoomLevelCount => _roiForZoomLevels.Length;

    /// <summary>The current zoom level.</summary>
    public ZoomLevel ZoomLevel => _zoomLevel;

    /// <summary>The point the view looks at, in map pixels; always inside the map.</summary>
    public Point Center => _center;

    /// <summary>The visible map rectangle at the current zoom level, in map pixels, <c>GetROIinPixels()</c>.</summary>
    public Rect RoiInPixels => _roiForZoomLevels[(int)_zoomLevel];

    /// <summary>
    /// The visible map rectangle rounded to whole tiles, <c>GetROIinTiles()</c> of <c>view_world.cpp:579-587</c>.
    /// </summary>
    /// <remarks>
    /// 四个值各自加半个 tile 再整除，负数时是向零截断（与参考的 C++ 整除一致），因此缩到最小档也不会
    /// 把「左边界刚好压在 tile 中间」的那一列吃掉。
    /// </remarks>
    public Rect RoiInTiles
    {
        get
        {
            Rect roi = RoiInPixels;
            const int half = ViewWorldZoomLevels.TileWidthPx / 2;

            return new Rect(
                (roi.X + half) / ViewWorldZoomLevels.TileWidthPx,
                (roi.Y + half) / ViewWorldZoomLevels.TileWidthPx,
                (roi.Width + half) / ViewWorldZoomLevels.TileWidthPx,
                (roi.Height + half) / ViewWorldZoomLevels.TileWidthPx);
        }
    }

    /// <summary>Zooms one step closer, <c>zoomIn()</c>.</summary>
    /// <param name="cycle">Whether the last level wraps back to the first one.</param>
    /// <returns><see langword="true"/> if the level actually changed.</returns>
    public bool ZoomIn(bool cycle = false) =>
        ChangeZoom(ViewWorldZoomLevels.Next(_zoomLevel, cycle, _roiForZoomLevels.Length));

    /// <summary>Zooms one step further away, <c>zoomOut()</c>.</summary>
    /// <param name="cycle">Whether the first level wraps back to the last one.</param>
    /// <returns><see langword="true"/> if the level actually changed.</returns>
    public bool ZoomOut(bool cycle = false) =>
        ChangeZoom(ViewWorldZoomLevels.Previous(_zoomLevel, cycle, _roiForZoomLevels.Length));

    /// <summary>
    /// Moves the view to a new centre, clamped so that the whole window stays on the map,
    /// <c>ChangeCenter()</c> of <c>view_world.cpp:531-557</c>.
    /// </summary>
    /// <param name="centerInPixels">The requested centre, in map pixels.</param>
    /// <returns><see langword="true"/> if the centre moved.</returns>
    /// <remarks>
    /// 窗口比地图大时中心被塞到地图正中：`( size - 1 ) / 2` 的减一是参考的写法，偶数尺寸下它比
    /// `size / 2` 少半格，也就是把地图往左上顶半像素。
    /// </remarks>
    public bool ChangeCenter(Point centerInPixels)
    {
        Rect currentRect = RoiInPixels;

        int x = _mapWidthInPixels <= currentRect.Width
            ? (_mapWidthInPixels - 1) / 2
            : Math.Clamp(centerInPixels.X, currentRect.Width / 2, _mapWidthInPixels - (currentRect.Width / 2));

        int y = _mapHeightInPixels <= currentRect.Height
            ? (_mapHeightInPixels - 1) / 2
            : Math.Clamp(centerInPixels.Y, currentRect.Height / 2, _mapHeightInPixels - (currentRect.Height / 2));

        Point newCenter = new(x, y);

        if (newCenter == _center)
        {
            return false;
        }

        _center = newCenter;
        UpdateZoomLevels();

        return true;
    }

    private void UpdateZoomLevels()
    {
        for (int i = 0; i < _roiForZoomLevels.Length; ++i)
        {
            _roiForZoomLevels[i] = ViewWorldZoomLevels.ComputeRoi(_center, (ZoomLevel)i, _visibleRoi);
        }
    }

    private bool UpdateCenter() => ChangeCenter(_center);

    private bool ChangeZoom(ZoomLevel newLevel)
    {
        bool changed = newLevel != _zoomLevel;
        _zoomLevel = newLevel;
        UpdateCenter();

        return changed;
    }
}
