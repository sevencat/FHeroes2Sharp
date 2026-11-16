namespace FHeroes2.Assets.Maps;

/// <summary>
/// Starting race of a kingdom, the port of <c>Race</c> in <c>race.h</c>. The values are the bit set used
/// by the MP2 header; an MP2 file stores an index (0…7) that <see cref="MapRaces.FromIndex"/> converts.
/// </summary>
[Flags]
public enum MapRace : byte
{
    /// <summary>No race; also the value of a player slot that does not exist.</summary>
    None = 0x00,

    /// <summary>Knight.</summary>
    Knight = 0x01,

    /// <summary>Barbarian.</summary>
    Barbarian = 0x02,

    /// <summary>Sorceress.</summary>
    Sorceress = 0x04,

    /// <summary>Warlock.</summary>
    Warlock = 0x08,

    /// <summary>Wizard.</summary>
    Wizard = 0x10,

    /// <summary>Necromancer.</summary>
    Necromancer = 0x20,

    /// <summary>Multiple races (a random castle of the player's own race).</summary>
    Multiple = 0x40,

    /// <summary>Random race.</summary>
    Random = 0x80,
}

/// <summary>Conversions of the race indices stored inside an MP2 header (<c>Race::IndexToRace</c>).</summary>
public static class MapRaces
{
    private static readonly MapRace[] ByIndex =
    [
        MapRace.Knight,
        MapRace.Barbarian,
        MapRace.Sorceress,
        MapRace.Warlock,
        MapRace.Wizard,
        MapRace.Necromancer,
        MapRace.Multiple,
        MapRace.Random,
    ];

    /// <summary>
    /// Race stored at <paramref name="index"/>; anything outside 0…7 becomes
    /// <see cref="MapRace.None"/>, exactly like the reference implementation.
    /// </summary>
    public static MapRace FromIndex(int index) => index >= 0 && index < ByIndex.Length ? ByIndex[index] : MapRace.None;

    /// <summary>
    /// Index the header stores for <paramref name="race"/>, the port of <c>Race::RaceToIndex</c>;
    /// <see langword="null"/> for <see cref="MapRace.None"/>, which no header slot can hold.
    /// </summary>
    public static int? ToIndex(MapRace race)
    {
        for (int index = 0; index < ByIndex.Length; ++index)
        {
            if (ByIndex[index] == race)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>
    /// The six races a player can actually be, in the order <c>Race::Rand()</c> picks them
    /// (<c>race.cpp:90-108</c>).
    /// </summary>
    public static IReadOnlyList<MapRace> Basic { get; } =
    [
        MapRace.Knight,
        MapRace.Barbarian,
        MapRace.Sorceress,
        MapRace.Warlock,
        MapRace.Wizard,
        MapRace.Necromancer,
    ];

    /// <summary>
    /// A random race out of the six, <c>Race::Rand()</c> (<c>race.cpp:90-108</c>).
    /// </summary>
    /// <remarks>
    /// The reference rolls <c>Rand::Get( 1, 6 )</c> and maps the six answers onto the six races, so the draw is
    /// uniform and <see cref="MapRace.Random"/>/<see cref="MapRace.Multiple"/> are never its result.
    /// </remarks>
    /// <returns>One of <see cref="Basic"/>.</returns>
    public static MapRace Random() => Basic[Core.Random.Rand.Get(1, 6) - 1];

    /// <summary>
    /// Whether a race is one of the three the game paints as evil, <c>Race::isEvilRace()</c>
    /// (<c>race.cpp:129-148</c>).
    /// </summary>
    /// <remarks>
    /// Barbarians, warlocks and necromancers get the evil interface; the rest do not. The reference asserts on
    /// an unknown value and answers false, which is what the port does without the assertion (ADR-139).
    /// </remarks>
    /// <param name="race">The race.</param>
    /// <returns>True for the three evil races.</returns>
    public static bool IsEvil(MapRace race) =>
        race is MapRace.Barbarian or MapRace.Warlock or MapRace.Necromancer;

    /// <summary>
    /// Whether a race is one of the four spell-casting ones, <c>Race::isMagicalRace()</c>
    /// (<c>race.cpp:110-127</c>).
    /// </summary>
    /// <param name="race">The race.</param>
    /// <returns>True for sorceress, warlock, wizard and necromancer.</returns>
    public static bool IsMagical(MapRace race) =>
        race is MapRace.Sorceress or MapRace.Warlock or MapRace.Wizard or MapRace.Necromancer;

    /// <summary>
    /// The printable name of a race, <c>Race::String()</c> (<c>race.cpp:30-58</c>).
    /// </summary>
    /// <remarks>
    /// The reference wraps the words in <c>_()</c>; the port has no translation layer and returns the English
    /// text (ADR-137), including the <c>"Neutral"</c> of one that is not set.
    /// </remarks>
    /// <param name="race">The race.</param>
    /// <returns>The name of the race.</returns>
    public static string Name(MapRace race) => race switch
    {
        MapRace.Knight => "Knight",
        MapRace.Barbarian => "Barbarian",
        MapRace.Sorceress => "Sorceress",
        MapRace.Warlock => "Warlock",
        MapRace.Wizard => "Wizard",
        MapRace.Necromancer => "Necromancer",
        MapRace.Multiple => "Multi",
        MapRace.Random => "Random",
        _ => "Neutral"
    };
}
