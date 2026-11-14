namespace FHeroes2.App.Scenes;

/// <summary>
/// A screen the host drives. <see cref="DrawFrame"/> paints the indexed 640x480 frame (the game
/// pixels), <see cref="DrawOverlay"/> paints GPU text on top of it, which is only needed by the
/// diagnostics screen - real screens draw their text with the game fonts.
/// </summary>
public interface IScene
{
    /// <summary>Advances time based state such as animations or the moving marker.</summary>
    void Update(double elapsedSeconds);

    /// <summary>Paints the indexed frame. Called before the frame is uploaded to the GPU.</summary>
    /// <remarks>
    /// The implementation has to mark the area it drew as dirty
    /// (<see cref="FHeroes2.Core.Imaging.ScreenBuffer.MarkDirty"/> or
    /// <see cref="FHeroes2.Core.Imaging.ScreenBuffer.InvalidateAll"/>), because the presenter only
    /// uploads the dirty region: pixels written straight into
    /// <see cref="FHeroes2.Core.Imaging.ScreenBuffer.Pixels"/> cannot report themselves, and a frame
    /// that marks nothing leaves the previous frame on screen. Scenes that repaint everything - the
    /// menu, the adventure map, the editor - call <c>InvalidateAll</c> first.
    /// </remarks>
    void DrawFrame(in DiagnosticsState state);

    /// <summary>Paints the vector text overlay, in logical (640x480) pixel space.</summary>
    void DrawOverlay(in DiagnosticsState state);
}
