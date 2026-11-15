using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using Xunit;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the four primary skills of a hero (<c>heroes.cpp:903-943</c>), their artifact modifiers
/// (<c>heroes_base.cpp:180-360</c>) and the point where they reach the army's strength
/// (<c>army.cpp:1291-1333</c>).
/// </summary>
public class HeroPrimarySkillsTests
{
    private static Hero NewHero() => new(1, MapRace.Knight);

    [Fact]
    public void ANewHeroHasZeroAttackAndDefense()
    {
        Hero hero = NewHero();

        Assert.Equal(0, hero.BaseAttack);
        Assert.Equal(0, hero.BaseDefense);
        Assert.Equal(0, hero.GetAttack());
        Assert.Equal(0, hero.GetDefense());
    }

    [Fact]
    public void ThePowerAndKnowledgeNeverDropToOneBelow()
    {
        // Both are clamped to 1..255: a hero with no power cannot cast anything, so the reference never lets
        // the effective value reach zero even though the base value can be zero.
        Hero hero = NewHero();

        Assert.Equal(0, hero.BasePower);
        Assert.Equal(0, hero.BaseKnowledge);
        Assert.Equal(1, hero.GetPower());
        Assert.Equal(1, hero.GetKnowledge());
    }

    [Fact]
    public void TheArtifactsRaiseTheAttack()
    {
        Hero hero = NewHero();
        hero.SetPrimarySkill(PrimarySkill.Attack, 3);
        hero.BagArtifacts.Push(ArtifactType.UltimateSword);

        Assert.Equal(12, hero.GetAttackModificator());
        Assert.Equal(15, hero.GetAttack());
    }

    [Fact]
    public void TwoCopiesOfTheSameArtifactAddUp()
    {
        // ATTACK_SKILL is cumulative: the second ultimate sword is another +12.
        Hero hero = NewHero();
        hero.BagArtifacts.Push(ArtifactType.UltimateSword);
        hero.BagArtifacts.Push(ArtifactType.UltimateSword);

        Assert.Equal(24, hero.GetAttack());
    }

    [Fact]
    public void TheCursesAreSubtractedFromThePower()
    {
        // The broach of shielding costs 2 spell power and the base is zero, so the effective value stays at
        // the floor of one; the modifier itself has to show the -2.
        Hero hero = NewHero();
        hero.BagArtifacts.Push(ArtifactType.BroachShielding);

        Assert.Equal(-2, hero.GetPowerModificator());
        Assert.Equal(1, hero.GetPower());

        hero.SetPrimarySkill(PrimarySkill.Power, 5);
        Assert.Equal(3, hero.GetPower());
    }

    [Fact]
    public void IncreaseRaisesOneSkillByOne()
    {
        Hero hero = NewHero();

        hero.IncreasePrimarySkill(PrimarySkill.Attack);
        hero.IncreasePrimarySkill(PrimarySkill.Attack);
        hero.IncreasePrimarySkill(PrimarySkill.Defense);
        hero.IncreasePrimarySkill(PrimarySkill.Power);
        hero.IncreasePrimarySkill(PrimarySkill.Knowledge);

        Assert.Equal(2, hero.BaseAttack);
        Assert.Equal(1, hero.BaseDefense);
        Assert.Equal(1, hero.BasePower);
        Assert.Equal(1, hero.BaseKnowledge);
    }

    [Fact]
    public void SetClampsToTheByteOfTheSaveFormat()
    {
        Hero hero = NewHero();

        hero.SetPrimarySkill(PrimarySkill.Attack, 300);
        hero.SetPrimarySkill(PrimarySkill.Defense, -5);

        Assert.Equal(255, hero.BaseAttack);
        Assert.Equal(0, hero.BaseDefense);
    }

    [Fact]
    public void TheEffectiveAttackIsClampedTo255()
    {
        Hero hero = NewHero();
        hero.SetPrimarySkill(PrimarySkill.Attack, 250);
        hero.BagArtifacts.Push(ArtifactType.UltimateSword);

        Assert.Equal(255, hero.GetAttack());
    }

    [Fact]
    public void TheMagicStrategicValueIsZeroWithoutASpellBook()
    {
        Hero hero = NewHero();

        Assert.Equal(0.0, hero.GetMagicStrategicValue(1000.0));
    }

    [Fact]
    public void TheHeroIsTheCommanderOfItsOwnArmy()
    {
        Hero hero = NewHero();

        Assert.Same(hero, hero.Army.Commander);
    }

    [Fact]
    public void TheArmyStrengthUsesTheCommandersAttackAndDefense()
    {
        // The hero's attack and defense are added to every stack's own stats, not substituted for them: an
        // archer is 5 / 3, a hero of 1 / 1 makes it 6 / 4, and the factor goes from 1.65 to 1.8.
        Hero hero = NewHero();
        hero.SetPrimarySkill(PrimarySkill.Attack, 1);
        hero.SetPrimarySkill(PrimarySkill.Defense, 1);
        hero.Army.JoinTroop(MonsterType.Archer, 1);

        Assert.Equal(6.0 * (1 + 6 * 0.1 + 4 * 0.05), hero.Army.GetStrength(), 9);
    }

    [Fact]
    public void TheArcherySkillScalesTheShootersOnly()
    {
        Hero hero = NewHero();
        hero.SetPrimarySkill(PrimarySkill.Attack, 1);
        hero.SetPrimarySkill(PrimarySkill.Defense, 1);
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Archery, SkillLevel.Basic));
        hero.Army.JoinTroop(MonsterType.Archer, 1);

        // Basic archery is worth 10 percent, and the reference takes the square root of it.
        Assert.Equal(6.0 * (1 + 6 * 0.1 + 4 * 0.05) * Math.Sqrt(1.1), hero.Army.GetStrength(), 9);

        // A melee stack ignores the skill entirely.
        var army = new ArmyStack();
        army.Commander = hero;
        army.JoinTroop(MonsterType.Swordsman, 1);

        MonsterBattleStats swordsman = MonsterStatistics.GetBattleStats(MonsterType.Swordsman);
        double expected = MonsterStatistics.GetMonsterStrength(
            MonsterType.Swordsman,
            swordsman.Attack + 1,
            swordsman.Defense + 1);
        Assert.Equal(expected, army.GetStrength(), 9);
    }
}
