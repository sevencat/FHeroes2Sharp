using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// One row of an item selection window, i.e. the <c>int32_t</c> the reference's
/// <c>ItemSelectionWindow : public ListBox&lt;int32_t&gt;</c> stores (<c>dialog_selectitems.h:76</c>).
/// </summary>
/// <remarks>
/// The reference stores plain ids and lets the subclass turn them into spells, artifacts or monsters when
/// it paints the row. The port needs a reference type for <see cref="ListBox{T}"/>, so the id gets a
/// wrapper; equality is decided by the id alone, which is what <see cref="ListBox{T}.SetCurrent(T)"/>
/// needs to find the row.
/// </remarks>
public sealed class ItemSelectionEntry : IEquatable<ItemSelectionEntry>
{
    /// <summary>Wraps an id.</summary>
    /// <param name="id">Id of the game object the row stands for.</param>
    public ItemSelectionEntry(int id) => Id = id;

    /// <summary>Id of the game object the row stands for, the reference's <c>int32_t</c>.</summary>
    public int Id { get; }

    /// <inheritdoc/>
    public bool Equals(ItemSelectionEntry? other) => other is not null && other.Id == Id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ItemSelectionEntry other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Id;
}

/// <summary>
/// A modal list of game objects with an okay and a cancel button, a port of
/// <c>Dialog::ItemSelectionWindow</c> (<c>dialog_selectitems.h:61-193</c>,
/// <c>dialog_selectitems.cpp:889-1081</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference renders the window and then runs its own <c>while ( res == Dialog::ZERO )</c> loop in
/// <c>selectItemsEventProcessing()</c>, drawing the list and reading the mouse and the keyboard itself.
/// The port has no blocking dialog: <see cref="Draw"/> composes one frame and <see cref="ProcessEvents"/>
/// consumes one frame of input, so the scene owns the loop. A frame can therefore answer one thing at a
/// time — the reference would show a right click tooltip and still process the list in the same
/// iteration, the port shows the tooltip and the list catches up on the next frame.
/// </para>
/// <para>
/// The layout is the reference's: the title is centred ten pixels under the top of the active area, the
/// optional description follows at thirty, and the list takes the remaining room minus a twenty pixel
/// inset on the left and right and a seventy pixel strip at the bottom for the buttons
/// (<c>dialog_selectitems.cpp:916-931</c>). The list area gets the text background shading and the scroll
/// bar groove of <see cref="StandardWindow"/>, and the two buttons are the reference's small
/// <c>BUTTON_SMALL_OKAY_GOOD</c> / <c>BUTTON_SMALL_CANCEL_GOOD</c> pair, twenty pixels in from the bottom
/// corners and seven pixels up.
/// </para>
/// <para>
/// <b>Not ported:</b> the toggle-on/toggle-off buttons and <c>enableToggleButtons()</c>. The only
/// reference window that shows them is <c>MultiArtifactSelection</c>, which is not ported yet; the base
/// class keeps the reference's other virtual hooks (<see cref="ActionListPressRight(ItemSelectionEntry)"/>
/// and <see cref="ActionListDoubleClick(ItemSelectionEntry)"/>) so a subclass can answer them.
/// </para>
/// </remarks>
public abstract class ItemSelectionWindow : ListBox<ItemSelectionEntry>
{
    /// <summary>Left inset of the list area inside the active area, <c>activeArea.x + 10</c>.</summary>
    public const int ListInsetX = 10;

    /// <summary>Top of the list area inside the active area, <c>activeArea.y + 30</c>.</summary>
    public const int ListTop = 30;

    /// <summary>How much narrower than the active area the list area is, <c>activeArea.width - 40</c>.</summary>
    public const int ListWidthDeduction = 40;

    /// <summary>How much shorter than the active area the list area is, <c>activeArea.height - 70</c>.</summary>
    public const int ListHeightDeduction = 70;

    /// <summary>Inset of the rows inside the list area, <c>listRoi.x + 5</c>.</summary>
    public const int RowInset = 5;

    /// <summary>Gap between the description and the list, <c>descriptionHeight + 3</c>.</summary>
    public const int DescriptionGap = 3;

    /// <summary>Distance from the right edge of the active area to the scroll bar, <c>-25</c>.</summary>
    public const int ScrollBarRightInset = 25;

    /// <summary>Room at the top and the bottom of the groove for the two step buttons, 19 pixels.</summary>
    public const int ScrollBarTopPartHeight = 19;

    /// <summary>Width of the groove the thumb travels in, 10 pixels.</summary>
    public const int ScrollBarGrooveWidth = 10;

    /// <summary>Width of the drawn groove, the <c>16</c> the reference hands to the background renderer.</summary>
    public const int ScrollBarBackgroundWidth = 16;

    /// <summary>Horizontal distance from the bottom corners to the two buttons, <c>{ 20, 7 }</c>.</summary>
    public const int ButtonEdgeGapX = 20;

    /// <summary>Vertical distance from the bottom edge to the two buttons, <c>{ 20, 7 }</c>.</summary>
    public const int ButtonEdgeGapY = 7;

    private readonly StandardWindow _window;
    private readonly ItemSelectionWindowArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _font;
    private readonly string _title;
    private readonly string? _description;
    private readonly Rect _listArea;
    private readonly Rect _grooveArea;
    private readonly ButtonGroup _buttons = new();
    private bool _isDoubleClicked;

    /// <summary>
    /// Lays out the window, the reference's
    /// <c>ItemSelectionWindow( const fheroes2::Size &amp;, std::string, std::string )</c>
    /// (<c>dialog_selectitems.cpp:889-1057</c>).
    /// </summary>
    /// <param name="dialogSize">Size of the active area, <c>dialogSize</c> of the reference.</param>
    /// <param name="title">Heading, drawn centred in the yellow font.</param>
    /// <param name="description">
    /// Optional explanation, drawn centred in the white font. An empty string leaves it out and moves the
    /// list three pixels up, exactly like the reference.
    /// </param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/>, <paramref name="titleFont"/> or <paramref name="font"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty.</exception>
    protected ItemSelectionWindow(Size dialogSize, string title, string description,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font, Size? screen = null)
        : base(new Point(0, 0))
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(font);

        _title = title;
        _description = string.IsNullOrEmpty(description) ? null : description;
        _artwork = artwork;
        _titleFont = titleFont;
        _font = font;

        _window = StandardWindow.CreateCentered(dialogSize, renderBackground: true, screen);
        Rect area = _window.ActiveArea;

        // The description pushes the list down by its own height plus three pixels; without one the list
        // starts directly under the title.
        int listOffsetY = _description is null ? 0 : font.LineHeight + DescriptionGap;

        _listArea = new Rect(area.X + ListInsetX, area.Y + ListTop + listOffsetY,
            area.Width - ListWidthDeduction, area.Height - ListHeightDeduction - listOffsetY);

        SetAreaItems(new Rect(_listArea.X + RowInset, _listArea.Y + RowInset,
            _listArea.Width - (RowInset * 2), _listArea.Height - (RowInset * 2)));

        int scrollBarX = area.X + area.Width - ScrollBarRightInset;
        int grooveHeight = _listArea.Height - (ScrollBarTopPartHeight * 2);

        _grooveArea = new Rect(scrollBarX, _listArea.Y, ScrollBarBackgroundWidth, _listArea.Height);

        SetScrollButtonUp(new ButtonSprite(artwork.ScrollUpReleased, artwork.ScrollUpPressed, null,
            scrollBarX + 1, _listArea.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollDownReleased, artwork.ScrollDownPressed, null,
            scrollBarX + 1, _listArea.Y + _listArea.Height - 15));
        SetScrollBarArea(new Rect(scrollBarX + 3, _listArea.Y + ScrollBarTopPartHeight,
            ScrollBarGrooveWidth, grooveHeight));
        SetScrollBarImage(artwork.ScrollSlider);

        DialogButtonArtwork okay = artwork.Okay;
        DialogButtonArtwork cancel = artwork.Cancel;

        _buttons.CreateButton(area.X + ButtonEdgeGapX,
            area.Y + area.Height - okay.Released.Height - ButtonEdgeGapY,
            okay.Released, okay.Pressed, DialogButton.Ok);
        _buttons.CreateButton(area.X + area.Width - cancel.Released.Width - ButtonEdgeGapX,
            area.Y + area.Height - cancel.Released.Height - ButtonEdgeGapY,
            cancel.Released, cancel.Pressed, DialogButton.Cancel);
    }

    /// <summary>The window the list lives in, <c>_window</c> of the reference.</summary>
    public StandardWindow Window => _window;

    /// <summary>
    /// The list area, i.e. the rectangle that holds the rows, the groove and the shading, <c>listRoi</c>.
    /// </summary>
    public Rect ListArea => _listArea;

    /// <summary>
    /// The sixteen pixel wide groove of the scroll bar, i.e. the rectangle the reference hands to
    /// <c>renderScrollbarBackground()</c>: <c>{ activeArea.x + activeArea.width - 25, listRoi.y, 16,
    /// listRoi.height }</c>.
    /// </summary>
    public Rect GrooveArea => _grooveArea;

    /// <summary>The two buttons, in the order okay then cancel.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The heading, <c>_title</c>.</summary>
    public string Title => _title;

    /// <summary>The explanation, or <see langword="null"/> when the window has none, <c>_description</c>.</summary>
    public string? Description => _description;

    /// <summary>
    /// What the last frame of <see cref="ProcessEvents"/> answered, <c>res</c> of the reference's loop;
    /// <see cref="DialogButton.None"/> while the window is still open.
    /// </summary>
    public DialogButton Result { get; private set; }

    /// <summary>Whether <see cref="Result"/> is anything but <see cref="DialogButton.None"/>.</summary>
    public bool IsClosed => Result != DialogButton.None;

    /// <summary>
    /// The tooltip a right click asked for during the last frame, or <see langword="null"/>. Showing it is
    /// the caller's job, because the port has no modal message box inside a window.
    /// </summary>
    /// <remarks>
    /// A subclass sets it from <see cref="ActionListPressRight(ItemSelectionEntry)"/>, which is what the
    /// reference's override does with <c>Dialog::SpellInfo()</c>.
    /// </remarks>
    public DialogPopup? Popup { get; protected set; }

    /// <summary>The white font, <c>FontType::normalWhite()</c>.</summary>
    protected IGlyphSource Font => _font;

    /// <summary>The yellow font, <c>FontType::normalYellow()</c>.</summary>
    protected IGlyphSource HighlightFont => _titleFont;

    /// <summary>
    /// Paints the stone window, the two texts, the list and the two buttons, i.e. the drawing half of the
    /// reference's <c>selectItemsEventProcessing()</c> loop.
    /// </summary>
    /// <remarks>
    /// The shading under the rows is applied here rather than restored from a saved image, because the
    /// scene redraws the whole frame: <see cref="StandardWindow.ApplyTextBackgroundShading"/> on the frame
    /// the window was just painted on is the same pixels the reference's <c>ImageRestorer</c> would put
    /// back. Painting the same frame twice would darken the list area twice, so the window must be composed
    /// into a fresh frame every time.
    /// </remarks>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        Rect area = _window.ActiveArea;

        DrawCentered(target, _titleFont, _title, area, area.Y + 10);

        if (_description is { } description)
        {
            DrawCentered(target, _font, description, area, area.Y + 30);
        }

        Redraw(target);

        _buttons.DrawShadows(target);
        _buttons.Draw(target);
    }

    /// <summary>
    /// Consumes one frame of input, the body of the reference's <c>selectItemsEventProcessing()</c> loop
    /// (<c>dialog_selectitems.cpp:1013-1057</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the reference's: a click or hot key on the buttons first, then the help of a button on
    /// a right click, then the list itself, and finally the double click that answers okay. The reference
    /// keeps looping after a tooltip; the port returns after drawing it, so the row under the same right
    /// click is handled on the next frame.
    /// </para>
    /// <para>
    /// <see cref="Popup"/> and <see cref="Result"/> are overwritten on every call, so a caller reads them
    /// right after the call that asked for them.
    /// </para>
    /// </remarks>
    /// <param name="events">Input of this frame.</param>
    /// <param name="hotKey">The hot key of this frame, if any, <c>HotKeyEvent</c>.</param>
    /// <returns><see langword="true"/> when the frame changed what the window looks like.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, ButtonHotKey? hotKey = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        Popup = null;

        DialogButton clicked = _buttons.ProcessEvents(events, hotKey);

        if (clicked is DialogButton.Ok or DialogButton.Cancel)
        {
            Result = clicked;

            return true;
        }

        // The reference shows the help of the two buttons on a right click on them.
        if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            Popup = new DialogPopup("Okay", "Accept the selection.");

            return true;
        }

        if (events.IsMouseRightButtonPressedInArea(_buttons[1].Area))
        {
            Popup = new DialogPopup("Cancel", "Cancel the selection.");

            return true;
        }

        bool listRedrawn = QueueEventProcessing(events);

        if (!_isDoubleClicked)
        {
            return listRedrawn;
        }

        // The reference leaves the loop with OKAY when the selected row is clicked again.
        _isDoubleClicked = false;
        Result = DialogButton.Ok;

        return true;
    }

    /// <summary>
    /// Paints the shading, the groove and the rows, the reference's <c>RedrawBackground()</c> plus
    /// <see cref="ListBox{T}.Redraw"/>.
    /// </summary>
    /// <param name="destination">Unused; the port keeps absolute coordinates.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        StandardWindow.ApplyTextBackgroundShading(output, _listArea);
        StandardWindow.RenderScrollbarBackground(output, _grooveArea, _artwork.ScrollBarBackground);
    }

    /// <summary>
    /// Does nothing; the reference's base class leaves the arrow keys alone
    /// (<c>dialog_selectitems.h:95</c>).
    /// </summary>
    public override void ActionCurrentUp()
    {
        // Do nothing, as in the reference.
    }

    /// <summary>
    /// Does nothing; the reference's base class leaves the arrow keys alone
    /// (<c>dialog_selectitems.h:95</c>).
    /// </summary>
    public override void ActionCurrentDn()
    {
        // Do nothing, as in the reference.
    }

    /// <summary>
    /// Does nothing; a single click only moves the selection in the reference's base class
    /// (<c>dialog_selectitems.h:95</c>).
    /// </summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListSingleClick(ItemSelectionEntry item)
    {
        // Do nothing, as in the reference.
    }

    /// <summary>
    /// Remembers that the selected row was clicked again; <see cref="ProcessEvents"/> turns that into
    /// <see cref="DialogButton.Ok"/>, <c>_isDoubleClicked</c> of the reference.
    /// </summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListDoubleClick(ItemSelectionEntry item) => _isDoubleClicked = true;

    /// <summary>
    /// Does nothing; the reference's base class has no right click behaviour, and the spell window
    /// overrides it (<c>dialog_selectitems.h:95</c>).
    /// </summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        // Do nothing, as in the reference.
    }

    private static void DrawCentered(PixelBuffer target, IGlyphSource font, string text, Rect area, int y)
    {
        int width = GlyphText.Measure(font, text);

        GlyphText.DrawLine(font, target, new Point(area.X + ((area.Width - width) / 2), y), text);
    }
}
