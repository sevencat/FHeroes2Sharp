using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Draws the pages of the spell book — <c>spellBookRedrawLists()</c>, <c>SpellBookRedrawSpells()</c> and
/// <c>SpellBookRedrawManaPoints()</c> of <c>spell_book.cpp:86-192</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference composes the whole book into one sprite and blits it at the window position, composing again
/// whenever the page or the filter changes. The port does the same: <see cref="RenderPages"/> returns the
/// shadowed sprite, and the spell rectangles it fills in are already in screen coordinates, which is what the
/// hit test of the window needs.
/// </para>
/// <para>
/// 参数里的 <c>owner</c> 与 <c>canCastSpell</c> 对应参考的 <c>hero</c> 与 <c>canCastSpell</c>：前者提供法术消耗
/// （带宝物减免）和「能不能施放」，后者决定要不要把不能施放的法术画成灰色。参考里 <c>canCastSpell</c> 为
/// <see langword="false"/> 时不查也能施放，因为整本书都不是用来施法的（编辑模式、只看）。
/// </para>
/// </remarks>
public sealed class SpellBookRenderer
{
    /// <summary>Width the spell name may occupy before it wraps to two rows, <c>maxTextWidth</c>.</summary>
    public const int MaxTextWidth = 80;

    private readonly SpellBookArtwork _artwork;
    private readonly SpellBookFonts _fonts;
    private readonly ISpellBookOwner _owner;
    private readonly bool _canCastSpell;

    /// <summary>Creates a renderer for one book.</summary>
    /// <param name="artwork">Frames of <c>ICN::BOOK</c> and the spell icons.</param>
    /// <param name="fonts">The two small fonts of the rows.</param>
    /// <param name="owner">Whoever the book belongs to.</param>
    /// <param name="canCastSpell">Whether the book is opened for casting, which greys out what cannot be cast.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public SpellBookRenderer(SpellBookArtwork artwork, SpellBookFonts fonts, ISpellBookOwner owner,
        bool canCastSpell)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(fonts.SmallWhite);
        ArgumentNullException.ThrowIfNull(fonts.SmallGray);
        ArgumentNullException.ThrowIfNull(owner);

        _artwork = artwork;
        _fonts = fonts;
        _owner = owner;
        _canCastSpell = canCastSpell;
    }

    /// <summary>
    /// Draws the spells of one page, <c>SpellBookRedrawSpells()</c> (<c>spell_book.cpp:86-122</c>).
    /// </summary>
    /// <remarks>
    /// Six rows of two columns. The row nearest the outer edge of each page sits five pixels higher, which is
    /// what the <c>extraOffsetY</c> term of the reference does. Each entry occupies as much of the icon as is
    /// drawn plus ten pixels of clickable height, so the empty space under a short icon still selects the spell.
    /// </remarks>
    /// <param name="spells">The filtered list of the whole book.</param>
    /// <param name="coords">Receives the clickable rectangle of every drawn spell, in screen coordinates.</param>
    /// <param name="index">Index in <paramref name="spells"/> of the first entry of this page.</param>
    /// <param name="px">X of the page in the composed sprite: zero for the left page, 220 for the right.</param>
    /// <param name="py">Y of the page in the composed sprite, always zero.</param>
    /// <param name="isRight">Whether this is the right page, which mirrors the row offset.</param>
    /// <param name="output">The composed book.</param>
    /// <param name="outputOffset">Where the composed sprite is drawn on screen, the reference's <c>pt</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spells"/>, <paramref name="coords"/> or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    public void RenderSpells(IReadOnlyList<SpellType> spells, List<Rect> coords, int index, int px, int py,
        bool isRight, Sprite output, Point outputOffset)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(coords);
        ArgumentNullException.ThrowIfNull(output);

        for (int i = 0; i < SpellBook.SpellsPerPage; ++i)
        {
            if (spells.Count <= index + i)
            {
                return;
            }

            int posX = i % 2;
            int ox = 84 + (81 * posX);

            // Make the spell near the left and the right edge of the book 5 pixels lower.
            int extraOffsetY = ((posX == 1) == isRight) ? 0 : 5;
            int oy = 71 + (78 * (i / 2)) - extraOffsetY;

            Spell spell = new(spells[index + i]);
            string spellName = spell.GetName();
            uint spellCost = spell.SpellPoints(_owner.BagArtifacts);

            // If casting spells is prohibited in principle, it makes no sense to check whether this owner can
            // cast them and highlight them in gray if not.
            bool isAvailable = !_canCastSpell || _owner.CanCastSpell(spell);

            Sprite? icon = GetIcon(spell);
            if (icon is null)
            {
                continue;
            }

            int vertOffset = Math.Min(6, 49 - icon.Height);
            Rect rect = new(px + ox - ((icon.Width + (icon.Width % 2)) / 2),
                py + oy - icon.Height - vertOffset + 2, icon.Width, icon.Height + 10);

            ImageOps.BlitSprite(output, new Point(rect.X, rect.Y), icon);

            int rowCount = GlyphText.WrapLines(_fonts.SmallWhite, spellName, MaxTextWidth).Count;
            string text = spellName + (rowCount == 1 ? GlyphText.LineSeparator : GlyphText.SpaceCharacter) +
                '[' + spellCost.ToString(CultureInfo.InvariantCulture) + ']';

            GlyphText.DrawMultiline(isAvailable ? _fonts.SmallWhite : _fonts.SmallGray, output.Image,
                new Point(px + ox - 40, py + oy + 2), text, MaxTextWidth, center: true);

            coords.Add(new Rect(rect.X + outputOffset.X, rect.Y + outputOffset.Y, rect.Width, rect.Height));
        }
    }

    /// <summary>
    /// Writes the spell points on their bookmark, <c>SpellBookRedrawManaPoints()</c>
    /// (<c>spell_book.cpp:124-141</c>).
    /// </summary>
    /// <remarks>
    /// Three stacked digits, each centred on the bookmark, with a blank in place of a leading zero — the
    /// bookmark has three holes. Anything above 999 is clamped, as the reference does for a corrupt save.
    /// </remarks>
    /// <param name="dst">Top left corner of the bookmark, in composed-sprite coordinates.</param>
    /// <param name="manaPoints">The mana to print.</param>
    /// <param name="output">The composed book.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void RenderManaPoints(Point dst, uint manaPoints, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        Point point = new(dst.X + 11, dst.Y + 11);

        if (manaPoints > 999)
        {
            manaPoints = 999; // just in case of a broken code
        }

        DrawDigit(manaPoints >= 100 ? (manaPoints / 100).ToString(CultureInfo.InvariantCulture) : " ",
            point, output);
        point = new Point(point.X, point.Y + _fonts.SmallWhite.LineHeight);

        DrawDigit(manaPoints >= 10 ? ((manaPoints % 100) / 10).ToString(CultureInfo.InvariantCulture) : " ",
            point, output);
        point = new Point(point.X, point.Y + _fonts.SmallWhite.LineHeight);

        DrawDigit(manaPoints > 0 ? (manaPoints % 10).ToString(CultureInfo.InvariantCulture) : "0", point,
            output);
    }

    /// <summary>
    /// Composes the whole open book, <c>spellBookRedrawLists()</c> (<c>spell_book.cpp:143-192</c>).
    /// </summary>
    /// <remarks>
    /// Left page, the straight corner if this is the first screen, right page, the straight corner if this is
    /// the last one, the four bookmarks, the mana, the twelve spells, and finally the drop shadow the book is
    /// drawn with.
    /// </remarks>
    /// <param name="spells">The filtered list of the whole book.</param>
    /// <param name="coords">Receives the clickable rectangle of every drawn spell, in screen coordinates.</param>
    /// <param name="index">Index of the first spell of the screen.</param>
    /// <param name="position">Where the book is drawn on screen.</param>
    /// <param name="spellPoints">The mana to print on the bookmark.</param>
    /// <param name="displayable">The filter the book was opened with, which decides the tabs and the height.</param>
    /// <returns>The shadowed book, ready to be blitted at <c>position + shadow</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spells"/> or <paramref name="coords"/> is <see langword="null"/>.
    /// </exception>
    public Sprite RenderPages(IReadOnlyList<SpellType> spells, List<Rect> coords, int index, Point position,
        uint spellPoints, SpellBookFilter displayable)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(coords);

        Size bookSize = _artwork.GetBookSize(displayable);
        Sprite output = new(bookSize.Width, bookSize.Height);
        output.Reset();

        Sprite page = _artwork.Page;

        // Left page.
        ImageOps.BlitSprite(output, Point.Zero, page);

        if (index == 0)
        {
            Sprite straightCorner = _artwork.StraightCorner;

            ImageOps.BlitSprite(output,
                new Point(page.Width - straightCorner.OffsetX - straightCorner.Width, straightCorner.OffsetY),
                straightCorner);
        }

        // Right page.
        ImageOps.BlitSprite(output, new Point(page.Width, 0), page);

        if (spells.Count <= index + SpellBook.SpellsPerScreen)
        {
            Sprite straightCorner = _artwork.StraightCorner;

            ImageOps.BlitSprite(output,
                new Point(page.Width + straightCorner.OffsetX, straightCorner.OffsetY), straightCorner);
        }

        ImageOps.BlitSprite(output, SpellBookWindow.InfoBookmarkOffset, _artwork.SpellPointsBookmark);

        if (displayable != SpellBookFilter.Combat)
        {
            ImageOps.BlitSprite(output, SpellBookWindow.AdventureBookmarkOffset, _artwork.AdventureBookmark);
        }

        if (displayable != SpellBookFilter.Adventure)
        {
            ImageOps.BlitSprite(output, SpellBookWindow.CombatBookmarkOffset, _artwork.CombatBookmark);
        }

        ImageOps.BlitSprite(output, SpellBookWindow.CloseBookmarkOffset, _artwork.CloseBookmark);

        RenderManaPoints(SpellBookWindow.InfoBookmarkOffset, spellPoints, output.Image);

        coords.Clear();
        RenderSpells(spells, coords, index, 0, 0, false, output, position);
        RenderSpells(spells, coords, index + SpellBook.SpellsPerPage, 220, 0, true, output, position);

        return ImageOps.AddShadow(output, SpellBookWindow.ShadowOffset, SpellBookWindow.ShadowTransform);
    }

    /// <summary>The icon of a spell in <c>ICN::SPELLS</c>, or <see langword="null"/> when the frame is missing.</summary>
    /// <param name="spell">The spell.</param>
    /// <returns>The sprite, or <see langword="null"/>.</returns>
    private Sprite? GetIcon(Spell spell)
    {
        uint index = spell.IndexSprite();

        return index < (uint)_artwork.SpellIcons.Count ? _artwork.SpellIcons[(int)index] : null;
    }

    /// <summary>Draws one digit of the mana counter centred on <paramref name="point"/>, <c>text.draw()</c>.</summary>
    /// <param name="text">The digit or a blank.</param>
    /// <param name="point">Top centre of the digit.</param>
    /// <param name="output">The composed book.</param>
    private void DrawDigit(string text, Point point, PixelBuffer output)
    {
        int width = GlyphText.Measure(_fonts.SmallWhite, text);

        GlyphText.DrawLine(_fonts.SmallWhite, output, new Point(point.X - (width / 2), point.Y), text);
    }
}
