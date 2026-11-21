using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Battle;

/// <summary>
/// One side of a battle: an army deployed onto the board, <c>Battle::Force</c> (<c>battle_army.h:120-260</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>Force</c> 同时继承 <c>Units</c>（一堆单位）与 <c>BitModes</c>（两个「撤退/投降过」的标记）。
/// 端口只继承 <see cref="BattleUnits"/>：那两位标记只有在算战斗结果与经验时才有意义，等 <c>battle_result</c> 落地再加。
/// </para>
/// <para>
/// 摆位公式逐字照抄 <c>battle_army.cpp:80-105</c>：兵按槽位 <c>i</c> 排成竖列，列间距 11 格（铺开阵型时 22 格）；
/// 镜像阵（<c>opposite</c>，也就是屏幕上方那一侧）整体右移 10 格，宽单位再各让一格（上侧 9、下侧 1），
/// 好让它们占住相邻两格而不是压在邻居身上。
/// </para>
/// <para>
/// 参考把每个单位的 UID 记在 <c>uids</c> 里，因为它要在「原始军队」和「场上单位」之间来回对号：
/// 清点伤亡、算经验、算投降价、战后写回。端口照做，<c>0</c> 表示该槽位本来是空的。
/// </para>
/// </remarks>
public sealed class BattleForce : BattleUnits
{
    private readonly GameArmy _army;
    private readonly List<uint> _uids = new(GameArmy.MaximumTroopCount);

    /// <summary>
    /// Deploys an army onto the board, <c>Force::Force()</c>.
    /// </summary>
    /// <param name="army">The army to deploy; its slots stay the source of truth for the counts.</param>
    /// <param name="board">The battlefield to place the stacks on.</param>
    /// <param name="opposite">
    /// Whether this is the mirror side (the one drawn at the top of the screen), <c>reflect</c>.
    /// </param>
    /// <param name="generator">The id source; one per battle, shared by both forces.</param>
    public BattleForce(GameArmy army, BattleBoardCells board, bool opposite, BattleTroopsUidGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(generator);

        _army = army;

        for (int index = 0; index < GameArmy.MaximumTroopCount; ++index)
        {
            Troop troop = _army.GetTroop(index);

            if (!troop.IsValid)
            {
                _uids.Add(0);
                continue;
            }

            bool wide = MonsterAbilities.IsWide(troop.Monster);
            int cell = _army.IsSpreadFormation ? index * 22 : 22 + index * 11;

            if (opposite)
            {
                cell += wide ? 9 : 10;
            }
            else if (wide)
            {
                cell += 1;
            }

            BattlePosition position = new(board, cell, wide, opposite);
            BattleUnit unit = new(troop, position, opposite, generator.GetUnique())
            {
                Army = _army,
                Color = _army.Color,
            };

            Add(unit);
            _uids.Add(unit.Uid);
        }
    }

    /// <summary>The army the force was deployed from, <c>Force::GetCommander()</c>'s source.</summary>
    public GameArmy OriginalArmy => _army;

    /// <summary>The hero or captain commanding the force, <c>Force::GetCommander()</c>; may be <see langword="null"/>.</summary>
    public IArmyCommander? Commander => _army.Commander;

    /// <summary>The force's side, <c>Force::GetColor()</c>.</summary>
    public PlayerColor Color => _army.Color;

    /// <summary>Who commands the force, <c>Force::GetControl()</c>.</summary>
    public PlayerControl GetControl() => _army.GetControl();

    /// <summary>
    /// Whether anybody still stands, <c>Force::isValid()</c>.
    /// </summary>
    /// <param name="considerBattlefieldArmy">
    /// <see langword="true"/> (the default) counts the stacks on the board; <see langword="false"/> counts the army's
    /// slots instead — the question "would the army still exist if nothing had been summoned or dissolved".
    /// </param>
    /// <remarks>
    /// 参考在 <c>considerBattlefieldArmy == false</c> 时走 <c>uids</c>：对每个还非空的原始槽位找出场上单位，
    /// 找不到（比如整队被镜像顶掉）就算没了。这条分支用于「军队是否还存在」，不是「场上还有没有人」。
    /// </remarks>
    public bool IsValid(bool considerBattlefieldArmy = true)
    {
        if (considerBattlefieldArmy)
        {
            foreach (BattleUnit unit in this)
            {
                if (unit.IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        for (int index = 0; index < GameArmy.MaximumTroopCount; ++index)
        {
            if (!_army.GetTroop(index).IsValid)
            {
                continue;
            }

            BattleUnit? unit = FindUid(_uids[index]);

            if (unit != null && unit.Dead < unit.MaxCount)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the army had a stack of a given creature, <c>Force::HasMonster()</c>.
    /// </summary>
    /// <param name="monster">The creature to look for.</param>
    /// <remarks>问的是**场上**的单位，不管它还剩几个人——刚刚死光但还没被清出场的那一帧也算。</remarks>
    public bool HasMonster(MonsterType monster)
    {
        foreach (BattleUnit unit in this)
        {
            if (unit.IsMonster(monster))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every creature that died on this side, <c>Force::GetTotalNumberOfDeadUnits()</c>.</summary>
    /// <remarks>
    /// 参考的两条分支（桥边/别处）在端口里是同一个算法：死亡数早就累计在单位上，没有「尸体没落地时另算」这回事。
    /// </remarks>
    public uint GetTotalNumberOfDeadUnits()
    {
        uint result = 0;

        foreach (BattleUnit unit in this)
        {
            result += unit.Dead;
        }

        return result;
    }

    /// <summary>
    /// How many of the dead the necromancer raises, <c>Force::CalculateNumberOfDeadUnitsForNecromancy()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_army.cpp:221-231</c>。只有「这场战斗里真的死掉的人」算数——即死亡数与本场初始人数的较小者，
    /// 这样复活/吸血搞出来的额外死亡不会凭空变成骷髅。
    /// </remarks>
    public uint CalculateNumberOfDeadUnitsForNecromancy()
    {
        uint result = 0;

        foreach (BattleUnit unit in this)
        {
            result += Math.Min(unit.InitialCount, unit.Dead);
        }

        return result;
    }

    /// <summary>
    /// Experience the other side earns for killing this force, <c>Force::CalculateExperienceBasedOnLosses()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_army.cpp:233-244</c>。每个单位的单体血量乘「本场死掉的人数」（同样夹在初始人数以内）。
    /// 注意用的是 <b>Monster::GetHitPoints</b> 而不是兵种在战斗里的血量加成——经验按兵种表算。
    /// </remarks>
    public uint CalculateExperienceBasedOnLosses()
    {
        uint result = 0;

        ApplyToTroopsFromOriginalArmy((_, unit) =>
            result += (uint)MonsterStatistics.GetBattleStats(unit.Monster).HitPoints
                      * Math.Min(unit.InitialCount, unit.Dead));

        return result;
    }

    /// <summary>
    /// What the force would have to pay to buy its survivors back, <c>Force::GetSurrenderCost()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>battle_army.cpp:186-217</c>。三步：先按每个还活着的单位累加「一个兵 × 剩下的人数」；
    /// 再乘指挥官的折扣——宝物把基数从 0.5 抬起、外交术再乘一档；最后四舍五入，且<b>低于 1 一律按 1</b>
    /// （投降没有白送的）。
    /// </para>
    /// <para>
    /// 宝物折扣是「连乘」：<c>mod = mod * value / 100</c>，每件都按百分比折一次（参考里那一行刻意不写
    /// <c>/ 100.0</c> 的括号，端口按它的实际意图算浮点）。
    /// </para>
    /// </remarks>
    public uint GetSurrenderCost()
    {
        double result = 0;

        for (int index = 0; index < GameArmy.MaximumTroopCount; ++index)
        {
            if (!_army.GetTroop(index).IsValid)
            {
                continue;
            }

            BattleUnit? unit = FindUid(_uids[index]);

            if (unit == null || !unit.IsValid)
            {
                continue;
            }

            result += unit.GetSurrenderCost().Gold;
        }

        IArmyCommander? commander = Commander;

        if (commander != null)
        {
            IReadOnlyList<int> reductions = commander.BagArtifacts.GetTotalMultipliedPercent(
                ArtifactBonusType.SurrenderCostReductionPercent);

            // 没有折扣宝物时基数是一半——原文就是这么给的，不是 1。
            double modifier = 0.5;

            if (reductions.Count > 0)
            {
                modifier = 1;

                foreach (int reduction in reductions)
                {
                    modifier = modifier * reduction / 100;
                }
            }

            SkillLevel diplomacy = commander.GetLevelSkill(SecondarySkill.Diplomacy);

            if (diplomacy > SkillLevel.None)
            {
                modifier *= 2;
                modifier *= SecondarySkills.GetDiplomacySurrenderCostDiscount(diplomacy) / 100.0;
            }

            result *= modifier;
        }

        return result >= 1 ? (uint)(result + 0.5) : 1;
    }

    /// <summary>
    /// Writes the survivors back into the army, <c>Force::syncOriginalArmy()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_army.cpp:246-258</c>。全灭的槽位清成空（人数 0、兵种保留），其余写回
    /// <c>max - dead</c>。战后 <c>battle_main</c> 靠这一步把战场还给冒险地图。
    /// </remarks>
    public void SyncOriginalArmy()
    {
        ApplyToTroopsFromOriginalArmy(static (troop, unit) =>
            troop.Count = unit.Dead >= unit.MaxCount ? 0 : unit.MaxCount - unit.Dead);
    }

    /// <summary>
    /// Starts a new round for the whole force, <c>Force::NewTurn()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_army.cpp:210-219</c>。先让指挥官重新能施法，再逐个 <c>Unit::NewTurn()</c>
    /// （补再生、清回合标记、法术时长走一格）。
    /// </remarks>
    public void NewTurn()
    {
        Commander?.ResetModes(HeroModes.Spellcasted);

        foreach (BattleUnit unit in this)
        {
            unit.NewTurn();
        }
    }

    /// <summary>
    /// Runs an action for every army slot that went to battle, paired with its battle unit.
    /// </summary>
    /// <param name="action">The action; the slot and the unit always match.</param>
    /// <remarks>
    /// <para>
    /// 参考里的 <c>_applyActionToTroopsFromOriginalArmy</c>（<c>battle_army.h</c>）是个模板小函数，端口写成私有方法加
    /// 委托——它只服务本类的几处统计，不值得给它一个公共的泛型形状。
    /// </para>
    /// <para>
    /// 参考会先跳过「空槽位」（<c>!troop-&gt;isValid()</c>），端口<b>不能照抄</b>：参考的战斗单位是原始兵堆的副本，
    /// 而端口的 <see cref="BattleUnit"/> 直接持有 <see cref="GameArmy"/> 的 <see cref="Troop"/>，一开打原始兵堆的人数就
    /// 被改小了。若在这里按 <c>troop.IsValid</c> 过滤，全灭的兵堆会被当成「本来就没上场」，于是
    /// <see cref="CalculateExperienceBasedOnLosses"/> 少算经验、<see cref="SyncOriginalArmy"/> 漏写槽位。
    /// 判据改成「有没有对应的战斗单位」——那才是真的上过场。
    /// </para>
    /// </remarks>
    private void ApplyToTroopsFromOriginalArmy(Action<Troop, BattleUnit> action)
    {
        for (int index = 0; index < GameArmy.MaximumTroopCount; ++index)
        {
            BattleUnit? unit = FindUid(_uids[index]);

            if (unit != null)
            {
                action(_army.GetTroop(index), unit);
            }
        }
    }
}
