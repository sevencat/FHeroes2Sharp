using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The clickable, drawable <c>Dialog::SelectCount()</c> window
/// (<c>src/fheroes2/dialog/dialog_selectcount.cpp:79-131</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window is built from <see cref="FrameBox"/> instead of <see cref="StandardWindow"/>: the reference
/// composes a <c>ResizableFrameBox</c> whose height is the header text plus the element, with the button row
/// reserved at the bottom. The laid out chain is
/// </para>
/// <code>
/// headerHeight = header wrapped to 244 pixels, times the line height
/// box          = FrameBox( headerHeight + 10 + 30, showButtons = true )
/// header         drawn at box.Area
/// value        = ValueSelectionElement at ( box.Area.x + 38, box.Area.y + headerHeight + 10 )
/// ok / cancel    laid out by ButtonGroup against box.Area
/// </code>
/// <para>
/// <b>Not ported</b>: the two optional caller widgets above and below the number (the reference keeps them
/// in <c>topUiElement</c> and <c>bottomUiElement</c>; no caller in the port needs them yet) and the MAX/MIN
/// shortcut buttons, whose MIN artwork is synthesized by the reference at startup and has no counterpart
/// in the archive. Cancelling, OK, the arrows and the clamping of the value all behave as the reference
/// does.
/// </para>
/// </remarks>
public sealed class SelectCountView
{
    /// <summary>Gap between the header and the number, <c>elementOffset</c> of the reference.</summary>
    public const int ElementOffset = 10;

    /// <summary>Height the layout reserves for the number, <c>selectionAreaHeight</c> of the reference.</summary>
    public const int SelectionAreaHeight = 30;

    /// <summary>Horizontal inset of the number from the text area, <c>38</c> of the reference.</summary>
    public const int SelectionInset = 38;

    private readonly SelectCountDialog _dialog;
    private readonly IGlyphSource _headerFont;
    private readonly string _header;
    private readonly FrameBox _box;
    private readonly ValueSelectionElement _value;
    private readonly ButtonGroup _buttons;

    private SelectCountView(SelectCountDialog dialog, IGlyphSource headerFont, string header, FrameBox box,
        ValueSelectionElement value, ButtonGroup buttons)
    {
        _dialog = dialog;
        _headerFont = headerFont;
        _header = header;
        _box = box;
        _value = value;
        _buttons = buttons;
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="dialog">The parameters and the answer.</param>
    /// <param name="chrome">Pieces of <c>BUYBUILD.ICN</c> the frame is composed of.</param>
    /// <param name="buttonArtwork">OK and CANCEL artwork.</param>
    /// <param name="headerFont">Font of the header, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="townWindowFrames">Frames of <c>TOWNWIND.ICN</c>, at least
    /// <see cref="ValueSelectionElement.RequiredFrameCount"/>.</param>
    /// <param name="initialValue">Number shown first, clamped into the range.</param>
    /// <returns>The window, or <see langword="null"/> when an asset is missing.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SelectCountView? TryCreate(SelectCountDialog dialog, FrameBoxChrome chrome,
        IDialogButtonArtworkSource buttonArtwork, IGlyphSource headerFont, IReadOnlyList<Sprite> townWindowFrames,
        int initialValue = 0)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(buttonArtwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(townWindowFrames);

        if (townWindowFrames.Count < ValueSelectionElement.RequiredFrameCount)
        {
            return null;
        }

        int headerHeight = GlyphText.WrapLines(headerFont, dialog.Header, FrameBox.AreaWidth).Count *
                           headerFont.LineHeight;
        FrameBox box = FrameBox.Create(
            chrome, headerHeight + ElementOffset + SelectionAreaHeight, showButtons: true);

        Point selectionOrigin = new(box.Area.X + SelectionInset, box.Area.Y + headerHeight + ElementOffset);
        ValueSelectionElement value = new(
            dialog.Minimum, dialog.Maximum, initialValue, dialog.Step, selectionOrigin, headerFont,
            townWindowFrames);

        ButtonGroup buttons = ButtonGroup.CreateStandard(
            box.Area, DialogButton.Ok | DialogButton.Cancel, buttonArtwork);

        return new SelectCountView(dialog, headerFont, dialog.Header, box, value, buttons);
    }

    /// <summary>The parameters and the answer of the window.</summary>
    public SelectCountDialog Dialog => _dialog;

    /// <summary>Text area of the frame, <c>GetArea()</c> of the reference.</summary>
    public Rect Area => _box.Area;

    /// <summary>Bounding box of the frame artwork.</summary>
    public Rect FrameArea => _box.FrameArea;

    /// <summary>The number element.</summary>
    public ValueSelectionElement Value => _value;

    /// <summary>The OK and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>
    /// A left button transition over the window.
    /// </summary>
    /// <remarks>
    /// The number is fed first: its arrows are 20x10 and do not overlap the button row, so the order only
    /// matters for the pressed state. On OK the chosen number is written into
    /// <see cref="SelectCountDialog.Result"/>, on CANCEL the result stays <see langword="null"/>.
    /// </remarks>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>The pressed button on release, or <see cref="DialogButton.None"/> when nothing was pressed.</returns>
    public DialogButton SetLeftButton(bool down, Point point)
    {
        _value.SetLeftButton(down, point);
        _buttons.SetCursor(point);
        DialogButton answer = _buttons.SetLeftButton(down);

        if (answer == DialogButton.Ok)
        {
            _dialog.Choose(_value.Value);
        }
        else if (answer == DialogButton.Cancel)
        {
            _dialog.Choose(null);
        }

        return answer;
    }

    /// <summary>Draws the frame, the header, the number and the buttons, in the order of the reference.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _box.Draw(target);
        GlyphText.DrawMultiline(_headerFont, target, _box.Area.Position, _header, FrameBox.AreaWidth);
        _value.Draw(target);
        _buttons.Draw(target);
        _buttons.DrawShadows(target);
    }
}
