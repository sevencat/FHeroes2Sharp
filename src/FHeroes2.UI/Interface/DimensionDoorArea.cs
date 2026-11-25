using FHeroes2.Core.Geometry;
using FHeroes2.Game;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The square a Dimension Door may land in and the rule for which tile of it may be picked, the geometry half of
/// <c>Interface::AdventureMap::GetDimensionDoorDestination</c> (<c>game_interface.cpp:206-217</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把这块算成像素矩形（<c>spellROI</c>）：以英雄格中心为心，边长 <c>(distance + 1)</c> 格，再按界面是否
/// 隐藏裁到屏幕边上。它同时是「框内可点」的判定范围与「框外压暗」的排除区，所以两处必须用同一个矩形。
/// </para>
/// <para>
/// 单独成类是为了能测：真正的点选循环要等鼠标，这个矩形与判定是纯函数。
/// </para>
/// </remarks>
public static class DimensionDoorArea
{
    /// <summary>
    /// The square of tiles a Dimension Door may reach, <c>spellROI</c>.
    /// </summary>
    /// <param name="heroPosition">
    /// Centre of the hero's tile in frame pixels, <c>_gameArea.GetRelativeTilePosition( … ) + TILEWIDTH / 2</c>.
    /// </param>
    /// <param name="gameArea">The visible map area, <c>_gameArea.GetROI()</c>.</param>
    /// <param name="distance">Spell points spent, which is the number of tiles the door reaches.</param>
    /// <param name="isHideInterface">Whether the four panels of the screen are hidden, <c>conf.isHideInterfaceEnabled</c>.</param>
    /// <returns>The square, in frame pixels and not clipped, exactly like the reference.</returns>
    public static Rect Compute(Point heroPosition, Rect gameArea, int distance, bool isHideInterface)
    {
        int offset = GameBorder.TileWidth * (distance / 2);
        int width = Math.Min(GameBorder.TileWidth * (distance + 1), gameArea.Width);
        int height = Math.Min(GameBorder.TileWidth * (distance + 1), gameArea.Height);

        int x = heroPosition.X - offset;
        int y = heroPosition.Y - offset;

        // 界面不隐藏时框贴着地图边框，不往面板下面钻（参考同样只在非隐藏界面的分支里收这一步）。
        if (!isHideInterface)
        {
            x = Math.Max(x, GameBorder.BorderWidth);
            y = Math.Max(y, GameBorder.BorderWidth);
        }

        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// Whether the tile under the cursor may be picked as the destination, the reference's
    /// <c>dst &lt; 0 || !spellROI &amp; le.getMouseCursor() || !Maps::isValidForDimensionDoor( dst, isWater )</c>.
    /// </summary>
    /// <param name="spellArea">The square of <see cref="Compute"/>.</param>
    /// <param name="cursor">Where the pointer is, in frame pixels.</param>
    /// <param name="targetTile">Tile index under the pointer, negative when there is none.</param>
    /// <param name="world">The map, asked <c>Maps::isValidForDimensionDoor</c>.</param>
    /// <param name="isWater">Whether the hero's spell path is over water, <c>isHeroWaterPath</c>.</param>
    /// <returns><see langword="true"/> when a click there would land the hero.</returns>
    /// <exception cref="ArgumentNullException">The world is <see langword="null"/>.</exception>
    public static bool CanSelect(Rect spellArea, Point cursor, int targetTile, World world, bool isWater)
    {
        ArgumentNullException.ThrowIfNull(world);

        return targetTile >= 0 && spellArea.Contains(cursor) && world.IsValidForDimensionDoor(targetTile, isWater);
    }
}
