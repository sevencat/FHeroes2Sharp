using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Writes one catalogue object onto an <see cref="Mp2Map"/>, the port of <c>putObjectOnMap()</c> +
/// <c>Maps::setObjectOnTile()</c> + <c>placeObjectOnTile()</c> (<c>map_random_generator_helper.cpp:761</c>,
/// <c>maps_tiles_helper.cpp:2214</c>, <c>:195</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要一个新原语</b>：参考的落子链写在**运行时 <c>Tile</c> 的 <c>ObjectPart</c> 列表**上，MP2 字段是存档时
/// 由 <c>Map_Format</c> 的对象表反推出来的。端口没有 <c>Map_Format</c>：编辑器直接写 <see cref="Mp2TileInfo"/>。所以生成器
/// 这一侧要自己把「物件目录里的一件东西」翻译成瓦片的底/顶面片与附加链。
/// </para>
/// <para>
/// <b>目标形状由读入侧决定</b>：<c>Tile.Init()</c> 从瓦片取一个底面片（<see cref="Mp2TileInfo.ObjectName1"/>、
/// <see cref="Mp2TileInfo.BottomIcnImageIndex"/>、层在 <see cref="Mp2TileInfo.Quantity1"/> 的低两位）和一个顶面片，
/// 再把 <see cref="Mp2Map.EnumerateAddonChain"/> 里的每一件追加成额外的底面片/顶面片，最后 `SortObjectParts()`。
/// 故「一格最多一个底 + 一个顶，其余进附加链」就是本类的全部规则。
/// </para>
/// <para>
/// <b>并进已有装饰</b>：参考的 <c>placeObjectOnTile</c> 会把新面片塞进已有格子并重新排序，而不是覆盖它。这里跟着做：
/// 目标格的 <see cref="Mp2TileInfo.ObjectType"/> 为空（哪怕已经有装饰面片）就能写，原有的面片会被读回来、与新面片一起
/// 按层号排好再落盘。宝物精灵的边缘常常溢到锚点上，守卫就站在这样的格里；覆盖它们会在地图上留下半截精灵。
/// 有类型的格子（已经站着一件东西）仍然拒绝，因为那说明落子规则没生效，属于调用方的错。
/// </para>
/// </remarks>
public static class RandomMapObjectWriter
{
    /// <summary>Writes the object <paramref name="objectIndex"/> of <paramref name="group"/> at <paramref name="position"/>.</summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="position">Anchor tile of the object.</param>
    /// <param name="group">Which catalogue.</param>
    /// <param name="objectIndex">Which entry of that catalogue.</param>
    /// <returns><see langword="true"/> when the object was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static bool TryWrite(Mp2Map map, in Point position, ObjectGroup group, int objectIndex)
    {
        ArgumentNullException.ThrowIfNull(map);

        return RandomMapObjectRules.TryGetObjectInfo(group, objectIndex, out ObjectInfo info)
            && TryWrite(map, position, info);
    }

    /// <summary>Writes the object <paramref name="objectIndex"/> of <paramref name="group"/> under a caller chosen UID.</summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="position">Anchor tile of the object.</param>
    /// <param name="group">Which catalogue.</param>
    /// <param name="objectIndex">Which entry of that catalogue.</param>
    /// <param name="uid">The object id every piece of this write carries.</param>
    /// <returns><see langword="true"/> when the object was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static bool TryWrite(Mp2Map map, in Point position, ObjectGroup group, int objectIndex, uint uid)
    {
        ArgumentNullException.ThrowIfNull(map);

        return RandomMapObjectRules.TryGetObjectInfo(group, objectIndex, out ObjectInfo info)
            && TryWrite(map, position, info, uid);
    }

    /// <summary>Writes the road <paramref name="objectIndex"/> at <paramref name="position"/>, clipping it to the map.</summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="position">Anchor tile of the road.</param>
    /// <param name="objectIndex">Which entry of <see cref="ObjectGroup.Roads"/> to place.</param>
    /// <param name="uid">The object id every piece of this write carries.</param>
    /// <returns><see langword="true"/> when the road was written.</returns>
    /// <remarks>
    /// <para>
    /// 路面是唯一会「合理地」伸出地图的物件：一个方向掩码带的接头面片最多可以铺到锚点周围五格，而地图的最后一排照样要
    /// 有路。参考在这里写的是「Ignore this tile since it is out of the map」——伸出去的那几片不画，其余照常落。
    /// 其它物件伸出去仍然是错，故只有这一个入口放宽。
    /// </para>
    /// <para>
    /// 锚点本身仍必须在地图里：那是这条路的身份所在的格子。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static bool TryWriteRoad(Mp2Map map, in Point position, int objectIndex, uint uid)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!WithinMap(map, position)
            || !RandomMapObjectRules.TryGetObjectInfo(ObjectGroup.Roads, objectIndex, out ObjectInfo info))
        {
            return false;
        }

        return TryWriteCore(map, position, info, uid, clipToMap: true);
    }

    /// <summary>Writes <paramref name="info"/> at <paramref name="position"/>.</summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="position">Anchor tile of the object.</param>
    /// <param name="info">The object, with its ground and top level parts.</param>
    /// <returns><see langword="true"/> when the object was written.</returns>
    /// <remarks>
    /// 一件物件只吃一个 UID（<see cref="Mp2Map.TakeObjectUid"/>），底面片与顶面片、以及每个附加件都带它——参考在
    /// <c>placeObjectOnTile</c> 里就是这么做的，这样读回时这件东西还是一个整体。瓦片的 13 位 metadata 按
    /// <see cref="MetadataOf"/> 的类型分支写，因为目录里的 <see cref="ObjectInfo.Metadata"/> 一半是对象数据（怪物编号、
    /// 旗帜颜色），不是文件字段。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="info"/> is <see langword="null"/>.</exception>
    public static bool TryWrite(Mp2Map map, in Point position, ObjectInfo info)
    {
        ArgumentNullException.ThrowIfNull(map);

        return TryWriteCore(map, position, info, uid: null, clipToMap: false);
    }

    /// <summary>Writes <paramref name="info"/> at <paramref name="position"/> under a caller chosen UID.</summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="position">Anchor tile of the object.</param>
    /// <param name="info">The object, with its ground and top level parts.</param>
    /// <param name="uid">The object id every piece of this write carries.</param>
    /// <returns><see langword="true"/> when the object was written.</returns>
    /// <remarks>
    /// 城堡这类由好几件「物件」拼成的东西在参考里共用一个 UID（<c>placeCastle</c> 把计数器退回去再写下一件），
    /// 所以调用方要能把同一个 id 传进来；普通的单件落子用上面那个重载，自己领一个新号。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="info"/> is <see langword="null"/>.</exception>
    public static bool TryWrite(Mp2Map map, in Point position, ObjectInfo info, uint uid)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(info);

        return TryWriteCore(map, position, info, uid, clipToMap: false);
    }

    /// <summary>The one write path: validates, then takes a UID only when it is about to write.</summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="position">Anchor tile of the object.</param>
    /// <param name="info">The object.</param>
    /// <param name="uid">The object id to use, or <see langword="null"/> to take the next one.</param>
    /// <param name="clipToMap">Whether the parts outside the map are dropped instead of rejecting the object.</param>
    /// <returns><see langword="true"/> when the object was written.</returns>
    /// <remarks>
    /// 校验全在领号之前：被拒绝的落子不该弄脏 <see cref="Mp2Map.LastObjectUid"/>，否则同一个种子会生成出不同的图。
    /// </remarks>
    private static bool TryWriteCore(Mp2Map map, in Point position, ObjectInfo info, uint? uid, bool clipToMap)
    {
        if (info.IsEmpty)
        {
            GameLog.Warn("The random map generator tried to write an object that has no ground part.");

            return false;
        }

        List<TileCells> cells = Collect(position, info);
        int coveredCells = cells.Count;

        cells.RemoveAll(cell => !WithinMap(map, cell.Point));

        if (cells.Count < coveredCells && !clipToMap)
        {
            GameLog.Warn($"An object at {position} reaches outside the {map.Width}x{map.Height} map.");

            return false;
        }

        if (cells.Count == 0)
        {
            GameLog.Warn($"Nothing of the object at {position} lands inside the {map.Width}x{map.Height} map.");

            return false;
        }

        // 参考 putObjectOnMap() 的两条前置（:764-786）只看**锚点那一格**：一格不许站两个动作物件，
        // 同一类可交互物件也不重复放（setObjectOnTile 不是幂等的）。覆盖到的其它格一律合并——
        // 参考就是这样让一件宝物的影子落在邻居的树上，检查所有覆盖格会把这个正常情形当成冲突。
        Mp2TileInfo anchorTile = map.Tile(position.X, position.Y);

        if (Mp2ObjectTypes.IsActionObject(info.ObjectType) && HasActionPiece(map, anchorTile))
        {
            GameLog.Warn($"Two action objects would share the tile {position}.");

            return false;
        }

        if (anchorTile.ObjectType == info.ObjectType && Mp2ObjectTypes.IsInGameActionObject(info.ObjectType))
        {
            return false;
        }

        uint objectUid = uid ?? map.TakeObjectUid();
        uint? metadata = MetadataOf(info);

        foreach (TileCells cell in cells)
        {
            WriteTile(map, cell, objectUid, metadata);
        }

        return true;
    }

    /// <summary>
    /// Overwrites the 13 bit metadata word of one tile, keeping its layer bits.
    /// </summary>
    /// <remarks>
    /// 城镇与英雄的瓦片用 metadata 指向自己的信息块（<see cref="Mp2InfoBlock.ObjectId"/>），而 <see cref="TryWrite"/>
    /// 写的是物件目录里的编号，两者不是一回事；所以落子和指块分两步。
    /// </remarks>
    /// <param name="map">The map to edit.</param>
    /// <param name="position">The tile.</param>
    /// <param name="metadata">The word, at most 13 bits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="metadata"/> does not fit into 13 bits.</exception>
    public static void SetMetadata(Mp2Map map, in Point position, uint metadata)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(metadata, 0x1FFFu);

        Mp2TileInfo tile = map.Tile(position.X, position.Y);

        map.SetTile(position.X, position.Y, tile with
        {
            Quantity1 = (byte)((tile.Quantity1 & 0x07) | PackMetadataLow(metadata)),
            Quantity2 = PackMetadataHigh(metadata),
        });
    }

    /// <summary>
    /// The 13 bit word the tiles of the object carry, the metadata branches of <c>setObjectOnTile()</c>
    /// (<c>maps_tiles_helper.cpp:2214-2284</c>).
    /// </summary>
    /// <param name="info">The object being written.</param>
    /// <returns>The word for <see cref="Mp2TileInfo.Quantity1"/> and <see cref="Mp2TileInfo.Quantity2"/>.</returns>
    /// <remarks>
    /// <para>
    /// 物件目录的 <see cref="ObjectInfo.Metadata"/> 与 MP2 的瓦片 metadata 不是一回事：目录对怪物存的是**怪物编号**
    /// （再大也只有几十）、对旗帜存的是颜色、对神龛存的是法术——那几样是 fh2 存档自己的字段，MP2 文件里没有位置。
    /// 真正要落到瓦片上的只有参考在 <c>setObjectOnTile</c> 里点名的四种。
    /// </para>
    /// <para>
    /// 怪物写 0：参考先把怪物按编号放到瓦片上，再把数量写成 0，0 在读图时表示「数量随机」。守卫的实际数量是
    /// 生成器后面自己算的，不该借 metadata 传。
    /// </para>
    /// <para>
    /// <b>不写 metadata 的物件返回 <see langword="null"/></b>：那一格原来的值要留着。城镇与英雄的瓦片用 metadata
    /// 指向自己的信息块，而一件宝物的影子常常正好压在相邻的英雄格上——把那里抹成 0，读图时英雄就找不到自己的块了。
    /// </para>
    /// </remarks>
    private static uint? MetadataOf(ObjectInfo info) => info.ObjectType switch
    {
        Mp2ObjectType.Monster => 0,
        Mp2ObjectType.Artifact => info.Metadata[0],
        Mp2ObjectType.AlchemistLab or Mp2ObjectType.Mine or Mp2ObjectType.Sawmill => info.Metadata[0],
        Mp2ObjectType.MagicGarden => 1,
        _ => null,
    };

    /// <summary>Writes one tile of the object: the two面片 fields, the add-on chain and the type.</summary>
    /// <param name="map">The map being written.</param>
    /// <param name="cell">The parts that belong to this tile.</param>
    /// <param name="uid">The object id every piece carries.</param>
    /// <param name="metadata">The 13 bit metadata word of the object, <see langword="null"/> to keep the tile's own.</param>
    private static void WriteTile(Mp2Map map, TileCells cell, uint uid, uint? metadata)
    {
        Mp2TileInfo before = map.Tile(cell.Point.X, cell.Point.Y);

        // 这一格原有的面片（装饰精灵的边缘）先收进来：参考的 placeObjectOnTile 是「并进同一格」，
        // 不是「清空再写」，覆盖它们会在地图上留下半截精灵。
        List<GroundPiece> ground = GroundPiecesOf(map, before);

        foreach (LayeredObjectPartInfo part in cell.Ground)
        {
            ground.Add(new GroundPiece(part.LayerType, part.IcnType, (byte)part.IcnIndex, uid, part.ObjectType));
        }

        List<TopPiece> top = TopPiecesOf(map, before);

        foreach (ObjectPartInfo part in cell.Top)
        {
            top.Add(new TopPiece(part.IcnType, (byte)part.IcnIndex, uid, part.ObjectType));
        }

        WritePieces(map, cell.Point, before, ground, top, metadata);
    }

    /// <summary>Lays the given pieces on a tile: the record parts first, the rest in the add-on chain.</summary>
    /// <param name="map">The map being written.</param>
    /// <param name="point">The tile.</param>
    /// <param name="before">Its value before the write, which keeps its ground and its metadata.</param>
    /// <param name="ground">Every ground piece of the tile, in catalogue order.</param>
    /// <param name="top">Every top piece of the tile, in catalogue order.</param>
    /// <param name="metadata">The 13 bit word to store, <see langword="null"/> to keep the tile's own.</param>
    private static void WritePieces(
        Mp2Map map,
        in Point point,
        Mp2TileInfo before,
        List<GroundPiece> ground,
        List<TopPiece> top,
        uint? metadata)
    {
        uint word = metadata ?? before.Metadata;

        // 参考的 sortObjectParts() 把层号小的排在前面（Object 0 → Terrain 3），落盘时第一个面片就是瓦片的那一个。
        ground.Sort((left, right) => left.Layer.CompareTo(right.Layer));

        ushort chain = 0;

        // 附加链从后往前接：AppendAddon 返回自己的下标，先接好尾巴再挂到更前面的那件上。
        for (int index = ground.Count + top.Count - 1; index >= 1; --index)
        {
            Mp2AddonInfo addon = index >= ground.Count
                ? TopAddon(top[index - ground.Count])
                : GroundAddon(ground[index]);

            chain = (ushort)map.AppendAddon(addon with { NextAddonIndex = chain });
        }

        GroundPiece? mainGround = ground.Count > 0 ? ground[0] : null;
        TopPiece? mainTop = top.Count > 0 ? top[0] : null;

        Mp2TileInfo after = before with
        {
            ObjectName1 = mainGround is { } written ? PackIcnType(written.Icn) : (byte)0,
            BottomIcnImageIndex = mainGround is { } bottom ? bottom.Index : Mp2TileInfo.EmptyImageIndex,
            Quantity1 = (byte)((mainGround is { } layer ? (byte)layer.Layer : 0) | PackMetadataLow(word)),
            Quantity2 = PackMetadataHigh(word),
            ObjectName2 = mainTop is { } upper ? PackIcnType(upper.Icn) : (byte)0,
            TopIcnImageIndex = mainTop is { } topIndex ? topIndex.Index : Mp2TileInfo.EmptyImageIndex,
            ObjectType = DecideObjectType(before, ground, top),
            NextAddonIndex = chain,
            Level1ObjectUid = mainGround is { } groundUid ? groundUid.Uid : 0,
            Level2ObjectUid = mainTop is { } topUid ? topUid.Uid : 0,
        };

        map.SetTile(point.X, point.Y, after);
    }

    /// <summary>
    /// Removes every piece of one object from the map, the port of <c>removeObjectFromMapByUID()</c>.
    /// </summary>
    /// <param name="map">The map to edit.</param>
    /// <param name="uid">The object id; every piece of that object on every tile goes away.</param>
    /// <returns>How many tiles were rebuilt.</returns>
    /// <remarks>
    /// <para>
    /// 一件物件可能把面片撒在好几格上（宝物的影子、路面的接头），所以撤除要扫整张图，按 UID 挑出所有属于它的面片，
    /// 再把剩下的重新铺一遍。件数与顺序都不变，只是那个 id 不见了。
    /// </para>
    /// <para>
    /// 撤除的物件类型不再参与判定：重建时瓦片的类型从「没有类型」起算，否则被撤掉的那件物件会借着旧类型留下来。
    /// 附加链里那些没人指向的旧件留在 <see cref="Mp2Map.Addons"/> 里——它们不再被任何链引用，读图时看不见，也不占格子。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static int Remove(Mp2Map map, uint uid)
    {
        ArgumentNullException.ThrowIfNull(map);

        int rebuilt = 0;

        for (int index = 0; index < map.Tiles.Count; ++index)
        {
            int x = index % map.Width;
            int y = index / map.Width;
            Mp2TileInfo tile = map.Tile(x, y);

            List<GroundPiece> ground = GroundPiecesOf(map, tile);
            List<TopPiece> top = TopPiecesOf(map, tile);

            bool holdsObject = ground.Exists(piece => piece.Uid == uid) || top.Exists(piece => piece.Uid == uid);

            if (!holdsObject)
            {
                continue;
            }

            ground.RemoveAll(piece => piece.Uid == uid);
            top.RemoveAll(piece => piece.Uid == uid);

            // 类型从零起算：被撤掉的那件东西不该借着旧值留下来。
            Mp2TileInfo blank = tile with { ObjectType = Mp2ObjectType.None };

            WritePieces(map, new Point(x, y), blank, ground, top, metadata: null);

            ++rebuilt;
        }

        return rebuilt;
    }

    /// <summary>The ground pieces a tile already carries, the record part first like the loader reads them.</summary>
    /// <param name="map">The map, needed for the add-on chain.</param>
    /// <param name="tile">The tile to read.</param>
    /// <returns>Its ground pieces, empty for a blank tile.</returns>
    private static List<GroundPiece> GroundPiecesOf(Mp2Map map, Mp2TileInfo tile)
    {
        List<GroundPiece> pieces = [];

        if (tile.HasGroundObject)
        {
            pieces.Add(new GroundPiece(
                (ObjectLayerType)tile.GroundLayer,
                tile.GroundIcnType,
                tile.BottomIcnImageIndex,
                tile.Level1ObjectUid,
                MapObjectCatalog.GetObjectTypeByIcn(tile.GroundIcnType, tile.BottomIcnImageIndex)));
        }

        foreach (int index in map.EnumerateAddonChain(tile))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.GroundIcnType != Mp2ObjectIcnType.Unknown)
            {
                pieces.Add(new GroundPiece(
                    (ObjectLayerType)addon.GroundLayer,
                    addon.GroundIcnType,
                    addon.BottomIcnImageIndex,
                    addon.Level1ObjectUid,
                    MapObjectCatalog.GetObjectTypeByIcn(addon.GroundIcnType, addon.BottomIcnImageIndex)));
            }
        }

        return pieces;
    }

    /// <summary>The top pieces a tile already carries.</summary>
    /// <param name="map">The map, needed for the add-on chain.</param>
    /// <param name="tile">The tile to read.</param>
    /// <returns>Its top pieces, empty for a blank tile.</returns>
    private static List<TopPiece> TopPiecesOf(Mp2Map map, Mp2TileInfo tile)
    {
        List<TopPiece> pieces = [];

        if (tile.HasTopObject)
        {
            pieces.Add(new TopPiece(
                tile.TopIcnType,
                tile.TopIcnImageIndex,
                tile.Level2ObjectUid,
                MapObjectCatalog.GetObjectTypeByIcn(tile.TopIcnType, tile.TopIcnImageIndex)));
        }

        foreach (int index in map.EnumerateAddonChain(tile))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.TopIcnType != Mp2ObjectIcnType.Unknown)
            {
                pieces.Add(new TopPiece(
                    addon.TopIcnType,
                    addon.TopIcnImageIndex,
                    addon.Level2ObjectUid,
                    MapObjectCatalog.GetObjectTypeByIcn(addon.TopIcnType, addon.TopIcnImageIndex)));
            }
        }

        return pieces;
    }

    /// <summary>
    /// The type the tile reports, the type rule of <c>placeObjectOnTile()</c>
    /// (<c>maps_tiles_helper.cpp:245-330</c>).
    /// </summary>
    /// <param name="cell">The parts of the tile, ground parts already sorted by layer.</param>
    /// <returns><see cref="Mp2ObjectType.None"/> when no part names a type.</returns>
    /// <remarks>
    /// <para>
    /// 参考在压每个面片之前都比一次「本格有没有更靠上的、带类型的面片」，有就不改写类型。因为它是**按层序压**的，
    /// 先压进去的层号不会更大，那条判断在空瓦片上永远不成立；于是规则收敛成两句：底面片里第一个有类型的说了算，
    /// 带动作位的类型可以覆盖它；顶面片只在当前类型不是动作类型时才覆盖。
    /// </para>
    /// <para>
    /// 「动作位」用的是 <see cref="Mp2ObjectTypes.IsActionObject"/> 而不是 <c>IsInGameActionObject</c>——参考这里写的就是
    /// <c>MP2::isOffGameActionObject</c>，一个纯动作位测试。
    /// </para>
    /// </remarks>
    private static Mp2ObjectType DecideObjectType(
        Mp2TileInfo before, IReadOnlyList<GroundPiece> ground, IReadOnlyList<TopPiece> top)
    {
        Mp2ObjectType type = before.ObjectType;

        foreach (GroundPiece piece in ground)
        {
            if (piece.Type == Mp2ObjectType.None)
            {
                continue;
            }

            if (type == Mp2ObjectType.None
                || Mp2ObjectTypes.IsActionObject(piece.Type)
                || !Mp2ObjectTypes.IsActionObject(type))
            {
                type = piece.Type;
            }
        }

        foreach (TopPiece piece in top)
        {
            if (piece.Type != Mp2ObjectType.None && !Mp2ObjectTypes.IsActionObject(type))
            {
                type = piece.Type;
            }
        }

        return type;
    }

    /// <summary>One add-on that carries an extra ground part of a tile.</summary>
    /// <param name="piece">The part.</param>
    /// <returns>The add-on, without its <see cref="Mp2AddonInfo.NextAddonIndex"/>.</returns>
    private static Mp2AddonInfo GroundAddon(GroundPiece piece) => new()
    {
        NextAddonIndex = 0,
        ObjectNameN1 = PackIcnType(piece.Icn),
        BottomIcnImageIndex = piece.Index,
        QuantityN = (byte)piece.Layer,
        ObjectNameN2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        Level1ObjectUid = piece.Uid,
        Level2ObjectUid = 0,
    };

    /// <summary>One add-on that carries an extra top part of a tile.</summary>
    /// <param name="piece">The part.</param>
    /// <returns>The add-on, without its <see cref="Mp2AddonInfo.NextAddonIndex"/>.</returns>
    private static Mp2AddonInfo TopAddon(TopPiece piece) => new()
    {
        NextAddonIndex = 0,
        ObjectNameN1 = 0,
        BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        QuantityN = 0,
        ObjectNameN2 = PackIcnType(piece.Icn),
        TopIcnImageIndex = piece.Index,
        Level1ObjectUid = 0,
        Level2ObjectUid = piece.Uid,
    };

    /// <summary>Packs an ICN type into the six high bits of an <c>objectName</c> byte.</summary>
    /// <param name="type">The ICN tile set.</param>
    /// <returns>The byte the file stores.</returns>
    private static byte PackIcnType(Mp2ObjectIcnType type) => (byte)((byte)type << 2);

    /// <summary>The low five bits of the metadata word, above the layer bits of <see cref="Mp2TileInfo.Quantity1"/>.</summary>
    /// <param name="metadata">The 13 bit word.</param>
    /// <returns>The bits that belong to <see cref="Mp2TileInfo.Quantity1"/>.</returns>
    private static byte PackMetadataLow(uint metadata) => (byte)((metadata & 0x1F) << 3);

    /// <summary>The high eight bits of the metadata word, which go to <see cref="Mp2TileInfo.Quantity2"/>.</summary>
    /// <param name="metadata">The 13 bit word.</param>
    /// <returns>The byte <see cref="Mp2TileInfo.Quantity2"/> holds.</returns>
    private static byte PackMetadataHigh(uint metadata) => (byte)((metadata >> 5) & 0xFF);

    /// <summary>
    /// Groups the parts of an object by the tile they land on, keeping the catalogue's order.
    /// </summary>
    /// <param name="position">Anchor tile of the object.</param>
    /// <param name="info">The object.</param>
    /// <returns>One entry per covered tile.</returns>
    private static List<TileCells> Collect(in Point position, ObjectInfo info)
    {
        List<TileCells> cells = [];

        foreach (LayeredObjectPartInfo part in info.GroundLevelParts)
        {
            CellsOf(cells, Offset(position, part.TileOffset)).Ground.Add(part);
        }

        foreach (ObjectPartInfo part in info.TopLevelParts)
        {
            CellsOf(cells, Offset(position, part.TileOffset)).Top.Add(part);
        }

        return cells;
    }

    /// <summary>Finds or creates the entry of one tile.</summary>
    /// <param name="cells">The entries collected so far.</param>
    /// <param name="point">The tile.</param>
    /// <returns>The entry of <paramref name="point"/>.</returns>
    private static TileCells CellsOf(List<TileCells> cells, Point point)
    {
        foreach (TileCells cell in cells)
        {
            if (cell.Point == point)
            {
                return cell;
            }
        }

        TileCells created = new(point);
        cells.Add(created);

        return created;
    }

    /// <summary>Whether a tile of an object is inside the map.</summary>
    /// <param name="map">The map.</param>
    /// <param name="point">The tile to test.</param>
    /// <returns><see langword="true"/> when the tile is inside the map.</returns>
    private static bool WithinMap(Mp2Map map, in Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X < map.Width && point.Y < map.Height;

    /// <summary>
    /// Builds the tile and the add-on chain a catalogue object would occupy, without writing anything.
    /// </summary>
    /// <param name="info">The catalogue object.</param>
    /// <param name="template">The tile of the anchor, with its two sprites and the object type.</param>
    /// <param name="addons">The extra pieces of the object, in catalogue order.</param>
    /// <returns><see langword="false"/> when the object has no part at all.</returns>
    /// <remarks>
    /// <para>
    /// 编辑器要的是「这件物件单独放在一张空地上长什么样」，也就是 <see cref="TryWrite"/> 在空瓦片上会写出来的东西。两者
    /// 用同一套 <see cref="Collect"/> 与面片换算，所以编辑器摆出的物件与生成器摆出的完全一致——少写一份换算表，
    /// 也少一处对不齐的机会。
    /// </para>
    /// <para>
    /// UID 一律写 <c>0</c>：模板只是形状，真正的编号由落子时领。地形图索引也是 0，落子时用的是目标格自己的地面。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public static bool TryBuildTemplate(
        ObjectInfo info, out Mp2TileInfo template, out IReadOnlyList<Mp2AddonInfo> addons)
    {
        ArgumentNullException.ThrowIfNull(info);

        template = default;
        addons = [];

        if (info.IsEmpty)
        {
            return false;
        }

        List<GroundPiece> ground = [];
        List<TopPiece> top = [];
        List<Mp2AddonInfo> chain = [];

        // 锚点那一格的面片进记录本身，其余格子的面片按目录顺序接成附加链。
        foreach (TileCells cell in Collect(new Point(0, 0), info))
        {
            bool isAnchor = cell.Point.X == 0 && cell.Point.Y == 0;

            foreach (LayeredObjectPartInfo part in cell.Ground)
            {
                GroundPiece piece = new(part.LayerType, part.IcnType, (byte)part.IcnIndex, 0, part.ObjectType);

                if (isAnchor)
                {
                    ground.Add(piece);
                }
                else
                {
                    chain.Add(GroundAddon(piece));
                }
            }

            foreach (ObjectPartInfo part in cell.Top)
            {
                TopPiece piece = new(part.IcnType, (byte)part.IcnIndex, 0, part.ObjectType);

                if (isAnchor)
                {
                    top.Add(piece);
                }
                else
                {
                    chain.Add(TopAddon(piece));
                }
            }
        }

        if (ground.Count == 0 && top.Count == 0)
        {
            return false;
        }

        ground.Sort((left, right) => left.Layer.CompareTo(right.Layer));

        GroundPiece? mainGround = ground.Count > 0 ? ground[0] : null;
        TopPiece? mainTop = top.Count > 0 ? top[0] : null;

        // 锚点那一格剩下的面片也要进链，否则多部件的物件会缺口。
        for (int index = 1; index < ground.Count; ++index)
        {
            chain.Add(GroundAddon(ground[index]));
        }

        for (int index = 1; index < top.Count; ++index)
        {
            chain.Add(TopAddon(top[index]));
        }

        uint metadata = MetadataOf(info) ?? 0;

        template = new Mp2TileInfo
        {
            TerrainImageIndex = 0,
            ObjectName1 = mainGround is { } written ? PackIcnType(written.Icn) : (byte)0,
            BottomIcnImageIndex = mainGround is { } bottom ? bottom.Index : Mp2TileInfo.EmptyImageIndex,
            Quantity1 = (byte)((mainGround is { } layer ? (byte)layer.Layer : 0) | PackMetadataLow(metadata)),
            Quantity2 = PackMetadataHigh(metadata),
            ObjectName2 = mainTop is { } upper ? PackIcnType(upper.Icn) : (byte)0,
            TopIcnImageIndex = mainTop is { } topIndex ? topIndex.Index : Mp2TileInfo.EmptyImageIndex,
            TerrainFlags = 0,
            ObjectType = DecideObjectType(default, ground, top),
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

        addons = chain;

        return true;
    }

    /// <summary>Whether a tile already carries an action object, the first guard of <c>putObjectOnMap()</c>.</summary>
    /// <param name="map">The map, needed for the add-on chain.</param>
    /// <param name="tile">The tile to test.</param>
    /// <returns><see langword="true"/> when anything on the tile carries the action bit.</returns>
    private static bool HasActionPiece(Mp2Map map, Mp2TileInfo tile)
    {
        if (Mp2ObjectTypes.IsActionObject(tile.ObjectType))
        {
            return true;
        }

        foreach (GroundPiece piece in GroundPiecesOf(map, tile))
        {
            if (Mp2ObjectTypes.IsActionObject(piece.Type))
            {
                return true;
            }
        }

        foreach (TopPiece piece in TopPiecesOf(map, tile))
        {
            if (Mp2ObjectTypes.IsActionObject(piece.Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One ground part of a tile, either read back from the map or taken from the object.</summary>
    /// <param name="Layer">Its drawing layer.</param>
    /// <param name="Icn">Its ICN tile set.</param>
    /// <param name="Index">Its sprite index.</param>
    /// <param name="Uid">The object id it belongs to.</param>
    /// <param name="Type">The object type the sprite stands for, <see cref="Mp2ObjectType.None"/> for scenery.</param>
    private readonly record struct GroundPiece(
        ObjectLayerType Layer, Mp2ObjectIcnType Icn, byte Index, uint Uid, Mp2ObjectType Type);

    /// <summary>One top part of a tile.</summary>
    /// <param name="Icn">Its ICN tile set.</param>
    /// <param name="Index">Its sprite index.</param>
    /// <param name="Uid">The object id it belongs to.</param>
    /// <param name="Type">The object type the sprite stands for, <see cref="Mp2ObjectType.None"/> for scenery.</param>
    private readonly record struct TopPiece(Mp2ObjectIcnType Icn, byte Index, uint Uid, Mp2ObjectType Type);

    private static Point Offset(in Point position, in Point offset) =>
        new(position.X + offset.X, position.Y + offset.Y);

    /// <summary>The parts of an object that land on one tile.</summary>
    private sealed class TileCells
    {
        public TileCells(Point point) => Point = point;

        /// <summary>The tile.</summary>
        public Point Point { get; }

        /// <summary>Ground parts of the object on this tile, in catalogue order.</summary>
        public List<LayeredObjectPartInfo> Ground { get; } = [];

        /// <summary>Top parts of the object on this tile, in catalogue order.</summary>
        public List<ObjectPartInfo> Top { get; } = [];
    }
}

