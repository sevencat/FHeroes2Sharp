using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where the parts of the gift dialog sit, <c>Dialog::MakeGiftResource()</c>
/// (<c>dialog_giftresources.cpp:231-346</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference opens a <c>StandardWindow( 320, 234, true )</c>, whose active area is the centred 320 by 234
/// rectangle (<c>ui_window.cpp:107-116</c>), and then positions everything relative to that area with the offsets
/// collected here. The three captions, the two resource rows and the two buttons are the whole layout; the only
/// horizontal centring that does not go through the active area is the block of recipient boxes, which the
/// reference centres inside the 320 pixel dialog width and not inside the narrower active area.
/// </para>
/// <para>
/// The resource rows are seven cells 40 pixels apart: the icons of <c>TRADPOST.ICN</c> are 34 pixels wide, so the
/// 240 pixel run leaves room for the arrow that a cell would carry in the marketplace.
/// </para>
/// </remarks>
public static class GiftWindowLayout
{
    /// <summary>Active area of the dialog, <c>giftDialogSize</c> (<c>dialog_giftresources.cpp:55</c>).</summary>
    public static Size WindowSize => new(320, 234);

    /// <summary>Top of the "Select Recipients" caption, <c>box.y + 7</c>.</summary>
    public const int RecipientsCaptionY = 7;

    /// <summary>Top of the row of recipient boxes, <c>box.y + 28</c>.</summary>
    public const int RecipientsY = 28;

    /// <summary>Top of the "Your Funds" caption, <c>box.y + 57</c>.</summary>
    public const int YourFundsCaptionY = 57;

    /// <summary>Top of the "Your Funds" row, <c>box.y + 80</c>.</summary>
    public const int YourFundsY = 80;

    /// <summary>Top of the "Planned Gift" caption, <c>box.y + 127</c>.</summary>
    public const int PlannedGiftCaptionY = 127;

    /// <summary>Top of the "Planned Gift" row, <c>box.y + 150</c>.</summary>
    public const int PlannedGiftY = 150;

    /// <summary>Gap between two recipient boxes, <c>recipientSpacing</c>.</summary>
    public const int RecipientSpacing = 22;

    /// <summary>Distance between two resource cells, the <c>40</c> of the reference's position table.</summary>
    public const int ResourceSpacing = 40;

    /// <summary>Width the seven resource cells span, <c>240</c> in the reference's centring formula.</summary>
    public const int ResourcesWidth = 240;

    /// <summary>Distance of a button from the corner of the active area, <c>border</c> of the reference.</summary>
    public const int ButtonBorder = 10;

    /// <summary>Number of resource cells in a row, <c>7</c>.</summary>
    public const int ResourceCount = 7;

    /// <summary>
    /// Places the recipient boxes, <c>SelectRecipientsColors</c> (<c>dialog_giftresources.cpp:84-100</c>).
    /// </summary>
    /// <param name="activeArea">Active area of the dialog.</param>
    /// <param name="boxSize">Size of one box, the height of a <c>CELLWIN.ICN</c> frame.</param>
    /// <param name="count">How many colours take part besides the sender.</param>
    /// <returns>The boxes, left to right, in the order of the colours.</returns>
    public static IReadOnlyList<Rect> RecipientBoxes(Rect activeArea, Size boxSize, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        int containerWidth = (count * boxSize.Width) + ((count - 1) * RecipientSpacing);
        int startX = activeArea.X + ((WindowSize.Width - containerWidth) / 2);
        int y = activeArea.Y + RecipientsY;

        Rect[] boxes = new Rect[count];

        for (int index = 0; index < count; ++index)
        {
            boxes[index] = new Rect(startX + (index * (RecipientSpacing + boxSize.Width)), y, boxSize.Width,
                boxSize.Height);
        }

        return boxes;
    }

    /// <summary>
    /// Places one resource row, <c>ResourceBar</c> (<c>dialog_giftresources.cpp:147-160</c>).
    /// </summary>
    /// <param name="activeArea">Active area of the dialog.</param>
    /// <param name="rowY">Top of the row, one of <see cref="YourFundsY"/> or <see cref="PlannedGiftY"/>.</param>
    /// <param name="cellSize">Size of a <c>TRADPOST.ICN</c> cell.</param>
    /// <returns>The seven cells, wood first, gold last.</returns>
    public static IReadOnlyList<Rect> ResourceCells(Rect activeArea, int rowY, Size cellSize)
    {
        // The reference centres the 240 pixel run inside the dialog width and adds the cell width, because the
        // last cell occupies the final 34 pixels of the run.
        int x = activeArea.X + ((WindowSize.Width - (cellSize.Width + ResourcesWidth)) / 2);
        int y = activeArea.Y + rowY;

        Rect[] cells = new Rect[ResourceCount];

        for (int index = 0; index < ResourceCount; ++index)
        {
            cells[index] = new Rect(x + (index * ResourceSpacing), y, cellSize.Width, cellSize.Height);
        }

        return cells;
    }

    /// <summary>
    /// Top left corner of the button in the bottom left corner, <c>box.x + border</c> of the reference.
    /// </summary>
    /// <param name="activeArea">Active area of the dialog.</param>
    /// <param name="buttonSize">Size of the button.</param>
    /// <returns>The corner the button's released sprite goes to.</returns>
    public static Point BottomLeftButton(Rect activeArea, Size buttonSize) =>
        new(activeArea.X + ButtonBorder,
            activeArea.Y + activeArea.Height - ButtonBorder - buttonSize.Height);

    /// <summary>
    /// Top left corner of the button in the bottom right corner, the cancel button of the reference.
    /// </summary>
    /// <param name="activeArea">Active area of the dialog.</param>
    /// <param name="buttonSize">Size of the button.</param>
    /// <returns>The corner the button's released sprite goes to.</returns>
    public static Point BottomRightButton(Rect activeArea, Size buttonSize) =>
        new(activeArea.X + activeArea.Width - ButtonBorder - buttonSize.Width,
            activeArea.Y + activeArea.Height - ButtonBorder - buttonSize.Height);
}
