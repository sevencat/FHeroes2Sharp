namespace FHeroes2.Game.Maps;

/// <summary>
/// A map object that carries data of its own and stands on one tile, the reference's
/// <c>MapBaseObject</c> (<c>maps_objects.h:42</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference derives <c>MapBaseObject</c> from <c>MapPosition</c> and keeps both a UID and a position;
/// for the objects of a map file the two are the same number, because <c>setUIDAndIndex()</c> assigns the
/// tile index to the UID as well (<c>maps_objects.h:57</c>). The port keeps the tile index and stores the
/// UID beside it, so that a port of the save format later reads the same two numbers the reference does.
/// </para>
/// <para>
/// Only the objects whose data sits in an information block live here: a sign or a bottle, a ground event
/// and, later, a sphinx. A castle keeps its data in the castle table, a hero in the hero table.
/// </para>
/// </remarks>
public abstract class MapObject
{
    /// <summary>
    /// Index of the tile the object stands on, <c>MapPosition::GetIndex()</c>. <c>-1</c> until the object
    /// is placed, which is where an empty <c>MapPosition</c> points.
    /// </summary>
    public int TileIndex { get; private set; } = -1;

    /// <summary>
    /// The object id, <c>MapBaseObject::GetUID()</c>. A map object takes its tile index as the UID.
    /// </summary>
    public uint Uid { get; private set; }

    /// <summary>Places the object on the tile its information block belongs to, <c>setUIDAndIndex()</c>.</summary>
    /// <param name="tileIndex">Index of the tile.</param>
    protected void PlaceOn(int tileIndex)
    {
        TileIndex = tileIndex;
        Uid = (uint)tileIndex;
    }
}
