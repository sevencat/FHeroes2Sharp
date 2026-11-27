using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The number pad a click on the edit box of a value element opens, <c>openVirtualNumpad()</c>
/// (<c>ui_keyboard.cpp:1295-1370</c>), run on the host's frame pump.
/// </summary>
/// <remarks>
/// <para>
/// 参考是个 <c>while ( action != DialogAction::Close )</c> 的阻塞函数，循环里只认键盘本身；关窗（点确定、回车或 ESC，
/// <c>Game::HotKeyCloseWindow()</c> 把后两者都算成关窗）之后才 <c>std::stoi</c> 并比范围，不合格就
/// <c>showStandardTextMessage</c> 弹一句错误、把 action 改回 <c>DoNothing</c> 继续循环——所以**越界不会关掉键盘**。
/// 端口把这层循环折成模态：错误框摞在 <see cref="EditorMapSpecsModal"/> 的同一个 <c>_inner</c> 思路下（自己这一层的栈），
/// 关掉错误框后接着打。
/// </para>
/// <para>
/// 参考这里没有「取消」：ESC 与回车一样是关窗，一样走校验。端口不做补充，免得凭空多一条参考没有的出口。
/// </para>
/// </remarks>
internal sealed class EditorMapSpecsValueModal : IModalScreen
{
    private readonly VirtualKeyboardView _pad;
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private ButtonHotKey? _hotKey;

    /// <summary>Wraps the pad.</summary>
    /// <param name="pad">The number pad to run.</param>
    /// <param name="minimum">Smallest number the field accepts.</param>
    /// <param name="maximum">Largest number the field accepts.</param>
    /// <param name="host">Where the error boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorMapSpecsValueModal(VirtualKeyboardView pad, int minimum, int maximum, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(pad);
        ArgumentNullException.ThrowIfNull(host);

        _pad = pad;
        _minimum = minimum;
        _maximum = maximum;
        _host = host;
    }

    /// <summary>The number the author typed, or <see langword="null"/> while the pad is still open.</summary>
    public int? Value { get; private set; }

    /// <summary>The pad being run.</summary>
    public VirtualKeyboardView Pad => _pad;

    /// <summary>Whether an error box is stacked above the pad.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The message on top of the pad, or <see langword="null"/> when none is open.</summary>
    internal MessageBoxModal? TopMessage => _inner.Count > 0 ? _inner.Peek() as MessageBoxModal : null;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    /// <remarks>
    /// 错误框还开着时热键归它；没有内层窗时先记下，下一帧才和鼠标一起交给键盘——参考的键盘关窗条件就是
    /// <c>Game::HotKeyCloseWindow()</c>，两个热键都算。
    /// </remarks>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (Top is { } inner)
        {
            inner.HotKey(hotKey);

            return;
        }

        _hotKey = hotKey;
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _pad.Draw(frame.Pixels);

        Top?.Draw(frame);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            return;
        }

        // 错误框还开着：这一帧只归它，键盘既不读事件也不改数字。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        // 两个热键在参考里都是「关窗」，与点确定同一条路。
        bool closeRequested = _hotKey is ButtonHotKey.Okay or ButtonHotKey.Cancel;
        _hotKey = null;

        VirtualKeyboardResult result = _pad.ProcessEvents(LocalEvent.Current, closeRequested);

        // 右键钥匙要说明框：参考的 processVirtualKeyboardEvent 也是就地弹一层。
        if (result.Popup is { } popup)
        {
            Push(_host.TryCreateMessageBox(popup.Header, popup.Body));

            return;
        }

        if (result.Action == KeyboardDialogAction.Close)
        {
            Accept();
        }
    }

    /// <summary>Turns the typed text into a number, the <c>Close</c> arm of the reference's loop.</summary>
    /// <remarks>越界与打不成数字都只弹错误框、键盘留着，与参考把 action 改回 <c>DoNothing</c> 一致。</remarks>
    private void Accept()
    {
        switch (VirtualKeyboardView.ReadNumpad(_pad.Text, _minimum, _maximum, out int value))
        {
            case VirtualNumpadParse.Valid:
                Value = value;
                IsFinished = true;

                return;

            case VirtualNumpadParse.Invalid:
                Push(_host.TryCreateMessageBox(_pad.Labels.ErrorTitle, _pad.Labels.InvalidValue));

                return;

            default:
                Push(_host.TryCreateMessageBox(_pad.Labels.ErrorTitle,
                    _pad.Labels.GetOutOfRangeMessage(_minimum, _maximum)));

                return;
        }
    }

    /// <summary>Stacks an error box above the pad; a missing one is logged and the pad stays usable.</summary>
    /// <param name="message">The box, or <see langword="null"/> when the fonts are missing.</param>
    private void Push(MessageBoxModal? message)
    {
        if (message is null)
        {
            GameLog.Warn("The number pad has to report a bad value, and the box is missing.");

            return;
        }

        _inner.Push(message);
    }
}
