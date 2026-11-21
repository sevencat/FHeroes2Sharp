using System.Diagnostics;

using FHeroes2.Game.Army;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The castle-structure half of <see cref="BattleArena"/>: which cell a wall occupies, how much of it is left and how
/// the Catapult and the Earthquake spell take it apart.
/// </summary>
public sealed partial class BattleArena
{
    /// <summary>
    /// The cell a castle structure occupies, <c>getPositionOfCastleDefenseStructure()</c>
    /// (<c>battle_arena.cpp:252-279</c>).
    /// </summary>
    /// <param name="structure">The structure.</param>
    /// <returns>The cell index; <c>0</c> for the central tower, which owns no cell of its own.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a structure with a cell.</exception>
    public static int GetPositionOfCastleDefenseStructure(CastleDefenseStructure structure) => structure switch
    {
        CastleDefenseStructure.Bridge => CastleGatePos,
        CastleDefenseStructure.Tower1 => CastleTopArcherTowerPos,
        CastleDefenseStructure.Tower2 => CastleBottomArcherTowerPos,
        CastleDefenseStructure.Wall1 => CastleFirstTopWallPos,
        CastleDefenseStructure.Wall2 => CastleSecondTopWallPos,
        CastleDefenseStructure.Wall3 => CastleThirdTopWallPos,
        CastleDefenseStructure.Wall4 => CastleFourthTopWallPos,
        CastleDefenseStructure.TopBridgeTower => CastleTopGateTowerPos,
        CastleDefenseStructure.BottomBridgeTower => CastleBottomGateTowerPos,
        CastleDefenseStructure.CentralTower => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(structure), structure, "This structure has no board cell."),
    };

    /// <summary>
    /// How much of a castle structure is left, from the point of view of a siege weapon,
    /// <c>Arena::getCastleDefenseStructureCondition()</c> (<c>battle_arena.cpp:1201-1293</c>).
    /// </summary>
    /// <param name="target">The structure.</param>
    /// <param name="siegeWeapon">What is hitting it.</param>
    /// <returns>The remaining hit points; zero when the structure is gone or there is no castle.</returns>
    /// <remarks>
    /// <para>
    /// 同一个数在两件武器眼里含义不同：投石车只关心「塔还站不站」（中央塔答 0/1），地震法术读的是墙体格上的
    /// <c>Object</c>（墙 1…3、桥头塔 1…2）。桥的耐久只有 0/1，与武器无关。
    /// </para>
    /// <para>
    /// 中央塔不在 <see cref="GetEarthQuakeSpellTargets"/> 里，因此「地震法术打中央塔」这条分支在正常流程下走不到；
    /// 参考届时读 0 号格，端口照抄这一行为而不是另行抛错。
    /// </para>
    /// </remarks>
    public int GetCastleDefenseStructureCondition(CastleDefenseStructure target, SiegeWeaponType siegeWeapon)
    {
        Debug.Assert(_castle != null, "Castle structure conditions only exist during a siege.");

        if (_castle == null)
        {
            return 0;
        }

        switch (target)
        {
        case CastleDefenseStructure.Wall1:
        case CastleDefenseStructure.Wall2:
        case CastleDefenseStructure.Wall3:
        case CastleDefenseStructure.Wall4:
            return Cell(GetPositionOfCastleDefenseStructure(target)).Object;

        case CastleDefenseStructure.TopBridgeTower:
        case CastleDefenseStructure.BottomBridgeTower:
            Debug.Assert(siegeWeapon == SiegeWeaponType.EarthquakeSpell, "Only the earthquake spell damages the gate towers.");
            return Cell(GetPositionOfCastleDefenseStructure(target)).Object - 1;

        case CastleDefenseStructure.Tower1:
        case CastleDefenseStructure.Tower2:
        case CastleDefenseStructure.CentralTower:
        {
            int towerIndex = GetTowerIndex(target);

            return siegeWeapon switch
            {
                SiegeWeaponType.Catapult => _towers[towerIndex] != null && _towers[towerIndex]!.IsValid ? 1 : 0,
                SiegeWeaponType.EarthquakeSpell => Cell(GetPositionOfCastleDefenseStructure(target)).Object - 1,
                _ => throw new ArgumentOutOfRangeException(nameof(siegeWeapon), siegeWeapon, "Unknown siege weapon."),
            };
        }

        case CastleDefenseStructure.Bridge:
            Debug.Assert(_bridge != null, "A siege has a bridge.");
            return _bridge != null && _bridge.IsValid ? 1 : 0;

        default:
            throw new ArgumentOutOfRangeException(nameof(target), target, "Not a castle defense structure.");
        }
    }

    /// <summary>
    /// Everything the Earthquake spell may damage, <c>Arena::getEarthQuakeSpellTargets()</c>
    /// (<c>battle_arena.cpp:1295-1306</c>).
    /// </summary>
    /// <returns>The nine targets, in the reference's order.</returns>
    public static CastleDefenseStructure[] GetEarthQuakeSpellTargets() =>
    [
        CastleDefenseStructure.Wall1,
        CastleDefenseStructure.Wall2,
        CastleDefenseStructure.Wall3,
        CastleDefenseStructure.Wall4,
        CastleDefenseStructure.Tower1,
        CastleDefenseStructure.Tower2,
        CastleDefenseStructure.Bridge,
        CastleDefenseStructure.TopBridgeTower,
        CastleDefenseStructure.BottomBridgeTower,
    ];

    /// <summary>
    /// The damage range of an Earthquake cast by a hero, <c>Arena::getEarthquakeDamageRange()</c>
    /// (<c>battle_arena.cpp:1308-1328</c>).
    /// </summary>
    /// <param name="commander">The caster.</param>
    /// <returns>Both ends of the roll, inclusive; <c>(0, 0)</c> for a commander without spell power.</returns>
    public static (uint Min, uint Max) GetEarthquakeDamageRange(IArmyCommander commander)
    {
        ArgumentNullException.ThrowIfNull(commander);

        int spellPower = commander.GetPower();

        if (spellPower is > 0 and < 3)
        {
            return (0, 1);
        }

        if (spellPower is >= 3 and < 6)
        {
            return (0, 2);
        }

        if (spellPower is >= 6 and < 10)
        {
            return (0, 3);
        }

        if (spellPower >= 10)
        {
            return (1, 3);
        }

        // 参考在这里只写了一条警告日志；0 点力量的地震法术造成 0 伤害。
        return (0, 0);
    }

    /// <summary>
    /// Damages a castle structure, <c>Arena::applyDamageToCastleDefenseStructure()</c>
    /// (<c>battle_arena.cpp:1096-1199</c>).
    /// </summary>
    /// <param name="target">The structure that is hit.</param>
    /// <param name="damage">How many hit points to remove.</param>
    /// <remarks>
    /// <para>
    /// 三类结构的「摧毁」不是同一件事：墙可以彻底打穿（格子变 0，部队能走）；桥头塔与箭塔只会掉一级，格子最低停在 1，
    /// 永远不能穿行；桥整座放下，格子变可通行。
    /// </para>
    /// <para>
    /// 有炮塔建筑的箭塔同时持有 <see cref="BattleTower"/>，此时掉血记在塔对象上（<c>SetDestroyed</c>），格子的
    /// <c>Object</c> 只是「还在」的标记；没有炮塔的箭塔则直接扣格子上的数。
    /// </para>
    /// </remarks>
    public void ApplyDamageToCastleDefenseStructure(CastleDefenseStructure target, int damage)
    {
        Debug.Assert(_castle != null, "Only a siege has castle structures to damage.");

        switch (target)
        {
        // Wall sections can be destroyed completely; the cells then become passable.
        case CastleDefenseStructure.Wall1:
        case CastleDefenseStructure.Wall2:
        case CastleDefenseStructure.Wall3:
        case CastleDefenseStructure.Wall4:
        {
            int position = GetPositionOfCastleDefenseStructure(target);
            int condition = Cell(position).Object;

            Debug.Assert(damage > 0 && damage <= condition, "Wall damage must not exceed what is left.");
            Cell(position).Object = condition - damage;
            break;
        }

        // Bridgehead towers lose one level but can never be demolished: their cells stay impassable.
        case CastleDefenseStructure.TopBridgeTower:
        case CastleDefenseStructure.BottomBridgeTower:
        {
            int position = GetPositionOfCastleDefenseStructure(target);
            int condition = Cell(position).Object;

            Debug.Assert(damage == 1 && damage < condition, "Bridge towers drop exactly one level.");
            Cell(position).Object = condition - damage;
            break;
        }

        // Archer towers and the central tower stop shooting once destroyed, but their cells stay impassable.
        case CastleDefenseStructure.Tower1:
        case CastleDefenseStructure.Tower2:
        case CastleDefenseStructure.CentralTower:
        {
            int towerIndex = GetTowerIndex(target);

            if (_towers[towerIndex] != null)
            {
                _towers[towerIndex]!.SetDestroyed(_board);
                break;
            }

            int position = GetPositionOfCastleDefenseStructure(target);
            int condition = Cell(position).Object;

            Debug.Assert(damage == 1 && damage < condition, "A tower without a turret drops exactly one level.");
            Cell(position).Object = condition - damage;
            break;
        }

        // The drawbridge falls and its cell becomes passable.
        case CastleDefenseStructure.Bridge:
            Debug.Assert(damage == 1 && _bridge != null && _bridge.IsValid, "The bridge is destroyed in one hit.");
            _bridge!.SetDestroyed();
            break;

        default:
            throw new ArgumentOutOfRangeException(nameof(target), target, "Not a castle defense structure.");
        }
    }

    /// <summary>
    /// Fires a tower at a unit, <c>Arena::ApplyActionTower()</c> (<c>battle_action.cpp:1180-1226</c>).
    /// </summary>
    /// <param name="command">Tower command: the tower type and the target unit UID.</param>
    /// <exception cref="ArgumentException">The tower is down, or the unit is not on the battlefield.</exception>
    /// <remarks>
    /// 塔只做一件事：按塔自己的攻击（弓箭手的攻击加法师公会等级）算伤害，然后打出去。它不吃攻击方的士气/幸运，
    /// 也不会被反击——塔不在场上，没有格子。参考把动画拆成前后两段夹住结算，端口没有界面，只剩中间那一次
    /// <see cref="BattleUnit.ApplyDamage"/>。
    /// </remarks>
    public void ApplyActionTower(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        BattleTowerType type = (BattleTowerType)command.GetNextValue();
        uint uid = (uint)command.GetNextValue();

        BattleTower? tower = GetTower(type);
        BattleUnit? unit = GetTroopUID(uid);

        if (tower == null || !tower.IsValid || unit == null || !unit.IsValid)
        {
            throw new ArgumentException("The tower or its target is not on the battlefield.", nameof(command));
        }

        uint damage = tower.GetDamage(unit, _randomGenerator);

        unit.ApplyDamage(tower.Unit, damage, out _, out _);
    }

    /// <summary>
    /// Fires the catapult, <c>Arena::ApplyActionCatapult()</c> (<c>battle_action.cpp:1228-1295</c>).
    /// </summary>
    /// <param name="command">
    /// Catapult command: the number of shots, then for each shot the structure, the damage and whether it hit.
    /// </param>
    /// <exception cref="ArgumentException">
    /// There is no catapult, or one shot names a structure the catapult cannot reach or a damage it cannot deal.
    /// </exception>
    /// <remarks>
    /// 每一次投石是三条参数（打哪、多少伤害、中没中）。没中也要把参数读完——参考拿它播「砸空」的烟尘动画，
    /// 端口只跳过结算。命中时才真的改建筑。
    /// </remarks>
    public void ApplyActionCatapult(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_catapult == null)
        {
            throw new ArgumentException("There is no catapult in this battle.", nameof(command));
        }

        CastleDefenseStructure[] allowedTargets = BattleCatapult.GetAllowedTargets();
        uint shots = (uint)command.GetNextValue();

        while (shots-- > 0)
        {
            CastleDefenseStructure target = (CastleDefenseStructure)command.GetNextValue();
            int damage = command.GetNextValue();
            bool hit = command.GetNextValue() != 0;

            if (target == CastleDefenseStructure.None)
            {
                continue;
            }

            if (damage <= 0 || Array.IndexOf(allowedTargets, target) < 0
                || damage > GetCastleDefenseStructureCondition(target, SiegeWeaponType.Catapult))
            {
                throw new ArgumentException("The catapult cannot hit that structure for that damage.", nameof(command));
            }

            if (!hit)
            {
                continue;
            }

            ApplyDamageToCastleDefenseStructure(target, damage);
        }
    }

    /// <summary>
    /// Lets one tower shoot, <c>Arena::TowerAction()</c> (<c>battle_arena.cpp:675-720</c>).
    /// </summary>
    /// <param name="tower">The tower; <see langword="null"/> or a destroyed one does nothing.</param>
    /// <remarks>
    /// <para>
    /// 目标不是「最近的」，而是**威胁最大**的：逐个问场上每个敌人「你对这座塔有多危险」
    /// （<see cref="BattleUnit.EvaluateThreatForUnit"/>），取最高分。并列时先遍历到的留下——参考用「严格小于才换」，
    /// 所以顺序影响结果，遍历顺序必须按格子下标。
    /// </para>
    /// <para>
    /// 塔不在棋盘上占格，所以打完之后要自己把躺下的单位收走：外围的 <see cref="RemoveDeadUnits"/> 只会在执行完
    /// 动作之后才轮到。
    /// </para>
    /// </remarks>
    private void TowerAction(BattleTower? tower)
    {
        if (tower == null || !tower.IsValid)
        {
            return;
        }

        BattleUnit? target = null;
        double targetThreat = double.NegativeInfinity;

        for (int index = 0; index < _board.Count; ++index)
        {
            BattleUnit? unit = _board.GetCell(index)?.Unit;

            if (unit == null || unit.GetCurrentColor() == tower.Unit.GetCurrentColor()
                || (unit.IsWide && unit.TailIndex == index))
            {
                continue;
            }

            double unitThreat = unit.EvaluateThreatForUnit(tower.Unit);

            if (target == null || targetThreat < unitThreat)
            {
                target = unit;
                targetThreat = unitThreat;
            }
        }

        if (target == null)
        {
            return;
        }

        ApplyAction(BattleCommand.Tower((int)tower.Type, (int)target.Uid));

        RemoveDeadUnits();
    }

    /// <summary>
    /// Fires the attacker's catapult at the walls, <c>Arena::CatapultAction()</c> (<c>battle_arena.cpp:722-760</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每一发的「打哪、多少伤害、中没中」都在这里掷完，再打包成一条命令交给 <see cref="ApplyActionCatapult"/>。
    /// 掷骰顺序是 目标 → 伤害 → 命中，少掷或多掷一次都会让同一份录像重放出不同的墙。
    /// </para>
    /// <para>
    /// 伤害被墙的剩余耐久截断：专家弹道学的一发两点打在只剩一点的墙上只算一点。没命中的那一发不改建筑状态，
    /// 但同样消耗了它的两次掷骰。
    /// </para>
    /// </remarks>
    private void CatapultAction()
    {
        if (_catapult == null)
        {
            return;
        }

        uint shots = _catapult.Shots;

        SortedDictionary<CastleDefenseStructure, int> stateOfCatapultTargets = [];

        foreach (CastleDefenseStructure target in BattleCatapult.GetAllowedTargets())
        {
            stateOfCatapultTargets[target] = GetCastleDefenseStructureCondition(target, SiegeWeaponType.Catapult);
        }

        List<int> shotsParameters = [(int)shots];

        while (shots-- > 0)
        {
            CastleDefenseStructure target = BattleCatapult.GetTarget(stateOfCatapultTargets, _randomGenerator);
            int state = stateOfCatapultTargets.TryGetValue(target, out int knownState) ? knownState : 0;
            int damage = Math.Min(_catapult.GetDamage(_randomGenerator), state);
            bool hit = _catapult.IsNextShotHit(_randomGenerator);

            shotsParameters.Add((int)target);
            shotsParameters.Add(damage);
            shotsParameters.Add(hit ? 1 : 0);

            if (hit)
            {
                stateOfCatapultTargets[target] = state - damage;
            }
        }

        ApplyAction(BattleCommand.Catapult(shotsParameters));
    }

    private static int GetTowerIndex(CastleDefenseStructure target) => target switch
    {
        CastleDefenseStructure.Tower1 => 0,
        CastleDefenseStructure.Tower2 => 2,
        CastleDefenseStructure.CentralTower => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Not a tower."),
    };
}
