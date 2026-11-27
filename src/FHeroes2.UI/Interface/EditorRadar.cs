using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The radar of the map editor, the MP2 half of <c>Interface::Radar</c> (<c>gui/interface_radar.cpp</c>):
/// one palette index per tile of an <see cref="Mp2Map"/>, scaled up into the
/// <see cref="MapFrame.PanelSize"/> square panel, plus the rectangle that marks the tiles the viewport
/// currently shows.
/// </summary>
/// <remarks>
/// <para>
/// 参考的编辑器用的是同一个 <c>Radar</c>，只是它读的 <c>world</c> 由编辑器装出来的地图填充。端口没有把编辑器接进
/// <c>World</c>（编辑中的地图本来就是 MP2 图块 + 一份编辑日志，不需要冒险地图那套模型），所以这份雷达读
/// <see cref="Mp2Map"/>：地面色来自 <c>Maps::Ground::getGroundByImageIndex</c> 那半张表，山与树加亮三档，道路用道路色，
/// 英雄用所属颜色。只有面板底图（<c>ADVBORD.ICN</c>）与迷雾不在这里：底图属于 <see cref="MapFrame"/>，编辑器没有迷雾。
/// </para>
/// <para>
/// 调色板、放大与虚线框在 <see cref="RadarPanel"/>，那是三份雷达共用的一半。
/// </para>
/// </remarks>
public sealed class EditorRadar
{
    private readonly byte[] _colors;

    private EditorRadar(int width, int height, double zoom, byte[] colors, PixelBuffer image)
    {
        Width = width;
        Height = height;
        Zoom = zoom;
        _colors = colors;
        Image = image;
    }

    /// <summary>Width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Height of the map in tiles.</summary>
    public int Height { get; }

    /// <summary>
    /// How many panel pixels one tile occupies, <c>PanelSize / Width</c> as in <c>SetZoom</c>.
    /// </summary>
    public double Zoom { get; }

    /// <summary>The scaled panel, <see cref="MapFrame.PanelSize"/> pixels on each side.</summary>
    public PixelBuffer Image { get; }

    /// <summary>
    /// Builds the panel of <paramref name="map"/>: every tile is coloured once, then the whole tile grid
    /// is scaled up by <see cref="Zoom"/>.
    /// </summary>
    /// <param name="map">The map being edited.</param>
    /// <returns>The baked panel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The map has no tiles.</exception>
    public static EditorRadar Build(Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (map.Width <= 0 || map.Height <= 0)
        {
            throw new ArgumentException("The map has no tiles.", nameof(map));
        }

        byte[] colors = new byte[map.Width * map.Height];
        for (int y = 0; y < map.Height; ++y)
        {
            for (int x = 0; x < map.Width; ++x)
            {
                Mp2TileInfo tile = map.Tile(x, y);
                colors[(y * map.Width) + x] = GetTileColor(tile);
            }
        }

        // Heroes are creatures of the map, not of a tile, so they are stamped over the ground after the
        // tile loop: the reference reads them back through `world.GetHeroes( position )`.
        foreach (Mp2MapHero hero in map.Heroes)
        {
            if (hero.X < 0 || hero.Y < 0 || hero.X >= map.Width || hero.Y >= map.Height)
            {
                continue;
            }

            colors[(hero.Y * map.Width) + hero.X] = RadarPanel.GetKingdomColor(hero.Color);
        }

        double zoom = (double)MapFrame.PanelSize / map.Width;
        return new EditorRadar(map.Width, map.Height, zoom, colors, RadarPanel.ScaleColors(colors, map.Width, map.Height));
    }

    /// <summary>
    /// The palette index the panel shows for <paramref name="tile"/>, i.e. the colour half of
    /// <c>Radar::RedrawObjects</c>. Castles and mines are not coloured yet: an MP2 map stores neither
    /// the owner of a castle nor of a mine, so those colours arrive with the kingdom milestone.
    /// </summary>
    /// <param name="tile">A tile of the map being edited.</param>
    /// <returns>A palette index.</returns>
    public static byte GetTileColor(Mp2TileInfo tile)
    {
        if (tile.GroundIcnType == Mp2ObjectIcnType.Road)
        {
            return RadarPanel.RoadColor;
        }

        byte ground = RadarPanel.GetGroundColor(MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex));
        if (ground == RadarPanel.BlackColor)
        {
            return RadarPanel.BlackColor;
        }

        return Mp2ObjectIcnTypes.IsMountain(tile.GroundIcnType) || Mp2ObjectIcnTypes.IsTree(tile.GroundIcnType)
            ? (byte)(ground + RadarPanel.HillShadeOffset)
            : ground;
    }

    /// <summary>The colour of one tile of the map, after the heroes have been stamped in.</summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <returns>A palette index.</returns>
    public byte ColorAt(int x, int y) => _colors[(y * Width) + x];

    /// <summary>
    /// The map tile under <paramref name="panelPoint"/>, a point of the panel relative to its top
    /// left corner: <c>( pt - rect.pos ) * world.size / rect.size</c> of
    /// <c>Radar::QueueEventProcessing</c>, the integer division included.
    /// </summary>
    /// <param name="panelPoint">Point inside the 144x144 panel.</param>
    /// <returns>The tile that a click there centres the map on.</returns>
    /// <remarks>
    /// The result is not clamped here: the reference passes it to <c>GameArea::SetCenter</c>, whose
    /// offset clamp keeps a point outside the panel harmless, and a click on the border of the panel
    /// has to reach the last tile rather than the one before it.
    /// </remarks>
    public Point TileAt(in Point panelPoint) =>
        new(panelPoint.X * Width / MapFrame.PanelSize, panelPoint.Y * Height / MapFrame.PanelSize);

    /// <summary>
    /// The rectangle that marks <paramref name="visibleTiles"/> on the panel, in panel pixels and
    /// relative to the top left corner of the panel. Empty when the viewport shows the whole map, which
    /// is when the reference hides the cursor instead of drawing a frame around everything.
    /// </summary>
    /// <param name="visibleTiles">The tiles the viewport shows.</param>
    /// <returns>The frame, or an empty rectangle.</returns>
    public Rect CursorArea(in Rect visibleTiles) => RadarPanel.CursorArea(visibleTiles, Width, Height);

    /// <summary>
    /// Draws the panel at <paramref name="origin"/> and the cursor of <paramref name="visibleTiles"/>
    /// over it, the two steps of <c>Radar::_redraw</c>.
    /// </summary>
    /// <param name="target">The frame to paint into.</param>
    /// <param name="origin">Top left corner of the panel on the screen.</param>
    /// <param name="visibleTiles">The tiles the viewport shows.</param>
    public void Draw(PixelBuffer target, in Point origin, in Rect visibleTiles)
    {
        ImageOps.Blit(target, origin, Image, Image.Bounds);
        DrawCursor(target, origin, visibleTiles);
    }

    /// <summary>
    /// Draws the dotted frame of <paramref name="visibleTiles"/> into <paramref name="target"/>; the
    /// panel pixels are assumed to be at <paramref name="origin"/> already.
    /// </summary>
    /// <param name="target">The frame to paint into.</param>
    /// <param name="origin">Top left corner of the panel on the screen.</param>
    /// <param name="visibleTiles">The tiles the viewport shows.</param>
    public void DrawCursor(PixelBuffer target, in Point origin, in Rect visibleTiles)
    {
        Rect cursor = CursorArea(visibleTiles);
        if (cursor.IsEmpty)
        {
            return;
        }

        RadarPanel.DrawCursorFrame(target, new Rect(origin.X + cursor.X, origin.Y + cursor.Y, cursor.Width, cursor.Height));
    }
}
