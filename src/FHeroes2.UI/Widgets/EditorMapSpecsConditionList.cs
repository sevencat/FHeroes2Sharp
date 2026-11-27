using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The drop-down list of the map specifications window, a port of <c>DropBoxList</c>
/// (<c>src/fheroes2/editor/editor_map_specs_window.cpp:625-751</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这一族下拉框在参考里是 <c>Interface::ListBox&lt;uint8_t&gt;</c> 的子类：列表项就是条件编号，边框由
/// <c>DROPLISL</c>（或它的 evil 版）的 0 号帧拼出来——上段原样、中段反复贴、下段再补一次，右侧再贴一列 1 像素宽的
/// 同一张图把边封住。参考直接画在显示缓冲上并用 <c>ImageRestorer</c> 记住底图；端口把整块拼好的底图留在
/// <see cref="_background"/> 里，<see cref="RedrawBackground"/> 时整块贴出去，效果相同。
/// </para>
/// <para>
/// <b>为什么行的画法由调用方给。</b>参考的 <c>RedrawItem</c> 转到页面里的 <c>redrawVictoryCondition</c> /
/// <c>redrawLossCondition</c>——那里才知道某个条件要不要带图标（收集金币带金币、找神器带神器）。控件只负责几何与
/// 选中，交给一个回调画行，页面在 <c>a7l.3.5b</c> 里接上。
/// </para>
/// <para>
/// <b>点击的语义。</b><c>ActionListSingleClick</c> 与 <c>ActionListDoubleClick</c> 都把 <see cref="IsClicked"/> 置真：
/// 参考靠它告诉窗口「用户选完了，可以关掉下拉框了」，两种点击没有区别。选中项用 <see cref="CurrentId"/> 取，
/// 而右键不选，只把条件说明抛给宿主（参考在这里弹 <c>showStandardTextMessage</c>）。
/// </para>
/// </remarks>
public sealed class EditorMapSpecsConditionList : ListBox<EditorMapSpecsConditionList.ConditionRow>
{
    /// <summary>One row: the condition index the window works with.</summary>
    public sealed class ConditionRow
    {
        /// <summary>Creates a row.</summary>
        /// <param name="value">Condition index, <c>uint8_t</c> in the reference.</param>
        public ConditionRow(int value) => Value = value;

        /// <summary>The condition index.</summary>
        public int Value { get; }
    }

    /// <summary>
    /// Height of a row, <c>_itemHeight</c> of the reference: the normal font height plus one pixel of gap
    /// (<c>editor_map_specs_window.cpp:748</c>).
    /// </summary>
    public static readonly int ItemHeight = Game.Gui.FontCharHandler.GetFontHeight(Game.Gui.FontSize.Normal) + 1;

    /// <summary>Height of the row body, the reference's <c>_itemHeight</c> used by <c>SetAreaMaxItems</c>.</summary>
    private readonly int _itemWidth;

    /// <summary>The tiled frame, <c>_restorer</c>'s pixels of the reference.</summary>
    private readonly PixelBuffer _background;

    /// <summary>Paints one row of the list.</summary>
    private readonly Action<int, Rect, bool, PixelBuffer> _drawRow;

    /// <summary>Whether a row was clicked since the list opened, <c>_isClicked</c>.</summary>
    private bool _isClicked;

    /// <summary>
    /// Creates the list and builds its frame, the constructor of <c>DropBoxList</c>
    /// (<c>editor_map_specs_window.cpp:636-683</c>).
    /// </summary>
    /// <remarks>
    /// 参考从归档里按 <c>dropBoxIcn</c> 取 0 号帧；端口把帧直接交给调用方，测试因此不需要游戏数据。
    /// </remarks>
    /// <param name="topLeft">Top-left corner of the frame, <c>pt</c>.</param>
    /// <param name="itemCount">How many rows are shown at once, <c>itemsCount</c>.</param>
    /// <param name="frame">Zero frame of <c>DROPLISL</c> (<c>DROPLISL_EVIL</c> on the evil interface).</param>
    /// <param name="drawRow">
    /// Paints a row: condition index, row rectangle, whether the row is the selected one, target buffer.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="frame"/> or <paramref name="drawRow"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemCount"/> is not positive.</exception>
    public EditorMapSpecsConditionList(Point topLeft, int itemCount, Sprite frame, Action<int, Rect, bool, PixelBuffer> drawRow)
        : base(topLeft)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(drawRow);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemCount);

        _drawRow = drawRow;

        SetAreaMaxItems(itemCount);

        PixelBuffer image = frame.Image;
        int topPartHeight = image.Height - 2;
        int listWidth = image.Width;
        int middlePartHeight = topPartHeight - 2;
        int bottomPartHeight = topPartHeight;
        int listHeight = itemCount * (ItemHeight + 2) + 6;

        _itemWidth = listWidth - 6;

        Area = new Rect(topLeft.X, topLeft.Y, listWidth + 1, listHeight);
        _background = BuildFrame(image, listWidth, listHeight, topPartHeight, middlePartHeight, bottomPartHeight);

        SetAreaItems(new Rect(topLeft.X + 2, topLeft.Y + 5, listWidth - 4, listHeight - 6));

        List<ConditionRow> rows = new(itemCount);
        for (int i = 0; i < itemCount; ++i)
        {
            rows.Add(new ConditionRow(i));
        }

        SetListContent(rows);
        SetCurrent(0);
    }

    /// <summary>The frame of the whole list, <c>getArea()</c>.</summary>
    public Rect Area { get; }

    /// <summary>Whether a row was clicked, <c>isClicked()</c>.</summary>
    public bool IsClicked => _isClicked;

    /// <summary>The condition index of the selected row, <c>GetCurrent()</c>.</summary>
    /// <exception cref="InvalidOperationException">Nothing is selected.</exception>
    public int SelectedCondition =>
        IsSelected ? GetCurrent().Value : throw new InvalidOperationException("The condition list has no row selected.");

    /// <summary>
    /// Called on a right click with the condition index, the reference's <c>ActionListPressRight</c>
    /// (<c>editor_map_specs_window.cpp:720-728</c>); the window answers with the condition's description.
    /// </summary>
    public Action<int>? RightClickHandler { get; set; }

    /// <inheritdoc/>
    public override void RedrawItem(ConditionRow item, int x, int y, bool current, PixelBuffer output) =>
        _drawRow(item.Value, new Rect(x, y, _itemWidth, ItemHeight), current, output);

    /// <inheritdoc/>
    public override void RedrawBackground(Point destination, PixelBuffer output) =>
        ImageOps.Blit(output, destination, _background, new Rect(0, 0, _background.Width, _background.Height));

    /// <inheritdoc/>
    public override void ActionCurrentUp()
    {
        // Do nothing, as in the reference.
    }

    /// <inheritdoc/>
    public override void ActionCurrentDn()
    {
        // Do nothing, as in the reference.
    }

    /// <inheritdoc/>
    public override void ActionListSingleClick(ConditionRow item) => _isClicked = true;

    /// <inheritdoc/>
    public override void ActionListDoubleClick(ConditionRow item) => _isClicked = true;

    /// <inheritdoc/>
    public override void ActionListPressRight(ConditionRow item) => RightClickHandler?.Invoke(item.Value);

    /// <summary>
    /// Tiles the frame the way the reference's constructor copies it (<c>editor_map_specs_window.cpp:656-680</c>).
    /// </summary>
    /// <remarks>
    /// 上段整块贴；中段贴 <c>srcY = 2</c> 起的行，一次最多 <c>middlePartHeight</c> 行，最后一块会被裁到列表底边；
    /// 下段再贴一次中段的起始行。右边界另外贴一列 1 像素宽的同一张图，宽出来的一列就是参考里的
    /// <c>lineFixOffsetX</c>。
    /// </remarks>
    private static PixelBuffer BuildFrame(PixelBuffer image, int listWidth, int listHeight, int topPartHeight, int middlePartHeight, int bottomPartHeight)
    {
        PixelBuffer background = new(listWidth + 1, listHeight);

        BlitColumn(background, image, 0, 0, listWidth, topPartHeight, 0);

        int middlePartCount = (listHeight - topPartHeight - bottomPartHeight + middlePartHeight - 1) / middlePartHeight;
        int offsetY = topPartHeight;

        for (int i = 0; i < middlePartCount; ++i)
        {
            int copyHeight = Math.Min(middlePartHeight, listHeight - bottomPartHeight - offsetY);
            BlitColumn(background, image, 2, offsetY, listWidth, copyHeight, 2);

            offsetY += middlePartHeight;
        }

        int bottomOffsetY = listHeight - bottomPartHeight;
        BlitColumn(background, image, 2, bottomOffsetY, listWidth, bottomPartHeight, bottomOffsetY);

        return background;
    }

    /// <summary>Copies one horizontal band and the one pixel wide right edge from <paramref name="image"/>.</summary>
    private static void BlitColumn(PixelBuffer background, PixelBuffer image, int sourceY, int destinationY, int width, int height, int destinationYForEdge)
    {
        if (height <= 0)
        {
            return;
        }

        ImageOps.Blit(background, new Point(0, destinationY), image, new Rect(0, sourceY, width, height));
        ImageOps.Blit(background, new Point(width, destinationYForEdge), image, new Rect(0, sourceY, 1, height));
    }
}
