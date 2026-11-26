using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// What the eraser deletes, the <c>ObjectErasureType</c> enumeration of the reference's editor panel
/// (<c>editor/editor_interface_panel.h:249-275</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>位值必须与参考逐位相同</b>：参考把这一组当成位掩码用（<c>_eraseTypes ^= _eraseButtonObjectTypes[i]</c>，
/// <c>editor_interface_panel.cpp:1383</c>），地图作者可以同时勾掉「山 + 树 + 路」。重排或改位值会让「提示里说的那一类」
/// 与「实际被删的那一类」错开。
/// </para>
/// <para>
/// <b>一位不等于一个分组</b>：<see cref="Landscape"/> 管两个目录分组，<see cref="AdventureNonPickable"/> 管五个，
/// 而 <see cref="ObjectGroup.AdventureWater"/> 同时归「可捡的冒险物件」和「不可捡的冒险物件」——照参考原样重叠，
/// 见 <see cref="EditorEraseTypes.Groups"/>。
/// </para>
/// <para>
/// 默认值是 <see cref="AllObjects"/>（<c>editor_interface_panel.h:337</c>）：刚打开编辑器时橡皮什么都能擦，
/// 作者才逐类排除。
/// </para>
/// </remarks>
[Flags]
public enum EditorEraseType : uint
{
    /// <summary>Nothing is erased, <c>ERASE_NONE</c>.</summary>
    None = 0,

    /// <summary>Mountains, <c>ERASE_MOUNTAINS</c>.</summary>
    Mountains = 1 << 0,

    /// <summary>Rocks, <c>ERASE_ROCKS</c>.</summary>
    Rocks = 1 << 1,

    /// <summary>Trees, <c>ERASE_TREES</c>.</summary>
    Trees = 1 << 2,

    /// <summary>Water and miscellaneous landscape objects, <c>ERASE_LANDSCAPE</c>.</summary>
    Landscape = 1 << 3,

    /// <summary>
    /// Dwellings, mines, power-ups, water and miscellaneous adventure objects, i.e. the ones a hero cannot pick up,
    /// <c>ERASE_ADVENTURE_NON_PICKABLE</c>.
    /// </summary>
    AdventureNonPickable = 1 << 4,

    /// <summary>Towns and castles, <c>ERASE_TOWNS</c>.</summary>
    Towns = 1 << 5,

    /// <summary>
    /// Artifacts, treasures and water adventure objects, i.e. the ones a hero picks up,
    /// <c>ERASE_ADVENTURE_PICKABLE</c>.
    /// </summary>
    AdventurePickable = 1 << 6,

    /// <summary>Monsters and their guards, <c>ERASE_MONSTERS</c>.</summary>
    Monsters = 1 << 7,

    /// <summary>Heroes, <c>ERASE_HEROES</c>.</summary>
    Heroes = 1 << 8,

    /// <summary>Streams, <c>ERASE_STREAMS</c>.</summary>
    Streams = 1 << 9,

    /// <summary>Roads, <c>ERASE_ROADS</c>.</summary>
    Roads = 1 << 10,

    /// <summary>Every type, <c>ERASE_ALL_OBJECTS</c>; the state the panel opens with.</summary>
    AllObjects = Mountains | Rocks | Trees | Landscape | AdventureNonPickable | Towns
        | AdventurePickable | Monsters | Heroes | Streams | Roads,
}

/// <summary>
/// The eleven erasure types as the panel shows them: the buttons, the catalogue groups each of them covers and the
/// names the reference gives them (<c>getEraseObjectGroups</c>, <c>editor_interface_panel.cpp:268-320</c>, and
/// <c>_getEraseObjectTypeName</c>, <c>:902-930</c>).
/// </summary>
public static class EditorEraseTypes
{
    /// <summary>
    /// The buttons of the erase page, in the order the reference lays them out: four per row over three rows, the
    /// last row shifted right because it holds three cells (<c>editor_interface_panel.h:282-292</c>).
    /// </summary>
    /// <remarks>
    /// 顺序照 <c>_eraseButtonObjectTypes</c>，也就是图标的顺序（山脉、石头、树……路、河）；重排会让「点第三个格子」选中别的类别。
    /// </remarks>
    public static readonly IReadOnlyList<EditorEraseType> Buttons =
    [
        EditorEraseType.Mountains,
        EditorEraseType.Rocks,
        EditorEraseType.Trees,
        EditorEraseType.Landscape,
        EditorEraseType.AdventureNonPickable,
        EditorEraseType.Towns,
        EditorEraseType.AdventurePickable,
        EditorEraseType.Monsters,
        EditorEraseType.Heroes,
        EditorEraseType.Roads,
        EditorEraseType.Streams,
    ];

    /// <summary>
    /// The catalogue groups the selected types cover, <c>getEraseObjectGroups()</c>. An object is erased when the group
    /// of the part that draws it is in this set.
    /// </summary>
    /// <param name="types">The types the author left checked.</param>
    /// <returns>
    /// The groups, empty for <see cref="EditorEraseType.None"/> — the reference returns <c>{}</c> there, so an eraser
    /// with nothing checked deletes nothing.
    /// </returns>
    public static IReadOnlySet<ObjectGroup> Groups(EditorEraseType types)
    {
        HashSet<ObjectGroup> groups = [];

        if (types == EditorEraseType.None)
        {
            return groups;
        }

        if ((types & EditorEraseType.Mountains) != 0)
        {
            groups.Add(ObjectGroup.LandscapeMountains);
        }

        if ((types & EditorEraseType.Rocks) != 0)
        {
            groups.Add(ObjectGroup.LandscapeRocks);
        }

        if ((types & EditorEraseType.Trees) != 0)
        {
            groups.Add(ObjectGroup.LandscapeTrees);
        }

        if ((types & EditorEraseType.Landscape) != 0)
        {
            groups.Add(ObjectGroup.LandscapeWater);
            groups.Add(ObjectGroup.LandscapeMiscellaneous);
        }

        if ((types & EditorEraseType.AdventureNonPickable) != 0)
        {
            groups.Add(ObjectGroup.AdventureDwellings);
            groups.Add(ObjectGroup.AdventureMines);
            groups.Add(ObjectGroup.AdventurePowerUps);
            groups.Add(ObjectGroup.AdventureWater);
            groups.Add(ObjectGroup.AdventureMiscellaneous);
        }

        if ((types & EditorEraseType.Towns) != 0)
        {
            groups.Add(ObjectGroup.KingdomTowns);
        }

        if ((types & EditorEraseType.AdventurePickable) != 0)
        {
            groups.Add(ObjectGroup.AdventureArtifacts);
            groups.Add(ObjectGroup.AdventureTreasures);
            groups.Add(ObjectGroup.AdventureWater);
        }

        if ((types & EditorEraseType.Monsters) != 0)
        {
            groups.Add(ObjectGroup.Monsters);
        }

        if ((types & EditorEraseType.Heroes) != 0)
        {
            groups.Add(ObjectGroup.KingdomHeroes);
        }

        if ((types & EditorEraseType.Streams) != 0)
        {
            groups.Add(ObjectGroup.Streams);
        }

        if ((types & EditorEraseType.Roads) != 0)
        {
            groups.Add(ObjectGroup.Roads);
        }

        return groups;
    }

    /// <summary>
    /// The name of one type, <c>_getEraseObjectTypeName</c>; the reference builds its right click hints out of it.
    /// </summary>
    /// <param name="type">One of <see cref="Buttons"/>.</param>
    /// <returns>The name, or <c>Unknown object type</c> for a combination or an unknown value.</returns>
    public static string Name(EditorEraseType type) => type switch
    {
        EditorEraseType.Mountains => "Mountains",
        EditorEraseType.Rocks => "Rocks",
        EditorEraseType.Trees => "Trees",
        EditorEraseType.Landscape => "Miscellaneous and Water landscape objects",
        EditorEraseType.AdventureNonPickable => "Adventure non pickable objects",
        EditorEraseType.Towns => "Castles",
        EditorEraseType.AdventurePickable => "Adventure pickable objects",
        EditorEraseType.Monsters => "Monsters",
        EditorEraseType.Heroes => "Heroes",
        EditorEraseType.Roads => "Roads",
        EditorEraseType.Streams => "Streams",
        _ => "Unknown object type",
    };

    /// <summary>Whether <paramref name="type"/> is exactly one of the eleven buttons, i.e. a single known bit.</summary>
    /// <param name="type">Value to test.</param>
    /// <returns>
    /// <see langword="true"/> for one button, <see langword="false"/> for <see cref="EditorEraseType.None"/> and for
    /// combinations.
    /// </returns>
    public static bool IsButton(EditorEraseType type) =>
        type != EditorEraseType.None && (type & EditorEraseType.AllObjects) == type && (type & (type - 1)) == 0;
}
