using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game;

/// <summary>
/// A map object that belongs to a player, the port of <c>CapturedObject</c> (<c>world.h:117</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference also keeps the guardians of the object (<c>Troop guardians</c>) so that a mine is
/// defended again after it changes hands. The army system is not ported yet, so the structure starts
/// with the object/color pair; the troop is added together with <c>Troop</c> — see
/// <c>docs/DECISIONS.md</c>, ADR-078.
/// </para>
/// </remarks>
public sealed class CapturedObject
{
    /// <summary>Object type at the moment it was captured, the first half of <c>objCol</c>.</summary>
    public Mp2ObjectType ObjectType { get; private set; } = Mp2ObjectType.None;

    /// <summary>Owner of the object, the second half of <c>objCol</c>.</summary>
    public PlayerColor Color { get; private set; } = PlayerColor.None;

    /// <summary>
    /// Sets the object type and the owner, <c>CapturedObject::Set()</c> (<c>world.h:134</c>).
    /// </summary>
    /// <param name="objectType">Type of the captured object.</param>
    /// <param name="color">New owner.</param>
    public void Set(Mp2ObjectType objectType, PlayerColor color)
    {
        ObjectType = objectType;
        Color = color;
    }

    /// <summary>Changes the owner only, <c>CapturedObject::SetColor()</c> (<c>world.h:139</c>).</summary>
    /// <param name="color">New owner.</param>
    public void SetColor(PlayerColor color) => Color = color;
}

/// <summary>
/// The objects of the map that belong to a player, keyed by tile index, the port of
/// <c>CapturedObjects</c> (<c>world.h:145</c>, <c>world.cpp:171</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>std::map</c> and relies on <c>operator[]</c> creating an empty entry
/// on read, which is why <see cref="Get"/> inserts and <see cref="GetColor"/> does not. Both are kept:
/// a captured object with <see cref="PlayerColor.None"/> is a real entry meaning "nobody owns this",
/// and the difference between "the key exists" and "the key does not exist" is only visible through
/// <see cref="Contains"/>.
/// </para>
/// <para>
/// <c>ClearFog</c> of the reference is not here: it needs <c>Maps::ClearFog</c> and the fog directions,
/// which are added with the fog system.
/// </para>
/// </remarks>
public sealed class CapturedObjects
{
    // Sorted like std::map so that iteration is in tile order: GetCountMines and the savegame rely on
    // a stable order and the reference never sorts explicitly.
    private readonly SortedDictionary<int, CapturedObject> _objects = [];

    /// <summary>Number of captured objects, <c>CapturedObjects::size()</c>.</summary>
    public int Count => _objects.Count;

    /// <summary>All entries in tile index order.</summary>
    public IEnumerable<KeyValuePair<int, CapturedObject>> Entries => _objects;

    /// <summary>Drops every entry, <c>clear()</c> of the reference's <c>Reset()</c> (<c>world.cpp:299</c>).</summary>
    public void Clear() => _objects.Clear();

    /// <summary>Whether the map already tracks the tile, <c>find( index ) != end()</c>.</summary>
    /// <param name="index">Tile index.</param>
    /// <returns><see langword="true"/> when an entry exists.</returns>
    public bool Contains(int index) => _objects.ContainsKey(index);

    /// <summary>
    /// The entry of a tile, created on first access, <c>CapturedObjects::Get()</c> (<c>world.h:160</c>).
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns>The entry; a new one owns nothing and has no object type.</returns>
    public CapturedObject Get(int index)
    {
        if (!_objects.TryGetValue(index, out CapturedObject? capturedObject))
        {
            capturedObject = new CapturedObject();
            _objects.Add(index, capturedObject);
        }

        return capturedObject;
    }

    /// <summary>
    /// Owner of a tile, <c>CapturedObjects::GetColor()</c> (<c>world.cpp:220</c>).
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns>The owner, or <see cref="PlayerColor.None"/> when the tile is not tracked.</returns>
    public PlayerColor GetColor(int index) => _objects.TryGetValue(index, out CapturedObject? capturedObject) ? capturedObject.Color : PlayerColor.None;

    /// <summary>
    /// Records a capture, <c>CapturedObjects::Set()</c> (<c>world.cpp:171</c>).
    /// </summary>
    /// <remarks>
    /// The reference wipes the guardians when the object changes hands; that branch is missing until
    /// <c>Troop</c> exists, and it can only ever clear state this class does not keep yet.
    /// </remarks>
    /// <param name="index">Tile index.</param>
    /// <param name="objectType">Type of the object on the tile.</param>
    /// <param name="color">New owner.</param>
    public void Set(int index, Mp2ObjectType objectType, PlayerColor color) => Get(index).Set(objectType, color);

    /// <summary>Changes the owner of a tracked tile, <c>CapturedObjects::SetColor()</c> (<c>world.h:151</c>).</summary>
    /// <param name="index">Tile index.</param>
    /// <param name="color">New owner.</param>
    public void SetColor(int index, PlayerColor color) => Get(index).SetColor(color);

    /// <summary>
    /// Drops the owner of every object of a color, <c>CapturedObjects::ResetColor()</c>
    /// (<c>world.cpp:261</c>).
    /// </summary>
    /// <remarks>
    /// The reference also repaints the ownership flags on the tiles (<c>Tile::setOwnershipFlag</c>);
    /// that side is the sprite update of the flag object, which is not ported, so only the data changes
    /// here. The object type of the entry is kept, which is what makes the entry still countable.
    /// </remarks>
    /// <param name="color">Color that loses its objects.</param>
    public void ResetColor(PlayerColor color)
    {
        foreach (CapturedObject capturedObject in _objects.Values)
        {
            if (capturedObject.Color == color)
            {
                capturedObject.SetColor(PlayerColor.None);
            }
        }
    }

    /// <summary>
    /// How many objects of one type one player owns, <c>CapturedObjects::GetCount()</c>
    /// (<c>world.cpp:182</c>).
    /// </summary>
    /// <param name="objectType">Object type to count.</param>
    /// <param name="ownerColor">Owner to count for.</param>
    /// <returns>Number of matching entries.</returns>
    public uint GetCount(Mp2ObjectType objectType, PlayerColor ownerColor)
    {
        uint result = 0;

        foreach (CapturedObject capturedObject in _objects.Values)
        {
            if (capturedObject.ObjectType == objectType && capturedObject.Color == ownerColor)
            {
                ++result;
            }
        }

        return result;
    }

    /// <summary>
    /// How many mines of one resource one player owns, <c>CapturedObjects::GetCountMines()</c>
    /// (<c>world.cpp:199</c>).
    /// </summary>
    /// <remarks>
    /// The resource of a mine is not stored here but read back from the tile: <c>metadata[0]</c> holds
    /// the resource type of a mine, an alchemist lab or a sawmill, so the world is needed to answer.
    /// </remarks>
    /// <param name="world">The world the tiles belong to.</param>
    /// <param name="resourceType"><c>Resource</c> constant to count.</param>
    /// <param name="ownerColor">Owner to count for.</param>
    /// <returns>Number of matching mines.</returns>
    public uint GetCountMines(World world, int resourceType, PlayerColor ownerColor)
    {
        ArgumentNullException.ThrowIfNull(world);

        uint count = 0;

        foreach ((int index, CapturedObject capturedObject) in _objects)
        {
            if (capturedObject.ObjectType != Mp2ObjectType.Mine || capturedObject.Color != ownerColor)
            {
                continue;
            }

            if (world.GetTile(index).GetDailyIncomeObjectResources(world).GetFirstValidResource().Type != resourceType)
            {
                continue;
            }

            ++count;
        }

        return count;
    }
}
