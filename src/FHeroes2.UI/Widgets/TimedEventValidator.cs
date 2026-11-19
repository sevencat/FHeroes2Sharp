using FHeroes2.Core.Timing;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Turns "the button is held down" into "the button repeats", a port of <c>fheroes2::TimedEventValidator</c>
/// (<c>src/fheroes2/gui/ui_tool.h:227-259</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference wires it to the button it repeats: <c>button.subscribe( &amp;validator )</c> makes every state change
/// of the button (<c>press()</c>, <c>release()</c>, <c>disable()</c>, …) call <c>senderUpdate()</c>, which restarts both
/// delays, and <c>isDelayPassed()</c> then answers true once the button has been held for the first delay
/// (<c>500</c> ms by default) and again every between delay (<c>100</c> ms) while it stays held.
/// </para>
/// <para>
/// <b>The port passes the held state in instead of subscribing.</b> <see cref="ButtonBase"/> has no observers — its
/// documentation says so — and the held state is already computed every frame by
/// <see cref="ButtonBase.UpdatePressState"/>; <see cref="IsDelayPassed(bool)"/> takes it as an argument and detects the
/// state change itself, which is exactly when the reference would have notified. The rest is the same: the first delay
/// fires once per gesture, the between delay restarts on every fire, and nothing fires while the button is up.
/// </para>
/// <para>
/// Used by the arrows that step through towns and heroes (<c>castle_mageguild.cpp:76-85</c>,
/// <c>castle_town.cpp:449-464</c>, <c>castle_dialog.cpp:331-346</c>), the heroes dialog
/// (<c>heroes_dialog.cpp:354-360</c>), the marketplace sides (<c>dialog_marketplace.cpp:464-465</c>) and the recruit
/// dialog's amount arrows (<c>dialog_recruit.cpp:335-336</c>).
/// </para>
/// </remarks>
public sealed class TimedEventValidator
{
    /// <summary>The countdown to the first repeat, <c>_delayBeforeFirstUpdateMs</c>.</summary>
    private readonly TimeDelay _beforeFirstUpdate;

    /// <summary>The countdown between two repeats, <c>_delayBetweenUpdateMs</c>.</summary>
    private readonly TimeDelay _betweenUpdates;

    /// <summary>Whether the last frame had the button held, the stand-in for <c>senderUpdate()</c>.</summary>
    private bool _wasHeld;

    /// <summary>Creates a validator, <c>TimedEventValidator( verification, delayBeforeFirstUpdateMs, delayBetweenUpdateMs )</c>.</summary>
    /// <param name="delayBeforeFirstUpdateMs">How long the first repeat waits, <c>500</c> in the reference.</param>
    /// <param name="delayBetweenUpdatesMs">The gap between two repeats, <c>100</c> in the reference.</param>
    /// <param name="clockMs">
    /// Monotonic clock in milliseconds; <see langword="null"/> uses <see cref="TimeDelay"/>'s own clock.
    /// </param>
    public TimedEventValidator(
        ulong delayBeforeFirstUpdateMs = 500,
        ulong delayBetweenUpdatesMs = 100,
        Func<long>? clockMs = null)
    {
        _beforeFirstUpdate = new TimeDelay(delayBeforeFirstUpdateMs, clockMs);
        _betweenUpdates = new TimeDelay(delayBetweenUpdatesMs, clockMs);
    }

    /// <summary>
    /// Whether the held button should act again this frame, <c>isDelayPassed()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// The first delay and the between delay must both have passed, and the button must be held — the reference's
    /// <c>_verification()</c>. Firing restarts only the between delay, so a held button keeps repeating at that pace;
    /// a new gesture restarts both, because pressing or releasing notifies the validator.
    /// </remarks>
    /// <param name="isHeld">
    /// Whether the button is held down this frame, the value of <c>button.isPressed()</c> that the reference's
    /// verification closure reads.
    /// </param>
    /// <returns><see langword="true"/> when the button should act again.</returns>
    public bool IsDelayPassed(bool isHeld)
    {
        // press() and release() both notify the subscriber, which resets both delays.
        if (isHeld != _wasHeld)
        {
            _wasHeld = isHeld;
            Reset();
        }

        if (!isHeld)
        {
            return false;
        }

        if (_beforeFirstUpdate.IsPassed() && _betweenUpdates.IsPassed())
        {
            _betweenUpdates.Reset();
            return true;
        }

        return false;
    }

    /// <summary>Restarts both delays, what <c>senderUpdate()</c> does.</summary>
    public void Reset()
    {
        _beforeFirstUpdate.Reset();
        _betweenUpdates.Reset();
    }
}
