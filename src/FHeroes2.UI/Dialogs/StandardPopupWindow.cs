using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The right button popup of the adventure map,
/// <c>Interface::displayStandardPopupWindow()</c> (<c>src/fheroes2/gui/ui_map_interface.cpp:56-81</c>).
/// </summary>
/// <remarks>
/// <para>
/// A single fixed <c>QWIKINFO.ICN</c> frame of 158x94, with the text drawn in
/// <c>FontType::smallWhite()</c>, horizontally centered inside the 126 pixels between the two 16 pixel
/// side borders and vertically centered in the frame, then offset 22 pixels right and 6 pixels up. The
/// window is placed on the tile under the cursor, one tile wide and tall past it, and clamped to the
/// interface area. The reference shows it while the right button is held; the port has no event loop
/// here, so the caller owns the lifetime and only the layout and the drawing live in this class.
/// </para>
/// <para>
/// <b>Not ported</b>: <c>CursorRestorer</c> and <c>ImageRestorer</c>, which are an optimisation of the
/// reference's blitting — the port redraws the whole scene every frame and has nothing to restore.
/// </para>
/// </remarks>
public sealed class StandardPopupWindow
{
    /// <summary>Name of the frame in the archive.</summary>
    public const string AssetName = "QWIKINFO.ICN";

    /// <summary>Width of the frame the reference keeps around the interface, <c>borderWidthPx</c>.</summary>
    public const int BorderWidth = UiConstants.BorderWidthPx;

    /// <summary>Side of a map tile in pixels, <c>tileWidthPx</c>.</summary>
    public const int TileWidth = UiConstants.TileWidthPx;

    /// <summary>Left inset of the text, <c>windowRoi.x + 22</c> of the reference.</summary>
    public const int TextInset = 22;

    /// <summary>Vertical offset of the text block, <c>windowRoi.y - 6 + ...</c> of the reference.</summary>
    public const int TextVerticalOffset = -6;

    private readonly IGlyphSource _font;
    private readonly Sprite _frame;
    private readonly string _text;
    private readonly Rect _area;
    private readonly Rect _textArea;

    private StandardPopupWindow(IGlyphSource font, Sprite frame, string text, Rect area)
    {
        _font = font;
        _frame = frame;
        _text = text;
        _area = area;

        int textWidth = area.Width - (2 * BorderWidth);
        int textHeight = GlyphText.WrapLines(font, text, textWidth).Count * font.LineHeight;
        _textArea = new Rect(
            area.X + TextInset,
            area.Y + TextVerticalOffset + ((area.Height - textHeight) / 2),
            textWidth,
            textHeight);
    }

    /// <summary>
    /// Builds the popup next to the tile under the cursor.
    /// </summary>
    /// <param name="frame">The <c>QWIKINFO.ICN</c> frame.</param>
    /// <param name="font">Font of the text, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <param name="text">Text to show.</param>
    /// <param name="mousePosition">Cursor of the mouse, in interface pixels.</param>
    /// <param name="interfaceArea">Area the popup has to stay inside, <c>getGameArea().GetROI()</c>.</param>
    /// <returns>The popup, or <see langword="null"/> when the frame cannot fit the interface.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="frame"/>, <paramref name="font"/> or <paramref name="text"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static StandardPopupWindow? TryCreate(Sprite frame, IGlyphSource font, string text,
        Point mousePosition, Rect interfaceArea)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);

        Size size = new(frame.Width, frame.Height);

        if (size.Width > interfaceArea.Width || size.Height > interfaceArea.Height)
        {
            return null;
        }

        return new StandardPopupWindow(font, frame, text, PositionOf(mousePosition, interfaceArea, size));
    }

    /// <summary>
    /// Where the popup goes for a cursor position, <c>getPopupWindowPosition()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// The window hangs off the lower right corner of the tile under the cursor: the tile is found with
    /// the 16 pixel border offset of the interface, and the window is centred on that corner, so a
    /// 158x94 popup straddles four tiles. The result is clamped to the interface area.
    /// </remarks>
    /// <param name="mousePosition">Cursor of the mouse, in interface pixels.</param>
    /// <param name="interfaceArea">Area the popup has to stay inside.</param>
    /// <param name="windowSize">Size of the popup frame.</param>
    /// <returns>The popup rectangle.</returns>
    public static Rect PositionOf(Point mousePosition, Rect interfaceArea, Size windowSize)
    {
        int localX = mousePosition.X - interfaceArea.X;
        int localY = mousePosition.Y - interfaceArea.Y;

        int x = (((localX - BorderWidth) / TileWidth) * TileWidth) + TileWidth - (windowSize.Width / 2);
        int y = (((localY - BorderWidth) / TileWidth) * TileWidth) + TileWidth - (windowSize.Height / 2);

        return new Rect(
            interfaceArea.X + Math.Clamp(x, 0, interfaceArea.Width - windowSize.Width),
            interfaceArea.Y + Math.Clamp(y, 0, interfaceArea.Height - windowSize.Height),
            windowSize.Width,
            windowSize.Height);
    }

    /// <summary>Rectangle of the frame.</summary>
    public Rect Area => _area;

    /// <summary>Rectangle the text is drawn in.</summary>
    public Rect TextArea => _textArea;

    /// <summary>The text shown.</summary>
    public string Text => _text;

    /// <summary>Draws the frame and the centered text.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _area.Position, _frame);
        GlyphText.DrawMultiline(_font, target, _textArea.Position, _text, _textArea.Width, center: true);
    }
}
