using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the part of <c>Battle::Unit</c> that does not need the arena, a hero or the spell layer
/// (<c>battle_troop.cpp:341-360</c>, <c>1388-1420</c>): counters, placement, modes and speed.
/// </summary>
public class BattleUnitTests
{
    private static BattleUnit Spawn(MonsterType monster, uint count, int head, bool wide, bool reflect, uint uid = 1)
    {
        BattleBoardCells board = new();

        return new BattleUnit(new Troop(monster, count), new BattlePosition(board, head, wide, reflect), reflect, uid);
    }

    [Fact]
    public void TheCountersStartFull()
    {
        Troop troop = new(MonsterType.Peasant, 10);
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);

        Assert.Equal(MonsterType.Peasant, unit.Monster);
        Assert.Equal(10u, unit.Count);
        Assert.Equal(10u, unit.InitialCount);
        Assert.Equal(10u, unit.MaxCount);
        Assert.Equal(0u, unit.Dead);
        Assert.Equal(troop.GetHitPoints(), unit.HitPoints);
        Assert.Equal((uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).Shots, unit.GetShots());
        Assert.True(unit.IsUid(1));
        Assert.False(unit.IsUid(2));
    }

    [Fact]
    public void AUnitOccupiesTheCellsItStandsOn()
    {
        BattleUnit cavalry = Spawn(MonsterType.Cavalry, 5, 45, wide: true, reflect: false);

        Assert.True(cavalry.IsWide);
        Assert.Equal(45, cavalry.HeadIndex);
        Assert.Equal(44, cavalry.TailIndex);
        Assert.Same(cavalry, cavalry.Position.Board.GetCell(45)!.Unit);
        Assert.Same(cavalry, cavalry.Position.Board.GetCell(44)!.Unit);
    }

    [Fact]
    public void MovingReleasesTheOldCells()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        BattleBoardCells board = unit.Position.Board;

        unit.SetPosition(50);

        Assert.Equal(50, unit.HeadIndex);
        Assert.Null(board.GetCell(40)!.Unit);
        Assert.Same(unit, board.GetCell(50)!.Unit);
    }

    [Fact]
    public void TurningAroundMovesTheTailToTheOtherSide()
    {
        BattleUnit cavalry = Spawn(MonsterType.Cavalry, 5, 45, wide: true, reflect: false);
        BattleBoardCells board = cavalry.Position.Board;

        Assert.Equal(44, cavalry.TailIndex);

        cavalry.SetReflection(true);

        Assert.True(cavalry.IsReflect);
        Assert.Equal(46, cavalry.TailIndex);
        Assert.Null(board.GetCell(44)!.Unit);
        Assert.Same(cavalry, board.GetCell(46)!.Unit);
    }

    [Fact]
    public void SlowAndHasteReplaceTheSpeedInsteadOfStacking()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        uint baseSpeed = (uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).Speed;

        Assert.Equal(baseSpeed, unit.GetSpeed());

        unit.SetModes(BattleMonsterState.SpellSlow);
        Assert.Equal(Speed.GetSlowSpeedFromSpell(baseSpeed), unit.GetSpeed());

        // 两个都在时先判加速——参考是 if (haste) ... if (slow) ...，所以加速赢。
        unit.SetModes(BattleMonsterState.SpellHaste);
        Assert.Equal(Speed.GetHasteSpeedFromSpell(baseSpeed), unit.GetSpeed());

        unit.ResetModes(BattleMonsterState.SpellHaste);
        Assert.Equal(Speed.GetSlowSpeedFromSpell(baseSpeed), unit.GetSpeed());
    }

    [Fact]
    public void AUnitThatAlreadyActedHasNoSpeedLeftUnlessTheCheckIsSkipped()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        uint baseSpeed = (uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).Speed;

        unit.SetModes(BattleMonsterState.Moved);

        // 跳过定身检查时连「已行动」也一起跳过，拿到的是纯数据速度。
        Assert.Equal(baseSpeed, unit.GetSpeed(true, false));
        Assert.Equal(baseSpeed, unit.GetSpeed(true, true));

        // 不跳过定身检查时，「已行动」被并进那个掩码里，于是没有速度。
        Assert.Equal(Speed.Standing, unit.GetSpeed(false, false));
        Assert.Equal(baseSpeed, unit.GetSpeed(false, true));
    }

    [Fact]
    public void ADebilitatedOrEmptyStackCannotMove()
    {
        BattleUnit blinded = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        blinded.SetModes(BattleMonsterState.SpellBlind);

        Assert.True(blinded.IsImmovable);
        Assert.Equal(Speed.Standing, blinded.GetSpeed());

        BattleUnit paralyzed = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        paralyzed.SetModes(BattleMonsterState.SpellStone);

        Assert.True(paralyzed.IsImmovable);

        BattleUnit dead = Spawn(MonsterType.Peasant, 0, 40, wide: false, reflect: false);
        Assert.Equal(Speed.Standing, dead.GetSpeed());
    }

    [Fact]
    public void ATowerStillReportsItsCreatureSpeed()
    {
        BattleUnit tower = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        uint baseSpeed = (uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).Speed;

        tower.SetModes(BattleMonsterState.CapTower);

        // 参考的 GetSpeed 不看 CAP_TOWER：塔之所以不动，是竞技场不给它行动，不是速度为零。
        Assert.Equal(baseSpeed, tower.GetSpeed());
        Assert.False(tower.IsHandFighting());
    }

    [Fact]
    public void TheAbilityFlagsComeFromTheCreature()
    {
        BattleUnit peasant = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);

        Assert.False(peasant.IsWide);
        Assert.False(peasant.IsFlying);
        Assert.False(peasant.IsDoubleAttack);
        Assert.False(peasant.IsRegenerating);

        BattleUnit wolf = Spawn(MonsterType.Wolf, 10, 40, wide: true, reflect: false);
        Assert.True(wolf.IsDoubleAttack);

        BattleUnit troll = Spawn(MonsterType.Troll, 10, 40, wide: false, reflect: false);
        Assert.True(troll.IsRegenerating);
    }

    [Fact]
    public void AGriffinKeepsItsRetaliationButLosesItsWingsWhenBlinded()
    {
        BattleUnit griffin = Spawn(MonsterType.Griffin, 10, 40, wide: true, reflect: false);

        Assert.True(griffin.IsFlying);
        Assert.True(griffin.IsRetaliationAllowed);

        // 无限反击的兵种不看「已反击」标记。
        griffin.SetModes(BattleMonsterState.Retaliated);
        Assert.True(griffin.IsRetaliationAllowed);

        griffin.SetModes(BattleMonsterState.SpellBlind);
        Assert.False(griffin.IsFlying);
        Assert.True(griffin.IsImmovable);
    }

    [Fact]
    public void AUnitWithoutUnlimitedRetaliationGetsOnePerTurn()
    {
        BattleUnit peasant = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);

        Assert.True(peasant.IsRetaliationAllowed);

        peasant.SetModes(BattleMonsterState.Retaliated);
        Assert.False(peasant.IsRetaliationAllowed);
    }

    [Fact]
    public void TheMirrorLinkIsPlainState()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40, wide: false, reflect: false);
        BattleUnit mirror = Spawn(MonsterType.Peasant, 10, 50, wide: false, reflect: false, uid: 2);

        Assert.Null(unit.MirrorUnit);

        unit.MirrorUnit = mirror;
        Assert.Same(mirror, unit.MirrorUnit);
    }
}
