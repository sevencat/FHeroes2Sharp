using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// One piece of an object placed on a map tile, the port of <c>Maps::ObjectPart</c>
/// (<c>参考：src/fheroes2/maps/maps_tiles.h:58</c>).
/// </summary>
/// <remarks>
/// <para>
/// A tile keeps its objects as three containers: the main part, the ground level parts and the top
/// level parts. All pieces of one multi tile object share <see cref="Uid"/>. The sprite comes from
/// <see cref="IcnType"/> at <see cref="IcnIndex"/>.
/// </para>
/// <para>
/// The port is a mutable struct because the reference mutates parts in place
/// (<c>replaceObject</c>, <c>updateObjectImageIndex</c>); <see cref="List{T}"/> hands out a reference
/// to the element, so those calls stay one line. The parameterless constructor exists because the
/// reference default initialises a part to <c>OBJ_ICN_TYPE_UNKNOWN</c> with image index <c>255</c>,
/// and <c>default( ObjectPart )</c> would give a zero index instead.
/// </para>
/// </remarks>
public partial struct ObjectPart : IEquatable<ObjectPart>
{
    /// <summary>Image index that means "the part carries no sprite", <c>icnIndex{ 255 }</c> of the reference.</summary>
    public const byte EmptyIcnIndex = 255;

    /// <summary>A part that belongs to no object: no UID, no ICN, image index 255.</summary>
    public ObjectPart()
    {
        Uid = 0;
        LayerType = ObjectLayerType.Object;
        IcnType = Mp2ObjectIcnType.Unknown;
        IcnIndex = EmptyIcnIndex;
    }

    /// <summary>Creates a part, the four argument constructor of the reference.</summary>
    /// <param name="layerType">Layer the part is rendered on and the one passability is derived from.</param>
    /// <param name="uid">Identifier of the whole object; shared by all its pieces.</param>
    /// <param name="icnType">ICN tile set the sprite comes from.</param>
    /// <param name="icnIndex">Sprite index inside that ICN.</param>
    public ObjectPart(ObjectLayerType layerType, uint uid, Mp2ObjectIcnType icnType, byte icnIndex)
    {
        Uid = uid;
        LayerType = layerType;
        IcnType = icnType;
        IcnIndex = icnIndex;
    }

    /// <summary>
    /// Identifier of the object, <c>_uid</c>. All parts of an object bigger than one tile share it.
    /// </summary>
    public uint Uid { get; set; }

    /// <summary>Layer the part is drawn on, <c>layerType</c>.</summary>
    public ObjectLayerType LayerType { get; set; }

    /// <summary>ICN tile set of the sprite, <c>icnType</c>.</summary>
    public Mp2ObjectIcnType IcnType { get; set; }

    /// <summary>Sprite index inside <see cref="IcnType"/>, <c>icnIndex</c>.</summary>
    public byte IcnIndex { get; set; }

    /// <summary>
    /// Whether a hero (also on a boat) can walk over the part, <c>isPassabilityTransparent()</c>: the
    /// layer is <see cref="ObjectLayerType.Shadow"/> or <see cref="ObjectLayerType.Terrain"/>.
    /// </summary>
    public readonly bool IsPassabilityTransparent =>
        LayerType is ObjectLayerType.Shadow or ObjectLayerType.Terrain;

    /// <inheritdoc/>
    public readonly bool Equals(ObjectPart other) =>
        Uid == other.Uid && LayerType == other.LayerType && IcnType == other.IcnType && IcnIndex == other.IcnIndex;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is ObjectPart other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(Uid, (byte)LayerType, (byte)IcnType, IcnIndex);

    /// <summary>Value equality, the <c>operator==</c> of the reference.</summary>
    public static bool operator ==(ObjectPart left, ObjectPart right) => left.Equals(right);

    /// <summary>Value inequality, the <c>operator!=</c> of the reference.</summary>
    public static bool operator !=(ObjectPart left, ObjectPart right) => !left.Equals(right);

    /// <inheritdoc/>
    public override readonly string ToString() =>
        $"uid {Uid} | {LayerType} | {IcnType}:{IcnIndex}";
}
