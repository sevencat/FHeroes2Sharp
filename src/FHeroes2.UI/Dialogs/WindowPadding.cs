namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where an item sits inside the active area of a <see cref="StandardWindow"/>, the
/// <c>fheroes2::StandardWindow::Padding</c> enumeration (<c>src/fheroes2/gui/ui_window.h:39-50</c>).
/// </summary>
/// <remarks>
/// The reference spells the nine places out as an enumeration and a switch with an <c>assert( 0 )</c> in the
/// default branch; the port keeps the enumeration so a caller reads like the reference call sites do, and
/// <see cref="StandardWindow.RenderPosition"/> refuses a value that is not one of the nine.
/// </remarks>
public enum WindowPadding
{
    /// <summary>Top left corner, offset to the right and down.</summary>
    TopLeft,

    /// <summary>Top edge, horizontally centred.</summary>
    TopCenter,

    /// <summary>Top right corner, offset to the left and down.</summary>
    TopRight,

    /// <summary>Left edge, vertically centred.</summary>
    CenterLeft,

    /// <summary>The middle of the window.</summary>
    CenterCenter,

    /// <summary>Right edge, vertically centred.</summary>
    CenterRight,

    /// <summary>Bottom left corner, offset to the right and up.</summary>
    BottomLeft,

    /// <summary>Bottom edge, horizontally centred.</summary>
    BottomCenter,

    /// <summary>Bottom right corner, offset to the left and up.</summary>
    BottomRight,
}
