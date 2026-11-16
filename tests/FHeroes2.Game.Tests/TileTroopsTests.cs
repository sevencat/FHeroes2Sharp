using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the monster stack of a tile — <c>Maps::getTroopFromTile()</c>,
/// <c>Maps::updateMonstersOnTile()</c> and <c>Maps::setMonsterOnTile()</c>
/// (<c>maps_tiles_helper.cpp:763</c>, <c>:1697</c>, <c>:1727</c>) — together with
/// <c>Monster::GetRNDSize()</c> (<c>monster.cpp:165-220</c>).
/// </summary>
public sealed class TileTroopsTests
{
    [Fact]
    public void AWanderingStackIsItsMonsterAndItsCount()
    {
        Tile tile = MakeMonsterTile(MonsterType.Goblin, 12);

        Troop troop = tile.GetTroop(MakeWorld(tile));

        Assert.Equal(MonsterType.Goblin, troop.Monster);
        Assert.Equal(12u, troop.Count);
    }

    [Fact]
    public void ACapturedObjectAnswersWithItsGuards()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);

        world.CaptureObject(tile.Index, PlayerColor.Red);
        world.GetCapturedObject(tile.Index).Guardians = new Troop(MonsterType.Skeleton, 7);

        Troop troop = tile.GetTroop(world);

        Assert.Equal(MonsterType.Skeleton, troop.Monster);
        Assert.Equal(7u, troop.Count);
    }

    [Fact]
    public void AStackOfSevenGrowsByExactlyOne()
    {
        Tile tile = MakeMonsterTile(MonsterType.Goblin, 7);

        tile.UpdateMonsters(1000);

        Assert.Equal(8u, tile.GetMonsterCount());
    }

    [Fact]
    public void AStackOfSixGrowsByAtMostOne()
    {
        Tile tile = MakeMonsterTile(MonsterType.Goblin, 6);

        tile.UpdateMonsters(1000);

        uint grown = tile.GetMonsterCount();

        Assert.True(grown is 6 or 7, $"A stack of six grew to {grown}.");
    }

    [Fact]
    public void GrowthStopsAtTheLimit()
    {
        Tile tile = MakeMonsterTile(MonsterType.Goblin, 21);

        tile.UpdateMonsters(22);

        Assert.Equal(22u, tile.GetMonsterCount());
    }

    [Fact]
    public void AStackThatReachedTheLimitIsLeftAlone()
    {
        Tile tile = MakeMonsterTile(MonsterType.Goblin, 40);

        tile.UpdateMonsters(40);

        Assert.Equal(40u, tile.GetMonsterCount());
    }

    [Fact]
    public void AnEmptyStackDoesNotGrow()
    {
        Tile tile = MakeMonsterTile(MonsterType.Goblin, 0);

        tile.UpdateMonsters(1000);

        Assert.Equal(0u, tile.GetMonsterCount());
    }

    [Fact]
    public void AnythingButAMonsterDoesNotGrow()
    {
        // A dwelling holds a count as well, but it is not a wandering stack: the early return of
        // `updateMonstersOnTile` is what this checks.
        Tile tile = MakeTile(Mp2ObjectType.GoblinHut, out World world);

        tile.SetMonsterCount(6);
        tile.UpdateMonsters(1000);

        Assert.Equal(6u, tile.GetMonsterCount());
    }

    [Fact]
    public void PlacingAStackPaintsTheMonsterSprite()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMonster(world, MonsterType.Goblin, 5);

        Assert.Equal(Mp2ObjectType.Monster, tile.MainObjectType);
        Assert.Equal(Mp2ObjectIcnType.Mons32, tile.MainObjectPart.IcnType);
        Assert.Equal((byte)(MonsterType.Goblin - 1), tile.MainObjectPart.IcnIndex);
        Assert.Equal(5u, tile.GetMonsterCount());
    }

    [Fact]
    public void PlacingAStackOnAnEmptyTileTakesAFreshUid()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMonster(world, MonsterType.Goblin, 5);

        // A fresh world starts its counter at zero, so the first object it hands out is one.
        Assert.Equal(1u, tile.MainObjectPart.Uid);
        Assert.NotEqual(ObjectUid, tile.MainObjectPart.Uid);
    }

    [Fact]
    public void PlacingAStackOverAMonsterKeepsTheUid()
    {
        Tile tile = MakeMonsterTile(MonsterType.Skeleton, 3);

        tile.SetMonster(MakeWorld(tile), MonsterType.Goblin, 5);

        Assert.Equal(ObjectUid, tile.MainObjectPart.Uid);
        Assert.Equal((byte)(MonsterType.Goblin - 1), tile.MainObjectPart.IcnIndex);
    }

    [Fact]
    public void TheSpriteOfAnotherObjectIsPushedDown()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, ObjectUid, Mp2ObjectIcnType.ObjnHaun, 3));

        tile.SetMonster(world, MonsterType.Goblin, 5);

        ObjectPart pushed = Assert.Single(tile.GroundObjectParts);

        Assert.Equal(Mp2ObjectIcnType.ObjnHaun, pushed.IcnType);
        Assert.Equal(Mp2ObjectIcnType.Mons32, tile.MainObjectPart.IcnType);
    }

    [Fact]
    public void AZeroCountMeansRollOne()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMonster(world, MonsterType.Goblin, 0);

        // Goblins are a level one monster: half of fifty to fifty.
        Assert.InRange(tile.GetMonsterCount(), 25u, 50u);
    }

    [Fact]
    public void AStackWithACountFromTheMapOnlyJoinsForMoney()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMonster(world, MonsterType.Goblin, 5);

        Assert.False(tile.IsMonsterJoinConditionSkip(world));
        Assert.False(tile.IsMonsterJoinConditionFree(world));
    }

    [Fact]
    public void GhostsNeverJoin()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMonster(world, MonsterType.Ghost, 5);

        Assert.True(tile.IsMonsterJoinConditionSkip(world));
    }

    [Fact]
    public void ElementalsNeverJoin()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        tile.SetMonster(world, MonsterType.AirElement, 5);

        Assert.True(tile.IsMonsterJoinConditionSkip(world));
    }

    [Fact]
    public void ARolledStackJoinsForFreeOrForMoney()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        // The roll depends on the generator of the thread: only the two allowed answers are checked.
        tile.SetMonster(world, MonsterType.Goblin, 0);

        Assert.True(tile.IsMonsterJoinConditionFree(world) || !tile.IsMonsterJoinConditionSkip(world));
    }

    [Theory]
    [InlineData(MonsterType.Peasant, 40u, 80u)]
    [InlineData(MonsterType.Skeleton, 25u, 50u)]
    [InlineData(MonsterType.Goblin, 25u, 50u)]
    [InlineData(MonsterType.Minotaur, 8u, 16u)]
    [InlineData(MonsterType.Paladin, 6u, 12u)]
    [InlineData(MonsterType.RedDragon, 4u, 8u)]
    public void TheRollOfAStackFollowsTheTable(MonsterType monster, uint low, uint high)
    {
        for (int i = 0; i < 32; i++)
        {
            uint count = MonsterStatistics.GetRandomCount(monster);

            Assert.InRange(count, low, high);
        }
    }

    [Fact]
    public void TheSpriteIndexIsTheIdMinusOne()
    {
        Assert.Equal(0u, Tile.MonsterSpriteIndex(MonsterType.Unknown));
        Assert.Equal((uint)(MonsterType.Peasant - 1), Tile.MonsterSpriteIndex(MonsterType.Peasant));
    }

    private const uint ObjectUid = 77;

    private static World MakeWorld(Tile tile) => new();

    private static Tile MakeTile(Mp2ObjectType type, out World world)
    {
        world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(5);

        tile.Index = 5;
        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, ObjectUid, Mp2ObjectIcnType.ObjnHaun, 0));
        tile.SetMainObjectType(type);

        return tile;
    }

    private static Tile MakeMonsterTile(MonsterType monster, uint count)
    {
        Tile tile = MakeTile(Mp2ObjectType.Monster, out World _);

        ObjectPart part = tile.MainObjectPart;
        part.IcnType = Mp2ObjectIcnType.Mons32;
        part.IcnIndex = (byte)(monster - 1);
        tile.SetMainObjectPart(part);
        tile.SetMonsterCount(count);

        return tile;
    }
}
