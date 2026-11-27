namespace FHeroes2.Game.Editor;

/// <summary>
/// One step of the editor's undo history, whatever it changed: a stroke of tiles or one metadata table.
/// </summary>
/// <remarks>
/// <para>
/// 参考只有一条 <c>HistoryManager</c>，整图快照、元数据字典与地块笔画混在同一个 <c>std::list&lt;Action&gt;</c> 里，
/// 所以撤销严格按时间倒序。端口原本让 <see cref="MapEditSession"/> 自己攒地块、别处另攒元数据，两边顺序就对不上；
/// 这个接口把两种步骤收成同一种元素，撤销与重做因此只有一条时间线。
/// </para>
/// <para>
/// <see cref="MetadataMapAction{T}"/> 已经是这个形状（它的 <c>Redo</c>/<c>Undo</c> 就是重放前后两份字典），
/// 所以它直接实现本接口；<see cref="MapStroke"/> 也实现，撤销时把该笔画的每个地块写回旧值。
/// </para>
/// </remarks>
public interface IEditStep
{
    /// <summary>Reverts the step, <c>undo()</c>.</summary>
    /// <returns><see langword="false"/> when the step could not be reverted.</returns>
    bool Undo();

    /// <summary>Re-applies the step, <c>redo()</c>.</summary>
    /// <returns><see langword="false"/> when the step could not be applied.</returns>
    bool Redo();
}
