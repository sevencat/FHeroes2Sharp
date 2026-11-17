using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game;

/// <summary>
/// The character fix of the French maps, <c>World::fixFrenchCharactersInStrings()</c> (<c>world.cpp:1279</c>).
/// </summary>
/// <remarks>
/// <para>
/// The French editor had no accented letters and borrowed ASCII characters for them, so a French map stores
/// <c>î</c> as a horizontal tab and <c>ô</c> as a number sign (<see cref="MapTextCodec.FixFrenchCharacters"/> has the
/// whole table). This walks the strings of the world — hero names, town names, the rumors the map ships, the daily
/// events, the signs, bottles, ground events and sphinxes — and restores them.
/// </para>
/// <para>
/// The reference calls it from the MP2 loader only, and only while the interface runs in French
/// (<c>world_loadmap.cpp</c>); the port has no language layer yet, so the method is public and the caller owns the
/// gate. Running it on an English or Chinese map would turn the punctuation of its messages into accents.
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// Restores the characters a French map borrowed, <c>World::fixFrenchCharactersInStrings()</c>
    /// (<c>world.cpp:1279-1325</c>).
    /// </summary>
    /// <remarks>
    /// The heroes and the towns come first, then the rumors and the daily events, then the tile objects. The
    /// reference switches on the main object type of the tile and looks the object up; the port does the same, with
    /// the type stripped of the action bit because the tiles keep the value the file had (ADR-075).
    /// </remarks>
    public void FixFrenchCharactersInStrings()
    {
        foreach (Hero hero in _heroes)
        {
            hero.FixFrenchCharactersInName();
        }

        foreach (CastleTown castle in _castles)
        {
            castle.FixFrenchCharactersInName();
        }

        for (int i = 0; i < _rumors.Count; ++i)
        {
            _rumors[i] = MapTextCodec.FixFrenchCharacters(_rumors[i]);
        }

        foreach (EventDate eventDate in _dailyEvents)
        {
            eventDate.FixFrenchCharacters();
        }

        foreach (Tile tile in _tiles)
        {
            switch (Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(this)))
            {
                case Mp2ObjectType.Sign:
                case Mp2ObjectType.Bottle:
                    _mapObjects.Get<MapSign>(tile.Index)?.FixFrenchCharacters();
                    break;
                case Mp2ObjectType.Event:
                    _mapObjects.Get<MapEvent>(tile.Index)?.FixFrenchCharacters();
                    break;
                case Mp2ObjectType.Sphinx:
                    _mapObjects.Get<MapSphinx>(tile.Index)?.FixFrenchCharacters();
                    break;
                default:
                    break;
            }
        }
    }
}
