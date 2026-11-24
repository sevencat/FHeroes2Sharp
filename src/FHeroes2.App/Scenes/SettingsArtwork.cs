using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The artwork the dialogs of the main menu are composed of: the standard OK and CANCEL buttons. The
/// stone frame of the window is read by <see cref="StandardWindow"/> itself.
/// </summary>
/// <remarks>
/// The ICN names live here rather than in the UI layer, because <c>UNIFORM_GOOD_OKAY_BUTTON</c> is one
/// of the many button tables the original keeps in <c>heroes2.agg</c>; the dialogs themselves only ever
/// ask for a <see cref="DialogButtonArtwork"/>. The settings dialog closes with OK and its Escape key,
/// so it places OK alone; the language picker of the first run carries both buttons, hence the CANCEL
/// pair. An archive without the CANCEL frames (frames 3 and 4) degrades to OK only, see
/// <see cref="HasCancel"/>.
/// </remarks>
internal sealed class SettingsArtwork : IDialogButtonArtworkSource
{
    /// <summary>
    /// Archive entry that holds the standard buttons: frames 1 and 2 are the released and pressed
    /// OKAY button, frames 3 and 4 the CANCEL one.
    /// </summary>
    internal const string OkayAssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released OKAY button in <see cref="OkayAssetName"/>.</summary>
    internal const int OkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OKAY button in <see cref="OkayAssetName"/>.</summary>
    internal const int OkayPressedFrame = 2;

    /// <summary>Frame of the released CANCEL button in <see cref="OkayAssetName"/>.</summary>
    internal const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed CANCEL button in <see cref="OkayAssetName"/>.</summary>
    internal const int CancelPressedFrame = 4;

    private readonly Sprite _okayReleased;
    private readonly Sprite _okayPressed;
    private readonly Sprite? _cancelReleased;
    private readonly Sprite? _cancelPressed;

    private SettingsArtwork(
        Sprite okayReleased, Sprite okayPressed, Sprite? cancelReleased, Sprite? cancelPressed)
    {
        _okayReleased = okayReleased;
        _okayPressed = okayPressed;
        _cancelReleased = cancelReleased;
        _cancelPressed = cancelPressed;
    }

    /// <summary>Height of the OK button, which is what the window has to reserve at the bottom.</summary>
    internal int OkayHeight => _okayReleased.Height;

    /// <summary>
    /// Whether <see cref="OkayAssetName"/> carries the CANCEL pair, i.e. whether a dialog can place
    /// <see cref="DialogButton.Cancel"/> next to OK.
    /// </summary>
    internal bool HasCancel => _cancelReleased is not null && _cancelPressed is not null;

    /// <summary>
    /// Returns the sprites of the requested button; a CANCEL request falls back to the OK pair when the
    /// archive has no CANCEL frames.
    /// </summary>
    public DialogButtonArtwork Get(DialogButton button) =>
        button == DialogButton.Cancel && _cancelReleased is { } released && _cancelPressed is { } pressed
            ? new DialogButtonArtwork(released, pressed)
            : new DialogButtonArtwork(_okayReleased, _okayPressed);

    /// <summary>Loads the artwork from the archives.</summary>
    /// <returns><see langword="null"/> when one of the entries is missing.</returns>
    internal static SettingsArtwork? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        try
        {
            IcnSpriteFile? okay = assets.TryReadIcn(OkayAssetName);

            if (okay is null || okay.Count <= OkayPressedFrame)
            {
                GameLog.Warn(
                    $"The settings dialog artwork is missing ('{OkayAssetName}'); the settings door stays inactive.");
                return null;
            }

            bool hasCancel = okay.Count > CancelPressedFrame;

            if (!hasCancel)
            {
                GameLog.Warn(
                    $"'{OkayAssetName}' has {okay.Count} sprites instead of {CancelPressedFrame + 1}; " +
                    "the language picker will only offer OK.");
            }

            return new SettingsArtwork(
                okay.Sprites[OkayReleasedFrame],
                okay.Sprites[OkayPressedFrame],
                hasCancel ? okay.Sprites[CancelReleasedFrame] : null,
                hasCancel ? okay.Sprites[CancelPressedFrame] : null);
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The settings dialog artwork could not be read: {exception.Message}");
            return null;
        }
    }
}
