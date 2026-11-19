using System.Diagnostics;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The pixel half of the View World window: the visible slice of the cached map and the object icons,
/// <c>DrawWorld()</c> of <c>view_world.cpp:270-315</c> and <c>DrawObjectsIcons()</c> of <c>view_world.cpp:317-460</c>.
/// </summary>
/// <remarks>
/// <para>
/// 两块都不碰窗口控件：<see cref="DrawWorld"/> 只把缓存图裁一块贴进调用方给的 <see cref="PixelBuffer"/>，四周多出来的
/// 地方涂黑；<see cref="DrawObjectsIcons"/> 把英雄、城堡旗、矿、宝物、资源的小图标贴进缓存图（所以**换模式就要重建
/// 缓存**，参考的 `_cacheForMapWithResources` 也是这么用的）。
/// </para>
/// <para>
/// 参考画图标时要读 `world`、`Assets::getImage` 与全局 `Display`；端口把这三种依赖都做成参数：`World`、<see cref="IViewWorldSpriteSource"/>、
/// 目标 <see cref="PixelBuffer"/>。
/// </para>
/// </remarks>
public static class ViewWorldRenderer
{
    /// <summary>Width of the window frame in pixels, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidthPx = 16;

    /// <summary>Side of the radar on the right in pixels, <c>fheroes2::radarWidthPx</c>.</summary>
    public const int RadarWidthPx = 144;

    /// <summary>
    /// Blits the cached map onto the screen so that <paramref name="visibleRoi"/> shows what
    /// <paramref name="zoom"/> looks at, <c>DrawWorld()</c> of <c>view_world.cpp:270-315</c>.
    /// </summary>
    /// <param name="zoom">The current zoom level and centre.</param>
    /// <param name="cache">The cached map, built for the same number of zoom levels.</param>
    /// <param name="display">Where to draw.</param>
    /// <param name="visibleRoi">The window inside <paramref name="display"/>, in screen pixels.</param>
    /// <param name="borderWidthPx">The frame around the screen, <see cref="BorderWidthPx"/> by default.</param>
    /// <param name="radarWidthPx">The radar strip on the right, <see cref="RadarWidthPx"/> by default.</param>
    /// <exception cref="ArgumentException">The cache was built for another number of zoom levels.</exception>
    /// <remarks>
    /// 偏移是 `tileSize * map 像素 / 32`：缓存图本身就按档位缩放过了，所以这里按同一比例换算。图比窗口小时
    /// 居中贴，四周留给黑边；图比窗口大时按 `inPos` 裁掉左上角、`outPos` 顶到边框。
    /// </remarks>
    public static void DrawWorld(ViewWorldZoom zoom, ViewWorldMapCache cache, PixelBuffer display, Rect visibleRoi,
        int borderWidthPx = BorderWidthPx, int radarWidthPx = RadarWidthPx)
    {
        ArgumentNullException.ThrowIfNull(zoom);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(display);

        if (zoom.ZoomLevelCount != cache.ZoomLevelCount)
        {
            throw new ArgumentException(
                $"The cache has {cache.ZoomLevelCount} zoom levels but the view uses {zoom.ZoomLevelCount}.", nameof(cache));
        }

        ZoomLevel zoomLevel = zoom.ZoomLevel;
        PixelBuffer image = cache.GetImage(zoomLevel);
        int tileSize = ViewWorldZoomLevels.TileSize(zoomLevel);
        Rect roiInPixels = zoom.RoiInPixels;

        int offsetPixelsX = tileSize * roiInPixels.X / ViewWorldZoomLevels.TileWidthPx;
        int offsetPixelsY = tileSize * roiInPixels.Y / ViewWorldZoomLevels.TileWidthPx;

        var inPos = new Point(Math.Max(offsetPixelsX, 0), Math.Max(offsetPixelsY, 0));
        var outPos = new Point(
            borderWidthPx + (offsetPixelsX < 0 ? -offsetPixelsX : 0),
            borderWidthPx + (offsetPixelsY < 0 ? -offsetPixelsY : 0));

        ImageOps.Blit(display, outPos, image, new Rect(inPos, visibleRoi.Size));

        // Fill black pixels outside of the main view.
        if (image.Width < visibleRoi.Width)
        {
            // Left black bar.
            FillBlack(display, borderWidthPx, borderWidthPx, outPos.X - borderWidthPx, visibleRoi.Height);
            // Right black bar.
            FillBlack(display,
                borderWidthPx - offsetPixelsX + image.Width,
                borderWidthPx,
                display.Width - (3 * borderWidthPx) - radarWidthPx + offsetPixelsX - image.Width,
                visibleRoi.Height);
        }

        if (image.Height < visibleRoi.Height)
        {
            // Top black bar.
            FillBlack(display, borderWidthPx, borderWidthPx, display.Width - (3 * borderWidthPx) - radarWidthPx, outPos.Y - borderWidthPx);
            // Bottom black bar.
            FillBlack(display,
                borderWidthPx,
                borderWidthPx - offsetPixelsY + image.Height,
                visibleRoi.Width,
                display.Height - (2 * borderWidthPx) + offsetPixelsY - image.Height);
        }
    }

    /// <summary>
    /// The side panel sprite of a display mode, <c>GetSpriteResource()</c> of <c>view_world.cpp:462-480</c>.
    /// </summary>
    /// <param name="mode">The display mode.</param>
    /// <param name="evil">Whether the evil interface is selected.</param>
    /// <returns>The <c>VIEW*</c> or <c>EVIW*</c> archive.</returns>
    public static IcnType GetSpriteResource(ViewWorldMode mode, bool evil) => mode switch
    {
        ViewWorldMode.ViewAll => evil ? IcnType.EviwAll : IcnType.ViewAll,
        ViewWorldMode.ViewArtifacts => evil ? IcnType.Eviwrtfx : IcnType.Viewrtfx,
        ViewWorldMode.ViewMines => evil ? IcnType.Eviwmine : IcnType.Viewmine,
        ViewWorldMode.ViewResources => evil ? IcnType.Eviwrsrc : IcnType.Viewrsrc,
        ViewWorldMode.ViewHeroes => evil ? IcnType.Eviwhros : IcnType.Viewhros,
        ViewWorldMode.ViewTowns => evil ? IcnType.Eviwtwns : IcnType.Viewtwns,
        _ => evil ? IcnType.Eviwwrld : IcnType.Viewwrld,
    };

    /// <summary>
    /// Stamps the object icons onto the cached map, <c>DrawObjectsIcons()</c> of <c>view_world.cpp:317-460</c>.
    /// </summary>
    /// <param name="color">The player the window is open for; fog of this player hides everything but its own objects.</param>
    /// <param name="mode">The display mode, which decides what is drawn through fog.</param>
    /// <param name="cache">The cached map; the icons are written into its images, so rebuild it after switching modes.</param>
    /// <param name="world">The map to read the objects and the fog from.</param>
    /// <param name="sprites">Where the icons come from.</param>
    /// <remarks>
    /// <para>
    /// 一格上最多有两个要看的对象（典型是「英雄站在城堡入口上」）：`getMainObjectType( false )` 取英雄脚下的那个，
    /// `getMainObjectType( true )` 取缓存的那个，两者不同就两个都画。
    /// </para>
    /// <para>
    /// 与参考的两处差异：循环边界用 `world.Width`/`world.Height`（参考外层写成了 `worldWidth`、内层写成 `worldHeight`，
    /// 方图看不出来，端口不跟着写错）；参考里 `icnFlagsBase == ICN::FLAG32` 时才用的 `2 * icnIndex + 1` 是死分支
    /// （四档的旗子表是 VWFLAG4/VWFLAG6/VWFLAG12/VWFLAG12），端口不保留。
    /// </para>
    /// </remarks>
    public static void DrawObjectsIcons(PlayerColor color, ViewWorldMode mode, ViewWorldMapCache cache, World world, IViewWorldSpriteSource sprites)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sprites);

        bool revealAll = mode == ViewWorldMode.ViewAll;
        bool revealMines = revealAll || mode == ViewWorldMode.ViewMines;
        bool revealHeroes = revealAll || mode == ViewWorldMode.ViewHeroes;
        bool revealTowns = revealAll || mode == ViewWorldMode.ViewTowns;
        bool revealArtifacts = revealAll || mode == ViewWorldMode.ViewArtifacts;
        bool revealResources = revealAll || mode == ViewWorldMode.ViewResources;

        Debug.Assert(world.Width > 0 && world.Height > 0, "The world has no tiles.");

        for (int posY = 0; posY < world.Height; ++posY)
        {
            for (int posX = 0; posX < world.Width; ++posX)
            {
                Tile tile = world.GetTile(posX, posY);

                Mp2ObjectType underHero = tile.GetMainObjectType(world, false);
                Mp2ObjectType cached = tile.GetMainObjectType(world, true);
                int objectCount = underHero == cached ? 1 : 2;

                for (int i = 0; i < objectCount; ++i)
                {
                    // The reference compares against MP2::OBJ_*, i.e. the action values (OBJ_CASTLE = 206 and
                    // not 35). The port's tiles may hold either form — the map loader keeps the value the file
                    // has and a few call sites write the non-action twin — so strip the action bit first;
                    // otherwise a town entrance whose type is 35 would draw no flags.
                    switch (Mp2ObjectTypes.NonActionType(i == 0 ? underHero : cached))
                    {
                    case Mp2ObjectType.Hero:
                        if (!revealHeroes && tile.IsFog(color))
                        {
                            break;
                        }

                        Hero? hero = world.Heroes.GetAtMapIndex(tile.Index);
                        if (hero is null)
                        {
                            break;
                        }

                        {
                            uint colorOffset = ViewWorldZoomLevels.ColorToOffsetIcn(hero.Color);

                            // Do not render an unknown color.
                            if (colorOffset != ViewWorldZoomLevels.UnknownIndex)
                            {
                                RenderIcon(cache, sprites, 7 + colorOffset, posX, posY);
                            }
                        }

                        break;

                    case Mp2ObjectType.Castle:
                        if (!revealTowns && tile.IsFog(color))
                        {
                            break;
                        }

                        {
                            var castle = world.GetCastleEntrance(tile.GetCenter(world));
                            if (castle is null)
                            {
                                break;
                            }

                            uint colorOffset = ViewWorldZoomLevels.ColorToOffsetIcn(castle.Color);

                            // Do not render an unknown color.
                            if (colorOffset != ViewWorldZoomLevels.UnknownIndex)
                            {
                                RenderCastleFlags(cache, sprites, colorOffset, posX, posY);
                            }
                        }

                        break;

                    case Mp2ObjectType.AlchemistLab:
                    case Mp2ObjectType.Mine:
                    case Mp2ObjectType.Sawmill:
                        if (!revealMines && tile.IsFog(color))
                        {
                            break;
                        }

                        {
                            uint colorOffset = ViewWorldZoomLevels.ColorToOffsetIcn(tile.GetColor(world));

                            // Do not render an unknown color.
                            if (colorOffset != ViewWorldZoomLevels.UnknownIndex)
                            {
                                Funds funds = tile.GetDailyIncomeObjectResources(world);

                                // 多格矿场的每个邻格也是 `OBJ_MINE`，但只有带车的那一格知道资源；参考靠
                                // 对象信息给整个对象的所有格子写同一份元数据，端口按格读 MP2 做不到，所以
                                // 没有资源的格子不画图标（否则会画出四五个重复的图标）。
                                if (funds.GetValidItemsCount() != 1)
                                {
                                    break;
                                }

                                RenderResourceIcon(cache, sprites, colorOffset, funds.GetFirstValidResource().Type, posX, posY);
                            }
                        }

                        break;

                    case Mp2ObjectType.Artifact:
                        if (revealArtifacts || !tile.IsFog(color))
                        {
                            RenderIcon(cache, sprites, 14, posX, posY);
                        }

                        break;

                    case Mp2ObjectType.Resource:
                        if (revealResources || !tile.IsFog(color))
                        {
                            Funds funds = tile.GetFundsFromTile(world);

                            // 参考 `view_world.cpp:392` 直接取 `metadata[0]`，默认就是 0；端口按格读 MP2，
                            // 多格资源堆只有一格带资源，所以没有资源就当没有，不画图标也不断言。
                            if (funds.GetValidItemsCount() != 1)
                            {
                                break;
                            }

                            RenderResourceIcon(cache, sprites, 13, funds.GetFirstValidResource().Type, posX, posY);
                        }

                        break;

                    default:
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Two player flags on the left and the right of a castle entrance, <c>renderCastleFlags</c> of
    /// <c>view_world.cpp:331-346</c>.
    /// </summary>
    private static void RenderCastleFlags(ViewWorldMapCache cache, IViewWorldSpriteSource sprites, uint icnIndex, int posX, int posY)
    {
        for (int level = 0; level < cache.ZoomLevelCount; ++level)
        {
            var zoomLevel = (ZoomLevel)level;
            int tileSize = ViewWorldZoomLevels.TileSize(zoomLevel);

            Sprite? sprite = sprites.GetSprite(ViewWorldZoomLevels.FlagIcn(zoomLevel), (int)icnIndex);
            if (sprite is null)
            {
                continue;
            }

            int dstx = (posX * tileSize) + ((tileSize - sprite.Width) / 2);
            int dsty = (posY * tileSize) + ((tileSize - sprite.Height) / 2) + 1;

            PixelBuffer image = cache.GetImage(zoomLevel);
            ImageOps.BlitSprite(image, new Point(dstx + tileSize, dsty), sprite);
            // We place a second flag, flipped horizontally.
            ImageOps.BlitSprite(image, new Point(dstx - tileSize, dsty), sprite.Flip(true, false));
        }
    }

    /// <summary>
    /// A hero or artifact icon centred on a tile, <c>renderIcon</c> of <c>view_world.cpp:349-358</c>.
    /// </summary>
    private static void RenderIcon(ViewWorldMapCache cache, IViewWorldSpriteSource sprites, uint icnIndex, int posX, int posY)
    {
        for (int level = 0; level < cache.ZoomLevelCount; ++level)
        {
            var zoomLevel = (ZoomLevel)level;
            int tileSize = ViewWorldZoomLevels.TileSize(zoomLevel);
            int dstx = (posX * tileSize) + (tileSize / 2);
            int dsty = (posY * tileSize) + (tileSize / 2);

            Sprite? sprite = sprites.GetSprite(ViewWorldZoomLevels.TerrainIcn(zoomLevel), (int)icnIndex);
            if (sprite is null)
            {
                continue;
            }

            ImageOps.BlitSprite(cache.GetImage(zoomLevel), new Point(dstx - (sprite.Width / 2), dsty - (sprite.Height / 2)), sprite);
        }
    }

    /// <summary>
    /// A resource or mine icon with the resource letter inside, <c>renderResourceIcon</c> of
    /// <c>view_world.cpp:361-379</c>.
    /// </summary>
    private static void RenderResourceIcon(ViewWorldMapCache cache, IViewWorldSpriteSource sprites, uint icnIndex, int resource, int posX, int posY)
    {
        uint letterIndex = ViewWorldZoomLevels.ResourceToOffsetIcn(resource);

        if (letterIndex == ViewWorldZoomLevels.UnknownIndex)
        {
            // This is an unknown resource.
            return;
        }

        for (int level = 0; level < cache.ZoomLevelCount; ++level)
        {
            var zoomLevel = (ZoomLevel)level;
            int tileSize = ViewWorldZoomLevels.TileSize(zoomLevel);
            int dstx = (posX * tileSize) + (tileSize / 2);
            int dsty = (posY * tileSize) + (tileSize / 2);

            PixelBuffer image = cache.GetImage(zoomLevel);

            Sprite? icon = sprites.GetSprite(ViewWorldZoomLevels.TerrainIcn(zoomLevel), (int)icnIndex);
            if (icon is not null)
            {
                ImageOps.BlitSprite(image, new Point(dstx - (icon.Width / 2), dsty - (icon.Height / 2)), icon);
            }

            Sprite? letter = sprites.GetSprite(ViewWorldZoomLevels.LetterIcn(zoomLevel), (int)letterIndex);
            if (letter is not null)
            {
                ImageOps.BlitSprite(image, new Point(dstx - (letter.Width / 2), dsty - (letter.Height / 2)), letter);
            }
        }
    }

    /// <summary>
    /// Blacks out a strip outside the map, <c>fillBlack</c> of <c>view_world.cpp:286-297</c>.
    /// </summary>
    /// <remarks>
    /// 参考用 `assert` 要求这块区域完整落在屏幕里；端口改成与屏幕求交后跳过空区域，因为窄屏（或调试档）下
    /// 右侧黑条的宽度会算成负数，按参考会在 release 构建里写坏内存。
    /// </remarks>
    private static void FillBlack(PixelBuffer display, int x, int y, int width, int height)
    {
        Rect bar = new Rect(x, y, width, height).Intersect(display.Bounds);

        if (bar.IsEmpty)
        {
            return;
        }

        ImageOps.FillRect(display, bar, 0);
    }
}
