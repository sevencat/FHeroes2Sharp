using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The block of spell scrolls of the editor's spell selection window, a port of
/// <c>SpellContainerUI</c> (<c>editor_spell_selection.cpp:51-167</c>).
/// </summary>
/// <remarks>
/// <para>
/// Up to five scrolls fit in a row and up to five rows are used, which covers every spell level of the game. The
/// columns follow from the number of spells - the smallest square that has room for them - and the whole block is
/// centred in the 640x480 screen rather than in the window's active area, exactly like the reference
/// (<c>:66-76</c>). A last row that is not full is centred under the others (<c>:83-97</c>).
/// </para>
/// <para>
/// The selection is a flag per scroll: the container owns it, and the caller reads it back. A scroll of a disabled
/// spell is the same scroll drawn with the gray palette (<c>:105-130</c>).
/// </para>
/// </remarks>
public sealed class EditorSpellSelectionGrid
{
    /// <summary>Vertical distance between two rows of scrolls, <c>spellRowOffsetY</c> (<c>:51</c>).</summary>
    public const int RowStepY = 90;

    /// <summary>Horizontal distance between two scrolls, <c>spellItemWidth</c> (<c>:52</c>).</summary>
    public const int ItemWidth = 110;

    /// <summary>Most scrolls in a row, <c>:54-56</c>.</summary>
    public const int MaxColumns = 5;

    /// <summary>Most scrolls in the block; the reference asserts a smaller number, <c>:63</c>.</summary>
    public const int MaxCells = 25;

    /// <summary>The screen the block is centred in, <c>Display::DEFAULT_WIDTH</c>/<c>DEFAULT_HEIGHT</c>.</summary>
    private const int ScreenWidth = 640;

    private const int ScreenHeight = 480;

    /// <summary>Room kept free under the block for the button row, <c>:76</c>.</summary>
    private const int BottomMargin = 50;

    private readonly SpellType[] _spells;
    private readonly bool[] _selected;
    private readonly Rect[] _areas;

    /// <summary>
    /// Lays the scrolls out.
    /// </summary>
    /// <param name="spells">The spells to show, in the order of <c>Spell::getAllSpellIdsSuitableForSpellBook()</c>.</param>
    /// <param name="selected">Which of them start out enabled, one flag per spell.</param>
    /// <param name="origin">Position of the window's active area, <c>activeArea.getPosition()</c> (<c>:272</c>).</param>
    /// <param name="itemSize">Size of one scroll, the size of <c>TOWNWIND</c> frame 0 (<c>:81</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="spells"/> or <paramref name="selected"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="spells"/> is empty or has no room in the block, or <paramref name="selected"/> does not have
    /// one flag per spell.
    /// </exception>
    public EditorSpellSelectionGrid(IReadOnlyList<SpellType> spells, IReadOnlyList<bool> selected, Point origin,
        Size itemSize)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(selected);

        if (spells.Count == 0 || spells.Count >= MaxCells)
        {
            throw new ArgumentException("The block shows between one and twenty four spells.", nameof(spells));
        }

        if (selected.Count != spells.Count)
        {
            throw new ArgumentException("There must be one flag per spell.", nameof(selected));
        }

        _spells = [.. spells];
        _selected = [.. selected];

        Columns = MaxColumns;
        for (int i = 1; i < MaxColumns; ++i)
        {
            if (i * i >= _spells.Length)
            {
                Columns = i;
                break;
            }
        }

        int rows = (_spells.Length + Columns - 1) / Columns;
        int x = origin.X + ((ScreenWidth - (Columns * ItemWidth)) / 2);
        int y = origin.Y + ((ScreenHeight - BottomMargin - (RowStepY * rows)) / 2);

        int lastRowColumns = _spells.Length % Columns;
        int lastRowOffsetX = lastRowColumns > 0 ? (Columns - lastRowColumns) * ItemWidth / 2 : 0;

        _areas = new Rect[_spells.Length];

        for (int i = 0; i < _spells.Length; ++i)
        {
            int row = i / Columns;
            int column = i % Columns;
            int offsetX = column * ItemWidth + (row == (_spells.Length / Columns) ? lastRowOffsetX : 0);

            _areas[i] = new Rect(x + offsetX, y + (row * RowStepY), itemSize.Width, itemSize.Height);
        }
    }

    /// <summary>Number of scrolls in the block.</summary>
    public int Count => _spells.Length;

    /// <summary>How many scrolls fit in a row.</summary>
    public int Columns { get; }

    /// <summary>The spell of each scroll, in drawing order.</summary>
    public IReadOnlyList<SpellType> Spells => _spells;

    /// <summary>Where each scroll is drawn, <c>_spellRoi</c> (<c>:96</c>).</summary>
    public IReadOnlyList<Rect> Areas => _areas;

    /// <summary>Whether the spell at <paramref name="index"/> is enabled.</summary>
    /// <param name="index">Zero based scroll number.</param>
    /// <returns>The flag of the scroll.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public bool IsSelected(int index) => _selected[index];

    /// <summary>How many spells are enabled, the count of <c>:379-389</c>.</summary>
    public int SelectedCount
    {
        get
        {
            int count = 0;

            foreach (bool selected in _selected)
            {
                if (selected)
                {
                    ++count;
                }
            }

            return count;
        }
    }

    /// <summary>The scroll under <paramref name="position"/>, <c>GetRectIndex()</c> (<c>:139</c>).</summary>
    /// <param name="position">Cursor position.</param>
    /// <returns>The zero based scroll number, or <c>-1</c> when the cursor is over none of them.</returns>
    public int IndexAt(Point position)
    {
        for (int i = 0; i < _areas.Length; ++i)
        {
            if (_areas[i].Contains(position))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Turns the spell at <paramref name="index"/> on or off, <c>:149</c>.</summary>
    /// <param name="index">Zero based scroll number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void Toggle(int index) => _selected[index] = !_selected[index];

    /// <summary>
    /// Turns every spell on or off, the two loops of the toggle buttons (<c>:347-349</c>, <c>:357-359</c>).
    /// </summary>
    /// <param name="selected">The state every spell is put into.</param>
    public void SetAllSelected(bool selected) => Array.Fill(_selected, selected);

    /// <summary>
    /// Draws the scrolls, the enabled ones in colour and the others in gray, <c>draw()</c> (<c>:101-135</c>).
    /// </summary>
    /// <param name="target">Where to draw.</param>
    /// <param name="scroll">The scroll of an enabled spell, <c>TOWNWIND</c> frame 0.</param>
    /// <param name="inactiveScroll">The same scroll through the gray palette.</param>
    /// <param name="iconFor">The <c>SPELLS</c> icon of a spell, or <see langword="null"/> when the artwork lacks it.</param>
    /// <param name="font">The small white font the names are drawn with.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, Sprite scroll, Sprite inactiveScroll, Func<SpellType, Sprite?> iconFor,
        IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(inactiveScroll);
        ArgumentNullException.ThrowIfNull(iconFor);
        ArgumentNullException.ThrowIfNull(font);

        for (int i = 0; i < _spells.Length; ++i)
        {
            Rect area = _areas[i];
            bool selected = _selected[i];

            ImageOps.BlitSprite(target, area.Position, selected ? scroll : inactiveScroll);

            if (iconFor(_spells[i]) is { } icon)
            {
                Sprite drawn = icon;

                if (!selected)
                {
                    drawn = new Sprite(icon.Image.Clone());
                    ImageOps.ApplyPalette(drawn.Image, PaletteId.Gray);
                }

                ImageOps.BlitSprite(
                    target,
                    new Point(
                        area.X + SpellScrollRow.IconInsetX + ((area.Width - drawn.Width) / 2),
                        area.Y + SpellScrollRow.IconCentreY - (drawn.Height / 2)),
                    drawn);
            }

            Rect nameArea = new(
                area.X + SpellScrollRow.NameInsetX,
                area.Y + SpellScrollRow.NameOffsetY,
                SpellScrollRow.NameWidth,
                font.LineHeight);

            GlyphText.DrawLine(font, target, nameArea.Position, new Spell(_spells[i]).GetName(), nameArea);
        }
    }
}
