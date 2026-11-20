using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where everything in the marketplace window goes, <c>Dialog::Marketplace()</c> and
/// <c>TradeWindowGUI</c> (<c>dialog_marketplace.cpp:118-200</c>, <c>:385-450</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every anchor of the reference is "the frame's text area plus a constant": the two rows of seven resource cells sit
/// at <c>y + 190</c>, the trade area above them at <c>y + 90</c> to <c>y + 175</c>, and the message at <c>y + 30</c>.
/// The tool keeps those constants here and answers the anchors; a handful of them depend on the size of a sprite
/// (the trade bar, the two resource icons, the buttons), so those take a <see cref="Size"/>.
/// </para>
/// <para>
/// The row of seven cells is <c>x + 37 * ( i % 4 )</c> by <c>y + 190 + 37 * ( i / 4 )</c> - four cells, then three
/// under them, because the resources are laid out in two rows of a 4 by 2 grid and gold ends up alone in the second
/// row's first slot. The exchange rate icon is centered over that gold cell.
/// </para>
/// </remarks>
public sealed class MarketplaceLayout
{
    /// <summary>Side of a resource cell, <c>34</c>.</summary>
    public const int CellSize = 34;

    /// <summary>Distance between two cells, <c>37</c>.</summary>
    public const int CellStep = 37;

    /// <summary>Row of the cells relative to the area, <c>y + 190</c>.</summary>
    public const int CellsOffsetY = 190;

    /// <summary>Column step between the "your resources" and the "available trades" rows, <c>136</c>.</summary>
    public const int ToCellsOffsetX = 136;

    /// <summary>Number of resources in a row, <c>7</c>.</summary>
    public const int ResourceCount = 7;

    /// <summary>Width the "Your Resources" and "Available Trades" labels are centered in, <c>108</c>.</summary>
    public const int TitleWidth = 108;

    /// <summary>Row of the two labels relative to the area, <c>190 - 15</c>.</summary>
    public const int TitleOffsetY = CellsOffsetY - 15;

    /// <summary>Row of the message relative to the area, <c>y + 30</c>.</summary>
    public const int MessageOffsetY = 30;

    /// <summary>Height of the message box, <c>100</c>.</summary>
    public const int MessageHeight = 100;

    /// <summary>Row of the trade area's own sentence relative to the area, <c>y + 32</c>.</summary>
    public const int TradeMessageOffsetY = 32;

    /// <summary>Row of the trade bar relative to the area, <c>y + 128</c>.</summary>
    public const int TradeBarOffsetY = 128;

    /// <summary>The trade bar starts two pixels left of the centered artwork, <c>- 2</c>.</summary>
    public const int TradeBarOffsetX = -2;

    /// <summary>Column of the scroll bar relative to the centered trade bar, <c>+ 22</c>.</summary>
    public const int SliderOffsetX = 22;

    /// <summary>Distance from the top of the area to the scroll bar, <c>y + 131</c>.</summary>
    public const int SliderOffsetY = 131;

    /// <summary>Length of the scroll bar, <c>187</c>.</summary>
    public const int SliderWidth = 187;

    /// <summary>Height of the scroll bar, <c>11</c>.</summary>
    public const int SliderHeight = 11;

    /// <summary>Baseline the two resource icons are bottom aligned to, <c>y + 115</c>.</summary>
    public const int TradeIconBaselineY = 115;

    /// <summary>Half the distance between the two resource icons, <c>70</c>.</summary>
    public const int TradeIconSpread = 70;

    /// <summary>Row of the "give to receive" arrow relative to the area, <c>y + 90</c>.</summary>
    public const int FromToOffsetY = 90;

    /// <summary>Row of the "Max" label relative to the area, <c>y + 80</c>.</summary>
    public const int MaxOffsetY = 80;

    /// <summary>Row of the "Min" label relative to the area, <c>y + 103</c>.</summary>
    public const int MinOffsetY = 103;

    /// <summary>Row of the "Qty to trade" label relative to the area, <c>y + 115</c>.</summary>
    public const int QuantityOffsetY = 115;

    /// <summary>Row of the "-N (M)" and "+N (M)" lines relative to the area, <c>y + 116</c>.</summary>
    public const int AmountsOffsetY = 116;

    /// <summary>Horizontal offset of the Max/Min labels from the centered text, <c>- 5</c>.</summary>
    public const int MaxMinOffsetX = -5;

    /// <summary>Row of the trade button relative to the area, <c>y + 150</c>.</summary>
    public const int TradeButtonOffsetY = 150;

    /// <summary>Column of the left arrow relative to the area, <c>x + 11</c>.</summary>
    public const int LeftArrowOffsetX = 11;

    /// <summary>Column of the right arrow relative to the area, <c>x + 220</c>.</summary>
    public const int RightArrowOffsetX = 220;

    /// <summary>Row of both arrows relative to the area, <c>y + 129</c>.</summary>
    public const int ArrowOffsetY = 129;

    /// <summary>Column of the gift button, which sticks out of the frame, <c>x - 68</c>.</summary>
    public const int GiftButtonOffsetX = -68;

    /// <summary>Column of the exit button, <c>x + 68</c>.</summary>
    public const int ExitButtonOffsetX = 68;

    /// <summary>Spacing between the exchange rate icon, its text and the icon's top edge, <c>2</c>.</summary>
    public const int ExchangeRateSpacing = 2;

    /// <summary>Creates the layout of a frame area.</summary>
    /// <param name="area">The text area of the frame, <c>Dialog::FrameBox::GetArea()</c>; 244 by 337 for the
    /// marketplace.</param>
    public MarketplaceLayout(in Rect area) => Area = area;

    /// <summary>The text area of the frame, <c>pos_rt</c>.</summary>
    public Rect Area { get; }

    /// <summary>Creates the layout of a frame area, <c>Dialog::FrameBox( 297, true )</c>'s area.</summary>
    /// <param name="chrome">Pieces of <c>BUYBUILD.ICN</c>.</param>
    /// <param name="screen">Screen the frame is centered on.</param>
    /// <returns>The layout.</returns>
    public static MarketplaceLayout Create(FrameBoxChrome chrome, Size? screen = null) =>
        new(FrameBox.Create(chrome, 297, showButtons: true, startY: -1, screen).Area);

    /// <summary>One cell of the "your resources" row, <c>rectsFrom[i]</c>.</summary>
    /// <param name="index">Index of the resource in the icon table, 0 to 6.</param>
    /// <returns>The cell.</returns>
    public Rect FromCell(int index) => Cell(Area.X, index);

    /// <summary>One cell of the "available trades" row, <c>rectsTo[i]</c>.</summary>
    /// <param name="index">Index of the resource in the icon table, 0 to 6.</param>
    /// <returns>The cell.</returns>
    public Rect ToCell(int index) => Cell(Area.X + ToCellsOffsetX, index);

    /// <summary>Top left corner the "Your Resources" label is centered from, <c>pt1 - 15</c>.</summary>
    public Point FromTitle => new(Area.X, Area.Y + TitleOffsetY);

    /// <summary>Top left corner the "Available Trades" label is centered from, <c>pt2 - 15</c>.</summary>
    public Point ToTitle => new(Area.X + ToCellsOffsetX, Area.Y + TitleOffsetY);

    /// <summary>The message box, <c>dst_rt</c> of the reference.</summary>
    public Rect Message => new(Area.X, Area.Y + MessageOffsetY, Area.Width, MessageHeight);

    /// <summary>Where the trade area's sentence starts, <c>pos_rt.x, pos_rt.y + 32</c>.</summary>
    public Point TradeMessage => new(Area.X, Area.Y + TradeMessageOffsetY);

    /// <summary>Center of the exchange rate icon, <c>{ pos_rt.x + pos_rt.width / 2, rectsFrom.back().y }</c>.</summary>
    public Point ExchangeRateCentre => new(Area.X + (Area.Width / 2), FromCell(ResourceCount - 1).Y);

    /// <summary>The trade bar, which is as wide as the frame's middle piece.</summary>
    /// <param name="barSize">Size of frame 1 of <c>TRADPOST.ICN</c>.</param>
    /// <returns>The sprite position.</returns>
    public Point TradeBar(in Size barSize) => new(
        Area.X + ((Area.Width - barSize.Width) / 2) + TradeBarOffsetX,
        Area.Y + TradeBarOffsetY);

    /// <summary>The scroll bar, which the trade bar carries.</summary>
    /// <param name="barSize">Size of frame 1 of <c>TRADPOST.ICN</c>.</param>
    /// <returns>The groove the thumb travels in.</returns>
    public Rect SliderArea(in Size barSize) => new(
        Area.X + ((Area.Width - barSize.Width) / 2) + SliderOffsetX,
        Area.Y + SliderOffsetY,
        SliderWidth,
        SliderHeight);

    /// <summary>One of the two resource icons of the trade area.</summary>
    /// <param name="size">Size of the icon.</param>
    /// <param name="isTo"><see langword="false"/> for the resource given away, <see langword="true"/> for the one
    /// asked for.</param>
    /// <returns>The sprite position, bottom aligned to <see cref="TradeIconBaselineY"/>.</returns>
    public Point TradeIcon(in Size size, bool isTo)
    {
        int spread = isTo ? TradeIconSpread : -TradeIconSpread;

        return new Point(
            Area.X + ((Area.Width - size.Width + 1) / 2) + spread,
            Area.Y + TradeIconBaselineY - size.Height);
    }

    /// <summary>The "give to receive" arrow between the two icons.</summary>
    /// <param name="size">Size of frame 0 of <c>TRADPOST.ICN</c>.</param>
    /// <returns>The sprite position.</returns>
    public Point FromTo(in Size size) => new(
        Area.X + ((Area.Width - size.Width) / 2),
        Area.Y + FromToOffsetY);

    /// <summary>The "Max" label.</summary>
    /// <param name="textWidth">Width of the measured text.</param>
    /// <returns>The top left corner; the label is centered on the frame and then moved five pixels left.</returns>
    public Point MaxLabel(int textWidth) => new(
        Area.X + ((Area.Width - textWidth) / 2) + MaxMinOffsetX,
        Area.Y + MaxOffsetY);

    /// <summary>The "Min" label, which shares the column of "Max".</summary>
    /// <param name="textWidth">Width of the measured text.</param>
    /// <returns>The top left corner.</returns>
    public Point MinLabel(int textWidth) => new(
        Area.X + ((Area.Width - textWidth) / 2) + MaxMinOffsetX,
        Area.Y + MinOffsetY);

    /// <summary>The "Qty to trade" label.</summary>
    /// <param name="textWidth">Width of the measured text.</param>
    /// <returns>The top left corner.</returns>
    public Point QuantityLabel(int textWidth) => new(
        Area.X + ((Area.Width - textWidth) / 2),
        Area.Y + QuantityOffsetY);

    /// <summary>The "-N (M)" line under the resource given away.</summary>
    /// <param name="textWidth">Width of the measured text.</param>
    /// <returns>The top left corner, centered on the left icon.</returns>
    public Point SellText(int textWidth) => new(
        Area.X + (Area.Width / 2) - TradeIconSpread - ((textWidth + 1) / 2),
        Area.Y + AmountsOffsetY);

    /// <summary>The "+N (M)" line under the resource asked for.</summary>
    /// <param name="textWidth">Width of the measured text.</param>
    /// <returns>The top left corner, centered on the right icon.</returns>
    public Point BuyText(int textWidth) => new(
        Area.X + (Area.Width / 2) + TradeIconSpread - ((textWidth + 1) / 2),
        Area.Y + AmountsOffsetY);

    /// <summary>The trade button.</summary>
    /// <param name="size">Size of the button's released sprite.</param>
    /// <returns>The sprite position.</returns>
    public Point TradeButton(in Size size) => new(
        Area.X + ((Area.Width - size.Width) / 2),
        Area.Y + TradeButtonOffsetY);

    /// <summary>The gift button, which hangs off the left side of the frame.</summary>
    /// <param name="size">Size of the button's released sprite.</param>
    /// <returns>The sprite position.</returns>
    public Point GiftButton(in Size size) => new(
        Area.X + GiftButtonOffsetX + ((Area.Width - size.Width) / 2),
        Area.Y + Area.Height - size.Height);

    /// <summary>The exit button, which sits on the frame's own button row.</summary>
    /// <param name="size">Size of the button's released sprite.</param>
    /// <returns>The sprite position.</returns>
    public Point ExitButton(in Size size) => new(
        Area.X + ExitButtonOffsetX + ((Area.Width - size.Width) / 2),
        Area.Y + Area.Height - size.Height);

    /// <summary>The "step down" arrow left of the trade bar.</summary>
    /// <returns>The sprite position.</returns>
    public Point LeftArrow => new(Area.X + LeftArrowOffsetX, Area.Y + ArrowOffsetY);

    /// <summary>The "step up" arrow right of the trade bar.</summary>
    /// <returns>The sprite position.</returns>
    public Point RightArrow => new(Area.X + RightArrowOffsetX, Area.Y + ArrowOffsetY);

    /// <summary>
    /// A cell of the resource grid, <c>fheroes2::Rect( x + 37 * ( i % 4 ), y + 190 + 37 * ( i / 4 ), 34, 34 )</c>.
    /// </summary>
    private Rect Cell(int x, int index) => new(
        x + (CellStep * (index % 4)),
        Area.Y + CellsOffsetY + (CellStep * (index / 4)),
        CellSize,
        CellSize);
}
