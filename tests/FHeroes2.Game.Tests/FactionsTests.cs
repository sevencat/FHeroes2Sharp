using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the six rows of <c>factionProperties</c> (<c>game_static.cpp:37-89</c>).
/// </summary>
public sealed class FactionsTests
{
    public static TheoryData<MapRace> PlayableRaces =>
        new()
        {
            MapRace.Knight,
            MapRace.Barbarian,
            MapRace.Sorceress,
            MapRace.Warlock,
            MapRace.Wizard,
            MapRace.Necromancer,
        };

    [Theory]
    [MemberData(nameof(PlayableRaces))]
    public void EveryPlayableRaceHasARow(MapRace race)
    {
        FactionProperties? properties = Factions.Get(race);

        Assert.NotNull(properties);
        Assert.False(string.IsNullOrEmpty(properties.Id));
    }

    [Theory]
    [MemberData(nameof(PlayableRaces))]
    public void BothPrimarySkillPoolsAddUpToOneHundred(MapRace race)
    {
        // The weights are a distribution, not a score: every row of the table sums to 100 in each of the two
        // pools. This is what catches a row that was reshuffled or a number that was mistyped.
        FactionProperties properties = Factions.Get(race)!;

        Assert.Equal(100, Sum(properties.WeightsOfPrimarySkillsForLowLevels));
        Assert.Equal(100, Sum(properties.WeightsOfPrimarySkillsForHighLevels));
    }

    [Theory]
    [MemberData(nameof(PlayableRaces))]
    public void TheBoundaryBetweenThePoolsIsTenForEveryRace(MapRace race)
    {
        Assert.Equal(10, Factions.Get(race)!.BoundaryBetweenLowAndHighLevels);
    }

    [Theory]
    [MemberData(nameof(PlayableRaces))]
    public void TheCaptainNeverStartsStrongerThanTheHero(MapRace race)
    {
        // A captain is a weaker copy of the race's hero: the reference gives knights a 1/1/1/1 captain and a
        // 2/2/1/1 hero, and every magic race a 0/0/2/2 captain against a stronger hero. A row where the
        // captain is ahead would make a defended castle weaker than its own hero.
        FactionProperties properties = Factions.Get(race)!;

        Assert.True(
            Sum(properties.CaptainPrimarySkills) <= Sum(properties.HeroInitialPrimarySkills),
            $"{properties.Id}: captain {Sum(properties.CaptainPrimarySkills)} > hero {Sum(properties.HeroInitialPrimarySkills)}");
    }

    [Fact]
    public void KnightsStartWithBasicBallisticsAndLeadership()
    {
        FactionProperties knight = Factions.Get(MapRace.Knight)!;

        Assert.Equal("knight", knight.Id);
        Assert.Equal(new PrimarySkillValues(2, 2, 1, 1), knight.HeroInitialPrimarySkills);
        Assert.Equal(new PrimarySkillValues(1, 1, 1, 1), knight.CaptainPrimarySkills);
        Assert.Equal(SkillLevel.Basic, LevelOf(knight, SecondarySkill.Ballistics));
        Assert.Equal(SkillLevel.Basic, LevelOf(knight, SecondarySkill.Leadership));
        Assert.Equal(2, InitialSkillCount(knight));
    }

    [Fact]
    public void BarbariansStartWithAdvancedPathfinding()
    {
        FactionProperties barbarian = Factions.Get(MapRace.Barbarian)!;

        Assert.Equal(new PrimarySkillValues(3, 1, 1, 1), barbarian.HeroInitialPrimarySkills);
        Assert.Equal(SkillLevel.Advanced, LevelOf(barbarian, SecondarySkill.Pathfinding));
        Assert.Equal(1, InitialSkillCount(barbarian));
    }

    [Fact]
    public void TheThreeMagicRacesStartWithTheirOwnSkillAndBasicWisdom()
    {
        FactionProperties sorceress = Factions.Get(MapRace.Sorceress)!;
        FactionProperties warlock = Factions.Get(MapRace.Warlock)!;
        FactionProperties necromancer = Factions.Get(MapRace.Necromancer)!;

        Assert.Equal(SkillLevel.Advanced, LevelOf(sorceress, SecondarySkill.Navigation));
        Assert.Equal(SkillLevel.Basic, LevelOf(sorceress, SecondarySkill.Wisdom));

        Assert.Equal(SkillLevel.Advanced, LevelOf(warlock, SecondarySkill.Scouting));
        Assert.Equal(SkillLevel.Basic, LevelOf(warlock, SecondarySkill.Wisdom));

        Assert.Equal(SkillLevel.Basic, LevelOf(necromancer, SecondarySkill.Necromancy));
        Assert.Equal(SkillLevel.Basic, LevelOf(necromancer, SecondarySkill.Wisdom));
    }

    [Fact]
    public void WizardsStartWithAdvancedWisdomOnly()
    {
        FactionProperties wizard = Factions.Get(MapRace.Wizard)!;

        Assert.Equal(SkillLevel.Advanced, LevelOf(wizard, SecondarySkill.Wisdom));
        Assert.Equal(1, InitialSkillCount(wizard));
        Assert.Equal(new PrimarySkillValues(0, 1, 2, 2), wizard.HeroInitialPrimarySkills);
    }

    [Fact]
    public void OnlyTheMagicRacesStartWithASpell()
    {
        Assert.Equal(0, Factions.Get(MapRace.Knight)!.InitialSpellId);
        Assert.Equal(0, Factions.Get(MapRace.Barbarian)!.InitialSpellId);

        // The ids are the reference's Spell:: values, kept raw until the spell system lands.
        Assert.Equal(15, Factions.Get(MapRace.Sorceress)!.InitialSpellId);
        Assert.Equal(19, Factions.Get(MapRace.Warlock)!.InitialSpellId);
        Assert.Equal(17, Factions.Get(MapRace.Wizard)!.InitialSpellId);
        Assert.Equal(10, Factions.Get(MapRace.Necromancer)!.InitialSpellId);
    }

    [Theory]
    [InlineData(MapRace.Knight, SecondarySkill.Pathfinding, 3)]
    [InlineData(MapRace.Knight, SecondarySkill.Archery, 2)]
    [InlineData(MapRace.Knight, SecondarySkill.Logistics, 3)]
    [InlineData(MapRace.Knight, SecondarySkill.Scouting, 2)]
    [InlineData(MapRace.Knight, SecondarySkill.Diplomacy, 3)]
    [InlineData(MapRace.Knight, SecondarySkill.Navigation, 2)]
    [InlineData(MapRace.Knight, SecondarySkill.Leadership, 5)]
    [InlineData(MapRace.Knight, SecondarySkill.Wisdom, 2)]
    [InlineData(MapRace.Knight, SecondarySkill.Mysticism, 1)]
    [InlineData(MapRace.Knight, SecondarySkill.Luck, 1)]
    [InlineData(MapRace.Knight, SecondarySkill.Ballistics, 4)]
    [InlineData(MapRace.Knight, SecondarySkill.EagleEye, 1)]
    [InlineData(MapRace.Knight, SecondarySkill.Necromancy, 0)]
    [InlineData(MapRace.Knight, SecondarySkill.Estates, 3)]
    [InlineData(MapRace.Warlock, SecondarySkill.Wisdom, 5)]
    [InlineData(MapRace.Warlock, SecondarySkill.Necromancy, 1)]
    [InlineData(MapRace.Necromancer, SecondarySkill.Necromancy, 5)]
    [InlineData(MapRace.Necromancer, SecondarySkill.Leadership, 0)]
    public void TheWeightTablesAreReshuffledIntoIdOrder(MapRace race, SecondarySkill skill, int expected)
    {
        // One field at a time, by name: the reference's literal is in alphabetical field order (archery,
        // ballistics, diplomacy, ...), and a whole-row comparison would accept a row that is wrong in two
        // places at once.
        Assert.Equal(expected, Factions.Get(race)!.WeightsOfSecondarySkills[skill]);
    }

    [Fact]
    public void TheRaceWithoutPropertiesHasNoRow()
    {
        Assert.Null(Factions.Get(MapRace.None));
        Assert.Null(Factions.Get(MapRace.Random));
        Assert.Null(Factions.Get(MapRace.Multiple));
    }

    [Fact]
    public void TheReferenceIdsAreTheGameOnes()
    {
        Assert.Equal("barbarian", Factions.Get(MapRace.Barbarian)!.Id);
        Assert.Equal("sorceress", Factions.Get(MapRace.Sorceress)!.Id);
        Assert.Equal("warlock", Factions.Get(MapRace.Warlock)!.Id);
        Assert.Equal("wizard", Factions.Get(MapRace.Wizard)!.Id);
        Assert.Equal("necromancer", Factions.Get(MapRace.Necromancer)!.Id);
    }

    [Fact]
    public void SecondarySkillTable_NeedsFourteenValues()
    {
        Assert.Throws<ArgumentException>(() => SecondarySkillTable.Create(1, 2, 3));
        Assert.Throws<ArgumentException>(() => SecondarySkillTable.Create(-1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void SecondarySkillTable_RejectsTheUnknownSkill()
    {
        SecondarySkillTable table = SecondarySkillTable.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => table[SecondarySkill.Unknown]);
    }

    private static int Sum(PrimarySkillValues values) => values.Attack + values.Defense + values.Power + values.Knowledge;

    private static SkillLevel LevelOf(FactionProperties properties, SecondarySkill skill) =>
        (SkillLevel)properties.HeroInitialSecondarySkills[skill];

    private static int InitialSkillCount(FactionProperties properties) =>
        SecondarySkills.All.Count(skill => properties.HeroInitialSecondarySkills[skill] > 0);
}
