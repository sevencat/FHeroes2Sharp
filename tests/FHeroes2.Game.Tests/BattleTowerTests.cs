using FHeroes2.Assets.Maps;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using Xunit;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Tower</c> (<c>battle_tower.h/.cpp</c>): the archers a castle's shooting positions are worth, and
/// the paragraphs the keep's description grows from them.
/// </summary>
public class BattleTowerTests
{
    private static CastleTown Town(params BuildingType[] buildings)
    {
        CastleTown castle = new(42, MapRace.Knight);

        foreach (BuildingType building in buildings)
        {
            castle.AddBuildings(building);
        }

        return castle;
    }

    private static uint ExpectedCount(CastleTown castle) => (uint)Math.Min(castle.CountBuildings(), 20);

    [Fact]
    public void TheKeepKeepsTheWholeStack()
    {
        CastleTown castle = Town(BuildingType.Castle);
        BattleTower tower = new(castle, BattleTowerType.Center, 1);

        Assert.Equal(ExpectedCount(castle), tower.Count);
        Assert.Equal(MonsterType.Archer, tower.Unit.Monster);
        Assert.Equal(BattleTowerType.Center, tower.Type);
    }

    [Fact]
    public void ATurretHalvesTheArchersButNotTheHitPoints()
    {
        CastleTown castle = Town(BuildingType.Castle, BuildingType.LeftTurret);
        BattleTower keep = new(castle, BattleTowerType.Center, 1);
        BattleTower turret = new(castle, BattleTowerType.Left, 2);

        Assert.True(keep.Count > 1, "the keep must have more than one archer for this test to mean anything");

        // 参考用 SetCount() 折半，只改数量、不动血量：炮塔因此是「半队人、满血」。
        Assert.Equal(keep.Count / 2, turret.Count);
        Assert.Equal(keep.Unit.HitPoints, turret.Unit.HitPoints);
    }

    [Fact]
    public void ASingleArcherIsNeverHalvedIntoNothing()
    {
        // 一座只有塔的小镇：CountBuildings() 小到折半为 0，塔至少留一个箭手。
        CastleTown castle = Town();
        BattleTower turret = new(castle, BattleTowerType.Right, 1);

        Assert.Equal(1u, turret.Count);
    }

    [Fact]
    public void TheAttackBonusIsTheMageGuildLevel()
    {
        Assert.Equal(0u, new BattleTower(Town(BuildingType.Castle), BattleTowerType.Center, 1).AttackBonus);
        Assert.Equal(1u, new BattleTower(Town(BuildingType.Castle, BuildingType.MageGuild1), BattleTowerType.Center, 1).AttackBonus);
        Assert.Equal(3u, new BattleTower(Town(BuildingType.Castle, BuildingType.MageGuild3), BattleTowerType.Center, 1).AttackBonus);
    }

    [Fact]
    public void TheShotGetsTheAttackBonusOnTopOfTheArchers()
    {
        CastleTown castle = Town(BuildingType.Castle, BuildingType.MageGuild2);
        BattleTower tower = new(castle, BattleTowerType.Center, 1);

        Assert.Equal(tower.Unit.GetAttack() + 2, tower.GetAttack());
    }

    [Fact]
    public void TheCommandingHeroAddsHisAttackToTheArchers()
    {
        CastleTown castle = Town(BuildingType.Castle);
        Hero hero = new(1, MapRace.Knight);

        // 驻军本来就挂着队长，所以这里不是「有英雄 vs 没英雄」，而是「塔的箭手拿到哪个指挥官的攻击」。
        BattleTower withHero = new(castle, BattleTowerType.Center, 1, hero);

        Assert.Equal(
            (uint)(MonsterStatistics.GetBattleStats(MonsterType.Archer).Attack + hero.GetAttack()),
            withHero.GetAttack() - withHero.AttackBonus);
    }

    [Fact]
    public void TheTowersHaveTheirReferenceNames()
    {
        CastleTown castle = Town(BuildingType.Castle);

        Assert.Equal("Ballista", new BattleTower(castle, BattleTowerType.Center, 1).Name);
        Assert.Equal("Left Turret", new BattleTower(castle, BattleTowerType.Left, 1).Name);
        Assert.Equal("Right Turret", new BattleTower(castle, BattleTowerType.Right, 1).Name);
    }

    [Fact]
    public void DestroyingATurretTurnsItsCellIntoRubble()
    {
        BattleBoardCells board = new();
        BattleTower left = new(Town(BuildingType.Castle, BuildingType.LeftTurret), BattleTowerType.Left, 1);
        BattleTower right = new(Town(BuildingType.Castle, BuildingType.RightTurret), BattleTowerType.Right, 1);

        Assert.True(left.IsValid);
        Assert.Equal(0, board.GetCell(BattleTower.TopArcherTowerIndex)!.Object);

        left.SetDestroyed(board);

        Assert.False(left.IsValid);
        Assert.Equal(1, board.GetCell(BattleTower.TopArcherTowerIndex)!.Object);
        Assert.Equal(0, board.GetCell(BattleTower.BottomArcherTowerIndex)!.Object);

        right.SetDestroyed(board);

        Assert.False(right.IsValid);
        Assert.Equal(1, board.GetCell(BattleTower.BottomArcherTowerIndex)!.Object);
    }

    [Fact]
    public void TheKeepHasNoCellToBlock()
    {
        BattleBoardCells board = new();
        BattleTower keep = new(Town(BuildingType.Castle), BattleTowerType.Center, 1);

        keep.SetDestroyed(board);

        Assert.False(keep.IsValid);

        // 中心塔就是主堡本身，塌了也不会在棋盘上多一块石头。
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            Assert.Equal(0, board.GetCell(index)!.Object);
        }
    }

    [Fact]
    public void ATownWithoutCastleHasNothingToSay()
    {
        Assert.Equal(string.Empty, BattleTower.GetInfo(Town()));
        Assert.Equal(string.Empty, BattleTower.GetInfo(Town(BuildingType.LeftTurret, BuildingType.RightTurret)));
    }

    [Fact]
    public void TheKeepAloneDescribesOneTower()
    {
        CastleTown castle = Town(BuildingType.Castle);

        string info = BattleTower.GetInfo(castle);

        Assert.Equal(
            $"The Ballista fires with the strength of {ExpectedCount(castle)} Archers.",
            info);
    }

    [Fact]
    public void EachTurretAddsAParagraph()
    {
        CastleTown castle = Town(BuildingType.Castle, BuildingType.LeftTurret, BuildingType.RightTurret);

        string[] paragraphs = BattleTower.GetInfo(castle).Split("\n\n");

        Assert.Equal(3, paragraphs.Length);
        Assert.StartsWith("The Ballista fires with the strength of ", paragraphs[0]);
        Assert.StartsWith("The Left Turret fires with the strength of ", paragraphs[1]);
        Assert.StartsWith("The Right Turret fires with the strength of ", paragraphs[2]);
        Assert.All(paragraphs, paragraph => Assert.EndsWith("Archers.", paragraph));
    }

    [Fact]
    public void AMageGuildAddsTheAttackBonusToTheText()
    {
        CastleTown castle = Town(BuildingType.Castle, BuildingType.MageGuild2);

        Assert.EndsWith("Archers, each with a +2 bonus to their attack skill.", BattleTower.GetInfo(castle));
    }

    [Fact]
    public void ADestroyedTurretIsReportedAsSuch()
    {
        CastleTown castle = Town(BuildingType.Castle, BuildingType.LeftTurret);

        Dictionary<BattleTowerType, BattleTower> towers = new()
        {
            [BattleTowerType.Center] = new BattleTower(castle, BattleTowerType.Center, 1),
            [BattleTowerType.Left] = new BattleTower(castle, BattleTowerType.Left, 2)
        };

        towers[BattleTowerType.Left].SetDestroyed(new BattleBoardCells());

        string[] paragraphs = BattleTower.GetInfo(castle, towers).Split("\n\n");

        Assert.Equal(2, paragraphs.Length);
        Assert.StartsWith("The Ballista fires with the strength of ", paragraphs[0]);
        Assert.Equal("The Left Turret is destroyed.", paragraphs[1]);
    }

    [Fact]
    public void TheGarrisonStrengthCountsTheTowers()
    {
        CastleTown keepOnly = Town(BuildingType.Castle);
        keepOnly.GetActualArmy(null).GetTroop(0).Set(MonsterType.Pikeman, 10);

        CastleTown withTurrets = Town(BuildingType.Castle, BuildingType.LeftTurret, BuildingType.RightTurret);
        withTurrets.GetActualArmy(null).GetTroop(0).Set(MonsterType.Pikeman, 10);

        Assert.True(withTurrets.GetGarrisonStrength() > keepOnly.GetGarrisonStrength());
    }
}
