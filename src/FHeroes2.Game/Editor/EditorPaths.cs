using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// Draws a road or a stream on a tile, the port of <c>setRoadOnTile</c> and <c>addStream</c>
/// (<c>参考：src/fheroes2/maps/map_format_helper.cpp:884</c> and <c>:1202</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps a table of objects and asks it which tiles hold a road; the port only has the
/// MP2 tiles, so "this tile has a road" is read off the sprites the way the reference's running game
/// does it (<c>Maps::Tile::isSpriteRoad</c>, <c>maps_tiles.cpp:801</c>): a road sprite is one of the
/// junction images, the images that connect two tiles are not — those are only a corner of somebody
/// else's road reaching into this tile.
/// </para>
/// <para>
/// A road is written as one object: the anchor tile gets the junction sprite and the neighbours get
/// the connecting corners. The pieces of one object share its UID, so changing the direction of a road
/// means taking it apart by UID and laying it again under the same UID — what the reference does in
/// <c>updateRoadOnTile</c> by rewinding <c>setLastObjectUID</c> before <c>setObjectOnTile</c>. A stream
/// is one tile big, so its direction is just the sprite index of that tile.
/// </para>
/// </remarks>
internal static class EditorPaths
{
    /// <summary>Index of the entrance road in <see cref="ObjectGroup.Roads"/>, <c>getRoadObjectIndex</c>.</summary>
    private const int CastleEntranceRoad = 512;

    /// <summary>The sprite index of "no stream", the <c>255</c> of <c>getStreamIndex</c>.</summary>
    private const byte NoStreamSprite = 255;

    /// <summary>
    /// How far a road can reach from its anchor: its corner pieces sit on the neighbouring tiles, so
    /// two tiles suffice, and that is also the reach of a neighbour that gets redrawn.
    /// </summary>
    private const int RoadRadius = 2;

    /// <summary>
    /// The road sprites the reference counts as a road, <c>Maps::Tile::isSpriteRoad</c>
    /// (<c>maps_tiles.cpp:805</c>). A tile that only shows a connecting corner is not a road.
    /// </summary>
    private static readonly HashSet<byte> RoadSprites =
    [
        0, 2, 3, 4, 5, 6, 7, 9, 12, 13, 14, 16, 17, 18, 19, 20, 21, 26, 28, 29, 30, 31, 32, 33, 34, 35,
        36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
    ];

    /// <summary>The four sides a stream connects over, <c>getStreamDirecton</c>.</summary>
    private static readonly Direction[] StreamSides = [Direction.Left, Direction.Top, Direction.Right, Direction.Bottom];

    /// <summary>
    /// Lays a road on <paramref name="tile"/> and redraws the roads around it,
    /// <c>setRoadOnTile</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tile holds one road: a tile that already has one is left alone, the way
    /// <c>placeNewRoadObjectOnTile</c> refuses a second road. After the new road is laid the eight
    /// neighbours are redrawn, because their direction masks changed the moment the new road touched
    /// them.
    /// </para>
    /// <para>
    /// The whole gesture lands in <paramref name="stroke"/> as one step: taking an old road apart and
    /// laying it again touches tiles of its own, and undoing once must bring all of them back.
    /// </para>
    /// </remarks>
    /// <param name="map">The map to draw on.</param>
    /// <param name="tile">Anchor tile of the road.</param>
    /// <param name="stroke">Stroke that records every tile the call changes.</param>
    /// <returns>Whether the map changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="stroke"/> is <see langword="null"/>.</exception>
    public static bool LayRoad(Mp2Map map, Point tile, MapStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stroke);

        if (!WithinMap(map, tile) || FindRoad(map, tile, out _, out _))
        {
            return false;
        }

        List<MapTileChange> window = Snapshot(map, tile, RoadRadius);

        if (!RandomMapObjectWriter.TryWriteRoad(map, tile, RoadObjectIndex(map, tile), map.TakeObjectUid()))
        {
            return false;
        }

        foreach (Direction direction in Directions.AllNeighboringDirections)
        {
            RedrawRoad(map, Neighbour(tile, direction));
        }

        return Record(map, window, stroke);
    }

    /// <summary>
    /// Draws a stream on <paramref name="tile"/> and redraws the streams around it, <c>addStream</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stream only runs over dry land that has none yet: water is refused and a tile that already
    /// carries a stream is skipped, both as in the reference. Dry land with embedded objects (flowers,
    /// rocks) loses them: a stream replaces the terrain image with the plain one, which is what the
    /// reference's <c>Ground::getRandomTerrainImageIndex(ground, false)</c> does. The port picks the
    /// plain image of the tile instead of a random one, so the same stroke always draws the same map.
    /// </para>
    /// <para>
    /// The four sides are redrawn, not the corners: a stream has no diagonal pieces.
    /// </para>
    /// </remarks>
    /// <param name="map">The map to draw on.</param>
    /// <param name="tile">Tile to draw the stream on.</param>
    /// <param name="stroke">Stroke that records every tile the call changes.</param>
    /// <returns>Whether the map changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="stroke"/> is <see langword="null"/>.</exception>
    public static bool LayStream(Mp2Map map, Point tile, MapStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stroke);

        if (!WithinMap(map, tile) || FindStream(map, tile, out _, out _) || IsWater(map, tile))
        {
            return false;
        }

        List<MapTileChange> window = Snapshot(map, tile, radius: 1);

        RedrawStream(map, tile, force: true);

        foreach (Direction side in StreamSides)
        {
            RedrawStream(map, Neighbour(tile, side), force: false);
        }

        StripEmbeddedObjects(map, tile);

        return Record(map, window, stroke);
    }

    /// <summary>
    /// The direction mask a road on <paramref name="tile"/> takes, <c>getRoadObjectIndex</c>: a bit per
    /// neighbouring road, or the entrance road when the tile above shows a castle.
    /// </summary>
    /// <remarks>
    /// The reference picks one of two layouts per mask at random; the port always takes the first of
    /// the two (<c>mask + 0</c>), so a stroke is reproducible and undo hands back the same sprites.
    /// </remarks>
    private static int RoadObjectIndex(Mp2Map map, Point tile)
    {
        if (tile.Y > 0 && HasGroundGroup(map, new Point(tile.X, tile.Y - 1), ObjectGroup.KingdomTowns))
        {
            // 城堡正上方那一格是门口，路面在这里换成引道的贴图。
            return CastleEntranceRoad;
        }

        Direction roads = Direction.Unknown;

        foreach (Direction direction in Directions.AllNeighboringDirections)
        {
            if (Neighbour(tile, direction) is { } around && FindRoad(map, around, out _, out _))
            {
                roads |= direction;
            }
        }

        return (int)roads;
    }

    /// <summary>Redraws one road whose direction mask may have changed, <c>updateRoadOnTile</c>.</summary>
    /// <remarks>
    /// The road is taken apart by UID and laid again under the same UID, which is how the reference
    /// keeps the identity of a road across an edit. A road that already shows the sprite its mask asks
    /// for is left untouched — rewriting it would give back an equal tile through a longer path.
    /// The caller records the tiles: it snapshots a window wide enough for this redraw, so a road that
    /// is touched twice within one step still has the value it had before the step.
    /// </remarks>
    private static void RedrawRoad(Mp2Map map, Point? tile)
    {
        if (tile is not { } point || !FindRoad(map, point, out uint uid, out _))
        {
            return;
        }

        int index = RoadObjectIndex(map, point);

        if (MatchesRoadLayout(map, point, index, uid))
        {
            return;
        }

        RandomMapObjectWriter.Remove(map, uid);
        RandomMapObjectWriter.TryWriteRoad(map, point, index, uid);
    }

    /// <summary>Whether the road of <paramref name="uid"/> already draws the layout of <paramref name="objectIndex"/>.</summary>
    /// <remarks>
    /// 不能只看锚点那一张：掩码 0 与掩码 8 的锚点都是第 2 帧，比贴图会把「该补接头」当成「已经对」。
    /// 这里按格比整件——三个格子里的面片连同贴图都要与目录里的那一件一一对上，多一张少一张都算不对。
    /// 道路面片的偏移都在一格之内，所以扫锚点周围 3x3 就够。
    /// </remarks>
    private static bool MatchesRoadLayout(Mp2Map map, Point tile, int objectIndex, uint uid)
    {
        ObjectInfo target = MapObjectCatalog.GetObjectInfo(ObjectGroup.Roads, objectIndex);

        for (int y = tile.Y - 1; y <= tile.Y + 1; ++y)
        {
            for (int x = tile.X - 1; x <= tile.X + 1; ++x)
            {
                List<byte> expected = [];

                foreach (LayeredObjectPartInfo part in target.GroundLevelParts)
                {
                    if (tile.X + part.TileOffset.X == x && tile.Y + part.TileOffset.Y == y)
                    {
                        expected.Add((byte)part.IcnIndex);
                    }
                }

                if (!expected.Order().SequenceEqual(RoadPiecesOf(map, new Point(x, y), uid).Order()))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The sprites of the road pieces a given object put on one tile.</summary>
    private static List<byte> RoadPiecesOf(Mp2Map map, Point tile, uint uid)
    {
        List<byte> sprites = [];

        if (!WithinMap(map, tile))
        {
            return sprites;
        }

        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (info.GroundIcnType == Mp2ObjectIcnType.Road && info.Level1ObjectUid == uid)
        {
            sprites.Add(info.BottomIcnImageIndex);
        }

        foreach (int index in map.EnumerateAddonChain(info))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.GroundIcnType == Mp2ObjectIcnType.Road && addon.Level1ObjectUid == uid)
            {
                sprites.Add(addon.BottomIcnImageIndex);
            }
        }

        return sprites;
    }

    /// <summary>Draws or redraws the stream of one tile, <c>updateStreamObjectOnMapTile</c>.</summary>
    /// <param name="map">The map.</param>
    /// <param name="tile">Tile to look at, ignored when outside the map.</param>
    /// <param name="force">Whether the tile gets a stream even when it has none (the tile under the pointer).</param>
    private static void RedrawStream(Mp2Map map, Point? tile, bool force)
    {
        if (tile is not { } point)
        {
            return;
        }

        Direction direction = StreamDirection(map, point, force);

        if (!Directions.Contains(direction, Direction.Center))
        {
            return;
        }

        byte sprite = StreamSprite(direction);

        if (sprite == NoStreamSprite)
        {
            return;
        }

        if (!FindStream(map, point, out _, out byte current))
        {
            RandomMapObjectWriter.TryWrite(map, point, ObjectGroup.Streams, sprite, map.TakeObjectUid());
            return;
        }

        if (current != sprite)
        {
            SetStreamSprite(map, point, sprite);
        }
    }

    /// <summary>The direction mask of a stream, <c>getStreamDirecton</c>.</summary>
    /// <remarks>
    /// The centre bit is set only when the tile itself carries a stream, which is what a caller that
    /// does not force the tile gets to check before touching it. The reference also looks at delta
    /// rivers here; the port has no delta layout, so its streams never touch one.
    /// </remarks>
    private static Direction StreamDirection(Mp2Map map, Point tile, bool force)
    {
        if (!force && !FindStream(map, tile, out _, out _))
        {
            return Direction.Unknown;
        }

        Direction direction = Direction.Center;

        foreach (Direction side in StreamSides)
        {
            if (Neighbour(tile, side) is { } around && FindStream(map, around, out _, out _))
            {
                direction |= side;
            }
        }

        return direction;
    }

    /// <summary>The stream sprite of a direction mask, <c>getStreamIndex</c>.</summary>
    /// <remarks>
    /// Two of the branches pick one of two sprites at random in the reference: a straight stretch has
    /// two drawings and a lone end has two as well. The port takes the first of each pair (2 and 3) for
    /// the same reason the road takes the first layout.
    /// </remarks>
    private static byte StreamSprite(Direction direction)
    {
        if (!Directions.Contains(direction, Direction.Center))
        {
            return NoStreamSprite;
        }

        if (Has(direction, Direction.Left | Direction.Bottom) && !HasAny(direction, Direction.Top | Direction.Right))
        {
            return 0;
        }

        if (Has(direction, Direction.Right | Direction.Bottom) && !HasAny(direction, Direction.Top | Direction.Left))
        {
            return 1;
        }

        if (Has(direction, Direction.Right | Direction.Top) && !HasAny(direction, Direction.Bottom | Direction.Left))
        {
            return 4;
        }

        if (Has(direction, Direction.Left | Direction.Top) && !HasAny(direction, Direction.Bottom | Direction.Right))
        {
            return 7;
        }

        if (Has(direction, Direction.Left | Direction.Top | Direction.Right) && !HasAny(direction, Direction.Bottom))
        {
            return 8;
        }

        if (Has(direction, Direction.Bottom | Direction.Top | Direction.Right) && !HasAny(direction, Direction.Left))
        {
            return 9;
        }

        if (Has(direction, Direction.Bottom | Direction.Top | Direction.Left) && !HasAny(direction, Direction.Right))
        {
            return 10;
        }

        if (Has(direction, Direction.Bottom | Direction.Left | Direction.Right) && !HasAny(direction, Direction.Top))
        {
            return 11;
        }

        if (Has(direction, Direction.Bottom | Direction.Left | Direction.Top | Direction.Right))
        {
            return 6;
        }

        if ((Has(direction, Direction.Left) || Has(direction, Direction.Right))
            && !HasAny(direction, Direction.Top | Direction.Bottom))
        {
            return 2;
        }

        return 3;
    }

    /// <summary>Replaces the terrain image of a tile that loses its embedded objects to a stream.</summary>
    /// <remarks>
    /// 参考在 addStream 里做的是同一件事：把带花鸟鱼虫的地形图换成「干净」的那一张，因为它画在河流底下会露出半截。
    /// 形状位一并清零，与地形笔刷画一格时的做法一致。
    /// </remarks>
    private static void StripEmbeddedObjects(Mp2Map map, Point tile)
    {
        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (!MapGrounds.ContainsEmbeddedObjects(info.TerrainImageIndex))
        {
            return;
        }

        MapGround ground = MapGrounds.FromTerrainImageIndex(info.TerrainImageIndex);

        if (ground == MapGround.Unknown)
        {
            return;
        }

        map.SetTile(tile.X, tile.Y, info with
        {
            TerrainImageIndex = TerrainTransitions.PlainImageIndex(ground, tile),
            TerrainFlags = 0,
        });
    }

    /// <summary>Finds the road of a tile and its sprite, <see langword="false"/> when it has none.</summary>
    /// <remarks>
    /// The road is not necessarily the main ground part of the tile: a decoration and a road share a
    /// tile, and the pieces are sorted by layer, so the road can sit in the add-on chain. Both places
    /// are looked at, and a connecting corner does not count as a road.
    /// </remarks>
    private static bool FindRoad(Mp2Map map, Point tile, out uint uid, out byte sprite)
    {
        uid = 0;
        sprite = 0;

        if (!WithinMap(map, tile))
        {
            return false;
        }

        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (info.GroundIcnType == Mp2ObjectIcnType.Road && RoadSprites.Contains(info.BottomIcnImageIndex))
        {
            uid = info.Level1ObjectUid;
            sprite = info.BottomIcnImageIndex;
            return true;
        }

        foreach (int index in map.EnumerateAddonChain(info))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.GroundIcnType == Mp2ObjectIcnType.Road && RoadSprites.Contains(addon.BottomIcnImageIndex))
            {
                uid = addon.Level1ObjectUid;
                sprite = addon.BottomIcnImageIndex;
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the stream of a tile and its sprite, <see langword="false"/> when it has none.</summary>
    private static bool FindStream(Mp2Map map, Point tile, out uint uid, out byte sprite)
    {
        uid = 0;
        sprite = 0;

        if (!WithinMap(map, tile))
        {
            return false;
        }

        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (info.GroundIcnType == Mp2ObjectIcnType.Stream)
        {
            uid = info.Level1ObjectUid;
            sprite = info.BottomIcnImageIndex;
            return true;
        }

        foreach (int index in map.EnumerateAddonChain(info))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.GroundIcnType == Mp2ObjectIcnType.Stream)
            {
                uid = addon.Level1ObjectUid;
                sprite = addon.BottomIcnImageIndex;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rewrites the sprite of the stream already on a tile, <c>updateTileObjectIcnIndex</c>: a stream is
    /// one piece, so it is either the main ground part or an add-on entry of the tile.
    /// </summary>
    private static bool SetStreamSprite(Mp2Map map, Point tile, byte sprite)
    {
        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (info.GroundIcnType == Mp2ObjectIcnType.Stream)
        {
            map.SetTile(tile.X, tile.Y, info with { BottomIcnImageIndex = sprite });
            return true;
        }

        foreach (int index in map.EnumerateAddonChain(info))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.GroundIcnType == Mp2ObjectIcnType.Stream)
            {
                map.SetAddonAt(index, addon with { BottomIcnImageIndex = sprite });
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a tile holds a ground piece of <paramref name="group"/>, main part or add-on.</summary>
    private static bool HasGroundGroup(Mp2Map map, Point tile, ObjectGroup group)
    {
        if (!WithinMap(map, tile))
        {
            return false;
        }

        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (MapObjectCatalog.GetGroundGroup(info) == group)
        {
            return true;
        }

        foreach (int index in map.EnumerateAddonChain(info))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (MapObjectCatalog.GetObjectGroupByIcn(addon.GroundIcnType, addon.BottomIcnImageIndex) == group)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the terrain of a tile is water, which a stream cannot cross.</summary>
    private static bool IsWater(Mp2Map map, Point tile) =>
        MapGrounds.FromTerrainImageIndex(map.Tile(tile.X, tile.Y).TerrainImageIndex) == MapGround.Water;

    /// <summary>The neighbour of a tile, <see langword="null"/> for the centre and for an unknown mask.</summary>
    private static Point? Neighbour(Point tile, Direction direction) => direction switch
    {
        Direction.TopLeft => new Point(tile.X - 1, tile.Y - 1),
        Direction.Top => new Point(tile.X, tile.Y - 1),
        Direction.TopRight => new Point(tile.X + 1, tile.Y - 1),
        Direction.Right => new Point(tile.X + 1, tile.Y),
        Direction.BottomRight => new Point(tile.X + 1, tile.Y + 1),
        Direction.Bottom => new Point(tile.X, tile.Y + 1),
        Direction.BottomLeft => new Point(tile.X - 1, tile.Y + 1),
        Direction.Left => new Point(tile.X - 1, tile.Y),
        _ => null,
    };

    /// <summary>Whether every bit of <paramref name="mask"/> is set, <c>hasBits</c>.</summary>
    private static bool Has(Direction direction, Direction mask) => (direction & mask) == mask;

    /// <summary>Whether any bit of <paramref name="mask"/> is set, <c>hasNoBits</c> negated.</summary>
    private static bool HasAny(Direction direction, Direction mask) => (direction & mask) != Direction.Unknown;

    /// <summary>The tiles a call can change, valued before the call, to be diffed afterwards.</summary>
    /// <remarks>
    /// 拆一条路要扫全图，改动的格子却只可能在它自己那一带：路面与接头面片都落在锚点旁边一格之内。于是先存下方圆
    /// <paramref name="radius"/> 格，做完再比一遍，比 <c>Remove</c> 的整图扫描便宜得多。
    /// </remarks>
    private static List<MapTileChange> Snapshot(Mp2Map map, Point tile, int radius)
    {
        List<MapTileChange> window = [];

        int left = Math.Max(0, tile.X - radius);
        int top = Math.Max(0, tile.Y - radius);
        int right = Math.Min(map.Width - 1, tile.X + radius);
        int bottom = Math.Min(map.Height - 1, tile.Y + radius);

        for (int y = top; y <= bottom; ++y)
        {
            for (int x = left; x <= right; ++x)
            {
                int index = map.TileIndex(x, y);
                Mp2TileInfo info = map.Tiles[index];
                window.Add(new MapTileChange(index, info, info));
            }
        }

        return window;
    }

    /// <summary>Records every tile of <paramref name="window"/> that the call changed.</summary>
    /// <returns>Whether anything changed.</returns>
    private static bool Record(Mp2Map map, List<MapTileChange> window, MapStroke stroke)
    {
        bool changed = false;

        foreach (MapTileChange snapshot in window)
        {
            Mp2TileInfo after = map.Tiles[snapshot.Index];

            if (after == snapshot.Before)
            {
                continue;
            }

            stroke.Record(snapshot.Index, snapshot.Before, after);
            changed = true;
        }

        return changed;
    }

    /// <summary>Whether a tile is inside the map.</summary>
    private static bool WithinMap(Mp2Map map, Point tile) =>
        tile.X >= 0 && tile.Y >= 0 && tile.X < map.Width && tile.Y < map.Height;
}
