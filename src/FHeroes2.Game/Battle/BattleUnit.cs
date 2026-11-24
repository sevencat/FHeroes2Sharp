using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Battle;

/// <summary>
/// One unit on the battlefield, <c>Battle::Unit</c> (<c>battle_troop.h:96-390</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>Unit : ArmyTroop : Troop : Monster</c> 四层继承。端口里 <see cref="Troop"/> 已经在（兵力 + 战斗属性），
/// 但 <c>ArmyTroop</c>（带所属军队与英雄）与 <c>Monster</c>（实例级别名）还没落地，于是单位**持有一个**
/// <see cref="Troop"/> 而不是继承它：单位要改的是自己的兵力，不该写回军队里那份。
/// </para>
/// <para>
/// 已经落地的：位置（含朝向）、兵力与血量的算术、模式位、速度、能力判定，以及整套伤害算术
/// （<c>GetAttack</c>/<c>GetDefense</c>、<c>CalculateDamageUnit</c>、<c>GetDamage</c>、
/// <c>HowManyWillBeKilled</c>、<c>ApplyDamage</c>、<c>Resurrect</c>、<c>NewTurn</c>、<c>PostKilledAction</c>）。
/// 竞技场那一侧的四件事——护城河、城墙后的射击惩罚、英雄的箭术、催眠后的反向颜色——收进
/// <see cref="IBattleField"/>，由将来的 <c>BattleArena</c> 实现；引用为空时按「没有这些加成」处理。
/// </para>
/// <para>
/// 法术应用层也在这一层：<c>AllowApplySpell</c>/<c>ApplySpell</c> 分派到伤害、治疗复活、模式效果三条路，
/// <c>GetMagicResist</c> 汇总反魔、无意义的施法、宝物免疫与怪物抗魔。施法者走 <see cref="IArmyCommander"/>，
/// 竞技场那侧的「选目标」不在这一层。坟场已经接上（<see cref="PostKilledAction"/> 会登记尸体），
/// 士气与运气也接上了军队与指挥官。
/// </para>
/// <para>
/// 模式的存储直接复用 <see cref="BitModes"/> 的 <c>uint</c> 标志字：参考的 <c>Unit::isModes()</c> 与
/// <c>BitModes::Modes()</c> 是同一件事。
/// </para>
/// </remarks>
public sealed class BattleUnit : BitModes
{
    private readonly Troop _troop;
    private readonly BattlePosition _position;
    private readonly BattleModesAffected _affected = new();
    private readonly uint _uid;
    private readonly uint _initialCount;
    private uint _maxCount;
    private uint _deadCount = 0;
    private uint _hitPoints;
    private uint _shotsLeft;
    private uint _disruptingRaysNum;
    private bool _isReflected;
    private bool _blindRetaliation;
    private BattleUnit? _mirrorUnit;
    private IBattleField? _field;
    private BattleGraveyard? _graveyard;
    private GameArmy? _army;

    /// <summary>Creates a unit out of a troop and places it on the battlefield.</summary>
    /// <param name="troop">The troop: what kind of creature and how many.</param>
    /// <param name="position">The position to stand on; it is filled by this constructor.</param>
    /// <param name="isReflected">Whether the unit faces left.</param>
    /// <param name="uid">The unique id the battle gives the unit.</param>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:443-470</c>。血量按「整队满血」起算，箭矢按兵种给的初值，死亡数从零开始；
    /// 随后 <c>SetPosition</c> 把格子上的 <c>Unit *</c> 指过来。
    /// </remarks>
    public BattleUnit(Troop troop, BattlePosition position, bool isReflected, uint uid)
    {
        _troop = troop;
        _position = position;
        _uid = uid;
        _initialCount = troop.Count;
        _maxCount = troop.Count;
        _hitPoints = troop.GetHitPoints();
        _shotsLeft = (uint)MonsterStatistics.GetBattleStats(troop.Monster).Shots;
        _isReflected = isReflected;

        SetPosition(position);
    }

    /// <summary>The troop behind the unit: its creature type and its count.</summary>
    public Troop Troop => _troop;

    /// <summary>The unit's creature, <c>Monster::GetID()</c>.</summary>
    public MonsterType Monster => _troop.Monster;

    /// <summary>How many creatures are left, <c>Troop::GetCount()</c>.</summary>
    public uint Count => _troop.Count;

    /// <summary>The unique id within the battle, <c>Unit::GetUID()</c>.</summary>
    public uint Uid => _uid;

    /// <summary>The unit's position, <c>Unit::GetPosition()</c>.</summary>
    public BattlePosition Position => _position;

    /// <summary>The head cell's index, <c>Unit::GetHeadIndex()</c>; <c>-1</c> when the unit is off the board.</summary>
    public int HeadIndex => _position.HeadIndex;

    /// <summary>The tail cell's index, <c>Unit::GetTailIndex()</c>; <c>-1</c> for a one-hex unit.</summary>
    public int TailIndex => _position.TailIndex;

    /// <summary>Whether the unit occupies two cells, <c>Monster::isWide()</c>.</summary>
    public bool IsWide => MonsterAbilities.IsWide(Monster);

    /// <summary>Whether the unit faces left, <c>Unit::isReflect()</c>.</summary>
    public bool IsReflect => _isReflected;

    /// <summary>The unit this one mirrors, <c>Unit::GetMirror()</c>; null when there is none.</summary>
    public BattleUnit? MirrorUnit
    {
        get => _mirrorUnit;
        set => _mirrorUnit = value;
    }

    /// <summary>The count the unit started the battle with, <c>Unit::GetInitialCount()</c>.</summary>
    public uint InitialCount => _initialCount;

    /// <summary>The most creatures this unit ever had, <c>Unit::GetMaxCount()</c>; resurrection cannot exceed it.</summary>
    public uint MaxCount => _maxCount;

    /// <summary>How many creatures have died, <c>Unit::GetDead()</c>.</summary>
    public uint Dead => _deadCount;

    /// <summary>The hit points left, as a total over the whole stack, <c>Unit::GetHitPoints()</c>.</summary>
    public uint HitPoints => _hitPoints;

    /// <summary>The arrows left, <c>Unit::GetShots()</c>.</summary>
    public uint GetShots() => _shotsLeft;

    /// <summary>
    /// The ammunition as the reference prints it, <c>Unit::GetShotString()</c> (<c>battle_troop.cpp:225-235</c>).
    /// </summary>
    /// <remarks>满弹只报存量；打掉过就写成「初始 (剩余)」，界面与 AI 共用这一种写法。</remarks>
    /// <returns>The shot count, or the initial and remaining counts.</returns>
    public string GetShotString()
    {
        int initialShots = _troop.GetShots();

        return initialShots == (int)_shotsLeft ? initialShots.ToString() : $"{initialShots} ({_shotsLeft})";
    }

    /// <summary>Whether this particular unit is the one with the id, <c>Unit::isUID()</c>.</summary>
    /// <param name="uid">The id to test.</param>
    public bool IsUid(uint uid) => _uid == uid;

    /// <summary>Whether the unit is made of a given creature, <c>Unit::isMonster()</c>.</summary>
    /// <param name="monster">The creature to test.</param>
    public bool IsMonster(MonsterType monster) => Monster == monster;

    /// <summary>Cost of one creature of this unit, <c>Monster::GetCost()</c>.</summary>
    /// <remarks>单位自己不存价钱，问的是兵种表——参考里这一层继承自 <c>Troop</c>。</remarks>
    public Funds GetCost() => MonsterStatistics.GetGeneralStats(Monster).GetCost();

    /// <summary>
    /// What buying the survivors back would cost, <c>Unit::GetSurrenderCost()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:1163-1167</c>。只有「现在还能站着的人」算钱，所以是
    /// <c>GetCost() * ( dead &gt; max ? 0 : max - dead )</c>——注意是 <c>&gt;</c> 不是 <c>&gt;=</c>：被吸血的单位
    /// 死亡数可能超过上限，那时才该归零。
    /// </remarks>
    public Funds GetSurrenderCost()
    {
        uint survivors = Dead > MaxCount ? 0 : MaxCount - Dead;

        return GetCost() * survivors;
    }

    /// <summary>
    /// Who commands this unit's side, <c>Unit::GetControl()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:1169-1172</c>。没有所属军队（召唤出来的兵）时是电脑。
    /// </remarks>
    public PlayerControl GetControl() => _army?.GetControl() ?? PlayerControl.Ai;

    /// <summary>
    /// Who commands the unit as it stands right now, <c>Unit::GetCurrentControl()</c>: after hypnosis the unit answers
    /// with its new owner's control.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:1174-1177</c>。颜色没变时问所属军队，变了就问 <c>Players</c>——被催眠的兵
    /// 现在听对面玩家的。狂暴（没有颜色）走 <see cref="GetControl"/>。
    /// </remarks>
    public PlayerControl GetCurrentControl()
    {
        PlayerColor current = GetCurrentColor();

        return current == PlayerColor.None ? GetControl() : Players.GetPlayerControl(current);
    }

    /// <summary>
    /// The morale the unit fights with, <c>Unit::GetMorale()</c> (<c>battle_troop.cpp:276-289</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：士气先问所属军队（<c>ArmyTroop::GetMorale()</c>），没有军队或兵种不受士气影响时按兵种默认值 0。
    /// 然后一条特例：能受士气影响的兵种，在敌方军队里有骨龙时再减一格，但不会掉到
    /// <see cref="Morale.Treason"/>（参考里那句 <c>armyTroopMorale &gt; Morale::TREASON</c>）。
    /// </para>
    /// <para>
    /// 骨龙要看的是**对面**的军队，那是竞技场才知道的事，于是走 <see cref="IBattleField.EnemyHasMonster"/>。
    /// 没有竞技场时按「没有骨龙」处理。
    /// </para>
    /// </remarks>
    /// <returns>The morale, already inside its scale.</returns>
    public int GetMorale()
    {
        int morale = _army != null && MonsterAbilities.IsAffectedByMorale(Monster) ? _army.GetMorale() : Morale.Normal;

        if (MonsterAbilities.IsAffectedByMorale(Monster)
            && _field?.EnemyHasMonster(_army?.Color ?? PlayerColor.None, MonsterType.BoneDragon) == true
            && morale > Morale.Treason)
        {
            --morale;
        }

        return morale;
    }

    /// <summary>
    /// The luck the unit fights with, <c>Unit::GetLuck()</c> = <c>ArmyTroop::GetLuck()</c>
    /// (<c>army_troop.cpp:178-181</c>).
    /// </summary>
    /// <remarks>
    /// 运气没有骨龙那样的例外：有军队问军队，没有军队问兵种默认值 0。兵种表里没有「不受运气影响」的旗标，
    /// 所以这里不看 <see cref="MonsterAbilities.IsAffectedByMorale"/>。
    /// </remarks>
    /// <returns>The luck, already inside its scale.</returns>
    public int GetLuck() => _army?.GetLuck() ?? Luck.Normal;

    /// <summary>
    /// Rolls the unit's morale for the round and sets <see cref="BattleMonsterState.MoraleGood"/> or
    /// <see cref="BattleMonsterState.MoraleBad"/>, <c>Unit::SetRandomMorale()</c>
    /// (<c>battle_troop.cpp:301-314</c>).
    /// </summary>
    /// <param name="randomGenerator">The battle's random source.</param>
    /// <remarks>
    /// <para>
    /// 好士气是 1..24 里掷中 <c>&lt;= morale</c>；坏士气是 1..12。坏士气多一道闸：电脑的兵是随机拼出来的，
    /// 给它 25% 的概率躲过去（参考注释原话）；人类的兵照掷不误。
    /// </para>
    /// <para>
    /// 两次掷骰的顺序与短路与参考一致：士气为正时根本不掷第二次（<c>else if</c>），这样随机序列不会因为
    /// 分支写法不同而错位。
    /// </para>
    /// </remarks>
    public void SetRandomMorale(Pcg32 randomGenerator)
    {
        int morale = GetMorale();

        if (morale > 0 && (int)Rand.GetWithGen(1, 24, randomGenerator) <= morale)
        {
            SetModes(BattleMonsterState.MoraleGood);
        }
        else if (morale < 0 && (int)Rand.GetWithGen(1, 12, randomGenerator) <= -morale)
        {
            if (GetCurrentControl().IsHuman() || Rand.GetWithGen(1, 4, randomGenerator) != 1)
            {
                SetModes(BattleMonsterState.MoraleBad);
            }
        }
    }

    /// <summary>
    /// Rolls the unit's luck for the round and sets <see cref="BattleMonsterState.LuckGood"/> or
    /// <see cref="BattleMonsterState.LuckBad"/>, <c>Unit::SetRandomLuck()</c>
    /// (<c>battle_troop.cpp:316-329</c>).
    /// </summary>
    /// <param name="randomGenerator">The battle's random source.</param>
    /// <remarks>
    /// 参考只掷一次 1..24（不像士气那样两处用不同的上限），好运气吃 <c>&lt;= luck</c>、坏运气吃
    /// <c>&lt;= -luck</c>。祈祷/诅咒与运气叠着算，这里不做互斥。
    /// </remarks>
    public void SetRandomLuck(Pcg32 randomGenerator)
    {
        int luck = GetLuck();
        int chance = (int)Rand.GetWithGen(1, 24, randomGenerator);

        if (luck > 0 && chance <= luck)
        {
            SetModes(BattleMonsterState.LuckGood);
        }
        else if (luck < 0 && chance <= -luck)
        {
            SetModes(BattleMonsterState.LuckBad);
        }
    }

    /// <summary>
    /// Whether the unit can fly, <c>Unit::isFlying()</c>: a flying creature that is not paralyzed nor blinded.
    /// </summary>
    /// <remarks>
    /// 参考：<c>Monster::isFlying() &amp;&amp; !Modes( SP_BLIND | IS_PARALYZE_MAGIC )</c>——被定住或被致盲的飞行
    /// 单位落在地上，走路而不是飞过去。
    /// </remarks>
    public bool IsFlying => MonsterAbilities.IsFlying(Monster) && !HasModes(BattleMonsterState.SpellBlind | BattleMonsterState.ParalyzeMagic);

    /// <summary>
    /// Whether the unit strikes twice, <c>Unit::isDoubleAttack()</c>: either in melee or with arrows.
    /// </summary>
    public bool IsDoubleAttack =>
        MonsterAbilities.IsPresent(Monster, MonsterAbility.DoubleMeleeAttack) ||
        MonsterAbilities.IsPresent(Monster, MonsterAbility.DoubleShooting);

    /// <summary>
    /// Whether the unit may still retaliate, <c>Unit::isRetaliationAllowed()</c>.
    /// </summary>
    /// <remarks>无限反击的兵种不看标记，其余的单位一个回合只有一次反击。</remarks>
    public bool IsRetaliationAllowed =>
        MonsterAbilities.IsPresent(Monster, MonsterAbility.UnlimitedRetaliation) ||
        !HasModes(BattleMonsterState.Retaliated);

    /// <summary>
    /// Whether the unit's blows cannot be retaliated against, <c>Unit::isIgnoringRetaliation()</c>.
    /// </summary>
    /// <remarks>参考：<c>Monster::isIgnoringRetaliation()</c>，能力位 <c>NO_ENEMY_RETALIATION</c>。</remarks>
    public bool IsIgnoringRetaliation => MonsterAbilities.IsPresent(Monster, MonsterAbility.NoEnemyRetaliation);

    /// <summary>
    /// Marks this turn's retaliation as used up, <c>Unit::setRetaliationAsCompleted()</c>.
    /// </summary>
    /// <remarks>置 <c>TR_RETALIATED</c>；除了无限反击的兵种，其余单位本回合不再还手。</remarks>
    public void SetRetaliationCompleted() => SetModes(BattleMonsterState.Retaliated);

    /// <summary>
    /// What a unit does right after its blow lands, <c>Unit::PostAttackAction()</c> (<c>battle_troop.cpp:943-956</c>).
    /// </summary>
    /// <param name="enemy">The unit that was struck; decides whether the blow was a shot or a melee hit.</param>
    /// <remarks>
    /// <para>
    /// 两件事：射击消耗一支箭（除非在近战，或指挥官带着「无尽弹药」），以及清掉这一击带来的一次性运气。
    /// 参考在这里断言「塔上的射手不耗箭、且箭还没打光」；端口照抄成 <see cref="Debug.Assert(bool)"/>，
    /// 打光箭仍然射击属于算术错误而不是可恢复状态。
    /// </para>
    /// <para>
    /// 改的是单位自己的箭数，不回写军队；战后由 <c>BattleForce.ApplyToTroopsFromOriginalArmy</c> 统一同步。
    /// </para>
    /// </remarks>
    public void PostAttackAction(BattleUnit enemy)
    {
        if (IsArchers && !IsHandFighting(this, enemy))
        {
            IArmyCommander? commander = GetCommander();

            if (commander == null || !commander.BagArtifacts.IsBonusPresent(ArtifactBonusType.EndlessAmmunition))
            {
                Debug.Assert(!HasModes(BattleMonsterState.CapTower) && _shotsLeft > 0, "A shooter fired with no arrows left.");

                --_shotsLeft;
            }
        }

        ResetModes(BattleMonsterState.LuckGood | BattleMonsterState.LuckBad);
    }

    /// <summary>
    /// Whether the unit hits both of the cells in front of it, <c>Monster::isDoubleCellAttack()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>monster.h:267-270</c>，能力位 <c>TWO_CELL_MELEE_ATTACK</c>。与 <see cref="IsDoubleAttack"/>
    /// （打两次）不是一回事，AI 用它决定要不要为了「顺带打第二个」挑攻击方向。
    /// </remarks>
    public bool IsDoubleCellAttack => MonsterAbilities.IsPresent(Monster, MonsterAbility.TwoCellMeleeAttack);

    /// <summary>
    /// Whether the unit hits every cell around it, <c>Monster::isAllAdjacentCellsAttack()</c>.
    /// </summary>
    /// <remarks>参考：<c>monster.h:272-275</c>，能力位 <c>ALL_ADJACENT_CELL_MELEE_ATTACK</c>。</remarks>
    public bool IsAllAdjacentCellsAttack => MonsterAbilities.IsPresent(Monster, MonsterAbility.AllAdjacentCellMeleeAttack);

    /// <summary>
    /// How many hit points the unit has lost against its full strength,
    /// <c>Unit::GetMissingHitPoints()</c> (<c>battle_troop.cpp</c>).
    /// </summary>
    /// <remarks>满员血量按「满编人数 × 单个兵的血」算，与参考的 <c>_maxCount * Monster::GetHitPoints()</c> 一致。</remarks>
    public uint MissingHitPoints
    {
        get
        {
            uint total = MaxCount * (uint)MonsterStatistics.GetBattleStats(Monster).HitPoints;

            Debug.Assert(total >= _hitPoints, "The current hit points never exceed the full count's.");

            return total - _hitPoints;
        }
    }

    /// <summary>
    /// Whether the unit heals itself at the start of its turn, <c>Monster::isRegenerating()</c>.
    /// </summary>
    public bool IsRegenerating => MonsterAbilities.IsPresent(Monster, MonsterAbility.HpRegeneration);

    /// <summary>
    /// Whether the unit cannot move at all, <c>Unit::isImmovable()</c>: blinded or paralyzed/petrified.
    /// </summary>
    public bool IsImmovable => HasModes(BattleMonsterState.SpellBlind | BattleMonsterState.ParalyzeMagic);

    /// <summary>
    /// The speed the unit currently has, <c>Unit::GetSpeed()</c>.
    /// </summary>
    /// <returns>The speed, <see cref="Speed.Standing"/> for a unit that cannot act.</returns>
    public uint GetSpeed() => GetSpeed(false, false);

    /// <summary>
    /// The speed the unit currently has, with the two extra checks of <c>Unit::GetSpeed( skipStandingCheck, skipMovedCheck )</c>.
    /// </summary>
    /// <param name="skipStandingCheck">
    /// When false, a dead, blinded or paralyzed unit answers <see cref="Speed.Standing"/>; a unit that already
    /// acted is included unless <paramref name="skipMovedCheck"/> is set too.
    /// </param>
    /// <param name="skipMovedCheck">Whether an already-acted unit still reports its speed.</param>
    /// <returns>The speed after haste or slow.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:424-447</c>。三个「不能动」的判据被打包成一个掩码：致盲与定身永远算，
    /// <c>TR_MOVED</c> 只有 <paramref name="skipMovedCheck"/> 为假时才并入。两个开关都置位时得到的是纯数据速度。
    /// 加速先判、减速后判仍是互斥的——两者同时存在时参考取加速。
    /// </remarks>
    public uint GetSpeed(bool skipStandingCheck, bool skipMovedCheck)
    {
        if (!skipStandingCheck)
        {
            uint modesToCheck = BattleMonsterState.SpellBlind | BattleMonsterState.ParalyzeMagic;

            if (!skipMovedCheck)
            {
                modesToCheck |= BattleMonsterState.Moved;
            }

            if (Count == 0 || HasModes(modesToCheck))
            {
                return Speed.Standing;
            }
        }

        uint speed = (uint)MonsterStatistics.GetBattleStats(Monster).Speed;

        if (HasModes(BattleMonsterState.SpellHaste))
        {
            return Speed.GetHasteSpeedFromSpell(speed);
        }

        if (HasModes(BattleMonsterState.SpellSlow))
        {
            return Speed.GetSlowSpeedFromSpell(speed);
        }

        return speed;
    }

    /// <summary>
    /// The speed as the reference prints it, <c>Unit::GetSpeedString()</c> (<c>battle_troop.cpp:240-244</c>).
    /// </summary>
    /// <remarks>读的是数据速度：不因「已经行动过」「被致盲」而变成 <see cref="Speed.Standing"/>。</remarks>
    /// <returns>The speed in the reference's wording, e.g. «Average» or «Very Fast».</returns>
    public string GetSpeedString() => FHeroes2.Game.Army.Troop.GetSpeedString(GetSpeed(true, false));

    /// <summary>
    /// Moves the unit's head to a cell, <c>Unit::SetPosition( int32_t index )</c>.
    /// </summary>
    /// <param name="index">The cell to stand on.</param>
    /// <remarks>
    /// 先把手上的两格清空（只清自己占着的那种），再按宽窄与朝向重新占位。参考：<c>battle_troop.cpp:341-360</c>。
    /// </remarks>
    public void SetPosition(int index)
    {
        ReleaseCells();
        _position.Set(index, IsWide, _isReflected);
        OccupyCells();
    }

    /// <summary>Copies a whole position, <c>Unit::SetPosition( const Position &amp; )</c>.</summary>
    /// <param name="position">The position to take; its two cells are copied, not referenced.</param>
    public void SetPosition(BattlePosition position)
    {
        ReleaseCells();
        _position.CopyFrom(position);
        OccupyCells();
    }

    /// <summary>
    /// Turns the unit back to its side's default facing, <c>Unit::UpdateDirection()</c>
    /// (<c>battle_troop.cpp:196-202</c>): the attacker faces right, the defender faces left.
    /// </summary>
    /// <remarks>
    /// 参考在单位停下之后调它，把「追着谁打就朝谁」的临时朝向收回去。没有竞技场的单位（单测里手工造的那种）
    /// 不改朝向——参考在这里是 <c>assert( arena != nullptr )</c>，端口没有可断的东西。
    /// </remarks>
    public void UpdateDirection()
    {
        if (_field == null)
        {
            return;
        }

        SetReflection(_field.AttackingArmyColor != Color);
    }

    /// <summary>
    /// Turns the unit towards a rectangle, <c>Unit::UpdateDirection( const fheroes2::Rect &amp; )</c>
    /// (<c>battle_troop.cpp:204-219</c>).
    /// </summary>
    /// <param name="position">The rectangle of the cell(s) the unit is about to occupy.</param>
    /// <returns>True when the facing changed.</returns>
    /// <remarks>
    /// 只比较横坐标：目标在右边就朝右、在左边就朝左，同一列则保持原样。飞行单位落地前用它转身，好让翅膀与
    /// 落点对得上——参考把这个判断放在动画层，端口放在这里，因为朝向是单位的属性而不是画面的属性。
    /// </remarks>
    public bool UpdateDirection(Rect position)
    {
        Rect current = _position.GetRect();

        if (current.X == position.X)
        {
            return false;
        }

        bool isReflected = current.X > position.X;

        if (isReflected == _isReflected)
        {
            return false;
        }

        SetReflection(isReflected);

        return true;
    }

    /// <summary>
    /// Turns the unit around, <c>Unit::SetReflection()</c>.
    /// </summary>
    /// <param name="isReflected">Whether it should face left.</param>
    /// <remarks>
    /// 朝向变了，宽单位的两格也跟着挪：尾巴从右边换到左边，旧尾巴格必须交还出来。参考让
    /// <c>Position::Set()</c> 直接换掉两个 <c>Cell *</c>，那口旧格子上的 <c>Unit *</c> 由竞技场的重绘流程收拾；
    /// 端口把格子上的单位当成占位的唯一真相，所以这里显式释放再重新占位。
    /// </remarks>
    public void SetReflection(bool isReflected)
    {
        if (_isReflected == isReflected)
        {
            return;
        }

        int headIndex = _position.HeadIndex;

        _isReflected = isReflected;
        ReleaseCells();
        _position.Set(headIndex, IsWide, _isReflected);
        OccupyCells();
    }

    /// <summary>
    /// The army the unit was deployed from, <c>Unit::GetArmy()</c>; <see langword="null"/> for summoned stacks.
    /// </summary>
    /// <remarks>
    /// 参考通过 <c>ArmyTroop::GetColor()</c>/<c>GetControl()</c> 一路问回军队；端口把这条边存成一个可空属性，
    /// 颜色由 <see cref="BattleForce"/> 在部署时抄一份（战斗里阵营不变），命令权则实时问回去。
    /// </remarks>
    public GameArmy? Army
    {
        get => _army;
        set => _army = value;
    }

    /// <summary>The battlefield facts the unit cannot know by itself, or <see langword="null"/> when there is none.</summary>
    /// <remarks>为空时护城河、城墙后的射击惩罚、箭术加成与反向颜色全部按「没有」处理。</remarks>
    public IBattleField? Field
    {
        get => _field;
        set => _field = value;
    }

    /// <summary>
    /// Where the body goes when the stack dies, <c>Arena::GetGraveyard()</c>; <see langword="null"/> means nowhere.
    /// </summary>
    /// <remarks>
    /// 参考在 <c>PostKilledAction</c> 里断言坟场非空；端口允许为空，好让单测里单独造一个单位而不必先造战场。
    /// </remarks>
    public BattleGraveyard? Graveyard
    {
        get => _graveyard;
        set => _graveyard = value;
    }

    /// <summary>Which army the unit belongs to, <c>Unit::GetColor()</c>; set by the arena when the unit is placed.</summary>
    public PlayerColor Color { get; set; } = PlayerColor.None;

    /// <summary>The timed spell effects on the unit, <c>Unit::_affected</c>.</summary>
    public BattleModesAffected Affected => _affected;

    /// <summary>Whether the unit still stands, <c>Unit::isValid()</c>.</summary>
    public bool IsValid => _troop.IsValid;

    /// <summary>Whether the unit still has arrows to shoot, <c>Battle::Unit::isArchers()</c>.</summary>
    /// <remarks>
    /// 参考的 <c>isArchers()</c> 写在 <c>Monster</c> 里（<c>monster.h:252-255</c>：<c>GetShots() &gt; 0</c>），
    /// 而 <c>GetShots()</c> 是虚函数，<c>Battle::Unit</c> 把它重写成「剩余箭数」（<c>battle_troop.h:229-232</c>）。
    /// 于是这个判定在战场上问的是<b>箭还有没有</b>，不是「这个兵种会不会射」：打光箭的弓箭手照着兵种表
    /// 仍然算射手，参考的 <c>PostAttackAction()</c>（<c>battle_troop.cpp:945-951</c>）却会因此扣一支不存在的箭。
    /// </remarks>
    public bool IsArchers => _shotsLeft > 0;

    /// <summary>
    /// Which colour the unit currently fights for, <c>Unit::GetCurrentColor()</c>.
    /// </summary>
    /// <returns>
    /// <see cref="PlayerColor.None"/> when berserk (the colour is deliberately unknown), the other army's colour
    /// when hypnotized, and its own otherwise.
    /// </returns>
    /// <remarks>参考：<c>battle_troop.cpp:1639-1653</c>。被狂暴的单位谁都打，所以没有颜色；被催眠的站到对面去。</remarks>
    public PlayerColor GetCurrentColor()
    {
        if (HasModes(BattleMonsterState.SpellBerserker))
        {
            return PlayerColor.None;
        }

        if (HasModes(BattleMonsterState.SpellHypnotize))
        {
            return _field?.GetOppositeColor(Color) ?? PlayerColor.None;
        }

        return Color;
    }

    /// <summary>
    /// The colour the unit fights for, falling back to its army's, <c>Unit::GetCurrentOrArmyColor()</c>.
    /// </summary>
    /// <returns>The current colour, or the army's own when the unit is berserk and has none.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp</c> 的 <c>Unit::GetCurrentOrArmyColor()</c>。被狂暴的单位按「谁都打」处理，颜色是
    /// <see cref="PlayerColor.None"/>，但竞技场问「现在轮到哪一方」时不能没有答案，于是退回它所属军队的颜色。
    /// </remarks>
    public PlayerColor GetCurrentOrArmyColor()
    {
        PlayerColor currentColor = GetCurrentColor();

        return currentColor == PlayerColor.None ? Color : currentColor;
    }

    /// <summary>
    /// The commander the unit currently fights for, <c>Unit::GetCurrentOrArmyCommander()</c>
    /// (<c>battle_troop.cpp:1725-1735</c>).
    /// </summary>
    /// <returns>
    /// The hero or captain of <see cref="GetCurrentOrArmyColor"/>, or <see langword="null"/> when the unit has no arena
    /// or that side has no commander (a neutral army, a summoned elemental).
    /// </returns>
    /// <remarks>
    /// 与 <see cref="GetCommander"/> 的差别只在催眠与狂暴：那个答「它属于谁」，这个答「它此刻替谁打」。
    /// </remarks>
    public IArmyCommander? GetCurrentOrArmyCommander() => _field?.GetCommander(GetCurrentOrArmyColor());

    /// <summary>
    /// Whether the unit stands outside the castle walls, <c>Unit::isOutOfCastleWalls()</c>.
    /// </summary>
    /// <returns>True when the head cell — and the tail cell of a wide unit — is outside the wall line.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp</c> 的 <c>Unit::isOutOfCastleWalls()</c>，判据是格号小于闸门格（50）：城外 0..49，
    /// 城内 50..98。城墙的射击惩罚只对「城外射城内」生效，所以这个判定是它的前提之一。
    /// </remarks>
    public bool IsOutOfCastleWalls() =>
        BattleBoard.IsOutOfWallsIndex(HeadIndex) || (IsWide && TailIndex >= 0 && BattleBoard.IsOutOfWallsIndex(TailIndex));

    /// <summary>
    /// The point a shot leaves the unit from, <c>Unit::GetBackPoint()</c>.
    /// </summary>
    /// <returns>The middle of the unit's rear edge, in board pixels.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp</c> 的 <c>Unit::GetBackPoint()</c>——取单位矩形朝后方那条边的中点，用作弹道线的起点。
    /// 面朝左的单位后方在右侧，所以镜像时要取右边的中点。
    /// </remarks>
    public Point GetBackPoint()
    {
        Rect area = Position.GetRect();

        return Position.IsReflect
            ? new Point(area.Right, area.Y + (area.Height / 2))
            : new Point(area.X, area.Y + (area.Height / 2));
    }

    /// <summary>
    /// Whether the unit is locked in close combat, <c>Unit::isHandFighting()</c>: an enemy stands next to it.
    /// </summary>
    /// <returns>False for a tower and for a unit with no enemy around; true otherwise.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:341-364</c>。判据是「相邻格里有没有别的颜色的单位」——不论那格是头还是尾，
    /// 所以宽单位要把两格的邻格都算上。塔永远不算近战。
    /// </remarks>
    public bool IsHandFighting()
    {
        if (HasModes(BattleMonsterState.CapTower))
        {
            return false;
        }

        int[] around = IsWide && TailIndex >= 0
            ? BattleBoard.GetAroundIndexes(HeadIndex, TailIndex)
            : BattleBoard.GetAroundIndexes(HeadIndex);

        PlayerColor ownColor = GetCurrentColor();

        foreach (int index in around)
        {
            BattleUnit? nearby = _position.Board.GetCell(index)?.Unit;

            if (nearby != null && nearby.GetCurrentColor() != ownColor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a blow from one unit to another is a melee attack, <c>Unit::isHandFighting( attacker, defender )</c>.
    /// </summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit that is struck.</param>
    /// <returns>True when the two are exactly one step apart.</returns>
    /// <remarks>参考：<c>battle_troop.cpp:366-380</c>。相邻就是近战，隔开一格就是射击；塔两头都不算。</remarks>
    public static bool IsHandFighting(BattleUnit attacker, BattleUnit defender)
    {
        if (attacker.HasModes(BattleMonsterState.CapTower) || defender.HasModes(BattleMonsterState.CapTower))
        {
            return false;
        }

        return BattleBoard.GetDistance(attacker._position, defender._position) == 1;
    }

    /// <summary>
    /// The attack the unit fights with, <c>Unit::GetAttack()</c>: its own, the army commander's, plus bloodlust.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp</c> 的 <c>Unit::GetAttack()</c> 调 <c>ArmyTroop::GetAttack()</c>，后者是
    /// <c>Troop::GetAttack() + 指挥官的 GetAttack()</c>（<c>army_troop.cpp:158-161</c>）。这一项是塔的虚拟箭手拿到
    /// 英雄攻击力的唯一通道，也是英雄带的兵与地图上的野兵拉开差距的地方。
    /// </remarks>
    public uint GetAttack()
    {
        uint attack = (uint)(MonsterStatistics.GetBattleStats(Monster).Attack + (_army?.Commander?.GetAttack() ?? 0));

        if (HasModes(BattleMonsterState.SpellBloodlust))
        {
            attack += new Spell(SpellType.BloodLust).ExtraValue();
        }

        return attack;
    }

    /// <summary>
    /// The defense the unit fights with, <c>Unit::GetDefense()</c>: its own, plus stone skin or steel skin, minus
    /// disrupting rays and the moat.
    /// </summary>
    /// <returns>Never less than 1: neither the rays nor the moat can take the last point away.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:969-1005</c>。石化皮肤与钢铁皮肤是 <c>else if</c>——两者同时存在时只算前者；
    /// 护城河的伤害减免要城堡真的建了护城河，这半句由 <see cref="IBattleField.IsOnMoat"/> 回答。
    /// </remarks>
    public uint GetDefense()
    {
        uint defense = (uint)(MonsterStatistics.GetBattleStats(Monster).Defense + (_army?.Commander?.GetDefense() ?? 0));

        if (HasModes(BattleMonsterState.SpellStoneSkin))
        {
            defense += new Spell(SpellType.StoneSkin).ExtraValue();
        }
        else if (HasModes(BattleMonsterState.SpellSteelSkin))
        {
            defense += new Spell(SpellType.SteelSkin).ExtraValue();
        }

        if (_disruptingRaysNum > 0)
        {
            uint step = _disruptingRaysNum * new Spell(SpellType.DisruptingRay).ExtraValue();

            defense = step >= defense ? 1 : defense - step;
        }

        if (_field != null && _field.IsOnMoat(this))
        {
            uint step = (uint)GameStatic.BattleMoatReduceDefense;

            defense = step >= defense ? 1 : defense - step;
        }

        return defense;
    }

    /// <summary>
    /// The strength of the stack with a flat attack and defense bonus, <c>Troop::GetStrengthWithBonus()</c> as
    /// inherited by <c>Battle::Unit</c>.
    /// </summary>
    /// <remarks>
    /// The castle's towers are valued this way: <c>castle.cpp:2103</c> asks a center tower for its strength with its
    /// own attack bonus, which is how the AI prices a siege. The unit is not damaged or blessed in that call, so the
    /// plain troop formula is what the reference uses.
    /// </remarks>
    /// <param name="bonusAttack">Attack to add.</param>
    /// <param name="bonusDefense">Defense to add.</param>
    /// <returns>The strength of one creature times the count.</returns>
    public double GetStrengthWithBonus(int bonusAttack, int bonusDefense) => _troop.GetStrengthWithBonus(bonusAttack, bonusDefense);

    /// <summary>Counts one more disrupting ray, <c>++_disruptingRaysNum</c> in <c>ApplySpell</c>.</summary>
    /// <remarks>参考没有单列的接口，这个计数只是被法术应用层直接改；端口把它显式化，好测「每次扣 3 点防御」。</remarks>
    public void AddDisruptingRay() => ++_disruptingRaysNum;

    /// <summary>How many disrupting rays are on the unit, <c>Unit::_disruptingRaysNum</c>.</summary>
    public uint DisruptingRays => _disruptingRaysNum;

    /// <summary>
    /// Whether the next retaliation of this unit is the weak one of a blinded stack,
    /// <c>Unit::_blindRetaliation</c>.
    /// </summary>
    /// <remarks>由致盲的反击流程置位，<c>CalculateDamageUnit</c> 据此把伤害按致盲的减伤比例打折。</remarks>
    public bool BlindRetaliation
    {
        get => _blindRetaliation;
        set => _blindRetaliation = value;
    }

    /// <summary>
    /// How much this unit would hit back for after taking a blow,
    /// <c>Unit::EstimateRetaliatoryDamage()</c> (<c>battle_troop.cpp:449-491</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 伤害是「剩几个兵 × 单个兵的伤害」，**不走** <see cref="CalculateDamageUnit"/>——参考只想估个毛伤害，
    /// 攻防修正与各种加成留给调用方。
    /// </para>
    /// <para>
    /// 五种情况不反击：整队被打光、已经反击过、是镜像、被催眠（镜像与催眠本来也不会还手）、被定住
    /// （无限反击的兵种不看这一条）。被致盲的单位仍会反击，只是按致盲法术的减伤比例打折。
    /// </para>
    /// </remarks>
    /// <param name="damageTaken">The damage the incoming blow would deal.</param>
    /// <returns>The damage the retaliation would deal, 0 when there is no retaliation.</returns>
    public uint EstimateRetaliatoryDamage(uint damageTaken)
    {
        if (damageTaken >= _hitPoints)
        {
            return 0;
        }

        if (HasModes(BattleMonsterState.Retaliated | BattleMonsterState.CapMirrorImage | BattleMonsterState.SpellHypnotize))
        {
            return 0;
        }

        if (HasModes(BattleMonsterState.ParalyzeMagic) && !MonsterAbilities.IsPresent(Monster, MonsterAbility.UnlimitedRetaliation))
        {
            return 0;
        }

        uint unitsLeft = Troop.GetCountFromHitPoints(Monster, _hitPoints - damageTaken);
        Debug.Assert(unitsLeft > 0, "A stack that survives keeps at least one creature.");

        // 单个兵的伤害：参考显式限定 Monster:: 就是为了绕开 Troop:: 里乘数量的版本。
        uint damagePerCreature = HasModes(BattleMonsterState.SpellCurse)
            ? (uint)MonsterStatistics.GetBattleStats(Monster).DamageMin
            : HasModes(BattleMonsterState.SpellBless)
                ? (uint)MonsterStatistics.GetBattleStats(Monster).DamageMax
                : (uint)((MonsterStatistics.GetBattleStats(Monster).DamageMin + MonsterStatistics.GetBattleStats(Monster).DamageMax) / 2);

        uint retaliatoryDamage = unitsLeft * damagePerCreature;

        if (!HasModes(BattleMonsterState.SpellBlind))
        {
            return retaliatoryDamage;
        }

        uint reductionPercent = new Spell(SpellType.Blind).ExtraValue();
        Debug.Assert(reductionPercent <= 100, "The blind penalty is a percentage.");

        return retaliatoryDamage * (100 - reductionPercent) / 100;
    }

    /// <summary>The smallest damage the unit would deal to the enemy, <c>Unit::CalculateMinDamage()</c>.</summary>
    /// <param name="enemy">The unit that would be struck.</param>
    /// <param name="extraAttack">
    /// Attack on top of the unit's own, standing in for <c>Tower::GetAttack()</c>'s override; 0 for everything that is
    /// really a unit on the board.
    /// </param>
    public uint CalculateMinDamage(BattleUnit enemy, uint extraAttack = 0) =>
        CalculateDamageUnit(enemy, _troop.GetDamageMin(), extraAttack);

    /// <summary>The largest damage the unit would deal to the enemy, <c>Unit::CalculateMaxDamage()</c>.</summary>
    /// <param name="enemy">The unit that would be struck.</param>
    /// <param name="extraAttack">Attack on top of the unit's own; see <see cref="CalculateMinDamage"/>.</param>
    public uint CalculateMaxDamage(BattleUnit enemy, uint extraAttack = 0) =>
        CalculateDamageUnit(enemy, _troop.GetDamageMax(), extraAttack);

    /// <summary>
    /// The damage an average blow of this unit would deal to the enemy,
    /// <c>Unit::getPotentialDamage()</c> (<c>battle_troop.cpp:504-516</c>).
    /// </summary>
    /// <remarks>
    /// 不掷骰：诅咒取下限、祝福取上限，其余取上下限的中点。AI 拿它当「这一击值多少」的估量。
    /// </remarks>
    /// <param name="enemy">The unit that would be struck.</param>
    /// <returns>The damage of an average blow.</returns>
    public uint GetPotentialDamage(BattleUnit enemy)
    {
        if (HasModes(BattleMonsterState.SpellCurse))
        {
            return CalculateMinDamage(enemy);
        }

        if (HasModes(BattleMonsterState.SpellBless))
        {
            return CalculateMaxDamage(enemy);
        }

        return (CalculateMinDamage(enemy) + CalculateMaxDamage(enemy)) / 2;
    }

    /// <summary>
    /// The damage a given blow would be turned into, <c>Unit::CalculateDamageUnit()</c>.
    /// </summary>
    /// <param name="enemy">The unit that would be struck.</param>
    /// <param name="damage">The raw damage, usually <c>GetDamageMin()</c> or <c>GetDamageMax()</c> times a roll.</param>
    /// <param name="extraAttack">
    /// Attack added to this unit's own when the attack difference is worked out. 参考里这来自
    /// <c>Tower::GetAttack()</c> 的覆写（塔的弓箭手带法师公会的攻击加成），端口用参数表达：塔不是
    /// <see cref="BattleUnit"/>，没法靠虚函数把加成渗进伤害公式。
    /// </param>
    /// <returns>The damage after every modifier, never less than 1.</returns>
    /// <remarks>
    /// <para>
    /// 参考：<c>battle_troop.cpp:517-588</c>。顺序是有意义的，这里逐条照抄：
    /// </para>
    /// <list type="number">
    /// <item>射手不在近战里（自己身边没有敌人，且这一击本身不是相邻攻击）才吃箭术加成与城墙惩罚；
    /// 反过来在近战里就减半，除非兵种有 <c>NO_MELEE_PENALTY</c>。</item>
    /// <item>护盾只保护被射击的一方，且塔的射击不受护盾影响。</item>
    /// <item>致盲后的反击按致盲的比例减伤；石化的目标只吃一半。</item>
    /// <item>对亡灵的额外伤害与四系元素弱点**只翻一倍**，同时满足多条也不叠。</item>
    /// <item>攻防差：正差每点 +10%（最多 +300%），负差每点 −5%（最多 −80%）。</item>
    /// </list>
    /// </remarks>
    public uint CalculateDamageUnit(BattleUnit enemy, double damage, uint extraAttack = 0)
    {
        if (IsArchers)
        {
            if (!IsHandFighting() && !IsHandFighting(this, enemy))
            {
                int archery = _field?.GetArcheryBonusPercent(this) ?? 0;

                if (archery != 0)
                {
                    damage += damage * archery / 100;
                }

                int penalty = _field?.GetShootingPenaltyPercent(this, enemy) ?? 0;

                if (penalty > 0)
                {
                    damage *= 1 - penalty / 100.0;
                }

                // 塔的射击不吃护盾。
                if (!HasModes(BattleMonsterState.CapTower) && enemy.HasModes(BattleMonsterState.SpellShield))
                {
                    damage /= new Spell(SpellType.Shield).ExtraValue();
                }
            }
            else if (!MonsterAbilities.IsPresent(Monster, MonsterAbility.NoMeleePenalty))
            {
                damage /= 2;
            }
        }

        if (_blindRetaliation)
        {
            uint reduction = new Spell(SpellType.Blind).ExtraValue();

            damage = damage * (100 - reduction) / 100;
        }

        if (enemy.HasModes(BattleMonsterState.SpellStone))
        {
            damage /= 2;
        }

        if (DealsDoubleDamageTo(enemy))
        {
            damage *= 2;
        }

        int difference = (int)(GetAttack() + extraAttack) - (int)enemy.GetDefense();

        if (MonsterAbilities.IsDragons(enemy.Monster) && HasModes(BattleMonsterState.SpellDragonSlayer))
        {
            difference += (int)new Spell(SpellType.DragonSlayer).ExtraValue();
        }

        damage *= 1 + (0 < difference ? 0.1 * Math.Min(difference, 20) : 0.05 * Math.Max(difference, -16));

        return Math.Max((uint)Math.Max(damage, 0.0), 1u);
    }

    /// <summary>
    /// The damage one blow actually deals, <c>Unit::GetDamage()</c>: the roll between min and max, doubled by good
    /// luck or halved by bad luck.
    /// </summary>
    /// <param name="enemy">The unit that would be struck.</param>
    /// <param name="randomGenerator">The battle's generator, so a replay deals the same damage.</param>
    /// <param name="extraAttack">Attack on top of the unit's own; see <see cref="CalculateMinDamage"/>.</param>
    /// <returns>The damage, never less than 1.</returns>
    /// <remarks>参考：<c>battle_troop.cpp:590-612</c>。祝福取上限、诅咒取下限，两者都不掷骰。</remarks>
    public uint GetDamage(BattleUnit enemy, Pcg32 randomGenerator, uint extraAttack = 0)
    {
        uint damage;

        if (HasModes(BattleMonsterState.SpellBless))
        {
            damage = CalculateMaxDamage(enemy, extraAttack);
        }
        else if (HasModes(BattleMonsterState.SpellCurse))
        {
            damage = CalculateMinDamage(enemy, extraAttack);
        }
        else
        {
            damage = Rand.GetWithGen(CalculateMinDamage(enemy, extraAttack), CalculateMaxDamage(enemy, extraAttack),
                randomGenerator);
        }

        if (HasModes(BattleMonsterState.LuckGood))
        {
            damage *= 2;
        }
        else if (HasModes(BattleMonsterState.LuckBad))
        {
            damage /= 2;
        }

        return Math.Max(damage, 1u);
    }

    /// <summary>
    /// How dangerous this unit is in the eyes of its enemy, <c>Unit::evaluateThreatForUnit()</c>
    /// (<c>battle_troop.cpp:1007-1165</c>).
    /// </summary>
    /// <param name="defender">The enemy that this unit would strike.</param>
    /// <returns>
    /// The threat this unit poses to <paramref name="defender"/>; negative when the defender should not hit it.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 战斗 AI 的全部判断都架在这个数上：先打威胁最大的。基础值是一击的平均伤害，然后按参考的顺序逐条修正：
    /// </para>
    /// <list type="number">
    /// <item>距离：走不到敌人的近战兵威胁按 `1.5 × 距离 ÷ 速度` 打折；塔里的单位、飞行兵与射手不打折。</item>
    /// <item>双击：敌人能反击时，第二击按「反击伤害 ÷ 自身血量」打折；不能反击时按两击都打满算（×2）。</item>
    /// <item>能力：半敌（ENEMY_HALVING）×2、噬魂（SOUL_EATER）×3、吸血（HP_DRAIN）×1.3。</item>
    /// <item>自带施法：致盲/定身/石化按触发几率把「敌人一击的伤害」折进去，诅咒只值十分之一，驱散不加分。</item>
    /// <item>镜像 ×10——一击就碎，优先清掉。</item>
    /// <item>打自己人 ×−2、被催眠 ×−1、不能动归零、本回合已行动 ÷1.25。</item>
    /// </list>
    /// <para>
    /// 参考里那几条 <c>assert</c>（双击单位不会双格近战/全相邻近战/范围射击）也照抄成断言：属性表一旦配错，
    /// 这里的两倍估算就不再成立。
    /// </para>
    /// </remarks>
    public double EvaluateThreatForUnit(BattleUnit defender)
    {
        ArgumentNullException.ThrowIfNull(defender);

        uint damageToDefender = GetPotentialDamage(defender);
        double threat = damageToDefender;

        // ① 距离：近战单位要走过去，威胁随距离衰减。
        {
            double distanceModifier;

            if (defender.HasModes(BattleMonsterState.CapTower) || IsFlying || IsArchers)
            {
                distanceModifier = 1.0;
            }
            else
            {
                uint speed = GetSpeed(true, false);
                Debug.Assert(speed > 0, "A unit standing on the battlefield has a speed.");

                uint attackRange = speed + 1;
                uint distance = BattleBoard.GetDistance(Position, defender.Position);
                Debug.Assert(distance > 0, "Two units never share a cell.");

                distanceModifier = distance <= attackRange ? 1.0 : (1.5 * distance) / speed;
            }

            threat /= distanceModifier;
        }

        // ② 双击：第二击能不能打满，取决于敌人还手打掉多少。
        if (IsDoubleAttack)
        {
            Debug.Assert(!MonsterAbilities.IsPresent(Monster, MonsterAbility.TwoCellMeleeAttack)
                         && !MonsterAbilities.IsPresent(Monster, MonsterAbility.AllAdjacentCellMeleeAttack)
                         && !MonsterAbilities.IsPresent(Monster, MonsterAbility.AreaShot),
                "A unit that strikes twice never hits several cells at once.");

            uint retaliatoryDamage;

            if (defender.HasModes(BattleMonsterState.CapTower)
                || MonsterAbilities.IsPresent(Monster, MonsterAbility.NoEnemyRetaliation)
                || (IsArchers && !IsHandFighting()))
            {
                retaliatoryDamage = 0;
            }
            else
            {
                retaliatoryDamage = defender.EstimateRetaliatoryDamage(damageToDefender);
            }

            if (retaliatoryDamage > 0)
            {
                Debug.Assert(HitPoints > 0, "A unit that strikes is alive.");

                threat += threat * (1.0 - Math.Min((double)retaliatoryDamage / HitPoints, 1.0));
            }
            else
            {
                threat *= 2;
            }
        }

        // ③ 特殊能力。
        if (MonsterAbilities.IsPresent(Monster, MonsterAbility.EnemyHalving))
        {
            threat *= 2;
        }

        if (MonsterAbilities.IsPresent(Monster, MonsterAbility.SoulEater))
        {
            threat *= 3;
        }

        if (MonsterAbilities.IsPresent(Monster, MonsterAbility.HpDrain))
        {
            threat *= 1.3;
        }

        // ④ 自带施法：把法术的触发几率折算成威胁。
        if (MonsterAbilities.TryGetInstance(Monster, MonsterAbility.SpellCaster, out MonsterAbilityInstance spellCaster))
        {
            SpellType castSpell = spellCaster.Spell;

            uint defenderDamage = defender.HasModes(BattleMonsterState.SpellCurse)
                ? defender.CalculateMinDamage(this)
                : defender.HasModes(BattleMonsterState.SpellBless)
                    ? defender.CalculateMaxDamage(this)
                    : (defender.CalculateMinDamage(this) + defender.CalculateMaxDamage(this)) / 2;

            double chance = spellCaster.Percentage / 100.0;

            switch (castSpell)
            {
            case SpellType.Blind:
            case SpellType.Paralyze:
            case SpellType.Petrify:
            {
                if (defender.AllowApplySpell(new Spell(castSpell), null))
                {
                    threat += defenderDamage * chance;
                }

                break;
            }
            case SpellType.Dispel:
                // 参考也没算驱散的价值。
                break;
            case SpellType.Curse:
            {
                if (defender.AllowApplySpell(new Spell(castSpell), null))
                {
                    threat += defenderDamage * chance / 10.0;
                }

                break;
            }
            default:
                Debug.Fail($"A creature casts {castSpell}, whose threat value has not been worked out yet.");

                break;
            }
        }

        // ⑤ 镜像：一击就碎，必须先清。
        if (HasModes(BattleMonsterState.CapMirrorImage))
        {
            threat *= 10;
        }

        // ⑥ 该不该打：自己人、被催眠、动不了、本回合已经打过。
        if (Color == defender.GetCurrentColor())
        {
            threat *= -2;
        }
        else if (Color != GetCurrentColor())
        {
            threat *= -1;
        }
        else if (IsImmovable)
        {
            threat = 0;
        }
        else if (HasModes(BattleMonsterState.Moved))
        {
            threat /= 1.25;
        }

        return threat;
    }

    /// <summary>
    /// How many creatures a blow of that size would kill, <c>Unit::HowManyWillBeKilled()</c>.
    /// </summary>
    /// <param name="damage">The damage the blow deals.</param>
    /// <returns>The whole stack for a mirror image, everything the damage covers otherwise.</returns>
    /// <remarks>参考：<c>battle_troop.cpp:614-621</c>。镜像被打中就整队消失，不看血量。</remarks>
    public uint HowManyWillBeKilled(uint damage)
    {
        if (HasModes(BattleMonsterState.CapMirrorImage))
        {
            return Count;
        }

        if (damage >= _hitPoints)
        {
            return Count;
        }

        return Count - Troop.GetCountFromHitPoints(Monster, _hitPoints - damage);
    }

    /// <summary>
    /// Applies a blow, <c>Unit::ApplyDamage( enemy, dmg, killed, resurrected )</c>.
    /// </summary>
    /// <param name="enemy">The unit that dealt the blow.</param>
    /// <param name="damage">The damage it deals.</param>
    /// <param name="killed">How many creatures died.</param>
    /// <param name="resurrected">How many creatures the attacker got back through soul eating or blood drain.</param>
    /// <returns>How many creatures died, the reference's return value.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:762-791</c>。吸取只发生在**真的打死**了人的时候（<c>killed == 0</c> 直接返回），
    /// 而且两种吸血的计量对象不同：噬魂按**攻击方**的单个血量算且可以超出上限，吸血按**被攻击方**的单个血量算
    /// 且不许超出上限。
    /// </remarks>
    public uint ApplyDamage(BattleUnit enemy, uint damage, out uint killed, out uint resurrected)
    {
        killed = ApplyDamageInternal(damage);
        resurrected = 0;

        if (killed == 0)
        {
            return killed;
        }

        uint enemyUnitHitPoints = (uint)MonsterStatistics.GetBattleStats(enemy.Monster).HitPoints;

        if (MonsterAbilities.IsPresent(enemy.Monster, MonsterAbility.SoulEater))
        {
            resurrected = enemy.Resurrect(killed * enemyUnitHitPoints, true, false);
        }
        else if (MonsterAbilities.IsPresent(enemy.Monster, MonsterAbility.HpDrain))
        {
            uint ownUnitHitPoints = (uint)MonsterStatistics.GetBattleStats(Monster).HitPoints;

            resurrected = enemy.Resurrect(killed * ownUnitHitPoints, false, false);
        }

        return killed;
    }

    /// <summary>
    /// Brings creatures back, <c>Unit::_resurrect()</c>.
    /// </summary>
    /// <param name="points">How many hit points to restore.</param>
    /// <param name="allowToExceedMaxCount">Whether the stack may grow past everything it ever had, as soul eating does.</param>
    /// <param name="isTemporary">Whether the resurrection is undone at the end of the battle (mirror images).</param>
    /// <returns>How many creatures actually came back.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:734-755</c>。不许超上限时，多出来的部分要退回去，并把血量截到「满员」——
    /// 否则一个被削到半血的队伍复活到上限后会带着不满的血。临时复活不减死亡数。
    /// </remarks>
    public uint Resurrect(uint points, bool allowToExceedMaxCount, bool isTemporary)
    {
        uint resurrect = Troop.GetCountFromHitPoints(Monster, _hitPoints + points) - Count;

        _troop.Count = Count + resurrect;
        _hitPoints += points;

        if (allowToExceedMaxCount)
        {
            _maxCount = Math.Max(_maxCount, Count);
        }
        else if (Count > _maxCount)
        {
            resurrect -= Count - _maxCount;
            _troop.Count = _maxCount;
            _hitPoints = _troop.GetHitPoints();
        }

        if (!isTemporary)
        {
            _deadCount -= Math.Min(resurrect, _deadCount);
        }

        return resurrect;
    }

    /// <summary>
    /// The spell power of a monster's built-in spell, <c>fheroes2::spellPowerForBuiltinMonsterSpells</c>
    /// (<c>spell_info.h</c>).
    /// </summary>
    /// <remarks>
    /// 没有英雄当施法者时（怪物自带法术、竞技场地形造成的伤害）法术强度固定为 3，参考在 <c>ApplySpell</c> 里直接用了
    /// 这个常量，此处提成公开常量好让调用方与测试共用。
    /// </remarks>
    public const uint SpellPowerForBuiltinMonsterSpells = 3;

    /// <summary>The commander of the unit's army, <c>ArmyTroop::GetCommander()</c>.</summary>
    /// <remarks>
    /// 参考：<c>army_troop.h</c> 的 <c>GetCommander()</c> 转调所属军队的 <c>commander</c>。没有归属军队的单位
    /// （竞技场临时单位、召唤物）回答 <see langword="null"/>。
    /// </remarks>
    /// <returns>The hero or captain, or <see langword="null"/> when the unit belongs to no army.</returns>
    public IArmyCommander? GetCommander() => _army?.Commander;

    /// <summary>
    /// The spell a spell-casting creature may throw this round, <c>Unit::GetSpellMagic()</c>
    /// (<c>battle_troop.cpp:1536-1552</c>).
    /// </summary>
    /// <param name="randomGenerator">The battle's generator.</param>
    /// <returns>The spell, or an invalid spell (<see cref="SpellType.None"/>) for a creature that cannot or did not cast.</returns>
    /// <remarks>
    /// 概率是兵种表里 <c>SPELL_CASTER</c> 那一项的百分比，掷骰在 1..100 上做——因此 0% 与 100% 都照常消耗一次掷骰，
    /// 战斗录像的随机序列依赖这一点。
    /// </remarks>
    public Spell GetSpellMagic(Pcg32 randomGenerator)
    {
        if (!MonsterAbilities.TryGetInstance(Monster, MonsterAbility.SpellCaster, out MonsterAbilityInstance caster)
            || Rand.GetWithGen(1, 100, randomGenerator) > caster.Percentage)
        {
            return new Spell(SpellType.None);
        }

        return new Spell(caster.Spell);
    }

    /// <summary>Whether the unit has lost hit points, <c>Unit::isHaveDamage()</c> (<c>battle_troop.cpp:1554-1557</c>).</summary>
    /// <remarks>
    /// 与「死了几个」不是一回事：满员但被削了血也算有伤。治疗法术据此判断「这个队伍还值不值得治」。
    /// </remarks>
    public bool IsHaveDamage => _hitPoints < (uint)(MonsterStatistics.GetBattleStats(Monster).HitPoints * (long)_maxCount);

    /// <summary>
    /// Adds a mode and starts its clock, <c>Unit::_addAffection()</c> (<c>battle_troop.cpp:1694-1704</c>).
    /// </summary>
    /// <param name="mode">The spell effect, a single bit of <see cref="BattleMonsterState"/>.</param>
    /// <param name="duration">How many turns it lasts.</param>
    /// <remarks>标记与时长必须一起写，否则 <see cref="NewTurn"/> 摘不掉它。</remarks>
    public void AddAffection(uint mode, uint duration)
    {
        SetModes(mode);
        _affected.AddMode(mode, duration);
    }

    /// <summary>
    /// Puts one effect in place of another, <c>Unit::_replaceAffection()</c> (<c>battle_troop.cpp:1682-1692</c>).
    /// </summary>
    /// <param name="remove">The effect to take away, possibly a mask.</param>
    /// <param name="add">The effect to put in place.</param>
    /// <param name="duration">How many turns the new effect lasts.</param>
    /// <remarks>祝福与诅咒、加速与迟缓、石肤与钢肤互为替代：互斥的两个效果不会同时挂在身上。</remarks>
    private void ReplaceAffection(uint remove, uint add, uint duration)
    {
        RemoveAffection(remove);
        AddAffection(add, duration);
    }

    /// <summary>
    /// How many turns an effect still lasts, <c>Unit::GetAffectedDuration()</c> (<c>battle_troop.cpp:1745-1748</c>).
    /// </summary>
    /// <param name="mode">The effect, a single bit of <see cref="BattleMonsterState"/>.</param>
    /// <returns>The remaining turns; zero when the effect is not on the unit.</returns>
    /// <remarks>AI 用它决定「这个目标身上的祝福还值不值得驱散」。</remarks>
    public uint GetAffectedDuration(uint mode) => _affected.GetMode(mode);

    /// <summary>
    /// Whether a spell may land on this unit, <c>Unit::AllowApplySpell()</c> (<c>battle_troop.cpp:793-811</c>).
    /// </summary>
    /// <param name="spell">The spell about to be applied.</param>
    /// <param name="applyingHero">The caster, or null for a monster's built-in spell or an arena effect.</param>
    /// <param name="forceApplyToAlly">
    /// Whether a hostile spell may hit its own side too — chain lightning is the only such spell.
    /// </param>
    /// <remarks>
    /// 四道关：镜像不吃镜像/反魔、本体不吃镜像、友方法术不能落在对面、敌方法术不能落在自己人身上，
    /// 最后才问抗魔。第二道关用**当前**颜色（<see cref="Color"/>），因为被催眠的单位已经算对面的人了。
    /// </remarks>
    /// <returns>Whether the spell goes through.</returns>
    public bool AllowApplySpell(Spell spell, IArmyCommander? applyingHero, bool forceApplyToAlly = false)
    {
        // 镜像不吃任何强化：它就是本体的影子，没有自己的意志。
        if (HasModes(BattleMonsterState.CapMirrorImage)
            && (spell.Type is SpellType.AntiMagic or SpellType.MirrorImage))
        {
            return false;
        }

        // 本体不能再造一个镜像出来。
        if (HasModes(BattleMonsterState.CapMirrorOwner) && spell.Type == SpellType.MirrorImage)
        {
            return false;
        }

        if (applyingHero != null)
        {
            if (spell.IsApplyToFriends() && Color != applyingHero.GetColor())
            {
                return false;
            }

            if (spell.IsApplyToEnemies() && Color == applyingHero.GetColor() && !forceApplyToAlly)
            {
                return false;
            }
        }

        return GetMagicResist(spell, applyingHero) < 100;
    }

    /// <summary>
    /// Applies a spell to the unit, <c>Unit::ApplySpell()</c> (<c>battle_troop.cpp:813-837</c>).
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <param name="applyingHero">The caster, or null for a monster's built-in spell.</param>
    /// <param name="target">Where the damage and the death toll are written back.</param>
    /// <returns><see langword="false"/> when <see cref="AllowApplySpell"/> refused the spell.</returns>
    /// <remarks>
    /// 三条路：伤害（<c>_spellApplyDamage</c>）、治疗与复活（<c>_spellRestoreAction</c>）、模式效果
    /// （<c>_spellModesAction</c>）。连锁闪电是唯一「对敌方施放却可能落到友军头上」的法术，
    /// 所以它强制允许落在自己人身上。
    /// </remarks>
    public bool ApplySpell(Spell spell, IArmyCommander? applyingHero, BattleSpellTarget target)
    {
        // 参考的注释原话：连锁闪电是唯一不能施放在友军身上、却可能被友军吃到的法术。
        bool isForceApply = spell.Type == SpellType.ChainLightning;

        if (!AllowApplySpell(spell, applyingHero, isForceApply))
        {
            return false;
        }

        uint spellPower = applyingHero != null ? (uint)applyingHero.GetPower() : SpellPowerForBuiltinMonsterSpells;

        if (spell.IsDamage())
        {
            SpellApplyDamage(spell, spellPower, applyingHero, target);
        }
        else if (spell.IsRestore() || spell.IsResurrect())
        {
            SpellRestoreAction(spell, spellPower, applyingHero);
        }
        else
        {
            SpellModesAction(spell, spellPower, applyingHero);
        }

        return true;
    }

    /// <summary>
    /// Deals the damage of a spell, <c>Unit::_spellApplyDamage()</c> (<c>battle_troop.cpp:1268-1279</c>).
    /// </summary>
    /// <remarks>先算伤害再落账：算出来的数写在 <paramref name="target"/> 上，击杀数由 <c>_applyDamage</c> 回答。</remarks>
    private void SpellApplyDamage(Spell spell, uint spellPower, IArmyCommander? applyingHero, BattleSpellTarget target)
    {
        Debug.Assert(spell.IsDamage());

        uint damage = CalculateSpellDamage(spell, spellPower, applyingHero, target.Damage);

        if (damage != 0)
        {
            target.Damage = damage;
            target.Killed = ApplyDamageInternal(damage);
        }
    }

    /// <summary>
    /// Puts the timed effects of a spell on the unit, <c>Unit::_spellModesAction()</c>
    /// (<c>battle_troop.cpp:1174-1266</c>).
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <param name="spellPower">The duration, in turns.</param>
    /// <param name="applyingHero">The caster, whose artifacts may lengthen the effect.</param>
    /// <remarks>
    /// <para>
    /// 时长先取法术强度，再叠上施法者的「每场战斗法术时长」宝物。有三处不按这个规矩：嗜血固定 3 回合（不受宝物影响）、
    /// 镜像只登记 <c>CAP_MIRROROWNER</c>（真正的镜像单位由竞技场创建）、瓦解射线只加计数（防御由 <c>GetDefense</c> 扣）。
    /// 驱散没有时长，直接摘掉 <c>IS_MAGIC</c>。
    /// </para>
    /// <para>
    /// 致盲还要顺手清掉 <c>_blindRetaliation</c>：被攻击者自带的致盲法术，挨打之后不该有「反击减弱」的后续。
    /// </para>
    /// </remarks>
    private void SpellModesAction(Spell spell, uint spellPower, IArmyCommander? applyingHero)
    {
        int extraDuration = applyingHero?.BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.EveryCombatSpellDuration) ?? 0;
        uint duration = (uint)Math.Max(0, (int)spellPower + extraDuration);

        switch (spell.Type)
        {
            case SpellType.Bless:
            case SpellType.MassBless:
                ReplaceAffection(BattleMonsterState.SpellCurse, BattleMonsterState.SpellBless, duration);
                break;

            case SpellType.BloodLust:
                AddAffection(BattleMonsterState.SpellBloodlust, 3);
                break;

            case SpellType.Curse:
            case SpellType.MassCurse:
                ReplaceAffection(BattleMonsterState.SpellBless, BattleMonsterState.SpellCurse, duration);
                break;

            case SpellType.Haste:
            case SpellType.MassHaste:
                ReplaceAffection(BattleMonsterState.SpellSlow, BattleMonsterState.SpellHaste, duration);
                break;

            case SpellType.Dispel:
            case SpellType.MassDispel:
                RemoveAffection(BattleMonsterState.Magic);
                break;

            case SpellType.Shield:
            case SpellType.MassShield:
                AddAffection(BattleMonsterState.SpellShield, duration);
                break;

            case SpellType.Slow:
            case SpellType.MassSlow:
                ReplaceAffection(BattleMonsterState.SpellHaste, BattleMonsterState.SpellSlow, duration);
                break;

            case SpellType.StoneSkin:
                ReplaceAffection(BattleMonsterState.SpellSteelSkin, BattleMonsterState.SpellStoneSkin, duration);
                break;

            case SpellType.Blind:
                AddAffection(BattleMonsterState.SpellBlind, duration);

                // 攻击者自带的致盲：挨了这一下之后不该再有「致盲反击」的减伤。
                _blindRetaliation = false;
                break;

            case SpellType.DragonSlayer:
                AddAffection(BattleMonsterState.SpellDragonSlayer, duration);
                break;

            case SpellType.SteelSkin:
                ReplaceAffection(BattleMonsterState.SpellStoneSkin, BattleMonsterState.SpellSteelSkin, duration);
                break;

            case SpellType.AntiMagic:
                ReplaceAffection(BattleMonsterState.Magic, BattleMonsterState.SpellAntiMagic, duration);
                break;

            case SpellType.Paralyze:
                AddAffection(BattleMonsterState.SpellParalyze, duration);
                break;

            case SpellType.Berserker:
                ReplaceAffection(BattleMonsterState.SpellHypnotize, BattleMonsterState.SpellBerserker, duration);
                break;

            case SpellType.Hypnotize:
                ReplaceAffection(BattleMonsterState.SpellBerserker, BattleMonsterState.SpellHypnotize, duration);
                break;

            case SpellType.Petrify:
                AddAffection(BattleMonsterState.SpellStone, duration);
                break;

            case SpellType.MirrorImage:
                // 特例：真正的镜像单位由竞技场创建，这里只登记「本体」这个身份。
                _affected.AddMode(BattleMonsterState.CapMirrorOwner, duration);
                break;

            case SpellType.DisruptingRay:
                ++_disruptingRaysNum;
                break;

            default:
                Debug.Assert(false, $"Unexpected spell {spell.Type} in SpellModesAction.");
                break;
        }
    }

    /// <summary>
    /// Heals or resurrects with a spell, <c>Unit::_spellRestoreAction()</c> (<c>battle_troop.cpp:1428-1471</c>).
    /// </summary>
    /// <param name="spell">The spell, one of cure or the three resurrections.</param>
    /// <param name="spellPower">The spell power, which sets how much comes back.</param>
    /// <param name="applyingHero">The caster, whose artifacts may raise a resurrection.</param>
    /// <remarks>
    /// 治疗顺手摘掉所有负面法术（这是 Cure 与纯粹回血的差别），并把血量截到「满员」——否则一个被削到半血的队伍
    /// 治满后会带着超出编制上限的血。复活要先从坟场把尸体取回来，再 <c>SetPosition</c> 把格子占回去；
    /// <see cref="SpellType.Resurrect"/> 是临时复活（战斗结束失效），真复活不是。
    /// </remarks>
    private void SpellRestoreAction(Spell spell, uint spellPower, IArmyCommander? applyingHero)
    {
        switch (spell.Type)
        {
            case SpellType.Cure:
            case SpellType.MassCure:
                RemoveAffection(BattleMonsterState.BadMagic);

                _hitPoints += SpellInfo.GetHPRestorePoints(spell, spellPower);
                _hitPoints = Math.Min(_hitPoints, _troop.GetHitPoints());
                break;

            case SpellType.Resurrect:
            case SpellType.AnimateDead:
            case SpellType.ResurrectTrue:
            {
                if (!IsValid)
                {
                    _graveyard?.RemoveUnit(this);
                }

                uint restore = SpellInfo.GetResurrectPoints(spell, spellPower, applyingHero?.BagArtifacts);
                Resurrect(restore, false, spell.Type == SpellType.Resurrect);

                // 把单位放回棋盘：死亡时格子已经交还，这里要重新占住。
                SetPosition(_position);
                break;
            }

            default:
                Debug.Assert(false, $"Unexpected spell {spell.Type} in SpellRestoreAction.");
                break;
        }
    }

    /// <summary>
    /// The damage a damage spell does to this unit, <c>Unit::CalculateSpellDamage()</c>
    /// (<c>battle_troop.cpp:1281-1426</c>).
    /// </summary>
    /// <param name="spell">The damage spell.</param>
    /// <param name="spellPower">The caster's spell power.</param>
    /// <param name="applyingHero">The caster, or null for a monster's built-in spell.</param>
    /// <param name="targetDamage">The chain lightning jump index: 0 for the first target, 1 for the second, and so on.</param>
    /// <param name="ignoreDefendingHero">Whether the defender's artifacts are to be ignored.</param>
    /// <returns>The damage after abilities, weaknesses and artifacts.</returns>
    /// <remarks>
    /// <para>
    /// 顺序照抄参考：先抗性类能力（越具体越靠前），再弱点，最后才是双方英雄的宝物，而且它**不**复用
    /// <see cref="SpellInfo.GetDamage"/> —— 那只是「宝物加成到法术伤害」，这里还叠了怪物能力与弱点，
    /// 连锁闪电还要按跳跃次数除以 2/4/8。
    /// </para>
    /// <para>
    /// 能力的四个分支是一条 <c>if/else if</c> 链，不是四个独立的 <c>if</c>：同时满足多项时只按最具体的那一条打折。
    /// 注意「元素法术减伤」这一条还带着 <see cref="Spell.IsElementalSpell"/> 的附加条件，条件不成立时会**继续**落到
    /// 弱点分支上——参考就是这样写的，别改成一遇到该能力就直接返回。
    /// </para>
    /// </remarks>
    public uint CalculateSpellDamage(Spell spell, uint spellPower, IArmyCommander? applyingHero, uint targetDamage, bool ignoreDefendingHero = false)
    {
        Debug.Assert(spell.IsDamage());

        uint damage = spell.Damage() * spellPower;

        // 能力优先于弱点：两者都命中时只看能力。
        if (TryFindAbility(Monster, MonsterAbility.CertainSpellDamageReduction, spell.Type, out MonsterAbilityInstance certainReduction))
        {
            damage = (uint)(damage * certainReduction.Percentage / 100);
        }
        else if (MonsterAbilities.TryGetInstance(Monster, MonsterAbility.ElementalSpellDamageReduction, out MonsterAbilityInstance elementalReduction)
                 && spell.IsElementalSpell())
        {
            damage = (uint)(damage * elementalReduction.Percentage / 100);
        }
        else if (TryFindWeakness(Monster, MonsterWeaknessType.ExtraDamageFromCertainSpell, spell.Type, out MonsterWeaknessInstance certainWeakness))
        {
            damage = (uint)(damage * (100 + certainWeakness.Percentage) / 100);
        }
        else if ((MonsterWeaknesses.IsPresent(Monster, MonsterWeaknessType.DoubleDamageFromFireSpells) && spell.IsFire())
                 || (MonsterWeaknesses.IsPresent(Monster, MonsterWeaknessType.DoubleDamageFromColdSpells) && spell.IsCold()))
        {
            damage *= 2;
        }

        if (applyingHero == null)
        {
            return damage;
        }

        IArmyCommander? defendingHero = GetCommander();

        switch (spell.Type)
        {
            case SpellType.ColdRay:
            case SpellType.ColdRing:
                damage = ApplyMultipliedPercent(damage, applyingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.ColdSpellExtraEffectivenessPercent), true);

                if (defendingHero != null && !ignoreDefendingHero)
                {
                    damage = ApplyMultipliedPercent(damage, defendingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.ColdSpellDamageReductionPercent), false);
                    damage = ApplyMultipliedPercent(damage, defendingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactCurseType.ColdSpellExtraDamagePercent), true);
                }

                break;

            case SpellType.Fireball:
            case SpellType.FireBlast:
                damage = ApplyMultipliedPercent(damage, applyingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.FireSpellExtraEffectivenessPercent), true);

                if (defendingHero != null && !ignoreDefendingHero)
                {
                    damage = ApplyMultipliedPercent(damage, defendingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.FireSpellDamageReductionPercent), false);
                    damage = ApplyMultipliedPercent(damage, defendingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactCurseType.FireSpellExtraDamagePercent), true);
                }

                break;

            case SpellType.LightningBolt:
            case SpellType.ChainLightning:
                damage = ApplyMultipliedPercent(damage, applyingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.LightningSpellExtraEffectivenessPercent), true);

                if (defendingHero != null && !ignoreDefendingHero)
                {
                    damage = ApplyMultipliedPercent(damage, defendingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.LightningSpellDamageReductionPercent), false);
                }

                if (spell.Type == SpellType.ChainLightning)
                {
                    damage = targetDamage switch
                    {
                        1 => damage / 2,
                        2 => damage / 4,
                        3 => damage / 8,
                        _ => damage,
                    };
                }

                break;

            case SpellType.ElementalStorm:
            case SpellType.Armageddon:
                if (defendingHero != null && !ignoreDefendingHero)
                {
                    damage = ApplyMultipliedPercent(damage, defendingHero.BagArtifacts.GetTotalMultipliedPercent(ArtifactBonusType.ElementalSpellDamageReductionPercent), false);
                }

                break;

            default:
                break;
        }

        return damage;
    }

    /// <summary>
    /// How likely the unit shrugs a spell off, <c>Unit::GetMagicResist()</c> (<c>battle_troop.cpp:1487-1534</c>).
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <param name="applyingHero">The caster, or null for a monster's built-in spell.</param>
    /// <returns>The resistance in percent; 100 means the spell cannot be applied at all.</returns>
    /// <remarks>
    /// <para>
    /// 反魔直接免疫。四种法术「没有意义时不生效」：治疗满血且无负面效果、复活已满员、驱散无效果可摘、催眠目标血量
    /// 超过法术上限。之后才看宝物的法术免疫，最后才是怪物的抗魔（含 <c>MAGIC_RESISTANCE</c> 与单法术免疫）。
    /// </para>
    /// <para>
    /// 参考在催眠分支断言施法者非空（催眠从来不是怪物自带法术）。端口退化成「用无限强度的内置强度」而不是抛异常：
    /// 这条路径只有一个调用者——<see cref="AllowApplySpell"/> 的判定，宁可放过也不该让战斗崩掉。
    /// </para>
    /// </remarks>
    public uint GetMagicResist(Spell spell, IArmyCommander? applyingHero)
    {
        if (HasModes(BattleMonsterState.SpellAntiMagic))
        {
            return 100;
        }

        switch (spell.Type)
        {
            case SpellType.Cure:
            case SpellType.MassCure:
                if (!IsHaveDamage && !HasModes(BattleMonsterState.BadMagic))
                {
                    return 100;
                }

                break;

            case SpellType.Resurrect:
            case SpellType.ResurrectTrue:
            case SpellType.AnimateDead:
                if (Count == _maxCount)
                {
                    return 100;
                }

                break;

            case SpellType.Dispel:
            case SpellType.MassDispel:
                if (!HasModes(BattleMonsterState.Magic))
                {
                    return 100;
                }

                break;

            case SpellType.Hypnotize:
                uint power = applyingHero != null ? (uint)applyingHero.GetPower() : SpellPowerForBuiltinMonsterSpells;

                if (SpellInfo.GetHypnotizeMonsterHPPoints(spell, power, applyingHero?.BagArtifacts) < _hitPoints)
                {
                    return 100;
                }

                break;

            default:
                break;
        }

        if (HasSpellImmunityArtifact(GetCommander(), spell))
        {
            return 100;
        }

        return MonsterSpellResistance.GetSpellResistance(Monster, spell.Type);
    }

    /// <summary>
    /// The spell effects currently on the unit, <c>Unit::getCurrentSpellEffects()</c>
    /// (<c>battle_troop.cpp:839-890</c>).
    /// </summary>
    /// <returns>One entry per effect in place, in the reference's order.</returns>
    /// <remarks>
    /// 参考的写法就是「把挂着的标记逐个翻成法术」：十五个 <c>if</c>、固定的先后顺序，最后连镜像本体也算一项。
    /// 名字容易被误解——它不是「这个怪物能施放什么」，怪物自带法术由 <c>SPELL_CASTER</c> 能力回答。
    /// </remarks>
    public IReadOnlyList<Spell> GetCurrentSpellEffects()
    {
        var spellList = new List<Spell>(4);

        if (HasModes(BattleMonsterState.SpellBless))
        {
            spellList.Add(new Spell(SpellType.Bless));
        }

        if (HasModes(BattleMonsterState.SpellCurse))
        {
            spellList.Add(new Spell(SpellType.Curse));
        }

        if (HasModes(BattleMonsterState.SpellHaste))
        {
            spellList.Add(new Spell(SpellType.Haste));
        }

        if (HasModes(BattleMonsterState.SpellSlow))
        {
            spellList.Add(new Spell(SpellType.Slow));
        }

        if (HasModes(BattleMonsterState.SpellShield))
        {
            spellList.Add(new Spell(SpellType.Shield));
        }

        if (HasModes(BattleMonsterState.SpellBloodlust))
        {
            spellList.Add(new Spell(SpellType.BloodLust));
        }

        if (HasModes(BattleMonsterState.SpellStoneSkin))
        {
            spellList.Add(new Spell(SpellType.StoneSkin));
        }

        if (HasModes(BattleMonsterState.SpellSteelSkin))
        {
            spellList.Add(new Spell(SpellType.SteelSkin));
        }

        if (HasModes(BattleMonsterState.SpellBlind))
        {
            spellList.Add(new Spell(SpellType.Blind));
        }

        if (HasModes(BattleMonsterState.SpellParalyze))
        {
            spellList.Add(new Spell(SpellType.Paralyze));
        }

        if (HasModes(BattleMonsterState.SpellStone))
        {
            spellList.Add(new Spell(SpellType.Petrify));
        }

        if (HasModes(BattleMonsterState.SpellDragonSlayer))
        {
            spellList.Add(new Spell(SpellType.DragonSlayer));
        }

        if (HasModes(BattleMonsterState.SpellBerserker))
        {
            spellList.Add(new Spell(SpellType.Berserker));
        }

        if (HasModes(BattleMonsterState.SpellHypnotize))
        {
            spellList.Add(new Spell(SpellType.Hypnotize));
        }

        if (HasModes(BattleMonsterState.CapMirrorOwner))
        {
            spellList.Add(new Spell(SpellType.MirrorImage));
        }

        return spellList;
    }

    /// <summary>
    /// Scales a damage number by one artifact bonus list, <c>dmg = dmg * ( 100 ± value ) / 100</c> per entry.
    /// </summary>
    /// <param name="damage">The damage so far.</param>
    /// <param name="percents">The percentages, one per artifact.</param>
    /// <param name="increase">Whether the percentages raise the damage instead of lowering it.</param>
    /// <returns>The scaled damage.</returns>
    /// <remarks>
    /// 参考是每个宝物各乘一次（不是把百分比加起来再乘），四舍五入到整数，所以顺序与逐项截断都要照抄。
    /// </remarks>
    private static uint ApplyMultipliedPercent(uint damage, IReadOnlyList<int> percents, bool increase)
    {
        foreach (int percent in percents)
        {
            damage = increase
                ? (uint)(damage * (100 + percent) / 100)
                : (uint)(damage * (100 - percent) / 100);
        }

        return damage;
    }

    /// <summary>The first ability entry of a type whose <c>value</c> names a spell, the reference's <c>std::find</c> with a pair.</summary>
    private static bool TryFindAbility(MonsterType monster, MonsterAbility ability, SpellType spell, out MonsterAbilityInstance instance)
    {
        foreach (MonsterAbilityInstance candidate in MonsterAbilities.GetInstances(monster))
        {
            if (candidate.Type == ability && candidate.Value == (int)spell)
            {
                instance = candidate;
                return true;
            }
        }

        instance = default;
        return false;
    }

    /// <summary>The first weakness entry of a type whose <c>value</c> names a spell, the reference's <c>std::find</c> with a pair.</summary>
    private static bool TryFindWeakness(MonsterType monster, MonsterWeaknessType weakness, SpellType spell, out MonsterWeaknessInstance instance)
    {
        foreach (MonsterWeaknessInstance candidate in MonsterWeaknesses.GetInstances(monster))
        {
            if (candidate.Type == weakness && candidate.Value == (int)spell)
            {
                instance = candidate;
                return true;
            }
        }

        instance = default;
        return false;
    }

    /// <summary>
    /// Whether a commander's artifacts make it immune to a spell, <c>getImmunityArtifactForSpell()</c>
    /// (<c>battle_troop.cpp:65-98</c>).
    /// </summary>
    /// <param name="commander">The unit's commander, or null.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>Whether an immunity artifact for that spell is worn.</returns>
    /// <remarks>
    /// 参考返回宝物本身、调用方只问 <c>isValid()</c>，所以这里直接回答「有没有」。八种免疫与法术的对应关系照抄参考的
    /// <c>switch</c>，没有对应的法术落到 <c>NONE</c> —— 那种情况永远不可能是免疫。
    /// </remarks>
    private static bool HasSpellImmunityArtifact(IArmyCommander? commander, Spell spell)
    {
        if (commander == null)
        {
            return false;
        }

        ArtifactBonusType bonus = spell.Type switch
        {
            SpellType.Curse or SpellType.MassCurse => ArtifactBonusType.CurseSpellImmunity,
            SpellType.Hypnotize => ArtifactBonusType.HypnotizeSpellImmunity,
            SpellType.DeathRipple or SpellType.DeathWave => ArtifactBonusType.DeathSpellImmunity,
            SpellType.Berserker => ArtifactBonusType.BerserkSpellImmunity,
            SpellType.Blind => ArtifactBonusType.BlindSpellImmunity,
            SpellType.Paralyze => ArtifactBonusType.ParalyzeSpellImmunity,
            SpellType.HolyWord or SpellType.HolyShout => ArtifactBonusType.HolySpellImmunity,
            SpellType.Dispel or SpellType.MassDispel => ArtifactBonusType.DispelSpellImmunity,
            _ => ArtifactBonusType.None,
        };

        return bonus != ArtifactBonusType.None
            && commander.BagArtifacts.GetFirstArtifactWithBonus(bonus) != ArtifactType.Unknown;
    }

    /// <summary>
    /// Starts the unit's turn, <c>Unit::NewTurn()</c>: regeneration, the per-turn flags, and the spell clock.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:387-422</c>。再生在这个时点把血量补满；六个「本回合用过」的标记全部清掉；
    /// 然后把每个限时法术的时长减一，到期的摘掉——镜像到期是特例，要把镜像本体一起打死。
    /// </remarks>
    public void NewTurn()
    {
        if (IsRegenerating)
        {
            _hitPoints = _troop.GetHitPoints();
        }

        ResetModes(
            BattleMonsterState.Retaliated |
            BattleMonsterState.Moved |
            BattleMonsterState.Skip |
            BattleMonsterState.LuckGood |
            BattleMonsterState.LuckBad |
            BattleMonsterState.MoraleGood |
            BattleMonsterState.MoraleBad);

        _affected.DecreaseDuration();

        for (uint mode = _affected.FindZeroDuration(); mode != 0; mode = _affected.FindZeroDuration())
        {
            if (mode == BattleMonsterState.CapMirrorOwner)
            {
                ExpireMirrorImage();
            }
            else
            {
                RemoveAffection(mode);
            }
        }
    }

    /// <summary>
    /// Cleans up after the unit died, <c>Unit::PostKilledAction()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:679-732</c>。做四件事：解开镜像的互相引用、摘掉所有法术、把尸体放进坟场、交还两格。
    /// 坟场这一步有两个例外——镜像与元素不进坟场（<c>!Modes( CAP_MIRRORIMAGE ) &amp;&amp; !isElemental()</c>）；
    /// 而且必须先登记再交还格子，因为坟场按格子下标归档。
    /// </remarks>
    public void PostKilledAction()
    {
        if (HasModes(BattleMonsterState.CapMirrorOwner))
        {
            _mirrorUnit = null;
            RemoveAffection(BattleMonsterState.CapMirrorOwner);
        }

        if (HasModes(BattleMonsterState.CapMirrorImage))
        {
            if (_mirrorUnit != null)
            {
                if (_mirrorUnit.HasModes(BattleMonsterState.CapMirrorOwner))
                {
                    _mirrorUnit._mirrorUnit = null;
                    _mirrorUnit.RemoveAffection(BattleMonsterState.CapMirrorOwner);
                }

                _mirrorUnit = null;
            }
        }

        RemoveAffection(BattleMonsterState.Magic);

        if (!HasModes(BattleMonsterState.CapMirrorImage) && !MonsterAbilities.IsElemental(Monster))
        {
            _graveyard?.AddUnit(this);
        }

        ReleaseCells();
    }

    /// <summary>
    /// Takes a mode away, <c>Unit::removeAffection()</c>: the flag and the clock entry both go.
    /// </summary>
    /// <param name="mode">The mode, or a mask of modes.</param>
    /// <remarks>参考：<c>battle_troop.cpp:1706-1711</c>。两者必须一起摘，不然标记清了、时长还留着，下一个法术会继承它。</remarks>
    public void RemoveAffection(uint mode)
    {
        ResetModes(mode);
        _affected.RemoveMode(mode);
    }

    /// <summary>
    /// Whether this blow doubles against the enemy, <c>DOUBLE_DAMAGE_TO_UNDEAD</c> and the four elemental weaknesses.
    /// </summary>
    /// <param name="enemy">The unit that would be struck.</param>
    /// <remarks>参考：<c>battle_troop.cpp:566-577</c>。多条同时成立也只翻一倍，所以这里是「或」而不是累加。</remarks>
    private bool DealsDoubleDamageTo(BattleUnit enemy) =>
        (MonsterAbilities.IsPresent(Monster, MonsterAbility.DoubleDamageToUndead) && MonsterAbilities.IsUndead(enemy.Monster))
        || (MonsterAbilities.IsPresent(Monster, MonsterAbility.EarthCreature)
            && MonsterWeaknesses.IsPresent(enemy.Monster, MonsterWeaknessType.DoubleDamageFromEarthCreatures))
        || (MonsterAbilities.IsPresent(Monster, MonsterAbility.AirCreature)
            && MonsterWeaknesses.IsPresent(enemy.Monster, MonsterWeaknessType.DoubleDamageFromAirCreatures))
        || (MonsterAbilities.IsPresent(Monster, MonsterAbility.FireCreature)
            && MonsterWeaknesses.IsPresent(enemy.Monster, MonsterWeaknessType.DoubleDamageFromFireCreatures))
        || (MonsterAbilities.IsPresent(Monster, MonsterAbility.WaterCreature)
            && MonsterWeaknesses.IsPresent(enemy.Monster, MonsterWeaknessType.DoubleDamageFromWaterCreatures));

    /// <summary>
    /// Takes the damage, <c>Unit::_applyDamage()</c>.
    /// </summary>
    /// <param name="damage">The damage to take.</param>
    /// <returns>How many creatures died.</returns>
    /// <remarks>
    /// 参考：<c>battle_troop.cpp:623-677</c>。挨打会把定身与致盲打破（并因此算作「本回合已行动」），
    /// 镜像被打中直接归零，本体死时镜像跟着死，死亡数单独累计。
    /// </remarks>
    private uint ApplyDamageInternal(uint damage)
    {
        if (damage == 0 || Count == 0)
        {
            return 0;
        }

        uint killed = HowManyWillBeKilled(damage);

        if (HasModes(BattleMonsterState.ParalyzeMagic))
        {
            if (!MonsterAbilities.IsPresent(Monster, MonsterAbility.UnlimitedRetaliation))
            {
                SetModes(BattleMonsterState.Retaliated);
            }

            SetModes(BattleMonsterState.Moved);
            RemoveAffection(BattleMonsterState.ParalyzeMagic);
        }

        if (HasModes(BattleMonsterState.SpellBlind))
        {
            SetModes(BattleMonsterState.Moved);
            RemoveAffection(BattleMonsterState.SpellBlind);
        }

        if (killed >= Count)
        {
            _deadCount += Count;
            _troop.Count = 0;
        }
        else
        {
            _deadCount += killed;
            _troop.Count = Count - killed;
        }

        if (HasModes(BattleMonsterState.CapMirrorImage))
        {
            _hitPoints = 0;
        }
        else
        {
            _hitPoints -= Math.Min(_hitPoints, damage);
        }

        if (HasModes(BattleMonsterState.CapMirrorOwner) && !IsValid && _mirrorUnit != null)
        {
            _mirrorUnit.ApplyDamageInternal(_mirrorUnit._hitPoints);
        }

        return killed;
    }

    /// <summary>Destroys the mirror image when its timer runs out, the <c>CAP_MIRROROWNER</c> branch of <c>NewTurn</c>.</summary>
    private void ExpireMirrorImage()
    {
        if (_mirrorUnit == null)
        {
            return;
        }

        BattleUnit mirror = _mirrorUnit;

        mirror._hitPoints = 0;
        mirror._troop.Count = 0;
        mirror.PostKilledAction();
    }

    /// <summary>Clears the cells the unit currently occupies.</summary>
    private void ReleaseCells()
    {
        if (_position.Head != null && _position.Head.Unit == this)
        {
            _position.Head.SetUnit(null);
        }

        if (_position.Tail != null && _position.Tail.Unit == this)
        {
            _position.Tail.SetUnit(null);
        }
    }

    /// <summary>Marks the cells the unit stands on as occupied by it.</summary>
    private void OccupyCells()
    {
        _position.Head?.SetUnit(this);
        _position.Tail?.SetUnit(this);
    }
}
