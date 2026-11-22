using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.UI.Input;
using SDL3;

namespace FHeroes2.App.Graphics;

/// <summary>
/// <see cref="IDisplayControl"/> on top of <see cref="SdlPresenter"/>: what the graphics settings page and the
/// resolution dialog reach into, the <c>fheroes2::engine()</c> and <c>fheroes2::Display</c> of the reference.
/// </summary>
/// <remarks>
/// The window modes come straight from SDL; the reference additionally guesses a few common resolutions when a
/// platform reports too few of them. The port lists what the system reports and nothing else, so the dialog never
/// offers a mode the window cannot actually be set to.
/// </remarks>
internal sealed class SdlDisplayControl : IDisplayControl
{
    /// <summary>Smallest window the port offers, the 640x480 frame buffer, <c>IsLowerThanDefaultRes()</c>.</summary>
    private const int MinimumWidth = 640;

    /// <summary>Smallest window the port offers, the 640x480 frame buffer, <c>IsLowerThanDefaultRes()</c>.</summary>
    private const int MinimumHeight = 480;

    private readonly SdlPresenter _presenter;

    /// <summary>Wraps a presenter.</summary>
    /// <param name="presenter">The presenter that owns the window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="presenter"/> is <see langword="null"/>.</exception>
    public SdlDisplayControl(SdlPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        _presenter = presenter;
    }

    /// <inheritdoc/>
    public bool IsFullScreen => _presenter.IsFullscreen;

    /// <inheritdoc/>
    public Size ScreenSize => _presenter.OutputSize;

    /// <inheritdoc/>
    public IReadOnlyList<ResolutionInfo> GetAvailableResolutions()
    {
        SortedSet<ResolutionInfo> modes = [];

        uint[]? displays = SDL.GetDisplays(out int displayCount);

        for (int index = 0; displays is not null && index < displayCount && index < displays.Length; ++index)
        {
            SDL.DisplayMode[]? displayModes =
                SDL.GetFullscreenDisplayModes(displays[index], out int modeCount);

            for (int modeIndex = 0;
                 displayModes is not null && modeIndex < modeCount && modeIndex < displayModes.Length;
                 ++modeIndex)
            {
                SDL.DisplayMode mode = displayModes[modeIndex];

                if (mode.W < MinimumWidth || mode.H < MinimumHeight)
                {
                    continue;
                }

                modes.Add(new ResolutionInfo(mode.W, mode.H));
            }
        }

        if (modes.Count == 0)
        {
            GameLog.Warn("The system reports no usable display mode; only the 640x480 default is offered.");

            return [new ResolutionInfo(MinimumWidth, MinimumHeight)];
        }

        // SortedSet walks from the smallest to the largest; the reference offers the largest window first.
        ResolutionInfo[] ordered = [.. modes];
        Array.Reverse(ordered);

        return ordered;
    }

    /// <inheritdoc/>
    public void SetFullScreen(bool enable) => _presenter.SetFullscreen(enable);

    /// <inheritdoc/>
    public void SetResolution(ResolutionInfo resolution) => _presenter.SetResolution(resolution);

    /// <inheritdoc/>
    public void ResetRenderer() => _presenter.ResetRenderer();
}
