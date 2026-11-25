using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Army;

/// <summary>
/// The two entry points that draw an army as one line of small portraits, <c>Army::drawSingleDetailedMonsterLine()</c>
/// and <c>Army::drawMultipleMonsterLines()</c> (<c>army.cpp:1605-1639</c>).
/// </summary>
/// <remarks>
/// <para>
/// Thin geometry around <see cref="MiniMonsters.Draw"/>, and the geometry is the whole of it: a one line army is
/// centred in two thirds of the space and dropped by half a row, and an army of three or more is broken into a
/// front line of two and a back line of the rest, the back line narrower when it also holds two.
/// </para>
/// <para>
/// <b>Where the reference draws to the screen</b> — <c>fheroes2::Display::instance()</c> — the port takes the
/// surface as a parameter, because the port has no display singleton. Same for the portraits: the reference asks
/// <c>Assets::getImage()</c> on every call, while callers here pass a <see cref="IMiniMonsterSprites"/> that caches.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// Draws an army as one full detail line, <c>Army::drawSingleDetailedMonsterLine()</c>
    /// (<c>army.cpp:1605</c>).
    /// </summary>
    /// <param name="troops">The army to draw.</param>
    /// <param name="cx">Left edge of the room the line may use.</param>
    /// <param name="cy">Top edge of the line.</param>
    /// <param name="width">Width of the room the line may use.</param>
    /// <param name="output">The image to draw into.</param>
    /// <param name="sprites">Where the portraits come from.</param>
    public static void DrawSingleDetailedMonsterLine(
        Army troops,
        int cx,
        int cy,
        int width,
        PixelBuffer output,
        IMiniMonsterSprites sprites) => MiniMonsters.Draw(troops, cx, cy, width, 0, 0, false, true, false, 0, output, sprites);

    /// <summary>
    /// Draws an arbitrary list of stacks as one full detail line, <c>Army::drawSingleDetailedMonsterLine()</c> over a
    /// <c>Troops</c> vector (<c>army.cpp:1605</c>).
    /// </summary>
    /// <remarks>
    /// 战斗结果窗画「阵亡部队」时传的就是参考的 <c>Troops</c>（不限长），而端口的 <see cref="Army"/> 固定七槽，
    /// 所以这条重载直接吃列表。摆放与军队行相同：从右往左，第一项在最右。
    /// </remarks>
    /// <param name="troops">The stacks to draw.</param>
    /// <param name="cx">Left edge of the room the line may use.</param>
    /// <param name="cy">Top edge of the line.</param>
    /// <param name="width">Width of the room the line may use.</param>
    /// <param name="output">The image to draw into.</param>
    /// <param name="sprites">Where the portraits come from.</param>
    public static void DrawSingleDetailedMonsterLine(
        IReadOnlyList<Troop> troops,
        int cx,
        int cy,
        int width,
        PixelBuffer output,
        IMiniMonsterSprites sprites) => MiniMonsters.Draw(troops, cx, cy, width, output, sprites);

    /// <summary>
    /// Draws an army as one or two lines of portraits, <c>Army::drawMultipleMonsterLines()</c>
    /// (<c>army.cpp:1610</c>).
    /// </summary>
    /// <remarks>
    /// Two stacks fit on one line; from the third on the line becomes a front row of two and a back row of the
    /// rest, which is how the army window shows a full army without shrinking the portraits. The back row of a
    /// four stack army is narrowed to two thirds so that its two portraits sit under the front two rather than
    /// spread out beneath them.
    /// </remarks>
    /// <param name="troops">The army to draw.</param>
    /// <param name="posX">Left edge of the room the lines may use.</param>
    /// <param name="posY">Top edge of the first line.</param>
    /// <param name="lineWidth">Width of the room the lines may use.</param>
    /// <param name="compact">Whether to draw the short form with the counts beside the portraits.</param>
    /// <param name="detailedView">Whether the counts are printed as numbers.</param>
    /// <param name="garrisonView">Whether the counts may be hidden by a thieves' guild.</param>
    /// <param name="thievesGuildCount">How many thieves' guilds the viewer has.</param>
    /// <param name="output">The image to draw into.</param>
    /// <param name="sprites">Where the portraits come from.</param>
    public static void DrawMultipleMonsterLines(
        Army troops,
        int posX,
        int posY,
        int lineWidth,
        bool compact,
        bool detailedView,
        bool garrisonView,
        uint thievesGuildCount,
        PixelBuffer output,
        IMiniMonsterSprites sprites)
    {
        ArgumentNullException.ThrowIfNull(troops);

        uint count = troops.OccupiedSlotCount;

        if (count == 0)
        {
            return;
        }

        int offsetX = lineWidth / 6;
        int offsetY = compact ? 31 : 49;

        if (count < 3)
        {
            MiniMonsters.Draw(
                troops,
                posX + offsetX,
                (posY + (offsetY / 2)) + 1,
                lineWidth * 2 / 3,
                0,
                0,
                compact,
                detailedView,
                garrisonView,
                thievesGuildCount,
                output,
                sprites);

            return;
        }

        const uint FirstLineTroopCount = 2;

        uint secondLineTroopCount = count - FirstLineTroopCount;
        int secondLineWidth = secondLineTroopCount == 2 ? lineWidth * 2 / 3 : lineWidth;

        MiniMonsters.Draw(
            troops,
            posX + offsetX,
            posY,
            lineWidth * 2 / 3,
            0,
            FirstLineTroopCount,
            compact,
            detailedView,
            garrisonView,
            thievesGuildCount,
            output,
            sprites);

        MiniMonsters.Draw(
            troops,
            posX,
            posY + offsetY,
            secondLineWidth,
            FirstLineTroopCount,
            secondLineTroopCount,
            compact,
            detailedView,
            garrisonView,
            thievesGuildCount,
            output,
            sprites);
    }
}
