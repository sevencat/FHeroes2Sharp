using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="HeroActionQuestionText"/>, the wording of the yes/no questions a hero action asks
/// (<c>heroes_action.cpp</c>, <c>dialog_armyinfo.cpp</c>).
/// </summary>
/// <remarks>
/// The texts are plain strings, so these tests need no game data: they pin the sentences the reference builds and
/// the rule that the monster name goes in lowercase, <c>StringReplaceWithLowercase</c>.
/// </remarks>
public sealed class HeroActionQuestionTextTests
{
    [Fact]
    public void FreeJoinAsksWithTheMultiNameInLowercase()
    {
        (string header, string body) =
            HeroActionQuestionText.Describe(HeroActionQuestion.JoinForFree, new Troop(MonsterType.Ghost, 19));

        Assert.Equal("Followers", header);
        Assert.Equal("A group of ghosts with a desire for greater glory wish to join you.\nDo you accept?", body);
    }

    [Fact]
    public void FreeJoinUsesTheMultiNameEvenForOneCreature()
    {
        (_, string body) =
            HeroActionQuestionText.Describe(HeroActionQuestion.JoinForFree, new Troop(MonsterType.Ghost, 1));

        Assert.Equal("A group of ghosts with a desire for greater glory wish to join you.\nDo you accept?", body);
    }

    [Fact]
    public void PaidJoinOfASingleCreatureQuotesTheSum()
    {
        (string header, string body) =
            HeroActionQuestionText.Describe(HeroActionQuestion.JoinForMoney, new Troop(MonsterType.Ghost, 1), 500);

        Assert.Equal(string.Empty, header);
        Assert.Equal(
            "The ghost is swayed by your diplomatic tongue, and offers to join your army for the sum of 500 gold.\nDo you accept?",
            body);
    }

    [Fact]
    public void PaidJoinOfManyCreaturesOffersTheWholeStack()
    {
        (_, string body) =
            HeroActionQuestionText.Describe(HeroActionQuestion.JoinForMoney, new Troop(MonsterType.Ghost, 19), 9500);

        Assert.Contains("All 19 of the ghosts will join your army for the sum of 9500 gold.", body, StringComparison.Ordinal);
        Assert.EndsWith("Do you accept?", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FleeingMonstersAreAskedAboutTheChase()
    {
        (string header, string body) =
            HeroActionQuestionText.Describe(HeroActionQuestion.PursueFleeingMonsters, new Troop(MonsterType.Ghost, 19));

        Assert.Equal(string.Empty, header);
        Assert.Equal(
            "The ghosts, awed by the power of your forces, begin to scatter.\nDo you wish to pursue and engage them?",
            body);
    }

    [Theory]
    [InlineData(HeroActionQuestion.SearchPyramid, "Pyramid")]
    [InlineData(HeroActionQuestion.EnterAbandonedMine, "Abandoned Mine")]
    [InlineData(HeroActionQuestion.EnterDaemonCave, "Daemon Cave")]
    [InlineData(HeroActionQuestion.FightDaemonServants, "Daemon Cave")]
    [InlineData(HeroActionQuestion.RubGenieLamp, "Genie's Lamp")]
    [InlineData(HeroActionQuestion.PayAlchemist, "Alchemist's Tower")]
    [InlineData(HeroActionQuestion.AcceptSphinxChallenge, "Sphinx")]
    [InlineData(HeroActionQuestion.SearchPoorMoraleObject, "")]
    public void TheFixedSitesCarryTheirObjectNameAsTitle(HeroActionQuestion question, string title)
    {
        (string header, string body) = HeroActionQuestionText.Describe(question);

        Assert.Equal(title, header);
        Assert.NotEmpty(body);
    }

    [Fact]
    public void TheRiddleWindowQuotesTheRiddleBetweenQuotes()
    {
        string body = HeroActionQuestionText.SphinxRiddle("What has roots as nobody sees?");

        Assert.Equal(
            "The Sphinx asks you the following riddle:\n\n'What has roots as nobody sees?'\n\nYour answer?",
            body);
        Assert.Equal("Sphinx", HeroActionQuestionText.SphinxTitle);
    }

    [Fact]
    public void TheRiddleWindowRefusesANullRiddle()
    {
        Assert.Throws<ArgumentNullException>(() => HeroActionQuestionText.SphinxRiddle(null!));
    }

    [Fact]
    public void TheRansomQuotesTheSum()
    {
        (_, string body) = HeroActionQuestionText.Describe(HeroActionQuestion.PayDaemonRansom, 2000);

        Assert.EndsWith("\"I will sell it back to you for 2000 gold.\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAlchemistQuotesTheSum()
    {
        (_, string body) = HeroActionQuestionText.Describe(HeroActionQuestion.PayAlchemist, 750);

        Assert.Contains("For 750 gold, the alchemist will remove it for you. Do you pay?", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HeroActionQuestion.JoinForFree)]
    [InlineData(HeroActionQuestion.JoinForMoney)]
    public void RefusingAnOfferOfTheMonstersIsAnsweredWithTheirAttack(HeroActionQuestion question) =>
        Assert.Equal("Insulted by your refusal of their offer, the monsters attack!", HeroActionQuestionText.Refusal(question));

    [Theory]
    [InlineData(HeroActionQuestion.SearchPyramid)]
    [InlineData(HeroActionQuestion.RubGenieLamp)]
    [InlineData(HeroActionQuestion.JoinDwellingMonsters)]
    public void OtherQuestionsSayNothingAfterARefusal(HeroActionQuestion question) =>
        Assert.Null(HeroActionQuestionText.Refusal(question));

    [Fact]
    public void TheStackOverloadRefusesAQuestionWithoutAStack() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HeroActionQuestionText.Describe(HeroActionQuestion.SearchPyramid, new Troop(MonsterType.Ghost, 1)));

    [Fact]
    public void ThePlainOverloadRefusesAQuestionWithAStack() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HeroActionQuestionText.Describe(HeroActionQuestion.JoinForFree));
}
