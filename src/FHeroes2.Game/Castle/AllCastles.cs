using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Castle;

/// <summary>
/// Every town and castle of the world, <c>AllCastles</c> (<c>castle.cpp:2283-2378</c>, <c>castle.h:384</c>).
/// </summary>
/// <remarks>
/// <para>
/// The class answers one question: "which town owns this tile?". A town covers seventeen tiles of the map — its
/// entrance plus the square of terrain the sprite is drawn on — so a lookup cannot just compare tile indices. The
/// footprints are registered on insert and removed with the town:
/// </para>
/// <code>
///              +
///            +++++
///            +++++
///            ++X++
///            ++ ++
/// </code>
/// <para>
/// <c>X</c> is the entrance tile, <c>+</c> are the tiles that answer with this town. The two tiles below the
/// entrance (left and right of <c>X</c>'s lower neighbour) stay free for the hero standing in front of the gate, and
/// the tile directly below is not part of the town either.
/// </para>
/// <para>
/// <b>The port keeps coordinates, not references, in the footprint table</b>: the reference keys a
/// <c>std::map&lt;Point, size_t&gt;</c> by position, which is what lets it answer before and after the world has a
/// width. The port has no <c>Point</c> type at this layer, so it keys by an <see cref="int"/> tile index and needs the
/// map width for the conversion — that is the one argument the reference does not have.
/// </para>
/// <para>
/// The towns are stored in insertion order, and the footprint table stores their positions in that list. Removing a
/// town therefore has to shift every later index, which is what <see cref="Remove(int, int)"/> does; a stale index
/// would silently point at the next town.
/// </para>
/// </remarks>
public sealed partial class AllCastles : IReadOnlyList<Castle>
{
    /// <summary>
    /// The most towns a map can hold, <c>maximumCastles</c> (<c>castle.h:380</c>) — the number of entries in a map
    /// file's town table, and the number of default names.
    /// </summary>
    public const int MaximumAllowed = 72;

    private readonly List<Castle> _castles = new(MaximumAllowed);
    private readonly Dictionary<int, int> _castleTiles = new(MaximumAllowed * 17);
    private readonly int _width;

    /// <summary>
    /// Creates an empty table for a map of the given width, <c>AllCastles::AllCastles()</c>.
    /// </summary>
    /// <remarks>The width is only needed to turn a tile index into the coordinates the footprint is kept in.</remarks>
    /// <param name="width">The map width in tiles; zero is allowed and makes index lookups fail.</param>
    public AllCastles(int width)
    {
        _width = width;
    }

    /// <summary>How many towns the world has, <c>AllCastles::Size()</c>.</summary>
    public int Count => _castles.Count;

    /// <summary>
    /// The position of the first town in the table, <c>VecCastles::GetFirstCastle()</c>
    /// (<c>castle.cpp:2400-2412</c>).
    /// </summary>
    /// <remarks>
    /// The reference scans for the first non-null entry, because its vector can hold gaps where a town was
    /// destroyed. The port's list never holds a gap — <see cref="Remove(int, int)"/> compacts it — so "the first
    /// town" is position 0, and only an empty table has no answer. Callers use it to pick an arbitrary town (the
    /// starting town of a colour, the town an event fires in), which is what the reference uses it for too.
    /// </remarks>
    /// <returns>Zero, or -1 when the world has no towns, the reference's <c>-1</c>.</returns>
    public int FirstCastle => _castles.Count > 0 ? 0 : -1;

    /// <summary>A town by its insertion order, <c>_castles</c>.</summary>
    /// <param name="index">Zero based position in the table.</param>
    public Castle this[int index] => _castles[index];

    /// <summary>
    /// Registers a town and its footprint, <c>AllCastles::AddCastle()</c> (<c>castle.cpp:2293-2335</c>).
    /// </summary>
    /// <remarks>
    /// A tile that already belongs to another town keeps the first one, exactly as the reference's
    /// <c>try_emplace</c> does: towns are read from the map file top to bottom, left to right, and the first claim
    /// wins. Overlaps are a broken map rather than a crash, so nothing is thrown.
    /// </remarks>
    /// <param name="castle">The town to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    public void Add(Castle castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        _castles.Add(castle);

        if (_width <= 0)
        {
            // Without a width the index cannot be turned back into coordinates, so there is no footprint to keep.
            // This has to be checked first: PointOf() divides by the width.
            return;
        }

        int id = _castles.Count - 1;
        (int centerX, int centerY) = PointOf(castle.TileIndex);

        for (int y = -2; y <= 1; ++y)
        {
            for (int x = -2; x <= 2; ++x)
            {
                if (y == 1 && x == 0)
                {
                    // The tile in front of the gate stays walkable and belongs to nobody.
                    continue;
                }

                _castleTiles.TryAdd((centerY + y) * _width + centerX + x, id);
            }
        }

        // The top of the sprite is one tile above the row the reference's loop covers.
        _castleTiles.TryAdd((centerY - 3) * _width + centerX, id);
    }

    /// <summary>
    /// The town that owns a tile, including the tiles its sprite covers, <c>AllCastles::Get()</c>.
    /// </summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <returns>The town, or <see langword="null"/> when no town covers that tile.</returns>
    public Castle? Get(int x, int y) => _width > 0 ? Get(y * _width + x) : null;

    /// <summary>
    /// The town that owns a tile, including the tiles its sprite covers, <c>AllCastles::Get()</c>.
    /// </summary>
    /// <param name="tileIndex">Tile index, row major.</param>
    /// <returns>The town, or <see langword="null"/> when no town covers that tile.</returns>
    public Castle? Get(int tileIndex) =>
        _castleTiles.TryGetValue(tileIndex, out int id) ? _castles[id] : null;

    /// <summary>
    /// Forgets a town and its footprint, <c>AllCastles::removeCastle()</c> (<c>castle.cpp:2349-2378</c>).
    /// </summary>
    /// <remarks>
    /// Every index greater than the removed one is decremented, because the footprint table stores positions in
    /// <see cref="_castles"/> rather than pointers.
    /// </remarks>
    /// <param name="x">Tile column of the town's entrance.</param>
    /// <param name="y">Tile row of the town's entrance.</param>
    public void Remove(int x, int y)
    {
        if (_width <= 0 || !_castleTiles.TryGetValue(y * _width + x, out int id))
        {
            return;
        }

        _castles.RemoveAt(id);

        List<int> stale = new();
        List<int> tiles = new(_castleTiles.Keys);

        foreach (int tileIndex in tiles)
        {
            if (_castleTiles[tileIndex] == id)
            {
                stale.Add(tileIndex);
            }
            else if (_castleTiles[tileIndex] > id)
            {
                --_castleTiles[tileIndex];
            }
        }

        foreach (int tileIndex in stale)
        {
            _castleTiles.Remove(tileIndex);
        }
    }

    /// <summary>
    /// Gives every town its day, <c>AllCastles::NewDay()</c> (<c>castle.cpp:2361-2366</c>).
    /// </summary>
    /// <remarks>
    /// One call, no arguments: the day only hands the building permit back to each town
    /// (<see cref="Castle.ActionNewDay"/>). The reference loops over the same table in the same order.
    /// </remarks>
    public void NewDay()
    {
        foreach (Castle castle in _castles)
        {
            castle.ActionNewDay();
        }
    }

    /// <summary>
    /// Gives every town its week, <c>AllCastles::NewWeek()</c> (<c>castle.cpp:2368-2373</c>).
    /// </summary>
    /// <remarks>
    /// The arguments are the clock the world passes down to each town; see <see cref="Castle.ActionNewWeek"/> for
    /// what a town does with them.
    /// </remarks>
    /// <param name="weekCount">Weeks since the start of the game, <c>world.CountWeek()</c>.</param>
    /// <param name="week">The current week, <c>world.GetWeekType()</c>.</param>
    /// <param name="isBeginMonth">Whether today is the first day of a month, <c>world.BeginMonth()</c>.</param>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>.</param>
    /// <param name="generator">The generator of the neutral garrison roll; <see langword="null"/> is the thread's own.</param>
    public void NewWeek(uint weekCount, in Week week, bool isBeginMonth, uint dayCount, Pcg32? generator = null)
    {
        foreach (Castle castle in _castles)
        {
            castle.ActionNewWeek(weekCount, week, isBeginMonth, dayCount, generator);
        }
    }

    /// <summary>
    /// Scouts around every town of the given colors, <c>AllCastles::Scout()</c> (<c>castle.cpp:2380-2391</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>castle.cpp:2380-2391</c>。没有主人的城堡（<c>PlayerColor::NONE</c>）永远筛不进来，所以
    /// MP2 里那些还没被占领的城镇不会开图——这正是开局只有英雄脚下亮一片的原因。
    /// </para>
    /// <para>
    /// 与 <see cref="AllHeroes.Scout"/> 一样，<paramref name="colors"/> 在参考里是
    /// <c>Players::GetPlayerFriends( color )</c>；端口的 <see cref="PlayerColor"/> 是 <c>[Flags]</c>，将来有了
    /// 盟友不用改签名。
    /// </para>
    /// </remarks>
    /// <param name="world">The world holding the fog.</param>
    /// <param name="colors">The colors that lose the fog.</param>
    /// <returns>Number of tiles uncovered over all those towns.</returns>
    public int Scout(World world, PlayerColor colors)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (colors == PlayerColor.None)
        {
            return 0;
        }

        int revealed = 0;

        foreach (Castle castle in _castles)
        {
            if ((colors & castle.Color) == 0)
            {
                continue;
            }

            revealed += castle.Scout(world);
        }

        return revealed;
    }

    /// <summary>Enumerates the towns in insertion order, the <c>begin()</c>/<c>end()</c> of the reference.</summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<Castle> GetEnumerator() => _castles.GetEnumerator();

    /// <inheritdoc/>
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    /// <summary>Turns a tile index into the coordinates the footprint table is keyed by.</summary>
    /// <param name="tileIndex">The tile index.</param>
    /// <returns>The column and row.</returns>
    private (int X, int Y) PointOf(int tileIndex) => (tileIndex % _width, tileIndex / _width);
}
