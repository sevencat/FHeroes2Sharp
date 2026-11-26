using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The prefab object groups the random map generator drops into a region, <c>prefabObjectSets</c> and
/// <c>powerupObjectSets</c> (<c>map_random_generator.cpp:113-211</c>).
/// </summary>
public sealed class RandomMapObjectSetsTests
{
    [Fact]
    public void ThereAreFiveTreasureAndFivePowerUpPrefabs()
    {
        Assert.Equal(5, RandomMapObjectSets.Prefabs.Count);
        Assert.Equal(5, RandomMapObjectSets.PowerUps.Count);
    }

    [Fact]
    public void TheFirstPrefabIsTheOneOfTheReference()
    {
        RandomMapObjectSet prefab = RandomMapObjectSets.Prefabs[0];

        Assert.Equal(
            [
                new RandomMapObjectPlacement(new Point(0, -1), ObjectGroup.LandscapeTrees, 3),
                new RandomMapObjectPlacement(new Point(3, -1), ObjectGroup.LandscapeTrees, 2),
                new RandomMapObjectPlacement(new Point(3, 1), ObjectGroup.LandscapeTrees, 3),
            ],
            prefab.Obstacles);

        Assert.Equal(4, prefab.Valuables.Count);

        Assert.Equal([new Point(-1, 0), new Point(-1, 1), new Point(0, 1)], prefab.EntranceCheck);
    }

    [Fact]
    public void EveryTreasurePrefabGuardsItsOwnTreasures()
    {
        foreach (RandomMapObjectSet prefab in RandomMapObjectSets.Prefabs)
        {
            Assert.NotEmpty(prefab.Obstacles);
            Assert.NotEmpty(prefab.Valuables);
            Assert.NotEmpty(prefab.EntranceCheck);

            Assert.All(prefab.Valuables, valuable =>
            {
                Assert.Equal(ObjectGroup.AdventureTreasures, valuable.Group);
                Assert.Equal(RandomMapObjectSets.TreasureIndex, valuable.ObjectIndex);
            });
        }
    }

    [Fact]
    public void APowerUpPrefabReplacesOneTreasureWithAPowerUp()
    {
        foreach (RandomMapObjectSet prefab in RandomMapObjectSets.PowerUps)
        {
            List<RandomMapObjectPlacement> powerUps = prefab.Valuables
                .Where(valuable => valuable.Group == ObjectGroup.AdventurePowerUps)
                .ToList();
            List<RandomMapObjectPlacement> treasures = prefab.Valuables
                .Where(valuable => valuable.Group == ObjectGroup.AdventureTreasures)
                .ToList();

            Assert.Single(powerUps);
            Assert.Equal(2, treasures.Count);
            Assert.All(treasures, treasure => Assert.Equal(RandomMapObjectSets.TreasureIndex, treasure.ObjectIndex));
        }
    }

    [Fact]
    public void ThePowerUpIndicesAreTheOnesOfTheReference()
    {
        int[] indices = RandomMapObjectSets.PowerUps
            .Select(prefab => prefab.Valuables.Single(valuable => valuable.Group == ObjectGroup.AdventurePowerUps))
            .Select(powerUp => powerUp.ObjectIndex)
            .ToArray();

        Assert.Equal([10, 18, 12, 13, 16], indices);
    }

    [Fact]
    public void OnlyTheFourPrefabGroupsAreUsed()
    {
        foreach (RandomMapObjectSet prefab in RandomMapObjectSets.Prefabs.Concat(RandomMapObjectSets.PowerUps))
        {
            Assert.All(prefab.Obstacles, obstacle => Assert.True(
                RandomMapObjectSets.IsPrefabGroup(obstacle.Group), $"{obstacle.Group} is no prefab group"));
            Assert.All(prefab.Valuables, valuable => Assert.True(
                RandomMapObjectSets.IsPrefabGroup(valuable.Group), $"{valuable.Group} is no prefab group"));
        }

        Assert.False(RandomMapObjectSets.IsPrefabGroup(ObjectGroup.Roads));
        Assert.False(RandomMapObjectSets.IsPrefabGroup(ObjectGroup.AdventureMines));
    }

    [Fact]
    public void TheEntranceAndThePrizesNeverSitOnAnObstacle()
    {
        foreach (RandomMapObjectSet prefab in RandomMapObjectSets.Prefabs.Concat(RandomMapObjectSets.PowerUps))
        {
            HashSet<Point> obstacles = [.. prefab.Obstacles.Select(obstacle => obstacle.Offset)];

            Assert.All(prefab.EntranceCheck, point => Assert.DoesNotContain(point, obstacles));
            Assert.All(prefab.Valuables, valuable => Assert.DoesNotContain(valuable.Offset, obstacles));
        }
    }

    [Fact]
    public void ThePrizesNeverShareATileWithEachOther()
    {
        foreach (RandomMapObjectSet prefab in RandomMapObjectSets.Prefabs.Concat(RandomMapObjectSets.PowerUps))
        {
            List<Point> offsets = [.. prefab.Valuables.Select(valuable => valuable.Offset)];

            Assert.Equal(offsets.Count, offsets.Distinct().Count());
        }
    }
}
