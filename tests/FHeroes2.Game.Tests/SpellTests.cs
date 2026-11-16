using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
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

    [Fact]
    public void TheHealingSpellsCarryTheirRestoreValue()
    {
        // Restore() is the extra value of the two healing spells and nothing for everything else
        // (spell.cpp:450-462).
        Assert.Equal(5u, new Spell(SpellType.Cure).Restore());
        Assert.Equal(5u, new Spell(SpellType.MassCure).Restore());
        Assert.True(new Spell(SpellType.Cure).IsRestore());
        Assert.True(new Spell(SpellType.MassCure).IsRestore());

        Assert.Equal(0u, new Spell(SpellType.Fireball).Restore());
        Assert.False(new Spell(SpellType.Fireball).IsRestore());
        Assert.False(new Spell(SpellType.Resurrect).IsRestore());
    }

    [Fact]
    public void TheTargetingPredicatesPickExactlyTheReferenceSpells()
    {
        // spell.cpp:562-776, listed here in the order of the spell table.
        Assert.Equal(
            [SpellType.HolyWord, SpellType.HolyShout, SpellType.AnimateDead],
            Select(spell => spell.IsUndeadOnly()));

        Assert.Equal(
            [
                SpellType.Resurrect, SpellType.ResurrectTrue, SpellType.Bless, SpellType.MassBless,
                SpellType.Curse, SpellType.MassCurse, SpellType.DeathRipple, SpellType.DeathWave,
            ],
            Select(spell => spell.IsAliveOnly()));

        Assert.Equal(
            [
                SpellType.LightningBolt, SpellType.Teleport, SpellType.Cure, SpellType.Resurrect,
                SpellType.ResurrectTrue, SpellType.Haste, SpellType.Slow, SpellType.Blind, SpellType.Bless,
                SpellType.StoneSkin, SpellType.SteelSkin, SpellType.Curse, SpellType.AntiMagic,
                SpellType.Dispel, SpellType.Arrow, SpellType.Berserker, SpellType.Paralyze,
                SpellType.Hypnotize, SpellType.ColdRay, SpellType.DisruptingRay, SpellType.DragonSlayer,
                SpellType.BloodLust, SpellType.AnimateDead, SpellType.MirrorImage, SpellType.Shield,
            ],
            Select(spell => spell.IsSingleTarget()));

        Assert.Equal([SpellType.Dispel, SpellType.MassDispel], Select(spell => spell.IsApplyToAnyTroops()));
        Assert.Equal(
            [SpellType.Cure, SpellType.MassCure, SpellType.Dispel, SpellType.MassDispel],
            Select(spell => spell.IsEffectDispel()));

        Assert.Equal(
            [
                SpellType.Teleport, SpellType.Cure, SpellType.MassCure, SpellType.Resurrect,
                SpellType.ResurrectTrue, SpellType.Haste, SpellType.MassHaste, SpellType.Bless,
                SpellType.MassBless, SpellType.StoneSkin, SpellType.SteelSkin, SpellType.AntiMagic,
                SpellType.DragonSlayer, SpellType.BloodLust, SpellType.AnimateDead, SpellType.MirrorImage,
                SpellType.Shield, SpellType.MassShield,
            ],
            Select(spell => spell.IsApplyToFriends()));

        Assert.Equal(
            [
                SpellType.LightningBolt, SpellType.ChainLightning, SpellType.Slow, SpellType.MassSlow,
                SpellType.Blind, SpellType.Curse, SpellType.MassCurse, SpellType.Arrow, SpellType.Berserker,
                SpellType.Paralyze, SpellType.Hypnotize, SpellType.ColdRay, SpellType.DisruptingRay,
            ],
            Select(spell => spell.IsApplyToEnemies()));

        // The mass spells and the summons cover their own side, so they need no target picked out.
        Assert.All(Select(spell => spell.IsMassActions()), spell => Assert.True(new Spell(spell).IsApplyWithoutFocusObject()));
        Assert.All(Select(spell => spell.IsSummon()), spell => Assert.True(new Spell(spell).IsApplyWithoutFocusObject()));
        Assert.True(new Spell(SpellType.Armageddon).IsApplyWithoutFocusObject());
        Assert.False(new Spell(SpellType.Fireball).IsApplyWithoutFocusObject());
    }

    [Fact]
    public void TheElementalAndGuardianTraitsAreTheReferenceLists()
    {
        Assert.Equal([SpellType.Fireball, SpellType.FireBlast], Select(spell => spell.IsFire()));
        Assert.Equal([SpellType.ColdRay, SpellType.ColdRing], Select(spell => spell.IsCold()));
        Assert.Equal(
            [
                SpellType.Fireball, SpellType.FireBlast, SpellType.LightningBolt, SpellType.ChainLightning,
                SpellType.ElementalStorm, SpellType.ColdRay, SpellType.ColdRing,
            ],
            Select(spell => spell.IsElementalSpell()));

        Assert.Equal([SpellType.Petrify], Select(spell => spell.IsBuiltinOnly()));
        Assert.Equal(
            [
                SpellType.Haunt, SpellType.SetEGuardian, SpellType.SetAGuardian, SpellType.SetFGuardian,
                SpellType.SetWGuardian,
            ],
            Select(spell => spell.IsGuardianType()));
    }

    [Fact]
    public void TheArtifactsCutThePriceOfTheFourKindsOfSpell()
    {
        // Snake Ring cuts bless, Skullcap cuts the mind spells, Elemental Ring cuts the summons; nothing cuts
        // Fireball (spell.cpp:173-219).
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.SnakeRing);
        bag.Push(ArtifactType.Skullcap);
        bag.Push(ArtifactType.ElementalRing);

        Assert.Equal(1u, new Spell(SpellType.Bless).SpellPoints(bag));
        Assert.Equal(7u, new Spell(SpellType.Hypnotize).SpellPoints(bag));
        Assert.Equal(15u, new Spell(SpellType.SummonEElement).SpellPoints(bag));

        Assert.Equal(9u, new Spell(SpellType.Fireball).SpellPoints(bag));
        Assert.Equal(6u, new Spell(SpellType.ColdRay).SpellPoints(bag));

        // Without a bag the list price stands.
        Assert.Equal(3u, new Spell(SpellType.Bless).SpellPoints());
        Assert.Equal(3u, new Spell(SpellType.Bless).SpellPoints(null));
    }

    [Fact]
    public void TheCostNeverDropsBelowOnePoint()
    {
        // Bless costs three; two Snake Rings take fifty percent each, the second of which truncates to zero.
        var bag = new ArtifactBag();
        bag.Push(ArtifactType.SnakeRing);
        bag.Push(ArtifactType.SnakeRing);

        Assert.Equal(1u, new Spell(SpellType.Bless).SpellPoints(bag));
    }

    [Fact]
    public void TheGuildListIsEveryLearnableSpellOfTheLevel()
    {
        // Twelve, eleven, sixteen, sixteen and ten: sixty-five spells, the placeholders and Petrify excluded
        // because their level is zero (spell.cpp:532-560).
        Assert.Equal(12, Spell.SuitableForSpellBook(1).Count);
        Assert.Equal(11, Spell.SuitableForSpellBook(2).Count);
        Assert.Equal(16, Spell.SuitableForSpellBook(3).Count);
        Assert.Equal(16, Spell.SuitableForSpellBook(4).Count);
        Assert.Equal(10, Spell.SuitableForSpellBook(5).Count);
        Assert.Equal(65, Spell.SuitableForSpellBook().Count);

        Assert.Equal(12u, Spell.TotalSpellsPerLevel(1));
        Assert.Equal(10u, Spell.TotalSpellsPerLevel(5));
        Assert.Equal(0u, Spell.TotalSpellsPerLevel(0));
        Assert.Equal(0u, Spell.TotalSpellsPerLevel(6));
    }

    [Fact]
    public void TheGuildMayTeachTheViewSpellsButNotThePlaceholders()
    {
        // The reference keeps the two level-one view spells: they are ordinary spells a guild may hand out.
        Assert.Contains(SpellType.ViewMines, Spell.SuitableForSpellBook(1));
        Assert.Contains(SpellType.ViewResources, Spell.SuitableForSpellBook(1));

        Assert.DoesNotContain(SpellType.Petrify, Spell.SuitableForSpellBook());
        Assert.DoesNotContain(SpellType.Random, Spell.SuitableForSpellBook());
        Assert.DoesNotContain(SpellType.Random1, Spell.SuitableForSpellBook());

        // A level that no spell has, and a level below the first, both answer an empty list: the reference
        // asserts and does the same in its debug builds.
        Assert.Empty(Spell.SuitableForSpellBook(0));
        Assert.Empty(Spell.SuitableForSpellBook(6));
        Assert.Empty(Spell.SuitableForSpellBook(-2));
    }

    [Fact]
    public void TheExclusionSetLeavesTheNamedSpellsOut()
    {
        HashSet<SpellType> exclude = [SpellType.Cure, SpellType.Arrow];

        IReadOnlyList<SpellType> levelOne = Spell.SuitableForSpellBook(1, exclude);

        Assert.Equal(10, levelOne.Count);
        Assert.DoesNotContain(SpellType.Cure, levelOne);
        Assert.DoesNotContain(SpellType.Arrow, levelOne);
    }

    [Fact]
    public void ARandomSpellComesFromTheLevelAskedFor()
    {
        for (int draw = 0; draw < 40; ++draw)
        {
            Assert.Equal(1, Spell.RandomSpell(1).Level());
            Assert.Equal(5, Spell.RandomSpell(5).Level());
        }

        // A level without spells is a programming error, which the reference asserts on.
        Assert.Throws<ArgumentOutOfRangeException>(() => Spell.RandomSpell(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Spell.RandomSpell(6));
    }

    [Fact]
    public void DimensionDoorReachesFourteenTiles() => Assert.Equal(14, Spell.DimensionDoorDistance());

    /// <summary>The ids of the spells that satisfy a predicate, in table order.</summary>
    private static SpellType[] Select(Func<Spell, bool> predicate)
    {
        List<SpellType> result = [];

        for (int id = 1; id < (int)SpellType.Count; ++id)
        {
            Spell spell = new(id);

            if (predicate(spell))
            {
                result.Add(spell.Type);
            }
        }

        return [.. result];
    }
}
