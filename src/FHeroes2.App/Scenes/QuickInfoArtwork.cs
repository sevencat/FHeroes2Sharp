using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.App.Scenes;

/// <summary>
/// Loads the frame of the right button popup of the adventure map, <c>QWIKINFO.ICN</c>.
/// </summary>
/// <remarks>
/// One frame, as wide as the widest text the reference ever puts in it; the layout lives in
/// <see cref="StandardPopupWindow"/>. A missing frame only costs the popup, the way a missing piece of the
/// options dialog only costs the dialog.
/// </remarks>
internal static class QuickInfoArtwork
{
    /// <summary>
    /// Reads the popup frame from the archives.
    /// </summary>
    /// <param name="assets">Archives of the game data.</param>
    /// <returns><see langword="null"/> when the frame is missing or cannot be read.</returns>
    internal static Sprite? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        try
        {
            IcnSpriteFile? file = assets.TryReadIcn(StandardPopupWindow.AssetName);
            Sprite? frame = file is { Sprites.Count: > 0 } ? file.Sprites[0] : null;

            if (frame is null)
            {
                GameLog.Warn(
                    $"The quick info artwork is missing ('{StandardPopupWindow.AssetName}'); " +
                    "the right button shows no popup.");
            }

            return frame;
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The quick info artwork could not be read: {exception.Message}");
            return null;
        }
    }
}
