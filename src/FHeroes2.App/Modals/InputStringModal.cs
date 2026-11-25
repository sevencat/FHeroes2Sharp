using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The riddle input window run on the host's frame pump, <c>Dialog::inputString()</c> of the sphinx.
/// </summary>
/// <remarks>
/// 窗口自己带整块背景（输入区的图是完整的一格），所以关窗后不需要留底——宿主的 <c>Draw</c> 每帧先重画场景再叠窗口。
/// 右键在这里没有作用：参考的 <c>QUICK_INFO</c> 分支在输入窗里只是再弹一遍问句，端口还没接。
/// </remarks>
internal sealed class InputStringModal : IModalScreen
{
    private readonly InputStringWindow _window;

    /// <summary>Wraps an already built input window.</summary>
    /// <param name="window">The window to run.</param>
    /// <exception cref="ArgumentNullException">The window is <see langword="null"/>.</exception>
    public InputStringModal(InputStringWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <summary>The window that was run; its <see cref="InputStringWindow.Result"/> is the answer.</summary>
    public InputStringWindow Window => _window;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <inheritdoc />
    public void SetCursor(Point? position) => _window.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => _window.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down)
    {
        _ = down;

        // 参考右键是 QUICK_INFO（把问句再弹一遍），留给问句窗接线时一起做。
    }

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey)
    {
        _ = _window.PressHotKey(hotKey);
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier)
    {
        _ = _window.PressKey(key, modifier);
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _ = elapsedSeconds;

        _ = _window.Update();
    }
}
