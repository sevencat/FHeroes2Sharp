using System.Diagnostics;

using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Maps;

// 从 Battle 命名空间里看，`Castle` 是 FHeroes2.Game.Castle 这个命名空间，不是城堡类型本身。
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The battlefield of one battle: both armies, the castle, its towers and bridge, the pathfinder and the turn state,
/// <c>Battle::Arena</c> (<c>battle_arena.h:94-367</c>, <c>battle_arena.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <see cref="BattleArena"/> 是个单例：构造函数把自己登记进文件静态的 <c>arena</c>，之后任何地方都能
/// <c>Arena::Get()</c>。端口不做单例——战场就是对象，谁需要就拿着它。<see cref="BattleUnit.Field"/> 与
/// <see cref="BattleUnit.Graveyard"/> 是这条边在单位一侧的落点。
/// </para>
/// <para>
/// <b>这一批只到「装配 + 查询」。</b>参考的 <c>Turns()</c>、<c>UnitTurn()</c>、<c>ApplyAction()</c> 与整段法术/攻击
/// 结算依赖 <c>battle_action</c>，等那一批落地再接进来；<c>TowerAction()</c>/<c>CatapultAction()</c> 同理。此处先把
/// 会被双方共用的状态与判据写全，免得行动层再回头改竞技场。
/// </para>
/// <para>
/// 没有界面的那半（<c>Interface</c>、音乐等待、AI 规划器初始化、自动战斗颜色）不在端口范围内；参考在
/// <c>isShowInterface == false</c> 时会给人类玩家打开自动战斗，端口暂时没有自动战斗，因此这一位也不需要。
/// </para>
/// </remarks>
public sealed partial class BattleArena : IBattleField
{
    /// <summary>The cell the catapult sits on, <c>Arena::CATAPULT_POS</c>.</summary>
    public const int CatapultPos = 77;

    /// <summary>The gate cell, <c>Arena::CASTLE_GATE_POS</c>.</summary>
    public const int CastleGatePos = 50;

    /// <summary>The first wall segment from the top, <c>Arena::CASTLE_FIRST_TOP_WALL_POS</c>.</summary>
    public const int CastleFirstTopWallPos = 8;

    /// <summary>The second wall segment, <c>Arena::CASTLE_SECOND_TOP_WALL_POS</c>.</summary>
    public const int CastleSecondTopWallPos = 29;

    /// <summary>The third wall segment, <c>Arena::CASTLE_THIRD_TOP_WALL_POS</c>.</summary>
    public const int CastleThirdTopWallPos = 73;

    /// <summary>The fourth wall segment, <c>Arena::CASTLE_FOURTH_TOP_WALL_POS</c>.</summary>
    public const int CastleFourthTopWallPos = 96;

    /// <summary>The top archer tower, <c>Arena::CASTLE_TOP_ARCHER_TOWER_POS</c>.</summary>
    public const int CastleTopArcherTowerPos = 19;

    /// <summary>The bottom archer tower, <c>Arena::CASTLE_BOTTOM_ARCHER_TOWER_POS</c>.</summary>
    public const int CastleBottomArcherTowerPos = 85;

    /// <summary>The top gate tower, <c>Arena::CASTLE_TOP_GATE_TOWER_POS</c>.</summary>
    public const int CastleTopGateTowerPos = 40;

    /// <summary>The bottom gate tower, <c>Arena::CASTLE_BOTTOM_GATE_TOWER_POS</c>.</summary>
    public const int CastleBottomGateTowerPos = 62;

    private readonly BattleBoardCells _board = new();
    private readonly BattleForce _attackingForce;
    private readonly BattleForce _defendingForce;
    private readonly CastleTown? _castle;
    private readonly BattleTower?[] _towers = new BattleTower?[3];
    private readonly BattleCatapult? _catapult;
    private readonly BattleBridge? _bridge;
    private readonly BattleGraveyard _graveyard = new();
    private readonly BattlePathfinder _pathfinder;
    private readonly Pcg32 _randomGenerator;

    private BattleResult _battleResult;
    private BattleUnit? _currentUnit;
    private IcnType _coverIcn = IcnType.Unknown;
    private uint _turnNumber;

    /// <summary>
    /// Deploys a field battle, <c>Battle::Arena::Arena()</c> (<c>battle_arena.cpp:346-452</c>) with no castle.
    /// </summary>
    /// <param name="attackingArmy">The army on the left (the one that walked into the fight).</param>
    /// <param name="defendingArmy">The army on the right.</param>
    /// <param name="randomGenerator">The battle's random source, shared by both sides.</param>
    /// <remarks>
    /// 这条路不走地图：没有城堡、也没有地形障碍。要按地图地形铺障碍，用 <see cref="FromTile"/>。
    /// </remarks>
    public BattleArena(GameArmy attackingArmy, GameArmy defendingArmy, Pcg32 randomGenerator)
        : this(attackingArmy, defendingArmy, null, randomGenerator)
    {
    }

    /// <summary>
    /// Deploys a battle on, in or around a castle, <c>Battle::Arena::Arena()</c>.
    /// </summary>
    /// <param name="attackingArmy">The army on the left.</param>
    /// <param name="defendingArmy">The army on the right.</param>
    /// <param name="castle">
    /// The castle the fight happens at, or <see langword="null"/> for a field battle. A town that has no castle
    /// building is not a castle and is treated as a field battle, exactly as the reference does at
    /// <c>battle_arena.cpp:360-362</c>.
    /// </param>
    /// <param name="randomGenerator">The battle's random source.</param>
    public BattleArena(GameArmy attackingArmy, GameArmy defendingArmy, CastleTown? castle, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(attackingArmy);
        ArgumentNullException.ThrowIfNull(defendingArmy);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        _randomGenerator = randomGenerator;

        BattleTroopsUidGenerator uidGenerator = new();
        _attackingForce = new BattleForce(attackingArmy, _board, false, uidGenerator);
        _defendingForce = new BattleForce(defendingArmy, _board, true, uidGenerator);

        // If this is a siege of a town, then there is in fact no castle.
        IsTown = castle != null;
        if (castle != null && !castle.IsCastle)
        {
            castle = null;
        }

        _castle = castle;

        if (castle != null)
        {
            if (castle.IsBuild(FHeroes2.Game.Castle.BuildingType.LeftTurret))
            {
                _towers[0] = new BattleTower(castle, BattleTowerType.Left, uidGenerator.GetUnique());
            }

            _towers[1] = new BattleTower(castle, BattleTowerType.Center, uidGenerator.GetUnique());

            if (castle.IsBuild(FHeroes2.Game.Castle.BuildingType.RightTurret))
            {
                _towers[2] = new BattleTower(castle, BattleTowerType.Right, uidGenerator.GetUnique());
            }

            if (_attackingForce.Commander != null)
            {
                _catapult = new BattleCatapult(_attackingForce.Commander);
            }

            _bridge = new BattleBridge(_board, _graveyard, _defendingForce.Color);

            // The towers shoot with the army that actually defends the town. The reference asks the castle for it
            // (castle->GetActualArmy()); the port has no town->hero back-pointer, and the army handed to the arena
            // is exactly that army.
            foreach (BattleTower? tower in _towers)
            {
                if (tower != null)
                {
                    tower.Unit.Army = _defendingForce.OriginalArmy;
                }
            }

            // Catapult cell.
            Cell(CatapultPos).Object = 1;

            // Walls: 3 when the fortifications are built, 2 otherwise.
            int wallObject = castle.IsFortificationBuilt ? 3 : 2;
            Cell(CastleFirstTopWallPos).Object = wallObject;
            Cell(CastleSecondTopWallPos).Object = wallObject;
            Cell(CastleThirdTopWallPos).Object = wallObject;
            Cell(CastleFourthTopWallPos).Object = wallObject;

            // NOTE: All towers can be destroyed but cannot be passed through. Their condition starts at 2 and drops
            // to 1 after the upper part is destroyed; it must never reach 0 or troops could walk through them.
            Cell(CastleTopGateTowerPos).Object = 2;
            Cell(CastleBottomGateTowerPos).Object = 2;
            Cell(CastleTopArcherTowerPos).Object = 2;
            Cell(CastleBottomArcherTowerPos).Object = 2;

            // The raised drawbridge itself.
            Cell(CastleGatePos).Object = 1;
        }

        _pathfinder = new BattlePathfinder(
            _board,
            castle != null && castle.IsBuild(FHeroes2.Game.Castle.BuildingType.Moat),
            unit => _bridge != null && _bridge.IsPassable(unit));

        foreach (BattleUnit unit in _attackingForce)
        {
            unit.Field = this;
            unit.Graveyard = _graveyard;
        }

        foreach (BattleUnit unit in _defendingForce)
        {
            unit.Field = this;
            unit.Graveyard = _graveyard;
        }
    }

    /// <summary>
    /// Deploys the battle that happens on a map tile, <c>Arena::Arena( ..., tileIndex, ... )</c>.
    /// </summary>
    /// <param name="world">The world, asked for the castle and for the tile's ground and object.</param>
    /// <param name="attackingArmy">The army on the left.</param>
    /// <param name="defendingArmy">The army on the right.</param>
    /// <param name="tileIndex">Where the fight happens.</param>
    /// <param name="randomGenerator">The battle's random source.</param>
    /// <returns>The arena, with the ground decoration and the obstacles of the tile already placed.</returns>
    /// <remarks>
    /// 障碍用**地图种子加格子号**单独派生一个随机源（<c>battle_arena.cpp:427-435</c>），与战斗随机源无关：
    /// 同一格的野战无论打多少次，地形都一样。
    /// </remarks>
    public static BattleArena FromTile(World world, GameArmy attackingArmy, GameArmy defendingArmy, int tileIndex, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(world);

        BattleArena arena = new(attackingArmy, defendingArmy, world.GetCastleEntrance(tileIndex), randomGenerator);

        if (arena.Castle == null)
        {
            arena.SetupFieldObstacles(world, tileIndex);
        }

        return arena;
    }

    /// <summary>The 99 cells, <c>Arena::GetBoard()</c>.</summary>
    public BattleBoardCells Board => _board;

    /// <summary>The castle of a siege, <c>Arena::GetCastle()</c>; <see langword="null"/> in the open field.</summary>
    public CastleTown? Castle => _castle;

    /// <summary>Whether the fight happens at a town or castle, <c>Arena::_isTown</c>.</summary>
    /// <remarks>
    /// 与 <see cref="Castle"/> 不是一回事：没有建城堡的村庄也算「攻城」，但棋盘上没有墙，所以 <see cref="Castle"/> 为空。
    /// </remarks>
    public bool IsTown { get; }

    /// <summary>The drawbridge of a siege, <c>Arena::GetBridge()</c>.</summary>
    public BattleBridge? Bridge => _bridge;

    /// <summary>The attacking hero's catapult, <c>Arena::_catapult</c>.</summary>
    public BattleCatapult? Catapult => _catapult;

    /// <summary>The tower at one of the three positions, <c>Arena::GetTower()</c>.</summary>
    /// <param name="type">Which position.</param>
    /// <returns>The tower, or <see langword="null"/> when that turret was never built.</returns>
    public BattleTower? GetTower(BattleTowerType type) => _towers[(int)type];

    /// <summary>Whether at least one tower still stands, <c>Arena::isAnyTowerPresent()</c>.</summary>
    public bool IsAnyTowerPresent
    {
        get
        {
            foreach (BattleTower? tower in _towers)
            {
                if (tower != null && tower.IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The bodies of the fallen, <c>Arena::GetGraveyard()</c>.</summary>
    public BattleGraveyard Graveyard => _graveyard;

    /// <summary>The walk graph, <c>Arena::_battlePathfinder</c>.</summary>
    public BattlePathfinder Pathfinder => _pathfinder;

    /// <summary>The random source of the battle, <c>Arena::_randomGenerator</c>.</summary>
    public Pcg32 RandomGenerator => _randomGenerator;

    /// <summary>The number of the round in progress, <c>Arena::GetTurnNumber()</c>.</summary>
    public uint TurnNumber => _turnNumber;

    /// <summary>The outcome so far, <c>Arena::GetResult()</c>.</summary>
    public ref BattleResult Result => ref _battleResult;

    /// <summary>The unit whose turn it is, <c>Arena::_currentUnit</c>.</summary>
    public BattleUnit? CurrentUnit => _currentUnit;

    /// <summary>The ground decoration chosen for a field battle, <c>Arena::GetCovr()</c>.</summary>
    public IcnType CoverIcn => _coverIcn;

    /// <summary>The attacking side, <c>Arena::getAttackingForce()</c>.</summary>
    public BattleForce AttackingForce => _attackingForce;

    /// <summary>The defending side, <c>Arena::getDefendingForce()</c>.</summary>
    public BattleForce DefendingForce => _defendingForce;

    /// <summary>The side of a colour, <c>Arena::getForce()</c>.</summary>
    /// <param name="color">The colour; anything but the attacking colour answers the defending side.</param>
    /// <returns>The force.</returns>
    public BattleForce GetForce(PlayerColor color) => _attackingForce.Color == color ? _attackingForce : _defendingForce;

    /// <summary>The other side, <c>Arena::getEnemyForce()</c>.</summary>
    /// <param name="color">One side's colour.</param>
    /// <returns>The opposite force.</returns>
    public BattleForce GetEnemyForce(PlayerColor color) => _attackingForce.Color == color ? _defendingForce : _attackingForce;

    /// <summary>The side of the unit whose turn it is, <c>Arena::GetCurrentForce()</c>.</summary>
    public BattleForce GetCurrentForce() => GetForce(GetCurrentColor());

    /// <summary>The attacking colour, <c>Arena::getAttackingArmyColor()</c>.</summary>
    public PlayerColor AttackingArmyColor => _attackingForce.Color;

    /// <summary>The defending colour, <c>Arena::getDefendingArmyColor()</c>.</summary>
    public PlayerColor DefendingArmyColor => _defendingForce.Color;

    /// <summary>The attacking commander, <c>Arena::getAttackingArmyCommander()</c>.</summary>
    public IArmyCommander? AttackingArmyCommander => _attackingForce.Commander;

    /// <summary>The defending commander, <c>Arena::getDefendingArmyCommander()</c>.</summary>
    public IArmyCommander? DefendingArmyCommander => _defendingForce.Commander;

    /// <summary>The commander of a colour, <c>Arena::getCommander()</c>.</summary>
    /// <param name="color">The colour; anything but the attacking colour answers the defending commander.</param>
    /// <returns>The hero or captain, or <see langword="null"/>.</returns>
    public IArmyCommander? GetCommander(PlayerColor color) =>
        _attackingForce.Color == color ? _attackingForce.Commander : _defendingForce.Commander;

    /// <summary>The other side's commander, <c>Arena::getEnemyCommander()</c>.</summary>
    /// <param name="color">One side's colour.</param>
    /// <returns>The opposite hero or captain, or <see langword="null"/>.</returns>
    public IArmyCommander? GetEnemyCommander(PlayerColor color) =>
        _attackingForce.Color == color ? _defendingForce.Commander : _attackingForce.Commander;

    /// <summary>The commander of the side whose turn it is, <c>Arena::GetCurrentCommander()</c>.</summary>
    public IArmyCommander? GetCurrentCommander() => GetCommander(GetCurrentColor());

    /// <summary>
    /// The colour of the unit whose turn it is, <c>Arena::GetCurrentColor()</c>.
    /// </summary>
    /// <remarks>
    /// 参考在这里断言「一定有当前单位」，因为只有回合推进时才问。没有当前单位时按参考发布版行为回答
    /// <see cref="PlayerColor.None"/>。
    /// </remarks>
    /// <returns>The acting colour, or <see cref="PlayerColor.None"/> when no unit acts.</returns>
    public PlayerColor GetCurrentColor()
    {
        if (_currentUnit == null)
        {
            Debug.Assert(false, "The current colour is asked for while no unit is active.");
            return PlayerColor.None;
        }

        return _currentUnit.GetCurrentOrArmyColor();
    }

    /// <summary>
    /// The colour of the other army, <c>Arena::GetOppositeColor()</c>.
    /// </summary>
    /// <param name="color">One army's colour; anything but the attacking colour gives the attacking colour.</param>
    /// <returns>The opposite army's colour.</returns>
    public PlayerColor GetOppositeColor(PlayerColor color) =>
        color == _attackingForce.Color ? _defendingForce.Color : _attackingForce.Color;

    /// <summary>The unit on a cell, <c>Arena::GetTroopBoard()</c>.</summary>
    /// <param name="index">The cell index.</param>
    /// <returns>The stack, or <see langword="null"/> when the cell is empty or off the board.</returns>
    public BattleUnit? GetTroopBoard(int index) => _board.GetCell(index)?.Unit;

    /// <summary>The unit with a UID, <c>Arena::GetTroopUID()</c>.</summary>
    /// <param name="uid">The id handed out when the stack entered the battle.</param>
    /// <returns>The stack, or <see langword="null"/> when neither side has it.</returns>
    public BattleUnit? GetTroopUID(uint uid) => _attackingForce.FindUid(uid) ?? _defendingForce.FindUid(uid);

    /// <summary>Whether the battle is still undecided, <c>Arena::BattleValid()</c>.</summary>
    public bool BattleValid() =>
        _attackingForce.IsValid()
        && _defendingForce.IsValid()
        && _battleResult.Attacker == BattleOutcome.None
        && _battleResult.Defender == BattleOutcome.None;

    /// <summary>The path a unit can walk this turn, <c>Arena::GetPath()</c>.</summary>
    /// <param name="unit">The walker.</param>
    /// <param name="position">The destination.</param>
    /// <returns>The cells, or an empty array when the destination is out of reach.</returns>
    public int[] GetPath(BattleUnit unit, BattlePosition position) => _pathfinder.BuildPath(unit, position);

    /// <summary>Whether a position can be reached, <c>Arena::isPositionReachable()</c>.</summary>
    public bool IsPositionReachable(BattleUnit unit, BattlePosition position, bool isOnCurrentTurn) =>
        _pathfinder.IsPositionReachable(unit, position, isOnCurrentTurn);

    /// <summary>The cost of walking to a reachable position, <c>Arena::CalculateMoveCost()</c>.</summary>
    public uint CalculateMoveCost(BattleUnit unit, BattlePosition position) => _pathfinder.GetCost(unit, position);

    /// <summary>The number of moves to a reachable position, <c>Arena::CalculateMoveDistance()</c>.</summary>
    public uint CalculateMoveDistance(BattleUnit unit, BattlePosition position) => _pathfinder.GetDistance(unit, position);

    /// <summary>Every cell the unit's head may reach this turn, <c>Arena::getAllAvailableMoves()</c>.</summary>
    public int[] GetAllAvailableMoves(BattleUnit unit) => _pathfinder.GetAllAvailableMoves(unit);

    /// <summary>
    /// The closest reachable cell towards a destination, <c>Arena::getClosestReachablePosition()</c>.
    /// </summary>
    public BattlePosition GetClosestReachablePosition(BattleUnit unit, BattlePosition position) =>
        _pathfinder.GetClosestReachablePosition(unit, position);

    /// <summary>
    /// The free cell next to where a hero stands, <c>Arena::GetFreePositionNearHero()</c>
    /// (<c>battle_arena.cpp:871-897</c>).
    /// </summary>
    /// <param name="heroColor">The colour of the hero; the attacking side stands on the left.</param>
    /// <returns>The first free cell of that side, or <c>-1</c> when the colour is neither side or all three are taken.</returns>
    public int GetFreePositionNearHero(PlayerColor heroColor)
    {
        int[] cells;

        if (_attackingForce.Color == heroColor)
        {
            cells = [11, 22, 33];
        }
        else if (_defendingForce.Color == heroColor)
        {
            cells = [21, 32, 43];
        }
        else
        {
            return -1;
        }

        foreach (int cell in cells)
        {
            if (_board.GetCell(cell)!.IsPassable(true))
            {
                return cell;
            }
        }

        return -1;
    }

    /// <summary>Hands the turn to a unit; used by the turn loop and by tests.</summary>
    /// <param name="unit">The unit to make current, or <see langword="null"/>.</param>
    internal void SetCurrentUnit(BattleUnit? unit) => _currentUnit = unit;

    /// <summary>Starts a new round, <c>Arena::Turns()</c> round bookkeeping.</summary>
    internal void StartNewRound() => ++_turnNumber;

    private BattleCell Cell(int index) => _board.GetCell(index)!;

    /// <summary>
    /// Places the ground decoration and the obstacles of a field battle, <c>battle_arena.cpp:427-452</c>.
    /// </summary>
    private void SetupFieldObstacles(World world, int tileIndex)
    {
        Tile tile = world.GetTile(tileIndex);

        // Do not use the battle's random source here: the layout of a tile must not depend on the fight.
        Pcg32 seededGenerator = new(unchecked(world.MapSeed + (uint)tileIndex));

        if (Rand.GetWithGen(0U, 99U, seededGenerator) < 40)
        {
            _coverIcn = GetCovr(tile.Ground, seededGenerator);
        }

        _board.SetCovrObjects(_coverIcn);
        _board.SetCobjObjects(tile.GetMainObjectType(world, false), tile.Ground, seededGenerator);
    }

    /// <summary>The decoration set allowed on a ground, <c>GetCovr()</c> (<c>battle_arena.cpp:246-300</c>).</summary>
    private static IcnType GetCovr(MapGround ground, Pcg32 generator)
    {
        // 参考只给四种地面配装饰；雪地用的是 0007-0012 那一组（不是沼泽那一组）。
        IcnType[] covrs = ground switch
        {
            MapGround.Snow => [IcnType.Covr0007, IcnType.Covr0008, IcnType.Covr0009, IcnType.Covr0010, IcnType.Covr0011, IcnType.Covr0012],
            MapGround.Wasteland => [IcnType.Covr0019, IcnType.Covr0020, IcnType.Covr0021, IcnType.Covr0022, IcnType.Covr0023, IcnType.Covr0024],
            MapGround.Dirt => [IcnType.Covr0013, IcnType.Covr0014, IcnType.Covr0015, IcnType.Covr0016, IcnType.Covr0017, IcnType.Covr0018],
            MapGround.Grass => [IcnType.Covr0001, IcnType.Covr0002, IcnType.Covr0003, IcnType.Covr0004, IcnType.Covr0005, IcnType.Covr0006],
            _ => [],
        };

        return covrs.Length == 0 ? IcnType.Unknown : Rand.GetWithGen(covrs, generator);
    }
}
