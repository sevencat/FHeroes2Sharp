using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// Enumerates the objects of one catalogue group as presets the editor can place, the
/// <c>getObjectsByGroup()</c> the reference's panel walks when it fills an object page
/// (<c>editor/editor_interface_panel.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的面板不是「一张预设表」，而是「当前刷子对应的那个 <c>ObjectGroup</c> 里的全部物件」：点冒险宝物，页面上就是
/// <c>ADVENTURE_TREASURES</c> 的全部条目。端口照这个做法，用
/// <see cref="RandomMapObjectWriter.TryBuildTemplate"/> 现搭模板——生成器与编辑器因此共享同一套换算。
/// </para>
/// <para>
/// <b>结果缓存</b>：一个分组可以有几百个条目（怪物 66、宝物 40+、路 513），每次画面板都重搭模板会明显拖帧。
/// 目录本身是静态的，所以按分组缓存一份，进程内不再变。
/// </para>
/// </remarks>
public static class MapObjectCatalogPresets
{
    private static readonly Dictionary<ObjectGroup, IReadOnlyList<Mp2ObjectPreset>> Cache = [];

    private static readonly Lock Sync = new();

    /// <summary>Every placeable object of a group, in catalogue order.</summary>
    /// <param name="group">The catalogue group.</param>
    /// <returns>The presets; empty when the group has no table or no buildable object.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="group"/> is not a group.</exception>
    public static IReadOnlyList<Mp2ObjectPreset> For(ObjectGroup group)
    {
        if (group <= ObjectGroup.None || group >= ObjectGroup.GroupCount)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown object group.");
        }

        lock (Sync)
        {
            if (Cache.TryGetValue(group, out IReadOnlyList<Mp2ObjectPreset>? cached))
            {
                return cached;
            }

            List<Mp2ObjectPreset> presets = [];

            IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(group);

            for (int index = 0; index < objects.Count; ++index)
            {
                if (!RandomMapObjectWriter.TryBuildTemplate(objects[index], out Mp2TileInfo template, out IReadOnlyList<Mp2AddonInfo> addons))
                {
                    continue;
                }

                presets.Add(new Mp2ObjectPreset($"{group} #{index}", template, addons));
            }

            Cache[group] = presets;

            return presets;
        }
    }

    /// <summary>The number of placeable objects of a group, without building them.</summary>
    /// <param name="group">The catalogue group.</param>
    /// <returns>The count; <c>0</c> for a group with no table.</returns>
    public static int CountFor(ObjectGroup group) => For(group).Count;
}
