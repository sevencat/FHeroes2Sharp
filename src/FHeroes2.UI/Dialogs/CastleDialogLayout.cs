using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The fixed coordinates of the castle screen, <c>Castle::OpenDialog</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/castle/castle_dialog.cpp:325-384</c>。城堡屏是整屏 640×480：底部 19 像素是状态栏
/// （<c>ICN::SMALLBAR</c> 三段：上一个城堡、状态条、下一个城堡），左上角是城堡旗帜与英雄像，中间两排军队条，
/// 右下角是资源面板与 EXIT，其余是建筑区（建筑坐标由 <c>CastleBuildingInfo</c> 给出，不在这一层）。
/// </para>
/// <para>
/// 这一层只管「东西放在哪」，不管画什么，也不猜素材尺寸：状态栏与旗帜的矩形由调用方把帧尺寸传进来算，
/// 军队条只给原点（格子与间距由 `ArmyBar` 自己摆），EXIT 只给位置（尺寸取自它的帧）。这样素材缺失时矩形
/// 仍然可算，测试也只需要断言矩形。
/// </para>
/// <para>
/// 未搬：建筑区的命中表（要 <c>BuildingsRenderQueue</c> 的 z 序与 <c>areas</c>）、状态栏的文字滚动
/// （<c>StatusBar</c>），后者随冒险界面外壳一起做。
/// </para>
/// </remarks>
public sealed class CastleDialogLayout
{
    /// <summary>The height of the bottom bar, <c>480 - 19</c>.</summary>
    public const int StatusBarHeight = 19;

    /// <summary>Columns of each army bar, <c>setTableSize( { 5, 1 } )</c>.</summary>
    public const int ArmyBarColumns = 5;

    /// <summary>Gap between two troop slots, <c>setInBetweenItemsOffset( { 6, 0 } )</c>.</summary>
    public const int ArmyBarItemSpacing = 6;

    /// <summary>Inset of the status text from both ends of the bar, <c>+16</c>.</summary>
    public const int StatusTextInset = 16;

    private const int CrestOffsetX = 5;
    private const int CrestOffsetY = 262;
    private const int HeroPortraitOffsetX = 5;
    private const int HeroPortraitOffsetY = 361;
    private const int HeroPortraitWidth = 100;
    private const int HeroPortraitHeight = 92;
    private const int ArmyBarOffsetX = 112;
    private const int CastleArmyOffsetY = 262;
    private const int HeroArmyOffsetY = 361;
    private const int ExitOffsetX = 553;
    private const int ExitOffsetY = 428;
    private const int TownNameCentreX = 320;
    private const int TownNameOffsetY = 248;
    private const int ResourceHitInset = 3;

    private CastleDialogLayout(Point offset, Size statusBar, Size castleButton, Size crest)
    {
        Offset = offset;
        CrestSize = crest;

        int barTop = offset.Y + ScreenSize.Height - StatusBarHeight;

        // 参考 :330-345：两个箭头各占自己那一帧的宽，状态条从箭头右侧开始画，下一个箭头接在状态条右端。
        // 三段的宽度不同（状态条比箭头宽得多），所以这里不能拿状态条的宽度去当箭头的宽度。
        PreviousCastleButton = new Rect(offset.X, barTop, castleButton.Width, castleButton.Height);
        StatusBarArea = new Rect(PreviousCastleButton.Right, barTop, statusBar.Width, statusBar.Height);
        NextCastleButton = new Rect(StatusBarArea.Right, barTop, castleButton.Width, castleButton.Height);
        StatusTextArea = new Rect(
            StatusBarArea.X + StatusTextInset,
            barTop,
            statusBar.Width - (StatusTextInset * 2),
            statusBar.Height);

        CrestArea = new Rect(
            offset.X + CrestOffsetX,
            offset.Y + CrestOffsetY,
            crest.Width,
            crest.Height);

        HeroPortraitArea = new Rect(
            offset.X + HeroPortraitOffsetX,
            offset.Y + HeroPortraitOffsetY,
            HeroPortraitWidth,
            HeroPortraitHeight);

        CastleArmyBarOrigin = new Point(offset.X + ArmyBarOffsetX, offset.Y + CastleArmyOffsetY);
        HeroArmyBarOrigin = new Point(offset.X + ArmyBarOffsetX, offset.Y + HeroArmyOffsetY);
        ExitButtonPosition = new Point(offset.X + ExitOffsetX, offset.Y + ExitOffsetY);

        Rect panel = Widgets.ResourcePanel.GetArea(offset);
        ResourcePanelArea = panel;

        // 参考 :383：命中区从面板顶到 EXIT 上沿再上收 3 像素，EXIT 自己单独接点击。
        ResourceHitArea = new Rect(
            panel.X,
            panel.Y,
            panel.Width,
            ExitButtonPosition.Y - panel.Y - ResourceHitInset);
    }

    /// <summary>The size of a castle screen, <c>640 × 480</c>.</summary>
    public static Size ScreenSize { get; } = new(640, 480);

    /// <summary>Origin of the castle screen.</summary>
    public Point Offset { get; }

    /// <summary>Size of the <c>ICN::CREST</c> frame, which decides the size of <see cref="CrestArea"/>.</summary>
    public Size CrestSize { get; }

    /// <summary>The "previous castle" button in the bottom left corner, <c>SMALLBAR</c> frames 1/2.</summary>
    public Rect PreviousCastleButton { get; }

    /// <summary>The status bar itself, <c>SMALLBAR</c> frame 0.</summary>
    public Rect StatusBarArea { get; }

    /// <summary>The "next castle" button in the bottom right corner, <c>SMALLBAR</c> frames 3/4.</summary>
    public Rect NextCastleButton { get; }

    /// <summary>Where the status text scrolls, inset by <see cref="StatusTextInset"/> from both ends.</summary>
    public Rect StatusTextArea { get; }

    /// <summary>The castle's flag, <c>ICN::CREST</c> frame of the castle's colour.</summary>
    public Rect CrestArea { get; }

    /// <summary>The hero's portrait, <c>( 5, 361, 100, 92 )</c>.</summary>
    public Rect HeroPortraitArea { get; }

    /// <summary>Top-left corner of the upper army bar, the castle's garrison, at <c>( 112, 262 )</c>.</summary>
    public Point CastleArmyBarOrigin { get; }

    /// <summary>Top-left corner of the lower army bar, the visiting hero's army, at <c>( 112, 361 )</c>.</summary>
    public Point HeroArmyBarOrigin { get; }

    /// <summary>The kingdom's resources, <c>drawResourcePanel</c>'s rectangle.</summary>
    public Rect ResourcePanelArea { get; }

    /// <summary>The part of the resource panel that reacts to the mouse, EXIT excluded.</summary>
    public Rect ResourceHitArea { get; }

    /// <summary>Top-left corner of the EXIT button, <c>( 553, 428 )</c>.</summary>
    public Point ExitButtonPosition { get; }

    /// <summary>
    /// Builds the layout of one castle screen.
    /// </summary>
    /// <param name="offset">Origin of the screen; the reference passes the display's area.</param>
    /// <param name="statusBar">Size of <c>SMALLBAR</c> frame 0, the bar between the two arrows.</param>
    /// <param name="crest">Size of a <c>ICN::CREST</c> frame.</param>
    /// <param name="castleButton">
    /// Size of <c>SMALLBAR</c> frames 1/3, the two arrows. Defaults to <paramref name="statusBar"/>, which is
    /// only right for square stand-ins; with the original frames the arrows are much narrower than the bar.
    /// </param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The bar is too narrow for the inset, or a size is empty.</exception>
    public static CastleDialogLayout Create(Point offset, Size statusBar, Size crest, Size? castleButton = null)
    {
        if (statusBar.Width <= StatusTextInset * 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusBar),
                statusBar,
                $"The status bar must be wider than {StatusTextInset * 2} pixels.");
        }

        if (crest.Width <= 0 || crest.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(crest), crest, "The crest must have a positive size.");
        }

        Size button = castleButton ?? statusBar;
        if (button.Width <= 0 || button.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(castleButton), button, "The arrow must have a positive size.");
        }

        return new CastleDialogLayout(offset, statusBar, button, crest);
    }

    /// <summary>
    /// Where the town name plate goes, <c>drawCastleName</c> (<c>ui_castle.cpp:303-307</c>).
    /// </summary>
    /// <param name="plate">The <c>ICN::TOWNNAME</c> frame.</param>
    /// <returns>The top-left corner of the plate.</returns>
    public Point GetTownNamePosition(Sprite plate)
    {
        ArgumentNullException.ThrowIfNull(plate);

        return new Point(Offset.X + TownNameCentreX - (plate.Width / 2), Offset.Y + TownNameOffsetY);
    }

    /// <summary>
    /// Where the EXIT button goes, once its frame is known, <c>castle_dialog.cpp:379</c>.
    /// </summary>
    /// <param name="exitFrame">
    /// The released EXIT button frame; its size is the button's size. With the original resources this is
    /// <c>TREASURY</c> frame 1, which is 80×25 (<c>game_assets.cpp:918-926</c>).
    /// </param>
    /// <returns>The button's rectangle.</returns>
    public Rect GetExitButtonArea(Sprite exitFrame)
    {
        ArgumentNullException.ThrowIfNull(exitFrame);

        return new Rect(ExitButtonPosition.X, ExitButtonPosition.Y, exitFrame.Width, exitFrame.Height);
    }
}
