using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where everything in the well window goes, <c>Castle::_openWell()</c> and
/// <c>Castle::_wellRedrawBackground()</c> (<c>castle_well.cpp:61-101</c>, <c>castle_well.cpp:377-547</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window is the full 640 by 480 screen, so the cells are laid out in two columns of three: 314 pixels apart
/// horizontally and 150 vertically, with the first cell one pixel below the top edge. Every anchor inside a cell is
/// therefore "cell origin plus a constant", which is what the <c>Get…</c> methods here answer.
/// </para>
/// <para>
/// The bottom bar is cut in two by the buttons: the left piece starts at the MAX button and the right one ends at
/// EXIT, and both are read from <c>ICN::SMALLBAR</c> starting one pixel in, because its first column is solid black.
/// The last column of the window is filled with black by hand — the original background has a transparent line
/// there that no other sprite covers.
/// </para>
/// </remarks>
public sealed class WellLayout
{
    /// <summary>Width of the window, <c>fheroes2::Display::DEFAULT_WIDTH</c>.</summary>
    public const int DialogWidth = 640;

    /// <summary>Height of the window, <c>fheroes2::Display::DEFAULT_HEIGHT</c>.</summary>
    public const int DialogHeight = 480;

    /// <summary>Top row of the bottom bar, <c>bottomBarOffsetY</c> (<c>castle_well.cpp:61</c>).</summary>
    public const int BottomBarOffsetY = 461;

    /// <summary>Row inside the bar the title is drawn on, <c>bottomBarOffsetY + 3</c>.</summary>
    public const int TitleOffsetY = 3;

    /// <summary>Column the title is centred on, <c>315</c>.</summary>
    public const int TitleCentreX = 315;

    /// <summary>Horizontal distance between two cells.</summary>
    public const int CellStepX = 314;

    /// <summary>Vertical distance between two cells.</summary>
    public const int CellStepY = 150;

    /// <summary>Row of the first cell, <c>1</c> — not zero, as in the reference.</summary>
    public const int FirstCellOffsetY = 1;

    /// <summary>Picture anchor inside a cell, <c>( 21, 35 )</c>.</summary>
    public const int PictureOffsetX = 21;

    /// <summary>Picture anchor inside a cell, <c>( 21, 35 )</c>.</summary>
    public const int PictureOffsetY = 35;

    /// <summary>Centre of the dwelling name inside a cell, <c>( 86, 104 )</c>.</summary>
    public const int DwellingNameCentreX = 86;

    /// <summary>Top row of the dwelling name inside a cell.</summary>
    public const int DwellingNameOffsetY = 104;

    /// <summary>Centre of the creature name inside a cell, <c>( 122, 19 )</c>.</summary>
    public const int MonsterNameCentreX = 122;

    /// <summary>Top row of the creature name inside a cell.</summary>
    public const int MonsterNameOffsetY = 19;

    /// <summary>Centre of the statistics column inside a cell, <c>269</c>.</summary>
    public const int StatsCentreX = 269;

    /// <summary>Top row of the statistics column inside a cell, <c>22</c>.</summary>
    public const int StatsOffsetY = 22;

    /// <summary>Top left corner of a monster's clickable area, <c>( 20, 18 )</c>.</summary>
    public const int MonsterAreaOffsetX = 20;

    /// <summary>Top row of a monster's clickable area, <c>18</c>.</summary>
    public const int MonsterAreaOffsetY = 18;

    /// <summary>Width of a monster's clickable area, <c>288</c>.</summary>
    public const int MonsterAreaWidth = 288;

    /// <summary>Height of a monster's clickable area, <c>124</c>.</summary>
    public const int MonsterAreaHeight = 124;

    /// <summary>Top left corner of the "Available" text, <c>( 20, 121 )</c>.</summary>
    public const int AvailableOffsetX = 20;

    /// <summary>Top row of the "Available" text, <c>121</c>.</summary>
    public const int AvailableOffsetY = 121;

    /// <summary>Column the population number is centred on, <c>109</c>.</summary>
    public const int AvailablePopulationCentreX = 109;

    /// <summary>Column the word "Available:" is centred on, <c>106 / 2</c>.</summary>
    public const int AvailableLabelCentreX = 106 / 2;

    /// <summary>Row the word "Available:" is drawn on, <c>offset.y + 2</c>.</summary>
    public const int AvailableLabelOffsetY = 2;

    /// <summary>Column the left piece of the bar is read from, <c>1</c>.</summary>
    public const int BarLeftSourceX = 1;

    /// <summary>Number of dwellings, i.e. number of cells.</summary>
    public const int CellCount = 6;

    /// <summary>Places the window in the middle of <paramref name="screen"/> and lays out its parts.</summary>
    /// <param name="screen">The screen the window is centred on.</param>
    /// <param name="barSize">Size of one bar sprite, i.e. the height of the bar.</param>
    /// <param name="maxSize">Size of the MAX button.</param>
    /// <param name="exitSize">Size of the EXIT button.</param>
    /// <exception cref="ArgumentOutOfRangeException">A sprite has no size.</exception>
    public WellLayout(in Rect screen, in Size barSize, in Size maxSize, in Size exitSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(barSize.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exitSize.Width);

        int left = screen.X + ((screen.Width - DialogWidth) / 2);
        int top = screen.Y + ((screen.Height - DialogHeight) / 2);
        Dialog = new Rect(left, top, DialogWidth, DialogHeight);

        int barY = Dialog.Y + BottomBarOffsetY;
        BarHeight = barSize.Height;

        Max = new Rect(Dialog.X, barY, maxSize.Width, maxSize.Height);
        Exit = new Rect(Dialog.Right - exitSize.Width, barY, exitSize.Width, exitSize.Height);

        // The two pieces of the bar share the window's half: the left one starts where MAX ends, the right one ends
        // where EXIT starts. The odd pixel of an odd width falls to the right piece because left = width / 2.
        BarLeft = new Rect(Dialog.X + maxSize.Width, barY, (DialogWidth / 2) - maxSize.Width, barSize.Height);
        BarRight = new Rect(
            Dialog.X + (DialogWidth / 2),
            barY,
            (DialogWidth / 2) - exitSize.Width + 1,
            barSize.Height);

        BarRightSourceX = barSize.Width - (DialogWidth / 2) + exitSize.Width - 1;
        FillColumn = new Rect(Dialog.Right - 1, barY, 1, barSize.Height);
        TitleRoi = new Rect(Dialog.X, barY, DialogWidth, barSize.Height - 1);
    }

    /// <summary>The window, 640 by 480 and centred on the screen.</summary>
    public Rect Dialog { get; }

    /// <summary>The MAX button, at the left end of the bottom bar.</summary>
    public Rect Max { get; }

    /// <summary>The EXIT button, at the right end of the bottom bar.</summary>
    public Rect Exit { get; }

    /// <summary>Left piece of the bottom bar, drawn from <see cref="BarLeftSourceX"/>.</summary>
    public Rect BarLeft { get; }

    /// <summary>Right piece of the bottom bar, drawn from <see cref="BarRightSourceX"/>.</summary>
    public Rect BarRight { get; }

    /// <summary>Column of the bar the right piece is read from, <c>width / 2</c> short of the end.</summary>
    public int BarRightSourceX { get; }

    /// <summary>The one column of the background the original artwork leaves transparent.</summary>
    public Rect FillColumn { get; }

    /// <summary>Region the title may be drawn in; one row shorter than the bar.</summary>
    public Rect TitleRoi { get; }

    /// <summary>Height of the bottom bar.</summary>
    public int BarHeight { get; }

    /// <summary>Top left corner of the whole bottom bar.</summary>
    public Rect BottomBar => new(Dialog.X, Dialog.Y + BottomBarOffsetY, DialogWidth, BarHeight);

    /// <summary>Top left corner of a cell, <c>offset</c> in <c>castle_well.cpp:406-439</c>.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The cell's origin in window coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not one to six.</exception>
    public Point GetCellOrigin(int level)
    {
        int index = GetIndex(level);

        return new Point(Dialog.X, Dialog.Y + FirstCellOffsetY)
            + new Point(CellStepX * (index / 3), CellStepY * (index % 3));
    }

    /// <summary>Where the dwelling's picture is drawn inside its cell.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The top left corner of the picture.</returns>
    public Point GetPictureOrigin(int level) =>
        GetCellOrigin(level) + new Point(PictureOffsetX, PictureOffsetY);

    /// <summary>The area a click on the dwelling's creature is tested against.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The rectangle in window coordinates.</returns>
    public Rect GetMonsterArea(int level)
    {
        int index = GetIndex(level);

        return new Rect(
            Dialog.X + MonsterAreaOffsetX + (CellStepX * (index / 3)),
            Dialog.Y + MonsterAreaOffsetY + (CellStepY * (index % 3)),
            MonsterAreaWidth,
            MonsterAreaHeight);
    }

    /// <summary>Top left corner of the "Available:" text, before any centring.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The anchor the reference calls <c>offset</c>.</returns>
    public Point GetAvailableOrigin(int level)
    {
        int index = GetIndex(level);

        return new Point(
            Dialog.X + AvailableOffsetX + (CellStepX * (index / 3)),
            Dialog.Y + AvailableOffsetY + (CellStepY * (index % 3)));
    }

    /// <summary>Anchor of the dwelling's name; <see cref="Point.X"/> is the centre of the text.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The centre and the top row of the name.</returns>
    public Point GetDwellingNameAnchor(int level) =>
        GetCellOrigin(level) + new Point(DwellingNameCentreX, DwellingNameOffsetY);

    /// <summary>Anchor of the creature's name; <see cref="Point.X"/> is the centre of the text.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The centre and the top row of the name.</returns>
    public Point GetMonsterNameAnchor(int level) =>
        GetCellOrigin(level) + new Point(MonsterNameCentreX, MonsterNameOffsetY);

    /// <summary>Anchor of the statistics column; <see cref="Point.X"/> is the centre of every line.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The centre and the top row of the first line.</returns>
    public Point GetStatsAnchor(int level) =>
        GetCellOrigin(level) + new Point(StatsCentreX, StatsOffsetY);

    /// <summary>Anchors every half of the text on a centre, as every line of this window is centred.</summary>
    /// <param name="anchor">The centre and the top row.</param>
    /// <param name="width">Width the text will occupy.</param>
    /// <returns>Where to start drawing the text.</returns>
    public static Point Centre(in Point anchor, int width) => new(anchor.X - (width / 2), anchor.Y);

    private static int GetIndex(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, CellCount);

        return level - 1;
    }
}
