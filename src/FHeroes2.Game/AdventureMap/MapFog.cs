using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The fog of war of a map as a view over the tiles that store it: <c>Maps::Tile::_fogColors</c>
/// (which colours have not discovered a tile) and <c>Maps::Tile::_fogDirection</c> (the mask the
/// renderer follows, <c>Maps::Tile::getDirection()</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>No state of its own.</b> The reference keeps the fog in the tile (<c>maps_tiles.h:421</c>) and
/// <c>Maps::ClearFog</c> writes it there; this class is the port of that reading, so every method
/// forwards to <see cref="Tile"/> and a second table of fog bytes cannot drift out of step with the
/// tiles. It exists because the AI, the radar and the map view all ask the same three questions
/// (<see cref="IsFogged"/>, <see cref="GetDirection"/>, <see cref="CountFoggedAround"/>) and because
/// the reveal disc is a map-level operation rather than a tile one.
/// </para>
/// <para>
/// The reference recomputes the direction masks of the rectangle a reveal touched
/// (<c>Maps::ClearFog</c> → <c>updateDirectionsInArea</c>). The rectangle is a caching optimisation:
/// the mask of a tile only depends on the fog of its eight neighbours, so this port recomputes the
/// masks of the whole map in one pass instead of tracking the dirty rectangles.
/// </para>
/// <para>
/// A tile is "under the fog" for a set of colours when it is undiscovered for <em>all</em> of them
/// (<c>Maps::Tile::isFog( colors )</c> is <c>( fog &amp; colors ) == colors</c>), so a tile that one
/// human player can see is drawn without fog for every human player.
/// </para>
/// </remarks>
public sealed class MapFog
{
    /// <summary>
    /// Tiles around a hero that start without fog, <c>GameStatic::getFogDiscoveryDistance(
    /// FogDiscoveryType::HEROES )</c>.
    /// </summary>
    /// <remarks>
    /// The base value only: <c>Heroes::GetScoutingDistance</c> — the port's
    /// <c>Hero.GetScoutingDistance</c> — adds the scouting skill and the reveal distance of the hero's
    /// artifacts on top of it, which is what <c>Hero.Scout</c> reveals with.
    /// </remarks>
    public const int HeroRevealDistance = 4;

    /// <summary>
    /// Tiles around a castle that start without fog,
    /// <c>GameStatic::getFogDiscoveryDistance( FogDiscoveryType::CASTLE )</c>.
    /// </summary>
    public const int CastleRevealDistance = 5;

    /// <summary>
    /// The radius an observation tower uncovers,
    /// <c>GameStatic::getFogDiscoveryDistance( FogDiscoveryType::OBSERVATION_TOWER )</c>
    /// (<c>game_static.cpp:119</c>).
    /// </summary>
    /// <remarks>
    /// 19 is not a typo: a tower shows a disc of nearly forty tiles across, which is why it is worth the
    /// detour. The reference clamps the same disc to the map in <c>Maps::ClearFog</c>, which
    /// <see cref="World.ClearFog"/> does too.
    /// </remarks>
    public const int ObservationTowerRevealDistance = 19;

    /// <summary>
    /// Tiles around an Eye of the Magi that its visit uncovers,
    /// <c>GameStatic::getFogDiscoveryDistance( FogDiscoveryType::MAGI_EYES )</c>
    /// (<c>game_static.cpp:122</c>).
    /// </summary>
    public const int MagiEyesRevealDistance = 9;

    private readonly IReadOnlyList<Tile> _tiles;

    /// <summary>
    /// Wraps the tiles of a map, <c>World::vec_tiles</c>, which own the fog.
    /// </summary>
    /// <param name="tiles">The tiles in map order, at least <paramref name="width"/> ×
    /// <paramref name="height"/> of them.</param>
    /// <param name="width">Width of the map in tiles.</param>
    /// <param name="height">Height of the map in tiles.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tiles"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive, or the list is shorter than the map.</exception>
    public MapFog(IReadOnlyList<Tile> tiles, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(tiles.Count, width * height);

        _tiles = tiles;
        Width = width;
        Height = height;
    }

    /// <summary>Width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Height of the map in tiles.</summary>
    public int Height { get; }

    /// <summary>
    /// Number of tiles the last <see cref="UpdateDirections"/> call marked as under the fog (its
    /// <paramref name="colors"/> argument); for diagnostics and tests.
    /// </summary>
    public int CountFogged()
    {
        int count = 0;

        for (int index = 0; index < Width * Height; ++index)
        {
            if (_tiles[index].FogDirection != Direction.Unknown)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Whether the tile is undiscovered for all <paramref name="colors"/>.</summary>
    public bool IsFogged(int column, int row, PlayerColor colors) =>
        _tiles[(row * Width) + column].IsFogForAll(colors);

    /// <summary>
    /// The fog mask of a tile, <see cref="Direction.Unknown"/> when it is visible. Only as good as
    /// the last <see cref="UpdateDirections"/> call.
    /// </summary>
    public Direction GetDirection(int column, int row) => _tiles[(row * Width) + column].FogDirection;

    /// <summary>
    /// Reveals the disc around a tile for <paramref name="colors"/> — <c>Maps::ClearFog</c>: every
    /// tile whose distance is below <see cref="SquaredRevealLimit"/> loses the fog of those colors.
    /// </summary>
    /// <remarks>
    /// A <paramref name="distance"/> of zero reveals the tile it is centered on and nothing else (the
    /// limit is one, and only <c>dx == dy == 0</c> stays below it) — that is how a single tile, a water
    /// tile for Magellan's maps for instance, is uncovered.
    /// </remarks>
    /// <param name="column">Column of the center tile.</param>
    /// <param name="row">Row of the center tile.</param>
    /// <param name="distance">Radius in tiles.</param>
    /// <param name="colors">The colors that lose the fog.</param>
    /// <returns>
    /// How many tiles actually lost fog, i.e. how many were undiscovered for at least one of
    /// <paramref name="colors"/> before; tiles that were already visible do not count.
    /// </returns>
    public int Reveal(int column, int row, int distance, PlayerColor colors)
    {
        int limit = SquaredRevealLimit(distance);
        int lastRow = Math.Min(Height - 1, row + distance);
        int lastColumn = Math.Min(Width - 1, column + distance);
        int revealed = 0;

        for (int y = Math.Max(0, row - distance); y <= lastRow; ++y)
        {
            int dy = y - row;
            for (int x = Math.Max(0, column - distance); x <= lastColumn; ++x)
            {
                int dx = x - column;
                if ((dx * dx) + (dy * dy) < limit)
                {
                    Tile tile = _tiles[(y * Width) + x];

                    if ((tile.FogColors & colors) != PlayerColor.None)
                    {
                        ++revealed;
                        tile.ClearFog(colors);
                    }
                }
            }
        }

        return revealed;
    }

    /// <summary>
    /// How many tiles <see cref="Reveal"/> would uncover with the same arguments,
    /// <c>Maps::getFogTileCountToBeRevealed</c> (<c>maps.cpp:531</c>).
    /// </summary>
    /// <remarks>
    /// The disc is the one of <see cref="Reveal"/> — same <see cref="SquaredRevealLimit"/> and the same clamp to
    /// the map — but nothing changes. The AI uses it to decide whether an Observation Tower is worth a visit, and
    /// the reference answers zero for a radius of zero or less and for a tile outside the map.
    /// </remarks>
    /// <param name="column">Column of the center tile.</param>
    /// <param name="row">Row of the center tile.</param>
    /// <param name="distance">Radius in tiles.</param>
    /// <param name="colors">The colors that would lose the fog.</param>
    /// <returns>How many tiles of the disc are undiscovered for <paramref name="colors"/>.</returns>
    public int CountFoggedAround(int column, int row, int distance, PlayerColor colors)
    {
        if (distance <= 0 || column < 0 || row < 0 || column >= Width || row >= Height)
        {
            return 0;
        }

        int limit = SquaredRevealLimit(distance);
        int lastRow = Math.Min(Height - 1, row + distance);
        int lastColumn = Math.Min(Width - 1, column + distance);
        int count = 0;

        for (int y = Math.Max(0, row - distance); y <= lastRow; ++y)
        {
            int dy = y - row;
            for (int x = Math.Max(0, column - distance); x <= lastColumn; ++x)
            {
                int dx = x - column;
                if (((dx * dx) + (dy * dy)) < limit && IsFogged(x, y, colors))
                {
                    ++count;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Recomputes the fog mask of every tile from the fog of its neighbours
    /// (<c>updateDirectionsInArea</c> over the whole map; tiles outside the map count as fogged).
    /// </summary>
    /// <remarks>
    /// The masks are derived data: a save carries the colours only, and the loader replicates the
    /// reference's <c>updateFogDirectionsInArea()</c> afterwards (<c>game_startgame.cpp:863</c>,
    /// <c>interface_gamearea.cpp:869</c>) by calling this method for the colours about to be drawn.
    /// </remarks>
    /// <param name="colors">Colors whose fog the current player shares.</param>
    public void UpdateDirections(PlayerColor colors)
    {
        if (colors == PlayerColor.None)
        {
            // Nothing is fogged: no tile is undiscovered for a set of no colors, so the masks are all
            // unknown and the renderer draws no fog at all.
            foreach (Tile tile in _tiles)
            {
                tile.SetFogDirection(Direction.Unknown);
            }

            return;
        }

        // The sweep itself is the reference's updateFogDirectionsInArea(), shared with the per-frame call of
        // Interface::GameArea: one implementation, so the two entry points cannot disagree about a mask.
        TileFog.UpdateFogDirectionsInArea(
            _tiles, Width, Height, new Point(0, 0), new Point(Width - 1, Height - 1), colors);
    }

    /// <summary>
    /// The squared distance limit of a reveal radius — <c>getSquaredScoutingRadiusLimit</c>
    /// (<c>maps.cpp:531</c>), including its two exceptions for the high scouting values an artifact can
    /// reach and the cutoff below which the reference shaves a tile off the disc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The limit is <c>( distance + 1 ) ^ 2</c>, minus <c>distance</c> <b>only while the radius is below
    /// six</b>. The subtraction is not a rounding tweak: it reproduces the disc of the original game, which
    /// is visibly smaller than a circle of radius <c>distance + 1</c> for the small radii a hero starts
    /// with. At six and above the limit is the plain square, so a hero with scouting artifacts reveals a
    /// full circle.
    /// </para>
    /// <para>
    /// Seven and eight are hardcoded because the original game does not follow the formula there: the
    /// reference notes the values have to sit in <c>[66, 72]</c> and <c>[90, 98]</c> respectively.
    /// </para>
    /// </remarks>
    /// <param name="distance">The scouting distance in tiles.</param>
    /// <returns>The squared distance a tile has to stay below to be revealed.</returns>
    public static int SquaredRevealLimit(int distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        return distance switch
        {
            7 => 66,
            8 => 90,
            < 6 => ((distance + 1) * (distance + 1)) - distance,
            _ => (distance + 1) * (distance + 1),
        };
    }
}
