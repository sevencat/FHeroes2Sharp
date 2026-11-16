using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game;

/// <summary>
/// The four values a region node or a region id can take before any region exists,
/// the anonymous enum of <c>world_regions.h:28-34</c>.
/// </summary>
/// <remarks>
/// <para>
/// These are not flags: the reference keeps them in the same integer space as the region ids, which start at
/// <see cref="Found"/> and count upwards. That is why <see cref="MapRegionNode.Type"/> is a <c>uint</c> and not an
/// enum — a node's type is either one of the four constants or the id of the region that claimed it.
/// </para>
/// <para>
/// <see cref="Border"/> is never assigned by the reference either; it is kept so the numbering matches
/// (<c>REGION_NODE_BORDER</c> is the gap the ids start after).
/// </para>
/// </remarks>
public static class MapRegionNodes
{
    /// <summary>The node cannot be walked on, or the tile holds no region (<c>REGION_NODE_BLOCKED</c>).</summary>
    public const uint Blocked = 0;

    /// <summary>The node is passable and belongs to no region yet (<c>REGION_NODE_OPEN</c>).</summary>
    public const uint Open = 1;

    /// <summary>A border node (<c>REGION_NODE_BORDER</c>); unused, see the remarks of the class.</summary>
    public const uint Border = 2;

    /// <summary>The first region id, and the type of every region that exists (<c>REGION_NODE_FOUND</c>).</summary>
    public const uint Found = 3;
}

/// <summary>
/// One tile as the region growing sees it, <c>MapRegionNode</c> (<c>world_regions.h:36-49</c>).
/// </summary>
/// <remarks>
/// <para>
/// A mutable struct, like <see cref="ObjectPart"/>: the growing loop edits the type of a node in the working array
/// and hands out copies of it to the region that claimed it. The array the algorithm works on is zero filled by
/// <c>new</c>, while the reference's default constructor sets <see cref="Index"/> to -1; no code reads the index of
/// a node whose type is <see cref="MapRegionNodes.Blocked"/>, so the difference never shows.
/// </para>
/// </remarks>
public struct MapRegionNode
{
    /// <summary>Creates an open node for a tile, the <c>MapRegionNode( int )</c> constructor.</summary>
    /// <param name="index">Tile index the node stands for.</param>
    public MapRegionNode(int index)
    {
        Index = index;
        Type = MapRegionNodes.Open;
        MapObject = Mp2ObjectType.None;
        Passable = Direction.Unknown;
        IsWater = false;
    }

    /// <summary>Tile index, <c>index</c>; -1 for the padding tiles of the extended map.</summary>
    public int Index { get; set; }

    /// <summary>Region id, or one of the <see cref="MapRegionNodes"/> constants, <c>type</c>.</summary>
    public uint Type { get; set; }

    /// <summary>
    /// The object on the tile when it is one a hero can act on, otherwise <see cref="Mp2ObjectType.None"/>,
    /// <c>mapObject</c>.
    /// </summary>
    /// <remarks>Only stone liths and whirlpools are read back, to connect regions through them.</remarks>
    public Mp2ObjectType MapObject { get; set; }

    /// <summary>The directions a hero may step in, <c>passable</c> — see <see cref="Tile.PassableDirections"/>.</summary>
    public Direction Passable { get; set; }

    /// <summary>Whether the tile is water, <c>isWater</c>.</summary>
    public bool IsWater { get; set; }
}

/// <summary>
/// A patch of connected tiles of one kind (land or water) and the regions next to it,
/// <c>MapRegion</c> (<c>world_regions.h:51-65</c>).
/// </summary>
/// <remarks>
/// The first three entries of <see cref="World"/>'s region list are never grown: ids 0, 1 and 2 are the
/// <see cref="MapRegionNodes"/> constants, and the real regions start at <see cref="MapRegionNodes.Found"/>.
/// </remarks>
public sealed class MapRegion
{
    /// <summary>
    /// The region the reference's default constructor makes: no nodes, no neighbours, and the id
    /// <see cref="MapRegionNodes.Found"/>, which <see cref="World.GetRegion"/> hands out for an id out of range.
    /// </summary>
    public MapRegion() => Id = MapRegionNodes.Found;

    /// <summary>Creates a region around one tile, <c>MapRegion( int, int, bool, size_t )</c>.</summary>
    /// <param name="id">Region id, which is also the type its nodes get.</param>
    /// <param name="mapIndex">Tile the region starts from.</param>
    /// <param name="water">Whether the region grows over water.</param>
    /// <param name="expectedSize">Expected number of tiles, used as the capacity of <see cref="Nodes"/>.</param>
    public MapRegion(uint id, int mapIndex, bool water, int expectedSize)
    {
        Id = id;
        IsWater = water;

        Nodes.Capacity = expectedSize;
        Nodes.Add(new MapRegionNode(mapIndex) { Type = id });
    }

    /// <summary>Region id, <c>_id</c>.</summary>
    public uint Id { get; }

    /// <summary>Whether the region grows over water, <c>_isWater</c>.</summary>
    public bool IsWater { get; }

    /// <summary>
    /// The ids of the regions this one touches, <c>_neighbours</c>.
    /// </summary>
    /// <remarks>
    /// Sorted rather than hashed: the reference's set is ordered, and the AI reads neighbours in that order.
    /// </remarks>
    public SortedSet<uint> Neighbours { get; } = [];

    /// <summary>The tiles of the region, <c>_nodes</c>, in the order they were claimed.</summary>
    public List<MapRegionNode> Nodes { get; } = [];

    /// <summary>How far the growing loop has got through <see cref="Nodes"/>, <c>_lastProcessedNode</c>.</summary>
    public int LastProcessedNode { get; set; }

    /// <summary>How many regions touch this one, <c>MapRegion::getNeighboursCount()</c> (<c>world_regions.cpp:158</c>).</summary>
    public int NeighboursCount => Neighbours.Count;
}
