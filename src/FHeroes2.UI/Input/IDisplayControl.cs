using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Input;

/// <summary>
/// The display half of the platform: full screen state, window size, the resolutions the host can switch to and the
/// renderer reset that follows a change. Plays the role of <c>fheroes2::engine()</c> as far as
/// <c>dialog_graphics_settings.cpp</c> and <c>dialog_resolution.cpp</c> reach into it.
/// </summary>
/// <remarks>
/// <para>
/// The reference splits the job: <c>engine().isFullScreen()</c>, <c>engine().setFullScreen()</c> and
/// <c>engine().getAvailableResolutions()</c> belong to the platform engine, while <c>Display::screenSize()</c>,
/// <c>Display::setResolution()</c> and <c>Display::resetRenderer()</c> belong to the display. The port keeps both
/// behind this one interface because it owns a single window and a single frame buffer; the application side
/// (<c>SdlDisplayControl</c>) is what couples them.
/// </para>
/// <para>
/// It hangs off <see cref="IEventEngine.Display"/>: the platform already owns the window, and a host without one
/// (the test fakes) reports <see langword="null"/> instead of pretending the switches work.
/// </para>
/// </remarks>
public interface IDisplayControl
{
    /// <summary>Whether the window is full screen, <c>engine().isFullScreen()</c>.</summary>
    bool IsFullScreen { get; }

    /// <summary>Pixel size of the window, <c>Display::screenSize()</c>.</summary>
    /// <remarks>The logic size — <see cref="IEventEngine.ScreenArea"/> — is what the scenes draw into.</remarks>
    Size ScreenSize { get; }

    /// <summary>
    /// The resolutions the host can switch to, <c>engine().getAvailableResolutions()</c>, largest window first.
    /// </summary>
    /// <returns>The available resolutions; empty when the host cannot enumerate them.</returns>
    IReadOnlyList<ResolutionInfo> GetAvailableResolutions();

    /// <summary>Switches between windowed and full screen, <c>engine().setFullScreen()</c>.</summary>
    /// <param name="enable">Whether the window has to become full screen.</param>
    void SetFullScreen(bool enable);

    /// <summary>Resizes the window, <c>Display::setResolution()</c>.</summary>
    /// <param name="resolution">The resolution to switch to.</param>
    void SetResolution(ResolutionInfo resolution);

    /// <summary>
    /// Rebuilds the renderer after a scaling change, <c>Display::resetRenderer()</c>.
    /// </summary>
    void ResetRenderer();
}
