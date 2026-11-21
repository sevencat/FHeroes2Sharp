using System.Globalization;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The clock and FPS readout the reference paints in the bottom left corner of the screen, a port of
/// <c>fheroes2::SystemInfoRenderer</c> (<c>src/fheroes2/gui/ui_tool.h:203-225</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_tool.cpp:283</c>。位置是硬编码的：左边距 <c>26</c> 像素、距屏幕下沿
/// <c>30</c> 像素（<c>offsetY = display.height() - 30</c>）。文字用 <c>FontType::normalWhite()</c>。
/// </para>
/// <para>
/// FPS 是「最近一秒内画了多少帧」：每次 <see cref="BuildInfo"/> 把上次调用到现在的耗时压进队首，
/// 只保留累计不超过一秒的样本，再取 <c>样本数 / 累计时间 + 0.05</c>。参考的 <c>+ 0.05</c> 是把
/// 刚好整数的平均值抬到上一个整数（<c>59.0 → 59</c> 而不是 <c>58</c>），端口照抄。
/// </para>
/// <para>
/// 差异一：参考从 <c>steady_clock</c> 与 <c>std::time</c> 取时间，端口把两者都做成可注入的委托，
/// 测试传假时钟就能精确断言「一秒内 60 帧 → FPS 60」。
/// 差异二：参考的 <c>_( ", FPS: " )</c> 走 gettext，端口的文案还没有本地化子系统接管，先写死英文。
/// 差异三：参考在 <c>averageFps</c> 的分母为 0 时得到 <c>inf</c>，端口直接返回 0（时钟没有前进）。
/// </para>
/// <para>
/// 用它的是宿主：参考在 <c>Settings::isSystemInfoEnabled()</c> 为真时每帧调用
/// <c>preRender()</c>／<c>postRender()</c>（<c>game.cpp</c> 的主循环），端口在 <see cref="Draw"/> 里
/// 一步完成并把覆盖层的矩形返回给调用方标脏。
/// </para>
/// </remarks>
public sealed class SystemInfoRenderer
{
    /// <summary>Left edge of the readout, <c>offsetX</c> of the reference.</summary>
    public const int OffsetX = 26;

    /// <summary>Distance from the bottom edge of the screen, <c>display.height() - 30</c>.</summary>
    public const int OffsetYFromBottom = 30;

    /// <summary>How long the FPS window is, in seconds, the <c>1.0</c> of the reference.</summary>
    public const double WindowSeconds = 1.0;

    /// <summary>The leading text every readout starts with, <c>", FPS: "</c> of the reference.</summary>
    public const string FpsLabel = ", FPS: ";

    private readonly Func<long> _clockMs;
    private readonly Func<DateTime> _timeOfDay;

    /// <summary>Frame times of the last second, newest first, the reference's <c>_delays</c>.</summary>
    private readonly List<double> _delays = [];

    private long _previousMs;

    /// <summary>Creates a renderer, <c>SystemInfoRenderer::SystemInfoRenderer()</c>.</summary>
    /// <param name="clockMs">Monotonic clock in milliseconds; <see langword="null"/> reads the system one.</param>
    /// <param name="timeOfDay">Wall clock for the <c>HH:MM:SS</c> part; <see langword="null"/> uses the local time.</param>
    public SystemInfoRenderer(Func<long>? clockMs = null, Func<DateTime>? timeOfDay = null)
    {
        _clockMs = clockMs ?? ReadMonotonicMilliseconds;
        _timeOfDay = timeOfDay ?? (() => DateTime.Now);
        _previousMs = _clockMs();
    }

    /// <summary>
    /// Builds the readout, the body of the reference's <c>preRender()</c>: it advances the FPS window
    /// as a side effect, so it must be called once per frame.
    /// </summary>
    /// <returns><c>HH:MM:SS, FPS: 60</c> (or <c>FPS: 9.8</c> below ten frames per second).</returns>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.cpp:288-328</c>。</remarks>
    public string BuildInfo()
    {
        long now = _clockMs();
        double totalTime = Math.Max(0, now - _previousMs) / 1000.0;
        _previousMs = now;

        _delays.Insert(0, totalTime);

        // Remove all delays that exceed one second: the sample that crosses the window stays in.
        double allTime = 0;
        for (int i = 0; i < _delays.Count; ++i)
        {
            allTime += _delays[i];

            if (allTime > WindowSeconds)
            {
                if (_delays.Count > i + 1)
                {
                    _delays.RemoveRange(i + 1, _delays.Count - (i + 1));
                }

                break;
            }
        }

        double averageFps = allTime > 0 ? (_delays.Count / allTime) + 0.05 : 0;
        int integerFps = (int)averageFps;

        string info = _timeOfDay().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + FpsLabel +
                      integerFps.ToString(CultureInfo.InvariantCulture);

        if (integerFps < 10)
        {
            info += '.';
            info += ((int)((averageFps - integerFps) * 10)).ToString(CultureInfo.InvariantCulture);
        }

        return info;
    }

    /// <summary>The readout as drawable text, <c>FontType::normalWhite()</c> of the reference.</summary>
    /// <returns>The text, positioned by the caller or by <see cref="Draw"/>.</returns>
    public TextBase PreRender() => new Text(BuildInfo(), FontType.NormalWhite());

    /// <summary>Paints the readout into the bottom left corner and returns the area it covered.</summary>
    /// <param name="frameArea">Area of the frame that is being drawn, i.e. the whole screen.</param>
    /// <param name="output">Buffer to paint into.</param>
    /// <returns>The rectangle that has to be marked dirty; empty when the text could not be placed.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:330-337</c>（<c>_text.update()</c>、<c>_text.draw()</c> 与 <c>updateNextRenderRoi()</c>）。</remarks>
    public Rect Draw(in Rect frameArea, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        TextBase text = PreRender();
        int x = frameArea.X + OffsetX;
        int y = frameArea.Bottom - OffsetYFromBottom;

        Rect area = text.Area();
        Rect roi = new Rect(x, y, area.Width, area.Height).Intersect(frameArea);
        if (roi.IsEmpty)
        {
            return Rect.Empty;
        }

        text.DrawInRoi(x, y, output, roi);

        return roi;
    }

    /// <summary>Monotonic milliseconds, the port's stand-in for <c>steady_clock</c>.</summary>
    private static long ReadMonotonicMilliseconds() =>
        (long)(System.Diagnostics.Stopwatch.GetTimestamp() * (1000.0 / System.Diagnostics.Stopwatch.Frequency));
}
