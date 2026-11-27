using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The adventure map object catalogue, the port of <c>src/fheroes2/maps/map_object_info.cpp</c>.
///
/// <para>
/// One table per <see cref="ObjectGroup"/> plus an index from an ICN part back to the object part
/// that uses it. The reference builds the tables once at start-up behind a
/// <c>static bool isPopulated</c>; here the static constructor does the same job with the CLR's
/// once-only guarantee, so callers never see a half-filled table.
/// </para>
///
/// <para>
/// The tables are read-only to callers, but the <see cref="ObjectInfo"/> instances are not, because
/// the reference patches <see cref="ObjectPartInfo.AnimationFrames"/> in place after the tables are
/// built (animated objects such as mills).
/// </para>
/// </summary>
public static partial class MapObjectCatalog
{
    /// <summary>
    /// The largest area occupied by the parts of a multi-tile action object,
    /// <c>maxActionGroundObjectDimensions</c>. Only used to validate the tables in debug builds of the
    /// reference.
    /// </summary>
    public static readonly Size MaxActionGroundObjectDimensions = new(4, 2);

    /// <summary>
    /// The largest area occupied by the parts of any object, <c>maxObjectDimensions</c>.
    /// </summary>
    public static readonly Size MaxObjectDimensions = new(8, 5);

    /// <summary>
    /// <c>objectData</c> of the reference, one list per group, indexed by <see cref="ObjectGroup"/>.
    /// </summary>
    private static readonly List<ObjectInfo>[] ObjectData = CreateObjectData();

    /// <summary>
    /// <c>objectInfoByIcn</c> of the reference: first part registered for an ICN image wins, which is
    /// the <c>try_emplace</c> semantics. Duplicates are accepted.
    /// </summary>
    private static readonly Dictionary<(Mp2ObjectIcnType IcnType, uint IcnIndex), ObjectPartInfo> ObjectInfoByIcn = [];

    /// <summary>
    /// The group that registered a given ICN image, built in the same pass and with the same "first wins" rule as
    /// <see cref="ObjectInfoByIcn"/>. The editor needs it to name what a tile holds: a tile stores an ICN type and a
    /// sprite index, not a group.
    /// </summary>
    private static readonly Dictionary<(Mp2ObjectIcnType IcnType, uint IcnIndex), ObjectGroup> GroupByIcn = [];

    /// <summary>
    /// The address of the object that registered a given ICN image, <see cref="MapObjectIndex"/>: the group and the
    /// position inside it. Built in the same pass and with the same "first wins" rule as
    /// <see cref="ObjectInfoByIcn"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="GroupByIcn"/> only answers the first half; saving a map in the <c>.fh2m</c> format needs both halves,
    /// because a tile of that format stores <c>( group, index )</c> and nothing about the ICN.
    /// </remarks>
    private static readonly Dictionary<(Mp2ObjectIcnType IcnType, uint IcnIndex), MapObjectIndex> ObjectIndexByIcn = [];

    static MapObjectCatalog() => PopulateObjectData();

    /// <summary>
    /// All objects of a group in the order the editor and the map format expect, <c>getObjectsByGroup</c>.
    /// </summary>
    /// <param name="group">Group to fetch. <see cref="ObjectGroup.GroupCount"/> is not a group.</param>
    /// <returns>The table of the group; it is never modified afterwards.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a group.</exception>
    public static IReadOnlyList<ObjectInfo> GetObjectsByGroup(ObjectGroup group)
    {
        if (group >= ObjectGroup.GroupCount)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Not an object group.");
        }

        return ObjectData[(int)group];
    }

    /// <summary>
    /// One object of a group, <c>getObjectInfo</c>. The reference asserts on a bad index and returns
    /// an empty object; a caller that gets it wrong here is a bug just the same.
    /// </summary>
    /// <param name="group">Group to fetch from.</param>
    /// <param name="objectIndex">Index inside the group.</param>
    /// <returns>The object at that index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group or the index does not exist.</exception>
    public static ObjectInfo GetObjectInfo(ObjectGroup group, int objectIndex)
    {
        IReadOnlyList<ObjectInfo> objects = GetObjectsByGroup(group);

        if (objectIndex < 0 || objectIndex >= objects.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(objectIndex), objectIndex, "No such object in the group.");
        }

        return objects[objectIndex];
    }

    /// <summary>
    /// The object part that draws a given ICN image, <c>getObjectPartByIcn</c>.
    /// </summary>
    /// <param name="icnType">ICN resource.</param>
    /// <param name="icnIndex">Sprite index inside the resource.</param>
    /// <returns>The first registered part, or <see langword="null"/> when no object uses that image.</returns>
    public static ObjectPartInfo? GetObjectPartByIcn(Mp2ObjectIcnType icnType, uint icnIndex) =>
        ObjectInfoByIcn.TryGetValue((icnType, icnIndex), out ObjectPartInfo? part) ? part : null;

    /// <summary>
    /// The object type that a given ICN image belongs to, <c>getObjectTypeByIcn</c>; the reference
    /// returns <c>OBJ_NONE</c> for an unknown image.
    /// </summary>
    /// <param name="icnType">ICN resource.</param>
    /// <param name="icnIndex">Sprite index inside the resource.</param>
    /// <returns>The object type of the part, or <see cref="Mp2ObjectType.None"/>.</returns>
    public static Mp2ObjectType GetObjectTypeByIcn(Mp2ObjectIcnType icnType, uint icnIndex) =>
        GetObjectPartByIcn(icnType, icnIndex)?.ObjectType ?? Mp2ObjectType.None;

    /// <summary>
    /// The editor group that draws a given ICN image. A tile stores the ICN type and the sprite index of its two
    /// object parts and nothing else, so this is how the editor names what is on it — the eraser asks it which group
    /// a tile belongs to, and the reference asks the same question of its object records.
    /// </summary>
    /// <param name="icnType">ICN resource of the part.</param>
    /// <param name="icnIndex">Sprite index inside the resource.</param>
    /// <returns>The group of the first object that registered the image, or <see cref="ObjectGroup.None"/>.</returns>
    public static ObjectGroup GetObjectGroupByIcn(Mp2ObjectIcnType icnType, uint icnIndex) =>
        GroupByIcn.TryGetValue((icnType, icnIndex), out ObjectGroup group) ? group : ObjectGroup.None;

    /// <summary>The group of the ground part of a tile, <see cref="ObjectGroup.None"/> when it has no known part.</summary>
    /// <param name="tile">Tile to look at.</param>
    /// <returns>The group of the part drawn at ground level.</returns>
    public static ObjectGroup GetGroundGroup(Mp2TileInfo tile) =>
        tile.HasGroundObject ? GetObjectGroupByIcn(tile.GroundIcnType, tile.BottomIcnImageIndex) : ObjectGroup.None;

    /// <summary>
    /// The address of the object that draws a given ICN image, the <c>( group, index )</c> pair the <c>.fh2m</c>
    /// format stores for a tile.
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetObjectGroupByIcn"/> 同一趟构建、同一条「先到先得」规则，只是多带回组内下标。存图时瓦片上只有
    /// ICN 类型与帧号，而 <c>.fh2m</c> 只认这个二元组，所以两者都要。
    /// </remarks>
    /// <param name="icnType">ICN resource of the part.</param>
    /// <param name="icnIndex">Sprite index inside the resource.</param>
    /// <param name="index">The address, when the image belongs to a known object.</param>
    /// <returns><see langword="true"/> when an object registered the image.</returns>
    public static bool TryGetObjectIndex(Mp2ObjectIcnType icnType, uint icnIndex, out MapObjectIndex index) =>
        ObjectIndexByIcn.TryGetValue((icnType, icnIndex), out index);

    /// <summary>How many objects a group has.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The size of its table.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a group.</exception>
    public static int GetObjectCount(ObjectGroup group) => GetObjectsByGroup(group).Count;

    /// <summary>The group of the top part of a tile, <see cref="ObjectGroup.None"/> when it has no known part.</summary>
    /// <param name="tile">Tile to look at.</param>
    /// <returns>The group of the part drawn on the top level.</returns>
    public static ObjectGroup GetTopGroup(Mp2TileInfo tile) =>
        tile.HasTopObject ? GetObjectGroupByIcn(tile.TopIcnType, tile.TopIcnImageIndex) : ObjectGroup.None;

    /// <summary>
    /// Tile offsets that an object occupies, <c>getGroundLevelOccupiedTileOffset</c>: only parts on the
    /// object and background layers block a tile.
    /// </summary>
    /// <param name="info">Object to inspect; it must have ground level parts.</param>
    /// <returns>Offsets relative to the main object tile.</returns>
    public static List<Point> GetGroundLevelOccupiedTileOffset(ObjectInfo info)
    {
        List<Point> offsets = [];

        foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
        {
            if (part.LayerType is ObjectLayerType.Object or ObjectLayerType.Background)
            {
                offsets.Add(part.TileOffset);
            }
        }

        return offsets;
    }

    /// <summary>
    /// Tile offsets that an object uses, <c>getGroundLevelUsedTileOffset</c>: everything except the
    /// shadow layer, shadows do not belong to the object itself.
    /// </summary>
    /// <param name="info">Object to inspect; it must have ground level parts.</param>
    /// <returns>Offsets relative to the main object tile.</returns>
    public static List<Point> GetGroundLevelUsedTileOffset(ObjectInfo info)
    {
        List<Point> offsets = [];

        foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
        {
            if (part.LayerType != ObjectLayerType.Shadow)
            {
                offsets.Add(part.TileOffset);
            }
        }

        return offsets;
    }

    /// <summary>Allocates one empty list per group, <c>std::array&lt;std::vector&lt;ObjectInfo&gt;, GROUP_COUNT&gt;</c>.</summary>
    /// <returns>The container of all tables.</returns>
    private static List<ObjectInfo>[] CreateObjectData()
    {
        List<ObjectInfo>[] groups = new List<ObjectInfo>[(int)ObjectGroup.GroupCount];

        for (int i = 0; i < groups.Length; ++i)
        {
            groups[i] = [];
        }

        return groups;
    }

    /// <summary>
    /// Fills the tables and the ICN index, <c>populateObjectData</c> of the reference.
    ///
    /// <para>
    /// <b>The order of objects inside a group must be preserved</b>: new objects go to the end. The
    /// call order below is the one of <c>populateObjectData</c> and only matters for the ICN index,
    /// where the first registration wins.
    /// </para>
    /// </summary>
    private static void PopulateObjectData()
    {
        PopulateRoads(ObjectData[(int)ObjectGroup.Roads]);
        PopulateStreams(ObjectData[(int)ObjectGroup.Streams]);

        PopulateLandscapeMountains(ObjectData[(int)ObjectGroup.LandscapeMountains]);
        PopulateLandscapeRocks(ObjectData[(int)ObjectGroup.LandscapeRocks]);
        PopulateLandscapeTrees(ObjectData[(int)ObjectGroup.LandscapeTrees]);
        PopulateLandscapeWater(ObjectData[(int)ObjectGroup.LandscapeWater]);
        PopulateLandscapeMiscellaneous(ObjectData[(int)ObjectGroup.LandscapeMiscellaneous]);

        // These are the extra objects used with the others and never used alone.
        PopulateLandscapeTownBasements(ObjectData[(int)ObjectGroup.LandscapeTownBasements]);
        PopulateLandscapeFlags(ObjectData[(int)ObjectGroup.LandscapeFlags]);

        PopulateAdventureDwellings(ObjectData[(int)ObjectGroup.AdventureDwellings]);
        PopulateAdventureMines(ObjectData[(int)ObjectGroup.AdventureMines]);
        PopulateAdventurePowerUps(ObjectData[(int)ObjectGroup.AdventurePowerUps]);
        PopulateAdventureTreasures(ObjectData[(int)ObjectGroup.AdventureTreasures]);
        PopulateAdventureArtifacts(ObjectData[(int)ObjectGroup.AdventureArtifacts]);
        PopulateAdventureWater(ObjectData[(int)ObjectGroup.AdventureWater]);
        PopulateAdventureMiscellaneous(ObjectData[(int)ObjectGroup.AdventureMiscellaneous]);

        PopulateKingdomHeroes(ObjectData[(int)ObjectGroup.KingdomHeroes]);
        PopulateKingdomTowns(ObjectData[(int)ObjectGroup.KingdomTowns]);
        PopulateMonsters(ObjectData[(int)ObjectGroup.Monsters]);

        PopulateExtraBoatDirections(ObjectData[(int)ObjectGroup.MapExtras]);

        for (int group = 0; group < ObjectData.Length; ++group)
        {
            ObjectGroup groupId = (ObjectGroup)group;
            IReadOnlyList<ObjectInfo> groupObjects = ObjectData[group];

            for (int objectIndex = 0; objectIndex < groupObjects.Count; ++objectIndex)
            {
                ObjectInfo objectInfo = groupObjects[objectIndex];

                // The index is the position inside the group, which is what the .fh2m format stores.
                MapObjectIndex address = new(groupId, (uint)objectIndex);

                foreach (LayeredObjectPartInfo part in objectInfo.GroundLevelParts)
                {
                    ObjectInfoByIcn.TryAdd((part.IcnType, part.IcnIndex), part);
                    GroupByIcn.TryAdd((part.IcnType, part.IcnIndex), groupId);
                    ObjectIndexByIcn.TryAdd((part.IcnType, part.IcnIndex), address);
                }

                foreach (ObjectPartInfo part in objectInfo.TopLevelParts)
                {
                    ObjectInfoByIcn.TryAdd((part.IcnType, part.IcnIndex), part);
                    GroupByIcn.TryAdd((part.IcnType, part.IcnIndex), groupId);
                    ObjectIndexByIcn.TryAdd((part.IcnType, part.IcnIndex), address);
                }
            }
        }
    }
}
