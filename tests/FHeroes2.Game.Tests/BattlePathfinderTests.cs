using System.Linq;

using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::BattlePathfinder</c> (<c>battle_pathfinding.cpp</c>): the walk graph, its caching, and the six
/// queries the arena makes on top of it.
/// </summary>
public class BattlePathfinderTests
{
    private static BattleUnit Unit(BattleBoardCells board, MonsterType monster, int head, bool reflect = false, uint uid = 1)
    {
        bool wide = MonsterAbilities.IsWide(monster);

        return new BattleUnit(
            new Troop(monster, 1),
            new BattlePosition(board, head, wide, reflect),
            reflect,
            uid);
    }

    [Fact]
    public void ANeighbouringCellCostsOneStep()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        BattlePosition target = new(board, 31, false, false);

        Assert.True(pathfinder.IsPositionReachable(unit, target, true));
        Assert.Equal(1u, pathfinder.GetCost(unit, target));
        Assert.Equal(1u, pathfinder.GetDistance(unit, target));
        Assert.Equal(new[] { 31 }, pathfinder.BuildPath(unit, target));
    }

    [Fact]
    public void AnEmptyPositionIsNeverReachable()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        // 越界下标造出来的空位置：参考在开头就答「到不了」，不做任何断言。
        BattlePosition offBoard = new(board, 200, false, false);

        Assert.False(pathfinder.IsPositionReachable(unit, offBoard, false));
    }

    [Fact]
    public void ABoulderMakesTheCellUnreachableAndTheCacheNotices()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        BattlePosition target = new(board, 31, false, false);

        Assert.True(pathfinder.IsPositionReachable(unit, target, true));

        // 回合中途棋盘被改动：缓存必须重建，否则会沿着一条已经不存在的路走。
        board.GetCell(31)!.Object = 1;

        Assert.False(pathfinder.IsPositionReachable(unit, target, false));
    }

    [Fact]
    public void AFarTargetIsReachableInPrincipleButNotThisTurn()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        uint speed = unit.GetSpeed();
        Assert.InRange(speed, 1u, 5u);

        BattlePosition far = new(board, 96, false, false);

        Assert.True(pathfinder.IsPositionReachable(unit, far, false));
        Assert.False(pathfinder.IsPositionReachable(unit, far, true));

        BattlePosition closest = pathfinder.GetClosestReachablePosition(unit, far);

        Assert.NotNull(closest.Head);

        // 空旷棋盘上最短路不会有弯，于是「这一步」的六边形距离正好等于步数。
        Assert.Equal(speed, BattleBoard.GetDistance(30, closest.HeadIndex));
    }

    [Fact]
    public void AvailableMovesFillTheRadiusOfTheSpeed()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        uint speed = unit.GetSpeed();

        int[] expected = BattleBoard.GetDistanceIndexes(30, speed).OrderBy(index => index).ToArray();

        Assert.Equal(expected, pathfinder.GetAllAvailableMoves(unit));
    }

    [Fact]
    public void AFlyerLandsAnywhereInOneGo()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Gargoyle, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        Assert.False(unit.IsWide);

        BattlePosition far = new(board, 96, false, false);

        Assert.True(pathfinder.IsPositionReachable(unit, far, true));

        // 飞过去一步就到，代价是 1，但「走了几步」按直线距离算。
        Assert.Equal(1u, pathfinder.GetCost(unit, far));
        Assert.Equal(BattleBoard.GetDistance(30, 96), pathfinder.GetDistance(unit, far));
        Assert.Equal(new[] { 96 }, pathfinder.BuildPath(unit, far));
    }

    [Fact]
    public void TurningAroundIsNotAMovement()
    {
        BattleBoardCells board = new();
        BattleUnit wolf = Unit(board, MonsterType.Wolf, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        Assert.True(wolf.IsWide);
        Assert.Equal(29, wolf.TailIndex);

        // 头尾互换：同一个身子转了个身，格子没换。
        BattlePosition flipped = new(board, 29, true, true);

        Assert.True(pathfinder.IsPositionReachable(wolf, flipped, true));
        Assert.Equal(0u, pathfinder.GetCost(wolf, flipped));
        Assert.Equal(0u, pathfinder.GetDistance(wolf, flipped));
    }

    [Fact]
    public void AWideUnitStepsSidewaysHalfACell()
    {
        BattleBoardCells board = new();
        BattleUnit wolf = Unit(board, MonsterType.Wolf, 30);
        BattlePathfinder pathfinder = new(board, false, _ => false);

        // 头 31、尾 30：整队向右挪半格。
        BattlePosition sideways = new(board, 31, true, false);

        Assert.Equal(1u, pathfinder.GetCost(wolf, sideways));
        Assert.Equal(new[] { 31 }, pathfinder.BuildPath(wolf, sideways));
    }

    [Fact]
    public void TheMoatSwallowsTheRestOfTheTurn()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 6);

        // 有护城河，桥对这个单位不通（这里不关心桥：7 是无条件的护城河格）。
        BattlePathfinder pathfinder = new(board, true, _ => false);

        // 罚款记在「从护城河格迈出去」这一步上，所以走进护城河只要 1，穿过去要 1 + 罚款。
        // 6 → 18（护城河）→ 29（城内），进城只可能从护城河格迈出，于是这条就是最便宜的。
        BattlePosition inner = new(board, 29, false, false);

        Assert.Equal(BattlePathfinder.MoatPenalty + 1, pathfinder.GetCost(unit, inner));

        // 罚款大到任何速度都填不平，于是当回合进不去；原则上它仍然在路网里。
        Assert.False(pathfinder.IsPositionReachable(unit, inner, true));
        Assert.True(pathfinder.IsPositionReachable(unit, inner, false));
    }

    [Fact]
    public void TheDrawbridgeGateOnlyHurtsWhenItIsClosed()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 38);

        // 38 → 49（闸门格）→ 50：闸门放下时两步就到，吊起来时 49 是水，穿过它要付罚款。
        BattlePosition inner = new(board, 50, false, false);

        Assert.Equal(2u, new BattlePathfinder(board, true, _ => true).GetCost(unit, inner));
        Assert.Equal(BattlePathfinder.MoatPenalty + 1, new BattlePathfinder(board, true, _ => false).GetCost(unit, inner));
    }

    [Fact]
    public void AWideUnitNeedsRoomForItsTail()
    {
        BattleBoardCells board = new();
        BattleUnit wolf = Unit(board, MonsterType.Wolf, 30);

        // 10 号格自己在最右一列，右边没有格子，只能把尾巴放到 9 号格。
        Assert.True(board.GetCell(10)!.IsPassable(true));
        Assert.True(board.GetCell(10)!.IsPassableForUnit(wolf));

        // 堵死唯一的尾巴候选之后，格子本身还是空的，但宽单位已经站不上去。
        board.GetCell(9)!.Object = 1;

        Assert.True(board.GetCell(10)!.IsPassable(true));
        Assert.False(board.GetCell(10)!.IsPassableForUnit(wolf));
    }

    [Fact]
    public void ANarrowUnitOnlyAsksAboutTheCellItLandsOn()
    {
        BattleBoardCells board = new();
        BattleUnit unit = Unit(board, MonsterType.Peasant, 30);

        BattleCell from = board.GetCell(30)!;
        BattleCell to = board.GetCell(31)!;

        Assert.True(to.IsPassableFromAdjacent(unit, from));
        Assert.True(to.IsPassableFromAdjacent(unit, board.GetCell(42)!));

        to.Object = 1;

        Assert.False(to.IsPassableFromAdjacent(unit, from));
    }

    [Fact]
    public void NodeIndexesCompareByValue()
    {
        Assert.Equal(new BattleNodeIndex(3, -1), new BattleNodeIndex(3, -1));
        Assert.NotEqual(new BattleNodeIndex(3, -1), new BattleNodeIndex(3, 4));
        Assert.Equal(new BattleNodeIndex(3, 4).GetHashCode(), new BattleNodeIndex(3, 4).GetHashCode());
        Assert.True(new BattleNodeIndex(3, -1).IsValid);
        Assert.False(BattleNodeIndex.None.IsValid);

        // 「未到达」只能显式构造：全零结构的父节点是 ( 0, 0 )，那是一个合法格子。
        Assert.False(BattlePathNode.None.IsReached);
    }
}
