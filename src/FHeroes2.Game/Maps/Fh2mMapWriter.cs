using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Turns the editor's working map into the <c>.fh2m</c> format, the write half of
/// <c>map_format_info.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 三段拼起来就是一整个文件：头部（<see cref="Fh2mMapText.FromSpecs"/>，语言块与地图规格）、尾段
/// （<see cref="Fh2mPayload"/>，瓦片与十张元数据表）、以及把它们接起来的 <see cref="Fh2mBaseMap.Write"/>。
/// </para>
/// <para>
/// <b>瓦片上存的是 ICN 与帧号，格式里要的是组与组内下标</b>：这一步靠
/// <see cref="MapObjectCatalog.TryGetObjectIndex"/> 反查。查不到的对象（编辑器标了但目录里没有的组合）不写进去，
/// 由调用方通过 <paramref name="skippedObjects"/> 拿到条数——静默丢掉比写坏一张图好，但也不能一声不响。
/// </para>
/// </remarks>
public static class Fh2mMapWriter
{
    /// <summary>
    /// Builds the <c>.fh2m</c> image of a map.
    /// </summary>
    /// <param name="map">The map the editor is editing.</param>
    /// <param name="specs">The map specifications: header fields, rumours and daily events.</param>
    /// <param name="metadata">The object metadata tables of the editor.</param>
    /// <param name="skippedObjects">How many tile objects could not be addressed in the format.</param>
    /// <returns>The header, with the compressed payload already in place.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Fh2mBaseMap Build(Mp2Map map, MapSpecsState specs, MapObjectMetadataStore metadata,
        out int skippedObjects)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(metadata);

        skippedObjects = 0;

        // 对象 id 在整张图里必须唯一：地图自己的 UID 计数器从 0 开始时，从头编一遍。
        uint nextId = 1;
        List<Fh2mTile> tiles = new(map.Tiles.Count);

        foreach (Mp2TileInfo tile in map.Tiles)
        {
            List<Fh2mTileObject> objects = [];

            if (tile.HasGroundObject)
            {
                AddObject(tile.GroundIcnType, tile.BottomIcnImageIndex, tile.Level1ObjectUid, objects, ref nextId,
                    ref skippedObjects);
            }

            if (tile.HasTopObject)
            {
                AddObject(tile.TopIcnType, tile.TopIcnImageIndex, tile.Level2ObjectUid, objects, ref nextId,
                    ref skippedObjects);
            }

            tiles.Add(new Fh2mTile(tile.TerrainImageIndex, tile.TerrainFlags, objects));
        }

        Fh2mPayload payload = new()
        {
            Tiles = tiles,
            DailyEvents = [.. specs.DailyEvents],
            Rumors = [.. specs.Rumors],
            Castles = metadata.Castles,
            Heroes = metadata.Heroes,
            Sphinxes = metadata.Sphinxes,
            Signs = metadata.Signs,
            Events = metadata.AdventureMapEvents,
            Selections = metadata.Selections,
            Monsters = metadata.Monsters,
            Artifacts = metadata.Artifacts,
            Resources = metadata.Resources,
        };

        Fh2mBaseMap header = Fh2mMapText.FromSpecs(specs);

        return header with { Payload = payload.Write() };
    }

    /// <summary>
    /// Writes <paramref name="map"/> as an <c>.fh2m</c> file, <c>saveMap()</c> (<c>map_format_info.cpp:806-830</c>).
    /// </summary>
    /// <param name="filePath">Destination file; it is overwritten.</param>
    /// <param name="map">The map the editor is editing.</param>
    /// <param name="specs">The map specifications.</param>
    /// <param name="metadata">The object metadata tables of the editor.</param>
    /// <returns>How many tile objects could not be addressed in the format.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int Save(string filePath, Mp2Map map, MapSpecsState specs, MapObjectMetadataStore metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Fh2mBaseMap header = Build(map, specs, metadata, out int skippedObjects);

        header.WriteFile(filePath);

        if (skippedObjects > 0)
        {
            GameLog.Warn(
                $"'{filePath}' was written without {skippedObjects} object(s) that have no place in the .fh2m tables.");
        }

        // 参考的读取端有一条 512 字节的下限（minFileSize，map_format_info.cpp:105）；一张几乎全空的小图压完可能真比
        // 它短，于是写出的文件连参考自己也读不回来。端口不改这个格式约定，只把这件事说出来。
        long length = new FileInfo(filePath).Length;

        if (length < Fh2mBaseMap.MinimumFileSize)
        {
            GameLog.Warn(
                $"'{filePath}' is {length} bytes, below the {Fh2mBaseMap.MinimumFileSize} byte minimum of the "
                + "format; a reader following the reference will refuse it.");
        }

        return skippedObjects;
    }

    /// <summary>Translates one ICN image into its format address and appends it, when that address exists.</summary>
    /// <param name="icnType">ICN resource the tile stores.</param>
    /// <param name="icnIndex">Sprite index inside it.</param>
    /// <param name="uid">Object UID the tile stores, or 0 when it never got one.</param>
    /// <param name="objects">The list being built for this tile.</param>
    /// <param name="nextId">Running id allocator, for objects without a UID.</param>
    /// <param name="skipped">Counter of the objects that could not be addressed.</param>
    private static void AddObject(Mp2ObjectIcnType icnType, uint icnIndex, uint uid, List<Fh2mTileObject> objects,
        ref uint nextId, ref int skipped)
    {
        if (!MapObjectCatalog.TryGetObjectIndex(icnType, icnIndex, out MapObjectIndex address))
        {
            ++skipped;

            return;
        }

        uint id = uid != 0 && objects.TrueForAll(item => item.Id != uid) ? uid : nextId++;

        objects.Add(new Fh2mTileObject(id, (byte)address.Group, address.Index));
    }
}
