using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Input;

/// <summary>
/// The platform half of <see cref="LocalEvent.HandleEvents"/>, the reference's
/// <c>EventProcessing::EventEngine</c> (<c>localevent.h:40</c>, <c>localevent.cpp:1265-1313</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference's engine owns SDL: it polls the events and translates them into
/// <see cref="LocalEvent.OnMouseMotion"/>, <see cref="LocalEvent.OnMouseButton"/>,
/// <see cref="LocalEvent.OnKeyboard"/> and <see cref="LocalEvent.OnMouseWheel"/>, reports whether the
/// whole frame has to be redrawn, presents the requested area and sleeps until the next frame.
/// </para>
/// <para>
/// The port keeps that behind this interface so <c>FHeroes2.UI</c> stays free of SDL: the application
/// supplies the implementation (<c>SdlEventEngine</c>), a test supplies a fake one. This is what makes
/// the reference's blocking dialogs portable — a dialog writes <c>while ( le.HandleEvents() ) { ... }</c>
/// and does not know where the events come from.
/// </para>
/// </remarks>
public interface IEventEngine
{
    /// <summary>
    /// The whole logical frame, the bounds of the reference's <c>fheroes2::Display::instance()</c>.
    /// <see cref="LocalEvent.HandleEvents"/> redraws all of it when a refresh is required.
    /// </summary>
    Rect ScreenArea { get; }

    /// <summary>
    /// Polls the platform for the events of one frame and pushes them into <paramref name="state"/>,
    /// the reference's <c>EventEngine::handleEvents( le, allowExit, isDisplayRefreshRequired )</c>
    /// (<c>localevent.cpp:1265</c>).
    /// </summary>
    /// <param name="state">The state the events are pushed into.</param>
    /// <param name="allowExit">Whether a close request may end the loop.</param>
    /// <param name="displayRefreshRequired">
    /// Whether the whole frame has to be redrawn, the reference's <c>isDisplayRefreshRequired</c>:
    /// a window resize or an active palette cycle.
    /// </param>
    /// <returns><see langword="false"/> when the application has to stop.</returns>
    bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired);

    /// <summary>
    /// Presents the frame, <c>display.render( area )</c> (<c>localevent.cpp:1290</c>).
    /// </summary>
    /// <param name="area">
    /// The area that changed. The port uploads the whole frame, so this is a hint for a later
    /// incremental upload, not a promise that only these pixels are touched.
    /// </param>
    void Render(Rect area);

    /// <summary>Waits between two frames, <c>EventProcessing::EventEngine::sleep</c>.</summary>
    /// <param name="milliseconds">How long to wait.</param>
    void Sleep(ulong milliseconds);

    /// <summary>
    /// The display half of the engine, or <see langword="null"/> when the host has no window to control.
    /// </summary>
    /// <remarks>
    /// The reference reaches the same thing through the global <c>fheroes2::engine()</c>. The port keeps it on the
    /// engine the dialogs already hold; the application supplies a real implementation, the test fakes leave it
    /// <see langword="null"/> and the settings page then reports the switches as unavailable instead of pretending
    /// they worked.
    /// </remarks>
    IDisplayControl? Display => null;
}
