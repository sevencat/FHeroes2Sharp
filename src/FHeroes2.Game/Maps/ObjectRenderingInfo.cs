using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// One sprite a map layer has to draw, <c>fheroes2::ObjectRenderingInfo</c>
/// (<c>参考：src/fheroes2/gui/ui_object_rendering.h:27-67</c>).
/// </summary>
/// <remarks>
/// <para>
/// The struct is what the tile renderer hands back for the pieces that do not fit into one tile
/// (<c>maps_tiles_render.cpp:1184-1321</c>): the offset from the object's own tile, the offset the sprite
/// carries, the part of the sprite to blit, which frame it is and how transparent it has to be drawn. The
/// reference keeps it trivially copyable on purpose — the renderer copies entries into <c>std::deque</c>
/// buckets and rewrites their alpha afterwards — so the port keeps it a mutable struct of value types with
/// settable members and no reference the frame may destroy.
/// </para>
/// <para>
/// <b>Deviation: <c>icnId</c> is <see cref="IcnType"/>, not <c>int</c>.</b> The reference stores a bare
/// <c>int</c> whose default <c>-1</c> means "no sprite"; the port has <see cref="IcnType"/> for exactly those
/// ids (<c>agg/icn.h</c>), so the field is typed and the "no sprite" value is <see cref="IcnType.Unknown"/>,
/// which is the one member <c>IcnCatalog</c> gives no file name. Object parts reach it through
/// <c>MP2::getIcnIdFromObjectIcnType</c> (<c>TileObjectTraits</c> documents that this is a one to one
/// mapping); hero, monster and boat frames are their own ids on the same enum.
/// </para>
/// </remarks>
public struct ObjectRenderingInfo : IEquatable<ObjectRenderingInfo>
{
    /// <summary>Creates an entry, the seven parameter constructor of the reference.</summary>
    /// <param name="tileOffset">Offset from the object's tile, <c>tileOffset</c>.</param>
    /// <param name="imageOffset">Offset the sprite itself carries, <c>imageOffset</c>.</param>
    /// <param name="area">Part of the sprite to blit, <c>area</c>.</param>
    /// <param name="icnId">ICN the frame comes from, <c>icnId</c>.</param>
    /// <param name="icnIndex">Frame index inside that ICN, <c>icnIndex</c>.</param>
    /// <param name="isFlipped">Whether the sprite is mirrored horizontally, <c>isFlipped</c>.</param>
    /// <param name="alphaValue">Opacity the entry is drawn with, <c>alphaValue</c>.</param>
    public ObjectRenderingInfo(Point tileOffset, Point imageOffset, Rect area, IcnType icnId, int icnIndex, bool isFlipped, byte alphaValue)
    {
        TileOffset = tileOffset;
        ImageOffset = imageOffset;
        Area = area;
        IcnId = icnId;
        IcnIndex = icnIndex;
        IsFlipped = isFlipped;
        AlphaValue = alphaValue;
    }

    /// <summary>Offset of the sprite from the tile the object stands on, <c>tileOffset</c>.</summary>
    public Point TileOffset { get; set; }

    /// <summary>Offset the sprite carries itself, <c>imageOffset</c>.</summary>
    public Point ImageOffset { get; set; }

    /// <summary>Part of the sprite to blit, <c>area</c>.</summary>
    public Rect Area { get; set; }

    /// <summary>
    /// ICN the frame comes from, <c>icnId</c>; <see cref="IcnType.Unknown"/> means the entry carries no sprite.
    /// </summary>
    public IcnType IcnId { get; set; }

    /// <summary>Frame index inside <see cref="IcnId"/>, <c>icnIndex</c>.</summary>
    public int IcnIndex { get; set; }

    /// <summary>Whether the sprite is mirrored horizontally, <c>isFlipped</c>.</summary>
    public bool IsFlipped { get; set; }

    /// <summary>Opacity the entry is drawn with, <c>alphaValue</c>; <c>255</c> is fully visible.</summary>
    public byte AlphaValue { get; set; }

    /// <inheritdoc/>
    public readonly bool Equals(ObjectRenderingInfo other) =>
        TileOffset == other.TileOffset && ImageOffset == other.ImageOffset && Area == other.Area
        && IcnId == other.IcnId && IcnIndex == other.IcnIndex && IsFlipped == other.IsFlipped
        && AlphaValue == other.AlphaValue;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is ObjectRenderingInfo other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() =>
        HashCode.Combine(TileOffset, ImageOffset, Area, (int)IcnId, IcnIndex, IsFlipped, AlphaValue);

    /// <summary>Value equality.</summary>
    public static bool operator ==(ObjectRenderingInfo left, ObjectRenderingInfo right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(ObjectRenderingInfo left, ObjectRenderingInfo right) => !left.Equals(right);

    /// <inheritdoc/>
    public override readonly string ToString() =>
        $"{IcnId}:{IcnIndex} at {TileOffset} + {ImageOffset}, {Area}, flip {IsFlipped}, alpha {AlphaValue}";
}
