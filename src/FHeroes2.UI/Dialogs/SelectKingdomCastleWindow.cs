using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Interface;
using FHeroes2.UI.Widgets;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The list of towns of <c>Dialog::selectKingdomCastle()</c> (<c>dialog_selectitems.cpp:791-843</c>), the window
/// Town Portal picks its destination in.
/// </summary>
/// <remarks>
/// <para>
/// Every row is 35 pixels high (<c>itemsOffsetY</c>). The row shows the town icon centred 35 pixels into it and the
/// name starting at 75, which is the reference's
/// <c>renderItem( castleIcon, castle-&gt;GetName(), { dstx, dsty }, 35, 75, itemsOffsetY / 2, current )</c>: the plate
/// of <c>ICN::LOCATORS</c> (or <c>LOCATORE</c>) frame 23 is 54x30, so it sits at <c>dstx + 35 - 27</c> and
/// <c>dsty + 17 - 15</c> and the town icon is painted into its top left corner four pixels in.
/// </para>
/// <para>
/// The row id is the town gate's tile index, which is what <c>Castle::GetIndex()</c> returns in the reference and
/// what the caller of Town Portal needs to send the hero somewhere.
/// </para>
/// <para>
/// <b>Ported:</b> the right click opens the castle quick info, the reference's
/// <c>Dialog::QuickInfoWithIndicationOnRadar()</c>. The port has no radar indication inside a window, so the caller
/// gets the town through <c>castleInfo</c> and shows <see cref="Interface.AdventureMapDialogs.ShowQuickInfo"/>
/// itself.
/// </para>
/// </remarks>
public sealed class SelectKingdomCastleWindow : ItemSelectionWindow
{
    /// <summary>Height of a row, <c>itemsOffsetY = 35</c>.</summary>
    public const int RowHeight = 35;

    /// <summary>Horizontal middle of the icon inside a row, <c>middleImageOffsetX = 35</c>.</summary>
    public const int IconOffsetX = 35;

    /// <summary>Left edge of the name inside a row, <c>textOffsetX = 75</c>.</summary>
    public const int TextOffsetX = 75;

    /// <summary>Vertical middle of a row, <c>itemsOffsetY / 2</c>.</summary>
    public const int RowMiddleOffsetY = RowHeight / 2;

    /// <summary>Room kept free on the right of a row for the scroll bar, <c>- 55</c> of the reference.</summary>
    public const int TextRightMargin = 55;

    /// <summary>Frame of the blank town plate, <c>Assets::getImage( _townFrameIcnId, 23 )</c>.</summary>
    public const int TownFrame = 23;

    /// <summary>Width of that plate, 54 pixels in the reference's <c>LOCATORS.ICN</c>.</summary>
    public const int PlateWidth = 54;

    /// <summary>Height of that plate, 30 pixels in the reference's <c>LOCATORS.ICN</c>.</summary>
    public const int PlateHeight = 30;

    /// <summary>Inset of the town icon inside the plate, <c>drawCastleIcon( *castle, castleIcon, { 4, 4 } )</c>.</summary>
    public const int PlateInset = 4;

    private readonly List<ItemSelectionEntry> _rows;
    private readonly Dictionary<int, CastleTown> _castles;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly bool _evilInterface;
    private readonly Funds _treasury;
    private readonly Action<CastleTown>? _castleInfo;

    /// <summary>
    /// Lays out the window and fills the list, the reference's
    /// <c>SelectKingdomCastle( const fheroes2::Size &amp;, std::string, std::string )</c> plus
    /// <c>SetListContent()</c> (<c>dialog_selectitems.cpp:791-805</c>, <c>860-884</c>).
    /// </summary>
    /// <param name="castles">The towns to show, in the order the caller wants them.</param>
    /// <param name="sprites">Where <c>LOCATORS</c> (or <c>LOCATORE</c>), <c>CSLMARKER</c> and the scroll bar come from.</param>
    /// <param name="treasury">The owner's resources, which decide the build marker of every row.</param>
    /// <param name="dialogSize">Size of the active area, normally <see cref="SelectKingdomCastleDialog.DialogSize"/>.</param>
    /// <param name="title">Heading, drawn centred in the yellow font.</param>
    /// <param name="description">Optional explanation, drawn centred in the white font.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect.</param>
    /// <param name="castleInfo">
    /// What a right click on a row opens, the reference's <c>Dialog::QuickInfoWithIndicationOnRadar()</c>;
    /// <see langword="null"/> leaves the right click without an answer.
    /// </param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="castles"/>, <paramref name="sprites"/>, <paramref name="artwork"/>,
    /// <paramref name="titleFont"/> or <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public SelectKingdomCastleWindow(IReadOnlyList<CastleTown> castles, IViewWorldSpriteSource sprites,
        Funds treasury, Size dialogSize, string title, string description, ItemSelectionWindowArtwork artwork,
        IGlyphSource titleFont, IGlyphSource font, bool evilInterface = false,
        Action<CastleTown>? castleInfo = null, Size? screen = null)
        : base(dialogSize, title, description, artwork, titleFont, font, screen)
    {
        ArgumentNullException.ThrowIfNull(castles);
        ArgumentNullException.ThrowIfNull(sprites);

        _sprites = sprites;
        _treasury = treasury;
        _evilInterface = evilInterface;
        _castleInfo = castleInfo;
        _rows = new List<ItemSelectionEntry>(castles.Count);
        _castles = new Dictionary<int, CastleTown>(castles.Count);

        foreach (CastleTown castle in castles)
        {
            _rows.Add(new ItemSelectionEntry(castle.TileIndex));
            _castles[castle.TileIndex] = castle;
        }

        SetAreaMaxItems(ItemsArea.Height / RowHeight);
        SetListContent(_rows);
        UpdateScrollBarImage();
    }

    /// <summary>The rows, in the order the caller gave them, <c>SetListContent()</c> of the reference.</summary>
    public IReadOnlyList<ItemSelectionEntry> Towns => _rows;

    /// <summary>
    /// The tile index of the selected town's gate, or -1 when the list is empty; the value the reference's
    /// <c>selectKingdomCastle()</c> returns after its loop.
    /// </summary>
    public int SelectedPositionIndex => IsValid ? GetCurrent().Id : -1;

    /// <summary>
    /// Moves the selection to a town, the reference's <c>SetCurrent( castlePositionIndex )</c>
    /// (<c>dialog_selectitems.cpp:881-883</c>).
    /// </summary>
    /// <remarks>
    /// The reference is handed a <b>position index</b>, i.e. the tile index of a town gate, not a row number; a
    /// tile that is not in the list resets the list, which is what <see cref="ListBox{T}.SetCurrent(int)"/> does
    /// when it cannot find the row.
    /// </remarks>
    /// <param name="positionIndex">Tile index of the town to select.</param>
    public void SelectTown(int positionIndex)
    {
        for (int i = 0; i < _rows.Count; ++i)
        {
            if (_rows[i].Id != positionIndex)
            {
                continue;
            }

            SetCurrent(i);
            UpdateScrollBarImage();

            return;
        }
    }

    /// <summary>
    /// Paints a row: the town plate with the castle icon and the badge of the build status, then the town name,
    /// the reference's <c>SelectKingdomCastle::RedrawItem()</c> over <c>ItemSelectionWindow::renderItem()</c>
    /// (<c>dialog_selectitems.cpp:805-819</c>, <c>978-1010</c>).
    /// </summary>
    /// <param name="item">The row, whose id is the town gate's tile index.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        if (!_castles.TryGetValue(item.Id, out CastleTown? castle))
        {
            return;
        }

        Point plate = new(x + IconOffsetX - (PlateWidth / 2), y + RowMiddleOffsetY - (PlateHeight / 2));

        if (_sprites.GetSprite(_evilInterface ? IcnType.Locatore : IcnType.Locators, TownFrame) is { } townFrame)
        {
            ImageOps.BlitSprite(output, plate, townFrame);
        }

        CastleIconPainter.Draw(castle, _sprites, new Point(plate.X + PlateInset, plate.Y + PlateInset), output,
            _evilInterface, Castle.GetAllBuildingStatus(castle, _treasury));

        RenderText(castle.Name, output, new Point(x, y), current);
    }

    /// <summary>
    /// Opens the quick info of the town on a right click, the reference's
    /// <c>SelectKingdomCastle::ActionListPressRight()</c> (<c>dialog_selectitems.cpp:821-824</c>).
    /// </summary>
    /// <param name="item">The clicked row, whose id is the town gate's tile index.</param>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_castleInfo is { } castleInfo && _castles.TryGetValue(item.Id, out CastleTown? castle))
        {
            castleInfo(castle);
        }
    }

    private void RenderText(string text, PixelBuffer output, Point row, bool selected)
    {
        IGlyphSource font = selected ? HighlightFont : Font;
        Rect area = Window.ActiveArea;
        int maxWidth = area.Width - TextOffsetX - TextRightMargin;

        string fitted = BuildDialogView.FitToOneRow(font, text, maxWidth);
        int y = row.Y - (font.LineHeight / 2) + RowMiddleOffsetY + 2;

        GlyphText.DrawLine(font, output, new Point(row.X + TextOffsetX, y), fitted,
            new Rect(row.X + TextOffsetX, area.Y, maxWidth, area.Height));
    }
}
