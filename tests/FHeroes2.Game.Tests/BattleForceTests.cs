using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Force</c> (<c>battle_army.cpp</c>): deploying an army, the bookkeeping that goes back the other
/// way, surrender price and the new-round reset.
/// </summary>
public class BattleForceTests
{
    private static GameArmy MakeArmy(params (MonsterType Monster, uint Count)[] troops)
    {
        GameArmy army = new();

        for (int index = 0; index < troops.Length; ++index)
        {
            army.GetTroop(index).Set(troops[index].Monster, troops[index].Count);
        }

        return army;
    }

    private static BattleForce Deploy(GameArmy army, bool opposite = false)
    {
        return new BattleForce(army, new BattleBoardCells(), opposite, new BattleTroopsUidGenerator());
    }

    /// <summary>A stack on a battlefield of its own, only ever used as the source of damage.</summary>
    private static BattleUnit Attacker() =>
        new(new Troop(MonsterType.Peasant, 1), new BattlePosition(new BattleBoardCells(), 40, false, false), false, 99);

    private static void Kill(BattleUnit unit, uint creatures)
    {
        uint hitPoints = (uint)MonsterStatistics.GetBattleStats(unit.Monster).HitPoints;

        unit.ApplyDamage(Attacker(), hitPoints * creatures, out _, out _);
    }

    [Fact]
    public void EveryFilledSlotBecomesAStack()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Vampire, 3));
        army.Color = PlayerColor.Blue;

        BattleForce force = Deploy(army);

        Assert.Equal(2, force.Count);
        Assert.Equal(1u, force[0].Uid);
        Assert.Equal(2u, force[1].Uid);
        Assert.Equal(22, force[0].HeadIndex);
        Assert.Equal(33, force[1].HeadIndex);
        Assert.Same(army, force[0].Army);
        Assert.Equal(PlayerColor.Blue, force[0].Color);
        Assert.Equal(PlayerColor.Blue, force.Color);
    }

    [Fact]
    public void AnEmptySlotKeepsItsPlaceInTheUidList()
    {
        GameArmy army = new();
        army.GetTroop(2).Set(MonsterType.Peasant, 7);

        BattleForce force = Deploy(army);

        Assert.Single(force);
        Assert.Equal(1u, force[0].Uid);
        Assert.Null(force.FindUid(0));
    }

    [Fact]
    public void TheMirrorSideIsShiftedRight()
    {
        GameArmy near = MakeArmy((MonsterType.Peasant, 5));
        GameArmy far = MakeArmy((MonsterType.Peasant, 5));

        Assert.Equal(22, Deploy(near)[0].HeadIndex);
        Assert.Equal(32, Deploy(far, opposite: true)[0].HeadIndex);
    }

    [Fact]
    public void WideStacksAreMovedOutOfTheirNeighbour()
    {
        GameArmy near = MakeArmy((MonsterType.Cavalry, 5));
        GameArmy far = MakeArmy((MonsterType.Cavalry, 5));

        BattleUnit own = Deploy(near)[0];
        BattleUnit other = Deploy(far, opposite: true)[0];

        Assert.Equal(23, own.HeadIndex);
        Assert.Equal(22, own.TailIndex);

        Assert.Equal(31, other.HeadIndex);
        Assert.Equal(32, other.TailIndex);
    }

    [Fact]
    public void SpreadFormationPutsEveryStackInItsOwnFile()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 5), (MonsterType.Peasant, 5), (MonsterType.Peasant, 5));
        army.IsSpreadFormation = true;

        BattleForce force = Deploy(army);

        Assert.Equal([0, 22, 44], force.Select(unit => unit.HeadIndex));
    }

    [Fact]
    public void TheForceIsValidWhileAStackStands()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 3), (MonsterType.Vampire, 2));
        BattleForce force = Deploy(army);

        Assert.True(force.IsValid());
        Assert.True(force.HasMonster(MonsterType.Peasant));

        Kill(force[0], 3);
        Assert.True(force.IsValid());
        Assert.False(force.HasMonster(MonsterType.Peasant) && force[0].IsValid);

        Kill(force[1], 2);
        Assert.False(force.IsValid());
        Assert.False(force.IsValid(considerBattlefieldArmy: false));
    }

    [Fact]
    public void TheDeadAreCountedPerStack()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Peasant, 5));
        BattleForce force = Deploy(army);

        Kill(force[0], 4);
        Kill(force[1], 5);

        Assert.Equal(9u, force.GetTotalNumberOfDeadUnits());
        Assert.Equal(9u, force.CalculateNumberOfDeadUnitsForNecromancy());
    }

    [Fact]
    public void NecromancyNeverCountsMoreThanTheStackStartedWith()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 5));
        BattleForce force = Deploy(army);

        // 一整队被打光：死亡数等于初始人数，不会因为「多打死几个」而多算。
        Kill(force[0], 5);

        Assert.Equal(5u, force.CalculateNumberOfDeadUnitsForNecromancy());
    }

    [Fact]
    public void ExperienceComesFromTheCreatureTable()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        BattleForce force = Deploy(army);

        Kill(force[0], 3);

        uint hitPoints = (uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).HitPoints;

        Assert.Equal(hitPoints * 3, force.CalculateExperienceBasedOnLosses());
    }

    [Fact]
    public void SyncingWritesTheSurvivorsBackIntoTheArmy()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Vampire, 2));
        BattleForce force = Deploy(army);

        Kill(force[0], 4);
        Kill(force[1], 2);

        force.SyncOriginalArmy();

        Assert.Equal(6u, army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Peasant, army.GetTroop(0).Monster);

        // 全灭的槽位清成空，但兵种留在原处。
        Assert.Equal(0u, army.GetTroop(1).Count);
        Assert.False(army.GetTroop(1).IsValid);
    }

    [Fact]
    public void SurrenderCostsHalfWithoutADiscount()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        BattleForce force = Deploy(army);

        uint each = (uint)force[0].GetCost().Gold;

        // 没有指挥官就没有折扣：全价。
        Assert.Equal(each * 10, force.GetSurrenderCost());
    }

    [Fact]
    public void AHeroBringsTheBaseDiscountDownToHalf()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Hero(1, MapRace.Knight);

        BattleForce force = Deploy(army);
        uint each = (uint)force[0].GetCost().Gold;

        Assert.Equal((uint)(each * 10 * 0.5 + 0.5), force.GetSurrenderCost());
    }

    [Fact]
    public void DiplomacyHalvesWhatTheHeroWouldPayAgain()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        Hero hero = new(1, MapRace.Knight);
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Diplomacy, SkillLevel.Expert));
        army.Commander = hero;

        BattleForce force = Deploy(army);
        uint each = (uint)force[0].GetCost().Gold;

        // 0.5（英雄基数）* 2（外交把基数抬回全价）* 0.2（专家折扣）
        Assert.Equal((uint)(each * 10 * 0.2 + 0.5), force.GetSurrenderCost());
    }

    [Fact]
    public void TheCostIsNeverFreeEvenWhenAlmostEverybodyIsDead()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 20));
        BattleForce force = Deploy(army);

        Kill(force[0], 20);

        // 全灭之后没有「剩下的兵」可买，但报价仍然是 1：投降不白送。
        Assert.Equal(1u, force.GetSurrenderCost());
    }

    [Fact]
    public void ANewRoundLetsTheHeroCastAgain()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        Hero hero = new(1, MapRace.Knight);
        hero.SetModes(HeroModes.Spellcasted);
        army.Commander = hero;

        BattleForce force = Deploy(army);
        force[0].SetModes(BattleMonsterState.Moved);

        force.NewTurn();

        Assert.False(hero.Modes.HasFlag(HeroModes.Spellcasted));
        Assert.False(force[0].HasModes(BattleMonsterState.Moved));
    }

    [Fact]
    public void ControlComesFromTheArmyAndFallsBackToTheComputer()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 5));
        BattleForce force = Deploy(army);

        Assert.Equal(PlayerControl.Ai, force.GetControl());
        Assert.Equal(PlayerControl.Ai, force[0].GetControl());

        // 没有所属军队的召唤物也算电脑的。
        Assert.Equal(PlayerControl.Ai, Attacker().GetControl());
    }
}
