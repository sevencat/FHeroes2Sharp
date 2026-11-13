using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// A message box with a frame and no buttons, the <c>fheroes2::showMessage( header, body,
/// Dialog::ZERO )</c> of the reference: a yellow title and a white body, both centered in the text
/// area of a <see cref="FrameBox"/>.
/// </summary>
/// <remarks>
/// <para>
/// The original uses this dialog for the tooltips of the main menu: a right click on a sign shows
/// the title and the explanation of that sign while the button is held
/// (<c>Game::MainMenu()</c>). The box therefore needs no input handling of its own - the caller
/// decides when it exists - and no buttons, which is why <c>getDialogHeight()</c> only adds the
/// text heights here.
/// </para>
/// <para>
/// Line breaks follow the reference: the text is wrapped to the 244 pixel wide area, each line is
/// centered, the title starts one <see cref="TextOffsetY"/> below the top of the area and the body
/// one more <see cref="TextOffsetY"/> below the title.
/// </para>
/// </remarks>
public sealed class MessageBox
{
    /// <summary>Gap above the title and between the title and the body, <c>textOffsetY</c> of the reference.</summary>
    public const int TextOffsetY = 10;

    private readonly FrameBox _frame;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly string _header;
    private readonly string _body;
    private readonly int _headerHeight;

    private MessageBox(FrameBox frame, IGlyphSource headerFont, IGlyphSource bodyFont, string header, string body,
        int headerHeight)
    {
        _frame = frame;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _header = header;
        _body = body;
        _headerHeight = headerHeight;
    }

    /// <summary>Text area of the frame, the <c>GetArea()</c> of the reference.</summary>
    public Rect Area => _frame.Area;

    /// <summary>The composed frame, exposed so a caller can invalidate or test the covered pixels.</summary>
    public FrameBox Frame => _frame;

    /// <summary>Translated title drawn with <paramref name="headerFont"/>.</summary>
    public string Header => _header;

    /// <summary>Translated message drawn with <paramref name="bodyFont"/>.</summary>
    public string Body => _body;

    /// <summary>
    /// Composes the box for <paramref name="header"/> and <paramref name="body"/>.
    /// </summary>
    /// <param name="header">Title of the message; an empty string leaves the title out.</param>
    /// <param name="body">Message text; an empty string leaves the body out.</param>
    /// <param name="chrome">Pieces of <c>BUYBUILD.ICN</c>.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the message, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="startY">Top of the frame, or a negative value to center it.</param>
    /// <param name="screen">Screen the box is centered on; 640x480 when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="header"/>, <paramref name="body"/>, <paramref name="chrome"/>,
    /// <paramref name="headerFont"/> or <paramref name="bodyFont"/> is <see langword="null"/>.
    /// </exception>
    public static MessageBox Create(string header, string body, FrameBoxChrome chrome, IGlyphSource headerFont,
        IGlyphSource bodyFont, int startY = -1, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        int headerHeight = header.Length == 0 ? 0 : (Rows(headerFont, header) * headerFont.LineHeight) + TextOffsetY;
        int bodyHeight = Rows(bodyFont, body) * bodyFont.LineHeight;
        int contentHeight = headerHeight + (bodyHeight > 0 ? bodyHeight + TextOffsetY : 0);

        FrameBox frame = FrameBox.Create(chrome, contentHeight, showButtons: false, startY, screen);

        return new MessageBox(frame, headerFont, bodyFont, header, body, headerHeight);
    }

    /// <summary>Draws the frame, then the title and the body, like <c>showMessage()</c> does.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _frame.Draw(target);

        if (_header.Length > 0)
        {
            GlyphText.DrawMultiline(
                _headerFont,
                target,
                new Point(Area.X, Area.Y + TextOffsetY),
                _header,
                Area.Width,
                center: true);
        }

        if (_body.Length > 0)
        {
            GlyphText.DrawMultiline(
                _bodyFont,
                target,
                new Point(Area.X, Area.Y + TextOffsetY + _headerHeight),
                _body,
                Area.Width,
                center: true);
        }
    }

    /// <summary>Number of lines <paramref name="text"/> takes inside the 244 pixel wide text area.</summary>
    private static int Rows(IGlyphSource font, string text) =>
        text.Length == 0 ? 0 : GlyphText.WrapLines(font, text, FrameBox.AreaWidth).Count;
}
