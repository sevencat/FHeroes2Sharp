using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The stone frame around the whole screen, a port of <c>Interface::GameBorderRedraw()</c>
/// (<c>src/fheroes2/gui/interface_border.cpp:70-418</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the border is assembled instead of blitted.</b> <c>ADVBORD.ICN</c> is a single 640x480 image of
/// the original 4:3 screen, but the reference window may be larger. The function therefore cuts the
/// artwork into nine pieces - the corners and the straight runs between them - and repeats the pieces
/// that may grow: the 6 pixel strips next to the corners, the 32 pixel tile of the top and bottom
/// border, the 32 pixel tile of the left and right border, and the bone piece of the evil bottom
/// border, which is only 7 pixels wide. Everything else is a single blit whose width comes from what
/// is left of the artwork.
/// </para>
/// <para>
/// <b>The vertical runs change with the number of icon rows.</b> The panel column on the right holds
/// the radar (144x144), then the icon bar, then the status panel. On a taller display the reference
/// repeats the icon row pattern <see cref="VertRepeatHeightTop"/> pixels at most, so it chooses the
/// count of icon rows from the extra height: under three extra rows the artwork has room for four
/// icons, exactly three extra rows for seven (removing one branch piece) and more than three for all
/// eight. <c>iconsCount</c> only moves the second piece of the icon border, which is the separator
/// strip under the icons.
/// </para>
/// <para>
/// The view world screen hides the small embranchment of the middle border with two 3 pixel wide
/// patches, because the view world panel sits where the branch used to be.
/// </para>
/// </remarks>
public static class GameBorder
{
    /// <summary>Width of the frame border, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidth = 16;

    /// <summary>Side of one map tile in pixels, <c>fheroes2::tileWidthPx</c>; also the repeat step of the border tiles.</summary>
    public const int TileWidth = 32;

    /// <summary>Width of the radar column, <c>fheroes2::radarWidthPx</c>.</summary>
    public const int RadarWidth = 144;

    /// <summary>Size of the artwork every run is cut from.</summary>
    public static readonly Size ArtworkSize = new(640, 480);

    /// <summary>File of the good interface frame, <c>ICN::ADVBORD</c>.</summary>
    public const string GoodAssetName = "ADVBORD.ICN";

    /// <summary>File of the evil interface frame, <c>ICN::ADVBORDE</c>.</summary>
    public const string EvilAssetName = "ADVBORDE.ICN";

    /// <summary>
    /// Reads the frame artwork of the requested theme.
    /// </summary>
    /// <param name="assets">Archive to read from, may be <see langword="null"/>.</param>
    /// <param name="evilInterface">Whether the evil (bone) artwork is wanted, <c>isEvilInterfaceEnabled()</c>.</param>
    /// <returns>Frame 0 of the artwork, or <see langword="null"/> when the archive misses it.</returns>
    public static Sprite? TryLoad(AssetBundle? assets, bool evilInterface)
    {
        if (assets is null)
        {
            return null;
        }

        string name = evilInterface ? EvilAssetName : GoodAssetName;

        return assets.TryReadIcn(name) is { Count: > 0 } icn ? icn.Sprites[0] : null;
    }

    /// <summary>
    /// Paints the frame around and across the screen, <c>GameBorderRedraw()</c>.
    /// </summary>
    /// <param name="target">Frame buffer of the display; its size decides how the runs repeat.</param>
    /// <param name="artwork">Frame 0 of <see cref="GoodAssetName"/> or <see cref="EvilAssetName"/>.</param>
    /// <param name="evilInterface">Whether <paramref name="artwork"/> is the evil one.</param>
    /// <param name="hideInterface">The «no interface» setting, <c>isHideInterfaceEnabled()</c>.</param>
    /// <param name="viewWorldMode">Whether the view world screen is up.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="target"/> or <paramref name="artwork"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// With the «no interface» setting on, the border belongs to the floating windows instead and
    /// nothing is painted, unless the view world screen asks for it explicitly - that is the
    /// <c>hideInterface &amp;&amp; !viewWorldMode</c> early return of the reference.
    /// </remarks>
    public static void Redraw(
        PixelBuffer target, Sprite artwork, bool evilInterface, bool hideInterface, bool viewWorldMode)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(artwork);

        if (hideInterface && !viewWorldMode)
        {
            return;
        }

        int displayWidth = target.Width;
        int displayHeight = target.Height;
        int extraDisplayWidth = displayWidth - ArtworkSize.Width;
        int extraDisplayHeight = displayHeight - ArtworkSize.Height;

        int topRepeatCount = extraDisplayWidth > 0 ? extraDisplayWidth / TileWidth : 0;
        int topRepeatWidth = (topRepeatCount + 1) * TileWidth;

        int vertRepeatCount = extraDisplayHeight > 0 ? extraDisplayHeight / TileWidth : 0;
        int iconsCount = vertRepeatCount > 3 ? 8 : (vertRepeatCount < 3 ? 4 : 7);

        int vertRepeatHeight = (vertRepeatCount + 1) * TileWidth;
        int vertRepeatHeightTop = (iconsCount - 3) * TileWidth;
        int vertRepeatHeightBottom = vertRepeatHeight - vertRepeatHeightTop;

        int topPadWidth = extraDisplayWidth % TileWidth;

        // The padding of the top and bottom border is split in two halves around the repeated tiles.
        int topPadWidthLeft = topPadWidth / 2;
        int topPadWidthRight = topPadWidth - topPadWidthLeft;

        int bottomTileWidth;
        int bottomRepeatCount;

        if (evilInterface)
        {
            bottomTileWidth = 7; // Width of a single bone piece.
            bottomRepeatCount = extraDisplayWidth > 0 ? extraDisplayWidth / bottomTileWidth : 0;
        }
        else
        {
            bottomTileWidth = TileWidth;
            bottomRepeatCount = topRepeatCount;
        }

        int bottomRepeatWidth = (bottomRepeatCount + 1) * bottomTileWidth;

        int bottomPadWidth = extraDisplayWidth % bottomTileWidth;
        int bottomPadWidthLeft = bottomPadWidth / 2;
        int bottomPadWidthRight = bottomPadWidth - bottomPadWidthLeft;

        int vertPadHeight = extraDisplayHeight % TileWidth;

        PixelBuffer art = artwork.Image;
        int artWidth = art.Width;
        int artHeight = art.Height;

        int srcX = 0;
        int srcY = 0;
        int dstX = 0;
        int dstY = 0;

        // TOP BORDER
        int srcWidth = evilInterface ? 153 : 193;
        int srcHeight = BorderWidth;

        srcX = 0;
        srcY = 0;
        dstX = srcX;
        dstY = srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        srcX += srcWidth;

        srcWidth = 6;
        dstX = srcX;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, srcWidth + topPadWidthLeft, BorderWidth);
        dstX += srcWidth + topPadWidthLeft;
        srcX += srcWidth;

        srcWidth = evilInterface ? 64 : 24;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstX += srcWidth;
        srcX += srcWidth;

        srcWidth = TileWidth;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, topRepeatWidth, BorderWidth);
        dstX += topRepeatWidth;
        srcX += TileWidth;

        srcWidth = evilInterface ? 65 : 25;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstX += srcWidth;
        srcX += srcWidth;

        srcWidth = 6;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, srcWidth + topPadWidthRight, BorderWidth);
        dstX += srcWidth + topPadWidthRight;
        srcX += srcWidth;

        srcWidth = artWidth - srcX;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // LEFT BORDER
        srcX = 0;
        srcY = BorderWidth;
        srcWidth = BorderWidth;
        srcHeight = 255 - BorderWidth;
        dstX = srcX;
        dstY = srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstY += srcHeight;
        srcY += srcHeight;

        if (evilInterface)
        {
            srcHeight = TileWidth;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightTop);
            dstY += vertRepeatHeightTop;
            srcY += TileWidth;

            srcHeight = 35;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            srcHeight = 6;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, srcHeight + vertPadHeight);
            dstY += srcHeight + vertPadHeight;
            srcY += srcHeight;

            srcHeight = 103;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            srcHeight = TileWidth;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += TileWidth;

            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightBottom);
            dstY += vertRepeatHeightBottom;
            srcY += TileWidth;
        }
        else
        {
            srcHeight = TileWidth;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeight);
            dstY += vertRepeatHeight;
            srcY += TileWidth;

            srcHeight = 125;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            srcHeight = 4;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, srcHeight + vertPadHeight);
            dstY += srcHeight + vertPadHeight;
            srcY += srcHeight;
        }

        srcHeight = artHeight - BorderWidth - srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // MIDDLE BORDER
        srcX = artWidth - RadarWidth - (2 * BorderWidth);
        srcY = BorderWidth;
        srcWidth = BorderWidth;
        srcHeight = 255 - BorderWidth;
        dstX = displayWidth - RadarWidth - (2 * BorderWidth);
        dstY = srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstY += srcHeight;
        srcY += srcHeight;

        srcHeight = TileWidth;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightTop);
        dstY += vertRepeatHeightTop;
        srcY += TileWidth;

        srcHeight = evilInterface ? 35 : 50;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // Hide embranchment.
        if (viewWorldMode)
        {
            var fixRect = new Rect(478, evilInterface ? 137 : 345, 3, evilInterface ? 15 : 20);
            var fixPoint = new Point(dstX + 14, dstY + 18);
            Blit(art, fixRect.X, fixRect.Y, fixRect.Width, fixRect.Height, target, fixPoint.X, fixPoint.Y);
        }

        dstY += srcHeight;
        srcY += srcHeight;

        if (evilInterface)
        {
            srcHeight = 6;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, srcHeight + vertPadHeight);
            dstY += srcHeight + vertPadHeight;
            srcY += srcHeight;

            srcHeight = 103;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            srcHeight = TileWidth;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += TileWidth;

            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightBottom);
            dstY += vertRepeatHeightBottom;
            srcY += TileWidth;
        }
        else
        {
            srcHeight = TileWidth;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += TileWidth;

            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightBottom);
            dstY += vertRepeatHeightBottom;
            srcY += TileWidth;

            srcHeight = 43;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            // The middle border is special on the good interface because of all the green leaves.
            srcHeight = 8;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, srcHeight + vertPadHeight);
            dstY += srcHeight + vertPadHeight;
            srcY += srcHeight;
        }

        srcHeight = artHeight - BorderWidth - srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // RIGHT BORDER
        srcX = artWidth - BorderWidth;
        srcY = BorderWidth;
        srcWidth = BorderWidth;
        srcHeight = 255 - BorderWidth;
        dstX = displayWidth - BorderWidth;
        dstY = srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstY += srcHeight;
        srcY += srcHeight;

        srcHeight = TileWidth;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightTop);
        dstY += vertRepeatHeightTop;
        srcY += TileWidth;

        srcHeight = evilInterface ? 35 : 50;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // Hide embranchment.
        if (viewWorldMode)
        {
            var fixRect = new Rect(624, evilInterface ? 139 : 345, 3, evilInterface ? 15 : 20);
            var fixPoint = new Point(dstX, dstY + 18);
            Blit(art, fixRect.X, fixRect.Y, fixRect.Width, fixRect.Height, target, fixPoint.X, fixPoint.Y);
        }

        dstY += srcHeight;
        srcY += srcHeight;

        if (evilInterface)
        {
            srcHeight = 6;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, srcHeight + vertPadHeight);
            dstY += srcHeight + vertPadHeight;
            srcY += srcHeight;

            srcHeight = 103;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            srcHeight = TileWidth;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += TileWidth;

            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightBottom);
            dstY += vertRepeatHeightBottom;
            srcY += TileWidth;
        }
        else
        {
            srcHeight = TileWidth;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += TileWidth;

            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, vertRepeatHeightBottom);
            dstY += vertRepeatHeightBottom;
            srcY += TileWidth;

            srcHeight = 43;
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
            dstY += srcHeight;
            srcY += srcHeight;

            srcHeight = 4;
            RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, BorderWidth, srcHeight + vertPadHeight);
            dstY += srcHeight + vertPadHeight;
            srcY += srcHeight;
        }

        srcHeight = artHeight - BorderWidth - srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // BOTTOM BORDER
        srcX = 0;
        srcY = artHeight - BorderWidth;
        srcWidth = evilInterface ? 129 : 193;
        srcHeight = BorderWidth;
        dstX = srcX;
        dstY = displayHeight - BorderWidth;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstX += srcWidth;
        srcX += srcWidth;

        srcWidth = 6;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, srcWidth + bottomPadWidthLeft, BorderWidth);
        dstX += srcWidth + bottomPadWidthLeft;
        srcX += srcWidth;

        srcWidth = evilInterface ? 90 : 24;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstX += srcWidth;
        srcX += srcWidth;

        srcWidth = bottomTileWidth;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, bottomRepeatWidth, BorderWidth);
        dstX += bottomRepeatWidth;
        srcX += srcWidth;

        // The evil bottom border is asymmetric.
        srcWidth = evilInterface ? 86 : 25;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        dstX += srcWidth;
        srcX += srcWidth;

        srcWidth = 6;
        RepeatPattern(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY, srcWidth + bottomPadWidthRight, BorderWidth);
        dstX += srcWidth + bottomPadWidthRight;
        srcX += srcWidth;

        srcWidth = artWidth - srcX;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        // ICON BORDER
        srcX = artWidth - RadarWidth - BorderWidth;
        srcY = RadarWidth + BorderWidth;
        srcWidth = RadarWidth;
        srcHeight = BorderWidth;
        dstX = displayWidth - RadarWidth - BorderWidth;
        dstY = srcY;
        Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);

        if (!viewWorldMode)
        {
            dstY = srcY + BorderWidth + (iconsCount * TileWidth);
            srcY = srcY + BorderWidth + (4 * TileWidth);
            Blit(art, srcX, srcY, srcWidth, srcHeight, target, dstX, dstY);
        }
    }

    /// <summary>
    /// Tiles a piece of the artwork over a destination rectangle, the file local <c>repeatPattern()</c>
    /// of <c>interface_border.cpp:40-67</c>.
    /// </summary>
    /// <remarks>
    /// The piece is copied whole as often as it fits, then once more with the remainder's size glued to
    /// the far edge - so the pattern always ends flush with the requested width and height instead of
    /// leaving a gap. Negative or zero sizes are ignored, as the reference asserts on them.
    /// </remarks>
    public static void RepeatPattern(
        PixelBuffer art,
        int inX,
        int inY,
        int inWidth,
        int inHeight,
        PixelBuffer target,
        int outX,
        int outY,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(target);

        if (inX < 0 || inY < 0 || outX < 0 || outY < 0 || inWidth <= 0 || inHeight <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        int countX = width / inWidth;
        int countY = height / inHeight;
        int restWidth = width % inWidth;
        int restHeight = height % inHeight;

        for (int y = 0; y < countY; ++y)
        {
            for (int x = 0; x < countX; ++x)
            {
                Blit(art, inX, inY, inWidth, inHeight, target, outX + (x * inWidth), outY + (y * inHeight));
            }

            if (restWidth != 0)
            {
                Blit(art, inX, inY, restWidth, inHeight, target, outX + width - restWidth, outY + (y * inHeight));
            }
        }

        if (restHeight != 0)
        {
            for (int x = 0; x < countX; ++x)
            {
                Blit(art, inX, inY, inWidth, restHeight, target, outX + (x * inWidth), outY + height - restHeight);
            }

            if (restWidth != 0)
            {
                Blit(art, inX, inY, restWidth, restHeight, target, outX + width - restWidth, outY + height - restHeight);
            }
        }
    }

    /// <summary>
    /// <c>fheroes2::Blit( in, inX, inY, out, outX, outY, width, height )</c> without a transform layer.
    /// </summary>
    private static void Blit(
        PixelBuffer art, int srcX, int srcY, int width, int height, PixelBuffer target, int dstX, int dstY)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        ImageOps.Blit(target, new Point(dstX, dstY), art, new Rect(srcX, srcY, width, height));
    }
}
