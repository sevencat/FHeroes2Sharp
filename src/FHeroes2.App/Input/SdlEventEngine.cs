using FHeroes2.Core.Geometry;
using FHeroes2.UI.Input;

namespace FHeroes2.App.Input;

/// <summary>
/// The SDL half of <see cref="LocalEvent.HandleEvents"/>, the port of the reference's
/// <c>EventProcessing::EventEngine</c> (<c>localevent.cpp:1265-1313</c>).
/// </summary>
/// <remarks>
/// <para>
/// The host keeps owning SDL: <paramref name="poll"/> drains the event queue and pushes the events
/// into <see cref="LocalEvent"/>, <paramref name="present"/> uploads the frame. This class only
/// connects them to the loop of <see cref="LocalEvent.HandleEvents"/>, so a dialog can be written as
/// <c>while ( le.HandleEvents() ) { ... }</c> without <c>FHeroes2.UI</c> ever seeing SDL.
/// </para>
/// <para>
/// The reference's engine also polls the gamepads, the touch devices and the window resize events.
/// The port has none of them yet: a resize is presented by <see cref="Start"/>'s caller and the
/// gamepad stays unported (<c>Input/LocalEvent.cs</c> says so), which is why
/// <c>displayRefreshRequired</c> is always <see langword="false"/>.
/// </para>
/// </remarks>
internal sealed class SdlEventEngine : IEventEngine
{
    private readonly Func<bool, bool> _poll;
    private readonly Action<Rect> _present;
    private readonly Rect _screenArea;

    /// <summary>Connects the host callbacks to the event loop of <see cref="LocalEvent"/>.</summary>
    /// <param name="poll">
    /// Drains the SDL queue; receives the <c>allowExit</c> flag and returns <see langword="false"/> when
    /// the client has to stop.
    /// </param>
    /// <param name="present">Uploads the frame; the area is a hint, the port always uploads all of it.</param>
    /// <param name="screenArea">The logical frame, the bounds of the reference's display.</param>
    /// <exception cref="ArgumentNullException">A callback is <see langword="null"/>.</exception>
    public SdlEventEngine(Func<bool, bool> poll, Action<Rect> present, Rect screenArea)
    {
        _poll = poll ?? throw new ArgumentNullException(nameof(poll));
        _present = present ?? throw new ArgumentNullException(nameof(present));
        _screenArea = screenArea;
    }

    /// <inheritdoc />
    public Rect ScreenArea => _screenArea;

    /// <inheritdoc />
    public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
    {
        ArgumentNullException.ThrowIfNull(state);

        // No palette cycling and no resize feed the loop yet, so the whole frame is only redrawn when
        // the caller asks for it.
        displayRefreshRequired = false;

        return _poll(allowExit);
    }

    /// <inheritdoc />
    public void Render(Rect area) => _present(area);

    /// <inheritdoc />
    public void Sleep(ulong milliseconds) => Thread.Sleep((int)milliseconds);
}
