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
    /// FogDiscoveryType::HEROES )</c>. The reference adds the scouting skill and the artifacts of the
    /// hero to it (<c>Heroes::GetScoutingDistance</c>); neither is parsed from an MP2 file yet, so
    /// every hero here is a level one hero without artifacts.
    /// </summary>
    public const int HeroRevealDistance = 4;

    /// <summary>
    /// Tiles around a castle that start without fog,
    /// <c>GameStatic::getFogDiscoveryDistance( FogDiscoveryType::CASTLE )</c>.
    /// </summary>
    public const int CastleRevealDistance = 5;

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
    /// Reveals the disc around a tile for <paramref name="colors"/> — <c>Maps::ClearFog</c>: every
    /// tile whose distance is below <see cref="SquaredRevealLimit"/> loses the fog of those colors.
    /// </summary>
    public void Reveal(int column, int row, int distance, PlayerColor colors)
    {
        int limit = SquaredRevealLimit(distance);
        int lastRow = Math.Min(Height - 1, row + distance);
        int lastColumn = Math.Min(Width - 1, column + distance);

        for (int y = Math.Max(0, row - distance); y <= lastRow; ++y)
        {
            int dy = y - row;
            for (int x = Math.Max(0, column - distance); x <= lastColumn; ++x)
            {
                int dx = x - column;
                if ((dx * dx) + (dy * dy) < limit)
                {
                    _fog[(y * Width) + x] &= (byte)~colors;
                }
            }
        }
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
    /// Builds the fog of a map as the game starts: everything fogged except the discs around the
    /// heroes of <paramref name="colors"/> (<c>AllHeroes::Scout</c>, which a scenario runs after
    /// loading).
    /// </summary>
    /// <remarks>
    /// The reference scouts around the castles too (<c>AllCastles::Scout</c>, with
    /// <see cref="CastleRevealDistance"/>), but an MP2 castle has no owner — the castle table of the
    /// format stores no color and <c>world_loadmap.cpp</c> creates every castle with
    /// <c>PlayerColor::NONE</c> — so this method has no castle to scout around. The caller reveals
    /// whatever tile it treats as the player's position instead, see <c>AdventureMapScene</c>.
    /// </remarks>
    public static MapFog CreateForMap(Mp2Map map, PlayerColor colors)
    {
        ArgumentNullException.ThrowIfNull(map);

        MapFog fog = new(map.Width, map.Height);
        foreach (Mp2MapHero hero in map.Heroes)
        {
            if ((hero.Color & colors) != 0)
            {
                fog.Reveal(hero.X, hero.Y, HeroRevealDistance, colors);
            }
        }

        fog.UpdateDirections(colors);
        return fog;
    }

    /// <summary>
    /// The squared distance limit of a reveal radius — <c>getSquaredScoutingRadiusLimit</c>, including
    /// its two exceptions for the high scouting values an artifact can reach.
    /// </summary>
    public static int SquaredRevealLimit(int distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        return distance switch
        {
            7 => 66,
            8 => 90,
            _ => (distance + 1) * (distance + 1) - distance,
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
