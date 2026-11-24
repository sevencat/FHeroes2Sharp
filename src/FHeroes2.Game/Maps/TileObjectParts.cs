using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The removal of an object from a tile and the debug helpers of <c>Maps::Tile</c>:
/// <c>maps_tiles.cpp:300-317</c> (<c>getObjectLayerName</c>), <c>:354-390</c>
/// (<c>getObjectPartInfo</c>), <c>:462-465</c> (<c>GetCenter</c>), <c>:885-1010</c> (<c>String</c>) and
/// <c>:1450-1509</c> (<c>removeObjectPartsByUID</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Removal is the interesting half.</b> Deleting parts is easy; what the reference spends forty lines
/// on is everything that has to be recomputed afterwards — the object type, the passability of the tile
/// <em>and of its eight neighbours</em>, and the type a hero standing on the tile hides. Skipping any of
/// those leaves a map that looks right and walks wrong, which is why the port keeps the whole sequence.
/// </para>
/// <para>
/// The two debug helpers are ported in a reduced form: the reference prints its own object names
/// (<c>MP2::StringObject</c>, <c>Ground::String</c>, <c>Direction::String</c>, <c>Color::String</c>) and
/// the <c>String()</c> of a hero and a castle, none of which exist in the port yet. What is printed here is
/// the numeric value plus the name of the port's enum, which carries the same information.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// The absolute point of the tile, <c>GetCenter()</c> (<c>maps_tiles.cpp:462</c>).
    /// </summary>
    /// <remarks>
    /// The reference calls <c>Maps::GetPoint( _index )</c>, which reads the global map width; the port
    /// takes it from the geometry of the world — the same trade every other <c>maps</c> type makes.
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns>The column and row of the tile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The world has no map.</exception>
    public Point GetCenter(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        MapGeometry geometry = world.Geometry ?? throw new InvalidOperationException("The world has no map yet.");

        return geometry.GetPoint(Index);
    }

    /// <summary>
    /// Removes every part of one object from this tile and repairs what the removal invalidates,
    /// <c>removeObjectPartsByUID()</c> (<c>maps_tiles.cpp:1450</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The UID is what identifies an object</b>, not the type and not the sprite: a multi tile object
    /// keeps one UID on every piece, so a single call takes the whole object off this tile. The main part
    /// participates — when it is the one removed, the ground parts are re-sorted so that the object with
    /// the highest rendering priority takes its place.
    /// </para>
    /// <para>
    /// <b>The neighbours are recomputed too.</b> A tall object forbids walking into the tile above it, so
    /// removing it opens a route that the neighbour has already recorded as closed. The reference sets the
    /// initial passability of the eight neighbours and then runs the narrowing pass on them, after doing
    /// the same for the tile itself; the port keeps that order.
    /// </para>
    /// <para>
    /// A hero standing on the tile keeps standing there: the reference parks the new object type on the
    /// hero and puts <see cref="Mp2ObjectType.ActionHero"/> back on the tile, so the hero does not turn
    /// into the object it was hiding.
    /// </para>
    /// <para>
    /// The reference leaves a note about resetting the metadata of a tile that just lost its only object
    /// with extra data. That is not done there either, and the port keeps the gap.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="objectUid">UID of the object to remove.</param>
    /// <returns><see langword="true"/> when at least one part was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public bool RemoveObjectPartsByUID(World world, uint objectUid)
    {
        ArgumentNullException.ThrowIfNull(world);

        bool isObjectPartRemoved = _groundObjectParts.RemoveAll(part => part.Uid == objectUid) > 0;
        isObjectPartRemoved |= _topObjectParts.RemoveAll(part => part.Uid == objectUid) > 0;

        if (_mainObjectPart.Uid == objectUid)
        {
            _mainObjectPart = new ObjectPart();

            // The main part is gone: the object with the highest rendering priority among the ground
            // parts has to take its place.
            SortObjectParts();

            isObjectPartRemoved = true;
        }

        if (!isObjectPartRemoved)
        {
            return false;
        }

        UpdateObjectType(world);

        SetInitialPassability();

        // The neighbours may have been blocked by this object, so they start over from their own parts.
        List<int> tilesAround = MapGeometry.GetAroundIndexes(Index, world.Width, world.Height, 1);

        foreach (int tileIndex in tilesAround)
        {
            world.GetTile(tileIndex).SetInitialPassability();
        }

        UpdatePassability(world);

        foreach (int tileIndex in tilesAround)
        {
            world.GetTile(tileIndex).UpdatePassability(world);
        }

        if (Hero.IsValidId(_occupantHeroId))
        {
            Hero? hero = world.GetHero(_occupantHeroId);

            if (hero is not null)
            {
                hero.SetObjectTypeUnderHero(_mainObjectType);

                SetMainObjectType(Mp2ObjectType.ActionHero);
                world.ResetPathfinder();
            }
        }

        return true;
    }

    /// <summary>
    /// The readable name of a layer, <c>getObjectLayerName()</c> (<c>maps_tiles.cpp:300</c>).
    /// </summary>
    /// <param name="layerType">Layer of an object part.</param>
    /// <returns>The name the reference prints in its tile dump.</returns>
    public static string GetObjectLayerName(ObjectLayerType layerType) =>
        layerType switch
        {
            ObjectLayerType.Object => "Object layer",
            ObjectLayerType.Background => "Background layer",
            ObjectLayerType.Shadow => "Shadow layer",
            ObjectLayerType.Terrain => "Terrain layer",
            _ => "Unknown layer",
        };

    /// <summary>
    /// One object part as the lines of the tile dump, <c>getObjectPartInfo()</c>
    /// (<c>maps_tiles.cpp:354</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parameter is the level the part was found on, because the layer the part itself claims has to
    /// agree with it: a top level part on the object layer is fine, a top level part on the terrain layer
    /// is a broken map and is printed with the reference's <c>!!! INVALID !!!</c> marker.
    /// </para>
    /// <para>
    /// An empty part stops after the layer and the shadow line, exactly like the reference: there is no
    /// sprite to look up.
    /// </para>
    /// </remarks>
    /// <param name="part">The part to describe.</param>
    /// <param name="isGroundLayer">
    /// <see langword="true"/> when the part lives in the main slot or on the ground level, because only a
    /// top level part is required to be on the object layer.
    /// </param>
    /// <returns>Several lines of text, each terminated by a newline.</returns>
    public static string GetObjectPartInfo(ObjectPart part, bool isGroundLayer)
    {
        var text = new StringBuilder();

        text.AppendLine($"UID             : {part.Uid}");
        text.AppendLine($"ICN object type : {(int)part.IcnType} ({Mp2ObjectIcnTypes.GetArchiveName(part.IcnType) ?? "UNKNOWN.ICN"})");
        text.AppendLine($"image index     : {part.IcnIndex}");

        if (isGroundLayer || part.LayerType == ObjectLayerType.Object)
        {
            text.AppendLine($"layer type      : {(int)part.LayerType} - {GetObjectLayerName(part.LayerType)}");
        }
        else
        {
            text.AppendLine($"!!! INVALID !!! layer type : {(int)part.LayerType} - {GetObjectLayerName(part.LayerType)}");
        }

        text.AppendLine($"is shadow       : {(TileObjectTraits.IsObjectPartShadow(part) ? "yes" : "no")}");

        if (part == new ObjectPart())
        {
            return text.ToString();
        }

        Mp2ObjectType objectType = MapObjectCatalog.GetObjectTypeByIcn(part.IcnType, part.IcnIndex);

        if (objectType == Mp2ObjectType.None)
        {
            text.AppendLine("!!! UNKNOWN OBJECT PART !!!");

            return text.ToString();
        }

        text.AppendLine($"MP2 object type : {(int)objectType} ({objectType})");

        return text.ToString();
    }

    /// <summary>
    /// The whole tile as the multi line dump of <c>Tile::String()</c> (<c>maps_tiles.cpp:885</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference has no argument because <c>world</c> is a global; the port asks for it, which is what
    /// the point and the guards need.
    /// </para>
    /// <para>
    /// <b>Reduced compared to the reference.</b> The dump of a hero and of a castle comes from
    /// <c>Heroes::String()</c> and <c>Castle::String()</c>, which do not exist in the port yet, and the
    /// names of the objects, of the ground and of the directions are the reference's own string tables.
    /// What is printed here is everything the tile itself knows, with the port's enum names.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns>The dump, one line per fact.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public string ToDebugString(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var text = new StringBuilder();

        text.AppendLine("******* Tile info *******");
        text.AppendLine($"index           : {Index}, point: {GetCenter(world)}");
        text.AppendLine($"MP2 object type : {(int)_mainObjectType} ({_mainObjectType})");
        text.AppendLine($"region Id       : {_region}");
        text.AppendLine($"ground type     : {Ground} (isRoad: {_isTileMarkedAsRoad})");
        text.AppendLine($"ground img index: {_terrainImageIndex}, image flags: {_terrainFlags}");
        text.AppendLine($"passable from   : {(_tilePassabilityDirections == Direction.Unknown ? "nowhere" : ((int)_tilePassabilityDirections).ToString())}");
        text.AppendLine($"metadata value 1: {_metadata[0]}");
        text.AppendLine($"metadata value 2: {_metadata[1]}");
        text.AppendLine($"metadata value 3: {_metadata[2]}");

        if (_mainObjectType == Mp2ObjectType.ActionBoat)
        {
            text.AppendLine($"boat owner color: {_boatOwnerColor}");
        }

        text.AppendLine("--------- Main object part --------");
        text.Append(GetObjectPartInfo(_mainObjectPart, true));

        foreach (ObjectPart part in _groundObjectParts)
        {
            text.Append(GetObjectPartInfo(part, true));
        }

        foreach (ObjectPart part in _topObjectParts)
        {
            text.Append(GetObjectPartInfo(part, false));
        }

        var extra = new StringBuilder();

        switch (Mp2ObjectTypes.NonActionType(_mainObjectType))
        {
        case Mp2ObjectType.Hero:
            extra.AppendLine($"hero id         : {_occupantHeroId}");
            break;

        case Mp2ObjectType.Castle:
            extra.AppendLine($"castle entrance : {world.IsValidCastleEntrance(Index)}");
            break;

        default:
            if (IsMonsterCountHolder(Mp2ObjectTypes.NonActionType(_mainObjectType)))
            {
                extra.AppendLine($"monster count   : {GetMonsterCount(world)}");
            }

            List<int> protectors = world.GetMonstersProtectingTile(Index);

            if (protectors.Count > 0)
            {
                extra.AppendLine($"protection      : {string.Join(", ", protectors)}");
            }

            break;
        }

        if (extra.Length > 0)
        {
            text.AppendLine("--- Extra information ---");
            text.Append(extra);
        }

        return text.ToString();
    }
}
