using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// A message box laid over a window that is already running in the host's inner loop, the reference's
/// <c>Dialog::Message</c> / <c>Dialog::ZERO</c> calls inside another dialog.
/// </summary>
/// <remarks>
/// <para>
/// 参考里这些提示框是从对话框的循环里再开一层模态循环（<c>showStandardTextMessage</c> 自己不返回，直到玩家点掉），
/// 但端口的窗口已经跑在 <c>GameHost.RunModal</c> 的内层循环里，不能再套一层宿主循环，所以提示框由窗口自己画在
/// 自己上面，生命周期由这个类记：<c>Dialog::OK</c> 是「按一下就收起」，<c>Dialog::ZERO</c> 是「松开右键就消失」。
/// </para>
/// <para>
/// 城镇屏用同样的两条规则（<c>CastleScene</c> 的 <c>_message</c> / <c>_messageHeldByRightButton</c>），这里把它抽成
/// 一份，供地图上开出来的窗口共用。
/// </para>
/// </remarks>
internal sealed class ModalMessage
{
    private readonly MessageBox _box;
    private readonly bool _heldByRightButton;

    /// <summary>Wraps an already composed box.</summary>
    /// <param name="box">The box to draw.</param>
    /// <param name="heldByRightButton">
    /// Whether the box lives only while the right button is held, the <c>Dialog::ZERO</c> of the reference; otherwise
    /// the next left press or ESC takes it down, <c>Dialog::OK</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public ModalMessage(MessageBox box, bool heldByRightButton)
    {
        ArgumentNullException.ThrowIfNull(box);

        _box = box;
        _heldByRightButton = heldByRightButton;
    }

    /// <summary>The composed box, exposed so a caller can read the text it shows.</summary>
    public MessageBox Box => _box;

    /// <summary>Whether the box is a <c>Dialog::ZERO</c>, which the release of the right button takes down.</summary>
    public bool IsHeldByRightButton => _heldByRightButton;

    /// <summary>Whether the player took the box down; the owner drops it on the next frame.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Records the pointer, which the button row of the box would need; the boxes of the map have none.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor) => _box.SetCursor(cursor);

    /// <summary>Records the left button; a press takes down every box but the one that belongs to the right button.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down)
    {
        if (down && !_heldByRightButton)
        {
            IsClosed = true;
        }
    }

    /// <summary>Records the right button; its release takes down a <c>Dialog::ZERO</c>.</summary>
    /// <param name="down">Whether the right button is now held.</param>
    public void SetRightButton(bool down)
    {
        if (!down && _heldByRightButton)
        {
            IsClosed = true;
        }
    }

    /// <summary>Records ESC and Enter, which take down every box but the one that belongs to the right button.</summary>
    /// <param name="hotKey">Key that was pressed, already folded into a standard button.</param>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (!_heldByRightButton && hotKey is ButtonHotKey.Okay or ButtonHotKey.Cancel)
        {
            IsClosed = true;
        }
    }

    /// <summary>Paints the box.</summary>
    /// <param name="target">Indexed frame buffer of the window below it.</param>
    public void Draw(PixelBuffer target) => _box.Draw(target);

    /// <summary>Repaints the animated elements of the box.</summary>
    /// <param name="target">Indexed frame buffer of the window below it.</param>
    /// <param name="elapsed">Time since the previous call.</param>
    public void Update(PixelBuffer target, TimeSpan elapsed) => _box.Update(target, elapsed);
}
