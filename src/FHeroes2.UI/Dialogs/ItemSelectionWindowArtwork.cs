using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork an <see cref="ItemSelectionWindow"/> needs, i.e. everything the reference loads from
/// <c>ICN::SCROLL</c>, <c>ICN::SCROLLE</c>, <c>ICN::ADVBORD</c> and the small okay/cancel buttons in its
/// constructor (<c>dialog_selectitems.cpp:909-1057</c>).
/// </summary>
/// <remarks>
/// The reference reads the archive inside the dialog. The port keeps the widget layer free of the archive
/// and hands the frames in, the same way <see cref="KingdomOverviewArtwork"/> does: the caller decides
/// between the good and the evil interface, and tests can build the sprites without an AGG file.
/// </remarks>
public sealed class ItemSelectionWindowArtwork
{
    /// <summary>
    /// The groove of the scroll bar, frame 0 of <c>ICN::ADVBORD</c> —
    /// <see cref="StandardWindow.RenderScrollbarBackground"/> slices it.
    /// </summary>
    public required Sprite ScrollBarBackground { get; init; }

    /// <summary>The step-up button, frame 0 of <c>ICN::SCROLL</c> or <c>ICN::SCROLLE</c>.</summary>
    public required Sprite ScrollUpReleased { get; init; }

    /// <summary>The pressed step-up button, frame 1 of the same ICN.</summary>
    public required Sprite ScrollUpPressed { get; init; }

    /// <summary>The step-down button, frame 2 of the same ICN.</summary>
    public required Sprite ScrollDownReleased { get; init; }

    /// <summary>The pressed step-down button, frame 3 of the same ICN.</summary>
    public required Sprite ScrollDownPressed { get; init; }

    /// <summary>The thumb before it is stretched to the list length, frame 4 of the same ICN.</summary>
    public required Sprite ScrollSlider { get; init; }

    /// <summary>The okay button, <c>ICN::BUTTON_SMALL_OKAY_GOOD</c> frames 0 and 1 in the reference.</summary>
    public required DialogButtonArtwork Okay { get; init; }

    /// <summary>The cancel button, <c>ICN::BUTTON_SMALL_CANCEL_GOOD</c> frames 0 and 1 in the reference.</summary>
    public required DialogButtonArtwork Cancel { get; init; }
}
