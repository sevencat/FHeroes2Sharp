using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

// "Army" is both a namespace (FHeroes2.Game.Army) and the type inside it, so the type needs an alias.
using ArmyUnit = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game;

/// <summary>
/// The scans and the guard queries of <c>src/fheroes2/maps/maps.cpp:56-182</c> and <c>:570-689</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two families live here because they answer the same kind of question — "what is around this tile" —
/// and both are needed by the spells (<c>Dimension Door</c>, <c>View Heroes</c>), the AI and the
/// adventure map actions: the <b>scans</b> that look for an object type in a neighbourhood, and the
/// <b>guards</b> that say which wandering monsters would attack a hero who interferes with a tile.
/// </para>
/// <para>
/// Everything in this file reads <see cref="Tile.GetMainObjectType(World, bool)"/> and
/// <see cref="Tile.IsPassableFrom(Direction)"/>, which is why it could only arrive after those two and
/// after the object classification of <c>Mp2ObjectTypes</c>.
/// </para>
/// <para>
/// <b>A wandering monster is <see cref="Mp2ObjectType.ActionMonster"/>, not
/// <see cref="Mp2ObjectType.Monster"/>.</b> The reference compares against <c>OBJ_MONSTER</c>, which carries
/// the action bit like every other object a hero interacts with; the base type <c>Monster</c> never reaches
/// a tile. The same trap applies to the other <c>Action*</c> pairs.
/// </para>
/// <para>
/// <c>Maps::isTileProtectionStrongerThan()</c> (<c>maps.cpp:644</c>) is missing on purpose: it builds an
/// <c>Army</c> from the monster tile to compare its strength, and <c>Army</c> is not ported yet. The
/// caller that needs it (the AI planner) cannot exist before the army does.
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// The neighbours of a tile that hold <paramref name="objectType"/>,
    /// <c>Maps::ScanAroundObject()</c> (<c>maps.cpp:570</c> and <c>:588</c>).
    /// </summary>
    /// <remarks>
    /// <b>The radius is one tile</b>, not a parameter: the reference has a separate
    /// <see cref="ScanAroundObjectWithDistance"/> for the wider search. The reference's two overloads
    /// differ only in the default of <paramref name="ignoreHeroes"/>, so the port keeps one method.
    /// </remarks>
    /// <param name="center">Index of the centre tile.</param>
    /// <param name="objectType">Object type to look for.</param>
    /// <param name="ignoreHeroes">
    /// <see langword="true"/> to look through a hero standing on the neighbour (the reference default);
    /// <see langword="false"/> to compare the type the hero covers.
    /// </param>
    /// <returns>The indexes of the matching neighbours, in the order of <see cref="MapGeometry.GetAroundIndexes(int, int)"/>.</returns>
    public List<int> ScanAroundObject(int center, Mp2ObjectType objectType, bool ignoreHeroes = true)
    {
        List<int> neighbours = MapGeometry.GetAroundIndexes(center, Width, Height, 1);

        var result = new List<int>();

        foreach (int index in neighbours)
        {
            if (GetTile(index).GetMainObjectType(this, !ignoreHeroes) == objectType)
            {
                result.Add(index);
            }
        }

        return result;
    }

    /// <summary>
    /// Whether any neighbour of a tile holds <paramref name="objectType"/>,
    /// <c>Maps::hasNearbyObject()</c> (<c>maps.cpp:576</c>).
    /// </summary>
    /// <remarks>
    /// This one stops at the first match and always looks through a hero (<c>getMainObjectType()</c>
    /// without an argument), unlike <see cref="ScanAroundObject"/>.
    /// </remarks>
    /// <param name="center">Index of the centre tile.</param>
    /// <param name="objectType">Object type to look for.</param>
    /// <returns><see langword="true"/> when at least one neighbour carries the object.</returns>
    public bool HasNearbyObject(int center, Mp2ObjectType objectType)
    {
        foreach (int index in MapGeometry.GetAroundIndexes(center, Width, Height, 1))
        {
            if (GetTile(index).GetMainObjectType(this) == objectType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a hero may teleport onto a tile — <c>Maps::isValidForDimensionDoor()</c>
    /// (<c>maps.cpp:582</c>).
    /// </summary>
    /// <remarks>
    /// Three independent conditions, and all three matter: the tile has to be enterable at all (its own
    /// direction bit), it has to be on the same side of the water as the hero, and it must not carry an
    /// object the hero would have to interact with — the reference asks
    /// <c>isInGameActionObject()</c> and not "is it empty", so a road or a shadow is fine. The object type
    /// is read <b>through</b> a hero (<c>getMainObjectType( true )</c>), so a hero standing on the target
    /// does not make the tile invalid.
    /// </remarks>
    /// <param name="targetIndex">Index of the tile to teleport to.</param>
    /// <param name="isWater">Whether the travelling hero is on the water.</param>
    /// <returns><see langword="true"/> when the tile is a legal landing tile.</returns>
    public bool IsValidForDimensionDoor(int targetIndex, bool isWater)
    {
        Tile tile = GetTile(targetIndex);

        return tile.IsPassableFrom(Direction.Center)
            && isWater == tile.IsWater
            && !Mp2ObjectTypes.IsInGameActionObject(tile.GetMainObjectType(this));
    }

    /// <summary>
    /// The tiles within a square around the centre that hold <paramref name="objectType"/>, nearest
    /// first, <c>Maps::ScanAroundObjectWithDistance()</c> (<c>maps.cpp:594</c>).
    /// </summary>
    /// <remarks>
    /// The square is the Chebyshev neighbourhood of <paramref name="distance"/> — the reference sorts the
    /// candidates by their <b>squared</b> euclidean distance and only then filters them, so the result is
    /// ordered by distance and never contains the centre tile. Ties come out in an unspecified order:
    /// <c>std::sort</c> is not stable and neither is <see cref="List{T}.Sort()"/>.
    /// </remarks>
    /// <param name="center">Index of the centre tile.</param>
    /// <param name="distance">Radius of the square in tiles, at least 1.</param>
    /// <param name="objectType">Object type to look for.</param>
    /// <returns>The indexes of the matches, nearest first.</returns>
    public List<int> ScanAroundObjectWithDistance(int center, int distance, Mp2ObjectType objectType)
    {
        List<int> candidates = MapGeometry.GetAroundIndexes(center, Width, Height, distance);

        SortByDistance(candidates, center);

        var result = new List<int>();

        foreach (int index in candidates)
        {
            if (GetTile(index).GetMainObjectType(this) == objectType)
            {
                result.Add(index);
            }
        }

        return result;
    }

    /// <summary>
    /// Every tile of the map that holds <paramref name="objectType"/>, nearest to
    /// <paramref name="center"/> first, <c>Maps::GetObjectPositions( center, objectType, ignoreHeroes )</c>
    /// (<c>maps.cpp:632</c>).
    /// </summary>
    /// <remarks>
    /// The whole-map scan of <see cref="GetObjectPositions(Mp2ObjectType, bool)"/> with the distance sort
    /// of <see cref="ScanAroundObjectWithDistance"/> on top; the reference is explicit that the sorted
    /// version is the slow one and that <see cref="GetObjectPositions(Mp2ObjectType, bool)"/> is what to
    /// use when the order does not matter.
    /// </remarks>
    /// <param name="center">Index of the tile the distances are measured from.</param>
    /// <param name="objectType">Object type to look for.</param>
    /// <param name="ignoreHeroes">
    /// <see langword="true"/> to look through a hero standing on the tile (the reference default).
    /// </param>
    /// <returns>The matching tile indexes, nearest first.</returns>
    public List<int> GetObjectPositions(int center, Mp2ObjectType objectType, bool ignoreHeroes)
    {
        List<int> result = GetObjectPositions(objectType, ignoreHeroes);

        SortByDistance(result, center);

        return result;
    }

    /// <summary>
    /// The monsters that would attack a hero who interferes with a tile,
    /// <c>Maps::getMonstersProtectingTile()</c> (<c>maps.cpp:664</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A tile that is interacted with from the front has no guards.</b> The reference asks
    /// <see cref="Mp2ObjectTypes.IsNeedStayFront"/> first: an object that is used while standing in front
    /// of it (a windmill, a treasure chest) never starts a fight — unless the "object" is the wandering
    /// monster itself, in which case the monsters stand <b>on</b> the tile and the tile is its own guard.
    /// That early return is why the answer is not simply "the monsters around the tile".
    /// </para>
    /// <para>
    /// <paramref name="checkObjectOnTile"/> is exposed because the caller that only needs the map-side
    /// guards (the AI estimating a route) has already decided that the object on the tile is harmless.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Index of the tile the hero interferes with.</param>
    /// <param name="checkObjectOnTile">
    /// <see langword="false"/> to skip the "interacted from the front" rule and look for the guards
    /// regardless.
    /// </param>
    /// <returns>Indexes of the guarding monsters; empty for an index outside the map.</returns>
    public List<int> GetMonstersProtectingTile(int tileIndex, bool checkObjectOnTile = true)
    {
        if (tileIndex < 0 || tileIndex >= _tiles.Count)
        {
            return [];
        }

        Tile tile = GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetMainObjectType(this);

        // A monster stands on the tile itself: it is its own guard.
        if (checkObjectOnTile && Mp2ObjectTypes.IsNeedStayFront(objectType))
        {
            return objectType == Mp2ObjectType.ActionMonster ? [tileIndex] : [];
        }

        var result = new List<int>(9);

        ForEachMonsterProtectingTile(tileIndex, result.Add);

        return result;
    }

    /// <summary>
    /// Whether a tile is guarded by monsters — <c>Maps::isTileUnderProtection()</c>
    /// (<c>maps.cpp:639</c>).
    /// </summary>
    /// <remarks>
    /// The centre tile is checked first: a monster standing on the tile protects it even when the tile
    /// carries an object that is normally interacted with from the front.
    /// </remarks>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns><see langword="true"/> when a fight would start.</returns>
    public bool IsTileUnderProtection(int tileIndex) =>
        GetTile(tileIndex).GetMainObjectType(this) == Mp2ObjectType.ActionMonster
        || GetMonstersProtectingTile(tileIndex).Count > 0;

    /// <summary>
    /// Whether some monster that guards a tile is stronger than <paramref name="strength"/>,
    /// <c>Maps::isTileProtectionStrongerThan()</c> (<c>maps.cpp:644</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AI asks this to decide whether it may walk onto a guarded object at all: a guard that is
    /// stronger than the army it brings means the tile stays unreachable, and the path is not built
    /// through it.
    /// </para>
    /// <para>
    /// Every guard is compared, not just the first — the reference does not stop at the first hit either,
    /// so a tile protected by two stacks costs two strength computations. A monster on the tile itself
    /// counts as its own guard here, which is why this walks
    /// <see cref="ForEachMonsterProtectingTile"/> and not <see cref="GetMonstersProtectingTile(int, bool)"/>
    /// with its "objects that are attacked from the front are their own guard" shortcut: the AI already
    /// made that decision before calling this.
    /// </para>
    /// <para>
    /// The comparison is strict (<c>&gt;</c>): a guard exactly as strong as the army does not stop it,
    /// because the fight is resolved elsewhere.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Index of the tile to test.</param>
    /// <param name="strength">Strength of the army that wants to take the tile.</param>
    /// <returns><see langword="true"/> when at least one guard is stronger.</returns>
    public bool IsTileProtectionStrongerThan(int tileIndex, double strength)
    {
        bool isStronger = false;
        ArmyUnit tileArmy = new();

        ForEachMonsterProtectingTile(tileIndex, monsterTileIndex =>
        {
            tileArmy.SetFromTile(this, GetTile(monsterTileIndex));

            if (tileArmy.GetStrength() > strength)
            {
                isStronger = true;
            }
        });

        return isStronger;
    }

    /// <summary>
    /// Calls <paramref name="action"/> with the index of every monster that protects a tile,
    /// <c>forEachMonsterProtectingTile()</c> (<c>maps.cpp:100</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The eight neighbours are visited in a fixed order</b> — the row above from left to right, the
    /// left neighbour, the tile itself, the right neighbour, then the row below. The port keeps that order
    /// instead of walking <see cref="MapGeometry.GetAroundIndexes(int, int)"/>, which yields the same
    /// tiles but not in the same sequence: callers that stop at the first hit would otherwise pick a
    /// different monster than the reference does.
    /// </para>
    /// <para>
    /// A monster only protects a tile it could walk into. The reference checks the passability of both
    /// tiles along the connecting direction, and then the four extra cases where the monster comes
    /// diagonally: the <see cref="Direction.Left"/> or <see cref="Direction.Right"/> bit of the monster's
    /// tile plus the vertical bit of the target one, because a hero who leaves the tile vertically and a
    /// monster who leaves its own horizontally still meet. The literal comparison against
    /// <c>TOP_LEFT</c>/<c>TOP_RIGHT</c>/<c>BOTTOM_RIGHT</c>/<c>BOTTOM_LEFT</c> is kept as it is — those four
    /// are exactly the diagonals, so testing the bit instead would change nothing.
    /// </para>
    /// <para>
    /// An index outside the map is ignored here; the reference leaves the check to its two callers, both
    /// of which do it.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Index of the tile the hero interferes with.</param>
    /// <param name="action">Called once per guarding monster, in the order described above.</param>
    public void ForEachMonsterProtectingTile(int tileIndex, Action<int> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (tileIndex < 0 || tileIndex >= _tiles.Count)
        {
            return;
        }

        int width = Width;
        int x = tileIndex % width;
        int y = tileIndex / width;
        Tile tile = GetTile(tileIndex);
        MapGeometry geometry = new(width, Height);

        bool IsProtectedBy(int monsterTileIndex)
        {
            Tile monsterTile = GetTile(monsterTileIndex);

            if (monsterTile.GetMainObjectType(this) != Mp2ObjectType.ActionMonster || tile.IsWater != monsterTile.IsWater)
            {
                return false;
            }

            Direction directionToMonster = geometry.GetDirection(tileIndex, monsterTileIndex);
            Direction directionFromMonster = Directions.Reflect(directionToMonster);

            // The monster can step onto the tile.
            if (tile.IsPassableFrom(directionToMonster) && monsterTile.IsPassableFrom(directionFromMonster))
            {
                return true;
            }

            // The tile is not directly reachable, but the diagonal meeting still happens: the hero leaves
            // the tile vertically while the monster leaves its own tile horizontally.
            return directionFromMonster switch
            {
                Direction.TopLeft => tile.IsPassableFrom(Direction.Bottom) && monsterTile.IsPassableFrom(Direction.Left),
                Direction.TopRight => tile.IsPassableFrom(Direction.Bottom) && monsterTile.IsPassableFrom(Direction.Right),
                Direction.BottomRight => tile.IsPassableFrom(Direction.Top) && monsterTile.IsPassableFrom(Direction.Right),
                Direction.BottomLeft => tile.IsPassableFrom(Direction.Top) && monsterTile.IsPassableFrom(Direction.Left),
                _ => false,
            };
        }

        void Visit(int monsterTileIndex)
        {
            if (IsProtectedBy(monsterTileIndex))
            {
                action(monsterTileIndex);
            }
        }

        if (y > 0)
        {
            if (x > 0)
            {
                Visit(tileIndex - width - 1);
            }

            Visit(tileIndex - width);

            if (x < width - 1)
            {
                Visit(tileIndex - width + 1);
            }
        }

        if (x > 0)
        {
            Visit(tileIndex - 1);
        }

        if (tile.GetMainObjectType(this) == Mp2ObjectType.ActionMonster)
        {
            Visit(tileIndex);
        }

        if (x < width - 1)
        {
            Visit(tileIndex + 1);
        }

        if (y < Height - 1)
        {
            if (x > 0)
            {
                Visit(tileIndex + width - 1);
            }

            Visit(tileIndex + width);

            if (x < width - 1)
            {
                Visit(tileIndex + width + 1);
            }
        }
    }

    /// <summary>
    /// Sorts tile indexes by their squared distance from <paramref name="center"/>, in place,
    /// <c>ComparisonDistance</c> (<c>maps.cpp:184</c>).
    /// </summary>
    /// <remarks>
    /// <c>ComparisonDistance</c> builds a point once in its constructor and then keeps <c>GetPoint()</c> of
    /// both compared indexes; the port reads the two points of the pair it is asked about, which is the
    /// same arithmetic without a stateful comparer. Ties keep an unspecified order, like the reference.
    /// </remarks>
    private void SortByDistance(List<int> indexes, int center)
    {
        MapGeometry geometry = Geometry ?? throw new InvalidOperationException("The world has no map yet.");

        indexes.Sort((left, right) => geometry.GetSquaredDistance(center, left).CompareTo(geometry.GetSquaredDistance(center, right)));
    }
}
