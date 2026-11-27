using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The 31 by 31 passability markers of the map editor, <c>PassableViewSurface()</c>
/// (<c>maps_tiles_render.cpp:188-277</c>) and the blit of <c>redrawPassable()</c> (<c>:916-929</c>).
/// </summary>
/// <remarks>
/// <para>
/// One frame per passability mask and action flag: red where a hero may not leave the tile, green where it may. The
/// reference draws the frame into a fresh image and outlines the eight sides of the tile by thirds so that a diagonal
/// connection only colours the part of the edge that belongs to it.
/// </para>
/// <para>
/// 这一份原先长在 UI 的 <c>TileRendering</c> 里，编辑器要画通行性之后搬到了这里：标记只用到 <see cref="Direction"/> 与
/// <see cref="Sprite"/>，与界面无关，而 Game 不能反过来引 UI。
/// </para>
/// </remarks>
public static class PassabilityOverlay
{
    /// <summary>
    /// Colour of the overlay where a hero may not leave the tile, <c>0xBA</c> in the reference.
    /// </summary>
    private const byte BlockedColor = 0xBA;

    /// <summary>Colour of the overlay where a hero may leave the tile.</summary>
    private const byte FreeColor = 90;

    /// <summary>
    /// The green of an action object, which the reference shifts up so that the marker stays readable on the sprite.
    /// </summary>
    private const byte ActionObjectColor = 115;

    /// <summary>Width and height of a marker, <c>31</c> in the reference.</summary>
    public const int Size = 31;

    /// <summary>
    /// Whether a tile gets a marker at all, the condition of <c>redrawPassable()</c>
    /// (<c>maps_tiles_render.cpp:926-928</c>).
    /// </summary>
    /// <param name="passable">Passability mask of the tile.</param>
    /// <param name="objectType">Main object type of the tile.</param>
    /// <param name="isEditor">
    /// Whether the map editor asks. The editor uses the plain action bit, the game only the objects it can visit.
    /// </param>
    /// <returns><see langword="true"/> when the tile needs a marker.</returns>
    public static bool IsMarked(Direction passable, Mp2ObjectType objectType, bool isEditor = true)
    {
        bool isActionObject = IsActionObject(objectType, isEditor);

        return isActionObject || passable != Directions.All;
    }

    /// <summary>
    /// Whether the tile carries an action object, the <c>isActionObject</c> of <c>redrawPassable()</c>.
    /// </summary>
    /// <param name="objectType">Main object type of the tile.</param>
    /// <param name="isEditor">Whether the map editor asks, <c>isOffGameActionObject</c> vs <c>isInGameActionObject</c>.</param>
    /// <returns><see langword="true"/> for an action object.</returns>
    public static bool IsActionObject(Mp2ObjectType objectType, bool isEditor = true) =>
        isEditor
            ? Mp2ObjectTypes.IsActionObject(objectType)
            : Mp2ObjectTypes.IsInGameActionObject(objectType);

    /// <summary>
    /// Draws the marker of one tile, the blit of <c>redrawPassable()</c>.
    /// </summary>
    /// <param name="target">Frame buffer.</param>
    /// <param name="topLeft">Top left pixel of the tile in <paramref name="target"/>.</param>
    /// <param name="passable">Passability mask of the tile.</param>
    /// <param name="isActionObject">Whether the marker shows an action object.</param>
    public static void Draw(PixelBuffer target, Point topLeft, Direction passable, bool isActionObject)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, topLeft, GetSurface(passable, isActionObject));
    }

    /// <summary>Overlays already built, keyed by the passability mask and the action flag.</summary>
    /// <remarks>
    /// The reference keeps the same table of 512 frames alive for the whole run; the markers never change, so they are
    /// built on first use and reused.
    /// </remarks>
    private static readonly Dictionary<(int PassableMask, bool IsActionObject), Sprite> Surfaces = [];

    /// <summary>
    /// The marker frame of one passability mask, <c>PassableViewSurface()</c>
    /// (<c>maps_tiles_render.cpp:188-277</c>).
    /// </summary>
    /// <param name="passable">Passability mask of the tile.</param>
    /// <param name="isActionObject">Whether the tile carries an action object, which shifts the green.</param>
    /// <returns>The overlay, owned by the cache: do not mutate it.</returns>
    public static Sprite GetSurface(Direction passable, bool isActionObject)
    {
        (int PassableMask, bool IsActionObject) key = ((int)passable, isActionObject);

        if (Surfaces.TryGetValue(key, out Sprite? cached))
        {
            return cached;
        }

        byte green = isActionObject ? ActionObjectColor : FreeColor;

        Sprite surface = new(Size, Size);

        // Every pixel starts transparent, so only the frame the reference paints becomes visible.
        if (surface.Transform is { } transform)
        {
            Array.Fill(transform, (byte)1);
        }

        if (passable == Direction.Unknown || passable == Direction.Center)
        {
            ImageOps.DrawBorder(surface.Image, BlockedColor);
        }
        else if (passable == Directions.All)
        {
            ImageOps.DrawBorder(surface.Image, green);
        }
        else
        {
            // Thirds of an edge: the outer thirds belong to the diagonal neighbours, the middle third to the
            // straight one.
            const int quarter = 10;
            const int threeQuarters = 21;

            byte topLeftColor = (passable & Direction.TopLeft) != 0 ? green : BlockedColor;
            byte topColor = (passable & Direction.Top) != 0 ? green : BlockedColor;
            byte topRightColor = (passable & Direction.TopRight) != 0 ? green : BlockedColor;
            byte leftColor = (passable & Direction.Left) != 0 ? green : BlockedColor;
            byte rightColor = (passable & Direction.Right) != 0 ? green : BlockedColor;
            byte bottomLeftColor = (passable & Direction.BottomLeft) != 0 ? green : BlockedColor;
            byte bottomColor = (passable & Direction.Bottom) != 0 ? green : BlockedColor;
            byte bottomRightColor = (passable & Direction.BottomRight) != 0 ? green : BlockedColor;

            Span<byte> pixels = surface.Image.Pixels;

            // Horizontal edges.
            for (int x = 0; x < quarter; ++x)
            {
                pixels[x] = topLeftColor;
                pixels[x + ((Size - 1) * Size)] = bottomLeftColor;
            }

            for (int x = quarter; x < threeQuarters; ++x)
            {
                pixels[x] = topColor;
                pixels[x + ((Size - 1) * Size)] = bottomColor;
            }

            for (int x = threeQuarters; x < Size; ++x)
            {
                pixels[x] = topRightColor;
                pixels[x + ((Size - 1) * Size)] = bottomRightColor;
            }

            // Vertical edges.
            for (int y = 0; y < quarter; ++y)
            {
                pixels[y * Size] = topLeftColor;
                pixels[(Size - 1) + (y * Size)] = topRightColor;
            }

            for (int y = quarter; y < threeQuarters; ++y)
            {
                pixels[y * Size] = leftColor;
                pixels[(Size - 1) + (y * Size)] = rightColor;
            }

            for (int y = threeQuarters; y < Size; ++y)
            {
                pixels[y * Size] = bottomLeftColor;
                pixels[(Size - 1) + (y * Size)] = bottomRightColor;
            }
        }

        if (surface.Transform is { } layer)
        {
            for (int x = 0; x < Size; ++x)
            {
                layer[x] = 0;
                layer[x + ((Size - 1) * Size)] = 0;
            }

            for (int y = 1; y < Size - 1; ++y)
            {
                layer[y * Size] = 0;
                layer[(Size - 1) + (y * Size)] = 0;
            }
        }

        Surfaces[key] = surface;

        return surface;
    }
}
