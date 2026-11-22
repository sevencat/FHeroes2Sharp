using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// A box the adventure map paints over itself while the right button is held: the tail every
/// <c>Dialog::QuickInfo()</c> overload shares in <c>dialog_quickinfo.cpp:604-690</c>.
/// </summary>
/// <remarks>
/// The reference keeps an <c>fheroes2::ImageRestorer</c>, blits the box into the display and waits for the
/// button to be released. The three boxes have nothing else in common — the tile text is a plain frame, the
/// castle and hero panels lay out icons, flags and army lines — so the loop only needs these two members.
/// </remarks>
public interface IQuickInfoPanel
{
    /// <summary>The area the panel covers, which the restorer saves and puts back.</summary>
    Rect Area { get; }

    /// <summary>Paints the panel over the map.</summary>
    /// <param name="target">The frame that already holds the map.</param>
    void Draw(PixelBuffer target);
}
