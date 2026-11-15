using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the spell table (<c>spell.cpp:55-150</c>), the level and the trait predicates
/// (<c>spell.cpp:269-750</c>) and the strategic value (<c>spell.cpp:221-267</c>).
/// </summary>
public class SpellTests
{
    [Fact]
    public void TheTableHasOneRowPerId()
    {
        // 73 ids: the 66 real spells, the five random placeholders and Petrify.
        Assert.Equal((int)SpellType.Count, SpellTable.Count);
        Assert.Equal(73, SpellTable.Count);
    }

    [Theory]
    [InlineData(SpellType.Fireball, 9, 0, 0, 8, 10)]
    [InlineData(SpellType.LightningBolt, 7, 0, 0, 4, 25)]
    [InlineData(SpellType.Resurrect, 12, 0, 0, 13, 50)]
    [InlineData(SpellType.Haste, 3, 0, 0, 14, 2)]
    [InlineData(SpellType.Blind, 6, 0, 0, 21, 50)]
    [InlineData(SpellType.SummonEElement, 30, 0, 0, 56, 3)]
    [InlineData(SpellType.DimensionDoor, 10, 225, 69, 47, 0)]
    [InlineData(SpellType.TownPortal, 20, 225, 69, 49, 0)]
    public void TheNumbersOfTheTableAreTheReferences(
        SpellType spell, uint spellPoints, uint movePoints, uint minMovePoints, uint imageId, uint extraValue)
    {
        SpellStats stats = SpellTable.Get(spell);

        Assert.Equal(spellPoints, stats.SpellPoints);
        Assert.Equal(movePoints, stats.MovePoints);
        Assert.Equal(minMovePoints, stats.MinMovePoints);
        Assert.Equal(imageId, stats.ImageId);
        Assert.Equal(extraValue, stats.ExtraValue);
    }

    [Fact]
    public void ThePlaceholdersCostNothingAndDoNothing()
    {
        // The random spells are picked by level, never cast, and Petrify is the medusa's gaze: all six have a
        // row of zeros so that the index arithmetic keeps working.
        foreach (SpellType spell in new[] { SpellType.Random, SpellType.Random1, SpellType.Random5, SpellType.Petrify })
        {
            Spell value = new(spell);

            Assert.Equal(0u, value.SpellPoints());
            Assert.Equal(0u, value.ExtraValue());
            Assert.Equal(0, value.Level());
            Assert.Equal(0.0, value.GetStrategicValue(1000, 1000, 10));
        }
    }

    [Fact]
    public void AnOutOfRangeIdReadsAsNoSpell()
    {
        Assert.Equal(SpellType.None, new Spell(-1).Type);
        Assert.Equal(SpellType.None, new Spell((int)SpellType.Count).Type);
        Assert.Equal(SpellType.None, new Spell(1000).Type);
        Assert.Equal(SpellType.None, default(Spell).Type);
        Assert.Equal(SpellType.TownPortal, new Spell(59).Type);
        Assert.Equal(SpellType.TownPortal, new Spell(SpellType.TownPortal).Type);
    }

    [Theory]
    [InlineData(SpellType.Fireball, 3)]
    [InlineData(SpellType.TownPortal, 5)]
    [InlineData(SpellType.DimensionDoor, 5)]
    [InlineData(SpellType.ViewMines, 1)]
    [InlineData(SpellType.ViewAll, 4)]
    [InlineData(SpellType.Blind, 2)]
    [InlineData(SpellType.Paralyze, 3)]
    [InlineData(SpellType.Resurrect, 4)]
    [InlineData(SpellType.None, 0)]
    public void TheLevelsAreTheReferences(SpellType spell, int level)
    {
        Spell value = new(spell);

        Assert.Equal(level, value.Level());

        // The adventure map spells have a level too — they are what a mage guild offers.
        for (int other = 1; other <= 5; ++other)
        {
            Assert.Equal(other == level, value.IsLevel(other));
        }
    }

    [Theory]
    [InlineData(SpellType.Fireball, true)]
    [InlineData(SpellType.Blind, true)]
    [InlineData(SpellType.SummonEElement, true)]
    [InlineData(SpellType.ViewAll, false)]
    [InlineData(SpellType.TownPortal, false)]
    [InlineData(SpellType.DimensionDoor, false)]
    [InlineData(SpellType.Haunt, false)]
    [InlineData(SpellType.SetWGuardian, false)]
    [InlineData(SpellType.None, false)]
    public void CombatIsADenyList(SpellType spell, bool isCombat)
    {
        Spell value = new(spell);

        Assert.Equal(isCombat, value.IsCombat());
        Assert.Equal(!isCombat, value.IsAdventure());
    }

    [Fact]
    public void TheDamageAndTheResurrectionComeFromTheExtraValue()
    {
        Assert.Equal(10u, new Spell(SpellType.Fireball).Damage());
        Assert.Equal(25u, new Spell(SpellType.LightningBolt).Damage());
        Assert.True(new Spell(SpellType.MeteorShower).IsDamage());

        // Haste has an extra value of two, but it is neither damage nor resurrection: the column means
        // something different for every spell.
        Spell haste = new(SpellType.Haste);
        Assert.Equal(2u, haste.ExtraValue());
        Assert.Equal(0u, haste.Damage());
        Assert.False(haste.IsDamage());

        Assert.Equal(50u, new Spell(SpellType.Resurrect).Resurrect());
        Assert.Equal(50u, new Spell(SpellType.ResurrectTrue).Resurrect());
        Assert.True(new Spell(SpellType.AnimateDead).IsResurrect());
        Assert.False(new Spell(SpellType.Fireball).IsResurrect());
    }

    [Fact]
    public void TheFourSummonsAndTheSevenMassSpellsAreListed()
    {
        Assert.True(new Spell(SpellType.SummonEElement).IsSummon());
        Assert.True(new Spell(SpellType.SummonAElement).IsSummon());
        Assert.True(new Spell(SpellType.SummonFElement).IsSummon());
        Assert.True(new Spell(SpellType.SummonWElement).IsSummon());
        Assert.False(new Spell(SpellType.MirrorImage).IsSummon());

        int mass = 0;

        foreach (SpellType spell in Enum.GetValues<SpellType>())
        {
            if (new Spell(spell).IsMassActions())
            {
                ++mass;
            }
        }

        Assert.Equal(7, mass);
    }

    [Fact]
    public void TheMindInfluenceIsTheFourSpellsThatTakeControl()
    {
        Assert.True(new Spell(SpellType.Blind).IsMindInfluence());
        Assert.True(new Spell(SpellType.Paralyze).IsMindInfluence());
        Assert.True(new Spell(SpellType.Berserker).IsMindInfluence());
        Assert.True(new Spell(SpellType.Hypnotize).IsMindInfluence());
        Assert.False(new Spell(SpellType.Slow).IsMindInfluence());
    }

    [Fact]
    public void TheSpellCostsIsTheTablePrice()
    {
        // The hero parameter of the reference only lowers the cost through the artifacts, which are not
        // ported, so every hero pays the list price.
        Assert.Equal(9u, new Spell(SpellType.Fireball).SpellPoints());
        Assert.Equal(20u, new Spell(SpellType.TownPortal).SpellPoints());
        Assert.Equal(0u, new Spell(SpellType.None).SpellPoints());
    }

    [Fact]
    public void ADamageSpellIsWorthItsDamageTimesThePowerTimesTheCasts()
    {
        Spell fireball = new(SpellType.Fireball);

        // One cast costs nine points: the second-to-tenth casts are worth less and less.
        Assert.Equal(30, fireball.GetStrategicValue(1000, 9, 3), 9);
        Assert.Equal(1.8 * 10 * 3, fireball.GetStrategicValue(1000, 18, 3), 9);
        Assert.Equal(5.0 * 10 * 3, fireball.GetStrategicValue(1000, 90, 3), 9);

        // The cap is ten casts, so more knowledge stops paying here.
        Assert.Equal(150, fireball.GetStrategicValue(1000, 9000, 3), 9);

        // Without a single cast there is no value at all.
        Assert.Equal(0.0, fireball.GetStrategicValue(1000, 8, 3));
        Assert.Equal(0.0, fireball.GetStrategicValue(1000, 0, 3));
    }

    [Fact]
    public void TheBattleWinningSpellsScaleWithTheArmy()
    {
        // Resurrection, the mass spells, Blind and Paralyze are the flat ten percent of the army.
        Assert.Equal(1000 * 0.1 * (3 - (0.05 * 3 * 3)), new Spell(SpellType.Resurrect).GetStrategicValue(1000, 36, 1), 9);
        Assert.Equal(1000 * 0.1 * 1.8, new Spell(SpellType.MassHaste).GetStrategicValue(1000, 20, 1), 9);
        Assert.Equal(1000 * 0.1, new Spell(SpellType.Blind).GetStrategicValue(1000, 6, 1), 9);

        // Paralyze has no extra value at all and is still in this pile, which is why the pile is decided by
        // the spell and not by the numbers.
        Assert.Equal(1000 * 0.1, new Spell(SpellType.Paralyze).GetStrategicValue(1000, 9, 1), 9);

        // Everything else is four percent.
        Assert.Equal(1000 * 0.04, new Spell(SpellType.Shield).GetStrategicValue(1000, 3, 1), 9);
        Assert.Equal(1000 * 0.04 * 1.8, new Spell(SpellType.Slow).GetStrategicValue(1000, 6, 1), 9);
    }

    [Fact]
    public void ASummonIsMeasuredWithTheWrongMonster()
    {
        // The reference asks Monster( id ) with the *spell* id (spell.cpp:263), so Summon Earth Elemental —
        // spell 44 — reports the strength of monster 44, the mage, not of the earth elemental. The port copies
        // the expression, because the numbers of the AI ranking depend on it.
        Assert.Equal(44, (int)MonsterType.Mage);

        Spell summon = new(SpellType.SummonEElement);
        double expected = MonsterStatistics.GetMonsterStrength(MonsterType.Mage) * 3 * 4;

        Assert.Equal(expected, summon.GetStrategicValue(1000, 30, 4), 9);

        // No cast, no value, and the casts do not diminish the worth of a summon either.
        Assert.Equal(0.0, summon.GetStrategicValue(1000, 29, 4));
        Assert.Equal(expected, summon.GetStrategicValue(1000, 300, 4), 9);
    }

    [Fact]
    public void TheAdventureSpellsAreWorthAPlainAmount()
    {
        // The three movement spells are flat values that still diminish with the casts, View All does not.
        Assert.Equal(500, new Spell(SpellType.DimensionDoor).GetStrategicValue(1000, 10, 1), 9);
        Assert.Equal(900, new Spell(SpellType.DimensionDoor).GetStrategicValue(1000, 20, 1), 9);
        Assert.Equal(250, new Spell(SpellType.TownPortal).GetStrategicValue(1000, 20, 1), 9);
        Assert.Equal(250, new Spell(SpellType.TownGate).GetStrategicValue(1000, 10, 1), 9);
        Assert.Equal(500, new Spell(SpellType.ViewAll).GetStrategicValue(1000, 3, 1), 9);

        // An adventure spell the switch does not mention is worth nothing rather than the four percent.
        Assert.Equal(0.0, new Spell(SpellType.ViewMines).GetStrategicValue(1000, 3, 1));
        Assert.Equal(0.0, new Spell(SpellType.SummonBoat).GetStrategicValue(1000, 3, 1));
    }
}
