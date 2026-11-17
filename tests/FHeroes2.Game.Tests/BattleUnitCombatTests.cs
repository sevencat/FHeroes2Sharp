using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the combat half of <c>Battle::Unit</c> (<c>battle_troop.cpp:387-791</c>, <c>969-1005</c>): attack,
/// defense, the damage arithmetic, death, resurrection and the turn reset.
/// </summary>
public class BattleUnitCombatTests
{
    private static BattleUnit Spawn(
        MonsterType monster,
        uint count,
        int head,
        PlayerColor color = PlayerColor.None,
        uint uid = 1,
        bool wide = false,
        bool reflect = false,
        BattleBoardCells? board = null)
    {
        BattlePosition position = new(board ?? new BattleBoardCells(), head, wide, reflect);

        return new BattleUnit(new Troop(monster, count), position, reflect, uid) { Color = color };
    }

    private static uint ExtraValue(SpellType spell) => new Spell(spell).ExtraValue();

    private static uint UnitHitPoints(MonsterType monster) => (uint)MonsterStatistics.GetBattleStats(monster).HitPoints;

    /// <summary>The attack-versus-defense step of <c>CalculateDamageUnit</c>, applied on its own.</summary>
    private static uint Against(BattleUnit attacker, BattleUnit defender, double damage)
    {
        int difference = (int)attacker.GetAttack() - (int)defender.GetDefense();

        damage *= 1 + (0 < difference ? 0.1 * Math.Min(difference, 20) : 0.05 * Math.Max(difference, -16));

        return Math.Max((uint)damage, 1u);
    }

    /// <summary>A field that hands out whatever numbers a test asks for.</summary>
    private sealed class FakeField : IBattleField
    {
        public bool OnMoat { get; set; }

        public int ShootingPenaltyPercent { get; set; }

        public int ArcheryBonusPercent { get; set; }

        public PlayerColor Opposite { get; set; } = PlayerColor.None;

        public bool IsOnMoat(BattleUnit unit) => OnMoat;

        public int GetShootingPenaltyPercent(BattleUnit attacker, BattleUnit defender) => ShootingPenaltyPercent;

        public int GetArcheryBonusPercent(BattleUnit unit) => ArcheryBonusPercent;

        public PlayerColor GetOppositeColor(PlayerColor color) => Opposite;

        public MonsterType? EnemyMonster { get; set; }

        public bool EnemyHasMonster(PlayerColor color, MonsterType monster) => EnemyMonster == monster;
    }

    [Fact]
    public void TheAttackAndDefenseStartFromTheCreature()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40);

        Assert.Equal((uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).Attack, unit.GetAttack());
        Assert.Equal((uint)MonsterStatistics.GetBattleStats(MonsterType.Peasant).Defense, unit.GetDefense());
    }

    [Fact]
    public void BloodlustAddsAttack()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40);
        uint attack = unit.GetAttack();

        unit.SetModes(BattleMonsterState.SpellBloodlust);

        Assert.Equal(attack + ExtraValue(SpellType.BloodLust), unit.GetAttack());
    }

    [Fact]
    public void StoneSkinWinsOverSteelSkin()
    {
        BattleUnit unit = Spawn(MonsterType.Cavalry, 10, 40, wide: true);
        uint defense = unit.GetDefense();

        unit.SetModes(BattleMonsterState.SpellStoneSkin);
        Assert.Equal(defense + ExtraValue(SpellType.StoneSkin), unit.GetDefense());

        // 参考是 else if：两个都在时钢铁皮肤不再另算一次。
        unit.SetModes(BattleMonsterState.SpellSteelSkin);
        Assert.Equal(defense + ExtraValue(SpellType.StoneSkin), unit.GetDefense());

        unit.ResetModes(BattleMonsterState.SpellStoneSkin);
        Assert.Equal(defense + ExtraValue(SpellType.SteelSkin), unit.GetDefense());
    }

    [Fact]
    public void EveryDisruptingRayTakesDefenseAndNeverTheLastPoint()
    {
        BattleUnit unit = Spawn(MonsterType.Cavalry, 10, 40, wide: true);
        uint defense = unit.GetDefense();
        uint step = ExtraValue(SpellType.DisruptingRay);

        for (uint rays = 1; rays <= 4; ++rays)
        {
            unit.AddDisruptingRay();

            uint taken = step * rays;

            Assert.Equal(taken >= defense ? 1u : defense - taken, unit.GetDefense());
            Assert.Equal(rays, unit.DisruptingRays);
        }
    }

    [Fact]
    public void TheMoatTakesItsDefenseToo()
    {
        BattleUnit unit = Spawn(MonsterType.Cavalry, 10, 40, wide: true);
        uint defense = unit.GetDefense();
        unit.Field = new FakeField { OnMoat = true };

        uint taken = (uint)FHeroes2.Game.Maps.GameStatic.BattleMoatReduceDefense;

        Assert.Equal(taken >= defense ? 1u : defense - taken, unit.GetDefense());
    }

    [Fact]
    public void DamageIsNeverLessThanOneEvenAgainstAWall()
    {
        BattleUnit peasant = Spawn(MonsterType.Peasant, 10, 40);
        BattleUnit dragon = Spawn(MonsterType.RedDragon, 2, 20, wide: true);

        Assert.Equal(1u, peasant.CalculateDamageUnit(dragon, 1));
    }

    [Fact]
    public void ArchersHalveTheirDamageInMelee()
    {
        BattleBoardCells board = new();
        BattleUnit archer = Spawn(MonsterType.Archer, 10, 40, PlayerColor.Blue, uid: 1, board: board);
        BattleUnit enemy = Spawn(MonsterType.Archer, 10, 41, PlayerColor.Red, uid: 2, board: board);

        Assert.True(archer.IsHandFighting());
        Assert.Equal(Against(archer, enemy, 5), archer.CalculateDamageUnit(enemy, 10));

        enemy.SetPosition(30);

        Assert.False(archer.IsHandFighting());
        Assert.Equal(Against(archer, enemy, 10), archer.CalculateDamageUnit(enemy, 10));
    }

    [Fact]
    public void TheShieldOnlyHalvesShots()
    {
        BattleUnit archer = Spawn(MonsterType.Archer, 10, 40, PlayerColor.Blue, uid: 1);
        BattleUnit shielded = Spawn(MonsterType.Archer, 10, 30, PlayerColor.Red, uid: 2);

        shielded.SetModes(BattleMonsterState.SpellShield);

        Assert.Equal(
            Against(archer, shielded, 10.0 / ExtraValue(SpellType.Shield)),
            archer.CalculateDamageUnit(shielded, 10));

        // 近战单位不吃护盾。
        BattleUnit peasant = Spawn(MonsterType.Peasant, 10, 20, PlayerColor.Blue, uid: 3);
        Assert.Equal(Against(peasant, shielded, 10), peasant.CalculateDamageUnit(shielded, 10));
    }

    [Fact]
    public void TheFieldSuppliesArcheryAndTheWallPenalty()
    {
        BattleUnit archer = Spawn(MonsterType.Archer, 10, 40, PlayerColor.Blue, uid: 1);
        BattleUnit enemy = Spawn(MonsterType.Archer, 10, 30, PlayerColor.Red, uid: 2);

        archer.Field = new FakeField { ArcheryBonusPercent = 10 };
        Assert.Equal(Against(archer, enemy, 11), archer.CalculateDamageUnit(enemy, 10));

        archer.Field = new FakeField { ShootingPenaltyPercent = 50 };
        Assert.Equal(Against(archer, enemy, 5), archer.CalculateDamageUnit(enemy, 10));
    }

    [Fact]
    public void BlindedRetaliationIsReduced()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 10, 20, uid: 2);

        unit.BlindRetaliation = true;

        uint reduction = ExtraValue(SpellType.Blind);

        Assert.Equal((uint)(10 * (100 - reduction) / 100), unit.CalculateDamageUnit(enemy, 10));
    }

    [Fact]
    public void BlessTakesTheTopAndCurseTheBottom()
    {
        BattleUnit unit = Spawn(MonsterType.Dwarf, 10, 40);
        BattleUnit enemy = Spawn(MonsterType.Dwarf, 10, 20, uid: 2);
        Pcg32 random = new(1);

        unit.SetModes(BattleMonsterState.SpellBless);
        Assert.Equal(unit.CalculateMaxDamage(enemy), unit.GetDamage(enemy, random));

        unit.ResetModes(BattleMonsterState.SpellBless);
        unit.SetModes(BattleMonsterState.SpellCurse);
        Assert.Equal(unit.CalculateMinDamage(enemy), unit.GetDamage(enemy, random));
    }

    [Fact]
    public void GoodLuckDoublesTheBlowAndBadLuckHalvesIt()
    {
        BattleUnit unit = Spawn(MonsterType.Dwarf, 10, 40);
        BattleUnit enemy = Spawn(MonsterType.Dwarf, 10, 20, uid: 2);
        Pcg32 random = new(1);

        unit.SetModes(BattleMonsterState.SpellBless | BattleMonsterState.LuckGood);
        Assert.Equal(unit.CalculateMaxDamage(enemy) * 2, unit.GetDamage(enemy, random));

        unit.ResetModes(BattleMonsterState.LuckGood);
        unit.SetModes(BattleMonsterState.LuckBad);
        Assert.Equal(unit.CalculateMaxDamage(enemy) / 2, unit.GetDamage(enemy, random));
    }

    [Fact]
    public void HowManyWillBeKilledCountsWholeCreatures()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40);
        uint perCreature = UnitHitPoints(MonsterType.Peasant);

        Assert.Equal(3u, unit.HowManyWillBeKilled(3 * perCreature));
        Assert.Equal(0u, unit.HowManyWillBeKilled(perCreature - 1));
        Assert.Equal(10u, unit.HowManyWillBeKilled(10 * perCreature));
    }

    [Fact]
    public void AMirrorImageDiesToTheFirstBlow()
    {
        BattleUnit mirror = Spawn(MonsterType.Peasant, 10, 40);

        mirror.SetModes(BattleMonsterState.CapMirrorImage);

        Assert.Equal(10u, mirror.HowManyWillBeKilled(1));
    }

    [Fact]
    public void DamageLowersTheCountAndTheHitPoints()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 10, 20, uid: 2);
        uint perCreature = UnitHitPoints(MonsterType.Peasant);

        uint killed = unit.ApplyDamage(enemy, 4 * perCreature, out uint killedOut, out uint resurrected);

        Assert.Equal(4u, killed);
        Assert.Equal(4u, killedOut);
        Assert.Equal(0u, resurrected);
        Assert.Equal(6u, unit.Count);
        Assert.Equal(6u * perCreature, unit.HitPoints);
        Assert.Equal(4u, unit.Dead);
        Assert.True(unit.IsValid);
    }

    [Fact]
    public void WoundsBreakParalysisAndBlindness()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 10, 20, uid: 2);

        unit.SetModes(BattleMonsterState.SpellStone);
        unit.Affected.AddMode(BattleMonsterState.SpellStone, 3);

        unit.ApplyDamage(enemy, 1, out _, out _);

        Assert.False(unit.HasModes(BattleMonsterState.SpellStone));
        Assert.True(unit.HasModes(BattleMonsterState.Moved | BattleMonsterState.Retaliated));
        Assert.Equal(0, unit.Affected.Count);
    }

    [Fact]
    public void SoulEatingBringsTheKillerBackPastItsOldMaximum()
    {
        BattleUnit ghost = Spawn(MonsterType.Ghost, 5, 40, PlayerColor.Blue, uid: 1);
        BattleUnit prey = Spawn(MonsterType.Peasant, 10, 20, PlayerColor.Red, uid: 2);

        ghost.ApplyDamage(prey, UnitHitPoints(MonsterType.Ghost), out _, out _);
        Assert.Equal(4u, ghost.Count);

        uint ghostHitPoints = UnitHitPoints(MonsterType.Ghost);
        uint damage = 3 * UnitHitPoints(MonsterType.Peasant);

        uint killed = prey.ApplyDamage(ghost, damage, out _, out uint resurrected);

        Assert.Equal(3u, killed);
        Assert.Equal(3u, resurrected);
        Assert.Equal(7u, ghost.Count);
        Assert.Equal(7u, ghost.MaxCount);
    }

    [Fact]
    public void BloodDrainIsCappedByTheOldMaximum()
    {
        BattleUnit vampire = Spawn(MonsterType.VampireLord, 5, 40, PlayerColor.Blue, uid: 1);
        BattleUnit prey = Spawn(MonsterType.Peasant, 10, 20, PlayerColor.Red, uid: 2);

        vampire.ApplyDamage(prey, 2 * UnitHitPoints(MonsterType.VampireLord), out _, out _);
        Assert.Equal(3u, vampire.Count);

        uint hitPointsBefore = vampire.HitPoints;
        uint points = 10 * UnitHitPoints(MonsterType.Peasant);

        prey.ApplyDamage(vampire, 10 * UnitHitPoints(MonsterType.Peasant), out uint killed, out uint resurrected);

        uint expected = Troop.GetCountFromHitPoints(MonsterType.VampireLord, hitPointsBefore + points) - 3;

        Assert.Equal(10u, killed);
        Assert.Equal(Math.Min(expected, 2u), resurrected);
        Assert.Equal(3u + resurrected, vampire.Count);
        Assert.Equal(5u, vampire.MaxCount);
    }

    [Fact]
    public void ResurrectionStopsAtTheMaximumUnlessItIsAllowedToExceed()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 5, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 5, 20, uid: 2);
        uint perCreature = UnitHitPoints(MonsterType.Peasant);

        unit.ApplyDamage(enemy, 2 * perCreature, out _, out _);
        Assert.Equal(3u, unit.Count);
        Assert.Equal(2u, unit.Dead);

        unit.Resurrect(100, false, false);

        Assert.Equal(5u, unit.Count);
        Assert.Equal(0u, unit.Dead);
        Assert.Equal(5u * perCreature, unit.HitPoints);

        uint resurrected = unit.Resurrect(100, true, false);

        Assert.Equal(100u, resurrected);
        Assert.Equal(105u, unit.Count);
        Assert.Equal(105u, unit.MaxCount);
    }

    [Fact]
    public void TemporaryResurrectionLeavesTheDeadCountAlone()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 5, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 5, 20, uid: 2);

        unit.ApplyDamage(enemy, 2 * UnitHitPoints(MonsterType.Peasant), out _, out _);
        unit.Resurrect(100, false, true);

        Assert.Equal(5u, unit.Count);
        Assert.Equal(2u, unit.Dead);
    }

    [Fact]
    public void TheNewTurnRefillsTheRegeneratingSurvivorsAndClearsTheFlags()
    {
        BattleUnit troll = Spawn(MonsterType.Troll, 5, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 5, 20, uid: 2);

        troll.ApplyDamage(enemy, 1, out _, out _);
        troll.SetModes(BattleMonsterState.Moved | BattleMonsterState.Retaliated | BattleMonsterState.LuckGood);
        troll.SetModes(BattleMonsterState.SpellSlow);
        troll.Affected.AddMode(BattleMonsterState.SpellSlow, 2);

        Assert.Equal(5u * UnitHitPoints(MonsterType.Troll) - 1, troll.HitPoints);

        troll.NewTurn();

        Assert.Equal(troll.Troop.GetHitPoints(), troll.HitPoints);
        Assert.False(troll.HasModes(
            BattleMonsterState.Moved |
            BattleMonsterState.Retaliated |
            BattleMonsterState.LuckGood));

        // 时长从 2 减到 1，法术还在。
        Assert.True(troll.HasModes(BattleMonsterState.SpellSlow));
        Assert.Equal(1u, troll.Affected.GetMode(BattleMonsterState.SpellSlow));

        troll.NewTurn();

        Assert.False(troll.HasModes(BattleMonsterState.SpellSlow));
        Assert.True(troll.Affected.IsEmpty);
    }

    [Fact]
    public void TheMirrorImageDiesWhenItsTimerRunsOut()
    {
        BattleUnit owner = Spawn(MonsterType.Peasant, 10, 40, PlayerColor.Blue, uid: 1);
        BattleUnit mirror = Spawn(MonsterType.Peasant, 10, 20, PlayerColor.Blue, uid: 2);

        owner.MirrorUnit = mirror;
        mirror.MirrorUnit = owner;
        owner.SetModes(BattleMonsterState.CapMirrorOwner);
        mirror.SetModes(BattleMonsterState.CapMirrorImage);
        owner.Affected.AddMode(BattleMonsterState.CapMirrorOwner, 1);

        owner.NewTurn();

        Assert.Equal(0u, mirror.Count);
        Assert.False(mirror.IsValid);
        Assert.Null(owner.MirrorUnit);
        Assert.False(owner.HasModes(BattleMonsterState.CapMirrorOwner));
    }

    [Fact]
    public void PostKilledActionClearsTheSpellsAndTheCells()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 5, 40);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 5, 20, uid: 2);
        BattleBoardCells board = unit.Position.Board;

        unit.SetModes(BattleMonsterState.SpellSlow);
        unit.Affected.AddMode(BattleMonsterState.SpellSlow, 3);

        unit.ApplyDamage(enemy, 100, out _, out _);
        Assert.False(unit.IsValid);
        Assert.NotNull(board.GetCell(40)!.Unit);

        unit.PostKilledAction();

        Assert.Null(board.GetCell(40)!.Unit);
        Assert.True(unit.Affected.IsEmpty);
        Assert.False(unit.HasModes(BattleMonsterState.SpellSlow));
    }

    [Fact]
    public void TheCurrentColorFollowsTheMindSpells()
    {
        BattleUnit unit = Spawn(MonsterType.Peasant, 10, 40, PlayerColor.Blue);

        Assert.Equal(PlayerColor.Blue, unit.GetCurrentColor());

        unit.SetModes(BattleMonsterState.SpellBerserker);
        Assert.Equal(PlayerColor.None, unit.GetCurrentColor());

        unit.ResetModes(BattleMonsterState.SpellBerserker);
        unit.SetModes(BattleMonsterState.SpellHypnotize);
        unit.Field = new FakeField { Opposite = PlayerColor.Red };

        Assert.Equal(PlayerColor.Red, unit.GetCurrentColor());
    }
}
