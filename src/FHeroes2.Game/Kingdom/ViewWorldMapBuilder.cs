using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Builds the whole-map cache of the View World window from the adventure map renderer, the
/// <c>CacheForMapWithResources</c> of <c>view_world.cpp:189-268</c> together with the
/// <c>gameArea</c> it draws its blocks with.
/// </summary>
/// <remarks>
/// <para>
/// 参考的快照是「把 `Interface::GameArea` 的 ROI 改成 576×576、逐块设中心、`Redraw`、再缩到每一档」。
/// 端口没有全局的地图区域，改成一次建一个 576×576 的 <see cref="AdventureMapView"/>
/// （<see cref="ViewWorldMapCache"/> 的 <see cref="ViewWorldBlockDrawer"/> 就是它的一个闭包），
/// 尺寸与 18 格一块的换算全在缓存里。
/// </para>
/// <para>
/// <b>英雄永远不画</b>：参考在非 <c>SAVE_WORLD_MAP</c> 构建里把 <c>LEVEL_HEROES</c> 异或掉，英雄改由
/// <see cref="ViewWorldRenderer.DrawObjectsIcons"/> 贴图标；所以这里的视图固定 `heroes: null`。
/// </para>
/// <para>
/// <b>迷雾随模式走</b>：<see cref="ViewWorldDrawLayers.ForMode"/> 已经算出四位开关，全图模式把
/// <see cref="World.Fog"/> 传 <see langword="null"/>，于是整张图都画出来；其余模式保留迷雾，城镇模式再把
/// <c>LEVEL_TOWNS</c> 透下去。
/// </para>
/// <para>
/// 标准地图的边长都是 18 的倍数（36/72/108/144），半块会写到缓存外面去，所以缓存那一层会拒绝；本类不重复校验。
/// </para>
/// </remarks>
public sealed class ViewWorldMapBuilder
{
    private readonly Mp2Map _map;
    private readonly TerrainTileSet _tiles;
    private readonly World _world;
    private readonly IObjectSpriteSource? _objects;
    private readonly IMonsterSpriteSource? _monsters;
    private readonly IBoatSpriteSource? _boats;
    private readonly IFogSpriteSource? _fogSprites;

    /// <summary>Creates a builder over one loaded map.</summary>
    /// <param name="map">The map to render; its size must be a multiple of 18 tiles.</param>
    /// <param name="tiles">The terrain tile set of the map.</param>
    /// <param name="world">The world the fog, the heroes and the monsters come from.</param>
    /// <param name="objects">The object sprites, <see langword="null"/> to draw the ground only.</param>
    /// <param name="monsters">The monster sprites, <see langword="null"/> to leave the monsters out.</param>
    /// <param name="boats">The boat sprites, <see langword="null"/> to leave the boats out.</param>
    /// <param name="fogSprites">The fog sprites, <see langword="null"/> to leave the fog out.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public ViewWorldMapBuilder(
        Mp2Map map,
        TerrainTileSet tiles,
        World world,
        IObjectSpriteSource? objects = null,
        IMonsterSpriteSource? monsters = null,
        IBoatSpriteSource? boats = null,
        IFogSpriteSource? fogSprites = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(world);

        _map = map;
        _tiles = tiles;
        _world = world;
        _objects = objects;
        _monsters = monsters;
        _boats = boats;
        _fogSprites = fogSprites;
    }

    /// <summary>
    /// The 576×576 view one block of the cache is painted with, configured for <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode">The mode the window is in.</param>
    /// <returns>A view whose scroll the block drawer moves before each draw.</returns>
    /// <remarks>
    /// 视图不复用参考的 `gameArea`：这里每次构建都新建一个，构建完就跟着缓存一起被丢掉，不会把冒险地图
    /// 的 ROI 与中心留在全局状态里。
    /// </remarks>
    public AdventureMapView CreateView(ViewWorldMode mode)
    {
        ViewWorldDrawLayers layers = ViewWorldDrawLayers.ForMode(mode);

        return new AdventureMapView(
            _map,
            _tiles,
            new Size(ViewWorldMapCache.BlockSizeInPixels, ViewWorldMapCache.BlockSizeInPixels),
            _objects,
            heroes: null,
            _monsters,
            _boats,
            layers.Fog ? _world.Fog : null,
            _fogSprites,
            _world);
    }

    /// <summary>
    /// Renders the whole map into one image per zoom level, <c>CacheForMapWithResources( viewMode, gameArea, zoomLevels )</c>.
    /// </summary>
    /// <param name="mode">The display mode; it decides the fog and the towns.</param>
    /// <param name="zoomLevelCount">Three levels, or four with the 1:1 debug level.</param>
    /// <returns>The cache, ready for <see cref="ViewWorldWindow"/>.</returns>
    /// <exception cref="ArgumentException">A map side is not a multiple of 18 tiles.</exception>
    public ViewWorldMapCache Build(ViewWorldMode mode, int zoomLevelCount = ViewWorldZoomLevels.RegularZoomLevels)
    {
        AdventureMapView view = CreateView(mode);
        const int half = ViewWorldMapCache.BlockSizeInPixels / 2;

        return new ViewWorldMapCache(
            mode,
            _map.Width,
            _map.Height,
            zoomLevelCount,
            (block, centerInPixels, layers) =>
            {
                // `SetCenterInPixels` keeps the centre inside the map, which the block centres of a
                // standard map always are: they sit 288 pixels inside each 576 pixel block.
                view.ScrollTo(centerInPixels.X - half, centerInPixels.Y - half);
                view.Draw(block, layers.Towns);
            });
    }
}
