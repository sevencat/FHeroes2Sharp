using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where everything on the thieves' guild report goes, the offsets of
/// <c>Dialog::ThievesGuild()</c> (<c>dialog_thievesguild.cpp:285-443</c>).
/// </summary>
/// <remarks>
/// <para>
/// The report is the whole 640 by 480 screen and has no frame: the stone plate is drawn at the dialog's corner and
/// every anchor is "corner plus a constant". Ten rows of labels step 24 pixels down from row 31; the flags of a row
/// hang from the same row, four pixels higher. The colour names, the best hero, its statistics, the personalities
/// and the best monsters follow at their own, uneven distances: the reference simply keeps adding to one
/// <c>offset</c> variable, and those sums are the constants below.
/// </para>
/// <para>
/// Columns are 68 pixels apart, starting at x 264. Some rows centre on the column (flags, colour names, hero
/// portraits, personalities, monsters), the statistics and the labels are left aligned.
/// </para>
/// </remarks>
public sealed class ThievesGuildLayout
{
    /// <summary>Width of the report, <c>fheroes2::Display::DEFAULT_WIDTH</c>.</summary>
    public const int DialogWidth = 640;

    /// <summary>Height of the report, <c>fheroes2::Display::DEFAULT_HEIGHT</c>.</summary>
    public const int DialogHeight = 480;

    /// <summary>Right edge the row labels are aligned to, <c>dialogRoi.x + 207</c>.</summary>
    public const int TextOffsetX = 207;

    /// <summary>Left edge of the first column, <c>dialogRoi.x + 264</c>.</summary>
    public const int StartOffsetX = 264;

    /// <summary>Horizontal distance between two columns, <c>68</c>.</summary>
    public const int StepX = 68;

    /// <summary>Row the "1st" … "6th" ordinals sit on, <c>dialogRoi.y + 3</c>.</summary>
    public const int OrdinalOffsetY = 3;

    /// <summary>Row of the first label, <c>dialogRoi.y + 31</c>.</summary>
    public const int FirstRowOffsetY = 31;

    /// <summary>Vertical distance between two labels, <c>24</c>.</summary>
    public const int RowStepY = 24;

    /// <summary>How many ranking rows the report prints, <c>10</c>.</summary>
    public const int RowCount = 10;

    /// <summary>Top row of the status bar, <c>461</c>.</summary>
    public const int BottomBarOffsetY = 461;

    /// <summary>Row the title is drawn on, <c>464</c>.</summary>
    public const int TitleOffsetY = 464;

    /// <summary>Row the colour names start on, <c>FirstRowOffsetY + RowStepY * RowCount</c>.</summary>
    public const int ColorNameOffsetY = FirstRowOffsetY + (RowStepY * RowCount);

    /// <summary>Every other colour name is pushed down by this much, <c>15</c>.</summary>
    public const int ColorNameShiftY = 15;

    /// <summary>Row of the "Best Hero:" label, <c>ColorNameOffsetY + 38</c>.</summary>
    public const int BestHeroOffsetY = ColorNameOffsetY + 38;

    /// <summary>Row the hero portraits are anchored on, <c>BestHeroOffsetY - 4</c>.</summary>
    public const int HeroIconOffsetY = BestHeroOffsetY - 4;

    /// <summary>Portraits are centred one pixel right of the column, <c>pos.x + 1</c>.</summary>
    public const int HeroIconOffsetX = 1;

    /// <summary>Statistics start 26 pixels left of the portrait's centre.</summary>
    public const int HeroStatsOffsetX = -26;

    /// <summary>Row the statistics start on, <c>HeroIconOffsetY + 34</c>.</summary>
    public const int HeroStatsOffsetY = HeroIconOffsetY + 34;

    /// <summary>Distance between the four statistics lines, <c>11</c>.</summary>
    public const int HeroStatsLineStepY = 11;

    /// <summary>Column the statistic numbers are right aligned to, <c>offsetX + 50</c>.</summary>
    public const int HeroStatsValueOffsetX = 50;

    /// <summary>Row of the "Best Hero Stats:" label, <c>HeroIconOffsetY + 47</c>.</summary>
    public const int BestHeroStatsOffsetY = HeroIconOffsetY + 47;

    /// <summary>Row of the "Personality:" label, <c>BestHeroStatsOffsetY + 42</c>.</summary>
    public const int PersonalityLabelOffsetY = BestHeroStatsOffsetY + 42;

    /// <summary>Row the personalities are drawn on, <c>PersonalityLabelOffsetY + 3</c>.</summary>
    public const int PersonalityOffsetY = PersonalityLabelOffsetY + 3;

    /// <summary>Row of the "Best Monster:" label, <c>PersonalityOffsetY + 32</c>.</summary>
    public const int BestMonsterOffsetY = PersonalityOffsetY + 32;

    /// <summary>Column the left piece of the bar is read from, <c>1</c>.</summary>
    public const int BarLeftSourceX = 1;

    private readonly int _normalLineHeight;

    /// <summary>Places the report in the middle of <paramref name="screen"/> and lays out its parts.</summary>
    /// <param name="screen">The screen the report is centred on.</param>
    /// <param name="barSize">Size of <c>SMALLBAR</c>, i.e. the height of the bar.</param>
    /// <param name="exitSize">Size of the EXIT button.</param>
    /// <param name="normalLineHeight">Line height of <c>normalWhite</c>, which centres the monster icons.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size or the line height is not positive.</exception>
    public ThievesGuildLayout(in Rect screen, in Size barSize, in Size exitSize, int normalLineHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(barSize.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exitSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(normalLineHeight);

        int left = screen.X + ((screen.Width - DialogWidth) / 2);
        int top = screen.Y + ((screen.Height - DialogHeight) / 2);
        Dialog = new Rect(left, top, DialogWidth, DialogHeight);

        _normalLineHeight = normalLineHeight;
        BarHeight = barSize.Height;

        int barY = Dialog.Y + BottomBarOffsetY;
        Exit = new Rect(Dialog.Right - exitSize.Width, barY, exitSize.Width, exitSize.Height);

        // The bar is cut in two halves; the right one overlaps the left one by the column the exit button takes.
        BarLeft = new Rect(Dialog.X, barY, DialogWidth / 2, barSize.Height);
        BarRight = new Rect(
            Dialog.X + (DialogWidth / 2),
            barY,
            (DialogWidth / 2) - exitSize.Width + 1,
            barSize.Height);

        BarRightSourceX = barSize.Width - (DialogWidth / 2) + exitSize.Width - 1;
    }

    /// <summary>The report, 640 by 480 and centred on the screen.</summary>
    public Rect Dialog { get; }

    /// <summary>The EXIT button, at the right end of the bottom bar.</summary>
    public Rect Exit { get; }

    /// <summary>Left piece of the bottom bar, drawn from <see cref="BarLeftSourceX"/>.</summary>
    public Rect BarLeft { get; }

    /// <summary>Right piece of the bottom bar, drawn from <see cref="BarRightSourceX"/>.</summary>
    public Rect BarRight { get; }

    /// <summary>Column of the bar the right piece is read from.</summary>
    public int BarRightSourceX { get; }

    /// <summary>Height of the bottom bar.</summary>
    public int BarHeight { get; }

    /// <summary>Row of a ranking label, <see cref="FirstRowOffsetY"/> plus 24 per row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The row's y coordinate.</returns>
    public int RowLabelY(ThievesGuildRow row) => Dialog.Y + FirstRowOffsetY + (RowStepY * (int)row);

    /// <summary>Where a row label starts, right aligned to <see cref="TextOffsetX"/>.</summary>
    /// <param name="row">The row.</param>
    /// <param name="textWidth">Width the label will occupy.</param>
    /// <returns>The top left corner of the label.</returns>
    public Point RowLabelAnchor(ThievesGuildRow row, int textWidth) =>
        new(Dialog.X + TextOffsetX - textWidth, RowLabelY(row));

    /// <summary>Where the flags of a row are centred; the first column's centre is <c>startOffsetX</c>.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The anchor the reference's <c>drawFlags()</c> takes.</returns>
    public Point FlagsAnchor(ThievesGuildRow row) => new(Dialog.X + StartOffsetX, RowLabelY(row));

    /// <summary>Centre of a column's ordinal.</summary>
    /// <param name="column">Zero based column.</param>
    /// <returns>The centre and the top row of the text.</returns>
    public Point OrdinalCentre(int column) =>
        new(Dialog.X + StartOffsetX + (StepX * column), Dialog.Y + OrdinalOffsetY);

    /// <summary>Centre of a colour's name; every other one is pushed fifteen pixels down.</summary>
    /// <param name="column">Zero based column.</param>
    /// <returns>The centre and the top row of the text.</returns>
    public Point ColorNameCentre(int column) => new(
        Dialog.X + StartOffsetX + (StepX * column),
        Dialog.Y + ColorNameOffsetY + (((column & 1) == 0) ? 0 : ColorNameShiftY));

    /// <summary>Centre of a column's hero portrait, one pixel right of the column.</summary>
    /// <param name="column">Zero based column.</param>
    /// <returns>The centre of the portrait frame.</returns>
    public Point HeroIconCentre(int column) => new(
        Dialog.X + StartOffsetX + HeroIconOffsetX + (StepX * column),
        Dialog.Y + HeroIconOffsetY);

    /// <summary>Where a column's four statistics start.</summary>
    /// <param name="column">Zero based column.</param>
    /// <returns>The top left corner of the first statistic line.</returns>
    public Point HeroStatsAnchor(int column) =>
        new(HeroIconCentre(column).X + HeroStatsOffsetX, Dialog.Y + HeroStatsOffsetY);

    /// <summary>Centre of a column's personality word.</summary>
    /// <param name="column">Zero based column.</param>
    /// <returns>The centre and the top row of the text.</returns>
    public Point PersonalityCentre(int column) =>
        new(Dialog.X + StartOffsetX + (StepX * column), Dialog.Y + PersonalityOffsetY);

    /// <summary>Centre of a column's strongest monster, half a normal line below the label.</summary>
    /// <param name="column">Zero based column.</param>
    /// <returns>The centre the icon is drawn around.</returns>
    public Point MonsterCentre(int column) => new(
        Dialog.X + StartOffsetX + (StepX * column),
        Dialog.Y + BestMonsterOffsetY + (_normalLineHeight / 2));

    /// <summary>Where the title starts on the status bar, centred in the room the exit button leaves.</summary>
    /// <param name="textWidth">Width the title will occupy.</param>
    /// <returns>The top left corner of the title.</returns>
    public Point TitleAnchor(int textWidth) => new(
        Dialog.X + ((DialogWidth - Exit.Width - textWidth) / 2),
        Dialog.Y + TitleOffsetY);

    /// <summary>Anchors the left of a text on a centre.</summary>
    /// <param name="centre">The centre and the top row.</param>
    /// <param name="width">Width the text will occupy.</param>
    /// <returns>Where to start drawing the text.</returns>
    public static Point Centre(in Point centre, int width) => new(centre.X - (width / 2), centre.Y);
}
