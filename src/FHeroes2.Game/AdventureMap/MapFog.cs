using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The fog of war of a map: per tile the colors that have not discovered it yet
/// (<c>Maps::Tile::_fogColors</c>) and the direction mask that follows from it
/// (<c>Maps::Tile::getDirection()</c>), which is all the renderer needs.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps one byte of fog flags per tile and recomputes the direction mask of a
/// rectangle whenever a tile is revealed (<c>Maps::ClearFog</c> →
/// <c>updateDirectionsInArea</c>). The rectangle is a caching optimisation: the mask of a tile
/// only depends on the fog of its eight neighbours, so this port recomputes the masks of the whole
/// map in one pass instead of tracking the dirty rectangles.
/// </para>
/// <para>
/// A tile is "under the fog" for a set of colors when it is undiscovered for <em>all</em> of them
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

    private readonly byte[] _fog;
    private readonly Direction[] _directions;

    /// <summary>Creates a map whose every tile is undiscovered for every color.</summary>
    public MapFog(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _fog = new byte[width * height];
        _directions = new Direction[width * height];

        Array.Fill(_fog, (byte)PlayerColors.All);
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
        foreach (Direction direction in _directions)
        {
            if (direction != Direction.Unknown)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Whether the tile is undiscovered for all <paramref name="colors"/>.</summary>
    public bool IsFogged(int column, int row, PlayerColor colors) =>
        (_fog[(row * Width) + column] & (byte)colors) == (byte)colors;

    /// <summary>
    /// The fog mask of a tile, <see cref="Direction.Unknown"/> when it is visible. Only as good as
    /// the last <see cref="UpdateDirections"/> call.
    /// </summary>
    public Direction GetDirection(int column, int row) => _directions[(row * Width) + column];

    /// <summary>
    /// The colors that still have the tile under the fog — the same value as <see cref="Maps.Tile.FogColors"/>
    /// once the two are in step.
    /// </summary>
    /// <param name="column">Column of the tile.</param>
    /// <param name="row">Row of the tile.</param>
    /// <returns>The colors that have not revealed the tile.</returns>
    public PlayerColor GetFogColors(int column, int row) => (PlayerColor)_fog[(row * Width) + column];

    /// <summary>
    /// Stores which colors have the tile under the fog, without touching the mask of <see cref="GetDirection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Reveal"/> is the only writer during play — this one exists for the save loader, which has the colours
    /// per tile in the file and no discs to reveal. Rebuilding them by revealing would lose the parts of the map the
    /// player has already seen.
    /// </para>
    /// <para>
    /// The masks are <b>not</b> stored in a save: the reference keeps them as derived data and recomputes them with
    /// <c>Maps::updateFogDirectionsInArea()</c> after a load (<c>game_startgame.cpp:863</c> for a blank map,
    /// <c>:922</c> and <c>interface_gamearea.cpp:869</c> for a player). The caller of this method is expected to
    /// follow the load with <see cref="UpdateDirections"/> for the colours it is about to draw.
    /// </para>
    /// </remarks>
    /// <param name="column">Column of the tile.</param>
    /// <param name="row">Row of the tile.</param>
    /// <param name="colors">The colors that have the tile under the fog.</param>
    internal void SetFog(int column, int row, PlayerColor colors) => _fog[(row * Width) + column] = (byte)colors;

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
                    int index = (y * Width) + x;
                    byte before = _fog[index];
                    byte after = (byte)(before & (byte)~colors);

                    if (after != before)
                    {
                        ++revealed;
                        _fog[index] = after;
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
    public void UpdateDirections(PlayerColor colors)
    {
        if (colors == PlayerColor.None)
        {
            // Nothing is fogged: no tile is undiscovered for a set of no colors, so the masks are all
            // unknown and the renderer draws no fog at all.
            Array.Clear(_directions);
            return;
        }

        for (int row = 0; row < Height; ++row)
        {
            for (int column = 0; column < Width; ++column)
            {
                _directions[(row * Width) + column] =
                    IsFogged(column, row, colors) ? ComputeDirection(column, row, colors) : Direction.Unknown;
            }
        }
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

    private Direction ComputeDirection(int column, int row, PlayerColor colors)
    {
        Direction direction = Direction.Center;

        if (IsFoggedOrOutside(column - 1, row - 1, colors))
        {
            direction |= Direction.TopLeft;
        }

        if (IsFoggedOrOutside(column, row - 1, colors))
        {
            direction |= Direction.Top;
        }

        if (IsFoggedOrOutside(column + 1, row - 1, colors))
        {
            direction |= Direction.TopRight;
        }

        if (IsFoggedOrOutside(column - 1, row, colors))
        {
            direction |= Direction.Left;
        }

        if (IsFoggedOrOutside(column + 1, row, colors))
        {
            direction |= Direction.Right;
        }

        if (IsFoggedOrOutside(column - 1, row + 1, colors))
        {
            direction |= Direction.BottomLeft;
        }

        if (IsFoggedOrOutside(column, row + 1, colors))
        {
            direction |= Direction.Bottom;
        }

        if (IsFoggedOrOutside(column + 1, row + 1, colors))
        {
            direction |= Direction.BottomRight;
        }

        return direction;
    }

    /// <summary>
    /// Whether a neighbour is under the fog, counting the tiles outside the map as fogged:
    /// <c>updateDirectionsInArea</c> fills its cache with <c>1</c> and leaves the entries outside
    /// the world untouched, so the fog of an edge tile reaches over the border.
    /// </summary>
    private bool IsFoggedOrOutside(int column, int row, PlayerColor colors) =>
        column < 0 || row < 0 || column >= Width || row >= Height || IsFogged(column, row, colors);
}
