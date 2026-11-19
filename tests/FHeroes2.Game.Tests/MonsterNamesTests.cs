using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="MonsterNames"/>: the text half of the creature table, <c>monster/monster_info.cpp</c> and
/// <c>Monster::GetMultiName()</c>/<c>getRandomRaceMonstersName()</c> in <c>monster.cpp</c>.
/// </summary>
public sealed class MonsterNamesTests
{
    [Fact]
    public void TheSingularAndThePluralAreTheReferences()
    {
        Assert.Equal("Peasant", MonsterNames.GetSingular(MonsterType.Peasant));
        Assert.Equal("Peasants", MonsterNames.GetMulti(MonsterType.Peasant));
        Assert.Equal("Unknown Monsters", MonsterNames.GetMulti(MonsterType.Unknown));
    }

    [Fact]
    public void AnIrregularPluralIsNotGuessed()
    {
        // "Magi" and "Archmagi", not "Mages".
        Assert.Equal("Magi", MonsterNames.GetMulti(MonsterType.Mage));
        Assert.Equal("Archmagi", MonsterNames.GetMulti(MonsterType.Archmage));
        Assert.Equal("Cavalries", MonsterNames.GetMulti(MonsterType.Cavalry));
        Assert.Equal("Wolves", MonsterNames.GetMulti(MonsterType.Wolf));
        Assert.Equal("Dwarves", MonsterNames.GetMulti(MonsterType.Dwarf));
    }

    [Fact]
    public void EveryCreatureHasAName()
    {
        for (int id = 0; id < (int)MonsterType.MonsterCount; ++id)
        {
            var monster = (MonsterType)id;

            Assert.False(string.IsNullOrEmpty(MonsterNames.GetSingular(monster)), monster.ToString());
            Assert.False(string.IsNullOrEmpty(MonsterNames.GetMulti(monster)), monster.ToString());
        }
    }

    [Fact]
    public void ACountOfOneIsSingular()
    {
        // Monster::GetPluralName( count ) is literally `count == 1 ? GetName() : GetMultiName()`.
        Assert.Equal("Peasant", MonsterNames.GetPluralName(MonsterType.Peasant, 1));
        Assert.Equal("Peasants", MonsterNames.GetPluralName(MonsterType.Peasant, 0));
        Assert.Equal("Peasants", MonsterNames.GetPluralName(MonsterType.Peasant, 2));
        Assert.Equal("Peasants", MonsterNames.GetPluralName(MonsterType.Peasant, 999));
    }

    [Fact]
    public void ARandomTownsDwellingNamesTheLevel()
    {
        Assert.Equal("level 1 creatures", MonsterNames.GetRandomRaceMonstersName(BuildingType.DwellingMonster1));
        Assert.Equal("level 2 creatures", MonsterNames.GetRandomRaceMonstersName(BuildingType.DwellingUpgrade2));
        Assert.Equal(
            "level 6 creatures",
            MonsterNames.GetRandomRaceMonstersName(BuildingType.DwellingUpgrade7));
    }
}
