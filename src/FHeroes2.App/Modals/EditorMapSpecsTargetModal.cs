using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The candidate list the condition panels ask for on a click of their selected row, the
/// <c>SelectMapCastle</c> / <c>SelectMapHero</c> window the reference opens inside its event loop
/// (<c>editor_map_specs_window.cpp:180-395</c>) and runs to the end.
/// </summary>
/// <remarks>
/// <para>
/// 参考在 <c>SelectMapObject</c> 里阻塞地把列表窗跑完，再拿 <c>GetCenter()</c> 写回条件目标；端口把这一段折成模态：
/// <see cref="EditorMapSpecsTargetPicker"/> 自己读 <see cref="LocalEvent.Current"/>（选择、双击、滚动、右键说明都在它
/// 和 <see cref="ItemSelectionWindow"/> 里），本类只做三个转换——把帧交给它、把按钮热键补给它、把
/// <see cref="SelectedPosition"/> 报给调用方。
/// </para>
/// <para>
/// 热键走一条单独的通道：<see cref="HotKey"/> 收到的 <see cref="ButtonHotKey"/> 不能当场交给窗口，否则同一帧的鼠标会被
/// 读两遍（列表的一次单击会变成双击），所以先存着，下一次 <see cref="Update"/> 连同鼠标一起交过去。
/// </para>
/// </remarks>
internal sealed class EditorMapSpecsTargetModal : IModalScreen
{
    private readonly EditorMapSpecsTargetPicker _picker;

    private ButtonHotKey? _hotKey;

    /// <summary>Wraps the picker.</summary>
    /// <param name="picker">The list to run.</param>
    /// <exception cref="ArgumentNullException">The picker is <see langword="null"/>.</exception>
    public EditorMapSpecsTargetModal(EditorMapSpecsTargetPicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);

        _picker = picker;
    }

    /// <summary>The list being run; its <see cref="ItemSelectionWindow.Result"/> is the answer.</summary>
    public EditorMapSpecsTargetPicker Picker => _picker;

    /// <summary>Whether the author accepted the selection instead of dropping it.</summary>
    public bool Accepted => _picker.Result == DialogButton.Ok;

    /// <summary>
    /// The tile to write back through <see cref="EditorMapSpecsInput.ResolveTarget"/>, the
    /// <c>GetCenter()</c> of the reference: <see langword="null"/> when the window was cancelled or lists nothing.
    /// </summary>
    public Point? SelectedPosition => Accepted ? _picker.SelectedPosition : null;

    /// <inheritdoc />
    public bool IsFinished => _picker.IsClosed;

    /// <inheritdoc />
    /// <remarks>列表窗自己读 <see cref="LocalEvent.Current"/>，与消息框、消息窗的输入面不一样。</remarks>
    public void SetCursor(Point? position)
    {
        // 光标已经在 LocalEvent 里了，窗口下一帧自己会读到。
        _ = position;
    }

    /// <inheritdoc />
    public void SetLeftButton(bool down) => _ = down;

    /// <inheritdoc />
    public void SetRightButton(bool down) => _ = down;

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => _hotKey = hotKey;

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _picker.Draw(frame.Pixels);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _ = elapsedSeconds;

        if (IsFinished)
        {
            return;
        }

        _ = _picker.ProcessEvents(LocalEvent.Current, _hotKey);
        _hotKey = null;
    }
}
