using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the ability table of <c>monster_info.cpp:388-590</c>. The numbers are the reference's: fifty
/// creatures get at least one ability out of one hundred and eight statements, eleven of them are undead,
/// four are elementals, thirteen fly, four are dragons and sixteen take two hexes.
/// </summary>
public class MonsterAbilitiesTests
{
    private static readonly MonsterType[] Undead =
    [
        MonsterType.Skeleton,
        MonsterType.Zombie,
        MonsterType.MutantZombie,
        MonsterType.Mummy,
        MonsterType.RoyalMummy,
        MonsterType.Vampire,
        MonsterType.VampireLord,
        MonsterType.Lich,
        MonsterType.PowerLich,
        MonsterType.BoneDragon,
        MonsterType.Ghost,
    ];

    private static readonly MonsterType[] Elementals =
    [
        MonsterType.AirElement,
        MonsterType.EarthElement,
        MonsterType.FireElement,
        MonsterType.WaterElement,
    ];

    private static readonly MonsterType[] Dragons =
    [
        MonsterType.GreenDragon,
        MonsterType.RedDragon,
        MonsterType.BlackDragon,
        MonsterType.BoneDragon,
    ];

    private static MonsterType[] Monsters()
    {
        return Enum.GetValues<MonsterType>().Distinct().ToArray();
    }

    [Fact]
    public void TheUndeadAreTheNecropolisLineAndTheGhost()
    {
        Assert.Equal(Undead.Order(), Monsters().Where(MonsterAbilities.IsUndead).Order());
    }

    [Fact]
    public void TheElementalsAreTheFourSummonedOnes()
    {
        Assert.Equal(Elementals.Order(), Monsters().Where(MonsterAbilities.IsElemental).Order());

        // The phoenix is immune to elemental spells but is a living creature, not an elemental.
        Assert.False(MonsterAbilities.IsElemental(MonsterType.Phoenix));
    }

    [Fact]
    public void TheDragonsAreTheFourGreatWorms()
    {
        Assert.Equal(Dragons.Order(), Monsters().Where(MonsterAbilities.IsDragons).Order());
    }

    [Fact]
    public void ThirteenCreaturesFly()
    {
        MonsterType[] expected =
        [
            MonsterType.Sprite,
            MonsterType.Phoenix,
            MonsterType.Gargoyle,
            MonsterType.Griffin,
            MonsterType.GreenDragon,
            MonsterType.RedDragon,
            MonsterType.BlackDragon,
            MonsterType.Roc,
            MonsterType.Vampire,
            MonsterType.VampireLord,
            MonsterType.BoneDragon,
            MonsterType.Ghost,
            MonsterType.Genie,
        ];

        Assert.Equal(expected.Order(), Monsters().Where(MonsterAbilities.IsFlying).Order());
    }

    [Fact]
    public void SixteenCreaturesTakeTwoHexes()
    {
        MonsterType[] expected =
        [
            MonsterType.Cavalry,
            MonsterType.Champion,
            MonsterType.Wolf,
            MonsterType.Unicorn,
            MonsterType.Phoenix,
            MonsterType.Centaur,
            MonsterType.Griffin,
            MonsterType.Hydra,
            MonsterType.GreenDragon,
            MonsterType.RedDragon,
            MonsterType.BlackDragon,
            MonsterType.Boar,
            MonsterType.Roc,
            MonsterType.BoneDragon,
            MonsterType.Medusa,
            MonsterType.Nomad,
        ];

        Assert.Equal(expected.Order(), Monsters().Where(MonsterAbilities.IsWide).Order());
        Assert.True(MonsterAbilities.IsWide(MonsterType.Phoenix));
        Assert.False(MonsterAbilities.IsWide(MonsterType.Peasant));
    }

    [Fact]
    public void MoraleSkipsTheUndeadAndTheElementals()
    {
        Assert.False(MonsterAbilities.IsAffectedByMorale(MonsterType.Skeleton));
        Assert.False(MonsterAbilities.IsAffectedByMorale(MonsterType.Ghost));
        Assert.False(MonsterAbilities.IsAffectedByMorale(MonsterType.BoneDragon));
        Assert.False(MonsterAbilities.IsAffectedByMorale(MonsterType.AirElement));

        // Everything else is affected, including creatures that are immune to some spells.
        Assert.True(MonsterAbilities.IsAffectedByMorale(MonsterType.Peasant));
        Assert.True(MonsterAbilities.IsAffectedByMorale(MonsterType.Phoenix));
    }

    [Fact]
    public void FiftyCreaturesHaveAtLeastOneAbility()
    {
        Assert.Equal(50, Monsters().Count(monster => MonsterAbilities.Get(monster) != MonsterAbility.None));

        // The rest of the table — peasants, goblins, most of the level one and two stacks — has no row.
        Assert.Equal(MonsterAbility.None, MonsterAbilities.Get(MonsterType.Peasant));
        Assert.Equal(MonsterAbility.None, MonsterAbilities.Get(MonsterType.Goblin));
    }

    [Fact]
    public void TheRowsHoldEveryBitTheReferenceGivesThem()
    {
        Assert.Equal(
            MonsterAbility.DoubleMeleeAttack | MonsterAbility.DoubleDamageToUndead | MonsterAbility.ImmuneToCertainSpell,
            MonsterAbilities.Get(MonsterType.Crusader));

        Assert.Equal(
            MonsterAbility.Undead | MonsterAbility.Dragon | MonsterAbility.DoubleHexSize | MonsterAbility.Flying | MonsterAbility.MoralDecrement,
            MonsterAbilities.Get(MonsterType.BoneDragon));

        Assert.Equal(
            MonsterAbility.Elemental | MonsterAbility.WaterCreature | MonsterAbility.ColdSpellImmunity,
            MonsterAbilities.Get(MonsterType.WaterElement));
    }

    [Fact]
    public void TheAbilityFlagsAreDistinctBits()
    {
        // Thirty two abilities above "none", so the mask is 33 bits wide: in a 32 bit enum the shift that
        // makes "soul eater" would wrap around onto the "double hex size" bit.
        Assert.Equal(1ul << 32, (ulong)MonsterAbility.SoulEater);

        ulong seen = 0;

        foreach (MonsterAbility ability in Enum.GetValues<MonsterAbility>())
        {
            ulong bits = (ulong)ability;

            if (bits == 0)
            {
                Assert.Equal(MonsterAbility.None, ability);
                continue;
            }

            Assert.Equal(0ul, bits & (bits - 1));
            Assert.Equal(0ul, seen & bits);

            seen |= bits;
        }
    }

    [Fact]
    public void TheTableKeepsTheNumbersOfTheReference()
    {
        // One hundred and eight statements in the reference, kept as instances so the numbers are not lost.
        Assert.Equal(108, Monsters().Sum(monster => MonsterAbilities.GetInstances(monster).Count));

        Assert.True(MonsterAbilities.TryGetInstance(MonsterType.Medusa, MonsterAbility.SpellCaster, out MonsterAbilityInstance gaze));
        Assert.Equal(SpellType.Petrify, gaze.Spell);
        Assert.Equal(20, gaze.Percentage);

        Assert.True(MonsterAbilities.TryGetInstance(MonsterType.RoyalMummy, MonsterAbility.SpellCaster, out MonsterAbilityInstance curse));
        Assert.Equal(SpellType.Curse, curse.Spell);
        Assert.Equal(30, curse.Percentage);

        Assert.True(MonsterAbilities.TryGetInstance(MonsterType.Dwarf, MonsterAbility.MagicResistance, out MonsterAbilityInstance resistance));
        Assert.Equal(25, resistance.Percentage);

        Assert.True(MonsterAbilities.TryGetInstance(MonsterType.Genie, MonsterAbility.EnemyHalving, out MonsterAbilityInstance halving));
        Assert.Equal(10, halving.Percentage);

        // The bone dragon's morale hit carries the amount in the second number.
        Assert.True(MonsterAbilities.TryGetInstance(MonsterType.BoneDragon, MonsterAbility.MoralDecrement, out MonsterAbilityInstance decrement));
        Assert.Equal(1, decrement.Value);
    }

    [Fact]
    public void AnImmuneSpellIsOneInstancePerSpell()
    {
        // The crusader is immune to curse and to mass curse, the earth elemental to three lightning spells. The
        // mask cannot tell them apart, the instances can.
        MonsterAbilityInstance[] crusader = MonsterAbilities.GetInstances(MonsterType.Crusader).ToArray();
        Assert.Equal(2, crusader.Count(entry => entry.Type == MonsterAbility.ImmuneToCertainSpell));
        Assert.Contains(crusader, entry => entry.Spell == SpellType.Curse);
        Assert.Contains(crusader, entry => entry.Spell == SpellType.MassCurse);

        MonsterAbilityInstance[] earth = MonsterAbilities.GetInstances(MonsterType.EarthElement).ToArray();
        Assert.Equal(3, earth.Count(entry => entry.Type == MonsterAbility.ImmuneToCertainSpell));
    }

    [Fact]
    public void TheMaskIsTheUnionOfTheInstances()
    {
        foreach (MonsterType monster in Monsters())
        {
            MonsterAbility folded = MonsterAbility.None;

            foreach (MonsterAbilityInstance instance in MonsterAbilities.GetInstances(monster))
            {
                folded |= instance.Type;
            }

            Assert.Equal(folded, MonsterAbilities.Get(monster));
        }
    }

    [Fact]
    public void ACreatureWithoutARowHasNothing()
    {
        Assert.Empty(MonsterAbilities.GetInstances(MonsterType.Peasant));
        Assert.False(MonsterAbilities.TryGetInstance(MonsterType.Peasant, MonsterAbility.Flying, out _));
        Assert.False(MonsterAbilities.TryGetInstance(MonsterType.RandomMonsterLevel3, MonsterAbility.Undead, out _));
    }
}
