using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The mage guild spells sub-window of the castle settings, <c>mageGuildSpellsDialog()</c>
/// (<c>editor_castle_details_window.cpp:383-605</c>), as a modal that runs its own windows.
/// </summary>
/// <remarks>
/// <para>
/// 参考在这个窗口的循环里直接调三个模态：法术选择器（<c>Dialog::selectSpell</c>）、禁用清单
/// （<c>Editor::openSpellSelectionWindow</c>）与消息框。端口不能在这里再调 <c>GameHost.RunModal</c>——那里已经开着
/// 城堡详情窗——所以这些窗都摞在这个类自己的栈上，关掉一层就把结果回填进
/// <see cref="MageGuildSpellsInput"/>，再回到法术行窗。
/// </para>
/// <para>
/// 禁用清单换等级时参考不写回这一层（<c>:515-521</c> 换掉 <c>currentBannedSpellsLevel</c> 再重开），端口照做：
/// 栈上再压一层新等级的窗，旧的那层结果丢掉。
/// </para>
/// </remarks>
internal sealed class MageGuildSpellsModal : IModalScreen
{
    /// <summary>Heading of the "nothing to pick" message, the <c>_( "Warning" )</c> of the picker.</summary>
    private const string WarningTitle = "Warning";

    private readonly MageGuildSpellsInput _input;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private MageGuildSpellsRequest _pending = MageGuildSpellsRequest.None;

    /// <summary>Wires the sub-window up.</summary>
    /// <param name="view">The window being run.</param>
    /// <param name="input">The state machine fed by the window's events.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MageGuildSpellsModal(MageGuildSpellsView view, MageGuildSpellsInput input, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(host);

        View = view;
        _input = input;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public MageGuildSpellsView View { get; }

    /// <summary>The state machine behind the window, for the caller that wants the table after the fact.</summary>
    public MageGuildSpellsInput Input => _input;

    /// <summary>How the author left the window.</summary>
    public MageGuildSpellsOutcome Outcome => _input.Outcome;

    /// <summary>Whether the metadata really changed; the caller reads it after the window closed.</summary>
    public bool HasChanges => _input.HasChanges;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether a picker, a description or a message is stacked above the rows.</summary>
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

        View.Paint(frame.Pixels, LocalEvent.Current);

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

        // 内层窗还开着：这一帧只归它，法术行窗既不读事件也不改状态。
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

        // 参考 :443-448 的顺序：先取消，再确定；两个都在鼠标之前。
        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel))
        {
            _ = _input.Cancel();
            Finish();

            return;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay))
        {
            _ = _input.Accept();
            Finish();

            return;
        }

        MageGuildSpellsOutcome outcome = _input.Process(LocalEvent.Current);

        if (outcome != MageGuildSpellsOutcome.None)
        {
            Finish();

            return;
        }

        if (_input.Request.Kind != MageGuildSpellsRequestKind.None)
        {
            Open(_input.Request);
        }
    }

    /// <summary>Opens the window a request of the sub-window asks for.</summary>
    /// <param name="request">The request; already consumed by the state machine.</param>
    private void Open(MageGuildSpellsRequest request)
    {
        IModalScreen? window = request.Kind switch
        {
            MageGuildSpellsRequestKind.ShowMessage => _host.TryCreateMessageBox(request.Title, request.Body),
            MageGuildSpellsRequestKind.ShowSpell => _host.TryCreateSpellInfo(request.Spell),
            MageGuildSpellsRequestKind.SelectSpell => TryCreateSpellPicker(request),
            MageGuildSpellsRequestKind.SelectBannedSpells => TryCreateBannedSpells(request),
            _ => null,
        };

        if (window is null)
        {
            GameLog.Warn($"The mage guild spells window asked for {request.Kind}, and no window answers it.");

            return;
        }

        _pending = request;
        _inner.Push(window);
    }

    /// <summary>
    /// Opens the picker of one row, <c>Dialog::selectSpell()</c> (<c>:496-501</c>); when the row offers nothing to
    /// pick the reference only shows a message (<c>dialog_selectitems.cpp:1130-1133</c>).
    /// </summary>
    /// <param name="request">The request, holding the level, the current spell and the exclusions.</param>
    /// <returns>The window to run.</returns>
    private IModalScreen? TryCreateSpellPicker(MageGuildSpellsRequest request)
    {
        if (SpellSelectDialog.BuildSpellList(includeRandomSpells: true,
                new HashSet<int>(request.ExcludedSpells), request.SpellLevel).Count == 0)
        {
            return _host.TryCreateMessageBox(WarningTitle, SpellSelectDialog.EmptyListMessage);
        }

        return _host.TryCreateSpellSelect(request.SpellLevel, request.CurrentSpell, request.ExcludedSpells);
    }

    /// <summary>
    /// Opens the banned spells window of a level, the <c>Editor::openSpellSelectionWindow</c> call of
    /// <c>:515-521</c>: the banned list goes in, the disabled ones come back.
    /// </summary>
    /// <param name="request">The request, holding the level, its title and its banned spells.</param>
    /// <returns>The window to run.</returns>
    private IModalScreen? TryCreateBannedSpells(MageGuildSpellsRequest request) =>
        _host.TryCreateSpellSelection(request.Title, request.SpellLevel,
            [.. request.BannedSpells.Select(id => (SpellType)id)],
            isMultiLevelSelectionEnabled: true,
            minimumEnabledSpells: View.Table.GetSlotCount(request.SpellLevel),
            pickDisabledSpells: true);

    /// <summary>Hands the answer of an inner window back to the state machine.</summary>
    /// <param name="closed">The window that just closed.</param>
    private void Resolve(IModalScreen closed)
    {
        MageGuildSpellsRequest request = _pending;

        _pending = MageGuildSpellsRequest.None;

        switch (closed)
        {
            case SpellSelectModal picker:
                _input.ApplySelectedSpell(picker.SelectedSpell);

                return;

            case EditorSpellSelectionModal selection:
                ResolveBannedSpells(selection);

                return;

            default:
                if (request.Kind is not (MageGuildSpellsRequestKind.ShowMessage
                    or MageGuildSpellsRequestKind.ShowSpell))
                {
                    GameLog.Warn($"The answer of {request.Kind} dropped on the floor.");
                }

                return;
        }
    }

    /// <summary>
    /// Writes the banned spells back, or reopens the window when the author asked for another level
    /// (<c>editor_castle_details_window.cpp:515-549</c>).
    /// </summary>
    /// <param name="selection">The closed selection window.</param>
    private void ResolveBannedSpells(EditorSpellSelectionModal selection)
    {
        if (selection.Outcome == EditorSpellSelectionOutcome.LevelChanged)
        {
            // 换等级：参考把 currentBannedSpellsLevel 换掉再重开一层，这一层的结果不算数。
            if (selection.Level == _input.CurrentBannedSpellsLevel)
            {
                return;
            }

            _input.FocusBannedSpellsLevel(selection.Level);

            MageGuildSpellsRequest reopened = MageGuildSpellsRequest.SelectBannedSpells(selection.Level,
                CastleDetailsLabels.GetMageGuildLevelTitle(selection.Level) + ":",
                GetBannedSpellsOf(selection.Level));

            if (TryCreateBannedSpells(reopened) is { } window)
            {
                _pending = reopened;
                _inner.Push(window);
            }

            return;
        }

        if (selection.Outcome == EditorSpellSelectionOutcome.Accepted)
        {
            _input.ApplyBannedSpells(selection.Level, [.. selection.Spells.Select(spell => (int)spell)]);
        }
    }

    /// <summary>The banned spells of one level, the list the reopened window has to start from.</summary>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <returns>The spell ids.</returns>
    private List<int> GetBannedSpellsOf(int spellLevel) =>
        MageGuildSpellTable.GroupByLevel(View.Table.BannedSpells)[spellLevel - 1];

    /// <summary>Ends the window; the outcome is the one the state machine just set.</summary>
    private void Finish() => IsFinished = true;
}
