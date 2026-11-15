using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Which race a hero id belongs to, the port of <c>getHeroIdRangeForRace()</c> (<c>heroes.cpp:38</c>) and
/// of the race argument of every <c>Heroes</c> the reference builds in <c>AllHeroes::Init()</c>
/// (<c>heroes.cpp:2273-2310</c>).
/// </summary>
/// <remarks>
/// <para>
/// The 54 regular heroes are nine contiguous ids per race, so the reference can ask for a range instead of
/// a table. The campaign heroes (55-60) and The Price of Loyalty heroes (61-71) are not in those ranges:
/// their races only exist as constructor arguments in <c>Init()</c>, and that is where the port takes them
/// from as well.
/// </para>
/// <para>
/// A race of <see cref="MapRace.None"/> is not a race of a hero: it is the "any race" filter of
/// <see cref="AllHeroes.GetHeroForHire"/> and the reference's <c>getHeroIdRangeForRace( Race::NONE )</c>,
/// which returns the whole regular range <c>{ LORDKILBURN, CELIA }</c>.
/// </para>
/// </remarks>
public static class HeroRaces
{
    /// <summary>
    /// Highest id a kingdom may hire, <c>Heroes::CELIA</c>: the campaign and The Price of Loyalty heroes
    /// never appear in a tavern (<c>heroes.cpp:2349</c>).
    /// </summary>
    public const int MaxRegularHeroId = Hero.Celia;

    /// <summary>
    /// The six races of The Succession Wars, in the order <c>AllHeroes::Init()</c> creates them
    /// (<c>heroes.cpp:2273</c>). <see cref="MapRace.Multiple"/> and <see cref="MapRace.Random"/> are not
    /// hero races, and the campaign and The Price of Loyalty heroes are not part of these ranges.
    /// </summary>
    public static IReadOnlyList<MapRace> RegularRaces { get; } =
    [
        MapRace.Knight,
        MapRace.Barbarian,
        MapRace.Sorceress,
        MapRace.Warlock,
        MapRace.Wizard,
        MapRace.Necromancer,
    ];

    /// <summary>
    /// The id range of <paramref name="race"/>, <c>getHeroIdRangeForRace()</c> (<c>heroes.cpp:38</c>).
    /// </summary>
    /// <param name="race">The race, or <see cref="MapRace.None"/> for "any race".</param>
    /// <returns>The first and the last id, both inclusive.</returns>
    public static (int Min, int Max) GetHeroIdRange(MapRace race) => race switch
    {
        MapRace.Knight => (1, 9),
        MapRace.Barbarian => (10, 18),
        MapRace.Sorceress => (19, 27),
        MapRace.Warlock => (28, 36),
        MapRace.Wizard => (37, 45),
        MapRace.Necromancer => (46, 54),

        // Race::NONE and Race::RAND both fall here in the reference; RAND only reaches it as a bug, and
        // the caller of this method never passes it.
        _ => (Hero.LordKilburn, Hero.Celia),
    };

    /// <summary>
    /// The race of the hero with the given id. The inverse of <see cref="GetHeroIdRange"/> for the regular
    /// heroes, plus the per-hero races of <c>AllHeroes::Init()</c>.
    /// </summary>
    /// <param name="heroId">Hero id.</param>
    /// <returns>The race, or <see cref="MapRace.None"/> when the id is not a hero id.</returns>
    public static MapRace GetRace(int heroId)
    {
        if (!Hero.IsValidId(heroId))
        {
            return MapRace.None;
        }

        if (heroId <= MaxRegularHeroId)
        {
            foreach (MapRace race in RegularRaces)
            {
                (int min, int max) = GetHeroIdRange(race);

                if (heroId >= min && heroId <= max)
                {
                    return race;
                }
            }

            return MapRace.None;
        }

        return heroId switch
        {
            Hero.Roland => MapRace.Wizard,
            Hero.Corlagon => MapRace.Knight,
            Hero.Eliza => MapRace.Sorceress,
            Hero.Archibald => MapRace.Warlock,
            Hero.Halton => MapRace.Knight,
            Hero.Brax => MapRace.Necromancer,

            Hero.Solmyr => MapRace.Wizard,
            Hero.Dainwin => MapRace.Warlock,
            Hero.Mog => MapRace.Necromancer,
            Hero.UncleIvan => MapRace.Barbarian,
            Hero.Joseph => MapRace.Wizard,
            Hero.Gallavant => MapRace.Knight,
            Hero.Elderian => MapRace.Warlock,
            Hero.Ceallach => MapRace.Knight,
            Hero.Drakonia => MapRace.Wizard,
            Hero.Martine => MapRace.Sorceress,
            Hero.Jarkonas => MapRace.Barbarian,

            // IS_DEVEL() builds create the debug hero as a warlock (heroes.cpp:2313); release builds, which
            // is what the port mirrors, never create it at all.
            Hero.DebugHero => MapRace.Warlock,

            _ => MapRace.None,
        };
    }
}
