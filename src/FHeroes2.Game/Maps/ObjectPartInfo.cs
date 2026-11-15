using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// One part (tile) of a map object, the port of <c>Maps::ObjectPartInfo</c>
/// (<c>参考：src/fheroes2/maps/map_object_info.h:42</c>).
///
/// <para>
/// An object usually consists of several parts; exactly one of them is the main part and comes first.
/// All parts of an object share the same ICN resource, see <see cref="IcnType"/>.
/// </para>
/// </summary>
public class ObjectPartInfo
{
    /// <summary>Creates a part, the constructor of the reference.</summary>
    /// <param name="icn">ICN resource the sprite comes from.</param>
    /// <param name="index">Sprite index inside that resource.</param>
    /// <param name="offset">Tile offset from the main object tile.</param>
    /// <param name="type">Object type of this part; shadows have none.</param>
    public ObjectPartInfo(Mp2ObjectIcnType icn, uint index, Point offset, Mp2ObjectType type)
    {
        IcnType = icn;
        IcnIndex = index;
        TileOffset = offset;
        ObjectType = type;
    }

    /// <summary>A tile offset from the main object tile, <c>tileOffset</c>.</summary>
    public Point TileOffset { get; }

    /// <summary>Image index from the <see cref="IcnType"/> resource, <c>icnIndex</c>.</summary>
    public uint IcnIndex { get; }

    /// <summary>
    /// Object type associated with this part, <c>objectType</c>. Not every part has one: shadows, for
    /// example, are <see cref="Mp2ObjectType.None"/>.
    /// </summary>
    public Mp2ObjectType ObjectType { get; }

    /// <summary>
    /// ICN resource associated with this part, <c>icnType</c>. Some objects, towns for instance, take
    /// parts from different resources.
    /// </summary>
    public Mp2ObjectIcnType IcnType { get; }

    /// <summary>
    /// How many following images by index animate this part, <c>animationFrames</c>. Zero for the
    /// majority of parts.
    /// </summary>
    public byte AnimationFrames { get; set; }

    /// <summary>
    /// Value copy of the part. The reference stores parts by value in a <c>std::vector</c>, so
    /// assigning one object to another in its tables copies every part; C# would only copy the
    /// reference, hence this method.
    /// </summary>
    /// <returns>A new part with the same values.</returns>
    public virtual ObjectPartInfo Copy() =>
        new ObjectPartInfo(IcnType, IcnIndex, TileOffset, ObjectType) { AnimationFrames = AnimationFrames };
}

/// <summary>
/// A ground level part with the layer it sits on, the port of <c>Maps::LayeredObjectPartInfo</c>
/// (<c>参考：src/fheroes2/maps/map_object_info.h:70</c>).
/// </summary>
public sealed class LayeredObjectPartInfo : ObjectPartInfo
{
    /// <summary>Creates a ground level part, the constructor of the reference.</summary>
    /// <param name="icn">ICN resource the sprite comes from.</param>
    /// <param name="index">Sprite index inside that resource.</param>
    /// <param name="offset">Tile offset from the main object tile.</param>
    /// <param name="type">Object type of this part.</param>
    /// <param name="layer">Layer the part sits on.</param>
    public LayeredObjectPartInfo(Mp2ObjectIcnType icn, uint index, Point offset, Mp2ObjectType type, ObjectLayerType layer)
        : base(icn, index, offset, type)
    {
        LayerType = layer;
    }

    /// <summary>Layer where this part sits on, <c>layerType</c>.</summary>
    public ObjectLayerType LayerType { get; }

    /// <inheritdoc />
    public override ObjectPartInfo Copy() =>
        new LayeredObjectPartInfo(IcnType, IcnIndex, TileOffset, ObjectType, LayerType) { AnimationFrames = AnimationFrames };
}
