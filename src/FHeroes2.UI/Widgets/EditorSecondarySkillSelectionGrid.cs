using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The block of skill cells of the editor's secondary skill selection window, a port of
/// <c>SecondarySkillContainerUI</c> (<c>editor_secondary_skill_selection.cpp:58-195</c>).
/// </summary>
/// <remarks>
/// <para>
/// Up to four cells fit in a row: the reference walks <c>i</c> from one to four and stops at the first
/// <c>i * i</c> that has room for the skills (<c>:67-72</c>). The block is centred in the 640x480 screen
/// rather than in the window's active area (<c>:74-75</c>), and the height it is centred by counts whole
/// rows only - twelve skills in four columns are centred as three rows, even though they do occupy three
/// (<c>:75</c>). A last row that is not full is centred under the others (<c>:83</c>, <c>:89-96</c>).
/// </para>
/// <para>
/// Each cell is the icon of the skill pushed three pixels into the <c>SECSKILL</c> frame (<c>:111-117</c>).
/// An inactive skill is the same pair through the gray palette (<c>:118-123</c>), which the port does by
/// graying the icon before it is blitted rather than by a second pass over the pixels it just wrote.
/// </para>
/// </remarks>
public sealed class EditorSecondarySkillSelectionGrid
{
    /// <summary>Vertical distance between two rows of cells, <c>skillRowOffsetY</c> (<c>:52</c>).</summary>
    public const int RowStepY = 90;

    /// <summary>Horizontal distance between two cells, <c>skillItemWidth</c> (<c>:53</c>).</summary>
    public const int ItemWidth = 90;

    /// <summary>Most cells in a row, the loop of <c>:67</c> that stops before five.</summary>
    public const int MaxColumns = 4;

    /// <summary>Most cells in the block; the reference asserts a smaller number, <c>:64</c>.</summary>
    public const int MaxCells = 25;

    /// <summary>How far the icon sits inside its frame, <c>roi.x + 3</c> (<c>:111</c>).</summary>
    public const int IconInset = 3;

    /// <summary>y of the skill name inside the cell, <c>roi.y + 7</c> (<c>:126</c>).</summary>
    public const int NameOffsetY = 7;

    /// <summary>y of the level line, counted from the bottom of the icon, <c>skillImage.height() - 10</c> (<c>:128</c>).</summary>
    public const int LevelOffsetFromIconBottom = 10;

    private const int ScreenWidth = 640;

    private const int ScreenHeight = 480;

    /// <summary>Room kept free under the block for the button row, <c>:75</c>.</summary>
    private const int BottomMargin = 50;

    private readonly SecondarySkillLevel[] _skills;
    private readonly bool[] _selected;
    private readonly Rect[] _areas;

    /// <summary>
    /// Lays the cells out.
    /// </summary>
    /// <param name="skills">The skills to show, in the order of <see cref="SecondarySkills.WitchsHutSkills"/>.</param>
    /// <param name="selected">Which of them start out enabled, one flag per skill.</param>
    /// <param name="origin">Position of the window's active area, <c>activeArea.getPosition()</c> (<c>:267</c>).</param>
    /// <param name="itemSize">Size of one cell, the size of <c>SECSKILL</c> frame 15 (<c>:80</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="skills"/> or <paramref name="selected"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="skills"/> is empty or has no room in the block, or <paramref name="selected"/> does not
    /// have one flag per skill.
    /// </exception>
    public EditorSecondarySkillSelectionGrid(IReadOnlyList<SecondarySkillLevel> skills, IReadOnlyList<bool> selected,
        Point origin, Size itemSize)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(selected);

        if (skills.Count == 0 || skills.Count >= MaxCells)
        {
            throw new ArgumentException("The block shows between one and twenty four skills.", nameof(skills));
        }

        if (selected.Count != skills.Count)
        {
            throw new ArgumentException("There must be one flag per skill.", nameof(selected));
        }

        _skills = [.. skills];
        _selected = [.. selected];

        Columns = MaxColumns;

        for (int i = 1; i < MaxColumns; ++i)
        {
            if (i * i >= _skills.Length)
            {
                Columns = i;
                break;
            }
        }

        // 参考 :75 用的是整数除法的行数（12 / 4 = 3），末行不满时并不加一，端口照抄。
        int x = origin.X + ((ScreenWidth - (Columns * ItemWidth)) / 2);
        int y = origin.Y + ((ScreenHeight - BottomMargin - (RowStepY * (_skills.Length / Columns))) / 2);

        int lastRowColumns = _skills.Length % Columns;
        int lastRowOffsetX = lastRowColumns > 0 ? (Columns - lastRowColumns) * ItemWidth / 2 : 0;

        _areas = new Rect[_skills.Length];

        for (int i = 0; i < _skills.Length; ++i)
        {
            int row = i / Columns;
            int column = i % Columns;
            int offsetX = column * ItemWidth + (row == (_skills.Length / Columns) ? lastRowOffsetX : 0);

            _areas[i] = new Rect(x + offsetX, y + (row * RowStepY), itemSize.Width, itemSize.Height);
        }
    }

    /// <summary>Number of cells in the block.</summary>
    public int Count => _skills.Length;

    /// <summary>How many cells fit in a row.</summary>
    public int Columns { get; }

    /// <summary>The skill of each cell, in drawing order.</summary>
    public IReadOnlyList<SecondarySkillLevel> Skills => _skills;

    /// <summary>Where each cell is drawn, <c>_skillRoi</c> (<c>:91-95</c>).</summary>
    public IReadOnlyList<Rect> Areas => _areas;

    /// <summary>Whether the skill at <paramref name="index"/> is enabled.</summary>
    /// <param name="index">Zero based cell number.</param>
    /// <returns>The flag of the cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public bool IsSelected(int index) => _selected[index];

    /// <summary>How many skills are enabled, the count of <c>:354-360</c>.</summary>
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

    /// <summary>The cell under <paramref name="position"/>, <c>GetRectIndex()</c> (<c>:163</c>).</summary>
    /// <param name="position">Cursor position.</param>
    /// <returns>The zero based cell number, or <c>-1</c> when the cursor is over none of them.</returns>
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

    /// <summary>Turns the skill at <paramref name="index"/> on or off, <c>:172-177</c>.</summary>
    /// <param name="index">Zero based cell number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void Toggle(int index) => _selected[index] = !_selected[index];

    /// <summary>
    /// Turns every skill on or off, the two loops of the toggle buttons (<c>:314-316</c>, <c>:324-326</c>).
    /// </summary>
    /// <param name="selected">The state every skill is put into.</param>
    public void SetAllSelected(bool selected) => Array.Fill(_selected, selected);

    /// <summary>
    /// Draws the cells, the enabled ones in colour and the others in gray, <c>draw()</c> (<c>:100-130</c>).
    /// </summary>
    /// <param name="target">Where to draw.</param>
    /// <param name="frame">The frame of an enabled cell, <c>SECSKILL</c> frame 15.</param>
    /// <param name="inactiveFrame">The same frame through the gray palette (<c>:104-105</c>).</param>
    /// <param name="iconFor">The <c>SECSKILL</c> icon of a skill, or <see langword="null"/> when the artwork lacks it.</param>
    /// <param name="font">The small white font the two lines are drawn with.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, Sprite frame, Sprite inactiveFrame, Func<SecondarySkill, Sprite?> iconFor,
        IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(inactiveFrame);
        ArgumentNullException.ThrowIfNull(iconFor);
        ArgumentNullException.ThrowIfNull(font);

        for (int i = 0; i < _skills.Length; ++i)
        {
            Rect area = _areas[i];
            bool selected = _selected[i];
            SecondarySkill skill = _skills[i].Skill;
            Point iconPosition = new(area.X + IconInset, area.Y + IconInset);
            Sprite? icon = iconFor(skill);

            if (icon is not null)
            {
                Sprite drawn = icon;

                if (!selected)
                {
                    drawn = new Sprite(icon.Image.Clone());
                    ImageOps.ApplyPalette(drawn.Image, PaletteId.Gray);
                }

                ImageOps.BlitSprite(target, iconPosition, drawn);
            }

            // 帧压在图标上面：参考先贴图标再贴帧（:113-117），帧的中间是透的。
            ImageOps.BlitSprite(target, area.Position, selected ? frame : inactiveFrame);

            if (icon is null)
            {
                continue;
            }

            string name = SecondarySkills.GetName(skill);
            int nameWidth = GlyphText.Measure(font, name);

            GlyphText.DrawLine(font, target,
                new Point(iconPosition.X + ((icon.Width - nameWidth) / 2), area.Y + NameOffsetY), name, area);

            string level = SecondarySkills.GetLevelName(_skills[i].Level);
            int levelWidth = GlyphText.Measure(font, level);

            GlyphText.DrawLine(font, target,
                new Point(iconPosition.X + ((icon.Width - levelWidth) / 2),
                    area.Y + icon.Height - LevelOffsetFromIconBottom), level, area);
        }
    }
}
