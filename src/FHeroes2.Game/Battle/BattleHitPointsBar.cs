namespace FHeroes2.Game.Battle;

/// <summary>
/// The geometry of a unit's hit point bar, <c>Battle::Interface::drawHitPointsBar()</c>
/// (<c>battle_interface.cpp:2202-2228</c>).
/// </summary>
/// <remarks>
/// <para>
/// 条子压在计数条（<c>TEXTBAR</c>）的正下方：外面一圈 117 号色描边，里面先铺满「已损失」的红色，
/// 再从左往右盖一截「剩余」的绿色。绿红两色由 <c>fheroes2::GetColorId( 0, 200, 0 )</c> /
/// <c>GetColorId( 200, 0, 0 )</c> 从调色板里找，这一步留在界面层（那里才拿得到调色板）；这里只放
/// 与调色板无关的那部分——两层矩形的宽度与颜色常量。
/// </para>
/// <para>
/// 计算留在游戏层，绘制留在界面层，这样「剩下三分之二的血，绿条该多宽」可以脱离资产直接断言。
/// </para>
/// </remarks>
public static class BattleHitPointsBar
{
    /// <summary>Height of the drawn bar, <c>hitPointsBarHeight = 4</c> (<c>battle_interface.cpp:2251</c>).</summary>
    public const int Height = 4;

    /// <summary>Width of the frame on each side, the <c>+ 1</c> and <c>- 2</c> of the reference.</summary>
    public const int Frame = 1;

    /// <summary>Palette index of the frame, <c>outlineColor{ 117 }</c>.</summary>
    public const byte OutlineColor = 117;

    /// <summary>Red channel of the remaining hit points, <c>GetColorId( 0, 200, 0 )</c>.</summary>
    public const byte RemainingRed = 0;

    /// <summary>Green channel of the remaining hit points, <c>GetColorId( 0, 200, 0 )</c>.</summary>
    public const byte RemainingGreen = 200;

    /// <summary>Blue channel of the remaining hit points, <c>GetColorId( 0, 200, 0 )</c>.</summary>
    public const byte RemainingBlue = 0;

    /// <summary>Red channel of the missing hit points, <c>GetColorId( 200, 0, 0 )</c>.</summary>
    public const byte MissingRed = 200;

    /// <summary>Green channel of the missing hit points, <c>GetColorId( 200, 0, 0 )</c>.</summary>
    public const byte MissingGreen = 0;

    /// <summary>Blue channel of the missing hit points, <c>GetColorId( 200, 0, 0 )</c>.</summary>
    public const byte MissingBlue = 0;

    /// <summary>
    /// Width between the two frame pixels, <c>innerHitPointsBarWidth</c>.
    /// </summary>
    /// <param name="barWidth">Width of the counter bar the hit point bar hangs under.</param>
    /// <returns>The inner width, or zero when the bar is too narrow to hold any fill.</returns>
    public static int GetInnerWidth(int barWidth) => barWidth - (Frame * 2);

    /// <summary>Height between the two frame pixels, <c>innerHitPointsBarHeight</c>.</summary>
    /// <remarks>参考恒为 <see cref="Height"/> 减两像素；单独留一个属性是因为两处填色都要用它。</remarks>
    public static int InnerHeight => Height - (Frame * 2);

    /// <summary>
    /// The width the green part of the bar takes, <c>remainingHitPointsWidth</c>
    /// (<c>battle_interface.cpp:2216-2218</c>).
    /// </summary>
    /// <param name="barWidth">Width of the counter bar, <c>troopCountBar.width()</c>.</param>
    /// <param name="remainingHitPoints">Hit points left on the whole stack.</param>
    /// <param name="maximumHitPoints">Hit points of one creature, <c>unit.GetMonster().GetHitPoints()</c>.</param>
    /// <returns>
    /// At least one pixel while the stack is alive, at most the inner width, and zero when there is nothing to fill.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 参考的 <c>std::clamp</c> 下限是 1——哪怕只剩一滴血也画出一像素的绿，免得看起来像空条。上限是内宽，
    /// 所以满血时绿条正好铺满内框。
    /// </para>
    /// <para>
    /// <b>偏差</b>：参考用 <c>assert</c> 要求 <paramref name="remainingHitPoints"/> 与
    /// <paramref name="maximumHitPoints"/> 都大于零且剩余不超过最大；端口是提前返回零——界面对已经死掉的
    /// 队伍也会调到这里（截断动画的最后一帧），与其让断言在发布版里变成除零，不如什么都不画。
    /// </para>
    /// </remarks>
    public static int GetRemainingWidth(int barWidth, uint remainingHitPoints, uint maximumHitPoints)
    {
        int innerWidth = GetInnerWidth(barWidth);

        if (innerWidth <= 0 || remainingHitPoints == 0 || maximumHitPoints == 0)
        {
            return 0;
        }

        // 参考先把剩余血量扩成 uint64 再乘，免得宽条子上的大血量溢出 32 位。
        int width = (int)((ulong)remainingHitPoints * (ulong)innerWidth / maximumHitPoints);

        return Math.Clamp(width, 1, innerWidth);
    }
}
