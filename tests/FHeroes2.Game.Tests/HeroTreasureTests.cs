using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the treasure chest (<c>ActionToTreasureChest</c>, <c>heroes_action.cpp:1795</c>) and Xanadu
/// (<c>ActionToXanadu</c>, <c>:2577</c>) with its admission rule (<c>game_static.cpp:298</c>).
/// </summary>
public sealed class HeroTreasureTests
{
    /// <summary>A blue kingdom with a knight and a chest on tile 0.</summary>
    private static HeroActions ChestActions(
        out PlayerKingdom kingdom,
        out World world,
        out Hero hero,
        uint gold,
        ArtifactType artifact = ArtifactType.Unknown,
        bool onWater = false)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        hero = new Hero(1, MapRace.Knight);
        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);

        Tile tile = world.GetTile(0);
        tile.SetMainObjectType(Mp2ObjectType.TreasureChest);
        tile.Metadata[0] = (uint)artifact;
        tile.Metadata[1] = gold;

        // Tile 0 of the fresh map is water; a land chest needs grass under it.
        tile.SetTerrain(onWater ? (ushort)0 : MapGrounds.GrassStartImageIndex, 0);

        return new HeroActions(hero, kingdoms);
    }

    /// <summary>A blue kingdom with a knight of the given level and Xanadu on tile 0.</summary>
    private static HeroActions XanaduActions(out PlayerKingdom kingdom, out World world, out Hero hero, int level)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        hero = new Hero(1, MapRace.Knight);
        hero.SetExperience(HeroExperience.FromLevel(level - 1));
        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);
        world.GetTile(0).SetMainObjectType(Mp2ObjectType.Xanadu);

        return new HeroActions(hero, kingdoms);
    }

    private static void FillBag(Hero hero)
    {
        for (int index = 1; index <= ArtifactBag.MaxCapacity; ++index)
        {
            Assert.True(hero.BagArtifacts.Push((ArtifactType)index));
        }

        Assert.True(hero.BagArtifacts.IsFull);
    }

    [Fact]
    public void TheChestPaysGoldByDefault()
    {
        HeroActions actions = ChestActions(out PlayerKingdom kingdom, out World world, out _, 1000);

        int before = kingdom.Treasury.Gold;
        TreasureChestReward reward = actions.ActionToTreasureChest(0, world);

        Assert.Equal(1000u, reward.Gold);
        Assert.Equal(0u, reward.Experience);
        Assert.Equal(before + 1000, kingdom.Treasury.Gold);
    }

    [Fact]
    public void TheChestCanPayExperienceInstead()
    {
        HeroActions actions = ChestActions(out PlayerKingdom kingdom, out World world, out Hero hero, 1000);

        int before = kingdom.Treasury.Gold;
        TreasureChestReward reward = actions.ActionToTreasureChest(0, world, (_, _) => false);

        Assert.Equal(0u, reward.Gold);
        Assert.Equal(500u, reward.Experience);
        Assert.Equal(before, kingdom.Treasury.Gold);
        Assert.Equal(500u, hero.Experience);
    }

    [Fact]
    public void ASmallChestStillPaysFiveHundredExperience()
    {
        HeroActions actions = ChestActions(out _, out World world, out Hero hero, 300);

        TreasureChestReward reward = actions.ActionToTreasureChest(0, world, (_, _) => false);

        Assert.Equal(500u, reward.Experience);
        Assert.Equal(500u, hero.Experience);
    }

    [Fact]
    public void TheChestClearsTheTile()
    {
        HeroActions actions = ChestActions(out _, out World world, out _, 1000);
        Tile tile = world.GetTile(0);

        actions.ActionToTreasureChest(0, world);

        Assert.Equal(0u, tile.Metadata[0]);
        Assert.Equal(0u, tile.Metadata[1]);
    }

    [Fact]
    public void AFilledBagTurnsTheArtifactIntoGold()
    {
        HeroActions actions = ChestActions(
            out _,
            out World world,
            out Hero hero,
            800,
            ArtifactType.UltimateSword);

        FillBag(hero);

        // The artifact's 1000 gold replaces the chest's own 800 rather than adding to it — the reference overwrites.
        TreasureChestReward reward = actions.ActionToTreasureChest(0, world);

        Assert.Equal(1000u, reward.Gold);
        Assert.Equal(ArtifactType.Unknown, reward.Artifact);
    }

    [Fact]
    public void AnEmptyChestPaysNothing()
    {
        HeroActions actions = ChestActions(out PlayerKingdom kingdom, out World world, out Hero hero, 0);

        int before = kingdom.Treasury.Gold;
        TreasureChestReward reward = actions.ActionToTreasureChest(0, world);

        Assert.True(reward.IsEmpty);
        Assert.Equal(before, kingdom.Treasury.Gold);
        Assert.Equal(0u, hero.Experience);
    }

    [Fact]
    public void ALandChestCanHoldAnArtifactInsteadOfGold()
    {
        HeroActions actions = ChestActions(out _, out World world, out Hero hero, 0, ArtifactType.UltimateSword);

        TreasureChestReward reward = actions.ActionToTreasureChest(0, world);

        Assert.Equal(0u, reward.Gold);
        Assert.Equal(ArtifactType.UltimateSword, reward.Artifact);
        Assert.True(hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
    }

    [Fact]
    public void AWaterChestPaysBothGoldAndTheArtifact()
    {
        HeroActions actions = ChestActions(
            out PlayerKingdom kingdom,
            out World world,
            out Hero hero,
            500,
            ArtifactType.UltimateSword,
            onWater: true);

        int before = kingdom.Treasury.Gold;
        TreasureChestReward reward = actions.ActionToTreasureChest(0, world);

        Assert.Equal(500u, reward.Gold);
        Assert.Equal(0u, reward.Experience);
        Assert.Equal(ArtifactType.UltimateSword, reward.Artifact);
        Assert.Equal(before + 500, kingdom.Treasury.Gold);
        Assert.True(hero.BagArtifacts.IsPresent(ArtifactType.UltimateSword));
    }

    [Fact]
    public void AWaterChestWithAFilledBagPaysGoldForTheArtifact()
    {
        HeroActions actions = ChestActions(
            out _,
            out World world,
            out Hero hero,
            500,
            ArtifactType.UltimateSword,
            onWater: true);

        FillBag(hero);

        TreasureChestReward reward = actions.ActionToTreasureChest(0, world);

        Assert.Equal(1000u, reward.Gold);
        Assert.Equal(0u, reward.Experience);
        Assert.Equal(ArtifactType.Unknown, reward.Artifact);
    }

    [Fact]
    public void XanaduTrainsTheWorthyHero()
    {
        HeroActions actions = XanaduActions(out _, out _, out Hero hero, 10);

        int attack = hero.BaseAttack;
        int defense = hero.BaseDefense;
        int power = hero.BasePower;
        int knowledge = hero.BaseKnowledge;

        Assert.Equal(XanaduOutcome.Trained, actions.ActionToXanadu(0));
        Assert.Equal(attack + 1, hero.BaseAttack);
        Assert.Equal(defense + 1, hero.BaseDefense);
        Assert.Equal(power + 1, hero.BasePower);
        Assert.Equal(knowledge + 1, hero.BaseKnowledge);
    }

    [Fact]
    public void XanaduTurnsAwayTheUnworthy()
    {
        HeroActions actions = XanaduActions(out _, out World world, out Hero hero, 1);

        Assert.Equal(XanaduOutcome.NotWorthy, actions.ActionToXanadu(0));
        Assert.False(hero.IsVisited(world.GetTile(0)));
    }

    [Fact]
    public void XanaduDoesNotTeachTwice()
    {
        HeroActions actions = XanaduActions(out _, out _, out _, 10);

        Assert.Equal(XanaduOutcome.Trained, actions.ActionToXanadu(0));
        Assert.Equal(XanaduOutcome.AlreadyVisited, actions.ActionToXanadu(0));
    }

    [Fact]
    public void DiplomacyLowersTheLevelRequirement()
    {
        HeroActions actions = XanaduActions(out _, out _, out Hero hero, 7);

        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Diplomacy, SkillLevel.Basic));

        Assert.Equal(XanaduOutcome.NotWorthy, actions.ActionToXanadu(0));

        hero.SetExperience(HeroExperience.FromLevel(7));
        Assert.Equal(XanaduOutcome.Trained, actions.ActionToXanadu(0));
    }
}
