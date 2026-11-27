using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The tiles of an <see cref="Mp2Map"/> without a world behind them: what the map editor has instead of
/// <see cref="World"/>. It builds the very same <see cref="Tile"/> objects <see cref="World.LoadMap"/> does
/// and computes their directional passability, so the passability overlay of the editor runs the reference
/// algorithm (<c>maps_tiles.cpp:583-800</c>) instead of a second implementation of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is missing compared to <see cref="World"/>.</b> No kingdoms, heroes, castles, spells or pathfinder.
/// <see cref="GetHero"/> therefore always answers <see langword="null"/>: the tiles of an editor map carry no
/// occupant hero, the editor keeps its heroes in a list of its own. The one consequence is a tile whose stored
/// type is <see cref="Mp2ObjectType.ActionHero"/> — a hero marker left by a map file — reads as an empty tile,
/// where the reference looks through the hero to the object it covers.
/// </para>
/// <para>
/// <b>Built like <see cref="World.LoadMap"/>.</b> Per tile: the terrain, <see cref="Tile.Init"/>, the add-on
/// chain, <see cref="Tile.SortObjectParts"/>, then the object type repairs; then two passes over all tiles for
/// the passability (<see cref="World.UpdatePassabilities"/>).
/// </para>
/// </remarks>
public sealed class MapTileSet : ITileNeighbourhood
{
    private readonly Tile[] _tiles;

    private MapTileSet(Mp2Map map)
    {
        _tiles = new Tile[map.Tiles.Count];
        Geometry = new MapGeometry(map.Width, map.Height);
    }

    /// <summary>Builds the tiles of a map and computes their passability.</summary>
    /// <param name="map">A map that was read from a file or created blank.</param>
    /// <returns>The tile set, ready to answer <see cref="GetTile"/> and <see cref="Tile.PassableDirections"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The dimensions of <paramref name="map"/> do not match its tile count.
    /// </exception>
    public static MapTileSet Create(Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (map.Width <= 0 || map.Height <= 0 || (long)map.Width * map.Height != map.Tiles.Count)
        {
            throw new ArgumentException(
                $"The map is {map.Width}x{map.Height} but holds {map.Tiles.Count} tiles.", nameof(map));
        }

        var set = new MapTileSet(map);

        for (int index = 0; index < map.Tiles.Count; ++index)
        {
            set.RebuildTile(map, index);
        }

        // The object type repairs of the loader (world_loadmap.cpp:1415 -> maps_tiles.cpp:1321): they change
        // what the passability has to be, the reefs fix in particular.
        foreach (Tile tile in set._tiles)
        {
            tile.FixMP2MapTileObjectType(set);
        }

        set.UpdatePassabilities();

        return set;
    }

    /// <summary>The size of the map.</summary>
    public MapGeometry? Geometry { get; }

    /// <summary>The tile at <paramref name="tileIndex"/>.</summary>
    /// <param name="tileIndex">Index of the tile, row major.</param>
    /// <returns>The tile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the map.</exception>
    public Tile GetTile(int tileIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileIndex);

        if (tileIndex >= _tiles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tileIndex), tileIndex, "The index is outside the map.");
        }

        return _tiles[tileIndex];
    }

    /// <summary>No hero ever stands on the tiles of an editor map.</summary>
    /// <param name="heroId">Id of the hero, ignored.</param>
    /// <returns>Always <see langword="null"/>.</returns>
    public Hero? GetHero(int heroId) => null;

    /// <summary>The editor tiles carry no ground events of the game.</summary>
    /// <param name="tileIndex">Index of the tile, ignored.</param>
    /// <returns>Always <see langword="null"/>.</returns>
    public MapEvent? GetMapEvent(int tileIndex) => null;

    /// <summary>
    /// Rebuilds one tile after the editor changed it and recomputes the passability of it and of its eight
    /// neighbours, the refresh the reference does after an object part is removed
    /// (<c>maps_tiles.cpp:1479-1493</c>).
    /// </summary>
    /// <remarks>
    /// 邻居也要重算：它们的方向标记会读这一格的主物件类型与地块独立性通行性——左边、右边的高物件决定上行斜向，
    /// 下面的物件决定整格能不能进。参考在同一处对 <c>getAroundIndexes( _index, 1 )</c> 的每一格做了同样的两步。
    /// </remarks>
    /// <param name="map">The map as it is now; <see cref="Mp2Map"/> is a record, an edit produces a new one.</param>
    /// <param name="tileIndex">Index of the tile that changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the map.</exception>
    public void Refresh(Mp2Map map, int tileIndex)
    {
        ArgumentNullException.ThrowIfNull(map);

        RebuildTile(map, tileIndex);

        // 取重建之后的那一个：RebuildTile 换掉的是数组里的一格，旧的 Tile 对象已经不在图上了。
        Tile tile = GetTile(tileIndex);

        List<int> around = Geometry?.GetAroundIndexes(tileIndex, 1) ?? [];

        // Two passes like UpdatePassabilities: every tile of the neighbourhood first resets its mask to the
        // tile independent value, then each narrows it down by its neighbours.
        foreach (int index in around)
        {
            _tiles[index].SetInitialPassability();
        }

        tile.SetInitialPassability();

        foreach (int index in around)
        {
            _tiles[index].UpdatePassability(this);
        }

        tile.UpdatePassability(this);
    }


    /// <summary>Replaces one tile by a fresh build from the map, <c>Tile::Init</c> and its add-on chain.</summary>
    private void RebuildTile(Mp2Map map, int index)
    {
        Mp2TileInfo mp2Tile = map.Tiles[index];

        var tile = new Tile();

        tile.Index = index;
        tile.SetTerrain(mp2Tile.TerrainImageIndex, mp2Tile.TerrainFlags);
        tile.Init(mp2Tile);

        foreach (int addonIndex in map.EnumerateAddonChain(mp2Tile))
        {
            Mp2AddonInfo addon = map.Addons[addonIndex];

            if (addon.GroundIcnType != Mp2ObjectIcnType.Unknown)
            {
                tile.PushGroundObjectPart(new ObjectPart(
                    (ObjectLayerType)addon.GroundLayer, addon.Level1ObjectUid, addon.GroundIcnType, addon.BottomIcnImageIndex));
            }

            if (addon.TopIcnType != Mp2ObjectIcnType.Unknown)
            {
                tile.PushTopObjectPart(new ObjectPart(
                    ObjectLayerType.Object, addon.Level2ObjectUid, addon.TopIcnType, addon.TopIcnImageIndex));
            }
        }

        tile.SortObjectParts();

        _tiles[index] = tile;
    }

    /// <summary>The two passes of <see cref="World.UpdatePassabilities"/>.</summary>
    private void UpdatePassabilities()
    {
        foreach (Tile tile in _tiles)
        {
            if (tile.MainObjectType == Mp2ObjectType.None)
            {
                tile.UpdateObjectType(this);
            }

            tile.SetInitialPassability();
        }

        foreach (Tile tile in _tiles)
        {
            tile.UpdatePassability(this);
        }
    }
}
