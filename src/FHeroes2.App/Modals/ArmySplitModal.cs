using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The split question, <c>Dialog::ArmySplitTroop()</c> (<c>dialog_selectcount.cpp:462-651</c>), as a modal.
/// </summary>
/// <remarks>
/// 与 <see cref="SelectCountModal"/> 同一种接法：窗口只认光标与左键（数字框的箭头、槽位那排、两颗按钮），
/// 关窗时取 <see cref="SlotCount"/>、<see cref="UseFastSplit"/> 与 <see cref="Count"/>，交给
/// <see cref="ArmyBar.ApplySplit"/>。
/// </remarks>
internal sealed class ArmySplitModal : IModalScreen
{
    private readonly ArmySplitView _view;

    private Point? _cursor;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="view">The window to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public ArmySplitModal(ArmySplitView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
    }

    /// <summary>The window being run.</summary>
    public ArmySplitView View => _view;

    /// <summary>
    /// The button that closed the window, <see cref="DialogButton.Ok"/> or <see cref="DialogButton.Cancel"/>;
    /// <see cref="DialogButton.None"/> while it is still open.
    /// </summary>
    public DialogButton Answer { get; private set; }

    /// <summary>How many slots the author chose, the <c>slots</c> the reference returns.</summary>
    public int SlotCount => _view.SlotCount;

    /// <summary>Whether the equal split was chosen, the <c>useFastSplit</c> out parameter of the reference.</summary>
    public bool UseFastSplit => _view.UseFastSplit;

    /// <summary>The number of units the author chose to move.</summary>
    public int Count => _view.Count;

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

    /// <summary>Nothing animates: the window is a frame, a number, a row of icons and two buttons.</summary>
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

        Answer = answer;
        IsFinished = true;
    }
}
