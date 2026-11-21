using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The stone frame drawn around a floating panel, a port of <c>Dialog::FrameBorder</c>
/// (<c>src/fheroes2/dialog/dialog.h:177-225</c>, <c>dialog_frameborder.cpp:34-77</c>).
/// </summary>
/// <remarks>
/// <para>
/// The class only keeps geometry: the outer rectangle of the frame, the inner area where the contents go
/// and the strip of the top border that works as the drag handle. All three are derived from a call to
/// <see cref="SetPosition(int, int, int, int)"/>, and the reference rect is
/// <c>encw + 2 * border</c> by <c>ench + 2 * border</c> - the content area grown by the frame width on
/// every side.
/// </para>
/// <para>
/// <b>The saved pixels are the point of the restorer.</b> A frame that moves calls
/// <see cref="SetPosition(int, int, int, int)"/> again: the restorer first puts back the pixels the frame
/// covered last time, then remembers the new rectangle. That is how a dragged panel can slide over the map
/// without smearing. The port keeps that behaviour with <see cref="ImageRestorer"/>.
/// </para>
/// <para>
/// The two render helpers are static in the reference and read <c>Display::instance()</c>,
/// <c>Settings</c> and <c>Assets</c> themselves. Here the frame buffer, the artwork and the theme are
/// passed in, so a caller decides which interface it draws for.
/// </para>
/// </remarks>
public sealed class FrameBorder : IDisposable
{
    /// <summary>Frame width of the default constructor, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int DefaultBorderWidth = 16;

    /// <summary>Width of the shadow strip the window artwork carries, <c>fheroes2::shadowWidthPx</c>.</summary>
    public const int ShadowWidth = 16;

    /// <summary>File of the good frame artwork, <c>ICN::SURDRBKG</c>.</summary>
    public const string GoodArtworkName = "SURDRBKG.ICN";

    /// <summary>File of the evil frame artwork, <c>ICN::SURDRBKE</c>.</summary>
    public const string EvilArtworkName = "SURDRBKE.ICN";

    private readonly ImageRestorer _restorer;
    private readonly int _border;
    private int _rectX;
    private int _rectY;
    private int _rectWidth;
    private int _rectHeight;
    private int _areaX;
    private int _areaY;
    private int _areaWidth;
    private int _areaHeight;
    private int _topX;
    private int _topY;
    private int _topWidth;
    private int _topHeight;

    /// <summary>
    /// Binds the frame to a display, <c>FrameBorder( int v = borderWidthPx )</c>.
    /// </summary>
    /// <param name="target">Frame buffer the frame is drawn into and restored from.</param>
    /// <param name="borderWidth">Width of the frame on every side.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public FrameBorder(PixelBuffer target, int borderWidth = DefaultBorderWidth)
    {
        ArgumentNullException.ThrowIfNull(target);

        _restorer = new ImageRestorer(target, 0, 0, 0, 0);
        _border = borderWidth;
    }

    /// <summary>Width of the frame, <c>BorderWidth()</c>.</summary>
    public int BorderWidth => _border;

    /// <summary>Height of the frame, <c>BorderHeight()</c>; the reference returns the same value.</summary>
    public int BorderHeight => _border;

    /// <summary>Outer rectangle of the frame, <c>GetRect()</c>.</summary>
    public Rect Rect => new(_rectX, _rectY, _rectWidth, _rectHeight);

    /// <summary>Inner rectangle the contents go to, <c>GetArea()</c>.</summary>
    public Rect Area => new(_areaX, _areaY, _areaWidth, _areaHeight);

    /// <summary>The top strip that works as the drag handle, <c>GetTop()</c>.</summary>
    public Rect Top => new(_topX, _topY, _topWidth, _topHeight);

    /// <summary>Whether the frame has a size at all, <c>isValid()</c>.</summary>
    public bool IsValid => _rectWidth != 0 && _rectHeight != 0;

    /// <summary>
    /// Reads the frame artwork of the requested theme, the <c>Assets::getImage()</c> call the reference hides
    /// inside <see cref="RenderRegular"/>.
    /// </summary>
    /// <param name="assets">Archive to read from, may be <see langword="null"/>.</param>
    /// <param name="evilInterface">Whether the bone frame is wanted.</param>
    /// <returns>Frame 0 of the artwork, or <see langword="null"/> when the archive misses it.</returns>
    public static Sprite? TryLoadArtwork(AssetBundle? assets, bool evilInterface)
    {
        if (assets is null)
        {
            return null;
        }

        string name = evilInterface ? EvilArtworkName : GoodArtworkName;

        return assets.TryReadIcn(name) is { Count: > 0 } icn ? icn.Sprites[0] : null;
    }

    /// <summary>Restores the pixels the frame covered, <c>~FrameBorder()</c>.</summary>
    public void Dispose() => _restorer.Dispose();

    /// <summary>
    /// Places the frame around an area of a given size, <c>SetPosition()</c>.
    /// </summary>
    /// <param name="posX">Left edge of the frame.</param>
    /// <param name="posY">Top edge of the frame.</param>
    /// <param name="encWidth">Width of the contents; not positive keeps the previously remembered size.</param>
    /// <param name="encHeight">Height of the contents; not positive keeps the previously remembered size.</param>
    /// <remarks>
    /// The pixels covered by the previous placement are restored before the new rectangle is remembered.
    /// A non-positive content size does not resize the frame: only the saved rectangle moves, which the
    /// reference uses when it re-places a frame it already measured.
    /// </remarks>
    public void SetPosition(int posX, int posY, int encWidth, int encHeight)
    {
        _restorer.Restore();

        _rectX = posX;
        _rectY = posY;

        if (encWidth > 0 && encHeight > 0)
        {
            _rectWidth = encWidth + (2 * _border);
            _rectHeight = encHeight + (2 * _border);

            _restorer.Update(_rectX, _rectY, _rectWidth, _rectHeight);

            _areaWidth = encWidth;
            _areaHeight = encHeight;
        }
        else
        {
            _restorer.Update(posX, posY, _restorer.Width, _restorer.Height);
        }

        _areaX = posX + _border;
        _areaY = posY + _border;

        _topX = _rectX;
        _topY = _rectY;
        _topWidth = _rectWidth;
        _topHeight = _border;
    }

    /// <summary>
    /// Draws the themed stone frame stretched over a rectangle, <c>RenderRegular()</c>.
    /// </summary>
    /// <param name="target">Frame buffer to draw into.</param>
    /// <param name="artwork">Frame 0 of <see cref="GoodArtworkName"/> or <see cref="EvilArtworkName"/>.</param>
    /// <param name="dstRect">Rectangle to fill.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="target"/> or <paramref name="artwork"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The artwork carries a shadow on its right and bottom edge, so the stretched part starts at
    /// <see cref="ShadowWidth"/> and is that much narrower and shorter than the sprite.
    /// </remarks>
    public static void RenderRegular(PixelBuffer target, Sprite artwork, Rect dstRect)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(artwork);

        PixelBuffer art = artwork.Image;
        PixelBuffer stretched = Stretch(
            art,
            new Rect(ShadowWidth, 0, art.Width - ShadowWidth, art.Height - ShadowWidth),
            dstRect.Width,
            dstRect.Height);

        ImageOps.Blit(target, new Point(dstRect.X, dstRect.Y), stretched, new Rect(0, 0, stretched.Width, stretched.Height));
    }

    /// <summary>
    /// Draws an arbitrary tile stretched over a rectangle, <c>RenderOther()</c>.
    /// </summary>
    /// <param name="target">Frame buffer to draw into.</param>
    /// <param name="source">Tile to stretch; its whole area is used.</param>
    /// <param name="dstRect">Rectangle to fill.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="target"/> or <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public static void RenderOther(PixelBuffer target, PixelBuffer source, Rect dstRect)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        PixelBuffer stretched = Stretch(
            source, new Rect(0, 0, source.Width, source.Height), dstRect.Width, dstRect.Height);

        ImageOps.Blit(target, new Point(dstRect.X, dstRect.Y), stretched, new Rect(0, 0, stretched.Width, stretched.Height));
    }

    /// <summary>
    /// <c>fheroes2::Stretch( in, x, y, width, height, outWidth, outHeight )</c>: crops a part of an image and
    /// resamples it to the requested size.
    /// </summary>
    private static PixelBuffer Stretch(PixelBuffer source, Rect sourceArea, int outWidth, int outHeight)
    {
        if (outWidth <= 0 || outHeight <= 0)
        {
            return new PixelBuffer(0, 0);
        }

        PixelBuffer cropped = new(sourceArea.Width, sourceArea.Height);
        ImageOps.Blit(cropped, new Point(0, 0), source, sourceArea);

        PixelBuffer result = new(outWidth, outHeight);
        ImageOps.Resize(cropped, result);

        return result;
    }
}
