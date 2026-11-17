using System.Diagnostics;

using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The shipyard: the three questions before a boat can be launched, the launch itself, and the ICN of a race's
/// boat — <c>castle.cpp:1830-1929</c>, <c>castle.cpp:2125-2165</c> and <c>castle.cpp:1428-1449</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference reaches the map through the <c>world</c> global and the town's centre through its own
/// <c>center</c> member; the port takes the world as an argument and derives the centre from
/// <see cref="TileIndex"/>, which is the same point in the reference (<c>GetIndex()</c> is the index of
/// <c>center</c>).
/// </para>
/// <para>
/// All three scans walk the same three tiles: the one two rows below the town, then its left and right
/// neighbours. The row is the harbour the town screen draws; the middle tile is tried first, so a boat appears
/// as close to the keep as the water allows.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// How far below the town's centre the harbour row lies, <c>center.y + 2</c> in all three scans.
    /// </summary>
    private const int HarbourRowOffset = 2;

    /// <summary>
    /// Whether a boat or a hero already waits in the harbour, <c>Castle::HasBoatNearby()</c>
    /// (<c>castle.cpp:1861-1894</c>).
    /// </summary>
    /// <remarks>
    /// A hero counts because a hero standing on water is in a boat: the reference treats both as "occupied" so
    /// that the shipyard never launches a second one on top of the first.
    /// </remarks>
    /// <param name="world">The world whose tiles are scanned.</param>
    /// <returns>Whether one of the three harbour tiles holds a boat or a hero.</returns>
    public bool HasBoatNearby(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (int index in GetHarbourTiles(world))
        {
            Tile tile = world.GetTile(index);

            if (!tile.IsWater)
            {
                continue;
            }

            // OBJ_BOAT and OBJ_HERO are the action values (mp2.h:315,327); a tile stores those, never the
            // mp2-file values Boat (43) and Hero (55).
            if (tile.MainObjectType is Mp2ObjectType.ActionBoat or Mp2ObjectType.ActionHero)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The tile a newly bought boat would be placed on, <c>Castle::getTileIndexToPlaceBoat()</c>
    /// (<c>castle.cpp:1896-1929</c>).
    /// </summary>
    /// <remarks>
    /// A tile qualifies when it is water with nothing drawn on it, or with nothing that would stop a boat: the
    /// reference asks for <c>OBJ_ICN_TYPE_UNKNOWN</c> (a shadow or a piece of a larger object) or for a
    /// passability-transparent tile.
    /// </remarks>
    /// <param name="world">The world whose tiles are scanned.</param>
    /// <returns>The first qualifying harbour tile, or -1 when there is none.</returns>
    public int GetTileIndexToPlaceBoat(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (int index in GetHarbourTiles(world))
        {
            Tile tile = world.GetTile(index);

            if (!tile.IsWater)
            {
                continue;
            }

            if (tile.MainObjectPart.IcnType == Mp2ObjectIcnType.Unknown || tile.IsPassabilityTransparent)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether the town may launch a boat, <c>Castle::AllowBuyBoat()</c> (<c>castle.cpp:2125-2140</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three conditions: a shipyard, the money, and somewhere to put the boat. The AI asks with
    /// <paramref name="checkPayment"/> off because it has already decided how to raise the funds
    /// (<c>ai_planner_castle.cpp:202</c>); the town screen asks with it on.
    /// </para>
    /// <para>
    /// The reference reads the owner's treasury through <c>GetKingdom()</c>. The port takes the kingdom as an
    /// argument, and treats a missing one as "cannot pay" rather than assuming it can.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the harbour is scanned in.</param>
    /// <param name="kingdom">The owner's kingdom; required when <paramref name="checkPayment"/> is set.</param>
    /// <param name="checkPayment">Whether the price is part of the answer.</param>
    /// <returns>Whether the boat may be bought.</returns>
    public bool AllowBuyBoat(World world, Kingdom.Kingdom? kingdom = null, bool checkPayment = true)
    {
        if (!IsBuild(BuildingType.Shipyard))
        {
            return false;
        }

        if (checkPayment && (kingdom is null || !kingdom.AllowPayment(PaymentConditions.BuyBoat())))
        {
            return false;
        }

        if (HasBoatNearby(world))
        {
            return false;
        }

        return GetTileIndexToPlaceBoat(world) >= 0;
    }

    /// <summary>
    /// Buys a boat and puts it in the harbour, <c>Castle::BuyBoat()</c> (<c>castle.cpp:2142-2165</c>).
    /// </summary>
    /// <remarks>
    /// The reference plays the town's build sound for a human owner; sound is UI and is not ported. The price is
    /// taken before the tile is written, so a town that passes <see cref="AllowBuyBoat"/> always pays exactly once.
    /// </remarks>
    /// <param name="world">The world whose harbour receives the boat.</param>
    /// <param name="kingdom">The owner's kingdom, which pays.</param>
    /// <returns>Whether a boat was bought and placed.</returns>
    public bool BuyBoat(World world, Kingdom.Kingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(kingdom);

        if (!AllowBuyBoat(world, kingdom))
        {
            return false;
        }

        Debug.Assert(HasSeaAccess, "The reference asserts sea access as well: a shipyard without water is a bug.");

        int index = GetTileIndexToPlaceBoat(world);

        if (index < 0)
        {
            return false;
        }

        kingdom.OddFundsResource(PaymentConditions.BuyBoat());
        world.GetTile(index).SetBoat(world, Direction.Right, kingdom.Color);

        return true;
    }

    /// <summary>
    /// The ICN a race's boat is drawn from, <c>Castle::GetICNBoat()</c> (<c>castle.cpp:1428-1449</c>).
    /// </summary>
    /// <remarks>
    /// The reference returns <c>ICN::UNKNOWN</c> for a race without a boat and logs a warning. The port returns
    /// <see cref="IcnType.Unknown"/> in silence — the six playable races all have one, and a neutral town cannot
    /// own a shipyard.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <returns>The boat's ICN.</returns>
    public static IcnType GetICNBoat(MapRace race) => race switch
    {
        MapRace.Barbarian => IcnType.Twnbboat,
        MapRace.Knight => IcnType.Twnkboat,
        MapRace.Necromancer => IcnType.Twnnboat,
        MapRace.Sorceress => IcnType.Twnsboat,
        MapRace.Warlock => IcnType.Twnwboat,
        MapRace.Wizard => IcnType.Twnzboat,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The three tiles the harbour scans look at, in the reference's order: the one below the town, then its
    /// left and right neighbours.
    /// </summary>
    /// <remarks>
    /// Empty when the tile below the town is off the map — the reference's early return, which also skips the
    /// neighbours because they would be off the map too.
    /// </remarks>
    /// <param name="world">The world, for its geometry.</param>
    /// <returns>The tile indices, at most three.</returns>
    private List<int> GetHarbourTiles(World world)
    {
        MapGeometry geometry = world.Geometry!.Value;
        Point center = geometry.GetPoint(TileIndex);
        int y = center.Y + HarbourRowOffset;

        if (!geometry.IsValidAbsPoint(center.X, y))
        {
            return [];
        }

        List<int> tiles = [geometry.GetIndexFromAbsPoint(center.X, y)];

        if (geometry.IsValidAbsPoint(center.X - 1, y))
        {
            tiles.Add(geometry.GetIndexFromAbsPoint(center.X - 1, y));
        }

        if (geometry.IsValidAbsPoint(center.X + 1, y))
        {
            tiles.Add(geometry.GetIndexFromAbsPoint(center.X + 1, y));
        }

        return tiles;
    }
}
