using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The castle settings window of the editor run on the host's frame pump,
/// <c>openCastleDialog()</c> of <c>editor_castle_details_window.cpp:790-1100</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考是个阻塞函数：循环里再开 <c>Dialog::inputString</c>（改名）、<c>showStandardTextMessage</c>（右键说明与未保存确认）、
/// <c>mageGuildSpellsDialog</c>（禁法）。端口只有一条帧循环，内层窗摞在这个类自己的栈上，与
/// <see cref="EditorMapSpecsModal"/> 同构：栈上有窗口时只更新栈顶、输入也只转给栈顶。
/// </para>
/// <para>
/// 军队条不是窗口：参考在光标落进条里时直接 <c>armyBar.QueueEventProcessing( &amp;message )</c>（<c>:992-1003</c>），
/// 端口照办——把这一帧喂给 <see cref="CastleDetailsView.ArmyBar"/>，它自己管选中与拖放，需要宿主回答的部分
/// （拆兵、兵种说明、编辑器加兵）留在 <see cref="ArmyEvent"/> 上。
/// </para>
/// <para>
/// <b>已知缺口</b>：Set Spells 那颗按钮的魔法行会窗（<c>mageGuildSpellsDialog</c>）还没接，点下去只留一条日志。
/// </para>
/// </remarks>
internal sealed class EditorCastleDetailsModal : IModalScreen
{
    /// <summary>
    /// Longest castle name the window takes, <c>Maps::Map_Format::nameCharLimit</c> (<c>map_format_info.h:60</c>).
    /// </summary>
    public const int NameCharacterLimit = 30;

    /// <summary>Body of the rename window, <c>_( "Enter Castle name:" )</c> of <c>:885</c>.</summary>
    public const string EnterNameBody = "Enter Castle name:";

    private readonly CastleDetailsInput _input;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private CastleDetailsRequest _pending = CastleDetailsRequest.None;

    /// <summary>Wraps the window and its state machine.</summary>
    /// <param name="input">The state machine fed by the window's events.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorCastleDetailsModal(CastleDetailsInput input, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(host);

        _input = input;
        _host = host;
    }

    /// <summary>The state machine being run.</summary>
    public CastleDetailsInput Input => _input;

    /// <summary>The window being painted.</summary>
    public CastleDetailsView View => _input.View;

    /// <summary>How the author left the window; <see cref="CastleDetailsOutcome.None"/> while it is still open.</summary>
    public CastleDetailsOutcome Outcome => _input.Outcome;

    /// <summary>
    /// What the army strip asked the host for in the last frame it was fed, or <see langword="null"/> when it asked
    /// nothing. Splitting, the unit information window and the editor's monster chooser are the answers the caller
    /// has to give; the bar's own selection and drags need none.
    /// </summary>
    public ArmyBarEvent? ArmyEvent { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => _inner.Count > 0 ? _inner.Peek() : null;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => TopWindow;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey)
    {
        if (Top is { } inner)
        {
            inner.HotKey(hotKey);

            return;
        }

        // :851-867 的两支热键与点 OKAY、点 EXIT 是同一条路，连未保存确认也一起走。
        CastleDetailsOutcome outcome = hotKey switch
        {
            ButtonHotKey.Okay => _input.Accept(),
            ButtonHotKey.Cancel => _input.RequestDiscard(),
            _ => CastleDetailsOutcome.None,
        };

        if (outcome != CastleDetailsOutcome.None)
        {
            IsFinished = true;

            return;
        }

        if (!_input.Request.IsNone)
        {
            Open(_input.Request);
        }
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        View.SetMessage(_input.Message);
        View.Paint(frame.Pixels);

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

        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
                Resolve(inner);
            }

            return;
        }

        LocalEvent events = LocalEvent.Current;

        // 参考每帧先刷六颗按钮的按压态再判事件（:845-849、:876）。
        View.UpdateButtonPressStates(events);

        CastleDetailsOutcome outcome = _input.Process(events);

        if (outcome != CastleDetailsOutcome.None)
        {
            IsFinished = true;

            return;
        }

        if (!_input.Request.IsNone)
        {
            Open(_input.Request);
        }
    }

    /// <summary>Answers a request of the window, <c>_queueEvent</c> of the reference.</summary>
    /// <param name="request">The request; already consumed by the window state machine.</param>
    private void Open(CastleDetailsRequest request)
    {
        // 军队条不是窗口：光标在条里时每帧都喂它一次，参考在这里同样只是一次 QueueEventProcessing（:992-1003）。
        if (request.Kind == CastleDetailsRequestKind.Army)
        {
            ArmyBarEvent result = View.ArmyBar.ProcessEvents(LocalEvent.Current);

            ArmyEvent = result.Handled ? result : null;

            if (result.Info is { } info)
            {
                // :992-1003：说明窗归军队条自己弹（army_bar.cpp 的双击分支），答案再由军队条回填。
                if (_host.TryCreateArmyInfo(info) is { } infoWindow)
                {
                    _pending = request;
                    _inner.Push(infoWindow);

                    return;
                }

                // 窗口造不出来就照参考「当没点过」，军队条自己已经把事件吃掉了，这一帧什么都不做。
                GameLog.Warn("The army bar asked for the unit information window, and it cannot be built.");
            }

            return;
        }

        IModalScreen? window = request.Kind switch
        {
            CastleDetailsRequestKind.EditName => TryCreateNameInput(),
            CastleDetailsRequestKind.ShowMessage => TryCreateMessageBox(request),
            CastleDetailsRequestKind.SetSpells => TryCreateMageGuildSpells(),
            _ => null,
        };

        if (window is null)
        {
            GameLog.Warn($"The castle settings window asked for {request.Kind}, and no window answers it yet.");

            return;
        }

        _pending = request;
        _inner.Push(window);
    }

    /// <summary>Feeds the answer of a closed inner window back into the window state machine.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        CastleDetailsRequest request = _pending;
        _pending = CastleDetailsRequest.None;

        if (closed is InputStringModal input)
        {
            // :882-890：确定且不是空串才写回，取消什么都不写。
            if (input.Window.Result is { Length: > 0 } name)
            {
                _input.State.Metadata.CustomName = name;
            }

            return;
        }

        if (closed is ArmyInfoModal armyInfo)
        {
            // :992-1003：说明窗的答案（升级 / 解散 / 取消）交给军队条，升级与解散都改军队本身。
            ArmyBar.ApplyInfoAnswer(armyInfo.Dialog.Troop, armyInfo.Answer);

            return;
        }

        if (closed is MessageBoxModal box && request.Body == CastleDetailsInput.UnsavedChangesMessage)
        {
            // :862-866：确认框是 OK | CANCEL，OK 表示「放弃改动」。
            CastleDetailsOutcome outcome = _input.ResolveUnsavedChanges(box.Answer == DialogButton.Ok);

            if (outcome != CastleDetailsOutcome.None)
            {
                IsFinished = true;
            }

            return;
        }

        if (closed is MageGuildSpellsModal)
        {
            // :383-605：法术表在确认时已经写回元数据，取消则什么都没动，这里没有要回填的。
            return;
        }

        if (request.Kind != CastleDetailsRequestKind.ShowMessage)
        {
            GameLog.Warn($"The answer of {request.Kind} dropped on the floor.");
        }
    }

    /// <summary>
    /// Builds the mage guild spells sub-window the SET SPELLS button opens, <c>mageGuildSpellsDialog()</c>
    /// (<c>editor_castle_details_window.cpp:383-605</c>).
    /// </summary>
    /// <returns>The window, or <see langword="null"/> when the artwork or the data is missing.</returns>
    private MageGuildSpellsModal? TryCreateMageGuildSpells() =>
        _host.TryCreateMageGuildSpells(_input.State, View.DialogArea);

    /// <summary>Builds the rename window, the <c>Dialog::inputString</c> of <c>:882-890</c>.</summary>
    /// <returns>The window, or <see langword="null"/> when the fonts or the input frame are missing.</returns>
    private InputStringModal? TryCreateNameInput()
    {
        // 参考传的标题是空的，正文才是那句提示；初值就是当前的名字。
        InputStringDialog dialog = InputStringDialog.Create(string.Empty, EnterNameBody, NameCharacterLimit,
            isMultiLine: false, initialText: _input.State.Metadata.CustomName);

        return _host.TryCreateInput(dialog);
    }

    /// <summary>Builds the box a right click or the exit confirmation asks for.</summary>
    /// <param name="request">The request that named the box.</param>
    /// <returns>The box, or <see langword="null"/> when the fonts or the buttons are missing.</returns>
    private MessageBoxModal? TryCreateMessageBox(CastleDetailsRequest request)
    {
        // 右键说明是 Dialog::ZERO（只要 OK），未保存确认是 Dialog::OK | CANCEL（:862）。
        DialogButton buttons = request.Body == CastleDetailsInput.UnsavedChangesMessage
            ? DialogButton.Ok | DialogButton.Cancel
            : DialogButton.Ok;

        return _host.TryCreateMessageBox(request.Title, request.Body, buttons);
    }
}
