namespace FHeroes2.Core.Geometry;

/// <summary>
/// Evenly spaced points along a straight line, <c>fheroes2::getLinePoints()</c> (<c>math_tools.cpp:39-82</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference walks a projectile or a shot by sampling the line at a fixed pixel step, which is how the battle
/// board asks "does this shot cross a standing wall section" without doing real line-of-sight geometry. The sample count
/// is the rounded-up distance divided by the step, and the points are spread evenly over the whole line, so the first and
/// the last point are always exactly the two ends.
/// </para>
/// <para>
/// 中点用累计的浮点偏移逐步推进而不是每次重新乘，参考如此——重算会与它差一个像素，而城墙判定的每个采样点都要
/// 落在格子矩形里，差一像素就可能穿墙。步长为零或为负时参考退化成「只有两端」。
/// </para>
/// </remarks>
public static class LinePoints
{
    /// <summary>
    /// The points of the line from <paramref name="from"/> to <paramref name="to"/>, both included.
    /// </summary>
    /// <param name="from">The start point.</param>
    /// <param name="to">The end point.</param>
    /// <param name="step">The wanted spacing in pixels.</param>
    /// <returns>The sampled points, two when the line is shorter than two steps.</returns>
    public static Point[] GetLinePoints(Point from, Point to, int step)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        double distance = Math.Sqrt((double)dx * dx + (double)dy * dy);

        // 四舍五入的整数除法，并避开 step 为零时的除零。
        int intervals = step > 0 ? (int)((distance + (step / 2)) / step) : 0;

        if (intervals < 2)
        {
            // 点太少时参考直接给出两端；两端重合时只给一个点。
            if (from == to)
            {
                return [from];
            }

            return [from, to];
        }

        double stepX = dx / (double)intervals;
        double stepY = dy / (double)intervals;

        Point[] line = new Point[intervals + 1];
        line[0] = from;

        double offsetX = from.X;
        double offsetY = from.Y;

        for (int i = 1; i < intervals; ++i)
        {
            offsetX += stepX;
            offsetY += stepY;

            line[i] = new Point((int)offsetX, (int)offsetY);
        }

        line[intervals] = to;

        return line;
    }
}
