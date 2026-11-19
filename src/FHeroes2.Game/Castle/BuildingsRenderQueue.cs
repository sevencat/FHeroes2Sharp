using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Castle;

/// <summary>One building of the town view and the rectangles it covers, <c>CastleDialog::BuildingRenderInfo</c>.</summary>
/// <remarks>
/// The areas are already offset to the screen: the queue adds the town screen's top-left once, so the drawing code
/// never has to add it again per blit.
/// </remarks>
public sealed class BuildingRenderInfo
{
    /// <summary>Wraps a building and the places it draws at, <c>BuildingRenderInfo( BuildingType, areas )</c>.</summary>
    /// <param name="id">The building.</param>
    /// <param name="areas">The screen rectangles it covers.</param>
    public BuildingRenderInfo(BuildingType id, IReadOnlyList<Rect> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        Id = id;
        Areas = areas;
    }

    /// <summary>The building, <c>id</c>.</summary>
    public BuildingType Id { get; }

    /// <summary>The screen rectangles it covers, <c>areas</c>.</summary>
    public IReadOnlyList<Rect> Areas { get; }
}

/// <summary>
/// Every building of the town view in the order it is drawn, <c>CastleDialog::BuildingsRenderQueue</c>
/// (<c>castle_building.cpp:464-477</c>).
/// </summary>
/// <remarks>
/// The order comes from <see cref="CastleBuildingInfo.GetDrawingPriorities"/> and is the whole point of the type:
/// a later building overlaps an earlier one, so the queue is not a set. The version matters because Price of
/// Loyalty adds a few buildings to some races.
/// </remarks>
public sealed class BuildingsRenderQueue : IReadOnlyList<BuildingRenderInfo>
{
    private readonly BuildingRenderInfo[] _buildings;

    /// <summary>Builds the queue for a town, <c>BuildingsRenderQueue( castle, top )</c>.</summary>
    /// <param name="castle">The town whose buildings are drawn.</param>
    /// <param name="top">The screen position of the town view's top-left corner.</param>
    public BuildingsRenderQueue(Castle castle, Point top)
    {
        ArgumentNullException.ThrowIfNull(castle);

        IReadOnlyList<BuildingType> order = CastleBuildingInfo.GetDrawingPriorities(castle.Race, castle.Version);
        _buildings = new BuildingRenderInfo[order.Count];

        for (int i = 0; i < order.Count; ++i)
        {
            IReadOnlyList<Rect> areas = CastleBuildingInfo.GetArea(castle.Race, order[i]);
            Rect[] placed = new Rect[areas.Count];

            for (int areaIndex = 0; areaIndex < areas.Count; ++areaIndex)
            {
                Rect area = areas[areaIndex];

                placed[areaIndex] = new Rect(area.X + top.X, area.Y + top.Y, area.Width, area.Height);
            }

            _buildings[i] = new BuildingRenderInfo(order[i], placed);
        }
    }

    /// <inheritdoc/>
    public int Count => _buildings.Length;

    /// <inheritdoc/>
    public BuildingRenderInfo this[int index] => _buildings[index];

    /// <inheritdoc/>
    public IEnumerator<BuildingRenderInfo> GetEnumerator() => ((IEnumerable<BuildingRenderInfo>)_buildings).GetEnumerator();

    /// <inheritdoc/>
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => _buildings.GetEnumerator();
}

/// <summary>
/// The questions the town view's drawing loop asks before blitting, <c>castle_building.cpp:74-98</c>.
/// </summary>
/// <remarks>
/// The reference answers them inside the drawing functions, mixed with the blits. The port keeps the answers here so
/// they can be checked without a renderer: which buildings are visible, which extra wall or bridge pieces belong to a
/// building, and whether a building is still fading in.
/// </remarks>
public static class CastleBuildingRender
{
    /// <summary>
    /// Whether a building needs its connecting piece drawn, <c>isBuildingConnectionNeeded()</c>.
    /// </summary>
    /// <remarks>
    /// Three towns have a piece that belongs to two buildings at once: the barbarian mage guild and the thieves' guild
    /// (a wall each), and the necromancer captain's quarters (a bridge to the castle). The piece is drawn once, on
    /// the building whose turn in the queue makes it visible last.
    /// </remarks>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building that has just been drawn.</param>
    /// <returns><see langword="true"/> when an extra piece must follow the building.</returns>
    public static bool IsBuildingConnectionNeeded(Castle castle, BuildingType building)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (castle.Race == MapRace.Barbarian)
        {
            if ((building & BuildingType.MageGuild) != BuildingType.Nothing)
            {
                int level = castle.GetLevelMageGuild();

                // Only the highest mage guild level is ever built, so the level always covers what was asked.
                Debug.Assert(level > 0, "a mage guild is asked about only once it exists");

                return building == (BuildingType)((int)BuildingType.MageGuild1 << (level - 1));
            }

            if (building == BuildingType.ThievesGuild)
            {
                return true;
            }
        }
        else if (castle.Race == MapRace.Necromancer && building == BuildingType.Captain)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a building is up and finished, <c>isBuildingFullyBuilt()</c>.
    /// </summary>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building to test.</param>
    /// <param name="underConstruction">The building that is currently fading in, or
    /// <see cref="BuildingType.Nothing"/>.</param>
    /// <returns><see langword="true"/> when it is built and not the one still fading.</returns>
    public static bool IsBuildingFullyBuilt(Castle castle, BuildingType building, BuildingType underConstruction)
    {
        ArgumentNullException.ThrowIfNull(castle);

        return castle.IsBuild(building) && building != underConstruction;
    }

    /// <summary>
    /// Whether a building of the queue is drawn this frame, the two skip tests of <c>redrawCastleBuildings()</c>.
    /// </summary>
    /// <remarks>
    /// A built building is hidden when its upgrade is built, because the upgrade's sprite includes the base one.
    /// The exception is the upgrade that is still fading in: the old sprite stays until the new one is opaque, so
    /// both are drawn while the fade lasts.
    /// </remarks>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building to test.</param>
    /// <param name="fadingIn">The building that is fading in, or <see cref="BuildingType.Nothing"/>.</param>
    /// <returns><see langword="true"/> when the building belongs in this frame.</returns>
    public static bool ShouldDrawBuilding(Castle castle, BuildingType building, BuildingType fadingIn)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (!castle.IsBuild(building))
        {
            return false;
        }

        BuildingType upgrade = CastleBuildings.GetUpgradeForBuilding(castle.Race, building);

        return upgrade == building
            || !castle.IsBuild(upgrade)
            || (fadingIn != BuildingType.Nothing && upgrade == fadingIn);
    }
}
