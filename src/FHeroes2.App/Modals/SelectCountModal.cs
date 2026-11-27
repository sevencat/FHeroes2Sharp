using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The "set the amount" window, <c>Dialog::SelectCount()</c> (<c>dialog_selectcount.cpp:79-225</c>), as a modal.
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="MessageBoxModal"/> 同一种接法：窗口只认光标与左键，答案写在 <see cref="SelectCountView.Dialog"/>
/// 的 <see cref="SelectCountDialog.Result"/> 上，关窗时取 <see cref="Result"/>。
/// </para>
/// <para>
/// 参考的循环还带两处现开的小窗——数字框右键的「范围」提示、上下键长按的重复——前者落在
/// <see cref="ValueSelectionElement.RangeMessage"/> 上，端口没有地方显示；后者只有
/// <see cref="ValueSelectionElement.ProcessEvents"/> 才有，而这一层走的是
/// <see cref="SelectCountView.SetLeftButton"/> 那条路，一次点击改一格。
/// </para>
/// </remarks>
internal sealed class SelectCountModal : IModalScreen
{
    private readonly SelectCountView _view;

    private Point? _cursor;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="view">The window to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public SelectCountModal(SelectCountView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
    }

    /// <summary>The window being run.</summary>
    public SelectCountView View => _view;

    /// <summary>
    /// The number the author chose, or <see langword="null"/> when the window was cancelled; the
    /// <see cref="SelectCountDialog.Result"/> of the window.
    /// </summary>
    public int? Result => _view.Dialog.Result;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <inheritdoc />
    public void SetCursor(Point? position)
    {
        _cursor = position;
        _view.Buttons.SetCursor(position);
    }

    /// <inheritdoc />
    public void SetLeftButton(bool down)
    {
        if (_cursor is not { } point)
        {
            return;
        }

        Finish(_view.SetLeftButton(down, point));
    }

    /// <summary>
    /// Ignored: the reference's right clicks only show help boxes, and this window has none of its own.
    /// </summary>
    /// <param name="down">Whether the button is now held.</param>
    public void SetRightButton(bool down)
    {
    }

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Finish(_view.ProcessHotKey(hotKey));

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _view.Draw(frame.Pixels);
    }

    /// <summary>Nothing animates: the window is a frame, a number and two buttons.</summary>
    /// <param name="frame">Unused.</param>
    /// <param name="elapsedSeconds">Unused.</param>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);
    }

    /// <summary>Closes the window as soon as a button reports an answer.</summary>
    /// <param name="answer">The button that was pressed, or <see cref="DialogButton.None"/>.</param>
    private void Finish(DialogButton answer)
    {
        if (answer == DialogButton.None)
        {
            return;
        }

        IsFinished = true;
    }
}
