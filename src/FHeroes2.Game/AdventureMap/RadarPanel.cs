using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The panel art the radars share: the palette indices of <c>interface_radar.cpp</c>, the
/// nearest-neighbour scale onto the <see cref="MapFrame.PanelSize"/> square panel and the dotted
/// frame that marks the visible tiles on it.
/// </summary>
/// <remarks>
/// <para>
/// 参考里只有一份 <c>Radar</c>，它既给冒险地图着色（<c>GetPaletteIndexFromGround</c> 一路读 <c>Maps::Tiles</c>），
/// 也给世界视图着色。端口里两份雷达各自按自己的模型着色（冒险地图与世界视图读 <c>World</c>，编辑器的雷达读 MP2
/// 图块），上色规则不同，但「每格一个调色板下标 → 放大到 144 见方 → 画虚线框」这一半是同一份，于是抽成这个静态类，
/// 让它们不必互相引用。
/// </para>
/// </remarks>
public static class RadarPanel
{
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
    public const int HillShadeOffset = 3;

    /// <summary>Step of the dotted border of the cursor, <c>DrawBorder(..., 6)</c> of the reference.</summary>
    private const int CursorSkipFactor = 6;

    /// <summary>The palette index of <paramref name="ground"/>, <c>GetPaletteIndexFromGround</c>.</summary>
    /// <param name="ground">The ground of a tile.</param>
    /// <returns>A palette index; black for the grounds the reference does not name.</returns>
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
    /// <param name="color">The kingdom colour of a player.</param>
    /// <returns>A palette index; white for the colours the reference cannot name.</returns>
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

    /// <summary>
    /// Scales a grid of tile colours onto the <see cref="MapFrame.PanelSize"/> square panel, nearest
    /// neighbour, the pixel loop of <c>Radar::RedrawObjects</c>.
    /// </summary>
    /// <param name="colors">One palette index per tile, row major.</param>
    /// <param name="width">Width of the grid in tiles.</param>
    /// <param name="height">Height of the grid in tiles.</param>
    /// <returns>The panel.</returns>
    public static PixelBuffer ScaleColors(byte[] colors, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(colors);

        double zoom = (double)MapFrame.PanelSize / width;
        PixelBuffer image = new(MapFrame.PanelSize, MapFrame.PanelSize);
        for (int y = 0; y < MapFrame.PanelSize; ++y)
        {
            int tileY = Math.Min((int)(y / zoom), height - 1);
            Span<byte> row = image.Row(y);
            for (int x = 0; x < MapFrame.PanelSize; ++x)
            {
                int tileX = Math.Min((int)(x / zoom), width - 1);
                row[x] = colors[(tileY * width) + tileX];
            }
        }

        return image;
    }

    /// <summary>
    /// The rectangle that marks <paramref name="visibleTiles"/> on a panel of any map size,
    /// <c>Radar::RedrawCursor</c>.
    /// </summary>
    /// <param name="visibleTiles">The tiles the window shows, relative to the top left of the map.</param>
    /// <param name="mapWidth">Width of the map in tiles.</param>
    /// <param name="mapHeight">Height of the map in tiles.</param>
    /// <returns>The frame in panel pixels, or an empty rectangle when the whole map is visible.</returns>
    /// <remarks>
    /// 两份雷达画的是同一个框，但地图尺寸不在同一个地方（冒险地图与世界视图各读各的模型），所以「裁到地图再缩放」这一步
    /// 是静态的。
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

        double zoom = (double)MapFrame.PanelSize / mapWidth;
        int left = (int)(clamped.X * zoom);
        int top = (int)(clamped.Y * zoom);
        int right = (int)(clamped.Right * zoom);
        int bottom = (int)(clamped.Bottom * zoom);

        // A viewport of a single tile can round down to zero pixels; the reference clamps the same way.
        return new Rect(left, top, Math.Max(right - left, 1), Math.Max(bottom - top, 1));
    }

    /// <summary>
    /// Paints the dotted frame of <paramref name="area"/> into <paramref name="target"/>,
    /// <c>fheroes2::DrawBorder</c> with the six pixel step of the radar cursor.
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="area">The frame in the coordinates of <paramref name="target"/>.</param>
    public static void DrawCursorFrame(PixelBuffer target, in Rect area)
    {
        ArgumentNullException.ThrowIfNull(target);

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
}
