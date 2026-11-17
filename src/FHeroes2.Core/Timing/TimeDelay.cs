using System.Diagnostics;

namespace FHeroes2.Core.Timing;

/// <summary>
/// A stopwatch that answers "has the delay passed?", the reference's <c>fheroes2::TimeDelay</c>
/// (<c>src/engine/timing.h:57</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>timing.h:57-113</c>。计时从构造那一刻起算，<see cref="IsPassed()"/> 比较的是「距上次
/// <see cref="Reset"/> 的毫秒数」，比较用 <c>&gt;=</c>。
/// </para>
/// <para>
/// 差异：参考直接读 <c>std::chrono::steady_clock</c>；端口把读数做成可注入的 <c>Func&lt;long&gt;</c>，
/// 默认仍是单调时钟（<see cref="Stopwatch"/>），测试传自己的假时钟就能精确控制「过了多久」，
/// 不必真的 sleep。参考里 <see cref="Pass"/> 用 <c>now - 2 * delay</c> 实现「下一次必定已过」，
/// 端口照抄。
/// </para>
/// </remarks>
public class TimeDelay
{
    private readonly Func<long> _clock;

    private long _previousMs;
    private ulong _delayMs;

    /// <summary>Creates a delay of <paramref name="delayMs"/> milliseconds, counting from now.</summary>
    /// <param name="delayMs">How long the delay lasts.</param>
    /// <param name="clockMs">
    /// Monotonic clock in milliseconds; <see langword="null"/> uses <see cref="Stopwatch"/>.
    /// </param>
    public TimeDelay(ulong delayMs, Func<long>? clockMs = null)
    {
        _delayMs = delayMs;
        _clock = clockMs ?? ReadMonotonicMilliseconds;

        Reset();
    }

    /// <summary>How many milliseconds the delay lasts, the reference's <c>getDelay</c>/<c>setDelay</c>.</summary>
    public ulong Delay
    {
        get => _delayMs;
        set => _delayMs = value;
    }

    /// <summary>Whether the delay has passed since the last reset.</summary>
    /// <remarks>参考：<c>timing.h:84</c>。</remarks>
    public bool IsPassed() => IsPassed(_delayMs);

    /// <summary>Whether <paramref name="delayMs"/> milliseconds have passed since the last reset.</summary>
    /// <remarks>参考：<c>timing.h:89-94</c>。</remarks>
    public bool IsPassed(ulong delayMs)
    {
        long now = _clock();

        // 参考比较的是无符号的毫秒差；假时钟往回走时不制造回绕，直接当作未到。
        if (now < _previousMs)
        {
            return false;
        }

        return (ulong)(now - _previousMs) >= delayMs;
    }

    /// <summary>Restarts the count from now.</summary>
    /// <remarks>参考：<c>timing.h:97</c>。</remarks>
    public void Reset() => _previousMs = _clock();

    /// <summary>
    /// Puts the delay into the passed state, so the very next <see cref="IsPassed()"/> answers
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>参考：<c>timing.h:105</c>——把起点拨回 <c>now - 2 * delay</c>。</remarks>
    public void Pass() => _previousMs = _clock() - (long)(2 * _delayMs);

    /// <summary>Monotonic milliseconds, the port's stand-in for <c>steady_clock</c>.</summary>
    private static long ReadMonotonicMilliseconds() =>
        (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
}
