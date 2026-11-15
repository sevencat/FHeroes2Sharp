using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// A hero of the world map. The first slice of <c>Heroes</c>
/// (<c>参考：src/fheroes2/heroes/heroes.h:63</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the identity plus the two fields a tile and a tavern need, not the hero.</b> The army, the
/// secondary skills, the spell book, the artifacts, the route, the experience and the movement points all
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
    }

    /// <summary>The id of the hero, <c>GetID()</c> (<c>heroes.h:330</c>).</summary>
    public int Id { get; }

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
    /// The reference asks the kingdom of the hero and the one of the color, and answers true for an
    /// alliance as well as for the same kingdom. The port has neither kingdoms nor alliances yet, so the
    /// answer is "the same color". Every caller of the reference form is a passability or combat decision
    /// where a wrong alliance would be a real bug, so the gap is here — one place — rather than spread
    /// over <see cref="Tile.IsPassableFrom"/>.
    /// </remarks>
    /// <param name="color">Color to test.</param>
    /// <returns><see langword="true"/> when the hero serves that color.</returns>
    public bool IsFriends(PlayerColor color) => Color == color;

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
    /// <param name="color">Color of the kingdom.</param>
    public void SetColor(PlayerColor color) => Color = color;

    /// <summary>Sets mode bits, <c>SetModes()</c> (<c>heroes.h:200</c>).</summary>
    /// <param name="modes">Bits to set.</param>
    public void SetModes(HeroModes modes) => _modes |= modes;

    /// <summary>Clears mode bits, <c>ResetModes()</c> (<c>heroes.h:205</c>).</summary>
    /// <param name="modes">Bits to clear.</param>
    public void ResetModes(HeroModes modes) => _modes &= ~modes;

    /// <summary>Clears every mode bit, the <c>modes = 0</c> at the start of <c>LoadFromMP2()</c>.</summary>
    public void ClearModes() => _modes = HeroModes.None;

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
    /// The name the map gave the hero, <c>GetName()</c>, empty when the map did not name him.
    /// </summary>
    /// <remarks>
    /// The 73 default names are a table with translated strings (<c>heroes.cpp</c>, <c>_name</c>
    /// initialised per race), which the port has no text layer for yet; only custom names reach this
    /// property so far.
    /// </remarks>
    public string Name { get; private set; } = string.Empty;

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
    /// <b>What is here</b>: the mode reset, the jail bit, the position, the color, the race, the custom
    /// portrait and its validation, the custom name and the patrol role. That is the part of the block
    /// that only needs the hero — everything else it carries feeds a system the port does not have.
    /// </para>
    /// <para>
    /// <b>What is missing, and why</b>: the default army, primary skills and initial spell of the race
    /// (<c>HeroBase::LoadDefaults</c>, <c>_army.Reset</c>), the secondary skills of the race, the custom
    /// army, the artifacts, the level up from <see cref="Mp2HeroInfo.Experience"/>, the spell points and
    /// the movement points. Each of them belongs to the army, the skill, the artifact, the level up or the
    /// movement system; reading them here would only park the values in a field nobody reads.
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
        }

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
