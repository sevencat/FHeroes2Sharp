using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The interface frame of a map screen: <c>ADVBORD.ICN</c> as one 640x480 image that already holds the
/// border, the radar bay and the panel bay, and the rectangles the bays leave for the caller.
/// </summary>
/// <remarks>
/// <para>
/// The reference cuts the same image into borders and patterns
/// (<c>Interface::BorderWindow::Redraw</c>, <c>gui/interface_border.cpp</c>) because its display can be
/// larger than one tile of art; the logical frame here is exactly the size of the image, so one blit is
/// enough. The layout is the one of <c>Interface::EditorInterface::reset</c>: the map sits between a
/// 16 pixel border and the radar column, the radar is in the top right corner and the panel fills the
/// column below it down to the bottom edge.
/// </para>
/// <para>
/// The adventure map of the reference paints the map over the whole display and covers its edges with
/// the border afterwards. Drawing into <see cref="Viewport"/> instead puts the same pixels on the
/// screen, because nothing outside the viewport is visible, and it keeps the scroll arithmetic in map
/// pixels that the viewport holds.
/// </para>
/// </remarks>
public sealed class MapFrame
{
    /// <summary>File of the interface frame, <c>ICN::ADVBORD</c>.</summary>
    public const string AssetName = "ADVBORD.ICN";

    /// <summary>Width of the border between the frame edge and the map, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidth = 16;

    /// <summary>Height of one brick of the panel background, 32 pixels.</summary>
    public const int PanelBrickHeight = 32;

    private readonly Sprite? _chrome;

    /// <summary>
    /// Lays out a frame of <paramref name="size"/> logical pixels.
    /// </summary>
    /// <param name="size">Size of the logical frame, 640x480 in the game.</param>
    /// <param name="chrome">The <c>ADVBORD</c> image, or <see langword="null"/> to draw flat bays.</param>
    public MapFrame(Size size, Sprite? chrome)
    {
        Size = size;
        _chrome = chrome;

        // The map keeps the frame between the border and the radar column: the radar is 144 pixels
        // wide, so both borders and the gap on its left are taken out of the width.
        Viewport = new Rect(
            BorderWidth,
            BorderWidth,
            Math.Max(size.Width - Minimap.PanelSize - (3 * BorderWidth), 0),
            Math.Max(size.Height - (2 * BorderWidth), 0));

        Radar = new Rect(
            size.Width - BorderWidth - Minimap.PanelSize,
            BorderWidth,
            Minimap.PanelSize,
            Minimap.PanelSize);

        // The panel bay runs from the bottom of the radar to the frame edge and not to the border: the
        // system buttons of the reference sit on the last rows of the display.
        Panel = new Rect(
            Radar.X,
            Radar.Bottom,
            Minimap.PanelSize,
            Math.Max(size.Height - Radar.Bottom, 0));

        BottomBorder = new Rect(
            BorderWidth,
            Math.Max(size.Height - BorderWidth, 0),
            Math.Max(size.Width - (2 * BorderWidth), 0),
            Math.Min(BorderWidth, size.Height));
    }

    /// <summary>Size of the logical frame the frame was laid out for.</summary>
    public Size Size { get; }

    /// <summary>Where the map is drawn, 448x448 at (16; 16) for a 640x480 display.</summary>
    public Rect Viewport { get; }

    /// <summary>The radar bay in the top right corner, 144x144.</summary>
    public Rect Radar { get; }

    /// <summary>The bay below the radar that holds the panel of the screen.</summary>
    public Rect Panel { get; }

    /// <summary>The strip of the bottom border, free of art, that a status line can use.</summary>
    public Rect BottomBorder { get; }

    /// <summary>Whether the artwork was found, so the caller need not fill the bays itself.</summary>
    public bool HasChrome => _chrome is not null;

    /// <summary>
    /// Reads <c>ADVBORD.ICN</c>, the single image the reference blits along the edges of its display.
    /// </summary>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <returns>The image, or <see langword="null"/> when the archive does not have it.</returns>
    public static Sprite? Load(AssetBundle? assets)
    {
        return assets?.TryReadIcn(AssetName) is { Count: > 0 } icn ? icn.Sprites[0] : null;
    }

    /// <summary>
    /// Tiles the brick of the panel bay, the way <c>Interface::IconsBar::redrawBackground</c> fills the
    /// column below the radar: the artwork holds one half-width brick at the top of that column
    /// (480; 176, 72x32) and the reference repeats it downwards and to the right. The icons that will
    /// sit on it belong to the interface that is not built yet, but the brick keeps the bay from
    /// looking like a hole in the frame.
    /// </summary>
    /// <param name="frame">Logical frame to paint into.</param>
    public void DrawPanel(PixelBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_chrome is null)
        {
            // The flat fallback of Draw() already covers the bay.
            return;
        }

        // The icons of the reference start one border below the radar, so the brick begins at
        // radarWidthPx + 2 * borderWidthPx (176) while the bay itself starts at the radar bottom (160).
        int top = Panel.Y + BorderWidth;
        Rect brick = new(Radar.X, top, Minimap.PanelSize / 2, PanelBrickHeight);

        for (int y = top; y < Panel.Bottom; y += PanelBrickHeight)
        {
            for (int x = Panel.X; x < Panel.Right; x += brick.Width)
            {
                // The destination offset of a sub sprite is relative to the sub rectangle, so the bake
                // of the brick has to be taken out of it: ( bricktopleft - bricktopleft ) puts the
                // brick where the loop wants it.
                ImageOps.BlitSprite(frame, new Point(x - brick.X, y - brick.Y), _chrome, brick);
            }
        }
    }

    /// <summary>
    /// Paints the frame: the artwork when it is there, flat bays in <paramref name="background"/>
    /// otherwise, so that the layout stays the same with and without the data.
    /// </summary>
    /// <param name="frame">Logical frame to paint into.</param>
    /// <param name="background">Palette index of the flat bays.</param>
    public void Draw(PixelBuffer frame, byte background)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_chrome is not null)
        {
            ImageOps.BlitSprite(frame, _chrome);
            return;
        }

        ImageOps.FillRect(frame, frame.Bounds, background);
        ImageOps.FillRect(frame, Viewport, background);
        ImageOps.FillRect(frame, Radar, background);
        ImageOps.FillRect(frame, Panel, background);
    }
}
