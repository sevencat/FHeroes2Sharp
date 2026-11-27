using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Credits;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Video;

/// <summary>
/// The credits of a finished campaign, <c>Game::ShowCredits()</c> (<c>game_credits.cpp:701-820</c>), on the host's
/// frame pump.
/// </summary>
/// <remarks>
/// <para>
/// 参考在 50 毫秒一格的循环里推进 <see cref="CreditsSequence"/>：淡入、停顿、淡出、换页；任何一个键或鼠标键都能
/// 立刻结束。端口把计时交给 <see cref="Update"/>（按 <c>elapsedSeconds</c> 累积），把「跳过」交给三种输入回调——
/// 与 <see cref="VideoModalScreen"/> 同一套接法。
/// </para>
/// <para>
/// <b>满亮度时不重画</b>：参考在 <c>NO_ACTION</c> 状态只递增计数、不画这一帧（<c>:779-784</c>）；端口的
/// <see cref="Update"/> 照做，省掉 5 秒里那 100 次完全相同的重绘。
/// </para>
/// </remarks>
internal sealed class CreditsModalScreen : IModalScreen
{
    /// <summary>Milliseconds between two steps, <c>animationDelay</c> of <c>:723</c>.</summary>
    public const int StepMilliseconds = 50;

    private readonly CreditsView _view;
    private readonly CreditsSequence _sequence;

    private double _elapsed;

    /// <summary>Wraps the view so the frame pump can run it.</summary>
    /// <param name="view">The pages to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public CreditsModalScreen(CreditsView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
        _sequence = new CreditsSequence(view.Pages.Count);
    }

    /// <summary>The view being shown.</summary>
    public CreditsView View => _view;

    /// <summary>The fade and page position, exposed so a test can step it.</summary>
    public CreditsSequence Sequence => _sequence;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <inheritdoc />
    public void SetCursor(Point? position)
    {
    }

    /// <inheritdoc />
    public void SetLeftButton(bool down)
    {
        if (down)
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc />
    public void SetRightButton(bool down)
    {
        if (down)
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc />
    /// <remarks>参考的跳过条件是「任意键」，所以两个标准按钮键与其它键一样。</remarks>
    public void HotKey(ButtonHotKey hotKey) => IsFinished = true;

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => IsFinished = true;

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            return;
        }

        _elapsed += Math.Max(0, elapsedSeconds);

        double step = StepMilliseconds / 1000.0;

        while (_elapsed >= step)
        {
            _elapsed -= step;

            _sequence.Increment();

            // 满亮度那一段参考不重画：只把计数器往前走。
            if (_sequence.State != CreditsAnimationState.NoAction)
            {
                frame.InvalidateAll();
            }
        }

        // 首帧：序列刚建好时是黑的，也要让屏幕画一次。
        if (_sequence.PageId == 0 && _sequence.Alpha == 0 && _sequence.State == CreditsAnimationState.FadingIn)
        {
            frame.InvalidateAll();
        }
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _view.Draw(frame.Pixels, frame.Palette, _sequence);

        frame.InvalidateAll();
    }
}
