namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The name a week or a month goes by, <c>WeekName</c> (<c>week.h:29-72</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference's three blocks are kept apart because they answer different draws: the first fifteen are a
/// regular week, the next ten the first week of a month (which is called a month), and the last two are the
/// special ones — a monster's week and the plague. The values are the reference's order and must stay that way,
/// because <c>RandomWeek</c> draws inside the ranges by number (<c>week.cpp:47</c>, <c>week.cpp:62</c>).
/// </para>
/// <para>
/// The names themselves are not here yet: <c>Week::GetName()</c> (<c>week.cpp:126</c>) goes through the
/// translation table, and a monster's week also needs <c>Monster::GetName()</c>, neither of which the port has
/// (ADR-113).
/// </para>
/// </remarks>
public enum WeekName
{
    /// <summary>No week yet, <c>WeekName::UNNAMED</c>.</summary>
    Unnamed = 0,

    /// <summary>A regular week of the month, <c>week.cpp:52</c>.</summary>
    Squirrel,
    Rabbit,
    Gopher,
    Badger,
    Rat,
    Eagle,
    Weasel,
    Raven,
    Mongoose,
    Dog,
    Aardvark,
    Lizard,
    Tortoise,
    Hedgehog,
    Condor,

    /// <summary>The first week of a month, which is a month, <c>week.cpp:67</c>.</summary>
    Ant,
    Grasshopper,
    Dragonfly,
    Spider,
    Butterfly,
    Bumblebee,
    Locust,
    Earthworm,
    Hornet,
    Beetle,

    /// <summary>A monster's week, or a monster's month on the first week, <c>week.cpp:86</c>.</summary>
    Monsters,

    /// <summary>The month of the plague, <c>week.cpp:70</c>.</summary>
    Plague,
}
