using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where everything in the mage guild window goes (<c>castle_mageguild.cpp:63-131</c>).
/// </summary>
/// <remarks>
/// <para>
/// Three things share the bottom strip: the previous town arrow at the far left, the exit button at the far right and
/// the status bar between them. The bar is narrower than the gap because the arrows and the button have their own
/// art, which is why <see cref="Bar"/> stops short of the button's left edge and starts at the arrow's right one.
/// </para>
/// <para>
/// The bar is drawn as two copies of <c>SMALLBAR</c> frame 0 rather than stretched: the left copy from source column
/// 1 — the archive's first column is all black and must not reach the screen — and the right copy from the archive's
/// right edge, so the plate's rounded ends survive. <see cref="BarSourceRightX"/> is that second source column.
/// </para>
/// </remarks>
public sealed class MageGuildLayout
{
    /// <summary>Width of the window, <c>Display::DEFAULT_WIDTH</c>.</summary>
    public const int DialogWidth = 640;

    /// <summary>Height of the window, <c>Display::DEFAULT_HEIGHT</c>.</summary>
    public const int DialogHeight = 480;

    /// <summary>Distance from the top of the window to the bottom strip, <c>bottomBarOffsetY</c>.</summary>
    public const int BottomBarOffsetY = 461;

    /// <summary>Where the status bar's text may start, <c>bottomUIOffset.x + 16</c>.</summary>
    public const int StatusBarInsetX = 16;

    /// <summary>
    /// Width of the column the guild's picture is confined to, the <c>200</c> of the reference's clip rectangle.
    /// </summary>
    public const int GuildColumnWidth = 200;

    /// <summary>
    /// Row the guild's picture stands on, the <c>offset.y + 290</c> of <c>renderMageGuildBuilding</c>.
    /// </summary>
    public const int GuildBaselineOffsetY = 290;

    private MageGuildLayout(
        Rect dialog,
        Point bottomUi,
        Rect previousTown,
        Rect nextTown,
        Rect exit,
        Rect bar,
        Rect statusBar,
        int barLeftWidth,
        int barRightWidth,
        int barSourceRightX,
        bool townsEnabled)
    {
        Dialog = dialog;
        BottomUi = bottomUi;
        PreviousTown = previousTown;
        NextTown = nextTown;
        Exit = exit;
        Bar = bar;
        StatusBar = statusBar;
        BarLeftWidth = barLeftWidth;
        BarRightWidth = barRightWidth;
        BarSourceRightX = barSourceRightX;
        TownsEnabled = townsEnabled;
    }

    /// <summary>The 640 by 480 window, centred on the display.</summary>
    public Rect Dialog { get; }

    /// <summary>Top left of the bottom strip, <c>bottomUIOffset</c>.</summary>
    public Point BottomUi { get; }

    /// <summary>The arrow to the previous town, at the left end of the strip.</summary>
    public Rect PreviousTown { get; }

    /// <summary>The arrow to the next town, just left of the exit button.</summary>
    public Rect NextTown { get; }

    /// <summary>The exit button, at the right end of the strip.</summary>
    public Rect Exit { get; }

    /// <summary>The status bar's plate, between the two arrows.</summary>
    public Rect Bar { get; }

    /// <summary>
    /// Where the status bar's text is confined, <c>statusBar.setRoi( { x + 16, y, width, 0 } )</c>; the height is
    /// zero because the reference lets the bar derive it.
    /// </summary>
    public Rect StatusBar { get; }

    /// <summary>Width of the left copy of the plate, <c>bottomBarWidth / 2</c>.</summary>
    public int BarLeftWidth { get; }

    /// <summary>Width of the right copy, <c>bottomBarWidth - bottomBarWidth / 2</c> — a pixel wider when odd.</summary>
    public int BarRightWidth { get; }

    /// <summary>
    /// Source column of the right copy, <c>bottomBar.width() - ( bottomBarWidth - bottomBarWidth / 2 )</c>.
    /// </summary>
    public int BarSourceRightX { get; }

    /// <summary>Whether the two arrows work, <c>GetKingdom().GetCastles().size() &gt;= 2</c>.</summary>
    public bool TownsEnabled { get; }

    /// <summary>
    /// Lays the window out.
    /// </summary>
    /// <param name="screen">The whole display.</param>
    /// <param name="barSize">Size of <c>SMALLBAR</c> frame 0.</param>
    /// <param name="exitSize">Size of <c>BUTTON_GUILDWELL_EXIT</c> frame 0.</param>
    /// <param name="previousSize">Size of the previous arrow, <c>SMALLBAR</c> frame 1.</param>
    /// <param name="nextSize">Size of the next arrow, <c>SMALLBAR</c> frame 3.</param>
    /// <param name="townsEnabled">Whether the arrows are enabled.</param>
    /// <returns>The layout.</returns>
    public static MageGuildLayout Create(
        in Rect screen,
        in Size barSize,
        in Size exitSize,
        in Size previousSize,
        in Size nextSize,
        bool townsEnabled)
    {
        Point dialogPosition = new(
            (screen.Width - DialogWidth) / 2,
            (screen.Height - DialogHeight) / 2);

        Point bottomUi = new(dialogPosition.X, dialogPosition.Y + BottomBarOffsetY);

        Rect exit = new(
            bottomUi.X + DialogWidth - exitSize.Width,
            bottomUi.Y,
            exitSize.Width,
            exitSize.Height);

        Rect nextTown = new(
            exit.X - nextSize.Width,
            bottomUi.Y,
            nextSize.Width,
            nextSize.Height);

        Rect previousTown = new(bottomUi.X, bottomUi.Y, previousSize.Width, previousSize.Height);

        int barWidth = DialogWidth - exitSize.Width - nextSize.Width - previousSize.Width;
        Rect bar = new(previousTown.Right, bottomUi.Y, barWidth, barSize.Height);

        int leftWidth = barWidth / 2;
        int rightWidth = barWidth - leftWidth;

        return new MageGuildLayout(
            new Rect(dialogPosition.X, dialogPosition.Y, DialogWidth, DialogHeight),
            bottomUi,
            previousTown,
            nextTown,
            exit,
            bar,
            new Rect(bottomUi.X + StatusBarInsetX, bottomUi.Y, barWidth, 0),
            leftWidth,
            rightWidth,
            barSize.Width - rightWidth,
            townsEnabled);
    }

    /// <summary>
    /// Top left of a spell row, <c>{ dialogOffset.x + 250, dialogOffset.y + 365 - 90 * levelIndex }</c>.
    /// </summary>
    /// <param name="levelIndex">Zero based spell level; the first level is the bottom row.</param>
    /// <returns>The row's anchor.</returns>
    public Point GetRowAnchor(int levelIndex) => new(
        Dialog.X + MageGuildDialog.RowsLeftOffset,
        Dialog.Y + MageGuildDialog.GetRowTopOffset(levelIndex));

}
