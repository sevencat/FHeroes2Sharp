using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// A message box with a frame and, optionally, a row of buttons, the <c>fheroes2::showMessage(
/// header, body, button )</c> of the reference: a yellow title and a white body, both centered in the
/// text area of a <see cref="FrameBox"/>, optionally followed by <see cref="DialogElement"/>s.
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
/// <para>
/// Elements are packed into rows the way <c>showMessage()</c> does it (<c>ui_dialog.cpp:113-210</c>): an
/// element joins the current row while <c>( max( rowWidth, elementWidth ) + 10 ) * ( count + 1 )</c> still fits
/// the area, every row is separated by <see cref="TextOffsetY"/> and an element sits at the bottom of its row,
/// centered inside it. The rows are what <c>getDialogHeight()</c> adds to the frame, so a box with elements grows.
/// </para>
/// <para>
/// <see cref="ProcessEvents"/> forwards to the elements and hands their answers back; popping the description
/// window stays with the owner, exactly like the reference splits <c>processEvents</c> from
/// <c>showStandardTextMessage</c>.
/// </para>
/// <para>
/// <b>Buttons.</b> The reference's <c>getDialogHeight()</c> adds <see cref="FrameBox.ButtonAreaHeight"/>
/// when the caller asks for buttons and the row is built by <c>ButtonGroup( area, buttonTypes )</c> on
/// the bottom edge of that area. The port does the same: <c>buttons</c> reserves the row and
/// <see cref="ButtonGroup.CreateStandard"/> places the OK/CANCEL/YES/NO sprites there, while
/// <see cref="SetCursor"/>/<see cref="SetLeftButton"/>/<see cref="ProcessHotKey"/> answer the clicks
/// and the two closing keys. The tavern is the first caller (<c>castle_tavern.cpp:39-53</c>), so the
/// boxes of the main menu keep passing no buttons and do not grow (ADR-192).
/// </para>
/// </remarks>
public sealed class MessageBox
{
    /// <summary>Gap above the title and between the title and the body, <c>textOffsetY</c> of the reference.</summary>
    public const int TextOffsetY = 10;

    /// <summary>Gap between two elements of a row, <c>elementOffsetX</c> of the reference.</summary>
    public const int ElementOffsetX = 10;

    private readonly FrameBox _frame;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly string _header;
    private readonly string _body;
    private readonly int _headerHeight;
    private readonly IReadOnlyList<DialogElement> _elements;
    private readonly IReadOnlyList<Rect> _elementAreas;
    private readonly ButtonGroup? _buttons;

    private MessageBox(FrameBox frame, IGlyphSource headerFont, IGlyphSource bodyFont, string header, string body,
        int headerHeight, IReadOnlyList<DialogElement> elements, IReadOnlyList<Rect> elementAreas,
        ButtonGroup? buttons)
    {
        _frame = frame;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _header = header;
        _body = body;
        _headerHeight = headerHeight;
        _elements = elements;
        _elementAreas = elementAreas;
        _buttons = buttons;
    }

    /// <summary>Text area of the frame, the <c>GetArea()</c> of the reference.</summary>
    public Rect Area => _frame.Area;

    /// <summary>The composed frame, exposed so a caller can invalidate or test the covered pixels.</summary>
    public FrameBox Frame => _frame;

    /// <summary>Translated title drawn with <paramref name="headerFont"/>.</summary>
    public string Header => _header;

    /// <summary>Translated message drawn with <paramref name="bodyFont"/>.</summary>
    public string Body => _body;

    /// <summary>Elements drawn under the text, empty when the box has none.</summary>
    public IReadOnlyList<DialogElement> Elements => _elements;

    /// <summary>Where the box put each element, in the order of <see cref="Elements"/>.</summary>
    public IReadOnlyList<Rect> ElementAreas => _elementAreas;

    /// <summary>The row of buttons at the bottom of the frame, <see langword="null"/> when the box has none.</summary>
    public ButtonGroup? Buttons => _buttons;

    /// <summary>
    /// Composes the box for <paramref name="header"/> and <paramref name="body"/>.
    /// </summary>
    /// <param name="header">Title of the message; an empty string leaves the title out.</param>
    /// <param name="body">Message text; an empty string leaves the body out.</param>
    /// <param name="chrome">Pieces of <c>BUYBUILD.ICN</c>.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the message, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="elements">Elements drawn under the text; empty or <see langword="null"/> for none.</param>
    /// <param name="startY">Top of the frame, or a negative value to center it.</param>
    /// <param name="screen">Screen the box is centered on; 640x480 when omitted.</param>
    /// <param name="buttons">
    /// Buttons to place on the bottom edge, e.g. <see cref="DialogButton.Ok"/>; <see cref="DialogButton.None"/>
    /// leaves the row out and keeps the box as small as the text needs.
    /// </param>
    /// <param name="buttonArtwork">Source of the button sprites, required as soon as <paramref name="buttons"/> is set.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="header"/>, <paramref name="body"/>, <paramref name="chrome"/>,
    /// <paramref name="headerFont"/>, <paramref name="bodyFont"/> or, when buttons are asked for,
    /// <paramref name="buttonArtwork"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">An element of <paramref name="elements"/> is <see langword="null"/>.</exception>
    public static MessageBox Create(string header, string body, FrameBoxChrome chrome, IGlyphSource headerFont,
        IGlyphSource bodyFont, IReadOnlyList<DialogElement>? elements = null, int startY = -1, Size? screen = null,
        DialogButton buttons = DialogButton.None, IDialogButtonArtworkSource? buttonArtwork = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        elements ??= [];

        foreach (DialogElement? element in elements)
        {
            if (element is null)
            {
                throw new ArgumentException("A message box element cannot be null.", nameof(elements));
            }
        }

        int headerHeight = header.Length == 0 ? 0 : (Rows(headerFont, header) * headerFont.LineHeight) + TextOffsetY;
        int bodyHeight = Rows(bodyFont, body) * bodyFont.LineHeight;
        int contentHeight = headerHeight + (bodyHeight > 0 ? bodyHeight + TextOffsetY : 0);

        List<Row> rows = PackRows(elements);
        int elementBlockHeight = 0;

        foreach (Row row in rows)
        {
            elementBlockHeight += row.Height + TextOffsetY;
        }

        if (rows.Count > 0 && bodyHeight > 0)
        {
            elementBlockHeight += TextOffsetY;
        }

        bool hasButtons = buttons != DialogButton.None;

        if (hasButtons && buttonArtwork is null)
        {
            throw new ArgumentNullException(nameof(buttonArtwork), "A message box with buttons needs their artwork.");
        }

        FrameBox frame = FrameBox.Create(chrome, contentHeight + elementBlockHeight, hasButtons, startY, screen);

        List<Rect> areas = PlaceElements(rows, frame.Area, contentHeight, bodyHeight > 0);

        ButtonGroup? group = hasButtons ? ButtonGroup.CreateStandard(frame.Area, buttons, buttonArtwork!) : null;

        return new MessageBox(frame, headerFont, bodyFont, header, body, headerHeight, elements, areas, group);
    }

    /// <summary>Draws the frame, the title, the body and the elements, like <c>showMessage()</c> does.</summary>
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

        for (int i = 0; i < _elements.Count; ++i)
        {
            Rect area = _elementAreas[i];

            _elements[i].Draw(target, new Point(area.X, area.Y));
        }

        _buttons?.Draw(target);
    }

    /// <summary>
    /// Repaints the animated elements, the <c>update( output, offset )</c> loop of the reference's
    /// message loop.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <param name="elapsed">Time since the previous call.</param>
    /// <returns><see langword="true"/> when an element painted something.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public bool Update(PixelBuffer target, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(target);

        bool painted = false;

        for (int i = 0; i < _elements.Count; ++i)
        {
            Rect area = _elementAreas[i];

            painted |= _elements[i].Update(target, new Point(area.X, area.Y), elapsed);
        }

        return painted;
    }

    /// <summary>
    /// Records the cursor position for the button row, <see cref="ButtonGroup.SetCursor"/>; does
    /// nothing for a box without buttons.
    /// </summary>
    /// <param name="cursor">Cursor position in logical pixels, or <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? cursor) => _buttons?.SetCursor(cursor);

    /// <summary>
    /// Records the left mouse button and answers the button the press and the release both landed on,
    /// <see cref="ButtonGroup.SetLeftButton"/>; <see cref="DialogButton.None"/> for a box without buttons.
    /// </summary>
    /// <param name="down">Whether the left mouse button is now held.</param>
    /// <returns>Value of the clicked button, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton SetLeftButton(bool down) => _buttons?.SetLeftButton(down) ?? DialogButton.None;

    /// <summary>
    /// Answers the button a hot key stands for, <see cref="ButtonGroup.ProcessHotKey"/>: ESC and Enter
    /// close an OK box (<c>Game::HotKeyCloseWindow()</c>).
    /// </summary>
    /// <param name="hotKey">Hot key that was pressed.</param>
    /// <returns>Value of the button the key stands for, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton ProcessHotKey(ButtonHotKey hotKey) => _buttons?.ProcessHotKey(hotKey) ?? DialogButton.None;

    /// <summary>
    /// Offers one frame of input to the elements, the loop <c>showMessage()</c> runs inside its dialog.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The first popup an element asked for and whether any of them painted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public DialogElementResult ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        bool requiresRedraw = false;
        DialogPopup? popup = null;

        for (int i = 0; i < _elements.Count; ++i)
        {
            Rect area = _elementAreas[i];
            DialogElementResult result = _elements[i].ProcessEvents(events, new Point(area.X, area.Y));

            requiresRedraw |= result.RequiresRedraw;

            popup ??= result.Popup;
        }

        return new DialogElementResult(requiresRedraw, popup);
    }

    /// <summary>Number of lines <paramref name="text"/> takes inside the 244 pixel wide text area.</summary>
    private static int Rows(IGlyphSource font, string text) =>
        text.Length == 0 ? 0 : GlyphText.WrapLines(font, text, FrameBox.AreaWidth).Count;

    /// <summary>Packs the elements into rows, <c>ui_dialog.cpp:113-160</c>.</summary>
    private static List<Row> PackRows(IReadOnlyList<DialogElement> elements)
    {
        List<Row> rows = [];

        foreach (DialogElement element in elements)
        {
            Size area = element.Area;

            if (rows.Count > 0)
            {
                Row last = rows[^1];

                if ((Math.Max(last.Width, area.Width) + ElementOffsetX) * (last.Count + 1) <= FrameBox.AreaWidth)
                {
                    last.Add(element);
                    continue;
                }
            }

            rows.Add(new Row(element));
        }

        return rows;
    }

    /// <summary>Places every element at the bottom of its row, centered inside it, <c>ui_dialog.cpp:186-210</c>.</summary>
    private static List<Rect> PlaceElements(List<Row> rows, Rect area, int contentHeight, bool hasBody)
    {
        List<Rect> areas = [];
        int rowTop = area.Y + contentHeight + TextOffsetY + (hasBody ? TextOffsetY : 0);

        foreach (Row row in rows)
        {
            int emptyWidth = FrameBox.AreaWidth - (row.Count * row.Width);
            int offsetBetween = emptyWidth / (row.Count + 1);

            for (int i = 0; i < row.Count; ++i)
            {
                DialogElement element = row.Elements[i];
                Size size = element.Area;
                int x = area.X + offsetBetween + (i * (row.Width + offsetBetween));

                x += (row.Width - size.Width) / 2;

                areas.Add(new Rect(x, rowTop + row.Height - size.Height, size.Width, size.Height));
            }

            rowTop += TextOffsetY + row.Height;
        }

        return areas;
    }

    /// <summary>One row of elements; the reference keeps the same three numbers in parallel vectors.</summary>
    private sealed class Row
    {
        public Row(DialogElement first)
        {
            Elements = [first];
            Width = first.Area.Width;
            Height = first.Area.Height;
        }

        public List<DialogElement> Elements { get; }

        /// <summary>Widest element of the row so far.</summary>
        public int Width { get; private set; }

        /// <summary>Tallest element of the row so far.</summary>
        public int Height { get; private set; }

        public int Count => Elements.Count;

        /// <summary>Appends an element, widening and growing the row to cover it.</summary>
        public void Add(DialogElement element)
        {
            Elements.Add(element);
            Width = Math.Max(Width, element.Area.Width);
            Height = Math.Max(Height, element.Area.Height);
        }
    }
}
