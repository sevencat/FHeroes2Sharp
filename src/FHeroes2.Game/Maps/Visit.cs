using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Who remembers that an object has been visited, <c>Visit::Type</c> (<c>visit.h:28-33</c>).
/// </summary>
public enum VisitScope
{
    /// <summary>Remembered by the hero who did it, <c>Visit::LOCAL</c>.</summary>
    Local,

    /// <summary>Remembered by the hero's kingdom, <c>Visit::GLOBAL</c>.</summary>
    Global,
}

/// <summary>
/// A tile index together with the object that was found there, <c>IndexObject</c> (<c>pairs.h:34-44</c>).
/// </summary>
/// <remarks>
/// <para>
/// The pair exists because a tile can change its object: a windmill that has been used still sits on the same tile
/// but a hero who took it away leaves something else behind, and «the obelisk at 1234» has to survive that. The
/// reference inherits from <c>std::pair&lt;int32_t, MP2::MapObjectType&gt;</c>; a record struct gives the same
/// value semantics and a free equality.
/// </para>
/// <para>
/// The kingdom's visit list is a list of these, newest first (<c>Kingdom::SetVisited</c> pushes to the front), so
/// that «is this object visited» can stop at the first match for the type.
/// </para>
/// </remarks>
/// <param name="Index">Tile index on the map.</param>
/// <param name="Object">The object type found on that tile.</param>
public readonly record struct IndexObject(int Index, Mp2ObjectType Object)
{
    /// <summary>Whether this pair is about a tile, <c>IndexObject::isIndex()</c>.</summary>
    /// <param name="index">Tile index to test.</param>
    /// <returns><see langword="true"/> when the pair is about that tile.</returns>
    public bool IsIndex(int index) => Index == index;

    /// <summary>Whether this pair is about an object type, <c>IndexObject::isObject()</c>.</summary>
    /// <param name="objectType">Object type to test.</param>
    /// <returns><see langword="true"/> when the pair is about that type.</returns>
    public bool IsObject(Mp2ObjectType objectType) => Object == objectType;
}

/// <summary>
/// The three questions the visit lists are filtered with, <c>Visit</c> (<c>visit.h:24-33</c>, <c>visit.cpp:22-40</c>).
/// </summary>
/// <remarks>
/// <para>
/// These are not about a hero or a kingdom: they answer «may this object be visited again, and if not, when does it
/// become visitable again». The three lifetimes are the same three the map objects are classified by, which is why
/// they forward straight to <see cref="Mp2ObjectTypes.IsDayLife"/>, <see cref="Mp2ObjectTypes.IsWeekLife"/> and
/// <see cref="Mp2ObjectTypes.IsBattleLife"/> — a kingdom clears the day-life visits every day and the week-life ones
/// every week, and the battle-life ones are never cleared by time at all.
/// </para>
/// </remarks>
public static class Visit
{
    /// <summary>Whether the object is refreshed daily, <c>Visit::isDayLife()</c>.</summary>
    /// <param name="visit">The visit to test.</param>
    /// <returns><see langword="true"/> for a daily object such as a magic well.</returns>
    public static bool IsDayLife(IndexObject visit) => Mp2ObjectTypes.IsDayLife(visit.Object);

    /// <summary>Whether the object is refreshed weekly, <c>Visit::isWeekLife()</c>.</summary>
    /// <param name="visit">The visit to test.</param>
    /// <returns><see langword="true"/> for a weekly object such as a windmill or a dwelling.</returns>
    public static bool IsWeekLife(IndexObject visit) => Mp2ObjectTypes.IsWeekLife(visit.Object);

    /// <summary>Whether the object is spent for good, <c>Visit::isBattleLife()</c>.</summary>
    /// <param name="visit">The visit to test.</param>
    /// <returns><see langword="true"/> for an object that pays once and never again.</returns>
    public static bool IsBattleLife(IndexObject visit) => Mp2ObjectTypes.IsBattleLife(visit.Object);
}
