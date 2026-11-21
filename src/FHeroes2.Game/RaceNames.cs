using FHeroes2.Assets.Maps;

namespace FHeroes2.Game;

/// <summary>
/// The display name of a race, <c>Race::String()</c> (<c>race.cpp:30-53</c>).
/// </summary>
/// <remarks>
/// 城建窗口的状态栏要写「Recruit %{name} the %{race}」（<c>castle_town.cpp:566-580</c>），而这几个词就是参考里
/// 界面上的原文，所以放在游戏层而不是让它跟着某个窗口走。
/// </remarks>
public static class RaceNames
{
    /// <summary>The name of one race.</summary>
    /// <param name="race">The race.</param>
    /// <returns>The name, empty for a value that is not a race.</returns>
    public static string Get(MapRace race) => race switch
    {
        MapRace.Knight => "Knight",
        MapRace.Barbarian => "Barbarian",
        MapRace.Sorceress => "Sorceress",
        MapRace.Warlock => "Warlock",
        MapRace.Wizard => "Wizard",
        MapRace.Necromancer => "Necromancer",
        MapRace.Multiple => "Multi",
        MapRace.Random => "Random",
        MapRace.None => "Neutral",
        _ => string.Empty,
    };
}
