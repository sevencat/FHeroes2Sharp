using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The list of spells of <c>Dialog::SelectEnumSpell</c> (<c>dialog_selectitems.h:366-381</c>,
/// <c>dialog_selectitems.cpp:379-412</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every row is 55 pixels high, the icon sits 37 pixels into it and the name starts at 80, which is the
/// reference's <c>_offsetY = 55</c>, <c>middleImageOffsetX = 75 / 2</c> and <c>textOffsetX = 80</c>. The
/// name is fitted to the width left of the right edge minus the width of the scroll bar and the right
/// inset, so a long name ends in an ellipsis instead of running under the scroll bar.
/// </para>
/// <para>
/// The reference shows <c>Dialog::SpellInfo()</c> on a right click, which is the full spell dialog with
/// the hero's numbers. The port has no hero here, so the row popup is the same name and description
/// pair that <see cref="SpellDialogElement"/> shows, substituted numbers and all.
/// </para>
/// </remarks>
public sealed class SpellSelectionWindow : ItemSelectionWindow
{
    /// <summary>The heading of the reference's window, <c>_( "Select Spell" )</c>.</summary>
    public const string Heading = "Select Spell";

    /// <summary>Height of a row, <c>_offsetY = 55</c>.</summary>
    public const int RowHeight = 55;

    /// <summary>Left edge of the icon inside a row, <c>middleImageOffsetX = 75 / 2</c>.</summary>
    public const int IconOffsetX = 75 / 2;

    /// <summary>Left edge of the name inside a row, <c>textOffsetX = 80</c>.</summary>
    public const int TextOffsetX = 80;

    /// <summary>Vertical middle of a row, <c>_offsetY / 2</c>.</summary>
    public const int RowMiddleOffsetY = RowHeight / 2;

    /// <summary>Room kept free on the right of a row for the scroll bar, <c>- 55</c> of the reference.</summary>
    public const int TextRightMargin = 55;

    private readonly List<ItemSelectionEntry> _spells;
    private readonly IReadOnlyList<Sprite> _spellIcons;

    /// <summary>
    /// Lays out the window and fills the list, the reference's
    /// <c>SelectEnumSpell( const std::vector&lt;int&gt; &amp;, const fheroes2::Size &amp; )</c>
    /// (<c>dialog_selectitems.cpp:379-393</c>).
    /// </summary>
    /// <param name="spellIds">Spell ids to show, in the order the caller wants them.</param>
    /// <param name="spellIcons">Every frame of <c>ICN::SPELLS</c>, indexed by <c>Spell::IndexSprite()</c>.</param>
    /// <param name="dialogSize">Size of the active area, normally <see cref="SpellSelectDialog.DialogSize"/>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spellIds"/>, <paramref name="spellIcons"/>, <paramref name="artwork"/>,
    /// <paramref name="titleFont"/> or <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public SpellSelectionWindow(IReadOnlyList<int> spellIds, IReadOnlyList<Sprite> spellIcons,
        Size dialogSize, ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font,
        Size? screen = null)
        : base(dialogSize, Heading, string.Empty, artwork, titleFont, font, screen)
    {
        ArgumentNullException.ThrowIfNull(spellIds);
        ArgumentNullException.ThrowIfNull(spellIcons);

        _spellIcons = spellIcons;
        _spells = new List<ItemSelectionEntry>(spellIds.Count);

        foreach (int id in spellIds)
        {
            _spells.Add(new ItemSelectionEntry(id));
        }

        SetAreaMaxItems(ItemsArea.Height / RowHeight);
        SetListContent(_spells);
        UpdateScrollBarImage();
    }

    /// <summary>The rows, in the order the caller gave them, <c>SetListContent()</c> of the reference.</summary>
    public IReadOnlyList<ItemSelectionEntry> Spells => _spells;

    /// <summary>
    /// The id the selection stands on, or <see cref="SpellType.None"/> when the list is empty; the value
    /// <c>selectSpell()</c> returns after the loop.
    /// </summary>
    public SpellType SelectedSpell => IsValid ? (SpellType)GetCurrent().Id : SpellType.None;

    /// <summary>
    /// Moves the selection to a spell, the reference's <c>SetCurrent( spellId )</c>
    /// (<c>dialog_selectitems.cpp:1140-1142</c>).
    /// </summary>
    /// <remarks>
    /// The reference's overload takes the id, not the row index; a spell that is not in the list resets the
    /// list, which is what <see cref="ListBox{T}.SetCurrent(T)"/> does for an unknown row.
    /// </remarks>
    /// <param name="spell">The spell to select.</param>
    public void SelectSpell(SpellType spell)
    {
        SetCurrent(new ItemSelectionEntry((int)spell));
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Paints a row: the icon of the spell and its name, the reference's
    /// <c>SelectEnumSpell::RedrawItem()</c> over <c>ItemSelectionWindow::renderItem()</c>
    /// (<c>dialog_selectitems.cpp:394-412</c>, <c>978-1010</c>).
    /// </summary>
    /// <param name="item">The row, whose id is the spell.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        Spell spell = new((SpellType)item.Id);
        uint iconIndex = spell.IndexSprite();

        if (iconIndex < (uint)_spellIcons.Count && _spellIcons[(int)iconIndex] is { } icon)
        {
            ImageOps.BlitSprite(output,
                new Point(x + IconOffsetX - (icon.Width / 2), y + RowMiddleOffsetY - (icon.Height / 2)),
                icon);
        }

        RenderText(spell.GetName(), output, new Point(x, y), current);
    }

    /// <summary>
    /// Shows the description of the spell on a right click, the reference's
    /// <c>SelectEnumSpell::ActionListPressRight()</c> (<c>dialog_selectitems.cpp:415-421</c>).
    /// </summary>
    /// <param name="item">The clicked row, whose id is the spell.</param>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Spell spell = new((SpellType)item.Id);

        Popup = new DialogPopup(spell.GetName(), spell.GetDescription());
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
