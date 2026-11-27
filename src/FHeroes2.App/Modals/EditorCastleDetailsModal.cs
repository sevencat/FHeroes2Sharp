using System.Globalization;
using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;
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
/// （拆兵、兵种说明、编辑器加兵）留在 <see cref="ArmyEvent"/> 上。说明窗与加兵的两层窗（选怪兽、问数量）都摞在
/// 这个类的栈上。
/// </para>
/// <para>
/// <b>已知缺口</b>：Set Spells 那颗按钮的魔法行会窗（<c>mageGuildSpellsDialog</c>）与拆兵数量窗还没接，
/// 前者点下去只留一条日志，后者的事件只落在 <see cref="ArmyEvent"/> 上。
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

    /// <summary>
    /// Header of the count window the add-troop flow opens, <c>_( "Set %{monster} Count:" )</c> of
    /// <c>army_bar.cpp:479</c>; <c>{0}</c> is the creature's name.
    /// </summary>
    public const string AddTroopCountHeaderFormat = "Set {0} Count:";

    /// <summary>
    /// Largest stack the editor adds to an empty slot, the <c>500000</c> of <c>army_bar.cpp:485</c>.
    /// </summary>
    public const int MaximumAddedTroopCount = 500_000;

    private readonly CastleDetailsInput _input;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private CastleDetailsRequest _pending = CastleDetailsRequest.None;

    /// <summary>The empty slot an add-troop flow is filling, or <see langword="null"/> when none is running.</summary>
    private ArmyBarAddTroop? _addTroop;

    /// <summary>The creature chosen for <see cref="_addTroop"/>, awaiting its count.</summary>
    private MonsterType _addTroopMonster = MonsterType.Unknown;

    /// <summary>The split question an open <see cref="ArmySplitModal"/> is answering, or <see langword="null"/>.</summary>
    private ArmyBarSplit? _split;

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
            ArmyBarEvent result = View.ArmyBar.ProcessEvents(LocalEvent.Current, CurrentArmyHotKey());

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

            if (result.AddTroop is { } addTroop)
            {
                // army_bar.cpp:448-489：编辑器点空槽先选怪兽，再问数量，最后自己 Troop::Set()。
                if (_host.TryCreateMonsterSelect(addTroop.SuggestedMonster) is { } picker)
                {
                    _addTroop = addTroop;
                    _pending = request;
                    _inner.Push(picker);

                    return;
                }

                GameLog.Warn("The army bar asked to add a troop, and the creature picker cannot be built.");
            }

            if (result.Split is { } splitQuestion)
            {
                // army_bar.cpp:96-142：军队条给出五个参数，窗口回两个答案，再由 ApplySplit 落账。
                if (_host.TryCreateArmySplit(splitQuestion) is { } splitWindow)
                {
                    _split = splitQuestion;
                    _pending = request;
                    _inner.Push(splitWindow);

                    return;
                }

                GameLog.Warn("The army bar asked how to split a stack, and the split window cannot be built.");
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

        if (closed is MonsterSelectModal picker)
        {
            OpenAddTroopCount(picker.SelectedMonster);

            return;
        }

        if (closed is SelectCountModal count)
        {
            FillAddTroopSlot(count.Result);

            return;
        }

        if (closed is ArmySplitModal splitWindow)
        {
            ResolveSplit(splitWindow);

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

    /// <summary>
    /// Opens the count window of the slot being filled, <c>Dialog::SelectCount( str, 1, 500000, count, 1, &amp;monsUi )</c>
    /// (<c>army_bar.cpp:478-486</c>).
    /// </summary>
    /// <remarks>
    /// 参考在怪兽选择窗返回 <c>Monster::UNKNOWN</c>（取消）时什么都不做，端口一样：槽保持空着，军队条这一帧的
    /// 点击已经消化掉了。数量窗的范围与初值照参考：1 到 500000，初值 1。
    /// </remarks>
    /// <param name="monster">The creature the picker answered, or <see cref="MonsterType.Unknown"/> when cancelled.</param>
    private void OpenAddTroopCount(MonsterType monster)
    {
        if (monster == MonsterType.Unknown || _addTroop is null)
        {
            _addTroop = null;

            return;
        }

        string header = string.Format(CultureInfo.InvariantCulture, AddTroopCountHeaderFormat,
            MonsterNames.GetSingular(monster));
        SelectCountDialog dialog = SelectCountDialog.Create(header, 1, MaximumAddedTroopCount, 1);

        if (_host.TryCreateSelectCount(dialog, initialValue: 1) is not { } window)
        {
            GameLog.Warn($"The creature picker chose {monster}, and the count window cannot be built.");
            _addTroop = null;

            return;
        }

        _addTroopMonster = monster;
        _inner.Push(window);
    }

    /// <summary>
    /// Writes the chosen amount into the empty slot, the <c>troop.Set( mons, count )</c> of
    /// <c>army_bar.cpp:486-487</c>.
    /// </summary>
    /// <param name="amount">The number the count window answered, or <see langword="null"/> when cancelled.</param>
    private void FillAddTroopSlot(int? amount)
    {
        ArmyBarAddTroop? addTroop = _addTroop;
        MonsterType monster = _addTroopMonster;

        _addTroop = null;
        _addTroopMonster = MonsterType.Unknown;

        if (amount is not { } count || addTroop is not { } slot || monster == MonsterType.Unknown)
        {
            return;
        }

        slot.Troop.Set(monster, (uint)count);
    }

    /// <summary>
    /// Feeds the answer of an open <see cref="ArmySplitModal"/> back into the army bar, the two out parameters of
    /// <c>Dialog::ArmySplitTroop()</c> and the <c>ApplySplit()</c> that follows (<c>army_bar.cpp:103-142</c>).
    /// </summary>
    /// <remarks>
    /// 取消时参考什么都不做（<c>slots &lt; 2</c> 直接 return），端口的 EXIT 与取消同一条路。
    /// </remarks>
    /// <param name="window">The split window that just closed.</param>
    private void ResolveSplit(ArmySplitModal window)
    {
        ArmyBarSplit? split = _split;

        _split = null;

        if (split is null || window.Answer != DialogButton.Ok)
        {
            return;
        }

        _ = ArmyBar.ApplySplit(split, window.SlotCount, window.UseFastSplit, window.Count);
    }

    /// <summary>
    /// The split or join key the player is holding, the three <c>Game::HotKeyHoldEvent()</c> queries of the army bar
    /// in <c>army_bar.cpp:123</c>.
    /// </summary>
    /// <remarks>
    /// 参考的判定是一条 else-if 链，所以同时按住多个键时只认第一个；默认绑定是 Shift（拆半）、Ctrl（拆一）、
    /// Alt（合并），可在热键页里改，这里统一问 <see cref="GameHotKeys"/>，不读具体键。
    /// </remarks>
    /// <returns>The key state the bar acts on this frame.</returns>
    private static ArmyBarHotKey CurrentArmyHotKey()
    {
        if (GameHotKeys.HotKeyHoldEvent(HotKeyEvent.ArmySplitStackByHalf))
        {
            return ArmyBarHotKey.SplitStackByHalf;
        }

        if (GameHotKeys.HotKeyHoldEvent(HotKeyEvent.ArmySplitStackByOne))
        {
            return ArmyBarHotKey.SplitStackByOne;
        }

        return GameHotKeys.HotKeyHoldEvent(HotKeyEvent.ArmyJoinStacks)
            ? ArmyBarHotKey.JoinStacks
            : ArmyBarHotKey.None;
    }

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
