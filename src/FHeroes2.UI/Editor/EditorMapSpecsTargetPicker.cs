using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Interface;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The list of candidate towns or heroes a condition panel row opens, a port of <c>SelectMapCastle</c> and
/// <c>SelectMapHero</c> (<c>editor_map_specs_window.cpp:180-330</c>).
/// </summary>
/// <remarks>
/// <para>
/// 两个参考窗都是 <c>Dialog::ItemSelectionWindow</c> 的子类：左键选行、右键看名称、OK 取值、Cancel 保留原值。端口
/// 把共同部分收到这里：行高、行内图标与文字的横向位置、窗口尺寸算法、以及「选中行是哪一格地图」；子类只决定每行
/// 画什么图标、读什么标题。
/// </para>
/// <para>
/// 窗口尺寸照参考：宽 450，高按行数算 <c>100 + 35 * 行数</c>，最少按 5 行、最多按 12 行，再被
/// <c>屏幕高 - 100</c> 压住（<c>:1232-1250</c> 与三处同类代码）。行数超过可见行数时由基类的滚动条负责。
/// </para>
/// <para>
/// 参考的 <c>selectItemsEventProcessing()</c> 是阻塞循环；端口一帧一步，调用方自己循环
/// <see cref="ItemSelectionWindow.ProcessEvents"/>：<see cref="ItemSelectionWindow.Result"/> 成为
/// <see cref="DialogButton.Ok"/> 时用 <see cref="SelectedPosition"/> 写回目标，
/// <see cref="DialogButton.Cancel"/> 时什么都不做。
/// </para>
/// </remarks>
public abstract class EditorMapSpecsTargetPicker : ItemSelectionWindow
{
    /// <summary>Height of a row, <c>itemsOffsetY = 35</c>.</summary>
    public const int RowHeight = 35;

    /// <summary>Vertical middle of a row, <c>itemsOffsetY / 2</c>.</summary>
    public const int RowMiddleOffsetY = RowHeight / 2;

    /// <summary>Room kept free on the right of a row for the scroll bar, <c>- 55</c> of the reference.</summary>
    public const int TextRightMargin = 55;

    /// <summary>Width of the window, <c>{ 450, totalHeight }</c>.</summary>
    public const int DialogWidth = 450;

    /// <summary>Height the window chrome (title, buttons) takes, the <c>100</c> of the reference's formula.</summary>
    public const int ListChromeHeight = 100;

    /// <summary>Most rows the window is ever sized for, the <c>35 * 12</c> of the reference.</summary>
    public const int MaxSizedRows = 12;

    /// <summary>Rows the window is always sized for, even when there are fewer, the <c>35 * 5</c> of the reference.</summary>
    public const int MinSizedRows = 5;

    /// <summary>Room kept free at the top and the bottom of the screen, <c>display.height() - 100</c>.</summary>
    public const int ScreenMargin = 100;

    /// <summary>Title of the town picker of the capture-town victory condition, <c>:1246</c>.</summary>
    public const string VictoryTownTitle = "Select a Town to capture to achieve victory";

    /// <summary>Title of the hero picker of the defeat-hero victory condition, <c>:1300</c>.</summary>
    public const string VictoryHeroTitle = "Select a Hero to defeat to achieve victory";

    /// <summary>Title of the town picker of the lose-town loss condition, <c>:1782</c>.</summary>
    public const string LossTownTitle = "Select a Town to lose to achieve defeat";

    /// <summary>Title of the hero picker of the lose-hero loss condition, <c>:1834</c>.</summary>
    public const string LossHeroTitle = "Select a Hero to lose to achieve defeat";

    private readonly List<ItemSelectionEntry> _rows;
    private readonly int _mapWidth;

    /// <summary>Lays out the window and fills the list, the constructors of <c>SelectMapCastle</c> / <c>SelectMapHero</c>.</summary>
    /// <param name="dialogSize">Size of the active area, normally <see cref="GetDialogSize"/>.</param>
    /// <param name="title">Heading, one of the four titles of this class.</param>
    /// <param name="mapWidth">Width of the map, which turns a row id into a tile.</param>
    /// <param name="rows">The candidates, one row each; the row id is the tile index of the candidate.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rows"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or <paramref name="font"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    protected EditorMapSpecsTargetPicker(Size dialogSize, string title, int mapWidth, List<ItemSelectionEntry> rows,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font, Size? screen = null)
        : base(dialogSize, title, string.Empty, artwork, titleFont, font, screen)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);

        _rows = rows;
        _mapWidth = mapWidth;

        SetAreaMaxItems(ItemsArea.Height / RowHeight);
        SetListContent(_rows);
        UpdateScrollBarImage();
    }

    /// <summary>Width of the map the rows point at, <c>mapWidth</c> of the reference's callers.</summary>
    public int MapWidth => _mapWidth;

    /// <summary>The rows, in the order the caller gave them, <c>SetListContent()</c> of the reference.</summary>
    public IReadOnlyList<ItemSelectionEntry> Candidates => _rows;

    /// <summary>
    /// The tile index of the selected row, or -1 when the list is empty; the <c>GetCurrent()</c> the reference reads
    /// after its loop.
    /// </summary>
    public int SelectedTile => IsValid ? GetCurrent().Id : -1;

    /// <summary>
    /// The tile of the selected row, or <see langword="null"/> when the list is empty; what the caller hands to
    /// <see cref="EditorMapSpecsInput.ResolveTarget"/>.
    /// </summary>
    public Point? SelectedPosition => SelectedTile < 0
        ? null
        : new Point(SelectedTile % _mapWidth, SelectedTile / _mapWidth);

    /// <summary>
    /// Moves the selection onto a tile, the reference's <c>SetCurrent( rowNumber )</c> after the list was filled; a tile
    /// that is not in the list leaves the selection alone.
    /// </summary>
    /// <param name="tile">The tile the candidate stands on.</param>
    public void SelectTile(Point tile)
    {
        int tileIndex = (tile.Y * _mapWidth) + tile.X;

        for (int index = 0; index < _rows.Count; ++index)
        {
            if (_rows[index].Id != tileIndex)
            {
                continue;
            }

            SetCurrent(index);
            UpdateScrollBarImage();

            return;
        }
    }

    /// <summary>
    /// Size of the active area for <paramref name="rowCount"/> rows, <c>:1232-1250</c> of the reference and its three
    /// siblings.
    /// </summary>
    /// <param name="rowCount">Number of candidates.</param>
    /// <param name="screen">Screen the window has to fit in, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The size to hand to the constructor.</returns>
    public static Size GetDialogSize(int rowCount, Size? screen = null)
    {
        Size display = screen ?? ScreenBuffer.DefaultSize;

        int maxHeight = Math.Min(ListChromeHeight + (RowHeight * MaxSizedRows), display.Height - ScreenMargin);
        int listHeight = ListChromeHeight + (RowHeight * Math.Max(rowCount, MinSizedRows));

        return new Size(DialogWidth, Math.Min(listHeight, maxHeight));
    }

    /// <summary>
    /// Builds the picker of <paramref name="target"/>, the window the reference opens right inside its event loop.
    /// </summary>
    /// <remarks>
    /// 参考在四个分支里各写一段开窗代码（<c>:1230-1264</c>、<c>:1289-1318</c>、<c>:1770-1803</c>、<c>:1822-1855</c>），
    /// 这里合成一个入口：宿主拿到 <see cref="EditorMapSpecsRequestKind.PickConditionTarget"/> 后按
    /// <see cref="EditorMapSpecsRequest.Target"/> 造窗，选完把 <see cref="SelectedPosition"/> 交回
    /// <see cref="EditorMapSpecsInput.ResolveTarget"/>。
    /// </remarks>
    /// <param name="target">What the panel asked for.</param>
    /// <param name="state">The specifications being edited.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>
    /// The window with the current target selected, or <see langword="null"/> for
    /// <see cref="EditorMapSpecsTargetKind.None"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or <paramref name="font"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static EditorMapSpecsTargetPicker? Create(EditorMapSpecsTargetKind target, MapSpecsState state,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font, bool evilInterface = false,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(font);

        int mapWidth = state.Map.Info.Width;

        switch (target)
        {
            case EditorMapSpecsTargetKind.VictoryTown:
                return new EditorMapSpecsTownPicker(
                    MapSpecsPanelViews.CreateTownRows(state), mapWidth, state.VictoryTarget, VictoryTownTitle,
                    artwork, titleFont, font, evilInterface, screen);

            case EditorMapSpecsTargetKind.LossTown:
                return new EditorMapSpecsTownPicker(
                    MapSpecsPanelViews.CreateTownRows(state), mapWidth, state.LossTarget, LossTownTitle,
                    artwork, titleFont, font, evilInterface, screen);

            case EditorMapSpecsTargetKind.VictoryHero:
                return new EditorMapSpecsHeroPicker(
                    MapSpecsPanelViews.CreateHeroRows(state.VictoryHeroes, mapWidth), mapWidth, state.VictoryTarget,
                    VictoryHeroTitle, artwork, titleFont, font, evilInterface, screen);

            case EditorMapSpecsTargetKind.LossHero:
                return new EditorMapSpecsHeroPicker(
                    MapSpecsPanelViews.CreateHeroRows(state.LossHeroes, mapWidth), mapWidth, state.LossTarget,
                    LossHeroTitle, artwork, titleFont, font, evilInterface, screen);

            default:
                return null;
        }
    }

    /// <summary>
    /// Paints one row: the icon centred at <paramref name="iconOffsetX"/> and the title starting at
    /// <paramref name="textOffsetX"/>, the reference's
    /// <c>renderItem( icon, title, { dstx, dsty }, iconOffsetX, textOffsetX, itemsOffsetY / 2, current )</c>
    /// (<c>dialog_selectitems.cpp:978-1010</c>).
    /// </summary>
    /// <param name="icon">The icon of the candidate, or <see langword="null"/> when it could not be composed.</param>
    /// <param name="title">The title the row shows.</param>
    /// <param name="iconOffsetX">Horizontal middle of the icon inside the row.</param>
    /// <param name="textOffsetX">Left edge of the title inside the row.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    protected void RenderRow(Sprite? icon, string title, int iconOffsetX, int textOffsetX, int x, int y, bool current,
        PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (icon is not null)
        {
            ImageOps.BlitSprite(
                output, new Point(x + iconOffsetX - (icon.Image.Width / 2), y + RowMiddleOffsetY - (icon.Image.Height / 2)),
                icon);
        }

        IGlyphSource font = current ? HighlightFont : Font;
        Rect area = Window.ActiveArea;
        int maxWidth = area.Width - textOffsetX - TextRightMargin;

        string fitted = BuildDialogView.FitToOneRow(font, title, maxWidth);
        int lineY = y - (font.LineHeight / 2) + RowMiddleOffsetY + 2;

        GlyphText.DrawLine(
            font, output, new Point(x + textOffsetX, lineY), fitted,
            new Rect(x + textOffsetX, area.Y, maxWidth, area.Height));
    }
}
