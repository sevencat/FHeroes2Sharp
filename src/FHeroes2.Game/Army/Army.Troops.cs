using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Army;

/// <summary>
/// The container half of an army — merging, ordering, counting and comparing the stacks —,
/// <c>Troops</c> of <c>army.cpp:324-700</c>, and the three army-level questions built on it
/// (<c>army.cpp:1573-1603</c>, <c>army.cpp:1758-1782</c>).
/// </summary>
/// <remarks>
/// <para>
/// In the reference <c>Troops</c> is a <c>std::vector&lt;Troop *&gt;</c> and <c>Army</c> adds the fixed five
/// slots, the commander and the colour on top of it. The port has no such split: <see cref="Army"/> is the
/// five slots. Everything in this file therefore works on the slots in place, and the two methods that
/// really do need a dynamic container — <see cref="GetOptimized"/> and <see cref="GetReinforcementValue"/> —
/// build a <see cref="List{T}"/> for as long as they need it.
/// </para>
/// <para>
/// <b>Two different strengths.</b> <see cref="GetTroopsStrength"/> is <c>Troops::GetStrength()</c>: the sum of
/// the stacks, nothing else. <see cref="GetStrength"/> is <c>Army::GetStrength()</c>: the same sum with the
/// commander's attack and defense, the archery square root, the morale and luck factors and the magic value on
/// top. The reference is careful to only ever ask the first one when it compares *containers* — a joined
/// army is a pile of creatures, not a hero — and the port keeps that distinction as two methods rather than
/// one with a flag.
/// </para>
/// <para>
/// Not here: the methods that move stacks <i>between</i> two armies — <c>JoinStrongest</c>,
/// <c>JoinStrongestFromArmy</c>, <c>MoveTroops</c> and <c>SwapTroops</c> — are in <c>Army.Transfer.cs</c>.
/// <c>UpgradeTroops</c> waits for <c>Castle</c> — it is the "upgrade what the castle can upgrade and the
/// kingdom can pay for" half of the pair, and both of its extra questions need a castle;
/// <c>SplitTroopIntoFreeSlots</c> and its three siblings wait for the dialog that drags a single stack.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// The number of creatures of one type in the army, <c>Troops::GetCountMonsters()</c>
    /// (<c>army.cpp:333-348</c>).
    /// </summary>
    /// <remarks>
    /// Every valid stack of that monster is summed, which is more than one only before
    /// <see cref="MergeSameMonsterTroops"/> has run — the reference has the same window, and its callers
    /// (quests, the map objects that count a stack) want the total either way.
    /// </remarks>
    /// <param name="monster">The monster to count.</param>
    /// <returns>The total number of creatures of that monster; zero when there are none.</returns>
    public uint GetCountMonsters(MonsterType monster)
    {
        uint result = 0;

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid && troop.IsMonster(monster))
            {
                result += troop.Count;
            }
        }

        return result;
    }

    /// <summary>
    /// The army with every duplicate stack merged, <c>Troops::GetOptimized()</c> (<c>army.cpp:667-695</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A new container, not a reordering of this one: the stacks come out in the order their monster first
    /// appears, and a monster that sits in two slots ends up as one stack at the position of the first. This
    /// is what <c>Troops::JoinStrongest</c> and <c>getReinforcementValue</c> run before deciding what to keep,
    /// because three stacks of peasants are one stack of peasants when the question is "how much is this
    /// army worth".
    /// </para>
    /// <para>
    /// The reference returns a <c>Troops</c>; the port returns the <see cref="List{T}"/> it is built from, with
    /// copies of the stacks. The caller owns the result and may sort or truncate it.
    /// </para>
    /// </remarks>
    /// <returns>At most five stacks, one per distinct monster, in first-appearance order.</returns>
    public List<Troop> GetOptimized()
    {
        List<Troop> result = [];

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            int index = IndexOfMonster(result, troop.Monster);

            if (index < 0)
            {
                result.Add(new Troop(troop.Monster, troop.Count));
            }
            else
            {
                result[index].Count += troop.Count;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges every duplicate stack into the first slot that holds it,
    /// <c>Troops::MergeSameMonsterTroops()</c> (<c>army.cpp:613-636</c>).
    /// </summary>
    /// <remarks>
    /// The later slots are emptied rather than removed — the army has five slots whatever happens — and the
    /// counts are added in place, so a stack of five and a stack of seven become one stack of twelve.
    /// </remarks>
    public void MergeSameMonsterTroops()
    {
        for (int slot = 0; slot < _slots.Length; ++slot)
        {
            Troop troop = _slots[slot];

            if (!troop.IsValid)
            {
                continue;
            }

            for (int other = slot + 1; other < _slots.Length; ++other)
            {
                Troop otherTroop = _slots[other];

                if (otherTroop.IsValid && otherTroop.IsMonster(troop.Monster))
                {
                    troop.Count += otherTroop.Count;
                    otherTroop.Reset();
                }
            }
        }
    }

    /// <summary>
    /// Merges the first duplicate stack it finds, <c>Troops::MergeSameMonsterOnce()</c>
    /// (<c>army.cpp:638-665</c>).
    /// </summary>
    /// <remarks>
    /// One merge per call, so that the caller can stop as soon as it has freed a slot. That is what the
    /// reference's "make room for the new troops" loops do: they call this and check
    /// <see cref="OccupiedSlotCount"/> after each merge instead of merging everything at once.
    /// </remarks>
    /// <returns>Whether anything was merged, i.e. whether a slot was freed.</returns>
    public bool MergeSameMonsterOnce()
    {
        for (int slot = 0; slot < _slots.Length; ++slot)
        {
            Troop troop = _slots[slot];

            if (!troop.IsValid)
            {
                continue;
            }

            for (int other = slot + 1; other < _slots.Length; ++other)
            {
                Troop otherTroop = _slots[other];

                if (otherTroop.IsValid && otherTroop.IsMonster(troop.Monster))
                {
                    troop.Count += otherTroop.Count;
                    otherTroop.Reset();

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Orders the slots from the strongest stack to the weakest, <c>Troops::SortStrongest()</c>
    /// (<c>army.cpp:697-700</c>).
    /// </summary>
    /// <remarks>
    /// The strength is <see cref="Troop.GetStrength"/> — the creature's strength times its count — so this is
    /// "what is worth the most", not "what fights first": the battle engine sorts by speed.
    /// <see cref="GetReinforcementValue"/> uses it to know which stacks survive when five slots cannot hold the
    /// six stacks of two merged armies.
    /// </remarks>
    public void SortStrongest() =>
        Array.Sort(_slots, static (left, right) =>
        {
            // The empty slots go last: a stack of zero creatures must never win a comparison and stay in
            // front of a real one when the array is only partially filled.
            if (left.IsValid != right.IsValid)
            {
                return left.IsValid ? -1 : 1;
            }

            return right.GetStrength().CompareTo(left.GetStrength());
        });

    /// <summary>
    /// The stack with the least strength, <c>Troops::GetWeakestTroop()</c> (<c>army.cpp:598-600</c>).
    /// </summary>
    /// <returns>The stack, or <see langword="null"/> when no slot is valid.</returns>
    public Troop? GetWeakestTroop() => GetBestMatch(static (troop, best) => troop.GetStrength() < best.GetStrength());

    /// <summary>
    /// The stack with the most strength, <c>Troops::GetStrongestTroop()</c>.
    /// </summary>
    /// <returns>The stack, or <see langword="null"/> when no slot is valid.</returns>
    public Troop? GetStrongestTroop() => GetBestMatch(static (troop, best) => troop.GetStrength() > best.GetStrength());

    /// <summary>
    /// The strength of the stacks alone, <c>Troops::GetStrength()</c> (<c>army.cpp:503-511</c>).
    /// </summary>
    /// <remarks>
    /// No commander, no morale, no luck, no magic — the sum of <see cref="Troop.GetStrength"/>. See the class
    /// remarks for why this is not <see cref="GetStrength"/>.
    /// </remarks>
    /// <returns>The summed strength of the valid stacks.</returns>
    public double GetTroopsStrength()
    {
        double strength = 0;

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid)
            {
                strength += troop.GetStrength();
            }
        }

        return strength;
    }

    /// <summary>
    /// What a reinforcement would add to the army, <c>Troops::getReinforcementValue()</c>
    /// (<c>army.cpp:350-366</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two armies are merged on paper: the reinforcement is optimized (duplicates merged), appended, merged
    /// again, sorted strongest first, cut back to five stacks and measured. The answer is the difference, so it
    /// is not simply the reinforcement's strength — the weakest stack of the combined army may be one of our
    /// own, and then joining the reinforcement can be worth <i>less</i> than the reinforcement alone.
    /// </para>
    /// <para>
    /// The reference notes that the combined container holds plain <c>Troop</c> objects rather than
    /// <c>ArmyTroop</c> ones, so its <c>GetStrength()</c> has no hero bonuses. The port has one troop type and
    /// gets the same effect by using <see cref="GetTroopsStrength"/> on both ends.
    /// </para>
    /// </remarks>
    /// <param name="reinforcement">The army that would join; it is not modified.</param>
    /// <returns>The change in strength the merge would cause.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reinforcement"/> is <see langword="null"/>.</exception>
    public double GetReinforcementValue(Army reinforcement)
    {
        ArgumentNullException.ThrowIfNull(reinforcement);

        double initialValue = GetTroopsStrength();

        List<Troop> combined = [];

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid)
            {
                combined.Add(new Troop(troop.Monster, troop.Count));
            }
        }

        foreach (Troop troop in reinforcement.GetOptimized())
        {
            int index = IndexOfMonster(combined, troop.Monster);

            if (index < 0)
            {
                combined.Add(new Troop(troop.Monster, troop.Count));
            }
            else
            {
                combined[index].Count += troop.Count;
            }
        }

        combined.Sort(static (left, right) => right.GetStrength().CompareTo(left.GetStrength()));

        while (combined.Count > MaximumTroopCount)
        {
            combined.RemoveAt(combined.Count - 1);
        }

        double strength = 0;

        foreach (Troop troop in combined)
        {
            strength += troop.GetStrength();
        }

        return strength - initialValue;
    }

    /// <summary>
    /// Whether this army would beat the given one, <c>Army::isStrongerThan()</c> (<c>army.cpp:1573-1585</c>).
    /// </summary>
    /// <remarks>
    /// An empty target loses to everything, including to an empty army — the reference returns
    /// <see langword="true"/> before comparing any numbers, so "is this stack stronger than nothing" is yes.
    /// <paramref name="safetyRatio"/> scales the target, which is how the AI asks for a comfortable win
    /// (1.5 means "half again as strong").
    /// </remarks>
    /// <param name="target">The army to compare with.</param>
    /// <param name="safetyRatio">Factor applied to the target's strength; 1 is a fair fight.</param>
    /// <returns>Whether this army is stronger.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public bool IsStrongerThan(Army target, double safetyRatio = 1.0)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.IsValid)
        {
            return true;
        }

        return GetStrength() > target.GetStrength() * safetyRatio;
    }

    /// <summary>
    /// Whether the army is mostly walkers that hit in melee, <c>Army::isMeleeDominantArmy()</c>
    /// (<c>army.cpp:1587-1603</c>).
    /// </summary>
    /// <remarks>
    /// The AI uses it to decide whether a castle is worth besieging: an army of shooters and flyers can cross a
    /// wall, a melee army has to break the gate. Strength, not count — one dragon outweighs thirty peasants.
    /// </remarks>
    /// <returns>Whether the melee stacks outweigh the shooting and flying ones.</returns>
    public bool IsMeleeDominantArmy()
    {
        double melee = 0;
        double other = 0;

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            if (!troop.IsArchers && !troop.IsFlying)
            {
                melee += troop.GetStrength();
            }
            else
            {
                other += troop.GetStrength();
            }
        }

        return melee > other;
    }

    /// <summary>
    /// The monster of the strongest stack, <c>Army::GetStrongestMonster()</c> (<c>army.cpp:1763-1772</c>).
    /// </summary>
    /// <remarks>
    /// <b>Per creature, not per stack</b>: the reference compares
    /// <c>troop-&gt;GetMonster().GetMonsterStrength()</c>, so one dragon beats forty peasants even though the
    /// peasants are the stronger stack. That is deliberate — the callers (the battle's "which monster does this
    /// army field" and the AI's spell targeting) ask about the creature, not the pile.
    /// </remarks>
    /// <returns>The monster, or <see cref="MonsterType.Unknown"/> for an empty army.</returns>
    public MonsterType GetStrongestMonster()
    {
        MonsterType strongest = MonsterType.Unknown;
        double bestStrength = MonsterStatistics.GetMonsterStrength(strongest);

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            double strength = MonsterStatistics.GetMonsterStrength(troop.Monster);

            if (strength > bestStrength)
            {
                strongest = troop.Monster;
                bestStrength = strength;
            }
        }

        return strongest;
    }

    /// <summary>
    /// Empties the slots that hold a monster but no creatures, <c>Army::resetInvalidMonsters()</c>
    /// (<c>army.cpp:1774-1781</c>).
    /// </summary>
    /// <remarks>
    /// A stack can be invalid while still naming a monster — a count that went to zero, or an id from a
    /// corrupt save. The reference repairs them into the unknown monster so that the map can draw the slot
    /// without asking <c>isValid()</c> first.
    /// </remarks>
    public void ResetInvalidMonsters()
    {
        foreach (Troop troop in _slots)
        {
            if (troop.Monster != MonsterType.Unknown && !troop.IsValid)
            {
                troop.Set(MonsterType.Unknown, 0);
            }
        }
    }

    /// <summary>
    /// Upgrades every stack of one monster in place, <c>Troops::UpgradeMonsters()</c> (<c>army.cpp:324-332</c>).
    /// </summary>
    /// <remarks>
    /// The reference's only caller is the "upgrade everything of this kind" button, and it asks by monster
    /// rather than by slot because the same monster may sit in two slots before
    /// <see cref="MergeSameMonsterTroops"/> has run. The count of each stack is kept — upgrading a creature is
    /// a change of kind, not of number — and stacks of other monsters are untouched.
    /// </remarks>
    /// <param name="monster">The monster whose stacks are to be upgraded.</param>
    public void UpgradeMonsters(MonsterType monster)
    {
        foreach (Troop troop in _slots)
        {
            if (troop.IsValid && troop.IsMonster(monster))
            {
                troop.Upgrade();
            }
        }
    }

    /// <summary>
    /// Lets the sirens sing the army into the sea, <c>Army::ActionToSirens()</c> (<c>army.cpp:1542-1571</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>army.cpp:1542-1571</c>。逐栈处理，三条规则缺一不可：
    /// </para>
    /// <list type="number">
    /// <item>空栈跳过；</item>
    /// <item><b>只有 1 个的栈完全不受影响</b>——塞伦唱不动仅剩的一个人；</item>
    /// <item>其余栈去掉 30%（向下取整），<b>但至少去掉 1 个</b>：3 个的栈取整后是 0，仍要淹死一个。</item>
    /// </list>
    /// <para>
    /// 返回值是「死掉的那些兵值多少经验」——按<b>每点血 1 点经验</b>算，即 <c>数量 × 单体生命值</c>，
    /// 不是按金币或战斗力折算。全部是营养公式，没有随机数。
    /// </para>
    /// <para>
    /// 参考里这个方法<b>不管名册</b>：登记访问与加经验都在 <c>Heroes::ActionToSirens</c> 里，这里只负责
    /// 「兵少了、经验值算出来了」。端口照抄这个分工。
    /// </para>
    /// </remarks>
    /// <returns>Experience the drowned troops are worth; <c>0</c> when no stack lost anyone.</returns>
    public uint ActionToSirens()
    {
        uint experience = 0;

        foreach (Troop troop in this)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            uint troopCount = troop.Count;

            if (troopCount == 1)
            {
                // Sirens do not affect a stack of one.
                continue;
            }

            // 30% of the stack drowns.
            uint troopToRemove = troopCount * 3 / 10;

            if (troopToRemove == 0)
            {
                // At least one creature goes, even when that is more than 30% of the stack.
                troopToRemove = 1;
            }

            troop.Count = troopCount - troopToRemove;

            experience += troopToRemove * (uint)MonsterStatistics.GetBattleStats(troop.Monster).HitPoints;
        }

        return experience;
    }

    /// <summary>
    /// Puts the stacks into the slots position by position, <c>Troops::Assign()</c> (<c>army.cpp:493-501</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="JoinTroop(MonsterType, uint, bool)"/> this never merges and never looks for a free slot: the
    /// first troop becomes the first slot, the second the second, and an invalid troop leaves a gap where it was.
    /// That is what a map's custom garrison needs — the block names five separate stacks, repeats and all.
    /// </para>
    /// <para>
    /// The army is cleared first, so this replaces the garrison rather than adding to it. The reference's two
    /// arguments are the ends of a range its callers take from an array; the port takes the array.
    /// </para>
    /// </remarks>
    /// <param name="troops">Up to five stacks, in slot order.</param>
    public void Assign(IReadOnlyList<Troop> troops)
    {
        ArgumentNullException.ThrowIfNull(troops);

        Clean();

        int limit = Math.Min(_slots.Length, troops.Count);

        for (int slot = 0; slot < limit; ++slot)
        {
            if (troops[slot] is { IsValid: true } troop)
            {
                _slots[slot].Set(troop.Monster, troop.Count);
            }
        }
    }

    /// <summary>
    /// Upgrades every stack this town allows, paying as it goes, <c>Troops::UpgradeTroops()</c>
    /// (<c>army.cpp:528-560</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three questions per stack, in the reference's order: is it upgradable at all, does the town share the
    /// stack's race, and does the town own the dwelling the upgraded creature lives in (not the dwelling of the
    /// creature it has now). The last one is the reason a town without the upgraded dwelling cannot upgrade even
    /// with money: the whole point of the building.
    /// </para>
    /// <para>
    /// Payment is per stack and immediate, so a treasury that runs out halfway through upgrades the first stacks
    /// and leaves the rest alone — the reference behaves the same way because it checks
    /// <c>kingdom.AllowPayment()</c> per stack. Prices come from <see cref="Troop.GetTotalUpgradeCost"/>, which is
    /// the difference between the two creatures' costs doubled, times the count.
    /// </para>
    /// </remarks>
    /// <param name="castle">The town whose race and dwellings decide what may be upgraded.</param>
    /// <param name="treasury">The owner's resources, reduced by each upgrade that happens.</param>
    public void UpgradeTroops(CastleTown castle, ref Funds treasury)
    {
        ArgumentNullException.ThrowIfNull(castle);

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid || !troop.IsAllowUpgrade)
            {
                continue;
            }

            if (castle.Race != troop.GetRace())
            {
                continue;
            }

            if (!castle.IsBuild(DwellingMonsters.GetDwelling(troop.GetUpgrade())))
            {
                continue;
            }

            Funds payment = troop.GetTotalUpgradeCost();

            if (treasury < payment)
            {
                continue;
            }

            treasury -= payment;
            treasury.Trim();

            troop.Upgrade();
        }
    }

    /// <summary>The index of the stack of a monster in a dynamic container, or <c>-1</c>.</summary>
    /// <param name="troops">The container to search.</param>
    /// <param name="monster">The monster to look for.</param>
    /// <returns>The index, or <c>-1</c>.</returns>
    private static int IndexOfMonster(List<Troop> troops, MonsterType monster)
        => troops.FindIndex(troop => troop.IsMonster(monster));
}
