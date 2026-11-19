using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The window that shows the whole map, <c>ViewWorld::ViewWorldWindow()</c> of <c>view_world.cpp:589-795</c>:
/// the layout, the fade rectangle, the initial view, the input of the message loop and the chrome.
/// </summary>
/// <remarks>
/// <para>
/// 参考的这个函数是「一个完整的模态循环」：淡出、建雷达、算初始中心、建缓存、画地图与侧栏、进出事件循环、
/// 淡入、还原。端口把它拆成两半：<b>本类管状态与几何</b>（窗口里各块的位置、可见地图的缩放视图、雷达、
/// 拖动/滚轮/点击的换算、退出时记住档位），<b>调用方管循环</b>（收事件、调方法、把 <see cref="Zoom"/> 换档后的
/// 画面重画一次）。这样窗口能在没有窗口句柄的情况下被测，App 层也只负责转发。
/// </para>
/// <para>
/// <b>缓存由调用方建</b>：<see cref="ViewWorldMapCache"/> 需要一层「把一块 18×18 的地图画进缓冲」的渲染器，
/// 那个渲染器（地形 + 迷雾 + 城镇/英雄图标）是独立的一批工作；本类只接收已经建好的缓存，并在
/// <see cref="DrawWorld"/> 里把它贴出去。因此这里没有参考里的 <c>CacheForMapWithResources</c> 构造。
/// </para>
/// <para>
/// <b>按钮的位置来自精灵尺寸</b>（参考的 <c>Button.area()</c> 就是精灵尺寸），所以构造时要拿到
/// <see cref="IViewWorldSpriteSource"/>；缺失的精灵让对应的按钮区为空，点击自然落空，而不是抛异常。
/// </para>
/// </remarks>
public sealed class ViewWorldWindow
{
    /// <summary>Height of the display the reference draws the interface for, <c>Display::DEFAULT_HEIGHT</c>.</summary>
    public const int DefaultDisplayHeight = 480;

    /// <summary>Rows of the side panel sprite that are drawn before the stone filler, <c>cutHeight = 275</c>.</summary>
    public const int SidePanelCutHeight = 275;

    /// <summary>First row of <c>STONBAK</c> that is repeated down a tall display, <c>startY = 11</c>.</summary>
    public const int StoneStartY = 11;

    /// <summary>Rows of <c>STONBAK</c> copied per repeat, <c>copyHeight = 46</c>.</summary>
    public const int StoneRepeatHeight = 46;

    /// <summary>Horizontal inset of the two buttons inside the radar strip, the <c>16</c> of the reference.</summary>
    public const int ButtonInsetPx = 16;

    /// <summary>Distance from the radar to the zoom button, the <c>128</c> of the reference.</summary>
    public const int ZoomButtonOffsetPx = 128;

    /// <summary>Distance from the radar to the exit button, the <c>236</c> of the reference.</summary>
    public const int ExitButtonOffsetPx = 236;

    private readonly ViewWorldMapCache _cache;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly ViewWorldRadar _radar;
    private readonly IcnType _zoomIcon;
    private readonly IcnType _exitIcon;
    private readonly int _displayWidth;
    private readonly int _displayHeight;

    private bool _isDragging;
    private Point _dragStart;
    private Point _dragCenter;

    /// <summary>Opens the window, <c>ViewWorldWindow()</c> up to its first draw.</summary>
    /// <param name="world">The world to show; also the source of the radar colours.</param>
    /// <param name="mode">Which objects to reveal.</param>
    /// <param name="friends">The colours the fog may be seen through, <c>Players::FriendColors()</c>.</param>
    /// <param name="zoomLevel">The zoom level the previous visit ended on, <c>Settings::ViewWorldZoomLevel()</c>.</param>
    /// <param name="visibleScreenInPixels">The map window on the screen, <c>GameArea::GetROI()</c>.</param>
    /// <param name="worldMapRoiInTiles">The tiles the adventure map shows, <c>GameArea::GetVisibleTileROI()</c>.</param>
    /// <param name="cache">The map cache, already built for <paramref name="mode"/>.</param>
    /// <param name="displayWidth">Width of the screen.</param>
    /// <param name="displayHeight">Height of the screen.</param>
    /// <param name="sprites">The archive reader of the side panel and the buttons.</param>
    /// <param name="isEvilInterface">Whether the evil artwork is selected.</param>
    /// <param name="isHideInterface">Whether the adventure map hides its interface.</param>
    /// <param name="zoomLevelCount">Three levels, or four with the 1:1 debug level.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The cache was built for another map or another number of levels.</exception>
    public ViewWorldWindow(
        World world,
        ViewWorldMode mode,
        PlayerColor friends,
        ZoomLevel zoomLevel,
        Rect visibleScreenInPixels,
        Rect worldMapRoiInTiles,
        ViewWorldMapCache cache,
        int displayWidth,
        int displayHeight,
        IViewWorldSpriteSource sprites,
        bool isEvilInterface = false,
        bool isHideInterface = false,
        int zoomLevelCount = ViewWorldZoomLevels.RegularZoomLevels)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayHeight);

        // The reference keeps the level it read from the settings; a level the smaller table cannot hold is
        // clamped instead of throwing, because the settings file may outlive a change of the table.
        if ((int)zoomLevel >= zoomLevelCount)
        {
            zoomLevel = (ZoomLevel)(zoomLevelCount - 1);
        }

        if (cache.ZoomLevelCount != zoomLevelCount)
        {
            throw new ArgumentException(
                $"The cache has {cache.ZoomLevelCount} zoom levels but the window uses {zoomLevelCount}.", nameof(cache));
        }

        _cache = cache;
        _sprites = sprites;
        _displayWidth = displayWidth;
        _displayHeight = displayHeight;
        Mode = mode;
        Friends = friends;
        IsEvilInterface = isEvilInterface;
        IsHideInterface = isHideInterface;
        VisibleArea = visibleScreenInPixels;

        Zoom = new ViewWorldZoom(
            zoomLevel, InitialCenter(world, worldMapRoiInTiles, visibleScreenInPixels, zoomLevel), visibleScreenInPixels,
            zoomLevelCount, world.Width, world.Height);

        _radar = new ViewWorldRadar(world);
        _radar.RedrawObjects(friends, mode);

        _zoomIcon = isEvilInterface ? IcnType.Lgndxtre : IcnType.Lgndxtra;
        _exitIcon = isEvilInterface ? IcnType.ButtonViewworldExitEvil : IcnType.ButtonViewworldExitGood;

        ZoomButtonArea = ButtonArea(new Point(ButtonX, ButtonY(ZoomButtonOffsetPx)), _zoomIcon);
        ExitButtonArea = ButtonArea(new Point(ButtonX, ButtonY(ExitButtonOffsetPx)), _exitIcon);
    }

    /// <summary>Whether the evil artwork is selected.</summary>
    public bool IsEvilInterface { get; }

    /// <summary>Whether the adventure map hides its interface.</summary>
    public bool IsHideInterface { get; }

    /// <summary>What the window reveals.</summary>
    public ViewWorldMode Mode { get; }

    /// <summary>The colours the fog may be seen through.</summary>
    public PlayerColor Friends { get; }

    /// <summary>The map window on the screen, in screen pixels.</summary>
    public Rect VisibleArea { get; }

    /// <summary>The zoom, centre and visible rectangles of the map.</summary>
    public ViewWorldZoom Zoom { get; }

    /// <summary>The radar panel and its cursor frame.</summary>
    public ViewWorldRadar Radar => _radar;

    /// <summary>The map cache this window draws, built for <see cref="Mode"/>.</summary>
    public ViewWorldMapCache Cache => _cache;

    /// <summary>Whether a drag of the map is in progress.</summary>
    public bool IsDragging => _isDragging;

    /// <summary>Area the reference fades through, <c>fadeRoi</c> of <c>view_world.cpp:600-608</c>.</summary>
    /// <remarks>
    /// 接口显示时只淡出地图区（左边 16、上边 16、右边扣掉边框与雷达、下边只扣边框），隐藏接口时淡出整屏。
    /// App 用它决定哪块画面要做淡入淡出，模型本身不画过渡帧。
    /// </remarks>
    public Rect FadeArea => IsHideInterface
        ? new Rect(0, 0, _displayWidth, _displayHeight)
        : new Rect(
            ViewWorldRenderer.BorderWidthPx,
            ViewWorldRenderer.BorderWidthPx,
            _displayWidth - (3 * ViewWorldRenderer.BorderWidthPx) - ViewWorldRenderer.RadarWidthPx,
            _displayHeight - (2 * ViewWorldRenderer.BorderWidthPx));

    /// <summary>The radar panel in screen pixels, the <c>Radar( radar, display )</c> constructor of the reference.</summary>
    public Rect RadarArea => new(
        _displayWidth - ViewWorldRenderer.BorderWidthPx - ViewWorldRenderer.RadarWidthPx,
        ViewWorldRenderer.BorderWidthPx,
        ViewWorldRenderer.RadarWidthPx,
        ViewWorldRenderer.RadarWidthPx);

    /// <summary>The zoom button, empty when its artwork is missing.</summary>
    public Rect ZoomButtonArea { get; }

    /// <summary>The exit button, empty when its artwork is missing.</summary>
    public Rect ExitButtonArea { get; }

    /// <summary>
    /// Blits the cached map into <paramref name="display"/>, <c>DrawWorld( currentROI, cache, visibleScreenInPixels )</c>.
    /// </summary>
    /// <param name="display">The screen buffer.</param>
    public void DrawWorld(PixelBuffer display) =>
        ViewWorldRenderer.DrawWorld(Zoom, _cache, display, VisibleArea);

    /// <summary>
    /// Draws the radar with its cursor frame, <c>radar.RedrawForViewWorld( roi, mode, renderMapObjects )</c>
    /// (<c>interface_radar.cpp:253-267</c>).
    /// </summary>
    /// <param name="display">The screen buffer.</param>
    /// <param name="renderMapObjects">
    /// Whether to re-colour the whole panel first. The reference passes <see langword="true"/> once when the
    /// window opens and <see langword="false"/> for every later move, where only the frame changes.
    /// </param>
    public void DrawRadar(PixelBuffer display, bool renderMapObjects)
    {
        if (renderMapObjects)
        {
            _radar.RedrawObjects(Friends, Mode);
        }

        _radar.Draw(display, new Point(RadarArea.X, RadarArea.Y), Zoom.RoiInTiles);
    }

    /// <summary>
    /// Draws the side panel and the two buttons, <c>drawViewWorldSprite()</c> followed by the two <c>Button::draw()</c>
    /// calls of <c>view_world.cpp:682-697</c>.
    /// </summary>
    /// <param name="display">The screen buffer.</param>
    /// <param name="zoomPressed">Whether the zoom button is held down, which switches its artwork to index 1.</param>
    /// <param name="exitPressed">Whether the exit button is held down.</param>
    public void DrawChrome(PixelBuffer display, bool zoomPressed = false, bool exitPressed = false)
    {
        ArgumentNullException.ThrowIfNull(display);

        DrawSidePanel(display);
        DrawButton(display, ZoomButtonArea, _zoomIcon, zoomPressed);
        DrawButton(display, ExitButtonArea, _exitIcon, exitPressed);
    }

    /// <summary>Whether <paramref name="point"/> is on the radar.</summary>
    /// <param name="point">A screen point.</param>
    /// <returns><see langword="true"/> inside the radar panel.</returns>
    public bool IsInRadar(Point point) => RadarArea.Contains(point);

    /// <summary>Whether <paramref name="point"/> is on the zoom button.</summary>
    /// <param name="point">A screen point.</param>
    /// <returns><see langword="true"/> inside the usable button area.</returns>
    public bool IsInZoomButton(Point point) => ZoomButtonArea.Contains(point);

    /// <summary>Whether <paramref name="point"/> is on the exit button.</summary>
    /// <param name="point">A screen point.</param>
    /// <returns><see langword="true"/> inside the usable button area.</returns>
    public bool IsInExitButton(Point point) => ExitButtonArea.Contains(point);

    /// <summary>Whether <paramref name="point"/> is on the map window.</summary>
    /// <param name="point">A screen point.</param>
    /// <returns><see langword="true"/> inside <see cref="VisibleArea"/>.</returns>
    public bool IsInVisibleArea(Point point) => VisibleArea.Contains(point);

    /// <summary>
    /// Starts dragging the map, the <c>else</c> branch of <c>view_world.cpp:741-745</c>.
    /// </summary>
    /// <param name="point">Where the left button went down.</param>
    /// <returns><see langword="true"/> when the drag started.</returns>
    public bool Press(Point point)
    {
        if (!VisibleArea.Contains(point))
        {
            _isDragging = false;

            return false;
        }

        _isDragging = true;
        _dragStart = point;
        _dragCenter = Zoom.Center;

        return true;
    }

    /// <summary>
    /// Moves the view by the drag, <c>view_world.cpp:734-740</c>.
    /// </summary>
    /// <param name="point">The cursor now.</param>
    /// <returns><see langword="true"/> when the centre moved and the screen has to be redrawn.</returns>
    /// <remarks>
    /// 参考按 `(新鼠标 - 旧鼠标) * 32 / 缩放` 反向推中心；两个方向都贴着边时 <see cref="ViewWorldZoom.ChangeCenter"/>
    /// 会夹住，返回 <see langword="false"/>，调用方因此不必重画。鼠标离开地图窗口时拖动结束（参考在同一帧里把
    /// <c>isDrag</c> 清掉）。
    /// </remarks>
    public bool DragTo(Point point)
    {
        if (!_isDragging)
        {
            return false;
        }

        if (!VisibleArea.Contains(point))
        {
            _isDragging = false;

            return false;
        }

        int tileSize = ViewWorldZoomLevels.TileSize(Zoom.ZoomLevel);

        return Zoom.ChangeCenter(new Point(
            _dragCenter.X - ((point.X - _dragStart.X) * ViewWorldZoomLevels.TileWidthPx / tileSize),
            _dragCenter.Y - ((point.Y - _dragStart.Y) * ViewWorldZoomLevels.TileWidthPx / tileSize)));
    }

    /// <summary>Ends the drag, <c>isDrag = false</c> of <c>view_world.cpp:760-762</c>.</summary>
    public void Release() => _isDragging = false;

    /// <summary>One wheel notch up, <c>currentROI.zoomIn( false )</c>.</summary>
    /// <returns><see langword="true"/> when the zoom level changed.</returns>
    public bool WheelUp() => Zoom.ZoomIn(false);

    /// <summary>One wheel notch down, <c>currentROI.zoomOut( false )</c>.</summary>
    /// <returns><see langword="true"/> when the zoom level changed.</returns>
    public bool WheelDown() => Zoom.ZoomOut(false);

    /// <summary>
    /// The zoom button, <c>currentROI.zoomOut( true )</c> of <c>view_world.cpp:727-729</c>.
    /// </summary>
    /// <returns><see langword="true"/> when the zoom level changed.</returns>
    /// <remarks>按钮把「缩小」循环起来：最远的一档再按一次回到最近的档位。</remarks>
    public bool ClickZoom() => Zoom.ZoomOut(true);

    /// <summary>
    /// A left click on the radar, <c>radar.QueueEventProcessingForWorldView( currentROI )</c>
    /// (<c>interface_radar.cpp:501-534</c>).
    /// </summary>
    /// <param name="point">A screen point.</param>
    /// <returns><see langword="true"/> when the centre moved.</returns>
    public bool ClickRadar(Point point)
    {
        if (!RadarArea.Contains(point))
        {
            return false;
        }

        return _radar.CenterOn(Zoom, new Point(point.X - RadarArea.X, point.Y - RadarArea.Y));
    }

    /// <summary>
    /// Closes the window and answers the level to remember, <c>conf.SetViewWorldZoomLevel( currentROI.getZoomLevel() )</c>
    /// of <c>view_world.cpp:771-772</c>.
    /// </summary>
    /// <returns>The zoom level the window was left at.</returns>
    public ZoomLevel Close()
    {
        _isDragging = false;

        return Zoom.ZoomLevel;
    }

    /// <summary>
    /// Where the view starts, <c>view_world.cpp:633-642</c>: on the adventure map centre, or on the map centre when
    /// the whole map fits at this zoom level.
    /// </summary>
    /// <param name="world">The world to show.</param>
    /// <param name="worldMapRoiInTiles">The tiles the adventure map shows.</param>
    /// <param name="visibleScreenInPixels">The map window on the screen.</param>
    /// <param name="zoomLevel">The starting zoom level.</param>
    /// <returns>The centre in map pixels.</returns>
    /// <remarks>
    /// 「整张图能放下就居中」这一步必须在这里做：<see cref="ViewWorldZoom.ChangeCenter"/> 会把中心夹到
    /// `[宽/2, 地图宽 - 宽/2]`，而整图档位下这两个端点相等，夹完自然就是地图中心，但参考显式写了这一步，
    /// 端口照写以免依赖夹取的副作用。
    /// </remarks>
    private static Point InitialCenter(World world, Rect worldMapRoiInTiles, Rect visibleScreenInPixels, ZoomLevel zoomLevel)
    {
        int tileSize = ViewWorldZoomLevels.TileSize(zoomLevel);

        if ((world.Width * tileSize) <= visibleScreenInPixels.Width
            && (world.Height * tileSize) <= visibleScreenInPixels.Height)
        {
            return new Point(
                world.Width * ViewWorldZoomLevels.TileWidthPx / 2,
                world.Height * ViewWorldZoomLevels.TileWidthPx / 2);
        }

        return new Point(
            (worldMapRoiInTiles.X * ViewWorldZoomLevels.TileWidthPx) + ((visibleScreenInPixels.Width + ViewWorldZoomLevels.TileWidthPx) / 2),
            (worldMapRoiInTiles.Y * ViewWorldZoomLevels.TileWidthPx) + ((visibleScreenInPixels.Height + ViewWorldZoomLevels.TileWidthPx) / 2));
    }

    private int ButtonX => _displayWidth - ViewWorldRenderer.RadarWidthPx + ButtonInsetPx;

    private static int ButtonY(int offset) =>
        (2 * ViewWorldRenderer.BorderWidthPx) + ViewWorldRenderer.RadarWidthPx + offset;

    private Rect ButtonArea(Point position, IcnType icn)
    {
        Sprite? sprite = _sprites.GetSprite(icn, 0);

        return sprite is null ? Rect.Empty : new Rect(position, new Size(sprite.Width, sprite.Height));
    }

    private void DrawButton(PixelBuffer display, in Rect area, IcnType icn, bool pressed)
    {
        if (area.IsEmpty)
        {
            return;
        }

        Sprite? sprite = _sprites.GetSprite(icn, pressed ? 1 : 0);
        if (sprite is null)
        {
            return;
        }

        ImageOps.Blit(display, new Point(area.X, area.Y), sprite.Image, sprite.Image.Bounds);
    }

    /// <summary>
    /// The side panel of the mode, <c>drawViewWorldSprite()</c> of <c>view_world.cpp:482-505</c>: the top 275 rows of
    /// the sprite, then - on a display taller than 480 pixels - repeating rows of the stone filler, then the rest.
    /// </summary>
    /// <param name="display">The screen buffer.</param>
    private void DrawSidePanel(PixelBuffer display)
    {
        Sprite? panel = _sprites.GetSprite(ViewWorldRenderer.GetSpriteResource(Mode, IsEvilInterface), 0);
        if (panel is null)
        {
            return;
        }

        int dstX = _displayWidth - panel.Width - ViewWorldRenderer.BorderWidthPx;
        int dstY = (2 * ViewWorldRenderer.BorderWidthPx) + ViewWorldRenderer.RadarWidthPx;
        int cutHeight = Math.Min(SidePanelCutHeight, panel.Height);

        ImageOps.Blit(display, new Point(dstX, dstY), panel.Image, new Rect(0, 0, panel.Width, cutHeight));
        dstY += cutHeight;

        if (_displayHeight > DefaultDisplayHeight)
        {
            Sprite? stone = _sprites.GetSprite(IsEvilInterface ? IcnType.Stonbake : IcnType.Stonback, 0);

            if (stone is not null)
            {
                // The reference leaves the bottom border out of the repeat area and lets the filler take the rest.
                int repeatHeight = Math.Max(
                    0, _displayHeight - ViewWorldRenderer.BorderWidthPx - dstY - (panel.Height - cutHeight));
                int count = repeatHeight / StoneRepeatHeight;

                for (int i = 0; i < count; ++i)
                {
                    CopyStone(display, dstX, dstY, stone, StoneRepeatHeight);
                    dstY += StoneRepeatHeight;
                }

                int tail = repeatHeight % StoneRepeatHeight;
                CopyStone(display, dstX, dstY, stone, tail);
                dstY += tail;
            }
        }

        ImageOps.Blit(
            display, new Point(dstX, dstY), panel.Image, new Rect(0, cutHeight, panel.Width, panel.Height - cutHeight));
    }

    private static void CopyStone(PixelBuffer display, int dstX, int dstY, Sprite stone, int height)
    {
        if (height <= 0)
        {
            return;
        }

        ImageOps.Blit(display, new Point(dstX, dstY), stone.Image, new Rect(0, StoneStartY, stone.Width, height));
    }
}
