namespace FHeroes2.Game.Resources;

/// <summary>
/// The two mappings between a resource flag and its icon, <c>Resource::getIconIcnIndex()</c> and
/// <c>Resource::getResourceTypeFromIconIndex()</c> (<c>resource.cpp:416-467</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>RESOURCE.ICN</c> holds the seven icons in a fixed order that has nothing to do with the flag values: wood is
/// frame 0 and mercury frame 1, although <see cref="Resource.Wood"/> is 1 and <see cref="Resource.Mercury"/> is 2. The
/// marketplace rows are indexed by frame, so the round trip through these two functions is what turns a click on the
/// third cell into <see cref="Resource.Ore"/>.
/// </para>
/// <para>
/// The reference asserts on anything else and then returns 0 in both directions; the port answers
/// <c>-1</c> for an unknown flag, which cannot be mistaken for a frame, and <see cref="Resource.Unknown"/> for an
/// unknown frame, which is the reference's fall-through value.
/// </para>
/// </remarks>
public static class ResourceIconIndex
{
    /// <summary>Number of resource icons, <c>7</c>.</summary>
    public const int Count = 7;

    /// <summary>Frame of a resource's icon in <c>RESOURCE.ICN</c>, <c>Resource::getIconIcnIndex()</c>.</summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The frame, 0 to 6, or <c>-1</c> when <paramref name="resource"/> is not a single resource.</returns>
    public static int Of(int resource) => resource switch
    {
        Resource.Wood => 0,
        Resource.Mercury => 1,
        Resource.Ore => 2,
        Resource.Sulfur => 3,
        Resource.Crystal => 4,
        Resource.Gems => 5,
        Resource.Gold => 6,
        _ => -1,
    };

    /// <summary>The resource of a frame of <c>RESOURCE.ICN</c>, <c>Resource::getResourceTypeFromIconIndex()</c>.</summary>
    /// <param name="index">The frame, 0 to 6.</param>
    /// <returns>The resource, or <see cref="Resource.Unknown"/> when the frame is out of the table.</returns>
    public static int ToResource(int index) => index switch
    {
        0 => Resource.Wood,
        1 => Resource.Mercury,
        2 => Resource.Ore,
        3 => Resource.Sulfur,
        4 => Resource.Crystal,
        5 => Resource.Gems,
        6 => Resource.Gold,
        _ => Resource.Unknown,
    };

    /// <summary>
    /// The frame of a resource pile on the map, <c>Resource::GetIndexSprite()</c> (<c>resource.cpp:366-389</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是**地图对象**的帧号，不是界面图标：<c>OBJN</c> 里资源堆按 wood、mercury、ore… 的顺序占 1、3、5、7、9、11、13
    /// 七个**奇数**帧（偶数帧是别的朝向），所以答案是 <c>2 * 图标序号 + 1</c>，而不是 0 到 6。
    /// </para>
    /// <para>
    /// 未知资源参考记一条警告后返回 0，端口照抄 0：0 帧在资源堆里不存在，落到这里就说明调用方给错了旗标。
    /// </para>
    /// </remarks>
    /// <param name="resource">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The odd frame number, or <c>0</c> when <paramref name="resource"/> is not a single resource.</returns>
    public static int ObjectFrameOf(int resource) => resource switch
    {
        Resource.Wood => 1,
        Resource.Mercury => 3,
        Resource.Ore => 5,
        Resource.Sulfur => 7,
        Resource.Crystal => 9,
        Resource.Gems => 11,
        Resource.Gold => 13,
        _ => 0,
    };

    /// <summary>
    /// The resource of a map pile frame, <c>Resource::FromIndexSprite()</c> (<c>resource.cpp:391-414</c>).
    /// </summary>
    /// <param name="frame">The <c>OBJN</c> frame number of a resource pile.</param>
    /// <returns>The resource, or <see cref="Resource.Unknown"/> for an even or out-of-range frame.</returns>
    public static int ResourceOfObjectFrame(uint frame) => frame switch
    {
        1 => Resource.Wood,
        3 => Resource.Mercury,
        5 => Resource.Ore,
        7 => Resource.Sulfur,
        9 => Resource.Crystal,
        11 => Resource.Gems,
        13 => Resource.Gold,
        _ => Resource.Unknown,
    };
}
