using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Rendering.Present;

/// <summary>
/// Uploads the indexed <see cref="ScreenBuffer"/> to the back end and scales it to the output
/// surface. Implementations live in the platform layer (MonoGame, headless test double, …).
/// </summary>
public interface IScreenPresenter : IDisposable
{
    /// <summary>Current output surface size in device pixels.</summary>
    Size OutputSize { get; }

    /// <summary>Viewport that maps the logical frame onto the output surface.</summary>
    PresentationViewport Viewport { get; }

    /// <summary>Pushes the dirty region into the GPU texture and draws the scaled frame.</summary>
    void Present(ScreenBuffer frame);
}
