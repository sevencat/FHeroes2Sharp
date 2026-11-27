namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The selection set of a multi-selection list, <c>MultiItemSelectionBase</c>
/// (<c>dialog_selectitems.cpp:88-129</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把「候选、选中」摊在一个基类里，靠 <c>_setup()</c> 把调用方给的选中集与候选求交、为空就全选；单击一行调
/// <c>_updateSelected()</c> 切换。端口的列表窗是泛型的，多选只是「记住哪几行亮着」，所以逻辑单拎出来，不依赖美术，
/// 可以直接测。
/// </para>
/// <para>
/// <b>为空则全选</b>不是笔误：参考的空集表示「没有指定，就等于都允许」，随机神器的元数据表里第一次是空的，全选才对得上
/// 「随便哪一件都可能出现」。
/// </para>
/// </remarks>
public sealed class MultiItemSelection
{
    private readonly List<int> _ids;
    private readonly HashSet<int> _selected;

    /// <summary>
    /// Builds the selection, <c>_setup()</c> (<c>dialog_selectitems.cpp:96-112</c>).
    /// </summary>
    /// <param name="allowed">The ids the list offers, in the order the caller wants them.</param>
    /// <param name="selected">
    /// The ids that are already picked; ids outside <paramref name="allowed"/> are ignored, and an empty result means
    /// every id is picked.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MultiItemSelection(IReadOnlyList<int> allowed, IReadOnlyList<int> selected)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        ArgumentNullException.ThrowIfNull(selected);

        _ids = [.. allowed];
        _selected = [];

        HashSet<int> allowedSet = [.. allowed];

        foreach (int id in selected)
        {
            if (allowedSet.Contains(id))
            {
                _selected.Add(id);
            }
        }

        if (_selected.Count == 0)
        {
            foreach (int id in _ids)
            {
                _selected.Add(id);
            }
        }
    }

    /// <summary>The ids the list offers, <c>_ids</c>.</summary>
    public IReadOnlyList<int> Ids => _ids;

    /// <summary>The ids that are picked, <c>_selected</c>.</summary>
    public IReadOnlyCollection<int> Selected => _selected;

    /// <summary>Whether nothing is picked, which is what disables the OKAY button.</summary>
    public bool IsEmpty => _selected.Count == 0;

    /// <summary>Whether one id is picked.</summary>
    /// <param name="id">The id to test.</param>
    /// <returns><see langword="true"/> when it is in the selection.</returns>
    public bool IsSelected(int id) => _selected.Contains(id);

    /// <summary>
    /// Toggles one id, <c>_updateSelected()</c> (<c>dialog_selectitems.cpp:117-129</c>).
    /// </summary>
    /// <param name="id">The id to flip; one outside the list is ignored, as the reference asserts instead.</param>
    /// <returns>Whether the id is picked afterwards.</returns>
    public bool Toggle(int id)
    {
        if (!_selected.Remove(id))
        {
            _selected.Add(id);

            return true;
        }

        return false;
    }

    /// <summary>Picks every id, <c>onToggleOn()</c> of the reference.</summary>
    public void SelectAll()
    {
        _selected.Clear();

        foreach (int id in _ids)
        {
            _selected.Add(id);
        }
    }

    /// <summary>Unpicks every id, <c>onToggleOff()</c> of the reference.</summary>
    public void Clear() => _selected.Clear();

    /// <summary>The selection in the list's order, <c>getSelected()</c> (<c>dialog_selectitems.cpp:91-94</c>).</summary>
    /// <remarks>
    /// 参考交回 <c>std::set</c> 的顺序（升序），端口按列表顺序交回——列表本身就是调用方给的顺序，两者在这里一致。
    /// </remarks>
    /// <returns>The picked ids.</returns>
    public List<int> GetSelected() => [.. _ids.Where(_selected.Contains)];
}
