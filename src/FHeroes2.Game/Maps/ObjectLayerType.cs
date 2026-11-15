namespace FHeroes2.Game.Maps;

/// <summary>
/// The layer an object part sits on, the port of <c>Maps::ObjectLayerType</c> (<c>maps_tiles.h</c>).
///
/// <para>
/// The layer decides both passability and the order in which objects are drawn: <see cref="Terrain"/>
/// first, then <see cref="Shadow"/>, <see cref="Background"/> and <see cref="Object"/> on top. The
/// numeric values are used by the object tables and must not be renumbered.
/// </para>
/// </summary>
public enum ObjectLayerType : byte
{
    /// <summary>
    /// <c>OBJECT_LAYER</c>: common objects such as mines, forests, mountains and castles. They affect
    /// passability.
    /// </summary>
    Object = 0,

    /// <summary>
    /// <c>BACKGROUND_LAYER</c>: objects that still affect passability but are drawn as a background,
    /// such as lakes and bushes.
    /// </summary>
    Background = 1,

    /// <summary>
    /// <c>SHADOW_LAYER</c>: shadows and special parts like the entrance road of a castle. No
    /// passability change.
    /// </summary>
    Shadow = 2,

    /// <summary>
    /// <c>TERRAIN_LAYER</c>: roads, water flows and cracks. Everything that is part of the terrain;
    /// no passability change.
    /// </summary>
    Terrain = 3,
}
