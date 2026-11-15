using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The object metadata lane readers and writers of a tile, <c>src/fheroes2/maps/maps_tiles_helper.cpp</c>
/// (<c>:388-424</c>, <c>:570-599</c>, <c>:719-761</c>, <c>:774-854</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps these free functions in the <c>Maps</c> namespace because a <c>Tile</c> is not
/// allowed to know about <c>Funds</c> and <c>Artifact</c>. The port keeps them as members of
/// <see cref="Tile"/> — the same choice it made for <see cref="Tile.GetDailyIncomeObjectResources"/> —
/// because the lane layout is tile data and the alternative would be an otherwise empty static class
/// whose first parameter is always a tile.
/// </para>
/// <para>
/// What is <b>not</b> here: anything that needs <c>Spell</c> or <c>Artifact</c> as a class
/// (<c>getSpellFromTile</c>, <c>setSpellOnTile</c>, <c>getArtifactFromTile</c> of a spell scroll's spell
/// id, <c>getMineSpellIdFromTile</c>) and the troop helpers, which need <c>Troop</c> and
/// <c>Monster</c>. The ids are read and written as integers instead, which is what the lanes hold.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// Stores the resource and its amount, <c>Maps::setResourceOnTile()</c> (<c>maps_tiles_helper.cpp:719</c>).
    /// </summary>
    /// <remarks>
    /// The reference writes the two lanes unconditionally; it is the caller's job to pass a tile that
    /// actually carries a resource.
    /// </remarks>
    /// <param name="resourceType">One of the <see cref="Resource"/> flags.</param>
    /// <param name="value">Amount of that resource.</param>
    public void SetResourceOnTile(int resourceType, uint value)
    {
        _metadata[0] = (uint)resourceType;
        _metadata[1] = value;
    }

    /// <summary>
    /// What picking up the tile would give, <c>Maps::getFundsFromTile()</c>
    /// (<c>maps_tiles_helper.cpp:725</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The objects differ in what they store: a barrel and a campfire hold <b>one</b> non-gold resource
    /// and pay a hundred gold per unit of it, flotsam holds wood in lane 0 and gold in lane 1, a chest
    /// keeps its gold in lane 1 (lane 0 is the artifact it may also contain), and a wagon keeps its
    /// artifact in lane 0, a <b>resource type</b> in lane 1 and the amount of that resource in lane 2.
    /// </para>
    /// <para>
    /// The resource of a barrel can be gold itself, and <c>Funds( type, count )</c> adds nothing for an
    /// amount of zero, so an empty barrel answers an empty bag rather than a bag of gold zero.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to, needed to see through a hero on the tile.</param>
    /// <returns>The daily-income style bag; empty for objects that hold nothing.</returns>
    public Funds GetFundsFromTile(World world)
    {
        switch (GetMainObjectType(world, false))
        {
            case Mp2ObjectType.Barrel:
            case Mp2ObjectType.Campfire:
                // N of a non-gold resource and N * 100 gold.
                return new Funds((int)_metadata[0], _metadata[1]) + new Funds(Resource.Gold, _metadata[1] * 100);

            case Mp2ObjectType.Flotsam:
                return new Funds(Resource.Wood, _metadata[0]) + new Funds(Resource.Gold, _metadata[1]);

            case Mp2ObjectType.DaemonCave:
            case Mp2ObjectType.Graveyard:
            case Mp2ObjectType.SeaChest:
            case Mp2ObjectType.Shipwreck:
            case Mp2ObjectType.TreasureChest:
                return new Funds(Resource.Gold, _metadata[1]);

            case Mp2ObjectType.DerelictShip:
            case Mp2ObjectType.LeanTo:
            case Mp2ObjectType.MagicGarden:
            case Mp2ObjectType.Resource:
            case Mp2ObjectType.Windmill:
            case Mp2ObjectType.WaterWheel:
                return new Funds((int)_metadata[0], _metadata[1]);

            case Mp2ObjectType.Wagon:
                return new Funds((int)_metadata[1], _metadata[2]);

            default:
                Debug.Assert(false, "The tile does not hold an object that can be picked up.");
                return default;
        }
    }

    /// <summary>
    /// The artifact the tile keeps, <c>Maps::getArtifactFromTile()</c> (<c>maps_tiles_helper.cpp:570</c>).
    /// </summary>
    /// <remarks>
    /// Nine objects keep an artifact id in lane 0. An artifact object with the "contains spell" condition
    /// is a spell scroll whose spell sits in lane 1, so the answer is
    /// <see cref="ArtifactType.SpellScroll"/> and the caller reads the spell itself — the port has no
    /// <c>Artifact</c> class to carry both, and returning the scroll without its spell is the least
    /// surprising half of the pair.
    /// </remarks>
    /// <param name="world">The world the tile belongs to, needed to see through a hero on the tile.</param>
    /// <returns>The artifact, <see cref="ArtifactType.Unknown"/> when the tile holds none.</returns>
    public ArtifactType GetArtifactFromTile(World world)
    {
        switch (GetMainObjectType(world, false))
        {
            case Mp2ObjectType.DaemonCave:
            case Mp2ObjectType.Graveyard:
            case Mp2ObjectType.SeaChest:
            case Mp2ObjectType.Shipwreck:
            case Mp2ObjectType.ShipwreckSurvivor:
            case Mp2ObjectType.Skeleton:
            case Mp2ObjectType.TreasureChest:
            case Mp2ObjectType.Wagon:
                return (ArtifactType)_metadata[0];

            case Mp2ObjectType.Artifact:
                return _metadata[2] == (uint)ArtifactCaptureCondition.ContainsSpell
                    ? ArtifactType.SpellScroll
                    : (ArtifactType)_metadata[0];

            default:
                Debug.Assert(false, "The tile does not hold an object that keeps an artifact.");
                return ArtifactType.Unknown;
        }
    }

    /// <summary>
    /// The color that opens a barrier or a traveller tent, <c>Maps::getBarrierColorFromTile()</c>
    /// (<c>maps_tiles_helper.cpp:774</c>).
    /// </summary>
    /// <remarks>
    /// The color is stored as a <c>PlayerColor</c> index in lane 0, not as the bit flag, so the answer is
    /// a plain number: <c>0</c> blue … <c>5</c> purple, and <c>6</c> for an "unknown" color that opens
    /// nothing.
    /// </remarks>
    /// <param name="world">The world the tile belongs to, needed to see through a hero on the tile.</param>
    /// <returns>The color index of the object.</returns>
    public int GetBarrierColorFromTile(World world)
    {
        switch (GetMainObjectType(world, false))
        {
            case Mp2ObjectType.Barrier:
            case Mp2ObjectType.TravellerTent:
                return (int)_metadata[0];

            default:
                Debug.Assert(false, "The tile holds neither a barrier nor a traveller tent.");
                return 0;
        }
    }

    /// <summary>
    /// Sets the color of a barrier or a traveller tent, <c>Maps::setBarrierColorOnTile()</c>
    /// (<c>maps_tiles_helper.cpp:793</c>).
    /// </summary>
    /// <param name="world">The world the tile belongs to, needed to see through a hero on the tile.</param>
    /// <param name="barrierColor">Color index to store.</param>
    public void SetBarrierColorOnTile(World world, int barrierColor)
    {
        switch (GetMainObjectType(world, false))
        {
            case Mp2ObjectType.Barrier:
            case Mp2ObjectType.TravellerTent:
                _metadata[0] = (uint)barrierColor;
                break;

            default:
                Debug.Assert(false, "The tile holds neither a barrier nor a traveller tent.");
                break;
        }
    }

    /// <summary>
    /// Whether the tile still holds something worth visiting, <c>Maps::doesTileContainValuableItems()</c>
    /// (<c>maps_tiles_helper.cpp:808</c>).
    /// </summary>
    /// <remarks>
    /// The AI uses this to decide whether a tile is worth a detour, so it has to answer "no" for an
    /// object that has already been emptied: a checked chest keeps zero in the lane the gold comes from,
    /// a visited windmill keeps zero in lane 1, and a wagon keeps neither artifact nor gold.
    /// </remarks>
    /// <param name="world">The world the tile belongs to, needed to see through a hero on the tile.</param>
    /// <returns><see langword="true"/> when the object still has something to give.</returns>
    public bool DoesTileContainValuableItems(World world)
    {
        switch (GetMainObjectType(world, false))
        {
            case Mp2ObjectType.Artifact:
            case Mp2ObjectType.Barrel:
            case Mp2ObjectType.Campfire:
            case Mp2ObjectType.Flotsam:
            case Mp2ObjectType.Resource:
            case Mp2ObjectType.SeaChest:
            case Mp2ObjectType.ShipwreckSurvivor:
            case Mp2ObjectType.TreasureChest:
                return true;

            case Mp2ObjectType.Pyramid:
                // The reference asks getSpellFromTile().isValid(), i.e. the spell id is not NONE.
                return _metadata[0] != 0;

            case Mp2ObjectType.DerelictShip:
            case Mp2ObjectType.Graveyard:
            case Mp2ObjectType.LeanTo:
            case Mp2ObjectType.MagicGarden:
            case Mp2ObjectType.Shipwreck:
            case Mp2ObjectType.WaterWheel:
            case Mp2ObjectType.Windmill:
                return _metadata[1] > 0;

            case Mp2ObjectType.Skeleton:
                return GetArtifactFromTile(world) != ArtifactType.Unknown;

            case Mp2ObjectType.Wagon:
                return GetArtifactFromTile(world) != ArtifactType.Unknown || _metadata[2] != 0;

            case Mp2ObjectType.DaemonCave:
                return _metadata[2] != 0;

            default:
                return false;
        }
    }

    /// <summary>
    /// Zeroes the object metadata, <c>Maps::resetObjectMetadata()</c> (<c>maps_tiles_helper.cpp:849</c>).
    /// </summary>
    /// <remarks>
    /// Every enumeration that lands in the metadata starts at 0 meaning "empty", which is what makes a
    /// plain zeroing enough to mark an object as visited.
    /// </remarks>
    public void ResetObjectMetadata()
    {
        _metadata[0] = 0;
        _metadata[1] = 0;
        _metadata[2] = 0;
    }
}
