using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The list of creatures of <c>Dialog::SelectEnumMonster</c> (<c>dialog_selectitems.cpp:131-167</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every row is 43 pixels high and shows the <c>MONS32</c> portrait of the creature on the small plate the
/// reference cuts out of <c>SWAPWIN</c> frame 0 at <c>( 36, 267, 43, 43 )</c>, with the name to its right — the
/// reference's <c>_offsetY = 43</c>, <c>middleImageOffsetX = 45 / 2</c> and <c>textOffsetX = 50</c>. The portrait is
/// centred on the plate, which is what <c>renderMonsterOnBackground()</c> does before handing it to
/// <c>renderItem()</c>.
/// </para>
/// <para>
/// The reference shows <c>Dialog::ArmyInfo( Troop( monster, 0 ), Dialog::ZERO )</c> on a right click, i.e. the full
/// unit information window. That window is a modal of its own, so the row only records the request here and the
/// caller runs the window, exactly like <see cref="SpellSelectionWindow"/> leaves <c>Dialog::SpellInfo()</c> to its
/// caller.
/// </para>
/// </remarks>
public sealed class MonsterSelectionWindow : ItemSelectionWindow
{
    /// <summary>The heading of the reference's window, <c>_( "Select Monster:" )</c>.</summary>
    public const string Heading = "Select Monster:";

    /// <summary>Height of a row, <c>_offsetY = 43</c>.</summary>
    public const int RowHeight = 43;

    /// <summary>
    /// Left edge of the portrait inside a row, <c>middleImageOffsetX = 45 / 2</c>: the plate is 43 wide, so the
    /// portrait is centred on it.
    /// </summary>
    public const int IconOffsetX = 45 / 2;

    /// <summary>Left edge of the name inside a row, <c>textOffsetX = 50</c>.</summary>
    public const int TextOffsetX = 50;

    /// <summary>Vertical middle of a row, <c>_offsetY / 2</c>.</summary>
    public const int RowMiddleOffsetY = RowHeight / 2;

    /// <summary>Room kept free on the right of a row for the scroll bar, <c>- 55</c> of the reference.</summary>
    public const int TextRightMargin = 55;

    /// <summary>
    /// Where the 43x43 plate under a portrait is cut from <c>SWAPWIN</c> frame 0,
    /// <c>fheroes2::Crop( backgroundOriginal, 36, 267, 43, 43 )</c>.
    /// </summary>
    public static readonly Rect PlateSourceArea = new(36, 267, RowHeight, RowHeight);

    private readonly List<ItemSelectionEntry> _monsters;
    private readonly IMiniMonsterSprites _sprites;
    private readonly Sprite? _plate;

    private MonsterType? _infoRequest;

    /// <summary>
    /// Lays out the window and fills the list, the reference's
    /// <c>SelectEnumMonster( const fheroes2::Size &amp;, std::string )</c> plus
    /// <c>SetListContent()</c> (<c>dialog_selectitems.cpp:132-155</c>, <c>:1219-1226</c>).
    /// </summary>
    /// <param name="monsterIds">Creature ids to show, in the order the caller wants them.</param>
    /// <param name="sprites">The small portraits, <c>ICN::MONS32</c>.</param>
    /// <param name="plate">
    /// The 43x43 plate the portraits stand on — <see cref="CutPlate"/> turns <c>SWAPWIN</c> frame 0 into it — or
    /// <see langword="null"/> to draw the portraits without one.
    /// </param>
    /// <param name="dialogSize">Size of the active area, normally <see cref="MonsterSelectDialog.DialogSize"/>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="monsterIds"/>, <paramref name="sprites"/>, <paramref name="artwork"/>,
    /// <paramref name="titleFont"/> or <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public MonsterSelectionWindow(IReadOnlyList<int> monsterIds, IMiniMonsterSprites sprites, Sprite? plate,
        Size dialogSize, ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font,
        Size? screen = null)
        : base(dialogSize, Heading, string.Empty, artwork, titleFont, font, screen)
    {
        ArgumentNullException.ThrowIfNull(monsterIds);
        ArgumentNullException.ThrowIfNull(sprites);

        _sprites = sprites;
        _plate = plate;
        _monsters = new List<ItemSelectionEntry>(monsterIds.Count);

        foreach (int id in monsterIds)
        {
            _monsters.Add(new ItemSelectionEntry(id));
        }

        SetAreaMaxItems(ItemsArea.Height / RowHeight);
        SetListContent(_monsters);
        UpdateScrollBarImage();
    }

    /// <summary>The rows, in the order the caller gave them, <c>SetListContent()</c> of the reference.</summary>
    public IReadOnlyList<ItemSelectionEntry> Monsters => _monsters;

    /// <summary>
    /// The creature the selection stands on, or <see cref="MonsterType.Unknown"/> when the list is empty; the value
    /// <c>selectMonster()</c> returns after the loop.
    /// </summary>
    public MonsterType SelectedMonster =>
        IsValid ? (MonsterType)GetCurrent().Id : MonsterType.Unknown;

    /// <summary>
    /// Moves the selection to a creature, the reference's <c>SetCurrent( monsterId )</c>
    /// (<c>dialog_selectitems.cpp:1224-1226</c>).
    /// </summary>
    /// <remarks>
    /// The reference's overload takes the id, not the row index; a creature that is not in the list resets the list,
    /// which is what <see cref="ListBox{T}.SetCurrent(T)"/> does for an unknown row.
    /// </remarks>
    /// <param name="monster">The creature to select.</param>
    public void SelectMonster(MonsterType monster)
    {
        SetCurrent(new ItemSelectionEntry((int)monster));
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Cuts the plate out of the window the portraits stand on, <c>fheroes2::Crop( …, 36, 267, 43, 43 )</c> of
    /// <c>renderMonsterOnBackground()</c> (<c>dialog_selectitems.cpp:72-82</c>).
    /// </summary>
    /// <param name="swapWindow">Frame 0 of <c>ICN::SWAPWIN</c>.</param>
    /// <returns>The 43x43 plate, at its own origin.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="swapWindow"/> is <see langword="null"/>.</exception>
    public static Sprite CutPlate(Sprite swapWindow)
    {
        ArgumentNullException.ThrowIfNull(swapWindow);

        Sprite plate = new(PlateSourceArea.Width, PlateSourceArea.Height);

        ImageOps.Blit(plate.Image, Point.Zero, swapWindow.Image, PlateSourceArea);

        return plate;
    }

    /// <summary>
    /// Takes the unit information request a right click made, and forgets it;
    /// <c>SelectEnumMonster::ActionListPressRight()</c> (<c>dialog_selectitems.cpp:157-166</c>).
    /// </summary>
    /// <remarks>
    /// The reference opens the window right there. The port records it instead and lets the caller run it, because a
    /// modal cannot be opened from inside a row. The request is taken rather than read so that one right click
    /// opens one window.
    /// </remarks>
    /// <returns>The creature whose window the caller has to show, or <see langword="null"/> when none was asked.</returns>
    public MonsterType? TakeInfoRequest()
    {
        MonsterType? request = _infoRequest;

        _infoRequest = null;

        return request;
    }

    /// <summary>
    /// Paints a row: the plate, the portrait and the name, the reference's
    /// <c>SelectEnumMonster::RedrawItem()</c> over <c>ItemSelectionWindow::renderItem()</c>
    /// (<c>dialog_selectitems.cpp:142-147</c>, <c>:978-1010</c>).
    /// </summary>
    /// <param name="item">The row, whose id is the creature.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        if (_plate is { } plate)
        {
            ImageOps.BlitSprite(output,
                new Point(x + IconOffsetX - (plate.Width / 2), y + RowMiddleOffsetY - (plate.Height / 2)), plate);
        }

        MonsterType monster = (MonsterType)item.Id;

        if (_sprites.GetMiniMonster(monster) is { } portrait)
        {
            ImageOps.BlitSprite(output,
                new Point(x + IconOffsetX - (portrait.Width / 2), y + RowMiddleOffsetY - (portrait.Height / 2)),
                portrait);
        }

        RenderText(MonsterNames.GetSingular(monster), output, new Point(x, y), current);
    }

    /// <summary>
    /// Asks for the unit information window of the clicked creature, the reference's
    /// <c>SelectEnumMonster::ActionListPressRight()</c> (<c>dialog_selectitems.cpp:157-166</c>).
    /// </summary>
    /// <remarks>
    /// The reference shows a plain message for a creature that is not valid and the information window otherwise;
    /// every row of this window names a real creature, so both halves end in the same request.
    /// </remarks>
    /// <param name="item">The clicked row, whose id is the creature.</param>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _infoRequest = (MonsterType)item.Id;
    }

    private void RenderText(string text, PixelBuffer output, Point row, bool selected)
    {
        IGlyphSource font = selected ? HighlightFont : Font;
        Rect area = Window.ActiveArea;
        int maxWidth = area.Width - TextOffsetX - TextRightMargin;

        string fitted = GlyphText.FitToOneRow(font, text, maxWidth);
        int y = row.Y - (font.LineHeight / 2) + RowMiddleOffsetY + 2;

        GlyphText.DrawLine(font, output, new Point(row.X + TextOffsetX, y), fitted,
            new Rect(row.X + TextOffsetX, area.Y, maxWidth, area.Height));
    }
}
