using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.App.Scenes;

/// <summary>
/// Loads the artwork of the adventure options dialog: the stone window, the four big buttons of
/// <c>APANEL.ICN</c> and the cancel button of <c>SYSTEM.ICN</c>.
/// </summary>
/// <remarks>
/// The ICN names live in <see cref="AdventureOptionsView"/> because the dialog owns its layout; this
/// class only resolves them against the archive and turns a missing entry into a warning instead of an
/// exception, the way <see cref="SettingsArtwork"/> does. An adventure map opened without the artwork
/// simply has no options dialog.
/// </remarks>
internal static class AdventureOptionsArtwork
{
    /// <summary>
    /// Builds the dialog view from the archives.
    /// </summary>
    /// <param name="assets">Archives of the game data.</param>
    /// <returns><see langword="null"/> when an entry is missing or cannot be read.</returns>
    internal static AdventureOptionsView? TryCreate(AssetBundle? assets)
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
            IcnSpriteFile? buttons = assets.TryReadIcn(AdventureOptionsView.ButtonsAssetName);
            IcnSpriteFile? system = assets.TryReadIcn(AdventureOptionsView.CancelAssetName);

            StandardWindowChrome? chrome = StandardWindowChrome.TryCreate(
                horizontal?.Sprites, vertical?.Sprites, background?.Sprites);

            AdventureOptionsView? view = AdventureOptionsView.TryCreate(chrome, buttons?.Sprites, system?.Sprites);

            if (view is null)
            {
                GameLog.Warn(
                    "The adventure options dialog artwork is missing " +
                    $"('{StandardWindowChrome.HorizontalAssetName}', '{StandardWindowChrome.VerticalAssetName}', " +
                    $"'{StandardWindowChrome.BackgroundAssetName}', '{AdventureOptionsView.ButtonsAssetName}' or " +
                    $"'{AdventureOptionsView.CancelAssetName}'); the options door stays inactive.");
            }

            return view;
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The adventure options dialog artwork could not be read: {exception.Message}");
            return null;
        }
    }
}
