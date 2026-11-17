namespace FHeroes2.Game.Battle;

/// <summary>
/// One spell effect and the number of turns it has left, <c>Battle::ModeDuration</c>
/// (<c>battle_troop.h:54-78</c>).
/// </summary>
/// <remarks>
/// 参考继承 <c>std::pair&lt;uint32_t, uint32_t&gt;</c>，于是有 <c>first</c>/<c>second</c> 两个名字；这里换成语义
/// 名字，行为一致：<see cref="IsMode"/> 用的是**按位与**，因此传 <c>IS_BAD_MAGIC</c> 这样的组合也能匹配到
/// 其中任一效果，而 <see cref="BattleModesAffected.GetMode"/> 找的正是第一个命中的条目。
/// </remarks>
public struct BattleModeDuration
{
    /// <summary>Builds one entry.</summary>
    /// <param name="mode">The spell effect, a single bit of <see cref="BattleMonsterState"/>.</param>
    /// <param name="duration">How many turns it still lasts.</param>
    public BattleModeDuration(uint mode, uint duration)
    {
        Mode = mode;
        Duration = duration;
    }

    /// <summary>The spell effect, the reference's <c>first</c>.</summary>
    public uint Mode { get; }

    /// <summary>Remaining turns, the reference's <c>second</c>.</summary>
    public uint Duration { get; set; }

    /// <summary>Whether this entry is covered by the flags, <c>ModeDuration::isMode()</c>.</summary>
    /// <param name="mode">One flag or a combination of them.</param>
    public readonly bool IsMode(uint mode) => (Mode & mode) != 0;

    /// <summary>Whether the effect runs out now, <c>ModeDuration::isZeroDuration()</c>.</summary>
    public readonly bool IsZeroDuration => Duration == 0;

    /// <summary>Burns one turn of the duration, <c>ModeDuration::DecreaseDuration()</c>.</summary>
    /// <remarks>参考不会让它掉到零以下。</remarks>
    public void DecreaseDuration()
    {
        if (Duration != 0)
        {
            --Duration;
        }
    }
}

/// <summary>
/// The list of spell effects with a duration that a unit carries, <c>Battle::ModesAffected</c>
/// (<c>battle_troop.h:80-93</c>, <c>battle_troop.cpp:100-131</c>).
/// </summary>
/// <remarks>
/// 参考继承 <c>std::vector&lt;ModeDuration&gt;</c> 并 <c>reserve( 3 )</c>，因为一场战斗里一个单位很少同时带
/// 超过三个限时效果。这里用 <see cref="List{T}"/> 保留同样的容量提示，顺序也保持插入序——<see cref="FindZeroDuration"/>
/// 取第一个到期的效果，顺序会影响先移除哪一个。
/// </remarks>
public sealed class BattleModesAffected
{
    private readonly List<BattleModeDuration> _items = new(3);

    /// <summary>How many effects are tracked.</summary>
    public int Count => _items.Count;

    /// <summary>Whether nothing is tracked.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>One entry by position, the reference's inherited <c>operator[]</c>.</summary>
    /// <param name="index">Position in the list.</param>
    public BattleModeDuration this[int index] => _items[index];

    /// <summary>
    /// The duration left for a mode, <c>ModesAffected::GetMode()</c>.
    /// </summary>
    /// <param name="mode">One flag or a combination; the first matching entry answers.</param>
    /// <returns>The remaining turns, or <c>0</c> when the mode is not tracked.</returns>
    public uint GetMode(uint mode)
    {
        foreach (BattleModeDuration item in _items)
        {
            if (item.IsMode(mode))
            {
                return item.Duration;
            }
        }

        return 0;
    }

    /// <summary>
    /// Adds a mode or replaces its duration, <c>ModesAffected::AddMode()</c>.
    /// </summary>
    /// <param name="mode">The effect to track.</param>
    /// <param name="duration">How many turns it lasts.</param>
    /// <remarks>重复添加同一个效果只是刷新时长，不会长出第二个条目。</remarks>
    public void AddMode(uint mode, uint duration)
    {
        for (int index = 0; index < _items.Count; ++index)
        {
            if (_items[index].IsMode(mode))
            {
                BattleModeDuration item = _items[index];
                item.Duration = duration;
                _items[index] = item;

                return;
            }
        }

        _items.Add(new BattleModeDuration(mode, duration));
    }

    /// <summary>Drops every entry the flags cover, <c>ModesAffected::RemoveMode()</c>.</summary>
    /// <param name="mode">One flag or a combination.</param>
    public void RemoveMode(uint mode) => _items.RemoveAll(item => item.IsMode(mode));

    /// <summary>Burns one turn off every entry, <c>ModesAffected::DecreaseDuration()</c>.</summary>
    public void DecreaseDuration()
    {
        for (int index = 0; index < _items.Count; ++index)
        {
            BattleModeDuration item = _items[index];
            item.DecreaseDuration();
            _items[index] = item;
        }
    }

    /// <summary>
    /// The first mode that has run out, <c>ModesAffected::FindZeroDuration()</c>.
    /// </summary>
    /// <returns>The mode, or <c>0</c> when every effect still lasts.</returns>
    public uint FindZeroDuration()
    {
        foreach (BattleModeDuration item in _items)
        {
            if (item.IsZeroDuration)
            {
                return item.Mode;
            }
        }

        return 0;
    }

    /// <summary>Forgets everything; the reference inherits <c>clear()</c>.</summary>
    public void Clear() => _items.Clear();
}
