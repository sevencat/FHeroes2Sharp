using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="World.ComputeStaticAnalysis"/>: the region map and the two terrain statistics
/// (<c>world_regions.cpp:178-398</c>).
/// </summary>
public sealed class WorldRegionsTests
{
    private const int Size = 8;

    [Fact]
    public void AnEmptyWorldHasNoRegions()
    {
        var world = new World();

        Assert.Equal(0, world.RegionCount);
        Assert.Equal(0, world.WaterPercentage);
        Assert.Equal(1.0, world.LandRoughness);

        // The reference hands out a static empty region for an id it does not have (world_regions.cpp:172).
        MapRegion region = world.GetRegion(MapRegionNodes.Found);

        Assert.Empty(region.Nodes);
        Assert.Empty(region.Neighbours);
        Assert.Equal(0, region.NeighboursCount);
        Assert.Equal(MapRegionNodes.Found, region.Id);
    }

    [Fact]
    public void AGrassMapIsOneLandRegion()
    {
        World world = WorldOf(_ => TileOf(MapGrounds.StartImageIndex(MapGround.Grass)));

        // Ids 0, 1 and 2 are the constants of MapRegionNodes, so the one real region is id 3.
        Assert.Equal(4, world.RegionCount);

        for (int index = 0; index < world.Size; ++index)
        {
            Assert.Equal(MapRegionNodes.Found, world.GetTile(index).Region);
        }

        MapRegion region = world.GetRegion(MapRegionNodes.Found);

        Assert.Equal(world.Size, region.Nodes.Count);
        Assert.False(region.IsWater);
        Assert.Empty(region.Neighbours);
        Assert.Equal(0, world.WaterPercentage);
        Assert.Equal(1.0, world.LandRoughness);
    }

    [Fact]
    public void RegionsThatMeetAreNeighboursOfEachOther()
    {
        // A map wide enough for two seeds: they grow towards each other, meet, and record the meeting in both
        // directions (world_regions.cpp:386-391).
        World world = WorldOf(_ => TileOf(MapGrounds.StartImageIndex(MapGround.Grass)), 24);

        Assert.True(world.RegionCount > (int)MapRegionNodes.Found + 1);

        bool anyNeighbour = false;

        for (uint id = MapRegionNodes.Found; id < (uint)world.RegionCount; ++id)
        {
            MapRegion region = world.GetRegion(id);

            foreach (uint adjacent in region.Neighbours)
            {
                anyNeighbour = true;

                Assert.Contains(id, world.GetRegion(adjacent).Neighbours);
            }
        }

        Assert.True(anyNeighbour);
    }

    [Fact]
    public void TheShorelineKeepsLandAndWaterApart()
    {
        // One column of water splits the island in two: the region growing must not cross it.
        World world = WorldOf(index => TileOf(index % Size == 3
            ? MapGrounds.StartImageIndex(MapGround.Water)
            : MapGrounds.StartImageIndex(MapGround.Grass)));

        // The three id constants are regions too — the reference's constructor gives them a node as well — so only
        // the ids from MapRegionNodes.Found upwards are real ones.
        List<MapRegion> regions = [.. Enumerable.Range((int)MapRegionNodes.Found, world.RegionCount - (int)MapRegionNodes.Found)
            .Select(id => world.GetRegion((uint)id))];

        Assert.Equal(3, regions.Count);

        List<MapRegion> land = [.. regions.Where(region => !region.IsWater)];
        List<MapRegion> water = [.. regions.Where(region => region.IsWater)];

        Assert.Equal(2, land.Count);
        Assert.Single(water);

        // The two halves are separated by water, so they touch each other nowhere. They do not list the water
        // either: neighbours are only recorded while regions grow into each other (world_regions.cpp:105), and the
        // water here was claimed afterwards by FindMissingRegions(), which adds no links at all. That is the
        // reference's behaviour, and it is why a lake in the middle of a continent is a neighbourless region.
        Assert.DoesNotContain(land[1].Id, land[0].Neighbours);
        Assert.DoesNotContain(land[0].Id, land[1].Neighbours);
        Assert.Empty(land[0].Neighbours);
        Assert.Empty(land[1].Neighbours);
        Assert.Empty(water[0].Neighbours);

        Assert.Equal(Size, water[0].Nodes.Count);
        Assert.Equal(world.Size - Size, land[0].Nodes.Count + land[1].Nodes.Count);

        // Eight water tiles of sixty four, rounded down.
        Assert.Equal(12, world.WaterPercentage);
    }

    [Fact]
    public void AnAllWaterMapIsOneWaterRegion()
    {
        World world = WorldOf(_ => TileOf(MapGrounds.StartImageIndex(MapGround.Water)));

        Assert.Equal(4, world.RegionCount);

        MapRegion region = world.GetRegion(MapRegionNodes.Found);

        Assert.True(region.IsWater);
        Assert.Equal(world.Size, region.Nodes.Count);
        Assert.Equal(100, world.WaterPercentage);

        // No land at all: the reference divides by zero here (world_regions.cpp:246), the port keeps 1.0.
        Assert.Equal(1.0, world.LandRoughness);
    }

    [Fact]
    public void DesertMakesTheLandRough()
    {
        World world = WorldOf(_ => TileOf(MapGrounds.StartImageIndex(MapGround.Desert)));

        // desert.cpp's row of the table: a hero without pathfinding pays twice the grass price (world_regions.cpp:246).
        Assert.Equal(2.0, world.LandRoughness);
        Assert.Equal(0, world.WaterPercentage);
    }

    [Fact]
    public void ResetForgetsTheRegions()
    {
        World world = WorldOf(_ => TileOf(MapGrounds.StartImageIndex(MapGround.Water)));

        Assert.Equal(4, world.RegionCount);

        world.Reset();

        // The reference leaves the old regions in place until the next ComputeStaticAnalysis (world_regions.cpp:258
        // only clears them there); the port clears them with the tiles they describe.
        Assert.Equal(0, world.RegionCount);
        Assert.Equal(0, world.WaterPercentage);
        Assert.Equal(1.0, world.LandRoughness);
    }

    [Fact]
    public void EveryRegionOfARealMapIsConnectedAndHomogeneous()
    {
        string? directory = GameData.TryResolveDataDirectory();

        if (directory is null)
        {
            return;
        }

        // The archives live in Data/ in most installations, while the maps sit next to them one level up.
        string? maps = new[] { Path.Combine(directory, "maps"), Path.Combine(Path.GetDirectoryName(directory) ?? directory, "maps") }
            .FirstOrDefault(candidate => Directory.Exists(candidate));

        if (maps is null)
        {
            return;
        }

        string? file = Directory.GetFiles(maps, "*.mp2").OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();

        if (file is null)
        {
            return;
        }

        var world = new World();

        Assert.True(world.TryLoadMap(file));
        Assert.True(world.RegionCount > (int)MapRegionNodes.Found);

        int waterTiles = 0;
        int landTiles = 0;

        for (int index = 0; index < world.Size; ++index)
        {
            Tile tile = world.GetTile(index);

            if (tile.PassableDirections == Direction.Unknown)
            {
                // A mountain, a tree, a building: no region, which is what the zero of MapRegionNodes means.
                Assert.Equal(MapRegionNodes.Blocked, tile.Region);

                continue;
            }

            Assert.True(tile.Region >= MapRegionNodes.Found);

            if (tile.IsWater)
            {
                ++waterTiles;
            }
            else
            {
                ++landTiles;
            }
        }

        Assert.Equal(waterTiles * 100 / (waterTiles + landTiles), world.WaterPercentage);

        uint terrainPenalty = 0;

        for (int index = 0; index < world.Size; ++index)
        {
            Tile tile = world.GetTile(index);

            if (tile.PassableDirections != Direction.Unknown && !tile.IsWater)
            {
                terrainPenalty += MapGrounds.GetPenalty(tile.Ground, SkillLevel.None);
            }
        }

        Assert.Equal(terrainPenalty / (double)(landTiles * MapGrounds.DefaultGroundPenalty), world.LandRoughness, 9);

        for (uint id = MapRegionNodes.Found; id < (uint)world.RegionCount; ++id)
        {
            MapRegion region = world.GetRegion(id);

            Assert.Equal(id, region.Id);
            Assert.NotEmpty(region.Nodes);

            foreach (MapRegionNode node in region.Nodes)
            {
                Assert.Equal(id, world.GetTile(node.Index).Region);
                Assert.Equal(region.IsWater, world.GetTile(node.Index).IsWater);
            }

            // Every tile of a region must be reachable from the first one through tiles of the same region: that is
            // what "connected patch" means, and it is the only property the growth loop has to guarantee.
            var visited = new HashSet<int> { region.Nodes[0].Index };
            var queue = new Queue<int>();
            queue.Enqueue(region.Nodes[0].Index);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();

                foreach (int neighbour in Neighbours(world, index))
                {
                    if (world.GetTile(neighbour).Region == id && visited.Add(neighbour))
                    {
                        queue.Enqueue(neighbour);
                    }
                }
            }

            Assert.Equal(region.Nodes.Count, visited.Count);

            foreach (uint adjacent in region.Neighbours)
            {
                Assert.Contains(id, world.GetRegion(adjacent).Neighbours);
            }
        }
    }

    private static IEnumerable<int> Neighbours(World world, int index)
    {
        int x = index % world.Width;

        for (int direction = 0; direction < 8; ++direction)
        {
            int neighbour = index + (direction switch
            {
                0 => -world.Width - 1,
                1 => -world.Width,
                2 => -world.Width + 1,
                3 => 1,
                4 => world.Width + 1,
                5 => world.Width,
                6 => world.Width - 1,
                _ => -1,
            });

            if (neighbour < 0 || neighbour >= world.Size)
            {
                continue;
            }

            // The row of a tile must not wrap around the map edge.
            if (Math.Abs((neighbour % world.Width) - x) > 1)
            {
                continue;
            }

            yield return neighbour;
        }
    }

    private static World WorldOf(Func<int, Mp2TileInfo> tileFactory, int size = Size)
    {
        var world = new World();
        world.LoadMap(SyntheticMap.Create(size, size, tileFactory));

        return world;
    }

    private static Mp2TileInfo TileOf(ushort terrainImageIndex) =>
        SyntheticMap.BlankTile() with { TerrainImageIndex = terrainImageIndex };
}
