using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The sprites of the marketplace window, <c>ICN::TRADPOST</c>/<c>TRADPOSE</c>, <c>MARKETPLACE_ICON</c>,
/// <c>RESOURCE</c> and the three buttons (<c>dialog_marketplace.cpp:118-200</c>, <c>:430-480</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>TRADPOST</c> is the same picture for both interfaces, so the reference keeps a single id for it; the evil
/// interface only changes <c>MARKETPLACE_ICON</c>, the trade and gift buttons and the exit button. The frames are
/// indexed the way the reference indexes them: 0 is the small "give to receive" arrow, 1 the bar the scroll bar sits
/// in, 2 the scroll bar's own artwork, 3/4 and 5/6 the two arrows, 7 to 13 the seven resource cells, and 14 the cursor
/// that marks the two selected cells.
/// </para>
/// <para>
/// The exit button of the reference is a <c>UNIFORM_*_EXIT_BUTTON</c> sprite, which fheroes2 builds in memory from
/// the button font at start-up; the port has no such generator yet, so the caller passes the two frames - the window
/// works with any pair of images, and <see cref="Sprite"/> is what the rest of the port passes for buttons it cannot
/// read from an archive either.
/// </para>
/// </remarks>
public sealed class MarketplaceArtwork
{
    /// <summary>Frame of the "give to receive" arrow, <c>0</c>.</summary>
    public const int FromToFrame = 0;

    /// <summary>Frame of the trade bar, <c>1</c>.</summary>
    public const int BarFrame = 1;

    /// <summary>Frame the scroll bar's thumb is composed of, <c>2</c>.</summary>
    public const int ScrollSliderFrame = 2;

    /// <summary>Frame of the left arrow's released look, <c>3</c>.</summary>
    public const int LeftArrowReleasedFrame = 3;

    /// <summary>Frame of the left arrow's pressed look, <c>4</c>.</summary>
    public const int LeftArrowPressedFrame = 4;

    /// <summary>Frame of the right arrow's released look, <c>5</c>.</summary>
    public const int RightArrowReleasedFrame = 5;

    /// <summary>Frame of the right arrow's pressed look, <c>6</c>.</summary>
    public const int RightArrowPressedFrame = 6;

    /// <summary>First of the seven resource cell frames, <c>7</c>.</summary>
    public const int FirstCellFrame = 7;

    /// <summary>Frame of the cursor that marks a selected cell, <c>14</c>.</summary>
    public const int CursorFrame = 14;

    /// <summary>Number of frames of <c>TRADPOST.ICN</c> the window reads, <c>15</c>.</summary>
    public const int RequiredTradpostFrames = CursorFrame + 1;

    /// <summary>Number of frames of <c>MARKETPLACE_ICON.ICN</c>, <c>2</c>.</summary>
    public const int RequiredMarketplaceIconFrames = 2;

    /// <summary>Number of frames of <c>RESOURCE.ICN</c> the window reads, <c>7</c>.</summary>
    public const int RequiredResourceFrames = ResourceIconIndex.Count;

    private MarketplaceArtwork(
        IReadOnlyList<Sprite> tradpost,
        IReadOnlyList<Sprite> marketplaceIcon,
        IReadOnlyList<Sprite> resourceIcons,
        DialogButtonArtwork tradeButton,
        DialogButtonArtwork giftButton,
        DialogButtonArtwork exitButton,
        bool evilInterface)
    {
        Tradpost = tradpost;
        MarketplaceIcon = marketplaceIcon;
        ResourceIcons = resourceIcons;
        TradeButton = tradeButton;
        GiftButton = giftButton;
        ExitButton = exitButton;
        EvilInterface = evilInterface;
    }

    /// <summary>Every frame of <c>TRADPOST.ICN</c> the window reads.</summary>
    public IReadOnlyList<Sprite> Tradpost { get; }

    /// <summary>Frame 0 (good) or 1 (evil) of <c>MARKETPLACE_ICON.ICN</c>.</summary>
    public IReadOnlyList<Sprite> MarketplaceIcon { get; }

    /// <summary>Frames of <c>RESOURCE.ICN</c>, indexed by <see cref="ResourceIconIndex.Of"/>.</summary>
    public IReadOnlyList<Sprite> ResourceIcons { get; }

    /// <summary>The trade button, <c>BUTTON_SMALL_TRADE_GOOD</c> or its evil twin.</summary>
    public DialogButtonArtwork TradeButton { get; }

    /// <summary>The gift button, <c>BUTTON_GIFT_GOOD</c> or its evil twin.</summary>
    public DialogButtonArtwork GiftButton { get; }

    /// <summary>The exit button, <c>UNIFORM_GOOD_EXIT_BUTTON</c> or its evil twin.</summary>
    public DialogButtonArtwork ExitButton { get; }

    /// <summary>Whether these frames are the evil interface's.</summary>
    public bool EvilInterface { get; }

    /// <summary>The arrow between the two resources, frame 0 of <c>TRADPOST.ICN</c>.</summary>
    public Sprite FromTo => Tradpost[FromToFrame];

    /// <summary>The bar the scroll bar sits in, frame 1 of <c>TRADPOST.ICN</c>.</summary>
    public Sprite Bar => Tradpost[BarFrame];

    /// <summary>The scroll bar's thumb source, frame 2 of <c>TRADPOST.ICN</c>.</summary>
    public Sprite ScrollSliderSource => Tradpost[ScrollSliderFrame];

    /// <summary>The cursor that marks a selected cell, frame 14 of <c>TRADPOST.ICN</c>.</summary>
    public Sprite Cursor => Tradpost[CursorFrame];

    /// <summary>The empty cell of a resource row, frames 7 to 13 of <c>TRADPOST.ICN</c>.</summary>
    /// <param name="index">Index of the resource in the icon table, 0 to 6.</param>
    /// <returns>The cell's background, 34 by 34.</returns>
    public Sprite Cell(int index) => Tradpost[FirstCellFrame + index];

    /// <summary>The icon of a resource, <c>RESOURCE.ICN</c> indexed by <see cref="ResourceIconIndex.Of"/>.</summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The icon, or <see langword="null"/> when the flag has none.</returns>
    public Sprite? ResourceIcon(int resource)
    {
        int index = ResourceIconIndex.Of(resource);

        return index < 0 || index >= ResourceIcons.Count ? null : ResourceIcons[index];
    }

    /// <summary>The exchange rate icon, frame 0 or 1 of <c>MARKETPLACE_ICON.ICN</c>.</summary>
    public Sprite ExchangeRateIcon => MarketplaceIcon[EvilInterface ? 1 : 0];

    /// <summary>Size of a resource cell, which is also the size of the cursor over it.</summary>
    public Size CellSize => new(Cell(0).Width, Cell(0).Height);

    /// <summary>
    /// Collects the window's sprites, in the spirit of <c>MageGuildArtwork.TryCreate</c>.
    /// </summary>
    /// <param name="tradpost">Frames of <c>TRADPOST.ICN</c> or <c>TRADPOSE.ICN</c>; fifteen of them.</param>
    /// <param name="marketplaceIcon">Frames of <c>MARKETPLACE_ICON.ICN</c>; two of them.</param>
    /// <param name="resourceIcons">Frames of <c>RESOURCE.ICN</c>; seven of them.</param>
    /// <param name="tradeButton">Frames of <c>BUTTON_SMALL_TRADE_GOOD</c>/<c>_EVIL</c>.</param>
    /// <param name="giftButton">Frames of <c>BUTTON_GIFT_GOOD</c>/<c>_EVIL</c>.</param>
    /// <param name="exitButton">Frames of the exit button, released and pressed.</param>
    /// <param name="evilInterface">Whether the frames are the evil interface's.</param>
    /// <returns>
    /// The artwork, or <see langword="null"/> when an archive is missing frames - the reference would read past the
    /// end of the sprite list instead.
    /// </returns>
    public static MarketplaceArtwork? TryCreate(
        IReadOnlyList<Sprite>? tradpost,
        IReadOnlyList<Sprite>? marketplaceIcon,
        IReadOnlyList<Sprite>? resourceIcons,
        IReadOnlyList<Sprite>? tradeButton,
        IReadOnlyList<Sprite>? giftButton,
        IReadOnlyList<Sprite>? exitButton,
        bool evilInterface = false)
    {
        if (tradpost is null || tradpost.Count < RequiredTradpostFrames
            || marketplaceIcon is null || marketplaceIcon.Count < RequiredMarketplaceIconFrames
            || resourceIcons is null || resourceIcons.Count < RequiredResourceFrames
            || tradeButton is null || tradeButton.Count < 2
            || giftButton is null || giftButton.Count < 2
            || exitButton is null || exitButton.Count < 2)
        {
            return null;
        }

        return new MarketplaceArtwork(
            tradpost,
            marketplaceIcon,
            resourceIcons,
            new DialogButtonArtwork(tradeButton[0], tradeButton[1]),
            new DialogButtonArtwork(giftButton[0], giftButton[1]),
            new DialogButtonArtwork(exitButton[0], exitButton[1]),
            evilInterface);
    }
}
