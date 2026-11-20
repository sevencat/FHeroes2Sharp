using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The sprites of the gift dialog: the resource cells and player boxes of <c>TRADPOST.ICN</c>/<c>CELLWIN.ICN</c> and
/// the small OKAY and CANCEL buttons (<c>dialog_giftresources.cpp:245-278</c>).
/// </summary>
/// <remarks>
/// <para>
/// The resource row reuses the marketplace cells: frames 7 to 13 of <c>TRADPOST.ICN</c> are the seven resource
/// icons, the same frames <see cref="MarketplaceArtwork.Cell"/> hands out. The recipient boxes are frames 43 to 48
/// of <c>CELLWIN.ICN</c>, one per player colour in the order of <see cref="PlayerColors"/>, with frame 2 as the
/// check mark that the reference blits two pixels into the box; the editor's <c>Checkbox</c> reads the same frames.
/// </para>
/// <para>
/// The buttons are <c>BUTTON_SMALL_OKAY_GOOD</c>/<c>_EVIL</c> and <c>BUTTON_SMALL_CANCEL_GOOD</c>/<c>_EVIL</c>,
/// frames 0 and 1 each. Like <see cref="MarketplaceArtwork"/>, the caller passes the frames, because the port has
/// no button table generator and the interface type is still a setting nobody toggles.
/// </para>
/// </remarks>
public sealed class GiftWindowArtwork
{
    /// <summary>Frame of the wood cell, <c>7</c>.</summary>
    public const int FirstCellFrame = 7;

    /// <summary>Number of frames of <c>TRADPOST.ICN</c> the dialog reads, <c>14</c>.</summary>
    public const int RequiredTradpostFrames = FirstCellFrame + GiftWindowLayout.ResourceCount;

    /// <summary>Frame of the blue box, <c>43</c>.</summary>
    public const int FirstBoxFrame = 43;

    /// <summary>Frame of the check mark, <c>2</c>.</summary>
    public const int CheckmarkFrame = 2;

    /// <summary>Number of frames of <c>CELLWIN.ICN</c> the dialog reads, <c>49</c>.</summary>
    public const int RequiredCellwinFrames = FirstBoxFrame + 6;

    private GiftWindowArtwork(IReadOnlyList<Sprite> tradpost, IReadOnlyList<Sprite> cellwin,
        DialogButtonArtwork okButton, DialogButtonArtwork cancelButton)
    {
        Tradpost = tradpost;
        Cellwin = cellwin;
        OkButton = okButton;
        CancelButton = cancelButton;
    }

    /// <summary>Every frame of <c>TRADPOST.ICN</c> the dialog reads.</summary>
    public IReadOnlyList<Sprite> Tradpost { get; }

    /// <summary>Every frame of <c>CELLWIN.ICN</c> the dialog reads.</summary>
    public IReadOnlyList<Sprite> Cellwin { get; }

    /// <summary>The OKAY button, <c>BUTTON_SMALL_OKAY_GOOD</c> or its evil twin.</summary>
    public DialogButtonArtwork OkButton { get; }

    /// <summary>The CANCEL button, <c>BUTTON_SMALL_CANCEL_GOOD</c> or its evil twin.</summary>
    public DialogButtonArtwork CancelButton { get; }

    /// <summary>The check mark that marks a selected recipient, frame 2 of <c>CELLWIN.ICN</c>.</summary>
    public Sprite Checkmark => Cellwin[CheckmarkFrame];

    /// <summary>A resource cell, frames 7 to 13 of <c>TRADPOST.ICN</c>.</summary>
    /// <param name="index">Index of the resource in the icon table, 0 to 6.</param>
    /// <returns>The cell.</returns>
    public Sprite Cell(int index) => Tradpost[FirstCellFrame + index];

    /// <summary>Size of a resource cell, which every cell shares.</summary>
    public Core.Geometry.Size CellSize => new(Cell(0).Width, Cell(0).Height);

    /// <summary>The box of a player colour, frames 43 to 48 of <c>CELLWIN.ICN</c>.</summary>
    /// <param name="color">One of the six playable colours.</param>
    /// <returns>The box without the check mark.</returns>
    public Sprite Box(PlayerColor color) => Cellwin[FirstBoxFrame + PlayerColors.IndexOf(color)];

    /// <summary>Size of a recipient box, which every box shares.</summary>
    public Core.Geometry.Size BoxSize => new(Box(PlayerColors.Ordered[0]).Width, Box(PlayerColors.Ordered[0]).Height);

    /// <summary>
    /// Collects the sprites of the dialog.
    /// </summary>
    /// <param name="tradpost">Frames of <c>TRADPOST.ICN</c> or <c>TRADPOSE.ICN</c>; fourteen of them.</param>
    /// <param name="cellwin">Frames of <c>CELLWIN.ICN</c>; forty nine of them.</param>
    /// <param name="okButton">Frames of <c>BUTTON_SMALL_OKAY_GOOD</c>/<c>_EVIL</c>, released and pressed.</param>
    /// <param name="cancelButton">Frames of <c>BUTTON_SMALL_CANCEL_GOOD</c>/<c>_EVIL</c>, released and pressed.</param>
    /// <returns>
    /// The artwork, or <see langword="null"/> when an archive is missing frames - the reference would read past the
    /// end of the sprite list instead.
    /// </returns>
    public static GiftWindowArtwork? TryCreate(IReadOnlyList<Sprite>? tradpost, IReadOnlyList<Sprite>? cellwin,
        IReadOnlyList<Sprite>? okButton, IReadOnlyList<Sprite>? cancelButton)
    {
        if (tradpost is null || tradpost.Count < RequiredTradpostFrames
            || cellwin is null || cellwin.Count < RequiredCellwinFrames
            || okButton is null || okButton.Count < 2
            || cancelButton is null || cancelButton.Count < 2)
        {
            return null;
        }

        return new GiftWindowArtwork(
            tradpost,
            cellwin,
            new DialogButtonArtwork(okButton[0], okButton[1]),
            new DialogButtonArtwork(cancelButton[0], cancelButton[1]));
    }
}
