using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// An interactive element inside a message box, a port of <c>fheroes2::DialogElement</c>
/// (<c>src/fheroes2/gui/ui_dialog.h:56-85</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The reference passes a <c>Point</c> and reads the screen; the port passes events and returns a result.</b> The
/// reference's <c>processEvents( const Point &amp; offset )</c> reaches for <c>LocalEvent::Get()</c> and pops a window
/// from inside itself (<c>showStandardTextMessage</c>), which cannot be tested and cannot be replayed. The port keeps
/// the two halves apart: <see cref="ProcessEvents"/> answers «repaint» and «open this popup», the owner of the dialog
/// does the popping.
/// </para>
/// <para>
/// <b>Elements are laid out by <c>showMessage</c>, not by themselves.</b> <see cref="Area"/> is the size the element
/// needs; the row packer decides where it lands and passes that position as the <c>offset</c> of
/// <see cref="Draw"/>. An element therefore never knows its absolute position.
/// </para>
/// </remarks>
public abstract class DialogElement
{
    /// <summary>Size the element needs, <c>_area</c> of the reference.</summary>
    public Size Area { get; protected set; }

    /// <summary>Paints the element, <c>draw( output, offset )</c>.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position the layout gave the element.</param>
    public abstract void Draw(PixelBuffer output, Point offset);

    /// <summary>
    /// Handles one frame of input, <c>processEvents( offset )</c>.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="offset">Position the layout gave the element.</param>
    /// <returns>What the owner has to do, empty when the element does not react.</returns>
    public virtual DialogElementResult ProcessEvents(LocalEvent events, Point offset) => default;

    /// <summary>Refreshes animated content, <c>update( output, offset )</c>; nothing by default.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position the layout gave the element.</param>
    /// <returns><see langword="true"/> when something was painted.</returns>
    public virtual bool Update(PixelBuffer output, Point offset) => false;
}

/// <summary>
/// A text run that takes the whole width of the message box, <c>TextDialogElement</c>
/// (<c>ui_dialog.h:91-107</c>, <c>ui_dialog.cpp:329-349</c>).
/// </summary>
public sealed class TextDialogElement : DialogElement
{
    private readonly TextBase _text;

    /// <summary>Creates the element and sizes it to the text wrapped at <see cref="FrameBox.AreaWidth"/>.</summary>
    /// <param name="text">The text; the element keeps a reference, the caller must not mutate it afterwards.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public TextDialogElement(TextBase text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;
        Area = new Size(FrameBox.AreaWidth, text.Height(FrameBox.AreaWidth));
    }

    /// <summary>Draws the text at the offset the layout gave it.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the text.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        _text.DrawInRoi(offset.X, offset.Y, FrameBox.AreaWidth, output, output.Bounds);
    }
}

/// <summary>
/// An arbitrary image, <c>CustomImageDialogElement</c> (<c>ui_dialog.h:109-127</c>, <c>ui_dialog.cpp:351-376</c>).
/// </summary>
/// <remarks>The reference stores the image by value; the port takes the sprite as is and the caller keeps it alive.</remarks>
public sealed class CustomImageDialogElement : DialogElement
{
    private readonly Sprite _image;

    /// <summary>Creates the element and sizes it to the image.</summary>
    /// <param name="image">The image to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public CustomImageDialogElement(Sprite image)
    {
        ArgumentNullException.ThrowIfNull(image);

        _image = image;
        Area = new Size(image.Width, image.Height);
    }

    /// <summary>Draws the image without its own offset, the <c>Blit( image, 0, 0, ... )</c> of the reference.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the image.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _image);
    }
}

/// <summary>
/// A resource icon with an optional amount under it, <c>ResourceDialogElement</c>
/// (<c>ui_dialog.h:146-163</c>, <c>ui_dialog.cpp:404-454</c>).
/// </summary>
/// <remarks>
/// The icon is centred over the quantity when the quantity is wider than the icon, so a narrow resource with a five
/// digit amount keeps a straight column. The right click popup is the resource name and its description, which the
/// reference takes from <c>Resource::String</c>/<c>Resource::getDescription</c>; the port takes both as text because
/// the translation layer is deliberately out of scope (ADR-139).
/// </remarks>
public sealed class ResourceDialogElement : DialogElement
{
    /// <summary>Gap between the icon and the quantity, <c>textOffsetFromElement</c>.</summary>
    public const int TextOffsetFromElement = 2;

    private readonly Sprite _icon;
    private readonly string _quantity;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Creates the element, <c>ResourceDialogElement( resourceType, text )</c>.</summary>
    /// <param name="icon">The resource icon, <c>RESOURCE.ICN</c> frame of the type.</param>
    /// <param name="quantity">
    /// Amount printed under the icon; an empty string makes the element exactly the size of the icon.
    /// </param>
    /// <param name="popupHeader">Header of the right click popup, usually the resource name.</param>
    /// <param name="popupBody">Body of the right click popup, usually the resource description.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ResourceDialogElement(Sprite icon, string quantity, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(popupHeader);
        ArgumentNullException.ThrowIfNull(popupBody);

        _icon = icon;
        _quantity = quantity;
        _popupHeader = popupHeader;
        _popupBody = popupBody;

        if (quantity.Length == 0)
        {
            Area = new Size(icon.Width, icon.Height);
        }
        else
        {
            var quantityText = new Text(quantity, FontType.SmallWhite());

            Area = new Size(
                Math.Max(icon.Width, quantityText.Width()),
                icon.Height + TextOffsetFromElement + quantityText.Height());
        }
    }

    /// <summary>Draws the icon and, when there is one, the amount under it.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the element.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (_quantity.Length == 0)
        {
            ImageOps.BlitSprite(output, offset, _icon);

            return;
        }

        var quantityText = new Text(_quantity, FontType.SmallWhite());

        int maxWidth = Math.Max(_icon.Width, quantityText.Width());

        ImageOps.BlitSprite(output, new Point(offset.X + ((maxWidth - _icon.Width) / 2), offset.Y), _icon);

        quantityText.DrawInRoi(
            offset.X + ((maxWidth - quantityText.Width()) / 2),
            offset.Y + _icon.Height + TextOffsetFromElement,
            maxWidth,
            output,
            output.Bounds);
    }

    /// <summary>Asks for the resource popup on a right click, the only event the reference handles.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="offset">Position of the element.</param>
    /// <returns>The popup, or an empty result.</returns>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>A standard message the owner of a dialog has to open, the <c>showStandardTextMessage</c> of the reference.</summary>
/// <param name="Header">Yellow header line.</param>
/// <param name="Body">White body text.</param>
public readonly record struct DialogPopup(string Header, string Body);

/// <summary>What one frame of <see cref="DialogElement.ProcessEvents"/> asks its owner to do.</summary>
/// <param name="RequiresRedraw">Whether the element painted something and the dialog has to be repainted.</param>
/// <param name="Popup">A popup the element asked for, or <see langword="null"/>.</param>
public readonly record struct DialogElementResult(bool RequiresRedraw, DialogPopup? Popup = null);
