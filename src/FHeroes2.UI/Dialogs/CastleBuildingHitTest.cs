using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The town screen's building hit table, <c>castle_dialog.cpp:413-435</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的命中区不是「每个建筑一个矩形」，而是 <see cref="CastleBuildingInfo.GetArea"/> 给的矩形组；后画的建筑盖住
/// 先画的，所以查表要从队列<b>反</b>着走，第一个命中的就是玩家看到的那一座。这里照抄这条顺序，队列本身仍由
/// <see cref="BuildingsRenderQueue"/> 提供。
/// </para>
/// <para>
/// 未建的建筑在队列里也有面积（面积表是静态的），所以「建没建」必须单独问 <see cref="Castle.IsBuild"/>；参考
/// 同样先跳过没建的。文案不在这里：悬停到建筑上说什么由
/// <see cref="CastleDialog.GetStatusLine"/>（即 <c>BuildingInfo.GetStatusLine</c>）决定。
/// </para>
/// </remarks>
public static class CastleBuildingHitTest
{
    /// <summary>
    /// The building under a screen point, <c>castle_dialog.cpp:416-435</c>.
    /// </summary>
    /// <param name="castle">The town on screen.</param>
    /// <param name="offset">The town screen's top-left corner, which the queue's areas are relative to.</param>
    /// <param name="cursor">The mouse position.</param>
    /// <returns>
    /// The topmost built building whose area holds the point, or <see cref="BuildingType.Nothing"/> when none does.
    /// </returns>
    public static BuildingType Find(Castle castle, Point offset, Point cursor)
    {
        ArgumentNullException.ThrowIfNull(castle);

        BuildingsRenderQueue queue = new(castle, offset);

        for (int index = queue.Count - 1; index >= 0; --index)
        {
            BuildingRenderInfo building = queue[index];

            if (!castle.IsBuild(building.Id))
            {
                continue;
            }

            foreach (Rect area in building.Areas)
            {
                if (area.Contains(cursor))
                {
                    return building.Id;
                }
            }
        }

        return BuildingType.Nothing;
    }
}
