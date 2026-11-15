using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// One tile of the world map, the port of <c>Maps::Tile</c> (<c>参考：src/fheroes2/maps/maps_tiles.h:99</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not the whole class.</b> The object parts (main, ground, top), the object type cache, the
/// metadata, the terrain image and flags, the road bit, the passability mask, the type repairs of the
/// loader and the hero occupancy are here. Everything that needs the fog, the region map or the ground
/// transition tables is deliberately absent and will be added with the system it belongs to (see
/// <c>docs/DECISIONS.md</c>, ADR-063 / ADR-066 / ADR-067 / ADR-068): boats, fog,
/// <c>removeObjectPartsByUID</c> (its tail touches the neighbours), the ownership flags, <c>String()</c>
/// and the savegame operators.
/// </para>
/// <para>
/// The class is not a struct like the reference: <c>Tile</c> owns three containers, and the
/// reference copies them only for the savegame operators, which are not ported yet.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    private readonly List<ObjectPart> _groundObjectParts = [];
    private readonly List<ObjectPart> _topObjectParts = [];
    private readonly uint[] _metadata = new uint[3];

    private ObjectPart _mainObjectPart = new();
    private int _index;
    private ushort _terrainImageIndex;
    private byte _terrainFlags;
    private Mp2ObjectType _mainObjectType = Mp2ObjectType.None;

    // DIRECTION_ALL of the reference: every step is allowed until the loader narrows it down.
    private Direction _tilePassabilityDirections = Directions.All;

    private bool _isTileMarkedAsRoad;

    // The reference stores this in a uint8_t initialised to Heroes::UNKNOWN (maps_tiles.h:417), which is
    // zero, so a tile starts out as "no hero" without any extra state.
    private byte _occupantHeroId = (byte)Hero.UnknownId;

    // A fresh tile is under the fog of every player until somebody reveals it (maps_tiles.h:421).
    private PlayerColor _fogColors = PlayerColors.All;

    // DIRECTION_ALL until the reveal finds out which neighbours are still hidden (maps_tiles.h:427).
    private Direction _fogDirection = Directions.All;

    /// <summary>
    /// Fills the tile from one <c>MP2TileInfo</c>, the <c>Tile::Init</c> of the reference
    /// (<c>maps_tiles.cpp:392</c>). Only the ground and top object are read; add-ons are handled by
    /// <c>world_loadmap.cpp</c> for the current tile.
    /// </summary>
    /// <param name="tile">Tile structure read from the map file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public void Init(Mp2TileInfo tile)
    {
        // The reference logs a warning when a non action object carries metadata; the value is kept
        // anyway because the loader may reset it later.
        _metadata[0] = tile.Metadata;

        SetMainObjectType(tile.ObjectType);

        if (tile.HasGroundObject)
        {
            PushGroundObjectPart(new ObjectPart((ObjectLayerType)(tile.Quantity1 & 0x03), tile.Level1ObjectUid, tile.GroundIcnType, tile.BottomIcnImageIndex));
        }

        if (tile.HasTopObject)
        {
            // Top level parts have no internal structure, so the reference stores them with the object
            // layer. One crack sprite is an exception and goes to the ground layer with the terrain
            // layer type, which reproduces an original game rendering bug (maps_tiles.cpp:420).
            if (tile.TopIcnType == Mp2ObjectIcnType.ObjnCrck && tile.TopIcnImageIndex == 226)
            {
                PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, tile.Level2ObjectUid, tile.TopIcnType, tile.TopIcnImageIndex));
            }
            else
            {
                PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, tile.Level2ObjectUid, tile.TopIcnType, tile.TopIcnImageIndex));
            }
        }
    }

    /// <summary>Index of the tile on the map, <c>_index</c>.</summary>
    public int Index
    {
        get => _index;
        set => _index = value;
    }

    /// <summary>Image index of the terrain inside the ground's TIL file, <c>_terrainImageIndex</c>.</summary>
    public ushort TerrainImageIndex => _terrainImageIndex;

    /// <summary>Terrain shape and beach bits of the tile, <c>_terrainFlags</c>.</summary>
    public byte TerrainFlags => _terrainFlags;

    /// <summary>
    /// Sets the terrain, <c>setTerrain()</c> of the reference. The reference assigns the flags before
    /// the image index; the order is kept.
    /// </summary>
    /// <param name="terrainImageIndex">Image index inside the ground's TIL file.</param>
    /// <param name="terrainFlags">Terrain shape and beach bits.</param>
    public void SetTerrain(ushort terrainImageIndex, byte terrainFlags)
    {
        _terrainFlags = terrainFlags;
        _terrainImageIndex = terrainImageIndex;
    }

    /// <summary>The ground of the tile, <c>GetGround()</c>: read back from the terrain image index.</summary>
    public MapGround Ground => MapGrounds.FromTerrainImageIndex(_terrainImageIndex);

    /// <summary>
    /// Whether the tile is water, <c>isWater()</c>: every terrain image below the first grass image.
    /// </summary>
    public bool IsWater => _terrainImageIndex < MapGrounds.GrassStartImageIndex;

    /// <summary>
    /// Id of the hero standing on the tile, <c>_occupantHeroId</c>. Zero
    /// (<see cref="Hero.UnknownId"/>) means no hero.
    /// </summary>
    public byte OccupantHeroId => _occupantHeroId;

    /// <summary>
    /// The hero standing on the tile, <c>getHero()</c> (<c>maps_tiles.cpp:429</c>).
    /// </summary>
    /// <param name="world">The world the tile belongs to, the reference's global.</param>
    /// <returns>The hero, or <see langword="null"/> when the tile has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The reference asks for both conditions: the cached type must be <c>MP2::OBJ_HERO</c> <b>and</b> the
    /// stored id must be a hero id, and only then does it look the object up. A tile whose type says
    /// "hero" but whose id is unknown therefore reports no hero — the reference would return
    /// <c>world.GetHeroes( id )</c>, which is a <c>nullptr</c> for such an id anyway.
    /// <c>MP2::OBJ_HERO</c> is the action value (183), i.e. <see cref="Mp2ObjectType.ActionHero"/>;
    /// <see cref="Mp2ObjectType.Hero"/> is the non-action twin that no map stores.
    /// </remarks>
    public Hero? GetHero(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return _mainObjectType == Mp2ObjectType.ActionHero && Hero.IsValidId(_occupantHeroId) ? world.GetHero(_occupantHeroId) : null;
    }

    /// <summary>
    /// Puts a hero on the tile, or takes the hero away when <paramref name="hero"/> is
    /// <see langword="null"/>, <c>setHero()</c> (<c>maps_tiles.cpp:434</c>).
    /// </summary>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="hero">Hero to place, or <see langword="null"/> to remove the one that is there.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Placing a hero <b>parks the type the tile currently shows on the hero</b> and overwrites the cached
    /// type with <see cref="Mp2ObjectType.ActionHero"/> (<c>MP2::OBJ_HERO</c>, the action value).
    /// Removing it puts the parked type back, so the object the hero covered reappears. When the tile
    /// claims to hold a hero that the world does not know, the removal falls back to
    /// <see cref="UpdateObjectType"/>, which re-derives the type from the parts.
    /// </para>
    /// <para>
    /// The reference asserts that the id fits into the byte the tile stores
    /// (<c>hero-&gt;GetID() &lt; 255</c>). <see cref="Hero"/> refuses ids at or above
    /// <see cref="Hero.IdCount"/> (73), so the cast below is always safe and the port has nothing to
    /// assert.
    /// </para>
    /// <para>
    /// Both branches change the cached type, and the reference's <c>setMainObjectType()</c> resets the
    /// pathfinder for that reason (<c>maps_tiles.cpp:477</c>). The port cannot do it there — the setter has
    /// no world — so the reset sits here, at the only call site that has one.
    /// </para>
    /// </remarks>
    public void SetHero(World world, Hero? hero)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (hero is not null)
        {
            hero.SetObjectTypeUnderHero(_mainObjectType);

            _occupantHeroId = (byte)hero.Id;

            SetMainObjectType(Mp2ObjectType.ActionHero);
            world.ResetPathfinder();

            return;
        }

        hero = GetHero(world);

        if (hero is not null)
        {
            SetMainObjectType(hero.ObjectTypeUnderHero);
            world.ResetPathfinder();

            hero.SetObjectTypeUnderHero(Mp2ObjectType.None);
        }
        else
        {
            UpdateObjectType(world);
        }

        _occupantHeroId = (byte)Hero.UnknownId;
    }

    /// <summary>
    /// The object whose data the tile shows, <c>getMainObjectType()</c>.
    /// </summary>
    /// <remarks>
    /// The reference has two forms: the cached value (<c>getMainObjectType( true )</c>, the default) and
    /// one that looks through an occupying hero (<c>getMainObjectType( false )</c>, see
    /// <see cref="GetMainObjectType(World, bool)"/>). This property is the cached value.
    /// </remarks>
    public Mp2ObjectType MainObjectType => _mainObjectType;

    /// <summary>
    /// Sets the cached object type, <c>setMainObjectType()</c>.
    /// </summary>
    /// <remarks>
    /// The reference also calls <c>world.resetPathfinder()</c> here, because the pathfinder caches
    /// whether a tile is an action object. The setter has no world in the port, so the reset is the
    /// caller's job — <see cref="SetHero"/> is the only caller that has a world today. When the
    /// pathfinder lands, this setter gets an overload that takes the world and the call sites move over.
    /// </remarks>
    /// <param name="objectType">Type to cache.</param>
    public void SetMainObjectType(Mp2ObjectType objectType) => _mainObjectType = objectType;

    /// <summary>
    /// The type of the object hidden by the hero standing on the tile,
    /// <c>_getMainObjectTypeUnderHero()</c> (<c>maps_tiles.cpp:467</c>).
    /// </summary>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns>
    /// The cached type when it is not <see cref="Mp2ObjectType.ActionHero"/>, the type the hero parked when
    /// it is, and <see cref="Mp2ObjectType.None"/> when the hero cannot be resolved.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public Mp2ObjectType GetMainObjectTypeUnderHero(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_mainObjectType != Mp2ObjectType.ActionHero)
        {
            return _mainObjectType;
        }

        Hero? hero = GetHero(world);

        return hero is not null ? hero.ObjectTypeUnderHero : Mp2ObjectType.None;
    }

    /// <summary>
    /// The object type with the option to look through a hero, <c>getMainObjectType( ignoreObjectUnderHero )</c>.
    /// </summary>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="ignoreObjectUnderHero">
    /// <see langword="true"/> (the reference default) returns the cached type, which is
    /// <see cref="Mp2ObjectType.ActionHero"/> while a hero stands here; <see langword="false"/> returns the
    /// type of the object the hero covers.
    /// </param>
    /// <returns>The object type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The difference matters exactly once, in <c>updatePassability()</c>: a hero is a temporary object, so
    /// the passability of the tile under it must be decided by the object the hero covers
    /// (<c>maps_tiles.cpp:686</c>: "get object type but ignore heroes as they are temporary objects").
    /// </remarks>
    public Mp2ObjectType GetMainObjectType(World world, bool ignoreObjectUnderHero = true) =>
        ignoreObjectUnderHero ? _mainObjectType : GetMainObjectTypeUnderHero(world);

    /// <summary>The part that represents the object of the tile, <c>_mainObjectPart</c>.</summary>
    public ObjectPart MainObjectPart => _mainObjectPart;

    /// <summary>Clears the main part, <c>resetMainObjectPart()</c>.</summary>
    public void ResetMainObjectPart() => _mainObjectPart = new ObjectPart();

    /// <summary>Ground level parts, <c>getGroundObjectParts()</c>.</summary>
    public List<ObjectPart> GroundObjectParts => _groundObjectParts;

    /// <summary>
    /// Appends a ground level part and updates the road bit, <c>pushGroundObjectPart()</c>
    /// (<c>maps_tiles.cpp:824</c>).
    /// </summary>
    /// <param name="part">Part to append.</param>
    public void PushGroundObjectPart(ObjectPart part)
    {
        if (IsSpriteRoad(part.IcnType, part.IcnIndex))
        {
            _isTileMarkedAsRoad = true;
        }

        _groundObjectParts.Add(part);
    }

    /// <summary>Appends a top level part, <c>pushTopObjectPart()</c>.</summary>
    /// <param name="part">Part to append.</param>
    public void PushTopObjectPart(ObjectPart part) => _topObjectParts.Add(part);

    /// <summary>Top level parts, <c>getTopObjectParts()</c>.</summary>
    public List<ObjectPart> TopObjectParts => _topObjectParts;

    /// <summary>
    /// Finds a ground level part by object UID, <c>getGroundObjectPart()</c>. The reference returns a
    /// pointer to the element; <see cref="List{T}"/> hands out a reference, so the caller can mutate it.
    /// </summary>
    /// <param name="uid">Object UID to look for.</param>
    /// <returns>The part, or <see langword="null"/> when this tile has none with that UID.</returns>
    public ObjectPart? FindGroundObjectPart(uint uid)
    {
        int index = _groundObjectParts.FindIndex(part => part.Uid == uid);

        return index >= 0 ? _groundObjectParts[index] : null;
    }

    /// <summary>
    /// Finds a top level part by object UID, <c>getTopObjectPart()</c>.
    /// </summary>
    /// <param name="uid">Object UID to look for.</param>
    /// <returns>The part, or <see langword="null"/> when this tile has none with that UID.</returns>
    public ObjectPart? FindTopObjectPart(uint uid)
    {
        int index = _topObjectParts.FindIndex(part => part.Uid == uid);

        return index >= 0 ? _topObjectParts[index] : null;
    }

    /// <summary>
    /// The part of this tile that produces <paramref name="type"/> as its object type,
    /// <c>getObjectPartByActionType()</c> (<c>maps_tiles_helper.cpp:487</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The main part is tried first, then the ground level parts, comparing what
    /// <c>getObjectTypeByIcn</c> says about each sprite with the requested type. Top level parts are not
    /// searched: no action object lives there.
    /// </para>
    /// <para>
    /// A type without the action bit is rejected outright, because that is what the reference does
    /// (<c>MP2::isOffGameActionObject</c>) — the query exists to find the part an action will run on.
    /// Note that this means the callers pass <b>action</b> values: <see cref="Mp2ObjectType.ActionStoneLiths"/>,
    /// not <see cref="Mp2ObjectType.StoneLiths"/>.
    /// </para>
    /// <para>
    /// The reference hands out a pointer; <see cref="ObjectPart"/> is a struct, so the port returns a
    /// copy. Every caller only reads <see cref="ObjectPart.IcnIndex"/> and <see cref="ObjectPart.Uid"/>.
    /// </para>
    /// </remarks>
    /// <param name="type">Object type to look for, action bit included.</param>
    /// <returns>The part, or <see langword="null"/> when this tile has none of that type.</returns>
    public ObjectPart? GetObjectPartByActionType(Mp2ObjectType type)
    {
        if (!Mp2ObjectTypes.IsActionObject(type))
        {
            return null;
        }

        if (MapObjectCatalog.GetObjectTypeByIcn(_mainObjectPart.IcnType, _mainObjectPart.IcnIndex) == type)
        {
            return _mainObjectPart;
        }

        foreach (ObjectPart part in _groundObjectParts)
        {
            if (MapObjectCatalog.GetObjectTypeByIcn(part.IcnType, part.IcnIndex) == type)
            {
                return part;
            }
        }

        return null;
    }

    /// <summary>
    /// Moves the main part to the ground level, <c>moveMainObjectPartToGroundLevel()</c>.
    /// </summary>
    public void MoveMainObjectPartToGroundLevel()
    {
        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown)
        {
            _groundObjectParts.Add(_mainObjectPart);
            _mainObjectPart = new ObjectPart();
        }
    }

    /// <summary>
    /// Sorts the ground level parts by layer and promotes one of them to the main part,
    /// <c>sortObjectParts()</c> (<c>maps_tiles.cpp:833</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parts are sorted by layer from <see cref="ObjectLayerType.Terrain"/> down to
    /// <see cref="ObjectLayerType.Object"/>, and the promoted one is the <em>last</em> non flag part of
    /// that order — the lowest layer value, i.e. the object layer when there is one. The reference
    /// walks the sorted list backwards for this, and its local name
    /// (<c>highestPriorityPartIter</c>) means priority of being the main object, not the layer number.
    /// </para>
    /// <para>
    /// The reference sorts with <c>std::list::sort</c>, which is stable, so parts on the same layer
    /// keep their insertion order. <see cref="List{T}.Sort(Comparison{T})"/> is not stable and would
    /// reorder them, so the port sorts through a stable ordering instead. The loop that promotes the
    /// part skips <c>FLAG32</c> because a flag belongs to another object and can never be the main part
    /// of the tile.
    /// </para>
    /// </remarks>
    public void SortObjectParts()
    {
        if (_groundObjectParts.Count == 0)
        {
            // Nothing to sort.
            return;
        }

        // Push everything to the container and sort it by level.
        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown)
        {
            _groundObjectParts.Insert(0, _mainObjectPart);
        }

        // Sort by internal layers, highest priority first. LINQ's OrderByDescending is stable.
        ObjectPart[] sorted = [.. _groundObjectParts.OrderByDescending(part => part.LayerType)];
        _groundObjectParts.Clear();
        _groundObjectParts.AddRange(sorted);

        // Walk the sorted list backwards: the last non flag part becomes the main part.
        for (int i = _groundObjectParts.Count - 1; i >= 0; --i)
        {
            // Flags might be on the same layer as other objects but they cannot be the main object
            // because they are a part of another object.
            if (_groundObjectParts[i].IcnType != Mp2ObjectIcnType.Flag32)
            {
                (_mainObjectPart, _groundObjectParts[i]) = (_groundObjectParts[i], _mainObjectPart);
                _groundObjectParts.RemoveAt(i);

                break;
            }
        }

        // Top layer objects have no rendering priorities and are drawn in insertion order.
    }

    /// <summary>
    /// Removes every part of the given ICN type from this tile, <c>removeObjects()</c>
    /// (<c>maps_tiles.cpp:1511</c>). Only meant for one tile objects, roads and streams.
    /// </summary>
    /// <param name="objectIcnType">ICN type to remove.</param>
    public void RemoveObjects(Mp2ObjectIcnType objectIcnType)
    {
        _groundObjectParts.RemoveAll(part => part.IcnType == objectIcnType);
        _topObjectParts.RemoveAll(part => part.IcnType == objectIcnType);

        if (_mainObjectPart.IcnType == objectIcnType)
        {
            _mainObjectPart = new ObjectPart();
        }

        UpdateRoadFlag();

        // TODO: the reference does not update the tile's object type after a removal either; it only
        // leaves a note to do it.
    }

    /// <summary>
    /// Replaces the sprite of one part, <c>replaceObject()</c> (<c>maps_tiles.cpp:1525</c>).
    /// </summary>
    /// <remarks>
    /// Every container is searched and the first match wins: only one object per tile can carry the
    /// same UID, so the reference returns immediately.
    /// </remarks>
    /// <param name="objectUid">Object UID of the part to replace.</param>
    /// <param name="originalObjectIcnType">ICN type the part must have now.</param>
    /// <param name="newObjectIcnType">ICN type to set.</param>
    /// <param name="originalImageIndex">Sprite index the part must have now.</param>
    /// <param name="newImageIndex">Sprite index to set.</param>
    public void ReplaceObject(uint objectUid, Mp2ObjectIcnType originalObjectIcnType, Mp2ObjectIcnType newObjectIcnType, byte originalImageIndex, byte newImageIndex)
    {
        for (int i = 0; i < _groundObjectParts.Count; ++i)
        {
            if (_groundObjectParts[i].Uid == objectUid && _groundObjectParts[i].IcnType == originalObjectIcnType && _groundObjectParts[i].IcnIndex == originalImageIndex)
            {
                _groundObjectParts[i] = new ObjectPart(_groundObjectParts[i].LayerType, objectUid, newObjectIcnType, newImageIndex);

                return;
            }
        }

        for (int i = 0; i < _topObjectParts.Count; ++i)
        {
            if (_topObjectParts[i].Uid == objectUid && _topObjectParts[i].IcnType == originalObjectIcnType && _topObjectParts[i].IcnIndex == originalImageIndex)
            {
                _topObjectParts[i] = new ObjectPart(_topObjectParts[i].LayerType, objectUid, newObjectIcnType, newImageIndex);

                return;
            }
        }

        if (_mainObjectPart.Uid == objectUid && _mainObjectPart.IcnType == originalObjectIcnType && _mainObjectPart.IcnIndex == originalImageIndex)
        {
            _mainObjectPart = new ObjectPart(_mainObjectPart.LayerType, objectUid, newObjectIcnType, newImageIndex);
        }
    }

    /// <summary>
    /// Shifts the sprite index of one part, <c>updateObjectImageIndex()</c>
    /// (<c>maps_tiles.cpp:1551</c>).
    /// </summary>
    /// <param name="objectUid">Object UID of the part.</param>
    /// <param name="objectIcnType">ICN type of the part.</param>
    /// <param name="imageIndexOffset">Offset to add to the sprite index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The offset takes the index out of <c>[0, 255)</c>.</exception>
    public void UpdateObjectImageIndex(uint objectUid, Mp2ObjectIcnType objectIcnType, int imageIndexOffset)
    {
        for (int i = 0; i < _groundObjectParts.Count; ++i)
        {
            if (_groundObjectParts[i].Uid == objectUid && _groundObjectParts[i].IcnType == objectIcnType)
            {
                _groundObjectParts[i] = WithShiftedIndex(_groundObjectParts[i], imageIndexOffset);

                return;
            }
        }

        for (int i = 0; i < _topObjectParts.Count; ++i)
        {
            if (_topObjectParts[i].Uid == objectUid && _topObjectParts[i].IcnType == objectIcnType)
            {
                _topObjectParts[i] = WithShiftedIndex(_topObjectParts[i], imageIndexOffset);

                return;
            }
        }

        if (_mainObjectPart.Uid == objectUid && _mainObjectPart.IcnType == objectIcnType)
        {
            _mainObjectPart = WithShiftedIndex(_mainObjectPart, imageIndexOffset);
        }
    }

    /// <summary>
    /// Sets the sprite index of one part of a tile, <c>updateTileObjectIcnIndex()</c>
    /// (<c>maps_tiles.cpp:1586</c>). Only the ground level and the main part are searched, exactly like
    /// the reference, and the road bit is refreshed afterwards.
    /// </summary>
    /// <param name="tile">Tile to update.</param>
    /// <param name="uid">Object UID of the part.</param>
    /// <param name="newIndex">Sprite index to set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static void UpdateTileObjectIcnIndex(Tile tile, uint uid, byte newIndex)
    {
        ArgumentNullException.ThrowIfNull(tile);

        int index = tile._groundObjectParts.FindIndex(part => part.Uid == uid);
        if (index >= 0)
        {
            ObjectPart part = tile._groundObjectParts[index];
            part.IcnIndex = newIndex;
            tile._groundObjectParts[index] = part;
        }
        else if (tile._mainObjectPart.Uid == uid)
        {
            tile._mainObjectPart.IcnIndex = newIndex;
        }

        tile.UpdateRoadFlag();
    }

    /// <summary>
    /// The UID of the object of the given ICN type, <c>getObjectIdByObjectIcnType()</c>
    /// (<c>maps_tiles.cpp:1683</c>).
    /// </summary>
    /// <param name="objectIcnType">ICN type to look for.</param>
    /// <returns>The UID, or <c>0</c> when there is no such part.</returns>
    public uint GetObjectIdByObjectIcnType(Mp2ObjectIcnType objectIcnType)
    {
        if (_mainObjectPart.IcnType == objectIcnType)
        {
            return _mainObjectPart.Uid;
        }

        foreach (ObjectPart part in _groundObjectParts)
        {
            if (part.IcnType == objectIcnType)
            {
                return part.Uid;
            }
        }

        foreach (ObjectPart part in _topObjectParts)
        {
            if (part.IcnType == objectIcnType)
            {
                return part.Uid;
            }
        }

        return 0;
    }

    /// <summary>
    /// The ICN types present on the tile, <c>getValidObjectIcnTypes()</c> (<c>maps_tiles.cpp:1704</c>),
    /// main part first.
    /// </summary>
    /// <returns>The ICN types, without duplicates being filtered out.</returns>
    public List<Mp2ObjectIcnType> GetValidObjectIcnTypes()
    {
        var objectIcnTypes = new List<Mp2ObjectIcnType>();

        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown)
        {
            objectIcnTypes.Add(_mainObjectPart.IcnType);
        }

        foreach (ObjectPart part in _groundObjectParts)
        {
            objectIcnTypes.Add(part.IcnType);
        }

        foreach (ObjectPart part in _topObjectParts)
        {
            objectIcnTypes.Add(part.IcnType);
        }

        return objectIcnTypes;
    }

    /// <summary>
    /// Whether the tile holds any of the given ICN types, <c>containsAnyObjectIcnType()</c>
    /// (<c>maps_tiles.cpp:1729</c>).
    /// </summary>
    /// <param name="objectIcnTypes">ICN types to test.</param>
    /// <returns><c>true</c> on the first match.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="objectIcnTypes"/> is <see langword="null"/>.</exception>
    public bool ContainsAnyObjectIcnType(IEnumerable<Mp2ObjectIcnType> objectIcnTypes)
    {
        ArgumentNullException.ThrowIfNull(objectIcnTypes);

        foreach (Mp2ObjectIcnType objectIcnType in objectIcnTypes)
        {
            if (_mainObjectPart.IcnType == objectIcnType || _groundObjectParts.Exists(part => part.IcnType == objectIcnType)
                                                      || _topObjectParts.Exists(part => part.IcnType == objectIcnType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the tile holds the given sprite, <c>containsSprite()</c> (<c>maps_tiles.cpp:1752</c>).
    /// </summary>
    /// <param name="objectIcnType">ICN type to test.</param>
    /// <param name="imageIdx">Sprite index to test.</param>
    /// <returns><c>true</c> when any part carries that exact sprite.</returns>
    public bool ContainsSprite(Mp2ObjectIcnType objectIcnType, uint imageIdx)
    {
        if (_mainObjectPart.IcnType == objectIcnType && imageIdx == _mainObjectPart.IcnIndex)
        {
            return true;
        }

        if (_groundObjectParts.Exists(part => part.IcnType == objectIcnType && imageIdx == part.IcnIndex))
        {
            return true;
        }

        return _topObjectParts.Exists(part => part.IcnType == objectIcnType && imageIdx == part.IcnIndex);
    }

    /// <summary>
    /// Whether the object occupies the tile for passability purposes, <c>doesObjectExist()</c>
    /// (<c>maps_tiles.cpp:791</c>). Top level parts never block, which is what the reference checks by
    /// looking at the main and ground parts only.
    /// </summary>
    /// <param name="uid">Object UID to look for.</param>
    /// <returns><c>true</c> when a non transparent part has that UID.</returns>
    public bool DoesObjectExist(uint uid)
    {
        if (_mainObjectPart.Uid == uid && !_mainObjectPart.IsPassabilityTransparent)
        {
            return true;
        }

        return _groundObjectParts.Exists(part => part.Uid == uid && !part.IsPassabilityTransparent);
    }

    /// <summary>
    /// Whether every part of the tile lets a hero pass, <c>isPassabilityTransparent()</c>
    /// (<c>maps_tiles.cpp:1091</c>).
    /// </summary>
    /// <remarks>
    /// The last check is the main part, and a tile without objects keeps a default main part on the
    /// object layer, so an empty tile reports <see langword="false"/>. That is what the reference does
    /// and it has no caller in the reference tree, so the quirk is kept as is instead of being turned
    /// into a "no object, so transparent" shortcut.
    /// </remarks>
    public bool IsPassabilityTransparent
    {
        get
        {
            foreach (ObjectPart part in _groundObjectParts)
            {
                if (!part.IsPassabilityTransparent)
                {
                    return false;
                }
            }

            return _mainObjectPart.IsPassabilityTransparent;
        }
    }

    /// <summary>
    /// Whether the terrain sprite is a road, <c>isSpriteRoad()</c> (<c>maps_tiles.cpp:801</c>).
    /// </summary>
    /// <remarks>
    /// The three allowed index sets are the reference's. Roads share <c>ROAD.ICN</c> with other sprites,
    /// and a castle's entrance road lives inside the town ICN, so only those exact sprites count.
    /// </remarks>
    /// <param name="objectIcnType">ICN type of the part.</param>
    /// <param name="imageIndex">Sprite index of the part.</param>
    /// <returns><c>true</c> when the sprite is a road.</returns>
    public static bool IsSpriteRoad(Mp2ObjectIcnType objectIcnType, byte imageIndex) => objectIcnType switch
    {
        Mp2ObjectIcnType.Road => imageIndex is 0 or 2 or 3 or 4 or 5 or 6 or 7 or 9 or 12 or 13 or 14 or 16 or 17 or 18 or 19 or 20 or 21 or 26 or 28 or 29
            or 30 or 31 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 43 or 44 or 45 or 46 or 47 or 48,
        Mp2ObjectIcnType.ObjnTown => imageIndex is 13 or 29 or 45 or 61 or 77 or 93 or 109 or 125 or 141 or 157 or 173 or 189,
        Mp2ObjectIcnType.ObjnTwrd => imageIndex is 13 or 29,
        _ => false,
    };

    /// <summary>
    /// Recomputes the road bit from the parts, <c>updateRoadFlag()</c> (<c>maps_tiles.cpp:1305</c>).
    /// </summary>
    public void UpdateRoadFlag()
    {
        _isTileMarkedAsRoad = IsSpriteRoad(_mainObjectPart.IcnType, _mainObjectPart.IcnIndex);

        if (_isTileMarkedAsRoad)
        {
            return;
        }

        foreach (ObjectPart part in _groundObjectParts)
        {
            if (IsSpriteRoad(part.IcnType, part.IcnIndex))
            {
                _isTileMarkedAsRoad = true;

                return;
            }
        }
    }

    /// <summary>
    /// The cached road bit, <c>isRoad()</c>. The original editor sets it even when no road exists, so
    /// it must never be used to decide whether a road is really there.
    /// </summary>
    public bool IsRoad => _isTileMarkedAsRoad;

    /// <summary>
    /// Whether the tile carries a stream, <c>isStream()</c> (<c>maps_tiles.cpp:1149</c>).
    /// </summary>
    /// <remarks>
    /// The reference counts two sprite ranges of <c>OBJNMUL2</c> as streams as well, because the
    /// original editor stored those water pieces in the multi object archive.
    /// </remarks>
    public bool IsStream =>
        IsStreamPart(_mainObjectPart) || _groundObjectParts.Exists(IsStreamPart);

    /// <summary>
    /// Whether every object part of the tile is a shadow, <c>isShadow()</c> (<c>maps_tiles.cpp:1163</c>).
    /// </summary>
    public bool IsShadow =>
        TileObjectTraits.IsObjectPartShadow(_mainObjectPart) && _groundObjectParts.TrueForAll(TileObjectTraits.IsObjectPartShadow);

    /// <summary>
    /// The directions a hero may step into this tile based on the tile alone,
    /// <c>getTileIndependentPassability()</c> (<c>maps_tiles.cpp:583</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parts are checked in the reverse of the rendering order — the main part first, then the ground
    /// parts from the end of the list to the start — and the result is the intersection of all of them.
    /// An action object ends the walk immediately: the action object is what the hero interacts with, so
    /// the parts below it cannot open the tile.
    /// </para>
    /// <para>
    /// Top level parts are not consulted: what stands above a tile never decides whether a hero can walk
    /// on it. The neighbouring tiles are not consulted either, that is
    /// <c>updatePassability()</c>, which needs the whole map and is not ported yet.
    /// </para>
    /// </remarks>
    /// <returns>The passable directions of this tile on its own.</returns>
    public Direction GetTileIndependentPassability()
    {
        Direction passability = Directions.All;

        bool isActionObject = false;

        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown)
        {
            passability &= GetObjectPartPassability(_mainObjectPart, ref isActionObject);

            if (isActionObject)
            {
                return passability;
            }
        }

        // The reference iterates the ground level parts in reverse: the list is sorted by layer from
        // terrain down to the object layer, so the last element is the topmost object.
        for (int i = _groundObjectParts.Count - 1; i >= 0; --i)
        {
            passability &= GetObjectPartPassability(_groundObjectParts[i], ref isActionObject);

            if (isActionObject)
            {
                return passability;
            }
        }

        return passability;
    }

    /// <summary>
    /// Stores the tile independent passability as the initial passability of the tile,
    /// <c>setInitialPassability()</c> (<c>maps_tiles.cpp:646</c>).
    /// </summary>
    public void SetInitialPassability() => _tilePassabilityDirections = GetTileIndependentPassability();

    /// <summary>
    /// The directions a hero may enter this tile from, <c>GetPassable()</c>.
    /// </summary>
    public Direction PassableDirections => _tilePassabilityDirections;

    /// <summary>
    /// Whether the tile can be entered from the given direction, <c>isPassableFrom()</c>.
    /// </summary>
    /// <param name="direction">Direction of the step, a single bit.</param>
    /// <returns><c>true</c> when the mask contains the bit.</returns>
    public bool IsPassableFrom(Direction direction) => (direction & _tilePassabilityDirections) != 0;

    /// <summary>
    /// Whether the tile can be left in the given direction, <c>isPassableTo()</c>.
    /// </summary>
    /// <param name="direction">Direction of the step, a single bit.</param>
    /// <returns><c>true</c> when the mask contains the bit.</returns>
    public bool IsPassableTo(Direction direction) => (direction & _tilePassabilityDirections) != 0;

    /// <summary>
    /// Colors that still have the tile under the fog, <c>_fogColors</c> (<c>maps_tiles.h:421</c>).
    /// </summary>
    public PlayerColor FogColors => _fogColors;

    /// <summary>
    /// Whether any of the players still has the tile under the fog, <c>isFog( PlayerColorsSet )</c>
    /// (<c>maps_tiles.h:301</c>).
    /// </summary>
    /// <remarks>
    /// The reference has two overloads of <c>isFog</c> — one for a single color and one for a set — that
    /// differ in whether <b>any</b> or <b>all</b> of the players must be in the fog. C# sees a single
    /// bit set type, so the "all" variant is spelled <see cref="IsFogForAll"/> here; for one color both
    /// answer the same.
    /// </remarks>
    /// <param name="colors">One or more player colors.</param>
    /// <returns><see langword="true"/> when that player (or one of them) has not revealed the tile.</returns>
    public bool IsFog(PlayerColor colors) => (_fogColors & colors) != PlayerColor.None;

    /// <summary>
    /// Whether every player of a set still has the tile under the fog, <c>isFog( PlayerColorsSet )</c>
    /// (<c>maps_tiles.h:306</c>).
    /// </summary>
    /// <remarks>
    /// The set is the union of an alliance's colors, and the reference answers <see langword="true"/>
    /// only when <b>all</b> of them are still in the fog — one allied player who has seen the tile reveals
    /// it for the whole alliance.
    /// </remarks>
    /// <param name="colors">One or more player colors.</param>
    /// <returns><see langword="true"/> when the whole set has not revealed the tile.</returns>
    public bool IsFogForAll(PlayerColor colors) => (_fogColors & colors) == colors;

    /// <summary>
    /// Reveals the tile for a set of players, <c>Tile::ClearFog()</c> (<c>maps_tiles.cpp:1576</c>).
    /// </summary>
    /// <remarks>
    /// The reference also resets the pathfinder here, because a tile can be revealed without the hero
    /// moving (a scouting level gained from a chest, a map bought in a Magellan's Maps object). The port
    /// has no pathfinder yet and <see cref="World.ResetPathfinder"/> is already a placeholder, so the
    /// call belongs to the world side and is left to whoever reveals tiles until then.
    /// </remarks>
    /// <param name="colors">Colors that reveal the tile.</param>
    public void ClearFog(PlayerColor colors) => _fogColors &= ~colors;

    /// <summary>
    /// Directions in which the tile still has fog to draw, <c>_fogDirection</c> (<c>maps_tiles.h:427</c>).
    /// </summary>
    /// <remarks>
    /// <c>Direction.Unknown</c> means "no fog at all"; otherwise the center bit is set and every bit of a
    /// neighbour that is fogged too. It is a rendering cache, recomputed by the area reveal.
    /// </remarks>
    public Direction FogDirection => _fogDirection;

    /// <summary>
    /// Stores the fog directions, <c>setFogDirection()</c> (<c>maps_tiles.h:356</c>).
    /// </summary>
    /// <remarks>
    /// The reference marks this <c>// Do NOT call this method directly!!!</c>: only the area reveal may
    /// set the value, because a stale direction draws fog over a revealed tile.
    /// </remarks>
    /// <param name="fogDirection">Directions of the fog around the tile.</param>
    public void SetFogDirection(Direction fogDirection) => _fogDirection = fogDirection;

    /// <summary>
    /// Recomputes the cached object type from the object parts, <c>updateObjectType()</c>
    /// (<c>maps_tiles.cpp:1599</c>). Called after parts are removed or rearranged, and once per tile by
    /// <see cref="World.UpdatePassabilities"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is: an action object wins immediately (main part first, then the ground parts from the
    /// back); otherwise the first ground part that carries a type is remembered, a top part with a type
    /// wins over it, and if nothing carries a type the tile may still be a coast because a neighbouring
    /// tile is water.
    /// </para>
    /// <para>
    /// The event branch is special: the reference keeps the event type only when
    /// <c>world.GetMapEvent()</c> finds the event data and resets it to <c>OBJ_NONE</c> when it does not,
    /// which is how a map whose event object lost its information block stops being visitable. Note that
    /// the reference's constant is <c>MP2::OBJ_EVENT</c>, an <em>action</em> value (<c>mp2.h:291</c>), i.e.
    /// <see cref="Mp2ObjectType.ActionEvent"/> here; <see cref="Mp2ObjectType.Event"/> is the non-action
    /// twin.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to, for the neighbouring tiles.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The tile is not part of <paramref name="world"/>.</exception>
    public void UpdateObjectType(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_mainObjectType == Mp2ObjectType.ActionEvent)
        {
            if (world.GetMapEvent(Index) is null)
            {
                SetMainObjectType(Mp2ObjectType.None);
            }

            return;
        }

        // After removing an object there can still be an object part in the main part.
        Mp2ObjectType objectType = MapObjectCatalog.GetObjectTypeByIcn(_mainObjectPart.IcnType, _mainObjectPart.IcnIndex);

        if (Mp2ObjectTypes.IsActionObject(objectType))
        {
            // Only an interactive type is cached here, so that the interaction has something to act on.
            SetMainObjectType(objectType);

            return;
        }

        // Sometimes the type hides in a ground part. The parts are walked from the back because the
        // latest one has the higher priority.
        for (int i = _groundObjectParts.Count - 1; i >= 0; --i)
        {
            Mp2ObjectType type = MapObjectCatalog.GetObjectTypeByIcn(_groundObjectParts[i].IcnType, _groundObjectParts[i].IcnIndex);

            if (type == Mp2ObjectType.None)
            {
                continue;
            }

            if (Mp2ObjectTypes.IsActionObject(type))
            {
                SetMainObjectType(type);

                return;
            }

            if (objectType == Mp2ObjectType.None)
            {
                objectType = type;
            }
        }

        // Or in a top part, which wins over whatever the ground says.
        for (int i = _topObjectParts.Count - 1; i >= 0; --i)
        {
            Mp2ObjectType type = MapObjectCatalog.GetObjectTypeByIcn(_topObjectParts[i].IcnType, _topObjectParts[i].IcnIndex);

            if (type != Mp2ObjectType.None)
            {
                SetMainObjectType(type);

                return;
            }
        }

        if (objectType != Mp2ObjectType.None)
        {
            SetMainObjectType(objectType);

            return;
        }

        // Nothing on the tile itself. A land tile next to water becomes a coast, so that the shore keeps
        // being drawn after whatever stood there was removed.
        if (IsWater)
        {
            SetMainObjectType(objectType);

            return;
        }

        MapGeometry geometry = world.Geometry ?? throw NotInWorld();

        foreach (int tileIndex in geometry.GetAroundIndexes(_index, 1))
        {
            if (tileIndex < 0)
            {
                continue;
            }

            if (world.GetTile(tileIndex).IsWater)
            {
                SetMainObjectType(Mp2ObjectType.Coast);

                return;
            }
        }

        SetMainObjectType(objectType);
    }

    /// <summary>
    /// Repairs the object type of a tile that was read from an MP2 map file,
    /// <c>fixMP2MapTileObjectType()</c> (<c>maps_tiles.cpp:1321</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original editor and the original game wrote several object types wrong, and the reference
    /// repairs each of them here instead of in the loader, because the same repairs are needed by the
    /// editor when it opens such a map. <see cref="World.LoadMap"/> calls this once per tile, before the
    /// passability is computed — the reefs fix in particular changes what the passability has to be.
    /// </para>
    /// <para>
    /// The type is read through <c>getMainObjectType( false )</c>, i.e. through the hero standing on the
    /// tile, because a map file cannot store a hero: what the file calls the tile's type is the type of
    /// the object the hero later stands on.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void FixMP2MapTileObjectType(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Mp2ObjectType originalObjectType = GetMainObjectType(world, false);

        // The left tile of a skeleton on desert is stored as a non action object.
        if (originalObjectType == Mp2ObjectType.ActionSkeleton && _mainObjectPart.IcnType == Mp2ObjectIcnType.ObjnDsrt
            && _mainObjectPart.IcnIndex == 83)
        {
            SetMainObjectType(Mp2ObjectType.Skeleton);

            return;
        }

        // The oasis has two top tiles marked as part of the ground object layer, while they belong to the
        // top level.
        if (originalObjectType == Mp2ObjectType.Oasis && _mainObjectPart.IcnType == Mp2ObjectIcnType.ObjnDsrt
            && _mainObjectPart.IcnIndex is 105 or 106)
        {
            _topObjectParts.Add(_mainObjectPart);
            _mainObjectPart = new ObjectPart();

            return;
        }

        // The original editor marks reefs as stones; the type is corrected without touching the parts,
        // which is also what the reefs passability rule expects.
        if (originalObjectType == Mp2ObjectType.Rock
            && TileObjectTraits.IsValidReefsSprite(_mainObjectPart.IcnType, _mainObjectPart.IcnIndex))
        {
            SetMainObjectType(Mp2ObjectType.Reefs);

            return;
        }

        // Some maps have water tiles marked as coast, which they cannot be.
        if (originalObjectType == Mp2ObjectType.Coast && IsWater)
        {
            // A hero on such a tile keeps the tile empty underneath instead of exposing the false coast.
            Hero? hero = GetHero(world);

            if (hero is not null)
            {
                hero.SetObjectTypeUnderHero(Mp2ObjectType.None);
            }
            else
            {
                SetMainObjectType(Mp2ObjectType.None);
            }

            return;
        }

        // On maps made by non standard editors the type of a random monster tile does not match the
        // placeholder sprite. The original game looks at the sprite when it places the monster, so the
        // type is brought in line with it.
        if (_mainObjectPart.IcnType == Mp2ObjectIcnType.Mons32)
        {
            byte objectSpriteIndex = _mainObjectPart.IcnIndex;

            Mp2ObjectType monsterObjectType = objectSpriteIndex switch
            {
                66 => Mp2ObjectType.ActionRandomMonster,
                67 => Mp2ObjectType.ActionRandomMonsterWeak,
                68 => Mp2ObjectType.ActionRandomMonsterMedium,
                69 => Mp2ObjectType.ActionRandomMonsterStrong,
                70 => Mp2ObjectType.ActionRandomMonsterVeryStrong,
                _ => originalObjectType,
            };

            if (monsterObjectType != originalObjectType)
            {
                SetMainObjectType(monsterObjectType);

                return;
            }
        }

        // The Price of Loyalty objects are all stored under a handful of common types; the real type is
        // in the metadata, which the port does not read, so it is recovered from the parts.
        switch (originalObjectType)
        {
        case Mp2ObjectType.ExpansionDwelling:
        case Mp2ObjectType.ExpansionObject:
        case Mp2ObjectType.ActionExpansionDwelling:
        case Mp2ObjectType.ActionExpansionObject:
        {
            if (UpdatePriceOfLoyaltyObjectType(_mainObjectPart, world))
            {
                return;
            }

            // A ground level part should not exist without a top object, but a broken map may have one.
            foreach (ObjectPart part in _groundObjectParts)
            {
                if (UpdatePriceOfLoyaltyObjectType(part, world))
                {
                    return;
                }
            }

            foreach (ObjectPart part in _topObjectParts)
            {
                if (UpdatePriceOfLoyaltyObjectType(part, world))
                {
                    return;
                }
            }

            break;
        }

        default:
            break;
        }
    }

    /// <summary>
    /// Recovers the real type of a The Price of Loyalty object from one of its parts,
    /// <c>updatePriceOfLoyaltyObjectType()</c> (<c>maps_tiles.cpp:319</c>).
    /// </summary>
    /// <remarks>
    /// Only the three object sets of the expansion carry those objects; a part from any other set is
    /// left alone, which is what the reference does to tell "not a PoL object" from "an unknown one".
    /// </remarks>
    /// <param name="part">Part of the object of this tile.</param>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns><c>true</c> when the tile type was decided from this part.</returns>
    private bool UpdatePriceOfLoyaltyObjectType(ObjectPart part, World world)
    {
        switch (part.IcnType)
        {
        case Mp2ObjectIcnType.XLoc1:
        case Mp2ObjectIcnType.XLoc2:
        case Mp2ObjectIcnType.XLoc3:
            break;

        default:
            // Not a part of an expansion object set.
            return false;
        }

        if (MapObjectCatalog.GetObjectPartByIcn(part.IcnType, part.IcnIndex) is not { } objectPart)
        {
            // A hacked map, or a part that carries nothing.
            return false;
        }

        if (objectPart.ObjectType == Mp2ObjectType.None)
        {
            // The part is not in the object tables or is marked incorrectly: rebuild the type from the
            // parts the tile has.
            UpdateObjectType(world);

            return true;
        }

        SetMainObjectType(objectPart.ObjectType);

        return true;
    }

    /// <summary>
    /// Whether an object on this tile is tall, i.e. has a part on the top layer of the tile above,
    /// <c>isAnyTallObjectOnTile()</c> (<c>maps_tiles.cpp:1767</c>).
    /// </summary>
    /// <remarks>
    /// A tall object overhangs the tile above it, so it decides whether a hero may step diagonally
    /// upwards from this tile — <see cref="UpdatePassability"/> asks this about the left and right
    /// neighbours. The reference cannot ask the tile itself, because the top parts of an overhanging
    /// object are stored on the tile above; hence the UID comparison against the top parts of that tile.
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns><c>true</c> when a part of one of this tile's objects is on the top layer of the tile above.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public bool IsAnyTallObjectOnTile(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        MapGeometry geometry = world.Geometry ?? throw NotInWorld();

        if (!geometry.IsValidDirection(_index, Direction.Top))
        {
            // First row: nothing can overhang it from above.
            return false;
        }

        var objectUids = new List<uint>();

        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown && !_mainObjectPart.IsPassabilityTransparent)
        {
            objectUids.Add(_mainObjectPart.Uid);
        }

        foreach (ObjectPart part in _groundObjectParts)
        {
            if (!part.IsPassabilityTransparent)
            {
                objectUids.Add(part.Uid);
            }
        }

        Tile topTile = world.GetTile(geometry.GetDirectionIndex(_index, Direction.Top));

        foreach (uint uid in objectUids)
        {
            // Only the top layer of the tile above is interesting, and transparency does not matter here.
            foreach (ObjectPart part in topTile._topObjectParts)
            {
                if (part.Uid == uid)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the object of this tile does not hang together with the tile below it, so that it must not
    /// be treated as a tall object blocking the walk, <c>isDetachedObject()</c>
    /// (<c>maps_tiles.cpp:1854</c>).
    /// </summary>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns><c>true</c> when the object stands on its own tile only.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public bool IsDetachedObject(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Mp2ObjectType objectType = _mainObjectType;

        if (TileObjectTraits.IsDetachedObjectType(objectType))
        {
            return true;
        }

        Mp2ObjectType correctedObjectType = Mp2ObjectTypes.BaseActionType(objectType);

        if (!TileObjectTraits.IsDetachedObjectType(correctedObjectType))
        {
            return false;
        }

        // An action object may be spread over several tiles: only the main tile owns it.
        int mainTileIndex = GetIndexOfMainTile(this, world);

        if (mainTileIndex == -1)
        {
            return false;
        }

        uint objectUid = world.GetTile(mainTileIndex).MainObjectPart.Uid;

        if (_mainObjectPart.Uid == objectUid)
        {
            return !_mainObjectPart.IsPassabilityTransparent;
        }

        foreach (ObjectPart part in _groundObjectParts)
        {
            if (part.Uid == objectUid)
            {
                return !part.IsPassabilityTransparent;
            }
        }

        return false;
    }

    /// <summary>
    /// The tile that owns the multi tile action object of <paramref name="tile"/>,
    /// <c>getIndexOfMainTile()</c> (<c>maps_tiles.cpp:1798</c>).
    /// </summary>
    /// <remarks>
    /// The reference does not know the size of an object, so it searches a radius of three tiles around
    /// the given one, from the rows below upwards (objects grow downwards, and no object has tiles more
    /// than one row below its main tile) and returns the first tile that has the action type of the
    /// object and shares one of its UIDs. Both the type and the UIDs have to match: a tile can carry the
    /// right type with a part of a completely different object.
    /// </remarks>
    /// <param name="tile">A tile of the object.</param>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns>The index of the main tile, or <c>-1</c> when the object has none (a broken map).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> or <paramref name="world"/> is <see langword="null"/>.</exception>
    public static int GetIndexOfMainTile(Tile tile, World world)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(world);

        Mp2ObjectType objectType = tile._mainObjectType;
        Mp2ObjectType correctedObjectType = Mp2ObjectTypes.BaseActionType(objectType);

        if (correctedObjectType == objectType)
        {
            return tile._index;
        }

        Debug.Assert((ushort)correctedObjectType > (ushort)objectType, "The action type is the base type plus the action bit.");

        // The UIDs of every layer: it is unknown whether the object starts on the ground or on the top
        // layer, and either way the main tile has to share one of them. The reference uses a std::set
        // for this, but only asks whether a UID is in it, so a hash set is the same thing.
        var uids = new HashSet<uint> { tile._mainObjectPart.Uid };

        foreach (ObjectPart part in tile._groundObjectParts)
        {
            uids.Add(part.Uid);
        }

        foreach (ObjectPart part in tile._topObjectParts)
        {
            uids.Add(part.Uid);
        }

        MapGeometry geometry = world.Geometry ?? throw NotInWorld();

        const int radiusOfSearch = 3;

        for (int y = radiusOfSearch; y >= -1; --y)
        {
            int rowOffset = tile._index + (y * geometry.Width);

            for (int x = -radiusOfSearch; x <= radiusOfSearch; ++x)
            {
                int index = rowOffset + x;

                if (!geometry.IsValidAbsIndex(index))
                {
                    continue;
                }

                Tile foundTile = world.GetTile(index);

                if (foundTile._mainObjectType != correctedObjectType)
                {
                    continue;
                }

                if (foundTile._mainObjectPart.Uid != 0 && uids.Contains(foundTile._mainObjectPart.Uid))
                {
                    return foundTile._index;
                }
            }
        }

        // Most likely an object put together by an editor.
        return -1;
    }

    /// <summary>
    /// Narrows the initial passability down by what the neighbouring tiles allow,
    /// <c>updatePassability()</c> (<c>maps_tiles.cpp:657</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Call it once per passability update, after <see cref="SetInitialPassability"/>.</b> The
    /// reference asserts that the mask still equals the tile independent passability — the function
    /// assumes it starts from that value and only ever clears bits. In a debug build the port asserts the
    /// same; in release it proceeds like the reference does.
    /// </para>
    /// <para>
    /// Two things are decided here: a tall object on the left or right neighbour forbids the upward
    /// diagonals, and an object on the tile below can forbid the whole tile (the walk below is blocked,
    /// the object continues on this tile, or land turns into water at the bottom edge of the map).
    /// </para>
    /// <para>
    /// The reference reads the object type through <c>getMainObjectType( false )</c> to look through a
    /// hero standing on the tile: a hero is a temporary object, so what it covers decides the
    /// passability.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void UpdatePassability(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_tilePassabilityDirections == Direction.Unknown)
        {
            // Already impassable, nothing to narrow down.
            return;
        }

        Debug.Assert(
            _tilePassabilityDirections == GetTileIndependentPassability(),
            "UpdatePassability has to be called on a tile whose passability was just set from scratch.");

        MapGeometry geometry = world.Geometry ?? throw NotInWorld();

        // A tall object on the left or the right neighbour overhangs this tile, which forbids the
        // diagonal step upwards on that side.
        if ((_tilePassabilityDirections & Direction.TopLeft) != 0 && geometry.IsValidDirection(_index, Direction.Left))
        {
            Tile leftTile = world.GetTile(geometry.GetDirectionIndex(_index, Direction.Left));

            if (leftTile.IsAnyTallObjectOnTile(world) && (leftTile.GetTileIndependentPassability() & Direction.Top) == 0)
            {
                _tilePassabilityDirections &= ~Direction.TopLeft;
            }
        }

        if ((_tilePassabilityDirections & Direction.TopRight) != 0 && geometry.IsValidDirection(_index, Direction.Right))
        {
            Tile rightTile = world.GetTile(geometry.GetDirectionIndex(_index, Direction.Right));

            if (rightTile.IsAnyTallObjectOnTile(world) && (rightTile.GetTileIndependentPassability() & Direction.Top) == 0)
            {
                _tilePassabilityDirections &= ~Direction.TopRight;
            }
        }

        Mp2ObjectType objectType = GetMainObjectType(world, false);

        if (Mp2ObjectTypes.IsActionObject(objectType))
        {
            // An action object is entered from where its own sprite allows; what stands next to it does
            // not matter.
            return;
        }

        if (IsShadow)
        {
            // Nothing but shadows: shadows never affect passability.
            return;
        }

        if (_mainObjectPart.IcnType == Mp2ObjectIcnType.Unknown)
        {
            // No main part at all, so there is no object to test against the tile below.
            // The reference notes that this is wrong for tiles that only have ground parts and intends
            // to fix it there; the behaviour is kept.
            return;
        }

        if (_mainObjectPart.IsPassabilityTransparent)
        {
            // This object does not affect passability.
            return;
        }

        if (!geometry.IsValidDirection(_index, Direction.Bottom))
        {
            // The object touches the bottom edge of the map: a hero could never stand below it.
            _tilePassabilityDirections = Direction.Unknown;

            return;
        }

        Tile bottomTile = world.GetTile(geometry.GetDirectionIndex(_index, Direction.Bottom));

        if (!IsWater && bottomTile.IsWater)
        {
            // A land object over water: mark the tile as impassable so that a hero is never asked to
            // disembark here.
            _tilePassabilityDirections = Direction.Unknown;

            return;
        }

        // If the tile below has a part of the same object, then this tile continues that object and
        // cannot be entered.
        var objectUids = new List<uint> { _mainObjectPart.Uid };

        foreach (ObjectPart part in _groundObjectParts)
        {
            if (!part.IsPassabilityTransparent)
            {
                objectUids.Add(part.Uid);
            }
        }

        foreach (uint objectUid in objectUids)
        {
            if (bottomTile.DoesObjectExist(objectUid))
            {
                _tilePassabilityDirections = Direction.Unknown;

                return;
            }
        }

        Mp2ObjectType bottomTileObjectType = bottomTile._mainObjectType;

        if (Mp2ObjectTypes.IsActionObject(bottomTileObjectType) && (bottomTile.GetTileIndependentPassability() & Direction.Top) != 0)
        {
            // The object below is entered from above, so it does not block this tile.
            return;
        }

        // How many objects are there below this one, not counting shadows, roads, streams and flags.
        int validBottomLayerObjects = _groundObjectParts.Count(part =>
            !TileObjectTraits.IsObjectPartShadow(part)
            && part.IcnType is not (Mp2ObjectIcnType.Road or Mp2ObjectIcnType.Stream or Mp2ObjectIcnType.Flag32));

        bool singleObjectTile = validBottomLayerObjects == 0 && _topObjectParts.Count == 0
                                && bottomTile._mainObjectPart.IcnType != _mainObjectPart.IcnType;

        if (singleObjectTile || IsDetachedObject(world) || bottomTile._mainObjectPart.IcnType == Mp2ObjectIcnType.Unknown
            || bottomTile._mainObjectPart.IsPassabilityTransparent)
        {
            return;
        }

        Mp2ObjectType correctedObjectType = Mp2ObjectTypes.BaseActionType(bottomTileObjectType);

        if (Mp2ObjectTypes.IsActionObject(bottomTileObjectType) || Mp2ObjectTypes.IsActionObject(correctedObjectType))
        {
            if (!TileObjectTraits.IsShortObject(bottomTileObjectType) && !TileObjectTraits.IsShortObject(correctedObjectType))
            {
                // A tall object below: this tile is the upper half of it.
                _tilePassabilityDirections = Direction.Unknown;
            }

            return;
        }

        if (TileObjectTraits.IsShortObject(bottomTileObjectType)
            || (!bottomTile.ContainsAnyObjectIcnType(GetValidObjectIcnTypes())
                && (TileObjectTraits.IsCombinedObject(objectType) || TileObjectTraits.IsCombinedObject(bottomTileObjectType))))
        {
            // The tile below is short, or the two tiles hold the two halves of a combined object, so the
            // walk below does not block this tile. The reference warns that the main object type is not
            // a fully reliable answer here — some map files mark tiles as objects that hold nothing
            // visible (a rock shadow on the Necroking map, for instance).
            return;
        }

        _tilePassabilityDirections = Direction.Unknown;
    }

    /// <summary>
    /// The exception for a tile that is asked about its world while it is not part of one.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException NotInWorld() =>
        new("The tile is not part of a world: the world has no dimensions, so it has no neighbours either.");

    /// <summary>
    /// Object metadata of the tile, <c>metadata()</c>. Lane 0 is the <c>quantity</c> word of the MP2
    /// file, lanes 1 and 2 are filled by the extended object data of Resurrection maps.
    /// </summary>
    public uint[] Metadata => _metadata;

    /// <summary>
    /// What the objects of the tile pay every day, <c>Maps::getDailyIncomeObjectResources()</c>
    /// (<c>maps_tiles_helper.cpp:372</c>).
    /// </summary>
    /// <remarks>
    /// Only three object types pay a daily income, and all three store it in the metadata: a mine keeps
    /// the resource in lane 0 and the amount in lane 1, and the alchemist lab and the sawmill are the
    /// mercury and the wood "mines" of the original game. Every other object answers an empty fund and
    /// trips the assertion of the reference, which is kept as <see cref="Debug.Assert(bool)"/>: the
    /// callers ask this only for objects they own and already know to be income objects.
    /// </remarks>
    /// <param name="world">The world the tile belongs to, needed to see through a hero on the tile.</param>
    /// <returns>The daily income, empty for objects that pay nothing.</returns>
    public Funds GetDailyIncomeObjectResources(World world)
    {
        switch (GetMainObjectType(world, false))
        {
            case Mp2ObjectType.AlchemistLab:
            case Mp2ObjectType.Mine:
            case Mp2ObjectType.Sawmill:
                return new Funds((int)_metadata[0], _metadata[1]);

            default:
                Debug.Assert(false, "The tile does not hold an object that pays a daily income.");
                return default;
        }
    }

    /// <summary>
    /// Whether one part draws a stream, the per part test of <c>isStream()</c>.
    /// </summary>
    /// <param name="part">Part to test.</param>
    /// <returns><c>true</c> when the part is a stream sprite.</returns>
    private static bool IsStreamPart(ObjectPart part) =>
        part.IcnType == Mp2ObjectIcnType.Stream
        || (part.IcnType == Mp2ObjectIcnType.ObjnMul2 && (part.IcnIndex < 14 || (part.IcnIndex > 217 && part.IcnIndex < 218 + 14)));

    /// <summary>
    /// The passability that one part allows, the <c>getObjectPartPassability</c> lambda of
    /// <c>getTileIndependentPassability()</c>.
    /// </summary>
    /// <param name="part">Part to check.</param>
    /// <param name="isActionObject">Set to <c>true</c> when the part is an action object.</param>
    /// <returns>The directions the part allows.</returns>
    private static Direction GetObjectPartPassability(ObjectPart part, ref bool isActionObject)
    {
        if (part.IcnType is Mp2ObjectIcnType.Road or Mp2ObjectIcnType.Stream or Mp2ObjectIcnType.Flag32)
        {
            // Roads, streams and flags are completely passable.
            return Directions.All;
        }

        // The tile has a valid object part. Check its passability.
        Mp2ObjectType objectType = MapObjectCatalog.GetObjectTypeByIcn(part.IcnType, part.IcnIndex);

        if (Mp2ObjectTypes.IsActionObject(objectType))
        {
            isActionObject = true;

            return TileObjectTraits.GetActionObjectDirection(objectType);
        }

        if (objectType == Mp2ObjectType.Reefs)
        {
            // Reefs are inaccessible.
            return Direction.Unknown;
        }

        if (!part.IsPassabilityTransparent && !TileObjectTraits.IsObjectPartShadow(part))
        {
            // A ground level part that is neither a shadow nor part of the terrain blocks the tile from
            // the middle row down: the sprite overhangs its tile towards the hero.
            return Directions.CenterRow | Directions.BottomRow;
        }

        // This object part is fully passable.
        return Directions.All;
    }

    /// <summary>
    /// The part with the given sprite index shifted, <c>part.icnIndex + imageIndexOffset</c>.
    /// </summary>
    /// <param name="part">Part to copy.</param>
    /// <param name="offset">Offset to apply.</param>
    /// <returns>The shifted part.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The result is not a valid sprite index.</exception>
    private static ObjectPart WithShiftedIndex(ObjectPart part, int offset)
    {
        int index = part.IcnIndex + offset;

        // The reference asserts on this range; a wrong offset is a caller bug, not a map issue.
        if (index < 0 || index >= ObjectPart.EmptyIcnIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The sprite index would leave [0, 254].");
        }

        part.IcnIndex = (byte)index;

        return part;
    }
}
