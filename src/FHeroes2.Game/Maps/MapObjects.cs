namespace FHeroes2.Game.Maps;

/// <summary>
/// The map objects that carry data of their own, the reference's <c>MapObjects</c> (<c>world.h:73</c>):
/// signs and bottles, ground events and sphinxes, keyed by their UID — which for a map file is the index
/// of the tile the object stands on.
/// </summary>
/// <remarks>
/// <para>
/// The reference stores <c>std::unique_ptr&lt;MapBaseObject&gt;</c> and hands out pointers; the port stores
/// the objects themselves, so nothing can be released twice. <c>add()</c> replaces an object that has the
/// same UID, which is what <c>try_emplace</c> followed by an assignment means (<c>world.h:97</c>).
/// </para>
/// <para>
/// A lookup by tile index is what the reference does with <c>get( const fheroes2::Point &amp; )</c>
/// (<c>world.h:108</c>); the port works in tile indexes like the rest of the world (ADR-068), so it takes
/// the index directly.
/// </para>
/// </remarks>
public sealed partial class MapObjects
{
    private readonly Dictionary<int, MapObject> _objects = [];

    /// <summary>Number of objects, <c>_objects.size()</c>.</summary>
    public int Count => _objects.Count;

    /// <summary>The objects in insertion order.</summary>
    public IEnumerable<MapObject> All => _objects.Values;

    /// <summary>Adds an object, replacing one that stands on the same tile, <c>add()</c>.</summary>
    /// <param name="mapObject">The object to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mapObject"/> is <see langword="null"/>.</exception>
    public void Add(MapObject mapObject)
    {
        ArgumentNullException.ThrowIfNull(mapObject);

        _objects[mapObject.TileIndex] = mapObject;
    }

    /// <summary>Removes the object on a tile, <c>remove( uid )</c>.</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    public void Remove(int tileIndex) => _objects.Remove(tileIndex);

    /// <summary>The object of a tile, <c>get( uid )</c>.</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns>The object, or <see langword="null"/> when the tile has none.</returns>
    public MapObject? Get(int tileIndex) => _objects.GetValueOrDefault(tileIndex);

    /// <summary>The object of a tile when it is of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Type of the object to look for.</typeparam>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns>The object, or <see langword="null"/> when the tile has none of that type.</returns>
    public T? Get<T>(int tileIndex)
        where T : MapObject => Get(tileIndex) as T;

    /// <summary>Drops every object, <c>clear()</c>.</summary>
    public void Clear() => _objects.Clear();
}
