using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The object-aware passability checks of a tile, <c>src/fheroes2/maps/maps_tiles.cpp:1007-1147</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Tile.PassableDirections"/> answers "can a walker step in this direction", which is what the
/// map loader and the editor need. The functions here answer the harder question the pathfinder asks:
/// can a <b>particular hero</b> step there, knowing whether he comes from the water and whether he may
/// look at tiles under the fog. The two do not compose: a shore tile is passable in the bit mask and is
/// only enterable from the water by a hero who is disembarking.
/// </para>
/// <para>
/// Two reference branches are missing because the systems they read are not ported: the allied castles
/// (<c>_mainObjectType == MP2::OBJ_CASTLE</c> combined with <c>world.getCastleEntrance()</c> and
/// <c>Castle::isFriends()</c>) and, inside them, the notion of an alliance behind
/// <c>Hero::isFriends()</c>. Both are marked at the exact spot where the reference tests them, so that
/// adding castles cannot miss them.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// Whether a hero from a boat may step onto the tile, <c>isSuitableForDisembarkation()</c>
    /// (<c>maps_tiles.cpp:1028</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A coast tile always qualifies: the coast is the sprite that draws the beach and the game trusts it
    /// instead of looking at the objects. Heroes and events do not qualify because their parts are either
    /// missing or temporary, so the checks below would answer the wrong thing. Water never qualifies —
    /// that is where the boat already is.
    /// </para>
    /// <para>
    /// For the rest, the test is per object part: roads, streams and flags are completely passable, and so
    /// are shadows and terrain layers. A stone in the middle of a beach tile blocks the landing, which is
    /// why the check cannot be reduced to the bit mask.
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> when a hero may land here.</returns>
    public bool IsSuitableForDisembarkation()
    {
        if (_mainObjectType == Mp2ObjectType.ActionCoast)
        {
            return true;
        }

        if (_mainObjectType == Mp2ObjectType.ActionHero)
        {
            return false;
        }

        if (_mainObjectType == Mp2ObjectType.ActionEvent)
        {
            return false;
        }

        if (IsWater)
        {
            return false;
        }

        return (_mainObjectPart.IcnType == Mp2ObjectIcnType.Unknown || IsObjectPartPassableForLanding(_mainObjectPart))
               && _groundObjectParts.All(IsObjectPartPassableForLanding);
    }

    /// <summary>
    /// Whether a boat may be summoned onto the tile, <c>isSuitableForSummoningBoat()</c>
    /// (<c>maps_tiles.cpp:1064</c>).
    /// </summary>
    /// <remarks>
    /// The mirror image of <see cref="IsSuitableForDisembarkation"/>: the tile has to be water, and the
    /// only part that may stand on it is the flag of a lighthouse — the half of that flag which spills
    /// onto the neighbouring tile must not stop a boat. Heroes and events are excluded for the same
    /// reason as above.
    /// </remarks>
    /// <returns><see langword="true"/> when a boat may be summoned here.</returns>
    public bool IsSuitableForSummoningBoat()
    {
        if (_mainObjectType == Mp2ObjectType.ActionHero)
        {
            return false;
        }

        if (_mainObjectType == Mp2ObjectType.ActionEvent)
        {
            return false;
        }

        if (!IsWater)
        {
            return false;
        }

        static bool IsObjectPartPassable(ObjectPart part) =>
            part.IcnType == Mp2ObjectIcnType.Flag32 || part.IsPassabilityTransparent || TileObjectTraits.IsObjectPartShadow(part);

        return (_mainObjectPart.IcnType == Mp2ObjectIcnType.Unknown || IsObjectPartPassable(_mainObjectPart))
               && _groundObjectParts.All(IsObjectPartPassable);
    }

    /// <summary>
    /// Whether the ultimate artifact may be buried on the tile, <c>isSuitableForUltimateArtifact()</c>
    /// (<c>maps_tiles.cpp:1007</c>).
    /// </summary>
    /// <remarks>
    /// Only a plain dry tile qualifies: the main part must be absent or a shadow, and every ground part
    /// must be a shadow. The passability check uses <see cref="Direction.Center"/> with the fog ignored,
    /// i.e. "is the tile passable at all" rather than "in which directions can a hero leave it".
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns><see langword="true"/> when the artifact may be hidden here.</returns>
    public bool IsSuitableForUltimateArtifact(World world)
    {
        if (IsWater || !IsPassableFrom(world, Direction.Center, false, true, PlayerColor.None))
        {
            return false;
        }

        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown && !TileObjectTraits.IsObjectPartShadow(_mainObjectPart))
        {
            return false;
        }

        if (_groundObjectParts.Any(part => !TileObjectTraits.IsObjectPartShadow(part)))
        {
            return false;
        }

        if (_topObjectParts.Any(part => !TileObjectTraits.IsObjectPartShadow(part)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="heroColor"/> may enter the tile from <paramref name="direction"/>,
    /// <c>isPassableFrom( direction, fromWater, ignoreFog, heroColor )</c>
    /// (<c>maps_tiles.cpp:1102</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four questions in order: is the tile visible to the hero, can he arrive from the side he comes
    /// from, is the tile free of a hero he cannot share it with, and finally does the tile's own
    /// passability mask allow the step. The mask test is last on purpose — the checks above are about
    /// <b>objects</b> that the mask cannot express (a boat blocks water, a shore only opens from the sea).
    /// </para>
    /// <para>
    /// <paramref name="fromWater"/> is the whole boat mechanic: a hero on a boat may step onto water only
    /// when no boat is there, and onto land only when the tile is suitable for disembarkation; a hero on
    /// foot may step onto water only onto a shipwreck, a boat or another hero (the last two are how one
    /// boards a boat and how a hero standing on the shore sees a hero on the sea).
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to, needed to find the hero standing on it.</param>
    /// <param name="direction">Direction the hero enters from, <see cref="Direction.Center"/> for "is it passable at all".</param>
    /// <param name="fromWater">Whether the hero arrives on a boat.</param>
    /// <param name="ignoreFog">Whether a tile under the fog still counts as known.</param>
    /// <param name="heroColor">Color of the hero that asks; <see cref="PlayerColor.None"/> for a neutral ask.</param>
    /// <returns><see langword="true"/> when the step is allowed.</returns>
    public bool IsPassableFrom(World world, Direction direction, bool fromWater, bool ignoreFog, PlayerColor heroColor)
    {
        if (!ignoreFog && IsFog(heroColor))
        {
            return false;
        }

        if (fromWater)
        {
            if (IsWater)
            {
                if (_mainObjectType == Mp2ObjectType.ActionBoat)
                {
                    return false;
                }
            }
            else if (!IsSuitableForDisembarkation())
            {
                return false;
            }
        }
        else if (IsWater && _mainObjectType != Mp2ObjectType.ActionShipwreck && _mainObjectType != Mp2ObjectType.ActionHero
                 && _mainObjectType != Mp2ObjectType.ActionBoat)
        {
            return false;
        }

        // Tiles on which allied heroes are located are inaccessible.
        if (_mainObjectType == Mp2ObjectType.ActionHero)
        {
            Hero? hero = GetHero(world);
            Debug.Assert(hero != null, "A tile marked as a hero tile has no hero.");

            if (hero != null && hero.Color != heroColor && hero.IsFriends(heroColor))
            {
                return false;
            }
        }

        // Tiles on which the entrances to the allied castles are located are inaccessible. The port has
        // no Castle type yet, so there is nothing to compare here; the reference asks
        // world.getCastleEntrance( GetCenter() ) and compares castle->GetColor() with heroColor.

        return (direction & PassableDirections) != 0;
    }

    /// <summary>
    /// Whether an object part is transparent enough to land on, the lambda of
    /// <c>isSuitableForDisembarkation</c>.
    /// </summary>
    /// <param name="part">Part to test.</param>
    /// <returns><see langword="true"/> when the part does not block a landing.</returns>
    private static bool IsObjectPartPassableForLanding(ObjectPart part) =>
        part.IcnType is Mp2ObjectIcnType.Road or Mp2ObjectIcnType.Stream or Mp2ObjectIcnType.Flag32
        || part.IsPassabilityTransparent
        || TileObjectTraits.IsObjectPartShadow(part);
}
