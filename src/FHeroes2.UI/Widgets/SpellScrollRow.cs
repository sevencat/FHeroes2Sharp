using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// One level's spells of the mage guild screen, drawn as a row of scrolls,
/// <c>fheroes2::SpellsInOneRow</c> (<c>gui/ui_mage_guild.cpp:95-228</c>).
/// </summary>
/// <remarks>
/// <para>
/// A row is a fixed number of slots, one per spell the guild owes that level; a slot holding
/// <see cref="SpellType.None"/> stays folded. The geometry is the reference's: the <i>centre</i> of slot
/// <c>i</c> is at <c>offset.X + 110 * i</c>, an open scroll hangs from <c>offset.Y</c> and a folded one
/// sits seven pixels lower, and the two scroll sizes differ, so a row mixes two widths.
/// </para>
/// <para>
/// <b>Not ported</b>: <c>queueEventProcessing()</c> — it polls <c>LocalEvent</c> and opens the spell
/// description dialog, so <see cref="GetIndexAt"/> is the hit test without the event plumbing — and
/// <c>setCurrentSpell()</c>, which only the map editor calls. <c>redrawCurrentSpell()</c> is folded into
/// <see cref="Draw"/>: repainting one slot exists because the reference keeps the screen between events,
/// while here the caller draws the row when it draws the screen.
/// </para>
/// </remarks>
public sealed class SpellScrollRow
{
    /// <summary>Horizontal distance between the centres of two slots, <c>110</c> in the reference.</summary>
    public const int SlotStepX = 110;

    /// <summary>How much lower a folded scroll hangs than an open one, <c>offset.y + 7</c>.</summary>
    public const int FoldedOffsetY = 7;

    /// <summary>Left inset of the spell icon inside an open scroll, <c>dst.x + 3</c>.</summary>
    public const int IconInsetX = 3;

    /// <summary>Vertical centre of the spell icon inside an open scroll, <c>dst.y + 31</c>.</summary>
    public const int IconCentreY = 31;

    /// <summary>Left inset of the spell name, <c>dst.x + 18</c>.</summary>
    public const int NameInsetX = 18;

    /// <summary>Top of the spell name inside an open scroll, <c>dst.y + 57</c>.</summary>
    public const int NameOffsetY = 57;

    /// <summary>Width the spell name may use, the <c>78</c> of the reference's <c>Text::draw</c>.</summary>
    public const int NameWidth = 78;

    private readonly SpellType[] _spells;
    private readonly Rect[] _coords;

    /// <summary>Builds a row of the given slots, <c>SpellsInOneRow( SpellStorage )</c>.</summary>
    /// <param name="spells">The spells, one entry per slot; <see cref="SpellType.None"/> for an empty slot.</param>
    public SpellScrollRow(IReadOnlyList<SpellType> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        _spells = [.. spells];
        _coords = new Rect[_spells.Length];
    }

    /// <summary>The spells of the row, one per slot.</summary>
    public IReadOnlyList<SpellType> Spells => _spells;

    /// <summary>
    /// Where each scroll goes, <c>_coords</c>; empty until <see cref="SetPosition"/> is called, and the
    /// same length as <see cref="Spells"/> afterwards.
    /// </summary>
    public IReadOnlyList<Rect> Coords => _coords;

    /// <summary>
    /// Index of the slot under the pointer, <c>_currentSpellIndex</c>; <c>-1</c> when the pointer is on no
    /// scroll. The window sets it from <see cref="GetIndexAt"/> before reading <see cref="GetCurrentSpell"/>.
    /// </summary>
    public int CurrentIndex { get; set; } = -1;

    /// <summary>
    /// Lays the row out around a centre, <c>setPosition( offset )</c>.
    /// </summary>
    /// <remarks>
    /// Both scroll sizes are inputs rather than looked up: the reference reads them from
    /// <c>ICN::TOWNWIND</c> frames 0 and 1, and a test can then check the arithmetic without an archive.
    /// </remarks>
    /// <param name="offset">Centre of the first slot and top of the row.</param>
    /// <param name="openedScroll">Size of a scroll holding a spell.</param>
    /// <param name="foldedScroll">Size of an empty scroll.</param>
    public void SetPosition(in Point offset, in Size openedScroll, in Size foldedScroll)
    {
        for (int index = 0; index < _spells.Length; ++index)
        {
            bool open = _spells[index] != SpellType.None;
            Size size = open ? openedScroll : foldedScroll;

            _coords[index] = new Rect(
                offset.X + (index * SlotStepX) - (size.Width / 2),
                open ? offset.Y : offset.Y + FoldedOffsetY,
                size.Width,
                size.Height);
        }
    }

    /// <summary>
    /// The slot under a point, <c>GetRectIndex( _coords, point )</c>: the first rectangle that contains
    /// it, or <c>-1</c>.
    /// </summary>
    /// <param name="point">A point in the row's coordinate space.</param>
    /// <returns>The slot index, or <c>-1</c>.</returns>
    public int GetIndexAt(in Point point)
    {
        for (int index = 0; index < _coords.Length; ++index)
        {
            if (_coords[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The spell under the pointer, <c>getCurrentSpell()</c>; <see cref="SpellType.None"/> when no slot is
    /// current or the current slot is empty.
    /// </summary>
    /// <returns>The current spell, if any.</returns>
    public SpellType GetCurrentSpell() =>
        CurrentIndex >= 0 && CurrentIndex < _spells.Length ? _spells[CurrentIndex] : SpellType.None;

    /// <summary>
    /// Makes the first slot holding <paramref name="spellToFind"/> current, <c>checkSpellAndMakeItCurrent()</c>.
    /// </summary>
    /// <param name="spellToFind">The spell to look for.</param>
    /// <returns>
    /// <see langword="true"/> when the spell is in the row, <see langword="false"/> otherwise — in which case
    /// <see cref="CurrentIndex"/> is reset to <c>-1</c>.
    /// </returns>
    public bool CheckSpellAndMakeItCurrent(SpellType spellToFind)
    {
        for (int index = 0; index < _spells.Length; ++index)
        {
            if (_spells[index] == spellToFind)
            {
                CurrentIndex = index;
                return true;
            }
        }

        CurrentIndex = -1;
        return false;
    }

    /// <summary>
    /// Paints the row, <c>redraw()</c> and <c>redrawCurrentSpell()</c> together.
    /// </summary>
    /// <remarks>
    /// A slot with a spell gets the open scroll, its icon and its name; an empty slot gets the folded
    /// scroll and nothing else. The icon is centred horizontally with a three pixel inset and vertically
    /// hung from <see cref="IconCentreY"/>, which is why a tall icon grows upwards rather than downwards.
    /// </remarks>
    /// <param name="target">The display buffer.</param>
    /// <param name="openedScroll">The open scroll sprite, <c>ICN::TOWNWIND</c> frame 0.</param>
    /// <param name="foldedScroll">The folded scroll sprite, <c>ICN::TOWNWIND</c> frame 1.</param>
    /// <param name="font">The small white font of the spell names.</param>
    /// <param name="iconFor">Looks a spell's icon up, <c>ICN::SPELLS</c> at <c>Spell::IndexSprite()</c>.</param>
    public void Draw(
        PixelBuffer target,
        Sprite openedScroll,
        Sprite foldedScroll,
        IGlyphSource font,
        Func<SpellType, Sprite?> iconFor)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(openedScroll);
        ArgumentNullException.ThrowIfNull(foldedScroll);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(iconFor);

        if (_coords.Length != _spells.Length)
        {
            return;
        }

        for (int index = 0; index < _spells.Length; ++index)
        {
            SpellType spell = _spells[index];
            Rect destination = _coords[index];

            if (spell == SpellType.None)
            {
                ImageOps.BlitSprite(target, destination.Position, foldedScroll);
                continue;
            }

            ImageOps.BlitSprite(target, destination.Position, openedScroll);

            if (iconFor(spell) is { } icon)
            {
                ImageOps.BlitSprite(
                    target,
                    new Point(
                        destination.X + IconInsetX + ((destination.Width - icon.Width) / 2),
                        destination.Y + IconCentreY - (icon.Height / 2)),
                    icon);
            }

            string name = new Spell(spell).GetName();
            Rect nameArea = new(
                destination.X + NameInsetX,
                destination.Y + NameOffsetY,
                NameWidth,
                font.LineHeight);

            GlyphText.DrawLine(font, target, nameArea.Position, name, nameArea);
        }
    }
}
