namespace FHeroes2.Core.Timing;

/// <summary>
/// Frame timing helper: keeps a smoothed frames-per-second value and exposes the elapsed
/// wall clock time used by animations.
/// </summary>
public sealed class FrameTimer
{
    private readonly double _smoothing;
    private double _accumulatedSeconds;
    private int _accumulatedFrames;

    public FrameTimer(double smoothing = 0.1)
    {
        if (smoothing is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothing), smoothing, "Smoothing must be in (0, 1).");
        }

        _smoothing = smoothing;
    }

    /// <summary>Smoothed frames per second.</summary>
    public double FramesPerSecond { get; private set; }

    /// <summary>Total elapsed seconds since the timer was created.</summary>
    public double TotalSeconds { get; private set; }

    /// <summary>Duration of the last frame in seconds.</summary>
    public double LastFrameSeconds { get; private set; }

    /// <summary>Number of frames registered so far.</summary>
    public long FrameCount { get; private set; }

    public void Tick(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
        {
            return;
        }

        ++FrameCount;
        LastFrameSeconds = elapsedSeconds;
        TotalSeconds += elapsedSeconds;

        _accumulatedSeconds += elapsedSeconds;
        ++_accumulatedFrames;

        if (_accumulatedSeconds < 0.25)
        {
            return;
        }

        double measured = _accumulatedFrames / _accumulatedSeconds;
        FramesPerSecond = FrameCount == _accumulatedFrames
            ? measured
            : (FramesPerSecond * (1.0 - _smoothing)) + (measured * _smoothing);

        _accumulatedSeconds = 0;
        _accumulatedFrames = 0;
    }

    public void Reset()
    {
        _accumulatedSeconds = 0;
        _accumulatedFrames = 0;
        FramesPerSecond = 0;
        LastFrameSeconds = 0;
        TotalSeconds = 0;
        FrameCount = 0;
    }
}
