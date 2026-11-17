using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// What a week-life object holds again when the week turns, <c>Maps::updateObjectInfoTile()</c>
/// (<c>maps_tiles_helper.cpp:1641</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference names it a free function in <c>namespace Maps</c> and calls it from <c>World::NewWeek()</c> for every
/// tile whose type answers <c>MP2::isWeekLife()</c>. The port keeps it as a member of <see cref="Tile"/>, as it does
/// for the rest of <c>maps_tiles_helper.cpp</c>.
/// </para>
/// <para>
/// <b>It is not the same set as "week life".</b> The caller filters by <c>isWeekLife</c>, which also covers the stables
/// and the artesian spring — objects whose week is a visit allowance rather than a payload — and this switch knows only
/// the objects that pay. That is why the caller's filter and this switch are two statements, not one.
/// </para>
/// <para>
/// <b>The switch compares action values</b>, because that is what a tile stores (ADR-075): the map file's
/// <c>mapObjectType</c> byte carries the <c>ActionObjectType</c> bit for everything the player can interact with, and
/// the reference's cases are written against the same values. The caller's <c>isWeekLife</c> filter, in contrast, speaks
/// non-action values and strips the bit first.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// Refills what the object on the tile pays this week, <c>Maps::updateObjectInfoTile()</c>
    /// (<c>maps_tiles_helper.cpp:1641</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The magic garden rolls gems against gold, the water wheel pays more from the second week on, the windmill picks a
    /// non-wood resource and the dwellings grow their population. Everything else — the stables, the spring — is left
    /// alone: its metadata is not a resource lane.
    /// </para>
    /// <para>
    /// <b>The water wheel's "first week" pays 500, not 1000,</b> because the reference compares the day counter against
    /// 0: the map settles the objects once at load time, when the counter is still 0, and this function is that
    /// settlement as well as the weekly refill.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to; the day counter decides the water wheel's pay.</param>
    public void UpdateObjectInfoTile(World world)
    {
        switch (GetMainObjectType(world))
        {
            case Mp2ObjectType.ActionMagicGarden:
                // 5 gems or 500 gold.
                if (Rand.Get(1) != 0)
                {
                    SetResourceOnTile(Resource.Gems, 5);
                }
                else
                {
                    SetResourceOnTile(Resource.Gold, 500);
                }

                break;

            case Mp2ObjectType.ActionWaterWheel:
                // First week 500 gold, next weeks 1000.
                SetResourceOnTile(Resource.Gold, world.DayCount == 0 ? 500u : 1000u);

                break;

            case Mp2ObjectType.ActionWindmill:
                int resource = Resource.Wood;

                while (resource == Resource.Wood)
                {
                    resource = Resource.Rand(false);
                }

                // Two pieces of a random resource.
                SetResourceOnTile(resource, 2);

                break;

            case Mp2ObjectType.ActionAirAltar:
            case Mp2ObjectType.ActionArcherHouse:
            case Mp2ObjectType.ActionBarrowMounds:
            case Mp2ObjectType.ActionCave:
            case Mp2ObjectType.ActionCityOfDead:
            case Mp2ObjectType.ActionDesertTent:
            case Mp2ObjectType.ActionDragonCity:
            case Mp2ObjectType.ActionDwarfCottage:
            case Mp2ObjectType.ActionEarthAltar:
            case Mp2ObjectType.ActionExcavation:
            case Mp2ObjectType.ActionFireAltar:
            case Mp2ObjectType.ActionGoblinHut:
            case Mp2ObjectType.ActionHalflingHole:
            case Mp2ObjectType.ActionPeasantHut:
            case Mp2ObjectType.ActionRuins:
            case Mp2ObjectType.ActionTreeCity:
            case Mp2ObjectType.ActionTreeHouse:
            case Mp2ObjectType.ActionTrollBridge:
            case Mp2ObjectType.ActionWagonCamp:
            case Mp2ObjectType.ActionWatchTower:
            case Mp2ObjectType.ActionWaterAltar:
                UpdateDwellingPopulation(world, false);

                break;

            default:
                break;
        }
    }
}
