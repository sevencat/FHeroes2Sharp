using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;

namespace FHeroes2.UI.Battle;

/// <summary>
/// Where a creature's sprite goes on the board: <c>GetTroopPosition()</c> (<c>battle_interface.cpp:502-527</c>)
/// and <c>Battle::Unit::GetCenterPoint()</c> (<c>battle_troop.cpp:1261-1271</c>).
/// </summary>
/// <remarks>
/// 参考里这两段一个在界面、一个在 <c>Battle::Unit</c>；端口把「单位在哪」和「单位长什么样」一起留在界面层，
/// 游戏层不碰像素。弹道需要同两段算术，所以单独拿出来，绘制器和弹道共用。
/// </remarks>
public static class BattleTroopSprite
{
    /// <summary>Vertical shift of every board anchored sprite, <c>cellYOffset</c> (<c>battle_interface.cpp:53</c>).</summary>
    public const int CellYOffset = -9;

    /// <summary>
    /// The top left corner of a unit's sprite, <c>GetTroopPosition()</c> (<c>battle_interface.cpp:502-527</c>).
    /// </summary>
    /// <param name="unit">The unit that owns the sprite.</param>
    /// <param name="sprite">The sprite itself, whose own offsets take part in the arithmetic.</param>
    /// <returns>Where the sprite is blitted, before any march offset is added.</returns>
    public static Point GetPosition(BattleUnit unit, Sprite sprite)
    {
        Rect area = unit.Position.GetRect();

        int offsetX;

        if (unit.IsReflect)
        {
            offsetX = unit.IsWide
                ? area.X + (area.Width / 2) + (area.Width / 4) - sprite.Width - sprite.OffsetX + 1
                : area.X + (area.Width / 2) - sprite.Width - sprite.OffsetX + 1;
        }
        else
        {
            offsetX = unit.IsWide
                ? area.X + (area.Width / 4) + sprite.OffsetX
                : area.X + (area.Width / 2) + sprite.OffsetX;
        }

        int offsetY = area.Y + area.Height + sprite.OffsetY + CellYOffset;

        return new Point(offsetX, offsetY);
    }

    /// <summary>
    /// The point a shot is aimed at, <c>Battle::Unit::GetCenterPoint()</c> (<c>battle_troop.cpp:1261-1271</c>):
    /// the middle of the cell, lifted by half the sprite's own vertical offset.
    /// </summary>
    /// <param name="unit">The unit to aim at.</param>
    /// <param name="sprite">Its current frame, whose offset lifts the point off the cell.</param>
    /// <returns>The point in surface coordinates.</returns>
    public static Point GetCenterPoint(BattleUnit unit, Sprite sprite)
    {
        Rect area = unit.Position.GetRect();
        int centerY = area.Y + area.Height + (sprite.OffsetY / 2) - 10;

        return new Point(area.X + (area.Width / 2), centerY);
    }
}
