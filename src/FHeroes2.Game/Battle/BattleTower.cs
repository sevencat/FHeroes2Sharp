using System.Diagnostics;
using System.Text;

using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;

// 从 Battle 命名空间里看，`Castle` 是 FHeroes2.Game.Castle 这个命名空间，不是城堡类型本身。
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Battle;

/// <summary>
/// A castle's shooting position and the virtual archers standing on it — <c>battle_tower.h</c> and
/// <c>battle_tower.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference derives <c>Tower</c> from <c>Unit</c> and overrides <c>isValid()</c> so that a tower knocked down by
/// a catapult stops shooting even though its archers are unharmed. <see cref="BattleUnit"/> is sealed in the port, so
/// this is a wrapper: it owns a unit built exactly as the reference builds one and answers <see cref="IsValid"/> from
/// its own flag.
/// </para>
/// <para>
/// Towers are never on the board — the archers only fire — so the wrapped unit gets an empty position on a scratch
/// board of its own. That board is unreachable from anywhere else, which is what keeps an off-board unit from
/// occupying a cell.
/// </para>
/// </remarks>
public sealed class BattleTower
{
    /// <summary>The board cell of the top archer tower, <c>Arena::CASTLE_TOP_ARCHER_TOWER_POS</c>.</summary>
    public const int TopArcherTowerIndex = 19;

    /// <summary>The board cell of the bottom archer tower, <c>Arena::CASTLE_BOTTOM_ARCHER_TOWER_POS</c>.</summary>
    public const int BottomArcherTowerIndex = 85;

    private readonly BattleUnit _unit;
    private readonly BattleTowerType _towerType;
    private readonly uint _attackBonus;

    private bool _isValid = true;

    /// <summary>
    /// Builds the tower of a castle, <c>Battle::Tower::Tower()</c> (<c>battle_tower.cpp:40-53</c>).
    /// </summary>
    /// <remarks>
    /// The stack is <c>min( CountBuildings(), 20 )</c> archers, and the two turrets get half of that (at least one).
    /// The reference halves the count with <c>SetCount()</c>, which only rewrites the troop's count and leaves the hit
    /// points at the full stack's value — a turret therefore has as many hit points as the keep's stack but half the
    /// archers, so it does not start the battle wounded. The port does the same by changing the troop's count after
    /// the unit has been built.
    /// </remarks>
    /// <param name="castle">The castle that owns the tower.</param>
    /// <param name="type">Which of the three positions.</param>
    /// <param name="uid">The unit id, 0 in the reference's out-of-combat calls.</param>
    /// <param name="guestHero">The hero standing in the castle, or <see langword="null"/> for none.</param>
    public BattleTower(CastleTown castle, BattleTowerType type, uint uid, Hero? guestHero = null)
    {
        _towerType = type;
        _attackBonus = (uint)castle.GetLevelMageGuild();

        Troop troop = new(MonsterType.Archer, Math.Min((uint)castle.CountBuildings(), 20U));

        _unit = new BattleUnit(troop, new BattlePosition(new BattleBoardCells()), false, uid);

        uint count = type == BattleTowerType.Center ? troop.Count : troop.Count / 2;

        troop.Count = Math.Max(count, 1U);

        // The virtual archers get the attack bonus of whoever commands the town's army.
        _unit.Army = castle.GetActualArmy(guestHero);
        _unit.SetModes(BattleMonsterState.CapTower);
    }

    /// <summary>The archers behind the tower.</summary>
    public BattleUnit Unit => _unit;

    /// <summary>Which of the three positions this tower is, <c>Tower::GetType()</c>.</summary>
    public BattleTowerType Type => _towerType;

    /// <summary>
    /// The flat attack bonus of the archers, <c>Tower::GetAttackBonus()</c> — the level of the castle's mage guild.
    /// </summary>
    public uint AttackBonus => _attackBonus;

    /// <summary>
    /// Whether the tower still stands, <c>Tower::isValid()</c>. A destroyed tower keeps its archers but stops firing.
    /// </summary>
    public bool IsValid => _isValid;

    /// <summary>The number of archers, <c>Tower::GetCount()</c>, inherited from the troop.</summary>
    public uint Count => _unit.Count;

    /// <summary>
    /// The tower's name, <c>Tower::GetName()</c> (<c>battle_tower.cpp:55-71</c>).
    /// </summary>
    /// <remarks>
    /// The center tower is the ballista the keep itself fires. The names are the reference's English strings; the
    /// port has no translation layer, the same as the rest of the data tables.
    /// </remarks>
    public string Name => _towerType switch
    {
        BattleTowerType.Left => "Left Turret",
        BattleTowerType.Right => "Right Turret",
        BattleTowerType.Center => "Ballista",
        _ => string.Empty
    };

    /// <summary>
    /// The attack of a tower's shot, <c>Tower::GetAttack()</c>: the archers' own (with the army's commander) plus the
    /// mage guild level.
    /// </summary>
    /// <returns>The attack skill used by the shot.</returns>
    public uint GetAttack() => _unit.GetAttack() + _attackBonus;

    /// <summary>
    /// The strength of the tower, <c>Tower::GetStrengthWithBonus()</c> as inherited from <c>Troop</c>.
    /// </summary>
    /// <param name="bonusAttack">Attack to add, normally <see cref="AttackBonus"/>.</param>
    /// <param name="bonusDefense">Defense to add.</param>
    /// <returns>The strength of one archer times the count.</returns>
    public double GetStrengthWithBonus(int bonusAttack, int bonusDefense) => _unit.GetStrengthWithBonus(bonusAttack, bonusDefense);

    /// <summary>
    /// The screen position the tower shoots from, <c>Tower::GetPortPosition()</c>.
    /// </summary>
    /// <returns>The anchor point, or the origin for an unknown type.</returns>
    public Point GetPortPosition() => _towerType switch
    {
        BattleTowerType.Left => new Point(410, 70),
        BattleTowerType.Right => new Point(410, 320),
        BattleTowerType.Center => new Point(560, 170),
        _ => default
    };

    /// <summary>
    /// Knocks the turret down, <c>Tower::SetDestroyed()</c> (<c>battle_tower.cpp:109-123</c>).
    /// </summary>
    /// <remarks>
    /// The rubble is the board cell the turret stood on: the reference turns it into an obstacle, which is what makes
    /// the corner of the wall useless to the defenders afterwards. The keep has no such cell, so only the flag changes.
    /// </remarks>
    /// <param name="board">The battle board the tower stands on.</param>
    public void SetDestroyed(BattleBoardCells board)
    {
        switch (_towerType)
        {
        case BattleTowerType.Left:
            board.GetCell(TopArcherTowerIndex)!.Object = 1;
            break;

        case BattleTowerType.Right:
            board.GetCell(BottomArcherTowerIndex)!.Object = 1;
            break;
        }

        _isValid = false;
    }

    /// <summary>
    /// The combat description of a castle's towers, <c>Battle::Tower::GetInfo()</c>
    /// (<c>battle_tower.cpp:125-192</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One paragraph per tower, separated by blank lines: the keep always, the turrets only if the castle built them.
    /// A turret that a catapult knocked down during the siege is reported as destroyed instead of as shooters.
    /// </para>
    /// <para>
    /// The reference asks the arena for its towers so that it can see the damage; the port takes the same state as a
    /// dictionary, <see langword="null"/> outside combat, where every tower counts as standing.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle to describe.</param>
    /// <param name="towers">The arena's towers by type, or <see langword="null"/> outside a battle.</param>
    /// <returns>The paragraphs, or an empty string for a town that has no castle.</returns>
    public static string GetInfo(CastleTown castle, IReadOnlyDictionary<BattleTowerType, BattleTower>? towers = null)
    {
        if (!castle.IsBuild(BuildingType.Castle))
        {
            return string.Empty;
        }

        List<BattleTowerType> towerTypes = new() { BattleTowerType.Center };

        if (castle.IsBuild(BuildingType.LeftTurret))
        {
            towerTypes.Add(BattleTowerType.Left);
        }

        if (castle.IsBuild(BuildingType.RightTurret))
        {
            towerTypes.Add(BattleTowerType.Right);
        }

        StringBuilder message = new();

        for (int i = 0; i < towerTypes.Count; ++i)
        {
            BattleTowerType towerType = towerTypes[i];
            BattleTower? state = null;

            // The arena knows all three towers; a missing key means the caller built the dictionary wrong.
            if (towers is not null && !towers.TryGetValue(towerType, out state))
            {
                Debug.Assert(false, "the arena has no such tower");
            }

            if (state is null || state.IsValid)
            {
                BattleTower tower = new(castle, towerType, 0);

                message.Append("The ").Append(tower.Name)
                    .Append(" fires with the strength of ").Append(tower.Count).Append(" Archers");

                if (tower.AttackBonus > 0)
                {
                    message.Append(", each with a +").Append(tower.AttackBonus)
                        .Append(" bonus to their attack skill.");
                }
                else
                {
                    message.Append('.');
                }
            }
            else
            {
                message.Append("The ").Append(state.Name).Append(" is destroyed.");
            }

            if (i + 1 != towerTypes.Count)
            {
                message.Append("\n\n");
            }
        }

        return message.ToString();
    }
}
