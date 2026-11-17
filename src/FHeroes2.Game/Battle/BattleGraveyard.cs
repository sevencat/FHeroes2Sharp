namespace FHeroes2.Game.Battle;

/// <summary>
/// The dead stacks lying on the battlefield, <c>Battle::Graveyard</c> (<c>battle_grave.h:36-57</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用 <c>std::map&lt;int32_t, std::vector&lt;Unit *&gt;&gt;</c>：键是格子下标，值是「躺在这格上的单位」。
/// 宽单位同时压在头格与尾格两条链里，所以同一个单位会被查两次；查询方（桥、对尸体施法）靠
/// <see cref="GetLastUnit"/> 拿到「最后躺上去的那个」，好把动画叠在最上面。
/// </para>
/// <para>
/// 顺序是有意义的：<see cref="MoveToLastIfPresent"/> 把某个单位挪到链尾，正是为了「对尸体施法时把它画在最上面」
/// （<c>battle_grave.h:53-54</c>）。所以这里用 <see cref="SortedDictionary{TKey,TValue}"/> 而不是哈希表——键按格子
/// 下标有序遍历，与 <c>std::map</c> 一致。
/// </para>
/// <para>
/// 参考在 <c>addUnit</c>/<c>removeUnit</c>/<c>moveToLastIfPresent</c> 开头用 <c>assert</c> 要求「头格有效、宽单位的尾格
/// 有效、窄单位的尾格无效」。端口把断言换成提前返回：坟场是竞技场在死亡流程里调的，与其让一个坏下标把整场战斗
/// 炸掉，不如让这一条登记静默失配——真正需要「一定有效」的地方由竞技场的调用点保证。
/// </para>
/// </remarks>
public sealed class BattleGraveyard
{
    private readonly SortedDictionary<int, List<BattleUnit>> _cells = [];

    /// <summary>
    /// The cells that have at least one dead stack on them, <c>getOccupiedCells()</c>.
    /// </summary>
    /// <returns>Cell indexes in ascending order, as <c>std::map</c> iterates them.</returns>
    /// <remarks>空链（单位被复活后从链里摘掉）不算占用，与参考跳过 <c>units.empty()</c> 一致。</remarks>
    public List<int> GetOccupiedCells()
    {
        List<int> result = new(_cells.Count);

        foreach ((int index, List<BattleUnit> units) in _cells)
        {
            if (units.Count != 0)
            {
                result.Add(index);
            }
        }

        return result;
    }

    /// <summary>
    /// Lays a stack down, <c>addUnit()</c>: it goes on top of whatever already lies on its cells.
    /// </summary>
    /// <param name="unit">The stack that just died.</param>
    public void AddUnit(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!BattleBoard.IsValidIndex(unit.HeadIndex) || (unit.IsWide && !BattleBoard.IsValidIndex(unit.TailIndex)))
        {
            return;
        }

        GetOrCreate(unit.HeadIndex).Add(unit);

        if (unit.IsWide)
        {
            GetOrCreate(unit.TailIndex).Add(unit);
        }
    }

    /// <summary>
    /// Takes a stack back off the ground, <c>removeUnit()</c>.
    /// </summary>
    /// <param name="unit">The stack that came back to life.</param>
    /// <remarks>
    /// 参考只把指针从链里摘掉，空的链留在 map 里；端口照抄——<see cref="GetOccupiedCells"/> 会跳过空链，所以留着无害。
    /// </remarks>
    public void RemoveUnit(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        RemoveFrom(unit.HeadIndex, unit);

        if (unit.IsWide)
        {
            RemoveFrom(unit.TailIndex, unit);
        }
    }

    /// <summary>
    /// The stack drawn on top of a cell, <c>getLastUnit()</c>.
    /// </summary>
    /// <param name="index">The cell.</param>
    /// <returns>The last stack laid on that cell, or <see langword="null"/> when the cell is empty.</returns>
    public BattleUnit? GetLastUnit(int index)
    {
        if (!_cells.TryGetValue(index, out List<BattleUnit>? units) || units.Count == 0)
        {
            return null;
        }

        return units[^1];
    }

    /// <summary>
    /// Brings a stack to the top of its cells, <c>moveToLastIfPresent()</c>.
    /// </summary>
    /// <param name="unit">The stack that a spell is about to be cast on.</param>
    /// <remarks>
    /// 参考用 <c>std::rotate( iter, iter + 1, end )</c> 把该元素挪到链尾，其余元素的相对顺序不变；端口用
    /// <c>RemoveAt</c> + <c>Add</c> 做同一件事。单位不在这条链里时什么都不做（「没死，不用挪」）。
    /// </remarks>
    public void MoveToLastIfPresent(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        MoveToEndIfPresent(unit.HeadIndex, unit);

        if (unit.IsWide)
        {
            MoveToEndIfPresent(unit.TailIndex, unit);
        }
    }

    /// <summary>
    /// Everything lying on a cell, <c>getUnits()</c>.
    /// </summary>
    /// <param name="index">The cell.</param>
    /// <returns>A copy of the chain, bottom first; empty when nothing lies there.</returns>
    public List<BattleUnit> GetUnits(int index) =>
        _cells.TryGetValue(index, out List<BattleUnit>? units) ? [.. units] : [];

    /// <summary>How many cells carry at least one stack, which is not the number of dead stacks.</summary>
    public int Count => GetOccupiedCells().Count;

    private List<BattleUnit> GetOrCreate(int index)
    {
        if (!_cells.TryGetValue(index, out List<BattleUnit>? units))
        {
            units = [];
            _cells.Add(index, units);
        }

        return units;
    }

    private void RemoveFrom(int index, BattleUnit unit)
    {
        if (_cells.TryGetValue(index, out List<BattleUnit>? units))
        {
            units.Remove(unit);
        }
    }

    private void MoveToEndIfPresent(int index, BattleUnit unit)
    {
        if (!_cells.TryGetValue(index, out List<BattleUnit>? units))
        {
            return;
        }

        int at = units.IndexOf(unit);

        if (at < 0)
        {
            return;
        }

        units.RemoveAt(at);
        units.Add(unit);
    }
}
