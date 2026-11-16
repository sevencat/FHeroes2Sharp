using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the hero's own visit book and its three ticks: <c>heroes.cpp</c>'s <c>visit_object</c>,
/// <c>ActionNewDay</c>, <c>ActionNewWeek</c> and <c>ActionAfterBattle</c>.
/// </summary>
public sealed class HeroVisitTests
{
    private static Hero HeroInAKingdom(out PlayerKingdom kingdom, out Kingdoms kingdoms, int id = 1)
    {
        kingdoms = new Kingdoms();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        Hero hero = new(id, MapRace.Knight);
        kingdom.AddHero(hero);

        return hero;
    }

    [Fact]
    public void TheHeroKeepsHisOwnVisitBook()
    {
        Hero hero = HeroInAKingdom(out _, out _);

        hero.SetVisited(10, Mp2ObjectType.Windmill);
        hero.SetVisited(20, Mp2ObjectType.Fountain);
        hero.SetVisited(10, Mp2ObjectType.Windmill);

        // A second visit is not recorded twice — unlike the kingdom's book, it does not even move to the front.
        Assert.Equal(2, hero.VisitedObjects.Count);
        Assert.Equal(new IndexObject(20, Mp2ObjectType.Fountain), hero.VisitedObjects[0]);
        Assert.Equal(new IndexObject(10, Mp2ObjectType.Windmill), hero.VisitedObjects[1]);

        Assert.True(hero.IsVisited(20, Mp2ObjectType.Fountain));
        Assert.False(hero.IsVisited(20, Mp2ObjectType.Windmill));
        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Fountain));
        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.Idol));
    }

    [Fact]
    public void TheDistinctTypesAreWhatLuckAndMoraleAreCountedBy()
    {
        Hero hero = HeroInAKingdom(out _, out _);

        // Three temples: the visit list has three records, but only one distinct type.
        hero.SetVisited(1, Mp2ObjectType.Temple);
        hero.SetVisited(2, Mp2ObjectType.Temple);
        hero.SetVisited(3, Mp2ObjectType.Temple);
        hero.SetVisited(4, Mp2ObjectType.Fountain);

        IReadOnlyList<Mp2ObjectType> types = hero.GetAllVisitedObjectTypes();

        Assert.Equal(2, types.Count);
        Assert.Contains(Mp2ObjectType.Temple, types);
        Assert.Contains(Mp2ObjectType.Fountain, types);
    }

    [Fact]
    public void AnEmptyObjectIsNotAVisit()
    {
        Hero hero = HeroInAKingdom(out _, out _);

        hero.SetVisited(7, Mp2ObjectType.None);

        Assert.Empty(hero.VisitedObjects);
    }

    [Fact]
    public void TheDayRefillsTheManaPoolFromTheSecondDayOn()
    {
        Hero hero = HeroInAKingdom(out _, out _);
        uint max = hero.GetMaxSpellPoints();
        Assert.True(max > 0);

        hero.SetSpellPoints(0);

        // heroes.cpp:640: on day one the pool stays as the map handed it over.
        hero.ActionNewDay(1);
        Assert.Equal(0u, hero.SpellPoints);

        hero.ActionNewDay(2);
        Assert.Equal(1u, hero.SpellPoints);

        hero.ActionNewDay(3);
        Assert.Equal(2u, hero.SpellPoints);
    }

    [Fact]
    public void TheManaPoolNeverGrowsPastItsMaximum()
    {
        Hero hero = HeroInAKingdom(out _, out _);
        hero.SetSpellPoints(hero.GetMaxSpellPoints() - 1);

        hero.ActionNewDay(2);
        hero.ActionNewDay(3);

        Assert.Equal(hero.GetMaxSpellPoints(), hero.SpellPoints);
    }

    [Fact]
    public void TheMysticismSkillSpeedsUpTheDailyRestore()
    {
        Hero hero = HeroInAKingdom(out _, out _);
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Mysticism, SkillLevel.Expert));
        hero.SetSpellPoints(0);

        hero.ActionNewDay(2);

        // One point from the day itself plus three from expert mysticism.
        Assert.Equal(4u, hero.SpellPoints);
    }

    [Fact]
    public void TheDayForgetsTheMagicWellAndNothingElse()
    {
        Hero hero = HeroInAKingdom(out _, out _);
        hero.SetVisited(1, Mp2ObjectType.MagicWell);
        hero.SetVisited(2, Mp2ObjectType.Windmill);
        hero.SetVisited(3, Mp2ObjectType.Fountain);

        hero.ActionNewDay(2);

        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.MagicWell));
        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Windmill));
        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Fountain));
    }

    [Fact]
    public void TheWeekForgetsTheWindmillAndNothingElse()
    {
        Hero hero = HeroInAKingdom(out _, out _);
        hero.SetVisited(1, Mp2ObjectType.MagicWell);
        hero.SetVisited(2, Mp2ObjectType.Windmill);
        hero.SetVisited(3, Mp2ObjectType.Fountain);

        hero.ActionNewWeek();

        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.MagicWell));
        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.Windmill));
        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Fountain));
    }

    [Fact]
    public void TheBattleForgetsWhatOnlyLastedUntilTheNextFight()
    {
        Hero hero = HeroInAKingdom(out _, out _);
        hero.SetVisited(1, Mp2ObjectType.Fountain);
        hero.SetVisited(2, Mp2ObjectType.Windmill);
        Assert.Equal(1, hero.GetLuck());

        hero.ActionAfterBattle();

        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.Fountain));
        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Windmill));
        Assert.Equal(0, hero.GetLuck());
        Assert.True(hero.Modes.HasFlag(HeroModes.Action));
    }

    [Fact]
    public void TheKingdomTicksItsHeroesWithItsOwnDayAndWeek()
    {
        Hero hero = HeroInAKingdom(out PlayerKingdom kingdom, out _, id: 3);
        hero.SetVisited(1, Mp2ObjectType.MagicWell);
        hero.SetVisited(2, Mp2ObjectType.Windmill);

        kingdom.ActionNewDay(2);

        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.MagicWell));
        Assert.True(hero.IsObjectTypeVisited(Mp2ObjectType.Windmill));

        kingdom.ActionNewWeek(2);

        Assert.False(hero.IsObjectTypeVisited(Mp2ObjectType.Windmill));
    }
}
