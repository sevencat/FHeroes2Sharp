using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game;

/// <summary>
/// The world map: its size, its tiles and (later) its heroes, castles, kingdoms and the pathfinder.
/// The port of <c>World</c> (<c>参考：src/fheroes2/world/world.h:197</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the skeleton, not the whole class.</b> The reference keeps more than twenty containers
/// and a hundred members in <c>World</c>; only what the already ported code needs is here: the map
/// dimensions, the tile vector and the two loaders' bookkeeping around them. Everything that depends
/// on systems the port does not have yet (heroes, castles, kingdoms, captured objects, map objects,
/// the day/week/month counters, events, rumors, regions, the ultimate artifact, static analysis) is
/// added together with the system it belongs to — see <c>docs/DECISIONS.md</c>, ADR-065.
/// </para>
/// <para>
/// The reference has a Meyers singleton (<c>World::Get()</c>) plus the file-scope reference
/// <c>World &amp; world</c>. The port follows the global-facade rule of ADR-056:
/// <see cref="Current"/> is the façade and everything else lives on the instance. The constructor is
/// <c>internal</c> instead of private so tests (and any future second world, e.g. a map preview) can
/// create an isolated instance; production code goes through <see cref="Current"/>.
/// </para>
/// <para>
/// The reference deletes the copy and the move constructors; the port is a <c>sealed class</c> and
/// never copies.
/// </para>
/// </remarks>
public sealed class World
{
    /// <summary>Number of days in a game week, <c>numOfDaysPerWeek</c>.</summary>
    public const int NumOfDaysPerWeek = 7;

    /// <summary>Number of weeks in a game month, <c>numOfWeeksPerMonth</c>.</summary>
    public const int NumOfWeeksPerMonth = 4;

    private readonly List<Tile> _tiles = [];

    /// <summary>
    /// Creates an empty world. Use <see cref="Current"/> in production code; this constructor exists
    /// for tests and for a second, isolated map instance.
    /// </summary>
    internal World()
    {
    }

    /// <summary>
    /// The world the game is playing, the reference's <c>World::Get()</c> / <c>world</c>.
    /// </summary>
    public static World Current { get; set; } = new();

    /// <summary>Map width in tiles, <c>w()</c>; zero while no map is loaded.</summary>
    public int Width { get; private set; }

    /// <summary>Map height in tiles, <c>h()</c>; zero while no map is loaded.</summary>
    public int Height { get; private set; }

    /// <summary>The tiles in map order, <c>vec_tiles</c>.</summary>
    public IReadOnlyList<Tile> Tiles => _tiles;

    /// <summary>
    /// Number of tiles on the map, <c>getSize()</c>. The reference returns
    /// <c>vec_tiles.size()</c> — not <c>w() * h()</c> — so a half-built map reports the tiles it has.
    /// </summary>
    public int Size => _tiles.Count;

    /// <summary>
    /// The map dimensions as a <see cref="MapGeometry"/>, or <see langword="null"/> while the world is
    /// empty. <see cref="MapGeometry"/> rejects zero dimensions, and the reference's world starts at
    /// <c>0 × 0</c>, so the empty case needs the nullable.
    /// </summary>
    public MapGeometry? Geometry => Width > 0 && Height > 0 ? new MapGeometry(Width, Height) : null;

    /// <summary>
    /// The tile at the given coordinates, <c>getTile( x, y )</c>.
    /// </summary>
    /// <param name="x">Column, from the left.</param>
    /// <param name="y">Row, from the top.</param>
    /// <exception cref="ArgumentOutOfRangeException">The computed index is outside the tile vector.</exception>
    /// <remarks>
    /// The reference indexes <c>y * width + x</c> and checks nothing else, so the two coordinates are
    /// <b>not</b> validated: <c>x == width</c> quietly lands on the first tile of the next row and a
    /// negative <c>x</c> borrows from the previous row. Only the resulting index decides. The quirk is
    /// kept — callers pass a point that came out of <see cref="MapGeometry"/>, where it cannot happen.
    /// </remarks>
    public Tile GetTile(int x, int y) => GetTile(y * Width + x);

    /// <summary>
    /// The tile at the given index, <c>getTile( tileId )</c>.
    /// </summary>
    /// <param name="tileIndex">Index in the tile vector.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="tileIndex"/> is outside the tile vector. The reference only throws here in a
    /// debug build (<c>std::vector::at</c>); the release build reads out of bounds. The port always
    /// throws, following the "assert becomes an exception" rule of ADR-037.
    /// </exception>
    public Tile GetTile(int tileIndex)
    {
        if ((uint)tileIndex >= (uint)_tiles.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(tileIndex), tileIndex, $"The world has {_tiles.Count} tiles.");
        }

        return _tiles[tileIndex];
    }

    /// <summary>
    /// Creates an empty square map of <paramref name="size"/> × <paramref name="size"/> tiles,
    /// <c>generateUninitializedMap( size )</c> (<c>world.cpp:358</c>). The tiles carry the default
    /// values only: no terrain, no objects, every direction passable.
    /// </summary>
    /// <param name="size">Map side length in tiles.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> is not positive (the reference only asserts <c>size &gt; 0</c>).
    /// </exception>
    /// <remarks>
    /// <para>
    /// The reference does three more things that the port cannot do yet: it publishes a
    /// <c>Maps::FileInfo</c> of the same size into <c>Settings</c>, calls <c>Defaults()</c> (which
    /// seeds the kingdoms, heroes and castles) and asserts that the tile vector came out empty. Those
    /// steps arrive with the systems they touch.
    /// </para>
    /// <para>
    /// <b>The tile indexes are deliberately left at zero</b>: the reference resizes the vector and
    /// stops, and the loaders set <c>setIndex</c> while they fill the tiles
    /// (<c>world_loadmap.cpp:279</c>; <c>generateBattleOnlyMap</c> does it in its own loop). Setting
    /// them here would hide a loader that forgets to.
    /// </para>
    /// </remarks>
    public void GenerateUninitializedMap(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A map must have a positive size.");
        }

        Reset();

        Width = size;
        Height = size;

        for (int i = 0; i < size * size; ++i)
        {
            _tiles.Add(new Tile());
        }
    }

    /// <summary>
    /// Empties the world, <c>Reset()</c> (<c>world.cpp:299</c>): dimensions back to zero, tile vector
    /// cleared. The reference also clears the kingdoms, events, rumors, castles, heroes, captured
    /// objects and map objects and resets the seed and the win/loss conditions; those containers do
    /// not exist yet and are added with their systems.
    /// </summary>
    /// <remarks>
    /// The reference keeps this <c>private</c> (the destructor and the two map loaders call it). The
    /// port exposes it because scenes and tests need a way back to an empty world, and a hidden reset
    /// would otherwise be reachable only through a loader that is not ported yet.
    /// </remarks>
    public void Reset()
    {
        Width = 0;
        Height = 0;
        _tiles.Clear();
    }

    /// <summary>
    /// Invalidates the pathfinder caches, <c>resetPathfinder()</c> (<c>world.cpp:1171</c>): the
    /// reference resets <c>_pathfinder</c> and the AI planner's copy.
    /// </summary>
    /// <remarks>
    /// Neither the world pathfinder (<c>world_pathfinding.cpp</c>) nor the AI planner is ported, so
    /// this is a placeholder: it exists because <c>Maps::Tile::setMainObjectType</c> and the map
    /// mutation helpers have to call it, and adding the call sites later would mean touching every one
    /// of them twice. When the pathfinder lands it gets its reset here and nowhere else.
    /// </remarks>
    public void ResetPathfinder()
    {
    }

    /// <summary>
    /// The last object UID handed out, <c>Maps::getLastObjectUID()</c>
    /// (<c>world_object_uid.cpp:42</c>). Every object of a map has a UID; a new object gets the next one
    /// so that parts of different objects can never be confused.
    /// </summary>
    /// <remarks>
    /// The reference keeps this counter in a file-scope variable of <c>world_object_uid.cpp</c> with four
    /// functions around it. The port keeps it on the world instead — one world, one counter, and tests can
    /// have their own. <c>world_object_uid.cpp</c> is therefore covered by this property and the three
    /// members below.
    /// </remarks>
    public uint LastObjectUid { get; private set; }

    /// <summary>
    /// Overwrites the UID counter, <c>Maps::setLastObjectUID()</c> (<c>world_object_uid.cpp:47</c>). The
    /// map loaders set it from the value the file stores at its end, the editor history restores it.
    /// </summary>
    /// <param name="uid">Value to continue from.</param>
    public void SetLastObjectUid(uint uid) => LastObjectUid = uid;

    /// <summary>
    /// Hands out the next object UID, <c>Maps::getNewObjectUID()</c> (<c>world_object_uid.cpp:35</c>).
    /// </summary>
    /// <returns>The new, never used before, UID.</returns>
    public uint GetNewObjectUid() => ++LastObjectUid;

    /// <summary>
    /// Resets the UID counter to zero, <c>Maps::resetObjectUID()</c> (<c>world_object_uid.cpp:30</c>).
    /// </summary>
    /// <remarks>
    /// The reference calls this only from the editor, when it drops every object of the map. The map
    /// loaders do not call it — <see cref="Reset"/> does not touch the counter either, which is why
    /// <see cref="LoadMap(Mp2Map)"/> sets it from the file.
    /// </remarks>
    public void ResetObjectUids() => LastObjectUid = 0;

    /// <summary>
    /// Builds the world from a parsed map file: the tiles, their add-ons and the passability,
    /// the portable part of <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:241</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is here</b>: the tile loop of the reference — <c>setIndex</c>, <c>setTerrain</c>,
    /// <c>Tile::Init</c>, the add-on chain of every tile, <c>sortObjectParts</c>, then the object type
    /// repairs of <c>ProcessNewMP2Map</c> and <c>PostLoad</c> (passability, pathfinder reset, UID counter
    /// fixup).
    /// </para>
    /// <para>
    /// <b>What is not here</b>: everything the port has no system for yet. The castle table, the
    /// capturable object table and the information blocks (heroes, castles, signs, bottles, events,
    /// sphinxes, rumors, the obelisk counter) are skipped; the Price of Loyalty metadata is not read;
    /// the teleport, whirlpool and Eye of Magi caches of <c>PostLoad</c> and the AI regions of
    /// <c>ComputeStaticAnalysis</c> are not computed. None of them affects which tiles exist or whether
    /// they can be walked on, which is what this method is for.
    /// </para>
    /// </remarks>
    /// <param name="map">A map that was read from a file or created blank.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The dimensions of <paramref name="map"/> do not match its tile count.
    /// </exception>
    public void LoadMap(Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (map.Width <= 0 || map.Height <= 0 || (long)map.Width * map.Height != map.Tiles.Count)
        {
            throw new ArgumentException(
                $"The map is {map.Width}x{map.Height} but holds {map.Tiles.Count} tiles.", nameof(map));
        }

        Reset();

        Width = map.Width;
        Height = map.Height;

        SetLastObjectUid(map.LastObjectUid);

        for (int index = 0; index < map.Tiles.Count; ++index)
        {
            Mp2TileInfo mp2Tile = map.Tiles[index];

            var tile = new Tile();

            tile.Index = index;
            tile.SetTerrain(mp2Tile.TerrainImageIndex, mp2Tile.TerrainFlags);
            tile.Init(mp2Tile);

            // The add-ons of the tile: the extra pieces of a multi tile object. The chain walk stops at
            // index 0 and at an out of range index, where the reference logs a warning and breaks.
            foreach (int addonIndex in map.EnumerateAddonChain(mp2Tile))
            {
                Mp2AddonInfo addon = map.Addons[addonIndex];

                if (addon.GroundIcnType != Mp2ObjectIcnType.Unknown)
                {
                    tile.PushGroundObjectPart(new ObjectPart(
                        (ObjectLayerType)addon.GroundLayer, addon.Level1ObjectUid, addon.GroundIcnType, addon.BottomIcnImageIndex));
                }

                if (addon.TopIcnType != Mp2ObjectIcnType.Unknown)
                {
                    tile.PushTopObjectPart(new ObjectPart(
                        ObjectLayerType.Object, addon.Level2ObjectUid, addon.TopIcnType, addon.TopIcnImageIndex));
                }
            }

            tile.SortObjectParts();

            _tiles.Add(tile);
        }

        foreach (Tile tile in _tiles)
        {
            tile.FixMP2MapTileObjectType(this);
        }

        PostLoad(true, false);
    }

    /// <summary>
    /// Reads an MP2 map file and builds the world from it, <c>World::LoadMapMP2()</c>
    /// (<c>world_loadmap.cpp:141</c>).
    /// </summary>
    /// <remarks>
    /// The reference returns <see langword="false"/> when the file is not a usable map; the port follows
    /// the same contract instead of throwing, because a broken map file is a normal thing to find in a
    /// game directory. <see cref="LoadMap(Mp2Map)"/> stays the throwing core.
    /// </remarks>
    /// <param name="filePath">Path of an <c>.mp2</c> or <c>.mx2</c> map file.</param>
    /// <param name="rejectPriceOfLoyaltyObjects">
    /// When <see langword="true"/>, a map that places The Price of Loyalty objects is refused, which is
    /// what the reference does for a file that is not recognised as an expansion map.
    /// </param>
    /// <returns><see langword="true"/> when the world was built.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    public bool TryLoadMap(string filePath, bool rejectPriceOfLoyaltyObjects = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!Mp2Map.TryRead(filePath, out Mp2Map? map, rejectPriceOfLoyaltyObjects) || map is null)
        {
            return false;
        }

        LoadMap(map);

        return true;
    }

    /// <summary>
    /// The part of <c>World::PostLoad()</c> (<c>world.cpp:1194</c>) that the port can run today:
    /// the passability, the pathfinder reset and the UID counter fixup.
    /// </summary>
    /// <remarks>
    /// The reference also caches the teleport, whirlpool and Eye of Magi positions and computes the AI
    /// regions; those belong to the systems that will consume them, and the world is consistent without
    /// them.
    /// </remarks>
    /// <param name="setTilePassabilities">Run <see cref="UpdatePassabilities"/>.</param>
    /// <param name="updateUidCounterToMaximum">
    /// Take the highest object UID found on the tiles instead of only repairing a counter that is too
    /// small. The resurrection map loader is the only caller that passes <see langword="true"/>.
    /// </param>
    private void PostLoad(bool setTilePassabilities, bool updateUidCounterToMaximum)
    {
        if (setTilePassabilities)
        {
            UpdatePassabilities();
        }

        ResetPathfinder();

        // Some maps — official campaign maps included — store a UID counter that is lower than the UIDs of
        // their own objects, which would make a new object collide with an existing one.
        uint maxUid = 0;

        foreach (Tile tile in _tiles)
        {
            maxUid = Math.Max(maxUid, tile.MainObjectPart.Uid);

            foreach (ObjectPart part in tile.GroundObjectParts)
            {
                maxUid = Math.Max(maxUid, part.Uid);
            }

            foreach (ObjectPart part in tile.TopObjectParts)
            {
                maxUid = Math.Max(maxUid, part.Uid);
            }
        }

        if (updateUidCounterToMaximum)
        {
            SetLastObjectUid(maxUid);
        }
        else if (LastObjectUid < maxUid)
        {
            SetLastObjectUid(maxUid);
        }
    }

    /// <summary>
    /// Recomputes the object types and the passability of every tile, <c>updatePassabilities()</c>
    /// (<c>world.cpp:1177</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two passes on purpose: the first one fills in the object types that were left empty and stores the
    /// tile independent passability, the second one narrows it down by the neighbours. The second pass
    /// reads the tile independent passability of the neighbours, so it cannot run interleaved with the
    /// first.
    /// </para>
    /// <para>
    /// This is what a loaded map and every map mutation end with — without it a hero would walk through
    /// cliffs or be stopped by a road.
    /// </para>
    /// </remarks>
    public void UpdatePassabilities()
    {
        foreach (Tile tile in _tiles)
        {
            if (tile.MainObjectType == Mp2ObjectType.None)
            {
                tile.UpdateObjectType(this);
            }

            tile.SetInitialPassability();
        }

        foreach (Tile tile in _tiles)
        {
            tile.UpdatePassability(this);
        }
    }
}
