using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Random;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game;

/// <summary>
/// The world map: its size, its tiles and (later) its heroes, castles, kingdoms and the pathfinder.
/// The port of <c>World</c> (<c>参考：src/fheroes2/world/world.h:197</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the skeleton, not the whole class.</b> The reference keeps more than twenty containers
/// and a hundred members in <c>World</c>; only what the already ported code needs is here: the map
/// dimensions, the tile vector, the hero list (as far as <see cref="Maps.Tile"/> needs to resolve the
/// hero standing on it), the object queries of <c>maps.cpp</c> and the two loaders' bookkeeping around
/// them. Everything that depends on systems the port does not have yet (castles, kingdoms, regions, the
/// ultimate artifact, the week type, static analysis) is added together with the system it belongs to —
/// see <c>docs/DECISIONS.md</c>, ADR-065, ADR-068, ADR-069, ADR-076 and ADR-078.
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
public sealed partial class World
{
    /// <summary>Number of days in a game week, <c>numOfDaysPerWeek</c>.</summary>
    public const int NumOfDaysPerWeek = 7;

    /// <summary>Number of weeks in a game month, <c>numOfWeeksPerMonth</c>.</summary>
    public const int NumOfWeeksPerMonth = 4;

    /// <summary>
    /// Smallest block that can be a rumor, <c>MP2::MP2_RUMOR_STRUCTURE_MIN_SIZE</c> (<c>mp2.h:40</c>).
    /// </summary>
    private const int RumorMinimumStructureSize = 9;

    /// <summary>Offset of the rumor text, after the marker and the seven unused bytes.</summary>
    private const int RumorTextOffset = 8;

    private readonly List<Tile> _tiles = [];
    private readonly AllHeroes _heroes = new();

    /// <summary>The pathfinder of the heroes of this world, <c>_pathfinder</c> (<c>world.h:701</c>).</summary>
    /// <remarks>
    /// Created on first use and dropped by <see cref="Reset"/>, because it caches one node per tile and
    /// the dimensions of the map. The reference holds it by value and resets it; the port cannot reset a
    /// pathfinder whose map is gone, so a new map gets a new pathfinder.
    /// </remarks>
    private PlayerWorldPathfinder? _pathfinder;

    /// <summary>Signs, bottles, sphinxes and ground events, keyed by tile index, <c>map_objects</c>.</summary>
    private readonly MapObjects _mapObjects = new();

    /// <summary>Timed events of the map, <c>vec_eventsday</c>.</summary>
    private readonly List<EventDate> _dailyEvents = [];

    /// <summary>Objects that belong to a player, keyed by tile index, <c>map_captureobj</c>.</summary>
    private readonly CapturedObjects _capturedObjects = new();

    /// <summary>Rumors the map ships instead of the original ones, <c>_customRumors</c>.</summary>
    private readonly List<string> _rumors = [];

    /// <summary>Days since the start of the game, <c>_day</c> (<c>world.h:665</c>).</summary>
    /// <remarks>
    /// Zero on a fresh world: the reference counts the first playable day as one and only reaches it
    /// through <see cref="NewDay"/>. The display values (<see cref="Day"/>, <see cref="Week"/>) are
    /// one-based and derived from these three counters, never stored.
    /// </remarks>
    private uint _day;

    /// <summary>Weeks since the start of the game, <c>_week</c>; zero on a fresh world.</summary>
    private uint _week;

    /// <summary>Months since the start of the game, <c>_month</c>; zero on a fresh world.</summary>
    private uint _month;

    /// <summary>Tiles of the stone liths, grouped by sprite index, <c>_allTeleports</c>.</summary>
    private readonly Dictionary<byte, List<int>> _allTeleports = [];

    /// <summary>Tiles of the whirlpool parts, grouped by sprite index, <c>_allWhirlpools</c>.</summary>
    private readonly Dictionary<byte, List<int>> _allWhirlpools = [];

    /// <summary>Tiles of the Eye of Magi objects, <c>_allEyeOfMagi</c>.</summary>
    private readonly List<int> _allEyeOfMagi = [];

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
    /// The heroes of this world, <c>vec_heroes</c> (<c>world.h:555</c>, an <c>AllHeroes</c>).
    /// </summary>
    /// <remarks>
    /// A tile stores only the id of the hero standing on it (<see cref="Tile.OccupantHeroId"/>), so the
    /// object has to be looked up here — exactly like <c>Maps::Tile::getHero()</c> does with
    /// <c>world.GetHeroes()</c>. The container is filled by <see cref="LoadMap"/> through
    /// <see cref="AllHeroes.Init"/> and indexed by id; an empty world has no heroes at all.
    /// </remarks>
    public AllHeroes Heroes => _heroes;

    /// <summary>
    /// The hero with the given id, <c>AllHeroes::Get( heroId )</c> (<c>heroes.cpp:2411</c>).
    /// </summary>
    /// <param name="heroId">Hero id.</param>
    /// <returns>
    /// The hero, or <see langword="null"/> when the id is not a hero id or the slot is not filled for this
    /// map (The Price of Loyalty heroes of a map without the expansion, the debug hero).
    /// </returns>
    public Hero? GetHero(int heroId) => _heroes.Get(heroId);

    /// <summary>
    /// Picks a hero for a tavern of <paramref name="race"/>, <c>World::GetHeroForHire()</c>
    /// (<c>world.cpp</c>, the thin wrapper around <c>AllHeroes::GetHeroForHire()</c>).
    /// </summary>
    /// <param name="race">Race to prefer.</param>
    /// <param name="heroIdToIgnore">Hero that must not be picked.</param>
    /// <returns>The hero, or <see langword="null"/> when every hero is hired or imprisoned.</returns>
    public Hero? GetHeroForHire(MapRace race, int heroIdToIgnore = Hero.UnknownId) =>
        _heroes.GetHeroForHire(race, heroIdToIgnore);

    /// <summary>
    /// Stone liths grouped by their sprite index, <c>_allTeleports</c> (<c>world.cpp:1201</c>).
    /// </summary>
    /// <remarks>
    /// Two stone liths only lead to each other when they use the same sprite: the sprite index is what
    /// pairs the entrances, so the cache is keyed by it. Filled by the post load of the map loader.
    /// </remarks>
    public IReadOnlyDictionary<byte, List<int>> AllTeleports => _allTeleports;

    /// <summary>
    /// Whirlpool parts grouped by their sprite index, <c>_allWhirlpools</c> (<c>world.cpp:1215</c>).
    /// </summary>
    /// <remarks>
    /// A whirlpool can carry a boat a hero left behind, and such a tile is marked as a boat instead of a
    /// whirlpool, so the cache is built from the parts and not from the tile types.
    /// </remarks>
    public IReadOnlyDictionary<byte, List<int>> AllWhirlpools => _allWhirlpools;

    /// <summary>Tiles of the Eye of Magi, <c>_allEyeOfMagi</c> (<c>world.cpp:1227</c>).</summary>
    public IReadOnlyList<int> AllEyeOfMagi => _allEyeOfMagi;

    /// <summary>
    /// The map objects that carry data of their own — signs, bottles and ground events, <c>map_objects</c>
    /// (<c>world.h:73</c>).
    /// </summary>
    /// <remarks>
    /// Filled by <see cref="LoadMap"/> from the information blocks of the map and keyed by tile index,
    /// which is the UID of the reference. A tile keeps its sign or event object: what lives here is the
    /// payload, not the object on the map.
    /// </remarks>
    public MapObjects MapObjects => _mapObjects;

    /// <summary>The sign or bottle of a tile, <c>map_objects.get( uid )</c> as a <c>MapSign</c>.</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns>The sign, or <see langword="null"/> when the tile carries none.</returns>
    public MapSign? GetMapSign(int tileIndex) => _mapObjects.Get<MapSign>(tileIndex);

    /// <summary>The ground event of a tile, <c>World::GetMapEvent()</c> (<c>world.cpp:1195</c>).</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns>The event, or <see langword="null"/> when the tile carries none.</returns>
    public MapEvent? GetMapEvent(int tileIndex) => _mapObjects.Get<MapEvent>(tileIndex);

    /// <summary>The sphinx of a tile, <c>map_objects.get( uid )</c> as a <c>MapSphinx</c>.</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns>The sphinx, or <see langword="null"/> when the tile carries none.</returns>
    public MapSphinx? GetMapSphinx(int tileIndex) => _mapObjects.Get<MapSphinx>(tileIndex);

    /// <summary>
    /// The timed events of the map, <c>vec_eventsday</c> (<c>world.h:559</c>).
    /// </summary>
    /// <remarks>
    /// A daily event has no tile: it comes from an information block that no object points at, which is why
    /// it is a list of its own and not a map object. Filled by <see cref="LoadMap"/> and read by the day
    /// cycle, which the port does not have yet.
    /// </remarks>
    public IReadOnlyList<EventDate> DailyEvents => _dailyEvents;

    /// <summary>
    /// The rumors the map carries, <c>_customRumors</c> (<c>world.h:558</c>).
    /// </summary>
    /// <remarks>
    /// A map may ship its own rumors instead of the ones of the original game; they are plain strings from
    /// information blocks that no object points at (<c>world_loadmap.cpp:663-680</c>).
    /// </remarks>
    public IReadOnlyList<string> Rumors => _rumors;

    /// <summary>
    /// Adds a timed event, <c>World::AddEventDate()</c> (<c>world.h:470</c>).
    /// </summary>
    /// <param name="eventDate">The event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventDate"/> is <see langword="null"/>.</exception>
    public void AddEventDate(EventDate eventDate)
    {
        ArgumentNullException.ThrowIfNull(eventDate);

        _dailyEvents.Add(eventDate);
    }

    /// <summary>
    /// The day of the current week, one based, <c>World::GetDay()</c> (<c>world.h:371</c>).
    /// </summary>
    /// <remarks>
    /// The reference computes <c>LastDay() ? numOfDaysPerWeek : _day % numOfDaysPerWeek</c>: the seventh
    /// day of a week is a modulo remainder of zero, and the <c>LastDay()</c> test turns it back into 7.
    /// A fresh world (<c>_day == 0</c>) therefore reports 0, not 1 — the reference asserts nothing here
    /// and neither does the port.
    /// </remarks>
    public uint Day => IsLastDay ? (uint)NumOfDaysPerWeek : _day % NumOfDaysPerWeek;

    /// <summary>
    /// The week of the current month, one based, <c>World::GetWeek()</c> (<c>world.h:376</c>).
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="Day"/>: the fourth week is a zero remainder promoted to 4.
    /// </remarks>
    public uint Week => IsLastWeek ? (uint)NumOfWeeksPerMonth : _week % NumOfWeeksPerMonth;

    /// <summary>The month since the start of the game, <c>World::GetMonth()</c> (<c>world.h:381</c>).</summary>
    public uint Month => _month;

    /// <summary>
    /// Total days elapsed since the start of the game, <c>World::CountDay()</c> (<c>world.h:386</c>).
    /// </summary>
    public uint DayCount => _day;

    /// <summary>
    /// Total weeks elapsed since the start of the game, <c>World::CountWeek()</c> (<c>world.h:391</c>).
    /// </summary>
    public uint WeekCount => _week;

    /// <summary>
    /// Whether today is the first day of a week, <c>World::BeginWeek()</c> (<c>world.h:396</c>).
    /// </summary>
    /// <remarks>
    /// Note the difference from <see cref="Day"/>: this is a bare modulo test, so a fresh world with
    /// <c>_day == 0</c> is <b>not</b> at the beginning of a week.
    /// </remarks>
    public bool IsBeginWeek => _day % NumOfDaysPerWeek == 1;

    /// <summary>
    /// Whether today is the first day of the first week of a month, <c>World::BeginMonth()</c>
    /// (<c>world.h:401</c>).
    /// </summary>
    public bool IsBeginMonth => IsFirstWeek && IsBeginWeek;

    /// <summary>
    /// Whether today is the seventh day of a week, <c>World::LastDay()</c> (<c>world.h:406</c>).
    /// </summary>
    public bool IsLastDay => _day % NumOfDaysPerWeek == 0;

    /// <summary>
    /// Whether this is the first week of a month, <c>World::FirstWeek()</c> (<c>world.h:411</c>).
    /// </summary>
    public bool IsFirstWeek => _week % NumOfWeeksPerMonth == 1;

    /// <summary>
    /// Whether this is the fourth week of a month, <c>World::LastWeek()</c> (<c>world.h:416</c>).
    /// </summary>
    public bool IsLastWeek => _week % NumOfWeeksPerMonth == 0;

    /// <summary>
    /// The date as the status panel prints it, <c>World::DateString()</c> (<c>world.cpp:888</c>):
    /// <c>"month: 1, week: 3, day: 5"</c>.
    /// </summary>
    /// <remarks>
    /// The reference builds the string by hand and the port keeps its wording — the status panel of the
    /// original has no room for a localized form here, and translating it is the port's job later, not
    /// the date's.
    /// </remarks>
    /// <returns>The three one-based display values.</returns>
    public string DateString() => $"month: {Month}, week: {Week}, day: {Day}";

    /// <summary>
    /// Advances the clock by one day, <c>World::NewDay()</c> (<c>world.cpp:425</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is here</b>: the three counters with the reference's exact nesting, and the removal of the
    /// daily events that can no longer fire (<c>world.cpp:456-459</c>).
    /// </para>
    /// <para>
    /// <b>What is not here</b>: the per-system ticks of the reference — <c>NewMonth()</c> (the month of
    /// monsters), <c>NewWeek()</c> (the week-life objects, the neutral monster refresh, the recruit
    /// reset) and <c>vec_kingdoms/vec_castles/vec_heroes.NewDay()/NewWeek()</c>. Each of them needs a
    /// system the port does not have (the week type, kingdoms, castles, the monster spawner), so each is
    /// added here — in this order — together with that system. The nesting of the counters is already
    /// final, which is the part that is easy to get wrong and expensive to change.
    /// </para>
    /// </remarks>
    public void NewDay()
    {
        ++_day;

        if (IsBeginWeek)
        {
            ++_week;

            if (IsBeginMonth)
            {
                ++_month;
            }
        }

        // The reference removes events that are past their only occurrence after every tick, checking
        // against the day that just ended (world.cpp:459).
        _dailyEvents.RemoveAll(eventDate => eventDate.IsDeprecated(_day - 1));
    }

    /// <summary>
    /// The tiles whose object type is <paramref name="objectType"/>, <c>Maps::GetObjectPositions()</c>
    /// (<c>maps.cpp:613</c>) over <c>MapsIndexesObject()</c> (<c>:65</c>).
    /// </summary>
    /// <remarks>
    /// <b>The flag is inverted twice, on purpose.</b> The reference compares
    /// <c>getMainObjectType( !ignoreHeroes )</c> against the type: with <paramref name="ignoreHeroes"/>
    /// <see langword="true"/> (the default of <c>GetObjectPositions</c>) the comparison looks
    /// <b>through</b> a hero, so a hero standing on a stone liths still reports the liths. Passing
    /// <see langword="false"/> asks for the cached type instead, which is what the hero itself occupies.
    /// </remarks>
    /// <param name="objectType">Object type to look for.</param>
    /// <param name="ignoreHeroes">
    /// <see langword="true"/> to look through a hero standing on the tile (the reference default).
    /// </param>
    /// <returns>The tile indexes in map order.</returns>
    public List<int> GetObjectPositions(Mp2ObjectType objectType, bool ignoreHeroes = true)
    {
        var result = new List<int>();

        for (int index = 0; index < _tiles.Count; ++index)
        {
            if (_tiles[index].GetMainObjectType(this, !ignoreHeroes) == objectType)
            {
                result.Add(index);
            }
        }

        return result;
    }

    /// <summary>
    /// Every tile that carries a part of <paramref name="objectType"/>, with that part,
    /// <c>Maps::getObjectParts()</c> (<c>maps.cpp:618</c>).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetObjectPositions"/>, this does not look at the tile's cached type but at the
    /// parts, which is how a whirlpool hidden under a boat is found. The part is a copy of the struct in
    /// the tile, not a handle to it.
    /// </remarks>
    /// <param name="objectType">Object type to look for, action bit included.</param>
    /// <returns>The tile index and the part of each match, in map order.</returns>
    public List<(int Index, ObjectPart Part)> GetObjectParts(Mp2ObjectType objectType)
    {
        var result = new List<(int Index, ObjectPart Part)>();

        for (int index = 0; index < _tiles.Count; ++index)
        {
            ObjectPart? part = _tiles[index].GetObjectPartByActionType(objectType);

            if (part is not null)
            {
                result.Add((index, part.Value));
            }
        }

        return result;
    }

    /// <summary>
    /// Whether any tile holds <paramref name="objectType"/>, <c>Maps::doesObjectExistOnMap()</c>
    /// (<c>maps.cpp:601</c>).
    /// </summary>
    /// <param name="objectType">Object type to look for.</param>
    /// <returns><see langword="true"/> when at least one tile has it.</returns>
    /// <remarks>
    /// This one uses the type the hero covers (<c>getMainObjectType( false )</c>), so the objects of a
    /// tile stay visible while a hero stands there.
    /// </remarks>
    public bool DoesObjectExistOnMap(Mp2ObjectType objectType)
    {
        foreach (Tile tile in _tiles)
        {
            if (tile.GetMainObjectType(this, false) == objectType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// How many obelisks the map carries, <c>World::CountObeliskOnMaps()</c> (<c>world.cpp:900</c>).
    /// </summary>
    /// <remarks>
    /// The reference clamps the answer to <b>at least 6</b>: the puzzle needs six pieces to be revealed by
    /// the Eye of Magi, and a map that has fewer obelisks (or none) would otherwise be unsolvable. Note
    /// that the count uses the type the hero covers, so a hero standing on an obelisk still counts it —
    /// which is the reference's behaviour and the only sensible one here.
    /// </remarks>
    /// <returns>The number of obelisks, but never less than six.</returns>
    public uint CountObeliskOnMaps()
    {
        uint count = 0;

        foreach (Tile tile in _tiles)
        {
            if (tile.GetMainObjectType(this, false) == Mp2ObjectType.Obelisk)
            {
                ++count;
            }
        }

        return count > 0 ? count : 6;
    }

    /// <summary>
    /// Gives a tile to a player, <c>World::CaptureObject()</c> (<c>world.cpp:815</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Any object on the map can carry an owner — the reference explicitly notes that this is not
    /// restricted to <c>MP2::isCaptureObject()</c>, dwellings use the same marking. The object type is
    /// read through the hero on the tile, so a hero standing on a mine does not turn the mine into a
    /// hero-owned hero object.
    /// </para>
    /// <para>
    /// Two branches of the reference are missing, both because their systems are: the castle entrance
    /// changes color (<c>getCastleEntrance</c> + <c>Castle::ChangeColor</c>) and the flag sprite of the
    /// object is repainted (<c>Tile::setOwnershipFlag</c>, a FLAG32 object part update). The data model
    /// is complete without them — the color of a captured object is what the game logic reads.
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index.</param>
    /// <param name="color">New owner.</param>
    public void CaptureObject(int index, PlayerColor color)
    {
        Debug.Assert(PlayerColors.Count(color) <= 1, "A captured object belongs to exactly one player.");

        _capturedObjects.Set(index, GetTile(index).GetMainObjectType(this, false), color);
    }

    /// <summary>
    /// Owner of a tile, <c>World::ColorCapturedObject()</c> (<c>world.h:446</c>).
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns>The owner, or <see cref="PlayerColor.None"/> when the tile is not tracked.</returns>
    public PlayerColor ColorCapturedObject(int index) => _capturedObjects.GetColor(index);

    /// <summary>
    /// Whether the world already tracks the tile, <c>map_captureobj.find( index ) != end()</c>.
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns><see langword="true"/> when the tile has an entry, even one without an owner.</returns>
    public bool HasCapturedObject(int index) => _capturedObjects.Contains(index);

    /// <summary>How many tiles the world tracks as captured objects, <c>map_captureobj.size()</c>.</summary>
    public int CapturedObjectCount => _capturedObjects.Count;

    /// <summary>
    /// The entry of a captured object, created when the tile is not tracked yet,
    /// <c>World::GetCapturedObject()</c> (<c>world.h:456</c>).
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns>The entry.</returns>
    public CapturedObject GetCapturedObject(int index) => _capturedObjects.Get(index);

    /// <summary>
    /// How many objects of one type one player owns, <c>World::CountCapturedObject()</c>
    /// (<c>world.h:438</c>).
    /// </summary>
    /// <param name="objectType">Object type to count.</param>
    /// <param name="color">Owner to count for.</param>
    /// <returns>Number of matching objects.</returns>
    public uint CountCapturedObject(Mp2ObjectType objectType, PlayerColor color) => _capturedObjects.GetCount(objectType, color);

    /// <summary>
    /// How many mines of one resource one player owns, <c>World::CountCapturedMines()</c>
    /// (<c>world.cpp:801</c>).
    /// </summary>
    /// <remarks>
    /// The sawmill and the alchemist lab are counted as the wood and the mercury mines they are, but
    /// they are stored as their own object types, so those two resources are answered by object type
    /// and everything else by reading the resource out of the metadata of each mine.
    /// </remarks>
    /// <param name="resourceType"><c>Resource</c> constant.</param>
    /// <param name="color">Owner to count for.</param>
    /// <returns>Number of mines of that resource.</returns>
    public uint CountCapturedMines(int resourceType, PlayerColor color) => resourceType switch
    {
        Resource.Wood => CountCapturedObject(Mp2ObjectType.Sawmill, color),
        Resource.Mercury => CountCapturedObject(Mp2ObjectType.AlchemistLab, color),
        _ => _capturedObjects.GetCountMines(this, resourceType, color),
    };

    /// <summary>
    /// Drops every object of a color, <c>World::ResetCapturedObjects()</c> (<c>world.h:451</c>).
    /// </summary>
    /// <param name="color">The player that loses the objects.</param>
    public void ResetCapturedObjects(PlayerColor color) => _capturedObjects.ResetColor(color);

    /// <summary>
    /// The other ends of the stone liths at <paramref name="index"/>,
    /// <c>World::GetTeleportEndPoints()</c> (<c>world.cpp:704</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two stone liths only lead to each other when they use the same sprite, so the candidates come from
    /// the cache entry of the sprite of the entrance. A candidate is dropped when it is the entrance
    /// itself, when its <b>cached</b> type is no longer stone liths (a hero standing there takes the type
    /// over) or when it is on the other side of the water/land divide.
    /// </para>
    /// <para>
    /// The entrance test uses <c>getMainObjectType( false )</c> while the candidates use the cached type
    /// — that asymmetry is the reference's (<c>world.cpp:710</c> vs <c>:725</c>): a hero on the entrance
    /// may still use it (he stands <i>on</i> the liths), a hero on the destination blocks it.
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index of the entrance.</param>
    /// <returns>The tile indexes of the other ends, empty when there is none.</returns>
    public List<int> GetTeleportEndPoints(int index)
    {
        var result = new List<int>();
        Tile entranceTile = GetTile(index);

        if (entranceTile.GetMainObjectType(this, false) != Mp2ObjectType.ActionStoneLiths)
        {
            return result;
        }

        ObjectPart? entrancePart = entranceTile.GetObjectPartByActionType(Mp2ObjectType.ActionStoneLiths);

        if (entrancePart is null)
        {
            // A broken map: the type says stone liths but no part does. The reference asserts and gives up.
            return result;
        }

        if (!_allTeleports.TryGetValue(entrancePart.Value.IcnIndex, out List<int>? teleports))
        {
            // PostLoad did not cache this sprite: the map was not loaded through LoadMap.
            return result;
        }

        foreach (int teleportIndex in teleports)
        {
            Tile teleportTile = GetTile(teleportIndex);

            if (teleportIndex == index ||
                teleportTile.MainObjectType != Mp2ObjectType.ActionStoneLiths ||
                teleportTile.IsWater != entranceTile.IsWater)
            {
                continue;
            }

            result.Add(teleportIndex);
        }

        return result;
    }

    /// <summary>
    /// A random other end of the stone liths at <paramref name="index"/>,
    /// <c>World::NextTeleport()</c> (<c>world.cpp:737</c>).
    /// </summary>
    /// <param name="index">Tile index of the entrance.</param>
    /// <returns>The destination, or <paramref name="index"/> itself when there is none.</returns>
    public int NextTeleport(int index)
    {
        List<int> teleports = GetTeleportEndPoints(index);

        return teleports.Count > 0 ? Rand.Get(teleports) : index;
    }

    /// <summary>
    /// The other ends of the whirlpool at <paramref name="index"/>,
    /// <c>World::GetWhirlpoolEndPoints()</c> (<c>world.cpp:748</c>).
    /// </summary>
    /// <remarks>
    /// The candidates are matched by sprite and then filtered by UID, because one whirlpool is made of
    /// several tiles that share a UID and a hero must not be thrown back onto his own whirlpool. As with
    /// the stone liths the entrance is looked at through a hero and the candidates are not.
    /// </remarks>
    /// <param name="index">Tile index of the entrance.</param>
    /// <returns>The tile indexes of the other ends, empty when there is none.</returns>
    public List<int> GetWhirlpoolEndPoints(int index)
    {
        var result = new List<int>();
        Tile entranceTile = GetTile(index);

        if (entranceTile.GetMainObjectType(this, false) != Mp2ObjectType.ActionWhirlpool)
        {
            return result;
        }

        ObjectPart? entrancePart = entranceTile.GetObjectPartByActionType(Mp2ObjectType.ActionWhirlpool);

        if (entrancePart is null)
        {
            // A broken map: the type says whirlpool but no part does. The reference asserts and gives up.
            return result;
        }

        if (!_allWhirlpools.TryGetValue(entrancePart.Value.IcnIndex, out List<int>? whirlpools))
        {
            return result;
        }

        foreach (int whirlpoolIndex in whirlpools)
        {
            Tile whirlpoolTile = GetTile(whirlpoolIndex);

            if (whirlpoolTile.MainObjectType != Mp2ObjectType.ActionWhirlpool)
            {
                continue;
            }

            ObjectPart? part = whirlpoolTile.GetObjectPartByActionType(Mp2ObjectType.ActionWhirlpool);

            if (part is null || part.Value.Uid == entrancePart.Value.Uid)
            {
                continue;
            }

            result.Add(whirlpoolIndex);
        }

        return result;
    }

    /// <summary>
    /// A random other end of the whirlpool at <paramref name="index"/>,
    /// <c>World::NextWhirlpool()</c> (<c>world.cpp:790</c>).
    /// </summary>
    /// <param name="index">Tile index of the entrance.</param>
    /// <returns>The destination, or <paramref name="index"/> itself when there is none.</returns>
    public int NextWhirlpool(int index)
    {
        List<int> whirlpools = GetWhirlpoolEndPoints(index);

        return whirlpools.Count > 0 ? Rand.Get(whirlpools) : index;
    }

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
    /// Empties the world, <c>Reset()</c> (<c>world.cpp:299</c>): dimensions back to zero, the tile vector,
    /// the hero list, the map objects, the daily events, the rumors, the captured objects and the date
    /// counters cleared. The reference also clears the kingdoms and the castles and resets the seed (and
    /// the win/loss conditions); those containers do not exist yet and are added with their systems.
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
        _heroes.Clear();
        _mapObjects.Clear();
        _dailyEvents.Clear();
        _rumors.Clear();
        _capturedObjects.Clear();

        // The clock is part of the world, not of a session (world.cpp:328-330): a new map starts at
        // day 0 / week 0 / month 0 and only World::NewDay() ever moves it.
        _day = 0;
        _week = 0;
        _month = 0;

        // The port clears the three post load caches here as well. The reference leaves them to
        // PostLoad(), but they hold tile indexes, and a cache that outlives its tiles is a landmine.
        _allTeleports.Clear();
        _allWhirlpools.Clear();
        _allEyeOfMagi.Clear();

        // Same reasoning: the pathfinder caches one node per tile and the map dimensions, so it must not
        // outlive the map it was built for.
        _pathfinder = null;
    }

    /// <summary>
    /// The pathfinder of this world's heroes, <c>_pathfinder</c>. Created on first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">The world holds no map; a pathfinder is one node per
    /// tile and cannot exist before the tiles do.</exception>
    public PlayerWorldPathfinder Pathfinder =>
        _pathfinder ??= new PlayerWorldPathfinder(this);

    /// <summary>
    /// Invalidates the pathfinder caches, <c>resetPathfinder()</c> (<c>world.cpp:1171</c>): the
    /// reference resets <c>_pathfinder</c> and the AI planner's copy.
    /// </summary>
    /// <remarks>
    /// The reference calls this whenever a tile changes in a way the search would have seen — a hero
    /// moving away, a monster killed, a road built. The port has the world pathfinder only, so the AI
    /// half is still missing; when it lands it is reset here too and nowhere else.
    /// </remarks>
    public void ResetPathfinder() => _pathfinder?.Reset();

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
    /// capturable object table and the information blocks (castles, signs, bottles, events, sphinxes,
    /// rumors, the obelisk counter) are skipped; the heroes standing on the tiles are not instantiated
    /// yet, only the hero table they are looked up in (<see cref="AllHeroes.Init"/>); the Price of
    /// Loyalty metadata is not read; the AI regions of <c>ComputeStaticAnalysis</c> are not computed.
    /// None of them affects which tiles exist or whether they can be walked on, which is what this
    /// method is for.
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

        // World::Defaults() -> vec_heroes.Init(): the hero table depends on the map version, because The
        // Price of Loyalty heroes only exist for maps of the expansions. The reference builds the table
        // before it reads the heroes of the map, which is why a hero tile can look its id up right away.
        _heroes.Init(map.Info.Version);

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

        // The information blocks of the map: a hero block describes the army, the artifacts, the experience,
        // the skills and the name of a hero whose position the tile carries, a jail block the hero waiting
        // inside the prison, a sign block a message and an event block resources, an artifact and a message
        // (world_loadmap.cpp:466-688).
        ReadInfoBlocks(map);

        foreach (Tile tile in _tiles)
        {
            tile.FixMP2MapTileObjectType(this);

            // The reference processes the objects of a new map tile by tile: the type is fixed first, then
            // the object data of the tile is applied (world_loadmap.cpp:1405), and for a hero that means
            // dropping the editor sprite and putting the hero on the tile (world_loadmap.cpp:1583).
            PlaceMapHero(tile);
        }

        PostLoad(true, false);
    }

    /// <summary>
    /// Reads the information blocks of the map, the block loop of <c>World::LoadMapMP2()</c>
    /// (<c>world_loadmap.cpp:460-688</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two halves of a hero live in different places: the tile carries where he stands and which color
    /// and race his <c>MINIHERO</c> sprite encodes, the information block carries everything else. The
    /// link between them is the 13 bit <c>quantity</c> word of the tile, which is the id of the block
    /// (<c>world_loadmap.cpp:466</c>). Signs, bottles, sphinxes and events use the same link.
    /// </para>
    /// <para>
    /// A jail is the exception among the heroes: the tile carries no sprite of the hero, so the race comes
    /// from byte 60 of the block instead, the color is <see cref="PlayerColor.None"/>, the hero gets the
    /// jail mode and the jail itself stays on the tile until the prison is visited
    /// (<c>world_loadmap.cpp:528-582</c>).
    /// </para>
    /// <para>
    /// A block that no object points at is a daily event or a rumor (<c>world_loadmap.cpp:656-684</c>);
    /// see <see cref="ReadWorldBlock"/>. That is also how a castle block ends up here: the port does not
    /// read the castle table yet, so nothing points at it.
    /// </para>
    /// <para>
    /// <b>What is missing</b>: the castle branches (a castle and a random castle, whose blocks fill the
    /// castle table). <c>Kingdom::AllowRecruitHero()</c> is the limit of heroes a kingdom may own, and the
    /// port has no kingdom, so a map with more heroes for one color than the limit is loaded as it is
    /// instead of dropping the surplus. A random race on a neutral hero is not resolved to the race of the
    /// kingdom either, for the same reason.
    /// </para>
    /// </remarks>
    /// <param name="map">The map being loaded.</param>
    private void ReadInfoBlocks(Mp2Map map)
    {
        foreach (Mp2InfoBlock block in map.InfoBlocks)
        {
            if (block.Data.Length == 0)
            {
                GameLog.Warn($"Map information block {block.ObjectId} is empty, skipped.");

                continue;
            }

            int tileIndex = FindInfoBlockTile(map, block);

            if (tileIndex < 0)
            {
                ReadWorldBlock(block);

                continue;
            }

            Mp2TileInfo tileInfo = map.Tiles[tileIndex];

            switch (tileInfo.ObjectType)
            {
                case Mp2ObjectType.Hero:
                case Mp2ObjectType.ActionHero:
                    CreateMapHero(block, tileIndex, tileInfo);
                    break;

                case Mp2ObjectType.Jail:
                case Mp2ObjectType.ActionJail:
                    CreateJailHero(block, tileIndex);
                    break;

                case Mp2ObjectType.Sign:
                case Mp2ObjectType.ActionSign:
                case Mp2ObjectType.Bottle:
                case Mp2ObjectType.ActionBottle:
                    CreateMapSign(block, tileIndex);
                    break;

                case Mp2ObjectType.Event:
                case Mp2ObjectType.ActionEvent:
                    CreateMapEvent(block, tileIndex);
                    break;

                case Mp2ObjectType.Sphinx:
                case Mp2ObjectType.ActionSphinx:
                    CreateMapSphinx(block, tileIndex);
                    break;

                default:
                    // A castle of either kind: its block fills the castle table, which the port has not.
                    break;
            }
        }
    }

    /// <summary>
    /// Reads a block that no object of the map points at: a daily event or a rumor, the
    /// <c>else if ( 0x00 == pblock[0] )</c> branch of <c>World::LoadMapMP2()</c>
    /// (<c>world_loadmap.cpp:656-684</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both kinds start with a zero byte, so the loader needs another way to tell them apart: a daily event
    /// has a <c>1</c> at byte 42 and is at least 50 bytes long, a rumor is an eight byte header followed by
    /// the text (<c>world_loadmap.cpp:659-667</c>).
    /// </para>
    /// <para>
    /// A castle block also lands here, because the port has no castle table to point it at a tile; it is at
    /// least 70 bytes long and the byte at 42 of a castle is a race or a color, which is not the constant
    /// <c>1</c> of a daily event — but a block that happens to fit is read as an event, exactly as the
    /// reference would treat a castle block it could not match to a tile.
    /// </para>
    /// </remarks>
    /// <param name="block">The information block.</param>
    private void ReadWorldBlock(Mp2InfoBlock block)
    {
        if (EventDate.TryRead(block.Data, out EventDate? eventDate))
        {
            _dailyEvents.Add(eventDate!);

            return;
        }

        if (block.Data.Length >= RumorMinimumStructureSize && block.Data[0] == 0x00)
        {
            string rumor = MapTextCodec.Decode(block.Data.AsSpan(RumorTextOffset));

            if (rumor.Length > 0)
            {
                _rumors.Add(rumor);
            }

            return;
        }

        GameLog.Warn($"Map information block {block.ObjectId}: {block.Data.Length} bytes of an unknown kind, skipped.");
    }

    /// <summary>
    /// The tile an information block belongs to, the loop over the objects of a block in
    /// <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:473-483</c>).
    /// </summary>
    /// <remarks>
    /// The reference walks the tiles it collected in <c>vec_object</c> and compares their metadata with the
    /// id of the block. The port walks the tiles too, and accepts every object the file gives a block to —
    /// including a castle, whose block is then ignored by the dispatch — so that a castle block is not
    /// mistaken for a daily event.
    /// </remarks>
    /// <param name="map">The map being loaded.</param>
    /// <param name="block">The information block.</param>
    /// <returns>The index of the tile, or <c>-1</c> when no tile carries the block.</returns>
    private static int FindInfoBlockTile(Mp2Map map, Mp2InfoBlock block)
    {
        for (int index = 0; index < map.Tiles.Count; ++index)
        {
            Mp2TileInfo tile = map.Tiles[index];

            if (HasInfoBlock(tile.ObjectType) && tile.Metadata == (uint)block.ObjectId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether the file gives the object an information block, i.e. whether a block of this type belongs to
    /// a tile instead of to the world.
    /// </summary>
    /// <remarks>
    /// The tile stores the object type as the file writes it, and the file mixes action and non-action
    /// values — <c>MP2::OBJ_JAIL</c> is a plain value while <c>MP2::OBJ_EVENT</c> is an action one — so
    /// both twins are accepted for every object.
    /// </remarks>
    /// <param name="type">Object type of a tile.</param>
    /// <returns><see langword="true"/> when a block of this type belongs to the tile.</returns>
    private static bool HasInfoBlock(Mp2ObjectType type) => type
        is Mp2ObjectType.Hero or Mp2ObjectType.ActionHero
        or Mp2ObjectType.Jail or Mp2ObjectType.ActionJail
        or Mp2ObjectType.Sign or Mp2ObjectType.ActionSign
        or Mp2ObjectType.Bottle or Mp2ObjectType.ActionBottle
        or Mp2ObjectType.Event or Mp2ObjectType.ActionEvent
        or Mp2ObjectType.Sphinx or Mp2ObjectType.ActionSphinx
        or Mp2ObjectType.Castle or Mp2ObjectType.ActionCastle
        or Mp2ObjectType.RandomCastle or Mp2ObjectType.ActionRandomCastle
        or Mp2ObjectType.RandomTown or Mp2ObjectType.ActionRandomTown;

    /// <summary>
    /// Puts a hero the map placed on a tile into the hero table, the <c>MP2::OBJ_HERO</c> branch
    /// (<c>world_loadmap.cpp:583-623</c>).
    /// </summary>
    /// <param name="block">The information block.</param>
    /// <param name="tileIndex">Index of the tile that carries the block.</param>
    /// <param name="tileInfo">The tile structure of the map file.</param>
    private void CreateMapHero(Mp2InfoBlock block, int tileIndex, Mp2TileInfo tileInfo)
    {
        if (!Mp2HeroInfo.TryRead(block.Data, out Mp2HeroInfo info))
        {
            GameLog.Warn($"Map hero block {block.ObjectId} at tile {tileIndex}: {block.Data.Length} bytes instead of {Mp2HeroInfo.StructureSize}, skipped.");

            return;
        }

        if (!Mp2MapHeroes.TryRead(in tileInfo, tileIndex % Width, tileIndex / Width, out Mp2MapHero sprite))
        {
            GameLog.Warn($"Map hero block {block.ObjectId} at tile {tileIndex}: the tile has no hero sprite, skipped.");

            return;
        }

        Hero? hero = SelectMapHero(info, sprite.Race);

        if (hero is null)
        {
            GameLog.Warn($"Map hero block {block.ObjectId} at tile {tileIndex}: no hero of race {sprite.Race} is left, skipped.");

            return;
        }

        hero.LoadFromMP2(tileIndex, sprite.Color, sprite.Race, false, info);
    }

    /// <summary>
    /// Puts the hero of a jail into the hero table, the <c>MP2::OBJ_JAIL</c> branch
    /// (<c>world_loadmap.cpp:528-582</c>).
    /// </summary>
    /// <remarks>
    /// The tile of a jail carries no <c>MINIHERO</c> sprite, so the race comes from byte 60 of the block —
    /// the byte that is the patrol distance of a patrolling hero. The hero is only parked at the index of
    /// the jail: the tile keeps the jail object and the reference does not mark it as taken by the hero.
    /// </remarks>
    /// <param name="block">The information block.</param>
    /// <param name="tileIndex">Index of the tile that carries the jail.</param>
    private void CreateJailHero(Mp2InfoBlock block, int tileIndex)
    {
        if (!Mp2HeroInfo.TryRead(block.Data, out Mp2HeroInfo info))
        {
            GameLog.Warn($"Map jail block {block.ObjectId} at tile {tileIndex}: {block.Data.Length} bytes instead of {Mp2HeroInfo.StructureSize}, skipped.");

            return;
        }

        MapRace race = MapRaces.FromIndex(info.PatrolDistanceOrJailRace);

        // The reference only knows the six playable races here and falls back to a knight with a warning
        // (world_loadmap.cpp:537-560).
        if (race is MapRace.None or MapRace.Multiple or MapRace.Random)
        {
            GameLog.Warn($"Map jail block {block.ObjectId}: race byte {info.PatrolDistanceOrJailRace} is not a playable race, a knight is imprisoned.");

            race = MapRace.Knight;
        }

        Hero? hero = SelectMapHero(info, race);

        if (hero is null)
        {
            GameLog.Warn($"Map jail block {block.ObjectId} at tile {tileIndex}: no hero of race {race} is left, skipped.");

            return;
        }

        hero.LoadFromMP2(tileIndex, PlayerColor.None, race, true, info);
    }

    /// <summary>
    /// Creates the sign or the bottle an information block describes, the <c>MP2::OBJ_SIGN</c> and
    /// <c>MP2::OBJ_BOTTLE</c> branches of <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:625-632</c>).
    /// </summary>
    /// <remarks>
    /// Nothing else is done with the object here: its text is read when a hero visits the tile, and the
    /// tile keeps carrying the sign.
    /// </remarks>
    /// <param name="block">The information block.</param>
    /// <param name="tileIndex">Index of the tile that carries the sign.</param>
    private void CreateMapSign(Mp2InfoBlock block, int tileIndex)
    {
        if (!MapSign.TryRead(block.Data, tileIndex, out MapSign? sign))
        {
            GameLog.Warn($"Map sign block {block.ObjectId} at tile {tileIndex}: {block.Data.Length} bytes instead of {MapSign.MinimumStructureSize} or the sign marker is missing, skipped.");

            return;
        }

        _mapObjects.Add(sign!);
    }

    /// <summary>
    /// Creates the ground event an information block describes, the <c>MP2::OBJ_EVENT</c> branch of
    /// <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:634-640</c>).
    /// </summary>
    /// <remarks>
    /// The event goes into the object table and the tile keeps the event object: the tile passability code
    /// asks the table whether there is still an event to step on (<c>Maps::Tiles::updateObjectType()</c>).
    /// </remarks>
    /// <param name="block">The information block.</param>
    /// <param name="tileIndex">Index of the tile that carries the event.</param>
    private void CreateMapEvent(Mp2InfoBlock block, int tileIndex)
    {
        if (!MapEvent.TryRead(block.Data, tileIndex, out MapEvent? mapEvent))
        {
            GameLog.Warn($"Map event block {block.ObjectId} at tile {tileIndex}: {block.Data.Length} bytes instead of {MapEvent.MinimumStructureSize} or the event marker is missing, skipped.");

            return;
        }

        _mapObjects.Add(mapEvent!);
    }

    /// <summary>
    /// Creates the sphinx an information block describes, the <c>MP2::OBJ_SPHINX</c> branch of
    /// <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:642-650</c>).
    /// </summary>
    /// <remarks>
    /// The reference validates the sphinx after reading it and drops it when it has no reward or no riddle,
    /// with a warning (<c>world_loadmap.cpp:646</c>). The object computes the same flag itself, because a
    /// sphinx without a riddle is useless but still readable — the caller is what decides to drop it.
    /// </remarks>
    /// <param name="block">The information block.</param>
    /// <param name="tileIndex">Index of the tile that carries the sphinx.</param>
    private void CreateMapSphinx(Mp2InfoBlock block, int tileIndex)
    {
        if (!MapSphinx.TryRead(block.Data, tileIndex, out MapSphinx? sphinx))
        {
            GameLog.Warn($"Map sphinx block {block.ObjectId} at tile {tileIndex}: {block.Data.Length} bytes instead of {MapSphinx.MinimumStructureSize} or the sphinx marker is missing, skipped.");

            return;
        }

        if (!sphinx!.IsValid)
        {
            GameLog.Warn($"Map sphinx block {block.ObjectId} at tile {tileIndex}: no riddle, no answers or no reward, skipped.");

            return;
        }

        _mapObjects.Add(sphinx);
    }

    /// <summary>
    /// Picks the hero an information block asks for (<c>world_loadmap.cpp:567-573</c>, <c>:604-610</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The portrait byte of the block names the hero outright, as long as he is still available for hire:
    /// the reference accepts up to the last The Price of Loyalty hero, so the debug hero can never be
    /// placed by a map. Otherwise — no portrait, or the named hero already stands on the map — a hero is
    /// drawn from the recruitment pool of the race, which claims him for the map and keeps a tavern from
    /// offering him.
    /// </para>
    /// <para>
    /// A The Price of Loyalty portrait on a map of the original game names a slot the port does not create,
    /// which counts as "no such hero" and takes the same way out. The reference has an UNKNOWN placeholder
    /// in that slot, and <c>isValid()</c> makes it no hero for hire just the same.
    /// </para>
    /// </remarks>
    /// <param name="info">The hero payload of the block.</param>
    /// <param name="race">Race of the hero, from the tile sprite or from the jail byte.</param>
    /// <returns>The hero, or <see langword="null"/> when his race has nobody left to place.</returns>
    private Hero? SelectMapHero(Mp2HeroInfo info, MapRace race)
    {
        if (info.HasCustomPortrait && info.PortraitId + 1 <= Hero.Jarkonas)
        {
            Hero? named = _heroes.Get(info.PortraitId + 1);

            if (named is not null && named.IsAvailableForHire)
            {
                return named;
            }
        }

        return _heroes.GetHeroForHire(race);
    }

    /// <summary>
    /// Puts the hero of a tile on it, the <c>MP2::OBJ_HERO</c> branch of <c>World::updateTileMetadata()</c>
    /// (<c>world_loadmap.cpp:1583</c>).
    /// </summary>
    /// <remarks>
    /// The map file draws a standing hero as a <c>MINIHERO</c> object, the game draws the hero himself, so
    /// the editor sprite goes away before the tile is marked as taken. A tile whose hero was not created —
    /// the block is missing or broken — is left as it is, with a warning; the reference asserts instead.
    /// </remarks>
    /// <param name="tile">Tile to place the hero of.</param>
    private void PlaceMapHero(Tile tile)
    {
        if (tile.MainObjectType is not (Mp2ObjectType.Hero or Mp2ObjectType.ActionHero))
        {
            return;
        }

        Hero? hero = _heroes.GetAtMapIndex(tile.Index);

        if (hero is null)
        {
            GameLog.Warn($"Tile {tile.Index} is a hero object without a hero, left empty.");

            return;
        }

        if (tile.MainObjectPart.IcnType == Mp2ObjectIcnType.MiniHero)
        {
            tile.RemoveObjects(Mp2ObjectIcnType.MiniHero);
        }

        tile.SetHero(this, hero);
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
    /// the passability, the teleport, whirlpool and Eye of Magi caches, the pathfinder reset and the UID
    /// counter fixup.
    /// </summary>
    /// <remarks>
    /// The reference also resolves the whirlpool tiles that still carry a boat and computes the AI
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

        // Cache all tiles that contain stone liths of a certain type (depending on object sprite index).
        _allTeleports.Clear();

        foreach (int index in GetObjectPositions(Mp2ObjectType.ActionStoneLiths))
        {
            ObjectPart? part = _tiles[index].GetObjectPartByActionType(Mp2ObjectType.ActionStoneLiths);

            if (part is null)
            {
                // A broken map: the tile claims to be stone liths but has no such part. The reference
                // asserts here and carries on, and so does the port.
                continue;
            }

            AppendToCache(_allTeleports, part.Value.IcnIndex, index);
        }

        // Cache all tiles that contain a certain part of the whirlpool (depending on object sprite index).
        // Whirlpools are unique objects because they can have boats on them which are leftovers from
        // heroes which disembarked on land: such tiles are marked as boats, so searching by type would
        // miss them and the parts are searched instead.
        _allWhirlpools.Clear();

        foreach ((int index, ObjectPart part) in GetObjectParts(Mp2ObjectType.ActionWhirlpool))
        {
            AppendToCache(_allWhirlpools, part.IcnIndex, index);
        }

        // Cache all positions of Eye of Magi objects.
        _allEyeOfMagi.Clear();
        _allEyeOfMagi.AddRange(GetObjectPositions(Mp2ObjectType.ActionEyeOfMagi));

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
    /// Appends <paramref name="index"/> to the list of the sprite index, creating the list on the way,
    /// the <c>_allTeleports[objectPart->icnIndex].push_back( index )</c> of the reference.
    /// </summary>
    /// <param name="cache">The cache to append to.</param>
    /// <param name="spriteIndex">Sprite index of the part, the key of the cache.</param>
    /// <param name="index">Tile index to append.</param>
    private static void AppendToCache(Dictionary<byte, List<int>> cache, byte spriteIndex, int index)
    {
        if (!cache.TryGetValue(spriteIndex, out List<int>? positions))
        {
            positions = [];
            cache.Add(spriteIndex, positions);
        }

        positions.Add(index);
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
