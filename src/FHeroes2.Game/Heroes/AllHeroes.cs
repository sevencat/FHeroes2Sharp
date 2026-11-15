using System.Collections;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Every hero of the game, <c>AllHeroes</c> (<c>heroes.h:823</c> / <c>heroes.cpp:2265</c>): the container
/// <c>World::vec_heroes</c> (<c>world.h:555</c>) and the recruitment pool of a map.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps a dense vector of <c>HEROES_COUNT</c> heroes and indexes it by id, so an id is a
/// slot, not a search key. The port does the same, with one difference: a slot that the reference fills
/// with a <c>Heroes( UNKNOWN, KNGT )</c> placeholder — The Price of Loyalty heroes of a map that does not
/// have them, the debug hero of a release build — is simply <see langword="null"/> here. The placeholder
/// exists only because the reference cannot store an empty slot; every consumer immediately tests
/// <c>isValid()</c>, which the null does on its own.
/// </para>
/// <para>
/// <see cref="Init"/> is what <c>World::Defaults()</c> reaches through <c>AllHeroes::Init()</c>
/// (<c>heroes.cpp:2265</c>), and it depends on the map version: The Price of Loyalty heroes only exist
/// for Price of Loyalty and Resurrection maps (<c>heroes.cpp:2291</c>).
/// </para>
/// </remarks>
public sealed class AllHeroes : IReadOnlyCollection<Hero>
{
    private readonly Hero?[] _slots = new Hero?[Hero.IdCount];

    /// <summary>Number of heroes in the container, <c>Size()</c>. Absent slots do not count.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// The hero in the slot of <paramref name="heroId"/>, <c>AllHeroes::Get( const int )</c>
    /// (<c>heroes.cpp:2411</c>).
    /// </summary>
    /// <param name="heroId">Hero id.</param>
    /// <returns>
    /// The hero, or <see langword="null"/> when the id is not a hero id or the slot is not filled for this
    /// map.
    /// </returns>
    public Hero? this[int heroId] => Hero.IsValidId(heroId) ? _slots[heroId] : null;

    /// <summary>
    /// The same lookup as the indexer; kept because the reference spells the call <c>Get()</c>.
    /// </summary>
    /// <param name="heroId">Hero id.</param>
    /// <returns>The hero, or <see langword="null"/>.</returns>
    public Hero? Get(int heroId) => this[heroId];

    /// <summary>
    /// The hero standing on a tile, <c>AllHeroes::Get( const fheroes2::Point & )</c>
    /// (<c>heroes.cpp:2404</c>).
    /// </summary>
    /// <remarks>
    /// The reference compares the center points of the heroes with the point it is given; the port stores
    /// the tile index instead of the point (<see cref="Hero.MapIndex"/>), so the test is the same with the
    /// map geometry already applied.
    /// </remarks>
    /// <param name="mapIndex">Tile index.</param>
    /// <returns>The hero, or <see langword="null"/> when no hero stands there.</returns>
    public Hero? GetAtMapIndex(int mapIndex)
    {
        if (mapIndex < 0)
        {
            return null;
        }

        foreach (Hero hero in this)
        {
            if (hero.MapIndex == mapIndex)
            {
                return hero;
            }
        }

        return null;
    }

    /// <summary>
    /// Fills the slots for a map of the given version, <c>AllHeroes::Init()</c> (<c>heroes.cpp:2265</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The six original races get nine heroes each, then the six campaign heroes, then the eleven The Price
    /// of Loyalty heroes — but only for maps that support them; otherwise those slots stay empty, exactly
    /// like the reference fills them with <c>UNKNOWN</c> placeholders. The debug hero is created only by
    /// <c>IS_DEVEL()</c> builds, so a port that mirrors release builds never creates it
    /// (<c>heroes.cpp:2312</c>).
    /// </para>
    /// <para>
    /// The campaign and The Price of Loyalty heroes start with 5000 experience points
    /// (<c>heroes.cpp:2283</c>); experience belongs to the level-up system, which is not ported, so the
    /// port creates them without it and reports nothing.
    /// </para>
    /// </remarks>
    /// <param name="version">Version of the map being loaded.</param>
    public void Init(MapVersion version)
    {
        Clear();

        foreach (MapRace race in HeroRaces.RegularRaces)
        {
            (int min, int max) = HeroRaces.GetHeroIdRange(race);

            for (int heroId = min; heroId <= max; ++heroId)
            {
                Place(heroId, race);
            }
        }

        // The Succession Wars campaign.
        Place(Hero.Roland, MapRace.Wizard);
        Place(Hero.Corlagon, MapRace.Knight);
        Place(Hero.Eliza, MapRace.Sorceress);
        Place(Hero.Archibald, MapRace.Warlock);
        Place(Hero.Halton, MapRace.Knight);
        Place(Hero.Brax, MapRace.Necromancer);

        if (version is MapVersion.PriceOfLoyalty or MapVersion.Resurrection)
        {
            Place(Hero.Solmyr, MapRace.Wizard);
            Place(Hero.Dainwin, MapRace.Warlock);
            Place(Hero.Mog, MapRace.Necromancer);
            Place(Hero.UncleIvan, MapRace.Barbarian);
            Place(Hero.Joseph, MapRace.Wizard);
            Place(Hero.Gallavant, MapRace.Knight);
            Place(Hero.Elderian, MapRace.Warlock);
            Place(Hero.Ceallach, MapRace.Knight);
            Place(Hero.Drakonia, MapRace.Wizard);
            Place(Hero.Martine, MapRace.Sorceress);
            Place(Hero.Jarkonas, MapRace.Barbarian);
        }
    }

    /// <summary>Empties the container, <c>AllHeroes::Clear()</c> (<c>heroes.h:845</c>).</summary>
    public void Clear()
    {
        Array.Clear(_slots);
        Count = 0;
    }

    /// <summary>
    /// Picks a hero for a tavern, <c>AllHeroes::GetHeroForHire()</c> (<c>heroes.cpp:2322</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference fills a candidate list in up to three passes — the requested race while avoiding
    /// custom portraits, then any race under the same restriction, then any hero at all — and only falls
    /// through when the previous pass found nothing. Only the 54 regular heroes are ever hired: a campaign
    /// or The Price of Loyalty hero never appears in a tavern (<c>heroes.cpp:2349</c>).
    /// </para>
    /// <para>
    /// Among the candidates the heroes already offered by some kingdom's tavern are dropped when possible
    /// (<c>Heroes::RECRUIT</c>), so that two kingdoms do not advertise the same hero; if every candidate is
    /// reserved, the reference allows the sharing rather than returning nothing.
    /// </para>
    /// <para>
    /// The pick itself is <c>Rand::Get()</c> over the candidate ids — with the thread generator, so it is
    /// not reproducible; the map seed does not enter here.
    /// </para>
    /// </remarks>
    /// <param name="race">Race to prefer; <see cref="MapRace.None"/> accepts any.</param>
    /// <param name="heroIdToIgnore">Hero that must not be returned, e.g. the one already offered.</param>
    /// <returns>The hero, or <see langword="null"/> when every hero is hired or imprisoned.</returns>
    public Hero? GetHeroForHire(MapRace race, int heroIdToIgnore = Hero.UnknownId)
    {
        HashSet<int> customPortraits = [];
        foreach (Hero hero in this)
        {
            if (hero.Modes.HasFlag(HeroModes.Custom))
            {
                customPortraits.Add(hero.PortraitId);
            }
        }

        List<int> candidates = [];

        void Fill(MapRace raceFilter, bool avoidCustomHeroes)
        {
            foreach (Hero hero in this)
            {
                // Only regular, non-campaign heroes can be hired.
                if (hero.Id > HeroRaces.MaxRegularHeroId || hero.Id == heroIdToIgnore)
                {
                    continue;
                }

                if (raceFilter != MapRace.None && hero.Race != raceFilter)
                {
                    continue;
                }

                if (!hero.IsAvailableForHire)
                {
                    continue;
                }

                if (avoidCustomHeroes && customPortraits.Contains(hero.PortraitId))
                {
                    continue;
                }

                candidates.Add(hero.Id);
            }
        }

        Fill(race, avoidCustomHeroes: true);

        if (candidates.Count == 0 && race != MapRace.None)
        {
            Fill(MapRace.None, avoidCustomHeroes: true);
        }

        if (candidates.Count == 0)
        {
            Fill(MapRace.None, avoidCustomHeroes: false);
        }

        if (candidates.Count == 0)
        {
            // Every hero is hired or imprisoned. The reference logs a warning and gives up.
            return null;
        }

        List<int> notReserved = [];
        foreach (int heroId in candidates)
        {
            if (!_slots[heroId]!.Modes.HasFlag(HeroModes.Recruit))
            {
                notReserved.Add(heroId);
            }
        }

        int pickedId = notReserved.Count > 0 ? Rand.Get(notReserved) : Rand.Get(candidates);

        return _slots[pickedId];
    }

    /// <summary>
    /// Iterates the heroes that exist in this map, in id order. The reference iterates all slots and
    /// asserts none is <see langword="null"/>; the port skips the empty ones, which is the same list of
    /// real heroes.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<Hero> GetEnumerator()
    {
        for (int heroId = Hero.LordKilburn; heroId < Hero.IdCount; ++heroId)
        {
            Hero? hero = _slots[heroId];

            if (hero is not null)
            {
                yield return hero;
            }
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a hero and puts it into its slot, the <c>emplace_back</c> of <c>Init()</c>.</summary>
    /// <param name="heroId">Hero id.</param>
    /// <param name="race">Race of the hero.</param>
    private void Place(int heroId, MapRace race)
    {
        _slots[heroId] = new Hero(heroId, race);
        ++Count;
    }
}
