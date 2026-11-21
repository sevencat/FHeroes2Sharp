using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The standard OK and CANCEL pair of <c>SYSTEM.ICN</c>, the one every <c>Dialog::FrameBox</c>
/// draws at the bottom of its frame.
/// </summary>
/// <remarks>
/// 帧号与 <see cref="SettingsArtwork"/> 同源：1/2 是常态与按下的 OKAY，3/4 是常态与按下的 CANCEL。
/// 设置对话框只用 OKAY，所以自己留了一份；这里两个都要，就单独装一个。
/// </remarks>
internal sealed class SystemButtonArtwork : IDialogButtonArtworkSource
{
    /// <summary>Archive entry that holds the standard buttons.</summary>
    internal const string AssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released OKAY button.</summary>
    internal const int OkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OKAY button.</summary>
    internal const int OkayPressedFrame = 2;

    /// <summary>Frame of the released CANCEL button.</summary>
    internal const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed CANCEL button.</summary>
    internal const int CancelPressedFrame = 4;

    private readonly Sprite _okayReleased;
    private readonly Sprite _okayPressed;
    private readonly Sprite _cancelReleased;
    private readonly Sprite _cancelPressed;

    private SystemButtonArtwork(Sprite okayReleased, Sprite okayPressed, Sprite cancelReleased, Sprite cancelPressed)
    {
        _okayReleased = okayReleased;
        _okayPressed = okayPressed;
        _cancelReleased = cancelReleased;
        _cancelPressed = cancelPressed;
    }

    /// <summary>Returns the pair of frames a button stands for.</summary>
    /// <param name="button">The button to draw.</param>
    /// <returns>Released and pressed frames.</returns>
    public DialogButtonArtwork Get(DialogButton button) => button == DialogButton.Cancel
        ? new DialogButtonArtwork(_cancelReleased, _cancelPressed)
        : new DialogButtonArtwork(_okayReleased, _okayPressed);

    /// <summary>Loads the four frames from the archives.</summary>
    /// <param name="assets">The loaded archives.</param>
    /// <returns>The artwork, or <see langword="null"/> when <c>SYSTEM.ICN</c> is missing or too short.</returns>
    internal static SystemButtonArtwork? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        try
        {
            if (assets.TryReadIcn(AssetName) is not { } system || system.Count <= CancelPressedFrame)
            {
                GameLog.Warn($"The standard buttons of '{AssetName}' are missing; the dialog cannot be drawn.");

                return null;
            }

            return new SystemButtonArtwork(
                system.Sprites[OkayReleasedFrame],
                system.Sprites[OkayPressedFrame],
                system.Sprites[CancelReleasedFrame],
                system.Sprites[CancelPressedFrame]);
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The standard buttons of '{AssetName}' could not be read: {exception.Message}");

            return null;
        }
    }
}
