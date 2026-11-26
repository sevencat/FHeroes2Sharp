using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The text of the object popup of the map editor, <c>Editor::showPopupWindow()</c> of the reference
/// (<c>src/fheroes2/editor/editor_object_popup_window.cpp:57-267</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把物件说清楚的办法是「用这一格的主部件去物件表里认领」：认得资源堆就报几个、认得神器就报名字、认得矿洞就报
/// 矿种和归属，剩下的报物件类型名。端口的地图是 MP2 记录，一格里只有地面部件与顶层部件的 ICN 与图片号，认领的办法
/// 一样，只是拿地面部件去比，物件表仍然是同一份 <see cref="MapObjectCatalog"/>。
/// </para>
/// <para>
/// 参考在物件表里找不到时会 <c>assert(0)</c>；release 构建里断言是空的，于是退回物件类型名。端口按 release 走：
/// 弹窗是只读的帮手，不该因为目录里没有这件东西就让编辑器崩掉。
/// </para>
/// <para>
/// 参考会先找「主格」（<c>Tile::getIndexOfMainTile</c>）再在那一格上取文字，因为多格物件的部件偏移只存在运行时
/// 瓦片里。端口的格式层没有偏移，但归属、数量这些都按物件 UID 取，同一件物件的每一格都能取到同一份数据，所以直接
/// 描述被点的那一格；末尾的 <c>[x; y]</c> 本来就是被点的那一格。
/// </para>
/// </remarks>
public static class EditorObjectPopup
{
    /// <summary>
    /// Describes the object on a tile, <c>Editor::showPopupWindow()</c>.
    /// </summary>
    /// <param name="map">Map to read.</param>
    /// <param name="tile">Tile that was clicked.</param>
    /// <param name="metadata">The per-object metadata tables of the map.</param>
    /// <returns>The text to show: a few lines plus <c>[x; y]</c>.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public static string Describe(Mp2Map map, Point tile, MapObjectMetadataStore metadata)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(metadata);

        return Describe(map.Tile(tile.X, tile.Y), tile, metadata);
    }

    /// <summary>
    /// Describes a tile that is already in hand, <c>Editor::showPopupWindow()</c> without the map.
    /// </summary>
    /// <param name="tile">The tile to describe.</param>
    /// <param name="position">Position of the tile on the map, used for the coordinates line.</param>
    /// <param name="metadata">The per-object metadata tables of the map.</param>
    /// <returns>The text to show: a few lines plus <c>[x; y]</c>.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public static string Describe(Mp2TileInfo tile, Point position, MapObjectMetadataStore metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return $"{ObjectInfoText(tile, metadata)}\n[{position.X}; {position.Y}]";
    }

    /// <summary>The body of the popup, <c>getObjectInfoText()</c> (<c>editor_object_popup_window.cpp:81-241</c>).</summary>
    private static string ObjectInfoText(Mp2TileInfo tile, MapObjectMetadataStore metadata)
    {
        Mp2ObjectType type = Mp2ObjectTypes.NonActionType(tile.ObjectType);

        switch (type)
        {
            case Mp2ObjectType.None:
                // 参考在这一格没有物件时只报「是路」或「是什么地形」。
                return tile.GroundIcnType == Mp2ObjectIcnType.Road
                    ? "Road"
                    : MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex).ToString();

            case Mp2ObjectType.Resource:
            {
                ObjectInfo? resource = FindObject(ObjectGroup.AdventureTreasures, type, tile, mainPartIsLast: false);

                if (resource is null)
                {
                    return Mp2ObjectName.Of(type);
                }

                string name = ResourceText.Of((int)resource.Metadata[0]);

                return metadata.Resources.TryGetValue(tile.Level1ObjectUid, out ResourceMetadata? pile) && pile.Count > 0
                    ? $"{pile.Count} {name}"
                    : name;
            }

            case Mp2ObjectType.Artifact:
            {
                ObjectInfo? artifact = FindObject(ObjectGroup.AdventureArtifacts, type, tile, mainPartIsLast: false);

                return artifact is null
                    ? Mp2ObjectName.Of(type)
                    : $"{Mp2ObjectName.Of(type)}\n{ArtifactTable.Get((ArtifactType)artifact.Metadata[0]).Name}";
            }

            case Mp2ObjectType.Monster:
            {
                ObjectInfo? stack = FindObject(ObjectGroup.Monsters, type, tile, mainPartIsLast: false);

                if (stack is null)
                {
                    return Mp2ObjectName.Of(type);
                }

                MonsterType monster = (MonsterType)stack.Metadata[0];

                if (metadata.Monsters.TryGetValue(tile.Level1ObjectUid, out MonsterMetadata? recorded) && recorded.Count > 0)
                {
                    string name = recorded.Count == 1
                        ? MonsterNames.GetSingular(monster)
                        : MonsterNames.GetMulti(monster);

                    return $"{recorded.Count} {name}";
                }

                return $"{Mp2ObjectName.Of(type)}\n{MonsterNames.GetMulti(monster)}";
            }

            case Mp2ObjectType.Mine:
            {
                // 矿洞的矿种写在背景层的最后一个部件上，参考比的就是它（:159-160）。
                ObjectInfo? mine = FindObject(ObjectGroup.AdventureMines, type, tile, mainPartIsLast: true);

                if (mine is null)
                {
                    return Mp2ObjectName.Of(type);
                }

                return CapturableText(tile, metadata, MineText.GetMineName((int)mine.Metadata[0]));
            }

            case Mp2ObjectType.AlchemistLab:
            case Mp2ObjectType.Sawmill:
            {
                ObjectInfo? building = FindObject(ObjectGroup.AdventureMines, type, tile, mainPartIsLast: false);

                return building is null
                    ? Mp2ObjectName.Of(type)
                    : CapturableText(tile, metadata, Mp2ObjectName.Of(type));
            }

            case Mp2ObjectType.Lighthouse:
            {
                ObjectInfo? building = FindObject(ObjectGroup.AdventureMiscellaneous, type, tile, mainPartIsLast: false);

                return building is null
                    ? Mp2ObjectName.Of(type)
                    : CapturableText(tile, metadata, Mp2ObjectName.Of(type));
            }

            case Mp2ObjectType.Barrier:
            {
                ObjectInfo? barrier = FindObject(ObjectGroup.AdventureMiscellaneous, type, tile, mainPartIsLast: false);

                return barrier is null
                    ? Mp2ObjectName.Of(type)
                    : $"{BarrierColors.GetBarrierName((int)barrier.Metadata[0])} Barrier";
            }

            case Mp2ObjectType.TravellerTent:
            {
                ObjectInfo? tent = FindObject(ObjectGroup.AdventureMiscellaneous, type, tile, mainPartIsLast: false);

                return tent is null
                    ? Mp2ObjectName.Of(type)
                    : $"{BarrierColors.GetTentName((int)tent.Metadata[0])} Tent";
            }

            default:
                return Mp2ObjectName.Of(type);
        }
    }

    /// <summary>
    /// Finds the object of a group that owns this tile, the loop every branch of
    /// <c>getObjectInfoText()</c> runs.
    /// </summary>
    /// <param name="group">Group to search.</param>
    /// <param name="type">The type of the object, without its action bit.</param>
    /// <param name="tile">The tile to identify.</param>
    /// <param name="mainPartIsLast">Whether the tile shows the last ground part instead of the first one.</param>
    /// <returns>The object, or <see langword="null"/> when the group has nothing that matches.</returns>
    private static ObjectInfo? FindObject(ObjectGroup group, Mp2ObjectType type, Mp2TileInfo tile, bool mainPartIsLast)
    {
        if (!tile.HasGroundObject)
        {
            return null;
        }

        foreach (ObjectInfo info in MapObjectCatalog.GetObjectsByGroup(group))
        {
            // 物件表记的是带动作位的类型（ActionResource），格上记的是不带的那一个，两边都抹平再比。
            if (Mp2ObjectTypes.NonActionType(info.ObjectType) != type || info.IsEmpty)
            {
                continue;
            }

            LayeredObjectPartInfo main = mainPartIsLast ? info.GroundLevelParts[^1] : info.GroundLevelParts[0];

            if (main.IcnType == tile.GroundIcnType && main.IcnIndex == tile.BottomIcnImageIndex)
            {
                return info;
            }
        }

        return null;
    }

    /// <summary>Adds the owner line of a capturable object, <c>getCapturableObjectInfo()</c> (<c>:57-79</c>).</summary>
    private static string CapturableText(Mp2TileInfo tile, MapObjectMetadataStore metadata, string name)
    {
        return metadata.CapturableObjects.TryGetValue(tile.Level1ObjectUid, out CapturableObjectMetadata? capturable) &&
               capturable.OwnerColor != PlayerColor.None
            ? $"{name}\n({PlayerColor.GetName(capturable.OwnerColor)} player)"
            : name;
    }
}
