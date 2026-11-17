using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the morale and luck half of <c>Battle::Unit</c> (<c>battle_troop.cpp:276-329</c>): where the numbers come
/// from, the bone-dragon penalty and the round roll that turns them into flags.
/// </summary>
public class BattleUnitMoraleTests
{
    /// <summary>A field that only answers the one question morale asks it.</summary>
    private sealed class Field : IBattleField
    {
        public MonsterType? EnemyMonster { get; set; }

        public bool IsOnMoat(BattleUnit unit) => false;

        public int GetShootingPenaltyPercent(BattleUnit attacker, BattleUnit defender) => 0;

        public int GetArcheryBonusPercent(BattleUnit unit) => 0;

        public PlayerColor GetOppositeColor(PlayerColor color) => PlayerColor.None;

        public bool EnemyHasMonster(PlayerColor color, MonsterType monster) => EnemyMonster == monster;

        public PlayerColor AttackingArmyColor => PlayerColor.Blue;
    }

    /// <summary>A commander that answers exactly what a test needs, without a hero or a town.</summary>
    private sealed class Commander : IArmyCommander
    {
        public int Morale { get; set; }

        public int Luck { get; set; }

        public int Power { get; set; } = 1;

        public PlayerColor Color { get; set; } = PlayerColor.Blue;

        public bool IsHeroes => true;

        public ArtifactBag BagArtifacts { get; } = new();

        public int GetAttack() => 2;

        public int GetDefense() => 2;

        public uint GetSecondarySkillValue(SecondarySkill skill) => 0;

        public double GetMagicStrategicValue(double armyStrength) => 0;

        public PlayerControl GetControl() => PlayerControl.Human;

        public PlayerColor GetColor() => Color;

        public int GetPower() => Power;

        public int GetMorale() => Morale;

        public int GetLuck() => Luck;
    }

    private static GameArmy MakeArmy(params (MonsterType Monster, uint Count)[] troops)
    {
        GameArmy army = new();

        for (int index = 0; index < troops.Length; ++index)
        {
            army.GetTroop(index).Set(troops[index].Monster, troops[index].Count);
        }

        return army;
    }

    private static BattleUnit Unit(MonsterType monster, GameArmy army, IBattleField? field = null, uint count = 1)
    {
        return new BattleUnit(
            new Troop(monster, count),
            new BattlePosition(new BattleBoardCells(), 30, false, false),
            false,
            1)
        {
            Army = army,
            Color = army.Color,
            Field = field,
        };
    }

    [Fact]
    public void AnUnledArmyTakesItsMoraleFromItsComposition()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));

        Assert.Equal(army.GetMoraleModificator(), Unit(MonsterType.Peasant, army).GetMorale());
    }

    [Fact]
    public void TwoLivingRacesAreStillNeutralButThreeCostOneMorale()
    {
        // 参考的种族表：0 族或 2 族不罚，`default` 才罚 `族数 - 2`（army.cpp:1178-1205）。
        GameArmy two = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Goblin, 5));
        Assert.Equal(0, two.GetMoraleModificator());

        GameArmy three = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Goblin, 5), (MonsterType.Halfling, 5));
        Assert.Equal(-1, three.GetMoraleModificator());
        Assert.Equal(-1, Unit(MonsterType.Peasant, three).GetMorale());
    }

    [Fact]
    public void UndeadStacksIgnoreTheArmysMorale()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Skeleton, 5), (MonsterType.Goblin, 5));

        Assert.True(army.GetMoraleModificator() < 0);

        // 骷髅不吃士气：问它也答「普通」。
        Assert.Equal(Morale.Normal, Unit(MonsterType.Skeleton, army).GetMorale());

        // 旁边活着的那两族照吃惩罚。
        Assert.Equal(army.GetMoraleModificator(), Unit(MonsterType.Peasant, army).GetMorale());
    }

    [Fact]
    public void TheHeroDecidesTheMoraleOfHisArmy()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10), (MonsterType.Goblin, 5), (MonsterType.Halfling, 5));

        Hero hero = new(1, MapRace.Knight);
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Leadership, SkillLevel.Expert));
        army.Commander = hero;

        // 有指挥官时军队的士气就是英雄的士气，多种族惩罚不再叠加（army.cpp:1220-1224）——
        // 三族并列本来是 -1，这里仍是英雄的 +3。
        Assert.Equal(-1, army.GetMoraleModificator());
        Assert.Equal(hero.GetMorale(), army.GetMorale());
        Assert.Equal(Morale.Blood, Unit(MonsterType.Peasant, army).GetMorale());
    }

    [Fact]
    public void TheHerosLuckReachesTheStack()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));

        Hero hero = new(1, MapRace.Knight);
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Luck, SkillLevel.Expert));
        army.Commander = hero;

        Assert.Equal(hero.GetLuck(), army.GetLuck());
        Assert.Equal(hero.GetLuck(), Unit(MonsterType.Peasant, army).GetLuck());
        Assert.Equal(Luck.Irish, Unit(MonsterType.Peasant, army).GetLuck());
    }

    [Fact]
    public void ABoneDragonOnTheOtherSideCostsOneMorale()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Commander { Morale = Morale.Blood };

        Field field = new() { EnemyMonster = MonsterType.BoneDragon };

        Assert.Equal(Morale.Blood - 1, Unit(MonsterType.Peasant, army, field).GetMorale());

        // 对面没有骨龙时不多减。
        Assert.Equal(Morale.Blood, Unit(MonsterType.Peasant, army, new Field()).GetMorale());
    }

    [Fact]
    public void MoraleNeverFallsToTreasonBecauseOfABoneDragon()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Commander { Morale = Morale.Treason };

        Field field = new() { EnemyMonster = MonsterType.BoneDragon };

        // 已经是最低士气时不再往下掉（参考的 `> Morale::TREASON`）。
        Assert.Equal(Morale.Treason, Unit(MonsterType.Peasant, army, field).GetMorale());
    }

    [Fact]
    public void UndeadStacksIgnoreTheBoneDragonToo()
    {
        GameArmy army = MakeArmy((MonsterType.Skeleton, 10));
        army.Commander = new Commander { Morale = Morale.Blood };

        Field field = new() { EnemyMonster = MonsterType.BoneDragon };

        Assert.Equal(Morale.Normal, Unit(MonsterType.Skeleton, army, field).GetMorale());
    }

    [Fact]
    public void GoodMoraleIsRolledIntoTheGoodFlag()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Commander { Morale = Morale.Blood };

        BattleUnit unit = Unit(MonsterType.Peasant, army);
        Pcg32 random = new(1);
        bool raised = false;

        for (int round = 0; round < 200 && !raised; ++round)
        {
            unit.ResetModes(BattleMonsterState.MoraleGood | BattleMonsterState.MoraleBad);
            unit.SetRandomMorale(random);

            Assert.False(unit.HasModes(BattleMonsterState.MoraleBad));
            raised = unit.HasModes(BattleMonsterState.MoraleGood);
        }

        Assert.True(raised);
    }

    [Fact]
    public void NegativeMoraleCanBeDodgedByTheComputer()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Commander { Morale = Morale.Treason };

        BattleUnit unit = Unit(MonsterType.Peasant, army);
        Pcg32 random = new(7);
        int lowered = 0;

        for (int round = 0; round < 200; ++round)
        {
            unit.ResetModes(BattleMonsterState.MoraleGood | BattleMonsterState.MoraleBad);
            unit.SetRandomMorale(random);

            if (unit.HasModes(BattleMonsterState.MoraleBad))
            {
                ++lowered;
            }
        }

        // 1..12 掷中，再有一道 25% 的 AI 豁免：两种结果都得出现。
        Assert.InRange(lowered, 1, 199);
    }

    [Fact]
    public void NeutralMoraleNeverSetsAFlag()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Commander { Morale = Morale.Normal };

        BattleUnit unit = Unit(MonsterType.Peasant, army);
        Pcg32 random = new(3);

        for (int round = 0; round < 50; ++round)
        {
            unit.ResetModes(BattleMonsterState.MoraleGood | BattleMonsterState.MoraleBad);
            unit.SetRandomMorale(random);

            Assert.False(unit.HasModes(BattleMonsterState.MoraleGood));
            Assert.False(unit.HasModes(BattleMonsterState.MoraleBad));
        }
    }

    [Fact]
    public void LuckIsRolledOnceWithTheSameTwentyFour()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        army.Commander = new Commander { Luck = Luck.Good };

        BattleUnit unit = Unit(MonsterType.Peasant, army);
        Pcg32 random = new(5);
        bool lucky = false;

        for (int round = 0; round < 200 && !lucky; ++round)
        {
            unit.ResetModes(BattleMonsterState.LuckGood | BattleMonsterState.LuckBad);
            unit.SetRandomLuck(random);

            Assert.False(unit.HasModes(BattleMonsterState.LuckBad));
            lucky = unit.HasModes(BattleMonsterState.LuckGood);
        }

        Assert.True(lucky);
    }

    [Fact]
    public void ANewRoundClearsTheRolledFlags()
    {
        GameArmy army = MakeArmy((MonsterType.Peasant, 10));
        BattleUnit unit = Unit(MonsterType.Peasant, army);

        unit.SetModes(BattleMonsterState.MoraleGood | BattleMonsterState.LuckBad);

        unit.NewTurn();

        Assert.False(unit.HasModes(BattleMonsterState.MoraleGood));
        Assert.False(unit.HasModes(BattleMonsterState.LuckBad));
    }
}
