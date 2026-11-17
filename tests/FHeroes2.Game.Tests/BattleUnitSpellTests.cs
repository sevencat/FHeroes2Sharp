using System.Linq;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the spell application half of <c>Battle::Unit</c> (<c>battle_troop.cpp:65-98</c>, <c>793-890</c>,
/// <c>1174-1534</c>): who may cast what on whom, what a damage spell does after abilities and artifacts,
/// and the three ways a spell lands.
/// </summary>
public class BattleUnitSpellTests
{
    /// <summary>A commander that answers exactly what a test needs, without a hero or a town.</summary>
    private sealed class Commander : IArmyCommander
    {
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

        public int GetMorale() => 0;

        public int GetLuck() => 0;
    }

    private static BattleUnit Unit(MonsterType monster, uint count, PlayerColor color = PlayerColor.Blue, IArmyCommander? commander = null)
    {
        GameArmy army = new() { Color = color, Commander = commander };
        army.GetTroop(0).Set(monster, count);

        return new BattleUnit(
            new Troop(monster, count),
            new BattlePosition(new BattleBoardCells(), 30, false, false),
            false,
            1)
        {
            Army = army,
            Color = color,
        };
    }

    [Fact]
    public void FriendlySpellsDoNotLandOnTheEnemySide()
    {
        Commander caster = new() { Color = PlayerColor.Blue };

        Assert.False(Unit(MonsterType.Goblin, 10, PlayerColor.Green).AllowApplySpell(new Spell(SpellType.Bless), caster));

        // 同一个颜色就放行（Goblin 没有抗魔）。
        Assert.True(Unit(MonsterType.Goblin, 10, PlayerColor.Blue).AllowApplySpell(new Spell(SpellType.Bless), caster));
    }

    [Fact]
    public void HostileSpellsDoNotLandOnTheirOwnSideUnlessForced()
    {
        Commander caster = new() { Color = PlayerColor.Blue };
        BattleUnit own = Unit(MonsterType.Goblin, 10, PlayerColor.Blue);

        Assert.False(own.AllowApplySpell(new Spell(SpellType.Curse), caster));

        // 连锁闪电是唯一「可能落在自己人身上」的法术，靠强制开关放行。
        Assert.True(own.AllowApplySpell(new Spell(SpellType.ChainLightning), caster, forceApplyToAlly: true));

        // 落在对面本来就允许。
        Assert.True(Unit(MonsterType.Goblin, 10, PlayerColor.Green).AllowApplySpell(new Spell(SpellType.Curse), caster));
    }

    [Fact]
    public void AMirrorImageTakesNoSpellsAndItsOwnerTakesNoMoreMirrors()
    {
        BattleUnit image = Unit(MonsterType.Goblin, 10);
        image.SetModes(BattleMonsterState.CapMirrorImage);

        Assert.False(image.AllowApplySpell(new Spell(SpellType.AntiMagic), null));
        Assert.False(image.AllowApplySpell(new Spell(SpellType.MirrorImage), null));

        // 除了这两条，镜像照样吃别的增益。
        Assert.True(image.AllowApplySpell(new Spell(SpellType.Bless), null));

        BattleUnit owner = Unit(MonsterType.Goblin, 10);
        owner.SetModes(BattleMonsterState.CapMirrorOwner);

        Assert.False(owner.AllowApplySpell(new Spell(SpellType.MirrorImage), null));
    }

    [Fact]
    public void AntiMagicMakesTheUnitFullyResistant()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);
        unit.SetModes(BattleMonsterState.SpellAntiMagic);

        Assert.Equal(100u, unit.GetMagicResist(new Spell(SpellType.Fireball), null));
        Assert.False(unit.AllowApplySpell(new Spell(SpellType.Fireball), null));
    }

    [Fact]
    public void CureIsPointlessOnAFullAndCleanStack()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);

        Assert.Equal(100u, unit.GetMagicResist(new Spell(SpellType.Cure), null));

        // 挂着诅咒就值得治，抗魔回到零。
        unit.SetModes(BattleMonsterState.SpellCurse);
        Assert.Equal(0u, unit.GetMagicResist(new Spell(SpellType.Cure), null));
    }

    [Fact]
    public void ResurrectionIsPointlessAtFullStrength()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);
        BattleUnit enemy = Unit(MonsterType.Peasant, 1);

        // 满员但有伤：参考只看数量，仍然拒绝复活。
        unit.ApplyDamage(enemy, 1, out _, out _);
        Assert.True(unit.IsHaveDamage);
        Assert.Equal(100u, unit.GetMagicResist(new Spell(SpellType.Resurrect), null));

        // 死掉一只之后才有意义。
        unit.ApplyDamage(enemy, 5, out _, out _);
        Assert.True(unit.Count < unit.MaxCount);
        Assert.Equal(0u, unit.GetMagicResist(new Spell(SpellType.Resurrect), null));
    }

    [Fact]
    public void DispelIsPointlessWithoutMagic()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);

        Assert.Equal(100u, unit.GetMagicResist(new Spell(SpellType.Dispel), null));

        unit.SetModes(BattleMonsterState.SpellHaste);
        Assert.Equal(0u, unit.GetMagicResist(new Spell(SpellType.Dispel), null));
    }

    [Fact]
    public void ArtifactImmunityStopsTheSpell()
    {
        Commander commander = new();
        commander.BagArtifacts.Push(ArtifactType.WandNegation);

        BattleUnit unit = Unit(MonsterType.Goblin, 10, PlayerColor.Blue, commander);
        unit.SetModes(BattleMonsterState.SpellHaste);

        Assert.Equal(100u, unit.GetMagicResist(new Spell(SpellType.Dispel), null));
        Assert.False(unit.AllowApplySpell(new Spell(SpellType.Dispel), null));
    }

    [Fact]
    public void ABlessingLastsAsManyTurnsAsTheCastersSpellPower()
    {
        Commander caster = new() { Power = 4 };
        BattleUnit unit = Unit(MonsterType.Goblin, 10, PlayerColor.Blue, caster);

        Assert.True(unit.ApplySpell(new Spell(SpellType.Bless), caster, new BattleTargetInfo(unit)));
        Assert.True(unit.HasModes(BattleMonsterState.SpellBless));
        Assert.Equal(4u, unit.Affected.GetMode(BattleMonsterState.SpellBless));
    }

    [Fact]
    public void CurseReplacesBlessing()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);

        unit.ApplySpell(new Spell(SpellType.Bless), null, new BattleTargetInfo(unit));
        unit.ApplySpell(new Spell(SpellType.Curse), null, new BattleTargetInfo(unit));

        Assert.False(unit.HasModes(BattleMonsterState.SpellBless));
        Assert.True(unit.HasModes(BattleMonsterState.SpellCurse));
    }

    [Fact]
    public void BloodlustAlwaysLastsThreeTurns()
    {
        Commander caster = new() { Power = 10 };
        BattleUnit unit = Unit(MonsterType.Goblin, 10, PlayerColor.Blue, caster);

        unit.ApplySpell(new Spell(SpellType.BloodLust), caster, new BattleTargetInfo(unit));

        // 参考的嗜血是唯一的定长效果，不跟法术强度走。
        Assert.Equal(3u, unit.Affected.GetMode(BattleMonsterState.SpellBloodlust));
    }

    [Fact]
    public void DispelRemovesTheMagic()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);

        unit.ApplySpell(new Spell(SpellType.Bless), null, new BattleTargetInfo(unit));
        unit.ApplySpell(new Spell(SpellType.Dispel), null, new BattleTargetInfo(unit));

        Assert.False(unit.HasModes(BattleMonsterState.SpellBless));
        Assert.True(unit.Affected.IsEmpty);
    }

    [Fact]
    public void BlindingClearsTheBlindRetaliation()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);
        unit.BlindRetaliation = true;

        unit.ApplySpell(new Spell(SpellType.Blind), null, new BattleTargetInfo(unit));

        Assert.True(unit.HasModes(BattleMonsterState.SpellBlind));
        Assert.False(unit.BlindRetaliation);
    }

    [Fact]
    public void ABlessedUnitListsItsEffectsInTheReferenceOrder()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);

        unit.ApplySpell(new Spell(SpellType.Haste), null, new BattleTargetInfo(unit));
        unit.ApplySpell(new Spell(SpellType.Bless), null, new BattleTargetInfo(unit));

        Assert.Equal(
            new[] { SpellType.Bless, SpellType.Haste },
            unit.GetCurrentSpellEffects().Select(spell => spell.Type));
    }

    [Fact]
    public void MirrorOwnershipIsListedAsMirrorImage()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);

        Assert.Empty(unit.GetCurrentSpellEffects());

        unit.SetModes(BattleMonsterState.CapMirrorOwner);

        Assert.Equal(new[] { SpellType.MirrorImage }, unit.GetCurrentSpellEffects().Select(spell => spell.Type));
    }

    [Fact]
    public void GolemHalvesArmageddonFarMoreThanAFireball()
    {
        BattleUnit golem = Unit(MonsterType.IronGolem, 1);

        // 指定法术减伤 50% 比元素法术减伤更具体，Armageddon 只吃前一条。
        Assert.Equal(25u, golem.CalculateSpellDamage(new Spell(SpellType.Armageddon), 1, null, 0));

        // 别的元素法术落到「元素法术减伤 50%」。
        Assert.Equal(5u, golem.CalculateSpellDamage(new Spell(SpellType.Fireball), 1, null, 0));
    }

    [Fact]
    public void WaterElementalTakesDoubleFireDamage()
    {
        BattleUnit element = Unit(MonsterType.WaterElement, 1);

        Assert.Equal(40u, element.CalculateSpellDamage(new Spell(SpellType.Fireball), 2, null, 0));
    }

    [Fact]
    public void AirElementalTakesDoubleChainLightningDamage()
    {
        BattleUnit element = Unit(MonsterType.AirElement, 1);

        Assert.Equal(80u, element.CalculateSpellDamage(new Spell(SpellType.ChainLightning), 1, null, 0));
    }

    [Fact]
    public void ChainLightningLosesHalfWithEveryJump()
    {
        BattleUnit unit = Unit(MonsterType.Peasant, 1);
        Commander caster = new();

        // 减半在参考里挂在「有施法者」的宝物分支里，没有英雄时不做减半。
        Assert.Equal(40u, unit.CalculateSpellDamage(new Spell(SpellType.ChainLightning), 1, caster, 0));
        Assert.Equal(20u, unit.CalculateSpellDamage(new Spell(SpellType.ChainLightning), 1, caster, 1));
        Assert.Equal(10u, unit.CalculateSpellDamage(new Spell(SpellType.ChainLightning), 1, caster, 2));
        Assert.Equal(5u, unit.CalculateSpellDamage(new Spell(SpellType.ChainLightning), 1, caster, 3));

        // 参考的 switch 只写了 0..3，其余落到 default 的「不减」；连锁闪电最多打四个目标，这条走不到。
        Assert.Equal(40u, unit.CalculateSpellDamage(new Spell(SpellType.ChainLightning), 1, caster, 4));
    }

    [Fact]
    public void FireballKillsAndReportsTheToll()
    {
        BattleUnit unit = Unit(MonsterType.Peasant, 100);
        BattleTargetInfo target = new(unit);

        Assert.True(unit.ApplySpell(new Spell(SpellType.Fireball), null, target));

        // 内置强度 3 → 10 × 3 = 30 点，农民每只 1 点血。
        Assert.Equal(30u, target.Damage);
        Assert.Equal(30u, target.Killed);
        Assert.Equal(70u, unit.Count);
        Assert.Equal(70u, unit.HitPoints);
    }

    [Fact]
    public void CureHealsAndRemovesTheCurse()
    {
        BattleUnit unit = Unit(MonsterType.Goblin, 10);
        BattleUnit enemy = Unit(MonsterType.Peasant, 1);

        // 1 点伤害：地精（3 点血）少了一点血，但没有死人。
        unit.ApplyDamage(enemy, 1, out _, out _);
        Assert.True(unit.IsHaveDamage);
        Assert.Equal(10u, unit.Count);

        unit.SetModes(BattleMonsterState.SpellCurse);
        unit.Affected.AddMode(BattleMonsterState.SpellCurse, 3);

        Assert.True(unit.ApplySpell(new Spell(SpellType.Cure), null, new BattleTargetInfo(unit)));

        Assert.False(unit.HasModes(BattleMonsterState.SpellCurse));
        Assert.False(unit.IsHaveDamage);

        // 治疗不回生：数量还是十只，血量截在编成的上限。
        Assert.Equal(10u, unit.Count);
        Assert.Equal(30u, unit.HitPoints);
    }

    [Fact]
    public void TrueResurrectionBringsTheStackBack()
    {
        BattleUnit unit = Unit(MonsterType.Peasant, 5);

        unit.ApplySpell(new Spell(SpellType.LightningBolt), null, new BattleTargetInfo(unit));

        Assert.False(unit.IsValid);
        Assert.Equal(0u, unit.Count);

        Assert.True(unit.ApplySpell(new Spell(SpellType.ResurrectTrue), null, new BattleTargetInfo(unit)));

        Assert.True(unit.IsValid);
        Assert.Equal(5u, unit.Count);
        Assert.Equal(5u, unit.HitPoints);
        Assert.Equal(0u, unit.Dead);
    }

    [Fact]
    public void MonsterMagicResistanceIsAskedLast()
    {
        Assert.Equal(25u, Unit(MonsterType.Dwarf, 1).GetMagicResist(new Spell(SpellType.Fireball), null));
        Assert.Equal(100u, Unit(MonsterType.BlackDragon, 1).GetMagicResist(new Spell(SpellType.Fireball), null));
        Assert.Equal(0u, Unit(MonsterType.Peasant, 1).GetMagicResist(new Spell(SpellType.Fireball), null));
    }

    [Fact]
    public void HypnotizeIsPointlessOnATooStrongStack()
    {
        Commander caster = new() { Power = 1 };

        // 催眠的强度是 25 × 法术强度：十只农民（10 点血）能被控制，一百只不能。
        Assert.Equal(0u, Unit(MonsterType.Peasant, 10).GetMagicResist(new Spell(SpellType.Hypnotize), caster));
        Assert.Equal(100u, Unit(MonsterType.Peasant, 100).GetMagicResist(new Spell(SpellType.Hypnotize), caster));
    }
}
