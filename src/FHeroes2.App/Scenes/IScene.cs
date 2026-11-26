using FHeroes2.UI.Widgets;

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

    /// <summary>
    /// The full-screen fade-in this frame still has to play, <c>fheroes2::fadeInDisplay()</c>.
    /// </summary>
    /// <returns>The fade, or <see langword="null"/> when nothing is pending.</returns>
    /// <remarks>
    /// 参考的淡入是阻塞的六帧、每帧 <c>Display::render()</c>；端口把呈现留在宿主，所以场景在
    /// <see cref="DrawFrame"/> 里把待播的淡入交出来（<c>BaseInterface.TakePendingFadeIn</c>），
    /// <see cref="GameHost"/> 在把画面推上屏之前用 <see cref="DisplayFade.TryAdvance"/> 播一帧。
    /// 只有会画 <c>Interface::BaseInterface</c> 那一套的场景有淡入，其余场景保持 <see langword="null"/>。
    /// </remarks>
    DisplayFade? TakePendingFadeIn() => null;
}
