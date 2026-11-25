using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;

namespace FHeroes2.UI.Battle;

/// <summary>
/// Draws a unit's hit point bar, <c>Battle::Interface::drawHitPointsBar()</c>
/// (<c>battle_interface.cpp:2202-2228</c>).
/// </summary>
/// <remarks>
/// <para>
/// 条子挂在计数条下面：<c>y</c> 取计数条的底边，宽取计数条的宽，高固定四像素。外面一圈
/// <see cref="BattleHitPointsBar.OutlineColor"/>，里面一像素的边距，先铺红（已损失）再铺绿（剩余）。
/// 两个颜色是调色板索引，调用方从 <c>fheroes2::GetColorId()</c> 的对应物算好传进来（战斗界面用注入的
/// 调色板，设置窗用 <c>KB.PAL</c>），这里只管填色，因而可以脱离资产单测。
/// </para>
/// <para>
/// 参考把宽度与剩余量算成整数，这里的宽度算法在 <see cref="BattleHitPointsBar.GetRemainingWidth"/> 里。
/// </para>
/// </remarks>
public static class BattleHitPointsBarPainter
{
    /// <summary>
    /// Fills the bar under a counter bar.
    /// </summary>
    /// <param name="target">The battlefield surface.</param>
    /// <param name="barPosition">
    /// Top left corner of the counter bar, <c>troopCountBarPosition</c>; the hit point bar starts at its bottom.
    /// </param>
    /// <param name="barSize">Size of the counter bar, <c>troopCountBar</c>; only its width and height are used.</param>
    /// <param name="remainingHitPoints">Hit points left on the stack, <c>unit.GetHitPointsLeft()</c>.</param>
    /// <param name="maximumHitPoints">Hit points of one creature, <c>unit.GetMonster().GetHitPoints()</c>.</param>
    /// <param name="remainingColor">Palette index of the green part.</param>
    /// <param name="missingColor">Palette index of the red part.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void Draw(PixelBuffer target, Point barPosition, Size barSize, uint remainingHitPoints,
        uint maximumHitPoints, byte remainingColor, byte missingColor)
    {
        ArgumentNullException.ThrowIfNull(target);

        int x = barPosition.X;
        int y = barPosition.Y + barSize.Height;
        int innerWidth = BattleHitPointsBar.GetInnerWidth(barSize.Width);
        int innerHeight = BattleHitPointsBar.InnerHeight;

        ImageOps.FillRect(target, new Rect(x, y, barSize.Width, BattleHitPointsBar.Height),
            BattleHitPointsBar.OutlineColor);

        if (innerWidth <= 0 || innerHeight <= 0)
        {
            return;
        }

        ImageOps.FillRect(target, new Rect(x + 1, y + 1, innerWidth, innerHeight), missingColor);

        int remainingWidth = BattleHitPointsBar.GetRemainingWidth(barSize.Width, remainingHitPoints, maximumHitPoints);

        if (remainingWidth > 0)
        {
            ImageOps.FillRect(target, new Rect(x + 1, y + 1, remainingWidth, innerHeight), remainingColor);
        }
    }
}
