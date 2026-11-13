namespace FHeroes2.Client.Desktop.Scenes;

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
    void DrawFrame(in DiagnosticsState state);

    /// <summary>Paints the vector text overlay, in logical (640x480) pixel space.</summary>
    void DrawOverlay(in DiagnosticsState state);
}
