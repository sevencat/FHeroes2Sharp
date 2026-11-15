using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The movement costs and the terrain image predicates of <c>Maps::Ground</c>
/// (<c>ground.h:72-76</c>, <c>ground.cpp:96-141</c>, <c>ground.cpp:169-242</c>).
/// </summary>
/// <remarks>
/// The movement of a hero is charged in hundredths of a movement point per tile, and the terrain is the
/// multiplier: a grass tile costs <see cref="DefaultGroundPenalty"/>, a road
/// <see cref="RoadPenalty"/>, and the slow terrains cost more unless the hero has learned pathfinding.
/// The pathfinder of the reference multiplies the penalty by the tile distance (100 for a straight step,
/// 141 for a diagonal one) and divides by a hundred, which is why the table is in percent.
/// </remarks>
public static partial class MapGrounds
{
    /// <summary>The cost of a road tile, <c>roadPenalty</c>.</summary>
    public const uint RoadPenalty = 75;

    /// <summary>The cost of an ordinary tile, <c>defaultGroundPenalty</c>.</summary>
    public const uint DefaultGroundPenalty = 100;

    /// <summary>
    /// The cheapest step the movement accounting has to handle, <c>fastestMovePenalty</c>: the road.
    /// </summary>
    /// <remarks>
    /// The reference defines it as <c>roadPenalty</c> rather than as a number of its own; the pair with
    /// <see cref="SlowestMovePenalty"/> is what sizes the single byte the hero's remaining movement is
    /// stored in on a save file.
    /// </remarks>
    public const uint FastestMovePenalty = RoadPenalty;

    /// <summary>The most expensive step the penalty table can produce, <c>slowestMovePenalty</c>.</summary>
    public const uint SlowestMovePenalty = 200;

    /// <summary>
    /// The movement cost of a tile of <paramref name="ground"/>, <c>Maps::Ground::GetPenalty()</c>
    /// (<c>ground.cpp:169</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference returns <c>defaultGroundPenalty + bonus</c> where the bonus depends on the ground and
    /// on the pathfinding skill; the port keeps that shape so that the table stays readable next to the
    /// comment the reference puts above it:
    /// </para>
    /// <code>
    /// //              none   basc   advd   expr
    /// //    Desert    2.00   1.75   1.50   1.00
    /// //    Swamp     1.75   1.50   1.25   1.00
    /// //    Snow      1.50   1.25   1.00   1.00
    /// //    Wasteland 1.25   1.00   1.00   1.00
    /// //    Beach     1.25   1.00   1.00   1.00
    /// //    Lava      1.00   1.00   1.00   1.00
    /// //    Dirt      1.00   1.00   1.00   1.00
    /// //    Grass     1.00   1.00   1.00   1.00
    /// //    Water     1.00   1.00   1.00   1.00
    /// //    Road      0.75   0.75   0.75   0.75
    /// </code>
    /// <para>
    /// The road is not part of this function: it is a property of the <b>tile</b>
    /// (<c>Tile.IsRoad()</c>), and the pathfinder replaces the ground penalty with
    /// <see cref="RoadPenalty"/> when the step leaves a road tile. A hero on a boat also pays
    /// <see cref="DefaultGroundPenalty"/> for water, so the water row of the table is the ordinary one.
    /// </para>
    /// </remarks>
    /// <param name="ground">Ground of the tile, <c>tile.GetGround()</c>.</param>
    /// <param name="pathfindingLevel">Level of the hero's pathfinding skill.</param>
    /// <returns>Penalty in hundredths of a movement point.</returns>
    public static uint GetPenalty(MapGround ground, SkillLevel pathfindingLevel) => ground switch
    {
        MapGround.Desert => pathfindingLevel switch
        {
            SkillLevel.Expert => DefaultGroundPenalty,
            SkillLevel.Advanced => DefaultGroundPenalty + 50,
            SkillLevel.Basic => DefaultGroundPenalty + 75,
            _ => DefaultGroundPenalty + 100,
        },

        MapGround.Swamp => pathfindingLevel switch
        {
            SkillLevel.Expert => DefaultGroundPenalty,
            SkillLevel.Advanced => DefaultGroundPenalty + 25,
            SkillLevel.Basic => DefaultGroundPenalty + 50,
            _ => DefaultGroundPenalty + 75,
        },

        MapGround.Snow => pathfindingLevel switch
        {
            SkillLevel.Expert or SkillLevel.Advanced => DefaultGroundPenalty,
            SkillLevel.Basic => DefaultGroundPenalty + 25,
            _ => DefaultGroundPenalty + 50,
        },

        MapGround.Wasteland or MapGround.Beach => pathfindingLevel == SkillLevel.None
            ? DefaultGroundPenalty + 25
            : DefaultGroundPenalty,

        _ => DefaultGroundPenalty,
    };

    /// <summary>
    /// Whether the terrain image draws a transition to another ground, <c>isTerrainTransitionImage()</c>
    /// (<c>ground.cpp:96</c>).
    /// </summary>
    /// <remarks>
    /// Each block of a ground starts with its transition images and only then reaches the plain ones:
    /// water and dirt have 16 of them, the others 38, and beach has none at all because its whole block
    /// is plain sand. The editor uses this to reject a transition image as the base of a tile, and the
    /// map loader uses it to decide whether a tile has to be re-derived.
    /// </remarks>
    /// <param name="terrainImageIndex">Index in <c>GROUND32.TIL</c>.</param>
    /// <returns><see langword="true"/> for a transition image.</returns>
    public static bool IsTerrainTransitionImage(ushort terrainImageIndex)
    {
        MapGround ground = FromTerrainImageIndex(terrainImageIndex);

        return ground switch
        {
            MapGround.Water or MapGround.Dirt => terrainImageIndex < 16 + StartImageIndex(ground),
            MapGround.Grass or MapGround.Snow or MapGround.Swamp or MapGround.Lava or MapGround.Desert
                or MapGround.Wasteland => terrainImageIndex < 38 + StartImageIndex(ground),
            MapGround.Beach => false,
            _ => false,
        };
    }

    /// <summary>
    /// Whether the terrain image draws a decoration of its own — a rock, a bush, a skeleton — instead of
    /// plain ground, <c>doesTerrainImageIndexContainEmbeddedObjects()</c> (<c>ground.cpp:119</c>).
    /// </summary>
    /// <remarks>
    /// The boundary is a per ground offset past the start of the block: 45 for grass, swamp, snow,
    /// wasteland, lava and desert, 23 for dirt and 7 for beach. Water has none. A tile whose ground image
    /// carries a decoration cannot take a boat: that is what
    /// <c>Tile.IsSuitableForSummoningBoat</c> ends up asking.
    /// </remarks>
    /// <param name="terrainImageIndex">Index in <c>GROUND32.TIL</c>.</param>
    /// <returns><see langword="true"/> for an image with an embedded decoration.</returns>
    public static bool ContainsEmbeddedObjects(ushort terrainImageIndex)
    {
        MapGround ground = FromTerrainImageIndex(terrainImageIndex);

        return ground switch
        {
            MapGround.Water => false,
            MapGround.Grass or MapGround.Snow or MapGround.Swamp or MapGround.Wasteland or MapGround.Lava
                or MapGround.Desert => terrainImageIndex > 45 + StartImageIndex(ground),
            MapGround.Dirt => terrainImageIndex > 23 + StartImageIndex(ground),
            MapGround.Beach => terrainImageIndex > 7 + StartImageIndex(ground),
            _ => false,
        };
    }
}
