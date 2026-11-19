using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The layers of the adventure map one block redraw has to paint, the port's form of the
/// <c>drawingFlags</c> of <c>CacheForMapWithResources</c> (<c>view_world.cpp:230-240</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用的是 `Interface::RedrawLevelType` 的位集合，端口的地图渲染器（<see cref="AdventureMap.AdventureMapView"/>）
/// 没有这一层：它总是把地形、物体、英雄和迷雾一次画完。所以这里把用到的四位翻译成四个开关，由调用方
/// 决定怎么画——`Fog` 关上就是「看不到迷雾」，`Towns` 打开就是「强制画城镇」，其余两位在正式构建里恒为
/// `false`（参考在非 `SAVE_WORLD_MAP` 构建里把 `LEVEL_HEROES` 异或掉，因为英雄图标后画）。
/// </para>
/// <para>
/// `LEVEL_ROUTES` 参考也一起关掉：世界视图里从不画英雄的移动路线。
/// </para>
/// </remarks>
public readonly record struct ViewWorldDrawLayers(bool Fog, bool Towns, bool Heroes, bool Routes)
{
    /// <summary>
    /// The layers a display mode asks for, <c>view_world.cpp:230-240</c>.
    /// </summary>
    /// <param name="mode">The mode the window is in.</param>
    /// <returns>The four switches.</returns>
    /// <remarks>
    /// 全图模式关掉迷雾（否则整张图除了自己看过的都是黑的）；城镇模式强制打开城镇；英雄与路线恒为关。
    /// </remarks>
    public static ViewWorldDrawLayers ForMode(ViewWorldMode mode) => new(
        Fog: mode != ViewWorldMode.ViewAll,
        Towns: mode == ViewWorldMode.ViewTowns,
        Heroes: false,
        Routes: false);
}

/// <summary>
/// Paints one block of the adventure map into <c>block</c>, <c>gameArea.Redraw( temporaryImg, flags )</c>
/// of <c>view_world.cpp:245-246</c>.
/// </summary>
/// <param name="block">The 576x576 image to paint; it is cleared to black before the call.</param>
/// <param name="centerInPixels">Where the block looks, in map pixels.</param>
/// <param name="layers">What the mode wants to see.</param>
/// <remarks>
/// 参考在这里直接操作 `Interface::GameArea`：改 ROI、设中心、`Redraw`、最后把 ROI 与中心还原。端口没有全局
/// 的地图区域，改由调用方拿自己的渲染器画——通常是一个 576x576 的临时 <see cref="AdventureMap.AdventureMapView"/>。
/// </remarks>
public delegate void ViewWorldBlockDrawer(PixelBuffer block, Point centerInPixels, ViewWorldDrawLayers layers);

/// <summary>
/// The whole map cached once per zoom level, <c>CacheForMapWithResources</c> of <c>view_world.cpp:189-268</c>.
/// </summary>
/// <remarks>
/// <para>
/// 地图按 18x18 格分块重画，每块画在 576x576 的临时图上，再最近邻缩到每一档的尺寸贴进该档的缓存图。
/// 分块的意义是临时图小、缩放比例固定（18 格整块参与采样），不会在分块接缝处出现半像素的错位。
/// </para>
/// <para>
/// <b>尺寸必须是 18 的倍数</b>：参考用两条 `assert` 保证，标准地图（36/72/108/144）都满足；端口换成
/// `ArgumentException`，因为半块会写到缓存图外面去。
/// </para>
/// <para>
/// 图标（英雄、城堡旗、矿、宝物、资源）<b>不在</b>这里画，它们在 <see cref="ViewWorldRenderer.DrawObjectsIcons"/>
/// 里贴到这些缓存图上；模式一变就要重建缓存，跟参考一样。
/// </para>
/// </remarks>
public sealed class ViewWorldMapCache
{
    /// <summary>Side of a block in tiles, <c>blockSizeX</c>/<c>blockSizeY</c>.</summary>
    public const int BlockSizeInTiles = 18;

    /// <summary>Side of a block in map pixels, 576.</summary>
    public const int BlockSizeInPixels = BlockSizeInTiles * ViewWorldZoomLevels.TileWidthPx;

    private readonly PixelBuffer[] _images;

    /// <summary>Builds the cache of the whole map for every zoom level.</summary>
    /// <param name="mode">The display mode, which decides the layers each block is painted with.</param>
    /// <param name="mapWidthInTiles">Map width in tiles, <c>world.w()</c>.</param>
    /// <param name="mapHeightInTiles">Map height in tiles, <c>world.h()</c>.</param>
    /// <param name="zoomLevelCount">Three levels, or four with the 1:1 debug level.</param>
    /// <param name="drawBlock">Paints one block, usually a game area of the caller's own.</param>
    /// <exception cref="ArgumentException">A map side is not a multiple of 18 tiles.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive or the level count is neither three nor four.</exception>
    public ViewWorldMapCache(ViewWorldMode mode, int mapWidthInTiles, int mapHeightInTiles, int zoomLevelCount, ViewWorldBlockDrawer drawBlock)
    {
        ArgumentNullException.ThrowIfNull(drawBlock);

        if (zoomLevelCount is not (ViewWorldZoomLevels.RegularZoomLevels or ViewWorldZoomLevels.TotalZoomLevels))
        {
            throw new ArgumentOutOfRangeException(nameof(zoomLevelCount), zoomLevelCount, "Only three or four zoom levels exist.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidthInTiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapHeightInTiles);

        if (mapWidthInTiles % BlockSizeInTiles != 0)
        {
            throw new ArgumentException($"The map width {mapWidthInTiles} is not a multiple of {BlockSizeInTiles} tiles.", nameof(mapWidthInTiles));
        }

        if (mapHeightInTiles % BlockSizeInTiles != 0)
        {
            throw new ArgumentException($"The map height {mapHeightInTiles} is not a multiple of {BlockSizeInTiles} tiles.", nameof(mapHeightInTiles));
        }

        Mode = mode;
        Layers = ViewWorldDrawLayers.ForMode(mode);
        _images = new PixelBuffer[zoomLevelCount];

        for (int level = 0; level < zoomLevelCount; ++level)
        {
            int tileSize = ViewWorldZoomLevels.TileSize((ZoomLevel)level);
            _images[level] = new PixelBuffer(mapWidthInTiles * tileSize, mapHeightInTiles * tileSize);
        }

        var block = new PixelBuffer(BlockSizeInPixels, BlockSizeInPixels);
        var scaled = new PixelBuffer?[zoomLevelCount];

        for (int y = 0; y < mapHeightInTiles; y += BlockSizeInTiles)
        {
            for (int x = 0; x < mapWidthInTiles; x += BlockSizeInTiles)
            {
                var center = new Point(
                    (x * ViewWorldZoomLevels.TileWidthPx) + (BlockSizeInPixels / 2),
                    (y * ViewWorldZoomLevels.TileWidthPx) + (BlockSizeInPixels / 2));

                // The reference reuses one image and lets Redraw overwrite it; clearing first keeps a
                // renderer that leaves holes from picking up the previous block's pixels.
                block.Clear();
                drawBlock(block, center, Layers);

                for (int level = 0; level < zoomLevelCount; ++level)
                {
                    int tileSize = ViewWorldZoomLevels.TileSize((ZoomLevel)level);
                    var offset = new Point(x * tileSize, y * tileSize);

                    if (tileSize == ViewWorldZoomLevels.TileWidthPx)
                    {
                        ImageOps.Blit(_images[level], offset, block, block.Bounds);
                        continue;
                    }

                    PixelBuffer target = scaled[level] ??= new PixelBuffer(BlockSizeInTiles * tileSize, BlockSizeInTiles * tileSize);
                    ImageOps.Resize(block, target);
                    ImageOps.Blit(_images[level], offset, target, target.Bounds);
                }
            }
        }
    }

    /// <summary>The mode the cache was built for.</summary>
    public ViewWorldMode Mode { get; }

    /// <summary>The layers every block was painted with.</summary>
    public ViewWorldDrawLayers Layers { get; }

    /// <summary>How many zoom levels are cached, three or four.</summary>
    public int ZoomLevelCount => _images.Length;

    /// <summary>One image per zoom level, in map pixels of that level.</summary>
    public IReadOnlyList<PixelBuffer> Images => _images;

    /// <summary>The cached map of one zoom level.</summary>
    /// <param name="level">The zoom level.</param>
    /// <returns>The image, sized <c>map width * tile size</c> by <c>map height * tile size</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is not cached.</exception>
    public PixelBuffer GetImage(ZoomLevel level)
    {
        int index = (int)level;
        if (index < 0 || index >= _images.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The zoom level is not cached.");
        }

        return _images[index];
    }
}
