using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// One map object and the parts it is drawn from, the port of <c>Maps::ObjectInfo</c>
/// (<c>参考：src/fheroes2/maps/map_object_info.h:85</c>).
///
/// <para>
/// The object tables of the reference are built once at start-up and never changed afterwards;
/// <see cref="MapObjectCatalog"/> owns them. The parts are mutable here only because the reference
/// patches <c>animationFrames</c> after the tables are built.
/// </para>
/// </summary>
public sealed class ObjectInfo
{
    /// <summary>Creates an empty object of the given type, the constructor of the reference.</summary>
    /// <param name="type">Type of the whole object, not of a single part.</param>
    public ObjectInfo(Mp2ObjectType type) => ObjectType = type;

    /// <summary>
    /// Ground level parts, <c>groundLevelParts</c>. Must not be empty, and the main part must come
    /// first.
    /// </summary>
    public List<LayeredObjectPartInfo> GroundLevelParts { get; } = [];

    /// <summary>
    /// Top level parts, <c>topLevelParts</c>. May be empty and never holds a part with a Y offset of
    /// zero or more.
    /// </summary>
    public List<ObjectPartInfo> TopLevelParts { get; } = [];

    /// <summary>
    /// Extra information that action objects need, <c>metadata</c>; the meaning depends on the group.
    /// </summary>
    public uint[] Metadata { get; } = new uint[2];

    /// <summary>
    /// Type of the whole object, <c>objectType</c>. Some objects, cracks for instance, have none.
    /// </summary>
    public Mp2ObjectType ObjectType { get; set; }

    /// <summary>Whether the object has no ground level part, <c>empty()</c> of the reference.</summary>
    public bool IsEmpty => GroundLevelParts.Count == 0;

    /// <summary>
    /// Appends a ground level part, the <c>groundLevelParts.emplace_back</c> of the reference. Kept
    /// as a method so the object tables stay one line per reference line.
    /// </summary>
    /// <param name="icn">ICN resource the sprite comes from.</param>
    /// <param name="icnIndex">Sprite index inside that resource.</param>
    /// <param name="offset">Tile offset from the main object tile.</param>
    /// <param name="type">Object type of this part.</param>
    /// <param name="layer">Layer the part sits on.</param>
    public void AddGroundPart(Mp2ObjectIcnType icn, uint icnIndex, Point offset, Mp2ObjectType type, ObjectLayerType layer) =>
        GroundLevelParts.Add(new LayeredObjectPartInfo(icn, icnIndex, offset, type, layer));

    /// <summary>
    /// Appends a top level part, the <c>topLevelParts.emplace_back</c> of the reference. Top level
    /// parts carry no layer, the value is ignored when they are drawn.
    /// </summary>
    /// <param name="icn">ICN resource the sprite comes from.</param>
    /// <param name="icnIndex">Sprite index inside that resource.</param>
    /// <param name="offset">Tile offset from the main object tile.</param>
    /// <param name="type">Object type of this part.</param>
    public void AddTopPart(Mp2ObjectIcnType icn, uint icnIndex, Point offset, Mp2ObjectType type) =>
        TopLevelParts.Add(new ObjectPartInfo(icn, icnIndex, offset, type));

    /// <summary>
    /// Value copy of the object and all its parts. The reference copies object tables with the value
    /// semantics of <c>std::vector</c> (<c>objects[256 + 3] = objects[3]</c> in the road table);
    /// C# assignment would share the instance instead.
    /// </summary>
    /// <returns>A new object with copied parts and metadata.</returns>
    public ObjectInfo Copy()
    {
        ObjectInfo copy = new(ObjectType);
        Metadata.CopyTo(copy.Metadata, 0);

        foreach (LayeredObjectPartInfo part in GroundLevelParts)
        {
            copy.GroundLevelParts.Add((LayeredObjectPartInfo)part.Copy());
        }

        foreach (ObjectPartInfo part in TopLevelParts)
        {
            copy.TopLevelParts.Add(part.Copy());
        }

        return copy;
    }
}
