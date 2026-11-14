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
}
