using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the writing half of the tile metadata and the object queries that need no removal
/// (<c>maps_tiles_helper.cpp:339-706</c>, <c>:786-1014</c>, <c>:2063-2094</c>).
/// </summary>
public sealed class TileMetadataMutatorsTests
{
    [Fact]
    public void AMineKeepsItsGuardingSpellInTheFirstLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.Mine, out World world);

        tile.SetMineSpell(world, SpellType.Fireball);

        Assert.Equal((uint)SpellType.Fireball, tile.Metadata[0]);
        Assert.Equal(SpellType.Fireball, tile.GetMineSpellId(world));
    }

    [Fact]
    public void RemovingTheMineSpellLeavesTheLaneEmptyButKeepsTheRest()
    {
        Tile tile = MakeTile(Mp2ObjectType.Mine, out World world);
        tile.Metadata[0] = (uint)SpellType.Fireball;
        tile.Metadata[1] = 3;
        tile.Metadata[2] = 7;

        tile.RemoveMineSpell(world);

        Assert.Equal(0u, tile.Metadata[0]);
        Assert.Equal(3u, tile.Metadata[1]);
        Assert.Equal(7u, tile.Metadata[2]);
    }

    [Fact]
    public void AShrineKeepsItsSpellInTheFirstLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.ShrineSecondCircle, out World world);

        tile.SetSpellOnTile(world, SpellType.Haste);

        Assert.Equal((uint)SpellType.Haste, tile.Metadata[0]);
    }

    [Fact]
    public void ASpellScrollKeepsItsSpellInTheSecondLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[2] = (uint)ArtifactCaptureCondition.ContainsSpell;

        tile.SetSpellOnTile(world, SpellType.Bless);

        Assert.Equal((uint)SpellType.Bless, tile.Metadata[1]);
        Assert.Equal(0u, tile.Metadata[0]);
    }

    [Theory]
    [InlineData(MonsterJoinConditions.Skip)]
    [InlineData(MonsterJoinConditions.Money)]
    [InlineData(MonsterJoinConditions.Free)]
    public void AJoinConditionRoundTripsThroughTheThirdLane(int condition)
    {
        Tile tile = MakeTile(Mp2ObjectType.Monster, out World world);

        tile.SetMonsterJoinCondition(world, condition);

        Assert.Equal((uint)condition, tile.Metadata[2]);
        Assert.Equal(condition == MonsterJoinConditions.Skip, tile.IsMonsterJoinConditionSkip(world));
        Assert.Equal(condition == MonsterJoinConditions.Free, tile.IsMonsterJoinConditionFree(world));
    }

    [Fact]
    public void AnUntouchedStackIsNeitherSkippingNorFree()
    {
        Tile tile = MakeTile(Mp2ObjectType.Monster, out World world);

        // The reference writes -1 into a uint32_t word, so the round trip has to be unchecked.
        tile.Metadata[2] = unchecked((uint)MonsterJoinConditions.Unset);

        Assert.False(tile.IsMonsterJoinConditionSkip(world));
        Assert.False(tile.IsMonsterJoinConditionFree(world));
    }

    [Theory]
    [InlineData(60, 1)]
    [InlineData(66, 2)]
    [InlineData(72, 3)]
    [InlineData(78, 4)]
    [InlineData(84, 5)]
    [InlineData(90, 6)]
    [InlineData(96, 7)]
    [InlineData(102, 8)]
    public void ABarrierSpriteCarriesTheColourIndex(byte icnIndex, int expected)
    {
        Assert.Equal(expected, Tile.GetColorFromBarrierSprite(Mp2ObjectIcnType.XLoc3, icnIndex));
    }

    [Theory]
    [InlineData(110, 1)]
    [InlineData(114, 2)]
    [InlineData(118, 3)]
    [InlineData(122, 4)]
    [InlineData(126, 5)]
    [InlineData(130, 6)]
    [InlineData(134, 7)]
    [InlineData(138, 8)]
    public void ATravellerTentSpriteCarriesTheColourIndex(byte icnIndex, int expected)
    {
        Assert.Equal(expected, Tile.GetColorFromTravellerTentSprite(Mp2ObjectIcnType.XLoc3, icnIndex));
    }

    [Theory]
    [InlineData(ArtifactCaptureCondition.HaveWisdomSkill, SecondarySkill.Wisdom)]
    [InlineData(ArtifactCaptureCondition.HaveLeadershipSkill, SecondarySkill.Leadership)]
    public void AGuardedArtifactMayAskForASkill(ArtifactCaptureCondition condition, SecondarySkill expected)
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[2] = (uint)condition;

        SecondarySkillLevel? skill = tile.GetArtifactSecondarySkillRequirement(world);

        Assert.NotNull(skill);
        Assert.Equal(expected, skill!.Value.Skill);
        Assert.Equal(SkillLevel.Basic, skill.Value.Level);
    }

    [Theory]
    [InlineData(ArtifactCaptureCondition.NoConditions)]
    [InlineData(ArtifactCaptureCondition.Pay2000Gold)]
    [InlineData(ArtifactCaptureCondition.Fight1Titan)]
    public void AGoldOrFightGuardAsksForNoSkill(ArtifactCaptureCondition condition)
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[2] = (uint)condition;

        Assert.Null(tile.GetArtifactSecondarySkillRequirement(world));
    }

    [Fact]
    public void AGuardedArtifactAsksForTwoThousandGold()
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[2] = (uint)ArtifactCaptureCondition.Pay2000Gold;

        Funds funds = tile.GetArtifactResourceRequirement(world);

        Assert.Equal(2000, funds.Gold);
        Assert.Equal(0, funds.Wood);
    }

    [Fact]
    public void AGuardedArtifactAsksForGoldAndThreeCrystals()
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[2] = (uint)ArtifactCaptureCondition.Pay2500GoldAnd3Resources;
        // 资源图标从 1 开始计数：5 号图标是水晶（wood = 1 … crystal = 5）。
        tile.Metadata[1] = 5;

        Funds funds = tile.GetArtifactResourceRequirement(world);

        Assert.Equal(2500, funds.Gold);
        Assert.Equal(3, funds.Crystal);
    }

    [Fact]
    public void AnUnguardedArtifactAsksForNothing()
    {
        Tile tile = MakeTile(Mp2ObjectType.Artifact, out World world);
        tile.Metadata[2] = (uint)ArtifactCaptureCondition.NoConditions;

        Funds funds = tile.GetArtifactResourceRequirement(world);

        Assert.Equal(0, funds.Gold);
        Assert.Null(tile.GetArtifactSecondarySkillRequirement(world));
    }

    [Fact]
    public void ADaemonCaveKeepsItsBonusInTheThirdLaneAndItsPriceInTheSecond()
    {
        Tile tile = MakeTile(Mp2ObjectType.DaemonCave, out World world);
        tile.Metadata[2] = (uint)DaemonCaveCaptureBonus.Pay2500Gold;
        tile.Metadata[1] = 2500;

        Assert.Equal(DaemonCaveCaptureBonus.Pay2500Gold, tile.GetDaemonCaveBonusType(world));
        Assert.Equal(2500, tile.GetDaemonPaymentCondition(world).Gold);
    }

    [Fact]
    public void AnExperienceDaemonCaveAsksForNoGold()
    {
        Tile tile = MakeTile(Mp2ObjectType.DaemonCave, out World world);
        tile.Metadata[2] = (uint)DaemonCaveCaptureBonus.Get1000Experience;
        tile.Metadata[1] = 2500;

        Assert.Equal(DaemonCaveCaptureBonus.Get1000Experience, tile.GetDaemonCaveBonusType(world));
        Assert.Equal(0, tile.GetDaemonPaymentCondition(world).Gold);
    }

    [Fact]
    public void AShipwreckKeepsItsConditionInTheThirdLane()
    {
        Tile tile = MakeTile(Mp2ObjectType.Shipwreck, out World world);
        tile.Metadata[2] = (uint)ShipwreckCaptureCondition.Fight25GhostsAndGet5000Gold;

        Assert.Equal(ShipwreckCaptureCondition.Fight25GhostsAndGet5000Gold, tile.GetShipwreckCaptureCondition(world));
    }

    [Theory]
    [InlineData(Mp2ObjectType.Monster)]
    [InlineData(Mp2ObjectType.DragonCity)]
    [InlineData(Mp2ObjectType.AbandonedMine)]
    [InlineData(Mp2ObjectType.PeasantHut)]
    public void AMonsterHolderRoundTripsItsCount(Mp2ObjectType objectType)
    {
        Tile tile = MakeTile(objectType, out World world);

        tile.SetMonsterCount(world, 250);

        Assert.Equal(250u, tile.GetMonsterCount(world));
    }

    [Fact]
    public void TheWanderingStacksCreatureIsTheSpriteIndexPlusOne()
    {
        Tile tile = MakeTile(Mp2ObjectType.Monster, out World world);
        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.Mons32, 34));

        Assert.Equal((MonsterType)35, tile.GetMonster(world));
    }

    [Theory]
    [InlineData(Mp2ObjectType.DragonCity, MonsterType.RedDragon)]
    [InlineData(Mp2ObjectType.CityOfDead, MonsterType.PowerLich)]
    [InlineData(Mp2ObjectType.GenieLamp, MonsterType.Genie)]
    [InlineData(Mp2ObjectType.Excavation, MonsterType.Skeleton)]
    [InlineData(Mp2ObjectType.TreeHouse, MonsterType.Sprite)]
    [InlineData(Mp2ObjectType.BarrowMounds, MonsterType.Ghost)]
    public void ADwellingTellsWhichCreatureItHolds(Mp2ObjectType objectType, MonsterType expected)
    {
        Tile tile = MakeTile(objectType, out World world);

        Assert.Equal(expected, tile.GetMonster(world));
    }

    [Fact]
    public void ACapturedObjectAnswersWithItsGuardians()
    {
        // A mine on a real map carries the action bit, which is what `MP2::isCaptureObject` looks for.
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);
        world.GetCapturedObject(tile.Index).Guardians = new Troop(MonsterType.Goblin, 42);

        Assert.Equal(MonsterType.Goblin, tile.GetMonster(world));
        Assert.Equal(42u, world.GetCapturedObject(tile.Index).Guardians.Count);
    }

    [Fact]
    public void TheColourOfAnObjectIsTheOneItWasCapturedBy()
    {
        Tile tile = MakeTile(Mp2ObjectType.Mine, out World world);

        Assert.Equal(PlayerColor.None, tile.GetColor(world));

        tile.SetColor(world, PlayerColor.Blue);

        Assert.Equal(PlayerColor.Blue, tile.GetColor(world));
    }

    [Fact]
    public void AFirstLoadFillsADragonCityWithTwoDragons()
    {
        Tile tile = MakeTile(Mp2ObjectType.DragonCity, out World world);

        tile.UpdateDwellingPopulation(world, isFirstLoad: true);

        Assert.Equal(2u, tile.GetMonsterCount(world));
    }

    [Fact]
    public void ACapturedDragonCityGrowsByOneDragonAWeek()
    {
        Tile tile = MakeTile(Mp2ObjectType.DragonCity, out World world);
        tile.UpdateDwellingPopulation(world, isFirstLoad: true);
        tile.SetColor(world, PlayerColor.Red);

        tile.UpdateDwellingPopulation(world, isFirstLoad: false);

        Assert.Equal(3u, tile.GetMonsterCount(world));
    }

    [Fact]
    public void AnUncapturedBridgeKeepsItsGarrison()
    {
        Tile tile = MakeTile(Mp2ObjectType.TrollBridge, out World world);
        tile.UpdateDwellingPopulation(world, isFirstLoad: true);
        uint before = tile.GetMonsterCount(world);

        tile.UpdateDwellingPopulation(world, isFirstLoad: false);

        Assert.Equal(before, tile.GetMonsterCount(world));
    }

    [Fact]
    public void APeasantHutStartsWithinItsWeeklyRange()
    {
        Tile tile = MakeTile(Mp2ObjectType.PeasantHut, out World world);

        tile.UpdateDwellingPopulation(world, isFirstLoad: true);

        Assert.InRange(tile.GetMonsterCount(world), 20u, 50u);
    }

    [Theory]
    [InlineData(Mp2ObjectType.None, true)]
    [InlineData(Mp2ObjectType.Coast, true)]
    [InlineData(Mp2ObjectType.Boat, false)]
    [InlineData(Mp2ObjectType.Mine, false)]
    public void ClearGroundIsEverythingThatNoLongerDoesAnything(Mp2ObjectType objectType, bool expected)
    {
        Tile tile = MakeTile(objectType, out World world);

        if (objectType is Mp2ObjectType.Mine or Mp2ObjectType.Boat)
        {
            tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.XLoc3, 3));
        }

        Assert.Equal(expected, tile.IsClearGround(world));
    }

    private static Tile MakeTile(Mp2ObjectType type, out World world)
    {
        world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(5);
        tile.SetMainObjectType(type);

        return tile;
    }
}
