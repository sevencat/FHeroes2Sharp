using System.Collections;

namespace FHeroes2.Game.Battle;

/// <summary>
/// A list of stacks on the battlefield, <c>Battle::Units</c> (<c>battle_army.h:47-117</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考直接继承 <c>std::vector&lt;Unit *&gt;</c> 并加了三件事：按 UID 找、按模式找、按速度排序。端口的
/// <see cref="List{T}"/> 不适合当基类（成员太多，语义会被稀释），所以这里用组合：内部一个
/// <see cref="List{T}"/>，外面只暴露战斗真正用得到的那些。
/// </para>
/// <para>
/// 这个类是 <see langword="public"/> 且不封的：参考的 <c>Battle::Force</c> 就继承 <c>Units</c>，
/// <see cref="BattleForce"/> 照样继承它，好让「一支军队」天然就是「一堆单位」。
/// </para>
/// <para>
/// 三个「筛选副本」对应参考的三个 <c>FilterType</c> 标签：只留活的、只留活的且排除某个单位、只留没换边的。
/// 参考靠模板参数在编译期选谓词，端口用三个具名方法——调用点读起来更直白，也不用为了一个谓词去写泛型。
/// </para>
/// </remarks>
public class BattleUnits : IReadOnlyList<BattleUnit>
{
    private const int DefaultCapacity = 16;

    private readonly List<BattleUnit> _units = new(DefaultCapacity);

    /// <summary>An empty list; the reference reserves room for sixteen stacks up front.</summary>
    public BattleUnits()
    {
    }

    /// <summary>How many stacks the list holds.</summary>
    public int Count => _units.Count;

    /// <summary>The stack at a position.</summary>
    /// <param name="index">Zero-based position.</param>
    public BattleUnit this[int index] => _units[index];

    /// <summary>Adds a stack, the <c>push_back</c> every caller makes.</summary>
    /// <param name="unit">The stack, which must not be <see langword="null"/>.</param>
    public void Add(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        _units.Add(unit);
    }

    /// <summary>Takes a stack out of the list, <c>erase( find( ... ) )</c>.</summary>
    /// <param name="unit">The stack to drop.</param>
    /// <returns>Whether it was there.</returns>
    public bool Remove(BattleUnit unit) => _units.Remove(unit);

    /// <summary>Empties the list, <c>clear()</c>.</summary>
    public void Clear() => _units.Clear();

    /// <summary>
    /// The stack with a given UID, <c>FindUID()</c>.
    /// </summary>
    /// <param name="uid">The unique id handed out when the stack entered the battle.</param>
    /// <returns>The stack, or <see langword="null"/> when no stack has that id.</returns>
    public BattleUnit? FindUid(uint uid)
    {
        foreach (BattleUnit unit in _units)
        {
            if (unit.IsUid(uid))
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>
    /// The first stack carrying a mode, <c>FindMode()</c>.
    /// </summary>
    /// <param name="mode">The mode, or a mask of modes; any of them matches.</param>
    /// <returns>The first stack that has the mode, or <see langword="null"/>.</returns>
    public BattleUnit? FindMode(uint mode)
    {
        foreach (BattleUnit unit in _units)
        {
            if (unit.HasModes(mode))
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>
    /// Sorts the stacks fastest first, <c>SortFastest()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考用 <c>std::stable_sort</c>，注释写明「速度相同的单位必须保持原有顺序，否则回合队列会乱」。所以这里不能
    /// 用 <c>List.Sort</c>（不稳定），而是走 <c>OrderByDescending</c>——LINQ 的排序是稳定的——再按顺序写回。
    /// </para>
    /// <para>
    /// 比较的是 <see cref="BattleUnit.GetSpeed()"/>，也就是**带减速/加速的战斗速度**：参考的
    /// <c>Army::FastestTroop</c> 拿的是 <c>Troop *</c>，而 <c>Troop::GetSpeed</c> 是虚函数、被 <c>Unit</c> 覆盖了，
    /// 所以这里不是兵种表上的那个数字。
    /// </para>
    /// </remarks>
    public void SortFastest()
    {
        BattleUnit[] sorted = [.. _units.OrderByDescending(static unit => unit.GetSpeed())];

        _units.Clear();
        _units.AddRange(sorted);
    }

    /// <summary>
    /// A copy holding only the living stacks, <c>Units( units, REMOVE_INVALID_UNITS )</c>.
    /// </summary>
    public BattleUnits ValidOnly()
    {
        BattleUnits result = new();

        foreach (BattleUnit unit in _units)
        {
            if (unit.IsValid)
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// A copy holding the living stacks except one, <c>Units( units, REMOVE_INVALID_UNITS_AND_SPECIFIED_UNIT, unit )</c>.
    /// </summary>
    /// <param name="unitToRemove">The stack that must not appear in the copy.</param>
    public BattleUnits ValidExcept(BattleUnit unitToRemove)
    {
        ArgumentNullException.ThrowIfNull(unitToRemove);

        BattleUnits result = new();

        foreach (BattleUnit unit in _units)
        {
            if (unit.IsValid && !ReferenceEquals(unit, unitToRemove))
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// A copy holding the living stacks that still fight for their own side,
    /// <c>Units( units, REMOVE_INVALID_UNITS_AND_UNITS_THAT_CHANGED_SIDES )</c>.
    /// </summary>
    /// <remarks>
    /// 「换了边」就是被催眠：<c>GetColor() != GetCurrentColor()</c>。这类单位还在场上，但不该再算进原阵营的行动队列。
    /// </remarks>
    public BattleUnits ValidOnOwnSide()
    {
        BattleUnits result = new();

        foreach (BattleUnit unit in _units)
        {
            if (unit.IsValid && unit.Color == unit.GetCurrentColor())
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>Enumerates the stacks in list order.</summary>
    public IEnumerator<BattleUnit> GetEnumerator() => _units.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
