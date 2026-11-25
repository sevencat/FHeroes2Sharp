using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The recruit window run as a window of the adventure map, <c>Dialog::RecruitMonster()</c> of the dwellings on the
/// map (<c>heroes_action.cpp:276</c>).
/// </summary>
/// <remarks>
/// <para>
/// 城镇屏里的招募窗是城堡屏的一个弹层（<see cref="CastleScene"/> 自己转它的帧），地图上的居所没有第二个场景位，
/// 只能像问句窗那样借宿主的内层循环：关掉之后 <see cref="RecruitWindow.Selected"/> 就是买下的那一队。
/// </para>
/// <para>
/// 窗口本身已经带整块背景（含标题栏与四颗按钮），所以关窗后不需要留底——宿主的 <c>Draw</c> 每帧先重画场景再叠窗口。
/// </para>
/// </remarks>
internal sealed class RecruitWindowModal : IModalScreen
{
    private readonly RecruitWindow _window;

    /// <summary>Wraps an already built recruit window.</summary>
    /// <param name="window">The window to run.</param>
    /// <exception cref="ArgumentNullException">The window is <see langword="null"/>.</exception>
    public RecruitWindowModal(RecruitWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <summary>The window that was run; its <see cref="RecruitWindow.Selected"/> is the answer.</summary>
    public RecruitWindow Window => _window;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <inheritdoc />
    public void SetCursor(Point? position) => _window.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => _window.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => _window.SetRightButton(down);

    /// <inheritdoc />
    /// <remarks>
    /// 参考里这两种键不是同一个意思：ESC 取消，回车只在 OKAY 按钮可用时才算（<c>dialog_recruit.cpp:617-620</c>），
    /// 所以一个交给 <see cref="RecruitWindow.PressClose"/>、一个交给 <see cref="RecruitWindow.PressOkay"/>。
    /// </remarks>
    public void HotKey(ButtonHotKey hotKey)
    {
        switch (hotKey)
        {
            case ButtonHotKey.Cancel:
                _window.PressClose();

                break;

            case ButtonHotKey.Okay:
                _window.PressOkay();

                break;

            default:
                break;
        }
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

        _window.Update();
    }
}
