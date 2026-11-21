using System.Diagnostics;

using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;

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
    private readonly BattleTroopsUidGenerator _uidGenerator;
    private readonly SpellStorage _usedSpells = new();

    private BattleResult _battleResult;

    /// <summary>Sides whose units the AI plays for, <c>Arena::_autoCombatColors</c>.</summary>
    private PlayerColor _autoCombatColors;
    private BattleUnit? _currentUnit;
    private IcnType _coverIcn = IcnType.Unknown;
    private uint _turnNumber;

    /// <summary>
    /// The side of the last unit that actually took an action, <c>Arena::_lastActiveUnitArmyColor</c>.
    /// </summary>
    /// <remarks>同速时的先手判据：上一回合谁最后出手，这一轮就轮到对面先。战斗开始时是 <see cref="PlayerColor.None"/>。</remarks>
    private PlayerColor _lastActiveUnitArmyColor = PlayerColor.None;

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

        _uidGenerator = new BattleTroopsUidGenerator();
        _attackingForce = new BattleForce(attackingArmy, _board, false, _uidGenerator);
        _defendingForce = new BattleForce(defendingArmy, _board, true, _uidGenerator);

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
                _towers[0] = new BattleTower(castle, BattleTowerType.Left, _uidGenerator.GetUnique());
            }

            _towers[1] = new BattleTower(castle, BattleTowerType.Center, _uidGenerator.GetUnique());

            if (castle.IsBuild(FHeroes2.Game.Castle.BuildingType.RightTurret))
            {
                _towers[2] = new BattleTower(castle, BattleTowerType.Right, _uidGenerator.GetUnique());
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

    /// <summary>
    /// Every spell cast in this battle so far, <c>Arena::_usedSpells</c> / <c>GetUsedSpells()</c>.
    /// </summary>
    /// <remarks>战后给鹰眼技能抄写用的清单：谁施过什么法术，战利品结算时按它决定能否学到。</remarks>
    public SpellStorage UsedSpells => _usedSpells;

    /// <summary>The sides currently played by the AI, <c>Arena::_autoCombatColors</c>.</summary>
    public PlayerColor AutoCombatColors => _autoCombatColors;

    /// <summary>Whether one side has handed its units to the AI, <c>Arena::isAutoCombat()</c>.</summary>
    /// <param name="color">The side to ask about.</param>
    /// <returns>True when auto combat is on for that side.</returns>
    public bool IsAutoCombat(PlayerColor color) => (_autoCombatColors & color) != 0;

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

    /// <summary>
    /// Whether the side may run away, <c>Arena::CanRetreatOpponent()</c> (<c>battle_arena.cpp:906-910</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两个条件：那一边的指挥官得是英雄（队长不能带队逃跑），而且它要么是**攻方**、要么不在城里。
    /// 「在城里」指守城战里被迫防守的一方——城墙替它挡着，跑就等于把城交出去。
    /// </para>
    /// <para>
    /// 注意这答的是「允许不允许」，界面还要再问「玩家有没有这个选项」；AI 从来不逃，但它不是靠这个函数拦住的。
    /// </para>
    /// </remarks>
    /// <param name="color">The side to ask about.</param>
    /// <returns>Whether that side can retreat.</returns>
    public bool CanRetreatOpponent(PlayerColor color)
    {
        IArmyCommander? commander = GetCommander(color);

        return commander is { IsHeroes: true } && (color == _attackingForce.Color || commander.InCastle == null);
    }

    /// <summary>
    /// Whether the side may give up, <c>Arena::CanSurrenderOpponent()</c> (<c>battle_arena.cpp:899-904</c>).
    /// </summary>
    /// <remarks>
    /// 投降要付钱、而且要有人收钱：自己得是英雄，对面得有指挥官（英雄或队长都行）。对面是一群野兵时无从谈判。
    /// </remarks>
    /// <param name="color">The side to ask about.</param>
    /// <returns>Whether that side can surrender.</returns>
    public bool CanSurrenderOpponent(PlayerColor color)
    {
        IArmyCommander? commander = GetCommander(color);

        return commander is { IsHeroes: true } && GetCommander(GetOppositeColor(color)) != null;
    }

    /// <summary>
    /// Whether the fight is being played by the computer, <c>Arena::AutoCombatInProgress()</c>
    /// (<c>battle_arena.cpp:1458-1472</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只看**当前出手的这一方**：攻城战里攻方开了自动、守方还在人手里，轮到守方时这仍然是 <see langword="false"/>。
    /// 没有人出手（<see cref="_currentUnit"/> 为空）时也答否——这时没有「正在进行」可言。
    /// </para>
    /// <para>
    /// 自动战斗只可能开给人类控制的一方：AI 本来就不等人，所以这里断言当前方不是 AI。
    /// </para>
    /// </remarks>
    public bool AutoCombatInProgress
    {
        get
        {
            if (_currentUnit == null)
            {
                return false;
            }

            if ((_autoCombatColors & _currentUnit.GetCurrentColor()) == 0)
            {
                return false;
            }

            Debug.Assert(GetCurrentForce().GetControl() != System.PlayerControl.Ai,
                "Auto combat can only be switched on for a human-controlled side.");

            return true;
        }
    }

    /// <summary>
    /// Whether the other side's AI is playing while this side's auto combat runs,
    /// <c>Arena::EnemyOfAIHasAutoCombatInProgress()</c> (<c>battle_arena.cpp:1474-1491</c>).
    /// </summary>
    /// <remarks>
    /// 人类把战斗交给电脑之后，「对面也是电脑」这件事在界面上不该再弹提示；这个函数专门回答那个场景：
    /// 当前方是 AI、对面是人、而人那边已经开了自动。
    /// </remarks>
    /// <returns>Whether the AI's opponent is running on auto combat.</returns>
    public bool EnemyOfAIHasAutoCombatInProgress
    {
        get
        {
            if (_currentUnit == null || GetCurrentForce().GetControl() != System.PlayerControl.Ai)
            {
                return false;
            }

            BattleForce enemyForce = GetEnemyForce(GetCurrentColor());

            if (enemyForce.GetControl() == System.PlayerControl.Ai)
            {
                return false;
            }

            return (_autoCombatColors & enemyForce.Color) != 0;
        }
    }

    /// <summary>
    /// Whether the player could hand the fight over to the computer, <c>Arena::CanToggleAutoCombat()</c>
    /// (<c>battle_arena.cpp:1493-1500</c>).
    /// </summary>
    /// <remarks>
    /// 战斗已经开始（有人出手）才有「交给电脑」这回事；而且 AI 那一方没有开关，它本来就不等人。
    /// </remarks>
    public bool CanToggleAutoCombat => _currentUnit != null && GetCurrentForce().GetControl() != System.PlayerControl.Ai;

    /// <summary>
    /// Whether spells are switched off for this fight, <c>Arena::IsSpellcastDisabled()</c>
    /// (<c>battle_arena.cpp:912-925</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 唯一的来源是「否定之球」：任意一方的指挥官带着它，整场战斗双方都不能施法。不含剧本开关，也不含自动战斗
    /// ——自动战斗里 AI 照样可以施法（<c>ai_battle.cpp:534</c> 用它决定英雄要不要施法）。
    /// </para>
    /// <para>
    /// 这是全场的性质而不是某一方的：带着它的一方自己也放不出法术。
    /// </para>
    /// </remarks>
    public bool IsSpellcastDisabled =>
        IsSphereOfNegationPresent(_attackingForce.Commander) || IsSphereOfNegationPresent(_defendingForce.Commander);

    private static bool IsSphereOfNegationPresent(IArmyCommander? commander) =>
        commander != null && commander.BagArtifacts.IsBonusPresent(ArtifactBonusType.DisableAllSpellCombatCasting);

    /// <summary>
    /// Whether casting this spell right now would be refused, <c>Arena::isDisableCastSpell()</c>
    /// (<c>battle_arena.cpp:927-1008</c>).
    /// </summary>
    /// <remarks>
    /// 依次排除：整场禁魔、没有指挥官、本回合已经施过法、空法术（空法术用来「探一探能不能施法」，
    /// 所以不算被禁）、地震在没有城堡时、召唤已有另一种元素时、召唤时英雄旁边没有空格，
    /// 最后是「场上有单位吃这个法术，或坟场里有能复活的尸体」。
    /// </remarks>
    /// <param name="spell">The spell about to be cast.</param>
    /// <returns>Whether the cast would be refused.</returns>
    public bool IsDisableCastSpell(Spell spell)
    {
        if (IsSpellcastDisabled)
        {
            return true;
        }

        IArmyCommander? commander = GetCurrentCommander();

        if (commander == null)
        {
            return true;
        }

        if ((commander.Modes & HeroModes.Spellcasted) != 0)
        {
            return true;
        }

        // 空法术拿来探「能不能施法」，本身不算被禁。
        if (spell.Type == SpellType.None)
        {
            return false;
        }

        if (spell.Type == SpellType.Earthquake)
        {
            return Castle == null;
        }

        if (spell.IsSummon())
        {
            MonsterType summoned = MonsterSummons.FromSpell(spell.Type);
            Debug.Assert(summoned != MonsterType.Unknown, "Every summoning spell names an elemental.");

            BattleUnit? elemental = GetCurrentForce().FindMode(BattleMonsterState.CapSummonElement);

            if (elemental != null && elemental.Monster != summoned)
            {
                return true;
            }

            if (GetFreePositionNearHero(GetCurrentColor()) < 0)
            {
                return true;
            }

            return false;
        }

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            BattleUnit? unit = GetTroopBoard(index);

            if (unit != null)
            {
                if (unit.AllowApplySpell(spell, commander))
                {
                    return false;
                }

                continue;
            }

            if (IsAbleToResurrectFromGraveyard(index, spell))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a resurrection spell could bring something back on this cell, <c>Arena::isAbleToResurrectFromGraveyard()</c>
    /// (<c>battle_arena.cpp:1011-1057</c>).
    /// </summary>
    /// <param name="index">The cell.</param>
    /// <param name="spell">The spell about to be cast.</param>
    /// <returns>Whether the cell holds a corpse the spell can raise.</returns>
    public bool IsAbleToResurrectFromGraveyard(int index, Spell spell)
    {
        if (!spell.IsResurrect())
        {
            return false;
        }

        IArmyCommander? commander = GetCurrentCommander();

        if (commander == null)
        {
            return false;
        }

        BattleUnit? unit = GetLastResurrectableUnitFromGraveyard(index, spell);

        if (unit == null)
        {
            return false;
        }

        if (!unit.AllowApplySpell(spell, commander))
        {
            return false;
        }

        int headIndex = unit.HeadIndex;
        Debug.Assert(BattleBoard.IsValidIndex(headIndex), "A fallen unit keeps a valid head cell.");

        // 尸体上不能站着别的单位。
        if (GetTroopBoard(headIndex) != null)
        {
            return false;
        }

        if (!unit.IsWide)
        {
            return true;
        }

        int tailIndex = unit.TailIndex;
        Debug.Assert(BattleBoard.IsValidIndex(tailIndex), "A fallen wide unit keeps a valid tail cell.");

        return GetTroopBoard(tailIndex) == null;
    }

    /// <summary>
    /// The topmost corpse on a cell that the spell can raise, <c>Arena::getLastResurrectableUnitFromGraveyard()</c>
    /// (<c>battle_arena.cpp:1079-1082</c>).
    /// </summary>
    /// <param name="index">The cell.</param>
    /// <param name="spell">The spell about to be cast.</param>
    /// <returns>The unit, or <see langword="null"/> when nothing there can be raised.</returns>
    public BattleUnit? GetLastResurrectableUnitFromGraveyard(int index, Spell spell)
    {
        if (!spell.IsResurrect())
        {
            return null;
        }

        IArmyCommander? commander = GetCurrentCommander();

        if (commander == null)
        {
            return null;
        }

        List<BattleUnit> units = _graveyard.GetUnits(index);

        // 参考从链尾往前找：最后倒下的那堆优先。
        for (int i = units.Count - 1; i >= 0; --i)
        {
            BattleUnit unit = units[i];
            Debug.Assert(!unit.IsValid, "The graveyard holds fallen units only.");

            if (unit.Army?.Color != commander.GetColor())
            {
                continue;
            }

            if (unit.AllowApplySpell(spell, commander))
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>
    /// Every unit a spell would hit when aimed at a cell, <c>Arena::GetTargetsForSpell()</c>
    /// (<c>battle_action.cpp:1046-1174</c>).
    /// </summary>
    /// <param name="hero">The commander casting the spell.</param>
    /// <param name="spell">The spell.</param>
    /// <param name="dst">The cell it is aimed at.</param>
    /// <param name="applyRandomMagicResistance">Whether to roll magic resistance; battle AI passes <see langword="true"/>.</param>
    /// <returns>The units that would be hit; empty when nothing can be affected.</returns>
    public List<BattleSpellTarget> GetTargetsForSpell(
        IArmyCommander hero,
        Spell spell,
        int dst,
        bool applyRandomMagicResistance = true)
    {
        ArgumentNullException.ThrowIfNull(hero);

        List<BattleSpellTarget> targets = [];

        BattleUnit? target = GetTroopBoard(dst);

        // 链闪电与寒冰环不认「瞄准的那个单位」：前者按链子挑，后者按格子周围挑。
        if (spell.Type is SpellType.ChainLightning or SpellType.ColdRing)
        {
            target = null;
        }

        HashSet<BattleUnit> consideredTargets = [];

        if (target != null && target.AllowApplySpell(spell, hero) && consideredTargets.Add(target))
        {
            targets.Add(new BattleSpellTarget { Defender = target });
        }
        else if (target == null && IsAbleToResurrectFromGraveyard(dst, spell))
        {
            target = GetLastResurrectableUnitFromGraveyard(dst, spell);
            Debug.Assert(target != null, "A cell that can be resurrected holds a corpse.");

            if (consideredTargets.Add(target))
            {
                targets.Add(new BattleSpellTarget { Defender = target });
            }
        }
        else
        {
            switch (spell.Type)
            {
                case SpellType.ChainLightning:
                    foreach (BattleSpellTarget spellTarget in TargetsForChainLightning(hero, dst, applyRandomMagicResistance))
                    {
                        Debug.Assert(spellTarget.Defender != null, "Chain lightning always targets units.");

                        if (consideredTargets.Add(spellTarget.Defender))
                        {
                            targets.Add(spellTarget);
                        }
                    }

                    // 链闪电在选目标时已经滚过抗魔，下面不再重复。
                    applyRandomMagicResistance = false;

                    break;

                case SpellType.Fireball:
                case SpellType.MeteorShower:
                case SpellType.ColdRing:
                case SpellType.FireBlast:
                    foreach (int index in BattleBoard.GetDistanceIndexes(dst, spell.Type == SpellType.FireBlast ? 2u : 1u))
                    {
                        BattleUnit? targetUnit = GetTroopBoard(index);

                        if (targetUnit != null && targetUnit.AllowApplySpell(spell, hero) && consideredTargets.Add(targetUnit))
                        {
                            targets.Add(new BattleSpellTarget { Defender = targetUnit });
                        }
                    }

                    break;

                case SpellType.DeathRipple:
                case SpellType.DeathWave:
                case SpellType.ElementalStorm:
                case SpellType.HolyWord:
                case SpellType.HolyShout:
                case SpellType.Armageddon:
                case SpellType.MassBless:
                case SpellType.MassCure:
                case SpellType.MassCurse:
                case SpellType.MassDispel:
                case SpellType.MassHaste:
                case SpellType.MassShield:
                case SpellType.MassSlow:
                    for (int index = 0; index < BattleBoard.SizeInCells; ++index)
                    {
                        BattleUnit? unit = GetTroopBoard(index);

                        if (unit != null && unit.AllowApplySpell(spell, hero) && consideredTargets.Add(unit))
                        {
                            targets.Add(new BattleSpellTarget { Defender = unit });
                        }
                    }

                    break;

                default:
                    break;
            }
        }

        // 标出被抗魔顶掉的目标。
        foreach (BattleSpellTarget spellTarget in targets)
        {
            BattleUnit defender = spellTarget.Defender!;
            uint resist = defender.GetMagicResist(spell, hero);

            Debug.Assert(resist < 100, "A fully immune unit never gets into the target list.");

            if (applyRandomMagicResistance && resist >= Rand.GetWithGen(1U, 100U, _randomGenerator))
            {
                spellTarget.Resist = true;
            }
        }

        return targets;
    }

    /// <summary>
    /// The chain of units a chain lightning would run through, <c>Arena::TargetsForChainLightning()</c>
    /// (<c>battle_action.cpp:1014-1044</c>).
    /// </summary>
    /// <param name="hero">The commander casting the spell.</param>
    /// <param name="attackedTroopIndex">The head cell of the first unit.</param>
    /// <param name="applyRandomMagicResistance">Whether to roll magic resistance.</param>
    /// <returns>The units in the order the bolt reaches them.</returns>
    private List<BattleSpellTarget> TargetsForChainLightning(IArmyCommander hero, int attackedTroopIndex, bool applyRandomMagicResistance)
    {
        List<BattleSpellTarget> targets = [];

        BattleUnit? unit = GetTroopBoard(attackedTroopIndex);

        if (unit == null)
        {
            Debug.Assert(false, "Chain lightning is aimed at a unit.");

            return targets;
        }

        Spell chainLightning = new(SpellType.ChainLightning);

        uint firstUnitResist = unit.GetMagicResist(chainLightning, hero);

        if (firstUnitResist >= 100 || (applyRandomMagicResistance && firstUnitResist >= Rand.GetWithGen(1U, 100U, _randomGenerator)))
        {
            targets.Add(new BattleSpellTarget { Defender = unit, Resist = true });

            return targets;
        }

        List<BattleUnit> targetUnits = FindChainLightningTargetUnits(hero, unit, applyRandomMagicResistance);

        for (int i = 0; i < targetUnits.Count; ++i)
        {
            // damage 先存「链子上的次序」，真正的伤害由施法流程按次序算。
            targets.Add(new BattleSpellTarget { Defender = targetUnits[i], Damage = (uint)i });
        }

        return targets;
    }

    /// <summary>
    /// The units a chain lightning would jump to, <c>Arena::FindChainLightningTargetIndexes()</c>
    /// (<c>battle_action.cpp:960-1011</c>).
    /// </summary>
    /// <param name="hero">The commander casting the spell.</param>
    /// <param name="firstUnit">The first unit struck.</param>
    /// <param name="applyRandomMagicResistance">Whether to roll magic resistance.</param>
    /// <returns>The units, starting with <paramref name="firstUnit"/>.</returns>
    private List<BattleUnit> FindChainLightningTargetUnits(IArmyCommander hero, BattleUnit firstUnit, bool applyRandomMagicResistance)
    {
        const int ChainLightningCreatureCount = 4;

        Spell chainLightning = new(SpellType.ChainLightning);

        List<BattleUnit> result = [firstUnit];
        List<BattleUnit> ignoredTroops = [firstUnit];

        List<BattleUnit> foundTroops = BattleBoard.GetNearestTroops(this, result[^1], ignoredTroops);

        // 先把完全免疫的剔掉：它们既不吃伤害，也不该挡在链子上。
        for (int i = 0; i < foundTroops.Count;)
        {
            if (foundTroops[i].GetMagicResist(chainLightning, hero) >= 100)
            {
                ignoredTroops.Add(foundTroops[i]);
                foundTroops.RemoveAt(i);
            }
            else
            {
                ++i;
            }
        }

        while (result.Count != ChainLightningCreatureCount && foundTroops.Count > 0)
        {
            bool targetFound = false;

            for (int i = 0; i < foundTroops.Count; ++i)
            {
                uint resist = foundTroops[i].GetMagicResist(chainLightning, hero);
                Debug.Assert(resist < 100, "Fully immune units were filtered out above.");

                if (!applyRandomMagicResistance || resist < Rand.GetWithGen(1U, 100U, _randomGenerator))
                {
                    ignoredTroops.Add(foundTroops[i]);
                    result.Add(foundTroops[i]);
                    foundTroops.RemoveAt(i);
                    targetFound = true;

                    break;
                }
            }

            // 全部抗住了：挑最近的继续跳，免得链子断掉。
            if (!targetFound)
            {
                ignoredTroops.Add(foundTroops[0]);
                result.Add(foundTroops[0]);
                foundTroops.RemoveAt(0);
            }

            if (result.Count != ChainLightningCreatureCount)
            {
                foundTroops = BattleBoard.GetNearestTroops(this, result[^1], ignoredTroops);
            }
        }

        return result;
    }
}
