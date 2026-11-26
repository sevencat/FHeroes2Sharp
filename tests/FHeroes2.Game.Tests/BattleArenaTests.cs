using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the assembled half of <c>Battle::Arena</c> (<c>battle_arena.cpp</c>): which pieces a field battle and a siege
/// get, how the castle structures are read and damaged, and the questions a <see cref="BattleUnit"/> asks the field
/// while it calculates damage.
/// </summary>
public class BattleArenaTests
{
    private static GameArmy Army(PlayerColor color, Hero? commander = null)
    {
        GameArmy army = new()
        {
            Color = color,
        };

        army.GetTroop(0).Set(MonsterType.Peasant, 10);
        army.Commander = commander;

        return army;
    }

    private static CastleTown Town(params BuildingType[] buildings)
    {
        CastleTown castle = new(42, MapRace.Knight);

        foreach (BuildingType building in buildings)
        {
            castle.AddBuildings(building);
        }

        return castle;
    }

    private static BattleArena Siege(CastleTown? castle = null, Hero? attacker = null) =>
        new(
            Army(PlayerColor.Blue, attacker),
            Army(PlayerColor.Red),
            castle ?? Town(BuildingType.Castle, BuildingType.Spec, BuildingType.LeftTurret, BuildingType.RightTurret),
            new Pcg32(7));

    private static Hero ArcherHero()
    {
        Hero hero = new(1, MapRace.Knight);
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Archery, SkillLevel.Basic));

        return hero;
    }

    [Fact]
    public void AFieldBattleHasNoCastlePieces()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(1));

        Assert.Null(arena.Castle);
        Assert.False(arena.IsTown);
        Assert.Null(arena.Bridge);
        Assert.Null(arena.Catapult);
        Assert.Null(arena.GetTower(BattleTowerType.Center));
        Assert.False(arena.IsAnyTowerPresent);
    }

    [Fact]
    public void AFieldBattleLeavesTheBoardEmpty()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(1));

        foreach (int cell in new[] { BattleArena.CatapultPos, BattleArena.CastleGatePos, BattleArena.CastleFirstTopWallPos })
        {
            Assert.Equal(0, arena.Board.GetCell(cell)!.Object);
        }
    }

    [Fact]
    public void ATownWithoutACastleIsNotASiege()
    {
        // 只有炮塔没有主堡的村庄：算「攻城」，但没有墙，也没有投石车。
        BattleArena arena = Siege(Town(BuildingType.LeftTurret));

        Assert.True(arena.IsTown);
        Assert.Null(arena.Castle);
        Assert.Null(arena.Catapult);
        Assert.Null(arena.Bridge);
        Assert.Equal(0, arena.Board.GetCell(BattleArena.CastleFirstTopWallPos)!.Object);
    }

    [Fact]
    public void ASiegeSetsUpWallsGateAndCatapult()
    {
        BattleArena arena = Siege(attacker: new Hero(1, MapRace.Knight));

        Assert.True(arena.IsTown);
        Assert.NotNull(arena.Castle);
        Assert.NotNull(arena.Bridge);
        Assert.NotNull(arena.Catapult);

        Assert.Equal(1, arena.Board.GetCell(BattleArena.CatapultPos)!.Object);
        Assert.Equal(1, arena.Board.GetCell(BattleArena.CastleGatePos)!.Object);

        foreach (BattleTowerType type in new[] { BattleTowerType.Left, BattleTowerType.Center, BattleTowerType.Right })
        {
            Assert.NotNull(arena.GetTower(type));
        }

        Assert.True(arena.IsAnyTowerPresent);
    }

    [Fact]
    public void AWalkedStepTurnsTheDrawbridgeAroundIt()
    {
        BattleArena arena = Siege();
        BattleBridge bridge = arena.Bridge!;
        BattleUnit ward = arena.DefendingForce.First();

        // 守城方从闸门内侧走到闸门上：参考在迈上闸门前把桥放下（battle_action.cpp:333-350）。
        ward.SetPosition(BattleBridge.CellAfterGates);
        arena.LowerBridgeForStep(ward, BattleBridge.CellGates);

        Assert.True(bridge.IsDown);

        // 站在闸门上时不能升（桥上有人），走到闸门后面才升回去。
        ward.SetPosition(BattleBridge.CellGates);
        arena.RaiseBridgeAfterStep();

        Assert.True(bridge.IsDown);

        ward.SetPosition(BattleBridge.CellAfterGates);
        arena.RaiseBridgeAfterStep();

        Assert.False(bridge.IsDown);
    }

    [Fact]
    public void FortifiedWallsStartAtThreeOtherwiseAtTwo()
    {
        BattleArena fortified = Siege();
        BattleArena bare = Siege(Town(BuildingType.Castle, BuildingType.LeftTurret, BuildingType.RightTurret));

        foreach (int wall in new[]
        {
            BattleArena.CastleFirstTopWallPos,
            BattleArena.CastleSecondTopWallPos,
            BattleArena.CastleThirdTopWallPos,
            BattleArena.CastleFourthTopWallPos,
        })
        {
            Assert.Equal(3, fortified.Board.GetCell(wall)!.Object);
            Assert.Equal(2, bare.Board.GetCell(wall)!.Object);
        }
    }

    [Fact]
    public void OnlyBuiltTurretsGetATower()
    {
        BattleArena arena = Siege(Town(BuildingType.Castle, BuildingType.RightTurret));

        Assert.Null(arena.GetTower(BattleTowerType.Left));
        Assert.NotNull(arena.GetTower(BattleTowerType.Center));
        Assert.NotNull(arena.GetTower(BattleTowerType.Right));
    }

    [Fact]
    public void TheCatapultNeedsAnAttackingCommander()
    {
        Assert.Null(Siege().Catapult);
        Assert.NotNull(Siege(attacker: new Hero(1, MapRace.Knight)).Catapult);
    }

    [Fact]
    public void TheFreeCellNextToAHeroDependsOnTheSide()
    {
        BattleArena arena = Siege();

        Assert.Equal(11, arena.GetFreePositionNearHero(PlayerColor.Blue));
        Assert.Equal(21, arena.GetFreePositionNearHero(PlayerColor.Red));
        Assert.Equal(-1, arena.GetFreePositionNearHero(PlayerColor.Green));
    }

    [Fact]
    public void EachColourSeesTheOtherSide()
    {
        BattleArena arena = Siege();

        Assert.Equal(PlayerColor.Red, arena.GetOppositeColor(PlayerColor.Blue));
        Assert.Equal(PlayerColor.Blue, arena.GetOppositeColor(PlayerColor.Red));

        Assert.Same(arena.AttackingForce, arena.GetForce(PlayerColor.Blue));
        Assert.Same(arena.DefendingForce, arena.GetForce(PlayerColor.Red));
        Assert.Same(arena.DefendingForce, arena.GetEnemyForce(PlayerColor.Blue));
        Assert.Same(arena.AttackingForce, arena.GetEnemyForce(PlayerColor.Red));
    }

    [Fact]
    public void WallsAndGateTowersCanOnlyLoseTheirUpperPart()
    {
        BattleArena arena = Siege();

        Assert.Equal(3, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Wall1, SiegeWeaponType.Catapult));
        Assert.Equal(1, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.TopBridgeTower, SiegeWeaponType.EarthquakeSpell));

        arena.ApplyDamageToCastleDefenseStructure(CastleDefenseStructure.Wall1, 1);
        arena.ApplyDamageToCastleDefenseStructure(CastleDefenseStructure.TopBridgeTower, 1);

        Assert.Equal(2, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Wall1, SiegeWeaponType.Catapult));
        Assert.Equal(0, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.TopBridgeTower, SiegeWeaponType.EarthquakeSpell));

        // 桥头塔的格子停在 1：掉了一级，但仍然挡路。
        Assert.Equal(1, arena.Board.GetCell(BattleArena.CastleTopGateTowerPos)!.Object);
        Assert.False(arena.Board.GetCell(BattleArena.CastleTopGateTowerPos)!.IsPassable(false));
    }

    [Fact]
    public void ABrokenWallBecomesWalkable()
    {
        BattleArena arena = Siege();

        arena.ApplyDamageToCastleDefenseStructure(CastleDefenseStructure.Wall2, 3);

        Assert.Equal(0, arena.Board.GetCell(BattleArena.CastleSecondTopWallPos)!.Object);
        Assert.True(arena.Board.GetCell(BattleArena.CastleSecondTopWallPos)!.IsPassable(false));
    }

    [Fact]
    public void TheBridgeFallsInOneHitAndOpensTheGate()
    {
        BattleArena arena = Siege();

        Assert.Equal(1, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Bridge, SiegeWeaponType.Catapult));

        arena.ApplyDamageToCastleDefenseStructure(CastleDefenseStructure.Bridge, 1);

        Assert.Equal(0, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Bridge, SiegeWeaponType.Catapult));
        Assert.True(arena.Bridge!.IsDestroyed);
        Assert.Equal(0, arena.Board.GetCell(BattleArena.CastleGatePos)!.Object);
    }

    [Fact]
    public void ADestroyedTowerStopsShootingButStillBlocks()
    {
        BattleArena arena = Siege();

        Assert.Equal(1, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Tower1, SiegeWeaponType.Catapult));

        arena.ApplyDamageToCastleDefenseStructure(CastleDefenseStructure.Tower1, 1);

        Assert.Equal(0, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Tower1, SiegeWeaponType.Catapult));
        Assert.False(arena.GetTower(BattleTowerType.Left)!.IsValid);

        // 塔的格子在塔倒后变成瓦砾（1），依旧不能穿过。
        Assert.Equal(1, arena.Board.GetCell(BattleArena.CastleTopArcherTowerPos)!.Object);
        Assert.False(arena.Board.GetCell(BattleArena.CastleTopArcherTowerPos)!.IsPassable(false));
    }

    [Fact]
    public void ATowerWithoutATurretDropsItsCellInstead()
    {
        BattleArena arena = Siege(Town(BuildingType.Castle));

        Assert.Null(arena.GetTower(BattleTowerType.Left));
        Assert.Equal(0, arena.GetCastleDefenseStructureCondition(CastleDefenseStructure.Tower1, SiegeWeaponType.Catapult));

        arena.ApplyDamageToCastleDefenseStructure(CastleDefenseStructure.Tower1, 1);

        Assert.Equal(1, arena.Board.GetCell(BattleArena.CastleTopArcherTowerPos)!.Object);
        Assert.False(arena.Board.GetCell(BattleArena.CastleTopArcherTowerPos)!.IsPassable(false));
    }

    [Fact]
    public void TheEarthquakeTargetsKeepTheReferenceOrder()
    {
        CastleDefenseStructure[] targets = BattleArena.GetEarthQuakeSpellTargets();

        Assert.Equal(9, targets.Length);
        Assert.Equal(CastleDefenseStructure.Wall1, targets[0]);
        Assert.Equal(CastleDefenseStructure.Wall4, targets[3]);
        Assert.Equal(CastleDefenseStructure.Tower1, targets[4]);
        Assert.Equal(CastleDefenseStructure.Tower2, targets[5]);
        Assert.Equal(CastleDefenseStructure.Bridge, targets[6]);
        Assert.Equal(CastleDefenseStructure.TopBridgeTower, targets[7]);
        Assert.Equal(CastleDefenseStructure.BottomBridgeTower, targets[8]);
    }

    [Fact]
    public void EarthquakeDamageGrowsWithSpellPower()
    {
        Hero hero = new(1, MapRace.Knight);

        hero.SetPrimarySkill(PrimarySkill.Power, 1);
        Assert.Equal((0U, 1U), BattleArena.GetEarthquakeDamageRange(hero));

        hero.SetPrimarySkill(PrimarySkill.Power, 3);
        Assert.Equal((0U, 2U), BattleArena.GetEarthquakeDamageRange(hero));

        hero.SetPrimarySkill(PrimarySkill.Power, 6);
        Assert.Equal((0U, 3U), BattleArena.GetEarthquakeDamageRange(hero));

        hero.SetPrimarySkill(PrimarySkill.Power, 10);
        Assert.Equal((1U, 3U), BattleArena.GetEarthquakeDamageRange(hero));
    }

    [Fact]
    public void AShotThroughIntactWallsLosesHalfItsDamage()
    {
        BattleArena arena = Siege();

        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        Assert.True(arena.IsShootingPenalty(attacker, defender));
        Assert.Equal(GameStatic.CastleWallRangedPenalty, arena.GetShootingPenaltyPercent(attacker, defender));
    }

    [Fact]
    public void AFieldBattleHasNoWallPenalty()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(1));

        Assert.False(arena.IsShootingPenalty(arena.AttackingForce.First(), arena.DefendingForce.First()));
        Assert.Equal(0, arena.GetShootingPenaltyPercent(arena.AttackingForce.First(), arena.DefendingForce.First()));
    }

    [Fact]
    public void ArcheryCancelsTheWallPenalty()
    {
        Hero hero = ArcherHero();
        BattleArena arena = Siege(attacker: hero);

        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        Assert.False(arena.IsShootingPenalty(attacker, defender));

        // 技能给的是百分比数值（10/25/50），不是等级（1/2/3）。
        Assert.Equal((int)hero.GetSecondarySkillValue(SecondarySkill.Archery), arena.GetArcheryBonusPercent(attacker));
    }

    [Fact]
    public void AMoatCellHurtsUntilTheBridgeIsDown()
    {
        BattleArena arena = Siege(Town(BuildingType.Castle, BuildingType.Moat));

        BattleUnit attacker = arena.AttackingForce.First();

        // 远离闸门的护城河格：飞行/桥面都不影响。
        attacker.SetPosition(7);
        Assert.True(arena.IsOnMoat(attacker));

        // 桥下的护城河格：桥升起时谁站上去都会陷进去（桥被自己占住，成了不可通行）。
        attacker.SetPosition(BattleBridge.CellMoat);
        Assert.True(arena.IsOnMoat(attacker));

        // 桥放下之后，同一个格子不再伤人。先把人挪开，桥才允许动作。
        attacker.SetPosition(7);
        Assert.True(arena.Bridge!.ActionDown());

        attacker.SetPosition(BattleBridge.CellMoat);
        Assert.False(arena.IsOnMoat(attacker));
    }

    [Fact]
    public void WithoutABuiltMoatThereIsNoMoatDamage()
    {
        BattleArena arena = Siege(Town(BuildingType.Castle));

        BattleUnit attacker = arena.AttackingForce.First();
        attacker.SetPosition(7);

        Assert.False(arena.IsOnMoat(attacker));
    }

    [Fact]
    public void EnemyHasMonsterAsksTheOtherArmy()
    {
        BattleArena arena = Siege();

        Assert.True(arena.EnemyHasMonster(PlayerColor.Blue, MonsterType.Peasant));
        Assert.False(arena.EnemyHasMonster(PlayerColor.Blue, MonsterType.BlackDragon));
    }

    [Fact]
    public void ABattleWithBothArmiesAndNoOutcomeIsStillValid()
    {
        BattleArena arena = Siege();

        Assert.True(arena.BattleValid());
        Assert.Equal(BattleOutcome.None, arena.Result.Attacker);
    }

    [Fact]
    public void UnitsKnowTheirFieldAndGraveyard()
    {
        BattleArena arena = Siege();

        foreach (BattleUnit unit in arena.AttackingForce)
        {
            Assert.Same(arena, unit.Field);
            Assert.Same(arena.Graveyard, unit.Graveyard);
        }

        foreach (BattleUnit unit in arena.DefendingForce)
        {
            Assert.Same(arena, unit.Field);
            Assert.Same(arena.Graveyard, unit.Graveyard);
        }
    }
}
