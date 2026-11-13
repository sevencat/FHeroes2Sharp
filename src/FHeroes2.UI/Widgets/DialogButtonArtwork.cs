using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The two looks of one standard dialog button, i.e. frames 0 and 1 of an ICN such as
/// <c>UNIFORM_GOOD_OKAY_BUTTON</c>.
/// </summary>
/// <param name="Released">Sprite drawn while the button is idle.</param>
/// <param name="Pressed">Sprite drawn while the button is held down.</param>
public readonly record struct DialogButtonArtwork(Sprite Released, Sprite Pressed);

/// <summary>
/// Resolves the artwork of the standard dialog buttons, the <c>ICN::UNIFORM_*_BUTTON</c> and
/// <c>ICN::BUTTON_SMALL_YES/NO_*</c> lookups of <c>fheroes2::ButtonGroup</c>.
/// </summary>
/// <remarks>
/// The reference picks the archive ids itself and branches on the interface type; the port asks the
/// caller for a sprite pair instead, so <see cref="ButtonGroup"/> does not need to know about ICN
/// ids or about the good/evil setting.
/// </remarks>
public interface IDialogButtonArtworkSource
{
    /// <summary>Returns the released and pressed sprite of <paramref name="button"/>.</summary>
    /// <param name="button">One of <see cref="DialogButton.Yes"/>, <see cref="DialogButton.No"/>,
    /// <see cref="DialogButton.Ok"/> or <see cref="DialogButton.Cancel"/>.</param>
    /// <returns>The artwork; both sprites are expected to be non empty.</returns>
    DialogButtonArtwork Get(DialogButton button);
}
