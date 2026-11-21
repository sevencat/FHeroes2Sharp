using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
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

    /// <summary>The AI's pathfinder for this world, the planner's <c>_pathfinder</c>.</summary>
    /// <remarks>
    /// Created on first use as well, and dropped by <see cref="Reset"/> for the same reason: it caches one
    /// node per tile. The reference reaches it through the global <c>AIWorldPathfinder::Get()</c>; the port
    /// keeps the instance on the world it was built for and hands it to <see cref="AiPlanner"/> when it is
    /// created.
    /// </remarks>
    private AIWorldPathfinder? _aiPathfinder;

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

    /// <summary>
    /// The seed of the map, <c>_seed</c> (<c>world.h:575</c>): drawn once at the start and persisted in saves.
    /// </summary>
    /// <remarks>
    /// The reference draws it in <c>Defaults()</c> (<c>world.cpp:291</c>). The port has no session yet, so it
    /// stays at zero until a loader or a save writes it; the week draw is deterministic from whatever it holds.
    /// </remarks>
    private uint _seed;

    /// <summary>Tiles of the stone liths, grouped by sprite index, <c>_allTeleports</c>.</summary>
    private readonly Dictionary<byte, List<int>> _allTeleports = [];

    /// <summary>Tiles of the whirlpool parts, grouped by sprite index, <c>_allWhirlpools</c>.</summary>
    private readonly Dictionary<byte, List<int>> _allWhirlpools = [];

    /// <summary>Tiles of the Eye of Magi objects, <c>_allEyeOfMagi</c>.</summary>
    private readonly List<int> _allEyeOfMagi = [];

    /// <summary>
    /// The fog of war, made on first use and dropped by <see cref="Reset"/>; <c>Maps::Tile::_fogColors</c>
    /// gathered into one table.
    /// </summary>
    private MapFog? _fog;

    /// <summary>The buried ultimate artifact, <c>_ultimateArtifact</c> (<c>world.cpp:326</c>).</summary>
    private readonly UltimateArtifact _ultimateArtifact = new();

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
    /// The hero a jail on <paramref name="tileIndex"/> holds, <c>World::FromJailHeroes()</c>
    /// (<c>world.h:340</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>world.h:340</c>，一行转给 <c>vec_heroes.FromJail</c>。地图加载时被关的英雄由
    /// <see cref="Hero.LoadFromMP2"/> 设上 <see cref="HeroModes.Jail"/> 位并站到监狱瓦片上，这里按位置找人。
    /// </remarks>
    /// <param name="tileIndex">Tile index of the jail.</param>
    /// <returns>The prisoner, or <see langword="null"/> when the jail is empty.</returns>
    public Hero? FromJailHeroes(int tileIndex) => _heroes.FromJail(tileIndex);

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
    /// The fog of war of the map.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考把迷雾<b>存在每一块瓦片的元数据里</b>（<c>Maps::Tile::_fogColors</c>，一个颜色位掩码），
    /// <c>Maps::ClearFog</c> 直接改瓦片。端口把这层状态收在 <see cref="MapFog"/> 里：
    /// 「每块瓦片对哪些颜色还没被发现」加一份由此算出的方向掩码，供渲染器使用。
    /// </para>
    /// <para>
    /// <b>按需创建</b>：没有地图时返回 <see langword="null"/>，有尺寸时按 <see cref="Width"/>×
    /// <see cref="Height"/> 建一张全黑的表。这样两个加载器（<see cref="GenerateUninitializedMap"/> 与
    /// <see cref="LoadMap"/>）都不必记得同时建迷雾，<see cref="Reset"/> 只管把它丢掉。
    /// </para>
    /// <para>
    /// <b>开局侦察要自己调</b>：地图一加载就是全黑的，参考在 <c>game_startgame.cpp:769</c> 里紧接着调
    /// <c>World::ClearFog( colors )</c>，端口的同名重载（<see cref="ClearFog(PlayerColor)"/>）就是这一步。
    /// 之后只有真正调用 <see cref="ClearFog(int, int, PlayerColor)"/> 的对象（观察塔）、
    /// <see cref="ActionForMagellanMaps"/> 与走动的英雄（<c>FogHeroMovementHandler</c>）才会揭开。
    /// </para>
    /// </remarks>
    public MapFog? Fog
    {
        get
        {
            if (Width <= 0 || Height <= 0)
            {
                return null;
            }

            return _fog ??= new MapFog(Width, Height);
        }
    }

    /// <summary>
    /// Reveals the disc around a tile, <c>Maps::ClearFog()</c> (<c>maps.cpp:464</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>maps.cpp:464-500</c>。半径 ≤ 0 或瓦片号越界时<b>什么也不做</b>（参考直接 <c>return</c>），
    /// 端口返回 0。半径到方形距离的换算是 <see cref="MapFog.SquaredRevealLimit"/>，不是简单的半径平方——
    /// 见那里的说明。
    /// </para>
    /// <para>
    /// 参考顺手用 <c>updateDirectionsInArea</c> 只重算被揭开的矩形；端口按整张图重算
    /// （<see cref="MapFog.UpdateDirections"/>），代价可以忽略，换来不必追踪脏矩形。
    /// </para>
    /// <para>
    /// <b>只揭自己的颜色</b>：参考揭 <c>Players::GetPlayerFriends( color )</c>（盟友一起看见），端口没有联盟，
    /// <see cref="Hero.IsFriends"/> 是那个缺口唯一的一处，等联盟落地再改这里。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Center tile.</param>
    /// <param name="distance">Radius in tiles; values at or below zero reveal nothing.</param>
    /// <param name="color">The colors that lose the fog.</param>
    /// <returns>Number of tiles that were uncovered; <c>0</c> when nothing changed.</returns>
    public int ClearFog(int tileIndex, int distance, PlayerColor color)
    {
        if (distance <= 0 || color == PlayerColor.None || tileIndex < 0 || tileIndex >= _tiles.Count)
        {
            return 0;
        }

        MapFog? fog = Fog;

        if (fog is null)
        {
            return 0;
        }

        int revealed = fog.Reveal(tileIndex % Width, tileIndex / Width, distance, color);
        fog.UpdateDirections(color);

        return revealed;
    }

    /// <summary>
    /// Reveals every water tile of the map, <c>World::ActionForMagellanMaps()</c> (<c>world.cpp:906</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>world.cpp:906-922</c>。逐块看瓦片，<b>只要是水</b>就揭掉——地图上的海一次看全，
    /// 陆地上的一概不动。这是麦哲伦地图（<c>MagellansMaps</c>）买到的东西。
    /// </para>
    /// <para>
    /// 参考对 AI 王国还会额外通知 <c>AI::Planner::revealFog</c>；端口没有 AI 规划器，那一行留给将来。
    /// </para>
    /// </remarks>
    /// <param name="color">The colors that lose the fog.</param>
    /// <returns>Number of water tiles that were uncovered; <c>0</c> when nothing changed.</returns>
    public int ActionForMagellanMaps(PlayerColor color)
    {
        if (color == PlayerColor.None)
        {
            return 0;
        }

        MapFog? fog = Fog;

        if (fog is null)
        {
            return 0;
        }

        int revealed = 0;

        for (int index = 0; index < _tiles.Count; ++index)
        {
            if (_tiles[index].IsWater)
            {
                // A radius of zero reveals the tile it is centered on and nothing else.
                revealed += fog.Reveal(index % Width, index / Width, 0, color);
            }
        }

        if (revealed > 0)
        {
            fog.UpdateDirections(color);
        }

        return revealed;
    }

    /// <summary>
    /// Uncovers the fog around everything a player owns, <c>World::ClearFog( PlayerColor )</c>
    /// (<c>world.cpp:838-848</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>world.cpp:838-848</c>。三件事，顺序固定：<c>vec_castles.Scout( colors )</c>、
    /// <c>vec_heroes.Scout( colors )</c>、<c>map_captureobj.ClearFog( colors )</c>。前两件现在都在
    /// （<see cref="Castle.AllCastles.Scout"/> 与 <see cref="Heroes.AllHeroes.Scout"/>），第三件还没有：
    /// 「被占领的矿井/资源点对盟友可见」要先把占领记录做成带颜色的表，端口的
    /// <c>CapturedObject</c> 只记颜色与对象类型，没有迷雾位。
    /// </para>
    /// <para>
    /// <b>调用时机</b>：参考在 <c>game_startgame.cpp:769</c> 里，开局读到盘面之后调一次，此后城堡被占领、
    /// 英雄被招募（<c>Heroes::Scout</c> 也在招募与挖宝之后被单独调用）时再补。端口没有游戏循环，这个方法就是
    /// 留给它的入口。
    /// </para>
    /// <para>
    /// 与 <see cref="ClearFog(int, int, PlayerColor)"/> 同名的重载：那个揭<b>一个圆</b>，这个揭<b>一方的全部
    /// 家当</b>。参考里它们甚至不在一个文件（<c>maps.cpp</c> / <c>world.cpp</c>），端口放在一起是因为两者都只是
    /// 在改同一张迷雾表。
    /// </para>
    /// </remarks>
    /// <param name="color">The color whose towns and heroes scout; <see cref="PlayerColor.None"/> does
    /// nothing.</param>
    /// <returns>Number of tiles uncovered.</returns>
    public int ClearFog(PlayerColor color)
    {
        if (color == PlayerColor.None)
        {
            return 0;
        }

        int revealed = _castles.Scout(this, color);

        revealed += _heroes.Scout(this, color);

        // Every reveal already recomputes the masks of its colors, but a call that reveals nothing — a map
        // whose human color has no hero and no town — still has to leave the table ready to be drawn: the
        // renderer walks the direction masks, not the fog bytes, and a fresh table has none yet.
        Fog?.UpdateDirections(color);

        return revealed;
    }

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
    /// The timed events that fire for <paramref name="color"/> today, <c>World::GetEventsDate()</c>
    /// (<c>world.cpp:875</c>).
    /// </summary>
    /// <remarks>
    /// A plain filter over <see cref="DailyEvents"/> by <see cref="EventDate.IsAllowed"/>: the color has to be
    /// one the event names, and today has to be the first occurrence or a repetition of it. The reference
    /// returns a vector by value; the port returns a list of the same events (they are immutable).
    /// </remarks>
    /// <param name="color">Color of the player asking.</param>
    /// <returns>The events that fire today, in map order.</returns>
    public List<EventDate> GetEventsDate(PlayerColor color)
    {
        var result = new List<EventDate>();

        foreach (EventDate eventDate in _dailyEvents)
        {
            if (eventDate.IsAllowed(color, _day))
            {
                result.Add(eventDate);
            }
        }

        return result;
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
    /// The seed of the map, <c>World::GetMapSeed()</c> (<c>world.h:504</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference draws it once in <c>Defaults()</c> (<c>world.cpp:291</c>, before the heroes are built,
    /// because a campaign hero levels up while being created), writes it into saves and zeroes it in
    /// <c>Reset()</c>.
    /// </para>
    /// <para>
    /// The port has neither <c>Defaults()</c> nor a session, so the value is written from the outside: the
    /// setter is <c>internal</c> for a loader, a future save game and the tests. Everything that draws from
    /// the map seed (the week, the month of monsters) is a pure function of it, so a world with the default
    /// seed still behaves consistently — it just plays the same sequence of weeks on every install.
    /// </para>
    /// </remarks>
    public uint MapSeed
    {
        get => _seed;
        internal set => _seed = value;
    }

    /// <summary>
    /// The seed of the current week, <c>World::GetWeekSeed()</c> (<c>world.cpp:1263</c>).
    /// </summary>
    /// <remarks>
    /// The map seed salted with the week counter, so every week of a map draws from a different seed while the
    /// same week of the same map always draws the same. The salt comes from <c>Rand</c>
    /// (<see cref="Rand.CombineSeedWithValueHash"/>), which is why <c>week.cpp</c> needs it.
    /// </remarks>
    public uint WeekSeed => Rand.CombineSeedWithValueHash(_seed, _week);

    /// <summary>
    /// What the current week is, <c>World::GetWeekType()</c> (<c>world.cpp:410</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The draw is stateless: <see cref="Week.RandomWeek"/> is fed the map seed salted with the week counter,
    /// plus whether this is the first week of a month (which switches from the week table to the month table).
    /// </para>
    /// <para>
    /// The reference caches the answer in a function-local static keyed by <c>( _week, GetWeekSeed() )</c>
    /// because callers ask once per tile while redrawing; the port recomputes it, since the draw is two hashes
    /// and at most two generator calls, and a cache that outlives <see cref="Reset"/> is a landmine.
    /// </para>
    /// <para>
    /// Fully qualified on purpose: <see cref="Week"/> below is the <c>uint</c> display property, which shadows
    /// the type name inside this class (ADR-113).
    /// </para>
    /// </remarks>
    public FHeroes2.Game.Kingdom.Week WeekType => FHeroes2.Game.Kingdom.Week.RandomWeek(IsFirstWeek, WeekSeed);

    /// <summary>
    /// Advances the clock by one day, <c>World::NewDay()</c> (<c>world.cpp:425</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is here</b>, in the reference's order (<c>world.cpp:427-459</c>): the three counters with their
    /// exact nesting, then the week reaching the towns (<see cref="CastleTown.ActionNewWeek"/>) before the day
    /// does (<see cref="CastleTown.ActionNewDay"/>), then the removal of the daily events that can no longer fire.
    /// </para>
    /// <para>
    /// <b>What is not here</b>: <c>NewMonth()</c> (the month of monsters), <c>NewWeek()</c> (the week-life
    /// objects, the neutral monster refresh) and <c>vec_heroes</c>' own tick. Each of them needs a system the
    /// port does not have (the monster spawner, the hero pool), so each is added here — in this order — together
    /// with that system. The order matters and is the part that is expensive to change: the month turns first,
    /// then the week, then the day. The kingdoms' own ticks <b>are</b> here (<see cref="Kingdom.Kingdoms.NewWeek"/>
    /// and <see cref="Kingdom.Kingdoms.NewDay"/>, ADR-120).
    /// </para>
    /// <para>
    /// The week type is <b>not</b> on this list: <see cref="WeekType"/> is derived from the counters
    /// (<c>world.cpp:410</c>), so there is nothing to tick — the week changes the moment <c>_week</c> does.
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

            // world.cpp:443-455: the month turns first, then the week, then the week reaches the systems before
            // their day does.
            if (IsBeginMonth)
            {
                NewMonth();
            }

            NewWeek();

            _kingdoms.NewWeek(this, _week);
            _castles.NewWeek(_week, WeekType, IsBeginMonth, _day);
        }

        _kingdoms.NewDay(_day);
        _castles.NewDay();

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
    /// Digs a hole on a tile and reports whether the ultimate artifact was under it,
    /// <c>World::DiggingForUltimateArtifact()</c> (<c>world.cpp:850</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hole is left behind either way — a hero spends the day digging whether or not there is
    /// anything under the tile. The hole is a part of the terrain layer with a fresh UID, so the tile knows
    /// it as an object and the renderer draws it.
    /// </para>
    /// <para>
    /// <b>The artifact is not removed from the tile.</b> The reference only marks it as found: the treasure
    /// is not a map object to begin with, it is a position the world remembers, and once found every later
    /// dig on that tile reports nothing. Water is the one ground that cannot be dug; the reference asserts
    /// there, the port answers <see langword="false"/> without adding a part (see
    /// <see cref="DiggingHole.TryGetSprite"/>).
    /// </para>
    /// </remarks>
    /// <param name="center">Tile the hero digs on.</param>
    /// <returns>Whether the ultimate artifact was found by this dig.</returns>
    public bool DiggingForUltimateArtifact(Point center)
    {
        Tile tile = GetTile(center.X, center.Y);

        if (!DiggingHole.TryGetSprite(tile.Ground, out Mp2ObjectIcnType icnType, out byte imageIndex))
        {
            return false;
        }

        tile.PushGroundObjectPart(
            new ObjectPart(ObjectLayerType.Terrain, GetNewObjectUid(), icnType, imageIndex));

        if (_ultimateArtifact.IsAt(tile.Index) && !_ultimateArtifact.IsFound)
        {
            _ultimateArtifact.MarkAsFound();

            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether any kingdom on the map has visited an object, <c>World::isAnyKingdomVisited()</c>
    /// (<c>world.cpp:1272</c>).
    /// </summary>
    /// <remarks>
    /// The reference keeps the visit log per kingdom and asks every one of them; the port keeps the same
    /// log in <see cref="Kingdom"/> and the loop lives in <see cref="Kingdoms.IsAnyVisited"/>.
    /// </remarks>
    /// <param name="objectType">Object type, action bit included.</param>
    /// <param name="tileIndex">Tile index of the object.</param>
    /// <returns>Whether at least one kingdom has visited it.</returns>
    public bool IsAnyKingdomVisited(Mp2ObjectType objectType, int tileIndex) =>
        _kingdoms.IsAnyVisited(tileIndex, objectType);

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
    /// <b>The flag follows the color</b> (<see cref="Tile.SetOwnershipFlag"/>): the tile of the object —
    /// or one of its neighbours, for the objects whose sprite covers two tiles — carries a FLAG32 part so
    /// the player can see who owns it. A castle entrance also repaints the town itself, and both are
    /// skipped for a color that is not one of the six players: the reference stores such a value in the
    /// table and then stops.
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index.</param>
    /// <param name="color">New owner.</param>
    public void CaptureObject(int index, PlayerColor color)
    {
        Debug.Assert(PlayerColors.Count(color) <= 1, "A captured object belongs to exactly one player.");

        // The owner can be set not only for the objects of `Mp2ObjectTypes.IsCaptureObject()`:
        // dwellings are marked with the player's color as well.
        Mp2ObjectType objectType = GetTile(index).GetMainObjectType(this, false);

        _capturedObjects.Set(index, objectType, color);

        if (color != PlayerColor.None && (PlayerColors.All & color) == 0)
        {
            // A color that is not playable is recorded but paints nothing.
            return;
        }

        // `Castle` is both the type and its namespace here, so the type is left to inference.
        var castle = GetCastleEntrance(index);

        if (castle is not null && castle.Color != color)
        {
            castle.ChangeColor(color);
        }

        GetTile(index).SetOwnershipFlag(this, objectType, color);
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
    /// <remarks>
    /// The entries lose their owner in <see cref="CapturedObjects.ResetColor"/> and the tiles lose their
    /// flag here, which is where the reference does both in one pass over its own table.
    /// </remarks>
    /// <param name="color">The player that loses the objects.</param>
    public void ResetCapturedObjects(PlayerColor color)
    {
        foreach (int index in _capturedObjects.ResetColor(color))
        {
            GetTile(index).SetOwnershipFlag(this, _capturedObjects.Get(index).ObjectType, PlayerColor.None);
        }
    }

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
    /// Makes a two by two world of one ground for a battle only game, <c>World::generateBattleOnlyMap()</c>
    /// (<c>world.cpp:338</c>).
    /// </summary>
    /// <remarks>
    /// The reference builds the uninitialized map, numbers the tiles and paints them with the start image of
    /// the ground. The numbering loop is the reference's own, because <see cref="GenerateUninitializedMap"/>
    /// deliberately leaves every index at zero.
    /// </remarks>
    /// <param name="ground">Ground every tile gets.</param>
    public void GenerateBattleOnlyMap(MapGround ground)
    {
        GenerateUninitializedMap(2);

        for (int i = 0; i < _tiles.Count; ++i)
        {
            _tiles[i].Index = i;
        }

        SetUniformTerrain(ground);
    }

    /// <summary>
    /// Paints every tile of the world with the first image of one ground, <c>World::setUniformTerrain()</c>
    /// (<c>world.cpp:349</c>).
    /// </summary>
    /// <remarks>
    /// The reference takes the raw ground id and asks <c>Maps::Ground::getTerrainStartImageIndex()</c> for the
    /// image; the port takes the ground itself and asks <see cref="MapGrounds.StartImageIndex"/> for the same
    /// number, which is the table that function reads. The shape bits of a tile are cleared, exactly like the
    /// reference's <c>setTerrain( index, 0 )</c>.
    /// </remarks>
    /// <param name="ground">Ground to paint.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ground has no terrain images.</exception>
    public void SetUniformTerrain(MapGround ground)
    {
        ushort terrainImageIndex = MapGrounds.StartImageIndex(ground);

        foreach (Tile tile in _tiles)
        {
            tile.SetTerrain(terrainImageIndex, 0);
        }
    }

    /// <summary>
    /// Empties the world, <c>Reset()</c> (<c>world.cpp:299</c>): dimensions back to zero, the tile vector,
    /// the hero list, the map objects, the daily events, the rumors, the captured objects, the kingdoms, the
    /// castles, the date counters, the seed and the win/loss conditions cleared.
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

        // The fog is built for one map's size, exactly like the teleport table below.
        _fog = null;

        // The kingdoms hold no map-dependent state, so clearing them is enough (world.cpp:308).
        _kingdoms.Clear();

        // The table is replaced rather than cleared: it is built for one map's width, and the next map may have
        // another one (see World.Castles.cs).
        _castles = new AllCastles(0);

        // The clock is part of the world, not of a session (world.cpp:328-330): a new map starts at
        // day 0 / week 0 / month 0 and only World::NewDay() ever moves it.
        _day = 0;
        _week = 0;
        _month = 0;

        // The seed goes with the counters: Defaults() draws a new one for every map (world.cpp:291, :335).
        _seed = 0;

        // The conditions belong to one map: the header they are read from and the two heroes they name
        // (world.cpp:308-330 resets the condition state along with the rest of the world).
        MapInfo = null;
        _heroIdAsWinCondition = Hero.UnknownId;
        _heroIdAsLossCondition = Hero.UnknownId;

        // The buried artifact is per map too: nothing is buried before a map is loaded (world.cpp:311).
        _ultimateArtifact.Reset();

        // The port clears the three post load caches here as well. The reference leaves them to
        // PostLoad(), but they hold tile indexes, and a cache that outlives its tiles is a landmine.
        _allTeleports.Clear();
        _allWhirlpools.Clear();
        _allEyeOfMagi.Clear();

        // Same reasoning: both pathfinders cache one node per tile and the map dimensions, so they must not
        // outlive the map they were built for.
        _pathfinder = null;
        _aiPathfinder = null;

        // And the regions, which are tile ids in a tile table that is about to be emptied.
        ClearRegions();
    }

    /// <summary>
    /// The pathfinder of this world's heroes, <c>_pathfinder</c>. Created on first use.
    /// </summary>
    /// <exception cref="ArgumentException">The world holds no map; a pathfinder is one node per tile and
    /// cannot exist before the tiles do.</exception>
    public PlayerWorldPathfinder Pathfinder =>
        _pathfinder ??= new PlayerWorldPathfinder(this);

    /// <summary>
    /// The AI's pathfinder for this world, <c>AIWorldPathfinder</c>. Created on first use.
    /// </summary>
    /// <remarks>
    /// The reference has one global AI pathfinder; the port builds it against a world, so the first use
    /// also hands it to <see cref="AiPlanner"/>, which is where the AI code looks for it. Keeping the
    /// instance on the world means <see cref="ResetPathfinder"/> resets the copy that belongs to this map
    /// rather than whichever world was attached last.
    /// </remarks>
    /// <exception cref="ArgumentException">The world holds no map; a pathfinder is one node per tile and
    /// cannot exist before the tiles do.</exception>
    public AIWorldPathfinder AiPathfinder
    {
        get
        {
            if (_aiPathfinder is null)
            {
                _aiPathfinder = new AIWorldPathfinder(this);
                AiPlanner.Instance.AttachPathfinder(_aiPathfinder);
            }

            return _aiPathfinder;
        }
    }

    /// <summary>
    /// Invalidates the pathfinder caches, <c>resetPathfinder()</c> (<c>world.cpp:1171</c>): the
    /// reference resets <c>_pathfinder</c> and the AI planner's copy.
    /// </summary>
    /// <remarks>
    /// The reference calls this whenever a tile changes in a way the search would have seen — a hero
    /// moving away, a monster killed, a road built. Both copies belong to this world, so both are reset
    /// here; the planner's copy is the same object the <see cref="AiPathfinder"/> getter attaches.
    /// </remarks>
    public void ResetPathfinder()
    {
        _pathfinder?.Reset();
        _aiPathfinder?.Reset();
    }

    /// <summary>
    /// The movement cost from where <paramref name="hero"/> stands to a tile, <c>World::getDistance()</c>
    /// (<c>world.cpp:1159</c>).
    /// </summary>
    /// <remarks>
    /// 参考把整位英雄交给寻路器（<c>reEvaluateIfNeeded( const Heroes &amp; )</c>），寻路器再去问那五个值。端口的
    /// 寻路器收的是拆开的五个值（它出现时 <c>Hero</c> 还没有移动力），所以这里负责把它们从英雄身上读出来——两行
    /// 代码，和参考一一对应。
    /// </remarks>
    /// <param name="hero">The hero to measure from; must stand on the map.</param>
    /// <param name="targetIndex">Index of the tile to measure to.</param>
    /// <returns>The movement points a trip costs; zero when the tile cannot be reached.</returns>
    /// <exception cref="ArgumentException"><paramref name="hero"/> stands nowhere.</exception>
    public uint GetDistance(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ReEvaluatePathfinder(hero);

        return Pathfinder.GetDistance(targetIndex);
    }

    /// <summary>
    /// The steps from where <paramref name="hero"/> stands to a tile, <c>World::getPath()</c>
    /// (<c>world.cpp:1165</c>).
    /// </summary>
    /// <remarks>
    /// 和 <see cref="GetDistance"/> 同一趟搜索：<c>buildPath()</c> 读的就是刚才那次搜索留下的父节点，所以先问路再问
    /// 距离不重复算。
    /// </remarks>
    /// <param name="hero">The hero to plan for; must stand on the map.</param>
    /// <param name="targetIndex">Index of the tile to plan to.</param>
    /// <returns>The steps, starting with the one that leaves the hero's tile.</returns>
    /// <exception cref="ArgumentException"><paramref name="hero"/> stands nowhere.</exception>
    public List<RouteStep> GetPath(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ReEvaluatePathfinder(hero);

        return Pathfinder.BuildPath(targetIndex);
    }

    /// <summary>
    /// Hands the hero's five settings to the pathfinder, <c>_pathfinder.reEvaluateIfNeeded( hero )</c>.
    /// </summary>
    /// <param name="hero">The hero whose movement the search depends on.</param>
    /// <exception cref="ArgumentException"><paramref name="hero"/> stands nowhere.</exception>
    private void ReEvaluatePathfinder(Hero hero)
    {
        if ((uint)hero.MapIndex >= (uint)Size)
        {
            throw new ArgumentException("The hero stands outside the map.", nameof(hero));
        }

        Pathfinder.ReEvaluateIfNeeded(
            hero.MapIndex,
            hero.Color,
            hero.MovePoints,
            hero.GetLevelSkill(SecondarySkill.Pathfinding),
            hero.GetMaxMovePoints());
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
    /// <b>What is not here</b>: everything the port has no system for yet. The obelisk counter and the
    /// Price of Loyalty metadata are not read; the AI
    /// regions of <c>ComputeStaticAnalysis</c> are not computed. None of them affects which tiles exist or whether
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

        // The header the conditions are read from (world_loadmap.cpp:1760 asks Settings for it; the port keeps
        // the header it was handed). See World.Conditions.cs.
        MapInfo = map.Info;

        Width = map.Width;
        Height = map.Height;

        // The castle table is built for this map's width, so it is replaced rather than cleared
        // (world_loadmap.cpp:296-355). An empty world has an empty table of width zero.
        _castles = new AllCastles(Width);

        // World::Defaults() -> vec_kingdoms.Init() (world.cpp:287): the kingdoms come first, because a kingdom's
        // starting resources depend on the difficulty and on whether the computer plays it (see World.Kingdoms.cs).
        CreateMapKingdoms(map.Info);

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

        // The town table of the file: two bytes of position and one of race per town, up to seventy-two of them
        // (world_loadmap.cpp:296-355). It has to come before the information blocks, because a block finds its town
        // by the tile it stands on, and after the tiles, because a town's sea access is read off the map.
        CreateMapCastles(map);

        // The capturable objects of the file: the sawmill, the alchemist lab, the mines, the lighthouse, the dragon
        // city and the abandoned mine, all with a neutral owner (world_loadmap.cpp:382-430). The towns were already
        // registered above; a block may give the owner of any of them later.
        ReadCaptureObjects(map);

        // The information blocks of the map: a hero block describes the army, the artifacts, the experience,
        // the skills and the name of a hero whose position the tile carries, a jail block the hero waiting
        // inside the prison, a sign block a message, an event block resources, an artifact and a message, and a
        // castle block the town standing on the tile (world_loadmap.cpp:466-688).
        ReadInfoBlocks(map);

        // The towns the map left unnamed get one of the default names (world_loadmap.cpp:692-699).
        UpdateCastleNames();

        foreach (Tile tile in _tiles)
        {
            tile.FixMP2MapTileObjectType(this);

            // The reference processes the objects of a new map tile by tile: the type is fixed first, then
            // the object data of the tile is applied (world_loadmap.cpp:1405), and for a hero that means
            // dropping the editor sprite and putting the hero on the tile (world_loadmap.cpp:1583).
            PlaceMapHero(tile);
        }

        // The rosters of the kingdoms, now that every town and hero carries the color the map gave it
        // (world_loadmap.cpp:1443-1445).
        AttachMapRosters();

        // The two heroes the win/loss conditions name, found by the tile they stand on
        // (world_loadmap.cpp:1447-1449). See World.Conditions.cs.
        SetHeroIdsForMapConditions();

        // The buried artifact, after the tiles are final and before the caches are built
        // (world_loadmap.cpp:1451-1452). See World.UltimateArtifact.cs.
        PlaceUltimateArtifact();

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
    /// A town's block takes a wider path (<c>world_loadmap.cpp:596-641</c>): a built castle is found by its entrance
    /// tile, a random one by any tile of its footprint, and after the block the random town is repainted. See
    /// <see cref="ReadCastleBlock"/>.
    /// </para>
    /// <para>
    /// A block that no object points at is a daily event or a rumor (<c>world_loadmap.cpp:656-684</c>);
    /// see <see cref="ReadWorldBlock"/>.
    /// </para>
    /// <para>
    /// <b>What is missing</b>: <c>Kingdom::AllowRecruitHero()</c> is the limit of heroes a kingdom may own, and the
    /// port has no kingdom, so a map with more heroes for one color than the limit is loaded as it is instead of
    /// dropping the surplus. A random race on a neutral hero is not resolved to the race of the kingdom either, for
    /// the same reason.
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

                case Mp2ObjectType.Castle:
                case Mp2ObjectType.ActionCastle:
                    // A town that stands on its entrance: the block fills the town the table already holds.
                    ReadCastleBlock(map, block, tileIndex, false);
                    break;

                case Mp2ObjectType.RandomCastle:
                case Mp2ObjectType.ActionRandomCastle:
                case Mp2ObjectType.RandomTown:
                case Mp2ObjectType.ActionRandomTown:
                    // A random town: the tile says "random" until the block says which town it is.
                    ReadCastleBlock(map, block, tileIndex, true);
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
    /// A castle block whose town is not on the map lands here too, and is then read as a daily event or a rumor if
    /// its bytes happen to fit — exactly what the reference does with a block it could not match to a tile.
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
    /// id of the block. The port walks the tiles too, and accepts every object the file gives a block to, so
    /// that a castle block is not mistaken for a daily event.
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
    /// the passability, the teleport, whirlpool and Eye of Magi caches, the pathfinder reset, the static
    /// analysis of the map and the UID counter fixup.
    /// </summary>
    /// <remarks>
    /// The reference also resolves the whirlpool tiles that still carry a boat; that belongs to the
    /// rendering of a boat whose hero disembarked and will land with it.
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

        // world.cpp:1233 asks for the static analysis at the very end, after every object and every passability is
        // final: it reads the tiles, the castles and the teleport caches.
        ComputeStaticAnalysis();

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
