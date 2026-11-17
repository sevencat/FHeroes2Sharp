using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Spells;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The captain as a commander: what it inherits from its town — the modificators, the morale and the luck — what it
/// is worth as a spell caster, and what it says it cannot do.
/// </summary>
public sealed class CastleCaptainTests
{
    [Fact]
    public void TheCaptainIsBoundToItsTownAndItsGarrison()
    {
        CastleTown castle = new(0, MapRace.Knight);

        Assert.Same(castle, castle.Captain.InCastle);
        Assert.Same(castle.Captain, castle.Army.Commander);
        Assert.False(castle.Captain.IsHeroes);
        Assert.Equal(0u, castle.Captain.GetSecondarySkillValue(SecondarySkill.Archery));
        Assert.Equal(0, castle.Captain.BagArtifacts.CountArtifacts());
    }

    [Fact]
    public void TheCaptainsMoraleComesFromItsTown()
    {
        CastleTown knight = new(0, MapRace.Knight);
        CastleTown barbarian = new(0, MapRace.Barbarian);

        Assert.Equal(Morale.Normal, knight.Captain.GetMorale());
        Assert.Equal(Morale.Normal, knight.Army.GetMorale());

        // The tavern is worth one in every town, the barbarian coliseum two more.
        Build(knight, BuildingType.Tavern);
        Build(barbarian, BuildingType.Tavern);
        Build(barbarian, BuildingType.Spec);

        Assert.Equal(Morale.Good, knight.Captain.GetMorale());
        Assert.Equal(Morale.Good, knight.Army.GetMorale());
        Assert.Equal(Morale.Blood, barbarian.Captain.GetMorale());
        Assert.Equal(Morale.Blood, barbarian.Army.GetMorale());
    }

    [Fact]
    public void TheCaptainsLuckComesFromItsTown()
    {
        CastleTown sorceress = new(0, MapRace.Sorceress);

        Assert.Equal(Luck.Normal, sorceress.Captain.GetLuck());
        Assert.Equal(Luck.Normal, sorceress.Army.GetLuck());

        // The rainbow is the sorceress's special building and is worth two points of luck.
        Build(sorceress, BuildingType.Spec);

        Assert.Equal(Luck.Great, sorceress.Captain.GetLuck());
        Assert.Equal(Luck.Great, sorceress.Army.GetLuck());
    }

    [Fact]
    public void TheNecromancersSpellPowerIsItsSpecialBuilding()
    {
        CastleTown castle = new(0, MapRace.Necromancer);

        int bare = castle.Captain.GetPower();

        Build(castle, BuildingType.Spec);

        Assert.Equal(bare + 2, castle.Captain.GetPower());
    }

    [Fact]
    public void ACaptainWithoutSpellPointsIsWorthNothingAsACaster()
    {
        CastleTown castle = new(0, MapRace.Wizard);

        Assert.True(castle.Captain.SpellPoints > 0, "a fresh captain is fully rested");
        Assert.True(castle.Captain.GetMagicStrategicValue(1000) > 0, "the wizard's signature spell is a combat one");

        castle.Captain.SetSpellPoints(0);

        Assert.Equal(0.0, castle.Captain.GetMagicStrategicValue(1000));
    }

    [Fact]
    public void TheBooksBestCombatSpellIsWhatTheCaptainIsWorth()
    {
        CastleTown castle = new(0, MapRace.Wizard);
        castle.Captain.AppendSpellToBook(new Spell(SpellType.Fireball), ignoreWisdom: true);
        castle.Captain.SetSpellPoints(40);

        double best = 0;
        bool sawFireball = false;

        foreach (Spell spell in castle.Captain.MagicBook)
        {
            sawFireball |= spell.Type == SpellType.Fireball;

            if (spell.IsCombat())
            {
                best = Math.Max(best, spell.GetStrategicValue(1000, castle.Captain.SpellPoints, castle.Captain.GetPower()));
            }
        }

        Assert.True(sawFireball, "the fireball went into the book");
        Assert.True(best > 0, "a fireball with forty spell points is worth something");
        Assert.Equal(best, castle.Captain.GetMagicStrategicValue(1000));
    }

    [Fact]
    public void TheSpellBookGrowsOnlyUpToTheSecondCircle()
    {
        CastleTown castle = new(0, MapRace.Wizard);

        Assert.True(castle.Captain.CanLearnSpell(new Spell(SpellType.Haste)));
        Assert.False(castle.Captain.CanLearnSpell(new Spell(SpellType.TownGate)));
    }

    /// <summary>
    /// Gives a town a building without charging for it: these tests start from the shape of the town, not from the
    /// budget.
    /// </summary>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building to add.</param>
    private static void Build(CastleTown castle, BuildingType building) => castle.AddBuildings(building);
}
