using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The artwork the settings dialog is composed of: the stone frame of every Heroes II window and
/// the standard OK button.
/// </summary>
/// <remarks>
/// The ICN names live here rather than in <see cref="StandardWindowChrome"/> or in the UI layer,
/// because <c>UNIFORM_GOOD_OKAY_BUTTON</c> is one of the many button tables the original keeps in
/// <c>heroes2.agg</c>; the dialog itself only ever asks for a
/// <see cref="DialogButtonArtwork"/>. Only the OK button is resolved - the reference settings
/// dialog closes with OK and its Escape key, so there is no CANCEL pair to draw.
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

    private readonly Sprite _okayReleased;
    private readonly Sprite _okayPressed;

    private SettingsArtwork(StandardWindowChrome chrome, Sprite okayReleased, Sprite okayPressed)
    {
        Chrome = chrome;
        _okayReleased = okayReleased;
        _okayPressed = okayPressed;
    }

    /// <summary>Frame and background of the dialog window.</summary>
    internal StandardWindowChrome Chrome { get; }

    /// <summary>Height of the OK button, which is what the window has to reserve at the bottom.</summary>
    internal int OkayHeight => _okayReleased.Height;

    /// <summary>
    /// Returns the OK sprites for every button; the dialog only places OK, so a CANCEL request would
    /// get the same artwork and is therefore never made.
    /// </summary>
    public DialogButtonArtwork Get(DialogButton button) => new(_okayReleased, _okayPressed);

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
            IcnSpriteFile? horizontal = assets.TryReadIcn(StandardWindowChrome.HorizontalAssetName);
            IcnSpriteFile? vertical = assets.TryReadIcn(StandardWindowChrome.VerticalAssetName);
            IcnSpriteFile? background = assets.TryReadIcn(StandardWindowChrome.BackgroundAssetName);
            IcnSpriteFile? okay = assets.TryReadIcn(OkayAssetName);

            StandardWindowChrome? chrome = StandardWindowChrome.TryCreate(
                horizontal?.Sprites, vertical?.Sprites, background?.Sprites);

            if (chrome is null || okay is null || okay.Count <= OkayPressedFrame)
            {
                GameLog.Warn(
                    $"The settings dialog artwork is missing ('{StandardWindowChrome.HorizontalAssetName}', " +
                    $"'{StandardWindowChrome.VerticalAssetName}', '{StandardWindowChrome.BackgroundAssetName}' " +
                    $"or '{OkayAssetName}'); the settings door stays inactive.");
                return null;
            }

            return new SettingsArtwork(chrome, okay.Sprites[OkayReleasedFrame], okay.Sprites[OkayPressedFrame]);
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The settings dialog artwork could not be read: {exception.Message}");
            return null;
        }
    }
}
