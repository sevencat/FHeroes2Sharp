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
        PixelBuffer image = Scale(colors, map.Width, map.Height, zoom);
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
    /// The rectangle that marks <paramref name="visibleTiles"/> on the panel, in panel pixels and
    /// relative to the top left corner of the panel. Empty when the viewport shows the whole map, which
    /// is when the reference hides the cursor instead of drawing a frame around everything.
    /// </summary>
    public Rect CursorArea(in Rect visibleTiles)
    {
        if (visibleTiles.Width >= Width && visibleTiles.Height >= Height)
        {
            return default;
        }

        // The reference clips the region of interest to the world before it scales it.
        Rect clamped = visibleTiles.Clip(new Rect(0, 0, Width, Height));
        if (clamped.IsEmpty)
        {
            return default;
        }

        int left = ScaleCoordinate(clamped.X);
        int top = ScaleCoordinate(clamped.Y);
        int right = ScaleCoordinate(clamped.Right);
        int bottom = ScaleCoordinate(clamped.Bottom);

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

        Rect area = new(origin.X + cursor.X, origin.Y + cursor.Y, cursor.Width, cursor.Height);
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

    /// <summary>Maps a tile coordinate onto the panel, `tile * area.width / world.width` of the reference.</summary>
    private int ScaleCoordinate(int tile) => (int)(tile * Zoom);

    private static PixelBuffer Scale(byte[] colors, int width, int height, double zoom)
    {
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
