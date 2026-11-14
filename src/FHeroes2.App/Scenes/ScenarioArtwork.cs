using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The artwork the scenario selection dialog is composed of, read from the archives: the panel and
/// its shadow of <c>REQSBKG.ICN</c>, the icons and button frames of <c>REQUESTS.ICN</c>, the slider
/// of <c>ESCROLL.ICN</c> and the standard OKAY button of <c>SYSTEM.ICN</c>. The product badges of
/// the map type column are synthesised from <c>ARTFX.ICN</c>, the way <c>ICN::MAP_TYPE_ICON</c> is
/// built at run time by the reference: that table never existed as a file.
/// </summary>
/// <remarks>
/// Like <see cref="SettingsArtwork"/> this type lives in the client: which ICN table holds which
/// sprite is knowledge of the archive layout, while <see cref="ScenarioSelectionDialog"/> only ever
/// asks for a <see cref="ScenarioDialogArtwork"/>. The panel and the icons are mandatory (without
/// them the dialog is not usable), the slider and the map type badges are not: a trimmed archive
/// only costs the scroll thumb and the product column, exactly the way the reference treats a
/// missing <c>ICN::UNKNOWN</c> image.
/// </remarks>
internal static class ScenarioArtwork
{
    /// <summary>Table that holds the dialog panel (frame 0) and its shadow (frame 1).</summary>
    internal const string PanelAssetName = "REQSBKG.ICN";

    /// <summary>Frame of the 377x458 dialog panel.</summary>
    internal const int PanelFrame = 0;

    /// <summary>Frame of the shadow that hangs below and left of the panel.</summary>
    internal const int ShadowFrame = 1;

    /// <summary>Table of the 17x17 icons and of the button frame pairs.</summary>
    internal const string IconAssetName = "REQUESTS.ICN";

    /// <summary>
    /// Number of sprites the dialog needs: the last loss condition icon is
    /// <see cref="ScenarioDialogArtwork.LossIconIndex"/> plus its four conditions.
    /// </summary>
    internal const int IconCount = ScenarioDialogArtwork.LossIconIndex + 4;

    /// <summary>Table of the scroll bar sliders.</summary>
    internal const string SliderAssetName = "ESCROLL.ICN";

    /// <summary>Frame of the horizontal slider the list maps onto its groove.</summary>
    internal const int SliderFrame = 3;

    /// <summary>
    /// Table the product badges of the map type column are scaled from. The reference synthesises
    /// <c>ICN::MAP_TYPE_ICON</c> at run time as well: the artwork never shipped as its own file.
    /// </summary>
    internal const string MapTypeAssetName = "ARTFX.ICN";

    /// <summary>Frames of <c>ARTFX.ICN</c> of the three badges, <c>ICN::MAP_TYPE_ICON</c> 0…2.</summary>
    internal static IReadOnlyList<int> MapTypeFrames { get; } = [6, 90, 101];

    /// <summary>Number of product badges: Succession Wars, Price of Loyalty and Resurrection.</summary>
    internal const int MapTypeCount = 3;

    /// <summary>Side of a badge inside its icon cell, the <c>Resize( …, 1, 1, 15, 15 )</c> of the reference.</summary>
    internal const int MapTypeBadgeSize = ScenarioDialogArtwork.IconSize - 2;

    /// <summary>Table of the standard dialog buttons; frames 1/2 are the released/pressed OKAY.</summary>
    internal const string ButtonAssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released OKAY button.</summary>
    internal const int OkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OKAY button.</summary>
    internal const int OkayPressedFrame = 2;

    /// <summary>Loads the artwork from the archives.</summary>
    /// <param name="assets">Open archives, or <see langword="null"/> when the game data is missing.</param>
    /// <returns>
    /// <see langword="null"/> when the panel, the icon table or the buttons are missing; the dialog
    /// then stays closed and the menu keeps its status bar message.
    /// </returns>
    internal static ScenarioDialogArtwork? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        try
        {
            IcnSpriteFile? panel = assets.TryReadIcn(PanelAssetName);
            IcnSpriteFile? icons = assets.TryReadIcn(IconAssetName);
            IcnSpriteFile? buttons = assets.TryReadIcn(ButtonAssetName);

            if (panel is null || panel.Count <= ShadowFrame || icons is null || icons.Count < IconCount
                || buttons is null || buttons.Count <= OkayPressedFrame)
            {
                GameLog.Warn(
                    $"The scenario dialog artwork is missing ('{PanelAssetName}', '{IconAssetName}' or " +
                    $"'{ButtonAssetName}'); the New Game sign stays inactive.");
                return null;
            }

            IcnSpriteFile? slider = assets.TryReadIcn(SliderAssetName);
            if (slider is null || slider.Count <= SliderFrame)
            {
                GameLog.Warn($"'{SliderAssetName}' has no slider frame {SliderFrame}; the list will have no thumb.");
                slider = null;
            }

            IcnSpriteFile? badges = assets.TryReadIcn(MapTypeAssetName);
            if (badges is null)
            {
                GameLog.Warn($"'{MapTypeAssetName}' is missing; the map type column stays empty.");
            }

            return new ScenarioDialogArtwork(
                panel.Sprites[PanelFrame],
                panel.Sprites[ShadowFrame],
                icons.Sprites,
                slider?.Sprites[SliderFrame],
                new DialogButtonArtwork(buttons.Sprites[OkayReleasedFrame], buttons.Sprites[OkayPressedFrame]),
                MapTypeIcons(badges));
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The scenario dialog artwork could not be read: {exception.Message}");
            return null;
        }
    }

    /// <summary>The three product badges scaled into 17x17 icon cells, or <see langword="null"/>.</summary>
    private static IReadOnlyList<Sprite>? MapTypeIcons(IcnSpriteFile? file)
    {
        if (file is null)
        {
            return null;
        }

        List<Sprite> icons = [];

        foreach (int frame in MapTypeFrames)
        {
            icons.Add(
                frame < file.Count
                    ? ScaleBadge(file.Sprites[frame])
                    : new Sprite(ScenarioDialogArtwork.IconSize, ScenarioDialogArtwork.IconSize));
        }

        return icons;
    }

    /// <summary>
    /// Scales a badge of <c>ARTFX.ICN</c> into the 15x15 centre of a 17x17 icon cell, the
    /// <c>Resize( badge, 0, 0, badge.width(), badge.height(), cell, 1, 1, 15, 15 )</c> the reference
    /// runs for <c>ICN::MAP_TYPE_ICON</c>. The cell stays transparent around the badge, palette
    /// index 0 of the source keeps its transparency as well.
    /// </summary>
    private static Sprite ScaleBadge(Sprite source)
    {
        PixelBuffer cell = new(ScenarioDialogArtwork.IconSize, ScenarioDialogArtwork.IconSize);

        for (int y = 0; y < MapTypeBadgeSize; ++y)
        {
            int sourceY = y * source.Height / MapTypeBadgeSize;

            for (int x = 0; x < MapTypeBadgeSize; ++x)
            {
                int sourceX = x * source.Width / MapTypeBadgeSize;
                cell[1 + x, 1 + y] = source.Image[sourceX, sourceY];
            }
        }

        return new Sprite(cell);
    }
}
