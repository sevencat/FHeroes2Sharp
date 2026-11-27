using FHeroes2.Game.Editor;

namespace FHeroes2.UI.Editor;

/// <summary>
/// What a drawn condition panel hands back to its window: the areas it used and the checkboxes it created.
/// </summary>
/// <remarks>
/// 参考把这些当成员留在 <c>VictoryConditionUI</c>/<c>LossConditionUI</c> 里（<c>_selectConditionRoi</c>、
/// <c>_alliancesCheckboxes</c> 等），事件循环照着它们判断点了哪里。端口把面板绘制做成函数，
/// 所以把这两样一起返回；窗口存住它，下一次画之前重新拿。
/// </remarks>
public sealed class MapSpecsConditionPanel
{
    /// <summary>
    /// Creates the result of one panel render.
    /// </summary>
    /// <param name="layout">The areas the panel used, for hit testing.</param>
    /// <param name="allianceCheckboxes">The alliance checkboxes, empty unless the panel had an alliance block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="layout"/> is <see langword="null"/>.</exception>
    public MapSpecsConditionPanel(
        MapSpecsConditionPanelLayout layout, IReadOnlyList<List<Checkbox>>? allianceCheckboxes = null)
    {
        ArgumentNullException.ThrowIfNull(layout);

        Layout = layout;
        AllianceCheckboxes = allianceCheckboxes ?? [];
    }

    /// <summary>The areas the panel drew into.</summary>
    public MapSpecsConditionPanelLayout Layout { get; }

    /// <summary>
    /// The checkbox row of every alliance line, in row order; each checkbox keeps its colour, its area and its mark.
    /// </summary>
    public IReadOnlyList<List<Checkbox>> AllianceCheckboxes { get; }
}
