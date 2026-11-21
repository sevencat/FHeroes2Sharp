using FHeroes2.Core.Geometry;
using FHeroes2.Core.Timing;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Inertial (smooth) scrolling of a draggable view, the reference's <c>fheroes2::UIScrollInertia</c>
/// (<c>参考：src/fheroes2/gui/ui_tool.h:348-407</c>、<c>ui_tool.cpp:950-1076</c>).
/// </summary>
/// <remarks>
/// <para>
/// While the user drags, every frame's displacement is recorded together with the drag timer
/// (<see cref="AddMovementStep"/>). On release the last 100 ms window is turned into a velocity in
/// 1/256 px/ms fixed point and — if it is faster than 0.3 px/ms — inertia starts
/// (<see cref="CommitRelease"/>). <see cref="Update"/> then decays that velocity by
/// <c>(200 - dt) / 200</c> per frame and shifts the view in half pixel steps until it is slower than
/// 0.05 px/ms.
/// </para>
/// <para>
/// <b>Deviation: the clock is injectable.</b> The reference reads <c>fheroes2::Time</c> directly; the port
/// takes the same two timers as <see cref="Time"/> instances that a test may give a fake clock, so the decay
/// curve and the boundary handling below are testable without sleeping. The arithmetic is otherwise the
/// reference's, including the truncating fixed point division and the thresholds 5929 (77²) and 169 (13²).
/// </para>
/// <para>
/// The reference keeps itself decoupled from any widget with the two callbacks of <see cref="Update"/>; the
/// port keeps that shape, so <see cref="Interface.GameArea"/> can hand over its own centre getter and setter.
/// </para>
/// </remarks>
public class ScrollInertia
{
    private const int MaxTrackingWindowMs = 100;
    private const int ReleaseSampleAgeMs = 50;
    private const int DecayTimeConstantMs = 200;
    private const int StartThresholdSquared = 5929; // ( 0.3 px/ms )², in 1/256 units
    private const int StopThresholdSquared = 169; // ( 0.05 px/ms )², in 1/256 units
    private const int FixedPointShift = 256;

    private readonly Queue<MovementStep> _movementSteps = new();
    private readonly Time _dragTimer;
    private readonly Time _timer;

    private int _velX;
    private int _velY;
    private int _subpixelShiftX;
    private int _subpixelShiftY;
    private bool _active;

    /// <summary>Creates an idle inertion handler.</summary>
    /// <param name="dragClockMs">Clock of the drag timer; <see langword="null"/> uses the monotonic clock.</param>
    /// <param name="inertiaClockMs">Clock of the inertia timer; <see langword="null"/> uses the monotonic clock.</param>
    public ScrollInertia(Func<long>? dragClockMs = null, Func<long>? inertiaClockMs = null)
    {
        _dragTimer = new Time(dragClockMs);
        _timer = new Time(inertiaClockMs);
    }

    /// <summary>Whether inertial scrolling is currently active, the reference's <c>isActive</c>.</summary>
    public bool IsActive => _active;

    /// <summary>
    /// Stops any active inertia and clears the recorded steps, the reference's <c>reset</c>.
    /// Call this at the beginning of a new drag.
    /// </summary>
    public void Reset()
    {
        _movementSteps.Clear();
        _dragTimer.Reset();
        _active = false;
        _velX = 0;
        _velY = 0;
        _subpixelShiftX = 0;
        _subpixelShiftY = 0;
    }

    /// <summary>
    /// Records one movement step, the reference's <c>addMovementStep</c>. Call this each frame while the
    /// user is dragging.
    /// </summary>
    /// <param name="delta">Displacement of this frame in pixels.</param>
    public void AddMovementStep(Point delta)
    {
        _movementSteps.Enqueue(new MovementStep(delta, _dragTimer.GetMs()));

        while (_movementSteps.Count > 2 && _movementSteps.Last().TimeMs - _movementSteps.Peek().TimeMs > MaxTrackingWindowMs)
        {
            _movementSteps.Dequeue();
        }
    }

    /// <summary>
    /// Turns the recorded steps into a velocity when the user releases, the reference's <c>commitRelease</c>.
    /// Inertia only starts when the release was recent and fast enough.
    /// </summary>
    public void CommitRelease()
    {
        if (_movementSteps.Count >= 2)
        {
            ulong now = _dragTimer.GetMs();
            ulong lastSampleAge = now - _movementSteps.Last().TimeMs;

            if (lastSampleAge < ReleaseSampleAgeMs)
            {
                ulong dt = _movementSteps.Last().TimeMs - _movementSteps.Peek().TimeMs;

                if (dt > 0)
                {
                    long sumX = 0;
                    long sumY = 0;

                    foreach (MovementStep step in _movementSteps)
                    {
                        sumX += step.Delta.X;
                        sumY += step.Delta.Y;
                    }

                    _velX = (int)(sumX * FixedPointShift / (long)dt);
                    _velY = (int)(sumY * FixedPointShift / (long)dt);

                    if ((_velX * _velX) + (_velY * _velY) > StartThresholdSquared)
                    {
                        _active = true;
                        _subpixelShiftX = 0;
                        _subpixelShiftY = 0;
                        _timer.Reset();
                    }
                    else
                    {
                        _velX = 0;
                        _velY = 0;
                    }
                }
            }
        }

        _movementSteps.Clear();
    }

    /// <summary>
    /// Advances inertia by one frame, the reference's <c>update</c>.
    /// </summary>
    /// <param name="getPosition">Reads the current scroll position.</param>
    /// <param name="setPosition">Writes a new scroll position, clamped by the view.</param>
    /// <returns><see langword="true"/> when a redraw is needed; <see langword="false"/> once inertia stopped.</returns>
    public bool Update(Func<Point> getPosition, Action<Point> setPosition)
    {
        ArgumentNullException.ThrowIfNull(getPosition);
        ArgumentNullException.ThrowIfNull(setPosition);

        if (!_active)
        {
            return false;
        }

        ulong elapsedTimeMs = _timer.GetMs();

        if (elapsedTimeMs == 0)
        {
            return false;
        }

        _timer.Reset();

        if (elapsedTimeMs >= DecayTimeConstantMs)
        {
            Deactivate();

            return false;
        }

        int dtMs = (int)elapsedTimeMs;
        _velX = _velX * (DecayTimeConstantMs - dtMs) / DecayTimeConstantMs;
        _velY = _velY * (DecayTimeConstantMs - dtMs) / DecayTimeConstantMs;

        if ((_velX * _velX) + (_velY * _velY) < StopThresholdSquared)
        {
            Deactivate();

            return false;
        }

        _subpixelShiftX += _velX * dtMs;
        _subpixelShiftY += _velY * dtMs;

        Point viewShift = new(_subpixelShiftX / FixedPointShift, _subpixelShiftY / FixedPointShift);

        _subpixelShiftX -= viewShift.X * FixedPointShift;
        _subpixelShiftY -= viewShift.Y * FixedPointShift;

        if (viewShift.X == 0 && viewShift.Y == 0)
        {
            return true;
        }

        Point beforeCenter = getPosition();

        setPosition(beforeCenter + viewShift);

        Point afterCenter = getPosition();

        if (beforeCenter == afterCenter)
        {
            Deactivate();

            return false;
        }

        if (viewShift.X != 0 && afterCenter.X == beforeCenter.X)
        {
            _velX = 0;
            _subpixelShiftX = 0;
        }

        if (viewShift.Y != 0 && afterCenter.Y == beforeCenter.Y)
        {
            _velY = 0;
            _subpixelShiftY = 0;
        }

        if (_velX == 0 && _velY == 0)
        {
            Deactivate();
        }

        return true;
    }

    /// <summary>Stops inertia and drops the recorded steps, the reference's <c>_deactivate</c>.</summary>
    private void Deactivate()
    {
        _active = false;
        _movementSteps.Clear();
    }

    /// <summary>One recorded displacement with the drag timer reading it was taken at, <c>MovementStep</c>.</summary>
    private readonly struct MovementStep
    {
        public MovementStep(Point delta, ulong timeMs)
        {
            Delta = delta;
            TimeMs = timeMs;
        }

        public Point Delta { get; }

        public ulong TimeMs { get; }
    }
}
