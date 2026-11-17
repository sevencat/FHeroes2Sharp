using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game;

/// <summary>
/// The week and the month of the world, <c>World::NewWeek()</c> (<c>world.cpp:462</c>) and
/// <c>World::NewMonth()</c> (<c>world.cpp:493</c>) with its <c>_monthOfMonstersAction()</c> (<c>:500</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both are called from <see cref="NewDay"/>, the month first: the reference increments the counters, runs the month,
/// then the week, then hands the week down to the kingdoms and the towns. The order matters because the month of
/// monsters looks at <see cref="WeekType"/>, which is derived from the counters — the month's week is the one that
/// has just started.
/// </para>
/// <para>
/// <b>Nothing here touches the heroes</b>: <c>vec_heroes.NewWeek()</c> is <c>Kingdom::ActionNewWeek</c> in the port,
/// because a kingdom's roster is every hero in play (ADR-111). The one thing the world does itself is the recruit
/// mode, which is a property of all heroes at once rather than of a kingdom.
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// The growth cap of a wandering stack on a map of the original version,
    /// <c>GameStatic::getNeutralMonsterLimit( false )</c> (<c>game_static.cpp:310</c>).
    /// </summary>
    /// <remarks>
    /// The original game clamps at 4000 because its army counts do not fit a longer fight; the port keeps the number so
    /// that a save played here and in the reference holds the same stacks.
    /// </remarks>
    private const uint NeutralMonsterLimit = 4000;

    /// <summary>
    /// The growth cap of a wandering stack on a Resurrection map,
    /// <c>GameStatic::getNeutralMonsterLimit( true )</c> (<c>game_static.cpp:310</c>).
    /// </summary>
    /// <remarks>
    /// The expansion raises the cap to four million: there is no reason to grow past that, and the count still fits an
    /// unsigned 32-bit integer.
    /// </remarks>
    private const uint ResurrectionNeutralMonsterLimit = 4000000;

    /// <summary>
    /// What happens at the first day of a month, <c>World::NewMonth()</c> (<c>world.cpp:493</c>).
    /// </summary>
    /// <remarks>
    /// The only month with a rule of its own is a monster's month, and only from the second month on: the first month's
    /// week is dealt before the game starts, and the reference does not spawn a wave from a week nobody played through.
    /// </remarks>
    public void NewMonth()
    {
        if (_month > 1 && WeekType.Type == WeekName.Monsters)
        {
            MonthOfMonstersAction(WeekType.Monster);
        }
    }

    /// <summary>
    /// What happens at the first day of a week, <c>World::NewWeek()</c> (<c>world.cpp:462</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two families of objects have a week: the week-life objects, which refill what they pay (the windmill, the water
    /// wheel, every dwelling), and the wandering monsters, which grow by a seventh. The reference skips both on the
    /// first week — the map loader has just settled them — and the port keeps that guard.
    /// </para>
    /// <para>
    /// <b>The recruit mode is reset for all heroes and then restored for the ones still on offer.</b> The reference
    /// clears the mode globally because a hero may have been hired or dismissed by any kingdom during the week; only
    /// the heroes that the kingdoms still list may keep the bit. A hero who retreated on the last day of the previous
    /// week stays on offer — an fheroes2 extension, see <see cref="Kingdoms.ResetRecruits"/>.
    /// </para>
    /// </remarks>
    public void NewWeek()
    {
        if (_week > 1)
        {
            uint monsterLimit = IsResurrectionMap ? ResurrectionNeutralMonsterLimit : NeutralMonsterLimit;

            for (int index = 0; index < _tiles.Count; ++index)
            {
                Tile tile = _tiles[index];

                // A tile stores the action value (ADR-075); IsWeekLife speaks the non-action one, like every other
                // classification predicate in the Assets layer.
                if (Mp2ObjectTypes.IsWeekLife(Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(this, false))))
                {
                    tile.UpdateObjectInfoTile(this);
                }
                else if (tile.GetMainObjectType(this) == Mp2ObjectType.ActionMonster)
                {
                    tile.UpdateMonsters(monsterLimit);
                }
            }
        }

        // Reset RECRUIT mode for all heroes at once.
        _heroes.ResetModes(HeroModes.Recruit);

        // Reset the recruits in all kingdoms at once and restore the mode for the ones that remain on offer.
        foreach (Hero hero in _kingdoms.ResetRecruits(_day, _heroes))
        {
            hero.SetModes(HeroModes.Recruit);
        }
    }

    /// <summary>
    /// Whether the map was made with the expansion, <c>GameVersion::RESURRECTION</c>.
    /// </summary>
    /// <remarks>
    /// The reference reads the version from the map header through <c>Settings</c>; the port has the header in
    /// <see cref="MapInfo"/> since ADR-158.
    /// </remarks>
    private bool IsResurrectionMap => MapInfo?.Version == MapVersion.Resurrection;

    /// <summary>
    /// Spawns a wave of <paramref name="monster"/> around the map's interesting objects,
    /// <c>World::_monthOfMonstersAction()</c> (<c>world.cpp:500</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three pools, in the reference's order of preference: 85% of the wave stands next to an in-game action object
    /// (a resource, a mine, an artifact), 10% on a road, and 5% on bare ground. A tile is only a candidate if no hero, town
    /// or monster is close enough — hence the exclusion set, which is filled before the scan and grows as tiles are
    /// chosen.
    /// </para>
    /// <para>
    /// The generator is seeded from the map seed and the month, so the same save month always spawns the same wave — the
    /// reference does this so that a reload does not change the map under the player.
    /// </para>
    /// <para>
    /// Internal rather than private: the month it belongs to only comes round after twenty-eight days and a week type,
    /// and a test that waited for both would be testing the calendar rather than the placement. The reference makes it
    /// private because nothing else calls it.
    /// </para>
    /// </remarks>
    /// <param name="monster">The monster of the month; an unknown one is a no-op.</param>
    internal void MonthOfMonstersAction(MonsterType monster)
    {
        if (monster == MonsterType.Unknown)
        {
            return;
        }

        var primaryTargetTiles = new List<int>();
        var secondaryTargetTiles = new List<int>();
        var tetriaryTargetTiles = new List<int>();
        var excludeTiles = new HashSet<int>();

        var seededGen = new Pcg32(_seed + _month);

        // Heroes, towns and monsters are excluded first, because the proximity check must see all of them.
        for (int index = 0; index < _tiles.Count; ++index)
        {
            Mp2ObjectType objectType = _tiles[index].GetMainObjectType(this);

            if (objectType == Mp2ObjectType.Castle
                || objectType == Mp2ObjectType.Hero
                || objectType == Mp2ObjectType.ActionMonster)
            {
                excludeTiles.Add(index);
            }
        }

        for (int index = 0; index < _tiles.Count; ++index)
        {
            Tile tile = _tiles[index];

            if (tile.IsWater)
            {
                // Monsters are not placed on water.
                continue;
            }

            if (excludeTiles.Contains(index))
            {
                continue;
            }

            Mp2ObjectType objectType = tile.GetMainObjectType(this);

            if (Mp2ObjectTypes.IsInGameActionObject(objectType))
            {
                if (IsTileBlockedForSettingMonster(index, 3, excludeTiles))
                {
                    continue;
                }

                int tileToSet = FindSuitableNeighbouringTile(index, tile.PassableDirections == Directions.All, seededGen);

                if (tileToSet >= 0)
                {
                    primaryTargetTiles.Add(tileToSet);
                    excludeTiles.Add(index);
                }
            }
            else if (tile.IsRoad)
            {
                if (IsTileBlockedForSettingMonster(index, 4, excludeTiles))
                {
                    continue;
                }

                if (GetNeighbouringEmptyTileCount(index) < 2)
                {
                    continue;
                }

                int tileToSet = FindSuitableNeighbouringTile(index, true, seededGen);

                if (tileToSet >= 0)
                {
                    secondaryTargetTiles.Add(tileToSet);
                    excludeTiles.Add(index);
                }
            }
            else if (tile.IsClearGround(this))
            {
                if (IsTileBlockedForSettingMonster(index, 4, excludeTiles))
                {
                    continue;
                }

                if (GetNeighbouringEmptyTileCount(index) < 4)
                {
                    continue;
                }

                int tileToSet = FindSuitableNeighbouringTile(index, true, seededGen);

                if (tileToSet >= 0)
                {
                    tetriaryTargetTiles.Add(tileToSet);
                    excludeTiles.Add(index);
                }
            }
        }

        Rand.ShuffleWithGen(primaryTargetTiles, seededGen);
        Rand.ShuffleWithGen(secondaryTargetTiles, seededGen);
        Rand.ShuffleWithGen(tetriaryTargetTiles, seededGen);

        uint monstersToBePlaced = (uint)(primaryTargetTiles.Count / 3);
        uint mapMinimum = (uint)(_tiles.Count / 360);

        if (monstersToBePlaced < mapMinimum)
        {
            monstersToBePlaced = mapMinimum;
        }
        else
        {
            monstersToBePlaced = Rand.GetWithGen(monstersToBePlaced * 75 / 100, monstersToBePlaced * 125 / 100, seededGen);
        }

        // 85% of positions are for primary targets, 10% for roads and 5% for bare ground.
        uint primaryTileCount = Math.Min(monstersToBePlaced * 85 / 100, (uint)primaryTargetTiles.Count);

        for (uint i = 0; i < primaryTileCount; ++i)
        {
            _tiles[primaryTargetTiles[(int)i]].SetMonster(this, monster, 0 /* random */);
        }

        uint secondaryTileCount = Math.Min(monstersToBePlaced * 10 / 100, (uint)secondaryTargetTiles.Count);

        for (uint i = 0; i < secondaryTileCount; ++i)
        {
            _tiles[secondaryTargetTiles[(int)i]].SetMonster(this, monster, 0 /* random */);
        }

        uint tetriaryTileCount = Math.Min(monstersToBePlaced * 5 / 100, (uint)tetriaryTargetTiles.Count);

        for (uint i = 0; i < tetriaryTileCount; ++i)
        {
            _tiles[tetriaryTargetTiles[(int)i]].SetMonster(this, monster, 0 /* random */);
        }
    }

    /// <summary>
    /// Whether one of <paramref name="excludeTiles"/> is within <paramref name="radius"/> of a tile,
    /// <c>isTileBlockedForSettingMonster()</c> (<c>world.cpp:71</c>).
    /// </summary>
    /// <param name="tileIndex">Centre tile.</param>
    /// <param name="radius">Chebyshev radius, in tiles.</param>
    /// <param name="excludeTiles">The tiles a monster must keep its distance from.</param>
    /// <returns>Whether the neighbourhood holds an excluded tile.</returns>
    private bool IsTileBlockedForSettingMonster(int tileIndex, int radius, HashSet<int> excludeTiles)
    {
        foreach (int index in MapGeometry.GetAroundIndexes(tileIndex, Width, Height, radius))
        {
            if (excludeTiles.Contains(index))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Picks a neighbour a monster may stand on, <c>findSuitableNeighbouringTile()</c> (<c>world.cpp:78</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A candidate must be land, clear ground and neither an event nor a lone coast tile. The <b>event</b> is skipped
    /// because two action objects cannot share a tile and the event has no sprite the player could tell the monster
    /// from; the <b>coast</b> is skipped unless another coast tile is next to it, because a single coast square is a
    /// corner where a monster would look misplaced.
    /// </para>
    /// <para>
    /// With <paramref name="allDirections"/> false only the row below the object is considered: an action object that
    /// blocks three directions has its free side there, and the reference wants the monster to stay in front of it.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile whose neighbourhood is searched.</param>
    /// <param name="allDirections">Whether every neighbour is a candidate.</param>
    /// <param name="generator">The seeded generator of the month.</param>
    /// <returns>A neighbour index, or <c>-1</c> when there is none.</returns>
    private int FindSuitableNeighbouringTile(int tileIndex, bool allDirections, Pcg32 generator)
    {
        var suitableIds = new List<int>();

        foreach (int index in MapGeometry.GetAroundIndexes(tileIndex, Width, Height, 1))
        {
            // If allDirections is false, only the tiles below the current object are considered.
            if (!allDirections && index < tileIndex + Width - 2)
            {
                continue;
            }

            Tile indexedTile = _tiles[index];

            if (indexedTile.IsWater || !indexedTile.IsClearGround(this))
            {
                continue;
            }

            Mp2ObjectType objectType = indexedTile.GetMainObjectType(this);

            if (objectType == Mp2ObjectType.Event)
            {
                continue;
            }

            // A coast tile is suitable only when there are other coast tiles nearby.
            if (objectType == Mp2ObjectType.Coast && ScanAroundObject(index, Mp2ObjectType.Coast).Count == 0)
            {
                continue;
            }

            suitableIds.Add(index);
        }

        return suitableIds.Count == 0 ? -1 : Rand.GetWithGen(suitableIds, generator);
    }

    /// <summary>
    /// How many neighbours are land and clear, <c>getNeighbouringEmptyTileCount()</c> (<c>world.cpp:126</c>).
    /// </summary>
    /// <param name="tileIndex">Centre tile.</param>
    /// <returns>The count, 0 to 8.</returns>
    private int GetNeighbouringEmptyTileCount(int tileIndex)
    {
        int count = 0;

        foreach (int index in MapGeometry.GetAroundIndexes(tileIndex, Width, Height, 1))
        {
            Tile indexedTile = _tiles[index];

            if (!indexedTile.IsWater && indexedTile.IsClearGround(this))
            {
                ++count;
            }
        }

        return count;
    }
}
