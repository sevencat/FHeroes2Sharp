using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Cursor;

/// <summary>
/// The pointer the game draws and moves, the port of <c>fheroes2::cursor()</c>
/// (<c>src/engine/screen.h:289-359</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这是引擎里的全局单例：硬件光标只是把图标交给窗口系统，软件光标则由 <c>Display</c> 每帧把图标画进缓冲，
/// 并给出需要重绘的矩形。端口现在的指针是操作系统自带的，还没有对应实现（<c>engine/screen.cpp</c> 的软件光标属于
/// App 层，B 步不搬），所以这里先按 <c>cursor.cpp</c> 实际用到的最小面抽接口：
/// <see cref="CursorState"/> 只依赖这些成员，测试里给一个假实现即可把整套状态机跑起来。
/// </para>
/// <para>
/// 成员名与参考保持一一对应，便于把 <c>cursor.cpp</c> 的调用逐行对上；参考里 <c>update</c> 收的是
/// <c>fheroes2::Image</c>，这里收 <see cref="Sprite"/>（带锚点的图），因为光标图必然来自 ICN 帧。
/// </para>
/// </remarks>
public interface ICursorRenderer
{
    /// <summary>Whether the pointer is drawn right now, <c>isVisible()</c>.</summary>
    bool IsVisible { get; }

    /// <summary>
    /// Whether the pointer is drawn by the engine into the display buffer rather than by the window system,
    /// <c>isSoftwareEmulation()</c>.
    /// </summary>
    bool IsSoftwareEmulation { get; }

    /// <summary>
    /// Where the pointer is, <c>LocalEvent::Get().getMouseCursorPos()</c> of the reference. The host keeps this up to
    /// date from the mouse events it feeds the game.
    /// </summary>
    Point Position { get; }

    /// <summary>Shows or hides the pointer, <c>show( enable )</c>.</summary>
    void Show(bool enable);

    /// <summary>
    /// Replaces the pointer image, <c>update( image, offsetX, offsetY )</c>. The offsets are the negation of the
    /// theme offset, so that the anchor of the sprite lands on the pointer position.
    /// </summary>
    void Update(Sprite image, int offsetX, int offsetY);

    /// <summary>Moves the pointer, <c>setPosition( x, y )</c>.</summary>
    void SetPosition(int x, int y);

    /// <summary>Whether the pointer is clamped into the screen area, <c>keepInScreenArea( value )</c>.</summary>
    void KeepInScreenArea(bool keep);
}
