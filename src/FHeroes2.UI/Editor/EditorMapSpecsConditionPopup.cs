using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The victory / loss condition drop-down of the map specifications page, <c>showWinLoseList()</c>
/// (<c>editor_map_specs_window.cpp:1881-1955</c>): a list of the conditions the map can use, opened under the
/// drop button, closed by picking a row, Enter, Escape or a click elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// 参考的做法是在 <c>mapSpecificationsDialog()</c> 里再套一层循环，直到点中或退出才返回选中的条件。端口不让输入层
/// 阻塞，所以把那一层循环拆成这个对象：打开时建好列表（<see cref="TryOpenVictory"/> /
/// <see cref="TryOpenLoss"/>），之后每帧把事件喂给 <see cref="Process"/>，由窗口负责把它画在页面上层
/// （<see cref="Draw"/>），关闭时输入层按 <see cref="ChosenCondition"/> 写回状态。
/// </para>
/// <para>
/// 列表的位置是参考的 <c>{ victoryTextRoi.x - 2, victoryTextRoi.y + victoryTextRoi.height }</c>；列表行的画法、
/// 右键说明（<c>Dialog::Message</c> 的标题与正文）都跟参考同源，正文取自 <see cref="MapSpecsConditionRows"/>。
/// </para>
/// </remarks>
public sealed class EditorMapSpecsConditionPopup
{
    /// <summary>Title of the right-click message of a victory condition, <c>"Special Victory Condition"</c>.</summary>
    public const string VictoryTitle = "Special Victory Condition";

    /// <summary>Title of the right-click message of a loss condition, <c>"Special Loss Condition"</c>.</summary>
    public const string LossTitle = "Special Loss Condition";

    /// <summary>How far the list is shifted left from the text area, the reference's <c>-2</c>.</summary>
    private const int LeftShift = -2;

    private readonly EditorMapSpecsConditionList _list;

    /// <summary>The condition values of the rows, in the order the list shows them.</summary>
    private readonly IReadOnlyList<int> _conditions;

    private readonly Func<int, Sprite?> _getIcon;

    /// <summary>Whether the list is the loss condition list, <c>isLossList</c>.</summary>
    private readonly bool _isLoss;

    private EditorMapSpecsConditionPopup(bool isLoss, Point topLeft, IReadOnlyList<int> conditions, Sprite frame, Func<int, Sprite?> getIcon)
    {
        _isLoss = isLoss;
        _getIcon = getIcon;
        _conditions = conditions;
        _list = new EditorMapSpecsConditionList(topLeft, conditions.Count, frame,
            isLoss ? DrawLossRow : DrawVictoryRow);
        _list.RightClickHandler = ShowDescription;
    }

    /// <summary>Whether this is the loss condition list, <c>isLossList</c>.</summary>
    public bool IsLoss => _isLoss;

    /// <summary>The area the list occupies, the reference's list ROI.</summary>
    public Rect Area => _list.Area;

    /// <summary>
    /// The title of the right-click message, <c>getLossConditionText()</c>'s caller title
    /// (<c>editor_map_specs_window.cpp:693-702</c>).
    /// </summary>
    public string MessageTitle => _isLoss ? LossTitle : VictoryTitle;

    /// <summary>
    /// The description the user asked for with a right click, the body of <c>Dialog::Message()</c>; the host shows
    /// it and calls <see cref="ClearMessage"/>.
    /// </summary>
    public string? Message { get; private set; }

    /// <summary>Whether the list was closed, either by picking a row or by dismissing it.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// The condition the user picked, the value <c>showWinLoseList()</c> returns; <see langword="null"/> when the list
    /// was dismissed without a choice.
    /// </summary>
    public int? ChosenCondition { get; private set; }

    /// <summary>
    /// Opens the victory condition list, the first half of <c>showWinLoseList()</c>
    /// (<c>editor_map_specs_window.cpp:1881-1912</c>).
    /// </summary>
    /// <param name="state">The map being edited.</param>
    /// <param name="layout">Page layout, for the anchor of the list.</param>
    /// <param name="frame">Zero frame of <c>DROPLISL</c>.</param>
    /// <param name="getIcon">Looks up a <c>REQUESTS.ICN</c> frame by index, the row icons.</param>
    /// <returns>The open list, or <see langword="null"/> when the map allows no victory condition at all.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state"/>, <paramref name="layout"/>, <paramref name="frame"/> or <paramref name="getIcon"/>
    /// is <see langword="null"/>.
    /// </exception>
    public static EditorMapSpecsConditionPopup? TryOpenVictory(MapSpecsState state, MapSpecsPageLayout layout, Sprite frame, Func<int, Sprite?> getIcon)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(layout);

        IReadOnlyList<MapVictoryCondition> conditions = MapSpecsConditionChoices.Victory(state);

        return TryOpen(isLoss: false, layout.VictoryTextRoi, state: state, conditions, frame, getIcon);
    }

    /// <summary>
    /// Opens the loss condition list, the first half of <c>showWinLoseList()</c>
    /// (<c>editor_map_specs_window.cpp:1914-1955</c>).
    /// </summary>
    /// <param name="state">The map being edited.</param>
    /// <param name="layout">Page layout, for the anchor of the list.</param>
    /// <param name="frame">Zero frame of <c>DROPLISL</c>.</param>
    /// <param name="getIcon">Looks up a <c>REQUESTS.ICN</c> frame by index, the row icons.</param>
    /// <returns>The open list, or <see langword="null"/> when the map allows no loss condition at all.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state"/>, <paramref name="layout"/>, <paramref name="frame"/> or <paramref name="getIcon"/>
    /// is <see langword="null"/>.
    /// </exception>
    public static EditorMapSpecsConditionPopup? TryOpenLoss(MapSpecsState state, MapSpecsPageLayout layout, Sprite frame, Func<int, Sprite?> getIcon)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(layout);

        IReadOnlyList<MapLossCondition> conditions = MapSpecsConditionChoices.Loss(state);

        return TryOpen(isLoss: true, layout.LossTextRoi, state: state, conditions, frame, getIcon);
    }

    /// <summary>
    /// Feeds one frame worth of events to the list, the body of <c>showWinLoseList()</c>'s loop
    /// (<c>editor_map_specs_window.cpp:1928-1955</c>).
    /// </summary>
    /// <param name="events">The events of the frame.</param>
    /// <remarks>
    /// 顺序照参考：先让列表处理（行点击会置 <c>_isClicked</c>），有选中就返回选中的条件；否则 Enter 取当前行、
    /// Escape 或任意左键关闭列表但不改条件。右键说明不关闭列表——参考那一步是模态消息，消息关掉还回到列表。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public void Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (IsClosed)
        {
            return;
        }

        _list.QueueEventProcessing(events);

        if (_list.IsClicked)
        {
            ChosenCondition = _conditions[_list.SelectedCondition];
            IsClosed = true;

            return;
        }

        if (events.IsKeyPressed(Key.Enter))
        {
            ChosenCondition = _conditions[_list.SelectedCondition];
            IsClosed = true;

            return;
        }

        if (events.IsKeyPressed(Key.Escape) || events.MouseClickLeft())
        {
            IsClosed = true;
        }
    }

    /// <summary>Draws the list over the page, the reference's list <c>Redraw()</c>.</summary>
    /// <param name="target">The page being drawn.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _list.Redraw(target);
    }

    /// <summary>Forgets the pending description once the host has shown it.</summary>
    public void ClearMessage() => Message = null;

    private static EditorMapSpecsConditionPopup? TryOpen<T>(bool isLoss, Rect textRoi, MapSpecsState state,
        IReadOnlyList<T> conditions, Sprite frame, Func<int, Sprite?> getIcon)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(getIcon);

        if (conditions.Count == 0)
        {
            return null;
        }

        List<int> values = new(conditions.Count);

        foreach (T condition in conditions)
        {
            values.Add(Convert.ToInt32(condition, System.Globalization.CultureInfo.InvariantCulture));
        }

        Point topLeft = new(textRoi.X + LeftShift, textRoi.Y + textRoi.Height);
        EditorMapSpecsConditionPopup popup = new(isLoss, topLeft, values, frame, getIcon);

        // 参考 SetCurrent( selectedCondition )：当前条件在筛过的列表里不一定还在，找不到就落在第一行。
        int selected = isLoss
            ? values.IndexOf(Convert.ToInt32(state.LossCondition, System.Globalization.CultureInfo.InvariantCulture))
            : values.IndexOf(Convert.ToInt32(state.VictoryCondition, System.Globalization.CultureInfo.InvariantCulture));

        popup._list.SetCurrent(selected < 0 ? 0 : selected);

        return popup;
    }

    private void DrawVictoryRow(int index, Rect roi, bool current, PixelBuffer output)
    {
        MapVictoryCondition condition = (MapVictoryCondition)_conditions[index];

        MapSpecsConditionRows.DrawVictory(condition, roi, current,
            _getIcon(MapSpecsConditionRows.GetVictoryIconIndex(condition)), output);
    }

    private void DrawLossRow(int index, Rect roi, bool current, PixelBuffer output)
    {
        MapLossCondition condition = (MapLossCondition)_conditions[index];

        MapSpecsConditionRows.DrawLoss(condition, roi, current,
            _getIcon(MapSpecsConditionRows.GetLossIconIndex(condition)), output);
    }

    private void ShowDescription(int index) => Message = _isLoss
        ? MapSpecsConditionRows.GetLossText((MapLossCondition)_conditions[index])
        : MapSpecsConditionRows.GetVictoryText((MapVictoryCondition)_conditions[index]);
}
