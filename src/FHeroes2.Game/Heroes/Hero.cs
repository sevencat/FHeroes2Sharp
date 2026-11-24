using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Random;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// A hero of the world map. The first slice of <c>Heroes</c>
/// (<c>参考：src/fheroes2/heroes/heroes.h:63</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the identity plus the two fields a tile and a tavern need, not the hero.</b> The army, the
/// secondary skills, the four primary skills and the artifacts are in their own partial files, and the spell
/// book with the spell points in <c>Hero.Spells.cs</c>; the route, the experience and the movement points still
/// arrive with the systems that consume them. What is here is the contract that
/// <see cref="Maps.Tile.SetHero"/> / <see cref="Maps.Tile.GetHero"/> close over
/// (<c>maps_tiles.cpp:429-460</c>) — the numeric id and the object type the hero is standing on — plus the
/// race, the color and the portrait <see cref="AllHeroes.GetHeroForHire"/> needs to fill a tavern.
/// </para>
/// <para>
/// The id space is the reference's <c>Heroes</c> enumeration (<c>heroes.h:87-186</c>):
/// <c>UNKNOWN</c> is 0, then nine heroes of each of the six races of The Succession Wars (1-54), the six
/// campaign heroes (55-60), the eleven The Price of Loyalty heroes (61-71) and <c>DEBUG_HERO</c> (72),
/// so <c>HEROES_COUNT</c> is 73. <see cref="IsValidId"/> is the reference's three-line function: above
/// <see cref="UnknownId"/> and below <see cref="IdCount"/> — the debug hero is therefore "valid", which
/// matches the reference and is what <c>getMaximumAllowedHeroes()</c> subtracts separately.
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>Id of an absent hero, <c>Heroes::UNKNOWN</c> (<c>heroes.h:90</c>).</summary>
    public const int UnknownId = 0;

    /// <summary>First hero of the knight race, <c>Heroes::LORDKILBURN</c> (<c>heroes.h:92</c>).</summary>
    public const int LordKilburn = 1;

    /// <summary>Last of the six original races, <c>Heroes::CELIA</c> (<c>heroes.h:137</c>).</summary>
    public const int Celia = 54;

    /// <summary>First of the six campaign heroes, <c>Heroes::ROLAND</c> (<c>heroes.h:139</c>).</summary>
    public const int Roland = 55;

    /// <summary>Second campaign hero, <c>Heroes::CORLAGON</c>.</summary>
    public const int Corlagon = 56;

    /// <summary>Third campaign hero, <c>Heroes::ELIZA</c>.</summary>
    public const int Eliza = 57;

    /// <summary>Fourth campaign hero, <c>Heroes::ARCHIBALD</c>.</summary>
    public const int Archibald = 58;

    /// <summary>Fifth campaign hero, <c>Heroes::HALTON</c>.</summary>
    public const int Halton = 59;

    /// <summary>Sixth campaign hero, <c>Heroes::BRAX</c> (<c>heroes.h:144</c>).</summary>
    public const int Brax = 60;

    /// <summary>First The Price of Loyalty hero, <c>Heroes::SOLMYR</c> (<c>heroes.h:146</c>).</summary>
    public const int Solmyr = 61;

    /// <summary>The Price of Loyalty hero, <c>Heroes::DAINWIN</c>.</summary>
    public const int Dainwin = 62;

    /// <summary>The Price of Loyalty hero, <c>Heroes::MOG</c>.</summary>
    public const int Mog = 63;

    /// <summary>The Price of Loyalty hero, <c>Heroes::UNCLEIVAN</c>.</summary>
    public const int UncleIvan = 64;

    /// <summary>The Price of Loyalty hero, <c>Heroes::JOSEPH</c>.</summary>
    public const int Joseph = 65;

    /// <summary>The Price of Loyalty hero, <c>Heroes::GALLAVANT</c>.</summary>
    public const int Gallavant = 66;

    /// <summary>The Price of Loyalty hero, <c>Heroes::ELDERIAN</c>.</summary>
    public const int Elderian = 67;

    /// <summary>The Price of Loyalty hero, <c>Heroes::CEALLACH</c>.</summary>
    public const int Ceallach = 68;

    /// <summary>The Price of Loyalty hero, <c>Heroes::DRAKONIA</c>.</summary>
    public const int Drakonia = 69;

    /// <summary>The Price of Loyalty hero, <c>Heroes::MARTINE</c>.</summary>
    public const int Martine = 70;

    /// <summary>Last The Price of Loyalty hero, <c>Heroes::JARKONAS</c> (<c>heroes.h:156</c>).</summary>
    public const int Jarkonas = 71;

    /// <summary>The developer hero, <c>Heroes::DEBUG_HERO</c> (<c>heroes.h:184</c>).</summary>
    public const int DebugHero = 72;

    /// <summary>Number of hero ids, <c>Heroes::HEROES_COUNT</c> (<c>heroes.h:185</c>).</summary>
    public const int IdCount = 73;

    /// <summary>
    /// Position of a hero that stands on no tile, the <c>center( -1, -1 )</c> a <c>Position</c> starts with.
    /// </summary>
    public const int NoPosition = -1;

    private Mp2ObjectType _objectTypeUnderHero = Mp2ObjectType.None;
    private HeroModes _modes = HeroModes.None;

    /// <summary>The name the map gave the hero, <c>_name</c>; empty means "use the default one".</summary>
    private string _customName = string.Empty;

    /// <summary>
    /// Creates a hero with the given id and race.
    /// </summary>
    /// <remarks>
    /// The reference builds all 73 slots up front in <c>AllHeroes::Init()</c>, passing the race of each
    /// hero explicitly (<c>heroes.cpp:2271-2314</c>); <see cref="AllHeroes"/> is the normal caller. The
    /// race is a constructor argument rather than a lookup so that the table stays in one place
    /// (<see cref="HeroRaces"/>) instead of being baked into the object.
    /// </remarks>
    /// <param name="id">Hero id, from <see cref="LordKilburn"/> to <see cref="DebugHero"/>.</param>
    /// <param name="race">Race of the hero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> is not a valid hero id (<see cref="IsValidId"/>).
    /// </exception>
    public Hero(int id, MapRace race)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, $"A hero id is between 1 and {IdCount - 1}.");
        }

        Id = id;
        Race = race;
        PortraitId = id;

        // The reference's Army points back at its commander from the constructor and takes the colour and the
        // race from it; the port's army keeps copies, filled here and kept in step by SetColor/LoadFromMP2.
        _army.Commander = this;
        _army.Race = race;
    }

    /// <summary>The id of the hero, <c>GetID()</c> (<c>heroes.h:330</c>).</summary>
    public int Id { get; private set; }

    /// <summary>
    /// The race of the hero, <c>GetRace()</c>. The class of the hero — knight, barbarian and so on — and
    /// what the recruitment pool filters on.
    /// </summary>
    /// <remarks>
    /// The table race is only a default: <see cref="LoadFromMP2"/> replaces it with the race the map asks
    /// for, which is why the setter is private rather than absent (<c>heroes.cpp:432-436</c>).
    /// </remarks>
    public MapRace Race { get; private set; }

    /// <summary>
    /// The portrait the hero is drawn with, <c>getPortraitId()</c>. It is the id unless the hero has a
    /// custom portrait, which is why <see cref="AllHeroes.GetHeroForHire"/> compares portraits and not ids.
    /// </summary>
    public int PortraitId { get; private set; }

    /// <summary>
    /// The color of the kingdom the hero serves, <c>GetColor()</c>; <see cref="PlayerColor.None"/> while the
    /// hero belongs to nobody, which is the state a hero waiting in a tavern is in.
    /// </summary>
    public PlayerColor Color { get; private set; } = PlayerColor.None;

    /// <summary>The mode bits of the hero, <c>Modes()</c> (<c>heroes.h:196</c>).</summary>
    public HeroModes Modes => _modes;

    /// <summary>
    /// Who gives the hero orders, <c>Heroes::GetControl()</c> (<c>heroes.cpp:2004-2007</c>).
    /// </summary>
    /// <remarks>
    /// 参考问王国（<c>Kingdom::GetControl()</c>），王国问 <c>Players::GetPlayerControl()</c>。端口没把英雄钉在
    /// 某个王国上（同一颜色只有一份玩家记录），所以直接问同一个地方，答案一样。
    /// </remarks>
    /// <returns>The control of the hero's player.</returns>
    public PlayerControl GetControl() => Players.GetPlayerControl(Color);

    /// <summary>The hero's side, <c>HeroBase::GetColor()</c>.</summary>
    /// <remarks>端口把颜色存在 <see cref="Color"/> 上，接口要求的是方法形状，这里只是转一手。</remarks>
    /// <returns>The colour the hero serves, or <see cref="PlayerColor.None"/>.</returns>
    public PlayerColor GetColor() => Color;

    /// <summary>
    /// Whether the hero may be offered in a tavern, <c>Heroes::isAvailableForHire()</c>
    /// (<c>heroes.cpp:1954</c>).
    /// </summary>
    /// <remarks>
    /// The reference also requires <c>isValid()</c>, i.e. an id other than <c>UNKNOWN</c>. The port's
    /// absent heroes are <see langword="null"/> slots in <see cref="AllHeroes"/> instead of objects with
    /// an unknown id, so a constructed hero is always valid and the test drops out.
    /// </remarks>
    public bool IsAvailableForHire => Color == PlayerColor.None && !_modes.HasFlag(HeroModes.Jail);

    /// <summary>
    /// Whether the hero serves the same side as <paramref name="color"/>, <c>Heroes::isFriends()</c>
    /// (<c>heroes.h:437</c>).
    /// </summary>
    /// <remarks>
    /// 参考问的是玩家名册上的联盟表（<c>Players::isFriends()</c>），不是英雄自己的颜色：同一方的人与同色的人
    /// 都算朋友。名册没建好（<see cref="Players.Get(PlayerColor)"/> 为 null）时参考答 false，端口由
    /// <see cref="Players.IsFriends(PlayerColor, PlayerColor)"/> 原样保留这个行为；调用方全是通行或战斗判定，
    /// 联盟写错是真 bug，所以判定只在这一处。
    /// </remarks>
    /// <param name="color">Color to test.</param>
    /// <returns><see langword="true"/> when the hero serves that color or its side.</returns>
    public bool IsFriends(PlayerColor color) => Players.IsFriends(Color, color);

    /// <summary>
    /// Whether this is a hero, <c>HeroBase::isHeroes()</c> (<c>heroes_base.cpp:91</c>).
    /// </summary>
    /// <remarks>
    /// True by definition: the reference answers <c>GetType() == HEROES</c> so that the battle-only
    /// <c>Captain</c> can answer false with the same call. The port has no captain yet; when it lands it
    /// implements <see cref="IArmyCommander"/> and this class does not change. The army asks the question to
    /// know whether it may be emptied — see <see cref="Army.SaveLastTroop"/>.
    /// </remarks>
    public bool IsHeroes => true;

    /// <summary>
    /// The object the hero is standing on, <c>getObjectTypeUnderHero()</c> (<c>heroes.h:405</c>).
    /// </summary>
    /// <remarks>
    /// The tile caches <see cref="Mp2ObjectType.ActionHero"/> while a hero stands on it and parks the real
    /// type here, so that removing the hero restores the object instead of leaving an empty tile.
    /// </remarks>
    public Mp2ObjectType ObjectTypeUnderHero => _objectTypeUnderHero;

    /// <summary>
    /// Stores the object type the hero covers, <c>setObjectTypeUnderHero()</c>
    /// (<c>heroes.h:410</c>).
    /// </summary>
    /// <param name="objectType">Type of the object under the hero.</param>
    /// <remarks>
    /// The reference refuses to store <c>MP2::OBJ_HERO</c> itself and writes <c>OBJ_NONE</c> instead: a
    /// hero can only stand on one tile, so "a hero on top of a hero" would lose the type of the tile
    /// below. Note that <c>OBJ_HERO</c> is the <b>action</b> value (<c>mp2.h:327</c>,
    /// <c>OBJ_NON_ACTION_HERO + 128 = 183</c>), so the comparison is against
    /// <see cref="Mp2ObjectType.ActionHero"/>; <see cref="Mp2ObjectType.Hero"/> is the twin the game never
    /// stores.
    /// </remarks>
    public void SetObjectTypeUnderHero(Mp2ObjectType objectType) =>
        _objectTypeUnderHero = objectType != Mp2ObjectType.ActionHero ? objectType : Mp2ObjectType.None;

    /// <summary>
    /// Assigns the hero to a kingdom, <c>SetColor()</c>. <see cref="PlayerColor.None"/> makes him available
    /// for hire again.
    /// </summary>
    /// <remarks>
    /// The army follows the hero: in the reference <c>Army::GetColor()</c> asks its commander, the port's army
    /// keeps a copy, so every colour change goes through here to keep the two from disagreeing.
    /// </remarks>
    /// <param name="color">Color of the kingdom.</param>
    public void SetColor(PlayerColor color)
    {
        Color = color;
        _army.Color = color;
    }

    /// <summary>Sets mode bits, <c>SetModes()</c> (<c>heroes.h:200</c>).</summary>
    /// <param name="modes">Bits to set.</param>
    public void SetModes(HeroModes modes) => _modes |= modes;

    /// <summary>Clears mode bits, <c>ResetModes()</c> (<c>heroes.h:205</c>).</summary>
    /// <param name="modes">Bits to clear.</param>
    public void ResetModes(HeroModes modes) => _modes &= ~modes;

    /// <summary>Clears every mode bit, the <c>modes = 0</c> at the start of <c>LoadFromMP2()</c>.</summary>
    public void ClearModes() => _modes = HeroModes.None;

    /// <summary>
    /// Whether the hero commands a boat, <c>Heroes::isShipMaster()</c> (<c>heroes.h:631-634</c>).
    /// </summary>
    public bool IsShipMaster => _modes.HasFlag(HeroModes.ShipMaster);

    /// <summary>
    /// Marks or clears the boat, <c>Heroes::SetShipMaster()</c> (<c>heroes.cpp:1717-1720</c>).
    /// </summary>
    /// <remarks>
    /// 参考只有这两种写法的三目，端口照抄：这个位一旦置上，海战士气/幸运才结算、<c>SUMMONBOAT</c> 之类才被拒、
    /// <c>GetMaxMovePoints()</c> 才走水路。
    /// </remarks>
    /// <param name="isShipMaster">Whether the hero commands a boat.</param>
    public void SetShipMaster(bool isShipMaster)
    {
        if (isShipMaster)
        {
            SetModes(HeroModes.ShipMaster);
        }
        else
        {
            ResetModes(HeroModes.ShipMaster);
        }
    }

    /// <summary>
    /// The land region the hero left when he took a boat, <c>Heroes::_lastGroundRegion</c> (<c>heroes.h:801</c>).
    /// </summary>
    /// <remarks>
    /// 参考在 <c>ActionToBoat()</c> 里写下它，然后<b>没有任何读者</b>——AI 与存档各自存一份自己的解释。
    /// 端口照抄，只为让「上船前在哪块陆地」这件事在状态里有据可查；默认 0 与参考一致。
    /// </remarks>
    public uint LastGroundRegion { get; private set; }

    /// <summary>
    /// Records the region a boat was pushed off from, <c>Heroes::setLastGroundRegion()</c> (<c>heroes.h:840</c>).
    /// </summary>
    /// <param name="region">Region of the tile the hero stood on before boarding.</param>
    public void SetLastGroundRegion(uint region) => LastGroundRegion = region;

    /// <summary>
    /// Opacity the hero is drawn with, <c>Heroes::getAlphaValue()</c> (<c>heroes.h:679-682</c>).
    /// </summary>
    /// <remarks>
    /// 参考只在英雄传送进出地图（<c>heroes_move.cpp</c> 的淡入淡出）时改写它，那一半还没转，所以现在恒为 255。
    /// </remarks>
    public byte AlphaValue { get; private set; } = 255;

    /// <summary>
    /// Sets the opacity the hero is drawn with, <c>Heroes::setAlphaValue()</c> (<c>heroes.h:684-687</c>).
    /// </summary>
    /// <param name="alphaValue">Opacity, <c>255</c> for a fully visible hero.</param>
    public void SetAlphaValue(byte alphaValue) => AlphaValue = alphaValue;

    /// <summary>
    /// The tile of the monster this hero is about to attack, <c>Heroes::GetAttackedMonsterTileIndex()</c>
    /// (<c>heroes.h:669-671</c>).
    /// </summary>
    /// <remarks>
    /// 参考只在攻击怪物前后改写它（<c>heroes_action.cpp:437</c> 置为目标格，<c>:495</c> 复位成 <c>-1</c>），
    /// 而地图渲染用它给该格的怪物换朝向英雄的攻击帧。行动那一半还没转，所以现在没有写者，渲染读到的恒是 <c>-1</c>。
    /// </remarks>
    public int AttackedMonsterTileIndex { get; private set; } = -1;

    /// <summary>
    /// Records the monster the hero attacks, <c>Heroes::SetAttackedMonsterTileIndex()</c>
    /// (<c>heroes.h:674-677</c>).
    /// </summary>
    /// <param name="tileIndex">Tile of the monster, <c>-1</c> when the attack is over.</param>
    public void SetAttackedMonsterTileIndex(int tileIndex) => AttackedMonsterTileIndex = tileIndex;

    /// <summary>
    /// The castle whose gate the hero stands on, <c>Heroes::inCastle()</c>/<c>inCastleMutable()</c>
    /// (<c>heroes.cpp:1245-1256</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考先从世界取出入口格上的城堡，再确认「那座城堡记的驻守英雄就是我」（访客与守将都站在门口，只有守将算）。
    /// 端口的 <see cref="Castle.Castle"/> 没有驻守英雄成员：英雄在城堡里就是站在入口格上，AI 层也一直这么判，
    /// 所以这里只比入口，注释说明这层差别。
    /// </para>
    /// <para>
    /// 无主英雄（<see cref="PlayerColor.None"/>）参考直接返回空：他站哪儿都不算在谁的城堡里。
    /// </para>
    /// </remarks>
    /// <param name="world">The world holding the castle entrances.</param>
    /// <returns>The castle, or <see langword="null"/> when the hero is not at one.</returns>
    public Castle.Castle? InCastle(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return Color == PlayerColor.None ? null : world.GetCastleEntrance(MapIndex);
    }

    /// <summary>
    /// The experience a freshly created hero starts with, <c>Heroes::_getStartingXp()</c>
    /// (<c>heroes.cpp:2009-2012</c>).
    /// </summary>
    /// <remarks>
    /// 参考在构造函数里写死 <c>_experience( _getStartingXp() )</c>，所以新英雄不是零经验而是 40~90 随机值；
    /// 战役英雄另有 5000 的构造参数。端口把这一步交给创建方（<see cref="AllHeroes.Place"/>），构造函数保持
    /// 零经验——地图读入时经验由存档/MP2 覆盖，构造函数再掷一次会白白消耗随机数。
    /// </remarks>
    /// <returns>The random starting experience, 40 to 90.</returns>
    public static uint GetStartingExperience() => (uint)Rand.Get(40, 90);

    /// <summary>
    /// Gives the hero a custom portrait, <c>setPortraitId()</c>. The mode bit is not set here — the
    /// reference sets <c>CUSTOM</c> where the portrait is chosen.
    /// </summary>
    /// <param name="portraitId">Portrait index.</param>
    public void SetPortraitId(int portraitId) => PortraitId = portraitId;

    /// <summary>
    /// The tile the hero stands on, <c>GetIndex()</c>, or <see cref="NoPosition"/> while he stands nowhere.
    /// </summary>
    /// <remarks>
    /// The reference stores the position as a point (<c>Position::_center</c>) and recovers the index with
    /// <c>Maps::GetIndex()</c>. The port stores the index and leaves the point to
    /// <see cref="Maps.MapGeometry.GetPoint"/>, which needs the map width the hero does not know.
    /// </remarks>
    public int MapIndex { get; private set; } = NoPosition;

    /// <summary>
    /// The name of the hero, <c>Heroes::GetName()</c> (<c>heroes.cpp:2100-2116</c>): the name the map gave him,
    /// or the race's default name when the map did not name him.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>_name</c> 只在读地图时由文字层写入，空着就回落到 <c>defaultHeroNames[id]</c>；端口照抄这个回落，
    /// 于是没有自定义名字的英雄也有名字（<see cref="HeroNames"/>）。
    /// </remarks>
    public string Name
    {
        get => _customName.Length > 0 ? _customName : HeroNames.Get(Id);
        private set => _customName = value;
    }

    /// <summary>
    /// Restores the stolen French characters of the name, <c>Heroes::fixFrenchCharactersInName()</c>
    /// (called from <c>world.cpp:1281-1283</c>).
    /// </summary>
    /// <remarks>
    /// 参考只处理 <c>_name</c>——默认名字是 ASCII，不需要修；端口同样只动自定义名字，否则会把默认名字写进
    /// <c>_customName</c>，之后再也回不到这张表。
    /// </remarks>
    internal void FixFrenchCharactersInName()
    {
        if (_customName.Length > 0)
        {
            _customName = MapTextCodec.FixFrenchCharacters(_customName);
        }
    }

    /// <summary>
    /// The tile a patrolling hero guards, <c>_patrolCenter</c>; the tile he stands on when the map placed
    /// him, <see cref="NoPosition"/> otherwise.
    /// </summary>
    public int PatrolCenterIndex { get; private set; } = NoPosition;

    /// <summary>
    /// How far a patrolling hero may wander from <see cref="PatrolCenterIndex"/>, <c>_patrolDistance</c>.
    /// </summary>
    public int PatrolDistance { get; private set; }

    /// <summary>
    /// What the AI uses this hero for, <c>getAIRole()</c>/<c>setAIRole()</c> (<c>heroes.h:679-687</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="HeroRole.Hunter"/> until the portfolio of the AI is ported; the planner only reads it so far, to
    /// tell the heroes it sends into a fight from the ones it keeps out of one.
    /// </remarks>
    public HeroRole AiRole { get; set; } = HeroRole.Hunter;

    /// <summary>
    /// Puts the hero on a tile, <c>Position::SetIndex()</c> as called by <c>LoadFromMP2()</c>
    /// (<c>heroes.cpp:427</c>).
    /// </summary>
    /// <param name="mapIndex">Tile index.</param>
    public void SetMapIndex(int mapIndex) => MapIndex = mapIndex;

    /// <summary>
    /// Reads the information block of the map, <c>Heroes::LoadFromMP2()</c> (<c>heroes.cpp:421</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is here</b>: the mode reset, the jail bit, the position, the color, the race, the default army
    /// of the race, the custom portrait and its validation, the custom name and the patrol role. That is the
    /// part of the block that can be applied without a system the port does not have.
    /// </para>
    /// <para>
    /// <b>What is missing, and why</b>: the primary skills and initial spell of the race
    /// (<c>HeroBase::LoadDefaults</c>), the secondary skills of the race, the custom army, the artifacts, the
    /// level up from <see cref="Mp2HeroInfo.Experience"/> (the experience itself now has a home —
    /// <see cref="SetExperience"/> — but replaying the level-ups here would need the map seed and the
    /// chooser), the spell points and the movement points. Each of them belongs to the skill, the artifact,
    /// the level up or the movement system; reading them here would only park the values in a field nobody
    /// reads. The default army is here because the movement points of the hero are derived from it.
    /// </para>
    /// <para>
    /// The <c>assert( _objectTypeUnderHero == OBJ_NONE )</c> at the end of the reference function is not
    /// reproduced: a hero that is placed twice would keep a stale type, and the port's
    /// <see cref="SetObjectTypeUnderHero"/> overwrites it on the next <c>SetHero</c> anyway.
    /// </para>
    /// </remarks>
    /// <param name="mapIndex">Tile index the hero stands on.</param>
    /// <param name="color">Kingdom of the hero.</param>
    /// <param name="race">Race the map asks for.</param>
    /// <param name="isInJail">Whether the hero waits in a jail.</param>
    /// <param name="info">The information block.</param>
    public void LoadFromMP2(int mapIndex, PlayerColor color, MapRace race, bool isInJail, Mp2HeroInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        ClearModes();

        if (isInJail)
        {
            SetModes(HeroModes.Jail);
        }

        SetMapIndex(mapIndex);
        SetColor(color);

        // The hero's race can be changed when the portrait the map asks for does not match the race of the
        // hero that carries it, or when no free hero of the requested race was left.
        if (Race != race)
        {
            SetModes(HeroModes.Custom);
            Race = race;
            Army.Race = race;
        }

        // The reference gives the hero the army of his race at the starting strength right after the race
        // change (heroes.cpp:453). Without it Heroes::GetMaxMovePoints() returns zero — an army of no troops
        // has no slowest troop, and the movement points of a hero are derived from it — so a hero loaded from
        // the map could not take a single step. The remaining defaults of the reference (primary skills,
        // initial spell, secondary skills) and the custom army of the block are still not read: they belong to
        // systems the port has not taken over yet.
        Army.Reset(true);

        if (info.HasCustomPortrait)
        {
            SetModes(HeroModes.Custom);

            // The MP2 file counts portraits from 0, the engine from 1 (heroes.cpp:491).
            int portraitId = info.PortraitId + 1;

            if (!IsValidId(portraitId))
            {
                GameLog.Warn($"Hero {Id}: invalid custom portrait id {portraitId}, falling back to the hero id.");
                portraitId = Id;
            }

            SetPortraitId(portraitId);
        }

        // An empty name can be set in the original editor; the reference keeps the default one.
        if (info.HasCustomName && !string.IsNullOrEmpty(info.Name))
        {
            SetModes(HeroModes.Custom);
            Name = info.Name;
        }

        if (info.IsOnPatrol)
        {
            SetModes(HeroModes.Patrol);
            PatrolCenterIndex = mapIndex;
            PatrolDistance = info.PatrolDistanceOrJailRace;
        }
    }

    /// <summary>
    /// Whether <paramref name="heroId"/> names a hero, <c>Heroes::isValidId()</c>
    /// (<c>heroes.h:723</c>).
    /// </summary>
    /// <param name="heroId">Id to test.</param>
    /// <returns><see langword="true"/> for the 72 real and debug ids, <see langword="false"/> otherwise.</returns>
    public static bool IsValidId(int heroId) => heroId > UnknownId && heroId < IdCount;
}
