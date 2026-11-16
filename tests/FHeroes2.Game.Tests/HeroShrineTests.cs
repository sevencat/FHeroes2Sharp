using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two knowledge objects of <c>heroes_action.cpp</c>: the shrines (<c>ActionToShrine</c>, <c>:995</c>)
/// and the witch's hut (<c>ActionToWitchsHut</c>, <c>:1058</c>).
/// </summary>
public sealed class HeroShrineTests
{
    /// <summary>A blue kingdom with a knight and one object on tile 0, its first lane set.</summary>
    private static HeroActions ActionsWithObject(
        out PlayerKingdom kingdom,
        out World world,
        Mp2ObjectType objectType,
        uint lane0,
        bool withSpellBook = true)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        Hero hero = new(1, MapRace.Knight);

        if (withSpellBook)
        {
            Assert.True(hero.BagArtifacts.Push(ArtifactType.MagicBook));
        }

        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);

        Tile tile = world.GetTile(0);
        tile.SetMainObjectType(objectType);
        tile.Metadata[0] = lane0;

        return new HeroActions(hero, kingdoms);
    }

    [Fact]
    public void AShrineWithoutASpellBookTeachesNothing()
    {
        HeroActions actions = ActionsWithObject(
            out PlayerKingdom kingdom,
            out World world,
            Mp2ObjectType.ShrineFirstCircle,
            (uint)SpellType.Bless,
            withSpellBook: false);

        Assert.Equal(ShrineOutcome.NoSpellBook, actions.ActionToShrine(0, world));
        Assert.Equal(0, actions.Hero.MagicBook.Count);

        // The trip is on the kingdom's books even so: the reference sets the flag outside every branch.
        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.ShrineFirstCircle));
    }

    [Fact]
    public void AShrineWritesTheSpellIntoTheBook()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.ShrineFirstCircle,
            (uint)SpellType.Bless);

        Assert.Equal(ShrineOutcome.Learned, actions.ActionToShrine(0, world));
        Assert.True(actions.Hero.MagicBook.IsPresent(new Spell(SpellType.Bless)));
        Assert.Equal(1, actions.Hero.MagicBook.Count);
    }

    [Fact]
    public void AThirdCircleSpellNeedsWisdom()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.ShrineThirdCircle,
            (uint)SpellType.Fireball);

        Assert.Equal(ShrineOutcome.TooHard, actions.ActionToShrine(0, world));
        Assert.Equal(0, actions.Hero.MagicBook.Count);
    }

    [Fact]
    public void AThirdCircleSpellGoesInWhenTheHeroHasWisdom()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.ShrineThirdCircle,
            (uint)SpellType.Fireball);

        actions.Hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Wisdom, SkillLevel.Basic));

        Assert.Equal(ShrineOutcome.Learned, actions.ActionToShrine(0, world));
        Assert.True(actions.Hero.MagicBook.IsPresent(new Spell(SpellType.Fireball)));
    }

    [Fact]
    public void AShrineDoesNotTeachTheSameSpellTwice()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.ShrineSecondCircle,
            (uint)SpellType.LightningBolt);

        Assert.Equal(ShrineOutcome.Learned, actions.ActionToShrine(0, world));
        Assert.Equal(ShrineOutcome.AlreadyKnown, actions.ActionToShrine(0, world));
        Assert.Equal(1, actions.Hero.MagicBook.Count);
    }

    [Fact]
    public void TheShrineRegistersWithTheKingdomButNotWithTheHero()
    {
        HeroActions actions = ActionsWithObject(
            out PlayerKingdom kingdom,
            out World world,
            Mp2ObjectType.ShrineFirstCircle,
            (uint)SpellType.Curse);

        actions.ActionToShrine(0, world);

        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.ShrineFirstCircle));
        Assert.False(actions.Hero.IsVisited(world.GetTile(0)));
    }

    [Fact]
    public void AWitchsHutTeachesTheBasicSkill()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.WitchsHut,
            (uint)SecondarySkill.Pathfinding);

        Assert.Equal(WitchsHutOutcome.Learned, actions.ActionToWitchsHut(0, world));
        Assert.Equal(SkillLevel.Basic, actions.Hero.GetLevelSkill(SecondarySkill.Pathfinding));
        Assert.True(actions.Hero.HasSecondarySkill(SecondarySkill.Pathfinding));
    }

    [Fact]
    public void AWitchsHutDoesNotRepeatASkillTheHeroHas()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.WitchsHut,
            (uint)SecondarySkill.Scouting);

        Assert.Equal(WitchsHutOutcome.Learned, actions.ActionToWitchsHut(0, world));
        Assert.Equal(WitchsHutOutcome.AlreadyKnown, actions.ActionToWitchsHut(0, world));
        Assert.Equal(1, actions.Hero.SecondarySkills.Count);
    }

    [Fact]
    public void AWitchsHutTurnsAwayAFullHero()
    {
        HeroActions actions = ActionsWithObject(
            out _,
            out World world,
            Mp2ObjectType.WitchsHut,
            (uint)SecondarySkill.Estates);

        for (int index = 0; index < SecSkills.MaxCount; ++index)
        {
            actions.Hero.LearnSkill(new SecondarySkillLevel(SecondarySkills.All[index], SkillLevel.Basic));
        }

        Assert.True(actions.Hero.HasMaxSecondarySkill());
        Assert.Equal(WitchsHutOutcome.NoRoom, actions.ActionToWitchsHut(0, world));
        Assert.False(actions.Hero.HasSecondarySkill(SecondarySkill.Estates));
    }

    [Fact]
    public void TheWitchsHutRegistersTheTripWithTheKingdom()
    {
        HeroActions actions = ActionsWithObject(
            out PlayerKingdom kingdom,
            out World world,
            Mp2ObjectType.WitchsHut,
            (uint)SecondarySkill.Logistics);

        actions.ActionToWitchsHut(0, world);

        Assert.True(kingdom.IsVisited(0, Mp2ObjectType.WitchsHut));
        Assert.False(actions.Hero.IsVisited(world.GetTile(0)));
    }
}
