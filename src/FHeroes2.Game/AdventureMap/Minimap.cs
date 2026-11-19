using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The minimap of the adventure map, the port of <c>Interface::Radar</c> of the reference: one
/// palette index per tile, scaled up into a square panel, plus the rectangle that marks the tiles the
/// viewport currently shows.
///
/// <para>
/// The reference builds the same thing from the world model: the ground colour from the terrain image
/// index (<c>Maps::Ground::getGroundByImageIndex</c>), a lighter variant for mountains and trees,
/// another for roads, and the kingdom colour for the tiles that hold a hero or a castle. Only the
/// palace panel art (<c>ADVBORD.ICN</c>) and fog are missing; fog is a later milestone, so every tile
/// is drawn as visible here, which is what the reference does before any fog is revealed.
/// </para>
/// </summary>
public sealed class Minimap
{
    /// <summary>Edge length of the panel in pixels, <c>fheroes2::radarWidthPx</c>.</summary>
    public const int PanelSize = 144;

    /// <summary>Palette index of the rectangle that marks the visible tiles, <c>RADARCOLOR</c>.</summary>
    public const byte CursorColor = 0xB5;

    /// <summary>Palette index of the desert ground, <c>COLOR_DESERT</c>.</summary>
    public const byte DesertColor = 0x76;

    /// <summary>Palette index of the snow ground, <c>COLOR_SNOW</c>.</summary>
    public const byte SnowColor = 0x0D;

    /// <summary>Palette index of the swamp ground, <c>COLOR_SWAMP</c>.</summary>
    public const byte SwampColor = 0x68;

    /// <summary>Palette index of the wasteland ground, <c>COLOR_WASTELAND</c>.</summary>
    public const byte WastelandColor = 0xCE;

    /// <summary>Palette index of the beach ground, <c>COLOR_BEACH</c>.</summary>
    public const byte BeachColor = 0x29;

    /// <summary>Palette index of the lava ground, <c>COLOR_LAVA</c>.</summary>
    public const byte LavaColor = 0x20;

    /// <summary>Palette index of the dirt ground, <c>COLOR_DIRT</c>.</summary>
    public const byte DirtColor = 0x36;

    /// <summary>Palette index of the grass ground, <c>COLOR_GRASS</c>.</summary>
    public const byte GrassColor = 0x62;

    /// <summary>Palette index of water, <c>COLOR_WATER</c>.</summary>
    public const byte WaterColor = 0x4D;

    /// <summary>Palette index of a road, <c>COLOR_ROAD</c>.</summary>
    public const byte RoadColor = 0x7A;

    /// <summary>Palette index of an artifact or a resource pile, <c>COLOR_GRAY</c>.</summary>
    public const byte GrayColor = 0x10;

    /// <summary>Palette index of the blue kingdom, <c>COLOR_BLUE</c>.</summary>
    public const byte BlueColor = 0x47;

    /// <summary>Palette index of the green kingdom, <c>COLOR_GREEN</c>.</summary>
    public const byte GreenColor = 0x5D;

    /// <summary>Palette index of the red kingdom, <c>COLOR_RED</c>.</summary>
    public const byte RedColor = 0xBD;

    /// <summary>Palette index of the yellow kingdom, <c>COLOR_YELLOW</c>.</summary>
    public const byte YellowColor = 0x70;

    /// <summary>Palette index of the orange kingdom, <c>COLOR_ORANGE</c>.</summary>
    public const byte OrangeColor = 0xCA;

    /// <summary>Palette index of the purple kingdom, <c>COLOR_PURPLE</c>.</summary>
    public const byte PurpleColor = 0x87;

    /// <summary>Palette index of an unknown ground or object, <c>COLOR_BLACK</c>.</summary>
    public const byte BlackColor = 0x00;

    /// <summary>Palette index of a kingdom the reference cannot name, <c>COLOR_WHITE</c>.</summary>
    public const byte WhiteColor = 0x0A;

    /// <summary>How many shades a mountain or a tree adds to the ground colour.</summary>
    private const byte HillShadeOffset = 3;

    /// <summary>Step of the dotted border of the cursor, <c>DrawBorder(..., 6)</c> of the reference.</summary>
    private const int CursorSkipFactor = 6;

    private readonly byte[] _colors;

    private Minimap(int width, int height, double zoom, byte[] colors, PixelBuffer image)
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

    /// <summary>How many panel pixels one tile occupies, <c>PanelSize / Width</c> as in <c>SetZoom</c>.</summary>
    public double Zoom { get; }

    /// <summary>The scaled panel, <see cref="PanelSize"/> pixels on each side.</summary>
    public PixelBuffer Image { get; }

    /// <summary>
    /// Builds the panel of <paramref name="map"/>: every tile is coloured once, then the whole tile grid
    /// is scaled up by <see cref="Zoom"/>.
    /// </summary>
    public static Minimap Build(Mp2Map map)
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

            colors[(hero.Y * map.Width) + hero.X] = GetKingdomColor(hero.Color);
        }

        double zoom = (double)PanelSize / map.Width;
        PixelBuffer image = ScaleColors(colors, map.Width, map.Height);
        return new Minimap(map.Width, map.Height, zoom, colors, image);
    }

    /// <summary>
    /// The palette index the panel shows for <paramref name="tile"/>, i.e. the colour half of
    /// <c>Radar::RedrawObjects</c>. Castles and mines are not coloured yet: an MP2 map stores neither
    /// the owner of a castle nor of a mine, so those colours arrive with the kingdom milestone.
    /// </summary>
    public static byte GetTileColor(Mp2TileInfo tile)
    {
        if (tile.GroundIcnType == Mp2ObjectIcnType.Road)
        {
            return RoadColor;
        }

        byte ground = GetGroundColor(MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex));
        if (ground == BlackColor)
        {
            return BlackColor;
        }

        return Mp2ObjectIcnTypes.IsMountain(tile.GroundIcnType) || Mp2ObjectIcnTypes.IsTree(tile.GroundIcnType)
            ? (byte)(ground + HillShadeOffset)
            : ground;
    }

    /// <summary>The palette index of <paramref name="ground"/>, <c>GetPaletteIndexFromGround</c>.</summary>
    public static byte GetGroundColor(MapGround ground) => ground switch
    {
        MapGround.Desert => DesertColor,
        MapGround.Snow => SnowColor,
        MapGround.Swamp => SwampColor,
        MapGround.Wasteland => WastelandColor,
        MapGround.Beach => BeachColor,
        MapGround.Lava => LavaColor,
        MapGround.Dirt => DirtColor,
        MapGround.Grass => GrassColor,
        MapGround.Water => WaterColor,
        _ => BlackColor,
    };

    /// <summary>The palette index of <paramref name="color"/>, <c>GetPaletteIndexFromColor</c>.</summary>
    public static byte GetKingdomColor(PlayerColor color) => color switch
    {
        PlayerColor.Blue => BlueColor,
        PlayerColor.Green => GreenColor,
        PlayerColor.Red => RedColor,
        PlayerColor.Yellow => YellowColor,
        PlayerColor.Orange => OrangeColor,
        PlayerColor.Purple => PurpleColor,
        _ => WhiteColor,
    };

    /// <summary>The colour of one tile of the map, after the heroes have been stamped in.</summary>
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
        new(panelPoint.X * Width / PanelSize, panelPoint.Y * Height / PanelSize);

    /// <summary>
    /// The rectangle that marks <paramref name="visibleTiles"/> on the panel, in panel pixels and
    /// relative to the top left corner of the panel. Empty when the viewport shows the whole map, which
    /// is when the reference hides the cursor instead of drawing a frame around everything.
    /// </summary>
    public Rect CursorArea(in Rect visibleTiles) => CursorArea(visibleTiles, Width, Height);

    /// <summary>
    /// The rectangle that marks <paramref name="visibleTiles"/> on a panel of any map size,
    /// <c>Radar::RedrawCursor</c>.
    /// </summary>
    /// <param name="visibleTiles">The tiles the window shows, relative to the top left of the map.</param>
    /// <param name="mapWidth">Width of the map in tiles.</param>
    /// <param name="mapHeight">Height of the map in tiles.</param>
    /// <returns>The frame in panel pixels, or an empty rectangle when the whole map is visible.</returns>
    /// <remarks>
    /// 世界视图的雷达和冒险地图的雷达画的是同一个框，但前者的地图尺寸不在 <see cref="Minimap"/> 实例里
    /// （面板由 <see cref="FHeroes2.Game.Kingdom.ViewWorldRadar"/> 自己着色），所以把「裁到世界再缩放」
    /// 这一步抽成静态的。
    /// </remarks>
    public static Rect CursorArea(in Rect visibleTiles, int mapWidth, int mapHeight)
    {
        if (visibleTiles.Width >= mapWidth && visibleTiles.Height >= mapHeight)
        {
            return default;
        }

        // The reference clips the region of interest to the world before it scales it.
        Rect clamped = visibleTiles.Clip(new Rect(0, 0, mapWidth, mapHeight));
        if (clamped.IsEmpty)
        {
            return default;
        }

        double zoom = (double)PanelSize / mapWidth;
        int left = (int)(clamped.X * zoom);
        int top = (int)(clamped.Y * zoom);
        int right = (int)(clamped.Right * zoom);
        int bottom = (int)(clamped.Bottom * zoom);

        // A viewport of a single tile can round down to zero pixels; the reference clamps the same way.
        return new Rect(left, top, Math.Max(right - left, 1), Math.Max(bottom - top, 1));
    }

    /// <summary>
    /// Draws the panel at <paramref name="origin"/> and the cursor of <paramref name="visibleTiles"/>
    /// over it, the two steps of <c>Radar::_redraw</c>.
    /// </summary>
    public void Draw(PixelBuffer target, in Point origin, in Rect visibleTiles)
    {
        ImageOps.Blit(target, origin, Image, Image.Bounds);
        DrawCursor(target, origin, visibleTiles);
    }

    /// <summary>
    /// Draws the dotted frame of <paramref name="visibleTiles"/> into <paramref name="target"/>; the
    /// panel pixels are assumed to be at <paramref name="origin"/> already.
    /// </summary>
    public void DrawCursor(PixelBuffer target, in Point origin, in Rect visibleTiles)
    {
        Rect cursor = CursorArea(visibleTiles);
        if (cursor.IsEmpty)
        {
            return;
        }

        DrawCursorFrame(target, new Rect(origin.X + cursor.X, origin.Y + cursor.Y, cursor.Width, cursor.Height));
    }

    /// <summary>
    /// Paints the dotted frame of <paramref name="area"/> into <paramref name="target"/>,
    /// <c>fheroes2::DrawBorder</c> with the six pixel step of the radar cursor.
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="area">The frame in the coordinates of <paramref name="target"/>.</param>
    public static void DrawCursorFrame(PixelBuffer target, in Rect area)
    {
        if (area.Width < 2 || area.Height < 2)
        {
            return;
        }

        // `fheroes2::DrawBorder` walks the four sides in one counter and leaves every sixth pixel out,
        // which is what makes the frame look dotted instead of solid.
        int counter = 1;
        for (int x = area.X; x < area.Right; ++x, ++counter)
        {
            if (counter % CursorSkipFactor != 0)
            {
                target[x, area.Y] = CursorColor;
            }
        }

        for (int y = area.Y + 1; y < area.Bottom - 1; ++y, ++counter)
        {
            if (counter % CursorSkipFactor != 0)
            {
                target[area.Right - 1, y] = CursorColor;
            }
        }

        for (int x = area.Right - 1; x >= area.X; --x, ++counter)
        {
            if (counter % CursorSkipFactor != 0)
            {
                target[x, area.Bottom - 1] = CursorColor;
            }
        }

        for (int y = area.Bottom - 2; y > area.Y; --y, ++counter)
        {
            if (counter % CursorSkipFactor != 0)
            {
                target[area.X, y] = CursorColor;
            }
        }
    }

    /// <summary>
    /// Scales a grid of tile colours onto the <see cref="PanelSize"/> square panel, nearest neighbour,
    /// the pixel loop of <c>Radar::RedrawObjects</c>.
    /// </summary>
    /// <param name="colors">One palette index per tile, row major.</param>
    /// <param name="width">Width of the grid in tiles.</param>
    /// <param name="height">Height of the grid in tiles.</param>
    /// <returns>The panel.</returns>
    public static PixelBuffer ScaleColors(byte[] colors, int width, int height)
    {
        double zoom = (double)PanelSize / width;
        PixelBuffer image = new(PanelSize, PanelSize);
        for (int y = 0; y < PanelSize; ++y)
        {
            int tileY = Math.Min((int)(y / zoom), height - 1);
            Span<byte> row = image.Row(y);
            for (int x = 0; x < PanelSize; ++x)
            {
                int tileX = Math.Min((int)(x / zoom), width - 1);
                row[x] = colors[(tileY * width) + tileX];
            }
        }

        return image;
    }
}
