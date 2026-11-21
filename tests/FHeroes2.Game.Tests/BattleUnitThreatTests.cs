using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the threat arithmetic the battle AI stands on (<c>battle_troop.cpp:449-516</c>, <c>1007-1165</c>):
/// <see cref="BattleUnit.EstimateRetaliatoryDamage"/>, <see cref="BattleUnit.GetPotentialDamage"/> and
/// <see cref="BattleUnit.EvaluateThreatForUnit"/>.
/// </summary>
/// <remarks>
/// 期望值都从被移植的函数之外算出来（<see cref="BattleUnit.CalculateMinDamage"/> 与被测函数互为对照，
/// 怪物属性表则直接查 <see cref="MonsterStatistics"/>），这样属性表一改测试不用跟着改。
/// </remarks>
public class BattleUnitThreatTests
{
    private static BattleUnit Spawn(
        MonsterType monster,
        uint count,
        int head,
        PlayerColor color = PlayerColor.None,
        uint uid = 1,
        bool wide = false)
    {
        BattlePosition position = new(new BattleBoardCells(), head, wide, false);

        return new BattleUnit(new Troop(monster, count), position, false, uid) { Color = color };
    }

    private static BattleUnit Attacker(uint count = 10) => Spawn(MonsterType.Peasant, count, 5);

    private static BattleUnit Defender(uint count = 10, uint uid = 2) => Spawn(MonsterType.Peasant, count, 6, uid: uid);

    /// <summary>单兵的平均伤害，参考里 <c>Monster::GetDamageMin/Max()</c> 的用法。</summary>
    private static uint DamagePerCreature(MonsterType monster)
    {
        MonsterBattleStats stats = MonsterStatistics.GetBattleStats(monster);

        return (uint)((stats.DamageMin + stats.DamageMax) / 2);
    }

    [Fact]
    public void GetPotentialDamageAveragesTheTwoExtremes()
    {
        BattleUnit attacker = Attacker();
        BattleUnit defender = Defender();

        Assert.Equal((attacker.CalculateMinDamage(defender) + attacker.CalculateMaxDamage(defender)) / 2, attacker.GetPotentialDamage(defender));
    }

    [Fact]
    public void GetPotentialDamageTakesTheUpperExtremeWhenBlessed()
    {
        BattleUnit attacker = Attacker();
        BattleUnit defender = Defender();

        attacker.SetModes(BattleMonsterState.SpellBless);

        Assert.Equal(attacker.CalculateMaxDamage(defender), attacker.GetPotentialDamage(defender));
    }

    [Fact]
    public void GetPotentialDamageTakesTheLowerExtremeWhenCursed()
    {
        BattleUnit attacker = Attacker();
        BattleUnit defender = Defender();

        attacker.SetModes(BattleMonsterState.SpellCurse);

        Assert.Equal(attacker.CalculateMinDamage(defender), attacker.GetPotentialDamage(defender));
    }

    [Fact]
    public void EstimateRetaliatoryDamageIsTheSurvivingCreaturesTimesTheirDamage()
    {
        BattleUnit unit = Defender(count: 5);

        // 一点伤都没受：五个兵都还在。
        Assert.Equal(5u * DamagePerCreature(MonsterType.Peasant), unit.EstimateRetaliatoryDamage(0));
    }

    [Fact]
    public void EstimateRetaliatoryDamageIsZeroWhenTheWholeStackWouldDie()
    {
        BattleUnit unit = Defender(count: 5);

        Assert.Equal(0u, unit.EstimateRetaliatoryDamage(unit.HitPoints));
        Assert.Equal(0u, unit.EstimateRetaliatoryDamage(unit.HitPoints + 1));
    }

    [Fact]
    public void EstimateRetaliatoryDamageIsZeroOnceTheUnitHasRetaliated()
    {
        BattleUnit unit = Defender();

        unit.SetModes(BattleMonsterState.Retaliated);

        Assert.Equal(0u, unit.EstimateRetaliatoryDamage(0));
    }

    [Fact]
    public void EstimateRetaliatoryDamageIsZeroForMirrorImagesAndHypnotizedUnits()
    {
        BattleUnit mirror = Defender(uid: 3);
        BattleUnit hypnotized = Defender(uid: 4);

        mirror.SetModes(BattleMonsterState.CapMirrorImage);
        hypnotized.SetModes(BattleMonsterState.SpellHypnotize);

        Assert.Equal(0u, mirror.EstimateRetaliatoryDamage(0));
        Assert.Equal(0u, hypnotized.EstimateRetaliatoryDamage(0));
    }

    [Fact]
    public void EvaluateThreatForUnitIsNegativeForTheOwnSide()
    {
        BattleUnit attacker = Spawn(MonsterType.Peasant, 10, 5, PlayerColor.Blue);
        BattleUnit defender = Spawn(MonsterType.Peasant, 10, 6, PlayerColor.Blue, uid: 2);

        Assert.True(attacker.EvaluateThreatForUnit(defender) < 0, "A unit must not plan to hit its own side.");
    }

    [Fact]
    public void EvaluateThreatForUnitDropsWhenTheUnitHasAlreadyActed()
    {
        BattleUnit fresh = Spawn(MonsterType.Peasant, 10, 5, PlayerColor.Blue, uid: 1);
        BattleUnit moved = Spawn(MonsterType.Peasant, 10, 5, PlayerColor.Blue, uid: 2);
        BattleUnit defender = Spawn(MonsterType.Peasant, 10, 6, PlayerColor.Red, uid: 3);

        moved.SetModes(BattleMonsterState.Moved);

        double expected = fresh.EvaluateThreatForUnit(defender) / 1.25;

        Assert.Equal(expected, moved.EvaluateThreatForUnit(defender), 6);
    }

    [Fact]
    public void EvaluateThreatForUnitIsZeroForAnImmovableUnit()
    {
        BattleUnit attacker = Spawn(MonsterType.Peasant, 10, 5, PlayerColor.Blue);
        BattleUnit defender = Spawn(MonsterType.Peasant, 10, 6, PlayerColor.Red, uid: 2);

        attacker.SetModes(BattleMonsterState.SpellBlind);

        Assert.Equal(0.0, attacker.EvaluateThreatForUnit(defender));
    }
}
