using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App;

/// <summary>
/// A window that owns the input and the frame until it closes, the port's stand-in for the blocking
/// dialogs of the reference (<c>Dialog::Message</c>, <c>Dialog::YES_NO</c>, <c>Game::OpenHeroesDialog</c>
/// and the rest of the blocking calls of <c>gui/interface_events.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这些函数是同步的：进函数、画框、自己转事件循环、关掉才返回结果。端口的主循环是单线程的
/// 场景状态机（每帧由 <see cref="GameHost"/> 拉一次），所以「同步返回」只能靠在宿主里再起一层帧循环：
/// <see cref="GameHost.RunModal"/> 反复取事件、把输入交给窗口、重绘「场景 + 窗口」，直到
/// <see cref="IsFinished"/>。契约照参考的调用面写，覆盖层才能像参考那样「开 → 等 → 取结果」。
/// </para>
/// <para>
/// 输入面就是 UI 层对话框和控件吃的那一套（<c>SetCursor</c> / <c>SetLeftButton</c> / 标准按钮热键），
/// 因为 <c>MessageBox</c>、<c>HeroDialog</c>、<c>SpellBookWindow</c> 都已经按这个形状写好了。什么算
/// 「关掉」由窗口自己决定，宿主不去猜。
/// </para>
/// </remarks>
public interface IModalScreen
{
    /// <summary>Whether the window has closed; the pump returns as soon as this turns <see langword="true"/>.</summary>
    bool IsFinished { get; }

    /// <summary>Pushes the cursor position; <see langword="null"/> when the pointer left the window.</summary>
    /// <param name="position">Cursor position in logical pixels.</param>
    void SetCursor(Point? position);

    /// <summary>Pushes a left button press or release.</summary>
    /// <param name="down">Whether the button is now held.</param>
    void SetLeftButton(bool down);

    /// <summary>Pushes a right button press or release.</summary>
    /// <param name="down">Whether the button is now held.</param>
    void SetRightButton(bool down);

    /// <summary>Pushes the key that stands for one of the standard buttons.</summary>
    /// <param name="hotKey">Key that was pressed.</param>
    void HotKey(ButtonHotKey hotKey);

    /// <summary>Draws the window over the frame of the scene below it.</summary>
    /// <param name="frame">Indexed frame the scene has just painted.</param>
    void Draw(ScreenBuffer frame);

    /// <summary>Advances the animations of the window, once per pumped frame.</summary>
    /// <param name="frame">Indexed frame the scene has just painted.</param>
    /// <param name="elapsedSeconds">Time since the previous pumped frame.</param>
    void Update(ScreenBuffer frame, double elapsedSeconds);
}
