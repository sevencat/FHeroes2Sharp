using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game;

/// <summary>
/// The static analysis of the map: the regions and the two terrain statistics,
/// <c>World::ComputeStaticAnalysis()</c> plus the accessors of <c>world.h:485-497</c>
/// (<c>world_regions.cpp:163-398</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>What a region is.</b> The map is cut into patches of connected passable tiles of one kind — land or water —
/// and every patch gets an id. The AI uses them to reason about "where the enemy is" without running a search per
/// turn, and the whirlpool are connected through the regions they lead to. The reference computes all of it once
/// per loaded map, at the end of <c>PostLoad()</c> (<c>world.cpp:1233</c>).
/// </para>
/// <para>
/// <b>Why the algorithm is nine steps.</b> Regions are grown from seeds and stop where another region got there
/// first, so the result depends on where the seeds are: castles first (their owners' colours decide the order),
/// then the emptiest rows and columns every seventh line. What is left over — islands, lakes, the interior of a
/// mountain ring — gets its own region at the end. The order is the reference's and it is not an implementation
/// detail: two seeds two tiles apart give a completely different partition from two seeds twenty tiles apart.
/// </para>
/// <para>
/// <b>What is not here.</b> Nothing: <c>world_regions.cpp</c> is a closed algorithm over tiles, castles and the
/// teleport caches the port already has. The AI that reads the result is still missing, but it reads, it does not
/// compute.
/// </para>
/// </remarks>
public sealed partial class World
{
    private readonly List<MapRegion> _regions = [];

    private byte _waterPercentage;
    private double _landRoughness = 1.0;

    /// <summary>
    /// How much of the walkable map is water, in per cent, <c>World::getWaterPercentage()</c> (<c>world.h:488</c>).
    /// </summary>
    /// <remarks>Rounded down, 0 until <see cref="ComputeStaticAnalysis"/> has run.</remarks>
    public byte WaterPercentage => _waterPercentage;

    /// <summary>
    /// How rough the land is compared with plain grass, <c>World::getLandRoughness()</c> (<c>world.h:493</c>).
    /// </summary>
    /// <remarks>
    /// The average movement penalty of the land tiles divided by the penalty of one grass tile
    /// (<c>world_regions.cpp:246</c>), so 1.0 is "all grass", 2.0 is "all desert for a hero without pathfinding".
    /// 1.0 until <see cref="ComputeStaticAnalysis"/> has run.
    /// </remarks>
    public double LandRoughness => _landRoughness;

    /// <summary>How many regions the map was cut into, <c>World::getRegionCount()</c> (<c>world_regions.cpp:163</c>).</summary>
    public int RegionCount => _regions.Count;

    /// <summary>
    /// A region by id, <c>World::getRegion()</c> (<c>world_regions.cpp:168</c>).
    /// </summary>
    /// <remarks>
    /// An id out of range gives an empty region rather than an exception: the reference returns a static empty
    /// object, because a tile that belongs to no region still gets asked about (a castle on a mountain, a
    /// whirlpool exit that is blocked).
    /// </remarks>
    /// <param name="id">Region id, the value of <see cref="Tile.Region"/>.</param>
    /// <returns>The region, or an empty one.</returns>
    public MapRegion GetRegion(uint id) => id < (uint)_regions.Count ? _regions[(int)id] : EmptyRegion;

    private static MapRegion EmptyRegion { get; } = new();

    /// <summary>
    /// Cuts the map into regions and measures its terrain, <c>World::ComputeStaticAnalysis()</c>
    /// (<c>world_regions.cpp:178-398</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The nine steps</b>, in the reference's order: count the obstacles per row and column and measure the
    /// terrain; pick the emptiest rows and columns as seed lines; add the castles as seeds; add the remaining seeds
    /// on the seed lines; build the working copy of the map with the extended border; create one region per seed;
    /// grow them all one tile per round so they compete for space; give the leftovers their own regions; finally
    /// write the region ids into the tiles and wire the neighbours together, including through stone liths and
    /// whirlpools.
    /// </para>
    /// <para>
    /// <b>Where the port guards and the reference asserts.</b> The reference asserts that the map has walkable tiles
    /// and land tiles (<c>world_regions.cpp:239-244</c>). A port world can be empty (that is what
    /// <see cref="Reset"/> leaves behind), so an empty world returns the empty answer, and a map without land keeps
    /// <see cref="LandRoughness"/> at 1.0 instead of dividing by zero. The regions themselves are still computed:
    /// an all-water map has regions, it just has no land.
    /// </para>
    /// <para>
    /// <b>Ties are broken by line number.</b> The reference sorts the rows and columns by obstacle count only
    /// (<c>world_regions.cpp:250</c>), which leaves the order of equally blocked lines to <c>std::sort</c>. The port
    /// sorts by count and then by line, which is the same partition for a map without ties and a reproducible one
    /// for a map with them.
    /// </para>
    /// </remarks>
    public void ComputeStaticAnalysis()
    {
        // The parameters of the reference (world_regions.cpp:181-183): how far apart seeds must be, and how often
        // a line is considered as a seed line.
        const int castleRegionSize = 17;
        const int extraRegionSize = 18;
        const int emptyLineFrequency = 7;

        ClearRegions();

        if (_tiles.Count == 0)
        {
            return;
        }

        int width = Width;
        int height = Height;

        var geometry = new MapGeometry(width, height);

        // Step 1. Split the map into blocked, water and ground tiles, and measure the terrain on the way.
        // Obstacles 0 and 1 are the columns and rows seen from the water, 2 and 3 from the land: a blocked tile
        // blocks both, a water tile is an obstacle for the land and the other way round.
        var obstacles = new List<(int Line, int Count)>[4];

        obstacles[0] = new List<(int, int)>(width);
        obstacles[2] = new List<(int, int)>(width);

        for (int x = 0; x < width; ++x)
        {
            obstacles[0].Add((x, 0));
            obstacles[2].Add((x, 0));
        }

        obstacles[1] = new List<(int, int)>(height);
        obstacles[3] = new List<(int, int)>(height);

        for (int y = 0; y < height; ++y)
        {
            obstacles[1].Add((y, 0));
            obstacles[3].Add((y, 0));
        }

        int obstacleCount = 0;
        int waterCount = 0;
        uint terrainPenalty = 0;

        for (int y = 0; y < height; ++y)
        {
            int rowIndex = y * width;

            for (int x = 0; x < width; ++x)
            {
                Tile tile = _tiles[rowIndex + x];

                if (tile.PassableDirections == Direction.Unknown)
                {
                    // A mountain, a tree: an obstacle for both kinds of navigation.
                    ++obstacleCount;

                    obstacles[0][x] = (x, obstacles[0][x].Count + 1);
                    obstacles[1][y] = (y, obstacles[1][y].Count + 1);
                    obstacles[2][x] = (x, obstacles[2][x].Count + 1);
                    obstacles[3][y] = (y, obstacles[3][y].Count + 1);
                }
                else if (tile.IsWater)
                {
                    ++waterCount;

                    obstacles[2][x] = (x, obstacles[2][x].Count + 1);
                    obstacles[3][y] = (y, obstacles[3][y].Count + 1);
                }
                else
                {
                    // A hero without pathfinding, which is what GetPenalty( tile, 0 ) asks for.
                    terrainPenalty += MapGrounds.GetPenalty(tile.Ground, SkillLevel.None);

                    obstacles[0][x] = (x, obstacles[0][x].Count + 1);
                    obstacles[1][y] = (y, obstacles[1][y].Count + 1);
                }
            }
        }

        int passableTileCount = (width * height) - obstacleCount;

        if (passableTileCount > 0)
        {
            _waterPercentage = (byte)(waterCount * 100 / passableTileCount);
        }

        int landTiles = passableTileCount - waterCount;

        if (landTiles > 0)
        {
            _landRoughness = terrainPenalty / (double)(landTiles * MapGrounds.DefaultGroundPenalty);
        }

        // The emptiest lines first; equal counts fall back to the line number, which the reference leaves open.
        foreach (List<(int Line, int Count)> lines in obstacles)
        {
            lines.Sort(static (left, right) => left.Count != right.Count ? left.Count.CompareTo(right.Count) : left.Line.CompareTo(right.Line));
        }

        // Step 2. The lines with the most usable space become candidate seed lines.
        var emptyLines = new List<int>[4];

        for (int axis = 0; axis < 4; ++axis)
        {
            emptyLines[axis] = new List<int>(Math.Max(width, height) / emptyLineFrequency);

            foreach ((int line, _) in obstacles[axis])
            {
                AppendIfFarEnough(emptyLines[axis], line, emptyLineFrequency, geometry);
            }
        }

        // Step 3. Every castle seeds a region. The order is by colour, the last colour first (NONE is zero, so
        // neutral towns come last), and by tile index within a colour.
        var castleCenters = new List<(int Index, int Color)>(_castles.Count);

        foreach (Castle.Castle castle in _castles)
        {
            castleCenters.Add((castle.TileIndex, (int)castle.Color));
        }

        castleCenters.Sort(static (left, right) => left.Color != right.Color ? right.Color.CompareTo(left.Color) : left.Index.CompareTo(right.Index));

        var regionCenters = new List<int>();
        int totalMapTiles = _tiles.Count;

        foreach ((int index, _) in castleCenters)
        {
            // A castle seeds the region below its entrance, which is the tile a hero walks onto. The reference's
            // range test is kept as it is, including the fact that a seed below the last row falls back to the
            // castle itself (world_regions.cpp:282).
            int castleIndex = index + width;
            bool belowMap = castleIndex >= 0 && castleIndex > totalMapTiles;

            AppendIfFarEnough(regionCenters, belowMap ? index : castleIndex, castleRegionSize, geometry);
        }

        // Step 4. Fill the gaps: the seed lines that are too far from every castle seed get one too, so that an
        // empty map and a map without castles still get a partition.
        int[] directionOffsets = GetDirectionOffsets(width);

        for (int waterOrGround = 0; waterOrGround < 4; waterOrGround += 2)
        {
            foreach (int lineIndex in emptyLines[waterOrGround])
            {
                int rowIndex = lineIndex * width;

                foreach (int column in emptyLines[waterOrGround + 1])
                {
                    int centerIndex = -1;
                    int tileIndex = rowIndex + column;
                    Tile tile = _tiles[tileIndex];

                    if (tile.PassableDirections != Direction.Unknown && tile.IsWater)
                    {
                        centerIndex = tileIndex;
                    }
                    else
                    {
                        for (int direction = 0; direction < 8; ++direction)
                        {
                            int neighbourIndex = tileIndex + directionOffsets[direction];

                            if (neighbourIndex < 0 || neighbourIndex >= totalMapTiles)
                            {
                                continue;
                            }

                            Tile neighbour = _tiles[neighbourIndex];

                            if (neighbour.PassableDirections != Direction.Unknown && tile.IsWater == (waterOrGround != 0))
                            {
                                centerIndex = neighbourIndex;
                                break;
                            }
                        }
                    }

                    if (centerIndex >= 0)
                    {
                        AppendIfFarEnough(regionCenters, centerIndex, extraRegionSize, geometry);
                    }
                }
            }
        }

        // Step 5. The working copy of the map, grown by one tile on every side, so that the search never has to
        // check the border. A node of the padding keeps type BLOCKED, which is what an array of structs starts as.
        int extendedWidth = width + 2;
        var data = new MapRegionNode[extendedWidth * (height + 2)];

        for (int y = 0; y < height; ++y)
        {
            int rowIndex = y * width;

            for (int x = 0; x < width; ++x)
            {
                int index = rowIndex + x;
                Tile tile = _tiles[index];
                Mp2ObjectType objectType = tile.GetMainObjectType(this);

                data[ConvertExtendedIndex(index, extendedWidth)] = new MapRegionNode(index)
                {
                    Passable = tile.PassableDirections,
                    IsWater = tile.IsWater,
                    MapObject = Mp2ObjectTypes.IsInGameActionObject(objectType, tile.IsWater) ? objectType : Mp2ObjectType.None,
                    Type = tile.PassableDirections != Direction.Unknown ? MapRegionNodes.Open : MapRegionNodes.Blocked,
                };
            }
        }

        // Step 6. One region per seed. Ids 0, 1 and 2 are the constants of MapRegionNodes and never grow, which is
        // how a node tells "no region" from "region number three".
        int averageRegionSize = regionCenters.Count > 0 ? (width * height * 2) / regionCenters.Count : 0;

        for (uint baseId = 0; baseId < MapRegionNodes.Found; ++baseId)
        {
            _regions.Add(new MapRegion(baseId, 0, false, 0));
        }

        foreach (int tileIndex in regionCenters)
        {
            uint regionId = (uint)_regions.Count;

            _regions.Add(new MapRegion(regionId, tileIndex, _tiles[tileIndex].IsWater, averageRegionSize));

            int extendedIndex = ConvertExtendedIndex(tileIndex, extendedWidth);
            MapRegionNode node = data[extendedIndex];
            node.Type = regionId;
            data[extendedIndex] = node;
        }

        // Step 7. Every region grows one tile per round, so that the one with the most room left does not win by
        // going first: they all compete for the same open tiles on equal terms.
        int[] extendedOffsets = GetDirectionOffsets(extendedWidth);
        bool stillRoomToExpand = true;

        while (stillRoomToExpand)
        {
            stillRoomToExpand = false;

            for (int regionIndex = (int)MapRegionNodes.Found; regionIndex < _regions.Count; ++regionIndex)
            {
                MapRegion region = _regions[regionIndex];

                RegionExpansion(data, extendedWidth, region, extendedOffsets);

                if (region.LastProcessedNode != region.Nodes.Count)
                {
                    stillRoomToExpand = true;
                }
            }
        }

        // Step 8. Islands, lakes and other patches no seed reached.
        FindMissingRegions(data, width, height, _regions);

        // Step 9. Write the result into the tiles and connect the regions to each other.
        foreach (MapRegion region in _regions)
        {
            if (region.Id < MapRegionNodes.Found)
            {
                continue;
            }

            foreach (MapRegionNode node in region.Nodes)
            {
                _tiles[node.Index].UpdateRegion(node.Type);

                List<int> exits;

                if (node.MapObject == Mp2ObjectType.ActionStoneLiths)
                {
                    exits = GetTeleportEndPoints(node.Index);
                }
                else if (node.MapObject == Mp2ObjectType.ActionWhirlpool)
                {
                    exits = GetWhirlpoolEndPoints(node.Index);
                }
                else
                {
                    continue;
                }

                foreach (int exitIndex in exits)
                {
                    region.Neighbours.Add(_tiles[exitIndex].Region);
                }
            }

            // The reference inserts into the very set it is walking here, which is safe for a std::set and not for
            // a C# collection; the snapshot is enough because the loop only adds back-references.
            foreach (uint adjacent in region.Neighbours.ToArray())
            {
                _regions[(int)adjacent].Neighbours.Add(region.Id);
            }
        }
    }

    /// <summary>
    /// Empties the region list and the two terrain statistics.
    /// </summary>
    /// <remarks>
    /// The reference never clears them — <c>World::Reset()</c> does not touch <c>_regions</c>, so a world that is
    /// reset keeps the regions of the map it held before (until the next <c>ComputeStaticAnalysis</c>). The port
    /// clears them for the reason ADR-114 gives about the week cache: state that describes tiles must not outlive
    /// the tiles.
    /// </remarks>
    private void ClearRegions()
    {
        _regions.Clear();
        _waterPercentage = 0;
        _landRoughness = 1.0;
    }

    /// <summary>
    /// Appends a seed unless there is one closer than <paramref name="distance"/> tiles,
    /// <c>AppendIfFarEnough()</c> (<c>world_regions.cpp:85</c>).
    /// </summary>
    /// <param name="dataSet">The seeds chosen so far.</param>
    /// <param name="value">The candidate, a tile index or a line number.</param>
    /// <param name="distance">The minimum distance to every seed already chosen.</param>
    /// <param name="geometry">The map dimensions the distance is measured in.</param>
    /// <returns><see langword="true"/> when the candidate was added.</returns>
    private static bool AppendIfFarEnough(List<int> dataSet, int value, int distance, MapGeometry geometry)
    {
        foreach (int current in dataSet)
        {
            if (geometry.GetStraightLineDistance(current, value) < distance)
            {
                return false;
            }
        }

        dataSet.Add(value);

        return true;
    }

    /// <summary>
    /// The eight step offsets of a map row, indexed the way <c>world_regions.cpp:44-55</c> numbers the directions.
    /// </summary>
    /// <param name="width">Width of the map the offsets are for.</param>
    /// <returns>Offsets in the order top left, top, top right, right, bottom right, bottom, bottom left, left.</returns>
    private static int[] GetDirectionOffsets(int width) =>
    [
        -width - 1,
        -width,
        -width + 1,
        1,
        width + 1,
        width,
        width - 1,
        -1,
    ];

    /// <summary>
    /// The bit of one direction, <c>GetDirectionBitmask()</c> (<c>world_regions.cpp:60</c>).
    /// </summary>
    /// <param name="direction">Direction number, 0 to 7.</param>
    /// <param name="reflect">
    /// Whether to ask for the passability of the <i>neighbour</i> towards the current tile rather than the other way
    /// round; the two halves of a step are stored separately.
    /// </param>
    /// <returns>The direction as a single bit.</returns>
    private static Direction GetDirectionBitmask(int direction, bool reflect) =>
        (Direction)(1 << (reflect ? (direction + 4) % 8 : direction));

    /// <summary>
    /// The index of a tile in the working copy that has one tile of padding on every side,
    /// <c>ConvertExtendedIndex()</c> (<c>world_regions.cpp:79</c>).
    /// </summary>
    /// <param name="index">Tile index of the real map.</param>
    /// <param name="width">Width of the working copy, the real width plus two.</param>
    /// <returns>The index in the working copy.</returns>
    private static int ConvertExtendedIndex(int index, int width)
    {
        int originalWidth = width - 2;

        return (((index / originalWidth) + 1) * width) + (index % originalWidth) + 1;
    }

    /// <summary>
    /// Claims the open neighbours of one node for a region, <c>CheckAdjacentTiles()</c>
    /// (<c>world_regions.cpp:97</c>).
    /// </summary>
    /// <param name="data">The working copy of the map.</param>
    /// <param name="region">The region that is growing.</param>
    /// <param name="width">Width of the working copy.</param>
    /// <param name="offsets">Step offsets of the working copy.</param>
    private static void CheckAdjacentTiles(MapRegionNode[] data, MapRegion region, int width, int[] offsets)
    {
        int nodeIndex = region.Nodes[region.LastProcessedNode].Index;
        int extendedIndex = ConvertExtendedIndex(nodeIndex, width);

        for (int direction = 0; direction < 8; ++direction)
        {
            int neighbourIndex = extendedIndex + offsets[direction];
            MapRegionNode newTile = data[neighbourIndex];

            // The neighbour must let the hero step towards this tile, and it must be of the same kind of terrain:
            // a region never crosses the shoreline.
            if ((newTile.Passable & GetDirectionBitmask(direction, reflect: true)) == Direction.Unknown ||
                newTile.IsWater != region.IsWater)
            {
                continue;
            }

            if (newTile.Type == MapRegionNodes.Open)
            {
                newTile.Type = region.Id;

                data[neighbourIndex] = newTile;
                region.Nodes.Add(newTile);
            }
            else if (newTile.Type > MapRegionNodes.Found && newTile.Type != region.Id)
            {
                region.Neighbours.Add(newTile.Type);
            }
        }
    }

    /// <summary>
    /// Lets one region process the nodes it has, <c>RegionExpansion()</c> (<c>world_regions.cpp:116</c>).
    /// </summary>
    /// <remarks>
    /// Only the nodes that existed when the round started are processed, so a region grows one tile per round no
    /// matter how many nodes it already has.
    /// </remarks>
    /// <param name="data">The working copy of the map.</param>
    /// <param name="width">Width of the working copy.</param>
    /// <param name="region">The region that is growing.</param>
    /// <param name="offsets">Step offsets of the working copy.</param>
    private static void RegionExpansion(MapRegionNode[] data, int width, MapRegion region, int[] offsets)
    {
        int nodesEnd = region.Nodes.Count;

        while (region.LastProcessedNode < nodesEnd)
        {
            CheckAdjacentTiles(data, region, width, offsets);

            ++region.LastProcessedNode;
        }
    }

    /// <summary>
    /// Gives every passable tile that no region claimed a region of its own, <c>FindMissingRegions()</c>
    /// (<c>world_regions.cpp:127</c>).
    /// </summary>
    /// <remarks>
    /// The reference leaves the type of the seed tile <c>REGION_NODE_OPEN</c> here, so when the region grows around
    /// it the seed is claimed a second time: the region that a lake in the middle of a continent gets lists that one
    /// tile twice (<c>world_regions.cpp:136-151</c> never writes <c>currentTile->type</c>). The port writes it, so
    /// <see cref="MapRegion.Nodes"/> holds exactly the tiles of the region — every count taken off it, by the tests
    /// and later by the AI, would be off by one otherwise.
    /// </remarks>
    /// <param name="data">The working copy of the map.</param>
    /// <param name="width">Map width.</param>
    /// <param name="height">Map height.</param>
    /// <param name="regions">The region list to append to.</param>
    private static void FindMissingRegions(MapRegionNode[] data, int width, int height, List<MapRegion> regions)
    {
        int extendedWidth = width + 2;
        int[] offsets = GetDirectionOffsets(extendedWidth);
        int mapEnd = extendedWidth * (height + 1);

        for (int index = extendedWidth + 1; index < mapEnd; ++index)
        {
            if (data[index].Type != MapRegionNodes.Open)
            {
                continue;
            }

            uint regionId = (uint)regions.Count;
            var region = new MapRegion(regionId, data[index].Index, data[index].IsWater, extendedWidth);

            regions.Add(region);

            MapRegionNode seed = data[index];
            seed.Type = regionId;
            data[index] = seed;

            do
            {
                CheckAdjacentTiles(data, region, extendedWidth, offsets);

                ++region.LastProcessedNode;
            }
            while (region.LastProcessedNode != region.Nodes.Count);
        }
    }
}
