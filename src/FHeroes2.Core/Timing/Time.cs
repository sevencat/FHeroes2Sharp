using System.Diagnostics;

namespace FHeroes2.Core.Timing;

/// <summary>
/// Elapsed milliseconds since the last reset, the reference's <c>fheroes2::Time</c>
/// (<c>参考：src/engine/timing.h:31-60</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这是 <c>steady_clock</c> 起点加两个读数函数（<c>getS</c>、<c>getMs</c>）。端口把读数做成可注入的
/// <c>Func&lt;long&gt;</c>，默认仍是单调时钟，和 <see cref="TimeDelay"/> 一致：测试传假时钟就能精确控制
/// 「过了多久」，不必真的 sleep。
/// </para>
/// <para>
/// 参考的 <c>getMs</c> 用 <c>duration_cast</c> 截断，端口同样向下取整；假时钟往回走时不制造回绕，直接报 0。
/// </para>
/// </remarks>
public class Time
{
    private readonly Func<long> _clock;

    private long _startMs;

    /// <summary>Creates a timer counting from now.</summary>
    /// <param name="clockMs">
    /// Monotonic clock in milliseconds; <see langword="null"/> uses <see cref="Stopwatch"/>.
    /// </param>
    public Time(Func<long>? clockMs = null)
    {
        _clock = clockMs ?? ReadMonotonicMilliseconds;

        Reset();
    }

    /// <summary>Elapsed milliseconds since the last reset, the reference's <c>getMs</c>.</summary>
    public ulong GetMs()
    {
        long now = _clock();

        return now > _startMs ? (ulong)(now - _startMs) : 0;
    }

    /// <summary>Elapsed seconds since the last reset, the reference's <c>getS</c>.</summary>
    public double GetSeconds() => GetMs() / 1000.0;

    /// <summary>Restarts the count from now, the reference's <c>reset</c>.</summary>
    public void Reset() => _startMs = _clock();

    /// <summary>Monotonic milliseconds, the port's stand-in for <c>steady_clock</c>.</summary>
    private static long ReadMonotonicMilliseconds() =>
        (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
}
