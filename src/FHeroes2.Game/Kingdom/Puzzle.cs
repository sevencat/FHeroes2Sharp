using FHeroes2.Core.Random;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The jigsaw the obelisks uncover, <c>Puzzle</c> (<c>puzzle.h:33-55</c>).
/// </summary>
/// <remarks>
/// <para>
/// The puzzle is 48 tiles — the <c>PUZZLE.ICN</c> frame holds six by eight — and the reference models it as a
/// <c>std::bitset&lt;48&gt;</c> with four extra lists of tile indices. Each list is shuffled once, when the kingdom is
/// created, and <see cref="Update"/> then walks them in order: the four corners and the frame first, the inner ring
/// second, the two 2×2 blocks last. That is why a fresh map shows the border of the picture before its middle —
/// the zones are a deliberate order, not an accident of indexing.
/// </para>
/// <para>
/// The reference splits the class over <c>puzzle.cpp</c>: the bitset and <see cref="Update"/> in the first three
/// hundred lines, the dialog and the save/load in the rest. Only the bitset is here. The dialog needs the drawing
/// stack and the save format is not written yet.
/// </para>
/// </remarks>
public sealed class Puzzle
{
    /// <summary>How many tiles the picture has, <c>numOfPuzzleTiles</c> (<c>puzzle.h:29</c>).</summary>
    public const int NumOfPuzzleTiles = 48;

    /// <summary>The outer frame and the four corners, <c>zone1_order</c> (<c>puzzle.h:41</c>).</summary>
    /// <remarks>24 tiles: rows 0-5 of columns 0-6, plus the last row and the right column.</remarks>
    private readonly List<byte> _zone1Order =
        [0, 1, 2, 3, 4, 5, 6, 11, 12, 17, 18, 23, 24, 29, 30, 35, 36, 41, 42, 43, 44, 45, 46, 47];

    /// <summary>The ring inside the frame, <c>zone2_order</c> (<c>puzzle.h:42</c>).</summary>
    /// <remarks>16 tiles: everything at distance two from the border.</remarks>
    private readonly List<byte> _zone2Order = [7, 8, 9, 10, 13, 16, 19, 22, 25, 28, 31, 34, 37, 38, 39, 40];

    /// <summary>The upper 2×2 block, <c>zone3_order</c> (<c>puzzle.h:43</c>).</summary>
    private readonly List<byte> _zone3Order = [14, 15, 32, 33];

    /// <summary>The lower 2×2 block, <c>zone4_order</c> (<c>puzzle.h:44</c>).</summary>
    private readonly List<byte> _zone4Order = [20, 21, 26, 27];

    private readonly bool[] _tiles = new bool[NumOfPuzzleTiles];

    /// <summary>
    /// A puzzle with all tiles hidden and its zones shuffled, <c>Puzzle::Puzzle()</c> (<c>puzzle.cpp:303</c>).
    /// </summary>
    /// <remarks>
    /// The reference shuffles with the global generator. The port takes one so that a saved game can replay the same
    /// layout; <see cref="Rand.CurrentThreadRandomDevice"/> is used when nobody passes one.
    /// </remarks>
    public Puzzle()
        : this(Rand.CurrentThreadRandomDevice)
    {
    }

    /// <summary>
    /// A puzzle with all tiles hidden and its zones shuffled by <paramref name="generator"/>.
    /// </summary>
    /// <param name="generator">The generator to shuffle the four zone lists with.</param>
    public Puzzle(Pcg32 generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        Rand.ShuffleWithGen(_zone1Order, generator);
        Rand.ShuffleWithGen(_zone2Order, generator);
        Rand.ShuffleWithGen(_zone3Order, generator);
        Rand.ShuffleWithGen(_zone4Order, generator);
    }

    /// <summary>
    /// Whether a tile is uncovered, <c>test()</c> of the bitset.
    /// </summary>
    /// <param name="tile">Tile index, 0 to <see cref="NumOfPuzzleTiles"/>.</param>
    /// <returns><see langword="true"/> when the tile is visible.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tile"/> is outside the picture.</exception>
    public bool this[int tile] => _tiles[tile];

    /// <summary>
    /// How many tiles are uncovered, <c>count()</c> of the bitset.
    /// </summary>
    public int Count
    {
        get
        {
            int count = 0;

            foreach (bool tile in _tiles)
            {
                if (tile)
                {
                    ++count;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Uncovers the tiles the obelisks have earned, <c>Puzzle::Update()</c> (<c>puzzle.cpp:323</c>).
    /// </summary>
    /// <param name="openObelisk">How many obelisks the kingdom has visited.</param>
    /// <param name="totalObelisk">How many obelisks the map has.</param>
    /// <remarks>
    /// <para>
    /// The share of the picture is <c>open * 48 / total</c>, truncated, and only the difference to what is already
    /// uncovered is added — visiting the same obelisk twice therefore adds nothing, and a map with 48 obelisks gives
    /// one tile per visit.
    /// </para>
    /// <para>
    /// The four zones are filled in order, and a zone that still has a hidden tile is filled completely before the
    /// next one is touched: that is what makes the frame appear first. The reference asserts on a total of zero;
    /// the port returns, because a map without obelisks is legal and simply has no puzzle to uncover.
    /// </para>
    /// <para>
    /// <b>This never hides a tile.</b> Losing a town that held an obelisk does not take the picture back — the
    /// reference only ever sets bits here, and the count it compares against is the current one.
    /// </para>
    /// </remarks>
    public void Update(uint openObelisk, uint totalObelisk)
    {
        if (totalObelisk == 0)
        {
            return;
        }

        uint openPuzzle = openObelisk * NumOfPuzzleTiles / totalObelisk;
        uint needPuzzle = openPuzzle > (uint)Count ? openPuzzle - (uint)Count : 0;

        if (needPuzzle == 0)
        {
            return;
        }

        if (ClosedTilesExist(_zone1Order))
        {
            OpenFirstTiles(ref needPuzzle, _zone1Order);
        }

        if (needPuzzle > 0 && ClosedTilesExist(_zone2Order))
        {
            OpenFirstTiles(ref needPuzzle, _zone2Order);
        }

        if (needPuzzle > 0 && ClosedTilesExist(_zone3Order))
        {
            OpenFirstTiles(ref needPuzzle, _zone3Order);
        }

        if (needPuzzle > 0 && ClosedTilesExist(_zone4Order))
        {
            OpenFirstTiles(ref needPuzzle, _zone4Order);
        }
    }

    /// <summary>
    /// Hides every tile again, the effect of <c>puzzle_maps.reset()</c> in <c>Kingdom::clear()</c> (<c>kingdom.cpp:134</c>).
    /// </summary>
    /// <remarks>
    /// The zone orders are <b>not</b> reshuffled: the reference resets the bitset only, and a kingdom that comes back
    /// to the same map keeps the layout it had.
    /// </remarks>
    public void Reset() => Array.Clear(_tiles);

    /// <summary>Whether a zone still has a hidden tile, <c>ClosedTilesExists()</c> (<c>puzzle.cpp:65</c>).</summary>
    private bool ClosedTilesExist(List<byte> zone) => zone.Exists(tile => !_tiles[tile]);

    /// <summary>
    /// Uncovers the first hidden tiles of a zone, <c>ZoneOpenFirstTiles()</c> (<c>puzzle.cpp:70</c>).
    /// </summary>
    /// <param name="opens">How many tiles may still be uncovered; decremented by the number actually opened.</param>
    /// <param name="zone">The zone, in its shuffled order.</param>
    private void OpenFirstTiles(ref uint opens, List<byte> zone)
    {
        foreach (byte tile in zone)
        {
            if (opens == 0)
            {
                return;
            }

            if (_tiles[tile])
            {
                continue;
            }

            _tiles[tile] = true;
            --opens;
        }
    }
}
