using System.Diagnostics;

using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Battle;

/// <summary>
/// 法术行动：把一条 <see cref="BattleCommandType.Spellcast"/> 命令变成战场上的变化，
/// <c>Battle::Arena</c> 的 <c>battle_action.cpp:412-479</c> 与 <c>:1372-1658</c>。
/// </summary>
/// <remarks>
/// <para>
/// 参考把法术分成五条路：传送、地震、镜像、召唤元素，其余全部走「选目标 + 生效」（<c>_applyActionSpellDefaults</c>）。
/// 前四条各自要动战场结构（挪位置、砸建筑、造单位），最后一条只把 <see cref="BattleSpellTarget"/> 交给
/// <see cref="BattleUnit.ApplySpell"/>。
/// </para>
/// <para>
/// 界面那一半不在这里：施法动画、抗魔提示音、地震的两段爆破、镜像的起手动画都归 <c>_interface</c>，端口没有界面，
/// 于是只留「动画之前选目标、动画之后生效」两条真实逻辑。参考在这两步之间剔除抗魔目标，端口照做。
/// </para>
/// </remarks>
public sealed partial class BattleArena
{
    /// <summary>
    /// Casts a spell, <c>Arena::ApplyActionSpellCast()</c> (<c>battle_action.cpp:412-479</c>).
    /// </summary>
    /// <param name="command">A <see cref="BattleCommandType.Spellcast"/> command.</param>
    /// <exception cref="ArgumentException">
    /// The spell is not a combat spell, is switched off right now, the side has no commander, or the commander cannot
    /// pay for it.
    /// </exception>
    /// <remarks>
    /// 五条路按参考的 <c>switch</c> 顺序分派。无论哪条路，施法之后都要做同样三件事：给指挥官打上
    /// <see cref="HeroModes.Spellcasted"/>（本回合不能再施）、扣法力、把法术记进
    /// <see cref="UsedSpells"/>（鹰眼技能要从这里抄）。
    /// </remarks>
    public void ApplyActionSpellCast(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        Spell spell = new((SpellType)command.GetNextValue());
        IArmyCommander? commander = GetCurrentForce().Commander;

        if (!spell.IsCombat() || IsDisableCastSpell(spell) || commander == null || !commander.CanCastSpell(spell))
        {
            throw new ArgumentException("The spell cannot be cast right now.", nameof(command));
        }

        switch (spell.Type)
        {
            case SpellType.Teleport:
                ApplyActionSpellTeleport(command);
                break;

            case SpellType.Earthquake:
                ApplyActionSpellEarthquake();
                break;

            case SpellType.MirrorImage:
                ApplyActionSpellMirrorImage(command);
                break;

            case SpellType.SummonEElement:
            case SpellType.SummonAElement:
            case SpellType.SummonFElement:
            case SpellType.SummonWElement:
                ApplyActionSpellSummonElemental(spell);
                break;

            default:
                ApplyActionSpellDefaults(command, spell);
                break;
        }

        commander.SetModes(HeroModes.Spellcasted);
        commander.SpellCasted(spell);

        // Saved for the Eagle Eye skill.
        _usedSpells.Append(spell);
    }

    /// <summary>
    /// Applies a spell to the units it hits, <c>Arena::TargetsApplySpell()</c> (<c>battle_action.cpp:955-964</c>).
    /// </summary>
    /// <param name="hero">The caster.</param>
    /// <param name="spell">The spell.</param>
    /// <param name="targets">The units to hit; each one gets the result written back.</param>
    /// <remarks>
    /// 参考只有一句「对每个目标调 <c>Unit::ApplySpell</c>」，此处照抄：目标筛选、抗魔与伤害算术全在单位那一层。
    /// </remarks>
    public void TargetsApplySpell(IArmyCommander? hero, Spell spell, List<BattleSpellTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        foreach (BattleSpellTarget target in targets)
        {
            Debug.Assert(target.Defender != null, "A spell target always names a unit.");

            target.Defender!.ApplySpell(spell, hero, target);
        }
    }

    /// <summary>
    /// The ordinary path: pick the targets, then let the spell land, <c>Arena::_applyActionSpellDefaults()</c>
    /// (<c>battle_action.cpp:1415-1458</c>).
    /// </summary>
    /// <param name="command">The command; its next parameter is the target cell.</param>
    /// <param name="spell">The spell being cast.</param>
    /// <exception cref="InvalidOperationException">The side has no commander to cast with.</exception>
    /// <remarks>
    /// 抵抗判定在 <see cref="GetTargetsForSpell"/> 里掷过骰子，被顶掉的目标打了 <see cref="BattleSpellTarget.Resist"/>。
    /// 参考把这一批剔出来只是为了「挨个播抗魔音效」，生效时它们一个都不算——端口没有音效，于是直接剔除。
    /// </remarks>
    public void ApplyActionSpellDefaults(BattleCommand command, Spell spell)
    {
        ArgumentNullException.ThrowIfNull(command);

        int destination = command.GetNextValue();
        IArmyCommander? commander = GetCurrentCommander();

        if (commander == null)
        {
            throw new InvalidOperationException("A spell needs a commander to cast it.");
        }

        List<BattleSpellTarget> targets = GetTargetsForSpell(commander, spell, destination, true);

        targets.RemoveAll(target => target.Resist);

        TargetsApplySpell(commander, spell, targets);
    }

    /// <summary>
    /// Teleports a unit across the field, <c>Arena::_applyActionSpellTeleport()</c>
    /// (<c>battle_action.cpp:1460-1513</c>).
    /// </summary>
    /// <param name="command">Teleport command: the UID of the unit and the destination cell.</param>
    /// <exception cref="ArgumentException">The unit is gone, the cell cannot hold it, or nobody is casting.</exception>
    public void ApplyActionSpellTeleport(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        int sourceIndex = command.GetNextValue();
        int destinationIndex = command.GetNextValue();

        BattleUnit? unit = GetTroopBoard(sourceIndex);
        BattleCell? cell = _board.GetCell(destinationIndex);

        if (unit == null || !unit.IsValid || cell == null || !cell.IsPassableForUnit(unit)
            || GetCurrentCommander() == null)
        {
            throw new ArgumentException("The unit cannot be teleported to that cell.", nameof(command));
        }

        BattlePosition position = BattlePosition.GetPosition(unit, destinationIndex);

        Debug.Assert(position.IsValidForUnit(unit), "A passable cell always fits the unit that lands on it.");

        unit.SetPosition(position);
    }

    /// <summary>
    /// Damages the castle with an earthquake, <c>Arena::_applyActionSpellEarthquake()</c>
    /// (<c>battle_action.cpp:1515-1584</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The side has no commander to cast with.</exception>
    /// <remarks>
    /// 每个还能挨打的建筑独立掷一次伤害，再按「建筑剩余耐久」截断；桥另加一次 50% 的空掷，因为原版里地震震塌桥
    /// 的概率明显低于震塌其他建筑。伤害按 <see cref="CastleDefenseStructure"/> 的枚举顺序结算（参考用
    /// <c>std::map</c>，天然有序），这样同一发地震在两边的日志里顺序一致。
    /// </remarks>
    public void ApplyActionSpellEarthquake()
    {
        IArmyCommander? commander = GetCurrentCommander();

        if (commander == null)
        {
            throw new InvalidOperationException("An earthquake needs a commander to cast it.");
        }

        (uint minDamage, uint maxDamage) = GetEarthquakeDamageRange(commander);

        SortedDictionary<CastleDefenseStructure, int> earthquakeDamage = [];

        foreach (CastleDefenseStructure target in GetEarthQuakeSpellTargets())
        {
            int condition = GetCastleDefenseStructureCondition(target, SiegeWeaponType.EarthquakeSpell);

            Debug.Assert(condition >= 0, "A castle structure always has a condition.");

            if (condition == 0)
            {
                continue;
            }

            // The bridge gets an extra 50% chance to "miss": the original game demolishes it more rarely.
            int damage = target == CastleDefenseStructure.Bridge && Rand.GetWithGen(0U, 1U, _randomGenerator) == 0
                ? 0
                : (int)Rand.GetWithGen(minDamage, maxDamage, _randomGenerator);

            if (damage == 0)
            {
                continue;
            }

            earthquakeDamage[target] = Math.Min(damage, condition);
        }

        foreach (KeyValuePair<CastleDefenseStructure, int> entry in earthquakeDamage)
        {
            ApplyDamageToCastleDefenseStructure(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Creates the mirror image of a unit, <c>Arena::_applyActionSpellMirrorImage()</c>
    /// (<c>battle_action.cpp:1586-1658</c>).
    /// </summary>
    /// <param name="command">Mirror image command: the head cell of the unit to copy.</param>
    /// <exception cref="ArgumentException">There is no unit there, or nobody is casting.</exception>
    /// <remarks>
    /// 镜像落在离本体最近的合法格子上：先取半径 4 内的所有格子，按到本体头格的距离排序，再取第一个「放得下且不与本体
    /// 重叠」的格子（<see cref="BattleBoard.IsValidMirrorImageIndex"/>）。一个都放不下时法术白费——参考只打日志。
    /// </remarks>
    public void ApplyActionSpellMirrorImage(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        int targetUnitCellIndex = command.GetNextValue();
        BattleUnit? unit = GetTroopBoard(targetUnitCellIndex);

        if (unit == null || !unit.IsValid || GetCurrentCommander() == null)
        {
            throw new ArgumentException("Mirror image needs a unit on the battlefield.", nameof(command));
        }

        int centerIndex = unit.HeadIndex;
        int[] distances = BattleBoard.GetDistanceIndexes(centerIndex, 4);

        Array.Sort(distances, (first, second) =>
            BattleBoard.GetDistance(centerIndex, first).CompareTo(BattleBoard.GetDistance(centerIndex, second)));

        int mirrorIndex = -1;

        foreach (int index in distances)
        {
            if (BattleBoard.IsValidMirrorImageIndex(index, unit))
            {
                mirrorIndex = index;
                break;
            }
        }

        if (mirrorIndex < 0)
        {
            return;
        }

        IArmyCommander commander = GetCurrentCommander()!;
        Spell mirrorImage = new(SpellType.MirrorImage);

        TargetsApplySpell(commander, mirrorImage, [new BattleSpellTarget(unit)]);

        BattleUnit mirrorUnit = CreateMirrorImage(unit);
        BattlePosition position = BattlePosition.GetPosition(mirrorUnit, mirrorIndex);

        Debug.Assert(position.IsValidForUnit(mirrorUnit), "The mirror image lands on a cell chosen for it.");

        mirrorUnit.SetPosition(position);
    }

    /// <summary>
    /// Summons an elemental next to the caster's hero, <c>Arena::_applyActionSpellSummonElemental()</c>
    /// (<c>battle_action.cpp:1372-1413</c>).
    /// </summary>
    /// <param name="spell">One of the four summoning spells.</param>
    /// <exception cref="InvalidOperationException">No commander, or no free cell next to the hero.</exception>
    public void ApplyActionSpellSummonElemental(Spell spell)
    {
        if (GetCurrentCommander() == null)
        {
            throw new InvalidOperationException("Summoning an elemental needs a commander.");
        }

        if (GetFreePositionNearHero(GetCurrentColor()) < 0)
        {
            throw new InvalidOperationException("There is no free cell next to the hero to summon into.");
        }

        CreateElemental(spell);
    }

    /// <summary>
    /// Puts the summoned elemental on the board, <c>Arena::CreateElemental()</c> (<c>battle_arena.cpp:1345-1378</c>).
    /// </summary>
    /// <param name="spell">One of the four summoning spells.</param>
    /// <returns>The new unit.</returns>
    /// <exception cref="InvalidOperationException">The side has no commander to summon with.</exception>
    /// <remarks>
    /// 元素落在英雄旁边的空格上，朝向与它那一侧一致（守方朝左）。命中的兵种由
    /// <see cref="MonsterSummons.FromSpell"/> 给出，数量按法术强度与「召唤」类宝物折算。它记着
    /// <see cref="BattleMonsterState.CapSummonElement"/>，于是同一场战斗里再召另一种元素会被
    /// <see cref="IsDisableCastSpell"/> 拦住。
    /// </remarks>
    public BattleUnit CreateElemental(Spell spell)
    {
        IArmyCommander? commander = GetCurrentCommander();

        if (commander == null)
        {
            throw new InvalidOperationException("A summons needs a commander to cast it.");
        }

        Debug.Assert(!IsDisableCastSpell(spell), "The caller checked that the spell may be cast.");

        int index = GetFreePositionNearHero(GetCurrentColor());

        Debug.Assert(BattleBoard.IsValidIndex(index), "The caller checked that there is room next to the hero.");

        MonsterType monster = MonsterSummons.FromSpell(spell.Type);

        Debug.Assert(monster != MonsterType.Unknown, "Every summoning spell names an elemental.");
        Debug.Assert(!MonsterAbilities.IsWide(monster), "An elemental is never a wide unit.");

        bool reflect = ReferenceEquals(commander, _defendingForce.Commander);
        uint count = SpellInfo.GetSummonMonsterCount(spell, (uint)commander.GetPower(), commander.BagArtifacts);

        BattlePosition position = new(_board, index, false, reflect);
        BattleUnit elemental = new(new Troop(monster, count), position, reflect, _uidGenerator.GetUnique())
        {
            Army = GetCurrentForce().OriginalArmy,
            Color = GetCurrentColor(),
        };

        Debug.Assert(elemental.TailIndex < 0, "An elemental occupies one cell.");

        elemental.SetModes(BattleMonsterState.CapSummonElement);

        GetCurrentForce().Add(elemental);

        return elemental;
    }

    /// <summary>
    /// Copies a unit into a mirror image, <c>Arena::CreateMirrorImage()</c> (<c>battle_arena.cpp:1380-1394</c>).
    /// </summary>
    /// <param name="unit">The unit to copy.</param>
    /// <returns>The mirror, not yet placed on the board.</returns>
    /// <remarks>
    /// 镜像拿到本体的**当前**人数（不是战斗开始时的），血量按整队满血起算；两边互指
    /// （<see cref="BattleUnit.MirrorUnit"/>），任一消失时另一份由 <see cref="BattleUnit.PostKilledAction"/> 清理。
    /// 位置留空，由调用方摆到选好的格子上。
    /// </remarks>
    public BattleUnit CreateMirrorImage(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        BattleUnit mirrorUnit = new(new Troop(unit.Monster, unit.Count), new BattlePosition(_board), unit.IsReflect,
            _uidGenerator.GetUnique())
        {
            Army = unit.Army,
            Color = unit.Color,
        };

        mirrorUnit.MirrorUnit = unit;
        mirrorUnit.SetModes(BattleMonsterState.CapMirrorImage);

        unit.MirrorUnit = mirrorUnit;
        unit.SetModes(BattleMonsterState.CapMirrorOwner);

        GetCurrentForce().Add(mirrorUnit);

        return mirrorUnit;
    }
}
