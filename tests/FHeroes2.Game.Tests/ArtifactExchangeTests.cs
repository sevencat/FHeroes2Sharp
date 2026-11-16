using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>BagArtifacts::exchangeArtifacts()</c> (<c>artifact.cpp:891-1038</c>): the pool of both bags, the three
/// give-away rules and the deal by value that follows them.
/// </summary>
public class ArtifactExchangeTests
{
    private static Hero NewHero() => new(1, MapRace.Knight);

    [Fact]
    public void ArtifactsWithNothingButCursesStayWithTheirHero()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();

        giver.BagArtifacts.Push(ArtifactType.HideousMask);
        giver.BagArtifacts.Push(ArtifactType.TaxLien);

        ArtifactExchange.Exchange(taker, giver);

        // The taker would rather not carry the mask or the lien, so both are given back before anything else.
        Assert.Equal(0, taker.BagArtifacts.CountArtifacts());
        Assert.Equal(2, giver.BagArtifacts.CountArtifacts());
        Assert.True(giver.BagArtifacts.IsPresent(ArtifactType.HideousMask));
    }

    [Fact]
    public void ANecromancerKeepsTheNecromancyArtifacts()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();
        giver.LearnSkill(new SecondarySkillLevel(SecondarySkill.Necromancy, SkillLevel.Basic));

        giver.BagArtifacts.Push(ArtifactType.SpadeNecromancy);
        giver.BagArtifacts.Push(ArtifactType.GoldenBow);

        ArtifactExchange.Exchange(taker, giver);

        // The spade only grants the skill the giver already has, so it goes back; the bow has a use for anyone.
        Assert.True(giver.BagArtifacts.IsPresent(ArtifactType.SpadeNecromancy));
        Assert.True(taker.BagArtifacts.IsPresent(ArtifactType.GoldenBow));
    }

    [Fact]
    public void TheMagicBookNeverMoves()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();
        giver.BagArtifacts.Push(ArtifactType.MagicBook);
        giver.BagArtifacts.Push(ArtifactType.UltimateSword);

        ArtifactExchange.Exchange(taker, giver);

        Assert.True(giver.HasSpellBook);
        Assert.False(taker.HasSpellBook);
        Assert.True(taker.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
    }

    [Fact]
    public void TheTakerTakesTheBestThirteen()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();

        // Fourteen artifacts, thirteen of them books: the taker has room for thirteen and keeps the best.
        for (int i = 0; i < 13; ++i)
        {
            giver.BagArtifacts.Push(ArtifactType.UltimateBook);
        }

        giver.BagArtifacts.Push(ArtifactType.EndlessSackGold);

        ArtifactExchange.Exchange(taker, giver);

        // One slot has to stay free for a spell book the taker does not have yet.
        Assert.Equal(ArtifactBag.MaxCapacity - 1, taker.BagArtifacts.CountArtifacts());
        Assert.Equal(13, taker.BagArtifacts.Count(ArtifactType.UltimateBook));
        Assert.False(taker.BagArtifacts.IsPresent(ArtifactType.EndlessSackGold));

        // The worst artifact of the pool went back to the giver.
        Assert.Equal(1, giver.BagArtifacts.CountArtifacts());
        Assert.True(giver.BagArtifacts.IsPresent(ArtifactType.EndlessSackGold));
    }

    [Fact]
    public void ThePartsOfASetAssembleInTheBagThatEndsUpWithThem()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();

        giver.BagArtifacts.Push(ArtifactType.HelmetAnduran);
        giver.BagArtifacts.Push(ArtifactType.SwordAnduran);
        giver.BagArtifacts.Push(ArtifactType.BreastplateAnduran);

        ArtifactExchange.Exchange(taker, giver);

        // The three parts travel together, so the taker ends up with the battle garb rather than the parts.
        Assert.True(taker.BagArtifacts.IsPresent(ArtifactType.BattleGarb));
        Assert.False(taker.BagArtifacts.IsPresent(ArtifactType.HelmetAnduran));
        Assert.False(taker.BagArtifacts.IsPresent(ArtifactType.SwordAnduran));
        Assert.False(taker.BagArtifacts.IsPresent(ArtifactType.BreastplateAnduran));
        Assert.Equal(1, taker.BagArtifacts.CountArtifacts());
        Assert.Equal(0, giver.BagArtifacts.CountArtifacts());
    }

    [Fact]
    public void AnEmptyGiverLeavesTheTakerAlone()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();
        taker.BagArtifacts.Push(ArtifactType.UltimateCrown);

        ArtifactExchange.Exchange(taker, giver);

        Assert.True(taker.BagArtifacts.IsPresent(ArtifactType.UltimateCrown));
        Assert.Equal(1, taker.BagArtifacts.CountArtifacts());
        Assert.Equal(0, giver.BagArtifacts.CountArtifacts());
    }

    [Fact]
    public void NothingIsLostWhenBothBagsAreFull()
    {
        Hero taker = NewHero();
        Hero giver = NewHero();

        // Twenty-eight artifacts between the two heroes: the pool cannot fit into one bag, so the deal has to put
        // everything back somewhere.
        for (int i = 0; i < ArtifactBag.MaxCapacity; ++i)
        {
            taker.BagArtifacts.Push(ArtifactType.UltimateBook);
            giver.BagArtifacts.Push(ArtifactType.EndlessSackGold);
        }

        ArtifactExchange.Exchange(taker, giver);

        Assert.Equal(ArtifactBag.MaxCapacity, taker.BagArtifacts.CountArtifacts());
        Assert.Equal(ArtifactBag.MaxCapacity, giver.BagArtifacts.CountArtifacts());
    }
}
