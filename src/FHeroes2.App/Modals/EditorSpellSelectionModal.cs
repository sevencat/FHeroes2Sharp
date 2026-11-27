using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The editor's spell selection window run on the host's frame pump, <c>openSpellSelectionWindow()</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的窗口是个阻塞函数，循环里先认两个热键再认鼠标（<c>editor_spell_selection.cpp:280-284</c>）：
/// <c>DEFAULT_CANCEL</c> 走在最前面，然后才是「OKAY 能用且按了 <c>DEFAULT_OKAY</c>」。端口把这两条放在
/// <see cref="EditorSpellSelectionInput.Process"/> 之前，顺序与参考一致——右键提示的那几处不会抢在关窗之前。
/// </para>
/// <para>
/// 内层窗（四个按钮与等级键的说明、法术说明）摞在这个类自己的栈上，与 <see cref="EditorMapSpecsModal"/> 同一条路：
/// 参考在循环里直接调 <c>showStandardTextMessage</c> 再回到循环，端口不能在这里递归
/// <c>GameHost.RunModal</c>（那里已经开着窗口就抛），只能自己摞一层。
/// </para>
/// <para>
/// 换个等级不是「关窗」也不是「确定」：参考 <c>:324-326</c> 在等级键上 <c>break</c>，既不写回列表也不再画，
/// 由调用方按新的 <c>spellLevel</c> 重开一层（<c>editor_castle_details_window.cpp:515</c> 的 <c>while</c>）。
/// 端口把它表示成 <see cref="EditorSpellSelectionOutcome.LevelChanged"/>，重开的活由
/// <see cref="EditorSceneHost.RunSpellSelection"/> 干。
/// </para>
/// </remarks>
internal sealed class EditorSpellSelectionModal : IModalScreen
{
    private readonly EditorSpellSelectionWindow _window;
    private readonly EditorSpellSelectionInput _input;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the window and its state machine.</summary>
    /// <param name="window">The window being run.</param>
    /// <param name="input">The state machine fed by the window's events.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorSpellSelectionModal(EditorSpellSelectionWindow window, EditorSpellSelectionInput input,
        EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _input = input;
        _host = host;
    }

    /// <summary>How the author left the window.</summary>
    public EditorSpellSelectionOutcome Outcome { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The window being run.</summary>
    public EditorSpellSelectionWindow Window => _window;

    /// <summary>The level the window shows after the last frame.</summary>
    public int Level => _window.Level;

    /// <summary>
    /// The spells handed back, <c>:418-429</c>; empty when the window was cancelled or asked for another level.
    /// </summary>
    public IReadOnlyList<SpellType> Spells { get; private set; } = [];

    /// <summary>Whether a message or a spell description is stacked above the selection window.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Top?.HotKey(hotKey);

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Paint(frame.Pixels, LocalEvent.Current);

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

        // 内层窗还开着：这一帧只归它，选择窗既不读事件也不改状态。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        // 参考 :280-284 的顺序：先取消，再确定；两个都在鼠标之前。
        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel))
        {
            Finish(EditorSpellSelectionOutcome.Cancelled);

            return;
        }

        if (_window.IsOkayEnabled && GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay))
        {
            Finish(EditorSpellSelectionOutcome.Accepted);

            return;
        }

        EditorSpellSelectionOutcome outcome = _input.Process(LocalEvent.Current);

        if (outcome != EditorSpellSelectionOutcome.None)
        {
            Finish(outcome);

            return;
        }

        if (!_input.Request.IsNone)
        {
            Open(_input.Request);
        }
    }

    /// <summary>Opens the window a request of the spell selection window asks for.</summary>
    /// <param name="request">The request; already consumed by the state machine.</param>
    private void Open(EditorSpellSelectionRequest request)
    {
        // 右键点法术要的是它的说明框（SpellDialogElement::showPopup()），说明框自己是模态，摞一层等它关。
        IModalScreen? window = request.Kind switch
        {
            EditorSpellSelectionRequestKind.ShowMessage => _host.TryCreateMessageBox(request.Title, request.Body),
            EditorSpellSelectionRequestKind.ShowSpell => _host.TryCreateSpellInfo(request.Spell),
            _ => null,
        };

        if (window is null)
        {
            GameLog.Warn($"The spell selection window asked for {request.Kind}, and no window answers it yet.");

            return;
        }

        _inner.Push(window);
    }

    /// <summary>Ends the window; the accepted spells are read off the window, as in the reference.</summary>
    /// <param name="outcome">How the window was left.</param>
    private void Finish(EditorSpellSelectionOutcome outcome)
    {
        Outcome = outcome;
        IsFinished = true;

        // 换等级时参考不写回任何列表（:324-326 直接 break），所以这里也只认「确定」。
        Spells = outcome == EditorSpellSelectionOutcome.Accepted ? _window.GetSelectedSpells() : [];
    }
}
